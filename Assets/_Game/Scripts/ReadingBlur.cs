using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReadingBlur : MonoBehaviour
{
    [Tooltip("Empty: the scene's global volume is used.")]
    [SerializeField] 
    Volume _globalVolume;
    [SerializeField] 
    float _normalFocalLength = 1;     
    [SerializeField] 
    float _readingFocalLength = 40;    
    [SerializeField] 
    float _transitionSeconds = 0.6f;

    private DepthOfField _dof;
    private float _targetFocalLength;
    private float _currentFocalLength;

    private void Start()
    {
        if (_globalVolume == null)
            _globalVolume = GlobalVolume();
        if (_globalVolume != null)
        {
            VolumeProfile profile = _globalVolume.profile;
            if (profile != null && !profile.TryGet(out _dof))
            {
                _dof = profile.Add<DepthOfField>(true);
                _dof.mode.value = DepthOfFieldMode.Bokeh;
                _dof.focusDistance.value = 0.1f;
                _dof.aperture.value = 1f;
                _dof.bladeCount.value = 7;
            }
            if (_dof != null)
            {
                _dof.active = true;
                _dof.mode.overrideState = true;
                _dof.focalLength.overrideState = true;
            }
        }
        foreach (Camera camera in Camera.allCameras)
            if (camera.CompareTag("MainCamera") && camera.TryGetComponent(out UniversalAdditionalCameraData data))
                data.renderPostProcessing = true;
        _currentFocalLength = _normalFocalLength;
        _targetFocalLength = _normalFocalLength;
        ApplyFocus();
    }

    private static Volume GlobalVolume()
    {
        Volume best = null;
        foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (volume.isGlobal && volume.enabled && (best == null || volume.priority > best.priority))
                best = volume;
        return best;
    }

    private void Update()
    {
        if (_dof == null) return;
        _currentFocalLength = Mathf.MoveTowards(_currentFocalLength, _targetFocalLength,
            Mathf.Abs(_normalFocalLength - _readingFocalLength) / _transitionSeconds * Time.unscaledDeltaTime);
        ApplyFocus();
    }

    public void SetReading(bool reading)
    {
        _targetFocalLength = reading ? _readingFocalLength : _normalFocalLength;
    }

    private void ApplyFocus()
    {
        if (_dof == null) return;
        _dof.focalLength.value = _currentFocalLength;
    }
}
