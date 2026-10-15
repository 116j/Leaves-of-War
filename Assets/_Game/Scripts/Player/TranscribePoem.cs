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
    [SerializeField] private string _headerFormat = "{0}, {1}, {2}";
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
        foreach (AssignPoem piece in FindObjectsByType<AssignPoem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!piece.IsFragment)
                continue;
            if (!fragmentTexts.TryGetValue(piece.PoemId, out SortedDictionary<int, string> texts))
                fragmentTexts[piece.PoemId] = texts = new SortedDictionary<int, string>();
            texts[piece.Fragment] = (piece.FragmentText ?? string.Empty).Trim();
        }
        foreach (PoemInfoContainer earlier in _earlierPoems)
            if (earlier != null && earlier.poemId > 0 && !Written.ContainsKey(earlier.poemId))
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
            return JoinedFragments(poem.poemId, FragmentsDone(poem.poemId));
        if (split && string.IsNullOrWhiteSpace(poem.poemText))
            return JoinedFragments(poem.poemId, int.MaxValue);
        return poem.poemText;
    }

    private void ShowPoemSpread(int poemId)
    {
        currentp = poemId;
        if (book != null && book.Spread != poemId)
            book.ShowSpread(poemId);
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

        if (voiceStarted)
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

        bool showPrompt = open && activePoem.IsRevealed && playing && !DiaryInteraction.IsReading && !(holding && DiaryInteraction.IsWriting);
        ScreenPrompt.Show(prompt, showPrompt, string.Format(_promptFormat, ScreenPrompt.KeyName(player != null ? player.WriteAction : null, "T")));
    }

    private void Gust()
    {
        unseenTime = 0f;
        if (LeafField.Instance != null)
            LeafField.Instance.Cover(activePoem, _recoverCoverage);
        if (_gustClip != null)
            AudioSource.PlayClipAtPoint(_gustClip, activePoem.Footprint.center, _gustVolume);
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
                LeafField.Instance.ClearRemainingExcept(FindObjectsByType<AssignPoem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
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
        var pieces = FindObjectsByType<AssignPoem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => p.PoemId == poemId && p.IsFragment).OrderBy(p => p.Fragment).ToList();
        string body = !string.IsNullOrWhiteSpace(poem.poemText)
            ? poem.poemText
            : string.Join("\n\n", pieces.Select(p => (p.FragmentText ?? string.Empty).Trim()).Where(t => t.Length > 0));

        if (diary != null && diary.HasDiary)
        {
            yield return new WaitUntil(() => DiaryInteraction.IsWriting || diary.CanOpen && !BroomSweeper.Active);
            if (!DiaryInteraction.IsWriting)
                diary.BeginWriting();
            yield return new WaitUntil(() => DiaryInteraction.IsWriting && !diary.Busy);
        }

        ShowPoemSpread(poemId);
        poemsSectionProgress.Remove(poemId);
        IsTranscribing = true;
        AnyTranscribing = true;
        if (book != null)
            book.interactable = false;
        var sections = new[] { HeaderLine(_assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent) }
            .Where(t => t != null)
            .Concat(new[] { _assignedPoemTextCurrent, _assignedPoemTextCurrentNext }.Where(t => t != null))
            .ToList();
        SetHeader(poem, _assignedDateCurrent, _assignedNameCurrent, _assignedPlaceCurrent);
        SplitBody(body, _assignedPoemTextCurrent, _assignedPoemTextCurrentNext);
        sections = sections.Where(t => !string.IsNullOrWhiteSpace(t.text)).ToList();
        float[] durations = new float[sections.Count];
        for (int i = 0; i < sections.Count; i++)
        {
            sections[i].alpha = 1f;
            SetReveal(sections[i], 0f);
            sections[i].ForceMeshUpdate(true);
            durations[i] = Mathf.Max(_minSectionSeconds, sections[i].textInfo.lineCount * _secondsPerLine);
        }
        float voiceLength = VoiceTrim.Length(poem.poemVoice);
        if (voiceLength > 0f && sections.Count > 0)
        {
            float scale = Mathf.Max(0.1f * sections.Count, voiceLength - _sectionPause * (sections.Count - 1)) / durations.Sum();
            for (int i = 0; i < durations.Length; i++)
                durations[i] *= scale;
        }

        PlayVoice(poem.poemVoice, 0f);
        TranscriptionStarted?.Invoke(poemId);
        writing = true;
        for (int i = 0; i < sections.Count; i++)
        {
            for (float t = 0f; t < 1f;)
            {
                if (Time.timeScale > 0f)
                    t += Time.unscaledDeltaTime / durations[i];
                SetReveal(sections[i], Mathf.Clamp01(t));
                yield return null;
            }
            if (i < sections.Count - 1)
                for (float t = 0f; t < _sectionPause;)
                {
                    if (Time.timeScale > 0f)
                        t += Time.unscaledDeltaTime;
                    yield return null;
                }
        }
        writing = false;
        while (voiceSource != null && voiceSource.clip == poem.poemVoice && !VoiceTrim.Finished(voiceSource, poem.poemVoice))
            yield return null;
        poemsSectionProgress[poemId] = 3;
        IsTranscribing = false;
        AnyTranscribing = false;
        if (book != null)
            book.interactable = true;
        transcribed.Add(poemId);
        Remember(poemId);
        Transcribed?.Invoke(poemId);
        yield return new WaitForSecondsRealtime(1.5f);
        joining = false;
        if (diary != null)
            diary.EndWriting();
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
        if (poem == null)
            yield break;
        AssignPoem piece = activePoem != null && activePoem.IsFragment && activePoem.PoemId == poemId ? activePoem : null;

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
        SplitBody(piece != null ? JoinedFragments(poemId, piece.Fragment) : BodyFor(poem), _assignedPoemTextCurrent, _assignedPoemTextCurrentNext);
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
        if (piece != null)
        {
            string before = JoinedFragments(poemId, piece.Fragment - 1);
            int previous = before.Length > 0 ? before.Split('\n').Length + 1 : 0;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i] == headerLine)
                    continue;
                int lines = Mathf.Max(1, sections[i].text.Split('\n').Length);
                from[i] = Mathf.Clamp01(previous / (float)lines);
                previous = Mathf.Max(0, previous - lines);
                SetReveal(sections[i], from[i]);
            }
        }

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
                if (CanWrite())
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
                if (activePoem != null)
                    activePoem.SetLifted((index + Mathf.Clamp01(t)) / sections.Count);
                yield return null;
            }
            index++;
            poemsSectionProgress[poemId] = index;
            writing = false;
            if (text != sections[sections.Count - 1])
                yield return Wait(_sectionPause);
        }
        writing = false;

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
        Holding() && poemSeen && activePoem != null && activePoem.IsRevealed;

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

    private static void PrepareBody(TextMeshProUGUI first, TextMeshProUGUI second)
    {
        foreach (TextMeshProUGUI text in new[] { first, second })
        {
            if (text == null)
                continue;
            text.linkedTextComponent = null;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.verticalAlignment = VerticalAlignmentOptions.Top;
        }
    }

    private void SplitBody(string body, TextMeshProUGUI first, TextMeshProUGUI second)
    {
        if (first == null)
            return;
        if (!baseSizes.TryGetValue(first, out float size))
            baseSizes[first] = size = first.fontSize;

        for (float f = 1f; f >= 0.55f; f -= 0.05f)
        {
            first.fontSize = size * f;
            if (second != null)
                second.fontSize = size * f;
            Split(body, first, second);
            if (Fits(first) && (second == null || Fits(second)))
                return;
        }
    }

    private static bool Fits(TextMeshProUGUI text)
    {
        TMP_TextInfo info = text.textInfo;
        if (info.lineCount == 0)
            return true;
        return info.lineInfo[info.lineCount - 1].descender >= text.rectTransform.rect.yMin + text.margin.w - 0.0001f;
    }

    private static void Split(string body, TextMeshProUGUI first, TextMeshProUGUI second)
    {
        body ??= string.Empty;
        first.maxVisibleWords = 99999;
        first.overflowMode = TextOverflowModes.Overflow;
        first.text = body;
        first.ForceMeshUpdate(true, true);

        int cut = body.Length;
        if (second != null)
        {
            float bottom = first.rectTransform.rect.yMin + first.margin.w;
            TMP_TextInfo info = first.textInfo;
            for (int i = 0; i < info.lineCount; i++)
            {
                if (info.lineInfo[i].descender >= bottom - 0.0001f)
                    continue;
                while (i > 1 && !IsBlank(info, i - 1) && !EndsLine(info, i - 1))
                    i--;
                int character = info.lineInfo[i].firstCharacterIndex;
                if (character >= 0 && character < info.characterCount)
                    cut = Mathf.Clamp(info.characterInfo[character].index, 0, body.Length);
                break;
            }
        }

        first.text = body.Substring(0, cut).TrimEnd();
        first.ForceMeshUpdate(true, true);
        if (second == null)
            return;

        second.maxVisibleWords = 99999;
        second.overflowMode = TextOverflowModes.Overflow;
        second.text = body.Substring(cut).TrimStart(' ', '\n', '\r', '\t');
        second.ForceMeshUpdate(true, true);
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
                ? string.Format(_headerFormat, (poem.poemDate ?? "").Trim(), (poem.poemPlace ?? "").Trim(), (poem.poemName ?? "").Trim())
                : string.Empty;
        }
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
