using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Renders diegetic UI into a transparent, higher-density texture and layers
    /// it over the centered world frame. The higher-resolution layer keeps the
    /// hard, point-filtered presentation while giving small type the configured
    /// source-pixel-density multiplier.
    /// </summary>
    internal sealed class DiegeticUiCompositor : IDisposable
    {
        private readonly GameObject cameraObject;
        private readonly GameObject outputObject;
        private readonly RenderTexture targetTexture;

        private DiegeticUiCompositor(
            GameObject cameraObject,
            Camera camera,
            GameObject outputObject,
            RenderTexture targetTexture)
        {
            this.cameraObject = cameraObject;
            Camera = camera;
            this.outputObject = outputObject;
            this.targetTexture = targetTexture;
        }

        public Camera Camera { get; }

        public static bool TryCreate(Transform owner, out DiegeticUiCompositor compositor)
        {
            compositor = null;

            GameSession session = GameSession.Instance;
            LowResolutionPresenter worldOutput = null;
            session?.SceneServices.TryGetWorldOutput(out worldOutput, out _);
            Canvas outputCanvas = worldOutput != null
                ? worldOutput.GetComponentInParent<Canvas>()
                : null;
            if (worldOutput == null || outputCanvas == null)
            {
                Debug.LogError(
                    $"Cannot create the {RetroResolution.Format(RetroResolution.DiegeticUiSize)} " +
                    "diegetic UI layer because the scene has no " +
                    "LowResolutionPresenter beneath an output Canvas.");
                return false;
            }

            RenderTexture texture = CreateTargetTexture();
            GameObject uiCameraObject = CreateUiCamera(owner, texture, out Camera uiCamera);
            GameObject uiOutputObject = CreateOutput(outputCanvas.transform, texture);

            compositor = new DiegeticUiCompositor(
                uiCameraObject,
                uiCamera,
                uiOutputObject,
                texture);
            return true;
        }

        public void SetVisible(bool visible)
        {
            if (outputObject != null)
                outputObject.SetActive(visible);
        }

        public void Dispose()
        {
            if (Camera != null)
                Camera.targetTexture = null;
            if (outputObject != null && outputObject.TryGetComponent(out RawImage image))
                image.texture = null;

            if (outputObject != null)
                UnityEngine.Object.Destroy(outputObject);
            if (cameraObject != null)
                UnityEngine.Object.Destroy(cameraObject);

            if (targetTexture != null)
            {
                targetTexture.Release();
                UnityEngine.Object.Destroy(targetTexture);
            }
        }

        private static RenderTexture CreateTargetTexture()
        {
            var texture = new RenderTexture(
                RetroResolution.DiegeticUiSize.x,
                RetroResolution.DiegeticUiSize.y,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = $"Diegetic UI {RetroResolution.Format(RetroResolution.DiegeticUiSize)}",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
            return texture;
        }

        private static GameObject CreateUiCamera(
            Transform owner,
            RenderTexture target,
            out Camera camera)
        {
            int uiLayer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
            var cameraObject = new GameObject(
                $"Diegetic UI Camera {RetroResolution.Format(RetroResolution.DiegeticUiSize)}");
            cameraObject.transform.SetParent(owner, false);
            cameraObject.layer = uiLayer;

            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << uiLayer;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.forceIntoRenderTexture = true;
            camera.targetTexture = target;

            UniversalAdditionalCameraData cameraData =
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;

            return cameraObject;
        }

        private static GameObject CreateOutput(Transform parent, RenderTexture texture)
        {
            var output = new GameObject(
                $"{RetroResolution.Format(RetroResolution.DiegeticUiSize)} Diegetic UI",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            output.transform.SetParent(parent, false);
            output.transform.SetAsLastSibling();

            RawImage image = output.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;

            output.AddComponent<LowResolutionPresenter>().MarkAsGeneratedOverlay();
            return output;
        }
    }
}
