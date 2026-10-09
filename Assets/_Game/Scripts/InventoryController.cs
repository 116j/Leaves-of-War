using System.Collections.Generic;
using Hortensia.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Owns the whole inventory feature end to end:
    ///
    /// - Starts LOCKED (no prompt, Open Inventory key does nothing).
    /// - Call <see cref="Unlock"/> once (e.g. at the end of the intro dialogue)
    ///   to show a "PRESS [KEY] TO OPEN INVENTORY" prompt.
    /// - Pressing Open Inventory shows an icon grid of everything in the
    ///   player's <see cref="InventoryHolder"/>. Arrow keys/mouse navigate,
    ///   Interact/click selects.
    /// - Selecting a document item plays its Open Sound (e.g. an envelope
    ///   opening) then presents it via the normal document reader. Selecting
    ///   a generic item shows its description in a side panel instead.
    /// - Close Document closes the grid back to gameplay.
    ///
    /// Attach to any persistent object (e.g. alongside SettingsApplier) in a
    /// gameplay scene. Finds the player/InventoryHolder itself; no manual
    /// wiring needed beyond calling Unlock() at the right story moment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryController : MonoBehaviour
    {
        [Tooltip("Grid cell size in pixels, at the UI's own reference resolution.")]
        [SerializeField] private Vector2 cellSize = new Vector2(500f, 500f);
        [Tooltip("Spacing between grid cells.")]
        [SerializeField] private Vector2 cellSpacing = new Vector2(40f, 40f);
        [Tooltip("Decorative frame drawn over the inventory grid. Falls back to Resources/MainMenu/Cornice if left empty, same as PauseController.")]
        [SerializeField] private Texture2D cornice;

        private bool unlocked;
        private bool promptDismissed;
        private bool isOpen;

        // While true, the "PRESS [KEY] TO OPEN INVENTORY" prompt stays
        // hidden even if otherwise eligible - set from ChapterRunner's
        // LineStarted/LineFinished events so it doesn't overlap the
        // subtitle text during a spoken line.
        private bool dialogueActive;

        private GameSessionRegistration sessionRegistration;
        private GameSession boundSession;
        private GameSession playerLockOwner;
        private global::FirstPersonController cachedPlayer;
        private InventoryHolder inventoryHolder;
        private AudioSource audioSource;

        private TMP_FontAsset menuFont;
        private TMP_FontAsset authorialFont;

        private GameObject promptObject;
        private TMP_Text promptText;

        private GameObject gridPanel;
        private GameObject corniceOverlay;
        private RectTransform gridContent;
        private TMP_Text detailTitleText;
        private TMP_Text detailBodyText;
        private readonly List<GameObject> spawnedCells = new List<GameObject>();

        /// <summary>Call once the moment the player should be told about the inventory (e.g. after the intro dialogue).</summary>
        public void Unlock()
        {
            GameSession session = boundSession ?? GameSession.Instance;
            if (session != null && session.State != null)
            {
                session.UnlockInventory();
                unlocked = session.State.Inventory.IsUnlocked;
                return;
            }

            unlocked = true;
        }

        private void OnEnable()
        {
            ChapterRunner.UnlockInventoryRequested += Unlock;
            ChapterRunner.GiveItemRequested += HandleGiveItemRequested;
            ChapterRunner.LineStarted += HandleLineStarted;
            ChapterRunner.LineFinished += HandleLineFinished;
            sessionRegistration?.Enable();
        }

        private void OnDisable()
        {
            ChapterRunner.UnlockInventoryRequested -= Unlock;
            ChapterRunner.GiveItemRequested -= HandleGiveItemRequested;
            ChapterRunner.LineStarted -= HandleLineStarted;
            ChapterRunner.LineFinished -= HandleLineFinished;
            sessionRegistration?.Disable();
            UnbindPlayer();

            if (isOpen)
                CloseGrid();
        }

        private void HandleLineStarted(SpeakerDefinition speaker) => dialogueActive = true;

        private void HandleLineFinished(SpeakerDefinition speaker) => dialogueActive = false;

        /// <summary>
        /// Looks up an InventoryItemDefinition by its ItemId (loaded from
        /// Resources/Inventory Items, cached once) and grants it to the
        /// player. Triggered by the [give item:ID] inline dialogue tag.
        /// </summary>
        private void HandleGiveItemRequested(string itemId)
        {
            InventoryItemDefinition item = FindItemById(itemId);
            if (item == null)
            {
                Debug.LogWarning(
                    $"[give item:{itemId}] used in dialogue, but no InventoryItemDefinition with that " +
                    "ItemId was found under a Resources/Inventory Items folder.");
                return;
            }

            (boundSession ?? GameSession.Instance)?.GiveInventoryItem(item.ItemId);

            if (!TryGetPlayer())
                return;

            inventoryHolder.AddItem(item);
        }

        private InventoryItemDefinition[] cachedItemLibrary;

        private InventoryItemDefinition FindItemById(string itemId)
        {
            if (cachedItemLibrary == null)
                cachedItemLibrary = Resources.LoadAll<InventoryItemDefinition>("Inventory Items");

            foreach (InventoryItemDefinition item in cachedItemLibrary)
            {
                if (item != null && string.Equals(item.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                BindSession,
                UnbindSession);

            EnsureEventSystem();

            menuFont = Resources.Load<TMP_FontAsset>("Fonts/Gotfridus");
            authorialFont = Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond") ?? menuFont ?? TMP_Settings.defaultFontAsset;
            if (menuFont == null)
                menuFont = authorialFont;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            AudioManager.Route(audioSource, AudioBus.SoundEffects);

            BuildPrompt();
            BuildGrid();
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }

        private void Update()
        {
            if (!TryGetPlayer())
                return;

            if (!isOpen)
            {
                UpdatePromptVisibility();

                if (unlocked && Pressed(cachedPlayer.OpenInventoryAction))
                    OpenGrid();
            }
            else
            {
                // Same key that opens it also closes it, in addition to the
                // dedicated Close key/button.
                if (Pressed(cachedPlayer.CloseDocumentAction) || Pressed(cachedPlayer.OpenInventoryAction))
                    CloseGrid();
            }
        }

        private static bool Pressed(InputAction action) =>
            action != null && action.WasPressedThisFrame();

        private bool TryGetPlayer()
        {
            if (cachedPlayer != null)
                return true;

            UnbindPlayer();

            GameSession session = boundSession ?? GameSession.Instance;
            if (session == null || !session.SceneServices.TryGetPlayer(out cachedPlayer, out _))
                return false;

            inventoryHolder = cachedPlayer.GetComponent<InventoryHolder>();
            if (inventoryHolder == null)
                inventoryHolder = cachedPlayer.gameObject.AddComponent<InventoryHolder>();
            inventoryHolder.ItemsChanged += RefreshGrid;
            inventoryHolder.SynchronizeFromSession(session);
            RefreshGrid();
            return true;
        }

        private void UnbindPlayer()
        {
            if (inventoryHolder != null)
                inventoryHolder.ItemsChanged -= RefreshGrid;

            inventoryHolder = null;
            cachedPlayer = null;
        }

        private void BindSession(GameSession session)
        {
            boundSession = session;
            session.InventoryStateChanged += HandleInventoryStateChanged;
            SynchronizeFromSession(session, isInitialSync: true);
        }

        private void UnbindSession(GameSession session)
        {
            session.InventoryStateChanged -= HandleInventoryStateChanged;
            if (ReferenceEquals(boundSession, session))
                boundSession = null;
        }

        private void HandleInventoryStateChanged() =>
            SynchronizeFromSession(boundSession, isInitialSync: false);

        internal void SynchronizeFromSession(GameSession session, bool isInitialSync = false)
        {
            if (session == null || session.State == null)
                return;

            unlocked = session.State.Inventory.IsUnlocked;
            // Only suppress the "first time" hint when the inventory was
            // ALREADY unlocked the moment this component (re)bound to the
            // session - e.g. loading a save, or a scene reload mid-
            // playthrough, where the player has certainly already seen/used
            // it before. If it just became unlocked THIS session (the
            // [unlock inventory] tag firing live, via
            // HandleInventoryStateChanged), the player hasn't seen the
            // prompt yet at all - let it show normally.
            if (isInitialSync && unlocked)
                promptDismissed = true;
            inventoryHolder?.SynchronizeFromSession(session);
        }

        // --- Prompt -------------------------------------------------------

        private void UpdatePromptVisibility()
        {
            bool shouldShow = unlocked && !promptDismissed && !dialogueActive;
            if (promptObject.activeSelf != shouldShow)
                promptObject.SetActive(shouldShow);

            if (shouldShow)
                promptText.text = $"PRESS {GetKeyLabel(cachedPlayer.OpenInventoryAction)} TO OPEN INVENTORY";
        }

        private static string GetKeyLabel(InputAction action)
        {
            if (action == null || action.bindings.Count == 0)
                return "I";

            string path = action.bindings[0].effectivePath;
            if (string.IsNullOrEmpty(path))
                return "I";

            string readable = InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            return string.IsNullOrEmpty(readable) ? "I" : readable.ToUpperInvariant();
        }

        private void BuildPrompt()
        {
            var canvasObject = new GameObject("Inventory Prompt Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            promptObject = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            promptObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = (RectTransform)promptObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(900f, 60f);
            rect.anchoredPosition = new Vector2(0f, 60f);

            promptText = promptObject.GetComponent<TextMeshProUGUI>();
            promptText.font = menuFont;
            promptText.fontSize = 26f;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(0.9f, 0.86f, 0.74f, 0.95f);
            promptText.raycastTarget = false;

            promptObject.SetActive(false);
        }

        // --- Grid -----------------------------------------------------------

        private void OpenGrid()
        {
            isOpen = true;
            promptDismissed = true;
            promptObject.SetActive(false);
            gridPanel.SetActive(true);
            corniceOverlay?.SetActive(true);
            RefreshGrid();

            // PushPlayerLock is the project's established way to suspend
            // FirstPersonController's own input handling (including its
            // "click to recapture the cursor" behaviour) while modal UI is
            // open - the same mechanism NarrativeContentPresenter and
            // patient sessions use. Without this, clicking anywhere on
            // screen re-locks the cursor and resumes camera look, even
            // though the inventory is still visually open.
            playerLockOwner = boundSession ?? GameSession.Instance;
            playerLockOwner?.PushPlayerLock();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseGrid()
        {
            isOpen = false;
            gridPanel.SetActive(false);
            corniceOverlay?.SetActive(false);

            playerLockOwner?.PopPlayerLock();
            playerLockOwner = null;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void BuildGrid()
        {
            var canvasObject = new GameObject("Inventory Grid Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gridPanel = CreatePanel(canvasObject.transform, "Panel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.018f, 0.015f, 0.94f));

            TMP_Text title = CreateText(gridPanel.transform, "Title", 34f, new Vector2(384f, 810f), new Vector2(-384f, -170f), TextAlignmentOptions.Center);
            title.text = "INVENTORY";

            var scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(gridPanel.transform, false);
            RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.anchorMin = new Vector2(0f, 0.32f);
            scrollRectTransform.anchorMax = new Vector2(1f, 0.71f);
            scrollRectTransform.offsetMin = new Vector2(384f, 0f);
            scrollRectTransform.offsetMax = new Vector2(-384f, 0f);
            scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.018f, 0.014f, 0.4f);

            // A dedicated Viewport child (not the scroll object itself) is the
            // correct ScrollRect anatomy - without it, Content's width can end
            // up unresolved, which is what pushed every cell into a single
            // sliver at the left edge instead of wrapping into a proper grid.
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            gridContent = (RectTransform)contentObject.transform;
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.offsetMin = Vector2.zero;
            gridContent.offsetMax = Vector2.zero;

            GridLayoutGroup gridLayout = contentObject.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = cellSize;
            gridLayout.spacing = cellSpacing;
            gridLayout.padding = new RectOffset(10, 10, 10, 10);
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.content = gridContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scrollObject.AddComponent<ArrowKeyScroll>();

            // Detail panel: description for generic items (documents open directly instead).
            GameObject detailPanel = CreatePanel(gridPanel.transform, "Detail", new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(60f, 40f), new Vector2(-60f, -20f), new Color(0.06f, 0.05f, 0.04f, 0.9f));
            detailTitleText = CreateText(detailPanel.transform, "DetailTitle", 26f, new Vector2(30f, -50f), new Vector2(-30f, -10f), TextAlignmentOptions.TopLeft);
            detailBodyText = CreateText(detailPanel.transform, "DetailBody", 20f, new Vector2(30f, 10f), new Vector2(-30f, -60f), TextAlignmentOptions.TopLeft);
            detailBodyText.font = authorialFont;

            // Placed at the bottom-center, clear of both the grid above and
            // the detail panel behind it.
            var closeButtonObject = new GameObject("Close Button", typeof(RectTransform), typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(gridPanel.transform, false);
            RectTransform closeButtonRect = (RectTransform)closeButtonObject.transform;
            closeButtonRect.anchorMin = new Vector2(0.5f, 0f);
            closeButtonRect.anchorMax = new Vector2(0.5f, 0f);
            closeButtonRect.pivot = new Vector2(0.5f, 0f);
            closeButtonRect.sizeDelta = new Vector2(220f, 34f);
            closeButtonRect.anchoredPosition = new Vector2(0f, 216f);
            closeButtonObject.GetComponent<Image>().color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            closeButtonObject.GetComponent<Button>().onClick.AddListener(CloseGrid);

            TMP_Text closeHint = CreateText(closeButtonObject.transform, "Label", 22f, new Vector2(10f, 6f), new Vector2(-10f, -6f), TextAlignmentOptions.Center);
            closeHint.text = "CLOSE";

            // Parented under the canvas (not under gridPanel) and drawn last
            // among the canvas's own children, so it sits above the whole
            // grid panel - same pattern as PauseController's corniceOverlay,
            // which the inventory canvas doesn't share since it's a
            // completely separate Screen Space Overlay canvas.
            corniceOverlay = CreateCorniceOverlay(canvasObject.transform, ResolveCorniceTexture());
            corniceOverlay?.SetActive(false);

            gridPanel.SetActive(false);
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

        private void RefreshGrid()
        {
            foreach (GameObject cell in spawnedCells)
                Destroy(cell);
            spawnedCells.Clear();

            if (inventoryHolder == null)
                return;

            Selectable firstSelectable = null;
            foreach (InventoryItemDefinition item in inventoryHolder.Items)
            {
                GameObject cell = CreateCell(item);
                spawnedCells.Add(cell);
                if (firstSelectable == null)
                    firstSelectable = cell.GetComponent<Selectable>();
            }

            if (firstSelectable != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);

            detailTitleText.text = string.Empty;
            detailBodyText.text = string.Empty;
        }

        private GameObject CreateCell(InventoryItemDefinition item)
        {
            var cellObject = new GameObject(item.DisplayName, typeof(RectTransform), typeof(Image), typeof(Button));
            cellObject.transform.SetParent(gridContent, false);

            Image background = cellObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.11f, 0.09f, 0.95f);

            if (item.Icon != null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(cellObject.transform, false);
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = new Vector2(0.1f, 0.25f);
                iconRect.anchorMax = new Vector2(0.9f, 0.95f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                Image iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = item.Icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            TMP_Text label = CreateText(cellObject.transform, "Label", 28f, new Vector2(12f, 8f), new Vector2(-12f, -140f), TextAlignmentOptions.Bottom);
            label.text = item.DisplayName;

            Button button = cellObject.GetComponent<Button>();
            button.onClick.AddListener(() => SelectItem(item));

            return cellObject;
        }

        private void SelectItem(InventoryItemDefinition item)
        {
            if (item == null)
                return;

            if (item.IsDocument)
            {
                if (item.OpenSound != null)
                    audioSource.PlayOneShot(item.OpenSound);

                GameSession session = GameSession.Instance;
                if (session != null)
                {
                    CloseGrid();
                    session.OpenDocument(item.LinkedDocument);
                }
                return;
            }

            detailTitleText.text = item.DisplayName;
            detailBodyText.text = item.Description;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                DontDestroyOnLoad(existing.gameObject);
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        // --- Small UI helpers, matching the project's existing menu style ---

        private GameObject CreatePanel(
            Transform parent, string objectName,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private TMP_Text CreateText(
            Transform parent, string objectName, float size,
            Vector2 offsetMin, Vector2 offsetMax,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = menuFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.86f, 0.74f);
            text.raycastTarget = false;
            return text;
        }
    }
}