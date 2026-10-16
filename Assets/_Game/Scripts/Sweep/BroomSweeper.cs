using Hortensia.Runtime;
using TMPro;
using UnityEngine;

public class BroomSweeper : MonoBehaviour
{
    [Tooltip("The broom held by the player camera.")]
    [SerializeField] private Transform broom;
    [Tooltip("The tip of the broom")]
    [SerializeField] private Transform broomTip;
    [Tooltip("Floor layer mask")]
    [SerializeField] private LayerMask floorLayer;

    [Header("Sweeping")]
    [Tooltip("Metres the broom head moves per pixel of mouse movement.")]
    [SerializeField, Min(0.0001f)] private float mouseSensitivity = 0.004f;
    [SerializeField, Min(0.1f)] private float halfWidth = 0.9f;
    [SerializeField] private Vector2 reachRange = new Vector2(0.8f, 1.9f);
    [SerializeField, Min(0.1f)] private float reach = 1.25f;
    [SerializeField, Min(0.05f)] private float radius = 0.35f;

    [Header("Broom Pose")]
    [SerializeField] private Vector3 sweepPositionOffset = new Vector3(0f, -0.12f, 0.15f);
    [Tooltip("Extra tilt while sweeping, around the camera's axes. Zero: the broom keeps exactly the rotation you gave it in the editor, held or sweeping.")]
    [SerializeField] private Vector3 sweepRotationOffset = Vector3.zero;
    [SerializeField, Range(0f, 60f)] private float swingAngle = 25f;
    [SerializeField, Range(0f, 1f)] private float swingShift = 0.25f;
    [SerializeField, Min(0.1f)] private float blendSpeed = 8f;

    [Header("Audio")]
    [Tooltip("Slow sweeping loop on this level's floor.")]
    [SerializeField] private AudioClip sweepClip;
    [Tooltip("Fast sweeping loop: crossfades in when the broom moves quickly.")]
    [SerializeField] private AudioClip fastClip;
    [SerializeField, Min(0.1f)] private float fastSpeed = 1.6f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    [Tooltip("Played now and then while leaves are pushed.")]
    [SerializeField] private AudioClip[] leafClips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float leafVolume = 0.4f;

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
    public static bool Holding { get; private set; }
    public int PushedTotal => pushedTotal;

    private FirstPersonController player;
    private AudioSource source;
    private AudioSource fastSource;
    private AudioSource leafSource;
    private int audioPushed;
    private float nextLeaf;
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
        if (floorLayer.value == 0)
            floorLayer = LayerMask.GetMask("Floor");
        HasBroom = startInHand;
        if (!HasBroom && broom != null)
            broom.gameObject.SetActive(false);

        if (sweepClip == null)
            sweepClip = Resources.Load<AudioClip>("MainMenu/Sweep");
        source = LoopSource(sweepClip);
        if (fastClip != null)
            fastSource = LoopSource(fastClip);
        leafSource = gameObject.AddComponent<AudioSource>();
        leafSource.playOnAwake = false;
        leafSource.spatialBlend = 0f;
        AudioManager.Route(leafSource, AudioBus.SoundEffects);

        prompt = ScreenPrompt.Create(transform, "Sweep Prompt", promptSize, 40f);
    }

    private void OnDisable()
    {
        Active = false;
        Ready = false;
        Holding = false;
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
        !DiaryInteraction.IsRaised && !DialogueRunner.LocksPlayer && !Examinable.IsOpen && !LevelFlow.SweepBlocked &&
        !(PauseController.Instance != null && PauseController.Instance.IsPaused);

    private void Update()
    {
        bool can = CanSweep;
        Ready = can;
        Holding = HasBroom;
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

            Vector3 head = GetBroomTipWorldPosition();
            if (wasSweeping && dt > 0f && weight >= 0.9f)
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
        ScreenPrompt.Show(prompt, hint, hint ? string.Format(promptFormat, ScreenPrompt.KeyName(player != null ? player.SweepAction : null, "LMB")) : null);
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
        Vector3 targetPosition = heldPosition + sweepPositionOffset + Vector3.right * side * swingShift + Vector3.up * (currentReach - reach) * 0.2f;
        Quaternion targetRotation = Quaternion.AngleAxis(swingAngle * side, Vector3.up) * Quaternion.Euler(sweepRotationOffset) * heldRotation;
        float k = Mathf.SmoothStep(0f, 1f, weight);
        broom.localPosition = Vector3.Lerp(heldPosition, targetPosition, k);
        broom.localRotation = Quaternion.Slerp(heldRotation, targetRotation, k);
    }

    private Vector3 GetBroomTipWorldPosition()
    {
        Camera view = Camera.main;
        Camera overlay = HeldItemsCamera.Overlay != null ? HeldItemsCamera.Overlay : view;
        if (view == null || overlay == null || broom == null)
            return transform.position;
        Vector3 tipPos = broomTip != null ? broomTip.position : broom.position;
        Vector3 tipScreen = overlay.WorldToViewportPoint(tipPos);

        if (tipScreen.z <= 0f)
            tipScreen = new Vector3(0.5f, 0.15f, 1f);

        Ray ray = view.ViewportPointToRay(tipScreen);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer))
            return hit.point;

        return ray.GetPoint(10f);
    }

    private AudioSource LoopSource(AudioClip clip)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        AudioManager.Route(s, AudioBus.SoundEffects);
        return s;
    }

    private void UpdateAudio(bool sweeping, float dt)
    {
        float target = sweeping ? volume * Mathf.Clamp01(headSpeed / 1.2f) : 0f;
        float fast = fastSource != null ? Mathf.Clamp01((headSpeed - fastSpeed * 0.6f) / (fastSpeed * 0.6f)) : 0f;
        Fade(source, target * (1f - fast), dt);
        Fade(fastSource, target * fast, dt);

        if (leafClips != null && leafClips.Length > 0 && leafSource != null)
        {
            if (pushedTotal > audioPushed + 3 && Time.time >= nextLeaf)
            {
                AudioClip clip = leafClips[Random.Range(0, leafClips.Length)];
                if (clip != null)
                    leafSource.PlayOneShot(clip, leafVolume * Random.Range(0.75f, 1f));
                nextLeaf = Time.time + Random.Range(0.25f, 0.5f);
                audioPushed = pushedTotal;
            }
            else if (!sweeping)
            {
                audioPushed = pushedTotal;
            }
        }
    }

    private static void Fade(AudioSource s, float target, float dt)
    {
        if (s == null || s.clip == null)
            return;
        s.volume = Mathf.MoveTowards(s.volume, target, dt * (target > s.volume ? 6f : 3f));
        if (s.volume > 0.001f && !s.isPlaying)
        {
            s.time = Random.Range(0f, s.clip.length * 0.9f);
            s.Play();
        }
        else if (s.volume <= 0.001f && s.isPlaying)
        {
            s.Stop();
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 head = GetBroomTipWorldPosition();
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(head, radius);
    }
}
