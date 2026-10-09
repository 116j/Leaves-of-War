using System;
using System.Collections.Generic;
using Hortensia.Narrative;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Editor.Validation
{
    internal enum SceneDiagnosticKind
    {
        Catalog,
        Gameplay
    }

    internal sealed class SceneInspectionCache : IDisposable
    {
        private readonly BuildSceneIndex buildScenes;
        private readonly Func<string, SceneSnapshot> sceneOpener;
        private readonly Dictionary<string, SceneSnapshot> snapshots =
            new Dictionary<string, SceneSnapshot>(StringComparer.Ordinal);
        private readonly HashSet<string> failedScenes =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<SceneSnapshot> openedSnapshots = new List<SceneSnapshot>();

        public SceneInspectionCache(
            BuildSceneIndex sceneIndex,
            Func<string, SceneSnapshot> opener = null)
        {
            buildScenes = sceneIndex ?? throw new ArgumentNullException(nameof(sceneIndex));
            sceneOpener = opener ?? SceneSnapshot.Open;
        }

        internal int CachedSceneCount => snapshots.Count;
        internal int FailedSceneCount => failedScenes.Count;

        public bool TryGet(
            string sceneName,
            SceneDiagnosticKind diagnosticKind,
            ICollection<string> problems,
            out SceneSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;
            if (snapshots.TryGetValue(sceneName, out snapshot))
                return true;
            if (failedScenes.Contains(sceneName))
                return false;

            string subject = diagnosticKind == SceneDiagnosticKind.Gameplay
                ? "Gameplay scene"
                : "Scene";
            if (!buildScenes.TryGetPath(sceneName, out string scenePath))
            {
                problems.Add($"{subject} '{sceneName}' is not an enabled Build Settings scene.");
                failedScenes.Add(sceneName);
                return false;
            }

            try
            {
                snapshot = sceneOpener(scenePath);
                if (snapshot == null)
                    throw new InvalidOperationException("The scene opener returned no inspection snapshot.");

                snapshots.Add(sceneName, snapshot);
                openedSnapshots.Add(snapshot);
                return true;
            }
            catch (Exception exception)
            {
                problems.Add($"Could not inspect {subject.ToLowerInvariant()} '{sceneName}': {exception.Message}");
                failedScenes.Add(sceneName);
                snapshot = null;
                return false;
            }
        }

        public void Dispose()
        {
            for (int i = openedSnapshots.Count - 1; i >= 0; i--)
                openedSnapshots[i].Dispose();

            openedSnapshots.Clear();
            snapshots.Clear();
            failedScenes.Clear();
        }
    }

    internal sealed class SceneSnapshot : IDisposable
    {
        private readonly Scene scene;
        private readonly bool closeWhenDone;
        private readonly Dictionary<Type, object> componentsByType =
            new Dictionary<Type, object>();
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<ChapterDressingGroup> dressingGroups =
            new List<ChapterDressingGroup>();
        private bool disposed;

        private SceneSnapshot(Scene inspectedScene, bool shouldClose)
        {
            scene = inspectedScene;
            closeWhenDone = shouldClose;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                    gameObjects.Add(transforms[transformIndex].gameObject);

                ChapterDressing[] dressings =
                    root.GetComponentsInChildren<ChapterDressing>(true);
                for (int dressingIndex = 0; dressingIndex < dressings.Length; dressingIndex++)
                {
                    ChapterDressing dressing = dressings[dressingIndex];
                    // ChapterDressing applies its groups from Start. A disabled
                    // component or one beneath an inactive hierarchy never runs,
                    // so its inactive roots cannot become registrable services.
                    if (dressing == null || !dressing.enabled ||
                        !dressing.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    for (int groupIndex = 0; groupIndex < dressing.Groups.Count; groupIndex++)
                    {
                        ChapterDressingGroup group = dressing.Groups[groupIndex];
                        if (group != null && group.Root != null)
                            dressingGroups.Add(group);
                    }
                }
            }
        }

        public string SceneName => scene.name;
        public IReadOnlyList<GameObject> GameObjects => gameObjects;

        public static SceneSnapshot Open(string scenePath)
        {
            Scene existing = SceneManager.GetSceneByPath(scenePath);
            if (existing.IsValid() && existing.isLoaded)
                return new SceneSnapshot(existing, false);

            Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                return new SceneSnapshot(opened, true);
            }
            catch
            {
                if (opened.IsValid() && opened.isLoaded)
                    EditorSceneManager.CloseScene(opened, true);
                throw;
            }
        }

        internal static SceneSnapshot Capture(Scene inspectedScene, bool closeOnDispose)
        {
            if (!inspectedScene.IsValid() || !inspectedScene.isLoaded)
                throw new ArgumentException("Scene inspection requires a valid loaded scene.", nameof(inspectedScene));

            return new SceneSnapshot(inspectedScene, closeOnDispose);
        }

        public IReadOnlyList<T> Components<T>() where T : Component
        {
            Type type = typeof(T);
            if (componentsByType.TryGetValue(type, out object cached))
                return (IReadOnlyList<T>)cached;

            var components = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                components.AddRange(roots[rootIndex].GetComponentsInChildren<T>(true));

            componentsByType.Add(type, components);
            return components;
        }

        public int CountMissingScripts()
        {
            int missing = 0;
            for (int objectIndex = 0; objectIndex < gameObjects.Count; objectIndex++)
            {
                Component[] components = gameObjects[objectIndex].GetComponents<Component>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    if (components[componentIndex] == null)
                        missing++;
                }
            }

            return missing;
        }

        public int CountSpawns(string id, int chapterIndex)
        {
            int matches = 0;
            IReadOnlyList<SpawnPoint> spawnPoints = Components<SpawnPoint>();
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                SpawnPoint spawnPoint = spawnPoints[i];
                if (WouldRegisterInChapter(spawnPoint, chapterIndex) &&
                    spawnPoint.Matches(id))
                {
                    matches++;
                }
            }

            return matches;
        }

        /// <summary>
        /// Mirrors the eligibility of scene services that register from
        /// MonoBehaviour.OnEnable. Chapter-dressing roots may be checked in as
        /// inactive and become active for their authored chapter; unrelated
        /// inactive hierarchy and disabled components never register.
        /// </summary>
        public bool WouldRegisterInChapter(Behaviour service, int chapterIndex)
        {
            return service != null &&
                service.enabled &&
                IsReachableInChapter(service.transform, chapterIndex);
        }

        public bool HasReachableDocument(DocumentDefinition document, int chapterIndex)
        {
            IReadOnlyList<DocumentInteractable> documents = Components<DocumentInteractable>();
            for (int i = 0; i < documents.Count; i++)
            {
                DocumentInteractable interactable = documents[i];
                if (interactable == null || !ReferenceEquals(interactable.Document, document))
                    continue;

                if (WouldRegisterInChapter(interactable, chapterIndex) &&
                    HasUsableInteractionCollider(interactable, chapterIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public int CountTaskReceivers(TaskObjective objective, int chapterIndex)
        {
            // Runtime registration includes receivers in inactive progressive
            // roots so their aggregate requirement is stable before any station
            // is revealed. Mirror that rule, but accept an inactive ancestor only
            // when a currently reachable listener for the same objective owns it.
            int count = 0;
            IReadOnlyList<TaskReceiver> receivers = Components<TaskReceiver>();
            for (int i = 0; i < receivers.Count; i++)
            {
                TaskReceiver receiver = receivers[i];
                if (receiver == null || !receiver.enabled ||
                    receiver.Accepts == null ||
                    !ReferenceEquals(receiver.Accepts.Objective, objective) ||
                    !IsTaskProgressReachable(
                        receiver.transform,
                        objective,
                        chapterIndex) ||
                    !HasTaskProgressInteractionCollider(
                        receiver,
                        objective,
                        chapterIndex))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Tests task hierarchy that may be checked in inactive for a progressive
        /// reveal. Every inactive non-dressing ancestor must be the direct reveal
        /// target of a reachable listener for the same objective; arbitrary hidden
        /// wrappers remain unreachable.
        /// </summary>
        public bool IsTaskProgressReachable(
            Transform target,
            TaskObjective objective,
            int chapterIndex)
        {
            if (target == null || objective == null ||
                !IsAllowedByChapterDressing(target, chapterIndex))
            {
                return false;
            }

            IReadOnlyList<TaskProgressListener> listeners =
                Components<TaskProgressListener>();
            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.gameObject.activeSelf ||
                    IsEnabledDressingRoot(current, chapterIndex))
                {
                    continue;
                }

                bool controlledReveal = false;
                for (int i = 0; i < listeners.Count; i++)
                {
                    TaskProgressListener listener = listeners[i];
                    if (listener == null || !listener.enabled ||
                        !ReferenceEquals(listener.Objective, objective) ||
                        !ReferenceEquals(listener.Revealed, current.gameObject) ||
                        !IsReachableInChapter(listener.transform, chapterIndex))
                    {
                        continue;
                    }

                    controlledReveal = true;
                    break;
                }

                if (!controlledReveal)
                    return false;
            }

            return true;
        }

        public bool HasTaskProgressInteractionCollider(
            MonoBehaviour interactable,
            TaskObjective objective,
            int chapterIndex)
        {
            if (interactable == null || objective == null ||
                !IsTaskProgressReachable(
                    interactable.transform,
                    objective,
                    chapterIndex))
            {
                return false;
            }

            Collider[] colliders = interactable.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger ||
                    !IsTaskProgressReachable(
                        collider.transform,
                        objective,
                        chapterIndex))
                {
                    continue;
                }

                IInteractable resolved =
                    collider.GetComponentInParent<IInteractable>(true);
                if (ReferenceEquals(resolved, interactable))
                    return true;
            }

            return false;
        }

        public bool HasUsableInteractionCollider(
            MonoBehaviour interactable,
            int chapterIndex)
        {
            if (interactable == null)
                return false;

            Collider[] colliders = interactable.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger ||
                    !IsReachableInChapter(collider.transform, chapterIndex))
                {
                    continue;
                }

                IInteractable resolved = collider.GetComponentInParent<IInteractable>();
                if (ReferenceEquals(resolved, interactable))
                    return true;
            }

            return false;
        }

        public bool IsReachableInChapter(Transform target, int chapterIndex)
        {
            if (target == null || !IsAllowedByChapterDressing(target, chapterIndex))
                return false;

            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.gameObject.activeSelf)
                    continue;
                if (!IsEnabledDressingRoot(current, chapterIndex))
                    return false;
            }

            return true;
        }

        public bool IsAllowedByChapterDressing(Transform target, int chapterIndex)
        {
            if (target == null)
                return false;

            for (int i = 0; i < dressingGroups.Count; i++)
            {
                ChapterDressingGroup group = dressingGroups[i];
                if (IsSelfOrChildOf(target, group.Root.transform) &&
                    !group.IsActiveIn(chapterIndex))
                {
                    return false;
                }
            }

            return true;
        }

        public T FindAncestor<T>(Transform target) where T : Component
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                T component = current.GetComponent<T>();
                if (component != null)
                    return component;
            }

            return null;
        }

        public static T ObjectReference<T>(UnityEngine.Object owner, string propertyName)
            where T : UnityEngine.Object
        {
            if (owner == null)
                return null;

            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        public static string HierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<missing>";

            var parts = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                parts.Push(current.name);
            return string.Join("/", parts);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            if (!closeWhenDone || !scene.IsValid() || !scene.isLoaded)
                return;

            if (EditorSceneManager.IsPreviewScene(scene))
                EditorSceneManager.ClosePreviewScene(scene);
            else
                EditorSceneManager.CloseScene(scene, true);
        }

        private bool IsEnabledDressingRoot(Transform transform, int chapterIndex)
        {
            for (int i = 0; i < dressingGroups.Count; i++)
            {
                ChapterDressingGroup group = dressingGroups[i];
                if (ReferenceEquals(group.Root.transform, transform) &&
                    group.IsActiveIn(chapterIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSelfOrChildOf(Transform target, Transform possibleAncestor)
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, possibleAncestor))
                    return true;
            }

            return false;
        }
    }
}
