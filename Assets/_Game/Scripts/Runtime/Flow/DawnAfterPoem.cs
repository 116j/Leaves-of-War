using System.Collections;
using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class DawnAfterPoem : MonoBehaviour
    {
        [Tooltip("0 = any poem.")]
        [SerializeField] private int poemId = 3;
        [SerializeField, Min(0f)] private float delay = 3f;
        [SerializeField, Min(0.5f)] private float seconds = 25f;
        [SerializeField] private Color ambient = new Color(0.33f, 0.33f, 0.38f);
        [SerializeField] private Color fog = new Color(0.46f, 0.46f, 0.5f);
        [SerializeField, Min(0f)] private float fogDensity = 0.022f;
        [SerializeField] private Color sun = new Color(0.86f, 0.74f, 0.7f);
        [SerializeField, Min(0f)] private float sunIntensity = 0.55f;
        [SerializeField] private Vector3 sunEuler = new Vector3(6f, 120f, 0f);

        private bool started;

        private void OnEnable() => TranscribePoem.Transcribed += OnTranscribed;

        private void OnDisable() => TranscribePoem.Transcribed -= OnTranscribed;

        private void OnTranscribed(int id)
        {
            if (started || poemId != 0 && id != poemId)
                return;
            started = true;
            StartCoroutine(Rise());
        }

        private IEnumerator Rise()
        {
            yield return new WaitForSeconds(delay);
            Light light = null;
            foreach (Light l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional)
                {
                    light = l;
                    break;
                }
            Color ambient0 = RenderSettings.ambientLight, fog0 = RenderSettings.fogColor;
            float density0 = RenderSettings.fogDensity;
            Color sun0 = light != null ? light.color : sun;
            float intensity0 = light != null ? light.intensity : sunIntensity;
            Quaternion rotation0 = light != null ? light.transform.rotation : Quaternion.Euler(sunEuler);
            Quaternion rotation1 = Quaternion.Euler(sunEuler);
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                RenderSettings.ambientLight = Color.Lerp(ambient0, ambient, k);
                RenderSettings.fogColor = Color.Lerp(fog0, fog, k);
                RenderSettings.fogDensity = Mathf.Lerp(density0, fogDensity, k);
                if (light != null)
                {
                    light.color = Color.Lerp(sun0, sun, k);
                    light.intensity = Mathf.Lerp(intensity0, sunIntensity, k);
                    light.transform.rotation = Quaternion.Slerp(rotation0, rotation1, k);
                }
                yield return null;
            }
        }
    }
}
