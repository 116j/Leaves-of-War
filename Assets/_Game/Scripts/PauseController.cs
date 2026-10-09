using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Owns the in-game pause menu and the pause state itself.
    ///
    /// Pausing is the coordination of several systems, because different parts of
    /// the game run on different clocks:
    ///   - Time.timeScale = 0 freezes everything on scaled time: player movement,
    ///     physics, and character animations.
    ///   - AudioListener.pause = true suspends ALL audio at once (voice, music,
    ///     SFX) regardless of how each source is routed through the mixer.
    ///   - NarrativeClock.SetPaused(true) freezes the dialogue coroutines, which
    ///     run on unscaled time and would otherwise keep crawling text and firing
    ///     beat timers straight through a timeScale pause.
    ///   - GameSession.PushPlayerLock() blocks player input/interaction, reusing
    ///     the lock the controller already honours.
    ///
    /// The pause key is the "Pause" action on the live player's action map (default
    /// Escape, separately rebindable in OPTIONS &gt; KEY BINDINGS). Reading it here,
    /// rather than in FirstPersonController, means it keeps working while the player
    /// is input-locked during the pause.
    ///
    /// Esc priority: if a narrative panel is open (document/card/choice), the pause
    /// does NOT open - that panel handles Escape itself. Pause opens only when the
    /// game is in free play (nothing narrative capturing input).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseController : MonoBehaviour
    {
        public static PauseController Instance { get; private set; }

        /// <summary>
        /// Guarantees a PauseController exists in the scene, creating one if
        /// needed. Safe to call repeatedly; a no-op once one is already present.
        /// </summary>
        public static PauseController EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var pauseObject = new GameObject("Pause Controller");
            return pauseObject.AddComponent<PauseController>();
        }

        public bool IsPaused { get; private set; }

        private Canvas canvas;
        private GameObject panel;
        private TMP_FontAsset menuFont;
        private TMP_FontAsset authorialFont;
        private OptionsPanel optionsPanel;
        private GameObject manualSavePanel;
        private GameObject manualLoadPanel;
        private TMP_Text manualSaveStatus;
        private TMP_Text manualLoadStatus;
        private readonly List<ManualSlotButton> manualSaveSlots = new List<ManualSlotButton>();
        private readonly List<ManualSlotButton> manualLoadSlots = new List<ManualSlotButton>();
        private FirstPersonController boundPlayer;
        private InputAction pauseAction;

        // Delete mode: while active, clicking a slot button deletes that
        // slot instead of loading/saving it. Shared between the SAVE and
        // LOAD panels since they operate on the same underlying files - only
        // one panel is ever visible at a time in practice.
        private bool deleteModeActive;
        private int pendingDeleteSlot = -1; // -1 = nothing pending confirmation.
        private bool pendingDeleteAll;
        private TMP_Text saveDeleteModeButtonLabel;
        private TMP_Text loadDeleteModeButtonLabel;

        [SerializeField] private Texture2D cornice;

        // The cornice is a sibling of PausePanel (parented directly under the
        // canvas), not a child of it - it needs to stay visible whether we're
        // showing PAUSED or OPTIONS, and those two are separate sibling panels
        // that toggle independently. Its own visibility instead follows
        // IsPaused as a whole (see Pause()/Resume()).
        private GameObject corniceOverlay;

        // Cached so we can restore the exact prior state on resume rather than
        // assuming defaults (another system may legitimately own timeScale).
        private float previousTimeScale = 1f;
        private bool previousAudioPause;
        private bool audioPauseOwnedByManager;
        private bool pushedPlayerLock;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            menuFont = Resources.Load<TMP_FontAsset>("Fonts/Gotfridus")
                ?? Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond")
                ?? TMP_Settings.defaultFontAsset;
            authorialFont = Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond") ?? menuFont;

            BuildUi();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            EnsurePauseActionBound();

            if (pauseAction != null && pauseAction.WasPressedThisFrame())
                HandlePauseKey();
        }

        /// <summary>
        /// Keeps a reference to the current player's Pause action. The player is
        /// rebuilt on scene loads, so we re-resolve it whenever it goes missing.
        /// </summary>
        private void EnsurePauseActionBound()
        {
            if (boundPlayer != null && pauseAction != null)
                return;

            boundPlayer = FindAnyObjectByType<FirstPersonController>();
            pauseAction = boundPlayer != null ? boundPlayer.PauseAction : null;
        }

        private void HandlePauseKey()
        {
            if (IsPaused)
            {
                Resume();
                return;
            }

            // Documents now close with their own dedicated key (see
            // FirstPersonController.CloseDocumentAction), not Escape, so
            // Escape always opens Pause - including while a document, dialogue,
            // card, or choice is on screen.
            if (boundPlayer == null)
                return;

            Pause();
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;

            previousTimeScale = Time.timeScale;

            Time.timeScale = 0f;
            AudioManager audioManager = AudioManager.Instance;
            audioPauseOwnedByManager = audioManager != null;
            if (audioPauseOwnedByManager)
            {
                audioManager.SetGameplayAudioPaused(true);
            }
            else
            {
                previousAudioPause = AudioListener.pause;
                AudioListener.pause = true;
            }
            NarrativeClock.SetPaused(true);
            GameSession.Instance?.SetSequencePaused(true);

            if (GameSession.Instance != null)
            {
                GameSession.Instance.PushPlayerLock();
                pushedPlayerLock = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            panel.SetActive(true);
            canvas.sortingOrder = 31000;
            corniceOverlay?.SetActive(true);
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;

            panel.SetActive(false);
            optionsPanel?.Hide();
            manualSavePanel?.SetActive(false);
            manualLoadPanel?.SetActive(false);
            corniceOverlay?.SetActive(false);

            NarrativeClock.SetPaused(false);
            GameSession.Instance?.SetSequencePaused(false);
            if (audioPauseOwnedByManager)
                AudioManager.ExistingInstance?.SetGameplayAudioPaused(false);
            else
                AudioListener.pause = previousAudioPause;
            audioPauseOwnedByManager = false;
            Time.timeScale = previousTimeScale;

            if (pushedPlayerLock && GameSession.Instance != null)
                GameSession.Instance.PopPlayerLock();
            pushedPlayerLock = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnContinue() => Resume();

        private void OnOptions()
        {
            panel.SetActive(false);
            optionsPanel.Show();
        }

        private void OnSave()
        {
            panel.SetActive(false);
            manualLoadPanel.SetActive(false);
            RefreshManualSlotButtons(manualSaveSlots, loadMode: false);
            manualSaveStatus.text = GameSession.Instance?.CanSaveManually == true
                ? "CHOOSE A SLOT TO SAVE YOUR CURRENT PROGRESS."
                : "SAVING IS UNAVAILABLE DURING AN ENDING.";
            manualSavePanel.SetActive(true);
        }

        private void OnLoad()
        {
            panel.SetActive(false);
            manualSavePanel.SetActive(false);
            RefreshManualSlotButtons(manualLoadSlots, loadMode: true);
            manualLoadStatus.text = "CHOOSE A SLOT TO LOAD. CURRENT PROGRESS WILL BE REPLACED.";
            manualLoadPanel.SetActive(true);
        }

        private void ShowPausePanel()
        {
            optionsPanel.Hide();
            manualSavePanel?.SetActive(false);
            manualLoadPanel?.SetActive(false);
            panel.SetActive(true);
        }

        private void OnQuit()
        {
            // QUIT exits the application, per the chosen behaviour.
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnMainMenu()
        {
            // Resume() restores everything Pause() touched (timeScale, audio
            // pause, the sequence clock, the player lock, cursor state) - the
            // same cleanup needed before leaving to the menu, not just before
            // returning to play.
            Resume();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ------------------------------------------------------------------
        //  UI construction (built once, toggled on pause)
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            var canvasObject = new GameObject(
                "Pause Menu",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen dimmer + container.
            panel = CreatePanel(
                canvas.transform,
                "PausePanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.015f, 0.013f, 0.011f, 0.86f));

            TMP_Text header = CreateText(
                panel.transform, "Header", 64f,
                new Vector2(0f, 300f), new Vector2(560f, 90f),
                new Color(0.88f, 0.84f, 0.72f));
            header.text = "PAUSED";

            CreateMenuButton("CONTINUE", 150f, OnContinue, enabled: true);
            CreateMenuButton("SAVE", 60f, OnSave, enabled: true);
            CreateMenuButton("LOAD", -30f, OnLoad, enabled: true);
            CreateMenuButton("OPTIONS", -120f, OnOptions, enabled: true);
            CreateMenuButton("MAIN MENU", -210f, OnMainMenu, enabled: true);
            CreateMenuButton("QUIT", -300f, OnQuit, enabled: true);

            // Same OptionsPanel the main menu uses. BACK re-shows this pause panel.
            optionsPanel = new OptionsPanel(canvas.transform, menuFont, authorialFont, ShowPausePanel);
            manualSavePanel = BuildManualSlotPanel("SAVE GAME", false, manualSaveSlots, out manualSaveStatus);
            manualLoadPanel = BuildManualSlotPanel("LOAD GAME", true, manualLoadSlots, out manualLoadStatus);

            panel.SetActive(false);
            manualSavePanel.SetActive(false);
            manualLoadPanel.SetActive(false);

            // Parented under the canvas (not under panel) and drawn last among
            // the canvas's own children, so it sits above BOTH PausePanel and
            // OptionsPanel regardless of which one is currently active.
            corniceOverlay = CreateCorniceOverlay(canvas.transform, ResolveCorniceTexture());
            corniceOverlay?.SetActive(false);
        }

        private void CreateMenuButton(string label, float y, UnityEngine.Events.UnityAction action, bool enabled)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 72f);
            rect.anchoredPosition = new Vector2(0f, y);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.115f, 0.1f, 0.075f, 0.96f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            colors.disabledColor = new Color(0.34f, 0.32f, 0.28f, 0.6f);
            button.colors = colors;
            button.interactable = enabled;
            if (enabled && action != null)
                button.onClick.AddListener(action);

            Color textColor = enabled
                ? new Color(0.9f, 0.86f, 0.74f)
                : new Color(0.55f, 0.53f, 0.47f);
            TMP_Text text = CreateText(
                buttonObject.transform, "Label", 30f,
                Vector2.zero, new Vector2(400f, 60f),
                textColor);
            text.text = enabled ? label : label + "  (SOON)";
        }

        private GameObject BuildManualSlotPanel(
            string title,
            bool loadMode,
            List<ManualSlotButton> slots,
            out TMP_Text statusText)
        {
            GameObject slotPanel = CreatePanel(
                canvas.transform,
                title + " Panel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.015f, 0.013f, 0.011f, 0.9f));

            TMP_Text header = CreateText(
                slotPanel.transform, "Header", 56f,
                new Vector2(0f, 330f), new Vector2(760f, 80f),
                new Color(0.88f, 0.84f, 0.72f));
            header.text = title;

            for (int index = 0; index < SaveGameStore.ManualSlotCount; index++)
            {
                int slotNumber = index + 1;
                float x = index % 2 == 0 ? -230f : 230f;
                float y = 205f - ((index / 2) * 82f);
                ManualSlotButton slot = CreateManualSlotButton(
                    slotPanel.transform,
                    slotNumber,
                    x,
                    y,
                    () => OnManualSlotSelected(slotNumber, loadMode));
                slots.Add(slot);
            }

            statusText = CreateText(
                slotPanel.transform, "Status", 22f,
                new Vector2(0f, -235f), new Vector2(820f, 70f),
                new Color(0.73f, 0.69f, 0.57f));
            statusText.enableWordWrapping = true;

            // I tre pulsanti stanno tutti sulla stessa riga (bottomRowY), subito
            // sotto lo status text (che finisce a circa y=-270) e ben sopra la
            // decorazione floreale della cornice in basso - prima erano su due
            // altezze diverse e finivano per sovrapporsi tra loro.
            const float bottomRowY = -310f;

            CreateSubmenuButton(slotPanel.transform, "BACK", bottomRowY, () =>
            {
                ExitDeleteModeSilently(loadMode);
                ShowPausePanel();
            }, x: -320f);

            TMP_Text deleteModeLabel = CreateSubmenuButtonWithLabel(
                slotPanel.transform, "DELETE", 0f, bottomRowY, () => ToggleDeleteMode(loadMode));
            if (loadMode)
                loadDeleteModeButtonLabel = deleteModeLabel;
            else
                saveDeleteModeButtonLabel = deleteModeLabel;

            CreateSubmenuButton(slotPanel.transform, "DELETE ALL SAVES", bottomRowY, () => OnDeleteAllSaves(loadMode), x: 320f);

            return slotPanel;
        }

        /// <summary>
        /// Silently drops delete mode and any pending confirmation without
        /// touching status text - used when leaving the panel entirely via
        /// BACK, where the delete-mode messaging would be stale/misleading
        /// the next time the panel opens.
        /// </summary>
        private void ExitDeleteModeSilently(bool loadMode)
        {
            deleteModeActive = false;
            pendingDeleteSlot = -1;
            pendingDeleteAll = false;

            TMP_Text label = loadMode ? loadDeleteModeButtonLabel : saveDeleteModeButtonLabel;
            if (label != null)
                label.text = "DELETE";
        }

        private ManualSlotButton CreateManualSlotButton(
            Transform parent,
            int slotNumber,
            float x,
            float y,
            UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(
                $"Slot {slotNumber:D2}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(410f, 62f);
            rect.anchoredPosition = new Vector2(x, y);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.115f, 0.1f, 0.075f, 0.96f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            colors.disabledColor = new Color(0.34f, 0.32f, 0.28f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text label = CreateText(
                buttonObject.transform, "Label", 22f,
                Vector2.zero, new Vector2(390f, 50f),
                new Color(0.9f, 0.86f, 0.74f));
            return new ManualSlotButton(slotNumber, button, label);
        }

        private void CreateSubmenuButton(
            Transform parent,
            string label,
            float y,
            UnityEngine.Events.UnityAction action,
            float x = 0f)
        {
            CreateSubmenuButtonWithLabel(parent, label, x, y, action);
        }

        private TMP_Text CreateSubmenuButtonWithLabel(
            Transform parent,
            string label,
            float x,
            float y,
            UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 62f);
            rect.anchoredPosition = new Vector2(x, y);
            buttonObject.GetComponent<Image>().color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            buttonObject.GetComponent<Button>().onClick.AddListener(action);

            TMP_Text text = CreateText(
                buttonObject.transform, "Label", 26f,
                Vector2.zero, new Vector2(280f, 50f),
                new Color(0.9f, 0.86f, 0.74f));
            text.text = label;
            return text;
        }

        private void RefreshManualSlotButtons(List<ManualSlotButton> slots, bool loadMode)
        {
            GameSession session = GameSession.Instance;
            for (int i = 0; i < slots.Count; i++)
            {
                ManualSlotButton slot = slots[i];
                SaveReadStatus readStatus = loadMode
                    ? SaveGameStore.GetManualSlotReadOnlyLoadStatus(
                        slot.SlotNumber,
                        session != null ? session.Catalog : null,
                        out _)
                    : SaveGameStore.GetManualSlotReadStatus(slot.SlotNumber);
                bool usable = readStatus == SaveReadStatus.Valid ||
                    readStatus == SaveReadStatus.RecoverableBackup;
                slot.Button.interactable = loadMode
                    ? usable && session?.CanLoadSavedGame == true
                    : session?.CanSaveManually == true;
                slot.Label.color = slot.Button.interactable
                    ? new Color(0.9f, 0.86f, 0.74f)
                    : new Color(0.55f, 0.53f, 0.47f);
                slot.Label.text = $"SLOT {slot.SlotNumber:D2}  —  {SlotStatusLabel(readStatus)}";
            }
        }

        private void OnManualSlotSelected(int slotNumber, bool loadMode)
        {
            GameSession session = GameSession.Instance;
            TMP_Text statusText = loadMode ? manualLoadStatus : manualSaveStatus;

            if (deleteModeActive)
            {
                HandleDeleteSlotClick(slotNumber, loadMode, statusText);
                return;
            }

            if (session == null)
            {
                statusText.text = "NO ACTIVE GAME.";
                return;
            }

            if (!loadMode)
            {
                if (!session.CanSaveManually)
                {
                    statusText.text = "SAVING IS UNAVAILABLE DURING AN ENDING.";
                    RefreshManualSlotButtons(manualSaveSlots, loadMode: false);
                    return;
                }

                statusText.text = session.SaveManualSlot(slotNumber) == SaveWriteStatus.Succeeded
                    ? $"SLOT {slotNumber:D2} SAVED."
                    : "SAVE FAILED. PLEASE TRY AGAIN.";
                RefreshManualSlotButtons(manualSaveSlots, loadMode: false);
                return;
            }

            if (session.LoadManualSlot(slotNumber, out SaveReadStatus readStatus))
            {
                // Matches the main-menu Continue flow: drop straight into
                // gameplay instead of leaving the load panel open waiting
                // for a manual "Continue" click.
                Resume();
                return;
            }

            statusText.text = $"SLOT {slotNumber:D2}: {SlotStatusLabel(readStatus)}.";
            RefreshManualSlotButtons(manualLoadSlots, loadMode: true);
        }

        /// <summary>
        /// First click on a slot while in delete mode asks for confirmation
        /// (updates the status text, doesn't touch any file yet); a second
        /// click on the SAME slot actually deletes it. Clicking a DIFFERENT
        /// slot, or leaving delete mode, cancels the pending confirmation
        /// without deleting anything.
        /// </summary>
        private void HandleDeleteSlotClick(int slotNumber, bool loadMode, TMP_Text statusText)
        {
            List<ManualSlotButton> slots = loadMode ? manualLoadSlots : manualSaveSlots;

            if (pendingDeleteSlot != slotNumber)
            {
                pendingDeleteSlot = slotNumber;
                statusText.text = $"CLICK SLOT {slotNumber:D2} AGAIN TO PERMANENTLY DELETE IT.";
                return;
            }

            pendingDeleteSlot = -1;
            SaveWriteStatus result = SaveGameStore.TryDiscardManualSlot(slotNumber);
            statusText.text = result == SaveWriteStatus.Succeeded
                ? $"SLOT {slotNumber:D2} DELETED."
                : $"COULD NOT DELETE SLOT {slotNumber:D2}. TRY AGAIN.";
            RefreshManualSlotButtons(slots, loadMode);
        }

        /// <summary>
        /// Toggles delete mode for both slot panels at once (they share the
        /// same underlying files). Always clears any pending single-slot or
        /// delete-all confirmation when toggled, so leaving delete mode
        /// never leaves a stale "click again" state behind.
        /// </summary>
        private void ToggleDeleteMode(bool loadMode)
        {
            deleteModeActive = !deleteModeActive;
            pendingDeleteSlot = -1;
            pendingDeleteAll = false;

            TMP_Text label = loadMode ? loadDeleteModeButtonLabel : saveDeleteModeButtonLabel;
            if (label != null)
                label.text = deleteModeActive ? "CANCEL DELETE" : "DELETE";

            TMP_Text statusText = loadMode ? manualLoadStatus : manualSaveStatus;
            statusText.text = deleteModeActive
                ? "DELETE MODE - CLICK A SLOT TWICE TO ERASE IT, OR CLICK DELETE ALL SAVES."
                : (loadMode
                    ? "CHOOSE A SLOT TO LOAD. CURRENT PROGRESS WILL BE REPLACED."
                    : "CHOOSE A SLOT TO SAVE YOUR CURRENT PROGRESS.");
        }

        /// <summary>
        /// First click asks for confirmation; a second click wipes the
        /// autosave AND every manual slot. Any click elsewhere (a slot, or
        /// toggling delete mode) cancels the pending confirmation first via
        /// ToggleDeleteMode/HandleDeleteSlotClick already clearing it.
        /// </summary>
        private void OnDeleteAllSaves(bool loadMode)
        {
            TMP_Text statusText = loadMode ? manualLoadStatus : manualSaveStatus;

            if (!pendingDeleteAll)
            {
                pendingDeleteAll = true;
                statusText.text = "CLICK DELETE ALL SAVES AGAIN TO PERMANENTLY ERASE EVERY SLOT AND THE AUTOSAVE.";
                return;
            }

            pendingDeleteAll = false;
            SaveWriteStatus result = SaveGameStore.TryDiscardEverything();
            statusText.text = result == SaveWriteStatus.Succeeded
                ? "ALL SAVES DELETED."
                : "COULD NOT DELETE SOME SAVES. TRY AGAIN.";
            RefreshManualSlotButtons(manualSaveSlots, loadMode: false);
            RefreshManualSlotButtons(manualLoadSlots, loadMode: true);
        }

        private static string SlotStatusLabel(SaveReadStatus status)
        {
            switch (status)
            {
                case SaveReadStatus.Valid:
                    return "SAVED";
                case SaveReadStatus.RecoverableBackup:
                    return "RECOVERABLE";
                case SaveReadStatus.Missing:
                    return "EMPTY";
                case SaveReadStatus.Invalid:
                    return "INVALID";
                case SaveReadStatus.TransientFailure:
                    return "UNAVAILABLE";
                default:
                    return "UNAVAILABLE";
            }
        }

        private sealed class ManualSlotButton
        {
            public ManualSlotButton(int slotNumber, Button button, TMP_Text label)
            {
                SlotNumber = slotNumber;
                Button = button;
                Label = label;
            }

            public int SlotNumber { get; }
            public Button Button { get; }
            public TMP_Text Label { get; }
        }

        private GameObject CreatePanel(
            Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panelObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panelObject.GetComponent<Image>().color = color;
            return panelObject;
        }

        private TMP_Text CreateText(
            Transform parent, string name, float size,
            Vector2 anchoredPosition, Vector2 sizeDelta,
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = menuFont;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateCorniceOverlay(Transform parent, Texture2D texture)
        {
            if (texture == null)
                return null;

            var corniceObject = new GameObject("Cornice", typeof(RectTransform), typeof(RawImage));
            corniceObject.transform.SetParent(parent, false);
            corniceObject.transform.SetAsLastSibling(); // sopra a tutto il resto del canvas

            RectTransform rect = (RectTransform)corniceObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = corniceObject.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false; // decorativa, non deve bloccare i click sui bottoni sotto
            return corniceObject;
        }

        private Texture2D ResolveCorniceTexture() =>
            cornice != null ? cornice : Resources.Load<Texture2D>("MainMenu/Cornice");
    }
}