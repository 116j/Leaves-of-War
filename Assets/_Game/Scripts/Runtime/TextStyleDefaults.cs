using UnityEngine;

namespace Hortensia.Runtime
{
    [CreateAssetMenu(fileName = "TextStyleDefaults", menuName = "Leaves of War/Text Style Defaults")]
    public sealed class TextStyleDefaults : ScriptableObject
    {
        public const string ResourcePath = "Settings/TextStyleDefaults";

        [Tooltip("Subtitle style a new player starts with.")]
        public TextStyle subtitles = TextStyle.SubtitleDefaults();
        [Tooltip("Screen text style (pause menu, interaction prompt) a new player starts with.")]
        public TextStyle screen = TextStyle.ScreenDefaults();

        private static TextStyleDefaults cached;
        private static bool searched;

        public static TextStyle SubtitleStyle => Load() != null ? cached.subtitles : TextStyle.SubtitleDefaults();
        public static TextStyle ScreenStyle => Load() != null ? cached.screen : TextStyle.ScreenDefaults();

        private static TextStyleDefaults Load()
        {
            if (!searched || cached == null)
            {
                searched = true;
                cached = Resources.Load<TextStyleDefaults>(ResourcePath);
            }
            return cached;
        }

        [ContextMenu("Copy From My Current Options")]
        private void CopyFromCurrentOptions()
        {
            SettingsData current = GameSettings.Current;
            subtitles = current.subtitleStyle;
            screen = current.screenStyle;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Leaves of War/Save Current Text Style As Default")]
        private static void SaveCurrentAsDefault()
        {
            const string folder = "Assets/Resources/Settings";
            const string path = folder + "/TextStyleDefaults.asset";
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Settings");

            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextStyleDefaults>(path);
            if (asset == null)
            {
                asset = CreateInstance<TextStyleDefaults>();
                UnityEditor.AssetDatabase.CreateAsset(asset, path);
            }

            asset.CopyFromCurrentOptions();
            cached = asset;
            searched = true;
            UnityEditor.Selection.activeObject = asset;
            Debug.Log($"Text style saved as default in {path}.", asset);
        }
#endif
    }
}
