using System;
using System.Collections.Generic;
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
            [Tooltip("Same clip forced to mono (Tools > Leaves of War > Audio > Mono Ambience Copies). Used to narrow the stereo image while the window is closed.")]
            public AudioClip monoClip;
            [Tooltip("Always, only with the window closed (inside version) or only with it open (outside version).")]
            public Mix mix = Mix.Always;
            [Range(0f, 1f)] public float volume = 0.6f;
            [Tooltip("Optional: plays from this point in the room (e.g. the window for the street). Empty: everywhere.")]
            public Transform source;
            [Min(0.5f)] public float maxDistance = 12f;
            [Tooltip("Silent at first; fades in (while the others fade out) when the weather clears, e.g. after the rain in San Paolo.")]
            public bool afterWeatherClears;
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
        [Tooltip("Played once when the poem is uncovered under the leaves.")]
        [SerializeField] private AudioClip poemFoundStinger;
        [SerializeField, Range(0f, 1f)] private float stingerVolume = 0.8f;

        [Header("Effects")]
        [Tooltip("Paper sound when the poem is uncovered (one at random).")]
        [SerializeField] private AudioClip[] paperRevealClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float paperRevealVolume = 0.7f;
        [Tooltip("Played when the golden leaf appears while writing (one at random).")]
        [SerializeField] private AudioClip[] followerLeafClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float followerLeafVolume = 0.35f;

        [Header("Ambience")]
        [SerializeField] private Ambience[] ambience = Array.Empty<Ambience>();
        [Tooltip("The window that crossfades the inside and outside ambience.")]
        [SerializeField] private OpenableWindow window;
        [Tooltip("How fast the outside takes over: higher = a small gap already lets the street in.")]
        [SerializeField, Range(0.2f, 1f)] private float leakCurve = 0.5f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 2f;
        [Tooltip("Seconds of overlap between the end of each ambience loop and its next start. 0 = plain loop.")]
        [SerializeField, Min(0f)] private float loopCrossfade = 3f;
        [Tooltip("Stereo width of the ambience with the window closed (0 = mono, 1 = full stereo). Opening the window widens it to full stereo. Needs the mono copies.")]
        [SerializeField, Range(0f, 1f)] private float closedStereoWidth = 0.3f;

        private const float MonoBoost = 1.3f;

        private sealed class Layer
        {
            public Ambience entry;
            public readonly AudioSource[] stereo = new AudioSource[2];
            public readonly AudioSource[] mono = new AudioSource[2];
        }

        private sealed class Track
        {
            public readonly List<Layer> layers = new List<Layer>();
            public int frequency;
            public double length;
            public double crossfade;
            public int current;
            public bool scheduled;
            public double ready;
            public readonly bool[] fadeIn = new bool[2];
        }

        private readonly List<Track> tracks = new List<Track>();
        private float clearPhase;
        private float clearTarget;
        private float clearSeconds = 6f;
        private bool hasAfter;

        public void ClearWeather(float seconds)
        {
            clearTarget = 1f;
            clearSeconds = Mathf.Max(0.1f, seconds);
        }
        private AudioSource musicSource;
        private float musicTarget;
        private float elapsed;
        private bool musicStarted;
        private TranscribePoem transcribe;
        private bool poemWasUncovered;

        private void OnEnable()
        {
            TranscribePoem.TranscriptionStarted += OnTranscriptionStarted;
            TranscribePoem.Transcribed += OnTranscribed;
            FollowerLeaf.Sounds = followerLeafClips;
            FollowerLeaf.SoundVolume = followerLeafVolume;
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
            transcribe = FindAnyObjectByType<TranscribePoem>();
            double syncStart = AudioSettings.dspTime + 0.1;
            foreach (Ambience entry in ambience)
                if (entry != null && entry.clip != null && entry.afterWeatherClears)
                    hasAfter = true;
            var synced = new Dictionary<long, Track>();
            foreach (Ambience entry in ambience)
            {
                if (entry == null || entry.clip == null)
                    continue;
                AudioClip clip = entry.clip;
                double length = clip.samples / (double)clip.frequency;
                Track track = null;
                long key = (long)clip.samples * 1000003L + clip.frequency;
                if (entry.mix != Mix.Always)
                    synced.TryGetValue(key, out track);
                if (track == null)
                {
                    track = new Track
                    {
                        frequency = clip.frequency,
                        length = length,
                        crossfade = Math.Min(loopCrossfade, length * 0.3),
                        ready = syncStart + 0.25,
                    };
                    tracks.Add(track);
                    if (entry.mix != Mix.Always)
                        synced[key] = track;
                }

                var layer = new Layer { entry = entry };
                AudioClip mono = entry.source == null && entry.monoClip != null && entry.monoClip.samples == clip.samples ? entry.monoClip : null;
                int voices = track.crossfade > 0.05 ? 2 : 1;
                for (int v = 0; v < voices; v++)
                {
                    layer.stereo[v] = MakeSource(entry, clip, voices == 1);
                    if (mono != null)
                        layer.mono[v] = MakeSource(entry, mono, voices == 1);
                }
                track.layers.Add(layer);
            }

            foreach (Track track in tracks)
            {
                bool always = track.layers[0].entry.mix == Mix.Always;
                int offset = always ? (int)(UnityEngine.Random.Range(0f, 0.9f) * track.length * track.frequency) : 0;
                foreach (Layer layer in track.layers)
                    foreach (AudioSource source in new[] { layer.stereo[0], layer.mono[0] })
                    {
                        if (source == null)
                            continue;
                        source.timeSamples = Mathf.Min(offset, source.clip.samples - 1);
                        source.PlayScheduled(syncStart);
                    }
            }
        }

        private AudioSource MakeSource(Ambience entry, AudioClip clip, bool loop)
        {
            Transform parent = entry.source != null ? entry.source : transform;
            var go = new GameObject("Ambience " + clip.name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = entry.source != null ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = entry.maxDistance;
            AudioManager.Route(source, AudioBus.SoundEffects);
            return source;
        }

        private void Advance(Track track)
        {
            if (track.layers[0].stereo[1] == null || AudioListener.pause || AudioSettings.dspTime < track.ready)
                return;
            int next = 1 - track.current;
            AudioSource current = track.layers[0].stereo[track.current];
            double played = current.timeSamples / (double)track.frequency;
            double remaining = track.length - track.crossfade - played;

            if (track.scheduled)
            {
                AudioSource upcoming = track.layers[0].stereo[next];
                if (upcoming.isPlaying && upcoming.timeSamples > 0 || !current.isPlaying)
                {
                    track.current = next;
                    track.scheduled = false;
                }
                return;
            }

            if (current.isPlaying && remaining > 0.25)
                return;
            double at = AudioSettings.dspTime + Math.Max(current.isPlaying ? remaining : 0.0, 0.02);
            foreach (Layer layer in track.layers)
                foreach (AudioSource source in new[] { layer.stereo[next], layer.mono[next] })
                {
                    if (source == null)
                        continue;
                    source.Stop();
                    source.timeSamples = 0;
                    source.PlayScheduled(at);
                }
            track.fadeIn[next] = true;
            track.scheduled = true;
        }

        private static float Gain(Track track, int voice)
        {
            AudioSource source = track.layers[0].stereo[voice];
            if (source == null || !source.isPlaying)
                return 0f;
            if (track.layers[0].stereo[1] == null)
                return 1f;
            double t = source.timeSamples / (double)track.frequency;
            double xf = track.crossfade;
            double gain = 1.0;
            if (track.fadeIn[voice] && t < xf)
                gain *= Math.Sin(t / xf * Math.PI * 0.5);
            if (t > track.length - xf)
                gain *= Math.Cos(Math.Min(1.0, (t - (track.length - xf)) / xf) * Math.PI * 0.5);
            return (float)gain;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = fadeInSeconds > 0f ? Mathf.Clamp01(elapsed / fadeInSeconds) : 1f;
            float openness = window != null ? window.Openness : 0f;
            float open = Mathf.Pow(openness, leakCurve);
            float outside = Mathf.Sin(open * Mathf.PI * 0.5f);
            float inside = Mathf.Cos(open * Mathf.PI * 0.5f);
            float width = window != null ? Mathf.Lerp(closedStereoWidth, 1f, open) : 1f;
            clearPhase = Mathf.MoveTowards(clearPhase, clearTarget, Time.unscaledDeltaTime / clearSeconds);
            foreach (Track track in tracks)
            {
                Advance(track);
                for (int v = 0; v < 2; v++)
                {
                    float gain = Gain(track, v);
                    foreach (Layer layer in track.layers)
                    {
                        if (layer.stereo[v] == null)
                            continue;
                        Ambience entry = layer.entry;
                        float mix = entry.mix == Mix.WindowOpen ? outside : entry.mix == Mix.WindowClosed ? inside : 1f;
                        float phase = entry.afterWeatherClears ? clearPhase : hasAfter ? 1f - clearPhase : 1f;
                        float level = entry.volume * fade * mix * gain * phase;
                        if (layer.mono[v] != null)
                        {
                            layer.stereo[v].volume = level * width;
                            layer.mono[v].volume = Mathf.Clamp01(level * (1f - width) * MonoBoost);
                        }
                        else
                        {
                            layer.stereo[v].volume = level;
                        }
                    }
                }
            }

            bool uncovered = transcribe != null && transcribe.IsPoemUncovered;
            if (uncovered && !poemWasUncovered)
            {
                if (poemFoundStinger != null)
                    PlayStinger();
                if (paperRevealClips.Length > 0)
                    OneShot(paperRevealClips[UnityEngine.Random.Range(0, paperRevealClips.Length)], paperRevealVolume);
            }
            poemWasUncovered = uncovered;

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

        private void PlayStinger()
        {
            var go = new GameObject("Poem Found Stinger");
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = stingerVolume;
            source.clip = poemFoundStinger;
            AudioManager.Route(source, AudioBus.Music);
            source.Play();
            Destroy(go, poemFoundStinger.length + 0.2f);
        }

        private void OneShot(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            var go = new GameObject(clip.name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = clip;
            AudioManager.Route(source, AudioBus.SoundEffects);
            source.Play();
            Destroy(go, clip.length + 0.2f);
        }

        private void OnDestroy()
        {
            if (musicSource != null)
                musicSource.Stop();
        }
    }
}
