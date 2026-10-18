using ChocDino.UIFX;
using Hortensia.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TranscribePoem : RevealableObject
{
    public PoemInfo poemInfo;

    [SerializeField] protected TextMeshProUGUI _assignedDateCurrent;
    [SerializeField] protected TextMeshProUGUI _assignedNameCurrent;
    [SerializeField] protected TextMeshProUGUI _assignedPlaceCurrent;
    [SerializeField] protected TextMeshProUGUI _assignedPoemTextCurrent;
    [Tooltip("Right page: continuation of the current poem.")]
    [SerializeField] protected TextMeshProUGUI _assignedPoemTextCurrentNext;
    [SerializeField] protected TextMeshProUGUI _assignedDateOld;
    [SerializeField] protected TextMeshProUGUI _assignedNameOld;
    [SerializeField] protected TextMeshProUGUI _assignedPlaceOld;
    [SerializeField] protected TextMeshProUGUI _assignedPoemTextOld;
    [Tooltip("Continuation of the old poem on the other side of the turning page.")]
    [SerializeField] protected TextMeshProUGUI _assignedPoemTextOldNext;

    [Header("Transcription")]
    [SerializeField] private int _firstPoemId = 1;
    [Tooltip("Seconds of holding needed to dissolve in one line of text.")]
    [SerializeField, Min(0.05f)] private float _secondsPerLine = 0.6f;
    [Tooltip("Minimum seconds of holding for each section (date, title, place, pages).")]
    [SerializeField, Min(0.05f)] private float _minSectionSeconds = 0.8f;
    [Tooltip("Flip if the dissolve goes from bottom to top.")]
    [SerializeField] private bool _invertDirection = false;
    [SerializeField, Min(0f)] private float _sectionPause = 0.35f;
    [Tooltip("The transcription lasts exactly as long as the recorded reading of the poem (silence at the start and end excluded).")]
    [SerializeField] private bool _matchVoiceLength = true;
    [Tooltip("The leaves still on the floor vanish when a poem is found.")]
    [SerializeField] private bool _clearLeavesWhenFound = true;
    [Tooltip("Single header line: {0} date, {1} place, {2} title.")]
    [SerializeField] private string _headerFormat = "{0}, {1}";
    [Tooltip("Extra space between the lines of the poem (TextMeshPro units, 0 = the font's own).")]
    [SerializeField] private float _lineSpacing = 0f;
    [Tooltip("Extra space after each verse (each line break of the poem).")]
    [SerializeField] private float _paragraphSpacing = 0f;
    [Tooltip("Pulls the lines of the poem closer together, in % of the font size (0 = the font's own spacing).")]
    [SerializeField, Range(0f, 60f)] private float _tighterLines = 25f;
    [Tooltip("Size of the poem title (first line of the page) compared to the poem text, in %.")]
    [SerializeField, Range(80f, 200f)] private float _titleSize = 125f;
    [Tooltip("Header font size compared to the poem text.")]
    [SerializeField, Range(0.3f, 1.5f)] private float _headerScale = 0.7f;
    [Header("Audio")]
    [Tooltip("Looping pen-on-paper sound while writing. Loaded from Resources/Audio/PenWriting when empty.")]
    [SerializeField] private AudioClip _writingClip;
    [SerializeField, Range(0f, 1f)] private float _writingVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _voiceVolume = 1f;
    [Tooltip("One is picked at random each time a page of the notebook is turned.")]
    [SerializeField] private AudioClip[] _pageTurnClips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float _pageTurnVolume = 0.7f;

    [Header("Look Away")]
    [Tooltip("Seconds the poem can stay unseen during a transcription before the wind covers it again.")]
    [SerializeField, Min(0.1f)] private float _lookAwaySeconds = 3f;
    [Tooltip("Leaf coverage the wind brings back over the poem (1 = as at the start).")]
    [SerializeField, Range(0.6f, 1f)] private float _recoverCoverage = 0.7f;
    [Tooltip("Wind gust. Loaded from Resources/MainMenu/Gust when empty.")]
    [SerializeField] private AudioClip _gustClip;
    [Tooltip("Several gusts: one is picked at random. Empty = Gust Clip.")]
    [SerializeField] private AudioClip[] _gustClips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float _gustVolume = 0.8f;
    [Tooltip("Quiet mode: looking away never interrupts the transcription.")]
    [SerializeField] private bool _immuneToLookingAway = false;
    [SerializeField, Min(0f)] private float _lowerAfterRelease = 1f;

    [Header("Notebook")]
    [Tooltip("Poems of the earlier levels, already written in the notebook when this level starts (filled by the level setup tools).")]
    [SerializeField] private PoemInfoContainer[] _earlierPoems = new PoemInfoContainer[0];

    [Header("Prompts")]
    [SerializeField] private string _promptFormat = "HOLD {0} TO TRANSCRIBE";
    [SerializeField, Min(8f)] private float _promptSize = 30f;

    public static event Action<int> Transcribed;
    public static event Action<int> TranscriptionStarted;
    public static event Action<int> Gusted;
    public static event Action<int, int> FragmentTranscribed;

    public bool PendingTranscription => pending.Count > 0;
    public AssignPoem ActivePoem => activePoem;
    public static bool IsVoicePlaying { get; private set; }

    private static readonly Dictionary<int, PoemInfoContainer> Written = new();

    public static void NewSession() => ResetSession();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Written.Clear();
        IsVoicePlaying = false;
    }
    public bool IsJoining => joining;
    public int FragmentsDone(int poemId) => fragmentsDone.TryGetValue(poemId, out int done) ? done : 0;
    public bool IsTranscribing { get; private set; }
    public static bool AnyTranscribing { get; private set; }
    public static List<TextMeshProUGUI> WritingSections { get; private set; }
    public static float[] WritingSeconds { get; private set; }
    public static int WritingSection { get; private set; }
    public static float WritingProgress { get; private set; }
    public static bool WordsHeld { get; private set; }

    private void OnDestroy()
    {
        WritingSections = null;
        WordsHeld = false;
    }
    public TMP_FontAsset PoemFont => _assignedPoemTextCurrent != null ? _assignedPoemTextCurrent.font : null;

    private int currentp = 1;
    private Book book;
    private readonly HashSet<int> transcribed = new HashSet<int>();
    private readonly HashSet<int> pending = new HashSet<int>();
    private readonly Dictionary<TextMeshProUGUI, float> baseSizes = new Dictionary<TextMeshProUGUI, float>();
    private readonly Dictionary<int, int> poemsSectionProgress = new();
    private readonly Dictionary<int, int> fragmentsDone = new();
    private readonly Dictionary<int, SortedDictionary<int, string>> fragmentTexts = new();
    private bool joining;
    private bool finishingVoice;
    private FirstPersonController player;
    private TextMeshProUGUI prompt;
    private AudioSource writingSource;
    private AudioSource voiceSource;
    private AudioSource pageSource;
    private bool writing;
    private AssignPoem activePoem;
    private DiaryInteraction diary;
    private float unseenTime;
    private float releasedTime;
    private float liftProgress;
    private bool started;
    private bool voicePaused;
    private float notWritingTime;
    private bool poemSeen;
    private bool voiceStarted;
    private Coroutine transcriptionCoroutine;

    private void OnEnable()
    {
        Book.OnMouseDragRight += OnDragRightPage;
        Book.OnMouseDragLeft += OnDragLeftPage;
        Book.RestoreOriginalPageLeft += ResetLeftPage;
        Book.RestoreOriginalPageRight += ResetRightPage;
        Book.ForwardPage += OnForwardPage;
        Book.BackwardPage += OnBackwardPage;
    }

    private void OnDisable()
    {
        Book.OnMouseDragRight -= OnDragRightPage;
        Book.OnMouseDragLeft -= OnDragLeftPage;
        Book.RestoreOriginalPageLeft -= ResetLeftPage;
        Book.RestoreOriginalPageRight -= ResetRightPage;
        Book.ForwardPage -= OnForwardPage;
        Book.BackwardPage -= OnBackwardPage;
    }

    private void Start()
    {
        if (_assignedPoemTextCurrent == null || _assignedNameCurrent == null)
            Debug.LogError("TranscribePoem: the notebook texts are not assigned on " + name + " (Assigned Poem Text Current, Name, Date...). Nothing can be written in the notebook.", this);
        currentp = _firstPoemId;
        book = _assignedPoemTextCurrent != null ? _assignedPoemTextCurrent.GetComponentInParent<Book>(true) : null;
        if (book == null)
            book = FindAnyObjectByType<Book>(FindObjectsInactive.Include);
        if (book != null)
            book.interactable = true;
        if (_assignedPoemTextCurrentNext == null && book != null && book.RightNext != null)
            _assignedPoemTextCurrentNext = book.RightNext.GetComponentInChildren<TextMeshProUGUI>(true);

        PrepareBody(_assignedPoemTextCurrent, _assignedPoemTextCurrentNext);
        PrepareBody(_assignedPoemTextOld, _assignedPoemTextOldNext);
        RaiseBody(_assignedPoemTextCurrent, _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
        RaiseBody(_assignedPoemTextOld, _assignedDateOld, _assignedNameOld, _assignedPlaceOld);
        foreach (TextMeshProUGUI header in new[] { _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent, _assignedDateOld, _assignedNameOld, _assignedPlaceOld })
        {
            if (header == null)
                continue;
            header.overflowMode = TextOverflowModes.Overflow;
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.enableAutoSizing = false;
            TextMeshProUGUI body = _assignedPoemTextCurrent != null ? _assignedPoemTextCurrent : _assignedPoemTextOld;
            if (body != null)
                header.fontSize = body.fontSize * _headerScale;
        }
        foreach (TextDissolve dissolve in FindObjectsByType<TextDissolve>(FindObjectsInactive.Include))
        {
            dissolve.StopAllCoroutines();
            dissolve.enabled = false;
        }
        foreach (TextMeshProUGUI text in AllTexts())
        {
            text.text = string.Empty;
            text.maxVisibleWords = 99999;
            SetReveal(text, 1f);
        }
        foreach (TextMeshProUGUI text in CurrentTexts())
        {
            DissolveFilter filter = text.GetComponent<DissolveFilter>();
            if (filter == null)
                continue;
            filter.Direction = DissolveDirection.Vertical;
            filter.DirectionInvert = _invertDirection;
        }
        RestoreNotebook();
        OnFlipPage();

        player = FindAnyObjectByType<FirstPersonController>();
        prompt = ScreenPrompt.Create(transform, "Transcription Prompt", _promptSize, 40f);

        if (_writingClip == null)
            _writingClip = Resources.Load<AudioClip>("Audio/PenWriting");
        writingSource = gameObject.AddComponent<AudioSource>();
        writingSource.clip = _writingClip;
        writingSource.loop = true;
        writingSource.playOnAwake = false;
        writingSource.volume = 0f;
        AudioManager.Route(writingSource, AudioBus.SoundEffects);

        diary = FindAnyObjectByType<DiaryInteraction>();
        if (_gustClip == null)
            _gustClip = Resources.Load<AudioClip>("MainMenu/Gust");

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.volume = _voiceVolume;
        AudioManager.Route(voiceSource, AudioBus.Voice);

        pageSource = gameObject.AddComponent<AudioSource>();
        pageSource.playOnAwake = false;
        AudioManager.Route(pageSource, AudioBus.SoundEffects);
    }

    private void RestoreNotebook()
    {
        foreach (AssignPoem piece in FindObjectsByType<AssignPoem>(FindObjectsInactive.Include))
        {
            if (!piece.IsFragment)
                continue;
            if (!fragmentTexts.TryGetValue(piece.PoemId, out SortedDictionary<int, string> texts))
                fragmentTexts[piece.PoemId] = texts = new SortedDictionary<int, string>();
            texts[piece.Fragment] = (piece.FragmentText ?? string.Empty).Trim();
        }
        int levelPoem = int.MaxValue;
        foreach (AssignPoem poem in FindObjectsByType<AssignPoem>(FindObjectsInactive.Include))
            if (poem.PoemId > 0)
                levelPoem = Mathf.Min(levelPoem, poem.PoemId);
        foreach (int later in Written.Keys.Where(id => id >= levelPoem).ToList())
            Written.Remove(later);
        foreach (PoemInfoContainer earlier in _earlierPoems)
            if (earlier != null && earlier.poemId > 0 && earlier.poemId < levelPoem && !Written.ContainsKey(earlier.poemId))
                Written[earlier.poemId] = earlier;
        foreach (KeyValuePair<int, PoemInfoContainer> pair in Written)
        {
            if (poemInfo != null && Find(pair.Key) == null)
            {
                if (poemInfo._poemsList == null)
                    poemInfo._poemsList = new List<PoemInfoContainer>();
                poemInfo._poemsList.Add(pair.Value);
            }
            transcribed.Add(pair.Key);
            poemsSectionProgress[pair.Key] = 3;
        }
    }

    private void Remember(int poemId)
    {
        PoemInfoContainer poem = Find(poemId);
        if (poem != null)
            Written[poemId] = poem;
    }

    private string JoinedFragments(int poemId, int upTo)
    {
        if (!fragmentTexts.TryGetValue(poemId, out SortedDictionary<int, string> texts))
            return string.Empty;
        return string.Join("\n\n", texts.Where(t => t.Key <= upTo && t.Value.Length > 0).Select(t => t.Value));
    }

    private string BodyFor(PoemInfoContainer poem)
    {
        if (poem == null)
            return string.Empty;
        bool split = fragmentTexts.ContainsKey(poem.poemId);
        if (split && !transcribed.Contains(poem.poemId))
            return Titled(poem, JoinedFragments(poem.poemId, FragmentsDone(poem.poemId)));
        if (split && string.IsNullOrWhiteSpace(poem.poemText))
            return Titled(poem, JoinedFragments(poem.poemId, int.MaxValue));
        return Titled(poem, poem.poemText);
    }

    private void ShowPoemSpread(int poemId) => currentp = poemId;

    public int PageCount => WrittenPages().Count;

    private List<int> WrittenPages() => transcribed.Concat(poemsSectionProgress.Keys).Distinct().Where(id => Find(id) != null).OrderBy(id => id).ToList();

    public void TurnPage(int direction)
    {
        if (IsTranscribing || joining)
            return;
        List<int> pages = WrittenPages();
        if (pages.Count == 0)
            return;
        int index = pages.IndexOf(currentp);
        int next = index < 0 ? (direction > 0 ? 0 : pages.Count - 1) : index + direction;
        if (next < 0 || next >= pages.Count || pages[next] == currentp)
            return;
        currentp = pages[next];
        OnFlipPage();
        PlayPageTurn();
    }

    private void PlayPageTurn()
    {
        if (pageSource == null || _pageTurnClips == null || _pageTurnClips.Length == 0)
            return;
        AudioClip clip = _pageTurnClips[UnityEngine.Random.Range(0, _pageTurnClips.Length)];
        if (clip == null)
            return;
        pageSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
        pageSource.PlayOneShot(clip, _pageTurnVolume);
    }

    private void Update()
    {
        IsVoicePlaying = voiceSource != null && voiceSource.isPlaying;
        UpdateWritingSound();
        float dt = Time.unscaledDeltaTime;
        bool playing = Time.timeScale > 0f && !DialogueRunner.LocksPlayer;
        bool open = activePoem != null && !ActiveDone() || IsTranscribing;
        poemSeen = activePoem != null && activePoem.IsRevealed && Gaze.IsSeen(activePoem.Footprint, 6f);
        bool holding = Holding() && playing && !DiaryInteraction.IsReading;

        if (open && diary != null && diary.HasDiary && !joining)
        {
            if (holding && poemSeen && !DiaryInteraction.IsWriting)
            {
                diary.BeginWriting();
                FollowerLeaf.Spawn();
            }
            if (DiaryInteraction.IsWriting && !diary.Busy && !IsTranscribing && pending.Contains(activePoem.PoemId))
            {
                currentp = activePoem.PoemId;
                StartTranscription();
            }

            releasedTime = holding ? 0f : releasedTime + dt;
            if (DiaryInteraction.IsWriting && !diary.Busy && (releasedTime > _lowerAfterRelease || !activePoem.IsRevealed))
            {
                diary.EndWriting();
            }

            if (started && !_immuneToLookingAway && playing && activePoem.IsRevealed)
            {
                unseenTime = poemSeen ? 0f : unseenTime + dt;
                if (unseenTime >= _lookAwaySeconds)
                    Gust();
            }
        }

        if (voiceStarted && !finishingVoice)
        {
            notWritingTime = CanWrite() ? 0f : notWritingTime + dt;
            if (!voicePaused && notWritingTime > 0.3f && voiceSource.isPlaying)
            {
                voiceSource.Pause();
                voicePaused = true;
            }
            else if (voicePaused && notWritingTime <= 0f)
            {
                voiceSource.UnPause();
                voicePaused = false;
            }
        }

        bool showPrompt = (joining && IsTranscribing || open && activePoem != null && activePoem.IsRevealed) && playing && !DiaryInteraction.IsReading && !(holding && DiaryInteraction.IsWriting);
        ScreenPrompt.Show(prompt, showPrompt, showPrompt ? string.Format(_promptFormat, ScreenPrompt.KeyName(player != null ? player.WriteAction : null, "T")) : null);
    }

    private void Gust()
    {
        unseenTime = 0f;
        if (LeafField.Instance != null)
            LeafField.Instance.Cover(activePoem, _recoverCoverage);
        AudioClip gust = _gustClips != null && _gustClips.Length > 0 ? _gustClips[UnityEngine.Random.Range(0, _gustClips.Length)] : _gustClip;
        if (gust != null)
            AudioSource.PlayClipAtPoint(gust, activePoem.Footprint.center, _gustVolume);
        if (diary != null)
            diary.EndWriting();
        Gusted?.Invoke(activePoem.PoemId);
    }

    private void PlayVoice(AudioClip clip, float offset)
    {
        if (clip == null || voiceSource == null)
            return;
        Vector2 range = VoiceTrim.Range(clip);
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = _voiceVolume;
        voiceSource.time = Mathf.Clamp(range.x + offset, 0f, Mathf.Max(0f, clip.length - 0.05f));
        voiceSource.Play();
    }

    private void UpdateWritingSound()
    {
        if (writingSource == null || writingSource.clip == null)
            return;
        float target = writing && (CanWrite() || joining) ? _writingVolume : 0f;
        writingSource.volume = Mathf.MoveTowards(writingSource.volume, target, Time.unscaledDeltaTime * 6f);
        if (writingSource.volume > 0.001f && !writingSource.isPlaying)
        {
            writingSource.time = UnityEngine.Random.Range(0f, writingSource.clip.length * 0.9f);
            writingSource.Play();
        }
        else if (writingSource.volume <= 0.001f && writingSource.isPlaying)
        {
            writingSource.Stop();
        }
    }

    private bool Holding()
    {
        InputAction write = player != null ? player.WriteAction : null;
        if (write != null)
            return write.IsPressed();
        return Keyboard.current != null && Keyboard.current.tKey.isPressed;
    }

    public bool IsTranscribed(int poemId) => transcribed.Contains(poemId);

    public bool IsPoemUncovered => activePoem != null && activePoem.IsRevealed;

    public void OnPoemRevealed(AssignPoem poem)
    {
        if (poem == null || !LevelFlow.PoemAllowed || transcribed.Contains(poem.PoemId) && !IsTranscribing)
            return;
        if (poem.IsFragment && (poem.Fragment != FragmentsDone(poem.PoemId) + 1 || IsTranscribing || joining))
            return;
        activePoem = poem;
        if (!transcribed.Contains(poem.PoemId))
            pending.Add(poem.PoemId);
        unseenTime = 0f;
        if (_clearLeavesWhenFound && LeafField.Instance != null)
        {
            if (!poem.IsFragment || poem.Fragment >= poem.FragmentCount)
                LeafField.Instance.ClearRemaining();
            else
                LeafField.Instance.ClearRemainingExcept(FindObjectsByType<AssignPoem>(FindObjectsInactive.Include)
                    .Where(p => p.PoemId == poem.PoemId && p.Fragment > poem.Fragment).Cast<RevealableObject>().ToList());
        }
    }

    private bool ActiveDone() => activePoem != null &&
        (transcribed.Contains(activePoem.PoemId) || activePoem.IsFragment && FragmentsDone(activePoem.PoemId) >= activePoem.Fragment);

    public void JoinFragments(int poemId)
    {
        if (!joining && !transcribed.Contains(poemId))
            StartCoroutine(Join(poemId));
    }

    private IEnumerator Join(int poemId)
    {
        PoemInfoContainer poem = Find(poemId);
        if (poem == null)
            yield break;
        joining = true;
        if (diary != null && diary.HasDiary)
        {
            yield return new WaitUntil(() => DiaryInteraction.IsWriting || diary.CanOpen && !BroomSweeper.Active);
            if (!DiaryInteraction.IsWriting)
                diary.BeginWriting();
            yield return new WaitUntil(() => DiaryInteraction.IsWriting && !diary.Busy);
        }
        poemsSectionProgress.Remove(poemId);
        currentp = poemId;
        transcriptionCoroutine = StartCoroutine(Transcribe(poemId));
        yield return new WaitUntil(() => transcribed.Contains(poemId) && transcriptionCoroutine == null);
        joining = false;
    }

    public void StartTranscription()
    {
        if (IsTranscribing || !pending.Remove(currentp) || transcriptionCoroutine != null)
            return;
        transcriptionCoroutine = StartCoroutine(Transcribe(currentp));
    }

    private IEnumerator Transcribe(int poemId)
    {
        PoemInfoContainer poem = Find(poemId);
        if (poem == null && activePoem != null && activePoem.PoemId == poemId)
        {
            poem = activePoem.Container();
            if (poemInfo != null)
            {
                if (poemInfo._poemsList == null)
                    poemInfo._poemsList = new List<PoemInfoContainer>();
                poemInfo._poemsList.Add(poem);
            }
        }
        if (poem == null)
        {
            Debug.LogWarning("TranscribePoem: poem " + poemId + " is not registered (check Poem Info on its Assign Poem).", this);
            pending.Add(poemId);
            transcriptionCoroutine = null;
            yield break;
        }
        AssignPoem piece = !joining && activePoem != null && activePoem.IsFragment && activePoem.PoemId == poemId ? activePoem : null;

        IsTranscribing = true;
        AnyTranscribing = true;
        if (book != null)
            book.interactable = false;
        var sections = new[] { HeaderLine(_assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent) }
            .Where(t => t != null)
            .Concat(new[] { _assignedPoemTextCurrent, _assignedPoemTextCurrentNext }.Where(t => t != null))
            .ToList();

        ShowPoemSpread(poemId);
        SetHeader(poem, _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
        string wholeBody = !string.IsNullOrWhiteSpace(poem.poemText) ? poem.poemText : JoinedFragments(poemId, int.MaxValue);
        SplitBody(piece != null ? Titled(poem, (piece.FragmentText ?? string.Empty).Trim(), piece.Fragment, piece.FragmentCount) : joining ? Titled(poem, wholeBody) : BodyFor(poem), _assignedPoemTextCurrent, _assignedPoemTextCurrentNext);
        TextMeshProUGUI headerLine = HeaderLine(_assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
        if (piece != null && piece.Fragment > 1 && headerLine != null)
            sections.Remove(headerLine);
        sections = sections.Where(t => !string.IsNullOrWhiteSpace(t.text)).ToList();
        foreach (TextMeshProUGUI text in sections)
        {
            text.alpha = 1f;
            SetReveal(text, 0f);
        }

        if (piece != null && piece.Fragment > 1 && headerLine != null)
            SetReveal(headerLine, 1f);
        float[] from = new float[sections.Count];

        voiceStarted = false;
        started = false;
        int startIndex = piece == null && poemsSectionProgress.TryGetValue(poemId, out int saved) ? saved : 0;
        float[] durations = new float[sections.Count];
        for (int i = 0; i < sections.Count; i++)
        {
            sections[i].ForceMeshUpdate(true);
            durations[i] = Mathf.Max(_minSectionSeconds, sections[i].textInfo.lineCount * _secondsPerLine);
        }
        float voiceLength = _matchVoiceLength && piece == null ? VoiceTrim.Length(poem.poemVoice) : 0f;
        if (voiceLength > 0f && sections.Count > 0)
        {
            float pauses = _sectionPause * (sections.Count - 1);
            float scale = Mathf.Max(0.1f * sections.Count, voiceLength - pauses) / durations.Sum();
            for (int i = 0; i < durations.Length; i++)
                durations[i] *= scale;
        }
        WritingSections = sections;
        WritingSeconds = durations;
        WritingSection = Mathf.Min(startIndex, sections.Count);
        WritingProgress = 0f;
        float voiceOffset = 0f;
        for (int i = 0; i < Mathf.Min(startIndex, durations.Length); i++)
            voiceOffset += durations[i] + _sectionPause;
        int index = 0;
        foreach (TextMeshProUGUI text in sections)
        {
            if (string.IsNullOrWhiteSpace(text.text))
                continue;
            if (index < startIndex)
            {
                SetReveal(text, 1f);
                index++;
                continue;
            }

            float t = from[index];
            float duration = Mathf.Max(0.05f, durations[index] * (1f - t));
            if (t >= 0.999f)
            {
                SetReveal(text, 1f);
                index++;
                continue;
            }
            writing = true;
            while (t < 1f)
            {
                WordsHeld = CanWrite();
                if (WordsHeld)
                {
                    t += Time.unscaledDeltaTime / duration * (1f - from[index]);
                    started = true;
                    if (!voiceStarted)
                    {
                        voiceStarted = true;
                        if (piece == null)
                        {
                            PlayVoice(poem.poemVoice, voiceOffset);
                            TranscriptionStarted?.Invoke(poemId);
                        }
                    }
                }
                SetReveal(text, Mathf.Clamp01(t));
                WritingSection = index;
                WritingProgress = Mathf.Clamp01(t);
                if (activePoem != null)
                    activePoem.SetLifted((index + Mathf.Clamp01(t)) / sections.Count);
                yield return null;
            }
            index++;
            WritingSection = index;
            WritingProgress = 0f;
            poemsSectionProgress[poemId] = index;
            writing = false;
            if (text != sections[sections.Count - 1])
                yield return Wait(_sectionPause);
        }
        writing = false;
        WordsHeld = false;
        if (piece == null && voiceStarted && voiceSource != null && voiceSource.clip == poem.poemVoice && poem.poemVoice != null)
        {
            finishingVoice = true;
            if (voicePaused)
            {
                voiceSource.UnPause();
                voicePaused = false;
            }
            float end = VoiceTrim.Range(poem.poemVoice).y - 0.05f;
            while ((voiceSource.isPlaying || Time.timeScale <= 0f) && voiceSource.time < end)
                yield return null;
            voiceSource.Stop();
            finishingVoice = false;
        }

        IsTranscribing = false;
        AnyTranscribing = false;
        started = false;
        voiceStarted = false;
        voicePaused = false;
        if (book != null)
            book.interactable = true;

        if (piece != null)
        {
            poemsSectionProgress[poemId] = 3;
            pending.Remove(poemId);
            fragmentsDone[poemId] = Mathf.Max(FragmentsDone(poemId), piece.Fragment);
            FragmentTranscribed?.Invoke(poemId, piece.Fragment);
        }
        else
        {
            transcribed.Add(poemId);
            Remember(poemId);
            Transcribed?.Invoke(poemId);
        }
        yield return new WaitForSecondsRealtime(1f);
        transcriptionCoroutine = null;
        if (diary != null)
            diary.EndWriting();
    }

    public void CancelTranscription()
    {
        if (transcriptionCoroutine != null)
        {
            StopCoroutine(transcriptionCoroutine);
            transcriptionCoroutine = null;


            if (IsTranscribing && activePoem != null && !transcribed.Contains(activePoem.PoemId))
                pending.Add(activePoem.PoemId);

            IsTranscribing = false;
            AnyTranscribing = false;
            started = false;
            writing = false;
            if (book != null)
                book.interactable = true;

            if (activePoem != null)
                ResetUnfinishedSections(activePoem.PoemId);
        }
    }

    private void ResetUnfinishedSections(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        TextMeshProUGUI header = HeaderLine(_assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
        if (header != null)
        {
            if (progress >= 1)
                SetReveal(header, 1f);
            else
            {
                header.text = string.Empty;
                SetReveal(header, 0f);
            }
        }

        if (_assignedPoemTextCurrent != null)
        {
            if (progress >= 2)
                SetReveal(_assignedPoemTextCurrent, 1f);
            else
            {
                _assignedPoemTextCurrent.text = string.Empty;
                SetReveal(_assignedPoemTextCurrent, 0f);
            }
        }

        if (_assignedPoemTextCurrentNext != null)
        {
            if (progress >= 3)
                SetReveal(_assignedPoemTextCurrentNext, 1f);
            else
            {
                _assignedPoemTextCurrentNext.text = string.Empty;
                SetReveal(_assignedPoemTextCurrentNext, 0f);
            }
        }
    }

    private static void SetReveal(TextMeshProUGUI text, float reveal)
    {
        DissolveFilter filter = text.GetComponent<DissolveFilter>();
        if (filter != null)
        {
            filter.Strength = 1f - reveal;
            text.alpha = 1f;
        }
        else
        {
            text.alpha = reveal;
        }
    }

    private bool CanWrite() => Time.timeScale > 0f && DiaryInteraction.IsWriting && diary != null && !diary.Busy &&
        Holding() && (joining || poemSeen && activePoem != null && activePoem.IsRevealed);

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (Time.timeScale > 0f && DiaryInteraction.IsWriting)
                t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private float Height(TextMeshProUGUI text)
    {
        Canvas canvas = text.canvas != null ? text.canvas.rootCanvas : null;
        return canvas != null ? canvas.transform.InverseTransformPoint(text.rectTransform.position).y : text.rectTransform.position.y;
    }

    private IEnumerable<TextMeshProUGUI> CurrentTexts() => new[]
    {
        _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent, _assignedPoemTextCurrent, _assignedPoemTextCurrentNext
    }.Where(t => t != null);

    private IEnumerable<TextMeshProUGUI> AllTexts() => new[]
    {
        _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent, _assignedPoemTextCurrent, _assignedPoemTextCurrentNext,
        _assignedDateOld, _assignedNameOld, _assignedPlaceOld, _assignedPoemTextOld, _assignedPoemTextOldNext
    }.Where(t => t != null);

    private void PrepareBody(TextMeshProUGUI first, TextMeshProUGUI second)
    {
        foreach (TextMeshProUGUI text in new[] { first, second })
        {
            if (text == null)
                continue;
            text.lineSpacing = _lineSpacing - _tighterLines;
            text.horizontalAlignment = HorizontalAlignmentOptions.Left;
            text.paragraphSpacing = _paragraphSpacing;
            text.linkedTextComponent = null;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.verticalAlignment = VerticalAlignmentOptions.Top;
        }
    }

    private void RaiseBody(TextMeshProUGUI body, params TextMeshProUGUI[] headers)
    {
        if (body == null)
            return;
        RectTransform rect = body.rectTransform;
        RectTransform parent = rect.parent as RectTransform;
        TextMeshProUGUI line = HeaderLine(headers.Where(h => h != null && h.rectTransform.parent == rect.parent).ToArray());
        if (line == null || parent == null)
            return;
        Rect box = line.rectTransform.rect;
        float headerBottom = parent.InverseTransformPoint(line.rectTransform.TransformPoint(new Vector3(0f, box.yMin, 0f))).y;
        Rect r = rect.rect;
        float bodyTop = parent.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, r.yMax, 0f))).y;
        float wanted = headerBottom - line.fontSize * 0.3f;
        if (wanted > bodyTop + 0.0001f)
            rect.offsetMax += new Vector2(0f, wanted - bodyTop);
    }

    private void SplitBody(string body, TextMeshProUGUI first, TextMeshProUGUI second)
    {
        if (first == null)
            return;
        if (!baseSizes.TryGetValue(first, out float size))
            baseSizes[first] = size = first.fontSize;
        body = (body ?? string.Empty).Replace("\r", string.Empty);
        string[] lines = body.Split('\n');
        int header = lines.Length > 1 && lines[0].StartsWith("<size=") ? 2 : 0;

        for (float f = 1f; f >= MinPoemScale - 0.001f; f -= 0.05f)
            foreach (int[] layout in Layouts)
            {
                if (second == null && layout[1] != layout[0])
                    continue;
                if (Show(lines, header, first, second, size * f, layout[0], layout[1]))
                    return;
            }
        Show(lines, header, first, second, size * MinPoemScale, 3, 3);
    }

    private static readonly int[][] Layouts = { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 2 }, new[] { 2, 3 }, new[] { 3, 3 } };
    private const float ColumnGap = 6f;

    private bool Show(string[] lines, int header, TextMeshProUGUI first, TextMeshProUGUI second, float fontSize, int leftColumns, int rightColumns)
    {
        first.fontSize = fontSize;
        if (second != null)
            second.fontSize = fontSize;
        WrapCache.Clear();
        bool fits = Pack(lines, header, first, second, leftColumns, rightColumns, out string left, out string right);
        Apply(first, left);
        if (second != null)
            Apply(second, right);
        return fits && Laid(first, null) && (second == null || Laid(second, first));
    }

    private static bool Pack(string[] lines, int header, TextMeshProUGUI first, TextMeshProUGUI second, int c1, int c2, out string left, out string right)
    {
        right = string.Empty;
        if (second == null)
        {
            left = Columns(first, lines, 0, lines.Length, header, c1, out int wraps);
            return Content(first, left) <= Room(first, null) && Tidy(lines.Length, c1, wraps);
        }

        float room1 = Room(first, null);
        int lo = Mathf.Min(lines.Length, header + 1), hi = lines.Length;
        if (Content(first, Columns(first, lines, 0, lo, header, c1)) > room1)
            lo = Mathf.Min(lines.Length, 1);
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Content(first, Columns(first, lines, 0, mid, header, c1)) <= room1)
                lo = mid;
            else
                hi = mid - 1;
        }
        int fill = Mathf.Max(1, lo);
        float room2 = Room(second, first);
        for (int k = fill; k >= Mathf.Max(header + 1, fill - 6); k--)
        {
            bool stanza = k >= lines.Length || lines[k - 1].Trim().Length == 0 || lines[k].Trim().Length == 0;
            if (!stanza)
                continue;
            string rest = Columns(second, lines, k, lines.Length, 0, c2, out int wrapsRight);
            if (Content(second, rest) <= room2)
            {
                left = Columns(first, lines, 0, k, header, c1, out int wrapsLeft);
                right = rest;
                return Tidy(k, c1, wrapsLeft) && Tidy(lines.Length - k, c2, wrapsRight);
            }
        }
        left = Columns(first, lines, 0, fill, header, c1, out int leftWraps);
        right = Columns(second, lines, fill, lines.Length, 0, c2, out int rightWraps);
        return Content(second, right) <= room2 && Tidy(fill, c1, leftWraps) && Tidy(lines.Length - fill, c2, rightWraps);
    }

    private static bool Tidy(int rows, int columns, int wraps) => columns <= 1 || wraps <= Mathf.Max(1, Mathf.FloorToInt(rows * 0.12f));

    private static string Columns(TextMeshProUGUI text, string[] lines, int from, int to, int header, int columns) =>
        Columns(text, lines, from, to, header, columns, out _);

    private static string Columns(TextMeshProUGUI text, string[] lines, int from, int to, int header, int columns, out int extra)
    {
        extra = 0;
        var head = new List<string>();
        var rows = new List<string>();
        for (int i = from; i < to; i++)
        {
            if (from == 0 && i < header)
                head.Add(lines[i]);
            else
                rows.Add(lines[i]);
        }
        Trim(rows);
        if (columns <= 1 || rows.Count == 0)
            return string.Join("\n", head.Concat(rows)).TrimEnd();

        float inner = Mathf.Max(0.0001f, text.rectTransform.rect.width - text.margin.x - text.margin.z);
        float share = (100f - ColumnGap * (columns - 1)) / columns;
        float limit = inner * share / 100f * 0.95f;
        float offset = WidthOffset(text);
        var wrapped = new List<string>();
        foreach (string row in rows)
        {
            string key = row + "\u0001" + limit.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (!WrapCache.TryGetValue(key, out string[] pieces))
                WrapCache[key] = pieces = Wrap(text, row, limit, offset).ToArray();
            wrapped.AddRange(pieces);
            extra += pieces.Length - 1;
        }

        var cols = new List<List<string>>();
        int start = 0;
        for (int c = 0; c < columns; c++)
        {
            int remaining = columns - c;
            int target = start + Mathf.CeilToInt((wrapped.Count - start) / (float)remaining);
            int end = c == columns - 1 ? wrapped.Count : target;
            if (c < columns - 1)
                for (int d = 0; d <= 3; d++)
                {
                    if (target - d > start && wrapped[target - d - 1].Trim().Length == 0)
                    {
                        end = target - d;
                        break;
                    }
                    if (target + d < wrapped.Count && target + d > start && wrapped[target + d - 1].Trim().Length == 0)
                    {
                        end = target + d;
                        break;
                    }
                }
            var col = wrapped.GetRange(start, Mathf.Clamp(end - start, 0, wrapped.Count - start));
            Trim(col);
            cols.Add(col);
            start = Mathf.Min(end, wrapped.Count);
        }

        int height = cols.Max(c => c.Count);
        var output = new List<string>(head);
        for (int r = 0; r < height; r++)
        {
            var row = new System.Text.StringBuilder();
            for (int c = 0; c < cols.Count; c++)
            {
                if (c > 0)
                    row.Append("<pos=").Append((c * (share + ColumnGap)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("%>");
                if (r < cols[c].Count)
                    row.Append(cols[c][r]);
            }
            output.Add(row.ToString());
        }
        return string.Join("\n", output).TrimEnd();
    }

    private static readonly Dictionary<string, string[]> WrapCache = new Dictionary<string, string[]>();

    private static void Trim(List<string> rows)
    {
        while (rows.Count > 0 && rows[0].Trim().Length == 0)
            rows.RemoveAt(0);
        while (rows.Count > 0 && rows[rows.Count - 1].Trim().Length == 0)
            rows.RemoveAt(rows.Count - 1);
    }

    private static float WidthOffset(TextMeshProUGUI text)
    {
        float one = text.GetPreferredValues("MMMM", 10000f, 0f).x;
        float two = text.GetPreferredValues("MMMMMMMM", 10000f, 0f).x;
        return Mathf.Max(0f, 2f * one - two);
    }

    private static float Width(TextMeshProUGUI text, string value, float offset) =>
        text.GetPreferredValues(value, 10000f, 0f).x - offset;

    private static IEnumerable<string> Wrap(TextMeshProUGUI text, string line, float limit, float offset)
    {
        if (line.Trim().Length == 0 || Width(text, line, offset) <= limit)
        {
            yield return line;
            yield break;
        }
        string[] words = line.Split(' ');
        string current = string.Empty;
        foreach (string word in words)
        {
            string attempt = current.Length == 0 ? word : current + " " + word;
            if (current.Trim().Length > 0 && Width(text, attempt, offset) > limit)
            {
                yield return current;
                current = "   " + word;
            }
            else
            {
                current = attempt;
            }
        }
        if (current.Length > 0)
            yield return current;
    }

    private const float MinPoemScale = 0.3f;

    private static bool Laid(TextMeshProUGUI text, TextMeshProUGUI reference)
    {
        TMP_TextInfo info = text.textInfo;
        if (info == null || info.lineCount == 0 || string.IsNullOrEmpty(text.text))
            return true;
        return info.lineInfo[info.lineCount - 1].descender >= Bottom(text, reference) - 0.0001f;
    }

    private static void Apply(TextMeshProUGUI text, string value)
    {
        text.maxVisibleWords = 99999;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = value;
        text.ForceMeshUpdate(true, true);
    }

    private static string Join(string[] lines, int from, int to) =>
        from >= to ? string.Empty : string.Join("\n", lines, from, to - from);

    private static float Content(TextMeshProUGUI text, string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0f;
        Rect r = text.rectTransform.rect;
        float width = Mathf.Max(0.0001f, r.width);
        float height = text.GetPreferredValues(value, width, 0f).y;
        return height - text.margin.y - text.margin.w;
    }

    private static float Room(TextMeshProUGUI text, TextMeshProUGUI reference)
    {
        Rect r = text.rectTransform.rect;
        float top = r.yMax - text.margin.y;
        return (top - Bottom(text, reference)) * 0.98f;
    }

    private static float Bottom(TextMeshProUGUI text, TextMeshProUGUI reference)
    {
        float bottom = text.rectTransform.rect.yMin + text.margin.w;
        if (reference == null)
            return bottom;
        Rect r = reference.rectTransform.rect;
        Vector3 world = reference.rectTransform.TransformPoint(new Vector3(r.xMin, r.yMin + reference.margin.w, 0f));
        float shared = text.rectTransform.InverseTransformPoint(world).y;
        return Mathf.Max(bottom, shared);
    }

    private static bool IsBlank(TMP_TextInfo info, int line) => info.lineInfo[line].visibleCharacterCount == 0;

    private static bool EndsLine(TMP_TextInfo info, int line)
    {
        int last = info.lineInfo[line].lastCharacterIndex;
        return last >= 0 && last < info.characterCount && info.characterInfo[last].character == '\n';
    }

    private TextMeshProUGUI HeaderLine(params TextMeshProUGUI[] headers) =>
        headers.Where(t => t != null).OrderByDescending(t => Height(t)).FirstOrDefault();

    private void SetHeader(PoemInfoContainer poem, TextMeshProUGUI date, TextMeshProUGUI name, TextMeshProUGUI place)
    {
        TextMeshProUGUI line = HeaderLine(date, name, place);
        foreach (TextMeshProUGUI header in new[] { date, name, place })
        {
            if (header == null)
                continue;
            header.maxVisibleWords = 99999;
            header.text = header == line
                ? HeaderText(poem)
                : string.Empty;
        }
        FitHeader(line);
    }

    private void FitHeader(TextMeshProUGUI header)
    {
        if (header == null || string.IsNullOrEmpty(header.text))
            return;
        if (!baseSizes.TryGetValue(header, out float size))
            baseSizes[header] = size = header.fontSize;
        header.fontSize = size;
        float width = header.rectTransform.rect.width - header.margin.x - header.margin.z;
        if (width <= 1f)
            return;
        float preferred = header.GetPreferredValues(header.text).x;
        if (preferred > width)
            header.fontSize = Mathf.Max(size * 0.5f, size * width / preferred * 0.98f);
    }

    private string HeaderText(PoemInfoContainer poem)
    {
        string date = (poem.poemDate ?? "").Trim();
        string place = (poem.poemPlace ?? "").Trim();
        string format = _headerFormat.Contains("{2}") ? "{0}, {1}" : _headerFormat;
        if (date.Length == 0 || place.Length == 0)
            return date + place;
        return string.Format(format, date, place);
    }

    private string Titled(PoemInfoContainer poem, string body, int fragment, int count)
    {
        string title = (poem?.poemName ?? "").Trim();
        if (title.Length == 0 || count <= 1)
            return Titled(poem, body);
        return "<size=" + _titleSize.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%>" + title + "</size>  <size=80%>" + fragment + "/" + count + "</size>\n\n" + (body ?? string.Empty).TrimStart('\n', '\r');
    }

    private string Titled(PoemInfoContainer poem, string body)
    {
        string title = (poem?.poemName ?? "").Trim();
        if (title.Length == 0)
            return body;
        return "<size=" + _titleSize.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%>" + title + "</size>\n\n" + (body ?? string.Empty).TrimStart('\n', '\r');
    }

    private PoemInfoContainer Find(int poemId)
    {
        if (poemInfo == null || poemInfo._poemsList == null)
            return null;
        return poemInfo._poemsList.LastOrDefault(p => p.poemId == poemId);
    }

    protected override void OnRevealChanged(bool revealed) { }

    private void OnDragRightPage()
    {
        InitPoemCurrentOldPoem(currentp);
        InitPoemOldCurrentPoem(currentp + 1);
        InitPoemOldDesc(currentp + 1);
        InitPoemCurrentDesc(currentp);
    }

    private void OnDragLeftPage()
    {
        InitPoemOldCurrentPoem(currentp);
        InitPoemCurrentOldPoem(currentp - 1);
        InitPoemCurrentDesc(currentp - 1);
        InitPoemOldDesc(currentp);
    }

    private void ResetLeftPage() => InitPoemCurrentDesc(currentp);

    private void ResetRightPage() => InitPoemCurrentPoem(currentp);

    private void OnForwardPage(int index)
    {
        PlayPageTurn();
        currentp = index;
        OnFlipPage();
    }

    private void OnBackwardPage(int index)
    {
        PlayPageTurn();
        currentp = index;
        OnFlipPage();
    }

    public void OnFlipPage()
    {
        InitPoemCurrentDesc(currentp);
        InitPoemCurrentPoem(currentp);
    }

    private void InitPoemCurrentDesc(int poemIndex)
    {
        PoemInfoContainer poem = transcribed.Contains(poemIndex) || poemsSectionProgress.ContainsKey(poemIndex) ?
            Find(poemIndex) : null;
        if (poem != null)
        {
            SetHeader(poem, _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
            ApplyProgressToPageDesc(poem.poemId);
        }
        else
            Clear(_assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
    }

    private void InitPoemCurrentPoem(int poemIndex)
    {
        PoemInfoContainer poem = transcribed.Contains(poemIndex) || poemsSectionProgress.ContainsKey(poemIndex) ?
            Find(poemIndex) : null;
        if (poem != null)
        {
            SplitBody(BodyFor(poem), _assignedPoemTextCurrent, _assignedPoemTextCurrentNext);
            ApplyProgressToPageText(poem.poemId);
            ApplyProgressToPageNext(poem.poemId);
        }
        else
            Clear(_assignedPoemTextCurrent, _assignedPoemTextCurrentNext);
    }

    private void InitPoemCurrentOldPoem(int poemIndex)
    {
        PoemInfoContainer poem = transcribed.Contains(poemIndex) || poemsSectionProgress.ContainsKey(poemIndex) ?
            Find(poemIndex) : null;
        if (poem != null)
        {
            SplitBody(BodyFor(poem), _assignedPoemTextCurrent, _assignedPoemTextOldNext);
            ApplyProgressToPageText(poem.poemId);
            ApplyProgressToOldPageNext(poem.poemId);
        }
        else
            Clear(_assignedPoemTextCurrent, _assignedPoemTextOldNext);
    }

    private void InitPoemOldDesc(int poemIndex)
    {
        PoemInfoContainer poem = transcribed.Contains(poemIndex) || poemsSectionProgress.ContainsKey(poemIndex) ?
            Find(poemIndex) : null;
        if (poem != null)
        {
            SetHeader(poem, _assignedDateOld, _assignedNameOld, _assignedPlaceOld);
            ApplyProgressToOldPageDesc(poemIndex);
        }
        else
            Clear(_assignedDateOld, _assignedNameOld, _assignedPlaceOld);
    }

    private void InitPoemOldPoem(int poemIndex)
    {
        PoemInfoContainer poem = transcribed.Contains(poemIndex) || poemsSectionProgress.ContainsKey(poemIndex) ?
            Find(poemIndex) : null;
        if (poem != null)
        {
            SplitBody(BodyFor(poem), _assignedPoemTextOld, _assignedPoemTextOldNext);
            ApplyProgressToOldPageText(poem.poemId);
            ApplyProgressToOldPageNext(poem.poemId);
        }
        else
            Clear(_assignedPoemTextOld, _assignedPoemTextOldNext);
    }

    private void InitPoemOldCurrentPoem(int poemIndex)
    {
        PoemInfoContainer poem = transcribed.Contains(poemIndex) || poemsSectionProgress.ContainsKey(poemIndex) ?
            Find(poemIndex) : null;
        if (poem != null)
        {
            SplitBody(BodyFor(poem), _assignedPoemTextOld, _assignedPoemTextCurrentNext);
            ApplyProgressToOldPageText(poem.poemId);
            ApplyProgressToPageNext(poem.poemId);
        }
        else
            Clear(_assignedPoemTextOld, _assignedPoemTextCurrentNext);
    }

    private void ApplyProgressToPageText(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        if (_assignedPoemTextCurrent != null)
            SetReveal(_assignedPoemTextCurrent, progress >= 2 ? 1f : 0f);
    }
    private void ApplyProgressToPageDesc(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        TextMeshProUGUI header = HeaderLine(_assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
        if (header != null)
            SetReveal(header, progress >= 1 ? 1f : 0f);
    }
    private void ApplyProgressToPageNext(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        if (_assignedPoemTextCurrentNext != null)
            SetReveal(_assignedPoemTextCurrentNext, progress >= 3 ? 1f : 0f);
    }

    private void ApplyProgressToOldPageDesc(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        TextMeshProUGUI header = HeaderLine(_assignedDateOld, _assignedNameOld, _assignedPlaceOld);
        if (header != null) 
            SetReveal(header, progress >= 1 ? 1f : 0f);
    }
    private void ApplyProgressToOldPageText(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        if (_assignedPoemTextOld != null) 
            SetReveal(_assignedPoemTextOld, progress >= 2 ? 1f : 0f);
    }
    private void ApplyProgressToOldPageNext(int poemId)
    {
        int progress = poemsSectionProgress.TryGetValue(poemId, out int p) ? p : 0;

        if (_assignedPoemTextOldNext != null) 
            SetReveal(_assignedPoemTextOldNext, progress >= 3 ? 1f : 0f);
    }

    private static void Clear(params TextMeshProUGUI[] texts)
    {
        foreach (TextMeshProUGUI text in texts)
            if (text != null)
            {
                text.text = string.Empty;
                SetReveal(text, 1f);
            }
    }

    private void OnApplicationQuit()
    {
        if (poemInfo != null && poemInfo._poemsList != null)
            poemInfo._poemsList.Clear();
    }
}
