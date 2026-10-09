using System.IO;
using TMPro;
using UnityEngine;
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

        /// <summary>The clip played to its end.</summary>
        public bool HasFinished { get; private set; }

        /// <summary>The clip could not be opened or decoded, so there is nothing to wait for.</summary>
        public bool HasFailed { get; private set; }

        public void Begin(string streamingAssetsFileName)
        {
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
            // Gotfridus belongs to the main menu and the bitmap fonts belong to
            // the diegetic layer; this pre-menu prompt borrows neither.
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond")
                ?? TMP_Settings.defaultFontAsset;

            var promptObject = new GameObject(
                "Skip Prompt",
                typeof(RectTransform),
                typeof(CanvasGroup));
            promptObject.transform.SetParent(presentationRoot.transform, false);

            var promptRect = (RectTransform)promptObject.transform;
            promptRect.anchorMin = new Vector2(1f, 0f);
            promptRect.anchorMax = new Vector2(1f, 0f);
            promptRect.pivot = new Vector2(1f, 0f);
            promptRect.sizeDelta = new Vector2(460f, 58f);
            promptRect.anchoredPosition = new Vector2(-64f, 64f);

            skipPrompt = promptObject.GetComponent<CanvasGroup>();
            skipPrompt.alpha = 0f;
            skipPrompt.interactable = false;
            skipPrompt.blocksRaycasts = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(promptObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(0f, 14f);
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 26f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.color = new Color(0.88f, 0.86f, 0.80f, 0.85f);
            label.raycastTarget = false;
            label.text = "HOLD TO SKIP";

            var trackObject = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackObject.transform.SetParent(promptObject.transform, false);
            var trackRect = (RectTransform)trackObject.transform;
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
            skipFill = (RectTransform)fillObject.transform;
            skipFill.anchorMin = Vector2.zero;
            skipFill.anchorMax = new Vector2(0f, 1f);
            skipFill.pivot = new Vector2(0f, 0.5f);
            skipFill.offsetMin = Vector2.zero;
            skipFill.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.88f, 0.86f, 0.80f, 0.92f);
            fillImage.raycastTarget = false;
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
}