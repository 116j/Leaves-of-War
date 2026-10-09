using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public abstract class NarrativeBeat
    {
        [SerializeField, TextArea(2, 5)] private string editorNote;
        [SerializeReference] private List<StateEffect> onStart = new List<StateEffect>();
        [SerializeReference] private List<StateEffect> onComplete = new List<StateEffect>();

        public string EditorNote => editorNote;
        public IReadOnlyList<StateEffect> OnStart => onStart;
        public IReadOnlyList<StateEffect> OnComplete => onComplete;

        public void ApplyStartEffects(NarrativeState state)
        {
            if (onStart == null)
                return;

            for (int i = 0; i < onStart.Count; i++)
                onStart[i]?.Apply(state);
        }

        public void ApplyCompletionEffects(NarrativeState state)
        {
            if (onComplete == null)
                return;

            for (int i = 0; i < onComplete.Count; i++)
                onComplete[i]?.Apply(state);
        }
    }
}