
using Unity.Cinemachine;
using UnityEngine;

public class DiaryInteraction : KeyboardControl
{
    [SerializeField] private CinemachineBrain _cinemachineBrain;
    [SerializeField] private CinemachineCamera _vc1;   
    [SerializeField] private CinemachineCamera _vc2;  
    [SerializeField] private CinemachineCamera _vc3;  
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private float _frontYaw = 90f;

    private ObjectViewController _controller;

    private void Start()
    {
        _controller = new ObjectViewController(
            _cinemachineBrain, _vc1, _vc2, _vc3, _cameraController, _frontYaw);
        _controller.OnInit();
    }

    public override void OnKeyPressed(KeyCode _keyCode)
    {
        if (_controller == null || _controller.IsBusy) return;

        StartCoroutine(_controller.State == ViewState.Look
            ? _controller.Enter()
            : _controller.Exit());
    }
}