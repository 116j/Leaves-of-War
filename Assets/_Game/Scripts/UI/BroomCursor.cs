using UnityEngine;
using UnityEngine.InputSystem;

public class BroomCursor : MonoBehaviour
{
    public static BroomCursor Instance { get; private set; }
    [SerializeField]
    LayerMask _sweepMask;
    [SerializeField]
    LayerMask _menuItemMask;

    readonly float _idleTime = 8f;
    float _idleTimer;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        //set cursor if necessary, or in the settings
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 screenPos = mouse.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _sweepMask))
        {
            //sweep
        }

        if(mouse.delta.ReadValue().sqrMagnitude > 0.0001f)
        {
            _idleTimer = 0f;
        }
        else
        {
            _idleTimer+= Time.deltaTime;
            if(_idleTimer >= _idleTime)
            {
                //drift leaves back
            }
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out hit, 100f, _menuItemMask))
            {
                if(hit.collider.TryGetComponent(out TitleMenuItem menuItem))
                {
                    menuItem.ActivateItem();
                }
            }
        }
    }
}
