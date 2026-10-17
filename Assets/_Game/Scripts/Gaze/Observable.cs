using UnityEngine;
using UnityEngine.Events;

public class Observable : MonoBehaviour
{
    public bool IsSeen { get; private set; }

    [SerializeField]
    float _changeDelay = 0.6f;
    [Tooltip("Objects the player can examine (Examinable inside): they never change while being examined, and only once all of them are examined or after this many seconds from when they were first seen.")]
    [SerializeField, Min(0f)]
    float _examineTime = 25f;

    public UnityEvent OnSeen;
    public UnityEvent OnUnseen;
    public UnityEvent OnChangeAllowed;

    Renderer _renderer;
    public Renderer Renderer => _renderer;

    public float _unseenTime;
    bool _gazeManagerSeen = false;
    Examinable[] _examinables;
    float _firstSeen = -1f;

    private void Start()
    {
        _examinables = GetComponentsInChildren<Examinable>(true);
        _renderer = GetComponent<Renderer>();
        if (_renderer == null || !_renderer.enabled)
        {
            float best = -1f;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                    continue;
                float size = r.bounds.size.sqrMagnitude;
                if (size > best)
                {
                    best = size;
                    _renderer = r;
                }
            }
        }
        OnSeen.AddListener(() =>
        {
            _unseenTime = 0f;
            IsSeen = true;
            _gazeManagerSeen = true;
            if (_firstSeen < 0f)
                _firstSeen = Time.time;
        });

        OnUnseen.AddListener(() =>
        {
            IsSeen = false;
        });

        if (OnChangeAllowed.GetPersistentEventCount() == 0 && !LevelFlow.Drives(this))
            foreach (ChangeBehaviour change in GetComponents<ChangeBehaviour>())
                OnChangeAllowed.AddListener(change.OnChange);
    }

    private void Update()
    {
        if (!IsSeen && _gazeManagerSeen)
        {
            _unseenTime += Time.deltaTime;
            if (_unseenTime >= _changeDelay && ReadyToChange())
            {
                _gazeManagerSeen = false;
                OnChangeAllowed.Invoke();
            }
        }
    }

    bool ReadyToChange()
    {
        if (_examinables == null || _examinables.Length == 0)
            return true;
        if (Examinable.IsOpen)
        {
            _unseenTime = 0f;
            return false;
        }
        bool all = true;
        foreach (Examinable item in _examinables)
            if (item != null && !item.Examined)
                all = false;
        return all || _firstSeen >= 0f && Time.time - _firstSeen >= _examineTime;
    }
}
