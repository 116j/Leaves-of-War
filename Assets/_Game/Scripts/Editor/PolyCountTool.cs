using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Tools/Hortensia/Poly Count Tool
    ///
    /// A standalone window (not tied to CombineMeshesTool) that:
    /// - Shows the live vertex/triangle count of whatever is currently
    ///   selected in the Hierarchy (summed across every MeshFilter found in
    ///   the selection and all of its children/grandchildren).
    /// - Offers a "Simplification" slider (0-100%) with a live preview of
    ///   the resulting vertex/triangle count, before you commit to anything.
    /// - "Apply" bakes the simplification into each selected mesh (as new
    ///   .asset files under Assets/GeneratedMeshes/Simplified/, originals
    ///   untouched on disk), with full Ctrl+Z support.
    /// - "Restore Original Mesh(es)" reverts any meshes this window has
    ///   simplified back to what they were, for as long as the Editor
    ///   session stays open (the original reference is cached in memory,
    ///   not saved to disk - closing/reopening Unity loses that cache, but
    ///   Ctrl+Z always still works regardless).
    ///
    /// HOW THE SIMPLIFICATION WORKS - vertex clustering:
    /// Unity has no built-in "real" mesh decimation (the kind that
    /// intelligently collapses edges while preserving shape, like
    /// dedicated tools/packages do). This window instead buckets nearby
    /// vertices into a 3D grid and merges every vertex that lands in the
    /// same cell into one, then rebuilds triangles against the merged set,
    /// dropping any triangle that collapsed to zero area. It is fast, has
    /// no external dependencies, and - usefully for this project - tends to
    /// produce a blocky, faceted look that fits the PS1 aesthetic rather
    /// than fighting it. It is NOT as shape-preserving as a proper
    /// edge-collapse decimator on organic/detailed meshes; for very
    /// important hero assets, treat the result as a starting point to
    /// inspect, not a guaranteed-good automatic LOD.
    ///
    /// Caveats shared with CombineMeshesTool:
    /// - Source meshes need "Read/Write Enabled" in their Import Settings to
    ///   be readable here at all; unreadable ones are skipped with a
    ///   warning.
    /// - Multiple submeshes/materials are preserved (each submesh's
    ///   triangles are rebuilt separately against the same shared vertex
    ///   merge), so multi-material objects don't lose any material slots.
    /// </summary>
    internal sealed class PolyCountToolWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/GeneratedMeshes/Simplified";

        // How much of the mesh's bounding-box diagonal the clustering grid
        // cell size can reach at 100% on the slider. Empirically chosen -
        // 12% of the diagonal is already fairly aggressive for most props;
        // tweak this constant directly in code if you want the slider's
        // top end to hit harder or softer across the board.
        private const float MaxCellSizeFraction = 0.12f;

        private static readonly Dictionary<MeshFilter, Mesh> OriginalMeshes = new Dictionary<MeshFilter, Mesh>();

        [Range(0f, 100f)] private float reduction;
        private int previewVertexCount;
        private int previewTriangleCount;
        private bool previewDirty = true;
        private Vector2 scroll;

        [MenuItem("Tools/Hortensia/Poly Count Tool")]
        private static void Open()
        {
            GetWindow<PolyCountToolWindow>("Poly Count");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            previewDirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            List<MeshFilter> filters = GatherSelectedMeshFilters();

            if (filters.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Select one or more objects in the Hierarchy to see their polygon/vertex count.",
                    MessageType.Info);
                return;
            }

            (int currentVerts, int currentTris) = CountMeshes(filters);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Selected mesh count", filters.Count.ToString());
            EditorGUILayout.LabelField("Current vertices", currentVerts.ToString("N0"));
            EditorGUILayout.LabelField("Current triangles", currentTris.ToString("N0"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simplification", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            reduction = EditorGUILayout.Slider("Amount", reduction, 0f, 100f);
            if (EditorGUI.EndChangeCheck())
                previewDirty = true;

            if (previewDirty)
            {
                (previewVertexCount, previewTriangleCount) = PreviewSimplification(filters, reduction);
                previewDirty = false;
            }

            EditorGUILayout.LabelField("Preview vertices", previewVertexCount.ToString("N0"));
            EditorGUILayout.LabelField("Preview triangles", previewTriangleCount.ToString("N0"));

            if (reduction > 0.01f && currentVerts > 0)
            {
                float vertReduction = 100f * (1f - (previewVertexCount / (float)currentVerts));
                EditorGUILayout.LabelField("Vertex reduction", $"{vertReduction:F0}%");
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply To Selected Mesh(es)"))
                    ApplySimplification(filters, reduction);

                if (GUILayout.Button("Restore Original Mesh(es)"))
                    RestoreOriginals(filters);
            }

            EditorGUILayout.HelpBox(
                "Restore only works for meshes this window has simplified in the current Editor " +
                "session. Ctrl+Z also undoes an Apply/Restore at any time.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private static List<MeshFilter> GatherSelectedMeshFilters()
        {
            var filters = new List<MeshFilter>();
            foreach (GameObject go in Selection.gameObjects)
                filters.AddRange(go.GetComponentsInChildren<MeshFilter>(true));
            return filters;
        }

        private static (int vertices, int triangles) CountMeshes(List<MeshFilter> filters)
        {
            int vertices = 0;
            int triangles = 0;
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null)
                    continue;
                vertices += mf.sharedMesh.vertexCount;
                triangles += SumSubMeshTriangles(mf.sharedMesh);
            }
            return (vertices, triangles);
        }

        private static int SumSubMeshTriangles(Mesh mesh)
        {
            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                total += (int)mesh.GetIndexCount(i) / 3;
            return total;
        }

        /// <summary>
        /// Runs the same clustering pass as ApplySimplification would, but
        /// only to report resulting counts - does not create any assets or
        /// touch any MeshFilter. Skips unreadable meshes silently here (no
        /// console spam while the user is just dragging the slider); Apply
        /// still warns properly about those.
        /// </summary>
        private static (int vertices, int triangles) PreviewSimplification(List<MeshFilter> filters, float reductionPercent)
        {
            if (reductionPercent <= 0.01f)
                return CountMeshes(filters);

            int totalVerts = 0;
            int totalTris = 0;
            foreach (MeshFilter mf in filters)
            {
                Mesh source = mf.sharedMesh;
                if (source == null || !source.isReadable)
                    continue;

                float cellSize = ComputeCellSize(source, reductionPercent);
                (int verts, int tris) = ClusterCount(source, cellSize);
                totalVerts += verts;
                totalTris += tris;
            }
            return (totalVerts, totalTris);
        }

        private static float ComputeCellSize(Mesh mesh, float reductionPercent)
        {
            float diagonal = mesh.bounds.size.magnitude;
            return Mathf.Max(diagonal * Mathf.Lerp(0f, MaxCellSizeFraction, reductionPercent / 100f), 0.0001f);
        }

        private static void ApplySimplification(List<MeshFilter> filters, float reductionPercent)
        {
            EnsureOutputFolder();

            Undo.SetCurrentGroupName("Simplify Mesh(es)");
            int undoGroup = Undo.GetCurrentGroup();

            int appliedCount = 0;
            foreach (MeshFilter mf in filters)
            {
                Mesh current = mf.sharedMesh;
                if (current == null)
                    continue;

                if (!OriginalMeshes.TryGetValue(mf, out Mesh original))
                {
                    original = current;
                    OriginalMeshes[mf] = original;
                }

                if (reductionPercent <= 0.01f)
                {
                    Undo.RecordObject(mf, "Simplify Mesh");
                    mf.sharedMesh = original;
                    appliedCount++;
                    continue;
                }

                if (!original.isReadable)
                {
                    Debug.LogWarning(
                        $"Skipping '{mf.name}': mesh '{original.name}' is not Read/Write Enabled. " +
                        "Select it, open its model Import Settings, tick Read/Write Enabled, Apply, then re-run.");
                    continue;
                }

                float cellSize = ComputeCellSize(original, reductionPercent);
                Mesh simplified = ClusterSimplify(original, cellSize);
                simplified.name = original.name + "_Simplified";

                string path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{simplified.name}.asset");
                AssetDatabase.CreateAsset(simplified, path);
                AssetDatabase.SaveAssets();

                Undo.RecordObject(mf, "Simplify Mesh");
                mf.sharedMesh = simplified;
                appliedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Poly Count Tool: simplified {appliedCount} of {filters.Count} mesh(es) at {reductionPercent:F0}%. Press Ctrl+Z to undo.");
        }

        private static void RestoreOriginals(List<MeshFilter> filters)
        {
            Undo.SetCurrentGroupName("Restore Original Mesh(es)");
            int undoGroup = Undo.GetCurrentGroup();

            int restoredCount = 0;
            foreach (MeshFilter mf in filters)
            {
                if (!OriginalMeshes.TryGetValue(mf, out Mesh original) || original == null)
                    continue;

                Undo.RecordObject(mf, "Restore Original Mesh");
                mf.sharedMesh = original;
                restoredCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (restoredCount == 0)
                Debug.LogWarning("Poly Count Tool: nothing to restore - none of the selected meshes were simplified by this window this session.");
            else
                Debug.Log($"Poly Count Tool: restored {restoredCount} mesh(es) to their original. Press Ctrl+Z to undo.");
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedMeshes"))
                AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/GeneratedMeshes", "Simplified");
        }

        // --------------------------------------------------------------
        //  Vertex clustering
        // --------------------------------------------------------------

        /// <summary>
        /// Builds the vertex remap table (which original vertex index maps
        /// to which merged/clustered index) for a given grid cell size.
        /// Shared by both the cheap count-only preview and the real Apply.
        /// </summary>
        private static int[] BuildClusterRemap(Vector3[] vertices, float cellSize, out int clusterCount)
        {
            var clusterMap = new Dictionary<Vector3Int, int>();
            var remap = new int[vertices.Length];
            int next = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                var cell = new Vector3Int(
                    Mathf.RoundToInt(v.x / cellSize),
                    Mathf.RoundToInt(v.y / cellSize),
                    Mathf.RoundToInt(v.z / cellSize));

                if (!clusterMap.TryGetValue(cell, out int index))
                {
                    index = next++;
                    clusterMap[cell] = index;
                }
                remap[i] = index;
            }

            clusterCount = next;
            return remap;
        }

        /// <summary>Counts what the result WOULD be, without building any mesh/geometry - used for the live preview.</summary>
        private static (int vertices, int triangles) ClusterCount(Mesh source, float cellSize)
        {
            Vector3[] vertices = source.vertices;
            int[] remap = BuildClusterRemap(vertices, cellSize, out int clusterCount);

            int triangleCount = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = remap[tris[i]];
                    int b = remap[tris[i + 1]];
                    int c = remap[tris[i + 2]];
                    if (a != b && b != c && a != c)
                        triangleCount++;
                }
            }

            return (clusterCount, triangleCount);
        }

        /// <summary>Builds the actual simplified Mesh, preserving submeshes/materials, normals and UV0.</summary>
        private static Mesh ClusterSimplify(Mesh source, float cellSize)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasUVs = uvs != null && uvs.Length == vertices.Length;

            int[] remap = BuildClusterRemap(vertices, cellSize, out int clusterCount);

            var newVertices = new Vector3[clusterCount];
            var newNormals = hasNormals ? new Vector3[clusterCount] : null;
            var newUVs = hasUVs ? new Vector2[clusterCount] : null;
            var written = new bool[clusterCount];

            for (int i = 0; i < vertices.Length; i++)
            {
                int index = remap[i];
                if (written[index])
                    continue;

                written[index] = true;
                newVertices[index] = vertices[i];
                if (hasNormals) newNormals[index] = normals[i];
                if (hasUVs) newUVs[index] = uvs[i];
            }

            var result = new Mesh
            {
                indexFormat = clusterCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            result.SetVertices(newVertices);
            if (hasNormals) result.SetNormals(newNormals);
            if (hasUVs) result.SetUVs(0, newUVs);

            result.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] srcTris = source.GetTriangles(sub);
                var newTris = new List<int>(srcTris.Length);
                for (int i = 0; i < srcTris.Length; i += 3)
                {
                    int a = remap[srcTris[i]];
                    int b = remap[srcTris[i + 1]];
                    int c = remap[srcTris[i + 2]];
                    if (a == b || b == c || a == c)
                        continue;
                    newTris.Add(a);
                    newTris.Add(b);
                    newTris.Add(c);
                }
                result.SetTriangles(newTris, sub);
            }

            if (!hasNormals)
                result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }
    }
}