using UnityEditor;
using UnityEngine;

namespace Hortensia.Editor
{
    /// <summary>
    /// Batch collider tools for prefab/GameObject hierarchies.
    ///
    /// - "Add Missing Colliders" only touches meshes that have NO collider yet.
    /// - "Recalibrate All Colliders To MeshCollider" replaces EVERY existing
    ///   collider (whatever type) with a MeshCollider matching the current
    ///   mesh - use this to fix objects that got the wrong collider type
    ///   earlier (e.g. a BoxCollider that filled in a gap between two shapes
    ///   combined into one mesh).
    ///
    /// Usage: select a prefab asset in the Project window (or a GameObject in
    /// the scene/Hierarchy), then use either command under Tools > Hortensia.
    /// </summary>
    internal static class AddMissingCollidersTool
    {
        [MenuItem("Tools/Hortensia/Create Stair Ramp Collider Between Two Points")]
        private static void CreateStairRampColliderBetweenTwoPoints()
        {
            Transform[] selection = Selection.transforms;
            if (selection.Length != 2)
            {
                Debug.LogWarning(
                    "Select EXACTLY two empty GameObjects first: one placed at the bottom of the " +
                    "stairs, one at the top (create them via GameObject > Create Empty, then drag " +
                    "each into position in the Scene view). Use this instead of the bounds-based " +
                    "version when the stairs are part of a single mesh that also includes other " +
                    "unrelated geometry, where the whole object's bounds wouldn't represent just " +
                    "the stairs.");
                return;
            }

            Transform a = selection[0];
            Transform b = selection[1];
            // Sort so 'bottom' is always the lower one, regardless of selection order.
            Transform bottom = a.position.y <= b.position.y ? a : b;
            Transform top = bottom == a ? b : a;

            Vector3 delta = top.position - bottom.position;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                Debug.LogWarning("The two selected points are too close together to build a meaningful ramp.");
                return;
            }

            const float defaultWidth = 2f;
            const float thickness = 0.15f;
            Vector3 midpoint = (bottom.position + top.position) * 0.5f;

            var rampObject = new GameObject("Stair Ramp Collider", typeof(BoxCollider));
            Undo.RegisterCreatedObjectUndo(rampObject, "Create Stair Ramp Collider Between Two Points");
            rampObject.transform.position = midpoint;
            float slopeAngleDegrees = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            rampObject.transform.rotation = Quaternion.Euler(-slopeAngleDegrees, yaw, 0f);

            BoxCollider box = rampObject.GetComponent<BoxCollider>();
            box.size = new Vector3(defaultWidth, thickness, length);

            Debug.Log(
                $"Create Stair Ramp Collider Between Two Points: created '{rampObject.name}' at {midpoint}, " +
                $"length={length:F2}m. Width defaulted to {defaultWidth}m - resize its Box Collider's " +
                "Size.x by hand to match how wide the actual stairs are.");
        }

        [MenuItem("Tools/Hortensia/Create Stair Ramp Collider")]
        private static void CreateStairRampCollider()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("Select one or more staircase objects first.");
                return;
            }

            int createdCount = 0;

            foreach (GameObject selected in selection)
            {
                if (TryCreateRampForObject(selected))
                    createdCount++;
            }

            Debug.Log($"Create Stair Ramp Collider: added a ramp to {createdCount} of {selection.Length} selected object(s).");
        }

        /// <summary>
        /// Builds a single smooth, invisible, sloped BoxCollider spanning a
        /// staircase's own local bounds - a CharacterController can always
        /// walk up a smooth ramp, no matter how tall the individual visible
        /// steps are (CharacterController.stepOffset only helps for steps
        /// shorter than that value; a real riser taller than it blocks the
        /// player outright). The ramp sits invisibly alongside the stepped
        /// visual mesh and its own (unchanged) MeshCollider - it doesn't
        /// replace anything, just adds a walkable surface underneath.
        ///
        /// Assumes the standard authoring convention for this project's
        /// stair assets: rise along local Y, run along local +Z, width along
        /// local X. If a specific staircase climbs the "wrong" way, rotate
        /// the created "Stair Ramp Collider" child by 180 degrees around Y
        /// afterward - everything else about it stays correct.
        /// </summary>
        private static bool TryCreateRampForObject(GameObject stairs)
        {
            Bounds localBounds;
            if (stairs.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                localBounds = meshFilter.sharedMesh.bounds;
            }
            else if (stairs.TryGetComponent(out MeshCollider meshCollider) && meshCollider.sharedMesh != null)
            {
                localBounds = meshCollider.sharedMesh.bounds;
            }
            else
            {
                Debug.LogWarning($"'{stairs.name}' has no MeshFilter/MeshCollider with a mesh - skipped.");
                return false;
            }

            float rise = localBounds.size.y;
            float run = localBounds.size.z;
            float width = localBounds.size.x;

            if (rise < 0.01f || run < 0.01f)
            {
                Debug.LogWarning($"'{stairs.name}' has too little rise/run in its bounds to build a ramp - skipped.");
                return false;
            }

            float slopeAngleDegrees = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float diagonalLength = Mathf.Sqrt(rise * rise + run * run);
            const float thickness = 0.15f;

            var rampObject = new GameObject("Stair Ramp Collider", typeof(BoxCollider));
            Undo.RegisterCreatedObjectUndo(rampObject, "Create Stair Ramp Collider");
            rampObject.transform.SetParent(stairs.transform, false);
            rampObject.transform.localPosition = localBounds.center;
            // Tilts the box so its length axis follows the stairs' overall
            // slope instead of running flat - the box's own top face becomes
            // the smooth ramp surface the player actually walks on.
            rampObject.transform.localRotation = Quaternion.Euler(-slopeAngleDegrees, 0f, 0f);

            BoxCollider box = rampObject.GetComponent<BoxCollider>();
            box.size = new Vector3(width, thickness, diagonalLength);

            return true;
        }

        [MenuItem("Tools/Hortensia/Add Missing Colliders To Selection")]
        private static void AddMissingColliders()
        {
            RunTool("Add Missing Colliders", AddIfMissing);
        }

        [MenuItem("Tools/Hortensia/Recalibrate All Colliders To MeshCollider")]
        private static void RecalibrateAllColliders()
        {
            RunTool("Recalibrate Colliders To MeshCollider", ForceMeshCollider);
        }

        [MenuItem("Tools/Hortensia/Remove All Colliders From Selection")]
        private static void RemoveAllColliders()
        {
            RunColliderRemovalTool();
        }

        /// <summary>
        /// Removes every Collider component found in the selection and its
        /// children - unlike the other two tools, this walks Colliders
        /// directly rather than MeshFilters, since a stray Collider can exist
        /// without a mesh (e.g. a manually added BoxCollider).
        /// </summary>
        private static void RunColliderRemovalTool()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("Select a prefab asset or GameObject first.");
                return;
            }

            int totalRemoved = 0;

            foreach (GameObject selected in selection)
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
                bool isPrefabAsset = string.IsNullOrEmpty(assetPath) &&
                    PrefabUtility.IsPartOfPrefabAsset(selected);
                if (isPrefabAsset)
                    assetPath = AssetDatabase.GetAssetPath(selected);

                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        totalRemoved += RemoveCollidersInHierarchy(root);
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
                else
                {
                    Undo.RegisterFullObjectHierarchyUndo(selected, "Remove All Colliders");
                    totalRemoved += RemoveCollidersInHierarchy(selected);
                }
            }

            Debug.Log($"Remove All Colliders: removed {totalRemoved}.");
        }

        private static int RemoveCollidersInHierarchy(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            int count = colliders.Length;
            foreach (Collider collider in colliders)
                Object.DestroyImmediate(collider, true);
            return count;
        }

        private static void RunTool(string undoLabel, System.Func<MeshFilter, bool> perMeshAction)
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("Select a prefab asset or GameObject first.");
                return;
            }

            int totalAffected = 0;
            int totalSkipped = 0;

            foreach (GameObject selected in selection)
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
                bool isPrefabAsset = string.IsNullOrEmpty(assetPath) &&
                    PrefabUtility.IsPartOfPrefabAsset(selected);
                if (isPrefabAsset)
                    assetPath = AssetDatabase.GetAssetPath(selected);

                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                {
                    // Editing a prefab ASSET: open its contents in isolation,
                    // modify, save, and unload - this persists the change on
                    // the prefab file itself, not just one scene instance.
                    GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        ProcessHierarchy(root, perMeshAction, ref totalAffected, ref totalSkipped);
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
                else
                {
                    // A plain scene GameObject (or a prefab instance already
                    // placed in a scene): modify it directly.
                    Undo.RegisterFullObjectHierarchyUndo(selected, undoLabel);
                    ProcessHierarchy(selected, perMeshAction, ref totalAffected, ref totalSkipped);
                }
            }

            Debug.Log($"{undoLabel}: affected {totalAffected}, skipped {totalSkipped}.");
        }

        private static void ProcessHierarchy(
            GameObject root,
            System.Func<MeshFilter, bool> perMeshAction,
            ref int affected,
            ref int skipped)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (perMeshAction(meshFilter))
                    affected++;
                else
                    skipped++;
            }
        }

        /// <summary>Adds a MeshCollider only if this object has no collider yet.</summary>
        private static bool AddIfMissing(MeshFilter meshFilter)
        {
            GameObject target = meshFilter.gameObject;
            if (target.GetComponent<Collider>() != null)
                return false;
            if (meshFilter.sharedMesh == null)
                return false;

            // MeshCollider (non-convex) follows the mesh's actual shape,
            // including gaps between separate pieces combined into one mesh -
            // a BoxCollider is always a solid box covering the full bounds
            // and would fill in any such gap.
            MeshCollider collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = false;
            return true;
        }

        /// <summary>
        /// Removes ANY existing collider(s) on this object and replaces them
        /// with a single MeshCollider matching the current mesh - use to fix
        /// objects that previously got the wrong collider type (e.g. from
        /// "Add Missing Colliders" adding a BoxCollider that filled a gap).
        /// </summary>
        private static bool ForceMeshCollider(MeshFilter meshFilter)
        {
            if (meshFilter.sharedMesh == null)
                return false;

            GameObject target = meshFilter.gameObject;
            Collider[] existing = target.GetComponents<Collider>();
            foreach (Collider collider in existing)
                Object.DestroyImmediate(collider, true);

            MeshCollider meshCollider = target.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            return true;
        }
    }
}