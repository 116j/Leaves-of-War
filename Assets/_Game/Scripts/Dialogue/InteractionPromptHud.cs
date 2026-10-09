using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptHud : MonoBehaviour
    {
        [Header("Font")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip(".ttf / .otf file. Wins over Font; Font's material look is copied onto it.")]
        [SerializeField] private Font fontFile;
        [Tooltip("Always bold, whatever the player picks in the options.")]
        [SerializeField] private bool bold = false;

        [Header("Text")]
        [Tooltip("{0} = the prompt of the object (e.g. TALK). Use \"[E] {0}\" to show the key too.")]
        [SerializeField] private string format = "{0}";
        [Tooltip("Colours, thickness, outline, size and background are set by the player in Options > Screen Text Style.")]
        [SerializeField, Min(8f)] private float size = 30f;
        [Tooltip("Position on a 1920x1080 screen (0,0 = centre): just below the centre, where the player is looking.")]
        [SerializeField] private Vector2 position = new Vector2(0f, -70f);
        [SerializeField] private Vector2 padding = new Vector2(18f, 6f);
        [SerializeField, Min(0f)] private float fadeSeconds = 0.15f;

        public static InteractionPromptHud Instance { get; private set; }

        private FirstPersonController player;
        private RectTransform holder;
        private RectTransform plate;
        private Image plateImage;
        private TMP_Text label;
        private CanvasGroup group;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            BuildUi();
            GameSettings.Applied += ApplyStyle;
            ApplyStyle();
        }

        private void OnDestroy()
        {
            GameSettings.Applied -= ApplyStyle;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (player == null)
                player = FindAnyObjectByType<FirstPersonController>();

            string prompt = GameSettings.Current.inGameTextEnabled && player != null && player.isActiveAndEnabled && !DialogueRunner.IsPlaying &&
                            (PauseController.Instance == null || !PauseController.Instance.IsPaused)
                ? player.InteractionPrompt
                : string.Empty;

            bool show = !string.IsNullOrWhiteSpace(prompt);
            if (show)
            {
                string text = string.Format(format, prompt);
                if (label.text != text)
                {
                    label.text = text;
                    Vector2 preferred = label.GetPreferredValues(text);
                    plate.sizeDelta = preferred + padding * 2f;
                }
            }

            float target = show ? 1f : 0f;
            group.alpha = fadeSeconds > 0f
                ? Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fadeSeconds)
                : target;
        }

        private void ApplyStyle()
        {
            TextStyle style = GameSettings.Current.screenStyle;
            label.fontSize = size * style.scale;
            plateImage.color = style.backgroundColor;
            TextStyling.Apply(label, style, style.textColor, bold);
            if (!string.IsNullOrEmpty(label.text))
                plate.sizeDelta = label.GetPreferredValues(label.text) + padding * 2f;
        }

        private void BuildUi()
        {
            var root = new GameObject("Interaction Prompt UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            holder = (RectTransform)new GameObject("Prompt", typeof(RectTransform)).transform;
            holder.SetParent(root.transform, false);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(1200f, size * 2f);
            holder.anchoredPosition = position;

            plate = (RectTransform)new GameObject("Background", typeof(RectTransform), typeof(Image)).transform;
            plate.SetParent(holder, false);
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 0.5f);
            plateImage = plate.GetComponent<Image>();
            plateImage.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(holder, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = MainMenuController.ResolveFont(fontFile, font, "Interaction Prompt Font")
                ?? Resources.Load<TMP_FontAsset>("Fonts/OSerif-Regular")
                ?? TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = string.Empty;
        }
    }
}
