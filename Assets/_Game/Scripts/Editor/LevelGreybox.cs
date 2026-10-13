using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class LevelGreybox
{
    internal const string Folder = "Assets/_Game/Scenes/";
    private const string MilanoPath = Folder + "Level 1 - Milano.unity";

    private static readonly string[] Remove =
    {
        "Milano Room", "Carlo", "Level Flow", "Broom (pickup)", "Diary (pickup)",
        "Note1", "Note2", "Note3", "Note4", "Note5", "Note6", "Plane", "Cube", "Sphere", "Cylinder", "Follower Leaf"
    };

    internal static bool Begin(string levelName, out Scene scene)
    {
        scene = default;
        string path = Folder + levelName + ".unity";
        if (!File.Exists(MilanoPath))
        {
            EditorUtility.DisplayDialog(levelName, "Missing " + MilanoPath, "OK");
            return false;
        }
        if (File.Exists(path) && !EditorUtility.DisplayDialog(levelName, path + " exists. Rebuild it from Level 1?", "Rebuild", "Cancel"))
            return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(MilanoPath, path);
        AssetDatabase.Refresh();
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (Remove.Contains(root.name.Trim()))
                Object.DestroyImmediate(root);
        return true;
    }

    internal static void Finish(Scene scene, string levelName, string previousLevel, Transform entry, Transform exit,
        Vector3 leafCenter, Vector2 leafArea, string title, string character, string nextScene, string summary)
    {
        FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (player != null)
            player.transform.SetPositionAndRotation(new Vector3(entry.position.x, entry.position.y + player.transform.position.y, entry.position.z), Quaternion.Euler(0f, entry.eulerAngles.y, 0f));

        Set<DiaryInteraction>("startInHand", true);
        Set<BroomSweeper>("startInHand", true);

        LeafField field = Object.FindAnyObjectByType<LeafField>(FindObjectsInactive.Include);
        if (field != null)
        {
            field.transform.position = leafCenter;
            var fieldSo = new SerializedObject(field);
            fieldSo.FindProperty("autoArea").boolValue = false;
            fieldSo.FindProperty("areaSize").vector2Value = leafArea;
            fieldSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var flowObject = new GameObject("Level Flow");
        LevelFlow flow = flowObject.AddComponent<LevelFlow>();
        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = title;
        so.FindProperty("characterName").stringValue = character;
        so.FindProperty("entryAnchor").objectReferenceValue = entry;
        so.FindProperty("exitAnchor").objectReferenceValue = exit;
        so.FindProperty("nextScene").stringValue = nextScene;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string path = Folder + levelName + ".unity";
        string previousPath = Folder + previousLevel + ".unity";
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            int previous = scenes.FindIndex(s => s.path == previousPath);
            scenes.Insert(previous >= 0 ? previous + 1 : scenes.Count, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        bool linked = LinkPrevious(previousPath, levelName);
        EditorUtility.DisplayDialog(levelName,
            summary + "\n\nAdded to Build Settings after " + previousLevel + "." +
            (linked ? "\n" + previousLevel + " → Next Scene set to \"" + levelName + "\"." : "") +
            "\n\nThen: Tools > Leaves of War > CC0 Assets > Apply to open scene.",
            "OK");
    }

    private static bool LinkPrevious(string previousPath, string levelName)
    {
        if (!File.Exists(previousPath))
            return false;
        Scene active = SceneManager.GetActiveScene();
        Scene previous = EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Additive);
        bool linked = false;
        foreach (GameObject root in previous.GetRootGameObjects())
            foreach (LevelFlow flow in root.GetComponentsInChildren<LevelFlow>(true))
            {
                var so = new SerializedObject(flow);
                SerializedProperty next = so.FindProperty("nextScene");
                if (!string.IsNullOrEmpty(next.stringValue))
                    continue;
                next.stringValue = levelName;
                so.ApplyModifiedPropertiesWithoutUndo();
                linked = true;
            }
        if (linked)
            EditorSceneManager.SaveScene(previous);
        EditorSceneManager.CloseScene(previous, true);
        SceneManager.SetActiveScene(active);
        return linked;
    }

    internal static Transform Floor(string name, Transform parent, Vector3 center, float width, float depth, Material material, bool sweepable)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = name;
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = center;
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localScale = new Vector3(width, depth, 1f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        int layer = LayerMask.NameToLayer("Floor");
        var collider = new GameObject(name + " Collider");
        collider.transform.SetParent(parent, false);
        collider.transform.localPosition = center + new Vector3(0f, -0.05f, 0f);
        collider.AddComponent<BoxCollider>().size = new Vector3(width, 0.1f, depth);
        if (layer >= 0)
        {
            collider.layer = layer;
            if (sweepable)
                floor.layer = layer;
        }
        return floor.transform;
    }

    internal static Transform Anchor(string name, Transform parent, Vector3 position, float yaw = 0f)
    {
        Transform t = MilanoRoomBuilder.Group(name, parent);
        t.localPosition = position;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    internal static Transform Prop(string name, Transform parent, Vector3 position, Vector3 size, Material material, float yaw = 0f, bool collider = true)
    {
        Transform t = Anchor(name, parent, position, yaw);
        MilanoRoomBuilder.Box("Placeholder", t, new Vector3(0f, size.y * 0.5f, 0f), size, material, collider);
        return t;
    }

    internal static Light Point(string name, Transform parent, Vector3 position, Color color, float intensity, float range, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return light;
    }

    internal static void Sky(Color sun, float intensity, Vector3 euler, Color ambient, bool fog, Color fogColor, float fogDensity)
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
                continue;
            light.color = sun;
            light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(euler);
            light.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    internal static void Wall(string name, Transform parent, float width, float height, float thickness, Vector3 center, float yaw,
        float openingX, float openingWidth, float bottom, float top, Material material)
    {
        Transform wall = Anchor(name, parent, center, yaw);
        if (openingWidth <= 0f)
        {
            MilanoRoomBuilder.Box("Wall", wall, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, thickness), material);
            return;
        }
        float left = -width * 0.5f;
        float right = width * 0.5f;
        float a = openingX - openingWidth * 0.5f;
        float b = openingX + openingWidth * 0.5f;
        MilanoRoomBuilder.Box("Left", wall, new Vector3((left + a) * 0.5f, height * 0.5f, 0f), new Vector3(a - left, height, thickness), material);
        MilanoRoomBuilder.Box("Right", wall, new Vector3((b + right) * 0.5f, height * 0.5f, 0f), new Vector3(right - b, height, thickness), material);
        MilanoRoomBuilder.Box("Above", wall, new Vector3(openingX, (top + height) * 0.5f, 0f), new Vector3(openingWidth, height - top, thickness), material);
        if (bottom > 0f)
            MilanoRoomBuilder.Box("Below", wall, new Vector3(openingX, bottom * 0.5f, 0f), new Vector3(openingWidth, bottom, thickness), material);
    }

    internal static void Set<T>(string property, bool value) where T : Component
    {
        T component = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (component == null)
            return;
        var so = new SerializedObject(component);
        SerializedProperty p = so.FindProperty(property);
        if (p != null)
            p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
