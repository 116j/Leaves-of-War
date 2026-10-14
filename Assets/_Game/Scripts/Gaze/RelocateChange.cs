using UnityEngine;

public class RelocateChange : ChangeBehaviour
{
    [SerializeField]
    Transform[] _relolcatePoints;

    int _nextPointIndex;

    public override void OnChange()
    {
        if(_nextPointIndex < _relolcatePoints.Length)
        {
            _changed = false;
            Transform target = _relolcatePoints[_nextPointIndex];
            Bounds pointBounds = new Bounds(target.position, Vector3.one * 0.1f);
            if (Gaze.IsSeen(pointBounds))
                return;

            transform.SetPositionAndRotation(target.position, target.rotation);
            _nextPointIndex++;
            _changed = true;
        }
    }
}
