
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public enum ViewState { Look, Front, Zoom }  

public class ObjectViewController
{
    private const int HighPriority = 20;
    private const int LowPriority = 0;

    private readonly CinemachineBrain _brain;
    private readonly CinemachineCamera _vc1;   
    private readonly CinemachineCamera _vc2;  
    private readonly CinemachineCamera _vc3;  
    private readonly CameraController _cameraController;
    private readonly float _frontYaw;

    public static Action<bool> ToggleCameraMovement;
    public static Action<bool, Transform> ToggleParent;  

    public ViewState State { get; private set; } = ViewState.Look;
    public bool IsBusy { get; private set; }

    public ObjectViewController(
        CinemachineBrain brain,
        CinemachineCamera vc1,
        CinemachineCamera vc2,
        CinemachineCamera vc3,
        CameraController cameraController,
        float frontYaw = 90f)
    {
        _brain = brain;
        _vc1 = vc1;
        _vc2 = vc2;
        _vc3 = vc3;
        _cameraController = cameraController;
        _frontYaw = frontYaw;
    }

    public void OnInit()
    {
        SetPriorities(_vc1);
        State = ViewState.Look;
    }


    public IEnumerator Enter()
    {
        IsBusy = true;
        ToggleCameraMovement?.Invoke(false);

        Vector3 e = _vc1.transform.eulerAngles;

        _vc2.transform.SetPositionAndRotation(
            _vc1.transform.position,
            Quaternion.Euler(_frontYaw, e.y, e.z)
        );

        
        yield return SwitchTo(_vc2);
        State = ViewState.Front;

        ToggleParent?.Invoke(true, _vc1.transform);

        _cameraController.VC3Rot();          
        yield return SwitchTo(_vc3);
        State = ViewState.Zoom;
       
        IsBusy = false;
    }


    public IEnumerator Exit()
    {
        IsBusy = true;

        yield return SwitchTo(_vc2);
        State = ViewState.Front;

        ToggleParent?.Invoke(false, null);

       
        _vc1.transform.position = _vc2.transform.position;
        _cameraController.SyncTo(_vc2.transform.rotation);



        yield return SwitchTo(_vc1, cut: true);
        State = ViewState.Look;
       
        ToggleCameraMovement?.Invoke(true);

        _vc2.transform.position = _vc3.transform.position;
        IsBusy = false;
    }

    private IEnumerator SwitchTo(CinemachineCamera cam, bool cut = false)
    {
        CinemachineBlendDefinition prevBlend = _brain.DefaultBlend;
        if (cut)
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

        SetPriorities(cam);

        yield return null; 
        yield return new WaitUntil(() => _brain.ActiveVirtualCamera == (ICinemachineCamera)cam);
        yield return new WaitUntil(() => !_brain.IsBlending);

        _brain.DefaultBlend = prevBlend;
    }

    private void SetPriorities(CinemachineCamera active)
    {
        _vc1.Priority = LowPriority;
        _vc2.Priority = LowPriority;
        _vc3.Priority = LowPriority;
        active.Priority = HighPriority;
    }
}