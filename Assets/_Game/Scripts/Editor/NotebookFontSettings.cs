using UnityEditor;
using UnityEngine;

public static class NotebookFontSettings
{
    [MenuItem("Tools/Leaves of War/UI/Notebook font settings")]
    private static void Open()
    {
        string path = "Assets/Resources/" + NotebookFont.ResourcePath + ".asset";
        NotebookFont settings = AssetDatabase.LoadAssetAtPath<NotebookFont>(path);
        if (settings == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Settings"))
                AssetDatabase.CreateFolder("Assets/Resources", "Settings");
            settings = ScriptableObject.CreateInstance<NotebookFont>();
            Font[] found = Resources.LoadAll<Font>(NotebookFont.FontFolder);
            settings.font = found.Length > 0 ? found[0] : null;
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
}
