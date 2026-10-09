using System;
using System.Collections.Generic;

namespace Hortensia.Narrative
{
    public sealed class TaskProgress
    {
        private readonly Dictionary<string, int> counts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> registeredRequirements =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Counts => counts;

        public event Action<TaskObjective, int> ProgressChanged;
        public event Action RequirementsChanged;

        public int CountFor(TaskObjective objective) =>
            objective == null ? 0 : CountForId(objective.Id);

        public int CountForId(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
                return 0;

            return counts.TryGetValue(objectiveId, out int count) ? count : 0;
        }

        public int RequiredFor(TaskObjective objective, int authored)
        {
            if (authored > 0)
                return authored;
            if (objective == null || string.IsNullOrWhiteSpace(objective.Id))
                return 0;

            return registeredRequirements.TryGetValue(objective.Id, out int registered)
                ? registered
                : 0;
        }

        public void Register(TaskObjective objective, int itemCount)
        {
            if (objective == null || string.IsNullOrWhiteSpace(objective.Id))
                return;

            registeredRequirements[objective.Id] = Math.Max(0, itemCount);
        }

        public void ClearRegistrations()
        {
            registeredRequirements.Clear();
        }

        /// <summary>
        /// Announces that a scene has finished rebuilding its task requirements.
        /// Call this after registering the complete scene, rather than after each
        /// receiver, so consumers never observe a partial requirement count.
        /// </summary>
        public void NotifyRequirementsChanged()
        {
            RequirementsChanged?.Invoke();
        }

        public void Report(TaskObjective objective)
        {
            if (objective == null || string.IsNullOrWhiteSpace(objective.Id))
                return;

            int current = CountFor(objective);
            int next = current + 1;
            if (registeredRequirements.TryGetValue(objective.Id, out int required) && required > 0)
                next = Math.Min(next, required);

            if (next == current)
                return;

            counts[objective.Id] = next;
            ProgressChanged?.Invoke(objective, next);
        }

        public void SetCount(TaskObjective objective, int count)
        {
            if (objective == null || string.IsNullOrWhiteSpace(objective.Id))
                return;

            int next = Math.Max(0, count);
            if (registeredRequirements.TryGetValue(objective.Id, out int required) && required > 0)
                next = Math.Min(next, required);

            int current = CountFor(objective);
            if (next == current)
                return;

            if (next == 0)
                counts.Remove(objective.Id);
            else
                counts[objective.Id] = next;

            ProgressChanged?.Invoke(objective, next);
        }

        public void RestoreCount(string objectiveId, int count)
        {
            if (string.IsNullOrWhiteSpace(objectiveId) || count <= 0)
                return;

            counts[objectiveId] = count;
        }
    }
}
