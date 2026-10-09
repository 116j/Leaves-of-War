using System;
using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;
using TMPro;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Serialized scene facade for narrative presentation. Authored playback,
    /// reusable HUD channels, and runtime UI construction are composed behind
    /// this stable component so scene references and INarrativePresenter callers
    /// do not depend on the implementation split.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativePresenter : MonoBehaviour, INarrativePresenter, IDiegeticHud
    {
        [Header("Scene")]
        [SerializeField] private Camera lowResolutionCamera;
        [SerializeField] private TMP_FontAsset diegeticFont;
        [SerializeField] private TMP_FontAsset authorialFont;
        [Tooltip("Font used only for the top task/objective indicator (GameplayHudText). Falls back to Diegetic Font if left empty.")]
        [SerializeField] private TMP_FontAsset taskFont;

        /// <summary>
        /// Fired the frame the voice AudioSource transitions from not-playing
        /// to playing - covers both a fresh line's clip starting and a
        /// segmented line's clip resuming after a between-segment pause.
        /// Carries no speaker info; pair with ChapterRunner's
        /// LineStarted/PatientLineStarted (or a direct BeginTurn/EndTurn
        /// call) to know WHOSE voice this is.
        /// </summary>
        public static event Action VoiceStarted;

        /// <summary>
        /// Fired the frame the voice AudioSource transitions from playing to
        /// not-playing - covers the clip finishing naturally, being stopped,
        /// AND a segmented line pausing between segments while it waits for
        /// the player to advance.
        /// </summary>
        public static event Action VoiceStopped;

        private PresentationSettings settings;
        private ISequenceClock clock = UnitySequenceClock.Instance;
        private ISequenceInput input = UnitySequenceInput.Instance;
        private AudioSource voiceSource;
        private bool wasVoicePlaying;
        private GameSessionRegistration sessionRegistration;
        private NarrativeUiView view;
        private NarrativeContentPresenter contentPresenter;
        private DiegeticHudChannels hudChannels;
        private RetroUiTheme activeTheme;
        private bool initialized;
        private bool componentEnabled;
        private string pendingStatus;
        private string pendingGameplayHud;

        public RetroUiTheme UiTheme => activeTheme ??
            RetroUiTheme.Resolve(settings != null ? settings.UiTheme : null);
        public bool IsDocumentOpen => contentPresenter != null && contentPresenter.IsDocumentOpen;

        private void Awake()
        {
            EnsureSettings();
            EnsureVoiceSource();
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterPresenter(this),
                session => session.UnregisterPresenter(this));
        }

        private void OnEnable()
        {
            componentEnabled = true;
            sessionRegistration?.Enable();
            if (!initialized)
                return;

            view.SetVisible(true);
            hudChannels.Enable();
        }

        private void Start()
        {
            // Scene-service registration happens in OnEnable across the scene.
            // Deferring construction until Start lets the compositor resolve the
            // explicitly registered world output without an ordering-dependent scan.
            EnsureInitialized();
        }

        private void Update()
        {
            hudChannels?.Tick();
            UpdateVoicePlaybackState();
        }

        private void UpdateVoicePlaybackState()
        {
            bool isPlaying = IsVoicePlaying();
            if (isPlaying == wasVoicePlaying)
                return;

            wasVoicePlaying = isPlaying;
            if (isPlaying)
                VoiceStarted?.Invoke();
            else
                VoiceStopped?.Invoke();
        }

        private void OnDisable()
        {
            componentEnabled = false;
            contentPresenter?.Clear();
            hudChannels?.Disable();
            view?.SetVisible(false);
            sessionRegistration?.Disable();

            // Stopping voice on disable (see StopVoice below) means the
            // playing->stopped transition itself won't be observed next
            // Update (this component is disabled). Fire it directly so
            // anything mid-turn doesn't stay stuck thinking voice is playing.
            if (wasVoicePlaying)
            {
                wasVoicePlaying = false;
                VoiceStopped?.Invoke();
            }
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
            hudChannels?.Dispose();
            hudChannels = null;
            contentPresenter?.Clear();
            contentPresenter?.Dispose();
            contentPresenter = null;
            view?.Dispose();
            view = null;
        }

        public void Configure(PresentationSettings presentationSettings)
        {
            Configure(presentationSettings, clock, input);
        }

        public void Configure(
            PresentationSettings presentationSettings,
            ISequenceClock sequenceClock,
            ISequenceInput sequenceInput)
        {
            settings = presentationSettings != null ? presentationSettings : settings;
            EnsureSettings();
            clock = sequenceClock ?? UnitySequenceClock.Instance;
            input = sequenceInput ?? UnitySequenceInput.Instance;
            contentPresenter?.Configure(settings, clock, input);
            hudChannels?.SetClock(clock);
        }

        public IEnumerator PresentLine(
            ResolvedLine line,
            string speakerName,
            float visionIntensity)
        {
            EnsureInitialized();
            return contentPresenter.PresentLine(line, speakerName, visionIntensity);
        }

        public IEnumerator PresentCard(string text)
        {
            EnsureInitialized();
            return contentPresenter.PresentCard(text);
        }

        public IEnumerator PresentChoice(
            IReadOnlyList<ChoiceOption> options,
            Action<EndingDefinition> selected)
        {
            EnsureInitialized();
            return contentPresenter.PresentChoice(options, selected);
        }

        public IEnumerator PresentDocument(IReadableDocument document, Action closed)
        {
            EnsureInitialized();
            return contentPresenter.PresentDocument(document, closed);
        }

        public void ShowStatus(string message)
        {
            if (!initialized)
            {
                pendingStatus = message;
                return;
            }

            hudChannels.ShowStatus(message);
        }

        public void SetCarryLabel(string label)
        {
            EnsureInitialized();
            hudChannels.SetCarryLabel(label);
        }

        public void SetGameplayHud(string message)
        {
            if (!initialized)
            {
                pendingGameplayHud = message;
                return;
            }

            hudChannels.SetGameplayHud(message);
        }

        public void ClearGameplayHud()
        {
            pendingGameplayHud = null;
            hudChannels?.ClearGameplayHud();
        }

        public void ClearPresentation()
        {
            pendingStatus = null;
            pendingGameplayHud = null;
            contentPresenter?.Clear();
            hudChannels?.ClearTransient();
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;

            EnsureSettings();
            EnsureVoiceSource();
            ResolveSceneCamera();
            ResolveFonts();
            activeTheme = RetroUiTheme.Resolve(settings.UiTheme);
            view = NarrativeUiFactory.Create(
                transform,
                lowResolutionCamera,
                diegeticFont,
                authorialFont,
                taskFont,
                activeTheme);
            contentPresenter = new NarrativeContentPresenter(
                view,
                settings,
                clock,
                input,
                PlayVoice,
                StopVoice,
                SetVision,
                PauseVoice,
                ResumeVoice,
                GetVoiceTime,
                SeekVoice,
                IsVoicePlaying);
            hudChannels = new DiegeticHudChannels(view, activeTheme, clock);
            initialized = true;

            contentPresenter.Clear();
            if (componentEnabled)
            {
                view.SetVisible(true);
                hudChannels.Enable();
            }

            if (!string.IsNullOrWhiteSpace(pendingStatus))
                hudChannels.ShowStatus(pendingStatus);
            if (!string.IsNullOrWhiteSpace(pendingGameplayHud))
                hudChannels.SetGameplayHud(pendingGameplayHud);
            pendingStatus = null;
            pendingGameplayHud = null;
        }

        private void EnsureSettings()
        {
            if (settings != null)
                return;

            settings = Resources.Load<PresentationSettings>("Narrative/PresentationSettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PresentationSettings>();
                settings.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void EnsureVoiceSource()
        {
            if (voiceSource == null)
                voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null)
                voiceSource = gameObject.AddComponent<AudioSource>();

            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
            AudioManager.Route(voiceSource, AudioBus.Voice);
        }

        private void ResolveSceneCamera()
        {
            if (lowResolutionCamera != null)
                return;

            GameSession session = GameSession.Instance;
            session?.SceneServices.TryGetPlayerCamera(out lowResolutionCamera, out _);
        }

        private void ResolveFonts()
        {
            TMP_FontAsset fallbackFont = TMP_Settings.defaultFontAsset;
            if (diegeticFont == null)
            {
                diegeticFont = Resources.Load<TMP_FontAsset>("Fonts/NimbusSansBitmap32") ??
                    Resources.Load<TMP_FontAsset>("Fonts/NimbusSansBitmap") ??
                    fallbackFont;
            }

            if (authorialFont == null)
            {
                authorialFont = Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond") ??
                    fallbackFont;
            }

            if (taskFont == null)
            {
                taskFont = Resources.Load<TMP_FontAsset>("Fonts/TaskFont") ??
                    diegeticFont;
            }
        }

        private void PlayVoice(AudioClip clip, float volume)
        {
            StopVoice();
            voiceSource.clip = clip;
            voiceSource.volume = volume;
            voiceSource.Play();
        }

        private void StopVoice()
        {
            if (voiceSource == null)
                return;

            voiceSource.Stop();
            voiceSource.clip = null;
        }

        // Segment-playback support: seek/pause/resume the voice source without
        // exposing it directly to NarrativeContentPresenter.
        private void PauseVoice()
        {
            if (voiceSource != null && voiceSource.isPlaying)
                voiceSource.Pause();
        }

        private void ResumeVoice()
        {
            if (voiceSource != null)
                voiceSource.UnPause();
        }

        private float GetVoiceTime() => voiceSource != null ? voiceSource.time : 0f;

        private void SeekVoice(float seconds)
        {
            if (voiceSource == null || voiceSource.clip == null)
                return;

            voiceSource.time = Mathf.Clamp(seconds, 0f, voiceSource.clip.length - 0.01f);
        }

        private bool IsVoicePlaying() => voiceSource != null && voiceSource.isPlaying;

        private static void SetVision(float intensity)
        {
            GameSession session = GameSession.Instance;
            if (session != null &&
                session.SceneServices.TryGetVisionBleed(
                    out VisionBleedController visionBleed,
                    out _))
            {
                visionBleed.SetIntensity(intensity);
            }
        }
    }
}