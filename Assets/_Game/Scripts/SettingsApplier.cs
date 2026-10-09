using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SettingsApplier : MonoBehaviour
    {
        private static SettingsApplier instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            GameSettings.Applied += ApplyAll;
            ApplyAll();
        }

        private void OnDisable() => GameSettings.Applied -= ApplyAll;

        public void ApplyAll()
        {
            SettingsData s = GameSettings.Current;
            ApplyScreen(s);
            ApplyQuality(s);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("hortensia.audio.masterVolume", 0.25f));
        }

        private static void ApplyScreen(SettingsData s)
        {
            Resolution[] resolutions = Screen.resolutions;
            FullScreenMode mode = s.fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

            if (resolutions != null && resolutions.Length > 0)
            {
                Resolution r = resolutions[Mathf.Clamp(s.resolutionIndex, 0, resolutions.Length - 1)];
                Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }

            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.vSync ? -1 : Mathf.Clamp(s.fpsLimit, 30, 300);
        }

        private static void ApplyQuality(SettingsData s)
        {
            switch (s.shadowQuality)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    break;
                case 2:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    break;
                case 3:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    break;
            }

            QualitySettings.antiAliasing = s.antiAliasing ? 2 : 0;
        }
    }
}
