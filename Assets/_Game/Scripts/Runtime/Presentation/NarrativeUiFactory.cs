using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Builds the shared runtime UI hierarchy from one retro theme. All
    /// resolution-dependent diegetic geometry originates in 320x240 base pixels.
    /// </summary>
    internal static class NarrativeUiFactory
    {
        public static NarrativeUiView Create(
            Transform owner,
            Camera lowResolutionCamera,
            TMP_FontAsset diegeticFont,
            TMP_FontAsset authorialFont,
            TMP_FontAsset taskFont,
            RetroUiTheme theme)
        {
            theme = RetroUiTheme.Resolve(theme);
            var view = new NarrativeUiView();

            Camera diegeticCamera = lowResolutionCamera;
            Vector2Int diegeticResolution = RetroResolution.WorldSize;
            if (DiegeticUiCompositor.TryCreate(owner, out DiegeticUiCompositor compositor))
            {
                view.DiegeticCompositor = compositor;
                diegeticCamera = compositor.Camera;
                diegeticResolution = RetroResolution.DiegeticUiSize;
            }

            Canvas diegeticCanvas = CreateCanvas(
                owner,
                "DiegeticCanvas",
                RenderMode.ScreenSpaceCamera,
                diegeticCamera,
                20);
            diegeticCanvas.pixelPerfect = true;
            CanvasScaler diegeticScaler = diegeticCanvas.gameObject.AddComponent<CanvasScaler>();
            diegeticScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            diegeticScaler.referenceResolution = diegeticResolution;
            diegeticScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            diegeticScaler.matchWidthOrHeight = 0.5f;

            view.SubtitlePanel = CreatePanel(
                diegeticCanvas.transform,
                "Subtitles",
                Vector2.zero,
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, theme.SubtitleHeight),
                theme.SubtitlePanelColor);
            view.SpeakerText = CreateText(
                view.SubtitlePanel.transform,
                "Speaker",
                diegeticFont,
                theme.DiegeticFontSize,
                theme.ScaledOffsetMin(theme.SpeakerRect),
                theme.ScaledOffsetMax(theme.SpeakerRect),
                TextAlignmentOptions.TopLeft,
                theme.SpeakerColor);
            view.SubtitleText = CreateText(
                view.SubtitlePanel.transform,
                "Line",
                diegeticFont,
                theme.DiegeticFontSize,
                theme.ScaledOffsetMin(theme.SubtitleRect),
                theme.ScaledOffsetMax(theme.SubtitleRect),
                TextAlignmentOptions.TopLeft,
                theme.SubtitleColor);
            view.SubtitleText.enableAutoSizing = true;
            view.SubtitleText.fontSizeMin = theme.DiegeticFontSize * 0.25f;
            view.SubtitleText.fontSizeMax = theme.DiegeticFontSize;

            view.InteractionPromptText = CreateText(
            diegeticCanvas.transform,
            "InteractionPrompt",
            diegeticFont,
            theme.DiegeticFontSize,
            theme.ScaledOffsetMin(theme.InteractionPromptRect),
            theme.ScaledOffsetMax(theme.InteractionPromptRect),
            TextAlignmentOptions.Bottom,
            theme.PromptColor);

            view.CarryStatusText = CreateText(
                diegeticCanvas.transform,
                "Carrying",
                diegeticFont,
                theme.DiegeticSmallFontSize,
                theme.ScaledOffsetMin(theme.CarryStatusRect),
                theme.ScaledOffsetMax(theme.CarryStatusRect),
                TextAlignmentOptions.BottomLeft,
                theme.PromptColor);
            view.CarryStatusText.gameObject.SetActive(false);

            view.StatusText = CreateText(
                diegeticCanvas.transform,
                "Status",
                diegeticFont,
                theme.DiegeticSmallFontSize,
                theme.ScaledOffsetMin(theme.StatusRect),
                theme.ScaledOffsetMax(theme.StatusRect),
                TextAlignmentOptions.Top,
                theme.StatusColor);
            view.StatusText.gameObject.SetActive(false);

            view.GameplayHudText = CreateText(
                diegeticCanvas.transform,
                "GameplayHud",
                taskFont,
                theme.DiegeticSmallFontSize,
                theme.ScaledOffsetMin(theme.GameplayHudRect),
                theme.ScaledOffsetMax(theme.GameplayHudRect),
                TextAlignmentOptions.Top,
                theme.GameplayHudColor);
            view.GameplayHudText.gameObject.SetActive(false);
            // Long task instructions can wrap to more lines than this fixed
            // box has room for - auto-size instead of letting the overflow
            // spill downward into the Carrying label just below it.
            view.GameplayHudText.enableAutoSizing = true;
            view.GameplayHudText.fontSizeMin = theme.DiegeticSmallFontSize * 0.6f;
            view.GameplayHudText.fontSizeMax = theme.DiegeticSmallFontSize;

            view.ObjectiveDirectionArrow = CreateObjectiveDirectionArrow(
                diegeticCanvas.transform,
                theme);

            Vector2 documentInset = Vector2.one * theme.DocumentInset;
            view.DocumentPanel = CreatePanel(
                diegeticCanvas.transform,
                "Document",
                Vector2.zero,
                Vector2.one,
                documentInset,
                -documentInset,
                theme.DocumentPanelColor);

            CreateBackgroundImage(view.DocumentPanel.transform, theme.DocumentBackgroundImage);

            view.DocumentTitleText = CreateText(
                            view.DocumentPanel.transform,
                "Title",
                diegeticFont,
                theme.DiegeticFontSize,
                theme.ScaledOffsetMin(theme.DocumentTitleRect),
                theme.ScaledOffsetMax(theme.DocumentTitleRect),
                TextAlignmentOptions.Top,
                theme.DocumentTitleColor);
            view.DocumentBodyText = CreateText(
                view.DocumentPanel.transform,
                "Body",
                diegeticFont,
                theme.DiegeticFontSize,
                theme.ScaledOffsetMin(theme.DocumentBodyRect),
                theme.ScaledOffsetMax(theme.DocumentBodyRect),
                TextAlignmentOptions.TopLeft,
                theme.DocumentBodyColor);
            // Authored document pages vary in length. Keep the body inside its
            // reserved reader area so it cannot paint over the page/close prompt.
            // The 8px base minimum remains legible at the doubled UI resolution.
            view.DocumentBodyText.enableAutoSizing = true;
            view.DocumentBodyText.fontSizeMin = theme.DocumentBodyMinFontSize;
            view.DocumentBodyText.fontSizeMax = theme.DiegeticFontSize;
            view.DocumentBodyText.overflowMode = TextOverflowModes.Ellipsis;
            view.DocumentPageText = CreateText(
                view.DocumentPanel.transform,
                "Page",
                diegeticFont,
                theme.DiegeticSmallFontSize,
                theme.ScaledOffsetMin(theme.DocumentPageRect),
                theme.ScaledOffsetMax(theme.DocumentPageRect),
                TextAlignmentOptions.BottomRight,
                theme.DocumentPageColor);

            Canvas authorialCanvas = CreateCanvas(
                owner,
                "AuthorialCanvas",
                RenderMode.ScreenSpaceOverlay,
                null,
                100);
            CanvasScaler authorialScaler = authorialCanvas.gameObject.AddComponent<CanvasScaler>();
            authorialScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            authorialScaler.referenceResolution = theme.AuthorialReferenceResolution;
            authorialScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            authorialScaler.matchWidthOrHeight = 0.5f;
            authorialCanvas.gameObject.AddComponent<GraphicRaycaster>();

            view.AuthorialPanel = CreatePanel(
                authorialCanvas.transform,
                "Card",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                theme.AuthorialPanelColor);
            Vector2 cardInset = Vector2.one * theme.CardMargin;
            view.AuthorialText = CreateText(
                view.AuthorialPanel.transform,
                "CardText",
                authorialFont,
                theme.CardFontSize,
                cardInset,
                -cardInset,
                TextAlignmentOptions.Center,
                theme.AuthorialTextColor);

            view.ChoicePanel = CreatePanel(
                authorialCanvas.transform,
                "Choice",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                theme.ChoicePanelColor);
            TMP_Text prompt = CreateText(
                view.ChoicePanel.transform,
                "Prompt",
                authorialFont,
                theme.ChoicePromptFontSize,
                RectMin(theme.ChoicePromptRect),
                RectMax(theme.ChoicePromptRect),
                TextAlignmentOptions.Center,
                theme.AuthorialTextColor);
            prompt.text = "WHAT WILL YOU DO?";

            for (int i = 0; i < 3; i++)
                CreateChoiceButton(view, i, authorialFont, theme);

            view.HideNarrativePanels();
            return view;
        }

        private static void CreateChoiceButton(
            NarrativeUiView view,
            int index,
            TMP_FontAsset font,
            RetroUiTheme theme)
        {
            var buttonObject = new GameObject(
                $"Choice {index + 1}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(view.ChoicePanel.transform, false);
            buttonObject.layer = view.ChoicePanel.layer;
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = theme.ChoiceButtonSize;
            rect.anchoredPosition = new Vector2(
                0f,
                theme.ChoiceButtonStartY - index * theme.ChoiceButtonSpacing);

            Image image = buttonObject.GetComponent<Image>();
            image.color = theme.ChoiceButtonColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = theme.ChoiceHighlightColor;
            colors.pressedColor = theme.ChoicePressedColor;
            button.colors = colors;

            Vector2 labelInset = theme.ChoiceButtonLabelInset;
            TMP_Text label = CreateText(
                buttonObject.transform,
                "Label",
                font,
                theme.ChoiceButtonFontSize,
                labelInset,
                -labelInset,
                TextAlignmentOptions.Center,
                theme.ChoiceTextColor);
            view.ChoiceButtons.Add(button);
            view.ChoiceLabels.Add(label);
        }

        private static Canvas CreateCanvas(
            Transform owner,
            string objectName,
            RenderMode mode,
            Camera worldCamera,
            int sortingOrder)
        {
            var canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(owner, false);
            canvasObject.layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = mode;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = 0.2f;
            canvas.sortingOrder = sortingOrder;
            return canvas;
        }

        private static void CreateBackgroundImage(Transform parent, Texture2D texture)
        {
            if (texture == null)
                return;

            var backgroundObject = new GameObject("Background Image", typeof(RectTransform), typeof(RawImage));
            backgroundObject.transform.SetParent(parent, false);
            // First sibling, so the panel's own dark tint (already on "parent"
            // itself) and every other child added afterwards (title, body, page
            // number) draw on top of it.
            backgroundObject.transform.SetAsFirstSibling();

            RectTransform rect = (RectTransform)backgroundObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = backgroundObject.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static ObjectiveDirectionArrowGraphic CreateObjectiveDirectionArrow(
            Transform parent,
            RetroUiTheme theme)
        {
            var arrowObject = new GameObject(
                "ObjectiveDirection",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ObjectiveDirectionArrowGraphic));
            arrowObject.transform.SetParent(parent, false);
            arrowObject.layer = parent.gameObject.layer;

            RectTransform rect = (RectTransform)arrowObject.transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * theme.ObjectiveArrowSize;
            rect.anchoredPosition = theme.ObjectiveArrowOffset;

            ObjectiveDirectionArrowGraphic arrow =
                arrowObject.GetComponent<ObjectiveDirectionArrowGraphic>();
            arrow.color = theme.ObjectiveArrowFarColor;
            arrow.raycastTarget = false;
            arrowObject.SetActive(false);
            return arrow;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string objectName,
            TMP_FontAsset font,
            float size,
            Vector2 offsetMin,
            Vector2 offsetMax,
            TextAlignmentOptions alignment,
            Color color)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            textObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Vector2 RectMin(Vector4 rect) => new Vector2(rect.x, rect.y);
        private static Vector2 RectMax(Vector4 rect) => new Vector2(rect.z, rect.w);
    }
}