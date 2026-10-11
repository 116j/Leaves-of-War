using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TitleCard
{
    public static void Show(MonoBehaviour host, string title, TMP_FontAsset font, float hold = 2.8f)
    {
        if (host != null && !string.IsNullOrWhiteSpace(title))
            host.StartCoroutine(Run(host.transform, title, font, hold));
    }

    private static IEnumerator Run(Transform owner, string title, TMP_FontAsset font, float hold)
    {
        var canvasObject = new GameObject("Title Card", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(owner, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var page = new GameObject("Page", typeof(RectTransform), typeof(Image));
        page.transform.SetParent(canvasObject.transform, false);
        RectTransform pageRect = (RectTransform)page.transform;
        pageRect.sizeDelta = new Vector2(900f, 260f);
        pageRect.anchoredPosition = new Vector2(0f, 120f);
        Image paper = page.GetComponent<Image>();
        paper.color = new Color(0.93f, 0.87f, 0.74f, 0.95f);
        paper.raycastTarget = false;

        var textObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(page.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 20f);
        textRect.offsetMax = new Vector2(-40f, -20f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = title;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 64f;
        text.color = new Color(0.12f, 0.1f, 0.18f);
        text.raycastTarget = false;

        yield return Fade(group, 0f, 1f, 0.8f);
        yield return new WaitForSecondsRealtime(hold);
        yield return Fade(group, 1f, 0f, 1f);
        Object.Destroy(canvasObject);
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
        {
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        group.alpha = to;
    }
}
