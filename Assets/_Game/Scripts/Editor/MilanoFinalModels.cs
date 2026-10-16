using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class MilanoFinalModels
{
    private const string Root = "Assets/_Game/Art/3D/Import/Level 1 Milano Assets";
    private const string MaterialFolder = Root + "/_Materials";
    internal const string Child = "Final Model";
    private const float RoomHalfWidth = 2f;
    private const float RoomHalfDepth = 2.5f;
    private const float WardrobeSouthLimit = -1.0f;

    [MenuItem("Tools/Leaves of War/Milano: Place Final Models")]
    private static void Menu()
    {
        Scene scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        int placed = Apply(scene, log);
        if (placed > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Milano final models\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Milano final models", placed + " models placed.\n\n" + string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    internal static int Apply(Scene scene, List<string> log)
    {
        if (!AssetDatabase.IsValidFolder(Root))
            return 0;
        int count = 0;

        Transform bed = Find(scene, "Iron Bed");
        if (bed != null && Place(bed, "Bed/Bed.fbx", null, null, "Bed", new[] { "*" }, Bed, log))
            count++;

        Transform table = Find(scene, "Walnut Table");
        if (table != null && Place(table, "Table&Chair/Tabel&Chair.fbx", "Box007", "Box002", "Table&Chair/Textures", new[] { "Top", "Leg" }, Table, log))
            count++;

        Transform chair = Find(scene, "Bentwood Chair (moves)");
        if (chair != null && Place(chair, "Table&Chair/Tabel&Chair.fbx", "Box002", "Box007", "Table&Chair/Textures", new[] { "*" }, Chair, log))
            count++;

        Transform wardrobe = Find(scene, "Wardrobe (carved cornice)");
        if (wardrobe != null && Place(wardrobe, "Wardrobe/WardrobeLowUnwrap.fbx", null, null, "Wardrobe", new[] { "." }, Wardrobe, log))
            count++;

        Transform door = Find(scene, "Door Panelled");
        if (door != null && Place(door, "Door/door.fbx", null, null, "Door", new[] { "." }, Door, log))
            count++;

        Transform books = Find(scene, "French Grammar Books");
        if (books != null && PlaceBooks(books, log))
            count++;
        return count;
    }

    private sealed class Fit
    {
        public Transform target;
        public Bounds goal;
        public List<Vector3> points;
        public Bounds model;
    }

    private delegate bool Rule(Fit fit, int yaw, out Vector3 scale, out Vector3 offset, out float score);

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name && !Inside(t))
                    return t;
        return null;
    }

    private static bool Inside(Transform t)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.name == Child || p.name == "CC0 Model")
                return true;
        return false;
    }

    private static bool Place(Transform target, string file, string keep, string drop, string textures, string[] hide, Rule rule, List<string> log)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + file);
        if (asset == null)
        {
            log.Add(target.name + ": missing " + file);
            return false;
        }
        Clear(target);
        List<Renderer> parts = Parts(target, hide);
        Matrix4x4 toFrame = Frame(target).inverse;
        Bounds goal = BoundsOf(parts.Select(r => MeshPoints(r, toFrame)).SelectMany(p => p));
        if (parts.Count == 0)
            goal = new Bounds(Vector3.up * 0.5f, Vector3.one);

        Transform fitNode = Nodes(target, out Transform pivot);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, fitNode);
        instance.name = Path.GetFileNameWithoutExtension(file);
        if (keep != null)
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true).Where(x => x.name == drop).ToList())
                t.gameObject.SetActive(false);
        Quaternion baseRotation = instance.transform.localRotation;

        float bestScore = float.MinValue;
        int bestYaw = 0;
        Vector3 bestScale = Vector3.one, bestOffset = Vector3.zero;
        for (int yaw = 0; yaw < 360; yaw += 90)
        {
            Pose(instance.transform, fitNode, baseRotation, yaw);
            var fit = new Fit { target = target, goal = goal, points = ModelPoints(instance, toFrame) };
            if (fit.points.Count == 0)
                break;
            fit.model = BoundsOf(fit.points);
            if (rule(fit, yaw, out Vector3 scale, out Vector3 offset, out float score) && score > bestScore)
            {
                bestScore = score;
                bestYaw = yaw;
                bestScale = scale;
                bestOffset = offset;
            }
        }
        if (bestScore == float.MinValue)
        {
            Object.DestroyImmediate(pivot.gameObject);
            log.Add(target.name + ": could not fit " + file);
            return false;
        }

        Pose(instance.transform, fitNode, baseRotation, bestYaw);
        fitNode.localScale = bestScale;
        fitNode.localPosition = bestOffset;
        Finish(target, pivot, instance, parts, textures);
        log.Add(target.name + ": " + Path.GetFileName(file) + (keep != null ? " (" + keep + ")" : "") + ", turned " + bestYaw + "°, scale " + bestScale.x.ToString("0.00"));
        return true;
    }

    private static void Pose(Transform instance, Transform fitNode, Quaternion baseRotation, int yaw)
    {
        fitNode.localScale = Vector3.one;
        fitNode.localPosition = Vector3.zero;
        instance.localPosition = Vector3.zero;
        instance.localRotation = Quaternion.Euler(0f, yaw, 0f) * baseRotation;
    }

    private static Transform Nodes(Transform target, out Transform pivot)
    {
        pivot = new GameObject(Child).transform;
        pivot.SetParent(target, false);
        Vector3 s = target.localScale;
        pivot.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(s.x), 1e-5f), 1f / Mathf.Max(Mathf.Abs(s.y), 1e-5f), 1f / Mathf.Max(Mathf.Abs(s.z), 1e-5f));
        var fitNode = new GameObject("Fit").transform;
        fitNode.SetParent(pivot, false);
        return fitNode;
    }

    private static void Clear(Transform target)
    {
        foreach (string name in new[] { Child, "CC0 Model" })
        {
            Transform old;
            while ((old = target.Find(name)) != null)
                Object.DestroyImmediate(old.gameObject);
        }
    }

    private static List<Renderer> Parts(Transform target, string[] hide)
    {
        var parts = new List<Renderer>();
        if (hide.Contains("."))
        {
            Renderer own = target.GetComponent<Renderer>();
            if (own != null)
                parts.Add(own);
            return parts;
        }
        foreach (Transform child in target)
        {
            if (child.name == Child || child.name == "CC0 Model")
                continue;
            if (!hide.Contains("*") && !hide.Contains(child.name))
                continue;
            parts.AddRange(child.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Inside(r.transform)));
        }
        return parts;
    }

    private static Matrix4x4 Frame(Transform target) => Matrix4x4.TRS(target.position, target.rotation, Vector3.one);

    private static IEnumerable<Vector3> MeshPoints(Renderer r, Matrix4x4 toFrame)
    {
        MeshFilter filter = r.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            yield break;
        Bounds b = filter.sharedMesh.bounds;
        Matrix4x4 m = toFrame * r.transform.localToWorldMatrix;
        for (int i = 0; i < 8; i++)
            yield return m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
    }

    private static List<Vector3> ModelPoints(GameObject instance, Matrix4x4 toFrame)
    {
        var points = new List<Vector3>();
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(false))
        {
            if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy)
                continue;
            Matrix4x4 m = toFrame * filter.transform.localToWorldMatrix;
            foreach (Vector3 v in filter.sharedMesh.vertices)
                points.Add(m.MultiplyPoint3x4(v));
        }
        return points;
    }

    private static Bounds BoundsOf(IEnumerable<Vector3> points)
    {
        bool first = true;
        var b = new Bounds();
        foreach (Vector3 p in points)
        {
            if (first)
            {
                b = new Bounds(p, Vector3.zero);
                first = false;
            }
            else
                b.Encapsulate(p);
        }
        return b;
    }

    private static Vector3 Upper(Fit fit, float fraction)
    {
        float cut = fit.model.min.y + fit.model.size.y * fraction;
        List<Vector3> top = fit.points.Where(p => p.y >= cut).ToList();
        if (top.Count == 0)
            return Vector3.zero;
        Vector3 c = top.Aggregate(Vector3.zero, (a, p) => a + p) / top.Count;
        return new Vector3(c.x - fit.model.center.x, 0f, c.z - fit.model.center.z);
    }

    private static float LocalZ(Transform target, string child)
    {
        Transform c = target.Find(child);
        return c != null ? c.localPosition.z : 0f;
    }

    private static Vector3 Uniform(float k) => new Vector3(k, k, k);

    private static Vector3 Align(Fit fit, Vector3 scale, float bottom)
    {
        Vector3 c = Vector3.Scale(fit.model.center, scale);
        float min = fit.model.min.y * scale.y;
        return new Vector3(fit.goal.center.x - c.x, bottom - min, fit.goal.center.z - c.z);
    }

    private static Vector3 KeepInRoom(Fit fit, Vector3 scale, Vector3 offset, float minRoomZ = float.MinValue)
    {
        Transform room = fit.target.parent;
        if (room == null)
            return offset;
        Matrix4x4 frameToRoom = room.worldToLocalMatrix * Frame(fit.target);
        Vector3 shift = Vector3.zero;
        var corners = new List<Vector3>();
        Vector3 min = Vector3.Scale(fit.model.min, scale) + offset;
        Vector3 max = Vector3.Scale(fit.model.max, scale) + offset;
        for (int i = 0; i < 8; i++)
            corners.Add(frameToRoom.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)));
        float lowX = corners.Min(p => p.x), highX = corners.Max(p => p.x);
        float lowZ = corners.Min(p => p.z), highZ = corners.Max(p => p.z);
        float wall = 0.02f;
        if (highX > RoomHalfWidth - wall)
            shift.x -= highX - (RoomHalfWidth - wall);
        if (lowX < -RoomHalfWidth + wall)
            shift.x += -RoomHalfWidth + wall - lowX;
        float zFloor = Mathf.Max(-RoomHalfDepth + wall, minRoomZ);
        if (highZ > RoomHalfDepth - wall)
            shift.z -= highZ - (RoomHalfDepth - wall);
        if (lowZ < zFloor)
            shift.z += zFloor - lowZ;
        Vector3 local = Frame(fit.target).inverse.MultiplyVector(room.localToWorldMatrix.MultiplyVector(shift));
        return offset + local;
    }

    private static bool Bed(Fit fit, int yaw, out Vector3 scale, out Vector3 offset, out float score)
    {
        float k = fit.goal.size.z / Mathf.Max(fit.model.size.z, 1e-4f);
        scale = Uniform(k);
        offset = KeepInRoom(fit, scale, Align(fit, scale, 0f));
        float head = Mathf.Sign(LocalZ(fit.target, "Headboard"));
        score = (fit.model.size.z > fit.model.size.x ? 10f : 0f) + (Mathf.Sign(Upper(fit, 0.75f).z) == head ? 5f : 0f) - yaw * 0.001f;
        return true;
    }

    private static bool Table(Fit fit, int yaw, out Vector3 scale, out Vector3 offset, out float score)
    {
        float k = fit.goal.max.y / Mathf.Max(fit.model.max.y - fit.model.min.y, 1e-4f);
        scale = Uniform(k);
        offset = KeepInRoom(fit, scale, Align(fit, scale, 0f));
        bool longZ = fit.goal.size.z >= fit.goal.size.x;
        score = ((fit.model.size.z >= fit.model.size.x) == longZ ? 10f : 0f) - yaw * 0.001f;
        return true;
    }

    private static bool Chair(Fit fit, int yaw, out Vector3 scale, out Vector3 offset, out float score)
    {
        float k = fit.goal.max.y / Mathf.Max(fit.model.size.y, 1e-4f);
        scale = Uniform(k);
        offset = KeepInRoom(fit, scale, Align(fit, scale, 0f));
        Vector3 back = Upper(fit, 0.7f);
        float wanted = Mathf.Sign(LocalZ(fit.target, "Back"));
        score = (Mathf.Abs(back.z) > Mathf.Abs(back.x) && Mathf.Sign(back.z) == wanted ? 10f : 0f) + Mathf.Abs(back.z) - yaw * 0.001f;
        return true;
    }

    private static bool Wardrobe(Fit fit, int yaw, out Vector3 scale, out Vector3 offset, out float score)
    {
        float k = 2.0f / Mathf.Max(fit.model.size.y, 1e-4f);
        scale = Uniform(k);
        bool thinX = fit.goal.size.x < fit.goal.size.z;
        bool modelThinX = fit.model.size.x < fit.model.size.z;
        Vector3 toRoom = Frame(fit.target).inverse.MultiplyVector(-fit.target.position);
        float roomSide = thinX ? Mathf.Sign(toRoom.x) : Mathf.Sign(toRoom.z);
        float front = FrontSign(fit, thinX);
        offset = Align(fit, scale, fit.goal.min.y);
        if (thinX)
            offset.x = (roomSide > 0 ? fit.goal.min.x : fit.goal.max.x) - (roomSide > 0 ? fit.model.min.x : fit.model.max.x) * k;
        else
            offset.z = (roomSide > 0 ? fit.goal.min.z : fit.goal.max.z) - (roomSide > 0 ? fit.model.min.z : fit.model.max.z) * k;
        offset = KeepInRoom(fit, scale, offset, WardrobeSouthLimit);
        score = (thinX == modelThinX ? 10f : 0f) + (front == roomSide ? 5f : 0f) - yaw * 0.001f;
        return true;
    }

    private static float FrontSign(Fit fit, bool alongX)
    {
        float center = alongX ? fit.model.center.x : fit.model.center.z;
        float half = (alongX ? fit.model.extents.x : fit.model.extents.z) * 0.5f;
        int plus = fit.points.Count(p => (alongX ? p.x : p.z) > center + half);
        int minus = fit.points.Count(p => (alongX ? p.x : p.z) < center - half);
        return plus >= minus ? 1f : -1f;
    }

    private static bool Door(Fit fit, int yaw, out Vector3 scale, out Vector3 offset, out float score)
    {
        float sx = fit.goal.size.x / Mathf.Max(fit.model.size.x, 1e-4f);
        float sy = fit.goal.size.y / Mathf.Max(fit.model.size.y, 1e-4f);
        scale = new Vector3(sx, sy, sx);
        offset = new Vector3(fit.goal.center.x - fit.model.center.x * sx, fit.goal.min.y - fit.model.min.y * sy, fit.goal.center.z - fit.model.center.z * sx);
        float mid = Median(fit.points.Select(p => p.z).ToList());
        List<Vector3> knobs = fit.points.OrderByDescending(p => Mathf.Abs(p.z - mid)).Take(Mathf.Max(4, fit.points.Count / 20)).ToList();
        float handle = knobs.Count > 0 ? knobs.Average(p => p.x) - fit.model.center.x : 0f;
        score = (fit.model.size.z < fit.model.size.x ? 10f : 0f) + (handle > 0f ? 5f : 0f) - yaw * 0.001f;
        return true;
    }

    private static float Median(List<float> values)
    {
        if (values.Count == 0)
            return 0f;
        values.Sort();
        return values[values.Count / 2];
    }

    private static bool PlaceBooks(Transform target, List<string> log)
    {
        var first = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/GrammarBooks/GrammarBook1.fbx");
        var second = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/GrammarBooks/GrammarBook2.fbx");
        if (first == null && second == null)
            return false;
        Clear(target);
        List<Renderer> parts = Parts(target, new[] { "." });
        Matrix4x4 toFrame = Frame(target).inverse;
        Bounds goal = BoundsOf(parts.SelectMany(r => MeshPoints(r, toFrame)));
        Transform fitNode = Nodes(target, out Transform pivot);
        float y = goal.min.y;
        int index = 0;
        foreach ((GameObject asset, string textures) in new[] { (first, "GrammarBooks/GrammarBook1Textures"), (second, "GrammarBooks/GrammarBook2Textures") })
        {
            if (asset == null)
                continue;
            var holder = new GameObject("Book " + (index + 1)).transform;
            holder.SetParent(fitNode, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            Bounds b = BoundsOf(ModelPoints(instance, toFrame));
            if (b.size.z < b.size.x)
            {
                holder.localRotation = Quaternion.Euler(0f, 90f, 0f);
                b = BoundsOf(ModelPoints(instance, toFrame));
            }
            float k = Mathf.Min(goal.size.x * 0.9f / Mathf.Max(b.size.x, 1e-4f), goal.size.z / Mathf.Max(b.size.z, 1e-4f));
            holder.localScale = Uniform(k);
            b = BoundsOf(ModelPoints(instance, toFrame));
            holder.localPosition += new Vector3(goal.center.x - b.center.x, y - b.min.y, goal.center.z - b.center.z);
            holder.localRotation = Quaternion.Euler(0f, index == 0 ? 0f : 7f, 0f) * holder.localRotation;
            y += b.size.y;
            Materials(instance, textures);
            index++;
        }
        foreach (Renderer r in parts)
            r.enabled = false;
        SetLayer(pivot, target.gameObject.layer);
        log.Add(target.name + ": " + index + " grammar book(s) stacked");
        return true;
    }

    private static void Finish(Transform target, Transform pivot, GameObject instance, List<Renderer> parts, string textures)
    {
        Materials(instance, textures);
        SetLayer(pivot, target.gameObject.layer);
        foreach (Renderer r in parts)
            r.enabled = false;
        if (target.name == "Door Panelled")
            return;
        foreach (Renderer r in parts)
            foreach (Collider c in r.GetComponents<Collider>())
                if (!c.isTrigger)
                    c.enabled = false;
        Bounds local = BoundsOf(ModelPoints(instance, pivot.worldToLocalMatrix));
        var box = pivot.gameObject.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = local.size;
    }

    private static void SetLayer(Transform root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private static void Materials(GameObject instance, string folder)
    {
        Material material = FolderMaterial(Root + "/" + folder);
        if (material == null)
            return;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
    }

    private static Material FolderMaterial(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return null;
        string[] textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder).ToArray();
        string colour = textures.FirstOrDefault(p => Has(p, "colour", "color", "albedo", "diffuse", "basecolor"));
        if (colour == null)
            return null;
        string normal = textures.FirstOrDefault(p => Has(p, "normal"));
        string occlusion = textures.FirstOrDefault(p => Has(p, "ambocc", "ao"));

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(Root, "_Materials");
        string name = folder.Substring(Root.Length + 1).Replace('/', ' ').Replace('&', '+');
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colour));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.2f);
        material.SetFloat("_Metallic", 0f);
        if (normal != null)
        {
            if (AssetImporter.GetAtPath(normal) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.EnableKeyword("_NORMALMAP");
        }
        if (occlusion != null)
        {
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(occlusion));
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool Has(string path, params string[] words)
    {
        string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return words.Any(w => w == "ao" ? file.EndsWith("ao") : file.Contains(w));
    }
}
