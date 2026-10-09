using System;
using System.Collections.Generic;
using System.IO;
using Hortensia.Editor.Validation;
using Hortensia.Narrative;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Strict, repeatable development WebGL build. The companion repository
/// script runs this entry point in a dedicated batch editor and performs one
/// final byte-for-byte project-settings restoration after Unity exits.
/// </summary>
public static class DevelopmentBuildCommand
{
    private const string MenuPath = "Tools/Hortensia/Build/Development WebGL";
    private const string BuildPathArgument = "-developmentBuildPath";
    private const string BuildTargetStatePathArgument = "-buildTargetStatePath";
    private const string ExpectedBuildTargetArgument = "-expectedBuildTarget";
    private const string DefaultBuildPath = "Builds/WebGL-Development";

    private static readonly string[] CanonicalScenePaths =
    {
        "Assets/Scenes/Boot.unity",
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scenes/Manor.unity",
        "Assets/Scenes/Merridew.unity",
        "Assets/Scenes/TherapyOffice.unity",
        "Assets/Scenes/Lynwarre.unity",
        "Assets/Scenes/DreamGreenhouse.unity"
    };

    private static readonly string[] IncidentalSerializationPaths =
    {
        "Assets/Resources/Fonts/Gotfridus.asset",
        "Assets/Settings/Mobile_RPAsset.asset",
        "Assets/Settings/PC_RPAsset.asset",
        "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset",
        "ProjectSettings/ProjectSettings.asset"
    };

    [MenuItem(MenuPath)]
    private static void BuildFromMenu()
    {
        try
        {
            string buildPath = ResolveBuildPath(Environment.GetCommandLineArgs());
            BuildDevelopmentWebGl(buildPath);
            Debug.Log($"Development WebGL build completed successfully at '{buildPath}'.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Development WebGL build failed",
                exception.Message,
                "OK");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanBuildFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    /// <summary>
    /// Command-line entry point for -executeMethod. Throws on any preflight,
    /// build, artifact, target-restoration, or project-restoration failure.
    /// </summary>
    public static void Execute()
    {
        BuildDevelopmentWebGl(ResolveBuildPath(Environment.GetCommandLineArgs()));
    }

    /// <summary>
    /// Batch wrapper probe. Unity cannot switch build targets from an
    /// -executeMethod, so the wrapper records the current target first and
    /// launches the actual build with -buildTarget webgl.
    /// </summary>
    public static void CaptureActiveBuildTarget()
    {
        string statePath = ResolveRequiredArgument(
            Environment.GetCommandLineArgs(),
            BuildTargetStatePathArgument);
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string commandLineTarget = CommandLineBuildTarget(target);
        string standaloneSubtarget = BuildPipeline.GetBuildTargetGroup(target) ==
            BuildTargetGroup.Standalone
                ? EditorUserBuildSettings.standaloneBuildSubtarget.ToString().ToLowerInvariant()
                : string.Empty;

        string directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(
            statePath,
            new[] { target.ToString(), commandLineTarget, standaloneSubtarget });
        Debug.Log(
            $"Captured active build target '{target}' as command-line target " +
            $"'{commandLineTarget}'.");
    }

    /// <summary>
    /// Batch wrapper verification after launching Unity with the previous
    /// -buildTarget value.
    /// </summary>
    public static void VerifyActiveBuildTarget()
    {
        string expectedName = ResolveRequiredArgument(
            Environment.GetCommandLineArgs(),
            ExpectedBuildTargetArgument);
        if (!Enum.TryParse(expectedName, false, out BuildTarget expected))
            throw new InvalidOperationException($"Unknown expected build target '{expectedName}'.");

        BuildTarget actual = EditorUserBuildSettings.activeBuildTarget;
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Expected restored build target '{expected}', but Unity reports '{actual}'.");
        }

        Debug.Log($"Restored active build target '{actual}'.");
    }

    internal static void BuildDevelopmentWebGl(string requestedBuildPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("A development build cannot start in Play Mode.");
        if (EditorApplication.isCompiling)
            throw new InvalidOperationException("A development build cannot start while scripts compile.");

        if (!NarrativeCatalogLocator.TryLoadSingle(
                out NarrativeCatalog catalog,
                out string catalogError))
        {
            throw new InvalidOperationException(catalogError);
        }

        if (!PlayableSkeletonValidator.TryValidate(catalog, out string validationError))
            throw new InvalidOperationException(validationError);

        if (!TryGetCanonicalEnabledScenes(out string[] scenePaths, out string sceneError))
            throw new InvalidOperationException(sceneError);

        string buildPath = NormalizeBuildPath(requestedBuildPath);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var serializationSnapshot = new ProjectSerializationSnapshot(
            projectRoot,
            IncidentalSerializationPaths);
        bool dataDirectoryExisted = Directory.Exists(Path.Combine(projectRoot, "Data"));

        BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);
        bool targetChanged = previousTarget != BuildTarget.WebGL;
        if (Application.isBatchMode && targetChanged)
        {
            throw new InvalidOperationException(
                "Batch development builds must launch Unity with '-buildTarget webgl'. " +
                "Use scripts/build-development-webgl.sh so the previous target is restored afterward.");
        }

        Exception failure = null;
        try
        {
            if (targetChanged &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL))
            {
                throw new InvalidOperationException("Unity could not switch the active build target to WebGL.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            ValidateBuildReport(report);
            ValidateWebGlArtifacts(buildPath);

            Debug.Log(
                $"Development WebGL build succeeded at '{buildPath}'. " +
                $"Scenes: {scenePaths.Length}; size: {report.summary.totalSize} bytes; " +
                $"duration: {report.summary.totalTime}.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Exception restorationFailure = null;
            try
            {
                if (targetChanged &&
                    !EditorUserBuildSettings.SwitchActiveBuildTarget(
                        previousGroup,
                        previousTarget))
                {
                    throw new InvalidOperationException(
                        $"Unity could not restore the previous build target '{previousTarget}'.");
                }
            }
            catch (Exception exception)
            {
                restorationFailure = exception;
            }

            try
            {
                serializationSnapshot.Restore();
            }
            catch (Exception exception)
            {
                restorationFailure = Combine(restorationFailure, exception);
            }

            try
            {
                RemoveNewDataDirectory(projectRoot, dataDirectoryExisted);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                restorationFailure = Combine(restorationFailure, exception);
            }

            failure = Combine(failure, restorationFailure);
        }

        if (failure != null)
            throw new InvalidOperationException("Development WebGL build failed.", failure);
    }

    internal static bool TryGetCanonicalEnabledScenes(
        out string[] scenePaths,
        out string error)
    {
        var report = new ValidationReport();
        BuildSceneIndex index = BuildSceneIndex.Capture(report);
        IReadOnlyList<string> enabled = index.EnabledScenePaths;

        if (enabled.Count != CanonicalScenePaths.Length)
        {
            report.Add(
                $"Build Settings must contain exactly {CanonicalScenePaths.Length} enabled scenes " +
                $"in canonical order, but contains {enabled.Count}.");
        }

        int comparableCount = Math.Min(enabled.Count, CanonicalScenePaths.Length);
        for (int i = 0; i < comparableCount; i++)
        {
            if (!string.Equals(enabled[i], CanonicalScenePaths[i], StringComparison.Ordinal))
            {
                report.Add(
                    $"Enabled Build Settings scene {i + 1} is '{enabled[i]}'; " +
                    $"expected '{CanonicalScenePaths[i]}'.");
            }
        }

        for (int i = 0; i < CanonicalScenePaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CanonicalScenePaths[i]) == null)
                report.Add($"Canonical build scene '{CanonicalScenePaths[i]}' is missing.");
        }

        scenePaths = new string[enabled.Count];
        for (int i = 0; i < enabled.Count; i++)
            scenePaths[i] = enabled[i];

        return report.TryFormat(
            "Development build scene preflight failed:",
            true,
            out error);
    }

    internal static string NormalizeBuildPath(string requestedBuildPath)
    {
        string path = string.IsNullOrWhiteSpace(requestedBuildPath)
            ? DefaultBuildPath
            : requestedBuildPath.Trim();
        if (!Path.IsPathRooted(path))
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            path = Path.Combine(projectRoot, path);
        }

        return Path.GetFullPath(path);
    }

    private static string ResolveBuildPath(IReadOnlyList<string> arguments)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];
            if (string.Equals(argument, BuildPathArgument, StringComparison.Ordinal) &&
                i + 1 < arguments.Count)
            {
                return arguments[i + 1];
            }

            string prefix = BuildPathArgument + "=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
                return argument.Substring(prefix.Length);
        }

        return DefaultBuildPath;
    }

    private static string ResolveRequiredArgument(
        IReadOnlyList<string> arguments,
        string argumentName)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];
            if (string.Equals(argument, argumentName, StringComparison.Ordinal) &&
                i + 1 < arguments.Count)
            {
                return arguments[i + 1];
            }

            string prefix = argumentName + "=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
                return argument.Substring(prefix.Length);
        }

        throw new InvalidOperationException(
            $"Required command-line argument '{argumentName}' was not supplied.");
    }

    private static string CommandLineBuildTarget(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneOSX:
                return "osxuniversal";
            case BuildTarget.StandaloneWindows:
                return "win";
            case BuildTarget.StandaloneWindows64:
                return "win64";
            case BuildTarget.StandaloneLinux64:
                return "linux64";
            case BuildTarget.iOS:
                return "ios";
            case BuildTarget.Android:
                return "android";
            case BuildTarget.WebGL:
                return "webgl";
            case BuildTarget.WSAPlayer:
                return "windowsstoreapps";
            case BuildTarget.tvOS:
                return "tvos";
            default:
                throw new InvalidOperationException(
                    $"The build wrapper does not know how to restore build target '{target}'.");
        }
    }

    private static void ValidateBuildReport(BuildReport report)
    {
        if (report == null)
            throw new InvalidOperationException("Unity returned no build report.");

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
        {
            throw new InvalidOperationException(
                $"Unity reported {summary.result} with {summary.totalErrors} error(s) " +
                $"and {summary.totalWarnings} warning(s).");
        }
    }

    private static void ValidateWebGlArtifacts(string buildPath)
    {
        RequireNonemptyFile(Path.Combine(buildPath, "index.html"), "WebGL index.html");

        string buildDirectory = Path.Combine(buildPath, "Build");
        if (!Directory.Exists(buildDirectory))
            throw new InvalidOperationException("The WebGL build has no Build directory.");

        RequireMatchingArtifact(buildDirectory, "*.loader.js", "WebGL loader");
        RequireMatchingArtifact(buildDirectory, "*.data*", "WebGL data");
        RequireMatchingArtifact(buildDirectory, "*.framework.js*", "WebGL framework");
        RequireMatchingArtifact(buildDirectory, "*.wasm*", "WebAssembly binary");
    }

    private static void RequireMatchingArtifact(
        string directory,
        string pattern,
        string description)
    {
        string[] matches = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
        for (int i = 0; i < matches.Length; i++)
        {
            if (new FileInfo(matches[i]).Length > 0)
                return;
        }

        throw new InvalidOperationException(
            $"The build produced no nonempty {description} artifact matching '{pattern}'.");
    }

    private static void RequireNonemptyFile(string path, string description)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidOperationException($"The build produced no nonempty {description}.");
    }

    internal static void RemoveNewDataDirectory(string projectRoot, bool existedBeforeBuild)
    {
        string dataPath = Path.Combine(projectRoot, "Data");
        if (!existedBeforeBuild && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
    }

    private static Exception Combine(Exception first, Exception second)
    {
        if (first == null)
            return second;
        if (second == null)
            return first;
        return new AggregateException(first, second);
    }

    internal sealed class ProjectSerializationSnapshot
    {
        private readonly List<FileSnapshot> files = new List<FileSnapshot>();

        public ProjectSerializationSnapshot(
            string projectRoot,
            IReadOnlyList<string> relativePaths)
        {
            for (int i = 0; i < relativePaths.Count; i++)
            {
                string absolutePath = Path.GetFullPath(
                    Path.Combine(projectRoot, relativePaths[i]));
                files.Add(new FileSnapshot(absolutePath));
            }
        }

        public void Restore()
        {
            for (int i = 0; i < files.Count; i++)
                files[i].Restore();
        }
    }

    private sealed class FileSnapshot
    {
        private readonly string path;
        private readonly bool existed;
        private readonly byte[] bytes;

        public FileSnapshot(string filePath)
        {
            path = filePath;
            existed = File.Exists(path);
            bytes = existed ? File.ReadAllBytes(path) : null;
        }

        public void Restore()
        {
            if (!existed)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            byte[] current = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (BytesEqual(bytes, current))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, bytes);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }
    }
}
