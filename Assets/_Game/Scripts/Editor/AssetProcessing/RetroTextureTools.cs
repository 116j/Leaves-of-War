using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporary tool for applying PS1-style texture settings to prototyping
/// models.
/// 
/// Select textures or folders in the Project window and run Tools > 
/// Retro > Apply PS1 Settings To Selection.
/// </summary>
public static class RetroTextureTools
{
    const int MaxSize = 128;

    static void ApplySettings(TextureImporter importer)
    {
        importer.filterMode         = FilterMode.Point;
        importer.mipmapEnabled      = false;
        importer.maxTextureSize     = MaxSize;
        importer.npotScale          = TextureImporterNPOTScale.ToNearest;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    [MenuItem("Assets/Retro/Apply PS1 Settings To Selection")]
    static void ApplyToSelection()
    {
        var paths = CollectTexturePaths();

        if (paths.Count == 0)
        {
            Debug.LogWarning("[Retro] No textures found in the current selection.");
            return;
        }

        // Batches all the reimports into one pass at StopAssetEditing().
        AssetDatabase.StartAssetEditing();
        try
        {
            var i = 0;
            foreach (var path in paths)
            {
                EditorUtility.DisplayProgressBar(
                    "Applying PS1 settings", path, (float)i++ / paths.Count);

                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    ApplySettings(importer);
                    importer.SaveAndReimport();
                }
            }
        }
        finally
        {
            // finally, so a thrown exception can't leave the AssetDatabase
            // stuck in editing mode or the progress bar stuck on screen.
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[Retro] Applied PS1 settings to {paths.Count} texture(s).");
    }

    // Greys the menu item out when nothing is selected.
    [MenuItem("Assets/Retro/Apply PS1 Settings To Selection", true)]
    static bool ApplyToSelectionValidate() => Selection.assetGUIDs.Length > 0;

    // Accepts loose textures, folders, or a mix. HashSet de-duplicates the case
    // where a folder AND a texture inside it are both selected.
    static HashSet<string> CollectTexturePaths()
    {
        var results = new HashSet<string>();

        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var childGuid in AssetDatabase.FindAssets("t:Texture", new[] { path }))
                    results.Add(AssetDatabase.GUIDToAssetPath(childGuid));
            }
            else if (AssetImporter.GetAtPath(path) is TextureImporter)
            {
                results.Add(path);
            }
        }

        return results;
    }
}