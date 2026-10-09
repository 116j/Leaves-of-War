using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string FloorPath = "MainMenu/Floor";
        private const string GoldenLeafPath = "MainMenu/GoldenLeaf";
        private const string LogoPath = "MainMenu/Logo";
        private const string CreditsPath = "MainMenu/Credits";
        private const string SweepSoundPath = "MainMenu/Sweep";
        private const string CursorPath = "UI/CustomCursor";
        private const string HandwritingFontPath = "Fonts/ErraticCursive";
        private const string ReadableFontPath = "Fonts/OSerif";
        private const string FontSample = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const float OffscreenMargin = 6f;

        private static readonly string[] MenuOrder = { "New Game", "Continue", "Load Game", "Options", "Credits", "Quit" };
        private static readonly Vector2 ReferenceSize = new Vector2(1920f, 1080f);
        private static readonly Color PanelTextColor = new Color(0.9f, 0.86f, 0.74f);
        private static readonly Color PanelTextDisabledColor = new Color(0.55f, 0.53f, 0.47f);
        private static readonly Color OverlayTextColor = new Color(0.96f, 0.93f, 0.88f, 1f);
        private static readonly Color[] AutumnColors =
        {
            new Color(0.45f, 0.27f, 0.12f), new Color(0.62f, 0.42f, 0.16f), new Color(0.38f, 0.17f, 0.10f),
            new Color(0.70f, 0.55f, 0.25f), new Color(0.30f, 0.20f, 0.10f)
        };

        [Header("New Game")]
        [Tooltip("First level. Must be in File > Build Settings.")]
        [SerializeField] private string newGameScene = "L1_Milano";

        [Header("Menu Entries")]
        [SerializeField] private bool showCredits = true;

        [Header("Look")]
        [SerializeField] private Color fallbackFloorColor = new Color(0.36f, 0.25f, 0.16f, 1f);
        [SerializeField, Min(0.25f)] private float floorTiling = 2f;
        [SerializeField, Min(100f)] private float logoWidth = 900f;

        [Header("Menu Entries Style")]
        [SerializeField] private TMP_FontAsset entryFont;
        [Tooltip(".ttf / .otf file. Wins over Entry Font; Entry Font's material look is copied onto it.")]
        [SerializeField] private Font entryFontFile;
        [SerializeField] private bool entryBold = false;
        [SerializeField, Min(10f)] private float menuWordSize = 64f;
        [SerializeField] internal Color inkColor = new Color(0.118f, 0.165f, 0.227f, 1f);
        [SerializeField] internal Color entryHighlightColor = new Color(0.077f, 0.107f, 0.148f, 1f);
        [SerializeField, Range(1f, 1.5f)] internal float entryHighlightScale = 1.08f;
        [SerializeField, Range(0f, 1f)] private float entryTextOutlineWidth = 0f;
        [SerializeField] private Color entryTextOutlineColor = Color.black;
        [SerializeField] private Vector2 entrySize = new Vector2(520f, 80f);
        [SerializeField, Min(10f)] private float entrySpacing = 88f;
        [SerializeField] private bool useEntryBackground = false;
        [Tooltip("The background unfolds on hover and crumples away on exit. Off = always visible.")]
        [SerializeField] internal bool entryBackgroundOnHover = true;
        [SerializeField, Range(0.05f, 1.5f)] internal float entryBackgroundUnfoldTime = 0.3f;
        [SerializeField, Range(0.05f, 1.5f)] internal float entryBackgroundCrumpleTime = 0.22f;
        [Tooltip("Sprite (9-sliced if it has borders). Empty = plain colour.")]
        [SerializeField] private Sprite entryBackground;
        [Tooltip("Per entry: New Game, Continue, Load Game, Options, Credits, Quit. Empty slot = shared background.")]
        [SerializeField] private Sprite[] entryBackgroundOverrides = new Sprite[0];
        [SerializeField] internal Color entryBackgroundColor = new Color(0.9f, 0.85f, 0.72f, 0.85f);
        [SerializeField] internal Color entryBackgroundHighlightColor = new Color(1f, 0.95f, 0.82f, 0.95f);
        [SerializeField, Min(0f)] private float entryBorderWidth = 0f;
        [SerializeField] private Color entryBorderColor = new Color(0.118f, 0.165f, 0.227f, 1f);

        [Header("Panels Style")]
        [Tooltip("Options, Key Bindings, Load Game, Credits and status messages.")]
        [SerializeField] private TMP_FontAsset panelFont;
        [SerializeField] private Font panelFontFile;
        [SerializeField] private bool panelBold = false;

        [Header("Hint Text Style")]
        [SerializeField, TextArea(1, 3)] private string hintMessage = "Hold the left mouse button and move to sweep the leaves  ·  Enter to blow them away";
        [Tooltip("Hint shown on phones and tablets.")]
        [SerializeField, TextArea(1, 3)] private string hintMessageTouch = "Swipe to sweep the leaves  ·  Double-tap to blow them away";
        [SerializeField] private TMP_FontAsset hintFont;
        [SerializeField] private Font hintFontFile;
        [SerializeField] private bool hintBold = false;
        [SerializeField, Min(8f)] private float hintSize = 26f;
        [SerializeField] private Color hintColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);
        [SerializeField] private Vector2 hintPosition = new Vector2(0f, -490f);
        [SerializeField, Range(0f, 1f)] private float hintTextOutlineWidth = 0f;
        [SerializeField] private Color hintTextOutlineColor = Color.black;
        [SerializeField] private bool useHintBackground = false;
        [SerializeField] private Sprite hintBackground;
        [SerializeField] private Color hintBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Vector2 hintPadding = new Vector2(28f, 12f);
        [SerializeField, Min(0f)] private float hintBorderWidth = 0f;
        [SerializeField] private Color hintBorderColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);

        [Header("Leaves")]
        [SerializeField] private Vector2Int maskResolution = new Vector2Int(320, 180);
        [Tooltip("Distance between leaves in the starting carpet (leaf-layer pixels).")]
        [SerializeField, Range(2f, 10f)] private float leafSpacing = 4.5f;
        [Tooltip("An entry is clickable when the covered share of its area falls below this.")]
        [SerializeField, Range(0.05f, 0.9f)] private float revealThreshold = 0.25f;
        [Tooltip("Golden leaves falling per second, from the first leaf moved by the broom.")]
        [SerializeField, Min(0f)] private float regrowLeavesPerSecond = 1f;
        [SerializeField, Min(0.2f)] private float goldLeafFallTime = 2.5f;
        [Tooltip("Half of the longer side of a leaf, in leaf-layer pixels.")]
        [SerializeField, Range(1f, 20f)] private float goldLeafSize = 5f;
        [SerializeField] private Vector2 goldLeafLength = new Vector2(0.85f, 1.15f);
        [SerializeField] private Vector2 goldLeafWidth = new Vector2(0.75f, 1.1f);
        [SerializeField] private Vector2 goldLeafBrightness = new Vector2(0.8f, 1.15f);
        [Tooltip("0 = gold as drawn, 1 = fully recoloured with autumn colours.")]
        [SerializeField, Range(0f, 1f)] private float goldLeafTint = 0.6f;
        [SerializeField, Min(0)] private int maxGoldLeaves = 1500;
        [SerializeField, Min(0.1f)] private float gustDuration = 0.8f;

        [Header("Touch")]
        [Tooltip("Max seconds between the two taps of a double-tap (gust of wind).")]
        [SerializeField, Range(0.15f, 0.6f)] private float doubleTapTime = 0.35f;

        [Header("Broom")]
        [SerializeField, Range(6f, 80f)] private float broomHalfWidth = 30f;
        [SerializeField, Range(1f, 20f)] private float broomHalfDepth = 4f;
        [SerializeField, Range(1f, 16f)] private float pileDepth = 5f;
        [Tooltip("Share of the broom speed kept by the pushed leaves.")]
        [SerializeField, Range(0f, 1.5f)] private float pushKick = 0.8f;
        [SerializeField, Range(0.5f, 15f)] private float leafFriction = 4f;
        [Tooltip("Chance that a leaf at the ends of the head slips out sideways.")]
        [SerializeField, Range(0f, 1f)] private float endSpill = 0.35f;
        [SerializeField, Range(0.05f, 3f)] private float minStroke = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
        [SerializeField] private AudioClip windAmbience;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.5f;
        [Tooltip("Empty = Resources/MainMenu/Sweep.")]
        [SerializeField] private AudioClip sweepLoop;
        [SerializeField, Range(0f, 1f)] private float sweepVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sweepBareWoodVolume = 0.45f;
        [SerializeField, Min(10f)] private float sweepFullSpeed = 350f;
        [SerializeField] private Vector2 sweepPitch = new Vector2(0.9f, 1.12f);
        [SerializeField] private AudioClip gustClip;
        [SerializeField, Range(0f, 1f)] private float gustVolume = 0.8f;

        [Header("Menu Entry Sounds")]
        [SerializeField] private AudioClip entryEnterSound;
        [SerializeField] private AudioClip entryExitSound;
        [SerializeField] private AudioClip entryClickSound;
        [SerializeField, Range(0f, 1f)] private float entrySoundVolume = 0.8f;
        [SerializeField] private Vector2 entrySoundPitch = new Vector2(0.94f, 1.06f);

        [Header("Custom Cursor")]
        [SerializeField, Min(8f)] private float cursorSize = 256f;
        [Tooltip("Image pixels from the top-left corner: centre of the bristle tips.")]
        [SerializeField] private Vector2 cursorHotspot = new Vector2(4f, 4f);
        [SerializeField, Range(0f, 45f)] private float cursorSwingAngle = 16f;
        [SerializeField] private Vector2 cursorSwingRate = new Vector2(1.2f, 2.6f);
        [Tooltip("Negative if the handle leans the wrong way.")]
        [SerializeField, Range(-45f, 45f)] private float cursorLeanAngle = 18f;
        [SerializeField, Range(0f, 0.3f)] private float cursorPressSquash = 0.08f;

        private struct Leaf
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public Vector2 Half;
            public float Angle;
            public float Spin;
            public float Shade;
            public float Fall;
            public float Phase;
            public Color Tint;
        }

        private TMP_FontAsset handwritingFont;
        private TMP_FontAsset readableFont;
        private TMP_FontAsset panelFontAsset;
        private TMP_FontAsset hintFontAsset;

        private RectTransform leavesRect;
        private GameObject logoObject;
        private GameObject menuGroup;
        private readonly List<FloorMenuItem> items = new List<FloorMenuItem>();
        private FloorMenuItem continueItem;

        private TMP_Text statusText;
        private CanvasGroup hintGroup;
        private Coroutine hintFade;
        private OptionsPanel optionsPanel;
        private readonly List<GameObject> optionsRoots = new List<GameObject>();
        private float nextOptionsStyle;
        private GameObject loadPanel;
        private TMP_Text loadStatusText;
        private Button autosaveButton;
        private TMP_Text autosaveLabel;
        private readonly List<(int number, Button button, TMP_Text label)> slots = new List<(int, Button, TMP_Text)>();
        private GameObject creditsPanel;
        private RectTransform creditsScroll;
        private Coroutine creditsRoutine;

        private Texture2D leavesTexture;
        private Color32[] leafBuffer;
        private Color32[] goldPixels;
        private int goldWidth;
        private int goldHeight;
        private Leaf[] leaves;
        private int leafCount;
        private System.Random rng;
        private bool leavesMoving;
        private bool leavesDirty;
        private bool gustActive;
        private bool regrowStarted;
        private float regrowAccumulator;
        private bool hasSwept;

        private bool strokeActive;
        private Vector2 lastStrokePoint;
        private Vector2 lastStrokeDir;
        private float lastStrokeTime = -10f;
        private float lastStrokeSpeed;
        private int pushedThisStroke;

        private bool inputLocked;
        private bool starting;

        private bool touchDevice;
        private float tapStartTime;
        private Vector2 tapStartPosition;
        private float lastTapTime = -10f;
        private Vector2 lastTapPosition;
        private RawImage cursorImage;

        private AudioSource sweepSource;
        private AudioSource sfxSource;
        private AudioSource entrySource;

        private Canvas cursorCanvas;
        private RectTransform cursorRect;
        private float cursorAngle;
        private float cursorSwingPhase;
        private float cursorSquash;

        private readonly Vector3[] corners = new Vector3[4];

        private void Awake()
        {
            touchDevice = Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);
            EnsureEventSystem();
            EnsureCamera();
            if (FindAnyObjectByType<SettingsApplier>() == null)
                new GameObject("Settings Applier").AddComponent<SettingsApplier>();
            LoadFonts();
            BuildUi();
            StartAudio();
        }

        private void OnEnable()
        {
            ApplyCursor();
            RefreshContinueAvailability();
        }

        private void OnDisable()
        {
            AudioManager.ExistingInstance?.StopMusicIfCurrent(menuMusic);
            if (cursorCanvas != null)
                cursorCanvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (leavesTexture != null)
                Destroy(leavesTexture);
        }

        private void Update()
        {
            if (inputLocked)
            {
                strokeActive = false;
            }
            else
            {
                HandlePointerSweeping();
                HandleConfirm();
                HandleDoubleTap();
                RegrowLeaves();
            }

            SimulateLeaves(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            if (leavesDirty)
                DrawLeaves();

            bool menuVisible = menuGroup.activeInHierarchy && !inputLocked;
            foreach (FloorMenuItem item in items)
                item.SetRevealed(menuVisible && CoverageUnder((RectTransform)item.transform) < revealThreshold);

            UpdateSweepAudio();
        }

        private void LateUpdate()
        {
            UpdateCursor();

            if ((panelFontAsset != null || panelBold) && Time.unscaledTime >= nextOptionsStyle &&
                optionsRoots.Exists(r => r != null && r.activeInHierarchy))
            {
                nextOptionsStyle = Time.unscaledTime + 0.2f;
                optionsRoots.ForEach(StylePanelTexts);
            }
        }

        private void LoadFonts()
        {
            TMP_FontAsset readable = ResolveFont(null, Resources.Load<TMP_FontAsset>(ReadableFontPath), ReadableFontPath);
            panelFontAsset = ResolveFont(panelFontFile, panelFont, "Panel Font");
            readableFont = panelFontAsset ?? readable ?? TMP_Settings.defaultFontAsset;
            handwritingFont = ResolveFont(entryFontFile, entryFont, "Entry Font")
                ?? ResolveFont(null, Resources.Load<TMP_FontAsset>(HandwritingFontPath), HandwritingFontPath)
                ?? readableFont;
            hintFontAsset = ResolveFont(hintFontFile, hintFont, "Hint Font") ?? readableFont;
        }

        internal static TMP_FontAsset ResolveFont(Font file, TMP_FontAsset asset, string label)
        {
            if (file != null)
            {
                TMP_FontAsset fromFile = CreateDynamicFont(file, asset);
                if (fromFile != null)
                    return fromFile;
                Debug.LogWarning($"MainMenuController ({label}): cannot build a font from '{file.name}'. Tick 'Include Font Data' in its import settings.");
            }

            if (asset == null)
                return null;
            if (HasLetters(asset))
                return asset;

            TMP_FontAsset rebuilt = asset.sourceFontFile != null ? CreateDynamicFont(asset.sourceFontFile, asset) : null;
            if (rebuilt != null)
                return rebuilt;

            Debug.LogWarning($"MainMenuController ({label}): '{asset.name}' has no letters, TextMeshPro will draw a fallback font. Use the matching Font File field.");
            return asset;
        }

        private static bool HasLetters(TMP_FontAsset font) => font.HasCharacters(FontSample, out uint[] _, false, true);

        private static TMP_FontAsset CreateDynamicFont(Font file, TMP_FontAsset look)
        {
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(file);
            if (created == null || !HasLetters(created))
                return null;
            created.name = file.name + " (runtime)";

            if (look != null && look.material != null && created.material != null)
            {
                Material target = created.material;
                Texture atlas = target.GetTexture(ShaderUtilities.ID_MainTex);
                float width = target.GetFloat(ShaderUtilities.ID_TextureWidth);
                float height = target.GetFloat(ShaderUtilities.ID_TextureHeight);
                float gradient = target.GetFloat(ShaderUtilities.ID_GradientScale);

                target.shader = look.material.shader;
                target.CopyPropertiesFromMaterial(look.material);
                target.shaderKeywords = look.material.shaderKeywords;

                target.SetTexture(ShaderUtilities.ID_MainTex, atlas);
                target.SetFloat(ShaderUtilities.ID_TextureWidth, width);
                target.SetFloat(ShaderUtilities.ID_TextureHeight, height);
                target.SetFloat(ShaderUtilities.ID_GradientScale, gradient);
            }
            return created;
        }

        private void BuildUi()
        {
            RectTransform root = CreateCanvas("Main Menu", 500, true);

            RawImage floor = Stretch(NewRect("Floor", root, typeof(RawImage))).GetComponent<RawImage>();
            floor.raycastTarget = false;
            Texture2D floorTexture = Resources.Load<Texture2D>(FloorPath);
            if (floorTexture != null)
            {
                floorTexture.wrapMode = TextureWrapMode.Repeat;
                floor.texture = floorTexture;
                floor.uvRect = new Rect(0f, 0f, floorTiling, floorTiling * 9f / 16f);
            }
            else
            {
                floor.color = fallbackFloorColor;
            }

            RectTransform writing = Place(NewRect("Floor Writing", root), ReferenceSize, Vector2.zero);
            logoObject = CreateLogo(writing);
            menuGroup = Stretch(NewRect("Menu Entries", writing)).gameObject;

            var entries = new List<(string label, System.Action action)>
            {
                ("New Game", StartNewGame),
                ("Continue", ContinueGame),
                ("Load Game", ShowLoadMenu),
                ("Options", ShowOptions)
            };
            if (showCredits)
                entries.Add(("Credits", ShowCredits));
#if !UNITY_WEBGL || UNITY_EDITOR
            entries.Add(("Quit", QuitGame));
#endif
            float y = 40f;
            foreach (var (label, action) in entries)
            {
                FloorMenuItem item = AddItem(label, y, action);
                if (label == "Continue")
                    continueItem = item;
                y -= entrySpacing;
            }

            for (int i = 0; i < items.Count; i++)
            {
                items[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i - 1 + items.Count) % items.Count],
                    selectOnDown = items[(i + 1) % items.Count]
                };
            }

            CreateLeafLayer(root);

            RectTransform overlay = Place(NewRect("Overlay", root), ReferenceSize, Vector2.zero);
            CreateHint(overlay);

            statusText = CreateText(overlay, "Status", readableFont, 24f, new Vector2(0f, 500f), new Vector2(1700f, 50f));
            statusText.color = OverlayTextColor;

            int childrenBefore = overlay.childCount;
            optionsPanel = new OptionsPanel(overlay, readableFont, readableFont, ShowMainMenu);
            for (int i = childrenBefore; i < overlay.childCount; i++)
                optionsRoots.Add(overlay.GetChild(i).gameObject);
            optionsPanel.Hide();

            loadPanel = BuildLoadPanel(overlay);
            creditsPanel = BuildCreditsPanel(overlay);

            StylePanelTexts(statusText.gameObject);
            StylePanelTexts(loadPanel);
            StylePanelTexts(creditsPanel);
            optionsRoots.ForEach(StylePanelTexts);

            loadPanel.SetActive(false);
            creditsPanel.SetActive(false);
            RefreshContinueAvailability();
        }

        private GameObject CreateLogo(RectTransform parent)
        {
            Texture2D logo = Resources.Load<Texture2D>(LogoPath);
            if (logo == null)
            {
                TMP_Text title = CreateText(parent, "Logo", handwritingFont, 140f, new Vector2(0f, 320f), new Vector2(1400f, 240f));
                title.text = "Leaves of War";
                title.color = inkColor;
                return title.gameObject;
            }

            RectTransform rect = Place(NewRect("Logo", parent, typeof(RawImage)),
                new Vector2(logoWidth, logoWidth * logo.height / Mathf.Max(1f, logo.width)), new Vector2(0f, 320f));
            RawImage image = rect.GetComponent<RawImage>();
            image.texture = logo;
            image.raycastTarget = false;
            return rect.gameObject;
        }

        private FloorMenuItem AddItem(string label, float y, System.Action action)
        {
            RectTransform rect = Place(NewRect(label, menuGroup.transform, typeof(Image)), entrySize, new Vector2(0f, y));
            rect.GetComponent<Image>().color = Color.clear;

            RectTransform visual = Stretch(NewRect("Visual", rect, typeof(CanvasGroup)));
            CanvasGroup group = visual.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Graphic background = null;
            if (useEntryBackground)
            {
                int order = System.Array.IndexOf(MenuOrder, label);
                Sprite sprite = order >= 0 && order < entryBackgroundOverrides.Length && entryBackgroundOverrides[order] != null
                    ? entryBackgroundOverrides[order]
                    : entryBackground;

                if (entryBackgroundOnHover)
                {
                    CrumplePaper paper = Stretch(NewRect("Background", visual, typeof(CrumplePaper))).GetComponent<CrumplePaper>();
                    paper.Sprite = sprite;
                    paper.Seed = order * 7.31f + 3.7f;
                    background = paper;
                }
                else
                {
                    background = CreateImage(visual, sprite);
                }
                background.color = entryBackgroundColor;
                background.raycastTarget = false;
            }

            if (entryBorderWidth > 0f)
                CreateBorder(visual, entryBorderWidth, entryBorderColor);

            TMP_Text text = CreateText(visual, "Word", handwritingFont, menuWordSize, Vector2.zero, entrySize);
            text.text = label;
            text.color = inkColor;
            StyleText(text, entryBold, entryTextOutlineWidth, entryTextOutlineColor);

            FloorMenuItem item = rect.gameObject.AddComponent<FloorMenuItem>();
            item.Init(this, text, visual, group, background, action);
            items.Add(item);
            return item;
        }

        private void CreateHint(RectTransform overlay)
        {
            RectTransform box = Place(NewRect("Hint", overlay, typeof(CanvasGroup)), Vector2.zero, hintPosition);
            hintGroup = box.GetComponent<CanvasGroup>();
            hintGroup.interactable = false;
            hintGroup.blocksRaycasts = false;

            TMP_Text text = CreateText(box, "Text", hintFontAsset, hintSize, Vector2.zero, new Vector2(1700f, hintSize * 2f));
            string message = touchDevice ? hintMessageTouch : hintMessage;
            text.text = message;
            text.color = hintColor;
            StyleText(text, hintBold, hintTextOutlineWidth, hintTextOutlineColor);

            Vector2 preferred = text.GetPreferredValues(message, 1700f, 0f);
            Vector2 textSize = new Vector2(Mathf.Min(preferred.x, 1700f), preferred.y);
            box.sizeDelta = textSize + hintPadding * 2f;
            text.rectTransform.sizeDelta = textSize;

            if (useHintBackground)
            {
                Image background = CreateImage(box, hintBackground);
                background.color = hintBackgroundColor;
                background.transform.SetAsFirstSibling();
            }
            if (hintBorderWidth > 0f)
                CreateBorder(box, hintBorderWidth, hintBorderColor);
        }

        private void FadeOutHint(float duration)
        {
            if (hintGroup == null || !isActiveAndEnabled)
                return;
            if (hintFade != null)
                StopCoroutine(hintFade);
            hintFade = StartCoroutine(FadeHint(duration));
        }

        private IEnumerator FadeHint(float duration)
        {
            float start = hintGroup.alpha;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration))
            {
                hintGroup.alpha = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            hintGroup.alpha = 0f;
            hintFade = null;
        }

        private void StylePanelTexts(GameObject root)
        {
            if (root == null)
                return;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (panelFontAsset != null && text.font != panelFontAsset)
                    text.font = panelFontAsset;
                if (panelBold)
                    text.fontStyle |= FontStyles.Bold;
            }
        }

        private static void StyleText(TMP_Text text, bool bold, float outlineWidth, Color outlineColor)
        {
            if (bold)
                text.fontStyle |= FontStyles.Bold;
            if (outlineWidth > 0f)
            {
                text.outlineWidth = outlineWidth;
                text.outlineColor = outlineColor;
            }
        }

        private GameObject BuildLoadPanel(Transform parent)
        {
            RectTransform panel = Place(NewRect("Load Game", parent, typeof(Image)), new Vector2(1360f, 900f), Vector2.zero);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.03f, 0.024f, 0.98f);

            TMP_Text header = CreateText(panel, "Header", readableFont, 52f, new Vector2(0f, 230f), new Vector2(1000f, 72f));
            header.text = "LOAD GAME";
            header.color = new Color(0.88f, 0.84f, 0.72f);

            for (int i = 0; i < LevelProgress.SlotCount; i++)
            {
                int number = i + 1;
                Button button = CreatePanelButton(panel, $"Slot {number:D2}", new Vector2(i % 2 == 0 ? -245f : 245f, 130f - i / 2 * 65f), () => LoadLevel(LevelProgress.Slot(number)));
                slots.Add((number, button, button.GetComponentInChildren<TMP_Text>()));
            }

            autosaveButton = CreatePanelButton(panel, "Autosave", new Vector2(0f, -210f), () => LoadLevel(LevelProgress.Autosave));
            autosaveLabel = autosaveButton.GetComponentInChildren<TMP_Text>();

            loadStatusText = CreateText(panel, "Status", readableFont, 20f, new Vector2(0f, -295f), new Vector2(1120f, 62f));
            loadStatusText.textWrappingMode = TextWrappingModes.Normal;
            loadStatusText.color = new Color(0.73f, 0.69f, 0.57f);

            CreatePanelButton(panel, "Back", new Vector2(455f, 230f), ShowMainMenu, 250f)
                .GetComponentInChildren<TMP_Text>().text = "BACK";
            return panel.gameObject;
        }

        private Button CreatePanelButton(Transform parent, string name, Vector2 position, UnityEngine.Events.UnityAction action, float width = 440f)
        {
            RectTransform rect = Place(NewRect(name, parent, typeof(Image), typeof(Button)), new Vector2(width, 58f), position);
            rect.GetComponent<Image>().color = new Color(0.115f, 0.1f, 0.075f, 0.96f);

            Button button = rect.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            colors.disabledColor = new Color(0.34f, 0.32f, 0.28f, 0.65f);
            button.colors = colors;
            button.onClick.AddListener(action);

            CreateText(rect, "Label", readableFont, 23f, Vector2.zero, new Vector2(width - 30f, 46f)).color = PanelTextColor;
            return button;
        }

        private GameObject BuildCreditsPanel(Transform parent)
        {
            RectTransform panel = Stretch(NewRect("Credits", parent, typeof(Image)));
            panel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.025f, 0.94f);

            RectTransform viewport = Place(NewRect("Viewport", panel, typeof(RectMask2D)), new Vector2(1400f, 900f), Vector2.zero);
            TMP_Text text = CreateText(viewport, "Credits Text", readableFont, 34f, Vector2.zero, new Vector2(1300f, 100f));
            text.alignment = TextAlignmentOptions.Top;
            text.color = OverlayTextColor;
            text.textWrappingMode = TextWrappingModes.Normal;
            TextAsset credits = Resources.Load<TextAsset>(CreditsPath);
            text.text = credits != null ? credits.text : "LEAVES OF WAR\n\nA game by TheCelticArtist";
            creditsScroll = text.rectTransform;
            creditsScroll.pivot = new Vector2(0.5f, 1f);

            TMP_Text back = CreateText(panel, "Back Hint", readableFont, 24f, new Vector2(0f, -500f), new Vector2(1200f, 40f));
            back.text = "Click or press Esc to go back";
            back.color = new Color(OverlayTextColor.r, OverlayTextColor.g, OverlayTextColor.b, 0.7f);
            return panel.gameObject;
        }

        private void CreateLeafLayer(RectTransform parent)
        {
            int w = Mathf.Max(16, maskResolution.x);
            int h = Mathf.Max(9, maskResolution.y);

            leavesRect = Stretch(NewRect("Leaves", parent, typeof(RawImage)));
            leavesTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Menu Leaf Layer"
            };
            RawImage image = leavesRect.GetComponent<RawImage>();
            image.texture = leavesTexture;
            image.raycastTarget = false;

            leafBuffer = new Color32[w * h];
            LoadGoldenLeaf();
            rng = new System.Random(1918);

            float spacing = Mathf.Max(2f, leafSpacing);
            var carpet = new List<Leaf>();
            for (float y = -1f; y < h + 1f; y += spacing)
            {
                for (float x = -1f; x < w + 1f; x += spacing)
                {
                    var p = new Vector2(x + RandomRange(-0.5f, 0.5f) * spacing, y + RandomRange(-0.5f, 0.5f) * spacing);
                    if (Mathf.PerlinNoise(p.x * 0.08f + 13.7f, p.y * 0.08f + 4.2f) < 0.28f && rng.NextDouble() < 0.45)
                        continue;
                    carpet.Add(MakeLeaf(p));
                }
            }

            for (int i = carpet.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (carpet[i], carpet[j]) = (carpet[j], carpet[i]);
            }

            leafCount = carpet.Count;
            leaves = new Leaf[leafCount + Mathf.Max(0, maxGoldLeaves)];
            carpet.CopyTo(leaves);
            DrawLeaves();
        }

        private void LoadGoldenLeaf()
        {
            Texture2D gold = Resources.Load<Texture2D>(GoldenLeafPath);
            if (gold != null && gold.isReadable)
            {
                goldPixels = gold.GetPixels32();
                goldWidth = gold.width;
                goldHeight = gold.height;
                return;
            }

            Debug.LogWarning(gold == null
                ? $"MainMenuController: Resources/{GoldenLeafPath} not found, using a plain leaf."
                : $"MainMenuController: enable Read/Write on Resources/{GoldenLeafPath}, using a plain leaf.");

            goldWidth = 24;
            goldHeight = 12;
            goldPixels = new Color32[goldWidth * goldHeight];
            for (int y = 0; y < goldHeight; y++)
            {
                for (int x = 0; x < goldWidth; x++)
                {
                    float u = (x + 0.5f) / goldWidth * 2f - 1f;
                    float v = Mathf.Abs((y + 0.5f) / goldHeight * 2f - 1f);
                    float profile = 1f - u * u;
                    if (v <= profile)
                        goldPixels[y * goldWidth + x] = v > profile * 0.68f ? new Color32(150, 105, 40, 255) : new Color32(215, 160, 60, 255);
                }
            }
        }

        private Leaf MakeLeaf(Vector2 position)
        {
            float longest = Mathf.Max(goldWidth, goldHeight);
            float lengthStretch = RandomRange(goldLeafLength.x, goldLeafLength.y);
            float widthStretch = RandomRange(goldLeafWidth.x, goldLeafWidth.y);
            bool lengthAlongX = goldWidth >= goldHeight;

            return new Leaf
            {
                Pos = position,
                Half = new Vector2(
                    goldLeafSize * goldWidth / longest * (lengthAlongX ? lengthStretch : widthStretch),
                    goldLeafSize * goldHeight / longest * (lengthAlongX ? widthStretch : lengthStretch)),
                Angle = RandomRange(0f, Mathf.PI * 2f),
                Shade = RandomRange(goldLeafBrightness.x, goldLeafBrightness.y),
                Tint = SampleLeafColor()
            };
        }

        private Color SampleLeafColor()
        {
            Color a = AutumnColors[rng.Next(AutumnColors.Length)];
            Color b = AutumnColors[rng.Next(AutumnColors.Length)];
            return Color.Lerp(a, b, RandomRange(0f, 1f));
        }

        private float RandomRange(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static bool ReadPointer(out Vector2 position, out bool pressed)
        {
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                position = touch.primaryTouch.position.ReadValue();
                pressed = true;
                return true;
            }

            Mouse mouse = Mouse.current;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            pressed = mouse != null && mouse.leftButton.isPressed;
            return mouse != null;
        }

        private void HandlePointerSweeping()
        {
            if (!ReadPointer(out Vector2 screen, out bool pressed) || !pressed)
            {
                strokeActive = false;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(leavesRect, screen, null, out Vector2 local))
                return;

            Rect r = leavesRect.rect;
            var point = new Vector2((local.x - r.xMin) / r.width * leavesTexture.width, (local.y - r.yMin) / r.height * leavesTexture.height);

            if (!strokeActive)
            {
                strokeActive = true;
                lastStrokePoint = point;
                return;
            }

            Vector2 delta = point - lastStrokePoint;
            float distance = delta.magnitude;
            if (distance < minStroke)
                return;

            lastStrokeDir = delta / distance;
            lastStrokeSpeed = distance / Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
            lastStrokeTime = Time.unscaledTime;
            SweepStroke(lastStrokePoint, point);
            lastStrokePoint = point;
        }

        private void SweepStroke(Vector2 from, Vector2 to)
        {
            Vector2 dir = lastStrokeDir;
            var perp = new Vector2(-dir.y, dir.x);
            int stamps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(1f, broomHalfDepth * 0.5f)));
            float kick = lastStrokeSpeed * pushKick;
            pushedThisStroke = 0;

            for (int s = 1; s <= stamps; s++)
            {
                Vector2 center = Vector2.Lerp(from, to, (float)s / stamps);
                for (int n = 0; n < leafCount; n++)
                {
                    ref Leaf leaf = ref leaves[n];
                    if (leaf.Fall > 0f)
                        continue;

                    Vector2 d = leaf.Pos - center;
                    float across = Vector2.Dot(d, perp);
                    float along = Vector2.Dot(d, dir);
                    if (Mathf.Abs(across) > broomHalfWidth || along < -broomHalfDepth || along > broomHalfDepth)
                        continue;

                    float side = Mathf.Sign(across);
                    if (Mathf.Abs(across) > broomHalfWidth * 0.82f && rng.NextDouble() < endSpill)
                    {
                        leaf.Pos = center + dir * Mathf.Max(along, 0f) + perp * side * (broomHalfWidth + RandomRange(0.5f, 2.5f));
                        leaf.Vel = perp * side * kick * RandomRange(0.2f, 0.45f) + dir * kick * 0.3f;
                    }
                    else
                    {
                        leaf.Pos = center + dir * (broomHalfDepth + RandomRange(0f, pileDepth)) + perp * (across + RandomRange(-0.6f, 0.6f));
                        leaf.Vel = dir * kick * RandomRange(0.75f, 1.05f) + perp * kick * RandomRange(-0.12f, 0.12f);
                    }

                    leaf.Spin = RandomRange(-6f, 6f);
                    leaf.Angle += RandomRange(-0.4f, 0.4f);
                    leavesMoving = true;
                    pushedThisStroke++;
                    StartRegrow();
                }
            }

            RemoveOffscreenLeaves();
            leavesDirty = true;

            if (!hasSwept)
            {
                hasSwept = true;
                FadeOutHint(1.5f);
            }
        }

        private void StartRegrow()
        {
            if (regrowStarted)
                return;
            regrowStarted = true;
            regrowAccumulator = 1f;
        }

        private void SimulateLeaves(float dt)
        {
            if (!leavesMoving || dt <= 0f)
                return;

            float damp = gustActive ? 1f : Mathf.Exp(-leafFriction * dt);
            leavesMoving = false;

            for (int n = 0; n < leafCount; n++)
            {
                ref Leaf leaf = ref leaves[n];
                if (leaf.Fall > 0f)
                {
                    leaf.Phase += dt * 3.2f;
                    leaf.Pos += new Vector2(Mathf.Sin(leaf.Phase) * 16f, Mathf.Cos(leaf.Phase * 0.7f) * 5f) * leaf.Fall * dt;
                    leaf.Angle += Mathf.Sin(leaf.Phase * 1.3f) * 2.5f * leaf.Fall * dt;
                    leaf.Fall = Mathf.Max(0f, leaf.Fall - dt / goldLeafFallTime);
                    leavesMoving = true;
                }
                else if (leaf.Vel.sqrMagnitude < 0.04f)
                {
                    leaf.Vel = Vector2.zero;
                    leaf.Spin = 0f;
                }
                else
                {
                    leaf.Pos += leaf.Vel * dt;
                    leaf.Angle += leaf.Spin * dt;
                    leaf.Vel *= damp;
                    leaf.Spin *= damp;
                    leavesMoving = true;
                }
            }

            leavesDirty = true;
            RemoveOffscreenLeaves();
        }

        private void RemoveOffscreenLeaves()
        {
            float maxX = leavesTexture.width + OffscreenMargin;
            float maxY = leavesTexture.height + OffscreenMargin;
            int write = 0;
            for (int read = 0; read < leafCount; read++)
            {
                Vector2 p = leaves[read].Pos;
                if (p.x >= -OffscreenMargin && p.y >= -OffscreenMargin && p.x <= maxX && p.y <= maxY)
                    leaves[write++] = leaves[read];
            }

            if (write != leafCount)
            {
                leafCount = write;
                leavesDirty = true;
            }
        }

        private void RegrowLeaves()
        {
            if (!regrowStarted || regrowLeavesPerSecond <= 0f || leafCount >= leaves.Length)
                return;

            regrowAccumulator += regrowLeavesPerSecond * Time.unscaledDeltaTime;
            while (regrowAccumulator >= 1f && leafCount < leaves.Length)
            {
                regrowAccumulator -= 1f;
                Leaf leaf = MakeLeaf(new Vector2(RandomRange(4f, leavesTexture.width - 4f), RandomRange(4f, leavesTexture.height - 4f)));
                leaf.Fall = 1f;
                leaf.Phase = RandomRange(0f, Mathf.PI * 2f);
                leaves[leafCount++] = leaf;
                leavesMoving = true;
            }
        }

        private void DrawLeaves()
        {
            int w = leavesTexture.width;
            int h = leavesTexture.height;
            System.Array.Clear(leafBuffer, 0, leafBuffer.Length);

            for (int n = 0; n < leafCount; n++)
            {
                Leaf leaf = leaves[n];
                float c = Mathf.Cos(leaf.Angle);
                float s = Mathf.Sin(leaf.Angle);
                float scale = 1f + leaf.Fall * leaf.Fall * 1.4f;
                float hx = leaf.Half.x * scale;
                float hy = leaf.Half.y * scale;
                float ex = Mathf.Abs(c) * hx + Mathf.Abs(s) * hy + 0.5f;
                float ey = Mathf.Abs(s) * hx + Mathf.Abs(c) * hy + 0.5f;

                int x0 = Mathf.Max(0, Mathf.FloorToInt(leaf.Pos.x - ex));
                int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(leaf.Pos.x + ex));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(leaf.Pos.y - ey));
                int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(leaf.Pos.y + ey));

                for (int y = y0; y <= y1; y++)
                {
                    float dy = y + 0.5f - leaf.Pos.y;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - leaf.Pos.x;
                        float gu = ((dx * c + dy * s) / hx + 1f) * 0.5f;
                        float gv = ((-dx * s + dy * c) / hy + 1f) * 0.5f;
                        if (gu < 0f || gu >= 1f || gv < 0f || gv >= 1f)
                            continue;

                        Color32 g = goldPixels[(int)(gv * goldHeight) * goldWidth + (int)(gu * goldWidth)];
                        if (g.a < 128)
                            continue;

                        float shading = 0.35f + 1.3f * (g.r + g.g + g.b) / 765f;
                        float k = leaf.Shade * 255f;
                        leafBuffer[y * w + x] = new Color32(
                            (byte)Mathf.Clamp(Mathf.Lerp(g.r / 255f, leaf.Tint.r * shading, goldLeafTint) * k, 0f, 255f),
                            (byte)Mathf.Clamp(Mathf.Lerp(g.g / 255f, leaf.Tint.g * shading, goldLeafTint) * k, 0f, 255f),
                            (byte)Mathf.Clamp(Mathf.Lerp(g.b / 255f, leaf.Tint.b * shading, goldLeafTint) * k, 0f, 255f),
                            255);
                    }
                }
            }

            leavesTexture.SetPixels32(leafBuffer);
            leavesTexture.Apply(false);
            leavesDirty = false;
        }

        private float CoverageUnder(RectTransform target)
        {
            target.GetWorldCorners(corners);
            Rect r = leavesRect.rect;
            int w = leavesTexture.width;
            int h = leavesTexture.height;
            Vector3 min = leavesRect.InverseTransformPoint(corners[0]);
            Vector3 max = leavesRect.InverseTransformPoint(corners[2]);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - r.xMin) / r.width * w), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - r.xMin) / r.width * w), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((min.y - r.yMin) / r.height * h), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((max.y - r.yMin) / r.height * h), 0, h - 1);

            int covered = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (leafBuffer[y * w + x].a > 0)
                        covered++;
                }
            }
            return (float)covered / ((x1 - x0 + 1) * (y1 - y0 + 1));
        }

        private void HandleConfirm()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool confirm =
                (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            if (confirm && !items.Exists(i => i.Revealed))
                StartCoroutine(Gust(null));
        }

        private void HandleDoubleTap()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null)
                return;

            var primary = touch.primaryTouch;
            Vector2 position = primary.position.ReadValue();
            float now = Time.unscaledTime;
            float slop = Screen.height * 0.04f;

            if (primary.press.wasPressedThisFrame)
            {
                tapStartTime = now;
                tapStartPosition = position;
            }

            if (!primary.press.wasReleasedThisFrame || now - tapStartTime > 0.25f || (position - tapStartPosition).magnitude > slop)
                return;

            bool secondTap = now - lastTapTime <= doubleTapTime && (position - lastTapPosition).magnitude <= slop * 2f;
            if (secondTap && !items.Exists(i => i.Revealed && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)i.transform, position, null)))
            {
                lastTapTime = -10f;
                StartCoroutine(Gust(null, false));
                return;
            }

            lastTapTime = now;
            lastTapPosition = position;
        }

        private IEnumerator Gust(System.Action onComplete, bool selectFirstEntry = true)
        {
            inputLocked = true;
            gustActive = true;
            if (gustClip != null)
                sfxSource.PlayOneShot(gustClip, gustVolume);
            FadeOutHint(0.3f);

            float w = leavesTexture.width;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / gustDuration)
            {
                float frontX = Mathf.Lerp(-0.1f * w, 1.2f * w, t * t);
                for (int n = 0; n < leafCount; n++)
                {
                    if (leaves[n].Pos.x > frontX || leaves[n].Vel.x >= 200f)
                        continue;
                    leaves[n].Vel = new Vector2(RandomRange(320f, 520f), RandomRange(-70f, 70f));
                    leaves[n].Spin = RandomRange(-14f, 14f);
                    leaves[n].Fall = 0f;
                    leavesMoving = true;
                }
                yield return null;
            }

            for (float wait = 0f; leafCount > 0 && wait < 1f; wait += Time.unscaledDeltaTime)
                yield return null;

            leafCount = 0;
            leavesMoving = false;
            gustActive = false;
            leavesDirty = true;
            hasSwept = true;
            StartRegrow();
            inputLocked = false;

            if (onComplete != null)
            {
                onComplete();
                yield break;
            }

            yield return null;
            if (selectFirstEntry && items.Count > 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(items[0].gameObject);
        }

        private void StartNewGame()
        {
            if (starting || inputLocked)
                return;

            if (string.IsNullOrEmpty(newGameScene) || !Application.CanStreamedLevelBeLoaded(newGameScene))
            {
                statusText.text = $"SCENE '{newGameScene}' IS NOT IN THE BUILD SETTINGS.";
                Debug.LogError($"MainMenuController: add '{newGameScene}' to File > Build Settings, or fix New Game Scene.");
                return;
            }

            starting = true;
            StartCoroutine(Gust(() => SceneManager.LoadScene(newGameScene)));
        }

        private void ContinueGame()
        {
            string level = LevelProgress.Autosave;
            if (starting || inputLocked || !LevelProgress.CanLoad(level))
                return;

            starting = true;
            StartCoroutine(Gust(() => SceneManager.LoadScene(level)));
        }

        private void LoadLevel(string level)
        {
            if (starting || !LevelProgress.CanLoad(level))
                return;

            starting = true;
            SceneManager.LoadScene(level);
        }

        private void OpenPanel(System.Action open)
        {
            if (inputLocked)
                return;
            inputLocked = true;
            SetFloorWritingVisible(false);
            statusText.text = string.Empty;
            open();
        }

        private void ShowLoadMenu() => OpenPanel(() =>
        {
            RefreshLoadAvailability();
            loadStatusText.text = "SELECT A SAVE TO LOAD. CURRENT PROGRESS WILL BE REPLACED.";
            loadPanel.SetActive(true);
        });

        private void ShowOptions() => OpenPanel(() =>
        {
            optionsPanel.Show();
            optionsRoots.ForEach(StylePanelTexts);
        });

        private void ShowCredits() => OpenPanel(() =>
        {
            creditsPanel.SetActive(true);
            creditsRoutine = StartCoroutine(RunCredits());
        });

        private void ShowMainMenu()
        {
            if (creditsRoutine != null)
                StopCoroutine(creditsRoutine);
            creditsRoutine = null;

            creditsPanel.SetActive(false);
            loadPanel.SetActive(false);
            optionsPanel.Hide();
            SetFloorWritingVisible(true);
            inputLocked = false;
            RefreshContinueAvailability();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetFloorWritingVisible(bool visible)
        {
            if (logoObject != null)
                logoObject.SetActive(visible);
            menuGroup.SetActive(visible);
            leavesRect.gameObject.SetActive(visible);
            hintGroup.gameObject.SetActive(visible);
        }

        private IEnumerator RunCredits()
        {
            Canvas.ForceUpdateCanvases();
            float textHeight = creditsScroll.GetComponent<TMP_Text>().preferredHeight;
            creditsScroll.sizeDelta = new Vector2(creditsScroll.sizeDelta.x, textHeight);
            float y = -450f;
            creditsScroll.anchoredPosition = new Vector2(0f, y);
            yield return null;

            while (true)
            {
                y += 60f * Time.unscaledDeltaTime;
                if (y > textHeight + 450f)
                    y = -450f;
                creditsScroll.anchoredPosition = new Vector2(0f, y);

                if ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                    (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) ||
                    (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                    (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame)))
                {
                    creditsRoutine = null;
                    ShowMainMenu();
                    yield break;
                }
                yield return null;
            }
        }

        private void RefreshContinueAvailability() =>
            continueItem?.SetAvailable(LevelProgress.CanLoad(LevelProgress.Autosave));

        private void RefreshLoadAvailability()
        {
            SetSlot(autosaveButton, autosaveLabel, "AUTOSAVE", LevelProgress.Autosave);
            foreach (var (number, button, label) in slots)
                SetSlot(button, label, $"SLOT {number:D2}", LevelProgress.Slot(number));
        }

        private static void SetSlot(Button button, TMP_Text label, string name, string level)
        {
            button.interactable = LevelProgress.CanLoad(level);
            label.color = button.interactable ? PanelTextColor : PanelTextDisabledColor;
            label.text = $"{name}  \u2014  {(string.IsNullOrEmpty(level) ? "EMPTY" : level.ToUpperInvariant())}";
        }

        private void StartAudio()
        {
            if (menuMusic != null)
                AudioManager.Instance?.PlayMusic(menuMusic, true, musicVolume);

            AudioSource wind = CreateSource("Wind", windAmbience, windVolume);
            if (windAmbience != null)
                wind.Play();
            sweepSource = CreateSource("Sweep", sweepLoop != null ? sweepLoop : Resources.Load<AudioClip>(SweepSoundPath), 0f);
            sfxSource = CreateSource("SFX", null, 1f);
            entrySource = CreateSource("Entry Sounds", null, 1f);
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip, float volume)
        {
            AudioSource source = new GameObject(sourceName).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = clip != null;
            source.volume = volume;
            source.clip = clip;
            return source;
        }

        internal void PlayEntrySound(int kind)
        {
            AudioClip clip = kind == 0 ? entryEnterSound : kind == 1 ? entryExitSound : entryClickSound;
            if (clip == null || entrySource == null)
                return;
            entrySource.pitch = Random.Range(entrySoundPitch.x, entrySoundPitch.y);
            entrySource.PlayOneShot(clip, entrySoundVolume);
        }

        private void UpdateSweepAudio()
        {
            if (sweepSource.clip == null)
                return;

            float speed01 = Mathf.Clamp01(lastStrokeSpeed / sweepFullSpeed);
            float target = !inputLocked && Time.unscaledTime - lastStrokeTime < 0.1f
                ? sweepVolume * Mathf.Lerp(0.3f, 1f, speed01) * Mathf.Lerp(sweepBareWoodVolume, 1f, Mathf.Clamp01(pushedThisStroke / 40f))
                : 0f;

            sweepSource.volume = Mathf.MoveTowards(sweepSource.volume, target, Time.unscaledDeltaTime * (target > sweepSource.volume ? 8f : 3f));
            sweepSource.pitch = Mathf.MoveTowards(sweepSource.pitch, Mathf.Lerp(sweepPitch.x, sweepPitch.y, speed01), Time.unscaledDeltaTime * 1.5f);

            if (target > 0f && !sweepSource.isPlaying)
            {
                sweepSource.time = Random.Range(0f, sweepSource.clip.length * 0.9f);
                sweepSource.Play();
            }
            else if (target <= 0f && sweepSource.isPlaying && sweepSource.volume <= 0.001f)
            {
                sweepSource.Stop();
            }
        }

        private void ApplyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            Texture2D texture = Resources.Load<Texture2D>(CursorPath);
            if (texture == null)
            {
                Cursor.visible = true;
                return;
            }

            if (cursorCanvas == null)
            {
                RectTransform root = CreateCanvas("Cursor", 32000, false);
                cursorCanvas = root.GetComponent<Canvas>();
                cursorRect = NewRect("Broom", root, typeof(RawImage));
                cursorRect.anchorMin = cursorRect.anchorMax = Vector2.zero;
                cursorImage = cursorRect.GetComponent<RawImage>();
                cursorImage.raycastTarget = false;
                cursorImage.texture = texture;
            }

            cursorRect.sizeDelta = new Vector2(cursorSize * texture.width / texture.height, cursorSize);
            cursorRect.pivot = new Vector2(Mathf.Clamp01(cursorHotspot.x / texture.width), Mathf.Clamp01(1f - cursorHotspot.y / texture.height));
            cursorCanvas.gameObject.SetActive(true);
            Cursor.visible = false;
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            if (cursorCanvas == null || !cursorCanvas.gameObject.activeSelf || !ReadPointer(out Vector2 screen, out bool held))
                return;

            Cursor.visible = false;
            cursorImage.enabled = !touchDevice || held;
            RectTransform canvasRect = (RectTransform)cursorCanvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                cursorRect.anchoredPosition = local - canvasRect.rect.min;

            float dt = Time.unscaledDeltaTime;
            bool pressed = held && !inputLocked;
            float speed01 = Mathf.Clamp01(lastStrokeSpeed / sweepFullSpeed);
            float target = 0f;
            if (pressed && Time.unscaledTime - lastStrokeTime < 0.15f)
            {
                cursorSwingPhase += dt * Mathf.PI * 2f * Mathf.Lerp(cursorSwingRate.x, cursorSwingRate.y, speed01);
                target = Mathf.Sin(cursorSwingPhase) * cursorSwingAngle * Mathf.Lerp(0.5f, 1f, speed01)
                    - lastStrokeDir.x * cursorLeanAngle * Mathf.Lerp(0.4f, 1f, speed01);
            }

            cursorAngle = Mathf.Lerp(cursorAngle, target, 1f - Mathf.Exp(-14f * dt));
            cursorSquash = Mathf.Lerp(cursorSquash, pressed ? cursorPressSquash : 0f, 1f - Mathf.Exp(-20f * dt));
            cursorRect.localEulerAngles = new Vector3(0f, 0f, cursorAngle);
            cursorRect.localScale = new Vector3(1f + cursorSquash * 0.5f, 1f - cursorSquash, 1f);
        }

        private RectTransform CreateCanvas(string canvasName, int order, bool raycaster)
        {
            var go = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            if (raycaster)
                go.AddComponent<GraphicRaycaster>();

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceSize;
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        private static RectTransform NewRect(string objectName, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            foreach (System.Type component in components)
                go.AddComponent(component);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static RectTransform Place(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string objectName, TMP_FontAsset font, float fontSize, Vector2 position, Vector2 size)
        {
            TextMeshProUGUI text = Place(NewRect(objectName, parent, typeof(TextMeshProUGUI)), size, position).GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(Transform parent, Sprite sprite)
        {
            Image image = Stretch(NewRect("Background", parent, typeof(Image))).GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static void CreateBorder(Transform parent, float width, Color color)
        {
            RectTransform frame = Stretch(NewRect("Border", parent));
            AddLine(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, width), color);
            AddLine(frame, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, width), color);
            AddLine(frame, Vector2.zero, new Vector2(0f, 1f), new Vector2(width, 0f), color);
            AddLine(frame, new Vector2(1f, 0f), Vector2.one, new Vector2(width, 0f), color);
        }

        private static void AddLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
        {
            RectTransform line = NewRect("Line", parent, typeof(Image));
            line.anchorMin = anchorMin;
            line.anchorMax = anchorMax;
            line.pivot = anchorMin;
            line.sizeDelta = size;
            line.anchoredPosition = Vector2.zero;
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void EnsureCamera()
        {
            if (FindAnyObjectByType<Camera>() != null)
                return;

            Camera menuCamera = new GameObject("Menu Camera").AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = Color.black;
            menuCamera.cullingMask = 0;
        }
    }

    internal sealed class FloorMenuItem : Selectable, IPointerClickHandler, ISubmitHandler
    {
        private const float MaxClickTravel = 20f;
        private const float UnavailableAlpha = 0.4f;

        private MainMenuController menu;
        private TMP_Text label;
        private RectTransform visual;
        private CanvasGroup visualGroup;
        private Graphic background;
        private CrumplePaper paper;
        private System.Action action;
        private Coroutine paperRoutine;
        private bool lit;
        private bool available = true;

        public bool Revealed { get; private set; }

        public void Init(MainMenuController owner, TMP_Text text, RectTransform visualRoot, CanvasGroup group, Graphic backgroundGraphic, System.Action onChosen)
        {
            menu = owner;
            label = text;
            visual = visualRoot;
            visualGroup = group;
            background = backgroundGraphic;
            paper = background as CrumplePaper;
            action = onChosen;
            transition = Transition.None;
            interactable = false;
            HideBackground();
        }

        public void SetRevealed(bool revealed)
        {
            if (Revealed == revealed)
                return;
            Revealed = revealed;
            interactable = Revealed && available;
        }

        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
            interactable = Revealed && available;
            ApplyLook(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                (eventData.position - eventData.pressPosition).sqrMagnitude <= MaxClickTravel * MaxClickTravel)
                Choose();
        }

        public void OnSubmit(BaseEventData eventData) => Choose();

        private void Choose()
        {
            if (!Revealed || !available || !IsInteractable())
                return;
            menu.PlayEntrySound(2);
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
            HideBackground();
        }

        private void ApplyLook(bool active)
        {
            if (menu == null)
                return;

            bool nowLit = active && available && Revealed;
            if (nowLit != lit)
            {
                lit = nowLit;
                menu.PlayEntrySound(lit ? 0 : 1);
                if (paper != null && menu.entryBackgroundOnHover)
                {
                    if (paperRoutine != null)
                        StopCoroutine(paperRoutine);
                    paperRoutine = isActiveAndEnabled ? StartCoroutine(AnimatePaper(lit)) : null;
                }
            }

            label.color = lit ? menu.entryHighlightColor : menu.inkColor;
            if (background != null)
                background.color = lit ? menu.entryBackgroundHighlightColor : menu.entryBackgroundColor;
            visualGroup.alpha = available ? 1f : UnavailableAlpha;
            visual.localScale = Vector3.one * (lit ? menu.entryHighlightScale : 1f);
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
            paper.gameObject.SetActive(true);
            float from = paper.Crumple;
            float to = open ? 0f : 1f;
            if (Mathf.Abs(from - to) > 0.999f)
                paper.NewCreases();

            float duration = Mathf.Max(0.01f, open ? menu.entryBackgroundUnfoldTime : menu.entryBackgroundCrumpleTime);
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
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class CrumplePaper : MaskableGraphic
    {
        private const int Columns = 18;
        private const int Rows = 6;

        private readonly List<UIVertex> triangles = new List<UIVertex>(Columns * Rows * 6);
        private readonly Vector2[] quad = new Vector2[4];
        private readonly Vector2[] quadUv = new Vector2[4];
        private Sprite sprite;
        private float crumple;
        private float seed;
        private float creaseSeed;

        public Sprite Sprite
        {
            get => sprite;
            set { sprite = value; SetAllDirty(); }
        }

        public float Crumple
        {
            get => crumple;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, crumple))
                    return;
                crumple = v;
                SetVerticesDirty();
            }
        }

        public float Seed
        {
            get => seed;
            set { seed = creaseSeed = value; SetVerticesDirty(); }
        }

        public void NewCreases()
        {
            creaseSeed = seed + Random.value * 50f;
            SetVerticesDirty();
        }

        public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f)
                return;

            Vector4 outer = new Vector4(0f, 0f, 1f, 1f);
            Vector4 inner = outer;
            Vector4 border = Vector4.zero;
            if (sprite != null)
            {
                outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                inner = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);
                border = sprite.border * (100f / sprite.pixelsPerUnit);
                float sx = Mathf.Min(1f, r.width / Mathf.Max(0.001f, border.x + border.z));
                float sy = Mathf.Min(1f, r.height / Mathf.Max(0.001f, border.y + border.w));
                border = new Vector4(border.x * sx, border.y * sy, border.z * sx, border.w * sy);
            }

            List<float> xs = GridLines(r.width, border.x, border.z, Columns);
            List<float> ys = GridLines(r.height, border.y, border.w, Rows);

            float c = crumple;
            float wrinkle = Mathf.Sin(c * Mathf.PI);
            float squeeze = c * c * (3f - 2f * c);
            float size = Mathf.Min(r.width, r.height);
            float fade = Mathf.Clamp01((c - 0.88f) / 0.12f);
            float alpha = 1f - fade * fade * (3f - 2f * fade);
            Color32 baseColor = color;

            triangles.Clear();
            for (int j = 0; j < ys.Count - 1; j++)
            {
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        float x = xs[i + (k & 1)];
                        float y = ys[j + (k >> 1)];
                        quadUv[k] = new Vector2(
                            MapAxis(x, r.width, border.x, border.z, outer.x, inner.x, inner.z, outer.z),
                            MapAxis(y, r.height, border.y, border.w, outer.y, inner.y, inner.w, outer.w));
                        quad[k] = Deform(new Vector2(r.xMin + x, r.yMin + y), r, size, squeeze, wrinkle);
                    }

                    Color32 a = Shade(baseColor, Facet(i, j, 0) * c, alpha);
                    Color32 b = Shade(baseColor, Facet(i, j, 1) * c, alpha);
                    AddTriangle(quad[0], quad[2], quad[3], quadUv[0], quadUv[2], quadUv[3], a);
                    AddTriangle(quad[0], quad[3], quad[1], quadUv[0], quadUv[3], quadUv[1], b);
                }
            }
            vh.AddUIVertexTriangleStream(triangles);
        }

        private static List<float> GridLines(float length, float startBorder, float endBorder, int divisions)
        {
            var lines = new List<float>(divisions + 3);
            for (int k = 0; k <= divisions; k++)
                lines.Add(length * k / divisions);
            if (startBorder > 0f && startBorder < length)
                lines.Add(startBorder);
            if (endBorder > 0f && endBorder < length)
                lines.Add(length - endBorder);
            lines.Sort();
            for (int k = lines.Count - 1; k > 0; k--)
            {
                if (lines[k] - lines[k - 1] < 0.01f)
                    lines.RemoveAt(k);
            }
            return lines;
        }

        private static float MapAxis(float pos, float length, float b0, float b1, float o0, float i0, float i1, float o1)
        {
            if (b0 > 0f && pos <= b0)
                return Mathf.Lerp(o0, i0, pos / b0);
            if (b1 > 0f && pos >= length - b1)
                return Mathf.Lerp(i1, o1, (pos - (length - b1)) / b1);
            float middle = length - b0 - b1;
            return middle <= 0f
                ? Mathf.Lerp(o0, o1, pos / length)
                : Mathf.Lerp(b0 > 0f ? i0 : o0, b1 > 0f ? i1 : o1, (pos - b0) / middle);
        }

        private Vector2 Deform(Vector2 p, Rect r, float size, float squeeze, float wrinkle)
        {
            if (squeeze <= 0f && wrinkle <= 0f)
                return p;

            Vector2 centre = r.center;
            float u = (p.x - centre.x) / (r.width * 0.5f);
            float v = (p.y - centre.y) / (r.height * 0.5f);

            float lump = 0.75f + 0.5f * Mathf.PerlinNoise(u * 1.7f + creaseSeed, v * 1.7f + creaseSeed * 0.7f);
            Vector2 ball = centre + new Vector2(u * Mathf.Sqrt(1f - v * v * 0.5f), v * Mathf.Sqrt(1f - u * u * 0.5f)) * size * 0.42f * lump;

            float freq = Mathf.Lerp(2.2f, 4.5f, squeeze);
            Vector2 fold = new Vector2(
                Mathf.PerlinNoise(u * freq + creaseSeed * 1.3f, v * freq * 0.6f + 11.1f) - 0.5f,
                Mathf.PerlinNoise(u * freq * 0.6f + 23.7f, v * freq + creaseSeed * 0.9f) - 0.5f) * size * 0.9f * wrinkle;

            float edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
            return Vector2.Lerp(p, ball, Mathf.Clamp01(squeeze * (0.8f + 0.4f * edge))) + fold;
        }

        private float Facet(int i, int j, int half)
        {
            float n = Mathf.PerlinNoise(i * 0.83f + creaseSeed * 2.1f + half * 0.37f, j * 0.91f + creaseSeed + half * 0.53f);
            float h = Mathf.Repeat(Mathf.Sin(i * 12.9898f + j * 78.233f + half * 37.719f + creaseSeed) * 43758.5453f, 1f);
            return Mathf.Clamp((n - 0.5f) * 2.4f + (h - 0.5f) * 0.6f, -1f, 1f);
        }

        private static Color32 Shade(Color32 baseColor, float crease, float alpha)
        {
            float k = 1f + crease * 0.45f;
            return new Color32(
                (byte)Mathf.Clamp(baseColor.r * k, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.g * k, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.b * k, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.a * alpha, 0f, 255f));
        }

        private void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Color32 col)
        {
            triangles.Add(Vertex(a, uvA, col));
            triangles.Add(Vertex(b, uvB, col));
            triangles.Add(Vertex(c, uvC, col));
        }

        private static UIVertex Vertex(Vector2 position, Vector2 uv, Color32 col)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            vertex.color = col;
            return vertex;
        }
    }
}
