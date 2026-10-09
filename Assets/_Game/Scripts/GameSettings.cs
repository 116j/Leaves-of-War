using System;
using TMPro;
using UnityEngine;

namespace Hortensia.Runtime
{
    [Serializable]
    public struct TextStyle
    {
        [Range(0.5f, 2f)] public float scale;
        public Color textColor;
        [Tooltip("Speaker name (subtitles) or status messages (screen text).")]
        public Color accentColor;
        public bool bold;
        [Range(-1f, 1f)] public float thickness;
        [Range(0f, 1f)] public float outlineWidth;
        public Color outlineColor;
        public Color backgroundColor;

        public static TextStyle SubtitleDefaults() => new TextStyle
        {
            scale = 1f,
            textColor = new Color(0.96f, 0.93f, 0.88f, 1f),
            accentColor = new Color(0.86f, 0.66f, 0.2f, 1f),
            bold = false,
            thickness = 0f,
            outlineWidth = 0f,
            outlineColor = Color.black,
            backgroundColor = new Color(0f, 0f, 0f, 0.6f),
        };

        public static TextStyle ScreenDefaults() => new TextStyle
        {
            scale = 1f,
            textColor = new Color(0.9f, 0.86f, 0.74f, 1f),
            accentColor = new Color(0.72f, 0.77f, 0.58f, 1f),
            bold = false,
            thickness = 0f,
            outlineWidth = 0.15f,
            outlineColor = Color.black,
            backgroundColor = new Color(0.115f, 0.1f, 0.075f, 0.9f),
        };

        public TextStyle Sanitized(float minScale, float maxScale)
        {
            TextStyle s = this;
            s.scale = Mathf.Clamp(s.scale, minScale, maxScale);
            s.thickness = Mathf.Clamp(s.thickness, -1f, 1f);
            s.outlineWidth = Mathf.Clamp01(s.outlineWidth);
            return s;
        }
    }

    public static class TextStyling
    {
        private static readonly int WeightNormal = Shader.PropertyToID("_WeightNormal");
        private static readonly int WeightBold = Shader.PropertyToID("_WeightBold");

        public static void Apply(TMP_Text text, TextStyle style, Color color, bool baseBold = false)
        {
            if (text == null)
                return;

            text.color = color;
            text.fontStyle = style.bold || baseBold ? text.fontStyle | FontStyles.Bold : text.fontStyle & ~FontStyles.Bold;

            Material shared = text.font != null ? text.font.material : null;
            Material material = text.fontMaterial;
            if (material != null && material.HasProperty(WeightNormal))
            {
                float baseNormal = shared != null && shared.HasProperty(WeightNormal) ? shared.GetFloat(WeightNormal) : 0f;
                float boldWeight = shared != null && shared.HasProperty(WeightBold) ? shared.GetFloat(WeightBold) : 0.5f;
                material.SetFloat(WeightNormal, baseNormal + style.thickness * 0.8f);
                material.SetFloat(WeightBold, boldWeight + style.thickness * 0.8f);
            }

            text.outlineWidth = style.outlineWidth * 0.5f;
            text.outlineColor = style.outlineColor;
            text.UpdateMeshPadding();
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    [Serializable]
    public struct SettingsData
    {
        public const int CurrentTextStyleVersion = 1;

        public float mouseSensitivity;
        public bool holdToSprint;

        public int resolutionIndex;
        public bool fullScreen;
        public bool vSync;
        public int fpsLimit;
        public float fieldOfView;

        public int shadowQuality;
        public bool antiAliasing;

        public bool subtitlesEnabled;
        public bool inGameTextEnabled;
        public int textStyleVersion;
        public TextStyle subtitleStyle;
        public TextStyle screenStyle;

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
                shadowQuality = 3,
                antiAliasing = true,
                subtitlesEnabled = true,
                inGameTextEnabled = true,
                textStyleVersion = CurrentTextStyleVersion,
                subtitleStyle = TextStyleDefaults.SubtitleStyle,
                screenStyle = TextStyleDefaults.ScreenStyle,
                inputOverridesJson = string.Empty,
            };
        }
    }

    public static class GameSettings
    {
        public const float SubtitleMinScale = 0.5f;
        public const float SubtitleMaxScale = 2f;
        public const float ScreenMinScale = 0.6f;
        public const float ScreenMaxScale = 1.6f;

        private const string Key = "hortensia.settings.v1";

        private static SettingsData current;
        private static bool loaded;

        public static event Action Applied;

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
                current = Sanitize(JsonUtility.FromJson<SettingsData>(json));
            }
            catch (Exception)
            {
                current = SettingsData.Defaults();
            }
        }

        public static SettingsData CreateEditableCopy()
        {
            EnsureLoaded();
            return current;
        }

        public static void Apply(SettingsData edited)
        {
            EnsureLoaded();
            current = Sanitize(edited);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
            Applied?.Invoke();
        }

        private static SettingsData Sanitize(SettingsData d)
        {
            if (d.textStyleVersion != SettingsData.CurrentTextStyleVersion)
            {
                d.textStyleVersion = SettingsData.CurrentTextStyleVersion;
                d.subtitleStyle = TextStyleDefaults.SubtitleStyle;
                d.screenStyle = TextStyleDefaults.ScreenStyle;
            }

            d.mouseSensitivity = Mathf.Clamp(d.mouseSensitivity, 0.02f, 1f);
            d.resolutionIndex = Mathf.Max(0, d.resolutionIndex);
            d.fpsLimit = Mathf.Clamp(d.fpsLimit, 30, 300);
            d.fieldOfView = Mathf.Clamp(d.fieldOfView, 40f, 110f);
            d.shadowQuality = Mathf.Clamp(d.shadowQuality, 0, 4);
            d.subtitleStyle = d.subtitleStyle.Sanitized(SubtitleMinScale, SubtitleMaxScale);
            d.screenStyle = d.screenStyle.Sanitized(ScreenMinScale, ScreenMaxScale);
            d.inputOverridesJson ??= string.Empty;
            return d;
        }
    }
}
