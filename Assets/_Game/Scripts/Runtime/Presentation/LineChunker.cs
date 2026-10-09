using System;
using System.Collections.Generic;

namespace Hortensia.Runtime
{
    public readonly struct LinePresentationChunk
    {
        public LinePresentationChunk(string text, bool pauseAfter)
        {
            Text = text;
            PauseAfter = pauseAfter;
        }

        public string Text { get; }
        public bool PauseAfter { get; }
    }

    public static class LineChunker
    {
        private static readonly string[] BeatMarkers =
        {
            "(beat)",
            "(beat, quieter)"
        };

        public static IReadOnlyList<LinePresentationChunk> Split(string text, int characterLimit)
        {
            var chunks = new List<LinePresentationChunk>();
            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            characterLimit = Math.Max(1, characterLimit);
            int cursor = 0;

            while (cursor < text.Length)
            {
                if (TryMarkerAt(text, cursor, out int markerLength))
                {
                    MarkPreviousForPause(chunks);
                    cursor += markerLength;
                    SkipWhitespace(text, ref cursor);
                    continue;
                }

                int markerIndex = FindNextMarker(text, cursor, out _);
                int segmentEnd = markerIndex >= 0 ? markerIndex : text.Length;
                AddSegment(text.Substring(cursor, segmentEnd - cursor), characterLimit, chunks);

                if (markerIndex < 0)
                    break;

                cursor = markerIndex;
            }

            return chunks;
        }

        private static void AddSegment(string segment, int limit, List<LinePresentationChunk> chunks)
        {
            segment = segment.Trim();
            while (segment.Length > 0)
            {
                int take = FindBoundary(segment, limit);
                string chunk = segment.Substring(0, take).Trim();
                if (chunk.Length > 0)
                    chunks.Add(new LinePresentationChunk(chunk, false));

                segment = segment.Substring(take).TrimStart();
            }
        }

        private static int FindBoundary(string text, int limit)
        {
            if (text.Length <= limit)
                return text.Length;

            int sentence = -1;
            int word = -1;
            int scanLimit = Math.Min(text.Length, limit);
            for (int i = 0; i < scanLimit; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                    word = i;

                if (IsSentenceEnd(c) && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))
                    sentence = i + 1;
            }

            if (sentence > 0)
                return sentence;
            if (word > 0)
                return word;

            return scanLimit;
        }

        private static bool IsSentenceEnd(char c) =>
            c == '.' || c == '!' || c == '?' || c == '\u2026';

        private static int FindNextMarker(string text, int start, out int markerLength)
        {
            int closest = -1;
            markerLength = 0;

            for (int i = 0; i < BeatMarkers.Length; i++)
            {
                string marker = BeatMarkers[i];
                int candidate = text.IndexOf(marker, start, StringComparison.OrdinalIgnoreCase);
                if (candidate >= 0 && (closest < 0 || candidate < closest))
                {
                    closest = candidate;
                    markerLength = marker.Length;
                }
            }

            return closest;
        }

        private static bool TryMarkerAt(string text, int index, out int markerLength)
        {
            for (int i = 0; i < BeatMarkers.Length; i++)
            {
                string marker = BeatMarkers[i];
                if (index + marker.Length <= text.Length &&
                    string.Compare(text, index, marker, 0, marker.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    markerLength = marker.Length;
                    return true;
                }
            }

            markerLength = 0;
            return false;
        }

        private static void MarkPreviousForPause(List<LinePresentationChunk> chunks)
        {
            if (chunks.Count == 0)
                return;

            LinePresentationChunk previous = chunks[chunks.Count - 1];
            chunks[chunks.Count - 1] = new LinePresentationChunk(previous.Text, true);
        }

        private static void SkipWhitespace(string text, ref int cursor)
        {
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                cursor++;
        }
    }
}
