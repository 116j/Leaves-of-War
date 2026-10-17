using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    public static class CharacterCards
    {
        public const float CardSize = 1024f;
        private static readonly Color Paper = new Color(0.937f, 0.902f, 0.824f, 1f);
        private static readonly Color Ink = new Color(0.118f, 0.165f, 0.227f, 1f);

        public sealed class Entry
        {
            public InkPortrait.Sitter sitter;
            public string name;
            public string line;
            public string level;
        }

        public static readonly Entry[] Cast =
        {
            new Entry { sitter = InkPortrait.Sitter.Carlo, level = "milano", name = "Signor Carlo",
                line = "A landlord in Milan. He stands for the ordinary people who gave Ungaretti a roof. (fictional)" },
            new Entry { sitter = InkPortrait.Sitter.Apollinaire, level = "parigi", name = "Guillaume Apollinaire",
                line = "The French poet, Ungaretti's friend in Paris. He died of the Spanish flu in November 1918. (real)" },
            new Entry { sitter = InkPortrait.Sitter.Pietro, level = "carso", name = "Pietro",
                line = "A stretcher-bearer on the Carso. He stands for the men who carried the wounded through the trenches. (fictional)" },
            new Entry { sitter = InkPortrait.Sitter.Rui, level = "san paolo", name = "Rui",
                line = "A colleague at the University of São Paulo. He stands for the friends who stayed close in grief. (fictional)" },
            new Entry { sitter = InkPortrait.Sitter.Leone, level = "roma", name = "Leone",
                line = "A student in occupied Rome. He stands for the young who protected their teachers. (fictional)" },
            new Entry { sitter = InkPortrait.Sitter.Lucien, level = "courton", name = "Lucien",
                line = "An old farmer near the Bois de Courton. He stands for the civilians who lived beside the war. (fictional)" },
        };

        public static RectTransform Build(Transform parent, int index)
        {
            bool final = index >= Cast.Length;
            Entry entry = final ? null : Cast[index];
            RectTransform card = Box(parent, final ? "Final Card" : entry.name + " Card", new Vector2(CardSize, CardSize), Vector2.zero, Paper);
            Frame(card, 36f, 3f);
            Frame(card, 50f, 1.2f);

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(InkPortrait));
            var portrait = (RectTransform)portraitObject.transform;
            portrait.SetParent(card, false);
            portrait.sizeDelta = final ? new Vector2(420f, 420f) : new Vector2(600f, 600f);
            portrait.anchoredPosition = final ? new Vector2(0f, 200f) : new Vector2(0f, 150f);
            InkPortrait ink = portraitObject.GetComponent<InkPortrait>();
            ink.color = Ink;
            ink.Seed = 11 + index * 17;
            ink.Person = final ? InkPortrait.Sitter.Ungaretti : entry.sitter;
            ink.raycastTarget = false;

            if (final)
            {
                TMP_FontAsset font = TitleCard.PeriodFont("milano");
                Text(card, "Name", font, "Giuseppe Ungaretti", 84f, new Vector2(0f, -110f), new Vector2(860f, 110f), Ink);
                Text(card, "Dates", font, "Alessandria d'Egitto, 8 febbraio 1888\nMilano, 2 giugno 1970", 40f, new Vector2(0f, -240f), new Vector2(860f, 120f), Ink);
                Ornament(card, InkOrnament.Kind.LibertyLine, new Vector2(0f, -350f), new Vector2(620f, 80f));
                return card;
            }

            Text(card, "Name", TitleCard.PeriodFont(entry.level), entry.name, 70f, new Vector2(0f, -205f), new Vector2(860f, 100f), Ink);
            TMP_Text line = Text(card, "Line", MenuLook.Current.PanelFont, entry.line, 36f, new Vector2(0f, -330f), new Vector2(800f, 150f), Ink);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.fontStyle = FontStyles.Italic;
            return card;
        }

        public static IEnumerator Play(System.Action done, float hold = 6f)
        {
            var canvasObject = new GameObject("Character Cards", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            RectTransform black = Box(canvasObject.transform, "Black", Vector2.zero, Vector2.zero, Color.black);
            black.anchorMin = Vector2.zero;
            black.anchorMax = Vector2.one;
            black.sizeDelta = Vector2.zero;
            CanvasGroup blackGroup = black.gameObject.AddComponent<CanvasGroup>();
            yield return Fade(blackGroup, 0f, 1f, 1.5f);

            for (int i = 0; i <= Cast.Length; i++)
            {
                RectTransform card = Build(canvasObject.transform, i);
                card.localScale = Vector3.one * 0.9f;
                card.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -1.2f : 1.1f);
                CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
                yield return Fade(group, 0f, 1f, 1f);
                float wait = i == Cast.Length ? hold * 1.4f : hold;
                for (float t = 0f; t < wait && !Skip(); t += Time.unscaledDeltaTime)
                    yield return null;
                yield return null;
                yield return Fade(group, 1f, 0f, 0.9f);
                Object.Destroy(card.gameObject);
            }
            done?.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            Object.Destroy(canvasObject);
        }

        private static bool Skip() =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
            Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) ||
            Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        private static void Ornament(Transform parent, InkOrnament.Kind kind, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Ornament", typeof(RectTransform), typeof(InkOrnament));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            InkOrnament ornament = go.GetComponent<InkOrnament>();
            ornament.color = Ink;
            ornament.Shape = kind;
            ornament.raycastTarget = false;
        }

        private static void Frame(RectTransform card, float inset, float width)
        {
            float half = CardSize * 0.5f - inset;
            Box(card, "Frame", new Vector2(half * 2f, width), new Vector2(0f, half), Ink);
            Box(card, "Frame", new Vector2(half * 2f, width), new Vector2(0f, -half), Ink);
            Box(card, "Frame", new Vector2(width, half * 2f), new Vector2(half, 0f), Ink);
            Box(card, "Frame", new Vector2(width, half * 2f), new Vector2(-half, 0f), Ink);
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
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
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
