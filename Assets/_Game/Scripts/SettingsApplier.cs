using UnityEngine;
using UnityEngine.Audio;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Applies the global slice of <see cref="GameSettings"/> to Unity subsystems that
    /// have engine-wide APIs: the audio mixer, screen resolution / mode, VSync, frame
    /// cap and quality settings. Player- and presentation-specific values (FOV, mouse
    /// sensitivity, subtitle sizes) are pulled by their own components from GameSettings.
    ///
    /// Attach ONE of these to a persistent object in your first-loaded scene (e.g. the
    /// same object that hosts the main menu, or a bootstrap object). It re-applies on
    /// every GameSettings.Applied event, so no scene needs to know about the others.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsApplier : MonoBehaviour
    {
        private static AudioMixer sharedMixer;
        private static SettingsApplier instance;
        private bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            // This must outlive the scene it was created in (the main menu),
            // otherwise it is destroyed on the first scene load and nothing
            // is left listening for GameSettings.Applied during gameplay -
            // options changed mid-game (e.g. from the pause menu) would then
            // silently have no effect.
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureMixer();
        }

        private void OnEnable()
        {
            if (!subscribed)
            {
                GameSettings.Applied += ApplyAll;
                subscribed = true;
            }

            ApplyAll();
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                GameSettings.Applied -= ApplyAll;
                subscribed = false;
            }
        }

        internal static AudioMixer Mixer
        {
            get
            {
                EnsureMixer();
                return sharedMixer;
            }
        }

        private static void EnsureMixer()
        {
            if (sharedMixer == null)
                sharedMixer = Resources.Load<AudioMixer>("Audio/HortensiaAudio");
        }

        public void ApplyAll()
        {
            SettingsData s = GameSettings.Current;
            ApplyScreen(s);
            ApplyQuality(s);
            ApplyGamma(s);
            ApplyRetroFilter(s);
            ApplyRetroFilter(s);
            AudioListener.volume = Mathf.Clamp01(
                PlayerPrefs.GetFloat("hortensia.audio.masterVolume", 0.25f));
        }

        /// <summary>
        /// Broadcasts the gamma value to the world output's own material.
        /// The URP Volume approach (LiftGammaGain, then Color Adjustments'
        /// Post Exposure) proved unreliable across several tests in this
        /// project's specific rendering pipeline, so gamma is applied directly
        /// via a dedicated shader on the world RawImage instead - see
        /// GammaSetting and LowResolutionPresenter.
        /// </summary>
        private static void ApplyGamma(SettingsData s)
        {
            GammaSetting.Set(Mathf.Clamp(s.gamma, 0.5f, 2.5f));
        }

        /// <summary>
        /// Broadcasts the RETRO FILTER toggle to the same world-output material.
        /// The PSXShaderKit's OnRenderImage-based effect never fires under URP,
        /// so the color-depth/dithering pass lives directly in that shader too.
        /// </summary>
        private static void ApplyRetroFilter(SettingsData s)
        {
            RetroFilterSetting.Set(s.retroFilter);
        }

        private static void ApplyScreen(SettingsData s)
        {
            Resolution[] resolutions = Screen.resolutions;
            FullScreenMode mode = s.fullScreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            if (resolutions != null && resolutions.Length > 0)
            {
                int index = Mathf.Clamp(s.resolutionIndex, 0, resolutions.Length - 1);
                Resolution r = resolutions[index];
                Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }

            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            // With VSync on, Unity ignores targetFrameRate; -1 = platform default.
            Application.targetFrameRate = s.vSync ? -1 : Mathf.Clamp(s.fpsLimit, 30, 300);
        }

        private static void ApplyQuality(SettingsData s)
        {
            switch (s.shadowQuality)
            {
                case 0:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                    break;
                case 1:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
                    break;
                case 2:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                    break;
                case 3:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
                    break;
                default:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
                    break;
            }

            QualitySettings.antiAliasing = s.antiAliasing ? 2 : 0;
        }
    }
}