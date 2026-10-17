using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class OpenableWindow : MonoBehaviour, IInteractable
    {
        [Tooltip("Hinges of the glass casements, rotated around their local Y axis.")]
        [SerializeField] private Transform[] hinges = new Transform[0];
        [Tooltip("Open angle per hinge, same order. Use negative values for the right casement.")]
        [SerializeField] private float[] openAngles = { -80f, 80f };
        [SerializeField, Min(0.1f)] private float duration = 1.2f;
        [SerializeField] private bool startOpen = false;
        [SerializeField] private string openPrompt = "OPEN WINDOW";
        [SerializeField] private string closePrompt = "CLOSE WINDOW";

        [Tooltip("Optional: the window opens by itself when this character starts walking (he goes out through it).")]
        [SerializeField] private DialogueSpeaker openWhenWalking;

        [Header("Audio")]
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        public float Openness { get; private set; }
        public bool IsOpen => target > 0.5f;
        public string Prompt => IsOpen ? closePrompt : openPrompt;

        private Quaternion[] closedRotations;
        private float progress;
        private float target;
        private AudioSource source;

        private void Awake()
        {
            closedRotations = new Quaternion[hinges.Length];
            for (int i = 0; i < hinges.Length; i++)
                if (hinges[i] != null)
                    closedRotations[i] = hinges[i].localRotation;

            target = progress = startOpen ? 1f : 0f;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.maxDistance = 10f;
            source.rolloffMode = AudioRolloffMode.Linear;
            AudioManager.Route(source, AudioBus.SoundEffects);
            Apply();
        }

        public void Interact()
        {
            target = IsOpen ? 0f : 1f;
            AudioClip clip = target > 0.5f ? openClip : closeClip;
            if (clip != null)
                source.PlayOneShot(clip, volume);
        }

        public void Open()
        {
            if (!IsOpen)
                Interact();
        }

        private void Update()
        {
            if (openWhenWalking != null && openWhenWalking.IsWalking)
                Open();
            if (Mathf.Approximately(progress, target))
                return;
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / duration);
            Apply();
        }

        private void Apply()
        {
            Openness = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < hinges.Length; i++)
            {
                if (hinges[i] == null)
                    continue;
                float angle = i < openAngles.Length ? openAngles[i] : 80f;
                hinges[i].localRotation = closedRotations[i] * Quaternion.Euler(0f, angle * Openness, 0f);
            }
        }
    }
}
