using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    internal static class LevelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SetUpScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => SetUpScene(scene);

        private static void SetUpScene(Scene scene)
        {
            KeepSingleEventSystem(scene);

            if (!LevelProgress.IsLevel(scene.name))
                return;

            PauseController.EnsureInstance();

            if (Object.FindAnyObjectByType<FirstPersonController>() != null &&
                InteractionPromptHud.Instance == null &&
                Object.FindAnyObjectByType<InteractionPromptHud>() == null)
            {
                new GameObject("Interaction Prompt HUD").AddComponent<InteractionPromptHud>();
            }
        }

        private static void KeepSingleEventSystem(Scene scene)
        {
            EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (systems.Length == 0)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                return;
            }

            EventSystem keep = systems[0];
            foreach (EventSystem system in systems)
            {
                if (system.gameObject.scene == scene && system.isActiveAndEnabled)
                {
                    keep = system;
                    break;
                }
            }

            foreach (EventSystem system in systems)
            {
                if (system != keep)
                    Object.Destroy(system.gameObject);
            }

            if (!keep.gameObject.activeSelf)
                keep.gameObject.SetActive(true);
            keep.enabled = true;
        }
    }
}
