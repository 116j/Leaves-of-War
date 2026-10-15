using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    public enum FootstepSurfaceType
    {
        Wood,
        Stone,
        Tile,
        Earth
    }

    public sealed class Footsteps : MonoBehaviour
    {
        [Serializable]
        public sealed class SurfaceClips
        {
            public FootstepSurfaceType surface;
            public AudioClip[] clips = new AudioClip[0];
        }

        [Tooltip("Used when the ground under the feet is not recognised.")]
        [SerializeField] private FootstepSurfaceType defaultSurface = FootstepSurfaceType.Wood;
        [SerializeField] private SurfaceClips[] surfaces = new SurfaceClips[0];
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private Vector2 pitch = new Vector2(0.93f, 1.07f);
        [Tooltip("Metres between two steps when walking.")]
        [SerializeField, Min(0.1f)] private float walkStride = 0.6f;
        [Tooltip("Metres between two steps when running.")]
        [SerializeField, Min(0.1f)] private float runStride = 0.8f;
        [Tooltip("Speed above which the run stride is used.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 4.2f;
        [SerializeField, Min(0f)] private float minSpeed = 0.25f;
        [Tooltip("3D: heard from where this character is (for NPCs). Off: always heard in the head (the player).")]
        [SerializeField] private bool spatial = false;
        [SerializeField, Min(1f)] private float maxDistance = 14f;
        [SerializeField] private LayerMask groundMask = ~0;

        private CharacterController controller;
        private AudioSource source;
        private Vector3 lastPosition;
        private float travelled;
        private int lastIndex = -1;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = maxDistance;
            AudioManager.Route(source, AudioBus.SoundEffects);
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            travelled = walkStride * 0.6f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            delta.y = 0f;
            float distance = delta.magnitude;
            float speed = distance / dt;
            bool grounded = controller == null || controller.isGrounded;
            if (!grounded || speed < minSpeed || distance > 2f)
            {
                travelled = Mathf.Min(travelled, walkStride * 0.6f);
                return;
            }
            travelled += distance;
            float stride = speed >= runSpeed ? runStride : walkStride;
            if (travelled < stride)
                return;
            travelled = 0f;
            Step(speed >= runSpeed);
        }

        private void Step(bool running)
        {
            AudioClip[] clips = ClipsFor(Surface());
            if (clips == null || clips.Length == 0)
                return;
            int index = UnityEngine.Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex)
                index = (index + 1) % clips.Length;
            lastIndex = index;
            AudioClip clip = clips[index];
            if (clip == null)
                return;
            source.pitch = UnityEngine.Random.Range(pitch.x, pitch.y);
            source.PlayOneShot(clip, volume * (running ? 1.15f : 1f));
        }

        private AudioClip[] ClipsFor(FootstepSurfaceType type)
        {
            AudioClip[] fallback = null;
            foreach (SurfaceClips entry in surfaces)
            {
                if (entry == null || entry.clips == null || entry.clips.Length == 0)
                    continue;
                if (entry.surface == type)
                    return entry.clips;
                if (entry.surface == defaultSurface)
                    fallback = entry.clips;
            }
            return fallback;
        }

        private FootstepSurfaceType Surface()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f, groundMask, QueryTriggerInteraction.Ignore))
                return defaultSurface;
            FootstepSurface marker = hit.collider.GetComponentInParent<FootstepSurface>();
            if (marker != null)
                return marker.Surface;
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null && hit.collider.transform.parent != null)
            {
                string name = hit.collider.name.Replace(" Collider", string.Empty);
                Transform sibling = hit.collider.transform.parent.Find(name);
                if (sibling != null)
                    renderer = sibling.GetComponent<Renderer>();
            }
            if (renderer != null && renderer.sharedMaterial != null && TryGuess(renderer.sharedMaterial.name, out FootstepSurfaceType fromMaterial))
                return fromMaterial;
            if (TryGuess(hit.collider.name, out FootstepSurfaceType fromName))
                return fromName;
            return defaultSurface;
        }

        private static readonly (string word, FootstepSurfaceType type)[] Words =
        {
            ("terrazzo", FootstepSurfaceType.Tile), ("tile", FootstepSurfaceType.Tile), ("marble", FootstepSurfaceType.Tile), ("ceramic", FootstepSurfaceType.Tile),
            ("plank", FootstepSurfaceType.Wood), ("parquet", FootstepSurfaceType.Wood), ("wood", FootstepSurfaceType.Wood), ("board", FootstepSurfaceType.Wood), ("walnut", FootstepSurfaceType.Wood),
            ("mud", FootstepSurfaceType.Earth), ("earth", FootstepSurfaceType.Earth), ("soil", FootstepSurfaceType.Earth), ("forest", FootstepSurfaceType.Earth),
            ("dirt", FootstepSurfaceType.Earth), ("grass", FootstepSurfaceType.Earth), ("litter", FootstepSurfaceType.Earth), ("ground", FootstepSurfaceType.Earth),
            ("stone", FootstepSurfaceType.Stone), ("rock", FootstepSurfaceType.Stone), ("limestone", FootstepSurfaceType.Stone), ("karst", FootstepSurfaceType.Stone),
            ("cobble", FootstepSurfaceType.Stone), ("concrete", FootstepSurfaceType.Stone), ("sandbag", FootstepSurfaceType.Earth),
        };

        private static bool TryGuess(string name, out FootstepSurfaceType type)
        {
            string lower = name.ToLowerInvariant();
            foreach (var (word, kind) in Words)
                if (lower.Contains(word))
                {
                    type = kind;
                    return true;
                }
            type = FootstepSurfaceType.Wood;
            return false;
        }
    }
}
