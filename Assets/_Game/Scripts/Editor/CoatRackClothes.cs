using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CoatRackClothes
{
    private const string Folder = "Assets/_Game/Art/3D/Generated/Clothes";
    private const string RackName = "Coat Hooks";
    private const string ModelChild = "CC0 Model";
    private const string ClothesName = "Clothes";
    private const string SourceMaterial = "Assets/_Game/Materials/Greybox/Wool Blanket.mat";

    [MenuItem("Tools/Leaves of War/Add Clothes to Coat Rack")]
    private static void Menu()
    {
        Scene scene = SceneManager.GetActiveScene();
        int count = Apply(scene);
        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Coat Rack", count > 0
            ? "Overcoat, scarf and hat hung on " + count + " coat rack(s).\n\nSave the scene (Ctrl+S)."
            : "No \"" + RackName + "\" with its 3D model in the open scene. Run Level Assets > Apply to open scene first.", "OK");
    }

    internal static int Apply(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform rack in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == RackName).ToList())
            {
                Transform model = rack.Find(ModelChild);
                if (model == null || !TryBounds(model, out Bounds stand))
                    continue;
                Transform old = rack.Find(ClothesName);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                Build(rack, stand);
                count++;
            }
        return count;
    }

    private const string ChairCoatName = "Moammed's Coat (vanishes)";
    private const string ChairCoatModel = "Coat Model";

    [MenuItem("Tools/Leaves of War/Build Moammed's Coat")]
    private static void ChairCoatMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        int count = ApplyChairCoat(scene);
        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Moammed's Coat", count > 0
            ? "Coat hung on the chair.\n\nSave the scene (Ctrl+S)."
            : "No \"" + ChairCoatName + "\" in the open scene: open Level 2 - Parigi.", "OK");
    }

    internal static int ApplyChairCoat(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform coat in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == ChairCoatName).ToList())
            {
                Transform old = coat.Find(ChairCoatModel);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                BuildChairCoat(coat);
                count++;
            }
        return count;
    }

    private static void BuildChairCoat(Transform coat)
    {
        MeshRenderer box = coat.GetComponent<MeshRenderer>();
        Material cloth = box != null && box.sharedMaterial != null ? box.sharedMaterial : MaterialFor("Coat Wool", new Color(0.24f, 0.21f, 0.2f));
        Material button = MaterialFor("Coat Button", new Color(0.06f, 0.05f, 0.05f));

        var model = new GameObject(ChairCoatModel).transform;
        model.SetParent(coat, false);
        model.localPosition = new Vector3(0f, 0.5f, 0f);
        Vector3 s = coat.lossyScale;
        model.localScale = new Vector3(1f / Mathf.Max(s.x, 1e-4f), 1f / Mathf.Max(s.y, 1e-4f), 1f / Mathf.Max(s.z, 1e-4f));

        float drop = Mathf.Clamp(model.position.y - 0.08f, 0.4f, 0.95f);
        const float back = -0.05f;

        Transform body = Part("Body", model, MeshAsset("Chair Coat Body", () => ChairCoatBody(drop)), cloth);
        body.localPosition = new Vector3(0f, 0f, back - 0.06f);

        Transform roll = Part("Shoulders", model, MeshAsset("Chair Coat Shoulders", ChairCoatRoll), cloth);
        roll.localPosition = new Vector3(0f, 0.012f, back);
        roll.localRotation = Quaternion.Euler(0f, 0f, 90f);

        Transform fold = Part("Collar Fold", model, MeshAsset("Chair Coat Fold", ChairCoatFold), cloth);
        fold.localPosition = new Vector3(0f, 0f, back + 0.04f);

        foreach (float side in new[] { -1f, 1f })
        {
            Transform lapel = Part(side < 0f ? "Left Lapel" : "Right Lapel", model, MeshAsset("Chair Coat Lapel", () => Box(new Vector3(0.075f, 0.2f, 0.012f), new Vector3(0f, -0.1f, 0f))), cloth);
            lapel.localPosition = new Vector3(0.07f * side, -0.01f, back + 0.065f);
            lapel.localRotation = Quaternion.Euler(0f, 0f, 14f * side);

            Transform sleeve = Part(side < 0f ? "Left Sleeve" : "Right Sleeve", model, MeshAsset("Chair Coat Sleeve", ChairCoatSleeve), cloth);
            sleeve.localPosition = new Vector3(0.235f * side, -0.02f, back - 0.05f);
            sleeve.localRotation = Quaternion.Euler(4f, 0f, 7f * side);
        }

        for (int i = 0; i < 4; i++)
        {
            Transform b = Part("Button " + (i + 1), model, MeshAsset("Chair Coat Button", () => Box(new Vector3(0.022f, 0.022f, 0.008f), Vector3.zero)), button);
            b.localPosition = new Vector3(0.075f, -0.16f - i * 0.15f, back - 0.06f - ChairCoatDepth(0.16f + i * 0.15f, drop) - 0.003f);
        }

        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = coat.gameObject.layer;
            if (t.TryGetComponent(out MeshRenderer r))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        if (box != null)
            box.enabled = false;
    }

    private static float ChairCoatDepth(float down, float drop) => Mathf.Lerp(0.03f, 0.055f, Mathf.Clamp01(down / drop));

    private static Mesh ChairCoatBody(float drop)
    {
        var rings = new List<Vector3>();
        float[] steps = { 0f, 0.03f, 0.12f, 0.3f, 0.5f, 0.75f, 1f };
        foreach (float k in steps)
        {
            float down = k * drop;
            float half = Mathf.Lerp(0.19f, 0.25f, k) + (k > 0.05f && k < 0.35f ? 0.01f : 0f);
            rings.Add(new Vector3(-down, k == 0f ? half * 0.85f : half, k == 0f ? 0.02f : ChairCoatDepth(down, drop)));
        }
        return Lathe(rings.ToArray(), 12, true, true, 0f);
    }

    private static Mesh ChairCoatRoll() => Lathe(new[]
    {
        new Vector3(0.21f, 0.03f, 0.03f),
        new Vector3(0.19f, 0.045f, 0.04f),
        new Vector3(-0.19f, 0.045f, 0.04f),
        new Vector3(-0.21f, 0.03f, 0.03f),
    }, 8, true, true, 0f);

    private static Mesh ChairCoatFold() => Lathe(new[]
    {
        new Vector3(0f, 0.17f, 0.018f),
        new Vector3(-0.12f, 0.18f, 0.022f),
        new Vector3(-0.26f, 0.19f, 0.02f),
    }, 10, true, true, 0f);

    private static Mesh ChairCoatSleeve() => Lathe(new[]
    {
        new Vector3(0f, 0.055f, 0.045f),
        new Vector3(-0.22f, 0.05f, 0.042f),
        new Vector3(-0.46f, 0.047f, 0.04f),
        new Vector3(-0.5f, 0.052f, 0.044f),
    }, 8, true, true, 0f);

    private static bool TryBounds(Transform model, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
                bounds.Encapsulate(r.bounds);
        }
        return any && bounds.size.y > 0.3f;
    }

    private static void Build(Transform rack, Bounds stand)
    {
        Material coat = MaterialFor("Coat Wool", new Color(0.24f, 0.21f, 0.2f));
        Material scarf = MaterialFor("Scarf Wool", new Color(0.55f, 0.16f, 0.13f));
        Material felt = MaterialFor("Hat Felt", new Color(0.08f, 0.07f, 0.07f));

        var clothes = new GameObject(ClothesName).transform;
        clothes.SetParent(rack, false);
        clothes.gameObject.layer = rack.gameObject.layer;

        Vector3 forward = Vector3.ProjectOnPlane(rack.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float radius = Mathf.Clamp(Mathf.Min(stand.extents.x, stand.extents.z), 0.05f, 0.35f);
        Vector3 axis = new Vector3(stand.center.x, 0f, stand.center.z);
        float top = stand.max.y;

        Transform overcoat = Part("Overcoat", clothes, MeshAsset("Overcoat", CoatMesh), coat);
        overcoat.position = axis + Vector3.up * (top - 0.1f) + forward * (radius * 0.45f + 0.12f);
        overcoat.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(4f, 0f, 0f);
        foreach (float side in new[] { -1f, 1f })
        {
            Transform sleeve = Part(side < 0f ? "Left Sleeve" : "Right Sleeve", overcoat, MeshAsset("Sleeve", SleeveMesh), coat);
            sleeve.localPosition = new Vector3(0.19f * side, -0.13f, 0.01f);
            sleeve.localRotation = Quaternion.Euler(0f, 0f, 5f * side);
        }

        Transform scarfRoot = new GameObject("Scarf").transform;
        scarfRoot.SetParent(clothes, false);
        scarfRoot.position = axis + Vector3.up * (top - 0.14f) - right * (radius * 0.5f + 0.04f) - forward * 0.02f;
        scarfRoot.rotation = Quaternion.LookRotation(-right, Vector3.up);
        Transform fold = Part("Fold", scarfRoot, MeshAsset("Scarf Fold", ScarfFoldMesh), scarf);
        fold.localPosition = Vector3.zero;
        foreach (float side in new[] { -1f, 1f })
        {
            Transform end = Part(side < 0f ? "Front End" : "Back End", scarfRoot, MeshAsset("Scarf End", ScarfEndMesh), scarf);
            end.localPosition = new Vector3(0.035f * side, 0f, 0.012f * side);
            end.localRotation = Quaternion.Euler(side * 6f, 0f, side * -3f);
            end.localScale = new Vector3(1f, side < 0f ? 1f : 0.82f, 1f);
        }

        Transform hat = Part("Bowler Hat", clothes, MeshAsset("Bowler Hat", HatMesh), felt);
        hat.position = axis + Vector3.up * (top - 0.035f);
        hat.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-6f, 25f, 3f);

        foreach (Transform t in clothes.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = rack.gameObject.layer;
    }

    private static Transform Part(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go.transform;
    }

    private static Material MaterialFor(string name, Color tint)
    {
        EnsureFolder(Folder);
        string path = Folder + "/" + name + ".mat";
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterial);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        else if (source != null)
        {
            material.shader = source.shader;
            material.CopyPropertiesFromMaterial(source);
        }
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", tint);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh MeshAsset(string name, System.Func<Mesh> build)
    {
        EnsureFolder(Folder);
        string path = Folder + "/" + name + ".asset";
        Mesh fresh = build();
        fresh.name = name;
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }
        existing.Clear();
        existing.vertices = fresh.vertices;
        existing.uv = fresh.uv;
        existing.triangles = fresh.triangles;
        existing.RecalculateNormals();
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(fresh);
        return existing;
    }

    private static Mesh CoatMesh() => Lathe(new[]
    {
        new Vector3(0f, 0.012f, 0.012f),
        new Vector3(-0.05f, 0.05f, 0.04f),
        new Vector3(-0.09f, 0.12f, 0.07f),
        new Vector3(-0.13f, 0.2f, 0.095f),
        new Vector3(-0.35f, 0.205f, 0.1f),
        new Vector3(-0.6f, 0.215f, 0.105f),
        new Vector3(-0.85f, 0.235f, 0.115f),
        new Vector3(-1.02f, 0.255f, 0.125f),
    }, 14, true, true, 0.05f);

    private static Mesh SleeveMesh() => Lathe(new[]
    {
        new Vector3(0f, 0.05f, 0.05f),
        new Vector3(-0.25f, 0.052f, 0.052f),
        new Vector3(-0.52f, 0.058f, 0.058f),
    }, 8, true, true, 0f);

    private static Mesh HatMesh() => Lathe(new[]
    {
        new Vector3(0.13f, 0f, 0f),
        new Vector3(0.118f, 0.068f, 0.058f),
        new Vector3(0.1f, 0.09f, 0.08f),
        new Vector3(0.03f, 0.095f, 0.085f),
        new Vector3(0.018f, 0.098f, 0.087f),
        new Vector3(0.012f, 0.155f, 0.14f),
        new Vector3(0.0f, 0.15f, 0.135f),
        new Vector3(0.002f, 0.092f, 0.082f),
    }, 16, false, true, 0f);

    private static Mesh ScarfFoldMesh() => Box(new Vector3(0.1f, 0.035f, 0.06f), new Vector3(0f, 0f, 0f));

    private static Mesh ScarfEndMesh() => Box(new Vector3(0.085f, 0.62f, 0.014f), new Vector3(0f, -0.31f, 0f));

    private static Mesh Lathe(Vector3[] rings, int segments, bool capTop, bool capBottom, float flatBack)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        float length = 0f;
        for (int r = 1; r < rings.Length; r++)
            length += Mathf.Abs(rings[r].x - rings[r - 1].x) + 0.01f;
        float v = 0f;
        for (int r = 0; r < rings.Length; r++)
        {
            if (r > 0)
                v += (Mathf.Abs(rings[r].x - rings[r - 1].x) + 0.01f) / Mathf.Max(0.01f, length);
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * rings[r].y;
                float z = Mathf.Cos(a) * rings[r].z;
                if (z < 0f)
                    z *= 1f - flatBack * 6f;
                vertices.Add(new Vector3(x, rings[r].x, z));
                uvs.Add(new Vector2(s / (float)segments * 2f, v * 3f));
            }
        }
        int row = segments + 1;
        for (int r = 0; r < rings.Length - 1; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
        if (capTop)
            Cap(vertices, uvs, triangles, 0, row, segments, true);
        if (capBottom)
            Cap(vertices, uvs, triangles, (rings.Length - 1) * row, row, segments, false);

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void Cap(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int start, int row, int segments, bool top)
    {
        Vector3 center = Vector3.zero;
        for (int s = 0; s < segments; s++)
            center += vertices[start + s];
        center /= segments;
        int c = vertices.Count;
        vertices.Add(center);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int s = 0; s < segments; s++)
        {
            int a = start + s, b = start + s + 1;
            if (top)
                triangles.AddRange(new[] { c, a, b });
            else
                triangles.AddRange(new[] { c, b, a });
        }
    }

    private static Mesh Box(Vector3 size, Vector3 offset)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh source = cube.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = source.vertices.Select(p => Vector3.Scale(p, size) + offset).ToArray();
        Vector2[] uvs = source.uv.Select(uv => Vector2.Scale(uv, new Vector2(size.x * 4f + 0.2f, size.y * 4f + 0.2f))).ToArray();
        var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = source.triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Object.DestroyImmediate(cube);
        return mesh;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
