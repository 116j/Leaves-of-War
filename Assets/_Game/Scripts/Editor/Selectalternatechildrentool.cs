using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Two ways to use this, both under Tools/Hortensia/Select Every Other
    /// Child, in either "Starting From 1st (Odd)" (indices 0, 2, 4...) or
    /// "Starting From 2nd (Even)" (indices 1, 3, 5...) flavour:
    ///
    /// - Select a SINGLE parent object, then run this: selects every other
    ///   DIRECT CHILD of that parent. Handy for halving the density of
    ///   something like placed grass instances before deleting the
    ///   selection.
    ///
    /// - Select SEVERAL objects directly (e.g. Ctrl-click a bunch of
    ///   siblings by hand), then run this: alternates within THAT selection
    ///   itself instead of looking at anyone's children - so you get every
    ///   other one of the objects you actually picked.
    /// </summary>
    internal static class SelectAlternateChildrenTool
    {
        [MenuItem("Tools/Hortensia/Select Every Other Child/Starting From 1st (Odd)")]
        private static void SelectStartingFromFirst() => SelectEveryOther(startIndex: 0);

        [MenuItem("Tools/Hortensia/Select Every Other Child/Starting From 2nd (Even)")]
        private static void SelectStartingFromSecond() => SelectEveryOther(startIndex: 1);

        /// <summary>
        /// Core logic, generalised over the starting offset (0-based) so both
        /// menu entries above share the same implementation, and over
        /// whether the selection is a single "parent" or a flat multi-pick.
        /// </summary>
        private static void SelectEveryOther(int startIndex)
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("Select a parent object (to alternate its children) or several objects directly (to alternate the selection itself) first.");
                return;
            }

            List<Object> picked;
            string sourceDescription;

            if (selected.Length == 1)
            {
                // Single object selected: treat it as a parent, alternate
                // its direct children - the original behaviour.
                Transform parentTransform = selected[0].transform;
                picked = new List<Object>();
                for (int i = startIndex; i < parentTransform.childCount; i += 2)
                    picked.Add(parentTransform.GetChild(i).gameObject);

                sourceDescription = $"children of '{selected[0].name}'";
            }
            else
            {
                // Multiple objects selected directly: the current selection
                // IS the list - alternate within it instead of looking at
                // anyone's children. Ordered by sibling index first so the
                // result is predictable (spatial/hierarchy order) rather
                // than dependent on click order.
                var ordered = new List<GameObject>(selected);
                ordered.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

                picked = new List<Object>();
                for (int i = startIndex; i < ordered.Count; i += 2)
                    picked.Add(ordered[i]);

                sourceDescription = "the selected objects";
            }

            if (picked.Count == 0)
            {
                Debug.LogWarning($"Nothing found at that offset among {sourceDescription}.");
                return;
            }

            Selection.objects = picked.ToArray();
            string ordinal = startIndex == 0 ? "1st" : "2nd";
            Debug.Log($"Select Every Other Child: selected {picked.Count} of {sourceDescription} (every 2nd, starting with the {ordinal}).");
        }
    }
}