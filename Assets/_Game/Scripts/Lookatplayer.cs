using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LookAtPlayer : MonoBehaviour
    {
        [Header("Head")]
        [Tooltip("Head bone. Empty = found automatically on a Humanoid model.")]
        [SerializeField] private Transform head;
        [Tooltip("Optional neck bone, shares part of the rotation. Empty = found automatically on a Humanoid model.")]
        [SerializeField] private Transform neck;
        [SerializeField, Range(0f, 1f)] private float neckShare = 0.35f;
        [Tooltip("How far the head turns left/right before stopping.")]
        [SerializeField, Range(0f, 120f)] private float maxYaw = 70f;
        [Tooltip("How far the head turns up/down before stopping.")]
        [SerializeField, Range(0f, 80f)] private float maxPitch = 35f;
        [Tooltip("Beyond this angle the player is behind: the head goes back to the centre.")]
        [SerializeField, Range(0f, 180f)] private float giveUpAngle = 110f;
        [SerializeField] private float headSpeedDegreesPerSecond = 180f;

        [Header("Body")]
        [Tooltip("Also turn the whole body towards the player.")]
        [SerializeField] private bool turnBody = false;
        [Tooltip("Object that turns with Turn Body. Empty = this object.")]
        [SerializeField] private Transform pivot;
        [SerializeField] private float turnSpeedDegreesPerSecond = 240f;
        [Tooltip("Use it if the model does not face its blue (Z) arrow: e.g. 180 if it shows its back to the player.")]
        [SerializeField] private float yawOffset = 0f;

        [Header("When")]
        [Tooltip("Look at the player only during a dialogue (needs a DialogueSpeaker on this object or a parent). Off = always.")]
        [SerializeField] private bool onlyDuringDialogue = false;
        [Tooltip("Outside dialogues, look at the player only within this distance (metres). 0 = any distance.")]
        [SerializeField, Min(0f)] private float lookRange = 0f;
        [Tooltip("Body only: turn back to the starting direction when the dialogue ends.")]
        [SerializeField] private bool returnWhenDone = true;
        [Tooltip("Empty = the FirstPersonController camera, otherwise the main camera.")]
        [SerializeField] private Transform target;

        private Quaternion restRotation;
        private float restYaw;
        private float bodyYaw;
        private float headYaw;
        private float headPitch;
        private bool inDialogue;
        private Quaternion headBase;
        private Quaternion neckBase;
        private Quaternion headApplied;
        private Quaternion neckApplied;
        private bool applied;
        private bool warned;

        public void SetDialogueActive(bool active) => inDialogue = active;

        private void Awake()
        {
            if (pivot == null)
                pivot = transform;
            restRotation = pivot.rotation;
            restYaw = restRotation.eulerAngles.y;
            bodyYaw = restYaw;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                if (head == null)
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (neck == null)
                    neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            }
        }

        private void OnEnable()
        {
            if (pivot != null)
                bodyYaw = pivot.rotation.eulerAngles.y;
        }

        private void LateUpdate()
        {
            bool hasTarget = TryGetTarget(out _);
            Vector3 targetPosition = hasTarget ? target.position : Vector3.zero;
            if (hasTarget && !inDialogue)
            {
                bool inRange = lookRange <= 0f || (targetPosition - transform.position).sqrMagnitude <= lookRange * lookRange;
                hasTarget = !onlyDuringDialogue && inRange;
            }

            if (head == null && !warned)
            {
                Debug.Log($"LookAtPlayer on '{name}': no head found, turning the whole body. Assign Head to move only the head.", this);
                warned = true;
            }

            if (turnBody || head == null)
                UpdateBody(hasTarget, targetPosition);

            if (head != null)
                UpdateHead(hasTarget, targetPosition);
        }

        private void UpdateBody(bool hasTarget, Vector3 targetPosition)
        {
            float goal = restYaw;
            if (hasTarget)
            {
                Vector3 flat = targetPosition - pivot.position;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.0001f)
                    return;
                goal = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg + yawOffset;
            }
            else if (!returnWhenDone)
            {
                return;
            }

            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, goal, turnSpeedDegreesPerSecond * Time.deltaTime);
            pivot.rotation = Quaternion.AngleAxis(Mathf.DeltaAngle(restYaw, bodyYaw), Vector3.up) * restRotation;
        }

        private void UpdateHead(bool hasTarget, Vector3 targetPosition)
        {
            Quaternion body = Quaternion.Euler(0f, pivot.rotation.eulerAngles.y + yawOffset, 0f);
            float goalYaw = 0f;
            float goalPitch = 0f;

            if (hasTarget)
            {
                Vector3 local = Quaternion.Inverse(body) * (targetPosition - head.position);
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(yaw) <= giveUpAngle)
                {
                    goalYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
                    float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                    goalPitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
                }
            }

            RestoreUnanimatedPose();

            float step = headSpeedDegreesPerSecond * Time.deltaTime;
            headYaw = Mathf.MoveTowards(headYaw, goalYaw, step);
            headPitch = Mathf.MoveTowards(headPitch, goalPitch, step);
            if (Mathf.Approximately(headYaw, 0f) && Mathf.Approximately(headPitch, 0f))
            {
                applied = false;
                return;
            }

            Quaternion look = body * Quaternion.Euler(headPitch, headYaw, 0f) * Quaternion.Inverse(body);
            if (neck != null && neckShare > 0f)
            {
                Quaternion neckPart = Quaternion.Slerp(Quaternion.identity, look, neckShare);
                neck.rotation = neckPart * neck.rotation;
                head.rotation = look * Quaternion.Inverse(neckPart) * head.rotation;
            }
            else
            {
                head.rotation = look * head.rotation;
            }

            headApplied = head.localRotation;
            if (neck != null)
                neckApplied = neck.localRotation;
            applied = true;
        }

        private void RestoreUnanimatedPose()
        {
            if (applied && head.localRotation == headApplied)
                head.localRotation = headBase;
            else
                headBase = head.localRotation;

            if (neck == null)
                return;
            if (applied && neck.localRotation == neckApplied)
                neck.localRotation = neckBase;
            else
                neckBase = neck.localRotation;
        }

        private bool TryGetTarget(out Transform found)
        {
            if (target == null)
            {
                FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null)
                    target = controller.PlayerCamera != null ? controller.PlayerCamera.transform : controller.transform;
                else if (Camera.main != null)
                    target = Camera.main.transform;
            }

            found = target;
            return target != null;
        }
    }
}
