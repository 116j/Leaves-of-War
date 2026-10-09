using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Minimal, self-contained GIF87a/89a decoder - no third-party plugin.
    /// Handles global/local color tables, LZW decompression, interlacing,
    /// per-frame delay and transparency, and the common disposal methods
    /// (leave-as-is and restore-to-background). Not a MonoBehaviour - call
    /// <see cref="Decode"/> directly with the raw file bytes.
    /// </summary>
    public static class GifDecoder
    {
        public readonly struct GifFrame
        {
            public GifFrame(Texture2D texture, float delaySeconds)
            {
                Texture = texture;
                DelaySeconds = delaySeconds;
            }

            public Texture2D Texture { get; }
            public float DelaySeconds { get; }
        }

        private const int MinimumFrameDelaySeconds = 2; // 1/100ths of a second, GIF's own units's floor
        private const float DefaultDelaySeconds = 0.1f;

        public static List<GifFrame> Decode(byte[] data)
        {
            var frames = new List<GifFrame>();
            if (data == null || data.Length < 13)
                return frames;

            int pos = 0;
            // Header: "GIF87a" or "GIF89a"
            if (data[0] != (byte)'G' || data[1] != (byte)'I' || data[2] != (byte)'F')
                return frames;
            pos = 6;

            int screenWidth = ReadUInt16(data, ref pos);
            int screenHeight = ReadUInt16(data, ref pos);
            byte packed = data[pos++];
            pos++; // background color index (unused - we composite on a transparent canvas)
            pos++; // pixel aspect ratio (unused)

            bool globalTableFlag = (packed & 0x80) != 0;
            int globalTableSize = 2 << (packed & 0x07);
            Color32[] globalTable = null;
            if (globalTableFlag)
            {
                globalTable = ReadColorTable(data, ref pos, globalTableSize);
            }

            var canvas = new Color32[screenWidth * screenHeight];
            for (int i = 0; i < canvas.Length; i++)
                canvas[i] = new Color32(0, 0, 0, 0);

            int gceDelay = -1;
            bool gceTransparentFlag = false;
            int gceTransparentIndex = -1;
            int gceDisposalMethod = 0;

            while (pos < data.Length)
            {
                byte introducer = data[pos++];

                if (introducer == 0x3B) // Trailer
                    break;

                if (introducer == 0x21) // Extension
                {
                    if (pos >= data.Length)
                        break;
                    byte label = data[pos++];

                    if (label == 0xF9) // Graphic Control Extension
                    {
                        byte blockSize = data[pos++];
                        byte gcePacked = data[pos];
                        gceDisposalMethod = (gcePacked >> 2) & 0x07;
                        gceTransparentFlag = (gcePacked & 0x01) != 0;
                        pos++;
                        int delayHundredths = ReadUInt16(data, ref pos);
                        gceTransparentIndex = data[pos++];
                        pos++; // block terminator
                        gceDelay = delayHundredths;
                    }
                    else
                    {
                        SkipSubBlocks(data, ref pos);
                    }
                    continue;
                }

                if (introducer == 0x2C) // Image Descriptor
                {
                    int imgLeft = ReadUInt16(data, ref pos);
                    int imgTop = ReadUInt16(data, ref pos);
                    int imgWidth = ReadUInt16(data, ref pos);
                    int imgHeight = ReadUInt16(data, ref pos);
                    byte idPacked = data[pos++];

                    bool localTableFlag = (idPacked & 0x80) != 0;
                    bool interlaced = (idPacked & 0x40) != 0;
                    int localTableSize = 2 << (idPacked & 0x07);

                    Color32[] colorTable = globalTable;
                    if (localTableFlag)
                        colorTable = ReadColorTable(data, ref pos, localTableSize);

                    if (colorTable == null)
                    {
                        // Malformed GIF - no color table at all; bail out safely.
                        break;
                    }

                    byte minCodeSize = data[pos++];
                    byte[] compressed = ReadSubBlocksConcatenated(data, ref pos);
                    byte[] indices = LzwDecode(compressed, minCodeSize, imgWidth * imgHeight);

                    // Blit onto the canvas, respecting transparency and interlacing.
                    int[] rowOrder = interlaced ? BuildInterlaceRowOrder(imgHeight) : null;
                    for (int row = 0; row < imgHeight; row++)
                    {
                        int sourceRow = interlaced ? rowOrder[row] : row;
                        int canvasY = imgTop + sourceRow;
                        if (canvasY < 0 || canvasY >= screenHeight)
                            continue;

                        for (int col = 0; col < imgWidth; col++)
                        {
                            int canvasX = imgLeft + col;
                            if (canvasX < 0 || canvasX >= screenWidth)
                                continue;

                            int srcIndex = row * imgWidth + col;
                            if (srcIndex >= indices.Length)
                                continue;

                            byte paletteIndex = indices[srcIndex];
                            if (gceTransparentFlag && paletteIndex == gceTransparentIndex)
                                continue; // Leave whatever was on the canvas showing through.

                            int paletteOffset = paletteIndex * 1;
                            if (paletteIndex >= colorTable.Length)
                                continue;

                            // GIF's canvas Y grows downward from the top, matching
                            // Texture2D's own row order when using SetPixels32 with
                            // row-major top-to-bottom data flipped at the end.
                            canvas[canvasY * screenWidth + canvasX] = colorTable[paletteIndex];
                        }
                    }

                    Texture2D frameTexture = BuildTexture(canvas, screenWidth, screenHeight);
                    float delaySeconds = gceDelay >= MinimumFrameDelaySeconds
                        ? gceDelay / 100f
                        : DefaultDelaySeconds;
                    frames.Add(new GifFrame(frameTexture, delaySeconds));

                    // Disposal method 2: restore the frame's own region to
                    // transparent before the NEXT frame draws. Other methods
                    // (0/1/3) are approximated as "leave the canvas as is",
                    // which covers the vast majority of simple loop gifs.
                    if (gceDisposalMethod == 2)
                    {
                        for (int row = 0; row < imgHeight; row++)
                        {
                            int canvasY = imgTop + row;
                            if (canvasY < 0 || canvasY >= screenHeight)
                                continue;
                            for (int col = 0; col < imgWidth; col++)
                            {
                                int canvasX = imgLeft + col;
                                if (canvasX < 0 || canvasX >= screenWidth)
                                    continue;
                                canvas[canvasY * screenWidth + canvasX] = new Color32(0, 0, 0, 0);
                            }
                        }
                    }

                    // Reset per-frame GCE state for the next image.
                    gceDelay = -1;
                    gceTransparentFlag = false;
                    gceTransparentIndex = -1;
                    gceDisposalMethod = 0;
                    continue;
                }

                // Unknown/corrupt block introducer - stop rather than loop forever.
                break;
            }

            return frames;
        }

        private static Texture2D BuildTexture(Color32[] canvas, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // GIF stores rows top-to-bottom; Texture2D.SetPixels32 expects
            // bottom-to-top, so flip vertically when copying out.
            var flipped = new Color32[canvas.Length];
            for (int row = 0; row < height; row++)
            {
                int srcRowStart = row * width;
                int dstRowStart = (height - 1 - row) * width;
                System.Array.Copy(canvas, srcRowStart, flipped, dstRowStart, width);
            }

            texture.SetPixels32(flipped);
            texture.Apply(false, true);
            return texture;
        }

        private static int[] BuildInterlaceRowOrder(int height)
        {
            // GIF interlacing draws in 4 passes: every 8th row starting at 0,
            // every 8th starting at 4, every 4th starting at 2, every 2nd
            // starting at 1 - in that pass order.
            var order = new int[height];
            int index = 0;
            for (int row = 0; row < height; row += 8) order[index++] = row;
            for (int row = 4; row < height; row += 8) order[index++] = row;
            for (int row = 2; row < height; row += 4) order[index++] = row;
            for (int row = 1; row < height; row += 2) order[index++] = row;
            return order;
        }

        private static Color32[] ReadColorTable(byte[] data, ref int pos, int entryCount)
        {
            var table = new Color32[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                byte r = data[pos++];
                byte g = data[pos++];
                byte b = data[pos++];
                table[i] = new Color32(r, g, b, 255);
            }
            return table;
        }

        private static void SkipSubBlocks(byte[] data, ref int pos)
        {
            while (pos < data.Length)
            {
                byte size = data[pos++];
                if (size == 0)
                    break;
                pos += size;
            }
        }

        private static byte[] ReadSubBlocksConcatenated(byte[] data, ref int pos)
        {
            var buffer = new List<byte>();
            while (pos < data.Length)
            {
                byte size = data[pos++];
                if (size == 0)
                    break;
                for (int i = 0; i < size; i++)
                    buffer.Add(data[pos++]);
            }
            return buffer.ToArray();
        }

        private static int ReadUInt16(byte[] data, ref int pos)
        {
            int value = data[pos] | (data[pos + 1] << 8);
            pos += 2;
            return value;
        }

        /// <summary>
        /// Standard GIF-variant LZW decompression (early-change code size
        /// growth). Returns one palette index per pixel, row-major.
        /// </summary>
        private static byte[] LzwDecode(byte[] data, int minCodeSize, int expectedPixelCount)
        {
            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            int codeSize = minCodeSize + 1;
            int nextCode = endCode + 1;

            var dictionary = new List<byte[]>(4096);
            void ResetDictionary()
            {
                dictionary.Clear();
                for (int i = 0; i < clearCode; i++)
                    dictionary.Add(new[] { (byte)i });
                dictionary.Add(null); // clear code slot
                dictionary.Add(null); // end code slot
                nextCode = endCode + 1;
                codeSize = minCodeSize + 1;
            }
            ResetDictionary();

            var output = new List<byte>(expectedPixelCount);
            int bitPos = 0;
            int totalBits = data.Length * 8;

            int ReadCode(int size)
            {
                int code = 0;
                for (int i = 0; i < size; i++)
                {
                    int bytePos = bitPos >> 3;
                    if (bytePos >= data.Length)
                        return endCode;
                    int bitOffset = bitPos & 7;
                    int bit = (data[bytePos] >> bitOffset) & 1;
                    code |= bit << i;
                    bitPos++;
                }
                return code;
            }

            byte[] previous = null;
            while (bitPos + codeSize <= totalBits && output.Count < expectedPixelCount)
            {
                int code = ReadCode(codeSize);

                if (code == clearCode)
                {
                    ResetDictionary();
                    previous = null;
                    continue;
                }

                if (code == endCode)
                    break;

                byte[] entry;
                if (code < dictionary.Count && dictionary[code] != null)
                {
                    entry = dictionary[code];
                }
                else if (code == nextCode && previous != null)
                {
                    entry = new byte[previous.Length + 1];
                    System.Array.Copy(previous, entry, previous.Length);
                    entry[previous.Length] = previous[0];
                }
                else
                {
                    break; // Corrupt/unsupported stream - stop cleanly.
                }

                output.AddRange(entry);

                if (previous != null && nextCode < 4096)
                {
                    var newEntry = new byte[previous.Length + 1];
                    System.Array.Copy(previous, newEntry, previous.Length);
                    newEntry[previous.Length] = entry[0];

                    if (nextCode < dictionary.Count)
                        dictionary[nextCode] = newEntry;
                    else
                        dictionary.Add(newEntry);
                    nextCode++;

                    if (nextCode == (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }

                previous = entry;
            }

            // Pad/truncate defensively so callers never index out of range.
            if (output.Count < expectedPixelCount)
            {
                while (output.Count < expectedPixelCount)
                    output.Add(0);
            }
            else if (output.Count > expectedPixelCount)
            {
                output.RemoveRange(expectedPixelCount, output.Count - expectedPixelCount);
            }

            return output.ToArray();
        }
    }
}