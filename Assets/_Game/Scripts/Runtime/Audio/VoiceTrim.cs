using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    public static class VoiceTrim
    {
        private const float Threshold = 0.015f;
        private const float Window = 0.02f;
        private const float HeadMargin = 0.04f;
        private const float TailMargin = 0.12f;
        private const float LongPause = 0.45f;
        private const float KeptPause = 0.25f;

        private sealed class Cut
        {
            public float start;
            public float end;
            public readonly List<Vector2> skips = new List<Vector2>();
        }

        private static readonly Dictionary<AudioClip, Cut> Cache = new Dictionary<AudioClip, Cut>();

        private static Cut Analyse(AudioClip clip)
        {
            if (Cache.TryGetValue(clip, out Cut cut))
                return cut;
            cut = new Cut { start = 0f, end = clip.length };
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.channels <= 0 || clip.samples <= 0)
                return cut;
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            if (clip.loadState != AudioDataLoadState.Loaded || !clip.GetData(data, 0))
                return cut;

            int frame = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * Window)) * clip.channels;
            int count = (data.Length + frame - 1) / frame;
            var loud = new bool[count];
            for (int w = 0; w < count; w++)
            {
                float sum = 0f;
                int from = w * frame;
                int to = Mathf.Min(data.Length, from + frame);
                for (int i = from; i < to; i++)
                    sum += data[i] * data[i];
                loud[w] = Mathf.Sqrt(sum / Mathf.Max(1, to - from)) >= Threshold;
            }

            int first = System.Array.IndexOf(loud, true);
            int last = System.Array.LastIndexOf(loud, true);
            if (first < 0)
            {
                Cache[clip] = cut;
                return cut;
            }
            cut.start = Mathf.Max(0f, first * Window - HeadMargin);
            cut.end = Mathf.Min(clip.length, (last + 1) * Window + TailMargin);

            int run = -1;
            for (int w = first; w <= last; w++)
            {
                if (!loud[w])
                {
                    if (run < 0)
                        run = w;
                    continue;
                }
                if (run >= 0)
                {
                    float a = run * Window;
                    float b = w * Window;
                    if (b - a > LongPause)
                        cut.skips.Add(new Vector2(a + KeptPause * 0.5f, b - KeptPause * 0.5f));
                    run = -1;
                }
            }
            Cache[clip] = cut;
            return cut;
        }

        public static Vector2 Range(AudioClip clip)
        {
            if (clip == null)
                return Vector2.zero;
            Cut cut = Analyse(clip);
            return new Vector2(cut.start, cut.end);
        }

        public static float Length(AudioClip clip)
        {
            Vector2 r = Range(clip);
            return Mathf.Max(0f, r.y - r.x);
        }

        public static void Play(AudioSource source, AudioClip clip, float volume)
        {
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.time = Range(clip).x;
            source.Play();
        }

        public static bool Finished(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null || source.clip != clip || !source.isPlaying)
                return true;
            Cut cut = Analyse(clip);
            float time = source.time;
            if (time >= cut.end)
            {
                source.Stop();
                return true;
            }
            foreach (Vector2 skip in cut.skips)
                if (time >= skip.x && time < skip.y)
                {
                    source.time = skip.y;
                    break;
                }
            return false;
        }
    }
}
