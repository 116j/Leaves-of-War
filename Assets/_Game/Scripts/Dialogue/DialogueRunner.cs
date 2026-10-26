using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DialogueRunner : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Speed of the text appearing, letter by letter. A click / tap shows the whole piece at once.")]
        [SerializeField, Min(1f)] private float charactersPerSecond = 40f;
        [Tooltip("How many letters are fading in at the same time: higher = softer appearance.")]
        [SerializeField, Min(1f)] private float fadeLetters = 6f;
        [Tooltip("Seconds the subtitle box takes to fade in and out.")]
        [SerializeField, Min(0f)] private float boxFadeSeconds = 0.25f;
        [Tooltip("Minimum time on screen for a piece without voice.")]
        [SerializeField, Min(0f)] private float silentPieceSeconds = 0.8f;
        [Tooltip("Stop the player moving and looking around during the dialogue.")]
        [SerializeField] private bool lockPlayer = true;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip(".ttf / .otf file. Wins over Font; Font's material look is copied onto it.")]
        [SerializeField] private Font fontFile;
        [Tooltip("Always bold, whatever the player picks in the options.")]
        [SerializeField] private bool bold = false;

        [Header("Text")]
        [Tooltip("Colours, thickness, outline, size and background are set by the player in Options > Subtitle Style.")]
        [SerializeField, Min(8f)] private float textSize = 34f;
        [SerializeField, Min(8f)] private float speakerSize = 28f;

        [Header("Box")]
        [Tooltip("Size of the subtitle box on a 1920x1080 screen.")]
        [SerializeField] private Vector2 boxSize = new Vector2(1500f, 210f);
        [Tooltip("Distance of the box from the bottom of the screen.")]
        [SerializeField] private float bottomMargin = 60f;
        [SerializeField] private bool useBackground = true;
        [Tooltip("Sprite (9-sliced if it has borders). Empty = plain colour. The colour comes from the options.")]
        [SerializeField] private Sprite background;
        [SerializeField] private Vector2 padding = new Vector2(40f, 22f);
        [SerializeField, Min(0f)] private float borderWidth = 0f;
        [SerializeField] private Color borderColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);

        [Header("Continue Indicator")]
        [Header("Portraits")]
        [Tooltip("Small ink portraits above the subtitle box: Ungaretti on the left, whoever he talks to on the right.")]
        [SerializeField] private bool showPortraits = true;
        [SerializeField, Min(40f)] private float portraitSize = 140f;
        [SerializeField, Range(0f, 1f)] private float silentPortraitAlpha = 0.35f;

        [SerializeField] private string continueSymbol = "▼";
        [SerializeField] private Color continueColor = new Color(0.96f, 0.93f, 0.88f, 0.9f);
        [SerializeField, Min(0f)] private float continueBlinkSpeed = 3f;

        public static DialogueRunner Instance { get; private set; }
        public static bool IsPlaying => Instance != null && Instance.playing;
        public static bool LocksPlayer => IsPlaying && Instance.lockPlayer;
        public static TMP_FontAsset ActiveFont => Instance != null ? Instance.resolvedFont : null;
        public static bool ActiveBold => Instance != null && Instance.bold;
        public static float ActiveTextSize => Instance != null ? Instance.textSize : 34f;
        public static float ActiveSpeakerSize => Instance != null ? Instance.speakerSize : 28f;
        public static Vector2 ActiveBoxSize => Instance != null ? Instance.boxSize : new Vector2(1500f, 210f);

        private GameObject root;
        private TMP_FontAsset resolvedFont;
        private float textOpacity = 1f;
        private CanvasGroup rootGroup;
        private TMP_Text speakerText;
        private TMP_Text bodyText;
        private TMP_Text continueText;
        private RectTransform boxRect;
        private RectTransform leftPortrait, rightPortrait;
        private InkPortrait rightInk;
        private CanvasGroup leftGroup, rightGroup;
        private bool rightUsed;
        private Image backgroundImage;
        private readonly List<Image> borderLines = new List<Image>();
        private AudioSource voice2D;
        private bool playing;
        private TMP_Text skipHint;
        private string pieceText = string.Empty;
        private readonly StringBuilder revealBuilder = new StringBuilder(256);

        public static DialogueRunner GetOrCreate()
        {
            if (Instance != null)
                return Instance;
            return new GameObject("Dialogue Runner").AddComponent<DialogueRunner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            voice2D = gameObject.AddComponent<AudioSource>();
            voice2D.playOnAwake = false;
            voice2D.spatialBlend = 0f;
            AudioManager.Route(voice2D, AudioBus.Voice);
            BuildUi();
            GameSettings.Applied += ApplySubtitleSettings;
        }

        private void OnDestroy()
        {
            GameSettings.Applied -= ApplySubtitleSettings;
            if (Instance == this)
                Instance = null;
        }

        public bool Play(IReadOnlyList<DialogueLine> lines, Action onFinished = null)
        {
            if (playing || lines == null || lines.Count == 0)
                return false;
            StartCoroutine(Run(lines, onFinished));
            return true;
        }

        private IEnumerator Run(IReadOnlyList<DialogueLine> lines, Action onFinished)
        {
            playing = true;

            CursorLockMode previousLock = Cursor.lockState;
            bool previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var speakers = new Dictionary<string, DialogueSpeaker>(StringComparer.OrdinalIgnoreCase);
            foreach (DialogueSpeaker speaker in FindObjectsByType<DialogueSpeaker>(FindObjectsInactive.Exclude))
            {
                if (!string.IsNullOrWhiteSpace(speaker.SpeakerId))
                    speakers[speaker.SpeakerId.Trim()] = speaker;
            }

            var involved = new List<DialogueSpeaker>();
            foreach (DialogueLine line in lines)
            {
                if (line != null && TryGetSpeaker(speakers, line.speakerId, out DialogueSpeaker speaker) && !involved.Contains(speaker))
                    involved.Add(speaker);
            }
            involved.ForEach(s => s.SetInDialogue(true));

            root.SetActive(true);
            PreparePortraits(lines);
            ApplySubtitleSettings();
            speakerText.text = string.Empty;
            bodyText.text = string.Empty;
            yield return FadeBox(0f, 1f);

            foreach (DialogueLine line in lines)
            {
                if (line == null || line.pieces == null)
                    continue;

                TryGetSpeaker(speakers, line.speakerId, out DialogueSpeaker speaker);
                speakerText.text = speaker != null ? speaker.DisplayName : line.speakerId ?? string.Empty;
                HighlightPortrait(line.speakerId);

                foreach (DialoguePiece piece in line.pieces)
                {
                    if (piece != null)
                        yield return PlayPiece(piece, speaker);
                }

                speaker?.SetState(DialogueSpeaker.State.Idle);
            }

            yield return FadeBox(1f, 0f);
            root.SetActive(false);
            involved.ForEach(s => s.SetInDialogue(false));
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            playing = false;
            onFinished?.Invoke();
        }

        private IEnumerator PlayPiece(DialoguePiece piece, DialogueSpeaker speaker)
        {
            AudioSource source = speaker != null && speaker.VoiceSource != null ? speaker.VoiceSource : voice2D;
            bool hasVoice = piece.voice != null;
            if (hasVoice)
                VoiceTrim.Play(source, piece.voice, piece.Volume);

            pieceText = Loc.T(piece.text ?? string.Empty);
            float fullReveal = pieceText.Length + fadeLetters;
            ApplyReveal(0f);
            continueText.gameObject.SetActive(false);
            speaker?.SetState(DialogueSpeaker.State.Talking);

            float shown = 0f;
            float elapsed = 0f;
            float hintBlink = 0f;
            bool skipped = false;
            while (true)
            {
                if (Time.timeScale > 0f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bool pressed = elapsed > 0.15f && AdvancePressed();
                    if (shown < fullReveal)
                    {
                        shown = pressed ? fullReveal : Mathf.Min(fullReveal, shown + charactersPerSecond * Time.unscaledDeltaTime);
                        ApplyReveal(shown);
                    }
                    else if (pressed)
                    {
                        skipped = true;
                        if (hasVoice && source != null)
                            source.Stop();
                        break;
                    }

                    bool voiceDone = !hasVoice || VoiceTrim.Finished(source, piece.voice);
                    bool silentDone = hasVoice || elapsed >= silentPieceSeconds;
                    if (shown >= fullReveal && voiceDone && silentDone)
                        break;
                    hintBlink += Time.unscaledDeltaTime * continueBlinkSpeed;
                    ShowSkipHint(shown < fullReveal, hintBlink);
                }
                yield return null;
            }
            HideSkipHint();

            ApplyReveal(fullReveal);
            if (skipped)
            {
                speaker?.SetState(DialogueSpeaker.State.Waiting);
                yield return null;
                yield break;
            }
            speaker?.SetState(DialogueSpeaker.State.Waiting);
            continueText.gameObject.SetActive(true);
            yield return null;

            float blink = 0f;
            while (Time.timeScale <= 0f || !AdvancePressed())
            {
                blink += Time.unscaledDeltaTime * continueBlinkSpeed;
                Color c = continueColor;
                c.a *= continueBlinkSpeed > 0f ? 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Cos(blink * Mathf.PI)) : 1f;
                continueText.color = c;
                yield return null;
            }
            continueText.gameObject.SetActive(false);
            yield return null;
        }

        private void ApplyReveal(float progress)
        {
            revealBuilder.Clear();
            float fade = Mathf.Max(1f, fadeLetters);
            for (int i = 0; i < pieceText.Length; i++)
            {
                float reveal = Mathf.Clamp01((progress - i) / fade);
                byte alpha = (byte)(reveal * textOpacity * 255f);
                if (reveal < 1f)
                    revealBuilder.Append("<alpha=#").Append(alpha.ToString("X2")).Append('>');
                char c = pieceText[i];
                if (c == '<')
                    revealBuilder.Append("<noparse><</noparse>");
                else
                    revealBuilder.Append(c);
            }
            bodyText.text = revealBuilder.ToString();
        }

        private IEnumerator FadeBox(float from, float to)
        {
            if (boxFadeSeconds <= 0f)
            {
                rootGroup.alpha = to;
                yield break;
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / boxFadeSeconds)
            {
                rootGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            rootGroup.alpha = to;
        }

        private static bool TryGetSpeaker(Dictionary<string, DialogueSpeaker> speakers, string id, out DialogueSpeaker speaker)
        {
            speaker = null;
            return !string.IsNullOrWhiteSpace(id) && speakers.TryGetValue(id.Trim(), out speaker) && speaker != null;
        }

        private void ShowSkipHint(bool twice, float blink)
        {
            if (skipHint == null)
                return;
            string english = twice ? "CLICK TWICE TO CONTINUE" : "CLICK TO CONTINUE";
            if (!skipHint.gameObject.activeSelf)
                skipHint.gameObject.SetActive(true);
            if (skipHint.text != english && skipHint.text != Loc.T(english))
                skipHint.text = english;
            Color c = continueColor;
            c.a *= 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Cos(blink * Mathf.PI));
            skipHint.color = c;
        }

        private void HideSkipHint()
        {
            if (skipHint != null && skipHint.gameObject.activeSelf)
                skipHint.gameObject.SetActive(false);
        }

        private static bool AdvancePressed()
        {
            Mouse mouse = Mouse.current;
            Touchscreen touch = Touchscreen.current;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                   (touch != null && touch.primaryTouch.press.wasPressedThisFrame) ||
                   (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                         keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)) ||
                   (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        }

        private void BuildUi()
        {
            TMP_FontAsset resolved = resolvedFont = MainMenuController.ResolveFont(fontFile, font, "Dialogue Font")
                ?? Resources.Load<TMP_FontAsset>("Fonts/OSerif-Regular")
                ?? TMP_Settings.defaultFontAsset;

            root = new GameObject("Dialogue UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform box = NewRect("Box", root.transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0f);
            box.sizeDelta = boxSize;
            boxRect = box;
            box.anchoredPosition = new Vector2(0f, bottomMargin);

            if (useBackground)
            {
                backgroundImage = Stretch(NewRect("Background", box)).gameObject.AddComponent<Image>();
                backgroundImage.sprite = background;
                backgroundImage.type = background != null && background.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                backgroundImage.raycastTarget = false;
            }

            if (borderWidth > 0f)
            {
                AddLine(box, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, borderWidth));
                AddLine(box, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, borderWidth));
                AddLine(box, Vector2.zero, new Vector2(0f, 1f), new Vector2(borderWidth, 0f));
                AddLine(box, new Vector2(1f, 0f), Vector2.one, new Vector2(borderWidth, 0f));
            }

            speakerText = CreateText(box, "Speaker", resolved, speakerSize, Color.white, TextAlignmentOptions.Top);
            RectTransform speakerRect = speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = Vector2.one;
            speakerRect.pivot = new Vector2(0.5f, 1f);

            bodyText = CreateText(box, "Text", resolved, textSize, Color.white, TextAlignmentOptions.Top);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;

            continueText = CreateText(box, "Continue", resolved, textSize * 0.7f, continueColor, TextAlignmentOptions.BottomRight);
            continueText.text = continueSymbol;
            RectTransform continueRect = continueText.rectTransform;
            continueRect.anchorMin = continueRect.anchorMax = continueRect.pivot = new Vector2(1f, 0f);
            continueRect.anchoredPosition = new Vector2(-padding.x * 0.5f, padding.y * 0.5f);
            continueText.gameObject.SetActive(false);

            skipHint = CreateText(root.transform, "Skip Hint", resolved, textSize * 0.6f, continueColor, TextAlignmentOptions.BottomRight);
            RectTransform hintRect = skipHint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(1f, 0f);
            hintRect.sizeDelta = new Vector2(900f, 60f);
            hintRect.anchoredPosition = new Vector2(-40f, 24f);
            skipHint.textWrappingMode = TextWrappingModes.NoWrap;
            skipHint.gameObject.SetActive(false);

            leftPortrait = Portrait(box, "Ungaretti Portrait", 0f, out InkPortrait leftInk, out leftGroup);
            leftInk.Person = InkPortrait.Sitter.Ungaretti;
            rightPortrait = Portrait(box, "Speaker Portrait", 1f, out rightInk, out rightGroup);

            ApplySubtitleSettings();
            root.SetActive(false);
        }

        private RectTransform Portrait(RectTransform box, string name, float side, out InkPortrait ink, out CanvasGroup group)
        {
            RectTransform tile = NewRect(name, box);
            tile.anchorMin = tile.anchorMax = new Vector2(side, 1f);
            tile.pivot = new Vector2(side, 0f);
            tile.sizeDelta = new Vector2(portraitSize, portraitSize);
            tile.anchoredPosition = new Vector2(side < 0.5f ? 24f : -24f, -18f);
            tile.localRotation = Quaternion.Euler(0f, 0f, side < 0.5f ? 2f : -2f);
            group = tile.gameObject.AddComponent<CanvasGroup>();
            Image paper = tile.gameObject.AddComponent<Image>();
            paper.color = new Color(0.937f, 0.902f, 0.824f, 1f);
            paper.raycastTarget = false;
            tile.gameObject.AddComponent<RectMask2D>();
            Color inkColor = new Color(0.118f, 0.165f, 0.227f, 1f);
            foreach (var (min, max, size) in new[] { (new Vector2(0f, 1f), Vector2.one, new Vector2(-12f, 2f)), (Vector2.zero, new Vector2(1f, 0f), new Vector2(-12f, 2f)), (Vector2.zero, new Vector2(0f, 1f), new Vector2(2f, -12f)), (new Vector2(1f, 0f), Vector2.one, new Vector2(2f, -12f)) })
            {
                RectTransform line = NewRect("Frame", tile);
                line.anchorMin = min;
                line.anchorMax = max;
                line.sizeDelta = size;
                line.anchoredPosition = new Vector2(min.x == max.x ? (min.x < 0.5f ? 6f : -6f) : 0f, min.y == max.y ? (min.y < 0.5f ? 6f : -6f) : 0f);
                Image image = line.gameObject.AddComponent<Image>();
                image.color = inkColor;
                image.raycastTarget = false;
            }
            RectTransform drawing = NewRect("Ink", tile);
            drawing.anchorMin = drawing.anchorMax = new Vector2(0.5f, 0.5f);
            drawing.sizeDelta = new Vector2(portraitSize * 1.3f, portraitSize * 1.3f);
            drawing.anchoredPosition = new Vector2(0f, -portraitSize * 0.12f);
            ink = drawing.gameObject.AddComponent<InkPortrait>();
            ink.color = new Color(0.118f, 0.165f, 0.227f, 1f);
            ink.raycastTarget = false;
            ink.Seed = side < 0.5f ? 3 : 5;
            return tile;
        }

        private void PreparePortraits(IEnumerable<DialogueLine> lines)
        {
            rightUsed = false;
            foreach (DialogueLine line in lines)
                if (line != null && InkPortrait.TryFind(line.speakerId, out InkPortrait.Sitter sitter) && sitter != InkPortrait.Sitter.Ungaretti)
                {
                    rightInk.Person = sitter;
                    rightUsed = true;
                    break;
                }
            leftGroup.alpha = silentPortraitAlpha;
            rightGroup.alpha = silentPortraitAlpha;
        }

        private void HighlightPortrait(string speakerId)
        {
            bool known = InkPortrait.TryFind(speakerId, out InkPortrait.Sitter sitter);
            bool left = known && sitter == InkPortrait.Sitter.Ungaretti;
            bool right = known && !left && rightUsed;
            if (right && sitter != rightInk.Person)
                rightInk.Person = sitter;
            leftGroup.alpha = left ? 1f : silentPortraitAlpha;
            rightGroup.alpha = right ? 1f : silentPortraitAlpha;
            leftPortrait.localScale = Vector3.one * (left ? 1.06f : 1f);
            rightPortrait.localScale = Vector3.one * (right ? 1.06f : 1f);
        }

        private void ApplySubtitleSettings()
        {
            if (boxRect == null)
                return;

            SettingsData settings = GameSettings.Current;
            TextStyle style = settings.subtitleStyle;
            bool show = settings.subtitlesEnabled;

            float speaker = speakerSize * style.scale;
            float body = textSize * style.scale;
            speakerText.fontSize = speaker;
            bodyText.fontSize = body;
            continueText.fontSize = body * 0.7f;
            speakerText.rectTransform.offsetMin = new Vector2(padding.x, -padding.y - speaker * 1.3f);
            speakerText.rectTransform.offsetMax = new Vector2(-padding.x, -padding.y);
            bodyText.rectTransform.offsetMin = new Vector2(padding.x + textSize, padding.y);
            bodyText.rectTransform.offsetMax = new Vector2(-padding.x - textSize, -padding.y - speaker * 1.4f);
            continueText.rectTransform.sizeDelta = new Vector2(body * 1.5f, body * 1.5f);
            textOpacity = style.textColor.a;
            TextStyling.Apply(speakerText, style, style.accentColor, bold);
            TextStyling.Apply(bodyText, style, style.textColor, bold);
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMax = body;
            bodyText.fontSizeMin = body * 0.45f;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            speakerText.gameObject.SetActive(show);
            bodyText.gameObject.SetActive(show);
            if (leftPortrait != null)
            {
                float scale = Mathf.Clamp(style.scale, 0.7f, 1.4f);
                leftPortrait.sizeDelta = rightPortrait.sizeDelta = new Vector2(portraitSize, portraitSize) * scale;
                leftPortrait.gameObject.SetActive(show && showPortraits);
                rightPortrait.gameObject.SetActive(show && showPortraits && rightUsed);
            }
            borderLines.ForEach(line => line.enabled = show);
            if (backgroundImage != null)
            {
                backgroundImage.color = style.backgroundColor;
                backgroundImage.enabled = show;
            }
        }

        private static TMP_Text CreateText(Transform parent, string objectName, TMP_FontAsset fontAsset, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = NewRect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private void AddLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            RectTransform line = NewRect("Border", parent);
            line.anchorMin = anchorMin;
            line.anchorMax = anchorMax;
            line.pivot = anchorMin;
            line.sizeDelta = size;
            line.anchoredPosition = Vector2.zero;
            Image image = line.gameObject.AddComponent<Image>();
            image.color = borderColor;
            image.raycastTarget = false;
            borderLines.Add(image);
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
