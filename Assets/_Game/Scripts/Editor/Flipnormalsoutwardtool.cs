using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// For each selected object's mesh, flips any triangle whose face normal
    /// points TOWARD the mesh's own centroid instead of away from it - fixes
    /// inside-out/mixed-winding geometry (a common cause of a Mesh Collider
    /// that seems to work visually but behaves wrong physically: the player
    /// falls through, gets pushed the wrong way, or clips through a wall
    /// that looks solid).
    ///
    /// Works on the mesh feeding a MeshFilter (visual) or a MeshCollider
    /// (physics) - checks both, flips whichever has a mesh assigned. Applied
    /// per-triangle relative to the mesh's own local-space centroid, so it
    /// works well on roughly convex or star-shaped meshes (most level
    /// geometry, walls, simple props); it is a heuristic, not a perfect
    /// solution for highly concave/complex shapes - inspect the result.
    /// </summary>
    internal static class FlipNormalsOutwardTool
    {
        [MenuItem("Tools/Hortensia/Flip Normals To Face Outward")]
        private static void FlipNormalsToFaceOutward()
        {
            RunOnSelection((mesh, worldRotation) => FlipTrianglesOutward(mesh));
        }

        [MenuItem("Tools/Hortensia/Flip Normals To Face Up (open/walkable geometry)")]
        private static void FlipNormalsToFaceUp()
        {
            RunOnSelection((mesh, worldRotation) => FlipTrianglesUpward(mesh, worldRotation));
        }

        private static void RunOnSelection(System.Func<Mesh, Quaternion, int> flipAlgorithm)
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("Select one or more objects (or prefab assets) first.");
                return;
            }

            int totalFixed = 0;
            int totalObjects = 0;

            foreach (GameObject selected in selection)
            {
                string assetPath = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        totalFixed += ProcessHierarchy(root, flipAlgorithm, ref totalObjects);
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
                else
                {
                    totalFixed += ProcessHierarchy(selected, flipAlgorithm, ref totalObjects);
                }
            }

            Debug.Log($"Flip Normals: flipped {totalFixed} triangle(s) across {totalObjects} mesh(es).");
        }

        private static int ProcessHierarchy(GameObject root, System.Func<Mesh, Quaternion, int> flipAlgorithm, ref int totalObjects)
        {
            int flipped = 0;

            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
                flipped += ProcessMesh(meshFilter, flipAlgorithm, ref totalObjects);

            foreach (MeshCollider meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
                flipped += ProcessMesh(meshCollider, flipAlgorithm, ref totalObjects);

            return flipped;
        }

        private static int ProcessMesh(MeshFilter meshFilter, System.Func<Mesh, Quaternion, int> flipAlgorithm, ref int totalObjects)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return 0;

            Mesh original = meshFilter.sharedMesh;
            Mesh working = Object.Instantiate(original);
            working.name = original.name;

            int flippedCount = flipAlgorithm(working, meshFilter.transform.rotation);
            if (flippedCount > 0)
            {
                Undo.RecordObject(meshFilter, "Flip Normals");
                meshFilter.sharedMesh = working;
                EditorUtility.SetDirty(meshFilter);
                totalObjects++;
            }

            return flippedCount;
        }

        private static int ProcessMesh(MeshCollider meshCollider, System.Func<Mesh, Quaternion, int> flipAlgorithm, ref int totalObjects)
        {
            if (meshCollider == null || meshCollider.sharedMesh == null)
                return 0;

            Mesh original = meshCollider.sharedMesh;
            Mesh working = Object.Instantiate(original);
            working.name = original.name;

            int flippedCount = flipAlgorithm(working, meshCollider.transform.rotation);
            if (flippedCount > 0)
            {
                Undo.RecordObject(meshCollider, "Flip Normals");
                meshCollider.sharedMesh = working;
                EditorUtility.SetDirty(meshCollider);
                totalObjects++;
            }

            return flippedCount;
        }

        /// <summary>
        /// Flips any triangle whose WORLD-SPACE normal points downward
        /// (negative Y), so every face ends up pointing generally upward.
        /// For open, one-sided geometry meant to be walked on from above
        /// (terraces, steps, terrain-like shapes) where there is no true
        /// "inside" to test against - the closed-volume ray test above
        /// requires a watertight mesh and gives unreliable results on an
        /// open one like this. Steep/vertical faces (riser sides of steps)
        /// are left as their winding already has them, since "up vs down"
        /// isn't a meaningful test for a near-vertical face; only faces the
        /// player could stand on need this correction to physically hold
        /// weight.
        /// </summary>
        private static int FlipTrianglesUpward(Mesh mesh, Quaternion worldRotation)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0)
                return 0;

            int flippedCount = 0;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = mesh.GetTriangles(sub);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];

                    Vector3 localNormal = Vector3.Cross(b - a, c - a);
                    Vector3 worldNormal = worldRotation * localNormal;

                    if (worldNormal.y < 0f)
                    {
                        (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                        flippedCount++;
                    }
                }

                mesh.SetTriangles(triangles, sub);
            }

            if (flippedCount > 0)
                mesh.RecalculateNormals();

            return flippedCount;
        }

        /// <summary>
        /// For each triangle, casts a ray from just outside its own surface
        /// (along its current normal) and counts how many OTHER triangles of
        /// the same mesh it crosses. An ODD count means that starting point
        /// was actually still INSIDE the solid - i.e. the current normal
        /// points inward and needs flipping. An EVEN count means it's
        /// correctly outside already. This is the standard point-in-mesh
        /// parity test, applied per-triangle - unlike a single global
        /// centroid comparison, it works correctly on concentric/thin/
        /// non-convex shapes (e.g. a hedge maze's inner rings), where "away
        /// from the mesh's overall centre" doesn't reliably mean "outward"
        /// for every wall segment.
        ///
        /// Cost is O(triangle count squared) - fine for typical level props
        /// and even a few-thousand-triangle maze, but can take a while on a
        /// very dense mesh. This is an Editor-only one-off tool, not
        /// something run at runtime.
        /// </summary>
        private static int FlipTrianglesOutward(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0)
                return 0;

            // Gather every triangle's three positions once, across all
            // submeshes, since a ray cast from one submesh's triangle must
            // still be tested against every other submesh's geometry too
            // (they're all part of the same solid).
            var allTriangleIndices = new List<int>();
            var subMeshRanges = new List<(int start, int count)>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = mesh.GetTriangles(sub);
                subMeshRanges.Add((allTriangleIndices.Count, triangles.Length));
                allTriangleIndices.AddRange(triangles);
            }

            int triangleCount = allTriangleIndices.Count / 3;
            var centers = new Vector3[triangleCount];
            var normals = new Vector3[triangleCount];

            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 a = vertices[allTriangleIndices[t * 3]];
                Vector3 b = vertices[allTriangleIndices[t * 3 + 1]];
                Vector3 c = vertices[allTriangleIndices[t * 3 + 2]];
                centers[t] = (a + b + c) / 3f;
                normals[t] = Vector3.Cross(b - a, c - a).normalized;
            }

            float epsilon = mesh.bounds.size.magnitude * 0.0001f;
            epsilon = Mathf.Max(epsilon, 0.0001f);
            var flipFlags = new bool[triangleCount];
            int flippedCount = 0;

            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 origin = centers[t] + normals[t] * epsilon;
                int crossings = 0;

                for (int other = 0; other < triangleCount; other++)
                {
                    if (other == t)
                        continue;

                    Vector3 a = vertices[allTriangleIndices[other * 3]];
                    Vector3 b = vertices[allTriangleIndices[other * 3 + 1]];
                    Vector3 c = vertices[allTriangleIndices[other * 3 + 2]];

                    if (RayIntersectsTriangle(origin, normals[t], a, b, c))
                        crossings++;
                }

                if ((crossings % 2) != 0)
                {
                    flipFlags[t] = true;
                    flippedCount++;
                }
            }

            if (flippedCount > 0)
            {
                int cursor = 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    (int start, int count) = subMeshRanges[sub];
                    int[] triangles = mesh.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int t = cursor + i / 3;
                        if (flipFlags[t])
                            (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                    }
                    mesh.SetTriangles(triangles, sub);
                    cursor += count / 3;
                }

                mesh.RecalculateNormals();
            }

            return flippedCount;
        }

        /// <summary>
        /// Möller–Trumbore ray-triangle intersection, one-directional (only
        /// counts a hit if it's in front of the ray origin, not behind).
        /// </summary>
        private static bool RayIntersectsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
        {
            const float epsilon = 1e-7f;

            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 pVec = Vector3.Cross(direction, edge2);
            float det = Vector3.Dot(edge1, pVec);

            if (Mathf.Abs(det) < epsilon)
                return false; // Ray is parallel to this triangle.

            float invDet = 1f / det;
            Vector3 tVec = origin - a;
            float u = Vector3.Dot(tVec, pVec) * invDet;
            if (u < 0f || u > 1f)
                return false;

            Vector3 qVec = Vector3.Cross(tVec, edge1);
            float v = Vector3.Dot(direction, qVec) * invDet;
            if (v < 0f || u + v > 1f)
                return false;

            float dist = Vector3.Dot(edge2, qVec) * invDet;
            return dist > epsilon;
        }
    }
}