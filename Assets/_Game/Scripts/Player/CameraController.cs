using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _vc1;
    [SerializeField] private CinemachineCamera _vc2;
    [SerializeField] private CinemachineCamera _vc3;

    [SerializeField] private float _sensitivity = 2f;

    [Header("VC3 Rotation Offset")]
    [SerializeField] private float _zoomAddX = 0f;
    [SerializeField] private float _zoomSubY = 12f;

    [Header("Rotation Limits (degrees from start)")]
    [SerializeField] private float _minYaw = -60f;
    [SerializeField] private float _maxYaw = 60f;
    [SerializeField] private float _minPitch = -40f;
    [SerializeField] private float _maxPitch = 40f;

    private float _yaw;
    private float _pitch;

    private Quaternion _startRotation;
    private bool _canRotate = true;

    private void OnEnable()
    {
        ObjectViewController.ToggleCameraMovement += OnToggleCameraMovement;
    }

    private void OnDisable()
    {
        ObjectViewController.ToggleCameraMovement -= OnToggleCameraMovement;
    }

    private void OnToggleCameraMovement(bool isEnabled)
    {
        _canRotate = isEnabled;
    }

    private void Start()
    {
        _startRotation = _vc1.transform.localRotation;
    }

    private void Update()
    {
        if (_canRotate)
            CameraRotation();
    }

    private void CameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * _sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _sensitivity;

        _yaw = Mathf.Clamp(
            _yaw + mouseX,
            _minYaw,
            _maxYaw
        );

        _pitch = Mathf.Clamp(
            _pitch - mouseY,
            _minPitch,
            _maxPitch
        );

        _vc1.transform.localRotation =
            _startRotation *
            Quaternion.Euler(_pitch, _yaw, 0f);
    }

    public void SyncTo(Quaternion worldRotation)
    {
        Transform parent = _vc1.transform.parent;

        Quaternion local = parent
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;

        _startRotation = local;  
        _pitch = 0f;
        _yaw = 0f;

        _vc1.transform.localRotation = local;
    }

    public void VC3Rot()
    {
        Quaternion r = _vc1.transform.rotation;   
        Vector3 v1 = r.eulerAngles;

        float x =  _zoomAddX;
        float y = Mathf.DeltaAngle(0f, v1.y) - _zoomSubY;

        _vc3.transform.rotation = Quaternion.Euler(x, y, 0f);
        _vc3.transform.position = _vc1.transform.position;


    }
}