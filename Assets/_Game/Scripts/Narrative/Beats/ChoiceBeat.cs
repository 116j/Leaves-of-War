using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public struct ChoiceOption
    {
        [SerializeField] private string label;
        [SerializeField] private EndingDefinition ending;

        public string Label => label;
        public EndingDefinition Ending => ending;
    }

    [Serializable]
    public sealed class ChoiceBeat : NarrativeBeat
    {
        [SerializeField] private List<ChoiceOption> options = new List<ChoiceOption>();

        public IReadOnlyList<ChoiceOption> Options => options;
    }
}
