using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using G = LevelGreybox;
using M = MilanoRoomBuilder;

public static class CourtonBuilder
{
    private const string LevelName = "Level 6 - Courton";
    private const float Clearing = 16f;

    [MenuItem("Tools/Leaves of War/Build Level 6 - Courton (Greybox)")]
    private static void Build()
    {
        if (!G.Begin(LevelName, out Scene scene))
            return;

        Transform root = new GameObject("Bosco di Courton").transform;
        Transform shell = M.Group("Ground", root);
        Transform forest = M.Group("Forest", root);
        Transform props = M.Group("Props", root);
        Transform camp = M.Group("Field Camp", root);

        Material floor = M.Mat("Forest Floor", new Color(0.3f, 0.24f, 0.15f));
        Material mud = M.Mat("Forest Mud", new Color(0.25f, 0.2f, 0.13f));
        Material bark = M.Mat("Bark", new Color(0.3f, 0.22f, 0.15f));
        Material foliage = M.Mat("Foliage", new Color(0.2f, 0.38f, 0.16f));
        Material canvas = M.Mat("Tent Canvas", new Color(0.55f, 0.52f, 0.4f));
        Material wood = M.Mat("Dark Wood", new Color(0.2f, 0.13f, 0.08f));
        Material stone = M.Mat("Limestone", new Color(0.78f, 0.76f, 0.7f));

        G.Floor("Floor (sweepable, single mesh)", shell, Vector3.zero, Clearing + 2f, Clearing + 2f, floor, true);
        G.Floor("Forest Ground (outer)", shell, new Vector3(0f, -0.02f, 0f), 70f, 70f, mud, false);

        for (int i = 0; i < 18; i++)
        {
            float angle = i * 20f + (i % 2) * 7f;
            float radius = 12f + (i * 37 % 5) * 0.8f;
            Vector3 p = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            Tree("Forest Tree " + (i + 1).ToString("00"), forest, p, i * 53f, 9f, bark, foliage, false);
        }
        string[] letters = { "A", "B", "C" };
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = Quaternion.Euler(0f, 30f + i * 120f, 0f) * Vector3.forward * 5.5f;
            Tree("Leaf Tree " + letters[i] + " (branch anchors)", forest, p, i * 90f, 10f, bark, foliage, true);
        }

        for (int i = 0; i < 6; i++)
        {
            Vector3 p = Quaternion.Euler(0f, 15f + i * 61f, 0f) * Vector3.forward * (7.5f + (i % 3));
            G.Prop("Fern " + (i + 1), props, p, new Vector3(0.9f, 0.6f, 0.9f), foliage, i * 47f, false);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = Quaternion.Euler(0f, 50f + i * 90f, 0f) * Vector3.forward * 10f;
            G.Prop("Shrub " + (i + 1), props, p, new Vector3(1.4f, 1.3f, 1.4f), foliage, i * 33f);
        }
        G.Prop("Stump 1", props, new Vector3(3.5f, 0f, -4.5f), new Vector3(0.8f, 0.5f, 0.8f), bark);
        G.Prop("Stump 2", props, new Vector3(-6f, 0f, -2.5f), new Vector3(0.7f, 0.45f, 0.7f), bark);
        G.Prop("Roots", props, new Vector3(-3.5f, 0f, 4.5f), new Vector3(1.2f, 0.5f, 1.2f), bark, 0f, false);
        G.Prop("Fallen Trunk", props, new Vector3(6.5f, 0f, 2.5f), new Vector3(1f, 0.8f, 4.5f), bark, 20f);
        G.Prop("Mossy Rocks 1", props, new Vector3(-7.5f, 0f, -5.5f), new Vector3(1.5f, 0.7f, 1.5f), stone);
        G.Prop("Mossy Rocks 2", props, new Vector3(8f, 0f, -6f), new Vector3(1.3f, 0.6f, 1.3f), stone);

        for (int i = 0; i < 8; i++)
        {
            Vector3 p = Quaternion.Euler(0f, 20f + i * 45f, 0f) * Vector3.forward * 10.5f;
            G.Prop("Leaf Litter " + (i + 1), props, p, new Vector3(2.4f, 0.12f, 2.4f), floor, i * 41f, false);
        }

        Vector3 c = new Vector3(-8.5f, 0f, 7f);
        Tent("Tent 1", camp, c + new Vector3(-0.5f, 0f, 1f), 25f, canvas, wood);
        Tent("Tent 2", camp, c + new Vector3(2.8f, 0f, 2.6f), -15f, canvas, wood);
        G.Prop("Camp Crate 1", camp, c + new Vector3(1.6f, 0f, -0.8f), new Vector3(0.6f, 0.45f, 0.45f), wood, 10f);
        G.Prop("Camp Crate 2", camp, c + new Vector3(2.2f, 0f, -0.4f), new Vector3(0.5f, 0.5f, 0.5f), wood, -25f);
        G.Prop("Camp Barrel", camp, c + new Vector3(0.4f, 0f, -1.4f), new Vector3(0.55f, 0.85f, 0.55f), wood);
        G.Prop("Camp Fence 1", camp, c + new Vector3(-1.2f, 0f, -2.6f), new Vector3(2f, 0.9f, 0.15f), wood, 0f);
        G.Prop("Camp Fence 2", camp, c + new Vector3(0.9f, 0f, -2.9f), new Vector3(2f, 0.9f, 0.15f), wood, -10f);
        G.Prop("Camp Fence 3", camp, c + new Vector3(3.6f, 0f, -1.2f), new Vector3(2f, 0.9f, 0.15f), wood, 60f);
        Transform fire = G.Anchor("Camp Fire", camp, c + new Vector3(1.4f, 0f, 1.2f));
        for (int i = 0; i < 7; i++)
            M.Box("Stone", fire, Quaternion.Euler(0f, i * 51f, 0f) * Vector3.forward * 0.4f + Vector3.up * 0.08f, new Vector3(0.18f, 0.16f, 0.14f), stone, false);
        M.Box("Embers", fire, new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.06f, 0.45f), M.Mat("Embers", new Color(0.6f, 0.2f, 0.05f)), false);
        Light fireLight = G.Point("Fire Light", fire, new Vector3(0f, 0.6f, 0f), new Color(1f, 0.55f, 0.25f), 2.4f, 7f);
        fireLight.shadows = LightShadows.Soft;

        Transform lucien = G.Anchor("Lucien Spot", root, new Vector3(2.6f, 0f, -2.6f), -45f);
        G.Prop("Lucien's Bucket 1", props, lucien.localPosition + new Vector3(-0.5f, 0f, 0.3f), new Vector3(0.3f, 0.32f, 0.3f), wood);
        G.Prop("Lucien's Scythe", props, lucien.localPosition + new Vector3(0.5f, 0f, 0.4f), new Vector3(0.6f, 1.6f, 0.15f), wood, 30f, false);
        G.Prop("Lucien's Bucket 2", props, lucien.localPosition + new Vector3(-0.2f, 0f, 0.6f), new Vector3(0.3f, 0.32f, 0.3f), wood);

        G.Anchor("Poem Area (centre)", root, Vector3.zero);
        Transform entry = G.Anchor("Entry Anchor (south edge)", root, new Vector3(0f, 0f, -7f));
        Transform exit = G.Anchor("Exit Anchor (none - finale)", root, new Vector3(0f, 0f, -9f));

        root.gameObject.AddComponent<LevelAudio>();
        G.Sky(new Color(0.6f, 0.7f, 0.95f), 0.4f, new Vector3(35f, 210f, 0f), new Color(0.08f, 0.09f, 0.13f), true, new Color(0.06f, 0.07f, 0.1f), 0.035f);

        G.Finish(scene, LevelName, "Level 5 - Roma", entry, exit, Vector3.zero, new Vector2(Clearing, Clearing),
            "BOSCO DI COURTON — LUGLIO 1918", "Lucien", "",
            "Courton greybox: night clearing, sweepable forest floor 18 x 18 m, ring of 18 forest trees, 3 central Leaf Trees each with 16 Branch Anchors for the leaves, " +
            "ferns, shrubs, stumps, roots, fallen trunk, mossy rocks; leaf litter; field camp (2 tents, fence, crates, barrel, camp fire light); Lucien's spot with two buckets and a scythe; poem area in the centre. Moonlight and fog.");
    }

    private static void Tree(string name, Transform parent, Vector3 position, float yaw, float height, Material bark, Material foliage, bool anchors)
    {
        Transform tree = G.Anchor(name, parent, position, yaw);
        M.Box("Trunk", tree, new Vector3(0f, height * 0.35f, 0f), new Vector3(0.5f, height * 0.7f, 0.5f), bark);
        M.Box("Canopy", tree, new Vector3(0f, height * 0.75f, 0f), new Vector3(height * 0.45f, height * 0.5f, height * 0.45f), foliage, false);
        if (!anchors)
            return;
        Transform branches = M.Group("Branch Anchors", tree);
        for (int i = 0; i < 16; i++)
        {
            float a = i * 137.5f;
            float h = height * (0.55f + 0.4f * ((i * 7) % 16) / 16f);
            float r = height * 0.2f * (1f - ((i * 5) % 16) / 32f);
            Transform anchor = M.Group("Branch " + (i + 1).ToString("00"), branches);
            anchor.localPosition = Quaternion.Euler(0f, a, 0f) * Vector3.forward * r + Vector3.up * h;
            anchor.localRotation = Quaternion.Euler(0f, a, 0f);
        }
    }

    private static void Tent(string name, Transform parent, Vector3 position, float yaw, Material canvas, Material wood)
    {
        Transform tent = G.Anchor(name, parent, position, yaw);
        Transform left = M.Group("Side Left", tent);
        left.localPosition = new Vector3(-0.5f, 0.6f, 0f);
        left.localRotation = Quaternion.Euler(0f, 0f, -40f);
        M.Box("Canvas", left, Vector3.zero, new Vector3(0.03f, 1.55f, 2.2f), canvas);
        Transform right = M.Group("Side Right", tent);
        right.localPosition = new Vector3(0.5f, 0.6f, 0f);
        right.localRotation = Quaternion.Euler(0f, 0f, 40f);
        M.Box("Canvas", right, Vector3.zero, new Vector3(0.03f, 1.55f, 2.2f), canvas);
        M.Box("Ridge Pole", tent, new Vector3(0f, 1.2f, 0f), new Vector3(0.05f, 0.05f, 2.3f), wood, false);
    }
}
