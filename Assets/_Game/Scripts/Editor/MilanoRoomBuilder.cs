using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MilanoRoomBuilder
{
    private const string RootName = "Milano Room";
    private const string MaterialFolder = "Assets/_Game/Materials/Greybox";
    private const float Width = 4f;
    private const float Depth = 5f;
    private const float Height = 3f;
    private const float Wall = 0.15f;

    private static readonly string[] TestObjects = { "Plane", "Cube", "Sphere", "Cylinder", "Note2", "Note3", "Note4", "Note5" };

    private static Transform shell;
    private static Transform props;

    [MenuItem("Tools/Leaves of War/Build Milano Room (Greybox)")]
    private static void Build()
    {
        GameObject old = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .Select(t => t.gameObject).FirstOrDefault(g => g.transform.parent == null && g.name == RootName && !EditorUtility.IsPersistent(g));
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Milano Room", "A Milano Room already exists. Rebuild it? Level Audio settings are kept.", "Rebuild", "Cancel"))
                return;
        }

        var log = new List<string>();
        var root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Milano Room");
        LevelAudio audio = root.gameObject.AddComponent<LevelAudio>();
        LevelAudio oldAudio = old != null ? old.GetComponent<LevelAudio>() : null;
        if (oldAudio != null)
            EditorUtility.CopySerialized(oldAudio, audio);
        if (old != null)
            Undo.DestroyObjectImmediate(old);
        shell = Group("Shell", root);
        props = Group("Props", root);
        Transform lights = Group("Lights", root);

        Material plaster = Mat("Plaster", new Color(0.82f, 0.76f, 0.64f));
        Material border = Mat("Stencil Border", new Color(0.55f, 0.42f, 0.35f));
        Material planks = Mat("Floor Planks", new Color(0.45f, 0.31f, 0.2f));
        Material walnut = Mat("Walnut", new Color(0.33f, 0.2f, 0.12f));
        Material darkWood = Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f));
        Material iron = Mat("Painted Iron", new Color(0.18f, 0.2f, 0.19f));
        Material wool = Mat("Wool Blanket", new Color(0.42f, 0.17f, 0.14f));
        Material linen = Mat("Linen", new Color(0.86f, 0.83f, 0.76f));
        Material marble = Mat("Marble", new Color(0.9f, 0.89f, 0.86f));
        Material ceramic = Mat("Ceramic", new Color(0.95f, 0.95f, 0.92f));
        Material brass = Mat("Brass", new Color(0.78f, 0.6f, 0.25f));
        Material paper = Mat("Paper", new Color(0.88f, 0.85f, 0.75f));
        Material cloth = Mat("Book Cloth", new Color(0.25f, 0.3f, 0.4f));
        Material street = Mat("Window View", new Color(0.55f, 0.58f, 0.62f));

        BuildFloor(planks, log);
        Box("Ceiling", shell, new Vector3(0f, Height + 0.05f, 0f), new Vector3(Width + Wall * 2f, 0.1f, Depth + Wall * 2f), plaster);

        float hz = Depth * 0.5f + Wall * 0.5f;
        float hx = Width * 0.5f + Wall * 0.5f;
        Box("Wall West", shell, new Vector3(-hx, Height * 0.5f, 0f), new Vector3(Wall, Height, Depth + Wall * 2f), plaster);
        Box("Wall East", shell, new Vector3(hx, Height * 0.5f, 0f), new Vector3(Wall, Height, Depth + Wall * 2f), plaster);
        WallWithOpening("Wall South (Door)", new Vector3(0f, 0f, -hz), 1.2f, 0.9f, 0f, 2.1f, plaster);
        WallWithOpening("Wall North (Window)", new Vector3(0f, 0f, hz), 0f, 1.1f, 0.8f, 2.0f, plaster);
        foreach (var (name, pos, size) in new[]
        {
            ("Stencil Border West", new Vector3(-Width * 0.5f + 0.005f, 2.6f, 0f), new Vector3(0.01f, 0.18f, Depth)),
            ("Stencil Border East", new Vector3(Width * 0.5f - 0.005f, 2.6f, 0f), new Vector3(0.01f, 0.18f, Depth)),
            ("Stencil Border North", new Vector3(0f, 2.6f, Depth * 0.5f - 0.005f), new Vector3(Width, 0.18f, 0.01f)),
            ("Stencil Border South", new Vector3(0f, 2.6f, -Depth * 0.5f + 0.005f), new Vector3(Width, 0.18f, 0.01f))
        })
            Box(name, shell, pos, size, border, false);

        Transform doorHinge = Group("Door (hinge)", shell);
        doorHinge.localPosition = new Vector3(1.2f - 0.45f, 0f, -Depth * 0.5f);
        Box("Door Panelled", doorHinge, new Vector3(0.45f, 1.05f, -0.03f), new Vector3(0.88f, 2.08f, 0.05f), darkWood);

        OpenableWindow openable = BuildWindow(shell, darkWood, street);
        var audioSo = new SerializedObject(audio);
        audioSo.FindProperty("window").objectReferenceValue = openable;
        audioSo.ApplyModifiedPropertiesWithoutUndo();

        Transform bed = Group("Iron Bed", props);
        bed.localPosition = new Vector3(-1.5f, 0f, 1.35f);
        Box("Frame", bed, new Vector3(0f, 0.3f, 0f), new Vector3(0.95f, 0.12f, 1.95f), iron);
        Box("Headboard", bed, new Vector3(0f, 0.55f, 0.95f), new Vector3(0.95f, 1.1f, 0.05f), iron);
        Box("Footboard", bed, new Vector3(0f, 0.4f, -0.95f), new Vector3(0.95f, 0.8f, 0.05f), iron);
        Box("Mattress + Wool Blanket", bed, new Vector3(0f, 0.43f, -0.05f), new Vector3(0.9f, 0.15f, 1.75f), wool, false);
        Box("Bolster Pillow", bed, new Vector3(0f, 0.55f, 0.75f), new Vector3(0.85f, 0.14f, 0.25f), linen, false);

        Transform table = Group("Walnut Table", props);
        table.localPosition = new Vector3(1.5f, 0f, 1.6f);
        Box("Top", table, new Vector3(0f, 0.74f, 0f), new Vector3(0.6f, 0.04f, 1.0f), walnut);
        foreach (var leg in new[] { new Vector3(-0.26f, 0.36f, -0.46f), new Vector3(0.26f, 0.36f, -0.46f), new Vector3(-0.26f, 0.36f, 0.46f), new Vector3(0.26f, 0.36f, 0.46f) })
            Box("Leg", table, leg, new Vector3(0.05f, 0.72f, 0.05f), walnut, false);
        Box("French Grammar Books", table, new Vector3(0.1f, 0.81f, 0.3f), new Vector3(0.22f, 0.1f, 0.16f), cloth, false);
        Box("Exercise Sheets", table, new Vector3(-0.05f, 0.765f, -0.05f), new Vector3(0.21f, 0.005f, 0.3f), paper, false);
        Box("Folded Newspaper 1914", table, new Vector3(0.12f, 0.77f, -0.3f), new Vector3(0.25f, 0.01f, 0.18f), paper, false);
        Box("Glass Ink Pot", table, new Vector3(-0.15f, 0.79f, 0.15f), new Vector3(0.05f, 0.06f, 0.05f), ceramic, false);
        Box("Dip Pen", table, new Vector3(-0.1f, 0.765f, 0.2f), new Vector3(0.01f, 0.01f, 0.16f), darkWood, false);
        Transform lamp = Group("Brass Oil Lamp", table);
        lamp.localPosition = new Vector3(0.15f, 0.76f, 0.05f);
        Box("Base", lamp, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.2f, 0.12f), brass, false);
        Box("Glass Chimney", lamp, new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.2f, 0.06f), ceramic, false);

        Transform chair = Group("Bentwood Chair (moves)", props);
        chair.localPosition = new Vector3(1.0f, 0f, 1.5f);
        chair.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.04f, 0.42f), walnut);
        Box("Back", chair, new Vector3(0f, 0.7f, -0.2f), new Vector3(0.4f, 0.5f, 0.03f), walnut, false);

        Box("Wardrobe (carved cornice)", props, new Vector3(1.7f, 1.05f, -0.7f), new Vector3(0.55f, 2.1f, 1.0f), darkWood);

        Transform wash = Group("Marble Washstand", props);
        wash.localPosition = new Vector3(-1.7f, 0f, -1.4f);
        Box("Cabinet", wash, new Vector3(0f, 0.4f, 0f), new Vector3(0.5f, 0.8f, 0.8f), walnut);
        Box("Marble Top", wash, new Vector3(0f, 0.82f, 0f), new Vector3(0.52f, 0.04f, 0.82f), marble, false);
        Box("Basin", wash, new Vector3(0f, 0.88f, 0.1f), new Vector3(0.32f, 0.08f, 0.32f), ceramic, false);
        Box("Jug", wash, new Vector3(0f, 0.98f, -0.25f), new Vector3(0.12f, 0.26f, 0.12f), ceramic, false);

        Transform hooks = Group("Coat Hooks", props);
        hooks.localPosition = new Vector3(-0.4f, 1.7f, -Depth * 0.5f + 0.03f);
        Box("Rack", hooks, Vector3.zero, new Vector3(0.7f, 0.08f, 0.03f), darkWood, false);
        Box("Overcoat", hooks, new Vector3(-0.15f, -0.5f, 0.08f), new Vector3(0.4f, 1.0f, 0.12f), darkWood, false);
        Box("Bowler Hat", hooks, new Vector3(0.22f, 0.02f, 0.1f), new Vector3(0.22f, 0.12f, 0.22f), iron, false);

        var lightGo = new GameObject("Oil Lamp Light");
        lightGo.transform.SetParent(lights, false);
        lightGo.transform.position = table.TransformPoint(lamp.localPosition + new Vector3(0f, 0.32f, 0f));
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.42f);
        light.intensity = 2.5f;
        light.range = 7f;
        light.shadows = LightShadows.Soft;

        Transform poemArea = Group("Poem Area 1.2 x 1.6", root);
        PlacePoem(poemArea, log);
        DisableTestObjects(log);
        PlacePeople(log);
        SetupLeaves(log);
        log.Add("Window opens inward with E: the ambience goes from inside to outside as it opens.");
        log.Add(oldAudio != null ? "Level Audio settings kept." : "Level Audio added on " + RootName + ": drag soundtrack and ambience.");

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Milano Room\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Milano Room", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static void BuildFloor(Material material, List<string> log)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor (sweepable, single mesh)";
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(shell, false);
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localScale = new Vector3(Width, Depth, 1f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        int layer = LayerMask.NameToLayer("Floor");
        if (layer >= 0)
            floor.layer = layer;

        var collider = new GameObject("Floor Collider");
        collider.transform.SetParent(shell, false);
        collider.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        collider.AddComponent<BoxCollider>().size = new Vector3(Width + Wall * 2f, 0.1f, Depth + Wall * 2f);
        if (layer >= 0)
            collider.layer = layer;
        log.Add("Room 4 x 5 m, ceiling 3 m: plank floor, plaster walls, door, window with shutters.");
    }

    private static void WallWithOpening(string name, Vector3 center, float openingX, float openingWidth, float bottom, float top, Material material)
    {
        Transform wall = Group(name, shell);
        wall.localPosition = center;
        float left = -Width * 0.5f - Wall;
        float right = Width * 0.5f + Wall;
        float a = openingX - openingWidth * 0.5f;
        float b = openingX + openingWidth * 0.5f;
        Box("Left", wall, new Vector3((left + a) * 0.5f, Height * 0.5f, 0f), new Vector3(a - left, Height, Wall), material);
        Box("Right", wall, new Vector3((b + right) * 0.5f, Height * 0.5f, 0f), new Vector3(right - b, Height, Wall), material);
        Box("Above", wall, new Vector3(openingX, (top + Height) * 0.5f, 0f), new Vector3(openingWidth, Height - top, Wall), material);
        if (bottom > 0f)
            Box("Below", wall, new Vector3(openingX, bottom * 0.5f, 0f), new Vector3(openingWidth, bottom, Wall), material);
    }

    private static void PlacePoem(Transform area, List<string> log)
    {
        AssignPoem poem = Object.FindObjectsByType<AssignPoem>(FindObjectsInactive.Include)
            .Where(p => !EditorUtility.IsPersistent(p)).OrderBy(p => p.PoemId).FirstOrDefault();
        if (poem == null)
        {
            log.Add("WARNING: no AssignPoem found for the poem area.");
            return;
        }

        Renderer renderer = poem.GetComponent<Renderer>();
        Undo.RecordObject(poem.transform, "Build Milano Room");
        if (renderer != null)
        {
            Vector3 size = renderer.bounds.size;
            Vector3 scale = poem.transform.localScale;
            poem.transform.localScale = new Vector3(scale.x * 1.2f / Mathf.Max(0.01f, size.x), scale.y, scale.z * 1.6f / Mathf.Max(0.01f, size.z));
        }
        poem.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        area.position = Vector3.zero;

        var so = new SerializedObject(poem);
        SerializedProperty threshold = so.FindProperty("_revealThreshold");
        if (threshold != null)
            threshold.floatValue = 0.05f;
        so.ApplyModifiedProperties();
        log.Add(poem.name.Trim() + " moved to the middle of the room (1.2 x 1.6 m), found at 95% swept.");
    }

    private static void DisableTestObjects(List<string> log)
    {
        var off = new List<string>();
        foreach (GameObject go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Select(t => t.gameObject).ToList())
        {
            if (go.transform.parent != null || !TestObjects.Contains(go.name.Trim()) || EditorUtility.IsPersistent(go))
                continue;
            Undo.RecordObject(go, "Build Milano Room");
            go.SetActive(false);
            off.Add(go.name.Trim());
        }
        if (off.Count > 0)
            log.Add("Disabled (not deleted): " + string.Join(", ", off) + ".");
    }

    private static void PlacePeople(List<string> log)
    {
        FirstPersonController player = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include).FirstOrDefault(p => !EditorUtility.IsPersistent(p));
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Build Milano Room");
            Vector3 p = player.transform.position;
            player.transform.SetPositionAndRotation(new Vector3(1.2f, p.y, -1.7f), Quaternion.Euler(0f, -20f, 0f));
            log.Add("Player placed inside the door.");
        }

        GameObject carlo = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject)
            .FirstOrDefault(g => g.transform.parent == null && g.name.Trim() == "Carlo" && !EditorUtility.IsPersistent(g));
        if (carlo != null)
        {
            Undo.RecordObject(carlo.transform, "Build Milano Room");
            Vector3 c = carlo.transform.position;
            carlo.transform.SetPositionAndRotation(new Vector3(-0.9f, c.y, -1.8f), Quaternion.Euler(0f, 30f, 0f));
            log.Add("Carlo placed by the coat hooks.");
        }
    }

    private static void SetupLeaves(List<string> log)
    {
        LeafField field = Object.FindObjectsByType<LeafField>(FindObjectsInactive.Include).FirstOrDefault(f => !EditorUtility.IsPersistent(f));
        if (field == null)
        {
            log.Add("Leaf Field not found: run Tools/Leaves of War/Setup Sweeping, then build the room again.");
            return;
        }

        Undo.RecordObject(field.transform, "Build Milano Room");
        field.transform.position = Vector3.zero;
        var so = new SerializedObject(field);
        so.FindProperty("autoArea").boolValue = false;
        so.FindProperty("areaSize").vector2Value = new Vector2(Width - 0.2f, Depth - 0.2f);
        int floor = LayerMask.NameToLayer("Floor");
        if (floor >= 0)
            so.FindProperty("groundMask").intValue = 1 << floor;
        so.ApplyModifiedProperties();
        log.Add("Leaves cover the whole floor and lie only on it.");
    }

    [MenuItem("Tools/Leaves of War/Rebuild Milano Window")]
    private static void RebuildWindow()
    {
        GameObject root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject)
            .FirstOrDefault(g => g.transform.parent == null && g.name == RootName && !EditorUtility.IsPersistent(g));
        Transform roomShell = root != null ? root.transform.Find("Shell") : null;
        if (roomShell == null)
        {
            EditorUtility.DisplayDialog("Milano Window", "No Milano Room in the open scene: run Build Milano Room first.", "OK");
            return;
        }

        Transform oldWindow = roomShell.Find("Window");
        if (oldWindow != null)
            Undo.DestroyObjectImmediate(oldWindow.gameObject);
        OpenableWindow openable = BuildWindow(roomShell, Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f)), Mat("Window View", new Color(0.55f, 0.58f, 0.62f)));
        Undo.RegisterCreatedObjectUndo(openable.gameObject, "Rebuild Milano Window");

        LevelAudio audio = root.GetComponent<LevelAudio>();
        if (audio == null)
            audio = Undo.AddComponent<LevelAudio>(root);
        var so = new SerializedObject(audio);
        so.FindProperty("window").objectReferenceValue = openable;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Milano Window", "Window rebuilt: shutters open, glass casements closed; E opens and closes the casements, the ambience follows.\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static OpenableWindow BuildWindow(Transform parent, Material wood, Material street)
    {
        Material glass = GlassMat();
        Transform window = Group("Window", parent);
        window.localPosition = new Vector3(0f, 0.8f, Depth * 0.5f);
        Box("Window View (backdrop)", window, new Vector3(0f, 1.0f, 3f), new Vector3(6f, 5f, 0.05f), street, false);

        Transform casementLeft = Casement("Casement Left (hinge)", window, -1f, -0.01f, glass, wood);
        Transform casementRight = Casement("Casement Right (hinge)", window, 1f, -0.01f, glass, wood);
        Transform shutterLeft = Group("Shutter Left (hinge)", window);
        shutterLeft.localPosition = new Vector3(-0.55f, 0f, -0.05f);
        shutterLeft.localRotation = Quaternion.Euler(0f, 100f, 0f);
        Box("Shutter Left", shutterLeft, new Vector3(0.275f, 1f, 0f), new Vector3(0.55f, 2f, 0.03f), wood, false);
        Transform shutterRight = Group("Shutter Right (hinge)", window);
        shutterRight.localPosition = new Vector3(0.55f, 0f, -0.05f);
        shutterRight.localRotation = Quaternion.Euler(0f, -100f, 0f);
        Box("Shutter Right", shutterRight, new Vector3(-0.275f, 1f, 0f), new Vector3(0.55f, 2f, 0.03f), wood, false);

        var interaction = new GameObject("Window Interaction");
        interaction.transform.SetParent(window, false);
        interaction.transform.localPosition = new Vector3(0f, 1f, -0.05f);
        interaction.AddComponent<BoxCollider>().size = new Vector3(1.1f, 2f, 0.12f);
        int interactable = LayerMask.NameToLayer("Interactable");
        if (interactable >= 0)
            interaction.layer = interactable;

        OpenableWindow openable = window.gameObject.AddComponent<OpenableWindow>();
        var so = new SerializedObject(openable);
        Transform[] hinges = { casementLeft, casementRight };
        float[] angles = { 80f, -80f };
        SerializedProperty hingeList = so.FindProperty("hinges");
        SerializedProperty angleList = so.FindProperty("openAngles");
        hingeList.arraySize = hinges.Length;
        angleList.arraySize = angles.Length;
        for (int i = 0; i < hinges.Length; i++)
        {
            hingeList.GetArrayElementAtIndex(i).objectReferenceValue = hinges[i];
            angleList.GetArrayElementAtIndex(i).floatValue = angles[i];
        }
        so.FindProperty("startOpen").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return openable;
    }

    private static Transform Casement(string name, Transform window, float side, float z, Material glass, Material wood)
    {
        Transform hinge = Group(name, window);
        hinge.localPosition = new Vector3(0.55f * side, 0f, z);
        float c = -0.275f * side;
        Box("Glass", hinge, new Vector3(c, 1f, 0f), new Vector3(0.5f, 1.94f, 0.012f), glass, false);
        Box("Frame Top", hinge, new Vector3(c, 1.985f, 0f), new Vector3(0.55f, 0.03f, 0.04f), wood, false);
        Box("Frame Bottom", hinge, new Vector3(c, 0.015f, 0f), new Vector3(0.55f, 0.03f, 0.04f), wood, false);
        Box("Frame Hinge Side", hinge, new Vector3(-0.0125f * side, 1f, 0f), new Vector3(0.025f, 2f, 0.04f), wood, false);
        Box("Frame Middle Side", hinge, new Vector3(-0.5375f * side, 1f, 0f), new Vector3(0.025f, 2f, 0.04f), wood, false);
        Box("Bar", hinge, new Vector3(c, 1f, 0f), new Vector3(0.5f, 0.025f, 0.03f), wood, false);
        return hinge;
    }

    internal static Material GlassMat()
    {
        string path = MaterialFolder + "/Window Glass.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Directory.CreateDirectory(MaterialFolder);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Window Glass" };
        material.SetColor("_BaseColor", new Color(0.75f, 0.85f, 0.9f, 0.2f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Smoothness", 0.9f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    internal static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    internal static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collider)
            Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    internal static Material Mat(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Directory.CreateDirectory(MaterialFolder);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
