using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectOptimizer
{
    private const int MaxTexture = 512;
    private const string Report = "Optimize Report.txt";
    private const string WindowsBuild = "Builds/Desktop/Leaves of War.exe";
    private const string WebBuild = "Builds/WebGL";

    private static readonly string[] KeepTextures =
    {
        "/Fonts/", "/TextMesh Pro/", "Assets/Resources/MainMenu/", "Assets/Resources/UI/", "/_Trash/", "Assets/StreamingAssets/"
    };

    [MenuItem("Tools/Leaves of War/Optimize Everything (clean, tidy, smallest builds)")]
    private static void Menu()
    {
        if (!EditorUtility.DisplayDialog("Optimize Everything",
            "1. Cleanup: demo folders, old trash, duplicate MakeHuman textures.\n" +
            "2. Tidy: test objects out of the level scenes, scenes ordered, loose assets in their folders, unused ones to _Trash (restorable).\n" +
            "3. Smallest builds: audio, textures, models and player settings set for the lightest Windows and WebGL builds.\n\n" +
            "It reimports many files and can take several minutes. Commit or back up first.", "Optimize", "Cancel"))
            return;
        RunAll();
    }

    [MenuItem("Tools/Leaves of War/Optimize Build Size")]
    private static void SizeMenu()
    {
        var log = new List<string>();
        Minimize(log);
        EditorUtility.DisplayDialog("Optimize Build Size", string.Join("\n", log), "OK");
    }

    [MenuItem("Tools/Leaves of War/Build/Windows (smallest)")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsBuild, BuildOptions.CompressWithLz4HC);

    [MenuItem("Tools/Leaves of War/Build/WebGL (smallest)")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, WebBuild, BuildOptions.None);

    public static void RunAll()
    {
        var log = new List<string> { "Leaves of War - optimize " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string active = EditorSceneManager.GetActiveScene().path;
        Step(log, "CLEANUP", () => log.AddRange(ProjectCleanup.Clean()));
        Step(log, "TIDY", () => ProjectTidy.RunAuto(log));
        Step(log, "SMALLEST BUILDS", () => Minimize(log));
        if (!Application.isBatchMode && !string.IsNullOrEmpty(active) && File.Exists(active))
            EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        Finish(log);
    }

    public static void Minimize(List<string> log)
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            log.Add(Audio());
            log.Add(Textures());
            log.Add(Models());
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        log.Add(Player());
        log.Add(Shaders());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Step(List<string> log, string title, Action action)
    {
        log.Add("");
        log.Add("== " + title + " ==");
        try
        {
            action();
        }
        catch (Exception e)
        {
            log.Add("ERROR: " + e.Message);
            Debug.LogException(e);
        }
    }

    private static void Finish(List<string> log)
    {
        string text = string.Join("\n", log);
        File.WriteAllText(Report, text);
        Debug.Log(text);
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("Optimize Everything", text.Length > 3000 ? text.Substring(0, 3000) + "\n...\n(full report in " + Report + ")" : text, "OK");
    }

    private static string Audio()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Trash/") || !(AssetImporter.GetAtPath(path) is AudioImporter importer))
                continue;
            string lower = path.ToLowerInvariant();
            bool music = lower.Contains("/music/");
            bool ambience = lower.Contains("/ambience/") && !lower.Contains("/ambience/mono/");
            bool voice = lower.Contains("/voices/");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = !music;
            if (music || ambience)
            {
                settings.quality = 0.3f;
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 32000u;
            }
            else
            {
                settings.quality = voice ? 0.35f : 0.3f;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 22050u;
                importer.forceToMono = true;
            }
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings("WebGL", settings);
            importer.loadInBackground = music || ambience;
            importer.SaveAndReimport();
            count++;
        }
        return "Audio: " + count + " clips. Voices mono 22 kHz Vorbis 35%; sound effects mono 22 kHz Vorbis 30%; music (streamed) and ambience 32 kHz Vorbis 30%. Same on WebGL.";
    }

    private static string Textures()
    {
        int count = 0, kept = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                continue;
            if (KeepTextures.Any(k => path.Contains(k)) || importer.isReadable || importer.textureType == TextureImporterType.Sprite ||
                importer.textureType == TextureImporterType.GUI || importer.textureType == TextureImporterType.Cursor || importer.textureType == TextureImporterType.Lightmap)
            {
                kept++;
                continue;
            }
            bool changed = false;
            if (importer.maxTextureSize > MaxTexture)
            {
                importer.maxTextureSize = MaxTexture;
                changed = true;
            }
            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }
            if (!importer.crunchedCompression || importer.compressionQuality > 50)
            {
                importer.crunchedCompression = true;
                importer.compressionQuality = 50;
                changed = true;
            }
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
            if (!web.overridden || web.maxTextureSize > MaxTexture || !web.crunchedCompression)
            {
                web.overridden = true;
                web.maxTextureSize = Mathf.Min(MaxTexture, importer.maxTextureSize);
                web.format = TextureImporterFormat.Automatic;
                web.textureCompression = TextureImporterCompression.Compressed;
                web.crunchedCompression = true;
                web.compressionQuality = 50;
                importer.SetPlatformTextureSettings(web);
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                count++;
            }
        }
        return "Textures: " + count + " set to at most " + MaxTexture + " px with crunch compression 50 (also on WebGL); " + kept + " left as they are (fonts, UI, menu, readable).";
    }

    private static string Models()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Trash/") || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
                continue;
            bool changed = false;
            if (importer.meshCompression == ModelImporterMeshCompression.Off)
            {
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                changed = true;
            }
            if (!importer.optimizeMeshPolygons || !importer.optimizeMeshVertices)
            {
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                changed = true;
            }
            if (importer.animationCompression != ModelImporterAnimationCompression.Optimal)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                count++;
            }
        }
        return "Models: " + count + " with mesh compression, optimized meshes and optimal animation compression.";
    }

    private static string Player()
    {
        PlayerSettings.stripUnusedMeshComponents = true;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.dataCaching = true;
        EditorUserBuildSettings.development = false;
        bool lz4hc = SetCompression(BuildTargetGroup.Standalone, "Lz4HC");
        return "Player: unused mesh data and engine code stripped, managed stripping Medium; WebGL IL2CPP optimized for size (Master), Brotli with decompression fallback (works on itch.io), no debug exceptions; " +
            (lz4hc ? "Windows build compression LZ4HC." : "Windows: use Build > Windows (smallest), it builds with LZ4HC.");
    }

    private static bool SetCompression(BuildTargetGroup group, string value)
    {
        try
        {
            Type type = typeof(EditorUserBuildSettings);
            Type compression = type.Assembly.GetType("UnityEditor.Compression");
            MethodInfo set = type.GetMethod("SetCompressionType", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (compression == null || set == null)
                return false;
            set.Invoke(null, new[] { (object)group, Enum.Parse(compression, value) });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Shaders()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings"))
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null)
                continue;
            var so = new SerializedObject(asset);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
                if (p.propertyType == SerializedPropertyType.Boolean && p.name.ToLowerInvariant().Contains("strip") && !p.boolValue)
                {
                    p.boolValue = true;
                    count++;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return "Shaders: " + count + " URP stripping options turned on (debug and unused variants are left out of the build).";
    }

    private static void Build(BuildTarget target, string location, BuildOptions options)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (target == BuildTarget.WebGL)
            Directory.CreateDirectory(location);
        else
            Directory.CreateDirectory(Path.GetDirectoryName(location));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            target = target,
            options = options
        });
        ulong size = report.summary.totalSize;
        string text = target + " build: " + report.summary.result + ", " + (size / 1048576.0).ToString("0.0") + " MB in " + location + ".";
        Debug.Log(text);
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("Build", text, "OK");
        else if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
