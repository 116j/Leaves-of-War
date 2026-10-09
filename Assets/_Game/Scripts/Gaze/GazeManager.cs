using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeManager : MonoBehaviour
{
    public static GazeManager Instance { get; private set; }

    [SerializeField]
    LayerMask _occlusionMask;
    [SerializeField]
    float _maxGazeDistance = 12f;
    [SerializeField]
    int _checksPerFrame = 5;

    readonly List<Observable> _observables = new();
    readonly Plane[] _frusrumPlanes = new Plane[6];
    readonly float _tickTime = 0.1f;
    readonly float _fovReduction = -8f;

    float _tickTimer;
    float _originFOV;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AddObservables();
    }

    void Update()
    {
        _tickTimer += Time.deltaTime;

        if (_tickTimer >= _tickTime)
        {
            _tickTimer = 0;
            StartCoroutine(CheckObservables());
        }
    }
    /// <summary>
    /// Refreshes observables in a new scene
    /// </summary>
    public void AddObservables()
    {
        _observables.Clear();
        _observables.AddRange(FindObjectsByType<Observable>());
    }
    /// <summary>
    /// Gets planes with specific FOV and checks <see cref="_checksPerFrame"/> observables's visibility per frame
    /// </summary>
    /// <returns></returns>
    IEnumerator CheckObservables()
    {
        _originFOV = Camera.main.fieldOfView;
        Camera.main.fieldOfView = _originFOV + _fovReduction;
        GeometryUtility.CalculateFrustumPlanes(Camera.main, _frusrumPlanes);
        Camera.main.fieldOfView = _originFOV;

        int observablesChecked = 0;
        for (int i = 0; i < _observables.Count; i++)
        {
            CheckVisibility(_observables[i]);
            observablesChecked++;
            if (observablesChecked % _checksPerFrame == 0)
            {
                yield return null;
            }
        }
    }
    /// <summary>
    /// Checks visibility for <paramref name="observable"/> and sets its seen state 
    /// </summary>
    /// <param name="observable"></param>
    void CheckVisibility(Observable observable)
    {
        if (observable.Renderer == null)
            return;
        if (!GeometryUtility.TestPlanesAABB(_frusrumPlanes, observable.Renderer.bounds))
        {
            SetSeen(observable, false);
        }
        else
        {
            Vector3 center = observable.Renderer.bounds.center;
            float distance = Vector3.Distance(Camera.main.transform.position, center);
            if (distance > _maxGazeDistance)
            {
                SetSeen(observable, false);
            }
            else
            {
                bool hasObstacles = Physics.Linecast(Camera.main.transform.position, center, _occlusionMask);
                SetSeen(observable, !hasObstacles);
            }
        }
    }
    /// <summary>
    /// Sets observable's seen state
    /// </summary>
    /// <param name="observable"></param>
    /// <param name="seen"></param>
    void SetSeen(Observable observable, bool seen)
    {
        if (seen == observable.IsSeen)
            return;

        if (seen)
        {
            observable.OnSeen.Invoke();
        }
        else
        {
            observable.OnUnseen.Invoke();
        }
    }

    void OnDrawGizmos()
    {

        DrawFrustumGizmo();

        if (Application.isPlaying)
        {
            DrawObservableGizmos();

            DrawOcclusionRays();
        }
    }

    void DrawFrustumGizmo()
    {
        float near = Camera.main.nearClipPlane;
        float far = Camera.main.farClipPlane;
        float fov = Camera.main.fieldOfView + _fovReduction;
        float aspect = Camera.main.aspect;

        float halfHeightNear = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float halfWidthNear = halfHeightNear * aspect;

        float halfHeightFar = far * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float halfWidthFar = halfHeightFar * aspect;

        Transform t = Camera.main.transform;

        Vector3 nearCenter = t.position + t.forward * near;
        Vector3 farCenter = t.position + t.forward * far;

        Vector3 nTL = nearCenter + t.up * halfHeightNear - t.right * halfWidthNear;
        Vector3 nTR = nearCenter + t.up * halfHeightNear + t.right * halfWidthNear;
        Vector3 nBL = nearCenter - t.up * halfHeightNear - t.right * halfWidthNear;
        Vector3 nBR = nearCenter - t.up * halfHeightNear + t.right * halfWidthNear;

        Vector3 fTL = farCenter + t.up * halfHeightFar - t.right * halfWidthFar;
        Vector3 fTR = farCenter + t.up * halfHeightFar + t.right * halfWidthFar;
        Vector3 fBL = farCenter - t.up * halfHeightFar - t.right * halfWidthFar;
        Vector3 fBR = farCenter - t.up * halfHeightFar + t.right * halfWidthFar;

        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);

        //near
        Gizmos.DrawLine(nTL, nTR);
        Gizmos.DrawLine(nTR, nBR);
        Gizmos.DrawLine(nBR, nBL);
        Gizmos.DrawLine(nBL, nTL);

        //far
        Gizmos.DrawLine(fTL, fTR);
        Gizmos.DrawLine(fTR, fBR);
        Gizmos.DrawLine(fBR, fBL);
        Gizmos.DrawLine(fBL, fTL);

        //side
        Gizmos.DrawLine(nTL, fTL);
        Gizmos.DrawLine(nTR, fTR);
        Gizmos.DrawLine(nBL, fBL);
        Gizmos.DrawLine(nBR, fBR);
    }

    void DrawObservableGizmos()
    {
        if (_observables == null) return;

        foreach (var obs in _observables)
        {
            if (obs == null) continue;
            var renderer = obs.Renderer;
            if (renderer == null) continue;

            Gizmos.color = obs.IsSeen
                ? new Color(0f, 1f, 0f, 0.5f)
                : new Color(1f, 0f, 0f, 0.5f);

            Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
        }
    }

    void DrawOcclusionRays()
    {
        if (_observables == null) return;

        Vector3 camPos = Camera.main.transform.position;

        foreach (var obs in _observables)
        {
            if (obs == null) continue;
            var renderer = obs.Renderer;
            if (renderer == null) continue;

            Vector3 center = renderer.bounds.center;

            Gizmos.color = obs.IsSeen
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(1f, 0f, 0f, 0.3f);

            Gizmos.DrawLine(camPos, center);
        }
    }
}
