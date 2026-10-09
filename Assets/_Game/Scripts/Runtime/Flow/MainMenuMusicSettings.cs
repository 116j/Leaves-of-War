using System;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    public enum MainMenuMusicStage
    {
        BeforeFirstDream,
        AfterFirstDream,
        AfterSecondDream
    }

    [CreateAssetMenu(
        fileName = "MainMenuMusicSettings",
        menuName = "Hortensia/Audio/Main Menu Music Settings")]
    public sealed class MainMenuMusicSettings : ScriptableObject
    {
        public const string ResourcePath = "Audio/MainMenuMusicSettings";

        private const string DreamSceneName = "DreamGreenhouse";

        [SerializeField] private AudioClip beforeFirstDream;
        [SerializeField] private AudioClip afterFirstDream;
        [SerializeField] private AudioClip afterSecondDream;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 0.7f;

        public AudioClip BeforeFirstDream => beforeFirstDream;
        public AudioClip AfterFirstDream => afterFirstDream;
        public AudioClip AfterSecondDream => afterSecondDream;
        public float VolumeScale => volumeScale;

        public AudioClip ClipFor(MainMenuMusicStage stage)
        {
            switch (stage)
            {
                case MainMenuMusicStage.BeforeFirstDream:
                    return beforeFirstDream;
                case MainMenuMusicStage.AfterFirstDream:
                    return afterFirstDream;
                case MainMenuMusicStage.AfterSecondDream:
                    return afterSecondDream;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        public static MainMenuMusicStage StageFor(
            NarrativeState progress,
            NarrativeCatalog catalog)
        {
            if (HasCompletedDream(progress, catalog, 2))
                return MainMenuMusicStage.AfterSecondDream;
            if (HasCompletedDream(progress, catalog, 1))
                return MainMenuMusicStage.AfterFirstDream;

            return MainMenuMusicStage.BeforeFirstDream;
        }

        private static bool HasCompletedDream(
            NarrativeState progress,
            NarrativeCatalog catalog,
            int dreamOrdinal)
        {
            if (progress == null || catalog == null || dreamOrdinal < 1)
                return false;

            int encounteredDreams = 0;
            for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
            {
                ChapterDefinition chapter = catalog.Chapters[chapterIndex];
                if (chapter == null)
                    continue;

                for (int beatIndex = 0; beatIndex < chapter.Beats.Count; beatIndex++)
                {
                    if (!(chapter.Beats[beatIndex] is TravelBeat entryTravel) ||
                        !string.Equals(
                            entryTravel.TargetScene,
                            DreamSceneName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    encounteredDreams++;
                    if (encounteredDreams != dreamOrdinal)
                        continue;

                    int completionCursor = chapter.Beats.Count;
                    for (int laterBeatIndex = beatIndex + 1;
                         laterBeatIndex < chapter.Beats.Count;
                         laterBeatIndex++)
                    {
                        if (chapter.Beats[laterBeatIndex] is TravelBeat)
                        {
                            completionCursor = laterBeatIndex + 1;
                            break;
                        }
                    }

                    if (progress.ChapterIndex > chapter.Index)
                        return true;
                    if (progress.ChapterIndex < chapter.Index)
                        return false;

                    return progress.BeatIndex >= completionCursor;
                }
            }

            return false;
        }
    }
}
