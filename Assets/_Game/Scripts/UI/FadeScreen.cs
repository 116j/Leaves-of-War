using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeScreen : MonoBehaviour
{
    public static FadeScreen Instance { get; private set; }
    [SerializeField]
    float _fadeTime = 0.6f;

    Image _screenImage;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (_screenImage == null)
            _screenImage = GetComponent<Image>();
    }

    public IEnumerator DarkenScreen(float toAlpha, Action OnComplete)
    {
        if(_screenImage == null)
            _screenImage = GetComponent<Image>();
        _screenImage.enabled = true;
        float elapsedTime = 0f;
        Color startColor = _screenImage.color;
        Color endColor = _screenImage.color;
        endColor.a = toAlpha;
        while (elapsedTime < _fadeTime)
        {
            elapsedTime += Time.deltaTime;
            _screenImage.color = Color.Lerp(startColor, endColor, elapsedTime / _fadeTime);
            yield return null;
        }

        _screenImage.color = endColor;
        if (toAlpha == 0f)
        {
            _screenImage.enabled = false;
        }
        OnComplete?.Invoke();
    }
}
