using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class HeldItemsCamera : MonoBehaviour
{
    [Tooltip("Layer drawn only by the held items camera, on top of the world.")]
    [SerializeField] private string layerName = "HeldItems";
    [Tooltip("Objects held in front of the camera: diary, broom.")]
    [SerializeField] private Transform[] items;
    [SerializeField, Min(0.001f)] private float nearClip = 0.01f;

    public static Camera Overlay { get; private set; }

    private Camera main;
    private Camera overlay;

    private void Awake()
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning("HeldItemsCamera: layer '" + layerName + "' missing. Run Tools/Leaves of War/Setup Held Items.", this);
            enabled = false;
            return;
        }

        foreach (Transform item in items)
        {
            if (item == null)
                continue;
            SetLayer(item, layer);
            foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }

        main = GetComponent<Camera>();
        main.cullingMask &= ~(1 << layer);

        var go = new GameObject("Held Items Camera");
        go.transform.SetParent(transform, false);
        overlay = go.AddComponent<Camera>();
        overlay.cullingMask = 1 << layer;
        overlay.nearClipPlane = nearClip;
        overlay.farClipPlane = 20f;
        overlay.fieldOfView = main.fieldOfView;
        Overlay = overlay;

        UniversalAdditionalCameraData overlayData = overlay.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderShadows = false;
        UniversalAdditionalCameraData mainData = main.GetUniversalAdditionalCameraData();
        if (!mainData.cameraStack.Contains(overlay))
            mainData.cameraStack.Add(overlay);
    }

    private void LateUpdate()
    {
        if (overlay == null)
            return;
        overlay.fieldOfView = main.fieldOfView;
        overlay.usePhysicalProperties = main.usePhysicalProperties;
    }

    private void OnDestroy()
    {
        if (Overlay == overlay)
            Overlay = null;
        if (main != null && overlay != null)
            main.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
    }

    private static void SetLayer(Transform root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
