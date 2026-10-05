using System;
using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    [SerializeField]
    GameObject _settingCanvas;

    Action _onClose;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    public void Open(Action onCloseCallback)
    {
        _settingCanvas.SetActive(true);
        _onClose = onCloseCallback;
    }

    public void Close()
    {
        _settingCanvas.SetActive(false);
        _onClose.Invoke();
        _onClose = null;
    }
}
