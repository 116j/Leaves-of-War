using Unity.Cinemachine;
using UnityEngine;

public class HeadBob : MonoBehaviour
{
    [SerializeField]
    protected CinemachineBasicMultiChannelPerlin _noise;

    private void OnEnable()
    {
        ObjectViewController.ToggleCameraMovement += ActivateHeadBob;
    }

    private void OnDisable()
    {
        ObjectViewController.ToggleCameraMovement -= ActivateHeadBob;
    }

    protected void ActivateHeadBob(bool activate)
    {
        _noise.AmplitudeGain = activate ? 1f : 0f;
    }


}
