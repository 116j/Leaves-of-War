using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class LevelAudio : MonoBehaviour
    {
        public enum Mix
        {
            Always,
            WindowClosed,
            WindowOpen
        }

        [Serializable]
        public sealed class Ambience
        {
            public AudioClip clip;
            [Tooltip("Always, only with the window closed (inside version) or only with it open (outside version).")]
            public Mix mix = Mix.Always;
            [Range(0f, 1f)] public float volume = 0.6f;
            [Tooltip("Optional: plays from this point in the room (e.g. the window for the street). Empty: everywhere.")]
            public Transform source;
            [Min(0.5f)] public float maxDistance = 12f;
        }

        public enum MusicMode
        {
            DuringTranscription,
            Always
        }

        [Header("Soundtrack")]
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
        [Tooltip("During Transcription: starts with the first written word and fades out when the poem is transcribed.")]
        [SerializeField] private MusicMode musicMode = MusicMode.DuringTranscription;
        [SerializeField, Min(0f)] private float musicDelay = 0f;
        [SerializeField, Min(0.05f)] private float musicFade = 2f;

        [Header("Ambience")]
        [SerializeField] private Ambience[] ambience = Array.Empty<Ambience>();
        [Tooltip("The window that crossfades the inside and outside ambience.")]
        [SerializeField] private OpenableWindow window;
        [Tooltip("How fast the outside takes over: higher = a small gap already lets the street in.")]
        [SerializeField, Range(0.2f, 1f)] private float leakCurve = 0.5f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 2f;

        private AudioSource[] sources = Array.Empty<AudioSource>();
        private AudioSource musicSource;
        private float musicTarget;
        private float elapsed;
        private bool musicStarted;

        private void OnEnable()
        {
            TranscribePoem.TranscriptionStarted += OnTranscriptionStarted;
            TranscribePoem.Transcribed += OnTranscribed;
        }

        private void OnDisable()
        {
            TranscribePoem.TranscriptionStarted -= OnTranscriptionStarted;
            TranscribePoem.Transcribed -= OnTranscribed;
        }

        private void OnTranscriptionStarted(int poemId)
        {
            if (musicMode == MusicMode.DuringTranscription)
                StartMusic();
        }

        private void OnTranscribed(int poemId)
        {
            if (musicMode == MusicMode.DuringTranscription)
                musicTarget = 0f;
        }

        private void StartMusic()
        {
            if (music == null)
                return;
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f;
                musicSource.volume = 0f;
                AudioManager.Route(musicSource, AudioBus.Music);
            }
            musicSource.clip = music;
            if (!musicSource.isPlaying)
                musicSource.Play();
            musicTarget = 1f;
        }

        private void Start()
        {
            if (window == null)
                window = FindAnyObjectByType<OpenableWindow>();
            sources = new AudioSource[ambience.Length];
            double syncStart = AudioSettings.dspTime + 0.1;
            for (int i = 0; i < ambience.Length; i++)
            {
                Ambience entry = ambience[i];
                if (entry == null || entry.clip == null)
                    continue;

                Transform parent = entry.source != null ? entry.source : transform;
                var go = new GameObject("Ambience " + entry.clip.name);
                go.transform.SetParent(parent, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.clip = entry.clip;
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
                source.spatialBlend = entry.source != null ? 1f : 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.maxDistance = entry.maxDistance;
                AudioManager.Route(source, AudioBus.SoundEffects);
                if (entry.mix == Mix.Always)
                {
                    source.time = UnityEngine.Random.Range(0f, entry.clip.length * 0.9f);
                    source.Play();
                }
                else
                {
                    source.PlayScheduled(syncStart);
                }
                sources[i] = source;
            }
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = fadeInSeconds > 0f ? Mathf.Clamp01(elapsed / fadeInSeconds) : 1f;
            float open = window != null ? Mathf.Pow(window.Openness, leakCurve) : 0f;
            float outside = Mathf.Sin(open * Mathf.PI * 0.5f);
            float inside = Mathf.Cos(open * Mathf.PI * 0.5f);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null)
                    continue;
                float mix = ambience[i].mix == Mix.WindowOpen ? outside : ambience[i].mix == Mix.WindowClosed ? inside : 1f;
                sources[i].volume = ambience[i].volume * fade * mix;
            }

            if (!musicStarted && musicMode == MusicMode.Always && music != null && elapsed >= musicDelay)
            {
                musicStarted = true;
                StartMusic();
            }

            if (musicSource != null)
            {
                float target = musicTarget * musicVolume;
                musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * musicVolume / musicFade);
                if (musicTarget <= 0f && musicSource.volume <= 0f && musicSource.isPlaying)
                    musicSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (musicSource != null)
                musicSource.Stop();
        }
    }
}
