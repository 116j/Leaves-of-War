using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Tools/Hortensia/Split Mesh Into Connected Pieces
    ///
    /// Reverses a previous merge (e.g. from CombineMeshesTool): select one or
    /// more objects whose mesh is the RESULT of an earlier combine, and this
    /// splits it back into one object per originally-separate piece of
    /// geometry - determined by actual vertex connectivity, not just by
    /// material. Two chunks of the mesh that never touched before the merge
    /// come back as two separate objects, even if they happened to share a
    /// material; two chunks that DID touch (even across a UV/normal seam,
    /// where vertices are duplicated at the same position) come back as one.
    ///
    /// A resulting piece that itself uses multiple materials keeps them all,
    /// as separate submeshes on that one object - exactly mirroring how
    /// CombineMeshesTool builds multi-material combined meshes in the first
    /// place.
    ///
    /// Safety, by design:
    /// - The original combined object is only DISABLED (SetActive(false)),
    ///   never destroyed - if the split result isn't what you wanted, just
    ///   delete the new pieces and re-enable the original. Delete it by hand
    ///   once you're happy with the split.
    /// - New meshes are saved as fresh .asset files under
    ///   Assets/GeneratedMeshes/Split/ - the original mesh asset is never
    ///   modified.
    /// - Every new object is created via Undo.RegisterCreatedObjectUndo, and
    ///   disabling the original goes through Undo.RecordObject, both
    ///   collapsed into one Undo group per source object - one Ctrl+Z per
    ///   split.
    /// </summary>
    internal static class SplitMeshTool
    {
        private const string OutputFolder = "Assets/GeneratedMeshes/Split";

        // Vertices within this distance of each other are treated as the
        // same connectivity node - handles duplicated seam vertices (hard
        // edges, UV seams) that sit at the same position but have different
        // indices in the source mesh.
        private const float PositionMergeEpsilon = 0.0001f;

        [MenuItem("Tools/Hortensia/Split Mesh Into Connected Pieces")]
        private static void SplitSelection()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("Select one or more previously-combined objects (with a MeshFilter) first.");
                return;
            }

            EnsureOutputFolder();

            Undo.SetCurrentGroupName("Split Mesh Into Connected Pieces");
            int undoGroup = Undo.GetCurrentGroup();

            int splitCount = 0;
            foreach (GameObject go in selected)
            {
                if (SplitOne(go))
                    splitCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Split Mesh Into Connected Pieces: processed {splitCount} of {selected.Length} selected object(s). Press Ctrl+Z to undo.");
        }

        private static bool SplitOne(GameObject go)
        {
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"'{go.name}': no MeshFilter with an assigned mesh - skipped.");
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

            var meshRenderer = go.GetComponent<MeshRenderer>();
            Material[] sourceMaterials = meshRenderer != null
                ? meshRenderer.sharedMaterials
                : new[] { default(Material) };

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            List<Vector2> uvs = new List<Vector2>();
            source.GetUVs(0, uvs);
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasUvs = uvs.Count == vertices.Length;

            int[] parent = BuildConnectivity(source, vertices);

            // Gather every (component root -> list of (submeshIndex, triangle)) entries.
            var componentTriangles = new Dictionary<int, List<(int submesh, int a, int b, int c)>>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i];
                    int root = Find(parent, a);
                    if (!componentTriangles.TryGetValue(root, out List<(int, int, int, int)> list))
                    {
                        list = new List<(int, int, int, int)>();
                        componentTriangles[root] = list;
                    }
                    list.Add((sub, a, tris[i + 1], tris[i + 2]));
                }
            }

            if (componentTriangles.Count <= 1)
            {
                Debug.LogWarning($"'{go.name}': mesh is a single connected piece already - nothing to split.");
                return false;
            }

            Transform sourceParent = go.transform.parent;
            int pieceIndex = 0;
            foreach (List<(int submesh, int a, int b, int c)> triangles in componentTriangles.Values)
            {
                pieceIndex++;
                BuildPiece(
                    go, source.name, pieceIndex, triangles,
                    vertices, hasNormals ? normals : null, hasUvs ? uvs : null,
                    sourceMaterials, sourceParent);
            }

            Undo.RecordObject(go, "Split Mesh Into Connected Pieces");
            go.SetActive(false);

            return true;
        }

        private static void BuildPiece(
            GameObject source,
            string sourceMeshName,
            int pieceIndex,
            List<(int submesh, int a, int b, int c)> triangles,
            Vector3[] sourceVertices,
            Vector3[] sourceNormals,
            List<Vector2> sourceUvs,
            Material[] sourceMaterials,
            Transform parent)
        {
            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = sourceNormals != null ? new List<Vector3>() : null;
            var newUvs = sourceUvs != null ? new List<Vector2>() : null;

            int RemapIndex(int original)
            {
                if (remap.TryGetValue(original, out int mapped))
                    return mapped;

                mapped = newVertices.Count;
                remap[original] = mapped;
                newVertices.Add(sourceVertices[original]);
                newNormals?.Add(sourceNormals[original]);
                newUvs?.Add(sourceUvs[original]);
                return mapped;
            }

            // Group this piece's triangles by submesh index, in the order
            // submeshes first appear, so multi-material pieces keep each
            // material as its own submesh (same approach CombineMeshesTool
            // uses when building a combined mesh in the first place).
            var submeshOrder = new List<int>();
            var trianglesBySubmesh = new Dictionary<int, List<int>>();
            foreach ((int submesh, int a, int b, int c) in triangles)
            {
                if (!trianglesBySubmesh.TryGetValue(submesh, out List<int> list))
                {
                    list = new List<int>();
                    trianglesBySubmesh[submesh] = list;
                    submeshOrder.Add(submesh);
                }

                list.Add(RemapIndex(a));
                list.Add(RemapIndex(b));
                list.Add(RemapIndex(c));
            }

            var mesh = new Mesh
            {
                indexFormat = newVertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(newVertices);
            if (newNormals != null)
                mesh.SetNormals(newNormals);
            if (newUvs != null)
                mesh.SetUVs(0, newUvs);

            mesh.subMeshCount = submeshOrder.Count;
            var pieceMaterials = new Material[submeshOrder.Count];
            for (int i = 0; i < submeshOrder.Count; i++)
            {
                int originalSubmesh = submeshOrder[i];
                mesh.SetTriangles(trianglesBySubmesh[originalSubmesh], i);
                pieceMaterials[i] = originalSubmesh < sourceMaterials.Length
                    ? sourceMaterials[originalSubmesh]
                    : null;
            }

            if (newNormals == null)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string pieceName = $"{sourceMeshName}_Piece{pieceIndex}";
            mesh.name = pieceName;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{pieceName}.asset");
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();

            var pieceObject = new GameObject(pieceName);
            Undo.RegisterCreatedObjectUndo(pieceObject, "Split Mesh Into Connected Pieces");
            pieceObject.transform.SetParent(parent, false);
            // The source mesh's vertices are already baked in the source
            // object's local space (that's how CombineMeshesTool produced
            // it), so giving the new piece the EXACT same local transform as
            // the source reproduces its original world position precisely.
            pieceObject.transform.localPosition = source.transform.localPosition;
            pieceObject.transform.localRotation = source.transform.localRotation;
            pieceObject.transform.localScale = source.transform.localScale;

            MeshFilter filter = pieceObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = pieceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = pieceMaterials;

            if (source.GetComponent<MeshCollider>() != null)
            {
                MeshCollider collider = pieceObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = false;
            }
        }

        /// <summary>
        /// Union-Find over the source mesh's vertex indices: every pair of
        /// vertices on the same triangle is unioned (trivially connected),
        /// and every pair of DIFFERENT vertex indices sitting at the same
        /// position (within PositionMergeEpsilon) is also unioned - this is
        /// what correctly keeps a piece together across a UV/hard-edge seam,
        /// where the source mesh duplicates a vertex at an identical
        /// position under a second index.
        /// </summary>
        private static int[] BuildConnectivity(Mesh source, Vector3[] vertices)
        {
            var parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++)
                parent[i] = i;

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Union(parent, tris[i], tris[i + 1]);
                    Union(parent, tris[i + 1], tris[i + 2]);
                }
            }

            var byPosition = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Quantize(vertices[i]);
                if (byPosition.TryGetValue(key, out int firstAtThisPosition))
                    Union(parent, firstAtThisPosition, i);
                else
                    byPosition[key] = i;
            }

            return parent;
        }

        private static Vector3Int Quantize(Vector3 v)
        {
            float inv = 1f / PositionMergeEpsilon;
            return new Vector3Int(
                Mathf.RoundToInt(v.x * inv),
                Mathf.RoundToInt(v.y * inv),
                Mathf.RoundToInt(v.z * inv));
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]]; // path halving
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB)
                parent[rootA] = rootB;
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedMeshes"))
                AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/GeneratedMeshes", "Split");
        }
    }
}