using System.Collections;
using Hortensia.Runtime;
using UnityEngine;

public sealed class RaycastInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "USE SWITCH";
    [SerializeField] private Light controlledLight;
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private Color inactiveColor = new Color(0.18f, 0.03f, 0.025f, 1f);
    [SerializeField] private Color activeColor = new Color(0.75f, 0.08f, 0.035f, 1f);

    private bool active;
    private Vector3 initialLocalPosition;
    private Coroutine animationRoutine;

    public string Prompt => prompt;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        ApplyState();
    }

    public void Interact()
    {
        active = !active;
        ApplyState();
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(PressAnimation());
    }

    private void ApplyState()
    {
        if (controlledLight != null)
            controlledLight.enabled = active;

        if (indicatorRenderer != null)
        {
            Material material = indicatorRenderer.material;
            Color color = active ? activeColor : inactiveColor;
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * (active ? 2.5f : 0.15f));
        }
    }

    private IEnumerator PressAnimation()
    {
        const float duration = 0.14f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float press = Mathf.Sin(t * Mathf.PI) * 0.035f;
            transform.localPosition = initialLocalPosition - transform.forward * press;
            yield return null;
        }
        transform.localPosition = initialLocalPosition;
        animationRoutine = null;
    }
}
