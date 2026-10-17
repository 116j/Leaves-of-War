using System.Collections;
using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class Weather : MonoBehaviour
    {
        [Tooltip("Stopped (the drops already falling finish) when the weather clears.")]
        [SerializeField] private ParticleSystem[] rain = new ParticleSystem[0];
        [SerializeField, Min(0.5f)] private float seconds = 8f;
        [Tooltip("The sun gets this much brighter once the rain is over.")]
        [SerializeField, Min(0f)] private float sunBoost = 1.4f;

        private bool cleared;

        public void Clear()
        {
            if (cleared)
                return;
            cleared = true;
            foreach (ParticleSystem system in rain)
                if (system != null)
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (LevelAudio audio in FindObjectsByType<LevelAudio>())
                audio.ClearWeather(seconds);
            foreach (LevelSky sky in FindObjectsByType<LevelSky>())
                sky.ChangeNow(seconds);
            StartCoroutine(Brighten());
        }

        private IEnumerator Brighten()
        {
            Light sun = null;
            foreach (Light l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional)
                {
                    sun = l;
                    break;
                }
            if (sun == null)
                yield break;
            float from = sun.intensity, to = from * sunBoost;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                sun.intensity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            sun.intensity = to;
        }
    }
}
