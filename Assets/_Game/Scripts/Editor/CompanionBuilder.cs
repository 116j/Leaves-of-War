using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CompanionBuilder
{
    private const string CompanionName = "Companion (seen only when unseen)";
    private const string Folder = "Assets/_Game/Art/3D/Generated/Companion";
    private const string CharacterFolder = "Assets/_Game/Art/Characters/MakeHuman";
    private const string BodyName = "Companion Body";
    private const string BlanketName = "Companion Blanket";
    private static readonly string[] Models = { "Leone", "Rui", "Carlo", "Lucien", "Pietro" };
    private static readonly string[] OldParts = { "Body under Blanket", "Boots" };

    [MenuItem("Tools/Leaves of War/Build Carso Companion")]
    private static void Menu()
    {
        string result = Build(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Carso Companion", result, "OK");
    }

    internal static string Build(Scene scene)
    {
        Transform companion = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == CompanionName);
        if (companion == null)
            return "No \"" + CompanionName + "\" in the open scene: open Level 3 - Carso.";
        GameObject model = Models.Select(Model).FirstOrDefault(m => m != null);
        if (model == null)
            return "No MakeHuman character found in " + CharacterFolder + ".";

        Material blanketMaterial = null;
        foreach (string part in OldParts)
        {
            Transform old = companion.Find(part);
            if (old == null)
                continue;
            if (blanketMaterial == null && part == OldParts[0] && old.TryGetComponent(out MeshRenderer r))
                blanketMaterial = r.sharedMaterial;
            Object.DestroyImmediate(old.gameObject);
        }
        foreach (string part in new[] { BodyName, BlanketName })
        {
            Transform old = companion.Find(part);
            if (old == null)
                continue;
            if (part == BlanketName && blanketMaterial == null && old.TryGetComponent(out MeshRenderer r))
                blanketMaterial = r.sharedMaterial;
            Object.DestroyImmediate(old.gameObject);
        }
        if (blanketMaterial == null)
            blanketMaterial = BlanketMaterial();

        var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PeriodClothes.Dress(temp, "Companion");
        RestArms(temp.transform);

        temp.transform.SetPositionAndRotation(companion.position, companion.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f));
        Vector3 shoulder = companion.InverseTransformPoint(Bone(temp.transform, "upperarm", "_l") is Transform s ? s.position : temp.transform.position);
        Mesh body = Bake(temp, companion, out Material[] materials);
        Object.DestroyImmediate(temp);
        if (body == null)
            return "The character has no meshes to bake.";

        Vector3[] vertices = body.vertices;
        Bounds b = body.bounds;
        Vector3 shift = new Vector3(-b.center.x, 0.01f - b.min.y, -b.center.z);
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] += shift;
        body.vertices = vertices;
        body.RecalculateBounds();
        shoulder += shift;

        EnsureFolder(Folder);
        body = Save(body, Folder + "/Companion Body.asset");
        var bodyObject = new GameObject(BodyName) { layer = companion.gameObject.layer };
        bodyObject.transform.SetParent(companion, false);
        bodyObject.AddComponent<MeshFilter>().sharedMesh = body;
        bodyObject.AddComponent<MeshRenderer>().sharedMaterials = materials;

        Mesh blanket = Save(Blanket(vertices, body.bounds, shoulder.z), Folder + "/Companion Blanket.asset");
        var blanketObject = new GameObject(BlanketName) { layer = companion.gameObject.layer };
        blanketObject.transform.SetParent(companion, false);
        blanketObject.AddComponent<MeshFilter>().sharedMesh = blanket;
        blanketObject.AddComponent<MeshRenderer>().sharedMaterial = blanketMaterial;

        foreach (Collider c in companion.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        PeripheralFigure figure = companion.GetComponent<PeripheralFigure>();
        if (figure == null)
            figure = companion.gameObject.AddComponent<PeripheralFigure>();
        var figureSo = new SerializedObject(figure);
        figureSo.FindProperty("_onlyWhileLookedAt").boolValue = true;
        figureSo.FindProperty("_focusAngle").floatValue = 30f;
        figureSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return "The companion is now a soldier (" + model.name + "'s body in grigioverde) lying on his back, a blanket over him up to the shoulders. He is there only while you look at him: look away and he is gone.\n\nSave the scene (Ctrl+S).";
    }

    private static GameObject Model(string name)
    {
        string folder = CharacterFolder + "/" + name;
        if (!AssetDatabase.IsValidFolder(folder))
            return null;
        string path = AssetDatabase.FindAssets("t:Model", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));
        return path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
    }

    private static Transform Bone(Transform root, string part, string side) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t =>
        {
            string n = t.name.ToLowerInvariant();
            return n.Contains(part) && n.EndsWith(side) && !n.Contains("twist");
        });

    private static void RestArms(Transform root)
    {
        Transform pelvis = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLowerInvariant().Contains("pelvis"));
        foreach (string side in new[] { "_l", "_r" })
        {
            Transform upper = Bone(root, "upperarm", side);
            Transform lower = Bone(root, "lowerarm", side);
            Transform hand = Bone(root, "hand", side);
            if (upper == null || lower == null || hand == null)
                continue;
            Vector3 centre = pelvis != null ? pelvis.position : root.position;
            Vector3 outward = Vector3.ProjectOnPlane(upper.position - centre, Vector3.up).normalized;
            Vector3 down = (Vector3.down + outward * 0.12f).normalized;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, down) * upper.rotation;
            Vector3 forearm = (Vector3.down + outward * 0.05f + Vector3.forward * 0.08f).normalized;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, forearm) * lower.rotation;
        }
    }

    private static Mesh Bake(GameObject source, Transform space, out Material[] materials)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var groups = new Dictionary<Material, List<int>>();
        var order = new List<Material>();

        void Add(Mesh mesh, Matrix4x4 toSpace, Material[] mats)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Vector2[] uv = mesh.uv;
            int start = positions.Count;
            for (int i = 0; i < v.Length; i++)
            {
                positions.Add(toSpace.MultiplyPoint3x4(v[i]));
                normals.Add(i < n.Length ? toSpace.MultiplyVector(n[i]).normalized : Vector3.up);
                uvs.Add(i < uv.Length ? uv[i] : Vector2.zero);
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;
                Material m = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                if (m == null)
                    continue;
                if (!groups.TryGetValue(m, out List<int> tris))
                {
                    groups[m] = tris = new List<int>();
                    order.Add(m);
                }
                foreach (int index in mesh.GetTriangles(s))
                    tris.Add(start + index);
            }
        }

        Matrix4x4 toLocal = space.worldToLocalMatrix;
        foreach (SkinnedMeshRenderer skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null)
                continue;
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            Add(baked, toLocal * Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one), skin.sharedMaterials);
            Object.DestroyImmediate(baked);
        }
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer r = filter.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || filter.sharedMesh == null)
                continue;
            Add(filter.sharedMesh, toLocal * filter.transform.localToWorldMatrix, r.sharedMaterials);
        }

        materials = order.ToArray();
        if (positions.Count == 0)
            return null;
        var result = new Mesh { name = BodyName };
        if (positions.Count > 65000)
            result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        result.SetVertices(positions);
        result.SetNormals(normals);
        result.SetUVs(0, uvs);
        result.subMeshCount = order.Count;
        for (int s = 0; s < order.Count; s++)
            result.SetTriangles(groups[order[s]], s);
        result.RecalculateBounds();
        return result;
    }

    private static Mesh Blanket(Vector3[] body, Bounds bounds, float top)
    {
        const float Cell = 0.035f;
        float x0 = bounds.min.x - 0.14f, x1 = bounds.max.x + 0.14f;
        float z0 = bounds.min.z - 0.06f, z1 = Mathf.Clamp(top + 0.03f, bounds.min.z + 0.3f, bounds.max.z);
        int nx = Mathf.Max(4, Mathf.CeilToInt((x1 - x0) / Cell));
        int nz = Mathf.Max(4, Mathf.CeilToInt((z1 - z0) / Cell));
        var height = new float[nx, nz];
        foreach (Vector3 v in body)
        {
            int i = Mathf.FloorToInt((v.x - x0) / Cell), k = Mathf.FloorToInt((v.z - z0) / Cell);
            if (i < 0 || k < 0 || i >= nx || k >= nz)
                continue;
            height[i, k] = Mathf.Max(height[i, k], v.y);
        }
        float[,] raw = (float[,])height.Clone();
        for (int pass = 0; pass < 2; pass++)
            height = Dilate(height);
        for (int pass = 0; pass < 3; pass++)
            height = Blur(height);
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
                height[i, k] = Mathf.Max(height[i, k], raw[i, k] + 0.02f, 0.008f);

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        for (int k = 0; k <= nz; k++)
            for (int i = 0; i <= nx; i++)
            {
                float h = 0f;
                int count = 0;
                for (int di = -1; di <= 0; di++)
                    for (int dk = -1; dk <= 0; dk++)
                    {
                        int a = i + di, c = k + dk;
                        if (a < 0 || c < 0 || a >= nx || c >= nz)
                            continue;
                        h += height[a, c];
                        count++;
                    }
                h = count > 0 ? h / count : 0.008f;
                if (i == 0 || i == nx)
                    h = 0.008f;
                vertices.Add(new Vector3(x0 + i * Cell, h, z0 + k * Cell));
                uvs.Add(new Vector2(i * Cell * 2f, k * Cell * 2f));
            }
        var tris = new List<int>();
        for (int k = 0; k < nz; k++)
            for (int i = 0; i < nx; i++)
            {
                int a = k * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        var mesh = new Mesh { name = BlanketName };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        Vector3[] n = mesh.normals;
        if (n.Length > 0 && n.Sum(x => x.y) < 0f)
        {
            for (int t = 0; t < tris.Count; t += 3)
            {
                int tmp = tris[t + 1];
                tris[t + 1] = tris[t + 2];
                tris[t + 2] = tmp;
            }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float[,] Dilate(float[,] h)
    {
        int nx = h.GetLength(0), nz = h.GetLength(1);
        var r = new float[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                float m = h[i, k];
                for (int di = -1; di <= 1; di++)
                    for (int dk = -1; dk <= 1; dk++)
                    {
                        int a = i + di, c = k + dk;
                        if (a >= 0 && c >= 0 && a < nx && c < nz)
                            m = Mathf.Max(m, h[a, c] * 0.92f);
                    }
                r[i, k] = m;
            }
        return r;
    }

    private static float[,] Blur(float[,] h)
    {
        int nx = h.GetLength(0), nz = h.GetLength(1);
        var r = new float[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                float sum = 0f;
                int count = 0;
                for (int di = -1; di <= 1; di++)
                    for (int dk = -1; dk <= 1; dk++)
                    {
                        int a = i + di, c = k + dk;
                        if (a < 0 || c < 0 || a >= nx || c >= nz)
                            continue;
                        sum += h[a, c];
                        count++;
                    }
                r[i, k] = sum / count;
            }
        return r;
    }

    private static Material BlanketMaterial()
    {
        string path = Folder + "/Companion Blanket.mat";
        EnsureFolder(Folder);
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null)
            return m;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(lit);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", new Color(0.4f, 0.38f, 0.35f));
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Cull"))
            m.SetFloat("_Cull", 0f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Mesh Save(Mesh mesh, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
