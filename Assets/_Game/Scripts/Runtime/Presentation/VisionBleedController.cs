using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class VisionBleedController : MonoBehaviour
    {
        [SerializeField] private Image overlay;
        [SerializeField] private Color tint = new Color(0.24f, 0.05f, 0.09f, 1f);
        [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.6f;
        private GameSessionRegistration sessionRegistration;

        public float Intensity { get; private set; }

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterVisionBleed(this),
                session => session.UnregisterVisionBleed(this));
            CacheOverlay();
            Apply();
        }

        private void OnEnable()
        {
            sessionRegistration?.Enable();
            CacheOverlay();
            Apply();
        }

        private void OnDisable()
        {
            sessionRegistration?.Disable();
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }

        public void SetIntensity(float visionIntensity)
        {
            // Not clamped here on purpose: a caller (e.g. a swap-transition
            // spike) may intentionally pass a value above 1 to punch through
            // stronger than any sustained baseline could. The final alpha
            // is still clamped safely in Apply().
            Intensity = Mathf.Max(0f, visionIntensity);
            Apply();
        }

        public void Clear()
        {
            SetIntensity(0f);
        }

        private void CacheOverlay()
        {
            if (overlay == null)
                overlay = GetComponent<Image>();

            if (overlay != null)
                overlay.raycastTarget = false;

            CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = RetroResolution.WorldSize;
            }
        }

        private void Apply()
        {
            if (overlay == null)
                return;

            Color color = tint;
            color.a = Mathf.Clamp01(maximumAlpha * Intensity);
            overlay.color = color;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheOverlay();
            maximumAlpha = Mathf.Clamp01(maximumAlpha);
            Apply();
        }
#endif
    }
}