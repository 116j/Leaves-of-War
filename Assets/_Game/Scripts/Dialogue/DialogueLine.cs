using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    [Serializable]
    public sealed class DialoguePiece
    {
        [TextArea(2, 5)] public string text;
        [Tooltip("Voice clip of this piece only. The player cannot move on until it has finished.")]
        public AudioClip voice;
        [Tooltip("0 = full volume.")]
        [Range(0f, 1f)] public float volume = 1f;

        public float Volume => volume <= 0f ? 1f : volume;
    }

    [Serializable]
    public sealed class DialogueLine
    {
        [Tooltip("Speaker Id of the DialogueSpeaker who says this line (e.g. carlo, ungaretti).")]
        public string speakerId;
        [Tooltip("The line split into pieces: each piece is shown, voiced and confirmed on its own.")]
        public DialoguePiece[] pieces = new DialoguePiece[0];
    }
}
