using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Hortensia.Editor.Validation
{
    internal sealed class BuildSceneIndex
    {
        private readonly Dictionary<string, string> pathsByName;
        private readonly List<string> enabledScenePaths;

        private BuildSceneIndex(
            Dictionary<string, string> scenePaths,
            List<string> orderedEnabledPaths)
        {
            pathsByName = scenePaths;
            enabledScenePaths = orderedEnabledPaths;
        }

        public IReadOnlyList<string> EnabledScenePaths => enabledScenePaths;

        public static BuildSceneIndex Capture(ICollection<string> problems)
        {
            return Create(EditorBuildSettings.scenes, problems);
        }

        internal static BuildSceneIndex Create(
            IEnumerable<EditorBuildSettingsScene> buildScenes,
            ICollection<string> problems)
        {
            if (buildScenes == null)
                throw new ArgumentNullException(nameof(buildScenes));
            if (problems == null)
                throw new ArgumentNullException(nameof(problems));

            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            var orderedPaths = new List<string>();
            foreach (EditorBuildSettingsScene buildScene in buildScenes)
            {
                if (buildScene == null || !buildScene.enabled)
                    continue;

                orderedPaths.Add(buildScene.path);
                string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
                if (paths.ContainsKey(sceneName))
                {
                    problems.Add(
                        $"Build Settings contains more than one enabled scene named '{sceneName}'.");
                    continue;
                }

                paths.Add(sceneName, buildScene.path);
            }

            return new BuildSceneIndex(paths, orderedPaths);
        }

        internal static BuildSceneIndex CreateForTests(
            IEnumerable<KeyValuePair<string, string>> scenePaths)
        {
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            var orderedPaths = new List<string>();
            foreach (KeyValuePair<string, string> pair in scenePaths)
            {
                paths.Add(pair.Key, pair.Value);
                orderedPaths.Add(pair.Value);
            }

            return new BuildSceneIndex(paths, orderedPaths);
        }

        public bool TryGetPath(string sceneName, out string scenePath)
        {
            return pathsByName.TryGetValue(sceneName, out scenePath);
        }
    }
}
