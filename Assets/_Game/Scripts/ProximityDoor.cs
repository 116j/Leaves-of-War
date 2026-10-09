using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Attach to a door mesh (e.g. a modified cube). Adds:
    ///
    /// 1. Physical collision: if this GameObject has no solid Collider, one is
    ///    added automatically, sized to the mesh bounds, so the player cannot
    ///    walk through the door.
    ///
    /// 2. Proximity open/close: every frame, checks real-world distance to the
    ///    player and opens/closes accordingly.
    ///
    /// 3. Hinge swing: the door swings around an actual hinge point on one
    ///    edge of its mesh (left or right), not around its own pivot/centre -
    ///    this works correctly even if the mesh's own pivot is at its centre.
    ///
    /// 4. Open/close sound, played once per state change via an AudioSource
    ///    (added automatically if missing).
    ///
    /// 5. Linked doors (double-door groups): assign other ProximityDoor
    ///    leaves in Linked Doors below to make this door open/close together
    ///    with them - the whole group opens if the player is near ANY one
    ///    leaf's own detection radius. Each leaf keeps its own hinge side,
    ///    swing angle, and detection radius (typical for a double door: one
    ///    leaf hinged Left, the other Right, swinging apart symmetrically).
    ///    Leave Linked Doors empty for a normal single door - nothing else
    ///    changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProximityDoor : MonoBehaviour
    {
        [Header("Collision")]
        [Tooltip("If no solid Collider is found on this object, one is added automatically at Awake, sized to the mesh bounds.")]
        [SerializeField] private bool autoAddCollisionIfMissing = true;

        [Header("Proximity Detection")]
        [Tooltip("Optional: drag the player object here directly. If left empty, the script looks it up via GameSession (which only exists once you start the game from the main menu) - assign this to test a scene in isolation.")]
        [SerializeField] private global::FirstPersonController playerOverride;
        [Tooltip("How close the player must get (in real-world metres) for the door to open.")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 2.5f;
        [Tooltip("How often (seconds) to re-check the player's distance. 0 = every frame.")]
        [SerializeField, Min(0f)] private float checkInterval = 0.1f;
        [Tooltip("If the door's pivot is far above/below where the player actually walks, ignore height and compare only horizontal distance.")]
        [SerializeField] private bool ignoreHeightDifference = true;

        [Header("Linked Doors (Double Door Group)")]
        [Tooltip("Other door leaves that should open/close together with this one - e.g. the second leaf of a double door. Assign each leaf's OTHER leaf(s) here (mutual: leaf A links to leaf B, and leaf B links to leaf A). The group opens if the player is within ANY linked leaf's own Detection Radius. Leave empty for a normal single door.")]
        [SerializeField] private List<ProximityDoor> linkedDoors = new List<ProximityDoor>();

        [Header("Hinge Swing")]
        [Tooltip("Which side of the mesh's own bounds (in its local/object space) the hinge sits on. The door swings around that edge, not its own pivot.")]
        [SerializeField] private HingeSide hingeSide = HingeSide.Left;

        [Header("Hinge Override")]
        [Tooltip("If enabled, uses Manual Hinge Local Position (draggable in Scene view) instead of computing the hinge from the mesh bounds.")]
        [SerializeField] private bool manualHinge;
        [Tooltip("Local-space position of the hinge, used only when Manual Hinge is enabled. With the door selected, drag the Move handle shown in Scene view - no need to type coordinates by hand.")]
        [SerializeField] private Vector3 manualHingeLocalPosition;
        [Tooltip("World-space axis to swing around. Doors normally swing around the vertical (world Y) regardless of any odd rotation baked into the mesh.")]
        [SerializeField] private Vector3 swingAxisWorld = Vector3.up;
        [Tooltip("Rotation (degrees) when fully open. Negative values swing the other way.")]
        [SerializeField] private float openAngle = 90f;
        [Tooltip("Degrees per second while opening/closing.")]
        [SerializeField, Min(1f)] private float swingSpeed = 180f;

        [Header("Sound")]
        [Tooltip("Played once when the door starts opening.")]
        [SerializeField] private AudioClip openSound;
        [Tooltip("Played once when the door starts closing.")]
        [SerializeField] private AudioClip closeSound;
        [Range(0f, 100f)]
        [SerializeField] private float soundVolume = 30f;

        [Header("Swing Safety Wall")]
        [Tooltip("While the door is actively swinging (not resting fully open or fully closed), an invisible solid wall keeps the player back, so they can't get pinched between the moving door and its frame. Auto-sized from the door's own mesh bounds (see Safety Wall Size Multiplier) unless Safety Wall Manual Size is enabled below.")]
        [SerializeField] private float safetyWallSizeMultiplier = 2f;
        [Tooltip("If enabled, ignores the door's mesh bounds and uses Safety Wall Local Size/Offset below directly instead.")]
        [SerializeField] private bool safetyWallManualSize = false;
        [SerializeField] private Vector3 safetyWallLocalSize = new Vector3(1.5f, 3f, 1.5f);
        [SerializeField] private Vector3 safetyWallLocalOffset = Vector3.zero;

        private BoxCollider safetyWall;

        private enum HingeSide { Left, Right }

        private Vector3 hingeWorldPoint;
        private Vector3 closedWorldPosition;
        private Quaternion closedWorldRotation;
        private float swingProgress; // 0 = closed, 1 = fully open
        private bool isOpen;
        private bool wasOpen;
        // This leaf's OWN proximity check result, independent of any linked
        // leaves - other doors in the same group read this via IsInProximity
        // to decide whether the whole group should be open.
        private bool localProximity;
        private float nextCheckTime;
        private global::FirstPersonController cachedPlayer;
        private AudioSource audioSource;
        private FlagGatedObject flagGate;

        /// <summary>
        /// Whether the PLAYER is within THIS leaf's own detection radius
        /// right now, regardless of any linked doors. Other ProximityDoor
        /// instances in the same group read this to decide whether to open
        /// too - see linkedDoors.
        /// </summary>
        public bool IsInProximity => localProximity;

        private void Awake()
        {
            EnsureSolidCollision();
            EnsureAudioSource();
            EnsureSafetyWall();
            // Checks this object first, then walks up the hierarchy - so a
            // single FlagGatedObject on a shared parent (e.g. an empty
            // "DoubleDoor" container holding both leaves) gates both leaves
            // at once, without needing to duplicate it on each leaf.
            flagGate = GetComponentInParent<FlagGatedObject>();

            closedWorldPosition = transform.position;
            closedWorldRotation = transform.rotation;
            hingeWorldPoint = ComputeHingeWorldPoint();
        }

        private void Update()
        {
            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                UpdateProximity();
            }

            if (isOpen != wasOpen)
            {
                PlayStateSound(isOpen);
                wasOpen = isOpen;
            }

            float progressPerSecond = swingSpeed / Mathf.Max(1f, Mathf.Abs(openAngle));
            float target = isOpen ? 1f : 0f;
            swingProgress = Mathf.MoveTowards(swingProgress, target, progressPerSecond * Time.deltaTime);

            // The door moves via direct transform manipulation, not physics,
            // so the player's CharacterController is never pushed out of the
            // way while it swings. This temporary wall keeps them back from
            // the moving mesh until it settles fully open or fully closed.
            bool isMoving = !Mathf.Approximately(swingProgress, target);
            if (safetyWall != null && safetyWall.enabled != isMoving)
                safetyWall.enabled = isMoving;

            Quaternion delta = Quaternion.AngleAxis(
                openAngle * swingProgress,
                swingAxisWorld.sqrMagnitude > 0.0001f ? swingAxisWorld.normalized : Vector3.up);

            transform.rotation = delta * closedWorldRotation;
            transform.position = hingeWorldPoint + delta * (closedWorldPosition - hingeWorldPoint);
        }

        private void UpdateProximity()
        {
            if (!IsFlagGateSatisfied())
            {
                // The task/flag this door is gated behind isn't done yet -
                // stays closed no matter how close the player gets, and
                // doesn't even bother checking distance against linked doors.
                localProximity = false;
                isOpen = false;
                return;
            }

            localProximity = ComputeLocalProximity();

            // The group opens if the player is near ANY linked leaf - each
            // leaf keeps checking its own radius independently (they may
            // differ), this just ORs the results together. A door with no
            // linkedDoors assigned behaves exactly as before.
            isOpen = localProximity;
            for (int i = 0; i < linkedDoors.Count; i++)
            {
                ProximityDoor linked = linkedDoors[i];
                if (linked != null && linked.IsInProximity)
                {
                    isOpen = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Same check TaskObjectiveGuidance uses: if a FlagGatedObject sits
        /// on this same GameObject, the door is only allowed to open once
        /// that gate's condition is met. No FlagGatedObject (or no flag
        /// assigned on it) means the door is never gated - always allowed.
        /// </summary>
        private bool IsFlagGateSatisfied()
        {
            if (flagGate == null || flagGate.Flag == null)
                return true;

            GameSession session = GameSession.Instance;
            if (session == null || session.State == null)
                return true; // No active session (e.g. isolated test scene) - don't block.

            return session.State.HasFlag(flagGate.Flag) == flagGate.ActiveWhenSet;
        }

        private bool ComputeLocalProximity()
        {
            if (cachedPlayer == null)
            {
                if (playerOverride != null)
                {
                    cachedPlayer = playerOverride;
                }
                else
                {
                    GameSession session = GameSession.Instance;
                    if (session == null ||
                        !session.SceneServices.TryGetPlayer(out cachedPlayer, out _))
                    {
                        return false;
                    }
                }
            }

            Vector3 doorPos = closedWorldPosition;
            Vector3 playerPos = cachedPlayer.transform.position;
            if (ignoreHeightDifference)
            {
                doorPos.y = 0f;
                playerPos.y = 0f;
            }

            float distance = Vector3.Distance(doorPos, playerPos);
            bool inProximity = distance <= detectionRadius;
            if (Time.frameCount % 60 == 0)
                Debug.Log($"[Door] '{name}' distance={distance:F2}m, radius={detectionRadius}m, inProximity={inProximity}");
            return inProximity;
        }

        private void PlayStateSound(bool opening)
        {
            AudioClip clip = opening ? openSound : closeSound;
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip, soundVolume);
        }

        /// <summary>
        /// Finds the hinge point on one edge of the mesh's own local bounds
        /// (its widest local axis is assumed to be the door's width) and
        /// converts it to a world-space point using the door's CLOSED
        /// transform. The door then orbits this fixed world point instead of
        /// rotating around its own pivot, so it swings correctly even when
        /// the mesh's pivot sits at its centre.
        /// </summary>
        private Vector3 ComputeHingeWorldPoint()
        {
            if (manualHinge)
                return transform.TransformPoint(manualHingeLocalPosition);

            if (!TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
                return closedWorldPosition; // No mesh info: fall back to the old centre-pivot behaviour.

            Bounds bounds = meshFilter.sharedMesh.bounds;

            // Assume the door's width is along its local X axis (true for the
            // vast majority of door meshes). If this door swings from the
            // wrong edge, the mesh's width may run along a different local
            // axis - in that case a manual fix in Blender/Unity is simplest.
            float edgeX = hingeSide == HingeSide.Left ? bounds.min.x : bounds.max.x;
            Vector3 edgeLocal = new Vector3(edgeX, bounds.center.y, bounds.center.z);

            return transform.TransformPoint(edgeLocal);
        }

        /// <summary>
        /// Visual debug aid: magenta sphere = computed/manual hinge point,
        /// cyan wire sphere = this object's own pivot/origin, yellow line
        /// between them. Works in Edit mode too (recomputes live) so you
        /// can see exactly where the hinge lands relative to the mesh
        /// without entering Play mode. When Manual Hinge is enabled, also
        /// draws a draggable Move handle on the hinge point - drag it in
        /// Scene view and Manual Hinge Local Position updates automatically.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 hinge;
#if UNITY_EDITOR
            if (manualHinge && !Application.isPlaying)
            {
                Vector3 worldHinge = transform.TransformPoint(manualHingeLocalPosition);
                UnityEditor.EditorGUI.BeginChangeCheck();
                Vector3 moved = UnityEditor.Handles.PositionHandle(worldHinge, Quaternion.identity);
                if (UnityEditor.EditorGUI.EndChangeCheck())
                {
                    UnityEditor.Undo.RecordObject(this, "Move Door Hinge");
                    manualHingeLocalPosition = transform.InverseTransformPoint(moved);
                }
                hinge = moved;
            }
            else
            {
                hinge = Application.isPlaying ? hingeWorldPoint : ComputeHingeWorldPoint();
            }
#else
            hinge = Application.isPlaying ? hingeWorldPoint : ComputeHingeWorldPoint();
#endif

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(hinge, 0.06f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.05f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(hinge, transform.position);
        }

        private void EnsureAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound - the door is a world object.
        }

        /// <summary>
        /// Creates the temporary safety wall as a child object, starting
        /// disabled - it only turns on while the door is mid-swing (see Update).
        /// </summary>
        private void EnsureSafetyWall()
        {
            var wallObject = new GameObject("Swing Safety Wall");
            wallObject.transform.SetParent(transform, false);

            // The wall's own scale is set to cancel the door's scale exactly,
            // so its BoxCollider.size below can be specified directly in real
            // metres - important if the door itself was scaled up (e.g. 100x,
            // common for a "cube modificato" reused as a door).
            Vector3 doorScale = transform.lossyScale;
            wallObject.transform.localScale = new Vector3(
                doorScale.x > 0.0001f ? 1f / doorScale.x : 1f,
                doorScale.y > 0.0001f ? 1f / doorScale.y : 1f,
                doorScale.z > 0.0001f ? 1f / doorScale.z : 1f);

            Vector3 size;
            Vector3 centerInDoorLocalSpace; // Unscaled - Unity applies the door's own scale automatically when placing this child.

            if (!safetyWallManualSize &&
                TryGetComponent(out MeshFilter meshFilter) &&
                meshFilter.sharedMesh != null)
            {
                // Sized from the door's own mesh bounds, converted to real
                // metres via the door's scale, with a margin multiplier -
                // this reliably covers the whole doorway (including the gap
                // near the frame) instead of a fixed guessed size.
                Bounds bounds = meshFilter.sharedMesh.bounds;
                size = Vector3.Scale(bounds.size, doorScale) * Mathf.Max(1f, safetyWallSizeMultiplier);
                centerInDoorLocalSpace = bounds.center + DivideSafe(safetyWallLocalOffset, doorScale);
            }
            else
            {
                size = safetyWallLocalSize;
                centerInDoorLocalSpace = DivideSafe(safetyWallLocalOffset, doorScale);
            }

            wallObject.transform.localPosition = centerInDoorLocalSpace;

            safetyWall = wallObject.AddComponent<BoxCollider>();
            safetyWall.isTrigger = false;
            safetyWall.size = size;
            safetyWall.enabled = false;
        }

        /// <summary>
        /// Converts a real-world-metres offset into the door's own unscaled
        /// local space, so it lands correctly once Unity re-applies the
        /// door's scale automatically to this child's localPosition.
        /// </summary>
        private static Vector3 DivideSafe(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.0001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.0001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.0001f ? value.z / divisor.z : value.z);
        }

        /// <summary>
        /// Ensures the door has a solid (non-trigger) collider so the player
        /// cannot walk through it. Only acts if nothing solid is present.
        /// </summary>
        private void EnsureSolidCollision()
        {
            if (!autoAddCollisionIfMissing)
                return;

            Collider[] existing = GetComponents<Collider>();
            foreach (Collider collider in existing)
            {
                if (!collider.isTrigger)
                    return; // Already has solid collision.
            }

            if (TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                MeshCollider solid = gameObject.AddComponent<MeshCollider>();
                solid.sharedMesh = meshFilter.sharedMesh;
                solid.convex = false;
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }
    }
}