using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class VoiceSplice : EditorWindow
{
    private const string BackupFolder = "VoiceBackups";

    private AudioClip target;
    private AudioClip source;
    private float targetStart = 1.225f, targetEnd = 1.425f, sourceStart = 3.90f;
    private float crossfade = 0.012f;
    private bool matchEnvelope = true;
    private Wav targetWav, sourceWav;
    private Texture2D targetWave, sourceWave;
    private string status = "";

    private sealed class Wav
    {
        public int channels, rate, bits, format;
        public float[] samples;
        public int Frames => samples.Length / channels;
        public float Seconds => Frames / (float)rate;
    }

    [MenuItem("Tools/Leaves of War/Voice Fix (replace a sound in a voice clip)")]
    private static void Open()
    {
        var window = GetWindow<VoiceSplice>("Voice Fix");
        window.minSize = new Vector2(620f, 520f);
        if (window.target == null)
        {
            window.target = UngarettiLines.Clip("UNG_L4_track5");
            window.source = window.target;
            window.Load();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Replaces a stretch of a voice clip with a stretch of another (or the same) clip, with crossfades and the original loudness envelope.\n" +
            "Preset: \"Antonietta\" → \"Antonietto\" in UNG_L4_track5: the final 'a' (1.225–1.425 s) is replaced by the 'o' of \"nove\" (from 3.90 s) in the same take.\n" +
            "The original is backed up in " + BackupFolder + " (next to the project's Assets) and can be restored.", MessageType.None);
        EditorGUI.BeginChangeCheck();
        target = (AudioClip)EditorGUILayout.ObjectField("Clip to fix", target, typeof(AudioClip), false);
        source = (AudioClip)EditorGUILayout.ObjectField("Take the sound from", source, typeof(AudioClip), false);
        if (EditorGUI.EndChangeCheck())
            Load();
        if (targetWav == null || sourceWav == null)
        {
            EditorGUILayout.HelpBox(status.Length > 0 ? status : "Pick two .wav voice clips.", MessageType.Warning);
            return;
        }

        GUILayout.Label("Clip to fix: the stretch to replace", EditorStyles.boldLabel);
        DrawWave(targetWave, targetWav.Seconds, targetStart, targetEnd);
        EditorGUILayout.MinMaxSlider(ref targetStart, ref targetEnd, 0f, targetWav.Seconds);
        using (new EditorGUILayout.HorizontalScope())
        {
            targetStart = EditorGUILayout.FloatField("From (s)", targetStart);
            targetEnd = EditorGUILayout.FloatField("To (s)", targetEnd);
            if (GUILayout.Button("▶ Play stretch", GUILayout.Width(110f)))
                Play(targetWav, targetStart - 0.25f, targetEnd + 0.25f);
        }
        float length = Mathf.Max(0.01f, targetEnd - targetStart);

        GUILayout.Label("Sound to put in its place (same length: " + length.ToString("F3") + " s)", EditorStyles.boldLabel);
        DrawWave(sourceWave, sourceWav.Seconds, sourceStart, sourceStart + length);
        sourceStart = EditorGUILayout.Slider("From (s)", sourceStart, 0f, Mathf.Max(0f, sourceWav.Seconds - length));
        if (GUILayout.Button("▶ Play sound to insert", GUILayout.Width(180f)))
            Play(sourceWav, sourceStart, sourceStart + length);

        crossfade = EditorGUILayout.Slider("Crossfade (s)", crossfade, 0.002f, Mathf.Min(0.05f, length * 0.45f));
        matchEnvelope = EditorGUILayout.Toggle("Keep the original loudness", matchEnvelope);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("▶ Preview the result", GUILayout.Height(26f)))
            {
                Wav result = Splice();
                Play(result, targetStart - 1.2f, targetEnd + 1.5f);
            }
            if (GUILayout.Button("Save into the clip", GUILayout.Height(26f)))
                Save();
            if (GUILayout.Button("Restore the original", GUILayout.Height(26f)))
                Restore();
        }
        if (status.Length > 0)
            EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    private void Load()
    {
        status = "";
        targetWav = Read(target);
        sourceWav = Read(source);
        targetWave = Waveform(targetWav);
        sourceWave = Waveform(sourceWav);
    }

    private Wav Splice()
    {
        Wav result = new Wav { channels = targetWav.channels, rate = targetWav.rate, bits = targetWav.bits, format = targetWav.format, samples = (float[])targetWav.samples.Clone() };
        int ch = targetWav.channels;
        int i0 = Mathf.Clamp(Mathf.RoundToInt(targetStart * targetWav.rate), 0, targetWav.Frames);
        int i1 = Mathf.Clamp(Mathf.RoundToInt(targetEnd * targetWav.rate), i0, targetWav.Frames);
        int length = i1 - i0;
        float ratio = sourceWav.rate / (float)targetWav.rate;
        var inserted = new float[length * ch];
        for (int f = 0; f < length; f++)
        {
            float at = sourceStart * sourceWav.rate + f * ratio;
            int a = Mathf.Clamp((int)at, 0, sourceWav.Frames - 1), b = Mathf.Min(a + 1, sourceWav.Frames - 1);
            float t = at - a;
            for (int c = 0; c < ch; c++)
            {
                int sc = Mathf.Min(c, sourceWav.channels - 1);
                inserted[f * ch + c] = Mathf.Lerp(sourceWav.samples[a * sourceWav.channels + sc], sourceWav.samples[b * sourceWav.channels + sc], t);
            }
        }
        if (matchEnvelope)
        {
            int frame = Mathf.Max(1, targetWav.rate / 200);
            int count = Mathf.CeilToInt(length / (float)frame);
            var gains = new float[count];
            for (int k = 0; k < count; k++)
            {
                double et = 0, es = 0;
                int n = 0;
                for (int f = k * frame; f < Mathf.Min(length, (k + 1) * frame); f++, n++)
                    for (int c = 0; c < ch; c++)
                    {
                        float o = targetWav.samples[(i0 + f) * ch + c], s = inserted[f * ch + c];
                        et += o * o;
                        es += s * s;
                    }
                gains[k] = es > 1e-12 ? Mathf.Clamp((float)Math.Sqrt(et / es), 0f, 4f) : 0f;
            }
            var smooth = new float[count];
            for (int k = 0; k < count; k++)
            {
                float sum = 0f;
                int n = 0;
                for (int j = Mathf.Max(0, k - 2); j <= Mathf.Min(count - 1, k + 2); j++, n++)
                    sum += gains[j];
                smooth[k] = sum / n;
            }
            for (int f = 0; f < length; f++)
            {
                float pos = f / (float)frame - 0.5f;
                int k = Mathf.Clamp(Mathf.FloorToInt(pos), 0, count - 1);
                float g = Mathf.Lerp(smooth[k], smooth[Mathf.Min(k + 1, count - 1)], Mathf.Clamp01(pos - k));
                for (int c = 0; c < ch; c++)
                    inserted[f * ch + c] *= g;
            }
        }
        int fade = Mathf.Max(1, Mathf.RoundToInt(crossfade * targetWav.rate));
        for (int f = 0; f < length; f++)
        {
            float w = Mathf.Min(1f, Mathf.Min(f / (float)fade, (length - 1 - f) / (float)fade));
            for (int c = 0; c < ch; c++)
            {
                int index = (i0 + f) * ch + c;
                result.samples[index] = Mathf.Lerp(targetWav.samples[index], inserted[f * ch + c], Mathf.Clamp01(w));
            }
        }
        return result;
    }

    private void Save()
    {
        string path = AssetDatabase.GetAssetPath(target);
        string backup = BackupPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup))
            File.Copy(path, backup);
        Write(Splice(), path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        status = "Saved into " + Path.GetFileName(path) + ". Every scene that uses it now plays the fixed version (same asset). Original kept in " + backup + ".";
        Load();
    }

    private void Restore()
    {
        string path = AssetDatabase.GetAssetPath(target);
        string backup = BackupPath(path);
        if (!File.Exists(backup))
        {
            status = "No backup of " + Path.GetFileName(path) + " yet.";
            return;
        }
        File.Copy(backup, path, true);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        status = "Original restored.";
        Load();
    }

    private static string BackupPath(string assetPath) =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath), BackupFolder, Path.GetFileName(assetPath)).Replace('\\', '/');

    private static Wav Read(AudioClip clip)
    {
        if (clip == null)
            return null;
        string path = AssetDatabase.GetAssetPath(clip);
        if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;
        byte[] data = File.ReadAllBytes(path);
        var wav = new Wav();
        int pos = 12;
        int dataStart = -1, dataLength = 0;
        while (pos + 8 <= data.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(data, pos, 4);
            int size = BitConverter.ToInt32(data, pos + 4);
            if (id == "fmt ")
            {
                wav.format = BitConverter.ToInt16(data, pos + 8);
                wav.channels = BitConverter.ToInt16(data, pos + 10);
                wav.rate = BitConverter.ToInt32(data, pos + 12);
                wav.bits = BitConverter.ToInt16(data, pos + 22);
                if (wav.format == -2 && size >= 26)
                    wav.format = BitConverter.ToInt16(data, pos + 32);
            }
            else if (id == "data")
            {
                dataStart = pos + 8;
                dataLength = Mathf.Min(size, data.Length - dataStart);
                break;
            }
            pos += 8 + size + (size & 1);
        }
        if (dataStart < 0 || wav.channels <= 0)
            return null;
        int bytes = wav.bits / 8;
        int count = dataLength / bytes;
        wav.samples = new float[count - count % wav.channels];
        for (int i = 0; i < wav.samples.Length; i++)
        {
            int at = dataStart + i * bytes;
            wav.samples[i] = wav.format == 3 && bytes == 4 ? BitConverter.ToSingle(data, at)
                : bytes == 2 ? BitConverter.ToInt16(data, at) / 32768f
                : bytes == 3 ? ((data[at] | data[at + 1] << 8 | (sbyte)data[at + 2] << 16) / 8388608f)
                : bytes == 4 ? BitConverter.ToInt32(data, at) / 2147483648f
                : (data[at] - 128) / 128f;
        }
        return wav;
    }

    private static void Write(Wav wav, string path)
    {
        int bits = wav.format == 3 ? 32 : wav.bits == 24 ? 24 : 16;
        int format = wav.format == 3 ? 3 : 1;
        int bytes = bits / 8;
        using (var stream = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(stream))
        {
            int dataLength = wav.samples.Length * bytes;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLength);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)format);
            w.Write((short)wav.channels);
            w.Write(wav.rate);
            w.Write(wav.rate * wav.channels * bytes);
            w.Write((short)(wav.channels * bytes));
            w.Write((short)bits);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLength);
            foreach (float s in wav.samples)
            {
                float v = Mathf.Clamp(s, -1f, 1f);
                if (format == 3)
                    w.Write(v);
                else if (bits == 24)
                {
                    int q = Mathf.Clamp(Mathf.RoundToInt(v * 8388608f), -8388608, 8388607);
                    w.Write((byte)(q & 255));
                    w.Write((byte)((q >> 8) & 255));
                    w.Write((byte)((q >> 16) & 255));
                }
                else
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32768f), -32768, 32767));
            }
        }
    }

    private static Texture2D Waveform(Wav wav)
    {
        if (wav == null)
            return null;
        const int width = 600, height = 70;
        var tex = new Texture2D(width, height) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.12f, 0.12f, 0.12f);
        int frames = wav.Frames;
        for (int x = 0; x < width; x++)
        {
            int a = x * frames / width, b = Mathf.Max(a + 1, (x + 1) * frames / width);
            float peak = 0f;
            for (int f = a; f < b; f++)
                peak = Mathf.Max(peak, Mathf.Abs(wav.samples[f * wav.channels]));
            int h = Mathf.Clamp(Mathf.RoundToInt(peak * height * 2.5f), 1, height);
            for (int y = (height - h) / 2; y < (height + h) / 2; y++)
                pixels[y * width + x] = new Color(0.55f, 0.75f, 0.95f);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void DrawWave(Texture2D wave, float seconds, float from, float to)
    {
        Rect r = GUILayoutUtility.GetRect(600f, 70f, GUILayout.ExpandWidth(true));
        if (wave != null)
            GUI.DrawTexture(r, wave, ScaleMode.StretchToFill);
        float x0 = r.x + r.width * Mathf.Clamp01(from / seconds), x1 = r.x + r.width * Mathf.Clamp01(to / seconds);
        EditorGUI.DrawRect(new Rect(x0, r.y, Mathf.Max(1f, x1 - x0), r.height), new Color(1f, 0.6f, 0.2f, 0.35f));
        for (int s = 0; s <= seconds; s++)
            EditorGUI.DrawRect(new Rect(r.x + r.width * s / seconds, r.yMax - 6f, 1f, 6f), Color.gray);
    }

    private static void Play(Wav wav, float from, float to)
    {
        from = Mathf.Max(0f, from);
        to = Mathf.Min(wav.Seconds, to);
        int a = Mathf.RoundToInt(from * wav.rate), b = Mathf.RoundToInt(to * wav.rate);
        if (b <= a)
            return;
        var part = new float[(b - a) * wav.channels];
        Array.Copy(wav.samples, a * wav.channels, part, 0, part.Length);
        AudioClip clip = AudioClip.Create("Voice Fix Preview", b - a, wav.channels, wav.rate, false);
        clip.SetData(part, 0);
        Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        if (util == null)
            return;
        util.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        MethodInfo play = util.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        play?.Invoke(null, new object[] { clip, 0, false });
    }
}
