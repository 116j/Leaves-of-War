using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using G = LevelGreybox;
using M = MilanoRoomBuilder;

public static class RomaBuilder
{
    private const string LevelName = "Level 5 - Roma";
    private const float Width = 5f;
    private const float Depth = 6f;
    private const float Height = 3.4f;
    private const float Wall = 0.25f;

    [MenuItem("Tools/Leaves of War/Build Level 5 - Roma (Greybox)")]
    private static void Build()
    {
        if (!G.Begin(LevelName, out Scene scene))
            return;

        Transform root = new GameObject("Roma Study").transform;
        Transform shell = M.Group("Shell", root);
        Transform props = M.Group("Props", root);

        Material parquet = M.Mat("Parquet", new Color(0.42f, 0.28f, 0.17f));
        Material plaster = M.Mat("Worn Plaster", new Color(0.8f, 0.74f, 0.62f));
        Material shutterWood = M.Mat("Shutter Wood", new Color(0.3f, 0.38f, 0.28f));
        Material wood = M.Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f));
        Material walnut = M.Mat("Walnut", new Color(0.33f, 0.2f, 0.12f));
        Material books = M.Mat("Paper Books", new Color(0.85f, 0.78f, 0.6f));
        Material fabric = M.Mat("Sofa Fabric", new Color(0.45f, 0.42f, 0.3f));
        Material brass = M.Mat("Brass", new Color(0.78f, 0.6f, 0.25f));
        Material street = M.Mat("Roma Street", new Color(0.62f, 0.56f, 0.48f));

        float hx = Width * 0.5f + Wall * 0.5f;
        float hz = Depth * 0.5f + Wall * 0.5f;
        G.Floor("Floor (sweepable, single mesh)", shell, Vector3.zero, Width, Depth, parquet, true);
        M.Box("Ceiling", shell, new Vector3(0f, Height + 0.05f, 0f), new Vector3(Width + Wall * 2f, 0.1f, Depth + Wall * 2f), plaster);
        G.Wall("Wall West", shell, Depth + Wall * 2f, Height, Wall, new Vector3(-hx, 0f, 0f), 90f, 0f, 0f, 0f, 0f, plaster);
        G.Wall("Wall East (Window)", shell, Depth + Wall * 2f, Height, Wall, new Vector3(hx, 0f, 0f), 90f, 0f, 1.2f, 0.9f, 2.6f, plaster);
        G.Wall("Wall South (Door)", shell, Width + Wall * 2f, Height, Wall, new Vector3(0f, 0f, -hz), 0f, -1.4f, 0.9f, 0f, 2.15f, plaster);
        G.Wall("Wall North", shell, Width + Wall * 2f, Height, Wall, new Vector3(0f, 0f, hz), 0f, 0f, 0f, 0f, 0f, plaster);

        Transform door = M.Group("Door (hinge)", shell);
        door.localPosition = new Vector3(-1.4f - 0.45f, 0f, -Depth * 0.5f);
        M.Box("Door", door, new Vector3(0.45f, 1.07f, -0.03f), new Vector3(0.88f, 2.13f, 0.05f), wood);

        Transform window = M.Group("Window (shutters)", shell);
        window.localPosition = new Vector3(Width * 0.5f + Wall, 0.9f, 0f);
        M.Box("Glass (interact)", window, new Vector3(-Wall * 0.5f, 0.85f, 0f), new Vector3(0.02f, 1.7f, 1.2f), M.GlassMat());
        M.Box("Sill", window, new Vector3(-Wall * 0.5f, -0.03f, 0f), new Vector3(Wall + 0.1f, 0.06f, 1.3f), plaster, false);
        Transform left = M.Group("Shutter Left (hinge)", window);
        left.localPosition = new Vector3(0.03f, 0f, -0.6f);
        M.Box("Shutter Left", left, new Vector3(0f, 0.85f, 0.3f), new Vector3(0.03f, 1.7f, 0.6f), shutterWood, false);
        Transform right = M.Group("Shutter Right (hinge)", window);
        right.localPosition = new Vector3(0.03f, 0f, 0.6f);
        M.Box("Shutter Right", right, new Vector3(0f, 0.85f, -0.3f), new Vector3(0.03f, 1.7f, 0.6f), shutterWood, false);
        M.Box("Street View (backdrop)", window, new Vector3(6f, 1f, 0f), new Vector3(0.05f, 8f, 14f), street, false);
        G.Anchor("Street Voices (source)", window, new Vector3(4f, 0f, 0f));

        OpenableWindow shutters = window.gameObject.AddComponent<OpenableWindow>();
        var so = new SerializedObject(shutters);
        SerializedProperty hinges = so.FindProperty("hinges");
        hinges.arraySize = 2;
        hinges.GetArrayElementAtIndex(0).objectReferenceValue = left;
        hinges.GetArrayElementAtIndex(1).objectReferenceValue = right;
        SerializedProperty angles = so.FindProperty("openAngles");
        angles.arraySize = 2;
        angles.GetArrayElementAtIndex(0).floatValue = 55f;
        angles.GetArrayElementAtIndex(1).floatValue = -55f;
        so.FindProperty("startOpen").boolValue = true;
        so.FindProperty("openPrompt").stringValue = "OPEN SHUTTERS";
        so.FindProperty("closePrompt").stringValue = "CLOSE SHUTTERS";
        so.ApplyModifiedPropertiesWithoutUndo();

        G.Prop("Writing Desk", props, new Vector3(1.75f, 0f, 1.4f), new Vector3(0.7f, 0.76f, 1.3f), walnut);
        G.Prop("Desk Chair", props, new Vector3(1.05f, 0f, 1.4f), new Vector3(0.45f, 0.9f, 0.45f), walnut, 90f);
        Transform lamp = G.Prop("Oil Lamp", props, new Vector3(1.85f, 0.76f, 1.9f), new Vector3(0.15f, 0.4f, 0.15f), brass, 0f, false);
        G.Point("Lamp Light", lamp, new Vector3(0f, 0.35f, 0f), new Color(1f, 0.75f, 0.45f), 1.4f, 4.5f);
        G.Prop("Pocket Watch", props, new Vector3(1.6f, 0.76f, 1.0f), new Vector3(0.05f, 0.012f, 0.05f), brass, 0f, false);
        G.Prop("Ink Bottle", props, new Vector3(1.55f, 0.76f, 1.75f), new Vector3(0.08f, 0.14f, 0.08f), brass, 0f, false);
        G.Prop("Stove", props, new Vector3(1.9f, 0f, -2.4f), new Vector3(0.5f, 1f, 0.5f), wood, -45f);
        G.Prop("Curtains", props, new Vector3(Width * 0.5f - 0.06f, 0.65f, 0f), new Vector3(0.08f, 2.2f, 1.7f), fabric, 0f, false);
        G.Prop("Study Bookcase 1", props, new Vector3(-2.3f, 0f, 1.2f), new Vector3(0.4f, 2.2f, 1.2f), wood, 90f);
        G.Prop("Study Bookcase 2", props, new Vector3(-2.3f, 0f, -0.3f), new Vector3(0.4f, 2.2f, 1.2f), wood, 90f);
        G.Prop("Reading Armchair", props, new Vector3(-1.2f, 0f, 2.3f), new Vector3(0.8f, 0.9f, 0.8f), fabric, 150f);
        G.Prop("Drawer Cabinet", props, new Vector3(0.3f, 0f, 2.75f), new Vector3(1f, 1f, 0.45f), wood, 180f);

        Vector3[] stacks =
        {
            new Vector3(-1.7f, 0f, 1.6f), new Vector3(-1.75f, 0f, 0.2f), new Vector3(-1.6f, 0f, -1.4f),
            new Vector3(0.2f, 0f, 1.9f), new Vector3(1.2f, 0f, 2.6f), new Vector3(2.0f, 0f, -0.6f)
        };
        for (int i = 0; i < stacks.Length; i++)
            G.Prop("Book Stack " + (i + 1), props, stacks[i], new Vector3(0.3f, 0.2f + 0.08f * (i % 3), 0.24f), books, i * 37f, false);

        G.Anchor("Poem Area (by the door)", root, new Vector3(-1.4f, 0f, -2.2f));
        Transform entry = G.Anchor("Entry Anchor (door)", root, new Vector3(-1.4f, 0f, -2.5f), 0f);
        Transform exit = G.Anchor("Exit Anchor (room centre)", root, Vector3.zero);
        G.Anchor("Leone Exit (door)", root, new Vector3(-1.4f, 0f, -3.6f));

        LevelAudio audio = root.gameObject.AddComponent<LevelAudio>();
        var audioSo = new SerializedObject(audio);
        audioSo.FindProperty("window").objectReferenceValue = shutters;
        audioSo.ApplyModifiedPropertiesWithoutUndo();
        G.Sky(new Color(1f, 0.88f, 0.7f), 0.5f, new Vector3(18f, -90f, 0f), new Color(0.16f, 0.15f, 0.14f), false, Color.gray, 0f);

        G.Finish(scene, LevelName, "Level 4 - San Paolo", entry, exit, Vector3.zero, new Vector2(Width - 0.2f, Depth - 0.2f),
            "ROMA — 1943–1944", "Leone", "Level 6 - Courton",
            "Roma greybox: study 5 x 6 m, herringbone parquet, worn plaster, door south, window east with half-closed shutters (OpenableWindow: CLOSE SHUTTERS), " +
            "street voices source and LevelAudio linked to the shutters. Desk with oil lamp, ink bottle and pocket watch, curtains, potbelly stove, chair, two bookcases, armchair, drawer cabinet, six book stacks on the floor. Poem area by the door.");
    }
}
