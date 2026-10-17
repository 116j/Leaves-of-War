using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BroomSkewFix
{
    private const string Folder = "Assets/_Game/Art/3D/Generated/Unskewed";
    private const float Tolerance = 1e-3f;

    [MenuItem("Tools/Leaves of War/Fix Broom Deforming When It Turns")]
    private static void Menu()
    {
        var roots = new List<Transform>();
        foreach (Transform t in Selection.transforms)
            roots.Add(t);
        if (roots.Count == 0)
            foreach (BroomSweeper sweeper in Object.FindObjectsByType<BroomSweeper>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(sweeper);
                if (so.FindProperty("broom")?.objectReferenceValue is Transform broom && !roots.Contains(broom))
                    roots.Add(broom);
            }
        if (roots.Count == 0)
        {
            EditorUtility.DisplayDialog("Fix Broom", "No broom found: select it in the Hierarchy and run the tool again.", "OK");
            return;
        }

        var log = new List<string>();
        foreach (Transform root in roots)
        {
            log.Add(Fix(root));
            log.Add(SamePose(root));
        }
        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        string text = string.Join("\n\n", log) + "\n\nSave the scene (Ctrl+S).";
        Debug.Log("Fix Broom\n" + text);
        EditorUtility.DisplayDialog("Fix Broom", text, "OK");
    }

    private static string SamePose(Transform broom)
    {
        int count = 0;
        foreach (BroomSweeper sweeper in Object.FindObjectsByType<BroomSweeper>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(sweeper);
            if (so.FindProperty("broom")?.objectReferenceValue as Transform != broom)
                continue;
            SerializedProperty tilt = so.FindProperty("sweepRotationOffset");
            if (tilt == null)
                continue;
            tilt.vector3Value = Vector3.zero;
            so.ApplyModifiedProperties();
            count++;
        }
        return count > 0
            ? "Sweeping now keeps the broom exactly as it is rotated in the scene: rotate \"" + broom.name + "\" as you like and it stays that way, held or sweeping (only the side swing is added)."
            : "No Broom Sweeper uses \"" + broom.name + "\": its sweeping pose was not touched.";
    }

    private static bool Uneven(Vector3 s) =>
        Mathf.Abs(Mathf.Abs(s.x) - Mathf.Abs(s.y)) > Tolerance * Mathf.Abs(s.x) + 1e-6f ||
        Mathf.Abs(Mathf.Abs(s.y) - Mathf.Abs(s.z)) > Tolerance * Mathf.Abs(s.y) + 1e-6f ||
        s.x < 0f || s.y < 0f || s.z < 0f;

    private static string Fix(Transform broom)
    {
        Transform top = null;
        for (Transform p = broom.parent; p != null; p = p.parent)
            if (Uneven(p.localScale))
                top = p;
        Transform scope = top != null ? top : broom;

        List<Transform> nodes = scope.GetComponentsInChildren<Transform>(true).ToList();
        if (!nodes.Any(n => Uneven(n.localScale)))
            return broom.name + ": no stretched scale above or inside it.";

        Undo.RegisterFullObjectHierarchyUndo(scope.gameObject, "Fix Broom");
        var world = nodes.ToDictionary(n => n, n => n.localToWorldMatrix);
        var position = nodes.ToDictionary(n => n, n => n.position);
        var rotation = nodes.ToDictionary(n => n, n => n.rotation);

        var changed = new List<string>();
        foreach (Transform n in nodes)
        {
            Vector3 s = n.localScale;
            if (!Uneven(s))
                continue;
            float k = Mathf.Pow(Mathf.Abs(s.x * s.y * s.z), 1f / 3f);
            n.localScale = Vector3.one * Mathf.Max(k, 1e-6f);
            changed.Add(n.name + " (" + s.x.ToString("0.###") + ", " + s.y.ToString("0.###") + ", " + s.z.ToString("0.###") + ")");
        }
        foreach (Transform n in nodes)
        {
            n.SetPositionAndRotation(position[n], rotation[n]);
        }

        int baked = 0;
        var skipped = new List<string>();
        EnsureFolder(Folder);
        foreach (Transform n in nodes)
        {
            Matrix4x4 rest = n.worldToLocalMatrix * world[n];
            if (IsIdentity(rest))
                continue;
            MeshFilter filter = n.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh mesh = Bake(filter.sharedMesh, rest);
                string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + Clean(n.name) + " " + Clean(filter.sharedMesh.name) + ".asset");
                AssetDatabase.CreateAsset(mesh, path);
                Undo.RecordObject(filter, "Fix Broom");
                filter.sharedMesh = mesh;
                MeshCollider meshCollider = n.GetComponent<MeshCollider>();
                if (meshCollider != null)
                {
                    Undo.RecordObject(meshCollider, "Fix Broom");
                    meshCollider.sharedMesh = mesh;
                }
                baked++;
            }
            foreach (Collider c in n.GetComponents<Collider>())
            {
                Undo.RecordObject(c, "Fix Broom");
                Vector3 axis = new Vector3(rest.GetColumn(0).magnitude, rest.GetColumn(1).magnitude, rest.GetColumn(2).magnitude);
                if (c is BoxCollider box)
                {
                    box.center = rest.MultiplyPoint3x4(box.center);
                    box.size = Vector3.Scale(box.size, axis);
                }
                else if (c is SphereCollider sphere)
                {
                    sphere.center = rest.MultiplyPoint3x4(sphere.center);
                    sphere.radius *= Mathf.Max(axis.x, axis.y, axis.z);
                }
                else if (c is CapsuleCollider capsule)
                {
                    capsule.center = rest.MultiplyPoint3x4(capsule.center);
                    float along = axis[capsule.direction];
                    float across = Mathf.Max(axis[(capsule.direction + 1) % 3], axis[(capsule.direction + 2) % 3]);
                    capsule.height *= along;
                    capsule.radius *= across;
                }
            }
            if (n.GetComponent<SkinnedMeshRenderer>() != null)
                skipped.Add(n.name);
        }

        string where = top != null
            ? "The stretched scale was on \"" + top.name + "\", above the broom: that is what bent it while turning."
            : "The stretched scale was on the broom's own parts.";
        return broom.name + ": fixed. " + where +
               "\nScales made even: " + string.Join(", ", changed) +
               "\nMeshes reshaped to look the same: " + baked +
               (skipped.Count > 0 ? "\nNot fixed (animated meshes): " + string.Join(", ", skipped) : "");
    }

    private static bool IsIdentity(Matrix4x4 m)
    {
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                if (Mathf.Abs(m[r, c] - (r == c ? 1f : 0f)) > 1e-4f)
                    return false;
        return true;
    }

    private static Mesh Bake(Mesh source, Matrix4x4 m)
    {
        var mesh = Object.Instantiate(source);
        mesh.name = source.name + " Unskewed";
        Matrix4x4 normalMatrix = m.inverse.transpose;
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = m.MultiplyPoint3x4(vertices[i]);
        mesh.vertices = vertices;
        Vector3[] normals = mesh.normals;
        if (normals.Length == vertices.Length)
        {
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            mesh.normals = normals;
        }
        Vector4[] tangents = mesh.tangents;
        if (tangents.Length == vertices.Length)
        {
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = m.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            mesh.tangents = tangents;
        }
        if (m.determinant < 0f)
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] tris = mesh.GetTriangles(s);
                for (int k = 0; k + 2 < tris.Length; k += 3)
                {
                    int tmp = tris[k + 1];
                    tris[k + 1] = tris[k + 2];
                    tris[k + 2] = tmp;
                }
                mesh.SetTriangles(tris, s);
            }
        mesh.RecalculateBounds();
        return mesh;
    }

    private static string Clean(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\- ]+", "_").Trim();

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
