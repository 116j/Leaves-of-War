using ChocDino.UIFX;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TextDissolve : MonoBehaviour
{
    [SerializeField] private List<TextDissolveContainer> _textDissolveList;
    private Dictionary<int, bool> diisolvePoemInfo;

    private void Start()
    {
        PlayDissolve();
    }

    public void PlayDissolve()
    {
        foreach (TextDissolveContainer container in _textDissolveList)
        {
            if (container != null)
            {
                StartCoroutine(AnimateDissolve(container));
            }
        }
    }

    private IEnumerator AnimateDissolve(TextDissolveContainer container)
    {
        DissolveFilter dissolveFilter = container.DissolveFilter;

        if (dissolveFilter == null)
            yield break;

       
        dissolveFilter.Strength = 1f;

       
        yield return new WaitForSeconds(container.DissolveDelay);

        
        float elapsed = 0f;
        float duration = container.DissolveDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            dissolveFilter.Strength = Mathf.Lerp(
                1f,
                0f,
                Mathf.SmoothStep(0f, 1f, t)
            );

            yield return null;
        }

        dissolveFilter.Strength = 0f;
    }
}

[System.Serializable]
public class TextDissolveContainer
{
    [SerializeField] private string _textName;
    [SerializeField] private DissolveFilter _dissolveFilter;

    [Range(0f, 1f)]
    [SerializeField] private float _dissolveStrength = 1f;

    [SerializeField] private float _dissloveDelay = 1f;
    [SerializeField] private float _dissolveDuration = 1f;

    public string TextName => _textName;
    public DissolveFilter DissolveFilter => _dissolveFilter;
    public float DissolveStrength => _dissolveStrength;
    public float DissolveDelay => _dissloveDelay;
    public float DissolveDuration => Mathf.Max(0.01f, _dissolveDuration);
}