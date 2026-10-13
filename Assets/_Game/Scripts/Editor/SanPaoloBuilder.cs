using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using G = LevelGreybox;
using M = MilanoRoomBuilder;

public static class SanPaoloBuilder
{
    private const string LevelName = "Level 4 - San Paolo";
    private const float Width = 6f;
    private const float Depth = 7f;
    private const float Height = 3.2f;
    private const float Wall = 0.2f;
    private const float Garden = 6f;

    [MenuItem("Tools/Leaves of War/Build Level 4 - San Paolo (Greybox)")]
    private static void Build()
    {
        if (!G.Begin(LevelName, out Scene scene))
            return;

        Transform root = new GameObject("San Paolo House").transform;
        Transform shell = M.Group("Shell", root);
        Transform props = M.Group("Props", root);
        Transform garden = M.Group("Garden", root);

        Material terrazzo = M.Mat("Terrazzo", new Color(0.78f, 0.74f, 0.68f));
        Material lime = M.Mat("Lime Wall", new Color(0.9f, 0.86f, 0.76f));
        Material soil = M.Mat("Garden Soil", new Color(0.3f, 0.25f, 0.16f));
        Material gardenWall = M.Mat("Garden Wall", new Color(0.85f, 0.8f, 0.7f));
        Material wood = M.Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f));
        Material walnut = M.Mat("Walnut", new Color(0.33f, 0.2f, 0.12f));
        Material fabric = M.Mat("Sofa Fabric", new Color(0.45f, 0.42f, 0.3f));
        Material paper = M.Mat("Paper", new Color(0.88f, 0.85f, 0.75f));
        Material ceramic = M.Mat("Ceramic", new Color(0.95f, 0.95f, 0.92f));
        Material leaf = M.Mat("Foliage", new Color(0.2f, 0.38f, 0.16f));
        Material outside = M.Mat("Rainy Sky", new Color(0.55f, 0.6f, 0.62f));

        float hx = Width * 0.5f + Wall * 0.5f;
        float hz = Depth * 0.5f + Wall * 0.5f;
        G.Floor("Floor (sweepable, single mesh)", shell, Vector3.zero, Width, Depth, terrazzo, true);
        M.Box("Ceiling", shell, new Vector3(0f, Height + 0.05f, 0f), new Vector3(Width + Wall * 2f, 0.1f, Depth + Wall * 2f), lime);
        G.Wall("Wall West", shell, Depth + Wall * 2f, Height, Wall, new Vector3(-hx, 0f, 0f), 90f, 0f, 0f, 0f, 0f, lime);
        G.Wall("Wall East", shell, Depth + Wall * 2f, Height, Wall, new Vector3(hx, 0f, 0f), 90f, 0f, 0f, 0f, 0f, lime);
        G.Wall("Wall South (Front Door)", shell, Width + Wall * 2f, Height, Wall, new Vector3(0f, 0f, -hz), 0f, 1.8f, 0.95f, 0f, 2.2f, lime);
        G.Wall("Wall North (Garden Doors)", shell, Width + Wall * 2f, Height, Wall, new Vector3(0f, 0f, hz), 0f, -0.5f, 1.8f, 0f, 2.5f, lime);

        Transform door = M.Group("Front Door (hinge)", shell);
        door.localPosition = new Vector3(1.8f - 0.475f, 0f, -Depth * 0.5f);
        M.Box("Door", door, new Vector3(0.475f, 1.1f, -0.03f), new Vector3(0.93f, 2.18f, 0.05f), wood);

        foreach (float side in new[] { -1f, 1f })
        {
            Transform hinge = M.Group(side < 0f ? "Garden Door Left (hinge)" : "Garden Door Right (hinge)", shell);
            hinge.localPosition = new Vector3(-0.5f + side * 0.9f, 0f, Depth * 0.5f + 0.05f);
            hinge.localRotation = Quaternion.Euler(0f, side * 75f, 0f);
            M.Box("Frame", hinge, new Vector3(-side * 0.45f, 1.25f, 0f), new Vector3(0.88f, 2.48f, 0.04f), walnut, false);
            M.Box("Glass", hinge, new Vector3(-side * 0.45f, 1.3f, 0f), new Vector3(0.7f, 2.1f, 0.05f), M.GlassMat(), false);
        }

        float gz = Depth * 0.5f + Wall + Garden * 0.5f;
        G.Floor("Garden Ground", garden, new Vector3(0f, -0.01f, gz), Width + Wall * 2f, Garden, soil, false);
        M.Box("Garden Wall West", garden, new Vector3(-hx, 1.1f, gz), new Vector3(Wall, 2.2f, Garden), gardenWall);
        M.Box("Garden Wall East", garden, new Vector3(hx, 1.1f, gz), new Vector3(Wall, 2.2f, Garden), gardenWall);
        M.Box("Garden Wall North", garden, new Vector3(0f, 1.1f, gz + Garden * 0.5f), new Vector3(Width + Wall * 2f, 2.2f, Wall), gardenWall);
        M.Box("Sky and Rain (backdrop)", garden, new Vector3(0f, 4f, gz + Garden * 0.5f + 3f), new Vector3(20f, 10f, 0.05f), outside, false);
        G.Prop("Garden Tree", garden, new Vector3(1.9f, 0f, gz + 1.6f), new Vector3(3f, 5f, 3f), leaf);
        G.Prop("Garden Shrub 1", garden, new Vector3(-2.3f, 0f, gz + 2.2f), new Vector3(1.2f, 1.3f, 1.2f), leaf);
        G.Prop("Garden Shrub 2", garden, new Vector3(-2.4f, 0f, gz - 0.6f), new Vector3(1f, 1.1f, 1f), leaf);
        G.Prop("Garden Shrub 3", garden, new Vector3(2.5f, 0f, gz - 1.8f), new Vector3(1f, 1f, 1f), leaf);
        G.Prop("Garden Fern 1", garden, new Vector3(-0.8f, 0f, gz + 2.4f), new Vector3(0.8f, 0.6f, 0.8f), leaf);
        G.Prop("Garden Fern 2", garden, new Vector3(0.6f, 0f, gz - 2.2f), new Vector3(0.7f, 0.5f, 0.7f), leaf);
        G.Anchor("Rain (particle system here)", garden, new Vector3(0f, 6f, gz));
        G.Anchor("Rui Exit (garden)", garden, new Vector3(-0.5f, 0f, gz + 1.5f));

        G.Prop("Sofa", props, new Vector3(-2.45f, 0f, -0.3f), new Vector3(0.9f, 0.85f, 2.0f), fabric, 90f);
        G.Prop("Coffee Table", props, new Vector3(-1.3f, 0f, -0.3f), new Vector3(0.6f, 0.45f, 1.0f), walnut);
        G.Prop("Armchair", props, new Vector3(-0.2f, 0f, -1.7f), new Vector3(0.8f, 0.9f, 0.8f), fabric, -45f);
        G.Prop("Rocking Chair", props, new Vector3(1.6f, 0f, 1.2f), new Vector3(0.7f, 1f, 0.9f), walnut, 200f);
        G.Prop("Bookcase", props, new Vector3(2.78f, 0f, -1.2f), new Vector3(0.4f, 2f, 1.2f), wood, -90f);
        G.Prop("Sideboard", props, new Vector3(2.75f, 0f, 1.9f), new Vector3(0.45f, 0.85f, 1.3f), walnut, -90f);
        G.Prop("Gramophone", props, new Vector3(2.75f, 0.85f, 1.9f), new Vector3(0.35f, 0.45f, 0.35f), wood, -90f, false);
        G.Prop("Vase", props, new Vector3(2.75f, 0.85f, 1.42f), new Vector3(0.2f, 0.35f, 0.2f), ceramic);
        G.Prop("Clock", props, new Vector3(2.75f, 0.85f, 2.3f), new Vector3(0.15f, 0.15f, 0.08f), wood, -90f);
        G.Prop("Potted Plant 1", props, new Vector3(-2.55f, 0f, 3.0f), new Vector3(0.6f, 1.2f, 0.6f), leaf);
        G.Prop("Potted Plant 2", props, new Vector3(0.8f, 0f, 3.05f), new Vector3(0.5f, 1f, 0.5f), leaf);
        G.Point("Window Light (grey day)", shell, new Vector3(-0.5f, 2.6f, 2.8f), new Color(0.8f, 0.86f, 0.9f), 1.2f, 7f);

        Transform corner = G.Anchor("Child's Corner (empties on look-away)", props, new Vector3(-2.2f, 0f, -2.8f));
        Transform drawings = M.Group("1 - Drawings", corner);
        M.Box("Drawing on Floor 1", drawings, new Vector3(0.1f, 0.003f, 0.2f), new Vector3(0.21f, 0.005f, 0.3f), paper, false);
        M.Box("Drawing on Floor 2", drawings, new Vector3(0.45f, 0.003f, 0.05f), new Vector3(0.3f, 0.005f, 0.21f), paper, false);
        M.Box("Drawing on Wall 1", drawings, new Vector3(-0.68f, 1.1f, 0.3f), new Vector3(0.005f, 0.3f, 0.21f), paper, false);
        M.Box("Drawing on Wall 2", drawings, new Vector3(0.2f, 1.2f, -0.68f), new Vector3(0.3f, 0.21f, 0.005f), paper, false);
        G.Prop("2 - Wooden Toy", corner, new Vector3(0.35f, 0f, 0.5f), new Vector3(0.25f, 0.22f, 0.08f), walnut, 30f, false);
        G.Prop("3 - Small Chair", corner, new Vector3(-0.35f, 0f, -0.3f), new Vector3(0.32f, 0.55f, 0.32f), walnut, 45f);
        G.Anchor("Poem Area (child's corner)", root, new Vector3(-1.9f, 0f, -2.5f));

        Transform entry = G.Anchor("Entry Anchor (front door)", root, new Vector3(1.8f, 0f, -2.7f));
        Transform exit = G.Anchor("Exit Anchor (front door)", root, new Vector3(1.8f, 0f, -3.3f));

        root.gameObject.AddComponent<LevelAudio>();
        G.Sky(new Color(0.75f, 0.8f, 0.85f), 0.6f, new Vector3(55f, 160f, 0f), new Color(0.32f, 0.34f, 0.36f), false, Color.gray, 0f);

        G.Finish(scene, LevelName, "Level 3 - Carso", entry, exit, Vector3.zero, new Vector2(Width - 0.2f, Depth - 0.2f),
            "SAN PAOLO DEL BRASILE — 1937–1942", "Rui", "Level 5 - Roma",
            "San Paolo greybox: living room 6 x 7 m, terrazzo floor, front door (exit), open garden doors onto a walled garden 6 m deep with tree, shrubs, ferns and a rain anchor. " +
            "Sofa, coffee table, armchair, rocking chair, bookcase, sideboard with gramophone, vase and clock, plants. Child's corner: 1 drawings, 2 wooden toy, 3 small chair.\nRui's exit point: \"Rui Exit (garden)\".");
    }
}
