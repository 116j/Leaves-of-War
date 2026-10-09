using UnityEngine;

namespace Hortensia.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PS1Fog : MonoBehaviour
    {
        // Initial greybox scenes use values as high as 0.2, which makes a
        // normal-sized room opaque within a few metres. Preserve the PS1 fog
        // language without hiding the playable route.
        private const float MaximumNavigableExponentialFogDensity = 0.008f;

        public enum Falloff
        {
            Linear,
            Exponential
        }

        [Tooltip("Turn fog off entirely without removing this component.")]
        [SerializeField] private bool enableFog = true;

        [Header("Falloff")]
        [SerializeField] private Falloff falloff = Falloff.Linear;
        [Tooltip("Distance where fog starts (Linear mode).")]
        [SerializeField, Min(0f)] private float fogStart = 4f;
        [Tooltip("Distance where fog is fully opaque (Linear mode).")]
        [SerializeField, Min(0.01f)] private float fogEnd = 22f;
        [Tooltip("Fog thickness (Exponential mode only).")]
        [SerializeField, Min(0f)] private float fogDensity = 0.05f;

        [Header("Colour")]
        [Tooltip("Should usually match the camera's background/clear colour.")]
        [SerializeField] private Color fogColor = new Color(0.02f, 0.02f, 0.03f, 1f);

        [Header("Render Distance")]
        [Tooltip("Caps how far the camera draws geometry using the main fog profile.")]
        [SerializeField, Range(5f, 500f)] private float renderDistance = 24f;

        [Header("Alternate Fog")]
        [Tooltip("A second, independent fog profile for materials listed below.")]
        [SerializeField, Min(0f)] private float altFogStart = 4f;
        [SerializeField, Min(0.01f)] private float altFogEnd = 22f;
        [SerializeField] private Color altFogColor = new Color(0.05f, 0.07f, 0.04f, 1f);
        [Tooltip("Materials listed here use the Alternate Fog profile above.")]
        [SerializeField] private Material[] alternateFogMaterials = System.Array.Empty<Material>();

        [Header("Alternate Render Distance")]
        [Tooltip("A SECOND, separate render distance for Alternate Fog Materials.")]
        [SerializeField, Min(1f)] private float alternateRenderDistance = 60f;
        [Tooltip("Materials listed here use Alternate Render Distance above instead of the main Render Distance for the fade/cull cutoff - independent of Alternate Fog Materials above.")]
        [SerializeField] private Material[] alternateRenderDistanceMaterials = System.Array.Empty<Material>();
        [Tooltip("How often (seconds) to re-check distances.")]
        [SerializeField, Min(0.05f)] private float alternateRenderDistanceCheckInterval = 0.5f;

        [Header("Distance Fade")]
        [Tooltip(
            "MASTER SWITCH for the classic PS1 fade-with-distance effect. When ON, renderers on " +
            "the Cullable Layers below get a per-object dither fade value written to their shader " +
            "as they approach Render Distance / Alternate Render Distance, so they fade out " +
            "smoothly via dithering instead of popping. This NEVER disables a Renderer component - " +
            "hard visibility beyond the fade distance is left entirely to the camera's far clip " +
            "plane (see Apply Render Distance below), so nothing gets hidden/shown by toggling.")]
        [SerializeField] private bool enableDistanceFade = false;

        [Tooltip(
            "Only used when Enable Distance Fade above is ON. Only renderers on these layers get " +
            "the fade applied. Defaults to NOTHING on purpose - tick only the layers your foliage " +
            "actually lives on (e.g. Trees, Grass), so unrelated scene objects are never touched.")]
        [SerializeField] private LayerMask cullableLayers = 0;

        [Tooltip("Metri prima della soglia in cui l'oggetto sfuma via dither.")]
        [SerializeField, Min(0.1f)] private float fadeBandMetres = 8f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        private static readonly int UseAlternateFogId = Shader.PropertyToID("_UseAlternateFog");
        private static readonly int DistanceFadeId = Shader.PropertyToID("_PS1DistanceFade");

        private Renderer[] cachedAlternateFogRenderers;
        private Renderer[] cachedFogMaterialRenderers;
        private Renderer[] cachedOtherRenderers;
        private Renderer[] cachedAllRenderers;
        private MaterialPropertyBlock fogBlock;
        private float nextAlternateDistanceCheckTime;

        // Created lazily the first time it's actually needed (inside
        // ApplyFade, only reachable when Enable Distance Fade is ON) rather
        // than as a field initializer - MaterialPropertyBlock cannot be
        // constructed from a field initializer/constructor context; Unity
        // throws "CreateImpl is not allowed to be called from a
        // MonoBehaviour constructor" and the field would silently stay
        // null, which caused a NullReferenceException every frame.
        private MaterialPropertyBlock fadeBlock;

        private void Update()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Apply()
        {
            RenderSettings.fog = enableFog;
            ApplyRenderDistance();

            if (enableDistanceFade && Time.time >= nextAlternateDistanceCheckTime)
            {
                nextAlternateDistanceCheckTime = Time.time + alternateRenderDistanceCheckInterval;
                ApplyDistanceFade();
                if (debugLogging)
                    DebugLogTreeRenderers();
            }

            ApplyAlternateFogMaterials();

            if (!enableFog)
                return;

            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = falloff == Falloff.Linear
                ? FogMode.Linear
                : FogMode.Exponential;

            if (falloff == Falloff.Linear)
            {
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = Mathf.Max(fogEnd, fogStart + 0.01f);
            }
            else
            {
                RenderSettings.fogDensity = Mathf.Min(
                    fogDensity,
                    MaximumNavigableExponentialFogDensity);
            }

            Shader.SetGlobalFloat("_PS1AltFogStart", altFogStart);
            Shader.SetGlobalFloat("_PS1AltFogEnd", Mathf.Max(altFogEnd, altFogStart + 0.01f));
            Shader.SetGlobalColor("_PS1AltFogColor", altFogColor);
        }

        private void DebugLogTreeRenderers()
        {
            Camera cam = ResolveWorldCamera();
            if (cam == null || cachedAllRenderers == null)
                return;

            Vector3 camPos = cam.transform.position;

            foreach (Renderer renderer in cachedAllRenderers)
            {
                if (renderer == null || renderer.name.IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                float distance = Vector3.Distance(renderer.transform.position, camPos);
                Bounds bounds = renderer.bounds;
                bool inFrustum = GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(cam), bounds);

                Debug.Log(
                    $"[PS1Fog][TreeDebug] '{renderer.name}' enabled={renderer.enabled}, " +
                    $"distance={distance:F1}, boundsCenter={bounds.center}, boundsSize={bounds.size}, " +
                    $"inCameraFrustum={inFrustum}, layer={LayerMask.LayerToName(renderer.gameObject.layer)}");

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;

                    string surfaceType = material.HasProperty("_Surface")
                        ? material.GetFloat("_Surface") == 0 ? "Opaque" : "Transparent"
                        : "n/a";
                    string alphaClip = material.HasProperty("_AlphaClip")
                        ? material.GetFloat("_AlphaClip").ToString()
                        : "n/a";
                    string cutoff = material.HasProperty("_Cutoff")
                        ? material.GetFloat("_Cutoff").ToString("F2")
                        : "n/a";
                    string mainTexName = "none";
                    bool mainTexMipmaps = false;
                    if (material.mainTexture != null)
                    {
                        mainTexName = material.mainTexture.name;
                        mainTexMipmaps = material.mainTexture is Texture2D tex2D && tex2D.mipmapCount > 1;
                    }

                    Debug.Log(
                        $"[PS1Fog][TreeDebug]   material '{material.name}': surface={surfaceType}, " +
                        $"alphaClip={alphaClip}, cutoff={cutoff}, mainTex='{mainTexName}', " +
                        $"hasMipmaps={mainTexMipmaps} (if true AND this is a cutout leaf texture, " +
                        $"try disabling 'Generate Mip Maps' on that texture's Import Settings)");
                }
            }
        }

        private static Camera ResolveWorldCamera()
        {
            GameSession session = GameSession.Instance;
            if (session != null && session.SceneServices.TryGetPlayerCamera(out Camera cam, out _))
                return cam;

            var player = FindAnyObjectByType<global::FirstPersonController>();
            return player != null ? player.PlayerCamera : null;
        }

        /// <summary>
        /// Sets "_UseAlternateFog" per-Renderer via MaterialPropertyBlock,
        /// NOT by calling material.SetFloat() on the shared Material asset
        /// directly. Calling SetFloat on a shared Material at runtime can
        /// leak the change back into the actual project asset file even
        /// after exiting Play mode (a known Unity quirk with shared
        /// materials, unlike GameObject/component state, which is properly
        /// reverted) - which is exactly what was happening here: every
        /// material ever listed stayed permanently marked afterwards,
        /// including inside built players. MaterialPropertyBlock never
        /// touches the shared asset at all, so this is safe regardless of
        /// Editor Play mode or a built player.
        /// </summary>
        private void ApplyAlternateFogMaterials()
        {
            if (cachedAllRenderers == null || cachedFogMaterialRenderers == null)
                RefreshRendererCaches();

            fogBlock ??= new MaterialPropertyBlock();

            foreach (Renderer renderer in cachedFogMaterialRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(fogBlock);
                fogBlock.SetFloat(UseAlternateFogId, 1f);
                renderer.SetPropertyBlock(fogBlock);
            }
        }

        private static float DistanceToVisibleGeometry(Renderer renderer, Vector3 camPos)
        {
            Bounds bounds = renderer.bounds;
            if (bounds.extents.sqrMagnitude < 1e-8f)
                return Vector3.Distance(renderer.transform.position, camPos);

            Vector3 closest = bounds.ClosestPoint(camPos);
            return Vector3.Distance(closest, camPos);
        }

        /// <summary>
        /// Writes the classic PS1 dither-fade amount (0 = fully visible, 1 =
        /// fully faded out) to the renderer's shader based on distance.
        /// Never touches renderer.enabled - a renderer that's fully faded
        /// (fade = 1) is still an active, enabled Renderer; it's the shader
        /// itself that dithers it away. Actual hard visibility beyond
        /// Render Distance is handled separately by the camera's far clip
        /// plane (ApplyRenderDistance), not by this method.
        /// </summary>
        private void ApplyFade(Renderer renderer, Vector3 camPos, float cutoff)
        {
            if (renderer == null)
                return;

            fadeBlock ??= new MaterialPropertyBlock();

            float distance = DistanceToVisibleGeometry(renderer, camPos);
            float fade = Mathf.InverseLerp(cutoff - fadeBandMetres, cutoff, distance);

            fadeBlock.SetFloat(DistanceFadeId, fade);
            renderer.SetPropertyBlock(fadeBlock);
        }

        private void ApplyDistanceFade()
        {
            Camera cam = ResolveWorldCamera();
            if (cam == null)
            {
                Debug.LogWarning("[PS1Fog] ApplyDistanceFade: could not resolve the world camera via GameSession, skipping.");
                return;
            }

            if (cachedAllRenderers == null || cachedAlternateFogRenderers == null)
                RefreshRendererCaches();

            Vector3 camPos = cam.transform.position;

            foreach (Renderer renderer in cachedAlternateFogRenderers)
                ApplyFade(renderer, camPos, alternateRenderDistance);

            foreach (Renderer renderer in cachedOtherRenderers)
                ApplyFade(renderer, camPos, renderDistance);

            if (debugLogging)
            {
                Debug.Log(
                    $"[PS1Fog] ApplyDistanceFade: alt group={cachedAlternateFogRenderers.Length} " +
                    $"(cutoff={alternateRenderDistance}), main group={cachedOtherRenderers.Length} " +
                    $"(cutoff={renderDistance}), camPos={camPos}");
            }
        }

        private void RefreshRendererCaches()
        {
            var materialSet = new System.Collections.Generic.HashSet<Material>(alternateRenderDistanceMaterials);
            Renderer[] allSceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var alternateGroup = new System.Collections.Generic.List<Renderer>();
            var otherGroup = new System.Collections.Generic.List<Renderer>();
            var managedRenderers = new System.Collections.Generic.List<Renderer>();

            foreach (Renderer renderer in allSceneRenderers)
            {
                // Renderers outside Cullable Layers never get a fade value
                // written to them at all - left completely alone, e.g.
                // greybox/dev placeholder geometry on Default.
                if ((cullableLayers.value & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                managedRenderers.Add(renderer);

                bool isAlternate = false;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materialSet.Contains(materials[i]))
                    {
                        isAlternate = true;
                        break;
                    }
                }

                if (isAlternate)
                    alternateGroup.Add(renderer);
                else
                    otherGroup.Add(renderer);
            }

            cachedAlternateFogRenderers = alternateGroup.ToArray();
            cachedOtherRenderers = otherGroup.ToArray();
            cachedAllRenderers = managedRenderers.ToArray();

            // Fog classification is intentionally separate from the
            // Cullable Layers filter above (that filter only governs the
            // distance-fade system) - any renderer using a listed Alternate
            // Fog Material gets the fog toggle, on any layer.
            var fogMaterialSet = new System.Collections.Generic.HashSet<Material>(alternateFogMaterials);
            var fogGroup = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in allSceneRenderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (fogMaterialSet.Contains(materials[i]))
                    {
                        fogGroup.Add(renderer);
                        break;
                    }
                }
            }
            cachedFogMaterialRenderers = fogGroup.ToArray();

            if (debugLogging)
            {
                Debug.Log(
                    $"[PS1Fog] RefreshRendererCaches: managed={managedRenderers.Count} of {allSceneRenderers.Length} total scene renderers " +
                    $"(rest excluded by Cullable Layers), alternate-fog group={cachedAlternateFogRenderers.Length}, " +
                    $"other group={cachedOtherRenderers.Length}, fog-material group={cachedFogMaterialRenderers.Length}");

                foreach (Renderer r in cachedAlternateFogRenderers)
                {
                    Debug.Log(
                        $"[PS1Fog]   alt-group renderer: '{r.name}', materials=[{string.Join(", ", System.Array.ConvertAll(r.sharedMaterials, m => m != null ? m.name : "NULL"))}]");
                }
            }
        }

        private void ApplyRenderDistance()
        {
            float effectiveDistance = Mathf.Max(renderDistance, alternateRenderDistance);
            Camera[] cameras = Camera.allCameras;
            foreach (Camera cam in cameras)
            {
                if (cam == null || cam.orthographic)
                    continue;
                cam.farClipPlane = effectiveDistance;
            }
        }
    }
}