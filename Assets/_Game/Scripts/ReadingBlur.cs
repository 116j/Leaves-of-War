using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReadingBlur : MonoBehaviour
{
    [SerializeField] 
    Volume _globalVolume;
    [SerializeField] 
    float _normalFocalLength = 1;     
    [SerializeField] 
    float _readingFocalLength = 40;    
    [SerializeField] 
    float _transitionSeconds = 1;

    private DepthOfField _dof;
    private float _targetFocalLength;
    private float _currentFocalLength;

    private void Start()
    {
        if (_globalVolume != null && _globalVolume.profile != null)
        {
            _globalVolume.profile.TryGet(out _dof);
        }
        _currentFocalLength = _normalFocalLength;
        _targetFocalLength = _normalFocalLength;
        ApplyFocus();
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