using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    /// <summary>
    /// A snapshot of everything the hand-off decision is allowed to consider.
    /// Passing one value keeps <see cref="BootController.ShouldHandOff"/> a pure
    /// function of observable state, so the policy can be reasoned about (and
    /// later tested) without a running VideoPlayer.
    /// </summary>
    public readonly struct StartupProgress
    {
        public StartupProgress(
            bool videoFinished,
            bool nextSceneReady,
            float skipHoldProgress,
            float elapsedSeconds)
        {
            VideoFinished = videoFinished;
            NextSceneReady = nextSceneReady;
            SkipHoldProgress = skipHoldProgress;
            ElapsedSeconds = elapsedSeconds;
        }

        /// <summary>The startup video reached its final frame.</summary>
        public bool VideoFinished { get; }

        /// <summary>The next scene is fully loaded and waiting only for activation.</summary>
        public bool NextSceneReady { get; }

        /// <summary>
        /// How far through the skip hold the player currently is, from 0 to 1.
        /// A sustained hold cannot be pressed by accident, which is why no
        /// separate minimum-display floor is needed to protect the ident.
        /// </summary>
        public float SkipHoldProgress { get; }

        /// <summary>The player held a skip control for the full required duration.</summary>
        public bool SkipHoldCompleted => SkipHoldProgress >= 1f;

        /// <summary>Unscaled seconds since Boot started.</summary>
        public float ElapsedSeconds { get; }
    }

    /// <summary>
    /// Build index 0. Starts loading the main menu, plays the director's studio
    /// ident over the top of that load, and activates the menu when
    /// <see cref="ShouldHandOff"/> says the startup screen is done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootController : MonoBehaviour
    {
        // Activation is held off while the video plays, which caps
        // AsyncOperation.progress at 0.9 forever. isDone would never be true.
        private const float LoadedButNotActivatedProgress = 0.9f;

        [SerializeField] private string nextSceneName = "MainMenu";

        [Tooltip("File name inside Assets/StreamingAssets. Played by URL so the same path works on desktop and WebGL.")]
        [SerializeField] private string startupVideoFileName = "loading-screen.mp4";

        [Tooltip("How long a key, mouse button, gamepad button or finger on the screen must be held down to skip the ident. A hold rather than a tap is what makes an accidental skip impossible.")]
        [SerializeField] private float skipHoldSeconds = 1f;

        [Tooltip("How fast an abandoned hold drains back to zero, as a multiple of the fill rate. Draining rather than snapping keeps a fumbled hold from looking like a dropped input.")]
        [SerializeField] private float skipHoldDecayRate = 2f;

        [Tooltip("Hard ceiling. If the video stalls or the policy never resolves, hand off anyway rather than trapping the player on a black screen.")]
        [SerializeField] private float failsafeSeconds = 45f;

        [Header("Skip Prompt Font")]
        [Tooltip("Font of the HOLD TO SKIP prompt (TMP Font Asset). Use the same as the main menu's Panel Font or Hint Font.")]
        [SerializeField] private TMP_FontAsset promptFont;
        [Tooltip("EASIEST WAY: the .ttf / .otf file. Wins over Prompt Font; Prompt Font's material look is copied onto it.")]
        [SerializeField] private Font promptFontFile;
        [SerializeField] private bool promptBold = false;

        [Header("Skip Prompt Style")]
        [SerializeField] private SkipPromptStyle promptStyle = new SkipPromptStyle();

        private AsyncOperation nextSceneLoad;
        private StartupVideoScreen videoScreen;
        private float elapsedSeconds;
        private float skipHoldElapsed;
        private bool handedOff;

        private void Start()
        {
            // Begin the load first so the menu builds during the ident rather than
            // after it.
            nextSceneLoad = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
            if (nextSceneLoad == null)
            {
                Debug.LogError(
                    $"Boot could not begin loading '{nextSceneName}'. Is it enabled in Build Settings?");
                return;
            }

            nextSceneLoad.allowSceneActivation = false;

            videoScreen = gameObject.AddComponent<StartupVideoScreen>();
            TMP_FontAsset font = MainMenuController.ResolveFont(promptFontFile, promptFont, "Boot Prompt Font");
            videoScreen.Begin(startupVideoFileName, font, promptBold, promptStyle);
        }

        private void Update()
        {
            if (handedOff || nextSceneLoad == null)
                return;

            elapsedSeconds += Time.unscaledDeltaTime;
            AccumulateSkipHold();

            // Failure and the failsafe bypass the policy entirely: there is no
            // startup screen left to honour, only a player to get to the menu.
            if (videoScreen.HasFailed || elapsedSeconds >= failsafeSeconds)
            {
                HandOff();
                return;
            }

            float skipHoldProgress = skipHoldSeconds > 0f
                ? Mathf.Clamp01(skipHoldElapsed / skipHoldSeconds)
                : 1f;
            videoScreen.SetSkipProgress(skipHoldProgress);

            var progress = new StartupProgress(
                videoFinished: videoScreen.HasFinished,
                nextSceneReady: nextSceneLoad.progress >= LoadedButNotActivatedProgress,
                skipHoldProgress: skipHoldProgress,
                elapsedSeconds: elapsedSeconds);

            if (ShouldHandOff(progress))
                HandOff();
        }

        /// <summary>
        /// Decides whether the startup screen has served its purpose and the main
        /// menu should be activated now.
        /// </summary>
        private static bool ShouldHandOff(in StartupProgress progress)
        {
            // Always hand off once the player has held the skip control long
            // enough. This is deliberately allowed before the menu has finished
            // loading: HandOff only enables activation, so the menu appears the
            // moment its load completes rather than being blocked on it.
            if (progress.SkipHoldCompleted)
                return true;

            // Otherwise, hand off only if the video has already played AND the next scene is ready
            return progress.VideoFinished && progress.NextSceneReady;
        }

        private void HandOff()
        {
            handedOff = true;

            if (videoScreen != null)
                videoScreen.Dismiss();

            // Safe even if progress has not reached 0.9 yet: the scene simply
            // activates as soon as its load completes.
            nextSceneLoad.allowSceneActivation = true;
        }

        private void AccumulateSkipHold()
        {
            if (StartupVideoScreen.IsSkipHeld)
            {
                skipHoldElapsed += Time.unscaledDeltaTime;
                return;
            }

            // Drain rather than snap to zero, so releasing for a frame - or a
            // key repeat gap - does not read as a dropped input.
            skipHoldElapsed = Mathf.Max(
                0f,
                skipHoldElapsed - (Time.unscaledDeltaTime * skipHoldDecayRate));
        }
    }
}