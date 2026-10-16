using System.Collections;
using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class LevelSky : MonoBehaviour
    {
        [SerializeField] private Material sky;
        [Tooltip("0.5 grey = the sky's own colours.")]
        [SerializeField] private Color tint = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField, Min(0f)] private float exposure = 1f;

        [Header("Change after a poem")]
        [Tooltip("Empty: the sky never changes in this level.")]
        [SerializeField] private Material laterSky;
        [SerializeField] private Color laterTint = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField, Min(0f)] private float laterExposure = 1f;
        [Tooltip("0 = any poem.")]
        [SerializeField] private int poemId;
        [SerializeField, Min(0f)] private float delay = 3f;
        [SerializeField, Min(0.5f)] private float seconds = 25f;

        private Material current;
        private bool changed;

        private void OnEnable() => TranscribePoem.Transcribed += OnTranscribed;

        private void OnDisable() => TranscribePoem.Transcribed -= OnTranscribed;

        private void Start()
        {
            if (sky == null)
                return;
            Show(sky, tint, exposure);
            foreach (Camera camera in Camera.allCameras)
                if (camera.CompareTag("MainCamera"))
                    camera.clearFlags = CameraClearFlags.Skybox;
        }

        private void OnTranscribed(int id)
        {
            if (changed || laterSky == null || poemId != 0 && id != poemId)
                return;
            changed = true;
            StartCoroutine(Change());
        }

        private IEnumerator Change()
        {
            yield return new WaitForSeconds(delay);
            float half = seconds * 0.5f;
            float from = current != null && current.HasProperty("_Exposure") ? current.GetFloat("_Exposure") : exposure;
            for (float t = 0f; t < 1f; t += Time.deltaTime / half)
            {
                SetExposure(Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            Show(laterSky, laterTint, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / half)
            {
                SetExposure(Mathf.Lerp(0f, laterExposure, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetExposure(laterExposure);
        }

        private void Show(Material source, Color color, float value)
        {
            if (current != null)
                Destroy(current);
            current = new Material(source) { name = source.name + " (level)" };
            if (current.HasProperty("_Tint"))
                current.SetColor("_Tint", color);
            SetExposure(value);
            RenderSettings.skybox = current;
        }

        private void SetExposure(float value)
        {
            if (current == null)
                return;
            if (current.HasProperty("_Exposure"))
                current.SetFloat("_Exposure", value);
            if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox)
                DynamicGI.UpdateEnvironment();
        }

        private void OnDestroy()
        {
            if (current != null)
                Destroy(current);
        }
    }
}
