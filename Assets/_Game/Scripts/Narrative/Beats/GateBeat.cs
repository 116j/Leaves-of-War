using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public sealed class GateBeat : NarrativeBeat
    {
        [SerializeReference] private BeatCondition condition;

        public BeatCondition Condition => condition;

        // An unauthored gate passes through rather than blocking, so a
        // half-finished chapter cannot soft-lock the game.
        public bool IsOpen(NarrativeState state) =>
            condition == null || condition.IsSatisfied(state);
    }
}
