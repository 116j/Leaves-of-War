using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public sealed class SequenceBeat : NarrativeBeat
    {
        [SerializeField] private string sequenceId;
        [SerializeField] private bool locksPlayer = true;

        public string SequenceId => sequenceId;
        public bool LocksPlayer => locksPlayer;
    }
}
