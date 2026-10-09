using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public sealed class TitleCardBeat : NarrativeBeat
    {
        [SerializeField] private string text;

        public string Text => text;
    }
}
