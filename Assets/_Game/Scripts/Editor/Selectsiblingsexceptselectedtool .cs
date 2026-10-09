using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Select one (or more) objects, then run this to select every OTHER
    /// direct child of their parent instead - the originally selected
    /// object(s) end up deselected. Handy for isolating "everything except
    /// this one part" of a prefab or group (e.g. every face of a shape
    /// except the one you just finished editing).
    /// </summary>
    internal static class SelectSiblingsExceptSelectedTool
    {
        [MenuItem("Tools/Hortensia/Select Siblings Except Selected")]
        private static void SelectSiblingsExceptSelected()
        {
            GameObject[] originallySelected = Selection.gameObjects;
            if (originallySelected.Length == 0)
            {
                Debug.LogWarning("Select one or more objects first - this selects their OTHER siblings instead.");
                return;
            }

            var excluded = new HashSet<GameObject>(originallySelected);
            var picked = new List<Object>();
            var visitedParents = new HashSet<Transform>();

            foreach (GameObject selected in originallySelected)
            {
                Transform parent = selected.transform.parent;
                if (parent == null || !visitedParents.Add(parent))
                    continue; // No parent to pick siblings from, or already handled this parent.

                for (int i = 0; i < parent.childCount; i++)
                {
                    GameObject sibling = parent.GetChild(i).gameObject;
                    if (!excluded.Contains(sibling))
                        picked.Add(sibling);
                }
            }

            if (picked.Count == 0)
            {
                Debug.LogWarning("No other siblings found (selected object(s) may have no parent, or be the only child).");
                return;
            }

            Selection.objects = picked.ToArray();
            Debug.Log($"Select Siblings Except Selected: selected {picked.Count} sibling(s), excluding the {originallySelected.Length} originally selected.");
        }
    }
}