using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelOneBuilder
{
    private const string Folder = "Assets/_Game/Scenes/";
    private const string LevelName = "Level 1 - Milano";
    private const string LevelPath = Folder + LevelName + ".unity";
    private const string CorePath = Folder + "Core.unity";
    private const string Core1Path = Folder + "Core 1.unity";
    private const string TestPath = Folder + "Test_Dialogue.unity";
    private const string MainMenuPath = Folder + "MainMenu.unity";

    private static readonly string[] FromTest = { "Carlo", "Dialogue Runner", "Player", "Test Tools" };
    private static readonly string[] FromCore1 = { "GazeManager" };
    private static readonly string[] CinemachineObjects = { "Main Camera", "VC1", "VC2", "VC3", "VC4" };

    [MenuItem("Tools/Leaves of War/Build Level 1 - Milano")]
    private static void Build()
    {
        foreach (string path in new[] { CorePath, Core1Path, TestPath, MainMenuPath })
        {
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Level 1 - Milano", "Missing " + path, "OK");
                return;
            }
        }
        if (File.Exists(LevelPath) &&
            !EditorUtility.DisplayDialog("Level 1 - Milano", LevelPath + " already exists. Rebuild it from Core, Core 1 and Test_Dialogue?", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var log = new List<string>();

        if (File.Exists(LevelPath))
            AssetDatabase.DeleteAsset(LevelPath);
        AssetDatabase.CopyAsset(CorePath, LevelPath);
        AssetDatabase.Refresh();

        Scene level = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);
        Scene test = EditorSceneManager.OpenScene(TestPath, OpenSceneMode.Additive);
        Scene core1 = EditorSceneManager.OpenScene(Core1Path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(level);

        GameObject oldCamera = Root(level, "Main Camera");
        Transform diary = oldCamera != null ? oldCamera.transform.Find("Diary_Demo") : null;
        Transform broom = oldCamera != null ? oldCamera.transform.Find("Broom_Stick") : null;
        var held = new List<(Transform item, Vector3 position, Quaternion rotation, Vector3 scale)>();
        foreach (Transform item in new[] { diary, broom })
        {
            if (item == null)
                continue;
            held.Add((item, item.localPosition, item.localRotation, item.localScale));
            item.SetParent(null, true);
        }

        foreach (string name in CinemachineObjects)
        {
            GameObject go = Root(level, name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
                log.Add("Removed " + name + " (Cinemachine).");
            }
        }

        foreach (string name in FromTest)
            Move(test, level, name, log);
        foreach (string name in FromCore1)
            Move(core1, level, name, log);

        FirstPersonController player = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include)
            .FirstOrDefault(p => p.gameObject.scene == level);
        Camera playerCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        if (playerCamera == null)
        {
            log.Add("ERROR: no FirstPersonController with a camera came from Test_Dialogue.");
        }
        else
        {
            playerCamera.tag = "MainCamera";
            foreach (var (item, position, rotation, scale) in held)
            {
                item.SetParent(playerCamera.transform, false);
                item.localPosition = position;
                item.localRotation = rotation;
                item.localScale = scale;
                foreach (GameObjectDetached detached in item.GetComponents<GameObjectDetached>())
                    Object.DestroyImmediate(detached);
                log.Add(item.name + " now held by the player camera.");
            }

            if (diary != null)
            {
                foreach (Canvas canvas in diary.GetComponentsInChildren<Canvas>(true))
                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        canvas.worldCamera = playerCamera;
            }
        }

        GameObject script = Root(level, "Script");
        if (script != null)
        {
            foreach (HeadBob bob in script.GetComponents<HeadBob>())
                Object.DestroyImmediate(bob);
            DiaryInteraction interaction = script.GetComponent<DiaryInteraction>();
            if (interaction != null)
            {
                var so = new SerializedObject(interaction);
                so.FindProperty("diary").objectReferenceValue = diary;
                so.FindProperty("broom").objectReferenceValue = broom;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Add("Diary (key " + so.FindProperty("_interactionKey").enumDisplayNames[so.FindProperty("_interactionKey").enumValueIndex] + ") linked to the player camera.");
            }
        }
        else
        {
            log.Add("WARNING: 'Script' object (diary and transcription) not found in Core.");
        }

        GameObject plane = Root(level, "Plane");
        if (plane != null && plane.GetComponent<InvisibleWalls>() == null)
        {
            plane.AddComponent<InvisibleWalls>();
            log.Add("Invisible walls added around the Plane.");
        }

        EditorSceneManager.CloseScene(test, true);
        EditorSceneManager.CloseScene(core1, true);
        EditorSceneManager.MarkSceneDirty(level);
        EditorSceneManager.SaveScene(level);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Folder + "Boot.unity", true),
            new EditorBuildSettingsScene(MainMenuPath, true),
            new EditorBuildSettingsScene(LevelPath, true)
        };
        log.Add("Build Settings: Boot, MainMenu, " + LevelName + ".");

        Scene menu = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("newGameScene").stringValue = LevelName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            log.Add("New Game now starts " + LevelName + ".");
        }

        bool deleted = false;
        if (EditorUtility.DisplayDialog("Level 1 - Milano",
                string.Join("\n", log) + "\n\nDelete Core and Core 1 now?", "Delete", "Keep for now"))
        {
            AssetDatabase.DeleteAsset(CorePath);
            AssetDatabase.DeleteAsset(Core1Path);
            deleted = true;
        }

        EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);
        Debug.Log("Level 1 - Milano built.\n" + string.Join("\n", log) + (deleted ? "\nCore and Core 1 deleted." : ""));
    }

    private static GameObject Root(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(g => g.name.Trim() == name);

    private static void Move(Scene from, Scene to, string name, List<string> log)
    {
        GameObject go = Root(from, name);
        if (go == null)
        {
            log.Add("WARNING: '" + name + "' not found in " + from.name + ".");
            return;
        }
        SceneManager.MoveGameObjectToScene(go, to);
        log.Add("Moved " + name + " from " + from.name + ".");
    }
}
