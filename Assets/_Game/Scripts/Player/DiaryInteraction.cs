using System.Collections;
using Hortensia.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class DiaryInteraction : KeyboardControl
{
    [Tooltip("The diary held in front of the player camera.")]
    [SerializeField] private Transform diary;
    [Tooltip("Optional: the broom, lowered out of view while reading.")]
    [SerializeField] private Transform broom;
    [SerializeField] private Vector3 broomHiddenOffset = new Vector3(0f, -0.25f, 0f);
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
    [Tooltip("How much of the screen the open book fills while reading.")]
    [SerializeField, Range(0.4f, 1f)] private float screenFill = 0.85f;
    [Tooltip("Writing (transcribing from the floor): how much of the screen the open notebook fills.")]
    [SerializeField, Range(0.2f, 1f)] private float writingFill = 0.5f;
    [Tooltip("Writing: how far down the screen the notebook sits (0 = centre, 0.5 = bottom edge).")]
    [SerializeField, Range(0f, 0.5f)] private float writingDrop = 0.3f;
    [Tooltip("How much of the diary face the pages cover (the rest is the cover border).")]
    [SerializeField, Range(0.5f, 1f)] private float pageFill = 0.95f;
    [Tooltip("Off: the diary stays on its pickup until the player takes it.")]
    [SerializeField] private bool startInHand = true;
    [Tooltip("Hold the diary closed and open the cover when reading.")]
    [SerializeField] private bool startClosed = true;

    public static bool IsReading { get; private set; }
    public static bool IsWriting { get; private set; }
    public static bool IsRaised => IsReading || IsWriting;
    public bool HasDiary { get; private set; } = true;

    private FirstPersonController player;
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
            StartCoroutine(Close());
    }

    private void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        viewCamera = player != null && player.PlayerCamera != null ? player.PlayerCamera : Camera.main;
        transcribe = FindAnyObjectByType<TranscribePoem>();
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
            broomPosition = broom.localPosition;

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
        Vector3 halfSize = Abs(wAxis) * width * 0.5f + Abs(nAxis) * thickness + Abs(sAxis) * height;

        rig = new GameObject("Diary Cover").transform;
        rig.SetParent(diary.parent, false);
        rig.gameObject.layer = diary.gameObject.layer;

        Block("Back", rig, center + wAxis * width * 0.25f, halfSize);

        hinge = new GameObject("Hinge").transform;
        hinge.SetParent(rig, false);
        hinge.localPosition = center + nAxis * thickness * 0.5f;
        hinge.gameObject.layer = rig.gameObject.layer;

        Block("Cover", hinge, wAxis * width * 0.25f + nAxis * thickness * 0.5f, halfSize);
        FollowDiary();
    }

    private void Block(string name, Transform parent, Vector3 position, Vector3 size)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(block.GetComponent<Collider>());
        block.name = name;
        block.layer = parent.gameObject.layer;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        MeshRenderer renderer = block.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = diaryRenderer.sharedMaterials;
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
            rig.gameObject.SetActive(closed);
        if (diaryRenderer != null)
            diaryRenderer.enabled = !closed;
        if (pageCanvas != null)
            pageCanvas.enabled = !closed;
    }

    private void OnDisable()
    {
        IsReading = false;
        IsWriting = false;
    }

    private void OnDestroy()
    {
        if (rig != null)
            Destroy(rig.gameObject);
    }

    protected override void Update()
    {
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
        if (busy || diary == null || viewCamera == null)
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

        GetReadingPose(out Vector3 position, out Quaternion rotation, writing ? writingFill : screenFill, writing ? writingDrop : 0f);
        yield return Move(heldPosition, heldRotation, position, rotation, Vector3.zero, broomHiddenOffset);

        Play(openClip, 0f);
        if (rig != null)
        {
            BeginFlip();
            yield return Flip(0f, 1f);
            EndFlip();
            ShowClosed(false);
        }

        if (!writing)
        {
            previousLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
        }
        busy = false;
    }

    private IEnumerator Close()
    {
        busy = true;
        if (IsReading)
            Cursor.lockState = previousLock;
        Play(closeClip, rig != null ? Mathf.Max(0f, flipSeconds - closeImpact) : 0f);

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

        yield return Move(diary.localPosition, diary.localRotation, heldPosition, heldRotation, broomHiddenOffset, Vector3.zero);

        IsReading = false;
        IsWriting = false;
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
        foreach (MonoBehaviour behaviour in page.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is Graphic))
                Destroy(behaviour);

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

    private IEnumerator Move(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, Vector3 broomFrom, Vector3 broomTo)
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
                broom.localPosition = broomPosition + Vector3.Lerp(broomFrom, broomTo, k);
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
