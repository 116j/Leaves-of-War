using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    public static class LevelProgress
    {
        public const int SlotCount = 6;

        private const string AutosaveKey = "leavesofwar.autosave";
        private const string SlotKey = "leavesofwar.slot.";
        private static readonly string[] NotLevels = { "Boot", "MainMenu", "Title", "Title 1" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && IsLevel(scene.name))
                Write(AutosaveKey, scene.name);
        }

        public static bool IsLevel(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && System.Array.IndexOf(NotLevels, sceneName) < 0;

        public static string CurrentLevel
        {
            get
            {
                string name = SceneManager.GetActiveScene().name;
                return IsLevel(name) ? name : null;
            }
        }

        public static string Autosave => Read(AutosaveKey);
        public static string Slot(int number) => Read(SlotKey + number);
        public static void SaveSlot(int number, string sceneName) => Write(SlotKey + number, sceneName);

        public static void DeleteSlot(int number)
        {
            PlayerPrefs.DeleteKey(SlotKey + number);
            PlayerPrefs.Save();
        }

        public static void DeleteAll()
        {
            PlayerPrefs.DeleteKey(AutosaveKey);
            for (int i = 1; i <= SlotCount; i++)
                PlayerPrefs.DeleteKey(SlotKey + i);
            PlayerPrefs.Save();
        }

        public static bool CanLoad(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        private static string Read(string key) => PlayerPrefs.GetString(key, string.Empty);

        private static void Write(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }
    }
}
