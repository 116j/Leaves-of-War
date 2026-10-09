using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Hortensia.Runtime
{
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VideoScreen : MonoBehaviour
    {
        private const float FallbackAspect = 16f / 9f;

        [Tooltip("Canvas sorting order for the video presentation. Raise this above any UI it needs to appear on top of.")]
        [SerializeField] private int sortingOrder = 1000;
        [Tooltip("Loops back to the start instead of firing Finished when playback reaches the end.")]
        [SerializeField] private bool loop;

        private VideoPlayer videoPlayer;
        private RawImage videoImage;
        private AspectRatioFitter aspectFitter;
        private GameObject presentationRoot;
        private GameObject cameraObject;

        /// <summary>The clip reached its end (never fires while Loop is on).</summary>
        public event Action Finished;

        /// <summary>The clip could not be opened or decoded. Argument is the engine's error message.</summary>
        public event Action<string> Failed;

        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;

        /// <summary>Starts playing a video from StreamingAssets by file name (e.g. "ending.mp4").</summary>
        private float pendingVolumeScale = 1f;

        /// <summary>Starts playing a video from StreamingAssets by file name (e.g. "ending.mp4"). volumeScale multiplies on top of the Video bus volume.</summary>
        public void Play(string streamingAssetsFileName, float volumeScale = 1f)
        {
            pendingVolumeScale = Mathf.Clamp01(volumeScale);

            if (presentationRoot == null)
                BuildPresentation();

            if (videoPlayer == null)
            {
                videoPlayer = gameObject.AddComponent<VideoPlayer>();
                videoPlayer.playOnAwake = false;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.skipOnDrop = true;
                videoPlayer.renderMode = VideoRenderMode.APIOnly;
                videoPlayer.source = VideoSource.Url;
                videoPlayer.prepareCompleted += OnPrepareCompleted;
                videoPlayer.loopPointReached += OnLoopPointReached;
                videoPlayer.errorReceived += OnErrorReceived;
                ConfigureAudio();
            }
            else
            {
                ApplyPendingVolume();
            }

            videoPlayer.isLooping = loop;
            videoImage.color = Color.clear;
            videoPlayer.url = Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
            videoPlayer.Prepare();
        }

        /// <summary>Tears the presentation down immediately (e.g. the player pressed a skip button).</summary>
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
            if (cameraObject != null)
                Destroy(cameraObject);

            presentationRoot = null;
            cameraObject = null;
        }

        private void OnDestroy()
        {
            Dismiss();
        }

        private void ConfigureAudio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            ApplyPendingVolume();
#else
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            // Ignores the scene-wide pause applied while this screen is
            // playing (see ChapterRunner.PlayEndingVideoAndCredits), so this
            // video's own audio keeps playing while everything else is
            // silenced.
            audioSource.ignoreListenerPause = true;
            // Deliberately NOT routed through AudioBus.Video: that bus is
            // shared with the loading-screen video and tuned for it. This
            // screen's volume is controlled solely by the per-ending
            // volumeScale passed into Play(), so it isn't capped by
            // whatever the shared bus happens to be set to.
            audioSource.volume = pendingVolumeScale;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
#endif
        }

        private void ApplyPendingVolume()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            videoPlayer.SetDirectAudioVolume(0, pendingVolumeScale);
#else
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
                audioSource.volume = pendingVolumeScale;
#endif
        }

        private void BuildPresentation()
        {
            // A culling-masked-out camera guarantees a solid black clear
            // behind the letterbox bars without URP warning about an empty
            // render - same trick as StartupVideoScreen.
            cameraObject = new GameObject("Video Screen Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = -100f;

            presentationRoot = new GameObject(
                "Video Presentation",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            presentationRoot.transform.SetParent(transform, false);

            Canvas canvas = presentationRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

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
            // A RawImage with no texture draws opaque white. Stay invisible
            // until the first decoded frame exists.
            videoImage.color = Color.clear;

            aspectFitter = videoObject.GetComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = FallbackAspect;
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
            if (!loop)
                Finished?.Invoke();
        }

        private void OnErrorReceived(VideoPlayer player, string message)
        {
            Debug.LogWarning($"Video '{player.url}' could not play ({message}).");
            Failed?.Invoke(message);
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