using UnityEngine;

public sealed class CarriedProp : MonoBehaviour
{
    public enum Mode
    {
        CarriedThenLeftOnTheGround,
        StaysWhereIPutIt
    }

    [Tooltip("Who carries it.")]
    [SerializeField] private Transform carrier;
    [Tooltip("Carried Then Left On The Ground: it moves with him keeping the place you gave it next to him (in the Scene view), and when he disappears it drops straight down to the ground, keeping its rotation.\nStays Where I Put It: it never moves; it is hidden until he appears.")]
    [SerializeField] private Mode mode = Mode.CarriedThenLeftOnTheGround;
    [Tooltip("Off: when he disappears it stays exactly where it is, without dropping to the ground.")]
    [SerializeField] private bool dropToGround = true;

    private Renderer[] renderers;
    private Vector3 offset;
    private Quaternion turn = Quaternion.identity;
    private bool carried;
    private bool dropped;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        if (carrier != null)
        {
            offset = carrier.InverseTransformPoint(transform.position);
            turn = Quaternion.Inverse(carrier.rotation) * transform.rotation;
        }
    }

    private void LateUpdate()
    {
        if (dropped || carrier == null)
            return;
        bool present = carrier.gameObject.activeInHierarchy;
        if (!present)
        {
            if (carried)
                Drop();
            else
                Show(false);
            return;
        }
        carried = true;
        Show(true);
        if (mode == Mode.CarriedThenLeftOnTheGround)
            transform.SetPositionAndRotation(carrier.TransformPoint(offset), carrier.rotation * turn);
    }

    private void Drop()
    {
        dropped = true;
        Show(true);
        if (mode != Mode.CarriedThenLeftOnTheGround || !dropToGround || renderers.Length == 0)
            return;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        float ground = float.MinValue;
        Vector3 from = new Vector3(b.center.x, b.min.y + 0.3f, b.center.z);
        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<CharacterController>() == null)
                ground = Mathf.Max(ground, hit.point.y);
        if (ground == float.MinValue)
            ground = carrier.position.y;
        transform.position += Vector3.up * (ground - b.min.y);
    }

    private void Show(bool on)
    {
        foreach (Renderer r in renderers)
            if (r != null)
                r.forceRenderingOff = !on;
    }
}
