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
    /// Lets the player examine an InspectableItemDefinition in an isolated
    /// 3D view - drag to rotate it, read its description below - without
    /// adding it to their inventory. Call Inspect() from an
    /// InspectableInteractable's Interact().
    ///
    /// The inspected model lives on a small "stage" positioned far from the
    /// rest of the scene (see stagePosition), rendered by its own camera
    /// onto a RenderTexture shown in a UI panel. The stage camera's culling
    /// mask is restricted to a dedicated Layer (see stageLayerName) so
    /// nothing else in the world can ever appear in the preview - create
    /// that Layer in Project Settings > Tags and Layers before using this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectInspector : MonoBehaviour
    {
        public static ObjectInspector Instance { get; private set; }

        /// <summary>Guarantees an ObjectInspector exists, creating one if needed. Safe to call repeatedly.</summary>
        public static ObjectInspector EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var inspectorObject = new GameObject("Object Inspector");
            return inspectorObject.AddComponent<ObjectInspector>();
        }

        [Tooltip("Name of a Layer dedicated to the inspection stage, so the stage camera renders ONLY the inspected model. Create this Layer in Project Settings > Tags and Layers and make sure the name here matches exactly.")]
        [SerializeField] private string stageLayerName = "ObjectInspection";

        [Tooltip("World position of the isolated stage - far enough from the rest of the level that nothing else can ever appear behind the inspected object.")]
        [SerializeField] private Vector3 stagePosition = new Vector3(0f, 4000f, 0f);

        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private float dragSensitivity = 0.3f;
        [SerializeField] private float pitchClampDegrees = 60f;
        [Tooltip("Background of the isolated stage. Alpha < 1 lets the real scene show through faintly, dimmed by this color, instead of a fully opaque backdrop.")]
        [SerializeField] private Color stageBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.55f);
        [Tooltip("How much each scroll-wheel tick changes zoom distance.")]
        [SerializeField] private float zoomSensitivity = 0.015f;
        [Tooltip("Zoom distance multiplier range relative to the auto-framed distance - 1 = the auto-fit distance.")]
        [SerializeField] private float minZoom = 0.3f;
        [SerializeField] private float maxZoom = 3f;

        private bool isOpen;
        private int stageLayer = -1;
        private Transform stagePivot;
        private Camera stageCamera;
        private RenderTexture stageTexture;
        private GameObject currentModel;

        private GameSession playerLockOwner;
        private global::FirstPersonController cachedPlayer;

        private TMP_FontAsset menuFont;
        private TMP_FontAsset authorialFont;

        private GameObject panel;
        private RawImage previewImage;
        private RectTransform previewRectTransform;
        private TMP_Text titleText;
        private TMP_Text descriptionText;

        private Vector2 lastPointerPosition;
        private bool dragging;
        private float yaw;
        private float pitch;
        private Transform rotationPivot;
        private Vector3 cameraLookCenter;
        private float cameraBaseDistance = 3f;
        private float zoomFactor = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureEventSystem();

            menuFont = Resources.Load<TMP_FontAsset>("Fonts/Gotfridus")
                ?? Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond")
                ?? TMP_Settings.defaultFontAsset;
            authorialFont = Resources.Load<TMP_FontAsset>("Fonts/CormorantGaramond") ?? menuFont;

            BuildStage();
            BuildUi();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (stageTexture != null)
                stageTexture.Release();
        }

        /// <summary>Opens the inspection view for the given item. No-op if already open or the item has no model.</summary>
        public void Inspect(InspectableItemDefinition item)
        {
            if (item == null || item.Model == null || isOpen)
                return;

            isOpen = true;
            ClearCurrentModel();

            rotationPivot.localPosition = Vector3.zero;
            rotationPivot.localRotation = Quaternion.identity;

            currentModel = Instantiate(item.Model, rotationPivot);
            IsolateTargetChildIfNeeded(item);
            SetLayerRecursively(currentModel, stageLayer);

            // Any Animator on the model (e.g. CharacterTalkAnimator's
            // Idle clip) re-applies its own baked Transform curves every
            // frame during Unity's animation update phase - AFTER our
            // Update() runs. Left enabled, it fights and overrides whatever
            // position/rotation we set below, every single frame, which is
            // why centering/rotation fixes appeared to do nothing.
            foreach (Animator modelAnimator in currentModel.GetComponentsInChildren<Animator>())
                modelAnimator.enabled = false;

            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localScale = Vector3.one * Mathf.Max(0.0001f, item.ScaleMultiplier);
            // The authored corrective tilt is baked into the MODEL's own
            // fixed local rotation, set once here and never touched again -
            // the player's drag rotation below is applied entirely to
            // rotationPivot instead, so the two never fight each other.
            currentModel.transform.localRotation = Quaternion.Euler(item.ModelRotationOffset);

            // Moves rotationPivot's origin to the mesh's actual visual
            // center (whatever that is, regardless of the source asset's
            // own internal pivot), compensating the model's position so it
            // doesn't visually jump. From here on, rotating rotationPivot
            // spins the mesh around its true center instead of orbiting
            // around a potentially off-center authored origin.
            CenterPivotOnBounds(currentModel);

            yaw = 0f;
            pitch = 0f;
            rotationPivot.localRotation = Quaternion.identity;

            FrameCamera(currentModel);

            titleText.text = item.DisplayName;
            descriptionText.text = item.Description;
            panel.SetActive(true);

            playerLockOwner = GameSession.Instance;
            playerLockOwner?.PushPlayerLock();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (!isOpen)
                return;

            // Belt-and-braces: re-assert every frame in case something else
            // (e.g. a click-to-recapture handler elsewhere) steals it back.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            HandleDragRotation();
            HandleZoom();

            if (TryGetPlayer() && Pressed(cachedPlayer.CloseDocumentAction))
                Close();
        }

        private void Close()
        {
            isOpen = false;
            dragging = false;
            panel.SetActive(false);
            ClearCurrentModel();

            playerLockOwner?.PopPlayerLock();
            playerLockOwner = null;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// If the item names a specific child object (because Model is
        /// actually a whole multi-object scene, common with Sketchfab
        /// downloads), detach that one child and destroy everything else in
        /// the instantiated hierarchy. Without this, rotating the combined
        /// scene as one rigid object makes unrelated props swing into view
        /// at different angles - looking like the model itself is broken.
        /// </summary>
        private void IsolateTargetChildIfNeeded(InspectableItemDefinition item)
        {
            if (string.IsNullOrWhiteSpace(item.TargetChildName))
                return;

            Transform target = FindDeepChild(currentModel.transform, item.TargetChildName);
            if (target == null)
            {
                Debug.LogWarning(
                    $"InspectableItemDefinition '{item.DisplayName}': no child named " +
                    $"'{item.TargetChildName}' was found in its Model. Showing the whole " +
                    "hierarchy instead - check the exact name in the model's Hierarchy.");
                return;
            }

            GameObject wholeScene = currentModel;
            target.SetParent(rotationPivot, true);
            Destroy(wholeScene);
            currentModel = target.gameObject;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void ClearCurrentModel()
        {
            if (currentModel != null)
                Destroy(currentModel);
            currentModel = null;
        }

        private void HandleDragRotation()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || currentModel == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                lastPointerPosition = mouse.position.ReadValue();
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
            }

            if (!dragging)
                return;

            Vector2 current = mouse.position.ReadValue();
            Vector2 delta = current - lastPointerPosition;
            lastPointerPosition = current;

            yaw += delta.x * dragSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * dragSensitivity, -pitchClampDegrees, pitchClampDegrees);
            rotationPivot.localRotation = Quaternion.Euler(pitch, -yaw, 0f);
        }

        private void HandleZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || currentModel == null)
                return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
                return;

            // Only zoom when the pointer is actually over the 3D preview -
            // otherwise scrolling over the description text (which has its
            // own ScrollRect) would zoom the object AND scroll the text at
            // the same time.
            if (previewRectTransform == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(
                    previewRectTransform, mouse.position.ReadValue(), null))
            {
                return;
            }

            zoomFactor = Mathf.Clamp(zoomFactor - scroll * zoomSensitivity, minZoom, maxZoom);
            ApplyCameraZoom();
        }

        private bool TryGetPlayer()
        {
            if (cachedPlayer != null)
                return true;

            GameSession session = GameSession.Instance;
            return session != null && session.SceneServices.TryGetPlayer(out cachedPlayer, out _);
        }

        private static bool Pressed(InputAction action) =>
            action != null && action.WasPressedThisFrame();

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

        // --- Stage & camera --------------------------------------------

        private void BuildStage()
        {
            stageLayer = LayerMask.NameToLayer(stageLayerName);
            if (stageLayer < 0)
            {
                Debug.LogWarning(
                    $"ObjectInspector: Layer '{stageLayerName}' does not exist. Create it in " +
                    "Project Settings > Tags and Layers and make sure this component's " +
                    "Stage Layer Name matches it exactly - otherwise the inspection camera " +
                    "can't isolate the model from the rest of the scene.");
            }

            var stageRoot = new GameObject("Inspection Stage");
            stageRoot.transform.SetParent(transform, false);
            stageRoot.transform.position = stagePosition;

            stagePivot = new GameObject("Pivot").transform;
            stagePivot.SetParent(stageRoot.transform, false);
            stagePivot.localPosition = Vector3.zero;

            rotationPivot = new GameObject("Rotation Pivot").transform;
            rotationPivot.SetParent(stagePivot, false);

            var lightObject = new GameObject("Stage Light", typeof(Light));
            lightObject.transform.SetParent(stageRoot.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            if (stageLayer >= 0)
                light.cullingMask = 1 << stageLayer;

            var cameraObject = new GameObject("Inspection Camera", typeof(Camera));
            cameraObject.transform.SetParent(stageRoot.transform, false);
            stageCamera = cameraObject.GetComponent<Camera>();
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = stageBackgroundColor;
            stageCamera.cullingMask = stageLayer >= 0 ? 1 << stageLayer : 0;
            stageCamera.orthographic = false;
            stageCamera.fieldOfView = 30f;
            stageCamera.nearClipPlane = 0.05f;
            stageCamera.farClipPlane = 50f;

            stageTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16)
            {
                name = "ObjectInspectionRT"
            };
            stageCamera.targetTexture = stageTexture;
        }

        /// <summary>
        /// Moves rotationPivot's own origin to the visual CENTER of the
        /// mesh's rendered geometry - not just wherever the mesh's authored
        /// Transform origin happens to be. Some source assets bake an
        /// off-center pivot (or even absolute world-space coordinates)
        /// directly into the mesh data; rotating around such a pivot makes
        /// the object appear to swing/tumble around a point that isn't its
        /// own center instead of spinning cleanly in place. The model's own
        /// position is shifted by the same amount in the opposite direction
        /// so it doesn't visually jump when the pivot moves.
        /// </summary>
        private void CenterPivotOnBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 delta = bounds.center - rotationPivot.position;
            rotationPivot.position += delta;
            model.transform.position -= delta;
        }

        /// <summary>Positions the stage camera so the model's full bounds fit in frame, regardless of its size. Also records the base center/distance the scroll-wheel zoom scales from.</summary>
        private void FrameCamera(GameObject model)
        {
            zoomFactor = 1f;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                cameraLookCenter = stagePivot.position;
                cameraBaseDistance = 3f;
                ApplyCameraZoom();
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.05f);
            float distance = radius / Mathf.Sin(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.3f;

            cameraLookCenter = bounds.center;
            cameraBaseDistance = distance;
            ApplyCameraZoom();
        }

        /// <summary>Repositions the camera along its fixed viewing axis at cameraBaseDistance * zoomFactor, always looking at the same recorded center.</summary>
        private void ApplyCameraZoom()
        {
            stageCamera.transform.position =
                cameraLookCenter + new Vector3(0f, 0f, -cameraBaseDistance * zoomFactor);
            stageCamera.transform.LookAt(cameraLookCenter);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
                return;

            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        // --- UI -----------------------------------------------------------

        private void BuildUi()
        {
            var canvasObject = new GameObject(
                "Object Inspector Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreatePanel(
                canvasObject.transform, "Panel",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.018f, 0.015f, 0.94f));

            titleText = CreateText(
                panel.transform, "Title", 34f,
                new Vector2(384f, 900f), new Vector2(-384f, -100f),
                TextAlignmentOptions.Center);

            var previewObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(panel.transform, false);
            previewRectTransform = (RectTransform)previewObject.transform;
            previewRectTransform.anchorMin = new Vector2(0.5f, 0.40f);
            previewRectTransform.anchorMax = new Vector2(0.5f, 0.78f);
            previewRectTransform.sizeDelta = new Vector2(700f, 0f);
            previewRectTransform.anchoredPosition = Vector2.zero;
            previewImage = previewObject.GetComponent<RawImage>();
            previewImage.texture = stageTexture;
            previewImage.raycastTarget = false;

            // Sits strictly between the preview's bottom edge (anchor 0.40 =
            // 432px) and the buttons below (Close Button top edge = 108px).
            // Scrolls instead of relying on font auto-shrinking alone - some
            // items (e.g. a notebook) have far more text than any reasonable
            // fixed-size box could show at a readable size, and auto-sizing
            // alone still overflowed once it hit its minimum font size.
            var descriptionScrollObject = new GameObject(
                "Description Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            descriptionScrollObject.transform.SetParent(panel.transform, false);
            RectTransform descriptionScrollRect = (RectTransform)descriptionScrollObject.transform;
            descriptionScrollRect.anchorMin = Vector2.zero;
            descriptionScrollRect.anchorMax = Vector2.one;
            descriptionScrollRect.offsetMin = new Vector2(384f, 130f);
            descriptionScrollRect.offsetMax = new Vector2(-384f, -680f);
            // Needs SOME alpha (even if tiny) so the ScrollRect/mask has a
            // raycastable graphic - otherwise scroll/drag input over this
            // area wouldn't be detected at all.
            // Not raycastable: this area no longer responds to mouse drag
            // at all (scrolling here happens via arrow keys instead, see
            // ArrowKeyScroll below) - so a drag anywhere on screen,
            // including over the description, rotates the object like
            // everywhere else, instead of the ScrollRect intercepting it.
            descriptionScrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            descriptionScrollObject.GetComponent<Image>().raycastTarget = false;

            var descriptionViewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            descriptionViewport.transform.SetParent(descriptionScrollObject.transform, false);
            RectTransform viewportRect = (RectTransform)descriptionViewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var descriptionTextObject = new GameObject(
                "Description", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            descriptionTextObject.transform.SetParent(descriptionViewport.transform, false);
            RectTransform descriptionRect = (RectTransform)descriptionTextObject.transform;
            descriptionRect.anchorMin = new Vector2(0f, 1f);
            descriptionRect.anchorMax = new Vector2(1f, 1f);
            descriptionRect.pivot = new Vector2(0.5f, 1f);
            // anchorMin.y == anchorMax.y (a single point at the top, not a
            // vertical stretch) - offsetMin/offsetMax assume a full 2D
            // stretch and give wrong results here. anchoredPosition zero
            // pins the top edge exactly at the viewport's top edge;
            // sizeDelta.x zero spans the full stretched width, sizeDelta.y
            // starts at zero and gets overwritten every frame by
            // ContentSizeFitter to match the text's actual height.
            descriptionRect.anchoredPosition = Vector2.zero;
            descriptionRect.sizeDelta = Vector2.zero;

            descriptionText = descriptionTextObject.GetComponent<TextMeshProUGUI>();
            descriptionText.font = authorialFont;
            descriptionText.fontSize = 24f;
            descriptionText.alignment = TextAlignmentOptions.Top;
            descriptionText.color = new Color(0.9f, 0.86f, 0.74f);
            descriptionText.raycastTarget = false;
            descriptionText.enableWordWrapping = true;

            ContentSizeFitter descriptionFitter = descriptionTextObject.GetComponent<ContentSizeFitter>();
            descriptionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect descriptionScroll = descriptionScrollObject.GetComponent<ScrollRect>();
            descriptionScroll.content = descriptionRect;
            descriptionScroll.viewport = viewportRect;
            descriptionScroll.horizontal = false;
            descriptionScroll.vertical = true;
            descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
            descriptionScroll.scrollSensitivity = 20f;
            descriptionScrollObject.AddComponent<ArrowKeyScroll>();

            TMP_Text closeButtonLabel = null;
            var closeButtonObject = new GameObject("Close Button", typeof(RectTransform), typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panel.transform, false);
            RectTransform closeButtonRect = (RectTransform)closeButtonObject.transform;
            closeButtonRect.anchorMin = new Vector2(0.5f, 0f);
            closeButtonRect.anchorMax = new Vector2(0.5f, 0f);
            closeButtonRect.pivot = new Vector2(0.5f, 0f);
            closeButtonRect.sizeDelta = new Vector2(260f, 48f);
            closeButtonRect.anchoredPosition = new Vector2(0f, 60f);
            closeButtonObject.GetComponent<Image>().color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            closeButtonObject.GetComponent<Button>().onClick.AddListener(Close);

            closeButtonLabel = CreateText(
                closeButtonObject.transform, "Label", 24f,
                new Vector2(10f, 6f), new Vector2(-10f, -6f),
                TextAlignmentOptions.Center);
            closeButtonLabel.text = "PUT DOWN";

            TMP_Text dragHint = CreateText(
                panel.transform, "DragHint", 20f,
                new Vector2(384f, 15f), new Vector2(-384f, -1030f),
                TextAlignmentOptions.Bottom);
            dragHint.text = "DRAG TO ROTATE, MOUSE WHEEL TO ZOOM IN OR OUT, ARROWS TO SCROLL THE TEXT";

            panel.SetActive(false);
        }

        private GameObject CreatePanel(
            Transform parent, string objectName,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
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