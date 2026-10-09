using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public struct LineContent : ILineContent
    {
        [SerializeField] private string lineId;
        [SerializeField, TextArea(3, 10)] private string text;
        [SerializeField] private AudioClip clip;
        [Tooltip("0 = full volume (default for lines authored before this field existed). Set a small positive value instead of 0 for near-silence.")]
        [SerializeField, Range(0f, 1f)] private float volume;

        // One entry per authored segment. Author the text with a '|' marker at
        // each pause point, then add one segment here per text piece, each with
        // the start and end time (seconds) of that piece's audio. The start lets
        // you skip a breath before a line. Leave empty for continuous playback.
        [SerializeField] private SegmentTime[] segments;

        public string LineId => lineId;
        public string Text => text;
        public AudioClip Clip => clip;
        public float Volume => volume <= 0f ? 1f : volume;
        public SegmentTime[] Segments => segments ?? Array.Empty<SegmentTime>();

        public string TextFor(NameVariant variant) => text;
        public AudioClip ClipFor(NameVariant variant) => clip;
        public SegmentTime[] SegmentsFor(NameVariant variant) => Segments;
        public float VolumeFor(NameVariant variant) => Volume;
    }
}