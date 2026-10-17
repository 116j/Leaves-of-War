using Hortensia.Runtime;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelFlow : MonoBehaviour
{
    [Flags]
    public enum Condition
    {
        None = 0,
        TalkToCharacter = 1 << 0,    // 1
        PickUpBroom = 1 << 1,        // 2
        SweepLeaves = 1 << 2,        // 4
        OpenNotebook = 1 << 3,       // 8
        CloseNotebook = 1 << 4,      // 16
        PoemFound = 1 << 5,          // 32
        PoemTranscribed = 1 << 6,    // 64
        Wait = 1 << 7,               // 128
        TurnAround = 1 << 8,         // 256
        ObservableBehaviourChanged = 1 << 9,        // 512
        FragmentFound = 1 << 10,     // 1024
        FragmentTranscribed = 1 << 11, // 2048
        LookAtObservable = 1 << 12,  // 4096
    }

    public enum StepAction
    {
        None,
        MoveObjectWhenUnseen,
        ShowFollowerLeaf,
        CharacterLeaves,
        LoadNextLevel,
        CharacterEnters,
        JoinFragments,
        WeatherClears,
        CharacterCardsThenNextLevel
    }

    [Serializable]
    public class Step
    {
        public string note;
        [Tooltip("What the step waits for before its lines play.")]
        public Condition waitFor;
        [Tooltip("Seconds for Wait, leaves to push for Sweep Leaves.")]
        public float amount;
        [Tooltip("Shown at the top while waiting. Empty: nothing.")]
        public string objective;
        [Tooltip("Played after the condition (for Talk To Character: when the player talks).")]
        public DialogueLine[] lines = new DialogueLine[0];
        [Tooltip("Done after the lines.")]
        public StepAction action;
        [Tooltip("The number of the observable witch will be used in an action.")]
        public int observableIndex;
        [Tooltip("Fragment Found / Fragment Transcribed: which piece of the poem (1, 2, 3...).")]
        public int fragment;
    }

    [Header("Level")]
    [Tooltip("Title card shown as the level starts (e.g. MILANO — 1914–1915).")]
    [SerializeField] private string titleCard;
    [SerializeField] private string nextScene;
    [Tooltip("Loaded instead of Next Scene while that level does not exist yet.")]
    [SerializeField] private string fallbackScene = "MainMenu";
    [Tooltip("The level's character: the camera starts on him and Talk To Character uses his trigger.")]
    [SerializeField] private DialogueTrigger character;
    [SerializeField] private string characterName = "Carlo";
    [Tooltip("Optional: what the camera looks at when the level starts. Empty: the character's head.")]
    [SerializeField] private Transform lookTarget;
    [Tooltip("Where the player stands when arriving from the previous level.")]
    [SerializeField] private Transform entryAnchor;
    [Tooltip("The door or place the player turns away from to leave. Its forward also aligns the next level.")]
    [SerializeField] private Transform exitAnchor;
    [SerializeField] private bool followerLeafFromStart;

    [Header("Objects")]
    [SerializeField] private PickupItem broom;
    [SerializeField] private Observable[] observables;
    [SerializeField] private Transform characterExitPoint;
    [Tooltip("Character Enters: where he waits outside the door.")]
    [SerializeField] private Transform characterOutside;
    [Tooltip("Character Enters: the door hinge that opens and closes.")]
    [SerializeField] private Transform door;
    [Tooltip("Used only if the door has no mesh to measure: half the width of the door leaf.")]
    [SerializeField, Min(0.1f)] private float doorHalfWidth = 0.42f;
    [SerializeField] private float doorOpenAngle = -90f;
    [SerializeField, Min(0.1f)] private float doorSeconds = 0.8f;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 0.55f;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.1f;
    [Tooltip("Turn the camera towards the character when the level starts.")]
    [SerializeField] private bool lookAtCharacterOnStart = false;
    [Tooltip("Played from the exit when the next level starts loading (e.g. distant artillery behind the door).")]
    [SerializeField] private AudioClip exitCue;
    [SerializeField, Range(0f, 1f)] private float exitCueVolume = 0.7f;

    [Header("Script")]
    [SerializeField] private Step[] steps = new Step[0];
    [Tooltip("Played the first time the wind covers the poem.")]
    [SerializeField] private DialogueLine[] firstGustLines = new DialogueLine[0];

    [Header("Objective Text")]
    [SerializeField, Min(8f)] private float objectiveSize = 34f;

    public static bool PoemAllowed { get; private set; } = true;

    public static bool Drives(Observable observable)
    {
        foreach (LevelFlow flow in FindObjectsByType<LevelFlow>(FindObjectsInactive.Include))
        {
            if (flow.observables == null || flow.steps == null)
                continue;
            int index = Array.IndexOf(flow.observables, observable);
            if (index < 0)
                continue;
            foreach (Step step in flow.steps)
                if (step != null && step.action == StepAction.MoveObjectWhenUnseen && step.observableIndex == index)
                    return true;
        }
        return false;
    }
    public static bool SweepBlocked { get; private set; }

    public static class Carry
    {
        public static bool Active;
        public static float Yaw;
        public static float Pitch;
    }

    private TextMeshProUGUI objective;
    private string objectiveText;
    private FirstPersonController player;
    private DiaryInteraction diary;
    private BroomSweeper sweeper;
    private TranscribePoem transcribe;
    private bool talked;
    private bool poemFound;
    private bool poemDone;
    private bool gustLinesPlayed;
    private ChangeBehaviour changeBehaviour;
    private AsyncOperation nextLoad;
    private bool changeBehaviourSet;
    private bool leaving;
    private bool loadingFallback;
    private int stepPushed;
    private float stepTime;
    private float turnUnseen;
    private float activeTime;
    private float lookTime;
    private AssignPoem[] fragments = new AssignPoem[0];
    private Vector3 characterSpot;
    private Quaternion? doorClosed;
    private Quaternion characterSpotRotation;

    private void OnEnable()
    {
        TranscribePoem.Transcribed += OnTranscribed;
        TranscribePoem.Gusted += OnGusted;
    }

    private void OnDisable()
    {
        TranscribePoem.Transcribed -= OnTranscribed;
        TranscribePoem.Gusted -= OnGusted;
        if (character != null)
            character.Finished -= OnTalked;
    }

    private void Start()
    {
        Setup();
        if (FadeScreen.Instance != null)
            StartCoroutine(FadeScreen.Instance.DarkenScreen(0f, Begin));
        else
            Begin();
    }

    private void Begin()
    {
        TitleCard.Show(this, titleCard, transcribe != null ? transcribe.PoemFont : null);
        StartCoroutine(Run());
    }

    private void Setup()
    {
        objective = ScreenPrompt.Create(transform, "Objective", objectiveSize, 50f, true);
        PoemAllowed = Array.FindIndex(steps, s => s != null && s.waitFor.HasFlag(Condition.PoemFound)) < 0;
        SweepBlocked = false;
        player = FindAnyObjectByType<FirstPersonController>();
        diary = FindAnyObjectByType<DiaryInteraction>();
        sweeper = FindAnyObjectByType<BroomSweeper>();
        transcribe = FindAnyObjectByType<TranscribePoem>();
        fragments = Array.FindAll(FindObjectsByType<AssignPoem>(FindObjectsInactive.Include), p => p.IsFragment);
        if (observables != null)
        {
        }
        if (character != null)
        {
            character.SetLines(new DialogueLine[0]);
            character.Finished += OnTalked;
        }
        if (broom != null)
            broom.Available = false;
        if (character != null && Array.Exists(steps, s => s != null && s.action == StepAction.CharacterEnters))
        {
            Transform body = CharacterBody();
            characterSpot = body.position;
            characterSpotRotation = body.rotation;
            body.gameObject.SetActive(false);
        }

        if (Carry.Active && player != null)
        {
            Carry.Active = false;
            if (FadeScreen.Instance == null)
                Blink(false);
            Transform entry = entryAnchor != null ? entryAnchor : transform;
            player.Place(new Vector3(entry.position.x, player.transform.position.y, entry.position.z), entry.eulerAngles.y + Carry.Yaw, Carry.Pitch);
        }
        else if (lookAtCharacterOnStart && player != null && character != null)
        {
            player.LookAt(LookPoint());
        }

        if (followerLeafFromStart)
            FollowerLeaf.Spawn();
    }

    private Vector3 LookPoint()
    {
        if (lookTarget != null)
            return lookTarget.position;
        Animator animator = character.GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        return head != null ? head.position : character.transform.position + Vector3.up * 1.6f;
    }

    private IEnumerator Run()
    {
        foreach (Step step in steps)
        {
            if (step == null)
                continue;
            objectiveText = Format(step.objective);

            if (step.waitFor == Condition.TalkToCharacter && character != null)
            {
                talked = false;
                character.SetLines(step.lines);
                yield return new WaitUntil(() => talked);
                character.SetLines(new DialogueLine[0]);
                objectiveText = null;
            }
            else
            {
                stepPushed = sweeper != null ? sweeper.PushedTotal : 0;
                stepTime = Time.time;
                activeTime = 0f;
                lookTime = 0f;
                turnUnseen = 0f;
                SweepBlocked = step.waitFor.HasFlag(Condition.OpenNotebook);
                yield return new WaitUntil(() => Done(step));
                SweepBlocked = false;
                objectiveText = null;
                yield return PlayLines(step.lines);
            }

            if (step.action == StepAction.CharacterEnters)
                yield return CharacterEnters();
            else
                Do(step);
        }
        objectiveText = null;
    }

    private bool Done(Step step)
    {
        bool done = true;
        foreach (Condition cond in Enum.GetValues(typeof(Condition)))
        {
            if (cond == Condition.None) continue;
            if (!step.waitFor.HasFlag(cond)) continue;

            switch (cond)
            {
                case Condition.PickUpBroom:
                    if (broom != null)
                        broom.Available = true;
                    done = done && (sweeper == null || sweeper.HasBroom);
                    break;
                case Condition.SweepLeaves:
                    done = done && (sweeper == null || sweeper.PushedTotal - stepPushed >= step.amount);
                    break;
                case Condition.OpenNotebook:
                    done = done && DiaryInteraction.IsReading;
                    break;
                case Condition.CloseNotebook:
                    done = done && !DiaryInteraction.IsRaised && (diary == null || !diary.Busy);
                    break;
                case Condition.PoemFound:
                    if (!PoemAllowed)
                    {
                        PoemAllowed = true;
                        foreach (AssignPoem poem in FindObjectsByType<AssignPoem>())
                            if (poem.IsRevealed && transcribe != null)
                                transcribe.OnPoemRevealed(poem);
                    }
                    done = done && poemFound;
                    break;
                case Condition.PoemTranscribed:
                    done = done && poemDone;
                    break;
                case Condition.Wait:
                    if (Time.timeScale > 0f && !DialogueRunner.IsPlaying && !DiaryInteraction.IsRaised)
                        activeTime += Time.deltaTime;
                    done = done && activeTime >= step.amount;
                    break;
                case Condition.TurnAround:
                    done = done && TurnedAround();
                    break;
                case Condition.ObservableBehaviourChanged:
                    done = done && IsBehaviourChanged(step.observableIndex);
                    break;
                case Condition.FragmentFound:
                    {
                        AssignPoem piece = Fragment(step.fragment);
                        if (piece != null && transcribe != null && piece.IsRevealed && transcribe.ActivePoem != piece && !transcribe.IsTranscribing)
                            transcribe.OnPoemRevealed(piece);
                        done = done && (piece == null || transcribe == null || piece.IsRevealed && transcribe.ActivePoem == piece);
                        break;
                    }
                case Condition.FragmentTranscribed:
                    {
                        AssignPoem piece = Fragment(step.fragment);
                        done = done && (piece == null || transcribe == null || transcribe.FragmentsDone(piece.PoemId) >= step.fragment);
                        break;
                    }
                case Condition.LookAtObservable:
                    {
                        bool seen = observables != null && step.observableIndex >= 0 && step.observableIndex < observables.Length &&
                            observables[step.observableIndex] != null && observables[step.observableIndex].IsSeen;
                        lookTime = seen && !DialogueRunner.IsPlaying ? lookTime + Time.deltaTime : 0f;
                        done = done && lookTime >= 0.4f;
                        break;
                    }
            }
        }

        return done;
    }

    private bool TurnedAround()
    {
        Transform exit = exitAnchor != null ? exitAnchor : transform;
        bool seen = Gaze.IsSeen(new Bounds(exit.position + Vector3.up * 1.2f, new Vector3(1.2f, 2.2f, 0.4f)), 30f);
        turnUnseen = seen ? 0f : turnUnseen + Time.deltaTime;
        if (turnUnseen < 1f || player == null)
            return false;
        if (nextLoad == null && !string.IsNullOrEmpty(nextScene))
            LoadNext();
        if (nextLoad != null && nextLoad.progress < 0.9f)
            return false;

        Carry.Active = !loadingFallback;
        Carry.Yaw = Mathf.DeltaAngle(exit.eulerAngles.y, player.Yaw);
        Carry.Pitch = player.Pitch;
        Leave();
        return true;
    }

    private IEnumerator PlayLines(DialogueLine[] lines)
    {
        if (lines == null || lines.Length == 0)
            yield break;
        yield return new WaitUntil(() => !DialogueRunner.IsPlaying && !TranscribePoem.IsVoicePlaying && (transcribe == null || !transcribe.IsJoining));
        bool finished = false;
        if (!DialogueRunner.GetOrCreate().Play(lines, () => finished = true))
            yield break;
        yield return new WaitUntil(() => finished);
    }

    private AssignPoem Fragment(int number) => Array.Find(fragments, p => p != null && p.Fragment == number);

    private void PlayExitCue()
    {
        if (exitCue == null)
            return;
        Transform exit = exitAnchor != null ? exitAnchor : transform;
        var go = new GameObject("Exit Cue");
        go.transform.position = exit.position + Vector3.up * 1.5f - exit.forward * 2f;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = exitCue;
        source.volume = exitCueVolume;
        source.spatialBlend = 0.7f;
        source.maxDistance = 25f;
        AudioManager.Route(source, AudioBus.SoundEffects);
        source.Play();
        Destroy(go, exitCue.length + 0.5f);
    }

    private bool IsBehaviourChanged(int observableIndex)
    {
        if (changeBehaviourSet)
            return changeBehaviour.Changed;
        else return false;
    }

    private void Do(Step step)
    {
        switch (step.action)
        {
            case StepAction.MoveObjectWhenUnseen:
                if (observables != null && observables.Length > step.observableIndex)
                {
                    changeBehaviourSet = true;
                    changeBehaviour = observables[step.observableIndex].GetComponent<ChangeBehaviour>();
                    observables[step.observableIndex].OnChangeAllowed.RemoveListener(changeBehaviour.OnChange);
                    changeBehaviour.Arm();
                    observables[step.observableIndex].OnChangeAllowed.AddListener(changeBehaviour.OnChange);
                }
                break;
            case StepAction.CharacterCardsThenNextLevel:
                StartCoroutine(Hortensia.Runtime.CharacterCards.Play(() =>
                {
                    LoadNext();
                    Activate();
                }));
                break;
            case StepAction.WeatherClears:
                foreach (Weather weather in FindObjectsByType<Weather>())
                    weather.Clear();
                break;
            case StepAction.ShowFollowerLeaf:
                FollowerLeaf.Spawn();
                break;
            case StepAction.CharacterLeaves:
                if (character != null)
                    StartCoroutine(CharacterLeaves());
                break;
            case StepAction.LoadNextLevel:
                LoadNext();
                PlayExitCue();
                break;
            case StepAction.JoinFragments:
                if (transcribe != null && fragments.Length > 0)
                    transcribe.JoinFragments(fragments[0].PoemId);
                break;
        }
    }

    private IEnumerator CharacterEnters()
    {
        if (character == null)
            yield break;
        GameObject body = CharacterBody().gameObject;
        Transform t = body.transform;
        var lookers = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in body.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour.GetType().Name == "LookAtPlayer" && behaviour.enabled)
            {
                behaviour.enabled = false;
                lookers.Add(behaviour);
            }

        bool hasDoor = Doorway(characterSpot, out Vector3 inner, out Vector3 outer);
        Vector3 outside = characterOutside != null ? characterOutside.position : characterSpot - t.forward * 2f;
        if (hasDoor && (characterOutside == null || Vector3.Dot(Flat(outside - outer), inner - outer) > 0f))
            outside = outer + Flat(outer - inner).normalized * 0.8f;
        outside.y = characterSpot.y;
        Vector3 first = hasDoor ? outer : characterSpot;

        t.position = outside;
        t.rotation = Quaternion.LookRotation(Flat(first - outside).sqrMagnitude > 1e-4f ? Flat(first - outside) : t.forward, Vector3.up);
        body.SetActive(true);

        yield return SwingDoor(0f, 1f, doorOpenClip);
        Walking(body, true);
        if (hasDoor)
        {
            yield return Walk(t, outer);
            yield return Walk(t, inner);
        }
        yield return Walk(t, characterSpot);
        Walking(body, false);
        StartCoroutine(SwingDoor(1f, 0f, doorCloseClip));

        Quaternion start = t.rotation;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.5f)
        {
            t.rotation = Quaternion.Slerp(start, characterSpotRotation, k);
            yield return null;
        }
        t.rotation = characterSpotRotation;
        foreach (MonoBehaviour behaviour in lookers)
            behaviour.enabled = true;
    }

    private static void Walking(GameObject body, bool on)
    {
        DialogueSpeaker speaker = body.GetComponentInChildren<DialogueSpeaker>(true);
        if (speaker != null)
            speaker.SetWalking(on);
    }

    private bool Doorway(Vector3 room, out Vector3 inner, out Vector3 outer)
    {
        inner = outer = room;
        if (door == null)
            return false;
        float leaf = LeafWidth(out float side);
        Quaternion closed = (door.parent != null ? door.parent.rotation : Quaternion.identity) * (doorClosed ?? door.localRotation);
        Vector3 right = Flat(closed * Vector3.right).normalized * side;
        Vector3 center = door.position + right * leaf * 0.5f;
        Vector3 normal = Flat(closed * Vector3.forward).normalized;
        if (Vector3.Dot(Flat(room - center), normal) < 0f)
            normal = -normal;
        center.y = room.y;
        inner = center + normal * (leaf + 0.45f);
        outer = center - normal * 1.3f;
        return true;
    }

    private float LeafWidth(out float side)
    {
        side = 1f;
        float far = 0f;
        foreach (MeshFilter filter in door.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float x = door.InverseTransformPoint(filter.transform.TransformPoint(corner)).x * door.lossyScale.x;
                if (Mathf.Abs(x) > Mathf.Abs(far))
                    far = x;
            }
        }
        if (Mathf.Abs(far) < 0.3f)
            return doorHalfWidth * 2f;
        side = Mathf.Sign(far);
        return Mathf.Abs(far);
    }

    private static bool Turn(Transform t, Vector3 direction)
    {
        Quaternion look = Quaternion.LookRotation(Flat(direction).normalized, Vector3.up);
        t.rotation = Quaternion.RotateTowards(t.rotation, look, 200f * Time.deltaTime);
        return Quaternion.Angle(t.rotation, look) < 1f;
    }

    private IEnumerator Walk(Transform t, Vector3 target)
    {
        while (true)
        {
            Vector3 to = Flat(target - t.position);
            if (to.sqrMagnitude < 0.0025f)
                yield break;
            if (Turn(t, to))
                t.position += to.normalized * Mathf.Min(to.magnitude, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator SwingDoor(float from, float to, AudioClip clip)
    {
        if (door == null)
            yield break;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, door.position, doorVolume);
        if (!doorClosed.HasValue)
            doorClosed = door.localRotation;
        Quaternion closed = doorClosed.Value;
        for (float k = 0f; k < 1f; k += Time.deltaTime / doorSeconds)
        {
            door.localRotation = closed * Quaternion.Euler(0f, doorOpenAngle * Mathf.SmoothStep(0f, 1f, Mathf.Lerp(from, to, k)), 0f);
            yield return null;
        }
        door.localRotation = closed * Quaternion.Euler(0f, doorOpenAngle * to, 0f);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward * 0.01f : v;
    }

    private void LoadNext()
    {
        if (nextLoad != null || string.IsNullOrEmpty(nextScene))
            return;
        string target = nextScene;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning("LevelFlow: \"" + target + "\" is not in the Build Settings yet, going back to " + fallbackScene + ".", this);
            target = fallbackScene;
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
                return;
        }
        loadingFallback = target != nextScene;
        nextLoad = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
        if (nextLoad != null)
            nextLoad.allowSceneActivation = false;
    }

    private void Leave()
    {
        if (leaving || nextLoad == null)
            return;
        leaving = true;
        if (FadeScreen.Instance != null)
            StartCoroutine(FadeScreen.Instance.DarkenScreen(1f, Activate));
        else
        {
            Blink(true);
            Activate();
        }
    }

    private void Activate()
    {
        if (nextLoad != null)
            nextLoad.allowSceneActivation = true;
    }

    private Transform CharacterBody()
    {
        Transform body = character.transform;
        while (body.parent != null && Size(body.parent) <= 2.5f)
            body = body.parent;
        return body;
    }

    private static float Size(Transform group)
    {
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy)
                continue;
            if (!any)
                bounds = r.bounds;
            else
                bounds.Encapsulate(r.bounds);
            any = true;
        }
        return any ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f;
    }

    private IEnumerator CharacterLeaves()
    {
        GameObject body = CharacterBody().gameObject;
        foreach (MonoBehaviour behaviour in body.GetComponentsInChildren<MonoBehaviour>())
            if (behaviour.GetType().Name == "LookAtPlayer")
                behaviour.enabled = false;

        Transform t = body.transform;
        var path = new System.Collections.Generic.List<Vector3>();
        bool hasDoor = Doorway(t.position, out Vector3 inner, out Vector3 outer);
        if (hasDoor)
        {
            path.Add(inner);
            path.Add(outer);
        }
        Vector3 end = characterExitPoint != null ? characterExitPoint.position : t.position - t.forward * 2f;
        if (hasDoor && (characterExitPoint == null || Vector3.Dot(Flat(end - outer), inner - outer) > 0f))
            end = outer + Flat(outer - inner).normalized * 1.5f;
        path.Add(end);
        for (int i = 0; i < path.Count; i++)
            path[i] = new Vector3(path[i].x, t.position.y, path[i].z);
        int next = 0;
        bool walking = false;
        bool opened = false;
        while (true)
        {
            Vector3 target = path[next];
            Vector3 to = target - t.position;
            to.y = 0f;
            if (to.sqrMagnitude <= 0.01f && next < path.Count - 1)
            {
                if (hasDoor && next == 0 && !opened)
                {
                    opened = true;
                    Walking(body, false);
                    walking = false;
                    yield return SwingDoor(0f, 1f, doorOpenClip);
                }
                next++;
                continue;
            }
            if (to.sqrMagnitude > 0.01f != walking)
            {
                walking = !walking;
                Walking(body, walking);
            }
            if (to.sqrMagnitude > 0.01f)
            {
                if (Turn(t, to))
                    t.position += to.normalized * Mathf.Min(to.magnitude, 0.9f * Time.deltaTime);
            }
            if (Time.timeScale > 0f && !Gaze.IsSeen(body))
            {
                body.SetActive(false);
                if (opened)
                    StartCoroutine(SwingDoor(1f, 0f, doorCloseClip));
                yield break;
            }
            yield return null;
        }
    }

    private void Blink(bool toBlack)
    {
        var go = new GameObject("Blink", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        go.GetComponent<Canvas>().sortingOrder = 32500;
        go.GetComponent<UnityEngine.UI.Image>().color = Color.black;
        go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        if (!toBlack)
            StartCoroutine(FadeOut(group));
    }

    private static IEnumerator FadeOut(CanvasGroup group)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
        {
            group.alpha = 1f - t;
            yield return null;
        }
        Destroy(group.gameObject);
    }

    private void OnTalked() => talked = true;

    private void OnTranscribed(int poemId)
    {
        poemDone = true;
        poemFound = true;
    }

    private void OnGusted(int poemId)
    {
        if (gustLinesPlayed || firstGustLines == null || firstGustLines.Length == 0)
            return;
        gustLinesPlayed = true;
        StartCoroutine(PlayLines(firstGustLines));
    }

    private string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        return text.Replace("{character}", characterName)
            .Replace("{diaryKey}", ScreenPrompt.KeyName(player != null ? player.DiaryAction : null, "I"))
            .Replace("{sweepKey}", ScreenPrompt.KeyName(player != null ? player.SweepAction : null, "LMB"))
            .Replace("{writeKey}", ScreenPrompt.KeyName(player != null ? player.WriteAction : null, "T"));
    }

    private void Update()
    {
        if (transcribe != null && transcribe.IsPoemUncovered)
            poemFound = true;

        bool hidden = string.IsNullOrEmpty(objectiveText) || DialogueRunner.IsPlaying || DiaryInteraction.IsRaised || Time.timeScale <= 0f;
        ScreenPrompt.Show(objective, !hidden, objectiveText);
    }
}
