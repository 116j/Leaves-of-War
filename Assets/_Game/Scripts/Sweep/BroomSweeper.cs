using Hortensia.Runtime;
using TMPro;
using UnityEngine;

public class BroomSweeper : MonoBehaviour
{
    [Tooltip("The broom held by the player camera.")]
    [SerializeField] private Transform broom;

    [Header("Sweeping")]
    [Tooltip("Metres the broom head moves per pixel of mouse movement.")]
    [SerializeField, Min(0.0001f)] private float mouseSensitivity = 0.004f;
    [SerializeField, Min(0.1f)] private float halfWidth = 0.9f;
    [SerializeField] private Vector2 reachRange = new Vector2(0.8f, 1.9f);
    [SerializeField, Min(0.1f)] private float reach = 1.25f;
    [SerializeField, Min(0.05f)] private float radius = 0.35f;

    [Header("Broom Pose")]
    [SerializeField] private Vector3 sweepPositionOffset = new Vector3(0f, -0.12f, 0.15f);
    [SerializeField] private Vector3 sweepRotationOffset = new Vector3(25f, 0f, 0f);
    [SerializeField, Range(0f, 60f)] private float swingAngle = 25f;
    [SerializeField, Range(0f, 1f)] private float swingShift = 0.25f;
    [SerializeField, Min(0.1f)] private float blendSpeed = 8f;

    [Header("Audio")]
    [SerializeField] private AudioClip sweepClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    [Header("Prompt")]
    [SerializeField] private string promptFormat = "HOLD {0} AND MOVE THE MOUSE TO SWEEP";
    [SerializeField, Min(8f)] private float promptSize = 30f;
    [Tooltip("Leaves to push before the hint stops showing.")]
    [SerializeField, Min(0)] private int hintLeaves = 300;

    [Tooltip("Off: the broom stays on its pickup until the player takes it.")]
    [SerializeField] private bool startInHand = true;

    public static bool Active { get; private set; }
    public static bool Ready { get; private set; }
    public bool HasBroom { get; private set; }
    public int PushedTotal => pushedTotal;

    private FirstPersonController player;
    private AudioSource source;
    private TextMeshProUGUI prompt;
    private Vector3 heldPosition;
    private Quaternion heldRotation;
    private Vector3 lastHead;
    private float offset;
    private float currentReach;
    private float weight;
    private float headSpeed;
    private bool wasSweeping;
    private int pushedTotal;

    private void Start()
    {
        player = GetComponentInParent<FirstPersonController>();
        if (player == null)
            player = FindAnyObjectByType<FirstPersonController>();
        if (broom != null)
        {
            heldPosition = broom.localPosition;
            heldRotation = broom.localRotation;
        }
        currentReach = reach;
        HasBroom = startInHand;
        if (!HasBroom && broom != null)
            broom.gameObject.SetActive(false);

        if (sweepClip == null)
            sweepClip = Resources.Load<AudioClip>("MainMenu/Sweep");
        source = gameObject.AddComponent<AudioSource>();
        source.clip = sweepClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        AudioManager.Route(source, AudioBus.SoundEffects);

        prompt = ScreenPrompt.Create(transform, "Sweep Prompt", promptSize, 40f);
    }

    private void OnDisable()
    {
        Active = false;
        Ready = false;
        ScreenPrompt.Show(prompt, false, null);
    }

    public void GiveBroom()
    {
        HasBroom = true;
        if (broom != null)
            broom.gameObject.SetActive(true);
    }

    private bool CanSweep =>
        HasBroom && player != null && player.SweepAction != null && LeafField.Instance != null &&
        Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f &&
        !DiaryInteraction.IsRaised && !DialogueRunner.LocksPlayer &&
        !(PauseController.Instance != null && PauseController.Instance.IsPaused);

    private void Update()
    {
        bool can = CanSweep;
        Ready = can;
        bool sweeping = can && player.SweepAction.IsPressed();
        Active = sweeping;
        float dt = Time.deltaTime;

        Transform body = player != null ? player.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        if (sweeping)
        {
            Vector2 delta = player.LookAction != null ? player.LookAction.ReadValue<Vector2>() : Vector2.zero;
            offset = Mathf.Clamp(offset + delta.x * mouseSensitivity, -halfWidth, halfWidth);
            currentReach = Mathf.Clamp(currentReach + delta.y * mouseSensitivity, reachRange.x, reachRange.y);

            Vector3 head = body.position + forward * currentReach + right * offset;
            if (wasSweeping && dt > 0f)
            {
                headSpeed = Vector3.Distance(head, lastHead) / dt;
                pushedTotal += LeafField.Instance.Push(lastHead, head, radius, forward, headSpeed);
            }
            lastHead = head;
        }
        else
        {
            headSpeed = 0f;
            offset = Mathf.MoveTowards(offset, 0f, dt * 2f);
            currentReach = Mathf.MoveTowards(currentReach, reach, dt * 2f);
        }
        wasSweeping = sweeping;

        UpdateBroom(sweeping, dt);
        UpdateAudio(sweeping, dt);

        bool inField = LeafField.Instance != null && LeafField.Instance.Area.Contains(new Vector3(body.position.x, LeafField.Instance.Area.center.y, body.position.z));
        bool hint = can && !sweeping && inField && pushedTotal < hintLeaves;
        ScreenPrompt.Show(prompt, hint, string.Format(promptFormat, ScreenPrompt.KeyName(player != null ? player.SweepAction : null, "LMB")));
    }

    private void UpdateBroom(bool sweeping, float dt)
    {
        if (broom == null || DiaryInteraction.IsRaised)
            return;

        float previous = weight;
        weight = Mathf.MoveTowards(weight, sweeping ? 1f : 0f, dt * blendSpeed);
        if (weight <= 0f && previous <= 0f)
            return;

        float side = halfWidth > 0f ? offset / halfWidth : 0f;
        Vector3 targetPosition = heldPosition + sweepPositionOffset + Vector3.right * side * swingShift + Vector3.forward * (currentReach - reach) * 0.2f;
        Quaternion targetRotation = Quaternion.AngleAxis(swingAngle * side, Vector3.up) * heldRotation * Quaternion.Euler(sweepRotationOffset);
        float k = Mathf.SmoothStep(0f, 1f, weight);
        broom.localPosition = Vector3.Lerp(heldPosition, targetPosition, k);
        broom.localRotation = Quaternion.Slerp(heldRotation, targetRotation, k);
    }

    private void UpdateAudio(bool sweeping, float dt)
    {
        if (source == null || source.clip == null)
            return;

        float target = sweeping ? volume * Mathf.Clamp01(headSpeed / 1.2f) : 0f;
        source.volume = Mathf.MoveTowards(source.volume, target, dt * (target > source.volume ? 6f : 3f));
        if (source.volume > 0.001f && !source.isPlaying)
        {
            source.time = Random.Range(0f, source.clip.length * 0.9f);
            source.Play();
        }
        else if (source.volume <= 0.001f && source.isPlaying)
        {
            source.Stop();
        }
    }
}
