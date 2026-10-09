using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Catalog", fileName = "NarrativeCatalog")]
    public sealed class NarrativeCatalog : ScriptableObject
    {
        [SerializeField] private List<ChapterDefinition> chapters = new List<ChapterDefinition>();
        [SerializeField] private List<EndingDefinition> endings = new List<EndingDefinition>();
        [SerializeField] private List<DocumentDefinition> documents = new List<DocumentDefinition>();
        [SerializeField] private List<FlagId> flags = new List<FlagId>();
        [SerializeField] private List<TaskObjective> taskObjectives = new List<TaskObjective>();
        [SerializeField, Min(0)] private int initialBloomCount = 3;

        public IReadOnlyList<ChapterDefinition> Chapters => chapters;
        public IReadOnlyList<EndingDefinition> Endings => endings;
        public IReadOnlyList<DocumentDefinition> Documents => documents;
        public IReadOnlyList<FlagId> Flags => flags;
        public IReadOnlyList<TaskObjective> TaskObjectives => taskObjectives;
        public int InitialBloomCount => initialBloomCount;
        public ChapterDefinition FinalChapter => chapters.Count > 0 ? chapters[chapters.Count - 1] : null;

        public ChapterDefinition ChapterAt(int chapterIndex)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterDefinition chapter = chapters[i];
                if (chapter != null && chapter.Index == chapterIndex)
                    return chapter;
            }

            return null;
        }

        public EndingDefinition EndingWithId(string endingId)
        {
            if (string.IsNullOrWhiteSpace(endingId))
                return null;

            for (int i = 0; i < endings.Count; i++)
            {
                EndingDefinition ending = endings[i];
                if (ending != null && string.Equals(ending.Id, endingId, StringComparison.Ordinal))
                    return ending;
            }

            return null;
        }

        public FlagId FlagWithId(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
                return null;

            for (int i = 0; i < flags.Count; i++)
            {
                FlagId flag = flags[i];
                if (flag != null && string.Equals(flag.Id, flagId, StringComparison.Ordinal))
                    return flag;
            }

            return null;
        }

        public TaskObjective TaskObjectiveWithId(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
                return null;

            for (int i = 0; i < taskObjectives.Count; i++)
            {
                TaskObjective objective = taskObjectives[i];
                if (objective != null &&
                    string.Equals(objective.Id, objectiveId, StringComparison.Ordinal))
                {
                    return objective;
                }
            }

            return null;
        }
    }
}
