using System;
using System.Collections.Generic;

namespace Hortensia.Editor.Validation
{
    internal sealed class ValidationContext : IDisposable
    {
        public ValidationContext(ICollection<string> problems)
            : this(BuildSceneIndex.Capture(problems), null)
        {
        }

        internal ValidationContext(
            BuildSceneIndex buildScenes,
            Func<string, SceneSnapshot> sceneOpener)
        {
            BuildScenes = buildScenes ?? throw new ArgumentNullException(nameof(buildScenes));
            Scenes = new SceneInspectionCache(BuildScenes, sceneOpener);
        }

        public BuildSceneIndex BuildScenes { get; }
        public SceneInspectionCache Scenes { get; }

        public void Dispose()
        {
            Scenes.Dispose();
        }
    }
}
