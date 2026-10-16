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
        Debug.Log("Project Cleanup\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Project Cleanup", string.Join("\n", log), "OK");
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

    [MenuItem("Tools/Leaves of War/Optimize Build Size")]
    private static void OptimizeBuildSize()
    {
        if (!EditorUtility.DisplayDialog("Optimize Build Size",
            "Voices and poems: mono, 22 kHz, Vorbis 50%.\nSound effects: 32 kHz, Vorbis 50%.\nMusic: Vorbis 45%, streamed. Ambience: Vorbis 45%, compressed in memory (needed for the loop crossfade).\n" +
            "Game textures: at most 1024 px (PS1 look), crunch compression 50.\n\nIt reimports many files: it can take a few minutes.", "Optimize", "Cancel"))
            return;
        int audio = 0, textures = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Game/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                    continue;
                string lower = path.ToLowerInvariant();
                bool voice = lower.Contains("/voices/");
                bool monoCopy = lower.Contains("/ambience/mono/");
                bool longClip = lower.Contains("/music/") || lower.Contains("/ambience/");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = longClip ? 0.45f : 0.5f;
                if (longClip)
                {
                    settings.loadType = lower.Contains("/music/") ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                }
                else
                {
                    settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    settings.sampleRateOverride = voice ? 22050u : 32000u;
                }
                importer.defaultSampleSettings = settings;
                if (voice || monoCopy)
                    importer.forceToMono = true;
                importer.SaveAndReimport();
                audio++;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.textureType == TextureImporterType.Sprite || path.Contains("/Fonts/"))
                    continue;
                bool changed = false;
                if (importer.maxTextureSize > 1024)
                {
                    importer.maxTextureSize = 1024;
                    changed = true;
                }
                if (!importer.crunchedCompression)
                {
                    importer.crunchedCompression = true;
                    importer.compressionQuality = 50;
                    changed = true;
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                    textures++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Optimize Build Size", audio + " audio clips and " + textures + " textures set up for a smaller build.\n\n" +
            "Last step by hand: File > Build Profiles > Windows > Compression Method = LZ4HC.", "OK");
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
