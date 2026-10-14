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
        if (_renderer == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                _renderer = transform.GetChild(i).GetComponent<Renderer>();
                if (_renderer != null && _renderer.enabled)
                    break;
            }
        }
        OnSeen.AddListener(() =>
        {
            Debug.Log("Seen");
            _unseenTime = 0f;
            IsSeen = true;
            _gazeManagerSeen = true;
        });

        OnUnseen.AddListener(() =>
        {
            Debug.Log("Unseen");
            IsSeen = false;
        });
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
