using UnityEngine;

public class PeripheralFigure : MonoBehaviour
{
    [Tooltip("How close to the border of the view it must be to show (0.18 = the outer 18% of the screen).")]
    [SerializeField, Range(0f, 0.45f)] float _edge = 0.18f;

    Renderer[] _renderers;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    void LateUpdate()
    {
        if (_renderers.Length == 0)
            return;
        Bounds bounds = _renderers[0].bounds;
        foreach (Renderer r in _renderers)
            bounds.Encapsulate(r.bounds);
        bool seen = Gaze.IsSeen(bounds, 30f, _edge, transform);
        foreach (Renderer r in _renderers)
            r.forceRenderingOff = seen;
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
