using System;
using System.Collections;
using Hortensia.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelFlow : MonoBehaviour
{
    public enum Condition
    {
        None,
        TalkToCharacter,
        PickUpBroom,
        SweepLeaves,
        OpenNotebook,
        CloseNotebook,
        PoemFound,
        PoemTranscribed,
        Wait,
        TurnAround
    }

    public enum StepAction
    {
        None,
        MoveObjectWhenUnseen,
        ShowFollowerLeaf,
        CharacterLeaves,
        LoadNextLevel,
        CharacterEnters
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
    }

    [Header("Level")]
    [Tooltip("Title card shown as the level starts (e.g. MILANO — 1914–1915).")]
    [SerializeField] private string titleCard;
    [SerializeField] private string nextScene;
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
    [SerializeField] private Transform movable;
    [SerializeField] private Transform movableTarget;
    [SerializeField] private Transform characterExitPoint;
    [Tooltip("Character Enters: where he waits outside the door.")]
    [SerializeField] private Transform characterOutside;
    [Tooltip("Character Enters: the door hinge that opens and closes.")]
    [SerializeField] private Transform door;
    [SerializeField] private float doorOpenAngle = -90f;
    [SerializeField, Min(0.1f)] private float doorSeconds = 0.8f;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 0.55f;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.1f;
    [Tooltip("Turn the camera towards the character when the level starts.")]
    [SerializeField] private bool lookAtCharacterOnStart = false;

    [Header("Script")]
    [SerializeField] private Step[] steps = new Step[0];
    [Tooltip("Played the first time the wind covers the poem.")]
    [SerializeField] private DialogueLine[] firstGustLines = new DialogueLine[0];

    [Header("Objective Text")]
    [SerializeField, Min(8f)] private float objectiveSize = 34f;

    public static bool PoemAllowed { get; private set; } = true;

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
    private bool moveWhenUnseen;
    private AsyncOperation nextLoad;
    private bool leaving;
    private int stepPushed;
    private float stepTime;
    private float turnUnseen;
    private float activeTime;
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
        PoemAllowed = Array.FindIndex(steps, s => s != null && s.waitFor == Condition.PoemFound) < 0;
        player = FindAnyObjectByType<FirstPersonController>();
        diary = FindAnyObjectByType<DiaryInteraction>();
        sweeper = FindAnyObjectByType<BroomSweeper>();
        transcribe = FindAnyObjectByType<TranscribePoem>();
        if (character != null)
        {
            character.SetLines(new DialogueLine[0]);
            character.Finished += OnTalked;
        }
        if (broom != null)
            broom.Available = false;
        if (character != null && Array.Exists(steps, s => s != null && s.action == StepAction.CharacterEnters))
        {
            Transform body = character.transform.root;
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
                turnUnseen = 0f;
                yield return new WaitUntil(() => Done(step));
                objectiveText = null;
                yield return PlayLines(step.lines);
            }

            if (step.action == StepAction.CharacterEnters)
                yield return CharacterEnters();
            else
                Do(step.action);
        }
        objectiveText = null;
    }

    private bool Done(Step step)
    {
        switch (step.waitFor)
        {
            case Condition.PickUpBroom:
                if (broom != null)
                    broom.Available = true;
                return sweeper == null || sweeper.HasBroom;
            case Condition.SweepLeaves:
                return sweeper == null || sweeper.PushedTotal - stepPushed >= step.amount;
            case Condition.OpenNotebook:
                return DiaryInteraction.IsReading;
            case Condition.CloseNotebook:
                return !DiaryInteraction.IsRaised && (diary == null || !diary.Busy);
            case Condition.PoemFound:
                if (!PoemAllowed)
                {
                    PoemAllowed = true;
                    foreach (AssignPoem poem in FindObjectsByType<AssignPoem>(FindObjectsSortMode.None))
                        if (poem.IsRevealed && transcribe != null)
                            transcribe.OnPoemRevealed(poem);
                }
                return poemFound;
            case Condition.PoemTranscribed:
                return poemDone;
            case Condition.Wait:
                if (Time.timeScale > 0f && !DialogueRunner.IsPlaying && !DiaryInteraction.IsRaised)
                    activeTime += Time.deltaTime;
                return activeTime >= step.amount;
            case Condition.TurnAround:
                return TurnedAround();
            default:
                return true;
        }
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

        Carry.Active = true;
        Carry.Yaw = Mathf.DeltaAngle(exit.eulerAngles.y, player.Yaw);
        Carry.Pitch = player.Pitch;
        Leave();
        return true;
    }

    private IEnumerator PlayLines(DialogueLine[] lines)
    {
        if (lines == null || lines.Length == 0)
            yield break;
        yield return new WaitUntil(() => !DialogueRunner.IsPlaying);
        bool finished = false;
        if (!DialogueRunner.GetOrCreate().Play(lines, () => finished = true))
            yield break;
        yield return new WaitUntil(() => finished);
    }

    private void Do(StepAction action)
    {
        switch (action)
        {
            case StepAction.MoveObjectWhenUnseen:
                moveWhenUnseen = movable != null && movableTarget != null;
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
                break;
        }
    }

    private IEnumerator CharacterEnters()
    {
        if (character == null)
            yield break;
        GameObject body = character.transform.root.gameObject;
        Transform t = body.transform;
        var lookers = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in body.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour.GetType().Name == "LookAtPlayer" && behaviour.enabled)
            {
                behaviour.enabled = false;
                lookers.Add(behaviour);
            }

        Vector3 outside = characterOutside != null ? characterOutside.position : characterSpot - t.forward * 2f;
        outside.y = characterSpot.y;
        Vector3 threshold = door != null ? new Vector3(door.position.x, characterSpot.y, door.position.z) : outside;
        if (door != null)
            threshold += (characterSpot - threshold).normalized * 0.6f + door.right * 0.42f;
        threshold.y = characterSpot.y;

        t.position = outside;
        t.rotation = Quaternion.LookRotation(Flat(threshold - outside), Vector3.up);
        body.SetActive(true);

        yield return SwingDoor(0f, 1f, doorOpenClip);
        yield return Walk(t, threshold);
        yield return Walk(t, characterSpot);
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

    private IEnumerator Walk(Transform t, Vector3 target)
    {
        while (true)
        {
            Vector3 to = Flat(target - t.position);
            if (to.sqrMagnitude < 0.0025f)
                yield break;
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), Time.deltaTime * 6f);
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
        nextLoad = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
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

    private IEnumerator CharacterLeaves()
    {
        GameObject body = character.transform.root.gameObject;
        foreach (MonoBehaviour behaviour in body.GetComponentsInChildren<MonoBehaviour>())
            if (behaviour.GetType().Name == "LookAtPlayer")
                behaviour.enabled = false;

        Transform t = body.transform;
        Vector3 target = characterExitPoint != null ? characterExitPoint.position : t.position - t.forward * 2f;
        target.y = t.position.y;
        while (true)
        {
            Vector3 to = target - t.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 5f);
                t.position += to.normalized * Mathf.Min(to.magnitude, 0.9f * Time.deltaTime);
            }
            if (Time.timeScale > 0f && !Gaze.IsSeen(body))
            {
                body.SetActive(false);
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
        if (moveWhenUnseen && !Gaze.IsSeen(movable.gameObject))
        {
            moveWhenUnseen = false;
            movable.SetPositionAndRotation(movableTarget.position, movableTarget.rotation);
        }

        if (transcribe != null && transcribe.IsPoemUncovered)
            poemFound = true;

        bool hidden = string.IsNullOrEmpty(objectiveText) || DialogueRunner.IsPlaying || DiaryInteraction.IsRaised || Time.timeScale <= 0f;
        ScreenPrompt.Show(objective, !hidden, objectiveText);
    }
}
