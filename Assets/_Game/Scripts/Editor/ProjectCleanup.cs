using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProjectCleanup
{
    private const string MakeHuman = "Assets/_Game/Art/Characters/MakeHuman";
    private const string SharedTextures = MakeHuman + "/Textures";
    private const string UiFolder = "Assets/_Game/Art/2D/UI";

    private static readonly (string from, string to)[] Keep =
    {
        ("Assets/Book-Page Curl/Example/sprites/TransparentGraybackgtound.png", UiFolder + "/TransparentGraybackgtound.png"),
        ("Assets/ChocDino/UIFX/Demos/Scripts/Common/diisovle texture.png", UiFolder + "/dissolve texture.png"),
    };

    private static readonly string[] Remove =
    {
        "Assets/Book-Page Curl/Example",
        "Assets/ChocDino/UIFX/Demos",
        "Assets/_Trash",
        "Assets/_Game/Audio/Import/Apollinaire.wav",
    };

    [MenuItem("Tools/Leaves of War/Project Cleanup")]
    private static void Run()
    {
        if (!EditorUtility.DisplayDialog("Project Cleanup",
            "Removes the demo folders of Book-Page Curl and ChocDino UIFX, _Trash and the already split Apollinaire.wav, " +
            "keeps one copy of each MakeHuman texture (capped at 512) and repoints the materials to it.\n\nCommit or back up first.", "Clean", "Cancel"))
            return;

        List<string> log = Clean();
        Debug.Log("Project Cleanup\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Project Cleanup", string.Join("\n", log), "OK");
    }

    public static List<string> Clean()
    {
        var log = new List<string>();
        EnsureFolder(UiFolder);
        foreach (var (from, to) in Keep)
            if (File.Exists(from) && !File.Exists(to))
            {
                string error = AssetDatabase.MoveAsset(from, to);
                log.Add(string.IsNullOrEmpty(error) ? "Kept " + Path.GetFileName(to) + " (used by a scene)." : "Could not move " + from + ": " + error);
            }

        foreach (string path in Remove)
            if (AssetDatabase.IsValidFolder(path) || File.Exists(path))
                log.Add((AssetDatabase.DeleteAsset(path) ? "Removed " : "Could not remove ") + path);

        log.Add(SharedMakeHumanTextures());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return log;
    }

    private static string SharedMakeHumanTextures()
    {
        if (!AssetDatabase.IsValidFolder(MakeHuman))
            return "No MakeHuman folder.";
        EnsureFolder(SharedTextures);
        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { MakeHuman }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(SharedTextures + "/") && !p.Contains("/_Outfits/")).Distinct().ToArray();
        var canonical = new Dictionary<string, Texture2D>();
        var duplicates = new Dictionary<Texture2D, Texture2D>();
        foreach (string path in paths.OrderBy(p => p))
        {
            string name = Path.GetFileName(path);
            string target = SharedTextures + "/" + name;
            if (!canonical.ContainsKey(name))
            {
                if (!File.Exists(target))
                    AssetDatabase.MoveAsset(path, target);
                canonical[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(File.Exists(target) ? target : path);
                if (File.Exists(path) && path != target)
                    duplicates[AssetDatabase.LoadAssetAtPath<Texture2D>(path)] = canonical[name];
            }
            else
            {
                duplicates[AssetDatabase.LoadAssetAtPath<Texture2D>(path)] = canonical[name];
            }
        }

        int repointed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MakeHuman }))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null)
                continue;
            bool changed = false;
            foreach (string property in material.GetTexturePropertyNames())
            {
                if (material.GetTexture(property) is Texture2D texture && texture != null && duplicates.TryGetValue(texture, out Texture2D shared))
                {
                    material.SetTexture(property, shared);
                    changed = true;
                }
            }
            if (changed)
            {
                EditorUtility.SetDirty(material);
                repointed++;
            }
        }
        AssetDatabase.SaveAssets();

        int removed = 0;
        foreach (Texture2D duplicate in duplicates.Keys.Where(t => t != null).ToList())
            if (AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(duplicate)))
                removed++;
        foreach (string folder in AssetDatabase.GetSubFolders(MakeHuman))
            foreach (string sub in AssetDatabase.GetSubFolders(folder).Where(f => Path.GetFileName(f).ToLowerInvariant() == "textures" && f != SharedTextures))
                if (!Directory.EnumerateFiles(sub).Any(f => !f.EndsWith(".meta")))
                    AssetDatabase.DeleteAsset(sub);

        foreach (Texture2D texture in canonical.Values.Where(t => t != null))
        {
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer && importer.maxTextureSize > 512)
            {
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
        }
        return "MakeHuman: " + canonical.Count + " shared textures (max 512), " + removed + " copies removed, " + repointed + " materials repointed.";
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
