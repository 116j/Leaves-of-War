using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Self-contained end-credits sequence: a scrolling text crawl (custom
    /// font, custom music, colour or image background) with a scattered
    /// gallery of WIP images floating near the four screen corners for the
    /// whole duration. Not wired to any specific trigger yet - call
    /// <see cref="Play"/> from wherever the game decides the credits should
    /// start (e.g. after the final ending video).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreditsSequenceController : MonoBehaviour
    {
        [Header("Text")]
        [Tooltip("The full credits text. Each line becomes one line of the crawl - use blank lines for spacing between sections.")]
        [SerializeField, TextArea(10, 40)] private string creditsText;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField, Min(1f)] private float fontSize = 28f;
        [Tooltip("Fallback scroll speed in pixels/sec (1920x1080 reference), used only when no Music clip is assigned. When Music is set, the crawl's duration matches the music's length instead.")]
        [SerializeField, Min(1f)] private float scrollSpeed = 18f;
        [Tooltip("Extra blank space scrolled through after the last line, before Credits Finished fires.")]
        [SerializeField, Min(0f)] private float trailingSpace = 400f;

        [Header("Music")]
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;

        [Header("Background (Image takes precedence if both are set)")]
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Texture2D backgroundImage;

        [Header("WIP Gallery")]
        [Tooltip("Resources-relative folder containing the WIP images (e.g. a folder at Assets/Resources/Credits/WIP, entered here as 'Credits/WIP'). Any Texture2D in that folder is picked up automatically - no need to assign images one by one, and no need to set their Texture Type to Sprite.")]
        [SerializeField] private string wipImagesResourcesFolder;
        [Tooltip("How many floating images appear at once, scattered across the four corners.")]
        [SerializeField, Min(0)] private int wipImageSlotCount = 8;
        [Tooltip("Random size range for each image's longer edge, in pixels (1920x1080 reference). Aspect ratio is preserved.")]
        [SerializeField] private Vector2 wipImageSizeRange = new Vector2(120f, 260f);
        [SerializeField] private Vector2 wipImageRotationRange = new Vector2(-25f, 25f);
        [SerializeField, Range(0f, 1f)] private float wipImageAlpha = 0.85f;
        [Tooltip("How long each WIP image stays fully visible before fading to the next one.")]
        [SerializeField, Min(0.1f)] private float wipImageHoldSeconds = 5f;
        [Tooltip("Fade duration, in and out, between one WIP image and the next.")]
        [SerializeField, Min(0.05f)] private float wipImageFadeSeconds = 0.6f;

        /// <summary>Fired once the crawl has fully scrolled past the top of the screen.</summary>
        public event Action CreditsFinished;

        private GameObject canvasObject;
        private GameObject wipCanvasObject;
        private RectTransform crawlRect;
        private Coroutine scrollRoutine;

        /// <summary>Builds and starts the credits sequence. Safe to call once; build a new instance/scene reload to replay.</summary>
        public void Play()
        {
            BuildCanvas();
            BuildBackground();
            BuildWipGallery();
            BuildCrawlText();
            PlayMusic();

            scrollRoutine = StartCoroutine(ScrollRoutine());
        }

        /// <summary>Stops the crawl and tears down everything this created.</summary>
        public void Stop()
        {
            if (scrollRoutine != null)
            {
                StopCoroutine(scrollRoutine);
                scrollRoutine = null;
            }

            for (int i = 0; i < wipSlotRoutines.Count; i++)
            {
                if (wipSlotRoutines[i] != null)
                    StopCoroutine(wipSlotRoutines[i]);
            }
            wipSlotRoutines.Clear();
            wipUnseenPool.Clear();

            if (canvasObject != null)
            {
                Destroy(canvasObject);
                canvasObject = null;
            }

            if (wipCanvasObject != null)
            {
                Destroy(wipCanvasObject);
                wipCanvasObject = null;
            }

            AudioManager.Instance?.EndExclusiveMusic(stopMusic: true);
        }

        private void OnDestroy() => Stop();

        private void BuildCanvas()
        {
            Canvas outputCanvas = FindOutputCanvas();
            if (outputCanvas != null)
            {
                // Match CenteredAuthorialOverlay exactly: parenting under the
                // root canvas alone isn't enough - THAT canvas spans the
                // full physical screen. A generated-overlay
                // LowResolutionPresenter is what actually sizes/centres this
                // frame to the same native low-res world rect everything
                // else authorial uses.
                canvasObject = new GameObject(
                    "Credits Sequence",
                    typeof(RectTransform),
                    typeof(RawImage));
                canvasObject.transform.SetParent(outputCanvas.transform, false);
                canvasObject.transform.SetAsLastSibling();

                RawImage sizingImage = canvasObject.GetComponent<RawImage>();
                sizingImage.color = Color.clear;
                sizingImage.raycastTarget = false;

                canvasObject.AddComponent<LowResolutionPresenter>().MarkAsGeneratedOverlay();
                return;
            }

            canvasObject = new GameObject(
                "Credits Sequence",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
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

        private void BuildBackground()
        {
            if (backgroundImage != null)
            {
                var imageObject = new GameObject("Background Image", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(canvasObject.transform, false);
                Stretch((RectTransform)imageObject.transform);
                RawImage image = imageObject.GetComponent<RawImage>();
                image.texture = backgroundImage;
                image.raycastTarget = false;
                return;
            }

            var colorObject = new GameObject("Background Colour", typeof(RectTransform), typeof(Image));
            colorObject.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)colorObject.transform);
            Image colorImage = colorObject.GetComponent<Image>();
            colorImage.color = backgroundColor;
            colorImage.raycastTarget = false;
        }

        private readonly List<Texture2D> wipUnseenPool = new List<Texture2D>();
        private Texture2D[] wipAllTextures;
        private readonly List<Coroutine> wipSlotRoutines = new List<Coroutine>();

        private void BuildWipGallery()
        {
            if (wipImageSlotCount <= 0 || string.IsNullOrWhiteSpace(wipImagesResourcesFolder))
                return;

            wipAllTextures = Resources.LoadAll<Texture2D>(wipImagesResourcesFolder);
            if (wipAllTextures == null || wipAllTextures.Length == 0)
            {
                Debug.LogWarning(
                    $"[CreditsSequenceController] No textures found under Resources/{wipImagesResourcesFolder}.",
                    this);
                return;
            }

            // WIP images get their own INDEPENDENT full-screen canvas -
            // deliberately not the narrow native frame the crawl text lives
            // in - so they have the whole window's width to spread out in,
            // not just the small centred square.
            wipCanvasObject = new GameObject(
                "WIP Gallery Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            Canvas wipCanvas = wipCanvasObject.GetComponent<Canvas>();
            wipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            wipCanvas.sortingOrder = 24999; // just under the text/background canvas's own content.
            CanvasScaler wipScaler = wipCanvasObject.GetComponent<CanvasScaler>();
            wipScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            wipScaler.referenceResolution = new Vector2(1920f, 1080f);
            wipScaler.matchWidthOrHeight = 0.5f;

            var galleryParent = new GameObject("WIP Gallery", typeof(RectTransform));
            galleryParent.transform.SetParent(wipCanvasObject.transform, false);
            Stretch((RectTransform)galleryParent.transform);

            // Fill and shuffle the shared deck once, up front - each image
            // is handed out at most once across the whole gallery, never
            // refilled, so nothing repeats.
            wipUnseenPool.Clear();
            wipUnseenPool.AddRange(wipAllTextures);
            for (int i = wipUnseenPool.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (wipUnseenPool[i], wipUnseenPool[swapIndex]) = (wipUnseenPool[swapIndex], wipUnseenPool[i]);
            }

            // The crawl text sits in the horizontal band x=[0.2, 0.8] OF THE
            // NARROW NATIVE FRAME, for its entire scroll (top to bottom).
            // Measured against THIS wider, independent canvas the safe
            // margin is generally wider still - images are confined to the
            // left/right side strips, arranged in rows down each strip,
            // with each image's rendered WIDTH capped to fit inside its strip.
            const float textColumnHalfWidth = 0.1f; // generous margin now that this is measured against the WIDE canvas, not the narrow native frame the text itself lives in.
            RectTransform canvasRect = (RectTransform)wipCanvasObject.transform;
            float canvasWidth = Mathf.Max(1f, canvasRect.rect.width);
            float canvasHeight = Mathf.Max(1f, canvasRect.rect.height);
            const float rotationSafetyFactor = 1.4f; // headroom for up to ~±25 degrees of tilt.

            float sideStripWidth = canvasWidth * (0.5f - textColumnHalfWidth);
            // The widest an image is allowed to render at, leaving a little
            // breathing room from both the text column and the screen edge.
            float maxRenderedWidth = Mathf.Max(20f, sideStripWidth / rotationSafetyFactor - 8f);

            // Effective long-edge ceiling for a LANDSCAPE image (width IS
            // the long edge) - portrait images get more headroom since
            // their width is only longEdge*aspect, always less than this.
            Vector2 effectiveSizeRange = new Vector2(
                Mathf.Min(wipImageSizeRange.x, maxRenderedWidth),
                Mathf.Min(wipImageSizeRange.y, maxRenderedWidth));

            int slotsPerSide = Mathf.CeilToInt(wipImageSlotCount / 2f);

            // Divide the vertical space into one explicit band per row, so
            // images in different rows can never touch no matter how big
            // either one happens to roll - each image's height is capped
            // to fit inside its own band.
            const float verticalEdgeMargin = 0.04f;
            float usableHeightFraction = 1f - 2f * verticalEdgeMargin;
            float rowBandFraction = usableHeightFraction / Mathf.Max(1, slotsPerSide);
            float rowBandHeightPixels = rowBandFraction * canvasHeight;
            // Leaves a gap between rows and covers rotation's diagonal growth.
            // Deliberately allows some collage-style overlap at the
            // edges/corners between rows now (rather than guaranteeing zero
            // contact) - with many simultaneous slots, that's the only way
            // to keep each image genuinely large and visible. Each image
            // can render up to ~1.6x its own row band's height, meaning
            // roughly its outer ~30% can overlap into a neighbouring row.
            float maxHeightPerRow = Mathf.Max(20f, rowBandHeightPixels * 1.6f / rotationSafetyFactor);

            int[] rowsUsed = new int[2];

            for (int i = 0; i < wipImageSlotCount; i++)
            {
                bool leftSide = i % 2 == 0;
                int sideIndex = leftSide ? 0 : 1;
                int row = rowsUsed[sideIndex]++;

                // A fixed point roughly in the middle of this side's strip,
                // with a little per-slot jitter so slots don't all line up
                // in a perfectly straight column.
                float stripCentre = leftSide
                    ? sideStripWidth * 0.5f / canvasWidth
                    : 1f - sideStripWidth * 0.5f / canvasWidth;
                float jitter = UnityEngine.Random.Range(-0.25f, 0.25f) * (sideStripWidth * 0.4f / canvasWidth);
                float anchorX = Mathf.Clamp(
                    stripCentre + jitter,
                    leftSide ? 0.02f : 1f - (sideStripWidth / canvasWidth) + 0.02f,
                    leftSide ? sideStripWidth / canvasWidth - 0.02f : 0.98f);

                // The centre of this row's own vertical band.
                float anchorY = Mathf.Clamp01(verticalEdgeMargin + (row + 0.5f) * rowBandFraction);

                var slotObject = new GameObject($"WIP Slot {i}", typeof(RectTransform), typeof(RawImage));
                slotObject.transform.SetParent(galleryParent.transform, false);
                RectTransform slotRect = (RectTransform)slotObject.transform;
                slotRect.anchorMin = new Vector2(anchorX, anchorY);
                slotRect.anchorMax = new Vector2(anchorX, anchorY);
                slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = Vector2.zero;

                RawImage image = slotObject.GetComponent<RawImage>();
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0f);

                // Small random head start so slots don't all fade in lockstep.
                float initialDelay = UnityEngine.Random.Range(0f, wipImageHoldSeconds);
                wipSlotRoutines.Add(StartCoroutine(
                    WipSlotRoutine(slotRect, image, initialDelay, effectiveSizeRange, maxHeightPerRow)));
            }
        }

        /// <summary>
        /// One floating WIP image slot, at a fixed screen position: wait,
        /// then keep drawing a not-yet-seen texture from the shared deck -
        /// fade in, hold, fade out - until the deck runs out. Each image is
        /// shown at most once across the whole gallery; once the deck is
        /// empty the slot simply stops (stays faded out) rather than
        /// repeating anything. Only size and rotation are re-randomised
        /// each cycle - position stays fixed so slots never drift into
        /// each other.
        /// </summary>
        private IEnumerator WipSlotRoutine(
            RectTransform rect, RawImage image, float initialDelay, Vector2 effectiveSizeRange, float maxHeight)
        {
            if (initialDelay > 0f)
                yield return new WaitForSecondsRealtime(initialDelay);

            while (TryGetNextUnseenWipTexture(out Texture2D texture))
            {
                RandomizeWipSizeAndRotation(rect, texture, effectiveSizeRange, maxHeight);
                image.texture = texture;

                yield return FadeRawImage(image, 0f, wipImageAlpha, wipImageFadeSeconds);
                yield return new WaitForSecondsRealtime(wipImageHoldSeconds);
                yield return FadeRawImage(image, wipImageAlpha, 0f, wipImageFadeSeconds);
            }
        }

        private bool TryGetNextUnseenWipTexture(out Texture2D texture)
        {
            if (wipUnseenPool.Count == 0)
            {
                texture = null;
                return false;
            }

            int lastIndex = wipUnseenPool.Count - 1;
            texture = wipUnseenPool[lastIndex];
            wipUnseenPool.RemoveAt(lastIndex);
            return true;
        }

        private void RandomizeWipSizeAndRotation(
            RectTransform rect, Texture2D texture, Vector2 effectiveSizeRange, float maxHeight)
        {
            float aspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;

            // The largest long-edge value THIS image can use while staying
            // within its row's height budget - landscape images (aspect>=1)
            // have height=longEdge/aspect, portrait images have
            // height=longEdge directly.
            float maxLongEdgeForHeight = aspect >= 1f ? maxHeight * aspect : maxHeight;
            float cappedMax = Mathf.Min(effectiveSizeRange.y, maxLongEdgeForHeight);
            float cappedMin = Mathf.Min(effectiveSizeRange.x, cappedMax);

            float longEdge = UnityEngine.Random.Range(cappedMin, Mathf.Max(cappedMin, cappedMax));
            rect.sizeDelta = aspect >= 1f
                ? new Vector2(longEdge, longEdge / aspect)
                : new Vector2(longEdge * aspect, longEdge);
            rect.localRotation = Quaternion.Euler(
                0f, 0f, UnityEngine.Random.Range(wipImageRotationRange.x, wipImageRotationRange.y));
        }

        private IEnumerator FadeRawImage(RawImage image, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(from, to, elapsed / duration);
                image.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            image.color = new Color(1f, 1f, 1f, to);
        }

        private void BuildCrawlText()
        {
            var textObject = new GameObject(
                "Crawl Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);

            crawlRect = (RectTransform)textObject.transform;
            crawlRect.anchorMin = new Vector2(0.2f, 0f);
            crawlRect.anchorMax = new Vector2(0.8f, 0f);
            crawlRect.pivot = new Vector2(0.5f, 0f);
            // Start just below the bottom edge of the screen.
            crawlRect.anchoredPosition = new Vector2(0f, 0f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Top;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.text = creditsText ?? string.Empty;

            // Height must be known up front to know how far "fully scrolled
            // past the top" is - ContentSizeFitter measures the wrapped
            // text at the crawl's fixed width, then we read the result.
            var fitter = textObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void PlayMusic()
        {
            if (music == null)
                return;

            AudioManager.Instance?.PlayExclusiveMusic(music, loop: true, volumeScale: musicVolume);
        }

        private IEnumerator ScrollRoutine()
        {
            // Wait until the ContentSizeFitter has actually measured a
            // non-zero height for the wrapped text, rather than a fixed
            // frame count - a fixed wait can read a height of 0 (or too
            // small) if layout hasn't settled yet, which would cut the
            // travel distance short and make the whole crawl feel far
            // faster than intended.
            float textHeight = 0f;
            for (int i = 0; i < 10 && textHeight <= 0f; i++)
            {
                yield return null;
                textHeight = crawlRect != null ? crawlRect.rect.height : 0f;
            }

            float screenHeight = ((RectTransform)canvasObject.transform).rect.height;
            // Start with the WHOLE text box below the visible screen (not
            // just its bottom edge at the screen's bottom edge) - otherwise
            // the first screenful of text is visible immediately, looking
            // like the crawl "already started" partway through.
            float startY = -textHeight;
            float travelDistance = screenHeight + textHeight + trailingSpace;

            // If a music clip is assigned, the crawl takes exactly as long
            // as the music - covering the same distance over that duration
            // - instead of using the authored Scroll Speed as a fixed rate.
            float effectiveSpeed = music != null && music.length > 0f
                ? travelDistance / music.length
                : scrollSpeed;

            float traveled = 0f;
            while (traveled < travelDistance)
            {
                float delta = effectiveSpeed * Time.unscaledDeltaTime;
                traveled += delta;
                if (crawlRect != null)
                    crawlRect.anchoredPosition = new Vector2(0f, startY + traveled);
                yield return null;
            }

            scrollRoutine = null;
            CreditsFinished?.Invoke();
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