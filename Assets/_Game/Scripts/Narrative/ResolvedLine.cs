using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    public readonly struct ResolvedLine
    {
        // Existing 3-arg constructor kept so callers without segments still compile.
        public ResolvedLine(string lineId, string text, AudioClip clip)
            : this(lineId, text, clip, null, 1f)
        {
        }

        // Existing 4-arg constructor kept so callers without volume still compile.
        public ResolvedLine(string lineId, string text, AudioClip clip, SegmentTime[] segments)
            : this(lineId, text, clip, segments, 1f)
        {
        }

        public ResolvedLine(string lineId, string text, AudioClip clip, SegmentTime[] segments, float volume)
        {
            LineId = lineId;
            Text = text;
            Clip = clip;
            Segments = segments ?? Array.Empty<SegmentTime>();
            Volume = volume;
        }

        public string LineId { get; }
        public string Text { get; }
        public AudioClip Clip { get; }
        public float Volume { get; }

        // One entry per authored segment (text split on '|'), with audio start/end.
        public SegmentTime[] Segments { get; }

        public bool HasSegmentTimings => Segments != null && Segments.Length > 0;
    }
}