
using UnityEngine;


public class GameObjectDetached : MonoBehaviour
{
    private Transform _originalParent;
    private Vector3 _localPos;
    private Quaternion _localRot;
    private Vector3 _localScale;

    private void Awake()
    {
        _originalParent = transform.parent;   
        _localPos = transform.localPosition;
        _localRot = transform.localRotation;
        _localScale = transform.localScale;
    }

    private void OnEnable() => ObjectViewController.ToggleParent += OnToggleParent;
    private void OnDisable() => ObjectViewController.ToggleParent -= OnToggleParent;

    private void OnToggleParent(bool anchor, Transform target)
    {
        if (anchor)
        {
          
            transform.SetParent(target, true);
        }
        else
        {
            transform.SetParent(_originalParent, false);
            transform.localPosition = _localPos;
            transform.localRotation = _localRot;
            transform.localScale = _localScale;
        }
    }
}