using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        public const float MinimumDecibels = -80f;

        private const string LegacySettingsKey = "hortensia.settings.v1";
        private const string LegacySettingsMigrationKey =
            "Hortensia.Audio.MigratedConsolidatedSettingsV1";

        private static AudioManager instance;

        private AudioRoutingSettings routingSettings;
        private AudioListener fallbackListener;
        private AudioSource musicSource;
        private AudioSource soundEffectsSource;
        private float voiceVolume = 1f;
        private float musicVolume = 1f;
        private float soundEffectsVolume = 1f;
        private float videoVolume = 1f;
        private bool canApplyMixerValues;
        private ulong activeSceneHandle;
        private int listenerRefreshFrames;
        private bool exclusiveMusicActive;
        private bool gameplayAudioPauseActive;
        private bool ownsListenerPause;
        private bool listenerPauseBeforeManagedPause;
        private bool musicIgnoredListenerPauseBeforeManagedPause;

#pragma warning disable CS0649
        [Serializable]
        private struct LegacyConsolidatedSettings
        {
            public float musicVolume;
            public float sfxVolume;
            public float dialogueVolume;
        }
#pragma warning restore CS0649

        public static AudioManager Instance => instance != null ? instance : EnsureInstance();
        internal static AudioManager ExistingInstance => instance;

        public event Action<AudioBus, float> VolumeChanged;

        public float VoiceVolume => voiceVolume;
        public float MusicVolume => musicVolume;
        public float SoundEffectsVolume => soundEffectsVolume;
        public float VideoVolume => videoVolume;

        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
        public bool IsExclusiveMusicActive => exclusiveMusicActive;
        public bool IsGameplayAudioPaused => gameplayAudioPauseActive;
        public bool IsConfigured => routingSettings != null && routingSettings.IsConfigured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        private static AudioManager EnsureInstance()
        {
            if (instance != null)
                return instance;

            if (!Application.isPlaying)
                return null;

            var audioObject = new GameObject("Audio Manager");
            instance = audioObject.AddComponent<AudioManager>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            fallbackListener = CreateFallbackListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            activeSceneHandle = SceneManager.GetActiveScene().handle.GetRawData();
            listenerRefreshFrames = 1;
            RefreshFallbackListener();

            routingSettings = Resources.Load<AudioRoutingSettings>(AudioRoutingSettings.ResourcePath);
            if (routingSettings == null || !routingSettings.IsConfigured)
            {
                Debug.LogError(
                    $"Audio routing settings are missing or incomplete at Resources/{AudioRoutingSettings.ResourcePath}.");
            }

            musicSource = CreateManagedSource("Music", AudioBus.Music);
            musicSource.loop = true;
            soundEffectsSource = CreateManagedSource("2D Sound Effects", AudioBus.SoundEffects);
            // Interactive sound effects (hits, interactions, etc.) should
            // never go silent just because an exclusive music cue or the
            // pause menu suspended AudioListener - PlayOneShot calls made
            // while paused don't get dropped, they get queued and all burst
            // out together the moment the pause lifts, which is far worse
            // than just letting them play through.
            soundEffectsSource.ignoreListenerPause = true;

            voiceVolume = LoadVolume(AudioBus.Voice);
            musicVolume = LoadVolume(AudioBus.Music);
            soundEffectsVolume = LoadVolume(AudioBus.SoundEffects);
            videoVolume = LoadVolume(AudioBus.Video);
            MigrateLegacyConsolidatedSettings();
        }

        private void Start()
        {
            canApplyMixerValues = true;
            ApplyAllVolumes();
            RefreshFallbackListener();
        }

        private void LateUpdate()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            ulong sceneHandle = activeScene.IsValid() ? activeScene.handle.GetRawData() : 0UL;
            if (sceneHandle != 0UL && sceneHandle != activeSceneHandle)
            {
                activeSceneHandle = sceneHandle;
                listenerRefreshFrames = 2;
            }

            if (listenerRefreshFrames <= 0)
                return;

            listenerRefreshFrames--;
            RefreshFallbackListener();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                AudioPreferences.Save();
        }

        private void OnApplicationQuit()
        {
            AudioPreferences.Save();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            exclusiveMusicActive = false;
            gameplayAudioPauseActive = false;
            ApplyManagedPauseState();

            if (instance == this)
                instance = null;
        }

        public void SetVoiceVolume(float normalizedVolume)
        {
            SetVolume(AudioBus.Voice, normalizedVolume);
        }

        public void SetMusicVolume(float normalizedVolume)
        {
            SetVolume(AudioBus.Music, normalizedVolume);
        }

        public void SetSoundEffectsVolume(float normalizedVolume)
        {
            SetVolume(AudioBus.SoundEffects, normalizedVolume);
        }

        public void SetVideoVolume(float normalizedVolume)
        {
            SetVolume(AudioBus.Video, normalizedVolume);
        }

        public void SetVolume(AudioBus bus, float normalizedVolume)
        {
            float sanitized = SanitizeVolume(normalizedVolume, 1f);

            switch (bus)
            {
                case AudioBus.Voice:
                    voiceVolume = sanitized;
                    break;
                case AudioBus.Music:
                    musicVolume = sanitized;
                    break;
                case AudioBus.SoundEffects:
                    soundEffectsVolume = sanitized;
                    break;
                case AudioBus.Video:
                    videoVolume = sanitized;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }

            AudioPreferences.StoreVolume(bus, sanitized);
            if (canApplyMixerValues)
                ApplyVolume(bus, sanitized);

            VolumeChanged?.Invoke(bus, sanitized);
        }

        public float GetVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Voice:
                    return voiceVolume;
                case AudioBus.Music:
                    return musicVolume;
                case AudioBus.SoundEffects:
                    return soundEffectsVolume;
                case AudioBus.Video:
                    return videoVolume;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }
        }

        public float DefaultVolumeFor(AudioBus bus) => DefaultVolume(bus);

        public void ResetVolumesToDefaults()
        {
            SetVolume(AudioBus.Voice, DefaultVolume(AudioBus.Voice));
            SetVolume(AudioBus.Music, DefaultVolume(AudioBus.Music));
            SetVolume(AudioBus.SoundEffects, DefaultVolume(AudioBus.SoundEffects));
            SetVolume(AudioBus.Video, DefaultVolume(AudioBus.Video));
            AudioPreferences.Save();
        }

        public void SavePreferences()
        {
            AudioPreferences.Save();
        }

        public bool RouteSource(AudioSource source, AudioBus bus)
        {
            if (source == null || !IsConfigured)
                return false;

            source.outputAudioMixerGroup = routingSettings.GroupFor(bus);
            return true;
        }

        public static bool Route(AudioSource source, AudioBus bus)
        {
            AudioManager manager = Instance;
            return manager != null && manager.RouteSource(source, bus);
        }

        public void PlayMusic(AudioClip clip, bool loop = true, float volumeScale = 1f)
        {
            if (clip == null || musicSource == null)
                return;

            float sourceVolume = SanitizeVolume(volumeScale, 1f);
            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                musicSource.loop = loop;
                musicSource.volume = sourceVolume;
                return;
            }

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = sourceVolume;
            musicSource.Play();
        }

        /// <summary>
        /// Plays a foreground music cue while suspending every other source through
        /// <see cref="AudioListener.pause"/>. The managed music source opts out of
        /// that global pause, so scene music, ambience, and sound effects already
        /// playing - plus sources started by the incoming scene - cannot overlap it.
        /// Call <see cref="EndExclusiveMusic"/> when the cue finishes naturally.
        /// </summary>
        public bool PlayExclusiveMusic(
            AudioClip clip,
            bool loop = false,
            float volumeScale = 1f)
        {
            if (clip == null || musicSource == null)
                return false;

            exclusiveMusicActive = true;
            ApplyManagedPauseState();
            PlayMusic(clip, loop, volumeScale);
            return true;
        }

        /// <summary>
        /// Releases the scene-audio suspension created by
        /// <see cref="PlayExclusiveMusic"/>. Pass <paramref name="stopMusic"/>
        /// when cancelling an unfinished cue; a naturally completed cue only needs
        /// its listener state restored.
        /// </summary>
        public void EndExclusiveMusic(bool stopMusic)
        {
            if (stopMusic && musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }

            exclusiveMusicActive = false;
            ApplyManagedPauseState();
        }

        public void StopMusic()
        {
            if (musicSource == null)
                return;

            musicSource.Stop();
            musicSource.clip = null;
            exclusiveMusicActive = false;
            ApplyManagedPauseState();
        }

        /// <summary>
        /// Stops music only when the managed source still owns the expected cue.
        /// Scene teardown can use this without cancelling a replacement cue that
        /// began while the outgoing scene was still loaded.
        /// </summary>
        public void StopMusicIfCurrent(AudioClip expectedClip)
        {
            if (expectedClip == null ||
                musicSource == null ||
                musicSource.clip != expectedClip)
            {
                return;
            }

            StopMusic();
        }

        /// <summary>
        /// Coordinates the pause menu with exclusive transition music. Keeping
        /// both reasons here prevents either system from restoring
        /// <see cref="AudioListener.pause"/> while the other still owns it.
        /// </summary>
        public void SetGameplayAudioPaused(bool paused)
        {
            if (gameplayAudioPauseActive == paused)
                return;

            gameplayAudioPauseActive = paused;
            ApplyManagedPauseState();
        }

        /// <summary>
        /// Pauses the currently playing music in place (keeps its position),
        /// unlike <see cref="StopMusic"/> which discards it. Use with
        /// <see cref="ResumeMusic"/> for temporary interruptions (e.g. reading
        /// a document) where the track should continue from where it left off.
        /// </summary>
        public void PauseMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
                musicSource.Pause();
        }

        /// <summary>Resumes music paused via <see cref="PauseMusic"/>.</summary>
        public void ResumeMusic()
        {
            musicSource?.UnPause();
        }

        public void PlaySoundEffect(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || soundEffectsSource == null)
                return;

            soundEffectsSource.PlayOneShot(clip, SanitizeVolume(volumeScale, 1f));
        }

        public static float LinearToDecibels(float normalizedVolume)
        {
            float sanitized = SanitizeVolume(normalizedVolume, 0f);
            if (sanitized <= 0f)
                return MinimumDecibels;

            return Mathf.Max(MinimumDecibels, 20f * Mathf.Log10(sanitized));
        }

        private AudioSource CreateManagedSource(string sourceName, AudioBus bus)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            RouteSource(source, bus);
            return source;
        }

        private AudioListener CreateFallbackListener()
        {
            var listenerObject = new GameObject("Fallback Audio Listener");
            listenerObject.transform.SetParent(transform, false);
            AudioListener listener = listenerObject.AddComponent<AudioListener>();
            listener.enabled = false;
            return listener;
        }

        private void ApplyManagedPauseState()
        {
            bool shouldPause = exclusiveMusicActive || gameplayAudioPauseActive;
            if (shouldPause && !ownsListenerPause)
            {
                listenerPauseBeforeManagedPause = AudioListener.pause;
                musicIgnoredListenerPauseBeforeManagedPause =
                    musicSource != null && musicSource.ignoreListenerPause;
                ownsListenerPause = true;
            }

            if (!ownsListenerPause)
                return;

            if (musicSource != null)
            {
                musicSource.ignoreListenerPause =
                    exclusiveMusicActive && !gameplayAudioPauseActive
                        ? true
                        : musicIgnoredListenerPauseBeforeManagedPause;
            }

            AudioListener.pause = shouldPause
                ? true
                : listenerPauseBeforeManagedPause;

            if (!shouldPause)
                ownsListenerPause = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshFallbackListener();
            listenerRefreshFrames = 2;
        }

        private void RefreshFallbackListener()
        {
            if (fallbackListener == null)
                return;

            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            bool hasSceneListener = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener != null && listener != fallbackListener && listener.enabled)
                {
                    hasSceneListener = true;
                    break;
                }
            }

            fallbackListener.enabled = !hasSceneListener;
        }

        private float LoadVolume(AudioBus bus)
        {
            return AudioPreferences.LoadVolume(bus, DefaultVolume(bus));
        }

        private float DefaultVolume(AudioBus bus)
        {
            return routingSettings != null ? routingSettings.DefaultVolumeFor(bus) : 1f;
        }

        private void MigrateLegacyConsolidatedSettings()
        {
            if (PlayerPrefs.GetInt(LegacySettingsMigrationKey, 0) != 0)
                return;

            string legacyJson = PlayerPrefs.GetString(LegacySettingsKey, string.Empty);
            if (!string.IsNullOrEmpty(legacyJson) &&
                legacyJson.Contains("\"musicVolume\""))
            {
                try
                {
                    LegacyConsolidatedSettings legacy =
                        JsonUtility.FromJson<LegacyConsolidatedSettings>(legacyJson);
                    musicVolume = SanitizeVolume(legacy.musicVolume, musicVolume);
                    soundEffectsVolume = SanitizeVolume(legacy.sfxVolume, soundEffectsVolume);
                    voiceVolume = SanitizeVolume(legacy.dialogueVolume, voiceVolume);
                    AudioPreferences.StoreVolume(AudioBus.Music, musicVolume);
                    AudioPreferences.StoreVolume(AudioBus.SoundEffects, soundEffectsVolume);
                    AudioPreferences.StoreVolume(AudioBus.Voice, voiceVolume);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Could not migrate legacy audio settings: {exception.Message}");
                }
            }

            PlayerPrefs.SetInt(LegacySettingsMigrationKey, 1);
            PlayerPrefs.Save();
        }

        private void ApplyAllVolumes()
        {
            ApplyVolume(AudioBus.Voice, voiceVolume);
            ApplyVolume(AudioBus.Music, musicVolume);
            ApplyVolume(AudioBus.SoundEffects, soundEffectsVolume);
            ApplyVolume(AudioBus.Video, videoVolume);
        }

        private void ApplyVolume(AudioBus bus, float normalizedVolume)
        {
            if (!IsConfigured)
                return;

            string parameter = routingSettings.VolumeParameterFor(bus);
            if (!routingSettings.Mixer.SetFloat(parameter, LinearToDecibels(normalizedVolume)))
                Debug.LogError($"Audio mixer parameter '{parameter}' is missing or is not exposed.");
        }

        private static float SanitizeVolume(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = fallback;

            return Mathf.Clamp01(value);
        }
    }
}