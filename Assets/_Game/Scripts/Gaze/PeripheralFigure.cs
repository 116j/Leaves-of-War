using UnityEngine;

public class PeripheralFigure : MonoBehaviour
{
    [Tooltip("The figure disappears while its centre is within this angle (degrees) from where the player looks, and shows again in the corner of the eye.")]
    [SerializeField, Range(3f, 60f)] float _focusAngle = 16f;
    [SerializeField, Min(1f)] float _maxDistance = 30f;
    [Tooltip("On: the opposite - the figure is there only while looked at, and disappears as soon as the player looks away.")]
    [SerializeField] bool _onlyWhileLookedAt;

    Renderer[] _renderers;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    void LateUpdate()
    {
        if (_renderers.Length == 0)
            return;
        Camera cam = Gaze.View;
        if (cam == null)
            return;
        Bounds bounds = _renderers[0].bounds;
        foreach (Renderer r in _renderers)
            bounds.Encapsulate(r.bounds);
        Vector3 eye = cam.transform.position;
        Vector3 toCentre = bounds.center - eye;
        bool looked = !DiaryInteraction.IsReading && toCentre.magnitude <= _maxDistance &&
                      Vector3.Angle(cam.transform.forward, toCentre) < _focusAngle;
        bool hidden = _onlyWhileLookedAt ? !looked : looked;
        foreach (Renderer r in _renderers)
            r.forceRenderingOff = hidden;
    }

    void OnDisable()
    {
        if (_renderers == null)
            return;
        foreach (Renderer r in _renderers)
            if (r != null)
                r.forceRenderingOff = false;
    }
}
