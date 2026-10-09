using Hortensia.Runtime;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class LowResolutionPresenter : MonoBehaviour
{
    [Header("Retro Filter Tuning")]
    [Tooltip("Number of horizontal blocks the image is snapped to when the retro filter is on. Lower = blockier.")]
    [SerializeField, Range(40f, 640f)] private float pixelBlocks = 160f;
    [Tooltip("Colour depth per channel after dithering. Lower = more visible PS1-style banding on gradients/shadows.")]
    [SerializeField, Range(4f, 64f)] private float ditherDepth = 32f;
    [Tooltip("How far the red/blue channels shift from the green channel, as a fraction of the screen.")]
    [SerializeField, Range(0f, 0.02f)] private float chromaticAberrationStrength = 0.0025f;
    [Tooltip("How strong the per-pixel grain/noise is.")]
    [SerializeField, Range(0f, 0.3f)] private float grainStrength = 0.06f;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private Vector2Int lastPixelSize;
    private float lastCanvasScale;
    private GameSessionRegistration sessionRegistration;
    private static Shader worldEffectsShader;
    private Material worldEffectsMaterial;

    public bool IsSceneWorldOutput { get; private set; } = true;

    private void Awake()
    {
        sessionRegistration = new GameSessionRegistration(
            session =>
            {
                if (IsSceneWorldOutput)
                    session.RegisterWorldOutput(this);
            },
            session => session.UnregisterWorldOutput(this));
        CacheReferences();
        ApplyWorldEffectsMaterialIfWorldOutput();
    }

    /// <summary>
    /// Only the scene's WORLD image gets gamma/retro-filter treatment
    /// (subtitles/UI stay crisp and untouched). This bypasses URP's Volume
    /// system entirely and applies both effects directly on this RawImage's
    /// own material, since the Volume-based approach proved unreliable in
    /// this project's pipeline.
    /// </summary>
    private void ApplyWorldEffectsMaterialIfWorldOutput()
    {
        if (!IsSceneWorldOutput)
            return;

        if (!TryGetComponent(out RawImage image))
            return;

        if (worldEffectsShader == null)
            worldEffectsShader = Shader.Find("Hortensia/WorldRetroEffects");
        if (worldEffectsShader == null)
        {
            Debug.LogWarning("Hortensia/WorldRetroEffects shader not found; gamma/retro filter options will have no effect.");
            return;
        }

        worldEffectsMaterial = new Material(worldEffectsShader) { name = "World Output Effects" };
        image.material = worldEffectsMaterial;
        GammaSetting.Applied += ApplyGammaValue;
        RetroFilterSetting.Applied += ApplyRetroFilterValue;
        ApplyGammaValue(GammaSetting.Current);
        ApplyRetroFilterValue(RetroFilterSetting.Current);
        ApplyTuningValues();
    }

    /// <summary>
    /// Pushes the Inspector-tunable retro filter values onto the material.
    /// Called on creation and from OnValidate, so dragging the sliders in
    /// the Inspector while in Play mode previews changes immediately.
    /// </summary>
    private void ApplyTuningValues()
    {
        if (worldEffectsMaterial == null)
            return;

        worldEffectsMaterial.SetFloat("_PixelBlocks", pixelBlocks);
        worldEffectsMaterial.SetFloat("_DitherDepth", ditherDepth);
        worldEffectsMaterial.SetFloat("_ChromaStrength", chromaticAberrationStrength);
        worldEffectsMaterial.SetFloat("_GrainStrength", grainStrength);
    }

    private void OnValidate()
    {
        ApplyTuningValues();
    }

    private void ApplyGammaValue(float gamma)
    {
        if (worldEffectsMaterial != null)
            worldEffectsMaterial.SetFloat("_Gamma", gamma);
    }

    private void ApplyRetroFilterValue(bool enabled)
    {
        if (worldEffectsMaterial != null)
            worldEffectsMaterial.SetFloat("_RetroEnabled", enabled ? 1f : 0f);
    }

    private void OnEnable()
    {
        CacheReferences();
        sessionRegistration?.Enable();
        ApplyLayout();
    }

    private void OnDisable()
    {
        sessionRegistration?.Disable();
    }

    private void OnDestroy()
    {
        sessionRegistration?.Dispose();
        sessionRegistration = null;
        GammaSetting.Applied -= ApplyGammaValue;
        RetroFilterSetting.Applied -= ApplyRetroFilterValue;
        if (worldEffectsMaterial != null)
            Destroy(worldEffectsMaterial);
    }

    public void MarkAsGeneratedOverlay()
    {
        if (!IsSceneWorldOutput)
            return;

        IsSceneWorldOutput = false;
        GameSession.Instance?.UnregisterWorldOutput(this);

        // Awake (which may have already applied the effects material) runs
        // synchronously during AddComponent, before the caller gets a chance
        // to call this method. Undo it here if that happened.
        if (worldEffectsMaterial != null)
        {
            GammaSetting.Applied -= ApplyGammaValue;
            RetroFilterSetting.Applied -= ApplyRetroFilterValue;
            if (TryGetComponent(out RawImage image))
                image.material = null;
            Destroy(worldEffectsMaterial);
            worldEffectsMaterial = null;
        }
    }

    private void LateUpdate()
    {
        if (rootCanvas == null)
        {
            CacheReferences();
            if (rootCanvas == null)
                return;
        }

        Rect pixelRect = rootCanvas.pixelRect;
        Vector2Int pixelSize = new(
            Mathf.RoundToInt(pixelRect.width),
            Mathf.RoundToInt(pixelRect.height));

        if (pixelSize != lastPixelSize ||
            !Mathf.Approximately(rootCanvas.scaleFactor, lastCanvasScale))
        {
            ApplyLayout();
        }
    }

    private void CacheReferences()
    {
        rectTransform = GetComponent<RectTransform>();
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    private void ApplyLayout()
    {
        if (rectTransform == null || rootCanvas == null)
            return;

        Rect pixelRect = rootCanvas.pixelRect;
        if (pixelRect.width <= 0f || pixelRect.height <= 0f)
            return;

        Vector2Int nativeSize = RetroResolution.WorldSize;
        float fitScale = Mathf.Min(
            pixelRect.width / nativeSize.x,
            pixelRect.height / nativeSize.y);
        // Keep source texels aligned to whole display pixels whenever the
        // frame fits at native size. Very small windows still need a
        // fractional fallback; clamping them to a 1x scale would crop the
        // image, while flooring the scale would collapse it to zero.
        float presentationScale = fitScale >= 1f
            ? Mathf.Floor(fitScale)
            : fitScale;
        float canvasScale = Mathf.Max(rootCanvas.scaleFactor, Mathf.Epsilon);
        float displayedWidth = nativeSize.x * presentationScale / canvasScale;
        float displayedHeight = nativeSize.y * presentationScale / canvasScale;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, displayedWidth);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, displayedHeight);

        lastPixelSize = new Vector2Int(
            Mathf.RoundToInt(pixelRect.width),
            Mathf.RoundToInt(pixelRect.height));
        lastCanvasScale = rootCanvas.scaleFactor;
    }
}