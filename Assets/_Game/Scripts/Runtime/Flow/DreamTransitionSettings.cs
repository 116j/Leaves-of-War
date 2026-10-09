using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Whether a cue fires when ENTERING the scene it names (the classic
    /// "falling asleep" transition into a dream), or when LEAVING it (a
    /// "waking up" transition back out, typically placed at the end of the
    /// chapter that uses that scene).
    /// </summary>
    public enum DreamTransitionTrigger
    {
        OnEnterScene,
        OnExitScene,
    }

    [Serializable]
    public sealed class DreamTransitionCue
    {
        [SerializeField, Min(1)] private int chapterIndex;
        [Tooltip("The scene this cue applies to. With OnEnterScene, fires when travel is about to load INTO this scene. With OnExitScene, fires when travel is about to load AWAY FROM this scene (e.g. at the end of the chapter that uses it).")]
        [SerializeField] private string sceneName;
        [SerializeField] private DreamTransitionTrigger trigger = DreamTransitionTrigger.OnEnterScene;
        [SerializeField] private string cardText;
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 0.526f;

        public int ChapterIndex => chapterIndex;
        public string SceneName => sceneName;
        public DreamTransitionTrigger Trigger => trigger;
        public string CardText => cardText;
        public AudioClip Music => music;
        public float VolumeScale => volumeScale;
    }

    [CreateAssetMenu(
        fileName = "DreamTransitionSettings",
        menuName = "Hortensia/Audio/Dream Transition Settings")]
    public sealed class DreamTransitionSettings : ScriptableObject
    {
        public const string ResourcePath = "Audio/DreamTransitionSettings";

        [SerializeField, Min(0f)] private float fadeInSeconds = 0.45f;
        [SerializeField, Min(0f)] private float minimumVisibleSeconds = 2.25f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.65f;
        [Tooltip("One entry per (chapter, scene, direction) combination. Add as many as you need - any scene, any chapter, entering or leaving, all from this single list.")]
        [SerializeField]
        private List<DreamTransitionCue> cues =
            new List<DreamTransitionCue>();

        public float FadeInSeconds => fadeInSeconds;
        public float MinimumVisibleSeconds => minimumVisibleSeconds;
        public float FadeOutSeconds => fadeOutSeconds;
        public IReadOnlyList<DreamTransitionCue> Cues => cues;

        /// <summary>
        /// Looks up a cue for a specific chapter, scene, and direction. Used
        /// twice per travel by GameSession: once for the scene being
        /// entered (OnEnterScene), once for the scene being left
        /// (OnExitScene) - whichever matches first wins if somehow both are
        /// configured for the same travel.
        /// </summary>
        public bool TryGetCue(
            int chapterIndex,
            string sceneName,
            DreamTransitionTrigger trigger,
            out DreamTransitionCue cue)
        {
            cue = null;
            for (int i = 0; i < cues.Count; i++)
            {
                DreamTransitionCue candidate = cues[i];
                if (candidate != null &&
                    candidate.ChapterIndex == chapterIndex &&
                    candidate.Trigger == trigger &&
                    string.Equals(candidate.SceneName, sceneName, StringComparison.Ordinal))
                {
                    cue = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}