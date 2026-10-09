using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    public interface ISceneLoadOperation
    {
        bool IsDone { get; }
    }

    public interface ISceneLoader
    {
        bool CanLoad(string sceneName);
        ISceneLoadOperation LoadSingleAsync(string sceneName);
        bool IsLoadedAndActive(string sceneName);
    }

    internal sealed class UnitySceneLoader : ISceneLoader
    {
        public static readonly UnitySceneLoader Instance = new UnitySceneLoader();

        private UnitySceneLoader()
        {
        }

        public bool CanLoad(string sceneName) =>
            !string.IsNullOrWhiteSpace(sceneName) &&
            Application.CanStreamedLevelBeLoaded(sceneName);

        public ISceneLoadOperation LoadSingleAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            return operation == null ? null : new UnitySceneLoadOperation(operation);
        }

        public bool IsLoadedAndActive(string sceneName)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() &&
                activeScene.isLoaded &&
                string.Equals(activeScene.name, sceneName, System.StringComparison.Ordinal);
        }

        private sealed class UnitySceneLoadOperation : ISceneLoadOperation
        {
            private readonly AsyncOperation operation;

            public UnitySceneLoadOperation(AsyncOperation operation)
            {
                this.operation = operation;
            }

            public bool IsDone => operation.isDone;
        }
    }
}
