using Hortensia.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ObjectInspector : MonoBehaviour
{
    public static ObjectInspector Instance { get; private set; }

    public static ObjectInspector EnsureInstance()
    {
        if (Instance != null)
            return Instance;
        return new GameObject("Object Inspector").AddComponent<ObjectInspector>();
    }

    [Tooltip("Layer used only by the inspection stage. If it does not exist, layer 31 is used.")]
    [SerializeField] private string stageLayerName = "ObjectInspection";
    [SerializeField] private Vector3 stagePosition = new Vector3(0f, 4000f, 0f);
    [SerializeField] private int renderTextureSize = 512;
    [SerializeField] private float dragSensitivity = 0.3f;
    [SerializeField] private float pitchClampDegrees = 60f;
    [SerializeField] private Color stageBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.55f);
    [SerializeField] private float zoomSensitivity = 0.015f;
    [SerializeField] private float minZoom = 0.3f;
    [SerializeField] private float maxZoom = 3f;

    [Header("Fonts")]
    [Tooltip("Title, PUT DOWN button and hint. Empty: Resources/Fonts/OSerif.")]
    [SerializeField] private TMP_FontAsset titleFont;
    [Tooltip(".ttf / .otf file. Wins over Title Font.")]
    [SerializeField] private Font titleFontFile;
    [Tooltip("Description text. Empty: the poem font of the notebook.")]
    [SerializeField] private TMP_FontAsset descriptionFont;
    [Tooltip(".ttf / .otf file. Wins over Description Font.")]
    [SerializeField] private Font descriptionFontFile;
    [SerializeField, Min(8f)] private float titleSize = 34f;
    [SerializeField, Min(8f)] private float descriptionSize = 28f;

    public bool IsOpen { get; private set; }

    private int stageLayer = 31;
    private Transform stagePivot;
    private Transform rotationPivot;
    private Camera stageCamera;
    private RenderTexture stageTexture;
    private GameObject currentModel;
    private AudioSource voiceSource;

    private TMP_FontAsset menuFont;
    private TMP_FontAsset textFont;
    private GameObject panel;
    private RectTransform previewRect;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private TMP_Text closeLabel;
    private ScrollRect descriptionScroll;
    private RectTransform descriptionArea;

    private Vector2 lastPointer;
    private bool dragging;
    private float yaw;
    private float pitch;
    private Vector3 lookCenter;
    private float baseDistance = 3f;
    private float zoom = 1f;
    private int openedFrame = -1;
    private int closedFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureEventSystem();
        menuFont = MenuLook.Current.PanelFont;
        TranscribePoem transcribe = FindAnyObjectByType<TranscribePoem>();
        textFont = MainMenuController.ResolveFont(descriptionFontFile, descriptionFont, "Inspector Description Font")
            ?? (transcribe != null && transcribe.PoemFont != null ? transcribe.PoemFont : menuFont);

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
        AudioManager.Route(voiceSource, AudioBus.Voice);

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

    public void Inspect(Examinable item)
    {
        if (item == null || item.Model == null || IsOpen || Time.frameCount == closedFrame)
            return;

        IsOpen = true;
        openedFrame = Time.frameCount;
        ClearModel();

        rotationPivot.localPosition = Vector3.zero;
        rotationPivot.localRotation = Quaternion.identity;

        currentModel = Instantiate(item.Model, rotationPivot);
        Strip(currentModel);
        SetLayer(currentModel, stageLayer);
        foreach (Animator animator in currentModel.GetComponentsInChildren<Animator>())
            animator.enabled = false;

        Transform t = currentModel.transform;
        t.localPosition = Vector3.zero;
        Vector3 scale = item.UsesSelf ? item.transform.lossyScale : item.Model.transform.localScale;
        t.localScale = scale;
        t.localRotation = FaceCamera(currentModel) * Quaternion.Euler(item.RotationOffset);
        if (!item.UsesSelf && TryBounds(item.gameObject, out Bounds inGame) && TryBounds(currentModel, out Bounds shown))
        {
            float target = Mathf.Max(inGame.size.x, inGame.size.y, inGame.size.z);
            float current = Mathf.Max(shown.size.x, shown.size.y, shown.size.z);
            if (current > 1e-5f && target > 1e-5f)
                t.localScale *= target / current;
        }
        t.localScale *= Mathf.Max(0.0001f, item.ScaleMultiplier);

        CenterPivot(currentModel);
        yaw = 0f;
        pitch = 0f;
        rotationPivot.localRotation = Quaternion.identity;
        FrameCamera(currentModel);

        titleText.text = item.DisplayName;
        descriptionText.text = item.Description;
        if (descriptionScroll != null)
            descriptionScroll.verticalNormalizedPosition = 1f;
        panel.SetActive(true);

        if (item.Voice != null)
        {
            voiceSource.clip = item.Voice;
            voiceSource.volume = item.VoiceVolume;
            voiceSource.Play();
        }

        stageCamera.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private Quaternion FaceCamera(GameObject model)
    {
        MeshFilter filter = model.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return Quaternion.identity;
        Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, model.transform.localScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        float thinnest = Mathf.Min(size.x, size.y, size.z);
        float largest = Mathf.Max(size.x, size.y, size.z);
        if (thinnest > largest * 0.2f)
            return Quaternion.identity;
        if (size.y <= size.x && size.y <= size.z)
            return Quaternion.Euler(-90f, 0f, 0f);
        if (size.x <= size.z)
            return Quaternion.Euler(0f, -90f, 0f);
        return Quaternion.identity;
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        HandleDrag();
        HandleZoom();

        if (Time.frameCount == openedFrame)
            return;
        FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
        InputAction interact = player != null ? player.InteractAction : null;
        bool close = (interact != null && interact.WasPressedThisFrame() && interact.activeControl != null && !(interact.activeControl.device is Mouse))
            || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        if (close)
            Close();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        closedFrame = Time.frameCount;
        dragging = false;
        panel.SetActive(false);
        ClearModel();
        voiceSource.Stop();
        stageCamera.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void Strip(GameObject model)
    {
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(b);
        foreach (AudioSource a in model.GetComponentsInChildren<AudioSource>(true))
            Destroy(a);
        foreach (Light l in model.GetComponentsInChildren<Light>(true))
            Destroy(l.gameObject);
    }

    private void ClearModel()
    {
        if (currentModel != null)
            Destroy(currentModel);
        currentModel = null;
    }

    private void HandleDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || currentModel == null)
            return;
        if (mouse.leftButton.wasPressedThisFrame)
        {
            lastPointer = mouse.position.ReadValue();
            dragging = previewRect == null || RectTransformUtility.RectangleContainsScreenPoint(previewRect, lastPointer, null);
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
            dragging = false;
        if (!dragging)
            return;

        Vector2 position = mouse.position.ReadValue();
        Vector2 delta = position - lastPointer;
        lastPointer = position;
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
        Vector2 pointer = mouse.position.ReadValue();
        if (descriptionArea != null && RectTransformUtility.RectangleContainsScreenPoint(descriptionArea, pointer, null))
        {
            ScrollDescription(Mathf.Sign(scroll));
            return;
        }
        if (previewRect == null || !RectTransformUtility.RectangleContainsScreenPoint(previewRect, pointer, null))
            return;
        zoom = Mathf.Clamp(zoom - scroll * zoomSensitivity, minZoom, maxZoom);
        ApplyZoom();
    }

    private void ScrollDescription(float direction)
    {
        if (descriptionScroll == null || descriptionScroll.content == null || descriptionScroll.viewport == null)
            return;
        float overflow = descriptionScroll.content.rect.height - descriptionScroll.viewport.rect.height;
        if (overflow <= 1f)
            return;
        descriptionScroll.verticalNormalizedPosition = Mathf.Clamp01(descriptionScroll.verticalNormalizedPosition + direction * 70f / overflow);
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void BuildStage()
    {
        int named = LayerMask.NameToLayer(stageLayerName);
        stageLayer = named >= 0 ? named : 31;

        var stageRoot = new GameObject("Inspection Stage");
        stageRoot.transform.SetParent(transform, false);
        stageRoot.transform.position = stagePosition;

        stagePivot = new GameObject("Pivot").transform;
        stagePivot.SetParent(stageRoot.transform, false);
        rotationPivot = new GameObject("Rotation Pivot").transform;
        rotationPivot.SetParent(stagePivot, false);

        var lightObject = new GameObject("Stage Light", typeof(Light));
        lightObject.transform.SetParent(stageRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << stageLayer;

        var cameraObject = new GameObject("Inspection Camera", typeof(Camera));
        cameraObject.transform.SetParent(stageRoot.transform, false);
        stageCamera = cameraObject.GetComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = stageBackgroundColor;
        stageCamera.cullingMask = 1 << stageLayer;
        stageCamera.fieldOfView = 30f;
        stageCamera.nearClipPlane = 0.01f;
        stageCamera.farClipPlane = 50f;

        stageTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16) { name = "ObjectInspectionRT" };
        stageCamera.targetTexture = stageTexture;
        stageCamera.enabled = false;

        foreach (Camera other in FindObjectsByType<Camera>())
            if (other != stageCamera)
                other.cullingMask &= ~(1 << stageLayer);
    }

    private void CenterPivot(GameObject model)
    {
        if (!TryBounds(model, out Bounds bounds))
            return;
        Vector3 delta = bounds.center - rotationPivot.position;
        rotationPivot.position += delta;
        model.transform.position -= delta;
    }

    private void FrameCamera(GameObject model)
    {
        zoom = 1f;
        if (!TryBounds(model, out Bounds bounds))
        {
            lookCenter = stagePivot.position;
            baseDistance = 3f;
            ApplyZoom();
            return;
        }
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.02f);
        baseDistance = radius / Mathf.Sin(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        lookCenter = bounds.center;
        ApplyZoom();
    }

    private static bool TryBounds(GameObject model, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = System.Array.FindAll(model.GetComponentsInChildren<Renderer>(), r => r.enabled);
        if (renderers.Length == 0)
            return false;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private void ApplyZoom()
    {
        stageCamera.transform.position = lookCenter + new Vector3(0f, 0f, -baseDistance * zoom);
        stageCamera.transform.LookAt(lookCenter);
    }

    private static void SetLayer(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayer(child.gameObject, layer);
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("Object Inspector Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.zero);
        panel.GetComponent<Image>().color = MenuLook.Current.panelColor;

        titleText = Text(panel.transform, "Title", titleSize, new Vector2(384f, 900f), new Vector2(-384f, -100f), TextAlignmentOptions.Center, menuFont);
        titleText.color = MenuLook.Current.panelHeaderColor;

        var previewObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        previewObject.transform.SetParent(panel.transform, false);
        previewRect = (RectTransform)previewObject.transform;
        previewRect.anchorMin = new Vector2(0.5f, 0.40f);
        previewRect.anchorMax = new Vector2(0.5f, 0.78f);
        previewRect.sizeDelta = new Vector2(700f, 0f);
        previewRect.anchoredPosition = Vector2.zero;
        RawImage preview = previewObject.GetComponent<RawImage>();
        preview.texture = stageTexture;
        preview.raycastTarget = false;

        var scrollObject = new GameObject("Description Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollObject.transform.SetParent(panel.transform, false);
        Stretch((RectTransform)scrollObject.transform, new Vector2(384f, 130f), new Vector2(-384f, -680f));
        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.01f);
        scrollImage.raycastTarget = false;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect, Vector2.zero, Vector2.zero);

        var descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        descriptionObject.transform.SetParent(viewport.transform, false);
        RectTransform descriptionRect = (RectTransform)descriptionObject.transform;
        descriptionRect.anchorMin = new Vector2(0f, 1f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.pivot = new Vector2(0.5f, 1f);
        descriptionRect.anchoredPosition = Vector2.zero;
        descriptionRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI description = descriptionObject.GetComponent<TextMeshProUGUI>();
        description.font = textFont;
        description.fontSize = descriptionSize;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.color = MenuLook.Current.panelTextColor;
        description.raycastTarget = false;
        description.textWrappingMode = TextWrappingModes.Normal;
        descriptionObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        descriptionText = description;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.content = descriptionRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 0f;
        scrollObject.AddComponent<ArrowKeyScroll>();
        descriptionScroll = scroll;
        descriptionArea = (RectTransform)scrollObject.transform;

        var buttonObject = new GameObject("Close Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(260f, 48f);
        buttonRect.anchoredPosition = new Vector2(0f, 60f);
        MenuLook.Current.StylePanelButton(buttonObject.GetComponent<Button>());
        buttonObject.GetComponent<Button>().onClick.AddListener(MenuLook.Current.PlayBackSound);
        buttonObject.GetComponent<Button>().onClick.AddListener(Close);
        closeLabel = Text(buttonObject.transform, "Label", 24f, new Vector2(10f, 6f), new Vector2(-10f, -6f), TextAlignmentOptions.Center, menuFont);
        closeLabel.text = "PUT DOWN";

        TMP_Text hint = Text(panel.transform, "DragHint", 20f, new Vector2(384f, 15f), new Vector2(-384f, -1030f), TextAlignmentOptions.Bottom, menuFont);
        hint.text = "DRAG THE OBJECT TO ROTATE IT, MOUSE WHEEL ON THE OBJECT TO ZOOM, ON THE TEXT (OR ARROWS) TO SCROLL IT";

        panel.SetActive(false);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static TMP_Text Text(Transform parent, string name, float size, Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Stretch((RectTransform)textObject.transform, offsetMin, offsetMax);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = MenuLook.Current.panelTextColor;
        text.raycastTarget = false;
        MenuLook.Current.StylePanelText(text);
        return text;
    }
}
