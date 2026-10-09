using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Three mesh-combining tools sharing one implementation:
    ///
    /// - Tools/Hortensia/Combine Selection Into Single Mesh
    ///   Select one or more objects in the Hierarchy (each can be a prefab
    ///   instance with any depth of children/grandchildren) - each selected
    ///   object's ENTIRE subtree is merged into a single combined
    ///   mesh/GameObject, replacing the original hierarchy (the selected
    ///   object itself is destroyed too). Multiple selected objects are each
    ///   combined independently into their own single model (they are NOT
    ///   merged together into one big mesh) - e.g. selecting two separate
    ///   fence prefabs produces two combined objects, one per fence.
    ///
    /// - Tools/Hortensia/Combine Similar-Named Siblings
    ///   Select several objects that share a parent (e.g. tree07 and
    ///   tree07top, sitting side by side under the same "Trees" parent) -
    ///   automatically groups them by similar base name and merges each
    ///   group into one combined mesh, so tree07 + tree07top become one
    ///   "tree07" object, tree08 + tree08top become one "tree08" object, all
    ///   from a single multi-selection. Two objects are considered
    ///   "similar" if they share the same DIRECT PARENT and their names
    ///   match up to and including the last digit found in the name. Names
    ///   with no digit only match another object with the exact same name.
    ///   Objects left alone in their group (no sibling matched) are
    ///   untouched.
    ///
    /// - Tools/Hortensia/Combine Children Of Empty Parent Into Single Mesh
    ///   Select one or more EMPTY organisational parents (e.g. an
    ///   "Environment" or "Trees" container GameObject with no mesh of its
    ///   own, just a bunch of prefab instances as children) - every direct
    ///   child (and all of ITS children/grandchildren, any depth) is merged
    ///   into one combined mesh placed back under the same parent. Unlike
    ///   the first tool above, the parent itself is kept, not destroyed -
    ///   only its children are replaced by the single combined result. A
    ///   parent that has a MeshFilter of its own is skipped with a warning
    ///   (this tool is for purely organisational, mesh-less parents; use
    ///   "Combine Selection Into Single Mesh" instead if the parent itself
    ///   also has geometry you want folded in).
    ///
    /// All three: the whole operation (every object/group combined) undoes
    /// in a single Ctrl+Z, via Undo.RegisterCreatedObjectUndo +
    /// Undo.DestroyObjectImmediate collapsed into one Undo group.
    ///
    /// Caveats worth knowing:
    /// - Source meshes must have "Read/Write Enabled" checked in their model
    ///   Import Settings, or CombineMeshes cannot read their vertex data.
    ///   Unreadable meshes are skipped with a Console warning instead of
    ///   crashing the whole operation - check the Console if some objects
    ///   are missing from the result.
    /// - Works on scene objects (Hierarchy), including prefab instances. To
    ///   combine a Prefab Asset itself rather than an instance, open it in
    ///   Prefab Mode first, or just run this on an instance in a scene.
    /// - The combined Mesh is saved as a real .asset file under
    ///   Assets/GeneratedMeshes/ so it survives play mode and builds. That
    ///   asset file is NOT removed by Ctrl+Z (Unity's Undo only covers scene
    ///   changes, not files written to disk) - if you undo a merge, the
    ///   leftover .asset is harmless but you may want to delete it by hand.
    /// </summary>
    internal static class CombineMeshesTool
    {
        private const string OutputFolder = "Assets/GeneratedMeshes";

        // --------------------------------------------------------------
        //  Tool: combine each selected object's whole subtree
        // --------------------------------------------------------------

        [MenuItem("Tools/Hortensia/Combine Selection Into Single Mesh")]
        private static void CombineSelectionIntoSingleMesh()
        {
            GameObject[] roots = Selection.gameObjects;
            if (roots.Length == 0)
            {
                Debug.LogWarning("Select one or more objects first - each selected object (with all its children/grandchildren) becomes one combined mesh.");
                return;
            }

            EnsureOutputFolder();

            Undo.SetCurrentGroupName("Combine Into Single Mesh");
            int undoGroup = Undo.GetCurrentGroup();

            int combinedCount = 0;
            foreach (GameObject root in roots)
            {
                var members = new List<GameObject> { root };
                if (CombineGroup(members, root.name + " (Combined)"))
                    combinedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Combine Into Single Mesh: combined {combinedCount} of {roots.Length} selected object(s). Press Ctrl+Z to undo the whole operation.");
        }

        // --------------------------------------------------------------
        //  Tool: combine similar-named siblings from the selection
        // --------------------------------------------------------------

        [MenuItem("Tools/Hortensia/Combine Similar-Named Siblings")]
        private static void CombineSimilarNamedSiblings()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length < 2)
            {
                Debug.LogWarning("Select two or more objects that share a parent (e.g. tree07 and tree07top) first.");
                return;
            }

            var groups = new Dictionary<(Transform parent, string key), List<GameObject>>();
            foreach (GameObject go in selected)
            {
                var groupKey = (go.transform.parent, GetBaseNameKey(go.name));
                if (!groups.TryGetValue(groupKey, out List<GameObject> list))
                {
                    list = new List<GameObject>();
                    groups[groupKey] = list;
                }
                list.Add(go);
            }

            EnsureOutputFolder();

            Undo.SetCurrentGroupName("Combine Similar-Named Siblings");
            int undoGroup = Undo.GetCurrentGroup();

            int mergedGroups = 0;
            int skippedLoners = 0;
            foreach (KeyValuePair<(Transform parent, string key), List<GameObject>> entry in groups)
            {
                List<GameObject> members = entry.Value;
                if (members.Count < 2)
                {
                    skippedLoners++;
                    continue;
                }

                // Anchor the combined object where the first sibling
                // (by original Hierarchy order) currently sits.
                members.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

                if (CombineGroup(members, entry.Key.key))
                    mergedGroups++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (mergedGroups == 0)
            {
                Debug.LogWarning(
                    "Combine Similar-Named Siblings: no matching groups found - selected objects need to share a " +
                    "parent AND a similar base name (e.g. tree07 / tree07top). Nothing changed.");
            }
            else
            {
                Debug.Log(
                    $"Combine Similar-Named Siblings: merged {mergedGroups} group(s)" +
                    (skippedLoners > 0 ? $", left {skippedLoners} object(s) untouched (no matching sibling)." : ".") +
                    " Press Ctrl+Z to undo the whole operation.");
            }
        }

        /// <summary>
        /// Strips trailing letters after the last digit in a name, so
        /// "tree07top" and "tree07" both resolve to the key "tree07". Names
        /// without any digit fall back to the full name as their own key.
        /// </summary>
        private static string GetBaseNameKey(string name)
        {
            Match match = Regex.Match(name, @"^.*\d");
            return match.Success ? match.Value : name;
        }

        // --------------------------------------------------------------
        //  Tool: combine every child of one or more empty parents
        // --------------------------------------------------------------

        [MenuItem("Tools/Hortensia/Combine Children Of Empty Parent Into Single Mesh")]
        private static void CombineChildrenOfEmptyParent()
        {
            GameObject[] parents = Selection.gameObjects;
            if (parents.Length == 0)
            {
                Debug.LogWarning("Select one or more empty parent objects first - a container with no mesh of its own, holding several prefab children.");
                return;
            }

            EnsureOutputFolder();

            Undo.SetCurrentGroupName("Combine Children Of Empty Parent");
            int undoGroup = Undo.GetCurrentGroup();

            int combinedCount = 0;
            foreach (GameObject parent in parents)
            {
                if (parent.GetComponent<MeshFilter>() != null)
                {
                    Debug.LogWarning(
                        $"'{parent.name}' skipped: it has its own MeshFilter, so it isn't an empty organisational " +
                        "parent. Use 'Combine Selection Into Single Mesh' instead if you also want the parent's " +
                        "own geometry folded into the result.");
                    continue;
                }

                var children = new List<GameObject>();
                for (int i = 0; i < parent.transform.childCount; i++)
                    children.Add(parent.transform.GetChild(i).gameObject);

                if (children.Count == 0)
                {
                    Debug.LogWarning($"'{parent.name}' skipped: it has no children to combine.");
                    continue;
                }

                // members[0] (the pivot CombineGroup anchors on) is one of
                // "parent"'s own children, so pivot.parent is "parent"
                // itself - the combined result lands back inside it
                // automatically, without needing a separate code path.
                if (CombineGroup(children, parent.name + "_Combined"))
                    combinedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Combine Children Of Empty Parent: processed {combinedCount} of {parents.Length} selected parent(s). Press Ctrl+Z to undo the whole operation.");
        }

        // --------------------------------------------------------------
        //  Shared combine logic
        // --------------------------------------------------------------

        private static void EnsureOutputFolder()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder))
                return;

            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
        }

        /// <summary>
        /// Combines every MeshFilter found in "members" and all of their
        /// children/grandchildren (recursively, any depth) into one new
        /// GameObject called "resultName", parented and positioned exactly
        /// where members[0] was. All original "members" (and therefore their
        /// whole subtrees) are destroyed afterwards.
        ///
        /// If any object carries an LODGroup (e.g. grass/foliage asset packs
        /// commonly do, with a separate mesh per LOD level), only LOD0 - the
        /// highest-detail level - is included; LOD1 and beyond are skipped.
        /// Without this, combining would merge all LOD levels together,
        /// stacking 3-4x redundant overlapping geometry at different detail
        /// levels into one mesh instead of just the one you'd actually see
        /// up close.
        ///
        /// Returns true if a combined object was created, false if there was
        /// nothing combinable in the selection (already logs a warning in
        /// that case, so callers don't need to log again).
        /// </summary>
        private static bool CombineGroup(List<GameObject> members, string resultName)
        {
            if (members == null || members.Count == 0)
                return false;

            Transform pivot = members[0].transform;

            var skipRenderers = new HashSet<Renderer>();
            foreach (GameObject member in members)
                CollectNonPrimaryLodRenderers(member, skipRenderers);

            var parts = new List<(MeshFilter mf, Renderer renderer)>();
            foreach (GameObject member in members)
            {
                foreach (MeshFilter mf in member.GetComponentsInChildren<MeshFilter>(true))
                {
                    Renderer renderer = mf.GetComponent<Renderer>();
                    if (renderer == null || mf.sharedMesh == null)
                        continue;
                    if (skipRenderers.Contains(renderer))
                        continue;

                    parts.Add((mf, renderer));
                }
            }

            if (parts.Count == 0)
            {
                Debug.LogWarning($"'{resultName}': no combinable MeshFilters found (with a matching Renderer and an assigned mesh) - skipped.");
                return false;
            }

            Mesh combinedMesh = CombineWithMaterials(parts, pivot, out Material[] materials);
            if (combinedMesh == null)
            {
                Debug.LogWarning($"'{resultName}': every candidate mesh was unreadable (Read/Write disabled) - nothing to combine.");
                return false;
            }

            combinedMesh.name = resultName + "_Mesh";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{combinedMesh.name}.asset");
            AssetDatabase.CreateAsset(combinedMesh, assetPath);
            AssetDatabase.SaveAssets();

            var combinedObject = new GameObject(resultName);
            Undo.RegisterCreatedObjectUndo(combinedObject, "Combine Meshes");
            combinedObject.transform.SetParent(pivot.parent, false);
            combinedObject.transform.localPosition = pivot.localPosition;
            combinedObject.transform.localRotation = pivot.localRotation;
            combinedObject.transform.localScale = pivot.localScale;

            MeshFilter combinedFilter = combinedObject.AddComponent<MeshFilter>();
            combinedFilter.sharedMesh = combinedMesh;
            MeshRenderer combinedRenderer = combinedObject.AddComponent<MeshRenderer>();
            combinedRenderer.sharedMaterials = materials;

            foreach (GameObject member in members)
                Undo.DestroyObjectImmediate(member);

            return true;
        }

        /// <summary>
        /// Finds every LODGroup under "root" and adds all renderers from LOD
        /// levels 1 and beyond (i.e. everything except LOD0) to "skip" - so
        /// the caller can exclude them and keep only the highest-detail
        /// level per LODGroup.
        /// </summary>
        private static void CollectNonPrimaryLodRenderers(GameObject root, HashSet<Renderer> skip)
        {
            foreach (LODGroup lodGroup in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lodGroup.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                {
                    foreach (Renderer renderer in lods[i].renderers)
                    {
                        if (renderer != null)
                            skip.Add(renderer);
                    }
                }
            }
        }

        /// <summary>
        /// Group every submesh by its material, baking each vertex into
        /// pivot's local space so the final mesh lines up correctly once
        /// placed on an object that shares pivot's transform. Runs in two
        /// passes so multiple materials survive the merge: submeshes that
        /// share a material are merged into one submesh each, then those
        /// per-material submeshes are stitched into a single Mesh with one
        /// submesh per material.
        /// </summary>
        private static Mesh CombineWithMaterials(
            List<(MeshFilter mf, Renderer renderer)> parts,
            Transform pivot,
            out Material[] materials)
        {
            var materialOrder = new List<Material>();
            var groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 pivotWorldToLocal = pivot.worldToLocalMatrix;

            foreach ((MeshFilter mf, Renderer renderer) in parts)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null)
                    continue;

                if (!mesh.isReadable)
                {
                    Debug.LogWarning(
                        $"Skipping '{mf.name}': mesh '{mesh.name}' is not Read/Write Enabled. " +
                        "Select it, open its model Import Settings, tick Read/Write Enabled, Apply, then re-run.");
                    continue;
                }

                Material[] rendererMaterials = renderer.sharedMaterials;
                int subMeshCount = mesh.subMeshCount;
                Matrix4x4 localToPivot = pivotWorldToLocal * mf.transform.localToWorldMatrix;

                for (int sub = 0; sub < subMeshCount; sub++)
                {
                    Material material = sub < rendererMaterials.Length
                        ? rendererMaterials[sub]
                        : rendererMaterials.Length > 0 ? rendererMaterials[rendererMaterials.Length - 1] : null;

                    if (material == null)
                        continue;

                    if (!groups.TryGetValue(material, out List<CombineInstance> list))
                    {
                        list = new List<CombineInstance>();
                        groups[material] = list;
                        materialOrder.Add(material);
                    }

                    list.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = sub,
                        transform = localToPivot,
                    });
                }
            }

            if (materialOrder.Count == 0)
            {
                materials = System.Array.Empty<Material>();
                return null;
            }

            // Pass 1: merge every submesh that shares a material into one
            // submesh per material.
            var perMaterialCombines = new List<CombineInstance>(materialOrder.Count);
            foreach (Material material in materialOrder)
            {
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(groups[material].ToArray(), mergeSubMeshes: true, useMatrices: true);
                perMaterialCombines.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = Matrix4x4.identity });
            }

            // Pass 2: stitch the per-material meshes together, keeping them
            // as separate submeshes so each still renders with its own
            // material afterwards.
            var finalMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            finalMesh.CombineMeshes(perMaterialCombines.ToArray(), mergeSubMeshes: false, useMatrices: false);

            materials = materialOrder.ToArray();
            return finalMesh;
        }
    }
}