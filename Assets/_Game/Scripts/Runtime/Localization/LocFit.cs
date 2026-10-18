using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class LocFit
{
    private sealed class Entry
    {
        public float baseSize;
        public float applied;
        public string english;
        public string shown;
    }

    private static readonly Dictionary<TMP_Text, Entry> Entries = new Dictionary<TMP_Text, Entry>();
    private static int cleanCountdown = 200;

    public static void Apply(TMP_Text text, string english, string shown)
    {
        if (text == null)
            return;
        if (--cleanCountdown <= 0)
        {
            cleanCountdown = 200;
            var dead = new List<TMP_Text>();
            foreach (TMP_Text key in Entries.Keys)
                if (key == null)
                    dead.Add(key);
            foreach (TMP_Text key in dead)
                Entries.Remove(key);
        }
        if (text.enableAutoSizing)
            return;
        bool known = Entries.TryGetValue(text, out Entry entry);
        if (!known)
            Entries[text] = entry = new Entry { baseSize = text.fontSize, applied = text.fontSize };
        else if (!Mathf.Approximately(text.fontSize, entry.applied))
            entry.baseSize = text.fontSize;
        else if (entry.english == english && entry.shown == shown)
            return;

        entry.english = english;
        entry.shown = shown;
        float size = entry.baseSize;
        if (!string.IsNullOrEmpty(english) && !string.IsNullOrEmpty(shown) && english != shown)
            size = entry.baseSize * Factor(text, english, shown, entry.baseSize) * Loc.Size(english);
        if (!Mathf.Approximately(text.fontSize, size))
            text.fontSize = size;
        entry.applied = text.fontSize;
    }

    public static bool Tracks(TMP_Text text) => text != null && Entries.ContainsKey(text);

    public static void Reset(TMP_Text text)
    {
        if (text == null || !Entries.TryGetValue(text, out Entry entry))
            return;
        if (!Mathf.Approximately(text.fontSize, entry.applied))
            entry.baseSize = text.fontSize;
        text.fontSize = entry.baseSize;
        entry.applied = entry.baseSize;
        entry.english = entry.shown = null;
    }

    private static float Factor(TMP_Text text, string english, string shown, float baseSize)
    {
        float keep = text.fontSize;
        text.fontSize = baseSize;
        float width = text.rectTransform.rect.width;
        bool wrap = text.textWrappingMode != TextWrappingModes.NoWrap && width > 1f;
        Vector2 en = Measure(text, english, wrap, width);
        Vector2 it = Measure(text, shown, wrap, width);
        float factor = 1f;
        if (!wrap)
        {
            float room = Mathf.Max(en.x, width > 1f && text.overflowMode != TextOverflowModes.Overflow ? Mathf.Min(width, en.x) : en.x);
            if (it.x > room && it.x > 0f)
                factor = room / it.x;
        }
        else if (it.y > en.y && it.y > 0f)
        {
            factor = Mathf.Sqrt(en.y / it.y);
            for (int i = 0; i < 12; i++)
            {
                text.fontSize = baseSize * factor;
                if (Measure(text, shown, true, width).y <= en.y + 0.5f)
                    break;
                factor *= 0.96f;
            }
        }
        text.fontSize = keep;
        return Mathf.Clamp(factor, 0.5f, 1f);
    }

    private static Vector2 Measure(TMP_Text text, string value, bool wrap, float width) =>
        wrap ? text.GetPreferredValues(value, width, 0f) : text.GetPreferredValues(value);
}
