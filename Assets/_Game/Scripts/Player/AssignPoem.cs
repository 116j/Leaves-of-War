using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AssignPoem : RevealableObject
{
    [SerializeField]
    private int _poemId;

    [SerializeField]
    protected string _assignedDate;

    [SerializeField]
    protected string _assignedPoemName;

    [SerializeField]
    protected string _assignedPlace;

    [SerializeField]
    [TextArea(3, 10)]
    protected string _assignedPoemText;

    [Tooltip("The recorded reading of the poem, played when its transcription starts.")]
    [SerializeField]
    protected AudioClip _poemVoice;

    public PoemInfo poemInfo;

    [Header("Fragment")]
    [Tooltip("0 = a whole poem. 1, 2, 3... = this is that piece of a poem split across the room (same Poem Id on every piece).")]
    [SerializeField, Min(0)] private int _fragment;
    [SerializeField, Min(1)] private int _fragmentCount = 1;
    [Tooltip("The lines of this piece: shown on the floor and written in the notebook. When every piece is transcribed they join into the whole poem (Assigned Poem Text, or all the pieces in order if that is empty).")]
    [SerializeField, TextArea(2, 8)] private string _fragmentText;

    [Header("Floor Writing")]
    [SerializeField] private Color _floorColor = new Color(0.12f, 0.08f, 0.05f, 0.85f);
    [SerializeField, Range(0f, 0.4f)] private float _floorPadding = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _shimmer = 0.35f;
    [SerializeField, Min(0.1f)] private float _fadeSeconds = 2.5f;

    public int PoemId => _poemId;
    public int Fragment => _fragment;
    public int FragmentCount => _fragmentCount;
    public bool IsFragment => _fragment > 0;
    public string FragmentText => _fragmentText;
    public override bool HasFootprint => true;
    public override Bounds Footprint => footprint;

    private Bounds footprint;
    private TextMeshPro floorText;
    private float lifted;
    private bool fading;
    private float fade = 1f;

    private void Awake()
    {
        if (poemInfo == null)
        {
            Debug.LogError("PoemInfo is not assigned in AssignPoem on " + gameObject.name, this);
            return;
        }
        if (poemInfo._poemsList == null)
            poemInfo._poemsList = new List<PoemInfoContainer>();

        if (!IsFragment || _fragment == 1)
            poemInfo._poemsList.Add(new PoemInfoContainer
        {
            poemId = _poemId,
            poemName = _assignedPoemName,
            poemText = _assignedPoemText,
            poemDate = _assignedDate,
            poemPlace = _assignedPlace,
            poemVoice = _poemVoice
        });

        Renderer body = GetComponent<Renderer>();
        footprint = body != null ? body.bounds : new Bounds(transform.position, new Vector3(2f, 0.1f, 2f));
        //if (body != null)
        //    body.enabled = false;
        foreach (Collider c in GetComponents<Collider>())
            c.enabled = false;
    }

    private void OnEnable()
    {
        TranscribePoem.Transcribed += OnTranscribed;
        TranscribePoem.FragmentTranscribed += OnFragmentTranscribed;
    }

    private void OnDisable()
    {
        TranscribePoem.Transcribed -= OnTranscribed;
        TranscribePoem.FragmentTranscribed -= OnFragmentTranscribed;
    }

    private void OnFragmentTranscribed(int poemId, int fragment)
    {
        if (poemId == _poemId && fragment == _fragment)
            fading = true;
    }

    private void Start() => BuildFloorText();

    private void BuildFloorText()
    {
        Vector3 center = footprint.center;
        float ground = footprint.min.y;
        if (Physics.Raycast(new Vector3(center.x, footprint.max.y + 0.3f, center.z), Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            ground = hit.point.y;

        Vector3 up = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.forward;

        var go = new GameObject(name.Trim() + " Floor Writing", typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetPositionAndRotation(new Vector3(center.x, ground + 0.004f, center.z), Quaternion.LookRotation(Vector3.down, up.normalized));
        floorText = go.GetComponent<TextMeshPro>();
        floorText.rectTransform.sizeDelta = new Vector2(footprint.size.x * (1f - _floorPadding), footprint.size.z * (1f - _floorPadding));

        TranscribePoem transcribe = FindAnyObjectByType<TranscribePoem>();
        if (transcribe != null && transcribe.PoemFont != null)
            floorText.font = transcribe.PoemFont;
        floorText.text = IsFragment
            ? (_fragment == 1 ? "<size=125%>" + (_assignedPoemName ?? "").Trim() + "</size>\n\n" : "") + (_fragmentText ?? "").Trim()
            : "<size=125%>" + (_assignedPoemName ?? "").Trim() + "</size>\n\n" + (_assignedPoemText ?? "").Trim();
        floorText.alignment = TextAlignmentOptions.Center;
        floorText.enableAutoSizing = true;
        floorText.fontSizeMin = 0.05f;
        floorText.fontSizeMax = 4f;
        floorText.lineSpacing = 5f;
        floorText.textWrappingMode = TextWrappingModes.Normal;
        floorText.color = new Color(_floorColor.r, _floorColor.g, _floorColor.b, 1f);
        floorText.ForceMeshUpdate();
    }

    public void SetLifted(float fraction) => lifted = Mathf.Clamp01(fraction);

    private void LateUpdate()
    {
        if (floorText == null || !floorText.gameObject.activeInHierarchy)
            return;

        if (fading)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / _fadeSeconds);
            if (fade <= 0f)
            {
                floorText.gameObject.SetActive(false);
                enabled = false;
                return;
            }
        }

        float shimmer = IsRevealed ? 1f - _shimmer * 0.5f * (1f + Mathf.Sin(Time.time * 2.2f)) : 1f;
        float alpha = _floorColor.a * shimmer * fade;

        TMP_TextInfo info = floorText.textInfo;
        if (info == null || info.characterCount == 0)
            return;
        float liftedLines = lifted * info.lineCount;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible)
                continue;
            float line = 1f - Mathf.Clamp01(liftedLines - character.lineNumber);
            byte a = (byte)Mathf.RoundToInt(255f * alpha * line);
            Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++)
                colors[character.vertexIndex + v].a = a;
        }
        floorText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    protected override void OnRevealChanged(bool revealed)
    {
        if (!revealed)
            return;
        TranscribePoem transcribe = FindAnyObjectByType<TranscribePoem>();
        if (transcribe != null)
            transcribe.OnPoemRevealed(this);
    }

    private void OnTranscribed(int poemId)
    {
        if (poemId == _poemId)
            fading = true;
    }
}
