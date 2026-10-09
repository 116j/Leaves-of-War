using UnityEngine;
using UnityEngine.Events;

public class Observable : MonoBehaviour
{
    public bool IsSeen { get; private set; }

    [SerializeField]
    float _changeDelay =0.6f;

    public UnityEvent OnSeen;
    public UnityEvent OnUnseen;
    public UnityEvent OnChangeAllowed;

    Renderer _renderer;
    public Renderer Renderer => _renderer;

    public float _unseenTime;
    bool _gazeManagerAway = false;

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        OnSeen.AddListener(() =>
        {
            Debug.Log("Seen");
            _unseenTime = 0f;
            IsSeen = true;
        });

        OnUnseen.AddListener(() =>
        {
            Debug.Log("Unseen");
            IsSeen = false;
            _gazeManagerAway = true;
        });
    }

    private void Update()
    {
        if (!IsSeen && _gazeManagerAway)
        {
            _unseenTime += Time.deltaTime;
            if(_unseenTime>= _changeDelay)
            {
                _gazeManagerAway = false;
                OnChangeAllowed.Invoke();
            }
        }
    }
}
