using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Reusable text-only credits blockout for the three canonical ending ids.
    /// The approved roster and key art can replace its body without changing
    /// ending ids, beat order, or the narrative runner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreditsSequencePlayer : MonoBehaviour, ISequencePlayer
    {
        private const string PendingCredits =
            "CREDITS — ART/ROSTER PENDING\n\n" +
            "CREATIVE CREDITS\nROSTER PENDING\n\n" +
            "CAST\nROSTER PENDING\n\n" +
            "VOICE PRODUCTION\nROSTER PENDING\n\n" +
            "ADDITIONAL CREDITS\nROSTER PENDING\n\n" +
            "THANK YOU FOR PLAYING";
        private const string SkipPrompt = "CLICK / ENTER TO ADVANCE";

        [SerializeField] private string sequenceId;
        [SerializeField, Min(0f)] private float minimumHoldSeconds = 1.25f;
        [SerializeField, Min(0.1f)] private float scrollDurationSeconds = 12f;

        private int playbackGeneration;
        private int activeGeneration;
        private bool playbackActive;
        private GameSessionRegistration sessionRegistration;
        private SequencePlaybackHandle activePlayback;
        private CenteredAuthorialOverlay activeOverlay;

        public string SequenceId => sequenceId;
        public SequenceLockFlags RequiredLocks => SequenceLockFlags.NarrativeInput;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterSequencePlayer(this),
                session => session.UnregisterSequencePlayer(this));
        }

        private void OnEnable()
        {
            sessionRegistration?.Enable();
        }

        private void OnDisable()
        {
            sessionRegistration?.Disable();
            activePlayback?.Cancel();
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
            activePlayback?.Cancel();
        }

        public IEnumerator Play(SequencePlaybackHandle playback)
        {
            if (playback == null)
                throw new ArgumentNullException(nameof(playback));
            if (playback.IsTerminal)
                return EmptyRoutine();

            activePlayback?.Cancel();
            activePlayback = playback;
            int generation = ++playbackGeneration;
            playback.RegisterCleanup(() => CleanupPlayback(generation, playback));
            return PlayRoutine(generation, playback);
        }

        private static IEnumerator EmptyRoutine()
        {
            yield break;
        }

        public static bool SupportsSequenceId(string id) =>
            TryGetTreatment(id, out _);

        private IEnumerator PlayRoutine(int generation, SequencePlaybackHandle playback)
        {
            playbackActive = true;
            activeGeneration = generation;
            if (!TryGetTreatment(sequenceId, out CreditsTreatment treatment))
            {
                string displayId = string.IsNullOrWhiteSpace(sequenceId) ? "<blank>" : sequenceId;
                string error =
                    $"{nameof(CreditsSequencePlayer)} cannot play unsupported sequence id '{displayId}'.";
                Debug.LogError(error);
                playback.Fail(error);
                yield break;
            }

            RetroUiTheme theme = ResolveTheme(playback.SceneServices);
            activeOverlay = CenteredAuthorialOverlay.Create(
                $"Text-only Credits — {treatment.EndingTitle}",
                treatment.BackgroundColor,
                treatment.AccentColor,
                theme);
            activeOverlay.SetContent(treatment.EndingTitle, PendingCredits, SkipPrompt);
            activeOverlay.SetBodyLayout(
                new Vector2(theme.CreditsBodyAnchors.x, theme.CreditsBodyAnchors.y),
                new Vector2(theme.CreditsBodyAnchors.z, theme.CreditsBodyAnchors.w),
                theme.CreditsBodyFontSize,
                TextAlignmentOptions.Center);
            activeOverlay.Alpha = 1f;

            RectTransform body = activeOverlay.Body;
            float frameHeight = activeOverlay.Frame != null
                ? Mathf.Max(1f, activeOverlay.Frame.rect.height)
                : RetroResolution.WorldHeight;
            float startY = frameHeight * theme.CreditsScrollFrameFactors.x;
            float endY = frameHeight * theme.CreditsScrollFrameFactors.y;
            if (body != null)
                body.anchoredPosition = new Vector2(0f, startY);

            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, scrollDurationSeconds);
            while (IsCurrent(generation) && elapsed < duration)
            {
                elapsed += FrameDelta();
                float progress = Mathf.Clamp01(elapsed / duration);
                if (body != null)
                {
                    body.anchoredPosition = new Vector2(
                        0f,
                        Mathf.Lerp(startY, endY, progress));
                }

                if (elapsed >= Mathf.Max(0f, minimumHoldSeconds) && AdvancePressed())
                    break;

                yield return null;
            }
        }

        private void CleanupPlayback(int generation, SequencePlaybackHandle playback)
        {
            if (!playbackActive ||
                activeGeneration != generation ||
                !ReferenceEquals(activePlayback, playback))
            {
                return;
            }

            activeOverlay?.Dispose();
            activeOverlay = null;
            playbackActive = false;
            activeGeneration = 0;
            activePlayback = null;
        }

        private bool IsCurrent(int generation) =>
            playbackActive &&
            activeGeneration == generation &&
            playbackGeneration == generation &&
            activePlayback != null &&
            !activePlayback.IsCancellationRequested &&
            isActiveAndEnabled;

        private float FrameDelta() => Mathf.Max(
            0f,
            activePlayback != null ? activePlayback.Clock.UnscaledDeltaTime : 0f);

        private bool AdvancePressed() => activePlayback != null &&
            activePlayback.Input.WasPressed(SequenceInputAction.Advance);

        private static RetroUiTheme ResolveTheme(SceneServiceRegistry services)
        {
            if (services != null &&
                services.TryGetPresenter(out INarrativePresenter presenter, out _) &&
                presenter is NarrativePresenter narrativePresenter)
            {
                return narrativePresenter.UiTheme;
            }

            return RetroUiTheme.Resolve(null);
        }

        private static bool TryGetTreatment(string id, out CreditsTreatment treatment)
        {
            switch (id)
            {
                case "credits_confession":
                    treatment = new CreditsTreatment(
                        "CONFESSION",
                        new Color(0.024f, 0.018f, 0.012f, 1f),
                        new Color(0.72f, 0.55f, 0.34f));
                    return true;

                case "credits_denial":
                    treatment = new CreditsTreatment(
                        "DENIAL",
                        new Color(0.006f, 0.016f, 0.009f, 1f),
                        new Color(0.42f, 0.63f, 0.39f));
                    return true;

                case "credits_sacrifice":
                    treatment = new CreditsTreatment(
                        "SACRIFICE",
                        new Color(0.025f, 0.008f, 0.008f, 1f),
                        new Color(0.69f, 0.34f, 0.31f));
                    return true;

                default:
                    treatment = default;
                    return false;
            }
        }

        private readonly struct CreditsTreatment
        {
            public CreditsTreatment(string endingTitle, Color backgroundColor, Color accentColor)
            {
                EndingTitle = endingTitle;
                BackgroundColor = backgroundColor;
                AccentColor = accentColor;
            }

            public string EndingTitle { get; }
            public Color BackgroundColor { get; }
            public Color AccentColor { get; }
        }
    }
}
