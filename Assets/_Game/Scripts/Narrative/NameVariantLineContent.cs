using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public struct NameVariantLineContent : ILineContent
    {
        [SerializeField] private string lineId;
        [SerializeField, TextArea(3, 10)] private string lauraText;
        [SerializeField, TextArea(3, 10)] private string everieText;
        [SerializeField] private AudioClip lauraClip;
        [SerializeField] private AudioClip everieClip;
        [Tooltip("0 = full volume (default for lines authored before this field existed). Set a small positive value instead of 0 for near-silence.")]
        [SerializeField, Range(0f, 1f)] private float lauraVolume;
        [SerializeField, Range(0f, 1f)] private float everieVolume;

        // One entry per authored segment, PER VARIANT - Laura's and Everie's
        // recordings are separate takes and can have different pacing/breath
        // timing even for "the same" line, so each needs its own start/end
        // seconds. Author each variant's text with '|' at each pause point;
        // add one segment per piece here, matching that variant's own clip.
        [SerializeField] private SegmentTime[] lauraSegments;
        [SerializeField] private SegmentTime[] everieSegments;

        public string LineId => lineId;

        public string TextFor(NameVariant variant) =>
            variant == NameVariant.Laura ? lauraText : everieText;

        // The takes are deliberately not exposed individually: a variant line's
        // clip should only ever be chosen by variant.
        public AudioClip ClipFor(NameVariant variant) =>
            variant == NameVariant.Laura ? lauraClip : everieClip;

        public SegmentTime[] SegmentsFor(NameVariant variant) =>
            (variant == NameVariant.Laura ? lauraSegments : everieSegments)
                ?? Array.Empty<SegmentTime>();

        public float VolumeFor(NameVariant variant)
        {
            float raw = variant == NameVariant.Laura ? lauraVolume : everieVolume;
            return raw <= 0f ? 1f : raw;
        }
    }
}