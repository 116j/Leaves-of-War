using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime
{
    public static class GameEnding
    {
        public const string FinishedKey = "LeavesOfWar.Finished";
        private const string CreditsPath = "MainMenu/Credits";
        private const string LeafPath = "MainMenu/GoldenLeaf";
        private static readonly Color Paper = new Color(0.937f, 0.902f, 0.824f, 1f);
        private static readonly Color Ink = new Color(0.118f, 0.165f, 0.227f, 1f);
        private static readonly Color Rule = new Color(0.45f, 0.58f, 0.72f, 0.22f);
        private static readonly Color Margin = new Color(0.72f, 0.3f, 0.26f, 0.35f);
        private static readonly Color CreditsInk = new Color(0.96f, 0.93f, 0.88f, 1f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color[] Autumn =
        {
            new Color(0.62f, 0.38f, 0.16f), new Color(0.78f, 0.55f, 0.22f), new Color(0.55f, 0.24f, 0.12f),
            new Color(0.85f, 0.68f, 0.3f), new Color(0.45f, 0.3f, 0.15f)
        };

        public static bool Finished => PlayerPrefs.GetInt(FinishedKey, 0) == 1;

        private sealed class FallingLeaf
        {
            public RectTransform rect;
            public float x0, y, speed, phase, spin, sway;
            public int line = -1;
        }

        public static IEnumerator Play(TMP_FontAsset poemFont, AudioClip music, float musicVolume, Action done, float pageHold = 10f)
        {
            var canvasObject = new GameObject("Ending", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30400;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform black = Box(canvasObject.transform, "Black", Vector2.zero, Vector2.zero, Color.black);
            black.anchorMin = Vector2.zero;
            black.anchorMax = Vector2.one;
            black.sizeDelta = Vector2.zero;
            CanvasGroup blackGroup = black.gameObject.AddComponent<CanvasGroup>();
            if (PauseController.Instance != null)
            {
                if (PauseController.Instance.IsPaused)
                    PauseController.Instance.Resume();
                PauseController.Instance.enabled = false;
            }
            var fading = new List<(AudioSource source, float volume)>();
            AudioManager manager = AudioManager.Instance;
            foreach (MonoBehaviour level in Object.FindObjectsByType<LevelAudio>(FindObjectsInactive.Exclude))
                level.enabled = false;
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
                if (source.isPlaying && (manager == null || !source.transform.IsChildOf(manager.transform)))
                    fading.Add((source, source.volume));
            if (music != null && manager != null)
                manager.StopMusic();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 2f)
            {
                blackGroup.alpha = t;
                foreach (var f in fading)
                    if (f.source != null)
                        f.source.volume = f.volume * (1f - t);
                yield return null;
            }
            blackGroup.alpha = 1f;
            foreach (var f in fading)
                if (f.source != null)
                    f.source.Stop();
            if (music != null && manager != null)
                manager.PlayMusic(music, true, musicVolume);

            TMP_Text hint = Hint(canvasObject.transform);
            yield return LastPage(canvasObject.transform, poemFont, pageHold, hint);
            yield return CharacterCards.Play(null);
            hint.text = "Click to continue  ·  Esc to skip the credits";
            yield return Credits(canvasObject.transform, null, hint);
            hint.text = "";

            PlayerPrefs.SetInt(FinishedKey, 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            done?.Invoke();
        }

        private static TMP_Text Hint(Transform parent)
        {
            var go = new GameObject("Click Hint", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30700;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            TMP_Text text = Text(go.transform, "Hint", MenuLook.Current.PanelFont, "", 28f, new Vector2(0f, -500f), new Vector2(1400f, 50f), CreditsInk);
            text.alpha = 0.7f;
            return text;
        }

        private static IEnumerator LastPage(Transform parent, TMP_FontAsset poemFont, float hold, TMP_Text hint)
        {
            RectTransform holder = Box(parent, "Last Page", Vector2.zero, Vector2.zero, Color.clear);
            holder.anchorMin = Vector2.zero;
            holder.anchorMax = Vector2.one;
            holder.sizeDelta = Vector2.zero;
            CanvasGroup group = holder.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Vector2 size = new Vector2(1500f, 960f);
            RectTransform shadow = Box(holder, "Shadow", size + new Vector2(16f, 16f), new Vector2(9f, -9f), new Color(0f, 0f, 0f, 0.35f));
            shadow.localRotation = Quaternion.Euler(0f, 0f, 0.5f);
            RectTransform page = Box(holder, "Page", size, Vector2.zero, Paper);
            page.localRotation = Quaternion.Euler(0f, 0f, 0.5f);
            for (int i = 0; i < 17; i++)
                Box(page, "Rule", new Vector2(size.x - 40f, 2f), new Vector2(0f, -size.y * 0.5f + 40f + i * 54f), Rule);
            Box(page, "Margin", new Vector2(2f, size.y), new Vector2(-size.x * 0.5f + 120f, 0f), Margin);

            TMP_FontAsset font = poemFont != null ? poemFont : MenuLook.Current.PanelFont;
            List<PoemInfoContainer> poems = TranscribePoem.Notebook;
            int rows = Mathf.Max(6, poems.Count);
            float rowHeight = (size.y - 120f) / rows;
            float top = size.y * 0.5f - 60f - rowHeight * 0.5f;
            for (int i = 0; i < poems.Count; i++)
            {
                PoemInfoContainer poem = poems[i];
                float y = top - i * rowHeight;
                TMP_Text title = Text(page, "Title", font, poem.poemName, 66f, new Vector2(-90f, y + 14f), new Vector2(980f, rowHeight * 0.62f), Ink);
                title.alignment = TextAlignmentOptions.Left;
                string where = string.IsNullOrWhiteSpace(poem.poemPlace) ? poem.poemDate : string.IsNullOrWhiteSpace(poem.poemDate) ? poem.poemPlace : poem.poemPlace + ", " + poem.poemDate;
                TMP_Text place = Text(page, "Place And Date", font, where, 38f, new Vector2(-90f, y - rowHeight * 0.3f), new Vector2(980f, rowHeight * 0.36f), Ink);
                place.alignment = TextAlignmentOptions.Left;
                place.alpha = 0.8f;
                TMP_Text number = Text(page, "Number", font, (i + 1).ToString(), 44f, new Vector2(-size.x * 0.5f + 70f, y + 6f), new Vector2(80f, rowHeight * 0.6f), Ink);
                number.alpha = 0.6f;
            }

            Texture2D leafTexture = Resources.Load<Texture2D>(LeafPath);
            if (leafTexture != null)
            {
                RawImage leaf = new GameObject("Golden Leaf", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                leaf.rectTransform.SetParent(page, false);
                float aspect = (float)leafTexture.height / Mathf.Max(1, leafTexture.width);
                leaf.rectTransform.sizeDelta = new Vector2(110f, 110f * aspect);
                leaf.rectTransform.anchoredPosition = new Vector2(size.x * 0.5f - 150f, -size.y * 0.5f + 120f);
                leaf.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                leaf.texture = leafTexture;
                leaf.color = Gold;
                leaf.raycastTarget = false;
            }

            yield return Fade(group, 0f, 1f, 1.5f);
            hint.text = "Click to continue";
            yield return null;
            for (float t = 0f; t < hold && !Skip(); t += Time.unscaledDeltaTime)
                yield return null;
            yield return null;
            yield return Fade(group, 1f, 0f, 1.2f);
            Object.Destroy(holder.gameObject);
        }

        private static List<List<string>> Pages(string text, TMP_FontAsset body, TMP_FontAsset header, out HashSet<string> headers)
        {
            headers = new HashSet<string>();
            var blocks = new List<List<string>>();
            var current = new List<string>();
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    if (current.Count > 0)
                        blocks.Add(current);
                    current = new List<string>();
                    continue;
                }
                current.Add(line);
            }
            if (current.Count > 0)
                blocks.Add(current);

            foreach (List<string> block in blocks)
                foreach (string line in block)
                    if (IsHeader(line))
                        headers.Add(line);

            for (int i = 1; i < blocks.Count - 1; i++)
                if (blocks[i].TrueForAll(headers.Contains))
                {
                    blocks[i + 1].InsertRange(0, blocks[i]);
                    blocks.RemoveAt(i);
                    i--;
                }

            var pages = new List<List<string>>();
            foreach (List<string> block in blocks)
            {
                var page = new List<string>();
                float height = 0f;
                foreach (string line in block)
                {
                    float h = LineHeight(line, headers.Contains(line) ? header : body, headers.Contains(line) ? 54f : 32f);
                    if (page.Count > 0 && height + h > 880f)
                    {
                        pages.Add(page);
                        page = new List<string>();
                        height = 0f;
                    }
                    page.Add(line);
                    height += h;
                }
                if (page.Count > 0)
                    pages.Add(page);
            }
            return pages;
        }

        private static bool IsHeader(string line)
        {
            bool letters = false;
            foreach (char c in line)
            {
                if (char.IsLetter(c))
                {
                    letters = true;
                    if (char.IsLower(c))
                        return false;
                }
            }
            return letters && line.Length <= 40;
        }

        private static TMP_Text measure;

        private static float LineHeight(string line, TMP_FontAsset font, float size)
        {
            if (measure == null)
            {
                var go = new GameObject("Credits Measure", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.hideFlags = HideFlags.HideAndDontSave;
                measure = go.GetComponent<TextMeshProUGUI>();
                measure.textWrappingMode = TextWrappingModes.Normal;
            }
            if (font != null)
                measure.font = font;
            measure.fontSize = size;
            return measure.GetPreferredValues(line, 1500f, 0f).y + size * 0.35f;
        }

        public static IEnumerator Credits(Transform parent, Func<bool> back = null, TMP_Text hint = null)
        {
            TextAsset asset = Resources.Load<TextAsset>(CreditsPath);
            string text = asset != null ? asset.text : "LEAVES OF WAR\n\nA game by TheCelticArtist";
            TMP_FontAsset body = MenuLook.Current.PanelFont;
            TMP_FontAsset header = MenuLook.Current.EntryFont;
            List<List<string>> pages = Pages(text, body, header, out HashSet<string> headers);

            RectTransform root = Box(parent, "Credits", Vector2.zero, Vector2.zero, Color.clear);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;
            RectTransform words = Box(root, "Names", Vector2.zero, Vector2.zero, Color.clear);
            RectTransform leafLayer = Box(root, "Leaves", Vector2.zero, Vector2.zero, Color.clear);
            Texture2D leafTexture = Resources.Load<Texture2D>(LeafPath);
            var leaves = new List<FallingLeaf>();
            for (int i = 0; i < 14; i++)
            {
                FallingLeaf early = NewLeaf(leafLayer, leafTexture, UnityEngine.Random.Range(-880f, 880f), -1);
                early.y = UnityEngine.Random.Range(-500f, 600f);
                leaves.Add(early);
            }
            float ambient = 0f;
            bool abort = false;

            void UpdateLeaves(float dt)
            {
                ambient -= dt;
                if (ambient <= 0f)
                {
                    ambient = UnityEngine.Random.Range(0.18f, 0.42f);
                    leaves.Add(NewLeaf(leafLayer, leafTexture, UnityEngine.Random.Range(-880f, 880f), -1));
                }
                for (int i = leaves.Count - 1; i >= 0; i--)
                {
                    FallingLeaf leaf = leaves[i];
                    leaf.y -= leaf.speed * dt;
                    leaf.phase += dt;
                    float x = leaf.x0 + Mathf.Sin(leaf.phase * 1.4f) * leaf.sway;
                    leaf.rect.anchoredPosition = new Vector2(x, leaf.y);
                    leaf.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(leaf.phase * 1.1f) * 40f + leaf.spin * leaf.phase);
                    if (leaf.y < -640f)
                    {
                        Object.Destroy(leaf.rect.gameObject);
                        leaves.RemoveAt(i);
                    }
                }
            }

            foreach (List<string> page in pages)
            {
                var texts = new List<TMP_Text>();
                var heights = new List<float>();
                float total = 0f;
                foreach (string line in page)
                {
                    bool isHeader = headers.Contains(line);
                    float size = isHeader ? 54f : 32f;
                    float h = LineHeight(line, isHeader ? header : body, size);
                    TMP_Text t = Text(words, isHeader ? "Header" : "Name", isHeader ? header : body, line, size, Vector2.zero, new Vector2(1500f, h), CreditsInk);
                    t.enableAutoSizing = false;
                    t.fontSize = size;
                    t.textWrappingMode = TextWrappingModes.Normal;
                    t.alpha = 0f;
                    texts.Add(t);
                    heights.Add(h);
                    total += h + (isHeader ? 18f : 0f);
                }
                float y = total * 0.5f;
                var lineY = new float[texts.Count];
                for (int i = 0; i < texts.Count; i++)
                {
                    bool isHeader = headers.Contains(page[i]);
                    lineY[i] = y - heights[i] * 0.5f;
                    texts[i].rectTransform.anchoredPosition = new Vector2(0f, lineY[i]);
                    y -= heights[i] + (isHeader ? 18f : 0f);
                }

                var revealAt = new float[texts.Count];
                for (int i = 0; i < revealAt.Length; i++)
                    revealAt[i] = -1f;
                int next = 0;
                float clock = 0f, nextSpawn = 0f, held = 0f;
                float hold = Mathf.Clamp(1.5f + texts.Count * 0.35f, 3f, 6.5f);
                while (true)
                {
                    float dt = Time.unscaledDeltaTime;
                    clock += dt;
                    if (next < texts.Count && clock >= nextSpawn)
                    {
                        float width = Mathf.Min(1300f, texts[next].GetPreferredValues(page[next], 1500f, 0f).x);
                        FallingLeaf leaf = NewLeaf(leafLayer, leafTexture, UnityEngine.Random.Range(-0.5f, 0.5f) * width, next);
                        leaves.Add(leaf);
                        next++;
                        nextSpawn = clock + 0.4f;
                    }
                    UpdateLeaves(dt);
                    foreach (FallingLeaf leaf in leaves)
                        if (leaf.line >= 0 && leaf.line < revealAt.Length && revealAt[leaf.line] < 0f && leaf.y <= lineY[leaf.line] + 10f)
                            revealAt[leaf.line] = clock;
                    bool all = true;
                    for (int i = 0; i < texts.Count; i++)
                    {
                        if (revealAt[i] < 0f)
                        {
                            all = false;
                            continue;
                        }
                        texts[i].alpha = Mathf.Clamp01((clock - revealAt[i]) / 0.7f);
                    }
                    if (back != null ? back() : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        abort = true;
                        break;
                    }
                    if (Skip())
                    {
                        if (all)
                            held = hold;
                        else
                        {
                            for (int i = 0; i < revealAt.Length; i++)
                                if (revealAt[i] < 0f)
                                    revealAt[i] = clock;
                            next = texts.Count;
                        }
                    }
                    if (all)
                    {
                        held += dt;
                        if (held >= hold)
                            break;
                    }
                    yield return null;
                }

                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
                {
                    foreach (TMP_Text line in texts)
                        line.alpha = Mathf.Min(line.alpha, 1f - t);
                    UpdateLeaves(Time.unscaledDeltaTime);
                    yield return null;
                }
                foreach (TMP_Text line in texts)
                    Object.Destroy(line.gameObject);
                if (abort)
                    break;
            }

            for (float t = 0f; !abort && t < 2.5f && leaves.Count > 0; t += Time.unscaledDeltaTime)
            {
                UpdateLeaves(Time.unscaledDeltaTime);
                yield return null;
            }
            Object.Destroy(root.gameObject);
        }

        private static FallingLeaf NewLeaf(RectTransform layer, Texture2D texture, float x, int line)
        {
            RawImage image = new GameObject("Leaf", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(layer, false);
            float size = UnityEngine.Random.Range(48f, 78f);
            float aspect = texture != null ? (float)texture.height / Mathf.Max(1, texture.width) : 0.6f;
            image.rectTransform.sizeDelta = new Vector2(size, size * aspect);
            image.texture = texture;
            Color c = Autumn[UnityEngine.Random.Range(0, Autumn.Length)];
            image.color = line < 0 ? new Color(c.r, c.g, c.b, 0.75f) : c;
            image.raycastTarget = false;
            return new FallingLeaf
            {
                rect = image.rectTransform,
                x0 = x,
                y = 600f,
                speed = UnityEngine.Random.Range(230f, 300f),
                phase = UnityEngine.Random.Range(0f, 6f),
                spin = UnityEngine.Random.Range(-25f, 25f),
                sway = UnityEngine.Random.Range(30f, 80f),
                line = line
            };
        }

        private static bool Skip() =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
            Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) ||
            Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

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
            text.fontSizeMin = size * 0.45f;
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
}
