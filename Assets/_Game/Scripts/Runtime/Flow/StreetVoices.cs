using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class StreetVoices : MonoBehaviour
    {
        [Tooltip("Voices heard with the shutters open.")]
        [SerializeField] private AudioClip outside;
        [Tooltip("Voices heard with the shutters closed.")]
        [SerializeField] private AudioClip inside;
        [Tooltip("The window: looking at it silences the street.")]
        [SerializeField] private Observable window;
        [SerializeField] private OpenableWindow shutters;
        [SerializeField, Range(0f, 1f)] private float volume = 0.32f;
        [Tooltip("Seconds of looking away for the voices to reach their loudest.")]
        [SerializeField, Min(0.1f)] private float riseSeconds = 4f;
        [SerializeField] private float loudestDb = 9f;
        [SerializeField] private float silentDb = -40f;
        [Tooltip("Seconds of the fade to silence when the player looks at the window.")]
        [SerializeField, Min(0.01f)] private float hushSeconds = 0.12f;
        [SerializeField, Range(0f, 1f)] private float insideLevel = 0.7f;

        private static StreetVoices current;
        private static bool pendingBegin;
        private AudioSource outSource, inSource;
        private bool active;
        private float away;
        private float gain;

        public static void Begin()
        {
            if (current != null)
                current.active = true;
            else
                pendingBegin = true;
        }

        private void Awake()
        {
            current = this;
            outSource = Source(outside);
            inSource = Source(inside);
            if (pendingBegin)
                active = true;
            pendingBegin = false;
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        private AudioSource Source(AudioClip clip)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.65f;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.volume = 0f;
            AudioManager.Route(source, AudioBus.SoundEffects);
            if (clip != null)
                source.Play();
            return source;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
                return;
            bool hushed = !active || Looking();
            away = hushed ? 0f : away + Time.deltaTime;
            float db = hushed ? silentDb : Mathf.Lerp(0f, loudestDb, Mathf.Clamp01(away / riseSeconds));
            float target = active ? volume * Mathf.Pow(10f, db / 20f) : 0f;
            gain = Mathf.MoveTowards(gain, target, Time.deltaTime / (target < gain ? hushSeconds : 0.6f));
            float open = shutters != null ? shutters.Openness : 1f;
            outSource.volume = gain * open;
            inSource.volume = gain * (1f - open) * insideLevel;
        }

        private bool Looking()
        {
            if (TranscribePoem.AnyTranscribing)
                return TranscribePoem.WordsHeld;
            return window != null && window.IsSeen;
        }
    }
}
