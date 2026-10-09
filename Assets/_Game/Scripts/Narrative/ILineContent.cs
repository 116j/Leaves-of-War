using UnityEngine;

namespace Hortensia.Narrative
{
    // A line's content owns its clips, so choosing between takes belongs here
    // rather than in the beat that holds it.
    public interface ILineContent
    {
        string LineId { get; }
        string TextFor(NameVariant variant);
        AudioClip ClipFor(NameVariant variant);

        // One entry per authored segment (text split on '|'), each with the
        // start and end time of that segment's audio. Empty means no segments.
        // Takes a variant because different takes (e.g. Laura's vs Everie's
        // recording of the "same" line) can have different pacing/breath
        // timing, even when the authored text and segment count match.
        SegmentTime[] SegmentsFor(NameVariant variant);

        // Playback volume (0-1) for this variant's clip - separate takes can
        // be recorded at different levels and need independent trim.
        float VolumeFor(NameVariant variant);
    }
}