using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Tools/Hortensia/Bake Transform Scale Into Mesh
    ///
    /// Select one or more objects with an extreme/non-uniform localScale
    /// (e.g. a Transform scale like (1, 1, 7.95) coming from an imported
    /// model) and run this to "bake" that scale directly into the mesh's
    /// vertex positions instead. The object ends up looking IDENTICAL, but
    /// its Transform scale resets to (1, 1, 1) - which avoids the precision
    /// problems PhysX can run into when cooking a non-convex Mesh Collider
    /// under a very uneven scale (thin/degenerate collision triangles that
    /// let the player clip through in spots).
    ///
    /// Safety, by design:
    /// - The ORIGINAL mesh asset is never modified or overwritten. A brand
    ///   new .asset file is created for the baked result, saved under
    ///   Assets/GeneratedMeshes/ScaleBaked/. If you don't like the outcome,
    ///   you can always re-assign the original mesh back by hand - nothing
    ///   about the source asset changes.
    /// - MeshFilter, MeshCollider (only if it was pointing at the SAME mesh
    ///   as the MeshFilter - a collider intentionally using a different mesh
    ///   is left untouched with a warning) and the Transform's scale are all
    ///   changed through Undo.RecordObject, and the whole batch is wrapped
    ///   in a single Undo group - one Ctrl+Z undoes everything from one run.
    /// - Objects whose scale is already (1,1,1) are skipped (nothing to
    ///   bake) rather than needlessly creating a duplicate mesh asset.
    ///
    /// Only vertex positions and normals need to change for a pure scale
    /// bake - triangle topology and UVs are untouched, and normals are
    /// corrected properly for non-uniform scale (divide by the matching
    /// scale component, then renormalize - the standard inverse-transpose
    /// simplification for a diagonal scale matrix), not just carried over
    /// as-is, which would leave lighting looking subtly wrong on anything
    /// but a uniform scale.
    /// </summary>
    internal static class BakeTransformScaleIntoMeshTool
    {
        private const string OutputFolder = "Assets/GeneratedMeshes/ScaleBaked";

        [MenuItem("Tools/Hortensia/Bake Transform Scale Into Mesh")]
        private static void BakeSelection()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("Select one or more objects with a MeshFilter and a non-(1,1,1) scale first.");
                return;
            }

            EnsureOutputFolder();

            Undo.SetCurrentGroupName("Bake Transform Scale Into Mesh");
            int undoGroup = Undo.GetCurrentGroup();

            int bakedCount = 0;
            foreach (GameObject go in selected)
            {
                if (BakeOne(go))
                    bakedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Bake Transform Scale Into Mesh: baked {bakedCount} of {selected.Length} selected object(s). Press Ctrl+Z to undo the whole operation.");
        }

        private static bool BakeOne(GameObject go)
        {
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"'{go.name}': no MeshFilter with an assigned mesh - skipped.");
                return false;
            }

            Transform t = go.transform;
            Vector3 scale = t.localScale;
            if (Approximately(scale, Vector3.one))
            {
                Debug.Log($"'{go.name}': scale is already (1,1,1) - nothing to bake, skipped.");
                return false;
            }

            Mesh source = meshFilter.sharedMesh;
            if (!source.isReadable)
            {
                Debug.LogWarning(
                    $"Skipping '{go.name}': mesh '{source.name}' is not Read/Write Enabled. " +
                    "Select it, open its model Import Settings, tick Read/Write Enabled, Apply, then re-run.");
                return false;
            }

            Mesh baked = BakeScale(source, scale);
            baked.name = source.name + "_ScaleBaked";

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{baked.name}.asset");
            AssetDatabase.CreateAsset(baked, assetPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(meshFilter, "Bake Transform Scale Into Mesh");
            meshFilter.sharedMesh = baked;

            var meshCollider = go.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                if (meshCollider.sharedMesh == source)
                {
                    Undo.RecordObject(meshCollider, "Bake Transform Scale Into Mesh");
                    meshCollider.sharedMesh = baked;
                }
                else if (meshCollider.sharedMesh != null)
                {
                    Debug.LogWarning(
                        $"'{go.name}': its MeshCollider uses a DIFFERENT mesh than its MeshFilter " +
                        "('{meshCollider.sharedMesh.name}' vs '{source.name}') - left untouched. " +
                        "Update it by hand if it also needs the scale baked in.");
                }
            }

            Undo.RecordObject(t, "Bake Transform Scale Into Mesh");
            t.localScale = Vector3.one;

            return true;
        }

        private static Mesh BakeScale(Mesh source, Vector3 scale)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            bool hasNormals = normals != null && normals.Length == vertices.Length;

            var bakedVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                bakedVertices[i] = new Vector3(v.x * scale.x, v.y * scale.y, v.z * scale.z);
            }

            Vector3[] bakedNormals = null;
            if (hasNormals)
            {
                bakedNormals = new Vector3[normals.Length];
                for (int i = 0; i < normals.Length; i++)
                {
                    Vector3 n = normals[i];
                    // Inverse-transpose of a diagonal scale matrix is just the
                    // reciprocal of each component - the standard correction
                    // so normals still point the right way (and light
                    // correctly) after a non-uniform scale, instead of just
                    // carrying the original direction over unchanged.
                    Vector3 corrected = new Vector3(
                        Mathf.Approximately(scale.x, 0f) ? n.x : n.x / scale.x,
                        Mathf.Approximately(scale.y, 0f) ? n.y : n.y / scale.y,
                        Mathf.Approximately(scale.z, 0f) ? n.z : n.z / scale.z);
                    bakedNormals[i] = corrected.sqrMagnitude > 1e-12f ? corrected.normalized : n;
                }
            }

            var result = new Mesh
            {
                indexFormat = source.indexFormat,
            };
            result.SetVertices(bakedVertices);
            if (hasNormals)
                result.SetNormals(bakedNormals);

            // UVs (as many channels as the source actually has) and triangle
            // topology are unaffected by a pure vertex-position scale, so
            // they carry straight over per submesh.
            CopyUvChannelIfPresent(source, result, 0);
            CopyUvChannelIfPresent(source, result, 1);
            CopyUvChannelIfPresent(source, result, 2);
            CopyUvChannelIfPresent(source, result, 3);

            result.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
                result.SetTriangles(source.GetTriangles(sub), sub);

            if (!hasNormals)
                result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        private static void CopyUvChannelIfPresent(Mesh source, Mesh destination, int channel)
        {
            var uvs = new List<Vector2>();
            source.GetUVs(channel, uvs);
            if (uvs.Count > 0)
                destination.SetUVs(channel, uvs);
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            const float epsilon = 0.0001f;
            return Mathf.Abs(a.x - b.x) < epsilon &&
                Mathf.Abs(a.y - b.y) < epsilon &&
                Mathf.Abs(a.z - b.z) < epsilon;
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedMeshes"))
                AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/GeneratedMeshes", "ScaleBaked");
        }
    }
}