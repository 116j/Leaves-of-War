using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    [Serializable]
    public struct SettingsData
    {
        // Controls
        public float mouseSensitivity;
        public bool holdToSprint;

        // Display
        public int resolutionIndex;
        public bool fullScreen;
        public bool vSync;
        public int fpsLimit;
        public float fieldOfView;
        public float gamma;

        // Graphics
        public int shadowQuality;   // 0=OFF .. 4=ULTRA
        public bool antiAliasing;
        public bool retroFilter;

        // Audio (0..1 linear)

        // Text & subtitles
        public float fontScale;
        public float subtitleScale;
        public float subtitleBackgroundOpacity;
        public bool subtitlesEnabled;
        public bool inGameTextEnabled; // Interaction prompts, task HUD, carry/status text, and the objective arrow.

        // Controls - key rebinding.
        // Serialized Input System binding overrides for the player action map.
        // Empty string means "use the default bindings built in code".
        public string inputOverridesJson;

        public static SettingsData Defaults()
        {
            return new SettingsData
            {
                mouseSensitivity = 0.12f,
                holdToSprint = false,
                resolutionIndex = Mathf.Max(0, Screen.resolutions.Length - 1),
                fullScreen = true,
                vSync = true,
                fpsLimit = 60,
                fieldOfView = 58f,
                gamma = 1f,
                shadowQuality = 3,
                antiAliasing = true,
                retroFilter = true,
                fontScale = 0.7875f,
                subtitleScale = 0.825f,
                subtitleBackgroundOpacity = 0.9f,
                subtitlesEnabled = true,
                inGameTextEnabled = true,
                inputOverridesJson = string.Empty,
            };
        }
    }

    public static class GameSettings
    {
        private const string Key = "hortensia.settings.v1";

        private static SettingsData current;
        private static bool loaded;

        /// <summary>Raised after Apply writes new values, so live systems can refresh.</summary>
        public static event Action Applied;

        /// <summary>The applied, on-disk settings. Read-only snapshot.</summary>
        public static SettingsData Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                current = SettingsData.Defaults();
                return;
            }

            try
            {
                current = JsonUtility.FromJson<SettingsData>(json);
                current = Sanitize(current);
            }
            catch (Exception)
            {
                current = SettingsData.Defaults();
            }
        }

        /// <summary>Returns an editable copy for the options screen to modify freely.</summary>
        public static SettingsData CreateEditableCopy()
        {
            EnsureLoaded();
            return current;
        }

        /// <summary>
        /// Commits an edited copy: sanitises it, stores it, writes to disk and
        /// notifies live systems. Call this from the APPLY/SAVE button only.
        /// </summary>
        public static void Apply(SettingsData edited)
        {
            EnsureLoaded();
            current = Sanitize(edited);

            string json = JsonUtility.ToJson(current);
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();

            Applied?.Invoke();
        }

        private static SettingsData Sanitize(SettingsData d)
        {
            d.mouseSensitivity = Mathf.Clamp(d.mouseSensitivity, 0.02f, 1f);
            d.resolutionIndex = Mathf.Max(0, d.resolutionIndex);
            d.fpsLimit = Mathf.Clamp(d.fpsLimit, 30, 300);
            d.fieldOfView = Mathf.Clamp(d.fieldOfView, 40f, 110f);
            d.gamma = Mathf.Clamp(d.gamma, 0.5f, 2.5f);
            d.shadowQuality = Mathf.Clamp(d.shadowQuality, 0, 4);
            d.fontScale = Mathf.Clamp(d.fontScale, 0.75f, 1.5f);
            d.subtitleScale = Mathf.Clamp(d.subtitleScale, 0.75f, 1.5f);
            d.subtitleBackgroundOpacity = Mathf.Clamp01(d.subtitleBackgroundOpacity);
            d.inputOverridesJson ??= string.Empty;
            return d;
        }
    }
}