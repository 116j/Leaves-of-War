using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    public sealed class GameMenuEntry : Selectable, IPointerClickHandler, ISubmitHandler
    {
        private TMP_Text label;
        private RectTransform visual;
        private Graphic background;
        private CrumplePaper paper;
        private System.Action action;
        private Coroutine paperRoutine;
        private bool lit;

        public TMP_Text Label => label;

        public static GameMenuEntry Create(Transform parent, string text, Vector2 position, System.Action onChosen, float sizeMultiplier = 1f)
        {
            MenuLook look = MenuLook.Current;
            var go = new GameObject(text, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = look.entrySize * sizeMultiplier;
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = Color.clear;

            var visualObject = new GameObject("Visual", typeof(RectTransform));
            var visualRect = (RectTransform)visualObject.transform;
            visualRect.SetParent(rect, false);
            Stretch(visualRect);

            Graphic back = null;
            if (look.useEntryBackground)
            {
                var backObject = new GameObject("Background", typeof(RectTransform));
                var backRect = (RectTransform)backObject.transform;
                backRect.SetParent(visualRect, false);
                Stretch(backRect);
                if (look.entryBackgroundOnHover)
                {
                    CrumplePaper crumple = backObject.AddComponent<CrumplePaper>();
                    crumple.Sprite = look.entryBackground;
                    crumple.Seed = text.Length * 7.31f + 3.7f;
                    back = crumple;
                }
                else
                {
                    Image image = backObject.AddComponent<Image>();
                    image.sprite = look.entryBackground;
                    image.type = look.entryBackground != null && look.entryBackground.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                    back = image;
                }
                back.color = look.entryBackgroundColor;
                back.raycastTarget = false;
            }
            if (look.entryBorderWidth > 0f)
                Border(visualRect, look.entryBorderWidth, look.entryBorderColor);

            var textObject = new GameObject("Word", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(visualRect, false);
            Stretch(textRect);
            TextMeshProUGUI word = textObject.GetComponent<TextMeshProUGUI>();
            word.text = text;
            word.fontSize = look.menuWordSize * sizeMultiplier;
            word.alignment = TextAlignmentOptions.Center;
            word.textWrappingMode = TextWrappingModes.NoWrap;
            word.raycastTarget = false;
            look.StyleEntryText(word, false);

            GameMenuEntry entry = go.AddComponent<GameMenuEntry>();
            entry.label = word;
            entry.visual = visualRect;
            entry.background = back;
            entry.paper = back as CrumplePaper;
            entry.action = onChosen;
            entry.transition = Transition.None;
            entry.HideBackground();
            return entry;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Choose();
        }

        public void OnSubmit(BaseEventData eventData) => Choose();

        private void Choose()
        {
            if (!IsInteractable())
                return;
            MenuLook.Current.PlayEntrySound(2);
            action?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            ApplyLook(state == SelectionState.Highlighted || state == SelectionState.Selected || state == SelectionState.Pressed);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            lit = false;
            paperRoutine = null;
            if (label != null)
                ApplyColours();
            HideBackground();
        }

        private void ApplyLook(bool active)
        {
            if (label == null)
                return;
            MenuLook look = MenuLook.Current;
            bool nowLit = active && IsInteractable();
            if (nowLit != lit)
            {
                lit = nowLit;
                look.PlayEntrySound(lit ? 0 : 1);
                if (paper != null)
                {
                    if (paperRoutine != null)
                        StopCoroutine(paperRoutine);
                    paperRoutine = isActiveAndEnabled ? StartCoroutine(AnimatePaper(lit)) : null;
                }
            }
            ApplyColours();
        }

        private void ApplyColours()
        {
            MenuLook look = MenuLook.Current;
            label.color = lit ? look.entryHighlightColor : look.inkColor;
            if (background != null)
                background.color = lit ? look.entryBackgroundHighlightColor : look.entryBackgroundColor;
            visual.localScale = Vector3.one * (lit ? look.entryHighlightScale : 1f);
            label.alpha = IsInteractable() ? 1f : 0.4f;
        }

        private void HideBackground()
        {
            if (paper == null)
                return;
            paper.Crumple = 1f;
            paper.gameObject.SetActive(false);
        }

        private IEnumerator AnimatePaper(bool open)
        {
            MenuLook look = MenuLook.Current;
            paper.gameObject.SetActive(true);
            float from = paper.Crumple;
            float to = open ? 0f : 1f;
            if (Mathf.Abs(from - to) > 0.999f)
                paper.NewCreases();
            float duration = Mathf.Max(0.01f, open ? look.entryBackgroundUnfoldTime : look.entryBackgroundCrumpleTime);
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
                float eased = open ? 1f - (1f - t) * (1f - t) * (1f - t) : t * t * (3f - 2f * t);
                paper.Crumple = Mathf.Lerp(from, to, eased);
                yield return null;
            }
            if (!open)
                paper.gameObject.SetActive(false);
            paperRoutine = null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Border(Transform parent, float width, Color color)
        {
            Line(parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, width), color);
            Line(parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, width), color);
            Line(parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(width, 0f), color);
            Line(parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(width, 0f), color);
        }

        private static void Line(Transform parent, Vector2 min, Vector2 max, Vector2 size, Color color)
        {
            var go = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
