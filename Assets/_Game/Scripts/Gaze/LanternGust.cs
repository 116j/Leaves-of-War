using UnityEngine;

public class LanternGust : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float _seconds = 1.4f;

    Light _light;
    float _base;
    float _until;

    void Awake()
    {
        _light = GetComponentInChildren<Light>(true);
        if (_light != null)
            _base = _light.intensity;
    }

    void OnEnable() => TranscribePoem.Gusted += OnGust;

    void OnDisable() => TranscribePoem.Gusted -= OnGust;

    void OnGust(int poemId) => _until = Time.time + _seconds;

    void Update()
    {
        if (_light == null)
            return;
        if (Time.time < _until)
            _light.intensity = _base * Mathf.Lerp(0.1f, 1f, Mathf.PerlinNoise(Time.time * 18f, 0.3f));
        else
            _light.intensity = Mathf.MoveTowards(_light.intensity, _base, _base * 2f * Time.deltaTime);
    }
}
