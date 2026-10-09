using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;

namespace Hortensia.Editor.AssetProcessing
{
    /// <summary>
    /// Keeps serialized resolution-dependent assets aligned with RetroResolution.
    /// Runs after script reloads, so changing the constants is sufficient.
    /// </summary>
    [InitializeOnLoad]
    internal static class RetroResolutionAssetSynchronizer
    {
        private const string WorldTargetPath = "Assets/Art/PS1_World.renderTexture";

        static RetroResolutionAssetSynchronizer()
        {
            EditorApplication.delayCall += Synchronize;
        }

        [MenuItem("Assets/Retro/Synchronize Resolution Assets")]
        public static void Synchronize()
        {
            RenderTexture target = AssetDatabase.LoadAssetAtPath<RenderTexture>(WorldTargetPath);
            if (target == null)
            {
                Debug.LogError($"World render texture is missing at {WorldTargetPath}.");
                return;
            }

            Vector2Int worldSize = RetroResolution.WorldSize;
            bool changed = false;

            if (target.width != worldSize.x || target.height != worldSize.y)
            {
                target.Release();
                target.width = worldSize.x;
                target.height = worldSize.y;
                changed = true;
            }

            if (target.filterMode != FilterMode.Point)
            {
                target.filterMode = FilterMode.Point;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                Debug.Log(
                    $"Synchronized {WorldTargetPath} to " +
                    $"{RetroResolution.Format(worldSize)} with point filtering.");
            }
        }
    }
}
