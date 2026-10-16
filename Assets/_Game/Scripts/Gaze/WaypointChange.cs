using UnityEngine;

public class WaypointChange : ChangeBehaviour
{
    [System.Serializable]
    public struct Point
    {
        public Transform target;
        [Tooltip("The step is done here: the next move waits for the next Move Object When Unseen.")]
        public bool stopHere;
        public bool carryLantern;
        public bool hide;
    }

    [SerializeField] Point[] _points = new Point[0];
    [SerializeField] GameObject _carriedLantern;
    [SerializeField] GameObject _hungLantern;

    int _next;
    bool _armed;

    public override void Arm()
    {
        base.Arm();
        _armed = true;
    }

    public override void OnChange()
    {
        if (!_armed || _next >= _points.Length)
            return;
        Point point = _points[_next];
        if (point.target != null)
        {
            if (Gaze.IsSeen(new Bounds(point.target.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f))))
                return;
            transform.SetPositionAndRotation(point.target.position, point.target.rotation);
        }
        if (_carriedLantern != null)
            _carriedLantern.SetActive(point.carryLantern);
        if (_hungLantern != null)
            _hungLantern.SetActive(!point.carryLantern);
        _next++;
        if (point.stopHere || _next >= _points.Length)
        {
            _armed = false;
            _changed = true;
        }
        if (point.hide)
            gameObject.SetActive(false);
    }
}
