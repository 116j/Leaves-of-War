using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public abstract class StateEffect
    {
        public abstract void Apply(NarrativeState state);
    }

    [Serializable]
    public sealed class SetFlagEffect : StateEffect
    {
        [SerializeField] private FlagId flag;

        public FlagId Flag => flag;

        public override void Apply(NarrativeState state) => state?.SetFlag(flag);
    }

    [Serializable]
    public sealed class AdjustGardenCounterEffect : StateEffect
    {
        [SerializeField] private GardenCounter target;
        [SerializeField] private int delta = -1;

        public override void Apply(NarrativeState state) =>
            state?.Garden.Adjust(target, delta);
    }

    /// <summary>
    /// Marks a TaskObjective as fully complete, regardless of its currently
    /// authored requirement. Uses int.MaxValue as the raw count - TaskProgress
    /// itself clamps this down to the real requirement once one is
    /// registered, so this always lands on "as complete as it can be"
    /// whether or not the requirement is already known at the point this
    /// effect runs.
    /// </summary>
    [Serializable]
    public sealed class CompleteTaskObjectiveEffect : StateEffect
    {
        [SerializeField] private TaskObjective objective;

        public TaskObjective Objective => objective;

        public override void Apply(NarrativeState state)
        {
            if (state == null || objective == null)
                return;

            state.Tasks.SetCount(objective, int.MaxValue);
        }
    }
}