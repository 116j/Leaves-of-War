using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FlyingLettersSync
{
    [MenuItem("Tools/Leaves of War/UI/Use Milano's flying letters in every level")]
    private static void Sync()
    {
        string path = Directory.Exists("Assets/_Game/Scenes") ? Directory.GetFiles("Assets/_Game/Scenes", "Level 1 - *.unity").FirstOrDefault() : null;
        if (path == null)
        {
            EditorUtility.DisplayDialog("Flying Letters", "Level 1 - Milano not found.", "OK");
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        Scene milano = SceneManager.GetSceneByPath(path.Replace('\\', '/'));
        bool opened = !milano.isLoaded;
        if (opened)
            milano = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        AssignPoem poem = milano.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AssignPoem>(true)).FirstOrDefault();
        string result = "No poem found in Milano.";
        if (poem != null)
        {
            var so = new SerializedObject(poem);
            FlyingLettersLook.Save(so.FindProperty("_lettersFly").boolValue, so.FindProperty("_flightSeconds").floatValue, so.FindProperty("_flightSize").floatValue,
                so.FindProperty("_spiralRadius").floatValue, so.FindProperty("_spiralTurns").floatValue);
            FlyingLettersLook look = FlyingLettersLook.Current;
            result = "Every level now uses Milano's flying letters:\nseconds " + look.flightSeconds + ", size " + look.flightSize + ", spiral radius " + look.spiralRadius + ", turns " + look.spiralTurns +
                (look.lettersFly ? "" : " (off)") + ".\n\nFrom now on, changing them on Milano's poem changes them everywhere.";
        }
        if (opened)
            EditorSceneManager.CloseScene(milano, true);
        if (active.IsValid())
            SceneManager.SetActiveScene(active);
        EditorUtility.DisplayDialog("Flying Letters", result, "OK");
    }
}
