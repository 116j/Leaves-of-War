using System.Collections;
using Hortensia.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleCard
{
    private static readonly Color Paper = new Color(0.937f, 0.902f, 0.824f, 1f);
    private static readonly Color Ink = new Color(0.118f, 0.165f, 0.227f, 1f);
    private static readonly Color Rule = new Color(0.45f, 0.58f, 0.72f, 0.22f);
    private static readonly Color Margin = new Color(0.72f, 0.3f, 0.26f, 0.35f);

    private sealed class Card
    {
        public string key;
        public string font;
        public string text;
        public InkOrnament.Kind ornament;
        public Color stampInk;
        public float wear;
        public float tilt;
        public string top, bottom, middle, middleSmall;
    }

    private static readonly Card[] Cards =
    {
        new Card { key = "milano", font = "BodoniModa", text = "MILANO — 1914–1915", ornament = InkOrnament.Kind.LibertyLine },
        new Card { key = "parigi", font = "PlayfairDisplay", text = "PARIGI — 5 RUE DES CARMES — 1912–1914", ornament = InkOrnament.Kind.HotelStamp,
            stampInk = new Color(0.32f, 0.25f, 0.55f, 0.78f), wear = 0.25f, tilt = -11f, top = "HÔTEL DES CARMES", bottom = "PARIS", middle = "5, RUE DES CARMES", middleSmall = "QUARTIER LATIN" },
        new Card { key = "carso", font = "StardosStencil", text = "IL CARSO — CIMA QUATTRO — 23 DICEMBRE 1915", ornament = InkOrnament.Kind.MilitaryPost,
            stampInk = new Color(0.36f, 0.22f, 0.42f, 0.72f), wear = 0.45f, tilt = 9f, top = "POSTA MILITARE", bottom = "ZONA DI GUERRA", middle = "23 DIC 1915", middleSmall = "" },
        new Card { key = "san paolo", font = "Limelight", text = "SAN PAOLO DEL BRASILE — 1937–1942", ornament = InkOrnament.Kind.DecoLine },
        new Card { key = "roma", font = "SpecialElite", text = "ROMA — 1943–1944", ornament = InkOrnament.Kind.TypedDate,
            stampInk = new Color(0.17f, 0.24f, 0.42f, 0.8f), wear = 0.3f, tilt = -4f, top = "R O M A", middle = "1943 — 1944" },
        new Card { key = "courton", font = "OldStandard", text = "BOSCO DI COURTON — LUGLIO 1918", ornament = InkOrnament.Kind.FieldPost,
            stampInk = new Color(0.12f, 0.12f, 0.14f, 0.42f), wear = 0.85f, tilt = 14f, top = "TRÉSOR ET POSTES", bottom = "", middle = "JUIL. 1918", middleSmall = "SECTEUR POSTAL" },
    };

    public static void Show(MonoBehaviour host, string title, TMP_FontAsset font, float hold = 2.8f)
    {
        if (host == null)
            return;
        Card card = Find(SceneManager.GetActiveScene().name) ?? (string.IsNullOrWhiteSpace(title) ? null : Find(title));
        string text = card != null ? card.text : title;
        if (!string.IsNullOrWhiteSpace(text))
            host.StartCoroutine(Run(host.transform, text, card, FontFor(card), hold));
    }

    private static readonly System.Collections.Generic.Dictionary<string, TMP_FontAsset> Fonts = new System.Collections.Generic.Dictionary<string, TMP_FontAsset>();

    public static TMP_FontAsset PeriodFont(string level) => FontFor(Find(level));

    private static TMP_FontAsset FontFor(Card card)
    {
        if (card == null || string.IsNullOrEmpty(card.font))
            return MenuLook.Current.PanelFont;
        if (!Fonts.TryGetValue(card.font, out TMP_FontAsset asset) || asset == null)
        {
            Font file = Resources.Load<Font>("Fonts/TitleCards/" + card.font);
            asset = file != null ? MainMenuController.ResolveFont(file, null, "Title Card " + card.font) : null;
            Fonts[card.font] = asset;
        }
        return asset != null ? asset : MenuLook.Current.PanelFont;
    }

    private static Card Find(string name)
    {
        string lower = (name ?? "").ToLowerInvariant().Replace("ã", "a");
        foreach (Card card in Cards)
            if (lower.Contains(card.key) || lower.Contains(card.key.Replace(" ", "")))
                return card;
        return null;
    }

    private static IEnumerator Run(Transform owner, string title, Card card, TMP_FontAsset font, float hold)
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

        bool stamp = card != null && card.top != null;
        Vector2 size = new Vector2(1180f, 400f);
        RectTransform shadow = Box(canvasObject.transform, "Shadow", size + new Vector2(14f, 14f), new Vector2(8f, 162f), new Color(0f, 0f, 0f, 0.28f));
        shadow.localRotation = Quaternion.Euler(0f, 0f, -0.6f);
        RectTransform page = Box(canvasObject.transform, "Page", size, new Vector2(0f, 170f), Paper);
        page.localRotation = Quaternion.Euler(0f, 0f, -0.6f);
        for (int i = 0; i < 7; i++)
            Box(page, "Rule", new Vector2(size.x - 40f, 2f), new Vector2(0f, -size.y * 0.5f + 52f + i * 50f), Rule);
        Box(page, "Margin", new Vector2(2f, size.y), new Vector2(-size.x * 0.5f + 96f, 0f), Margin);

        string[] parts = title.Split(new[] { " — " }, System.StringSplitOptions.RemoveEmptyEntries);
        string first = parts.Length > 0 ? parts[0] : title;
        string rest = parts.Length > 1 ? string.Join(" — ", parts, 1, parts.Length - 1) : "";
        float textShift = stamp ? -120f : 0f;
        float textWidth = stamp ? size.x - 420f : size.x - 160f;
        Text(page, "City", font, first, 84f, new Vector2(textShift, rest.Length > 0 ? 62f : 20f), new Vector2(textWidth, 130f), Ink);
        if (rest.Length > 0)
            Text(page, "Place And Date", font, rest, 46f, new Vector2(textShift, -36f), new Vector2(textWidth, 80f), Ink);

        if (card != null && card.ornament != InkOrnament.Kind.None)
        {
            if (stamp)
                Stamp(page, card, font);
            else
                Ornament(page, card.ornament, new Vector2(0f, rest.Length > 0 ? -120f : -80f), new Vector2(820f, 96f), Ink, 0f, 0f, 3);
        }

        yield return Fade(group, 0f, 1f, 0.8f);
        yield return new WaitForSecondsRealtime(hold);
        yield return Fade(group, 1f, 0f, 1f);
        Object.Destroy(canvasObject);
    }

    private static void Stamp(RectTransform page, Card card, TMP_FontAsset font)
    {
        bool round = card.ornament != InkOrnament.Kind.TypedDate;
        Vector2 size = card.ornament == InkOrnament.Kind.HotelStamp ? new Vector2(330f, 220f)
            : card.ornament == InkOrnament.Kind.TypedDate ? new Vector2(320f, 120f) : new Vector2(230f, 230f);
        Vector2 position = new Vector2(page.sizeDelta.x * 0.5f - 210f, -30f);
        RectTransform holder = Ornament(page, card.ornament, position, size, card.stampInk, card.wear, card.tilt, card.key.Length);
        var rng = new System.Random(card.key.Length * 31);
        if (round)
        {
            float rx = size.x * 0.5f - 25f, ry = size.y * 0.5f - 25f;
            if (!string.IsNullOrEmpty(card.top))
                ArcText(holder, card.top, font, rx, ry, 90f, card.ornament == InkOrnament.Kind.HotelStamp ? 130f : 150f, true, 22f, card, rng);
            if (!string.IsNullOrEmpty(card.bottom))
                ArcText(holder, card.bottom, font, rx, ry, 270f, card.ornament == InkOrnament.Kind.HotelStamp ? 70f : 150f, false, 22f, card, rng);
            if (!string.IsNullOrEmpty(card.middle))
                Worn(Text(holder, "Middle", font, card.middle, card.ornament == InkOrnament.Kind.HotelStamp ? 22f : 28f, new Vector2(0f, card.ornament == InkOrnament.Kind.FieldPost ? -36f : 0f), new Vector2(size.x - 60f, 40f), card.stampInk), card, rng);
            if (!string.IsNullOrEmpty(card.middleSmall))
                Worn(Text(holder, "Middle Small", font, card.middleSmall, 15f, new Vector2(0f, card.ornament == InkOrnament.Kind.FieldPost ? 6f : -26f), new Vector2(size.x - 70f, 26f), card.stampInk), card, rng);
            return;
        }
        TMP_Text top = Text(holder, "Top", font, card.top, 18f, new Vector2(0f, 26f), new Vector2(size.x - 40f, 28f), card.stampInk);
        top.characterSpacing = 18f;
        Worn(top, card, rng);
        TMP_Text date = Text(holder, "Typed Date", font, card.middle, 36f, new Vector2(0f, -16f), new Vector2(size.x - 40f, 50f), card.stampInk);
        date.fontStyle = FontStyles.Bold;
        date.characterSpacing = 6f;
        Worn(date, card, rng);
    }

    private static void ArcText(RectTransform holder, string text, TMP_FontAsset font, float rx, float ry, float centre, float span, bool top, float size, Card card, System.Random rng)
    {
        int count = text.Length;
        for (int i = 0; i < count; i++)
        {
            if (text[i] == ' ')
                continue;
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = top ? centre + span * 0.5f - t * span : centre - span * 0.5f + t * span;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(rad) * rx, Mathf.Sin(rad) * ry);
            TMP_Text letter = Text(holder, "Letter", font, text[i].ToString(), size, position, new Vector2(size * 1.4f, size * 1.4f), card.stampInk);
            letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, top ? angle - 90f : angle + 90f);
            letter.fontStyle = FontStyles.Bold;
            Worn(letter, card, rng);
        }
    }

    private static void Worn(TMP_Text text, Card card, System.Random rng)
    {
        text.alpha = Mathf.Max(0.7f, card.stampInk.a) * Mathf.Lerp(1f, 0.75f + 0.25f * (float)rng.NextDouble(), card.wear);
    }

    private static RectTransform Ornament(Transform parent, InkOrnament.Kind kind, Vector2 position, Vector2 size, Color ink, float wear, float tilt, int seed)
    {
        var go = new GameObject("Ornament", typeof(RectTransform), typeof(InkOrnament));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        InkOrnament ornament = go.GetComponent<InkOrnament>();
        ornament.color = ink;
        ornament.Seed = seed;
        ornament.Wear = wear;
        ornament.Shape = kind;
        ornament.raycastTarget = false;
        return rect;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, string value, float size, Vector2 position, Vector2 box, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = box;
        rect.anchoredPosition = position;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.4f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color;
        text.raycastTarget = false;
        return text;
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
