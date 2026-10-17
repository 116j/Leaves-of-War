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
        private MenuLook look;
        private GameObject backdrop;
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
        private readonly List<Button> panelButtons = new List<Button>();
        private Transform canvasRoot;
        private float nextOptionsStyle;

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
            look = MenuLook.Current;
            font = look.PanelFont ?? Resources.Load<TMP_FontAsset>(FontPath) ?? TMP_Settings.defaultFontAsset;
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
            if (IsPaused && optionsPanel != null && !panel.activeSelf && !slotPanel.activeSelf && Time.unscaledTime >= nextOptionsStyle)
            {
                nextOptionsStyle = Time.unscaledTime + 0.2f;
                foreach (TMP_Text text in canvasRoot.GetComponentsInChildren<TMP_Text>(true))
                    if (!text.transform.IsChildOf(panel.transform))
                        look.StylePanelText(text);
            }
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

            backdrop.SetActive(true);
            panel.SetActive(true);
            corniceOverlay?.SetActive(true);
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            backdrop.SetActive(false);
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

        private void BackToPausePanel()
        {
            look.PlayBackSound();
            ShowPausePanel();
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
                Color color = look.panelTextColor;
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
            canvasRoot = canvas.transform;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            backdrop = CreateFloor(canvas.transform);

            panel = CreateLayer(canvas.transform, "PausePanel");
            TMP_Text header = CreateText(panel.transform, "Header", 140f, new Vector2(0f, 320f), new Vector2(1400f, 240f));
            header.text = "Paused";
            look.StyleEntryText(header, false);

            var entries = new List<(string label, UnityAction action)>
            {
                ("Continue", Resume),
                ("Save Game", () => ShowSlots(true)),
                ("Load Game", () => ShowSlots(false)),
                ("Options", ShowOptions),
                ("Main Menu", OnMainMenu)
            };
#if !UNITY_WEBGL || UNITY_EDITOR
            entries.Add(("Quit", OnQuit));
#endif
            float top = 40f;
            for (int i = 0; i < entries.Count; i++)
            {
                UnityAction chosen = entries[i].action;
                GameMenuEntry.Create(panel.transform, entries[i].label, new Vector2(0f, top - i * look.entrySpacing), () => chosen());
            }

            slotPanel = CreateLayer(canvas.transform, "SlotPanel");
            RectTransform box = CreateBox(slotPanel.transform, new Vector2(1360f, 900f));
            slotHeader = CreateText(box, "Header", 52f, new Vector2(0f, 360f), new Vector2(1000f, 72f));
            slotHeader.color = look.panelHeaderColor;
            for (int i = 0; i < LevelProgress.SlotCount; i++)
            {
                int number = i + 1;
                Vector2 position = new Vector2(i % 2 == 0 ? -245f : 245f, 250f - i / 2 * 70f);
                TMP_Text label = CreateButton(box, $"Slot {number:D2}", position, new Vector2(440f, 58f), 23f, () => OnSlot(number));
                slots.Add((number, label.GetComponentInParent<Button>(), label));
            }
            slotStatus = CreateText(box, "Status", 20f, new Vector2(0f, -240f), new Vector2(1120f, 62f));
            CreateButton(box, "BACK", new Vector2(-330f, -350f), new Vector2(300f, 58f), 23f, BackToPausePanel, false);
            deleteLabel = CreateButton(box, "DELETE", new Vector2(0f, -350f), new Vector2(300f, 58f), 23f, () => SetDeleteMode(!deleteMode));
            CreateButton(box, "DELETE ALL SAVES", new Vector2(330f, -350f), new Vector2(300f, 58f), 23f, OnDeleteAll);

            optionsPanel = new OptionsPanel(canvas.transform, font, font, BackToPausePanel);

            backdrop.SetActive(false);
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
            slotStatus.color = look.panelStatusColor;
            panelButtons.ForEach(look.StylePanelButton);
            RefreshSlots();
        }

        private GameObject CreateFloor(Transform parent)
        {
            var floorObject = new GameObject("Floor", typeof(RectTransform), typeof(RawImage));
            floorObject.transform.SetParent(parent, false);
            Stretch((RectTransform)floorObject.transform);
            RawImage floor = floorObject.GetComponent<RawImage>();
            floor.raycastTarget = true;
            Texture2D texture = look.FloorTexture;
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                floor.texture = texture;
                floor.uvRect = new Rect(0f, 0f, look.floorTiling, look.floorTiling * 9f / 16f);
                floor.color = Color.white;
            }
            else
            {
                floor.color = look.fallbackFloorColor;
            }
            return floorObject;
        }

        private static GameObject CreateLayer(Transform parent, string name)
        {
            var layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            Stretch((RectTransform)layer.transform);
            return layer;
        }

        private RectTransform CreateBox(Transform parent, Vector2 size)
        {
            var boxObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(parent, false);
            var rect = (RectTransform)boxObject.transform;
            Place(rect, Vector2.zero, size);
            boxObject.GetComponent<Image>().color = look.panelColor;
            return rect;
        }

        private TMP_Text CreateButton(Transform parent, string label, Vector2 position, Vector2 size, float fontSize, UnityAction action, bool clickSound = true)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Place((RectTransform)buttonObject.transform, position, size);

            Button button = buttonObject.GetComponent<Button>();
            look.StylePanelButton(button);
            panelButtons.Add(button);
            if (clickSound)
                button.onClick.AddListener(() => look.PlayEntrySound(2));
            button.onClick.AddListener(action);

            TMP_Text text = CreateText(buttonObject.transform, "Label", fontSize, Vector2.zero, size - new Vector2(30f, 12f));
            text.color = look.panelTextColor;
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
            look.StylePanelText(text);
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
