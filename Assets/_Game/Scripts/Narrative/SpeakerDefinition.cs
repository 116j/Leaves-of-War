using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public struct ArcPhase
    {
        [SerializeField, Min(1)] private int chapterIndex;
        [SerializeField] private string directionWord;

        public int ChapterIndex => chapterIndex;
        public string DirectionWord => directionWord;
    }

    [CreateAssetMenu(menuName = "Hortensia/Narrative/Speaker", fileName = "Speaker")]
    public sealed class SpeakerDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private bool isProtagonist;
        [SerializeField] private ArcPhase[] phasesByChapter = Array.Empty<ArcPhase>();

        public string Id => id;
        public string DisplayName => displayName;
        public bool IsProtagonist => isProtagonist;

        // Phases are authored at the chapters where the voice changes, so the
        // phase in force is the latest one at or before the given chapter.
        public bool TryGetPhase(int chapterIndex, out ArcPhase phase)
        {
            phase = default;
            bool found = false;

            for (int i = 0; i < phasesByChapter.Length; i++)
            {
                ArcPhase candidate = phasesByChapter[i];
                if (candidate.ChapterIndex > chapterIndex)
                    continue;

                if (!found || candidate.ChapterIndex > phase.ChapterIndex)
                {
                    phase = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
