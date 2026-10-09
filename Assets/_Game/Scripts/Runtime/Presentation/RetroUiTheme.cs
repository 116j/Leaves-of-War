using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Shared geometry and palette for runtime-created gameplay UI. Diegetic
    /// offsets are authored in 320x240 world pixels and are scaled only through
    /// <see cref="RetroResolution"/> when rendered into the denser UI target.
    /// </summary>
    [CreateAssetMenu(menuName = "Hortensia/Retro UI Theme", fileName = "RetroUiTheme")]
    public sealed class RetroUiTheme : ScriptableObject
    {
        public const string ResourcePath = "Narrative/RetroUiTheme";

        private static RetroUiTheme runtimeFallback;

        [Header("Diegetic Type")]
        [SerializeField, Min(1f)] private float diegeticFontSize = 16f;
        [SerializeField, Min(1f)] private float diegeticSmallFontSize = 14f;
        [SerializeField, Min(1f)] private float documentBodyMinFontSize = 8f;
        [SerializeField, Min(1f)] private float subtitleHeight = 100f;
        [SerializeField, Min(0f)] private float documentInset = 8f;
        [SerializeField, Min(0f)] private float statusDurationSeconds = 6f;
        [SerializeField, Min(1f)] private float objectiveArrowSize = 16f;
        [SerializeField] private Vector2 objectiveArrowOffset = new Vector2(-16f, -16f);
        [SerializeField, Min(0f)] private float objectiveArrowNearDistance = 3f;
        [SerializeField, Min(0f)] private float objectiveArrowMediumDistance = 12f;

        [Header("Diegetic Geometry (base 320x240 pixels)")]
        [SerializeField] private Vector4 speakerRect = new Vector4(6f, 59f, -6f, -4f);
        [SerializeField] private Vector4 subtitleRect = new Vector4(6f, 5f, -6f, -23f);
        [SerializeField] private Vector4 interactionPromptRect = new Vector4(4f, 84f, -4f, -136f);
        [SerializeField] private Vector4 carryStatusRect = new Vector4(6f, 145f, -6f, -74f);
        [SerializeField] private Vector4 statusRect = new Vector4(6f, 190f, -6f, -8f);
        // Leave a top-right gutter for the objective arrow so task/boss HUD text
        // never renders beneath it.
        [SerializeField] private Vector4 gameplayHudRect = new Vector4(6f, 174f, -30f, -8f);
        [SerializeField] private Vector4 documentTitleRect = new Vector4(8f, 191f, -8f, -8f);
        [SerializeField] private Vector4 documentBodyRect = new Vector4(8f, 60f, -8f, -44f);
        [SerializeField] private Vector4 documentPageRect = new Vector4(8f, 4f, -8f, -208f);

        [Header("Authorial Geometry")]
        [SerializeField] private Vector2 authorialReferenceResolution = new Vector2(800f, 600f);
        [SerializeField, Min(1f)] private float cardFontSize = 44f;
        [SerializeField, Min(0f)] private float cardMargin = 80f;
        [SerializeField, Min(1f)] private float choicePromptFontSize = 38f;
        [SerializeField] private Vector4 choicePromptRect = new Vector4(80f, 420f, -80f, -70f);
        [SerializeField] private Vector2 choiceButtonSize = new Vector2(520f, 72f);
        [SerializeField] private float choiceButtonStartY = 100f;
        [SerializeField, Min(0f)] private float choiceButtonSpacing = 92f;
        [SerializeField, Min(1f)] private float choiceButtonFontSize = 30f;
        [SerializeField] private Vector2 choiceButtonLabelInset = new Vector2(18f, 8f);

        [Header("Pause Menu Geometry")]
        [SerializeField, Min(1f)] private float pauseHeaderFontSize = 44f;
        [SerializeField] private float pauseHeaderY = 180f;
        [SerializeField] private float pauseButtonStartY = 85f;
        [SerializeField, Min(0f)] private float pauseButtonSpacing = 76f;
        [SerializeField] private Vector2 pauseButtonSize = new Vector2(420f, 62f);
        [SerializeField, Min(1f)] private float pauseButtonFontSize = 28f;

        [Header("Centered Sequence Overlay")]
        [SerializeField, Min(1f)] private float overlayHeadingFontSize = 34f;
        [SerializeField, Min(1f)] private float overlayBodyFontSize = 24f;
        [SerializeField, Min(1f)] private float overlayPromptFontSize = 17f;
        [SerializeField] private Vector4 overlayHeadingAnchors = new Vector4(0.08f, 0.72f, 0.92f, 0.94f);
        [SerializeField] private Vector4 overlayBodyAnchors = new Vector4(0.08f, 0.18f, 0.92f, 0.72f);
        [SerializeField] private Vector4 overlayPromptAnchors = new Vector4(0.08f, 0.04f, 0.92f, 0.17f);
        [SerializeField] private Vector4 creditsBodyAnchors = new Vector4(0.1f, 0.12f, 0.9f, 0.76f);
        [SerializeField, Min(1f)] private float creditsBodyFontSize = 22f;
        [SerializeField] private Vector2 creditsScrollFrameFactors = new Vector2(-0.58f, 0.66f);

        [Header("Palette")]
        [SerializeField] private Color subtitlePanelColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] private Color speakerColor = new Color(0.72f, 0.84f, 0.62f);
        [SerializeField] private Color subtitleColor = new Color(0.92f, 0.9f, 0.82f);
        [SerializeField] private Color promptColor = new Color(0.76f, 0.86f, 0.66f);
        [SerializeField] private Color statusColor = new Color(0.86f, 0.72f, 0.52f);
        [SerializeField] private Color gameplayHudColor = new Color(0.82f, 0.78f, 0.62f);
        [SerializeField] private Color objectiveArrowNearColor = new Color(0.68f, 0.84f, 0.62f);
        [SerializeField] private Color objectiveArrowMediumColor = new Color(0.86f, 0.72f, 0.52f);
        [SerializeField] private Color objectiveArrowFarColor = new Color(0.72f, 0.4f, 0.38f);
        [SerializeField] private Color documentPanelColor = new Color(0.055f, 0.05f, 0.038f, 0.12f);
        [Tooltip("Optional full-screen background image behind the document/letter panel, drawn behind the panel's own dark tint.")]
        [SerializeField] private Texture2D documentBackgroundImage;
        [SerializeField] private Color documentTitleColor = new Color(0.76f, 0.84f, 0.62f);
        [SerializeField] private Color documentBodyColor = new Color(0.9f, 0.86f, 0.76f);
        [SerializeField] private Color documentPageColor = new Color(0.66f, 0.65f, 0.57f);
        [SerializeField] private Color authorialPanelColor = new Color(0.015f, 0.013f, 0.011f, 1f);
        [SerializeField] private Color choicePanelColor = new Color(0.015f, 0.013f, 0.011f, 0.96f);
        [SerializeField] private Color authorialTextColor = new Color(0.88f, 0.84f, 0.72f);
        [SerializeField] private Color choiceButtonColor = new Color(0.12f, 0.105f, 0.08f, 0.95f);
        [SerializeField] private Color choiceHighlightColor = new Color(0.72f, 0.76f, 0.58f);
        [SerializeField] private Color choicePressedColor = new Color(0.5f, 0.54f, 0.4f);
        [SerializeField] private Color choiceTextColor = new Color(0.9f, 0.86f, 0.74f);
        [SerializeField] private Color overlayBodyColor = new Color(0.9f, 0.87f, 0.77f);
        [SerializeField] private Color overlayPromptColor = new Color(0.72f, 0.69f, 0.59f);

        public float DiegeticScale => RetroResolution.DiegeticUiScale;
        public float DiegeticFontSize => diegeticFontSize * DiegeticScale;
        public float DiegeticSmallFontSize => diegeticSmallFontSize * DiegeticScale;
        public float DocumentBodyMinFontSize => documentBodyMinFontSize * DiegeticScale;
        public float SubtitleHeight => subtitleHeight * DiegeticScale;
        public float DocumentInset => documentInset * DiegeticScale;
        public float StatusDurationSeconds => statusDurationSeconds;
        public float ObjectiveArrowSize => objectiveArrowSize * DiegeticScale;
        public Vector2 ObjectiveArrowOffset => objectiveArrowOffset * DiegeticScale;
        public float ObjectiveArrowNearDistance => objectiveArrowNearDistance;
        public float ObjectiveArrowMediumDistance => Mathf.Max(
            objectiveArrowNearDistance,
            objectiveArrowMediumDistance);
        public Vector2 AuthorialReferenceResolution => authorialReferenceResolution;
        public float CardFontSize => cardFontSize;
        public float CardMargin => cardMargin;
        public float ChoicePromptFontSize => choicePromptFontSize;
        public Vector4 ChoicePromptRect => choicePromptRect;
        public Vector2 ChoiceButtonSize => choiceButtonSize;
        public float ChoiceButtonStartY => choiceButtonStartY;
        public float ChoiceButtonSpacing => choiceButtonSpacing;
        public float ChoiceButtonFontSize => choiceButtonFontSize;
        public Vector2 ChoiceButtonLabelInset => choiceButtonLabelInset;
        public float PauseHeaderFontSize => pauseHeaderFontSize;
        public float PauseHeaderY => pauseHeaderY;
        public float PauseButtonStartY => pauseButtonStartY;
        public float PauseButtonSpacing => pauseButtonSpacing;
        public Vector2 PauseButtonSize => pauseButtonSize;
        public float PauseButtonFontSize => pauseButtonFontSize;
        public float OverlayHeadingFontSize => overlayHeadingFontSize;
        public float OverlayBodyFontSize => overlayBodyFontSize;
        public float OverlayPromptFontSize => overlayPromptFontSize;
        public Vector4 OverlayHeadingAnchors => overlayHeadingAnchors;
        public Vector4 OverlayBodyAnchors => overlayBodyAnchors;
        public Vector4 OverlayPromptAnchors => overlayPromptAnchors;
        public Vector4 CreditsBodyAnchors => creditsBodyAnchors;
        public float CreditsBodyFontSize => creditsBodyFontSize;
        public Vector2 CreditsScrollFrameFactors => creditsScrollFrameFactors;
        public Color SubtitlePanelColor => subtitlePanelColor;
        public Color SpeakerColor => speakerColor;
        public Color SubtitleColor => subtitleColor;
        public Color PromptColor => promptColor;
        public Color StatusColor => statusColor;
        public Color GameplayHudColor => gameplayHudColor;
        public Color ObjectiveArrowNearColor => objectiveArrowNearColor;
        public Color ObjectiveArrowMediumColor => objectiveArrowMediumColor;
        public Color ObjectiveArrowFarColor => objectiveArrowFarColor;
        public Color DocumentPanelColor => documentPanelColor;
        public Texture2D DocumentBackgroundImage => documentBackgroundImage;
        public Color DocumentTitleColor => documentTitleColor;
        public Color DocumentBodyColor => documentBodyColor;
        public Color DocumentPageColor => documentPageColor;
        public Color AuthorialPanelColor => authorialPanelColor;
        public Color ChoicePanelColor => choicePanelColor;
        public Color AuthorialTextColor => authorialTextColor;
        public Color ChoiceButtonColor => choiceButtonColor;
        public Color ChoiceHighlightColor => choiceHighlightColor;
        public Color ChoicePressedColor => choicePressedColor;
        public Color ChoiceTextColor => choiceTextColor;
        public Color OverlayBodyColor => overlayBodyColor;
        public Color OverlayPromptColor => overlayPromptColor;

        public Vector2 ScaledOffsetMin(Vector4 rect) =>
            new Vector2(rect.x, rect.y) * DiegeticScale;

        public Vector2 ScaledOffsetMax(Vector4 rect) =>
            new Vector2(rect.z, rect.w) * DiegeticScale;

        public Vector4 SpeakerRect => speakerRect;
        public Vector4 SubtitleRect => subtitleRect;
        public Vector4 InteractionPromptRect => interactionPromptRect;
        public Vector4 CarryStatusRect => carryStatusRect;
        public Vector4 StatusRect => statusRect;
        public Vector4 GameplayHudRect => gameplayHudRect;
        public Vector4 DocumentTitleRect => documentTitleRect;
        public Vector4 DocumentBodyRect => documentBodyRect;
        public Vector4 DocumentPageRect => documentPageRect;

        public static RetroUiTheme Resolve(RetroUiTheme candidate)
        {
            if (candidate != null)
                return candidate;

            RetroUiTheme loaded = Resources.Load<RetroUiTheme>(ResourcePath);
            if (loaded != null)
                return loaded;

            if (runtimeFallback == null)
            {
                runtimeFallback = CreateInstance<RetroUiTheme>();
                runtimeFallback.name = "Runtime Retro UI Theme";
                runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return runtimeFallback;
        }
    }
}
