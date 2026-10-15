using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParisRoomBuilder
{
    private const string Folder = "Assets/_Game/Scenes/";
    private const string MilanoPath = Folder + "Level 1 - Milano.unity";
    private const string LevelName = "Level 2 - Parigi";
    private const string LevelPath = Folder + LevelName + ".unity";
    private const float Width = 3f;
    private const float Depth = 5f;
    private const float Height = 3f;
    private const float Wall = 0.15f;

    private static readonly string[] Remove =
    {
        "Milano Room", "Carlo", "Level Flow", "Broom (pickup)", "Diary (pickup)",
        "Note1", "Note2", "Note3", "Note4", "Note5", "Note6", "Plane", "Cube", "Sphere", "Cylinder", "Follower Leaf"
    };

    [MenuItem("Tools/Leaves of War/Build Level 2 - Parigi (Greybox)")]
    private static void Build()
    {
        if (!File.Exists(MilanoPath))
        {
            EditorUtility.DisplayDialog(LevelName, "Missing " + MilanoPath, "OK");
            return;
        }
        if (File.Exists(LevelPath) && !EditorUtility.DisplayDialog(LevelName, LevelPath + " exists. Rebuild it from Level 1?", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (File.Exists(LevelPath))
            AssetDatabase.DeleteAsset(LevelPath);
        AssetDatabase.CopyAsset(MilanoPath, LevelPath);
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (Remove.Contains(root.name.Trim()))
                Object.DestroyImmediate(root);

        Transform room = BuildRoom(out Transform entry, out Transform window);

        FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (player != null)
            player.transform.SetPositionAndRotation(new Vector3(entry.position.x, player.transform.position.y, entry.position.z), Quaternion.Euler(0f, 0f, 0f));

        Set<DiaryInteraction>("startInHand", true);
        Set<BroomSweeper>("startInHand", true);

        LeafField field = Object.FindAnyObjectByType<LeafField>(FindObjectsInactive.Include);
        if (field != null)
        {
            field.transform.position = room.position;
            var fieldSo = new SerializedObject(field);
            fieldSo.FindProperty("autoArea").boolValue = false;
            fieldSo.FindProperty("areaSize").vector2Value = new Vector2(Width - 0.2f, Depth - 0.2f);
            fieldSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var flowObject = new GameObject("Level Flow");
        LevelFlow flow = flowObject.AddComponent<LevelFlow>();
        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "PARIGI — 5 RUE DES CARMES — 1912–1914";
        so.FindProperty("characterName").stringValue = "Apollinaire";
        so.FindProperty("entryAnchor").objectReferenceValue = entry;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == LevelPath))
        {
            int milano = scenes.FindIndex(s => s.path == MilanoPath);
            scenes.Insert(milano >= 0 ? milano + 1 : scenes.Count, new EditorBuildSettingsScene(LevelPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        EditorUtility.DisplayDialog(LevelName,
            "Paris greybox built and added to Build Settings after Level 1.\nRoom 3 x 5 m: two beds, washstand, desk with candle, shelf, chair with Moammed's coat, French window with balcony rail, door with transom.\n\nNext: Tools > Leaves of War > Setup Paris Script (Apollinaire, Moammed, the three fragments of In memoria and the whole level flow).",
            "OK");
    }

    private static Transform BuildRoom(out Transform entry, out Transform window)
    {
        var root = new GameObject("Parigi Room").transform;
        Transform shell = MilanoRoomBuilder.Group("Shell", root);
        Transform props = MilanoRoomBuilder.Group("Props", root);

        Material wallpaper = MilanoRoomBuilder.Mat("Ochre Wallpaper", new Color(0.78f, 0.6f, 0.32f));
        Material planks = MilanoRoomBuilder.Mat("Floor Planks", new Color(0.45f, 0.31f, 0.2f));
        Material wood = MilanoRoomBuilder.Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f));
        Material iron = MilanoRoomBuilder.Mat("Painted Iron", new Color(0.18f, 0.2f, 0.19f));
        Material linen = MilanoRoomBuilder.Mat("Linen", new Color(0.86f, 0.83f, 0.76f));
        Material blanket = MilanoRoomBuilder.Mat("Grey Blanket", new Color(0.4f, 0.38f, 0.35f));
        Material ceramic = MilanoRoomBuilder.Mat("Ceramic", new Color(0.95f, 0.95f, 0.92f));
        Material brass = MilanoRoomBuilder.Mat("Brass", new Color(0.78f, 0.6f, 0.25f));
        Material books = MilanoRoomBuilder.Mat("Paper Books", new Color(0.85f, 0.78f, 0.6f));
        Material coat = MilanoRoomBuilder.Mat("Dark Coat", new Color(0.12f, 0.12f, 0.14f));
        Material roofs = MilanoRoomBuilder.Mat("Paris Roofs", new Color(0.5f, 0.55f, 0.6f));

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor (sweepable, single mesh)";
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(shell, false);
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localScale = new Vector3(Width, Depth, 1f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = planks;
        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer >= 0)
            floor.layer = floorLayer;
        var floorCollider = new GameObject("Floor Collider");
        floorCollider.transform.SetParent(shell, false);
        floorCollider.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floorCollider.AddComponent<BoxCollider>().size = new Vector3(Width + Wall * 2f, 0.1f, Depth + Wall * 2f);
        if (floorLayer >= 0)
            floorCollider.layer = floorLayer;

        MilanoRoomBuilder.Box("Ceiling", shell, new Vector3(0f, Height + 0.05f, 0f), new Vector3(Width + Wall * 2f, 0.1f, Depth + Wall * 2f), wallpaper);
        float hx = Width * 0.5f + Wall * 0.5f;
        float hz = Depth * 0.5f + Wall * 0.5f;
        MilanoRoomBuilder.Box("Wall West", shell, new Vector3(-hx, Height * 0.5f, 0f), new Vector3(Wall, Height, Depth + Wall * 2f), wallpaper);
        MilanoRoomBuilder.Box("Wall East", shell, new Vector3(hx, Height * 0.5f, 0f), new Vector3(Wall, Height, Depth + Wall * 2f), wallpaper);
        Opening("Wall South (Door + Transom)", shell, new Vector3(0f, 0f, -hz), 0.9f, 0.85f, 0f, 2.05f, wallpaper);
        Opening("Wall North (French Window)", shell, new Vector3(0f, 0f, hz), 0f, 1.0f, 0.1f, 2.5f, wallpaper);

        Transform door = MilanoRoomBuilder.Group("Door (hinge)", shell);
        door.localPosition = new Vector3(0.9f - 0.425f, 0f, -Depth * 0.5f);
        MilanoRoomBuilder.Box("Door", door, new Vector3(0.425f, 1.02f, -0.03f), new Vector3(0.83f, 2.03f, 0.05f), wood);
        MilanoRoomBuilder.Box("Transom", shell, new Vector3(0.9f, 2.3f, -Depth * 0.5f - 0.02f), new Vector3(0.85f, 0.3f, 0.03f), MilanoRoomBuilder.GlassMat(), false);
        var lampObject = new GameObject("Corridor Gas Lamp");
        lampObject.transform.SetParent(shell, false);
        lampObject.transform.localPosition = new Vector3(0.9f, 2.1f, -Depth * 0.5f - 1f);
        Light gas = lampObject.AddComponent<Light>();
        gas.type = LightType.Point;
        gas.color = new Color(1f, 0.85f, 0.6f);
        gas.intensity = 1.2f;
        gas.range = 4f;

        window = MilanoRoomBuilder.Group("French Window", shell);
        window.localPosition = new Vector3(0f, 0.1f, Depth * 0.5f);
        MilanoRoomBuilder.Box("Glass Left", window, new Vector3(-0.25f, 1.2f, 0f), new Vector3(0.48f, 2.38f, 0.015f), MilanoRoomBuilder.GlassMat(), false);
        MilanoRoomBuilder.Box("Glass Right", window, new Vector3(0.25f, 1.2f, 0f), new Vector3(0.48f, 2.38f, 0.015f), MilanoRoomBuilder.GlassMat(), false);
        MilanoRoomBuilder.Box("Mullion", window, new Vector3(0f, 1.2f, 0f), new Vector3(0.04f, 2.4f, 0.04f), wood, false);
        MilanoRoomBuilder.Box("Balcony Rail", window, new Vector3(0f, 0.9f, 0.25f), new Vector3(1.1f, 0.05f, 0.05f), iron);
        MilanoRoomBuilder.Box("Balcony Rail Bars", window, new Vector3(0f, 0.45f, 0.25f), new Vector3(1.1f, 0.9f, 0.01f), iron, false);
        MilanoRoomBuilder.Box("Window View (rooftops)", window, new Vector3(0f, 0.5f, 4f), new Vector3(8f, 6f, 0.05f), roofs, false);

        foreach (float side in new[] { -1f, 1f })
        {
            Transform bed = MilanoRoomBuilder.Group(side < 0f ? "Bed Left" : "Bed Right", props);
            bed.localPosition = new Vector3(side * 1.02f, 0f, 1.0f);
            MilanoRoomBuilder.Box("Frame", bed, new Vector3(0f, 0.3f, 0f), new Vector3(0.85f, 0.12f, 1.9f), iron);
            MilanoRoomBuilder.Box("Headboard", bed, new Vector3(0f, 0.5f, 0.93f), new Vector3(0.85f, 1.0f, 0.05f), iron);
            MilanoRoomBuilder.Box("Mattress + Blanket", bed, new Vector3(0f, 0.42f, -0.05f), new Vector3(0.8f, 0.14f, 1.75f), blanket, false);
            MilanoRoomBuilder.Box("Pillow", bed, new Vector3(0f, 0.53f, 0.75f), new Vector3(0.75f, 0.12f, 0.25f), linen, false);
        }

        Transform wash = MilanoRoomBuilder.Group("Washstand", props);
        wash.localPosition = new Vector3(-1.25f, 0f, -1.7f);
        MilanoRoomBuilder.Box("Stand", wash, new Vector3(0f, 0.4f, 0f), new Vector3(0.45f, 0.8f, 0.6f), wood);
        MilanoRoomBuilder.Box("Bowl", wash, new Vector3(0f, 0.85f, 0f), new Vector3(0.3f, 0.08f, 0.3f), ceramic, false);
        MilanoRoomBuilder.Box("Jug", wash, new Vector3(0f, 0.95f, -0.18f), new Vector3(0.11f, 0.24f, 0.11f), ceramic, false);

        Transform desk = MilanoRoomBuilder.Group("Small Desk", props);
        desk.localPosition = new Vector3(1.2f, 0f, -0.9f);
        MilanoRoomBuilder.Box("Top", desk, new Vector3(0f, 0.74f, 0f), new Vector3(0.55f, 0.04f, 0.9f), wood);
        MilanoRoomBuilder.Box("Legs", desk, new Vector3(0f, 0.36f, 0f), new Vector3(0.5f, 0.72f, 0.85f), wood, false);
        MilanoRoomBuilder.Box("Letters and Envelopes", desk, new Vector3(-0.05f, 0.765f, 0.1f), new Vector3(0.25f, 0.01f, 0.3f), books, false);
        MilanoRoomBuilder.Box("Brass Candlestick", desk, new Vector3(0.1f, 0.85f, -0.3f), new Vector3(0.06f, 0.18f, 0.06f), brass, false);
        var candleObject = new GameObject("Candle Light");
        candleObject.transform.SetParent(desk, false);
        candleObject.transform.localPosition = new Vector3(0.1f, 1.0f, -0.3f);
        Light candle = candleObject.AddComponent<Light>();
        candle.type = LightType.Point;
        candle.color = new Color(1f, 0.7f, 0.4f);
        candle.intensity = 1.6f;
        candle.range = 5f;
        candle.shadows = LightShadows.Soft;

        Transform chair = MilanoRoomBuilder.Group("Straw Chair", props);
        chair.localPosition = new Vector3(0.7f, 0f, -0.9f);
        chair.localRotation = Quaternion.Euler(0f, -90f, 0f);
        MilanoRoomBuilder.Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.04f, 0.42f), wood);
        MilanoRoomBuilder.Box("Back", chair, new Vector3(0f, 0.7f, -0.2f), new Vector3(0.4f, 0.5f, 0.03f), wood, false);
        MilanoRoomBuilder.Box("Moammed's Coat (vanishes)", chair, new Vector3(0f, 0.65f, -0.15f), new Vector3(0.42f, 0.6f, 0.1f), coat, false);

        Transform shelf = MilanoRoomBuilder.Group("Shelf", props);
        shelf.localPosition = new Vector3(-1.38f, 1.5f, -0.6f);
        MilanoRoomBuilder.Box("Board", shelf, Vector3.zero, new Vector3(0.22f, 0.03f, 0.9f), wood, false);
        MilanoRoomBuilder.Box("Books (vanish as a group)", shelf, new Vector3(0f, 0.12f, 0f), new Vector3(0.18f, 0.22f, 0.8f), books, false);

        Transform fragments = MilanoRoomBuilder.Group("Fragment Areas 0.8 x 0.8", root);
        foreach (var (name, pos) in new[] { ("Fragment 1 - between the beds", new Vector3(0f, 0f, 1.0f)), ("Fragment 2 - under the desk", new Vector3(1.1f, 0f, -0.9f)), ("Fragment 3 - under the window", new Vector3(0f, 0f, 2.0f)) })
        {
            Transform f = MilanoRoomBuilder.Group(name, fragments);
            f.localPosition = pos;
        }

        entry = MilanoRoomBuilder.Group("Entry Anchor (door)", root);
        entry.localPosition = new Vector3(0.15f, 0f, -1.75f);
        entry.localRotation = Quaternion.Euler(0f, 180f, 0f);

        root.gameObject.AddComponent<LevelAudio>();
        return root;
    }

    private static void Opening(string name, Transform shell, Vector3 center, float openingX, float openingWidth, float bottom, float top, Material material)
    {
        Transform wall = MilanoRoomBuilder.Group(name, shell);
        wall.localPosition = center;
        float left = -Width * 0.5f - Wall;
        float right = Width * 0.5f + Wall;
        float a = openingX - openingWidth * 0.5f;
        float b = openingX + openingWidth * 0.5f;
        MilanoRoomBuilder.Box("Left", wall, new Vector3((left + a) * 0.5f, Height * 0.5f, 0f), new Vector3(a - left, Height, Wall), material);
        MilanoRoomBuilder.Box("Right", wall, new Vector3((b + right) * 0.5f, Height * 0.5f, 0f), new Vector3(right - b, Height, Wall), material);
        MilanoRoomBuilder.Box("Above", wall, new Vector3(openingX, (top + Height) * 0.5f, 0f), new Vector3(openingWidth, Height - top, Wall), material);
        if (bottom > 0f)
            MilanoRoomBuilder.Box("Below", wall, new Vector3(openingX, bottom * 0.5f, 0f), new Vector3(openingWidth, bottom, Wall), material);
    }

    private static void Set<T>(string property, bool value) where T : Component
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
