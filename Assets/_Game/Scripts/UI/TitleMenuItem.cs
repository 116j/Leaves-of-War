using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class TitleMenuItem : RevealableObject
{
    [SerializeField]
    UnityEvent onMenuItemSelected;

    Collider _collider;
    TextMeshPro _text;

    private void Start()
    {
        _collider = GetComponent<Collider>();
        _text = GetComponent<TextMeshPro>();
    }
    protected override void OnRevealChanged(bool revealed)
    {
        _collider.enabled = revealed;
        _text.enabled = revealed;
    }

    public void ActivateItem()
    {
        onMenuItemSelected.Invoke();
    }
}
