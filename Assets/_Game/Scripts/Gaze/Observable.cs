using UnityEngine;
using UnityEngine.Events;

public class Observable : MonoBehaviour
{
    public bool IsSeen { get; private set; }

    [SerializeField]
    float _changeDelay = 0.6f;

    public UnityEvent OnSeen;
    public UnityEvent OnUnseen;
    public UnityEvent OnChangeAllowed;

    Renderer _renderer;
    public Renderer Renderer => _renderer;

    public float _unseenTime;
    bool _gazeManagerSeen = false;

    private void Start()
    {
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
            if (_unseenTime >= _changeDelay)
            {
                _gazeManagerSeen = false;
                OnChangeAllowed.Invoke();
            }
        }
    }
}
