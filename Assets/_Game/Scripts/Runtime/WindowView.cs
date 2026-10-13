using UnityEngine;

public class WindowView : MonoBehaviour
{
    [Tooltip("0 = the picture stays where it is (flat, close). 1 = it moves with the player (infinitely far, like a sky).")]
    [SerializeField, Range(0f, 1f)] private float depth = 0.9f;
    [Tooltip("Size multiplier so the picture still fills the window when the player stands right in front of it.")]
    [SerializeField, Min(1f)] private float sizeMultiplier = 2.5f;
    [Tooltip("The picture never comes closer than this to the window (metres, along the window's outward axis).")]
    [SerializeField, Min(0.1f)] private float minDistance = 1f;
    [Tooltip("Where the player stands, in the window's local space, when the picture is exactly where it was placed.")]
    [SerializeField] private Vector3 viewFrom = new Vector3(0f, 0.8f, -2.5f);

    private Transform frame;
    private Transform view;
    private Vector3 baseLocal;

    private void Start()
    {
        frame = transform.parent != null ? transform.parent : transform;
        baseLocal = transform.localPosition;
        transform.localScale = Vector3.Scale(transform.localScale, new Vector3(sizeMultiplier, sizeMultiplier, 1f));
    }

    private void LateUpdate()
    {
        if (view == null)
        {
            Camera main = Camera.main;
            if (main == null)
                return;
            view = main.transform;
        }
        Vector3 cameraLocal = frame.InverseTransformPoint(view.position);
        Vector3 local = baseLocal + (cameraLocal - viewFrom) * depth;
        local.z = Mathf.Max(local.z, minDistance);
        transform.localPosition = local;
    }
}
