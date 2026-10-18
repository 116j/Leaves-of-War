using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class ScreenPrompt
{
    public static TextMeshProUGUI Create(Transform owner, string name, float size, float height, bool top = false)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(owner, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var textObject = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.anchoredPosition = new Vector2(0f, top ? -height : height);
        rect.sizeDelta = new Vector2(0f, size * 2f);

        var prompt = textObject.GetComponent<TextMeshProUGUI>();
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontSize = size;
        prompt.raycastTarget = false;
        Hortensia.Runtime.ScreenTextStyle.Attach(prompt, size);
        textObject.SetActive(false);
        return prompt;
    }

    public static void Show(TextMeshProUGUI prompt, bool show, string text, string english = null)
    {
        if (prompt == null)
            return;
        if (show)
        {
            string shown = Loc.T(text);
            prompt.text = shown;
            LocFit.Apply(prompt, english ?? text, shown);
        }
        if (prompt.gameObject.activeSelf != show)
            prompt.gameObject.SetActive(show);
    }

    public static string KeyName(InputAction action, string fallback)
    {
        string name = action != null ? action.GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions) : fallback;
        return Loc.T(string.IsNullOrWhiteSpace(name) ? fallback : name.ToUpperInvariant());
    }
}
