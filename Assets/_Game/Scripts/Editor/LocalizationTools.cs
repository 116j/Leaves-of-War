using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LocalizationTools
{
    private const string FilePath = "Assets/Resources/" + Loc.ItalianPath + ".txt";

    private static readonly HashSet<string> Fields = new HashSet<string>
    {
        "objective", "prompt", "caption", "openPrompt", "closePrompt", "promptFormat", "_promptFormat",
        "closeHint", "hintMessage", "hintMessageTouch", "message", "messageTouch"
    };

    [MenuItem("Tools/Leaves of War/Localization/Open the Italian translations")]
    private static void OpenFile()
    {
        Object file = AssetDatabase.LoadAssetAtPath<TextAsset>(FilePath);
        if (file == null)
        {
            EditorUtility.DisplayDialog("Localization", FilePath + " not found.", "OK");
            return;
        }
        Selection.activeObject = file;
        AssetDatabase.OpenAsset(file);
    }

    [MenuItem("Tools/Leaves of War/Localization/Add untranslated texts")]
    private static void Collect()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string active = SceneManager.GetActiveScene().path;
        Loc.Reload();
        var found = new List<(string scene, string text)>();
        var seen = new HashSet<string>();
        foreach (string path in Directory.GetFiles("Assets/_Game/Scenes", "*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (MonoBehaviour behaviour in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (behaviour == null || behaviour is AssignPoem || behaviour is TranscribePoem)
                    continue;
                var so = new SerializedObject(behaviour);
                SerializedProperty p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.String)
                        continue;
                    bool subtitle = p.name == "text" && p.propertyPath.Contains("pieces");
                    bool examinable = p.name == "displayName" && behaviour is Examinable;
                    if (!subtitle && !examinable && !Fields.Contains(p.name))
                        continue;
                    string value = p.stringValue;
                    if (string.IsNullOrWhiteSpace(value) || Loc.Has(value) || !value.Any(char.IsLetter))
                        continue;
                    string key = value.Trim().ToLowerInvariant();
                    if (seen.Add(key))
                        found.Add((Path.GetFileNameWithoutExtension(path), value.Replace("\r", string.Empty).Trim()));
                }
            }
        }
        if (!string.IsNullOrEmpty(active) && File.Exists(active))
            EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

        if (found.Count == 0)
        {
            EditorUtility.DisplayDialog("Localization", "Every text in the scenes already has its Italian translation.", "OK");
            return;
        }
        var text = new StringBuilder();
        text.Append("\n\n# ===== TO TRANSLATE (added ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("): write the Italian after <<< =====\n");
        foreach (var (scene, value) in found)
        {
            text.Append("# ").Append(scene).Append('\n');
            text.Append(">>> ").Append(value).Append('\n');
            text.Append("<<< \n");
        }
        File.AppendAllText(FilePath, text.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(FilePath);
        Loc.Reload();
        EditorUtility.DisplayDialog("Localization", found.Count + " texts without Italian were added at the end of " + FilePath + ".\n\nWrite the Italian after each <<< and save the file.", "OK");
        OpenFile();
    }

    [MenuItem("Tools/Leaves of War/Localization/Reload translations")]
    private static void ReloadMenu()
    {
        AssetDatabase.ImportAsset(FilePath);
        Loc.Reload();
    }
}
