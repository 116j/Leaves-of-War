using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    /// <summary>
    /// Start and end time (seconds) of one authored dialogue segment within its
    /// audio clip. One entry per text segment (the parts split on '|'). The audio
    /// plays from <see cref="start"/> to <see cref="end"/>; the start lets a
    /// segment skip a breath or gap before its line.
    /// </summary>
    [Serializable]
    public struct SegmentTime
    {
        [Tooltip("Seconds into the clip where this segment's audio begins.")]
        public float start;

        [Tooltip("Seconds into the clip where this segment's audio ends (playback pauses here).")]
        public float end;
    }
}