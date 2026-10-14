using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeManager : MonoBehaviour
{
    public static GazeManager Instance { get; private set; }

    [SerializeField]
    float _maxGazeDistance = 12f;
    [SerializeField]
    int _checksPerFrame = 5;

    readonly List<Observable> _observables = new();
    readonly float _tickTime = 0.1f;

    float _tickTimer;

    Coroutine _checkCorutine;

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
            _checkCorutine ??= StartCoroutine(CheckObservables());
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

        _checkCorutine = null;
    }
    /// <summary>
    /// Checks visibility for <paramref name="observable"/> and sets its seen state 
    /// </summary>
    /// <param name="observable"></param>
    void CheckVisibility(Observable observable)
    {
        if (observable.Renderer == null)
            return;

        SetSeen(observable, Gaze.IsSeen(observable.Renderer.bounds, _maxGazeDistance, 0.06f, observable.transform));
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
}
