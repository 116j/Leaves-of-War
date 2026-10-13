using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using G = LevelGreybox;
using M = MilanoRoomBuilder;

public static class CarsoBuilder
{
    private const string LevelName = "Level 3 - Carso";
    private const float Length = 20f;
    private const float Width = 1.6f;
    private const float Depth = 1.9f;

    [MenuItem("Tools/Leaves of War/Build Level 3 - Carso (Greybox)")]
    private static void Build()
    {
        if (!G.Begin(LevelName, out Scene scene))
            return;

        Transform root = new GameObject("Carso Trench").transform;
        Transform shell = M.Group("Shell", root);
        Transform props = M.Group("Props", root);

        Material earth = M.Mat("Trench Earth", new Color(0.45f, 0.24f, 0.16f));
        Material lime = M.Mat("Limestone", new Color(0.78f, 0.76f, 0.7f));
        Material karst = M.Mat("Karst Ground", new Color(0.62f, 0.6f, 0.54f));
        Material planks = M.Mat("Trench Planks", new Color(0.35f, 0.27f, 0.2f));
        Material sand = M.Mat("Sandbag", new Color(0.56f, 0.5f, 0.38f));
        Material blanket = M.Mat("Grey Blanket", new Color(0.4f, 0.38f, 0.35f));
        Material wood = M.Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f));
        Material iron = M.Mat("Painted Iron", new Color(0.18f, 0.2f, 0.19f));
        Material canvas = M.Mat("Tent Canvas", new Color(0.55f, 0.52f, 0.4f));
        Material brass = M.Mat("Brass", new Color(0.78f, 0.6f, 0.25f));

        float hw = Width * 0.5f;
        G.Floor("Floor (sweepable, single mesh)", shell, Vector3.zero, Width, Length, earth, true);

        M.Box("Ground West", shell, new Vector3(-hw - 10f, Depth * 0.5f, 0f), new Vector3(20f, Depth, Length + 10f), lime);
        M.Box("Ground East North", shell, new Vector3(hw + 10f, Depth * 0.5f, 7.5f), new Vector3(20f, Depth, 15f), lime);
        M.Box("Ground East South", shell, new Vector3(hw + 10f, Depth * 0.5f, -9.4f), new Vector3(20f, Depth, 13.2f), lime);
        M.Box("Ground East (behind dugout)", shell, new Vector3(3.3f + 8.75f, Depth * 0.5f, -1.4f), new Vector3(17.5f, Depth, 2.8f), lime);
        M.Box("Trench End South", shell, new Vector3(0f, Depth * 0.5f, -Length * 0.5f - 2.5f), new Vector3(Width, Depth, 5f), lime);
        M.Box("Trench End North", shell, new Vector3(0f, Depth * 0.5f, Length * 0.5f + 2.5f), new Vector3(Width, Depth, 5f), lime);
        M.Box("Karst Surface West", shell, new Vector3(-hw - 10f, Depth + 0.01f, 0f), new Vector3(20f, 0.02f, Length + 10f), karst, false);
        M.Box("Karst Surface East", shell, new Vector3(3.3f + 8.75f, Depth + 0.01f, 0f), new Vector3(17.5f, 0.02f, Length + 10f), karst, false);

        M.Box("Revetment West", shell, new Vector3(-hw + 0.02f, 0.7f, 0f), new Vector3(0.04f, 1.4f, Length), planks, false);
        M.Box("Revetment East North", shell, new Vector3(hw - 0.02f, 0.7f, 5f), new Vector3(0.04f, 1.4f, 10f), planks, false);
        M.Box("Revetment East South", shell, new Vector3(hw - 0.02f, 0.7f, -6.4f), new Vector3(0.04f, 1.4f, 7.2f), planks, false);
        for (float z = -Length * 0.5f + 1f; z < Length * 0.5f; z += 2.5f)
        {
            M.Box("Post West", shell, new Vector3(-hw + 0.06f, 0.75f, z), new Vector3(0.1f, 1.5f, 0.1f), wood, false);
            if (z > 0.3f || z < -3.1f)
                M.Box("Post East", shell, new Vector3(hw - 0.06f, 0.75f, z), new Vector3(0.1f, 1.5f, 0.1f), wood, false);
        }
        M.Box("Sandbags West", shell, new Vector3(-hw - 0.35f, Depth + 0.15f, 0f), new Vector3(0.6f, 0.3f, Length), sand);
        M.Box("Sandbags East North", shell, new Vector3(hw + 0.35f, Depth + 0.15f, 5f), new Vector3(0.6f, 0.3f, 10f), sand);
        M.Box("Sandbags East South", shell, new Vector3(hw + 0.35f, Depth + 0.15f, -6.4f), new Vector3(0.6f, 0.3f, 7.2f), sand);

        Transform dugout = M.Group("First-Aid Dugout", shell);
        G.Floor("Dugout Floor", dugout, new Vector3(2.05f, 0f, -1.4f), 2.5f, 2.8f, earth, false);
        M.Box("Dugout Front Left", dugout, new Vector3(hw + 0.1f, 1f, -2.4f), new Vector3(0.2f, 2f, 0.8f), planks);
        M.Box("Dugout Front Right", dugout, new Vector3(hw + 0.1f, 1f, -0.4f), new Vector3(0.2f, 2f, 0.8f), planks);
        M.Box("Dugout Lintel", dugout, new Vector3(hw + 0.1f, 1.85f, -1.4f), new Vector3(0.2f, 0.3f, 1.2f), wood, false);
        M.Box("Dugout Roof", dugout, new Vector3(2.05f, 2.05f, -1.4f), new Vector3(2.7f, 0.15f, 3.2f), planks);
        M.Box("Dugout Roof Sandbags", dugout, new Vector3(2.05f, 2.3f, -1.4f), new Vector3(2.5f, 0.35f, 3f), sand, false);
        M.Box("Dugout Side South", dugout, new Vector3(2.05f, 1f, -2.9f), new Vector3(2.5f, 2f, 0.2f), lime);
        M.Box("Dugout Side North", dugout, new Vector3(2.05f, 1f, 0.1f), new Vector3(2.5f, 2f, 0.2f), lime);

        Transform stretcher = G.Anchor("Stretcher", props, new Vector3(2.2f, 0f, -1.4f));
        M.Box("Pole Left", stretcher, new Vector3(-0.27f, 0.25f, 0f), new Vector3(0.04f, 0.04f, 2.1f), wood, false);
        M.Box("Pole Right", stretcher, new Vector3(0.27f, 0.25f, 0f), new Vector3(0.04f, 0.04f, 2.1f), wood, false);
        M.Box("Canvas", stretcher, new Vector3(0f, 0.25f, 0f), new Vector3(0.52f, 0.01f, 1.8f), canvas);
        G.Prop("Medical Crate", props, new Vector3(3.0f, 0f, -2.4f), new Vector3(0.45f, 0.45f, 0.6f), wood);
        G.Prop("Supply Crate", props, new Vector3(3.0f, 0f, -0.4f), new Vector3(0.5f, 0.5f, 0.5f), wood);
        G.Prop("Water Bucket", props, new Vector3(1.3f, 0f, -2.5f), new Vector3(0.3f, 0.32f, 0.3f), wood);
        G.Prop("Dugout Stove", props, new Vector3(3.0f, 0f, -1.4f), new Vector3(0.45f, 0.9f, 0.45f), iron);
        G.Prop("Trench Shovel", props, new Vector3(-0.68f, 0f, -5f), new Vector3(0.25f, 1.1f, 0.1f), wood, 90f, false);
        G.Anchor("Lantern Hook (dugout)", props, new Vector3(1.2f, 1.7f, -1.4f));

        Transform waypoints = M.Group("Lantern Waypoints", root);
        Vector3[] wp = { new Vector3(0f, 0f, -7f), new Vector3(0.4f, 0f, -3.5f), new Vector3(-0.3f, 0f, 0.5f), new Vector3(0.3f, 0f, 3.5f), new Vector3(0.1f, 0f, 6.2f) };
        for (int i = 0; i < wp.Length; i++)
            G.Anchor(i == wp.Length - 1 ? "WP 5 - next to the companion" : "WP " + (i + 1), waypoints, wp[i], 180f);

        Transform lantern = G.Prop("Pietro's Lantern", props, wp[0] + new Vector3(0f, 0.9f, 0f), new Vector3(0.18f, 0.3f, 0.18f), brass, 0f, false);
        Light lanternLight = G.Point("Lantern Light (radius 2.5 m)", lantern, new Vector3(0f, 0.2f, 0f), new Color(1f, 0.72f, 0.38f), 2.2f, 2.5f);
        lanternLight.shadows = LightShadows.Soft;

        Transform companion = G.Anchor("Companion (seen only when unseen)", props, new Vector3(-0.4f, 0f, 6.5f));
        M.Box("Body under Blanket", companion, new Vector3(0f, 0.13f, 0f), new Vector3(0.5f, 0.26f, 1.75f), blanket, false);
        M.Box("Boots", companion, new Vector3(0f, 0.1f, -0.95f), new Vector3(0.35f, 0.2f, 0.15f), M.Mat("Dark Coat", new Color(0.12f, 0.12f, 0.14f)), false);
        G.Anchor("Poem Area (beside the companion)", root, new Vector3(0.3f, 0f, 6.5f));

        G.Prop("Karst Boulder 1", props, new Vector3(-3.2f, Depth, -6f), new Vector3(1.2f, 0.8f, 1.1f), lime);
        G.Prop("Karst Boulder 2", props, new Vector3(4.2f, Depth, 4.5f), new Vector3(1.5f, 1f, 1.3f), lime);
        G.Prop("Karst Rocks 1", props, new Vector3(-5f, Depth, 2f), new Vector3(1.6f, 0.6f, 1.6f), lime);
        G.Prop("Karst Rocks 2", props, new Vector3(2.8f, Depth, -8f), new Vector3(1.4f, 0.5f, 1.4f), lime);
        G.Prop("Karst Rocks 3", props, new Vector3(-2.4f, Depth, 8.5f), new Vector3(1.2f, 0.5f, 1.2f), lime);
        G.Prop("Dead Tree", props, new Vector3(-6.5f, Depth, -1f), new Vector3(1.2f, 3.5f, 1.2f), wood);

        Transform wire = M.Group("Barbed Wire", props);
        int post = 1;
        for (float z = -9f; z <= 9f; z += 2.5f)
            G.Prop("Wire Post " + post++, wire, new Vector3(-4.2f, Depth, z), new Vector3(0.14f, 1.05f, 0.14f), wood, post * 23f);
        float[] walls = { -7.5f, -4.5f, -1.5f, 4.5f };
        for (int i = 0; i < walls.Length; i++)
            G.Prop("Drystone Wall " + (i + 1), props, new Vector3(5.5f, Depth, walls[i]), new Vector3(0.6f, 0.9f, 3f), lime);
        G.Prop("Drystone Wall 5", props, new Vector3(-8f, Depth, 0f), new Vector3(0.6f, 0.8f, 3f), lime);
        G.Prop("Drystone Wall 6", props, new Vector3(-8f, Depth, 3.2f), new Vector3(0.6f, 0.8f, 3f), lime);
        M.Box("Wire High", wire, new Vector3(-4.2f, Depth + 0.85f, 0f), new Vector3(0.01f, 0.01f, 18f), iron, false);
        M.Box("Wire Low", wire, new Vector3(-4.2f, Depth + 0.45f, 0f), new Vector3(0.01f, 0.01f, 18f), iron, false);

        Transform entry = G.Anchor("Entry Anchor (trench start)", root, new Vector3(0f, 0f, -8.8f));
        Transform exit = G.Anchor("Exit Anchor (trench end)", root, new Vector3(0f, 0f, 9.4f));
        G.Anchor("Pietro Exit (trench end)", root, new Vector3(0f, 0f, 9.8f));

        root.gameObject.AddComponent<LevelAudio>();
        G.Sky(new Color(0.55f, 0.65f, 0.9f), 0.35f, new Vector3(28f, 35f, 0f), new Color(0.07f, 0.08f, 0.12f), true, new Color(0.05f, 0.06f, 0.1f), 0.03f);

        G.Finish(scene, LevelName, "Level 2 - Parigi", entry, exit, Vector3.zero, new Vector2(Width - 0.1f, Length - 0.4f),
            "IL CARSO — CIMA QUATTRO — 23 DICEMBRE 1915", "Pietro", "Level 4 - San Paolo",
            "Carso greybox: night trench 20 x 1.6 m, 1.9 m deep, limestone walls, plank revetment, sandbags, first-aid dugout (stretcher, crates, bucket), " +
            "stove and shovel, Pietro's lantern with a 2.5 m light and 5 waypoints, the companion under a blanket, karst rocks, drystone walls, dead tree and barbed wire above. Moonlight and fog.");
    }
}
