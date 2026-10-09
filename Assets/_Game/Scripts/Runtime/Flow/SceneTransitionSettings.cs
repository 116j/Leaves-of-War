using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// The player-facing copy shown while arriving at one specific authored
    /// location. Music here is optional - leave it empty for a silent card.
    /// </summary>
    [Serializable]
    public sealed class SceneTransitionCard
    {
        [SerializeField, Min(1)] private int chapterIndex;
        [SerializeField] private string targetSceneName;
        [SerializeField] private string spawnPointId;
        [SerializeField, TextArea(3, 8)] private string cardText;
        [Tooltip("Optional music played while this card is shown. Leave empty for a silent card.")]
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;
        [Tooltip("Shows a 'HOLD TO CONTINUE' skip prompt instead of holding for the music/minimum duration. Intended for the very first transition of the game only.")]
        [SerializeField] private bool showSkipPrompt;

        public int ChapterIndex => chapterIndex;
        public string TargetSceneName => targetSceneName;
        public string SpawnPointId => spawnPointId;
        public string CardText => cardText;
        public AudioClip Music => music;
        public float VolumeScale => volumeScale;
        public bool ShowSkipPrompt => showSkipPrompt;
    }

    /// <summary>
    /// Central authored arrival-card catalog for narrative scene travel.
    /// A card is keyed by the active chapter, destination scene, and
    /// destination spawn so returning to the Manor can retain its distinct
    /// story context.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneTransitionSettings",
        menuName = "Hortensia/Narrative/Scene Transition Settings")]
    public sealed class SceneTransitionSettings : ScriptableObject
    {
        public const string ResourcePath = "Narrative/SceneTransitionSettings";

        [Tooltip("Background texture (frame) applied to all transition cards.")]
        [SerializeField] private Texture2D frameTexture;

        [Tooltip("Text color applied to all transition cards.")]
        [SerializeField] private Color textColor = Color.white;

        [Tooltip("One required card per authored chapter, destination scene, and destination spawn-point combination.")]
        [SerializeField]
        private List<SceneTransitionCard> cards = new List<SceneTransitionCard>();

        public IReadOnlyList<SceneTransitionCard> Cards => cards;
        public Texture2D FrameTexture => frameTexture;
        public Color TextColor => textColor;

        public bool TryGetCard(
            int chapterIndex,
            string targetSceneName,
            string spawnPointId,
            out SceneTransitionCard card)
        {
            card = null;
            for (int i = 0; i < cards.Count; i++)
            {
                SceneTransitionCard candidate = cards[i];
                if (candidate == null || candidate.ChapterIndex != chapterIndex)
                    continue;

                if (!string.Equals(
                        candidate.TargetSceneName,
                        targetSceneName,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.SpawnPointId,
                        spawnPointId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                card = candidate;
                return true;
            }

            return false;
        }
    }
}