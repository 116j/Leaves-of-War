using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Plays footstep sounds as the player walks, picking a clip based on the
    /// ground material's name (via <see cref="FootstepSurfaceLibrary"/>),
    /// varying pitch each time, and varying the distance between steps
    /// slightly so the cadence doesn't feel mechanically even.
    ///
    /// Step timing is driven by DISTANCE walked, so it naturally speeds up
    /// or slows down with the player's actual movement speed (walk vs run)
    /// on top of the per-step randomisation below. While sprinting, the step
    /// distance threshold is also multiplied down (see
    /// sprintStepDistanceMultiplier), so the cadence audibly quickens beyond
    /// just "covering the same distance faster".
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FootstepController : MonoBehaviour
    {
        [SerializeField] private FootstepSurfaceLibrary library;

        [Header("Timing")]
        [Tooltip("Shortest distance (metres) the player can walk before the next footstep.")]
        [SerializeField, Range(0.1f, 5f)] private float minMetresPerStep = 1.6f;
        [Tooltip("Longest distance (metres) the player can walk before the next footstep.")]
        [SerializeField, Range(0.1f, 5f)] private float maxMetresPerStep = 2.4f;
        [Tooltip("While sprinting, step thresholds are multiplied by this (below 1 = shorter/more frequent steps, on top of the natural speedup from covering distance faster).")]
        [SerializeField, Range(0.3f, 1f)] private float sprintStepDistanceMultiplier = 0.65f;

        [Header("Ground Detection")]
        [Tooltip("How far below the player to check for ground.")]
        [SerializeField, Min(0.1f)] private float raycastDistance = 1.5f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Sound Variation")]
        [Tooltip("Lowest random pitch applied to a footstep.")]
        [SerializeField, Range(0.5f, 2f)] private float minPitch = 0.92f;
        [Tooltip("Highest random pitch applied to a footstep.")]
        [SerializeField, Range(0.5f, 2f)] private float maxPitch = 1.08f;
        [Range(0f, 10f)]
        [SerializeField] private float volume = 3f;

        private CharacterController controller;
        private FirstPersonController firstPersonController;
        private AudioSource audioSource;
        private Vector3 lastPosition;
        private float distanceAccumulated;
        private float currentStepThreshold;
        private Renderer[] allSceneRenderers;

        [Header("Fallback (separate collision hierarchy)")]
        [Tooltip("If the collider hit has no Renderer in its own hierarchy (a separate, purpose-built collision mesh with no visible counterpart nearby), search for the closest Renderer in the whole scene within this distance instead.")]
        [SerializeField, Min(0.1f)] private float fallbackSearchRadius = 2f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            firstPersonController = GetComponent<FirstPersonController>();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D - footsteps come from the player's feet.
            AudioManager.Route(audioSource, AudioBus.SoundEffects);

            lastPosition = transform.position;
            currentStepThreshold = RandomStepThreshold();

            // Cached once: this project uses a separate, simplified collision
            // hierarchy (see ResolveMaterialName) with no Renderer of its own,
            // so material lookup falls back to a scene-wide proximity search.
            allSceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (controller == null || !controller.isGrounded)
            {
                lastPosition = transform.position;
                distanceAccumulated = 0f;
                return;
            }

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f; // Only horizontal movement counts toward step distance.
            float moved = delta.magnitude;
            lastPosition = transform.position;

            if (moved < 0.0001f)
            {
                distanceAccumulated = 0f; // Standing still - reset so the next step isn't "free".
                return;
            }

            distanceAccumulated += moved;
            if (distanceAccumulated >= currentStepThreshold)
            {
                distanceAccumulated = 0f;
                currentStepThreshold = RandomStepThreshold(); // Pick a fresh, slightly different distance for the NEXT step.
                PlayFootstep();
            }
        }

        private float RandomStepThreshold()
        {
            float lo = Mathf.Min(minMetresPerStep, maxMetresPerStep);
            float hi = Mathf.Max(minMetresPerStep, maxMetresPerStep);
            float threshold = Random.Range(lo, hi);

            if (firstPersonController != null && firstPersonController.IsSprinting)
                threshold *= sprintStepDistanceMultiplier;

            return threshold;
        }

        private void PlayFootstep()
        {
            if (library == null)
                return;

            Vector3 origin = transform.position + Vector3.up * 0.1f;
            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    raycastDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"[Footstep] no ground hit within {raycastDistance}m below the player.");
                return;
            }

            string materialName = ResolveMaterialName(hit.collider, hit.point);
            Debug.Log($"[Footstep] hit path='{GetHierarchyPath(hit.collider.transform)}', resolved material='{materialName}'");

            AudioClip clip = library.GetRandomClip(materialName);
            if (clip == null)
            {
                Debug.Log($"[Footstep] no clip found for material='{materialName}' (and no Default Clips set).");
                return;
            }

            float lo = Mathf.Min(minPitch, maxPitch);
            float hi = Mathf.Max(minPitch, maxPitch);
            audioSource.pitch = Random.Range(lo, hi);
            audioSource.PlayOneShot(clip, volume);
            Debug.Log($"[Footstep] surface='{materialName}', clip='{clip.name}', pitch={audioSource.pitch:F2}");
        }

        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            Transform current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private string ResolveMaterialName(Collider hitCollider, Vector3 hitPoint)
        {
            // The collider sometimes lives on a "group" object with the actual
            // visible mesh(es)/material(s) on child objects underneath it (e.g.
            // one combined collision shape covering several floor pieces), so
            // check self, then children, then ancestors, in that order.
            Renderer renderer = hitCollider.GetComponent<Renderer>();
            if (renderer == null)
                renderer = hitCollider.GetComponentInChildren<Renderer>();
            if (renderer == null)
                renderer = hitCollider.GetComponentInParent<Renderer>();

            // This project also has a separate, purpose-built collision
            // hierarchy with no Renderer anywhere near it (a "MANOR COLLISION"
            // proxy mesh, entirely disconnected from the visible geometry). In
            // that case, fall back to the closest Renderer in the whole scene
            // to the actual impact point, since hierarchy search can't help.
            if (renderer == null)
                renderer = FindClosestRenderer(hitPoint);

            if (renderer == null || renderer.sharedMaterial == null)
                return string.Empty;

            string name = renderer.sharedMaterial.name;

            // Unity appends " (Instance)" to runtime-duplicated materials;
            // strip it so name matching still works against the library.
            int instanceSuffix = name.IndexOf(" (Instance)", System.StringComparison.Ordinal);
            if (instanceSuffix >= 0)
                name = name.Substring(0, instanceSuffix);

            return name;
        }

        private Renderer FindClosestRenderer(Vector3 point)
        {
            if (allSceneRenderers == null)
                return null;

            Renderer closest = null;
            float closestSqrDistance = fallbackSearchRadius * fallbackSearchRadius;

            foreach (Renderer candidate in allSceneRenderers)
            {
                if (candidate == null)
                    continue;

                Vector3 nearestPointOnBounds = candidate.bounds.ClosestPoint(point);
                float sqrDistance = (nearestPointOnBounds - point).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = candidate;
                }
            }

            return closest;
        }
    }
}