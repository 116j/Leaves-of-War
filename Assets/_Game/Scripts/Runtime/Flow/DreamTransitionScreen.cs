using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime
{
    /// <summary>
    /// A persistent authorial card that can cover a single-mode scene load.
    /// It deliberately reuses the main menu's cornice without coupling the
    /// narrative runner to the menu scene or its controller.
    /// </summary>
    internal sealed class DreamTransitionScreen
    {
        private const string CorniceAddress = "MainMenu/Cornice";
        private const string AuthorialFontAddress = "Fonts/CormorantGaramond";
        private const float ContinuePromptDelaySeconds = 2f;
        private const float ContinuePromptFadeSeconds = 0.25f;
        private const float ContinueHoldSeconds = 1f;
        private const float ContinueHoldDecayRate = 2f;

        private readonly GameObject canvasObject;
        private readonly CanvasGroup canvasGroup;
        private readonly ISequenceClock clock;
        private readonly float shownAt;
        private readonly CanvasGroup continuePrompt;
        private readonly RectTransform continueFill;

        private DreamTransitionScreen(
            GameObject canvasObject,
            CanvasGroup canvasGroup,
            ISequenceClock clock,
            CanvasGroup continuePrompt,
            RectTransform continueFill)
        {
            this.canvasObject = canvasObject;
            this.canvasGroup = canvasGroup;
            this.clock = clock ?? UnitySequenceClock.Instance;
            this.continuePrompt = continuePrompt;
            this.continueFill = continueFill;
            shownAt = this.clock.UnscaledTime;
        }

        public static DreamTransitionScreen Create(
            string cardText,
            ISequenceClock clock)
        {
            if (!Application.isPlaying)
                return null;

            // Carica le impostazioni condivise per la texture e il colore
            SceneTransitionSettings transitionSettings = Resources.Load<SceneTransitionSettings>(SceneTransitionSettings.ResourcePath);

            Texture2D frameTexture = transitionSettings != null ? transitionSettings.FrameTexture : null;
            Color textColor = transitionSettings != null ? transitionSettings.TextColor : new Color(0.88f, 0.84f, 0.72f);

            var canvasObject = new GameObject(
                "Dream Transition",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            Object.DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = true;

            // 1. Sfondo scuro di base (garantisce leggibilità se la texture della cornice ha trasparenze)
            Image background = CreateImage(
                canvasObject.transform,
                "Background",
                new Color(0.018f, 0.016f, 0.013f, 1f));
            Stretch(background.rectTransform);

            // 2. La cornice come sfondo principale a tutto schermo
            Texture2D cornice = frameTexture ?? Resources.Load<Texture2D>(CorniceAddress);
            if (cornice != null)
            {
                var corniceObject = new GameObject(
                    "Cornice Background",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

                // La cornice viene attaccata direttamente alla Canvas, così sta dietro a tutto il resto
                corniceObject.transform.SetParent(canvasObject.transform, false);
                Stretch((RectTransform)corniceObject.transform); // Occupa tutto lo schermo

                RawImage image = corniceObject.GetComponent<RawImage>();
                image.texture = cornice;
                image.raycastTarget = false;
            }
            else
            {
                Debug.LogWarning(
                    $"Dream transition frame is missing. Checked SceneTransitionSettings and Resources/{CorniceAddress}.");
            }

            // 3. Contenitore centrale per il testo e il prompt di continuazione
            var frameObject = new GameObject("Frame Content", typeof(RectTransform));
            frameObject.transform.SetParent(canvasObject.transform, false);
            RectTransform frameRect = (RectTransform)frameObject.transform;
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(1920f, 1080f);
            frameRect.anchoredPosition = Vector2.zero;

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(AuthorialFontAddress) ??
                TMP_Settings.defaultFontAsset;

            var textObject = new GameObject(
                "Transition Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(frameObject.transform, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = new Vector2(0.16f, 0.34f);
            textRect.anchorMax = new Vector2(0.84f, 0.66f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.enableAutoSizing = true;
            text.fontSizeMax = 74f;
            text.fontSizeMin = 28f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.color = textColor; // Usa il colore dalle impostazioni
            text.raycastTarget = false;
            text.text = cardText ?? string.Empty;

            BuildContinuePrompt(frameObject.transform, font, out CanvasGroup continuePrompt, out RectTransform continueFill);

            return new DreamTransitionScreen(
                canvasObject,
                group,
                clock,
                continuePrompt,
                continueFill);
        }

        public IEnumerator FadeIn(float duration)
        {
            yield return FadeTo(1f, duration);
        }

        public IEnumerator HoldAndFadeOut(float minimumVisibleSeconds, float fadeOutSeconds)
        {
            float remaining = Mathf.Max(
                0f,
                minimumVisibleSeconds - (clock.UnscaledTime - shownAt));
            while (remaining > 0f)
            {
                remaining -= clock.UnscaledDeltaTime;
                yield return null;
            }

            yield return FadeTo(0f, fadeOutSeconds);
        }

        /// <summary>
        /// Waits for the player to deliberately continue a silent arrival card.
        /// Dream transitions remain timed to their authored music; this is only
        /// used by ordinary between-scene cards.
        /// </summary>
        public IEnumerator HoldToContinueAndFadeOut(float fadeOutSeconds)
        {
            float holdElapsed = 0f;
            while (holdElapsed < ContinueHoldSeconds)
            {
                if (IsContinueHeld())
                {
                    holdElapsed += clock.UnscaledDeltaTime;
                }
                else
                {
                    holdElapsed = Mathf.Max(
                        0f,
                        holdElapsed - (clock.UnscaledDeltaTime * ContinueHoldDecayRate));
                }

                UpdateContinuePrompt(holdElapsed / ContinueHoldSeconds);
                yield return null;
            }

            UpdateContinuePrompt(1f);
            yield return FadeTo(0f, fadeOutSeconds);
        }

        public void Dispose()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            if (canvasObject != null)
                Object.Destroy(canvasObject);
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (canvasGroup == null)
                yield break;

            float startAlpha = canvasGroup.alpha;
            if (duration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += clock.UnscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        private void UpdateContinuePrompt(float normalizedHold)
        {
            if (continuePrompt == null)
                return;

            float visibleSeconds = clock.UnscaledTime - shownAt;
            if (visibleSeconds >= ContinuePromptDelaySeconds)
            {
                continuePrompt.alpha = Mathf.MoveTowards(
                    continuePrompt.alpha,
                    1f,
                    clock.UnscaledDeltaTime / ContinuePromptFadeSeconds);
            }

            if (continueFill != null)
                continueFill.anchorMax = new Vector2(Mathf.Clamp01(normalizedHold), 1f);
        }

        private static bool IsContinueHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed)
                return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null &&
                (gamepad.buttonSouth.isPressed || gamepad.startButton.isPressed);
        }

        private static void BuildContinuePrompt(
            Transform parent,
            TMP_FontAsset font,
            out CanvasGroup prompt,
            out RectTransform fill)
        {
            var promptObject = new GameObject(
                "Continue Prompt",
                typeof(RectTransform),
                typeof(CanvasGroup));
            promptObject.transform.SetParent(parent, false);

            RectTransform promptRect = (RectTransform)promptObject.transform;
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.sizeDelta = new Vector2(460f, 58f);
            promptRect.anchoredPosition = new Vector2(0f, 250f);

            prompt = promptObject.GetComponent<CanvasGroup>();
            prompt.alpha = 0f;
            prompt.interactable = false;
            prompt.blocksRaycasts = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(promptObject.transform, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, 14f);
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 26f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = new Color(0.88f, 0.86f, 0.80f, 0.85f);
            label.raycastTarget = false;
            label.text = "HOLD TO CONTINUE";

            var trackObject = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackObject.transform.SetParent(promptObject.transform, false);
            RectTransform trackRect = (RectTransform)trackObject.transform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.sizeDelta = new Vector2(0f, 4f);
            trackRect.anchoredPosition = Vector2.zero;
            Image trackImage = trackObject.GetComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.18f);
            trackImage.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(trackObject.transform, false);
            fill = (RectTransform)fillObject.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.88f, 0.86f, 0.80f, 0.92f);
            fillImage.raycastTarget = false;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}