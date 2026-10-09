using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PauseController : MonoBehaviour
    {
        private const string FontPath = "Fonts/OSerif-Regular";

        public static PauseController Instance { get; private set; }

        public static PauseController EnsureInstance() =>
            Instance != null ? Instance : new GameObject("Pause Controller").AddComponent<PauseController>();

        public bool IsPaused { get; private set; }

        [SerializeField] private Texture2D cornice;

        private TMP_FontAsset font;
        private GameObject panel;
        private GameObject slotPanel;
        private GameObject corniceOverlay;
        private TMP_Text slotHeader;
        private TMP_Text slotStatus;
        private OptionsPanel optionsPanel;
        private readonly List<(int number, Button button, TMP_Text label)> slots = new List<(int, Button, TMP_Text)>();
        private bool slotPanelSaves;
        private bool deleteMode;
        private int pendingDelete = -1;
        private bool pendingDeleteAll;
        private TMP_Text deleteLabel;
        private readonly List<(TMP_Text text, float size)> styledTexts = new List<(TMP_Text, float)>();
        private readonly List<Image> buttonImages = new List<Image>();

        private FirstPersonController boundPlayer;
        private float previousTimeScale = 1f;
        private bool previousAudioPause;
        private bool audioPausedByManager;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            font = Resources.Load<TMP_FontAsset>(FontPath) ?? TMP_Settings.defaultFontAsset;
            BuildUi();
            GameSettings.Applied += ApplyStyle;
            ApplyStyle();
        }

        private void OnDestroy()
        {
            GameSettings.Applied -= ApplyStyle;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!PausePressed())
                return;

            if (IsPaused)
                Resume();
            else if (LevelProgress.CurrentLevel != null)
                Pause();
        }

        private bool PausePressed()
        {
            if (boundPlayer == null)
                boundPlayer = FindAnyObjectByType<FirstPersonController>();

            if (boundPlayer != null && boundPlayer.PauseAction != null)
            {
                if (boundPlayer.PauseAction.WasPressedThisFrame())
                    return true;
            }
            else if (Keyboard.current != null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (Keyboard.current.pKey.wasPressedThisFrame)
                    return true;
#else
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    return true;
#endif
            }

            return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            AudioManager audioManager = AudioManager.Instance;
            audioPausedByManager = audioManager != null;
            if (audioPausedByManager)
            {
                audioManager.SetGameplayAudioPaused(true);
            }
            else
            {
                previousAudioPause = AudioListener.pause;
                AudioListener.pause = true;
            }

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            panel.SetActive(true);
            corniceOverlay?.SetActive(true);
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            panel.SetActive(false);
            slotPanel.SetActive(false);
            optionsPanel.Hide();
            corniceOverlay?.SetActive(false);

            if (audioPausedByManager)
                AudioManager.ExistingInstance?.SetGameplayAudioPaused(false);
            else
                AudioListener.pause = previousAudioPause;
            audioPausedByManager = false;
            Time.timeScale = previousTimeScale;

            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
        }

        private void ShowPausePanel()
        {
            optionsPanel.Hide();
            slotPanel.SetActive(false);
            panel.SetActive(true);
        }

        private void ShowOptions()
        {
            panel.SetActive(false);
            optionsPanel.Show();
        }

        private void ShowSlots(bool saves)
        {
            slotPanelSaves = saves;
            slotHeader.text = saves ? "SAVE GAME" : "LOAD GAME";
            SetDeleteMode(false);
            panel.SetActive(false);
            slotPanel.SetActive(true);
        }

        private void RefreshSlots()
        {
            foreach (var (number, button, label) in slots)
            {
                string level = LevelProgress.Slot(number);
                button.interactable = deleteMode ? !string.IsNullOrEmpty(level)
                    : slotPanelSaves ? LevelProgress.CurrentLevel != null : LevelProgress.CanLoad(level);
                Color color = GameSettings.Current.screenStyle.textColor;
                if (!button.interactable)
                    color.a *= 0.45f;
                label.color = color;
                label.text = $"SLOT {number:D2}  —  {(string.IsNullOrEmpty(level) ? "EMPTY" : level.ToUpperInvariant())}";
            }
        }

        private void SetDeleteMode(bool active)
        {
            deleteMode = active;
            pendingDelete = -1;
            pendingDeleteAll = false;
            deleteLabel.text = active ? "CANCEL DELETE" : "DELETE";
            slotStatus.text = active ? "DELETE MODE - CLICK A SLOT TWICE TO ERASE IT."
                : slotPanelSaves ? "CHOOSE A SLOT TO SAVE THE CURRENT LEVEL."
                : "CHOOSE A SLOT TO LOAD. CURRENT PROGRESS WILL BE REPLACED.";
            RefreshSlots();
        }

        private void OnDeleteAll()
        {
            pendingDelete = -1;
            if (!pendingDeleteAll)
            {
                pendingDeleteAll = true;
                slotStatus.text = "CLICK DELETE ALL SAVES AGAIN TO ERASE EVERY SLOT AND THE AUTOSAVE.";
                return;
            }

            LevelProgress.DeleteAll();
            deleteMode = false;
            pendingDeleteAll = false;
            deleteLabel.text = "DELETE";
            slotStatus.text = "ALL SAVES DELETED.";
            RefreshSlots();
        }

        private void OnSlot(int number)
        {
            if (deleteMode)
            {
                pendingDeleteAll = false;
                if (pendingDelete != number)
                {
                    pendingDelete = number;
                    slotStatus.text = $"CLICK SLOT {number:D2} AGAIN TO DELETE IT.";
                    return;
                }

                pendingDelete = -1;
                LevelProgress.DeleteSlot(number);
                slotStatus.text = $"SLOT {number:D2} DELETED.";
                RefreshSlots();
                return;
            }

            if (slotPanelSaves)
            {
                LevelProgress.SaveSlot(number, LevelProgress.CurrentLevel);
                slotStatus.text = $"SLOT {number:D2} SAVED.";
                RefreshSlots();
                return;
            }

            string level = LevelProgress.Slot(number);
            if (!LevelProgress.CanLoad(level))
                return;

            Resume();
            SceneManager.LoadScene(level);
        }

        private void OnMainMenu()
        {
            Resume();
            SceneManager.LoadScene("MainMenu");
        }

        private static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("Pause Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreatePanel(canvas.transform, "PausePanel");
            CreateText(panel.transform, "Header", 64f, new Vector2(0f, 300f), new Vector2(560f, 90f)).text = "PAUSED";

            var entries = new (string label, UnityAction action)[]
            {
                ("CONTINUE", Resume),
                ("SAVE", () => ShowSlots(true)),
                ("LOAD", () => ShowSlots(false)),
                ("OPTIONS", ShowOptions),
                ("MAIN MENU", OnMainMenu),
                ("QUIT", OnQuit)
            };
            for (int i = 0; i < entries.Length; i++)
                CreateButton(panel.transform, entries[i].label, new Vector2(0f, 150f - i * 90f), new Vector2(420f, 72f), 30f, entries[i].action);

            slotPanel = CreatePanel(canvas.transform, "SlotPanel");
            slotHeader = CreateText(slotPanel.transform, "Header", 56f, new Vector2(0f, 330f), new Vector2(760f, 80f));
            for (int i = 0; i < LevelProgress.SlotCount; i++)
            {
                int number = i + 1;
                Vector2 position = new Vector2(i % 2 == 0 ? -230f : 230f, 205f - i / 2 * 82f);
                TMP_Text label = CreateButton(slotPanel.transform, $"Slot {number:D2}", position, new Vector2(410f, 62f), 22f, () => OnSlot(number));
                slots.Add((number, label.GetComponentInParent<Button>(), label));
            }
            slotStatus = CreateText(slotPanel.transform, "Status", 22f, new Vector2(0f, -235f), new Vector2(820f, 70f));
            CreateButton(slotPanel.transform, "BACK", new Vector2(-320f, -310f), new Vector2(300f, 62f), 26f, ShowPausePanel);
            deleteLabel = CreateButton(slotPanel.transform, "DELETE", new Vector2(0f, -310f), new Vector2(300f, 62f), 26f, () => SetDeleteMode(!deleteMode));
            CreateButton(slotPanel.transform, "DELETE ALL SAVES", new Vector2(320f, -310f), new Vector2(300f, 62f), 26f, OnDeleteAll);

            optionsPanel = new OptionsPanel(canvas.transform, font, font, ShowPausePanel);

            panel.SetActive(false);
            slotPanel.SetActive(false);
            optionsPanel.Hide();

            Texture2D frame = cornice != null ? cornice : Resources.Load<Texture2D>("MainMenu/Cornice");
            if (frame != null)
            {
                corniceOverlay = new GameObject("Cornice", typeof(RectTransform), typeof(RawImage));
                corniceOverlay.transform.SetParent(canvas.transform, false);
                Stretch((RectTransform)corniceOverlay.transform);
                RawImage image = corniceOverlay.GetComponent<RawImage>();
                image.texture = frame;
                image.raycastTarget = false;
                corniceOverlay.SetActive(false);
            }
        }

        private void ApplyStyle()
        {
            TextStyle style = GameSettings.Current.screenStyle;
            foreach (var (text, size) in styledTexts)
            {
                text.fontSize = size * style.scale;
                TextStyling.Apply(text, style, style.textColor);
            }
            slotStatus.color = style.accentColor;
            buttonImages.ForEach(image => image.color = style.backgroundColor);
            RefreshSlots();
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            Stretch((RectTransform)panelObject.transform);
            panelObject.GetComponent<Image>().color = new Color(0.015f, 0.013f, 0.011f, 0.88f);
            return panelObject;
        }

        private TMP_Text CreateButton(Transform parent, string label, Vector2 position, Vector2 size, float fontSize, UnityAction action)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Place((RectTransform)buttonObject.transform, position, size);
            buttonImages.Add(buttonObject.GetComponent<Image>());

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            colors.disabledColor = new Color(0.34f, 0.32f, 0.28f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text text = CreateText(buttonObject.transform, "Label", fontSize, Vector2.zero, size - new Vector2(20f, 12f));
            text.text = label;
            return text;
        }

        private TMP_Text CreateText(Transform parent, string name, float size, Vector2 position, Vector2 sizeDelta)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            Place((RectTransform)textObject.transform, position, sizeDelta);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            styledTexts.Add((text, size));
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
