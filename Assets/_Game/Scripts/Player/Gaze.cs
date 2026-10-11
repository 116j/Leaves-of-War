using UnityEngine;

public static class Gaze
{
    private static readonly Plane[] Planes = new Plane[6];
    private static int planesFrame = -1;
    private static Camera planesCamera;

    private static FirstPersonController player;

    public static Camera View
    {
        get
        {
            if (player == null)
                player = Object.FindAnyObjectByType<FirstPersonController>();
            return player != null && player.PlayerCamera != null ? player.PlayerCamera : Camera.main;
        }
    }

    public static bool IsSeen(Bounds bounds, float maxDistance = 12f, float edge = 0.06f, Transform ignore = null)
    {
        if (DiaryInteraction.IsReading)
            return false;
        Camera cam = View;
        if (cam == null)
            return false;

        Vector3 eye = cam.transform.position;
        if (Vector3.Distance(eye, bounds.ClosestPoint(eye)) > maxDistance)
            return false;

        if (planesFrame != Time.frameCount || planesCamera != cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, Planes);
            planesFrame = Time.frameCount;
            planesCamera = cam;
        }
        if (!GeometryUtility.TestPlanesAABB(Planes, bounds))
            return false;

        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        Vector3[] samples =
        {
            c, c + new Vector3(e.x, 0f, e.z) * 0.8f, c + new Vector3(-e.x, 0f, e.z) * 0.8f,
            c + new Vector3(e.x, 0f, -e.z) * 0.8f, c + new Vector3(-e.x, 0f, -e.z) * 0.8f
        };
        foreach (Vector3 point in samples)
        {
            Vector3 viewport = cam.WorldToViewportPoint(point);
            if (viewport.z <= 0f || viewport.x < edge || viewport.x > 1f - edge || viewport.y < edge || viewport.y > 1f - edge)
                continue;
            if (!Blocked(eye, point, ignore))
                return true;
        }
        return false;
    }

    public static bool IsSeen(GameObject target, float maxDistance = 12f)
    {
        if (target == null || !target.activeInHierarchy)
            return false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return IsSeen(new Bounds(target.transform.position, Vector3.one * 0.5f), maxDistance, 0.06f, target.transform);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return IsSeen(b, maxDistance, 0.06f, target.transform);
    }

    private static bool Blocked(Vector3 from, Vector3 to, Transform ignore)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to) - 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<CharacterController>() != null)
                continue;
            if (hit.collider.GetComponentInParent<PickupItem>() != null)
                continue;
            if (ignore != null && hit.collider.transform.IsChildOf(ignore))
                continue;
            return true;
        }
        return false;
    }
}
