using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuLookSync
{
    [MenuItem("Tools/Leaves of War/UI/Copy main menu look to the game")]
    private static void Sync()
    {
        MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        Scene opened = default;
        if (menu == null)
        {
            string path = EditorBuildSettings.scenes.Select(s => s.path)
                .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) && System.IO.File.ReadAllText(p).Contains(MenuScriptGuid()));
            if (path == null)
            {
                EditorUtility.DisplayDialog("Menu Look", "No scene with the Main Menu Controller found.", "OK");
                return;
            }
            opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            menu = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuController>(true)).FirstOrDefault();
        }
        if (menu != null)
            menu.SaveLookAsset();
        if (opened.IsValid())
            EditorSceneManager.CloseScene(opened, true);
        EditorUtility.DisplayDialog("Menu Look", "Fonts, colours, paper backgrounds and sounds of the main menu copied to " + MenuLook.AssetPath +
            ".\n\nPause menu, its panels and the in-game prompts use them in every level. It also updates by itself whenever the Main Menu Controller is edited.", "OK");
    }

    private static string MenuScriptGuid()
    {
        MonoScript script = AssetDatabase.FindAssets("MainMenuController t:MonoScript").Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(m => m != null && m.GetClass() == typeof(MainMenuController));
        return script != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)) : "MainMenuController";
    }
}
