using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public abstract class BeatCondition
    {
        public abstract bool IsSatisfied(NarrativeState state);
    }

    [Serializable]
    public sealed class FlagCondition : BeatCondition
    {
        [SerializeField] private FlagId flag;
        [SerializeField] private bool mustBePresent = true;

        public FlagId Flag => flag;
        public bool MustBePresent => mustBePresent;

        public override bool IsSatisfied(NarrativeState state) =>
            state != null && state.HasFlag(flag) == mustBePresent;
    }

    [Serializable]
    public sealed class TaskCondition : BeatCondition
    {
        [SerializeField] private TaskObjective objective;
        [SerializeField, Min(0)] private int requiredCount;

        public TaskObjective Objective => objective;
        public int RequiredCount => requiredCount;

        public override bool IsSatisfied(NarrativeState state)
        {
            if (state == null || objective == null || string.IsNullOrWhiteSpace(objective.Id))
                return false;

            int required = state.Tasks.RequiredFor(objective, requiredCount);
            return required > 0 && state.Tasks.CountFor(objective) >= required;
        }
    }

    [Serializable]
    public sealed class AllOfCondition : BeatCondition
    {
        [SerializeReference] private List<BeatCondition> conditions = new List<BeatCondition>();

        public IReadOnlyList<BeatCondition> Conditions => conditions;

        // An empty list is vacuously true.
        public override bool IsSatisfied(NarrativeState state)
        {
            if (conditions == null)
                return true;

            for (int i = 0; i < conditions.Count; i++)
            {
                BeatCondition condition = conditions[i];
                if (condition != null && !condition.IsSatisfied(state))
                    return false;
            }

            return true;
        }
    }

    [Serializable]
    public sealed class AnyOfCondition : BeatCondition
    {
        [SerializeReference] private List<BeatCondition> conditions = new List<BeatCondition>();

        public IReadOnlyList<BeatCondition> Conditions => conditions;

        // An empty list is unsatisfiable.
        public override bool IsSatisfied(NarrativeState state)
        {
            if (conditions == null)
                return false;

            for (int i = 0; i < conditions.Count; i++)
            {
                BeatCondition condition = conditions[i];
                if (condition != null && condition.IsSatisfied(state))
                    return true;
            }

            return false;
        }
    }
}
