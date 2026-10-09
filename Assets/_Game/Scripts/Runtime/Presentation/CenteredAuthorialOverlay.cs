using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Temporary native-resolution authorial UI constrained to the same centred
    /// 4:3 frame as the low-resolution world output. Set-pieces and credits use
    /// this instead of creating another full-screen presentation convention.
    /// </summary>
    internal sealed class CenteredAuthorialOverlay
    {
        private const string AuthorialFontAddress = "Fonts/CormorantGaramond";

        private readonly GameObject ownedCanvas;
        private readonly GameObject frameObject;
        private readonly RawImage background;
        private readonly CanvasGroup canvasGroup;
        private readonly TMP_Text heading;
        private readonly TMP_Text body;
        private readonly TMP_Text prompt;

        private CenteredAuthorialOverlay(
            GameObject ownedCanvas,
            GameObject frameObject,
            RawImage background,
            CanvasGroup canvasGroup,
            TMP_Text heading,
            TMP_Text body,
            TMP_Text prompt)
        {
            this.ownedCanvas = ownedCanvas;
            this.frameObject = frameObject;
            this.background = background;
            this.canvasGroup = canvasGroup;
            this.heading = heading;
            this.body = body;
            this.prompt = prompt;
        }

        public RectTransform Frame => frameObject != null
            ? frameObject.GetComponent<RectTransform>()
            : null;

        public RectTransform Body => body != null ? body.rectTransform : null;

        public float Alpha
        {
            get => canvasGroup != null ? canvasGroup.alpha : 0f;
            set
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Clamp01(value);
            }
        }

        public float BackgroundAlpha
        {
            get => background != null ? background.color.a : 0f;
            set
            {
                if (background == null)
                    return;

                Color color = background.color;
                color.a = Mathf.Clamp01(value);
                background.color = color;
            }
        }

        public static CenteredAuthorialOverlay Create(
            string objectName,
            Color backgroundColor,
            Color accentColor,
            RetroUiTheme theme = null)
        {
            theme = RetroUiTheme.Resolve(theme);
            Canvas outputCanvas = FindOutputCanvas();
            GameObject fallbackCanvas = null;
            if (outputCanvas == null)
            {
                fallbackCanvas = new GameObject(
                    $"{objectName} Canvas",
                    typeof(RectTransform),
                    typeof(Canvas));
                outputCanvas = fallbackCanvas.GetComponent<Canvas>();
                outputCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            int uiLayer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
            var frame = new GameObject(objectName, typeof(RectTransform));
            frame.SetActive(false);
            frame.AddComponent<CanvasRenderer>();
            frame.AddComponent<RawImage>();
            frame.AddComponent<CanvasGroup>();
            frame.AddComponent<LowResolutionPresenter>().MarkAsGeneratedOverlay();
            frame.transform.SetParent(outputCanvas.transform, false);
            frame.transform.SetAsLastSibling();
            SetLayerRecursively(frame, uiLayer);
            frame.SetActive(true);

            RawImage image = frame.GetComponent<RawImage>();
            image.color = backgroundColor;
            image.raycastTarget = false;

            CanvasGroup group = frame.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(AuthorialFontAddress) ??
                TMP_Settings.defaultFontAsset;
            TMP_Text heading = CreateText(
                frame.transform,
                "Heading",
                font,
                theme.OverlayHeadingFontSize,
                RectMin(theme.OverlayHeadingAnchors),
                RectMax(theme.OverlayHeadingAnchors),
                TextAlignmentOptions.Center,
                accentColor);
            TMP_Text body = CreateText(
                frame.transform,
                "Body",
                font,
                theme.OverlayBodyFontSize * 1.4f,
                RectMin(theme.OverlayBodyAnchors),
                RectMax(theme.OverlayBodyAnchors),
                TextAlignmentOptions.Center,
                Color.white,
                addOutline: true);
            TMP_Text prompt = CreateText(
                frame.transform,
                "Advance",
                font,
                theme.OverlayPromptFontSize,
                RectMin(theme.OverlayPromptAnchors),
                RectMax(theme.OverlayPromptAnchors),
                TextAlignmentOptions.Center,
                theme.OverlayPromptColor);

            return new CenteredAuthorialOverlay(
                fallbackCanvas,
                frame,
                image,
                group,
                heading,
                body,
                prompt);
        }

        public void SetContent(string headingText, string bodyText, string promptText)
        {
            if (heading != null)
                heading.text = headingText ?? string.Empty;
            if (body != null)
                body.text = bodyText ?? string.Empty;
            if (prompt != null)
                prompt.text = promptText ?? string.Empty;
        }

        public void SetBodyLayout(
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            if (body == null)
                return;

            RectTransform rect = body.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            body.fontSize = fontSize;
            body.alignment = alignment;
        }

        public void Dispose()
        {
            DestroyObject(frameObject);
            DestroyObject(ownedCanvas);
        }

        private static Canvas FindOutputCanvas()
        {
            GameSession session = GameSession.Instance;
            if (session == null ||
                !session.SceneServices.TryGetWorldOutput(
                    out LowResolutionPresenter presenter,
                    out _))
            {
                return null;
            }

            Canvas canvas = presenter.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas : null;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string objectName,
            TMP_FontAsset font,
            float fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAlignmentOptions alignment,
            Color color,
            bool addOutline = false)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            textObject.layer = parent.gameObject.layer;

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;

            if (addOutline)
            {
                // A unique material instance, so the outline only affects
                // this text object - not every other text sharing the same
                // font asset's default (shared) material.
                text.fontMaterial = new Material(text.fontSharedMaterial);
                text.outlineWidth = 0.2f;
                text.outlineColor = Color.black;
            }

            return text;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private static Vector2 RectMin(Vector4 rect) => new Vector2(rect.x, rect.y);

        private static Vector2 RectMax(Vector4 rect) => new Vector2(rect.z, rect.w);

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}