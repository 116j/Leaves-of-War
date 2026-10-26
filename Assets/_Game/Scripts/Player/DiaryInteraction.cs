using ChocDino.UIFX;
using Hortensia.Runtime;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DiaryInteraction : KeyboardControl
{
    [Tooltip("The diary held in front of the player camera.")]
    [SerializeField] private Transform diary;
    [Tooltip("Optional: the broom, lowered out of view while reading.")]
    [SerializeField] private Transform broom;
    [SerializeField] private Vector3 broomHiddenOffset = new Vector3(0f, -0.25f, 0f);
    [Tooltip("Size of the broom while the notebook is open (reading or transcribing), as a fraction of its normal size.")]
    [SerializeField, Range(0.05f, 1f)] private float broomHiddenScale = 0.6f;
    [Tooltip("Transcribing: where the broom goes, relative to the notebook and seen from the camera (x = right, y = up, z = away). Positive x = right, positive z = behind the notebook.")]
    [SerializeField] private Vector3 broomWritingOffset = new Vector3(0.3f, 0f, 0.2f);
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.45f;
    [Tooltip("Duration of the cover opening/closing.")]
    [SerializeField, Min(0.05f)] private float flipSeconds = 0.5f;
    [Header("Audio")]
    [Tooltip("Loaded from Resources/Audio/DiaryOpen when empty.")]
    [SerializeField] private AudioClip openClip;
    [Tooltip("Loaded from Resources/Audio/DiaryClose when empty.")]
    [SerializeField] private AudioClip closeClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [Tooltip("Seconds into the close clip where the cover lands.")]
    [SerializeField, Min(0f)] private float closeImpact = 0.17f;
    [Tooltip("How much of the screen the open notebook fills while reading (1 = the whole screen).")]
    [SerializeField, Range(0.4f, 1f)] private float readingSize = 0.96f;
    [Tooltip("Writing (transcribing from the floor): how much of the screen the open notebook fills.")]
    [SerializeField, Range(0.2f, 1f)] private float writingSize = 0.66f;
    [Tooltip("Writing: how far down the screen the notebook sits (0 = centre, 0.5 = bottom edge).")]
    [SerializeField, Range(0f, 0.5f)] private float writingLower = 0.17f;
    [Tooltip("How much of the diary face the pages cover (the rest is the cover border).")]
    [SerializeField, Range(0.5f, 1f)] private float pageFill = 0.95f;
    [Tooltip("Off: the diary stays on its pickup until the player takes it.")]
    [SerializeField] private bool startInHand = true;
    [Tooltip("Hold the diary closed and open the cover when reading.")]
    [SerializeField] private bool startClosed = true;
    [Header("Book Model")]
    [Tooltip("Optional material for the page block. Empty: plain paper.")]
    [SerializeField] private Material pagesMaterial;
    [SerializeField] private Color paperColor = new Color(0.93f, 0.89f, 0.8f);
    [Tooltip("Cover board thickness, as a fraction of the book thickness.")]
    [SerializeField, Range(0.05f, 0.4f)] private float boardThickness = 0.16f;
    [Tooltip("How far the covers stick out past the pages, as a fraction of the page height.")]
    [SerializeField, Range(0f, 0.1f)] private float boardOverhang = 0.025f;
    [Header("Close Hint")]
    [SerializeField] private string closeHint = "PRESS {0} TO CLOSE THE NOTEBOOK";
    [SerializeField, Min(8f)] private float closeHintSize = 28f;
    [SerializeField] private string turnHint = "A / D TO TURN THE PAGES";
    [SerializeField] private string zoomHint = "HOLD RIGHT CLICK TO LOOK CLOSER";
    [SerializeField] private string translationHint = "{0} FOR THE TRANSLATION";
    [Tooltip("How much bigger the notebook gets while the right mouse button is held (the mouse moves around the page).")]
    [SerializeField, Range(1.2f, 3f)] private float zoom = 1.9f;

    public Transform Notebook => diary;
    public static bool IsReading { get; private set; }
    public static bool IsWriting { get; private set; }
    public static bool IsRaised => IsReading || IsWriting;
    public bool HasDiary { get; private set; } = true;

    private FirstPersonController player;
    private ReadingBlur blur;
    private TMPro.TextMeshProUGUI closePrompt;
    private Camera viewCamera;
    private Canvas pages;
    private Canvas pageCanvas;
    private TranscribePoem transcribe;
    private AudioSource source;
    private RectTransform leftPage;
    private RectTransform leftCopy;
    private MeshRenderer diaryRenderer;
    private Transform rig;
    private Transform hinge;
    private Vector3 hingeAxis;
    private Vector3 heldPosition;
    private Quaternion heldRotation;
    private Vector3 broomPosition;
    private Vector3 broomScale = Vector3.one;
    private Vector3 broomAway;
    private bool busy;
    private CursorLockMode previousLock = CursorLockMode.Locked;

    private Camera PageCamera => HeldItemsCamera.Overlay != null ? HeldItemsCamera.Overlay : viewCamera;

    public bool Busy => busy;
    public bool CanOpen => HasDiary && !busy && !IsRaised && diary != null && viewCamera != null && !DialogueRunner.IsPlaying &&
        !(PauseController.Instance != null && PauseController.Instance.IsPaused);

    public void RequestOpen()
    {
        if (CanOpen)
            StartCoroutine(Open(false));
    }

    public void BeginWriting()
    {
        if (CanOpen && !BroomSweeper.Active)
            StartCoroutine(Open(true));
    }

    public void EndWriting()
    {
        if (IsWriting && !busy)
            {
            if (transcribe != null)
                transcribe.CancelTranscription();
            StartCoroutine(Close());
        }
    }

    private void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        viewCamera = player != null && player.PlayerCamera != null ? player.PlayerCamera : Camera.main;
        transcribe = FindAnyObjectByType<TranscribePoem>();
        blur = GetComponent<ReadingBlur>();
        if (blur == null)
            blur = gameObject.AddComponent<ReadingBlur>();
        closePrompt = ScreenPrompt.Create(transform, "Notebook Close Hint", closeHintSize, 40f);
        if (openClip == null)
            openClip = Resources.Load<AudioClip>("Audio/DiaryOpen");
        if (closeClip == null)
            closeClip = Resources.Load<AudioClip>("Audio/DiaryClose");
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        AudioManager.Route(source, AudioBus.SoundEffects);

        if (diary == null)
        {
            Debug.LogWarning("DiaryInteraction: assign the diary.", this);
            return;
        }

        foreach (Transform held in new[] { diary, broom })
            if (held != null)
                foreach (Collider c in held.GetComponentsInChildren<Collider>(true))
                    c.enabled = false;

        heldPosition = diary.localPosition;
        heldRotation = diary.localRotation;
        if (broom != null)
        {
            broomPosition = broom.localPosition;
            broomScale = broom.localScale;
        }

        pages = diary.GetComponentInChildren<Canvas>(true);
        foreach (Canvas canvas in diary.GetComponentsInChildren<Canvas>(true))
            if (canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = PageCamera;
        pageCanvas = pages != null ? pages.rootCanvas : null;
        Book book = diary.GetComponentInChildren<Book>(true);
        if (book != null && book.LeftNext != null)
            leftPage = book.LeftNext.rectTransform;

        FitPages();
        diaryRenderer = diary.GetComponent<MeshRenderer>();
        if (startClosed && diaryRenderer != null)
            BuildCover();

        ShowClosed(rig != null);
        SetCover(0f);

        HasDiary = startInHand;
        if (!HasDiary)
        {
            diary.gameObject.SetActive(false);
            if (rig != null)
                rig.gameObject.SetActive(false);
        }
    }

    public void GiveDiary()
    {
        if (HasDiary || diary == null)
            return;
        HasDiary = true;
        diary.gameObject.SetActive(true);
        ShowClosed(rig != null);
        SetCover(0f);
    }

    private void FitPages()
    {
        MeshFilter meshFilter = diary.GetComponent<MeshFilter>();
        if (pageCanvas == null || meshFilter == null || meshFilter.sharedMesh == null || pageCanvas.transform.parent != diary)
            return;

        RectTransform rect = (RectTransform)pageCanvas.transform;
        Vector3 size = meshFilter.sharedMesh.bounds.size;
        Quaternion toLocal = Quaternion.Inverse(diary.rotation);
        float width = Mathf.Abs(Vector3.Dot(size, Cardinal(toLocal * rect.right)));
        float height = Mathf.Abs(Vector3.Dot(size, Cardinal(toLocal * rect.up)));
        Vector2 canvasSize = rect.rect.size;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f)
            return;

        float scale = Mathf.Min(width * pageFill / canvasSize.x, height * pageFill / canvasSize.y);
        rect.localScale = new Vector3(scale, scale, rect.localScale.z);
    }

    private void BuildCover()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
        MeshFilter meshFilter = diary.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
            bounds = meshFilter.sharedMesh.bounds;

        Quaternion toLocal = Quaternion.Inverse(diary.rotation);
        Transform face = pageCanvas != null ? pageCanvas.transform : diary;
        Vector3 wAxis = Cardinal(toLocal * face.right);
        Vector3 nAxis = Cardinal(toLocal * -face.forward);
        if (Mathf.Abs(Vector3.Dot(wAxis, nAxis)) > 0.5f)
            nAxis = Cardinal(Vector3.Cross(wAxis, Vector3.forward));
        Vector3 sAxis = Vector3.Cross(wAxis, nAxis);
        hingeAxis = sAxis;

        Vector3 scale = diary.localScale;
        Vector3 center = Vector3.Scale(bounds.center, scale);
        Vector3 size = Abs(Vector3.Scale(bounds.size, scale));
        float width = Mathf.Abs(Vector3.Dot(size, wAxis));
        float thickness = Mathf.Abs(Vector3.Dot(size, nAxis));
        float height = Mathf.Abs(Vector3.Dot(size, sAxis));
        float half = width * 0.5f;
        float board = Mathf.Clamp(thickness * boardThickness, 0.0015f, thickness * 0.4f);
        float overhang = height * boardOverhang;
        float pageWidth = Mathf.Max(half - overhang, half * 0.5f);
        float pageHeight = Mathf.Max(height - overhang * 2f, height * 0.5f);
        float pageThickness = thickness - board;
        Material paper = PaperMaterial();

        rig = new GameObject("Diary Cover").transform;
        rig.SetParent(diary.parent, false);
        rig.gameObject.layer = diary.gameObject.layer;

        Vector3 bottom = center - nAxis * thickness * 0.5f;
        Block("Back Board", rig, bottom + wAxis * half * 0.5f + nAxis * board * 0.5f, Size(wAxis, nAxis, sAxis, half, board, height), null);
        Block("Back Pages", rig, bottom + wAxis * pageWidth * 0.5f + nAxis * (board + pageThickness * 0.5f), Size(wAxis, nAxis, sAxis, pageWidth, pageThickness, pageHeight), paper);
        Block("Back Spine", rig, center - wAxis * board * 0.5f, Size(wAxis, nAxis, sAxis, board, thickness, height), null);

        hinge = new GameObject("Hinge").transform;
        hinge.SetParent(rig, false);
        hinge.localPosition = center + nAxis * thickness * 0.5f;
        hinge.gameObject.layer = rig.gameObject.layer;

        Block("Front Pages", hinge, wAxis * pageWidth * 0.5f + nAxis * pageThickness * 0.5f, Size(wAxis, nAxis, sAxis, pageWidth, pageThickness, pageHeight), paper);
        Block("Front Board", hinge, wAxis * half * 0.5f + nAxis * (thickness - board * 0.5f), Size(wAxis, nAxis, sAxis, half, board, height), null);
        Block("Front Spine", hinge, -wAxis * board * 0.5f + nAxis * thickness * 0.5f, Size(wAxis, nAxis, sAxis, board, thickness, height), null);
        FollowDiary();
    }

    private static Vector3 Size(Vector3 w, Vector3 n, Vector3 h, float width, float thickness, float height) =>
        Abs(w) * width + Abs(n) * thickness + Abs(h) * height;

    private Material PaperMaterial()
    {
        if (pagesMaterial != null)
            return pagesMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = diaryRenderer.sharedMaterial != null ? diaryRenderer.sharedMaterial.shader : Shader.Find("Standard");
        var material = new Material(shader) { name = "Diary Pages" };
        material.SetColor("_BaseColor", paperColor);
        material.color = paperColor;
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.05f);
        return material;
    }

    private void Block(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(block.GetComponent<Collider>());
        block.name = name;
        block.layer = parent.gameObject.layer;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        MeshRenderer renderer = block.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = material != null ? new[] { material } : diaryRenderer.sharedMaterials;
        renderer.shadowCastingMode = diaryRenderer.shadowCastingMode;
        renderer.receiveShadows = diaryRenderer.receiveShadows;
    }

    private static Vector3 Cardinal(Vector3 v)
    {
        Vector3 a = Abs(v);
        if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
        if (a.y >= a.z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
        return new Vector3(0f, 0f, Mathf.Sign(v.z));
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private void FollowDiary()
    {
        if (rig == null)
            return;
        rig.localPosition = diary.localPosition;
        rig.localRotation = diary.localRotation;
    }

    private void SetCover(float open)
    {
        if (hinge != null)
            hinge.localRotation = Quaternion.AngleAxis(180f * open, hingeAxis);
    }

    private void ShowClosed(bool closed)
    {
        if (rig != null)
            rig.gameObject.SetActive(true);
        if (diaryRenderer != null)
            diaryRenderer.enabled = rig == null && !closed;
        if (pageCanvas != null)
            pageCanvas.enabled = !closed;
    }

    private void OnDisable()
    {
        IsReading = false;
        IsWriting = false;
        ScreenPrompt.Show(closePrompt, false, null);
    }

    private void OnDestroy()
    {
        if (rig != null)
            Destroy(rig.gameObject);
    }

    protected override void Update()
    {
        bool hint = HasDiary && IsReading && !busy && !DialogueRunner.IsPlaying &&
            !(PauseController.Instance != null && PauseController.Instance.IsPaused);
        string hintText = null, hintEnglish = null;
        if (hint)
        {
            string key = ScreenPrompt.KeyName(player != null ? player.DiaryAction : null, "I");
            hintEnglish = string.Format(closeHint, key);
            hintText = string.Format(Loc.T(closeHint), key);
            if (transcribe != null && transcribe.PageCount > 1)
            {
                hintEnglish += "   ·   " + turnHint;
                hintText += "   ·   " + Loc.T(turnHint);
            }
            if (!string.IsNullOrEmpty(zoomHint))
            {
                hintEnglish += "   ·   " + zoomHint;
                hintText += "   ·   " + Loc.T(zoomHint);
            }
            if (TranslationAvailable())
            {
                string writeKey = ScreenPrompt.KeyName(player != null ? player.WriteAction : null, "T");
                hintEnglish += "   ·   " + string.Format(translationHint, writeKey);
                hintText += "   ·   " + string.Format(Loc.T(translationHint), writeKey);
            }
        }
        ScreenPrompt.Show(closePrompt, hint, hintText, hintEnglish);
        UpdateZoom();
        UpdateTranslation(hint);
        if (hint && transcribe != null)
        {
            int direction = 0;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
                direction = 1;
            else if (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame))
                direction = -1;
            Mouse mouse = Mouse.current;
            if (direction == 0 && mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel < -0.1f)
                    direction = 1;
                else if (wheel > 0.1f)
                    direction = -1;
            }
            if (direction != 0)
                transcribe.TurnPage(direction);
        }
        if (!HasDiary)
            return;
        bool pressed = player != null && player.DiaryAction != null
            ? player.DiaryAction.WasPressedThisFrame()
            : Input.GetKeyDown(_interactionKey);
        if (pressed)
            OnKeyPressed(_interactionKey);
    }

    public override void OnKeyPressed(KeyCode keyCode)
    {
        if (busy || diary == null || viewCamera == null || Examinable.IsOpen)
            return;
        if (IsWriting)
            return;
        if (!IsReading && (DialogueRunner.IsPlaying || BroomSweeper.Active || (PauseController.Instance != null && PauseController.Instance.IsPaused)))
            return;

        StartCoroutine(IsReading ? Close() : Open(false));
    }

    private IEnumerator Open(bool writing)
    {
        busy = true;
        if (writing)
            IsWriting = true;
        else
            IsReading = true;

        if (blur != null)
            blur.SetReading(!writing);
        GetReadingPose(out Vector3 position, out Quaternion rotation, writing ? writingSize : readingSize, writing ? writingLower : 0f);
        broomAway = broomHiddenOffset;
        if (writing && broom != null)
        {
            Vector3 notebook = diary.parent != null ? diary.parent.TransformPoint(position) : position;
            Vector3 target = notebook + viewCamera.transform.rotation * broomWritingOffset;
            broomAway = (broom.parent != null ? broom.parent.InverseTransformPoint(target) : target) - broomPosition;
        }
        yield return Move(heldPosition, heldRotation, position, rotation, Vector3.zero, broomAway, 1f, broomHiddenScale);

        Play(openClip, 0f);
        if (rig != null)
        {
            BeginFlip();
            yield return Flip(0f, 1f);
            EndFlip();
            ShowClosed(false);
        }

        if (transcribe != null)
            transcribe.OnFlipPage();

        if (!writing)
        {
            previousLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
        }
        busy = false;
        if (!writing)
            PrepareZoom();
    }

    private Vector3 readPosition;
    private Vector3 zoomPosition;
    private Vector3 panRight;
    private Vector3 panUp;
    private bool zoomReady;
    private float zoomBlend;

    private void PrepareZoom()
    {
        if (diary == null || viewCamera == null)
            return;
        Vector3 keepPosition = diary.localPosition;
        Quaternion keepRotation = diary.localRotation;
        GetReadingPose(out readPosition, out _, readingSize, 0f);
        float fill = readingSize * zoom;
        GetReadingPose(out zoomPosition, out _, fill, 0f);
        diary.localPosition = keepPosition;
        diary.localRotation = keepRotation;
        Transform parent = diary.parent;
        Transform cam = viewCamera.transform;
        Vector3 world = parent != null ? parent.TransformPoint(zoomPosition) : zoomPosition;
        float depth = Mathf.Max(0.01f, Vector3.Dot(world - cam.position, cam.forward));
        float halfV = depth * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfH = halfV * viewCamera.aspect;
        float extra = Mathf.Max(0f, fill - 1f);
        Vector3 right = cam.right * halfH * extra;
        Vector3 up = cam.up * halfV * extra;
        panRight = parent != null ? parent.InverseTransformVector(right) : right;
        panUp = parent != null ? parent.InverseTransformVector(up) : up;
        zoomBlend = 0f;
        zoomReady = true;
    }

    private void UpdateZoom()
    {
        if (!zoomReady || !IsReading || busy || diary == null)
            return;
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool held = (mouse != null && mouse.rightButton.isPressed) || (keyboard != null && keyboard.zKey.isPressed);
        zoomBlend = Mathf.MoveTowards(zoomBlend, held ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
        float x = Mathf.Clamp(pointer.x / Mathf.Max(1f, Screen.width) - 0.5f, -0.5f, 0.5f) * 2f;
        float y = Mathf.Clamp(pointer.y / Mathf.Max(1f, Screen.height) - 0.5f, -0.5f, 0.5f) * 2f;
        Vector3 target = zoomPosition - panRight * x - panUp * y;
        diary.localPosition = Vector3.Lerp(readPosition, target, Mathf.SmoothStep(0f, 1f, zoomBlend));
    }

    private GameObject translationRoot;
    private TextMeshProUGUI translationTitle;
    private TextMeshProUGUI translationBody;
    private bool translationOn;
    private int translationShown = -1;

    private bool TranslationAvailable() =>
        !Loc.Italian && transcribe != null && transcribe.PageCount > 0 && PoemTranslations.TryGet(transcribe.CurrentPoemId, out _);

    private void UpdateTranslation(bool reading)
    {
        bool available = reading && TranslationAvailable();
        if (available)
        {
            InputAction write = player != null ? player.WriteAction : null;
            bool pressed = write != null ? write.WasPressedThisFrame() : Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
            if (pressed)
                translationOn = !translationOn;
        }
        bool show = available && translationOn;
        if (show && translationRoot == null)
            BuildTranslation();
        if (translationRoot != null && translationRoot.activeSelf != show)
            translationRoot.SetActive(show);
        if (!show)
        {
            if (!reading)
                translationOn = false;
            return;
        }
        int poemId = transcribe.CurrentPoemId;
        if (poemId == translationShown || !PoemTranslations.TryGet(poemId, out PoemTranslations.Entry entry))
            return;
        translationShown = poemId;
        translationTitle.text = entry.IsNote ? entry.Title + "  —  about the poem" : entry.Title;
        translationBody.text = entry.Text;
    }

    private void BuildTranslation()
    {
        translationRoot = new GameObject("Poem Translation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        translationRoot.transform.SetParent(transform, false);
        var canvas = translationRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = translationRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(translationRoot.transform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-40f, 20f);
        panelRect.sizeDelta = new Vector2(620f, 900f);
        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.06f, 0.05f, 0.04f, 0.86f);
        image.raycastTarget = false;

        TMP_FontAsset font = DialogueRunner.ActiveFont != null ? DialogueRunner.ActiveFont : TMP_Settings.defaultFontAsset;
        translationTitle = TranslationText(panel.transform, "Title", font, 34f, new Vector2(30f, -28f), new Vector2(-30f, -90f), true);
        translationBody = TranslationText(panel.transform, "Body", font, 25f, new Vector2(30f, -100f), new Vector2(-30f, -30f), false);
        translationBody.enableAutoSizing = true;
        translationBody.fontSizeMin = 14f;
        translationBody.fontSizeMax = 26f;
        translationRoot.SetActive(false);
    }

    private static TextMeshProUGUI TranslationText(Transform parent, string name, TMP_FontAsset font, float size, Vector2 topLeft, Vector2 bottomRight, bool title)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        if (title)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(topLeft.x, -bottomRight.y);
            rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
        }
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.color = new Color(0.95f, 0.91f, 0.84f, 1f);
        text.alignment = title ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopLeft;
        text.fontStyle = title ? FontStyles.Bold : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private IEnumerator Close()
    {
        if (zoomReady && diary != null)
            diary.localPosition = readPosition;
        zoomReady = false;
        zoomBlend = 0f;
        busy = true;
        if (IsReading)
            Cursor.lockState = previousLock;
        Play(closeClip, rig != null ? Mathf.Max(0f, flipSeconds - closeImpact) : 0f);
        if (blur != null)
            blur.SetReading(false);
        if (rig != null)
        {
            SetCover(1f);
            ShowClosed(true);
            BeginFlip();
            yield return Flip(1f, 0f);
            EndFlip();
            if (pageCanvas != null)
                pageCanvas.enabled = false;
        }

        IsReading = false;
        IsWriting = false;

        yield return Move(diary.localPosition, diary.localRotation, heldPosition, heldRotation, broomAway, Vector3.zero, broomHiddenScale, 1f);

        busy = false;
    }

    private void Play(AudioClip clip, float delay)
    {
        if (source == null || clip == null)
            return;
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.PlayDelayed(delay);
    }

    private void BeginFlip()
    {
        if (pageCanvas != null)
            pageCanvas.enabled = true;
        if (leftPage == null || pageCanvas == null || hinge == null)
            return;

        Quaternion current = hinge.localRotation;
        SetCover(1f);

        Transform source = pageCanvas.transform;
        var canvasObject = new GameObject("Left Page (Flip)", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = source.gameObject.layer;
        var copyCanvas = canvasObject.GetComponent<Canvas>();
        copyCanvas.renderMode = RenderMode.WorldSpace;
        copyCanvas.worldCamera = PageCamera;
        copyCanvas.sortingLayerID = pageCanvas.sortingLayerID;
        copyCanvas.sortingOrder = pageCanvas.sortingOrder;

        leftCopy = (RectTransform)canvasObject.transform;
        leftCopy.SetParent(hinge, false);
        leftCopy.localPosition = hinge.InverseTransformPoint(source.position);
        leftCopy.localRotation = Quaternion.Inverse(hinge.rotation) * source.rotation;
        Vector3 hingeScale = hinge.lossyScale;
        Vector3 sourceScale = source.lossyScale;
        leftCopy.localScale = new Vector3(sourceScale.x / hingeScale.x, sourceScale.y / hingeScale.y, sourceScale.z / hingeScale.z);
        RectTransform sourceRect = (RectTransform)source;
        leftCopy.pivot = sourceRect.pivot;
        leftCopy.sizeDelta = sourceRect.rect.size;

        RectTransform page = Instantiate(leftPage.gameObject, leftCopy, false).GetComponent<RectTransform>();
        page.anchorMin = page.anchorMax = new Vector2(0.5f, 0.5f);
        page.pivot = leftPage.pivot;
        page.sizeDelta = leftPage.rect.size;
        page.localPosition = source.InverseTransformPoint(leftPage.position);
        page.localRotation = Quaternion.Inverse(source.rotation) * leftPage.rotation;
        page.localScale = Vector3.one;
        var originalFilters = leftPage.GetComponentsInChildren<DissolveFilter>(true);
        var copyFilters = page.GetComponentsInChildren<DissolveFilter>(true);
        for (int i = 0; i < copyFilters.Length && i < originalFilters.Length; i++)
            copyFilters[i].Strength = originalFilters[i].Strength;
        //foreach (MonoBehaviour behaviour in page.GetComponentsInChildren<MonoBehaviour>(true))
        //    if (!(behaviour is Graphic) && !(behaviour is DissolveFilter))
        //        Destroy(behaviour);

        hinge.localRotation = current;
        leftPage.gameObject.SetActive(false);
    }

    private void EndFlip()
    {
        if (leftCopy != null)
            Destroy(leftCopy.gameObject);
        leftCopy = null;
        if (leftPage != null)
            leftPage.gameObject.SetActive(true);
    }

    private IEnumerator Flip(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            if (Time.timeScale > 0f)
                t += Time.unscaledDeltaTime / flipSeconds;
            SetCover(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
            yield return null;
        }
    }

    private IEnumerator Move(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, Vector3 broomFrom, Vector3 broomTo, float broomScaleFrom, float broomScaleTo)
    {
        float t = 0f;
        while (t < 1f)
        {
            if (Time.timeScale > 0f)
                t += Time.unscaledDeltaTime / transitionSeconds;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            diary.localPosition = Vector3.Lerp(fromPosition, toPosition, k);
            diary.localRotation = Quaternion.Slerp(fromRotation, toRotation, k);
            FollowDiary();
            if (broom != null)
            {
                broom.localPosition = broomPosition + Vector3.Lerp(broomFrom, broomTo, k);
                broom.localScale = broomScale * Mathf.Lerp(broomScaleFrom, broomScaleTo, k);
            }
            yield return null;
        }
    }

    private void GetReadingPose(out Vector3 localPosition, out Quaternion localRotation, float fill, float drop)
    {
        Transform parent = diary.parent;
        Transform cam = viewCamera.transform;

        diary.localPosition = heldPosition;
        diary.localRotation = heldRotation;

        Quaternion pageRotation = pageCanvas != null ? pageCanvas.transform.rotation : diary.rotation;
        Quaternion inCamera = Quaternion.Inverse(Quaternion.Inverse(diary.rotation) * pageRotation);

        Bounds local = new Bounds(Vector3.zero, Vector3.one * 0.5f);
        MeshFilter meshFilter = diary.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
            local = meshFilter.sharedMesh.bounds;

        Vector3 scale = diary.lossyScale;
        Vector3 min = Vector3.one * float.MaxValue;
        Vector3 max = Vector3.one * float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 p = inCamera * Vector3.Scale(corner, scale);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        float width = max.x - min.x;
        float height = max.y - min.y;
        float vertical = viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontal = Mathf.Atan(Mathf.Tan(vertical) * viewCamera.aspect);
        float distance = Mathf.Max(height * 0.5f / Mathf.Tan(vertical), width * 0.5f / Mathf.Tan(horizontal)) / fill;
        distance = Mathf.Max(distance, viewCamera.nearClipPlane * 2f);

        Vector3 pivotInCamera = new Vector3(-(min.x + max.x) * 0.5f, -(min.y + max.y) * 0.5f - drop * 2f * distance * Mathf.Tan(vertical), distance - min.z);
        Quaternion worldRotation = cam.rotation * inCamera;
        Vector3 worldPosition = cam.TransformPoint(pivotInCamera);

        localPosition = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
        localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * worldRotation : worldRotation;
    }
}
