using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Full-screen startup video shown by <see cref="BootController"/> while the
    /// main menu loads behind it. The presentation is generated at runtime in the
    /// same spirit as <see cref="MainMenuController"/>, so Boot.unity stays a bare
    /// scene holding one component.
    /// </summary>
    /// <remarks>
    /// Playback reads the file from StreamingAssets over <see cref="VideoSource.Url"/>
    /// rather than a compiled <see cref="VideoClip"/>. WebGL cannot use VideoClip at
    /// all - the browser decodes the file itself - and the URL path behaves the same
    /// on desktop, so both platforms share one code path.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StartupVideoScreen : MonoBehaviour
    {
        private const float FallbackAspect = 16f / 9f;

        private const float SkipPromptFadeSeconds = 0.25f;

        private VideoPlayer videoPlayer;
        private RawImage videoImage;
        private AspectRatioFitter aspectFitter;
        private GameObject presentationRoot;
        private GameObject cameraObject;
        private CanvasGroup skipPrompt;
        private RectTransform skipFill;
        private bool skipPromptRevealed;

        /// <summary>
        /// True while any skip control is held: a key, the left mouse button,
        /// a gamepad button, or a finger on a touch screen.
        /// </summary>
        public static bool IsSkipHeld =>
            (Keyboard.current != null && Keyboard.current.anyKey.isPressed) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) ||
            (Gamepad.current != null && (Gamepad.current.buttonSouth.isPressed || Gamepad.current.startButton.isPressed));

        /// <summary>The clip played to its end.</summary>
        public bool HasFinished { get; private set; }

        /// <summary>The clip could not be opened or decoded, so there is nothing to wait for.</summary>
        public bool HasFailed { get; private set; }

        private TMP_FontAsset promptFont;
        private bool promptBold;
        private SkipPromptStyle promptStyle;

        public void Begin(string streamingAssetsFileName, TMP_FontAsset font = null, bool bold = false, SkipPromptStyle style = null)
        {
            promptFont = font;
            promptBold = bold;
            promptStyle = style;
            BuildPresentation();

            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);

            ConfigureAudio();

            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.loopPointReached += OnLoopPointReached;
            videoPlayer.errorReceived += OnErrorReceived;
            videoPlayer.Prepare();
        }

        /// <summary>
        /// Drives the hold-to-skip affordance. The prompt stays hidden until the
        /// player first touches a control: someone content to watch the ident is
        /// never told about a control they did not ask for, while someone who
        /// taps a key immediately learns that a hold is what is wanted.
        /// </summary>
        /// <param name="normalizedHold">Hold completion from 0 to 1.</param>
        public void SetSkipProgress(float normalizedHold)
        {
            if (skipPrompt == null)
                return;

            if (normalizedHold > 0f)
                skipPromptRevealed = true;

            skipFill.anchorMax = new Vector2(Mathf.Clamp01(normalizedHold), 1f);
        }

        private void Update()
        {
            if (skipPrompt == null || !skipPromptRevealed)
                return;

            // Fade in rather than pop, so the prompt does not punch a hole in the
            // director's opening frames.
            skipPrompt.alpha = Mathf.MoveTowards(
                skipPrompt.alpha,
                1f,
                Time.unscaledDeltaTime / SkipPromptFadeSeconds);
        }

        /// <summary>
        /// Tears the overlay down before the next scene activates so the menu is
        /// never composited underneath a stale video frame.
        /// </summary>
        public void Dismiss()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepareCompleted;
                videoPlayer.loopPointReached -= OnLoopPointReached;
                videoPlayer.errorReceived -= OnErrorReceived;
                videoPlayer.Stop();
            }

            if (presentationRoot != null)
                Destroy(presentationRoot);

            skipPrompt = null;
            skipFill = null;

            if (cameraObject != null)
                Destroy(cameraObject);
        }

        private void ConfigureAudio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL hands the audio track straight to the browser's media element,
            // so it cannot go through a mixer group - read the Video bus's current
            // volume once and apply it directly instead. Browser autoplay policy
            // may also hold it silent until the player interacts with the page.
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.SetDirectAudioVolume(
                0, AudioManager.Instance != null ? AudioManager.Instance.VideoVolume : 1f);
#else
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            AudioManager.Route(audioSource, AudioBus.Video);
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
#endif
        }

        private void BuildPresentation()
        {
            // Boot has no camera of its own. A culling-masked-out camera keeps URP
            // from warning about an empty render and guarantees a black clear behind
            // the letterbox bars.
            cameraObject = new GameObject("Startup Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            Camera startupCamera = cameraObject.GetComponent<Camera>();
            startupCamera.clearFlags = CameraClearFlags.SolidColor;
            startupCamera.backgroundColor = Color.black;
            startupCamera.cullingMask = 0;
            startupCamera.depth = -100f;

            presentationRoot = new GameObject(
                "Startup Video",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            presentationRoot.transform.SetParent(transform, false);

            Canvas canvas = presentationRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // Above the menu's 500, in case they ever overlap.

            CanvasScaler scaler = presentationRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(presentationRoot.transform, false);
            StretchToParent((RectTransform)backdrop.transform);
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = Color.black;
            backdropImage.raycastTarget = false;

            var videoObject = new GameObject(
                "Video",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(AspectRatioFitter));
            videoObject.transform.SetParent(presentationRoot.transform, false);
            StretchToParent((RectTransform)videoObject.transform);

            videoImage = videoObject.GetComponent<RawImage>();
            videoImage.raycastTarget = false;
            // A RawImage with no texture draws opaque white. Stay invisible until
            // the first decoded frame exists.
            videoImage.color = Color.clear;

            aspectFitter = videoObject.GetComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = FallbackAspect;

            BuildSkipPrompt();
        }

        private void BuildSkipPrompt()
        {
            SkipPromptStyle style = promptStyle ?? new SkipPromptStyle();
            TMP_FontAsset font = promptFont
                ?? Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond")
                ?? TMP_Settings.defaultFontAsset;

            var promptObject = new GameObject("Skip Prompt", typeof(RectTransform), typeof(CanvasGroup));
            promptObject.transform.SetParent(presentationRoot.transform, false);
            var promptRect = (RectTransform)promptObject.transform;
            promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(1f, 0f);
            promptRect.sizeDelta = new Vector2(460f, 58f);
            promptRect.anchoredPosition = style.position;

            skipPrompt = promptObject.GetComponent<CanvasGroup>();
            skipPrompt.alpha = 0f;
            skipPrompt.interactable = false;
            skipPrompt.blocksRaycasts = false;

            var frameObject = new GameObject("Frame", typeof(RectTransform));
            frameObject.transform.SetParent(promptObject.transform, false);
            var frame = (RectTransform)frameObject.transform;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = -style.padding;
            frame.offsetMax = style.padding;

            if (style.useBackground)
            {
                Image background = CreateChild<Image>(frame, "Background");
                StretchToParent(background.rectTransform);
                background.sprite = style.background;
                background.type = style.background != null && style.background.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                background.color = style.backgroundColor;
            }

            if (style.borderWidth > 0f)
            {
                AddBorderLine(frame, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, style.borderWidth), style.borderColor);
                AddBorderLine(frame, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, style.borderWidth), style.borderColor);
                AddBorderLine(frame, Vector2.zero, new Vector2(0f, 1f), new Vector2(style.borderWidth, 0f), style.borderColor);
                AddBorderLine(frame, new Vector2(1f, 0f), Vector2.one, new Vector2(style.borderWidth, 0f), style.borderColor);
            }

            float barHeight = Mathf.Max(1f, style.barThickness);

            var label = CreateChild<TextMeshProUGUI>(promptObject.transform, "Label");
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, barHeight + style.barGap);
            labelRect.offsetMax = Vector2.zero;
            label.font = font;
            if (promptBold)
                label.fontStyle |= FontStyles.Bold;
            label.fontSize = style.fontSize;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.color = style.textColor;
            bool touch = Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);
            label.text = touch ? style.messageTouch : style.message;
            if (style.textOutlineWidth > 0f)
            {
                label.outlineWidth = style.textOutlineWidth;
                label.outlineColor = style.textOutlineColor;
            }

            Image track = CreateChild<Image>(promptObject.transform, "Track");
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.sizeDelta = new Vector2(0f, barHeight);
            trackRect.anchoredPosition = Vector2.zero;
            track.color = style.barTrackColor;

            Image fill = CreateChild<Image>(trackRect, "Fill");
            skipFill = fill.rectTransform;
            skipFill.anchorMin = Vector2.zero;
            skipFill.anchorMax = new Vector2(0f, 1f);
            skipFill.pivot = new Vector2(0f, 0.5f);
            skipFill.offsetMin = skipFill.offsetMax = Vector2.zero;
            fill.color = style.barFillColor;
        }

        private static T CreateChild<T>(Transform parent, string objectName) where T : Graphic
        {
            var child = new GameObject(objectName, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            T graphic = child.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void AddBorderLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
        {
            Image line = CreateChild<Image>(parent, "Border");
            RectTransform rect = line.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            line.color = color;
        }

        private void OnPrepareCompleted(VideoPlayer player)
        {
            if (player.height > 0u)
                aspectFitter.aspectRatio = player.width / (float)player.height;

            videoImage.texture = player.texture;
            videoImage.color = Color.white;
            player.Play();
        }

        private void OnLoopPointReached(VideoPlayer player)
        {
            HasFinished = true;
        }

        private void OnErrorReceived(VideoPlayer player, string message)
        {
            HasFailed = true;
            Debug.LogWarning($"Startup video could not play ({message}). Continuing to the menu.");
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Look of the HOLD TO SKIP prompt, set on the BootController (same options as the main menu hint).</summary>
    [System.Serializable]
    public sealed class SkipPromptStyle
    {
        [TextArea(1, 2)] public string message = "HOLD TO SKIP";
        [Tooltip("Text shown on phones and tablets.")]
        [TextArea(1, 2)] public string messageTouch = "TOUCH AND HOLD TO SKIP";
        [Min(8f)] public float fontSize = 26f;
        public Color textColor = new Color(0.88f, 0.86f, 0.80f, 0.85f);
        [Tooltip("Position of the prompt's bottom-right corner, from the bottom-right corner of a 1920x1080 screen.")]
        public Vector2 position = new Vector2(-64f, 64f);
        [Tooltip("Outline around the letters (0 = none, 0.1-0.3 = thin).")]
        [Range(0f, 1f)] public float textOutlineWidth = 0f;
        public Color textOutlineColor = Color.black;

        public bool useBackground = false;
        [Tooltip("Sprite (9-sliced if it has borders). Empty = plain colour.")]
        public Sprite background;
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        [Tooltip("How far the background and border extend around the prompt (x = sides, y = top/bottom).")]
        public Vector2 padding = new Vector2(28f, 12f);
        [Min(0f)] public float borderWidth = 0f;
        public Color borderColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);

        [Header("Bar")]
        [Tooltip("Colour of the bar as it fills.")]
        public Color barFillColor = new Color(0.88f, 0.86f, 0.80f, 0.92f);
        [Tooltip("Colour of the empty bar behind it.")]
        public Color barTrackColor = new Color(1f, 1f, 1f, 0.18f);
        [Tooltip("Distance between the bar and the text. It was 10; small or negative = the bar almost underlines the text.")]
        public float barGap = 2f;
        [Min(1f)] public float barThickness = 4f;
    }
}