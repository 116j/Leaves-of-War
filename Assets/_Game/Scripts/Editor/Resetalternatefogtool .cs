using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// PS1Fog.ApplyAlternateFogMaterials() only ever sets a material's
    /// "_UseAlternateFog" toggle TO 1 (for materials in its list) - it never
    /// resets it back to 0 for anything else. A material that was ever set
    /// to 1 (even once, even if later removed from that list) stays stuck
    /// that way, and if it's STILL in the list, PS1Fog forces it back to 1
    /// every frame, making a manual uncheck in the Inspector pointless.
    ///
    /// This tool finds every Material asset in the project using
    /// Hortensia/Retro Lit and forces "_UseAlternateFog" back to 0 - a clean
    /// slate before deciding again which materials should actually have it.
    /// </summary>
    internal static class ResetAlternateFogTool
    {
        private const string PropertyName = "_UseAlternateFog";

        [MenuItem("Tools/Hortensia/Reset Alternate Fog On All Materials")]
        private static void ResetAlternateFogOnAllMaterials()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            int resetCount = 0;
            int skippedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty(PropertyName))
                {
                    skippedCount++;
                    continue;
                }

                float current = material.GetFloat(PropertyName);
                if (current == 0f)
                    continue;

                Undo.RecordObject(material, "Reset Alternate Fog");
                material.SetFloat(PropertyName, 0f);
                EditorUtility.SetDirty(material);
                resetCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Reset Alternate Fog On All Materials: reset {resetCount} material(s) " +
                $"(scanned {guids.Length}, {skippedCount} had no {PropertyName} property).");
        }
    }
}