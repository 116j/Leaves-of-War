using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Leaves of War — title screen.
    ///
    /// A wooden floor seen from above, covered in autumn leaves. The logo and the
    /// standard menu entries (New Game, Continue, Load Game, Options, Credits,
    /// Quit) are written in ink on the floorboards, UNDER the leaves. The player
    /// sweeps the leaves away with the broom (hold the left mouse button and
    /// move) and clicks an entry once it is uncovered. From the first stroke
    /// on, small golden leaves keep falling from above, one at a time, and
    /// settle on the floor.
    ///
    /// Every leaf, the starting carpet included, is the golden leaf image with
    /// its own length, width, brightness and colour (the colours come from the
    /// leaf texture). Every leaf is a real, separate object: the broom never deletes a leaf.
    /// It pushes the leaves ahead of its head (they pile up and slide on with
    /// their own momentum) and some slip off the ends of the head. A leaf is
    /// removed only once it has been pushed completely off screen.
    ///
    /// Keyboard / gamepad: Enter, Space or the South button blows all the leaves
    /// away at once, then the menu can be navigated normally.
    ///
    /// Resources used (all optional, with fallbacks):
    ///   Resources/MainMenu/Floor          floor texture (tileable)
    ///   Resources/MainMenu/Leaves         colour source for the leaves (Read/Write Enabled)
    ///   Resources/MainMenu/GoldenLeaf     single golden leaf, PNG with transparency (Read/Write Enabled)
    ///   Resources/MainMenu/Logo           logo PNG with transparency
    ///   Resources/MainMenu/Credits        credits text (TextAsset, .txt)
    ///   Resources/UI/CustomCursor         broom cursor (Texture Type: Default, Point filter)
    ///   Resources/Fonts/ErraticCursive    TMP font asset for the handwritten entries
    ///   Resources/Fonts/OSerif            TMP font asset for readable text
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string FloorPath = "MainMenu/Floor";
        private const string LeavesPath = "MainMenu/Leaves";
        private const string GoldenLeafPath = "MainMenu/GoldenLeaf";
        private const string LogoPath = "MainMenu/Logo";
        private const string CreditsPath = "MainMenu/Credits";
        private const string SweepSoundPath = "MainMenu/Sweep";
        private const string CursorResourcePath = "UI/CustomCursor";
        private const string HandwritingFontPath = "Fonts/ErraticCursive";
        private const string ReadableFontPath = "Fonts/OSerif";

        private static readonly Color PanelTextColor = new Color(0.9f, 0.86f, 0.74f);
        private static readonly Color PanelTextDisabledColor = new Color(0.55f, 0.53f, 0.47f);
        private static readonly Color OverlayTextColor = new Color(0.96f, 0.93f, 0.88f, 1f);

        [Header("New Game")]
        [Tooltip("Scene loaded by NEW GAME (the first level). Must be added to File > Build Settings. The player is always Giuseppe Ungaretti: there is no name selection.")]
        [SerializeField] private string newGameScene = "L1_Milano";

        [Header("Menu Entries")]
        [Tooltip("Show CREDITS among the entries written on the floor.")]
        [SerializeField] private bool showCredits = true;

        [Header("Look")]
        [Tooltip("Colour of the floor when no Resources/MainMenu/Floor texture is found.")]
        [SerializeField] private Color fallbackFloorColor = new Color(0.36f, 0.25f, 0.16f, 1f);
        [Tooltip("How many times the floor texture repeats across the screen width.")]
        [SerializeField, Min(0.25f)] private float floorTiling = 2f;
        [Tooltip("How many times the leaf texture repeats across the screen width.")]
        [SerializeField, Min(0.25f)] private float leavesTiling = 3f;
        [Tooltip("Width of the logo on a 1920x1080 screen. Height follows the image's own proportions.")]
        [SerializeField, Min(100f)] private float logoWidth = 900f;

        [Header("Menu Entries Style")]
        [Tooltip("Font of the entries (TMP Font Asset). Empty = Resources/Fonts/ErraticCursive.")]
        [SerializeField] private TMP_FontAsset entryFont;
        [Tooltip("EASIEST WAY: drag the font FILE here (.ttf / .otf). The game builds the TextMeshPro font from it, with every letter. Wins over Entry Font, but if Entry Font is also set, its material look (Outline, Underlay, Glow...) is copied over.")]
        [SerializeField] private Font entryFontFile;
        [Tooltip("Write the entries in bold.")]
        [SerializeField] private bool entryBold = false;
        [Tooltip("Size of the menu entries.")]
        [SerializeField, Min(10f)] private float menuWordSize = 64f;
        [Tooltip("Colour of the entries (ink on the floor, #1E2A3A).")]
        [SerializeField] private Color inkColor = new Color(0.118f, 0.165f, 0.227f, 1f);
        [Tooltip("Colour of an entry under the mouse or selected with keyboard / gamepad.")]
        [SerializeField] private Color entryHighlightColor = new Color(0.077f, 0.107f, 0.148f, 1f);
        [Tooltip("How much an entry grows when highlighted (1 = no change).")]
        [SerializeField, Range(1f, 1.5f)] private float entryHighlightScale = 1.08f;
        [Tooltip("Outline around the letters of the entries (0 = none, 0.1-0.3 = thin).")]
        [SerializeField, Range(0f, 1f)] private float entryTextOutlineWidth = 0f;
        [SerializeField] private Color entryTextOutlineColor = Color.black;
        [Tooltip("Size of each entry (clickable area and background), on a 1920x1080 screen.")]
        [SerializeField] private Vector2 entrySize = new Vector2(520f, 80f);
        [Tooltip("Vertical distance between one entry and the next.")]
        [SerializeField, Min(10f)] private float entrySpacing = 88f;
        [Tooltip("Draw a background behind every entry.")]
        [SerializeField] private bool useEntryBackground = false;
        [Tooltip("Background image shared by all entries (Texture Type: Sprite). Empty = plain colour. If the sprite has borders set in the Sprite Editor, it is 9-sliced and the corners do not stretch.")]
        [SerializeField] private Sprite entryBackground;
        [Tooltip("OPTIONAL: a different background for each entry, in menu order: New Game, Continue, Load Game, Options, Credits, Quit. An empty slot uses the shared background.")]
        [SerializeField] private Sprite[] entryBackgroundOverrides = new Sprite[0];
        [Tooltip("Colour / tint of the background (alpha = transparency).")]
        [SerializeField] private Color entryBackgroundColor = new Color(0.9f, 0.85f, 0.72f, 0.85f);
        [Tooltip("Colour / tint of the background when the entry is highlighted.")]
        [SerializeField] private Color entryBackgroundHighlightColor = new Color(1f, 0.95f, 0.82f, 0.95f);
        [Tooltip("Thickness of the border around each entry, in pixels (0 = no border).")]
        [SerializeField, Min(0f)] private float entryBorderWidth = 0f;
        [SerializeField] private Color entryBorderColor = new Color(0.118f, 0.165f, 0.227f, 1f);

        [Header("Panels Style")]
        [Tooltip("Font of everything else in the menu: Options, Key Bindings, Load Game, Credits and the status messages (TMP Font Asset). Empty = Resources/Fonts/OSerif.")]
        [SerializeField] private TMP_FontAsset panelFont;
        [Tooltip("EASIEST WAY: drag the font FILE here (.ttf / .otf). The game builds the TextMeshPro font from it, with every letter. Wins over Panel Font, but if Panel Font is also set, its material look (Outline, Underlay, Glow...) is copied over.")]
        [SerializeField] private Font panelFontFile;
        [Tooltip("Write Options, Key Bindings, Load Game, Credits and the status messages in bold.")]
        [SerializeField] private bool panelBold = false;

        [Header("Hint Text Style")]
        [Tooltip("The instruction shown at the start, at the bottom of the screen.")]
        [SerializeField, TextArea(1, 3)] private string hintMessage = "Hold the left mouse button and move to sweep the leaves  ·  Enter to blow them away";
        [Tooltip("Font of the hint (TMP Font Asset). Empty = the Panel Font (or Resources/Fonts/OSerif).")]
        [SerializeField] private TMP_FontAsset hintFont;
        [Tooltip("EASIEST WAY: drag the font FILE here (.ttf / .otf). Wins over Hint Font, but if Hint Font is also set, its material look (Outline, Underlay, Glow...) is copied over.")]
        [SerializeField] private Font hintFontFile;
        [Tooltip("Write the hint in bold.")]
        [SerializeField] private bool hintBold = false;
        [SerializeField, Min(8f)] private float hintSize = 26f;
        [SerializeField] private Color hintColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);
        [Tooltip("Position of the hint on a 1920x1080 screen (0,0 = centre).")]
        [SerializeField] private Vector2 hintPosition = new Vector2(0f, -490f);
        [Tooltip("Outline around the letters of the hint (0 = none, 0.1-0.3 = thin).")]
        [SerializeField, Range(0f, 1f)] private float hintTextOutlineWidth = 0f;
        [SerializeField] private Color hintTextOutlineColor = Color.black;
        [Tooltip("Draw a background behind the hint.")]
        [SerializeField] private bool useHintBackground = false;
        [Tooltip("Background image of the hint (Texture Type: Sprite). Empty = plain colour. 9-sliced if the sprite has borders.")]
        [SerializeField] private Sprite hintBackground;
        [SerializeField] private Color hintBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        [Tooltip("Space between the hint text and the edge of its background (x = sides, y = top/bottom).")]
        [SerializeField] private Vector2 hintPadding = new Vector2(28f, 12f);
        [Tooltip("Thickness of the border around the hint, in pixels (0 = no border).")]
        [SerializeField, Min(0f)] private float hintBorderWidth = 0f;
        [SerializeField] private Color hintBorderColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);

        [Header("Leaves")]
        [Tooltip("Resolution of the leaf layer. Low values give the PSX look and keep it fast in WebGL.")]
        [SerializeField] private Vector2Int maskResolution = new Vector2Int(320, 180);
        [Tooltip("Distance between leaves in the starting carpet, in leaf-layer pixels. Smaller = thicker carpet, but more leaves to draw (slower in WebGL).")]
        [SerializeField, Range(2f, 10f)] private float leafSpacing = 4.5f;
        [Tooltip("ONLY IF THE GOLDEN LEAF IMAGE IS MISSING: half length of the fallback leaf (min, max), in leaf-layer pixels.")]
        [SerializeField] private Vector2 leafHalfLength = new Vector2(3f, 4.5f);
        [Tooltip("ONLY IF THE GOLDEN LEAF IMAGE IS MISSING: half width of the fallback leaf (min, max), in leaf-layer pixels.")]
        [SerializeField] private Vector2 leafHalfWidth = new Vector2(1.8f, 2.8f);
        [Tooltip("A menu entry becomes clickable when the share of its area still covered by leaves falls below this value (0 = clean, 1 = fully covered).")]
        [SerializeField, Range(0.05f, 0.9f)] private float revealThreshold = 0.25f;
        [Tooltip("Golden leaves falling per second, starting from the first broom stroke. 0 = once swept, the floor stays clean.")]
        [SerializeField, Min(0f)] private float regrowLeavesPerSecond = 1f;
        [Tooltip("Seconds a golden leaf takes to fall and settle on the floor.")]
        [SerializeField, Min(0.2f)] private float goldLeafFallTime = 2.5f;
        [Tooltip("Size of every leaf (starting carpet and falling ones): half of the longer side of the golden leaf image, in leaf-layer pixels (5 = about 60 px on a 1080p screen).")]
        [SerializeField, Range(1f, 20f)] private float goldLeafSize = 5f;
        [Tooltip("Random stretch of each leaf along its LENGTH (the longer side of the image): min, max. 1 = as drawn.")]
        [SerializeField] private Vector2 goldLeafLength = new Vector2(0.85f, 1.15f);
        [Tooltip("Random stretch of each leaf across its WIDTH (the shorter side of the image): min, max. 1 = as drawn.")]
        [SerializeField] private Vector2 goldLeafWidth = new Vector2(0.75f, 1.1f);
        [Tooltip("Random brightness of each leaf: min, max. 1 = as drawn.")]
        [SerializeField] private Vector2 goldLeafBrightness = new Vector2(0.8f, 1.15f);
        [Tooltip("How much each leaf is recoloured with a colour picked at random from Resources/MainMenu/Leaves. 0 = all leaves gold as drawn, 1 = fully recoloured.")]
        [SerializeField, Range(0f, 1f)] private float goldLeafTint = 0.6f;
        [Tooltip("Maximum number of golden leaves on the floor at the same time.")]
        [SerializeField, Min(0)] private int maxGoldLeaves = 1500;
        [Tooltip("Duration of the gust that blows every leaf away (Enter / Space / gamepad, and when starting a new game).")]
        [SerializeField, Min(0.1f)] private float gustDuration = 0.8f;

        [Header("Broom")]
        [Tooltip("Half width of the broom head, across the stroke, in leaf-layer pixels. 30 on a 320-wide layer is about a fifth of the screen.")]
        [SerializeField, Range(6f, 80f)] private float broomHalfWidth = 30f;
        [Tooltip("Half depth of the broom head, along the stroke, in leaf-layer pixels.")]
        [SerializeField, Range(1f, 20f)] private float broomHalfDepth = 4f;
        [Tooltip("Thickness of the pile of leaves pushed ahead of the head, in leaf-layer pixels.")]
        [SerializeField, Range(1f, 16f)] private float pileDepth = 5f;
        [Tooltip("Share of the broom speed given to the pushed leaves: they keep sliding after the broom stops. A quick flick throws them off screen.")]
        [SerializeField, Range(0f, 1.5f)] private float pushKick = 0.8f;
        [Tooltip("How quickly sliding leaves come to a stop.")]
        [SerializeField, Range(0.5f, 15f)] private float leafFriction = 4f;
        [Tooltip("Chance that a leaf near the ends of the head slips out sideways instead of being pushed ahead.")]
        [SerializeField, Range(0f, 1f)] private float endSpill = 0.35f;
        [Tooltip("Minimum mouse movement (leaf-layer pixels) to count as a stroke. The broom does nothing if the mouse stands still.")]
        [SerializeField, Range(0.05f, 3f)] private float minStroke = 0.3f;

        [Header("Audio")]
        [Tooltip("Title music (the single held piano note). Played through the AudioManager.")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
        [Tooltip("Wind through the half-open window, looping.")]
        [SerializeField] private AudioClip windAmbience;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.5f;
        [Tooltip("Broom-on-wood loop, heard only while the broom moves. If empty, Resources/MainMenu/Sweep is used.")]
        [SerializeField] private AudioClip sweepLoop;
        [Tooltip("Volume of the sweep at full speed through a thick layer of leaves.")]
        [SerializeField, Range(0f, 1f)] private float sweepVolume = 0.7f;
        [Tooltip("Volume share when the broom moves on bare wood, without pushing leaves (0 = silent, 1 = same as through leaves).")]
        [SerializeField, Range(0f, 1f)] private float sweepBareWoodVolume = 0.45f;
        [Tooltip("Broom speed (leaf-layer pixels per second) at which the sweep is at full volume. Slower strokes are quieter and lower in pitch.")]
        [SerializeField, Min(10f)] private float sweepFullSpeed = 350f;
        [Tooltip("Pitch at the slowest and at the fastest stroke.")]
        [SerializeField] private Vector2 sweepPitch = new Vector2(0.9f, 1.12f);
        [Tooltip("One-shot gust when all the leaves are blown away.")]
        [SerializeField] private AudioClip gustClip;
        [SerializeField, Range(0f, 1f)] private float gustVolume = 0.8f;

        [Header("Custom Cursor")]
        [Tooltip("Height of the broom cursor on a 1920x1080 screen (it scales with the window, like the rest of the menu). Width follows the image's proportions. The cursor is drawn by the game, so any size works, also in WebGL.")]
        [SerializeField, Min(8f)] private float cursorSize = 256f;
        [Tooltip("Click point of the cursor loaded from Resources/UI/CustomCursor, in pixels of the IMAGE, from its TOP-LEFT corner (the tip of the broom bristles). It stays right at any cursor size.")]
        [SerializeField] private Vector2 cursorHotspot = new Vector2(4f, 4f);
        [Tooltip("How far the broom handle swings back and forth while sweeping, in degrees. The broom rotates around the hotspot, so the bristles stay on the floor.")]
        [SerializeField, Range(0f, 45f)] private float cursorSwingAngle = 16f;
        [Tooltip("Swings per second: (slow stroke, fast stroke).")]
        [SerializeField] private Vector2 cursorSwingRate = new Vector2(1.2f, 2.6f);
        [Tooltip("How much the handle leans against the direction of the stroke, in degrees, like when dragging a real broom. If it leans the wrong way for your image, use a negative value.")]
        [SerializeField, Range(-45f, 45f)] private float cursorLeanAngle = 18f;
        [Tooltip("How much the broom presses down while the mouse button is held (0 = no squash).")]
        [SerializeField, Range(0f, 0.3f)] private float cursorPressSquash = 0.08f;

        // Fonts
        private TMP_FontAsset handwritingFont;
        private TMP_FontAsset readableFont;
        private TMP_FontAsset resolvedPanelFont;
        private TMP_FontAsset resolvedHintFont;

        // Floor writing
        private RectTransform leavesRect;
        private GameObject logoObject;
        private GameObject menuGroup;
        private readonly List<FloorMenuItem> items = new List<FloorMenuItem>();
        private FloorMenuItem continueItem;

        // Overlay panels
        private TMP_Text hintText;
        private CanvasGroup hintGroup;
        private Coroutine hintFade;
        private TMP_Text statusText;
        private OptionsPanel optionsPanel;
        private readonly List<GameObject> optionsRoots = new List<GameObject>();
        private float nextPanelFontCheck;
        private GameObject loadPanel;
        private TMP_Text loadStatusText;
        private TMP_Text autosaveLoadLabel;
        private Button autosaveLoadButton;
        private readonly List<ManualSlotButton> manualLoadSlots = new List<ManualSlotButton>();
        private GameObject creditsPanel;
        private RectTransform creditsScroll;

        // Leaf layer
        private struct Leaf
        {
            public Vector2 Pos;   // centre, leaf-layer pixels
            public Vector2 Vel;   // pixels per second
            public Vector2 Half;  // half length, half width
            public Vector2 Src;   // where its colours are taken from in the leaf texture
            public float Angle;   // radians
            public float Spin;    // radians per second
            public float Shade;   // brightness, so neighbouring leaves read apart
            public bool Gold;     // small golden leaf that fell while the player was idle
            public float Fall;    // 1 = just started falling, 0 = on the floor
            public float Phase;   // flutter phase while falling
            public Color Tint;    // colour taken from the starting leaves (golden leaves only)
        }

        private const float OffscreenMargin = 6f;

        private Texture2D leavesTexture;
        private Color32[] leafColors;
        private Color32[] goldPixels;
        private int goldWidth;
        private int goldHeight;
        private Color32[] leafBuffer;
        private Leaf[] leaves;
        private int leafCount;
        private System.Random leafRandom;
        private bool leavesMoving;
        private bool gustActive;
        private float regrowAccumulator;
        private bool regrowStarted;
        private bool leavesDirty;
        private float lastSweepTime;
        private bool hasSwept;
        private bool strokeActive;
        private Vector2 lastStrokePoint;
        private float lastStrokeTime = -10f;
        private float lastStrokeSpeed;
        private int pushedThisStroke;

        // State
        private bool inputLocked;
        private bool starting;
        private Coroutine creditsRoutine;

        // Audio
        private AudioSource sweepSource;
        private AudioSource sfxSource;

        private readonly Vector3[] corners = new Vector3[4];

        // Cursor drawn by the game (any size, also in WebGL)
        private Canvas cursorCanvas;
        private RectTransform cursorRect;
        private float cursorAngle;
        private float cursorSwingPhase;
        private float cursorSquash;
        private Vector2 lastStrokeDir;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            EnsureEventSystem();
            EnsureSettingsApplier();
            LoadFonts();
            BuildUi();
            StartAudio();
            lastSweepTime = Time.unscaledTime;
        }

        private void OnEnable()
        {
            ApplyCursor();
            RefreshContinueAvailability();
        }

        private void OnDisable()
        {
            AudioManager.ExistingInstance?.StopMusicIfCurrent(menuMusic);
            ResetCursor();
        }

        private void LateUpdate()
        {
            UpdateCursor();

            // Texts the options panel creates later (e.g. Key Bindings rows) get the Panel Font too.
            if ((resolvedPanelFont != null || panelBold) && Time.unscaledTime >= nextPanelFontCheck && AnyOptionsRootActive())
            {
                nextPanelFontCheck = Time.unscaledTime + 0.2f;
                ApplyPanelFontToOptions();
            }
        }

        private bool AnyOptionsRootActive()
        {
            for (int i = 0; i < optionsRoots.Count; i++)
            {
                if (optionsRoots[i] != null && optionsRoots[i].activeInHierarchy)
                    return true;
            }
            return false;
        }

        /// <summary>Forces the Panel Font on every text of the Options / Key Bindings panel.</summary>
        private void ApplyPanelFontToOptions()
        {
            if (resolvedPanelFont == null && !panelBold)
                return;

            for (int i = 0; i < optionsRoots.Count; i++)
            {
                if (optionsRoots[i] == null)
                    continue;
                TMP_Text[] texts = optionsRoots[i].GetComponentsInChildren<TMP_Text>(true);
                for (int t = 0; t < texts.Length; t++)
                {
                    if (resolvedPanelFont != null && texts[t].font != resolvedPanelFont)
                        texts[t].font = resolvedPanelFont;
                    if (panelBold)
                        SetBold(texts[t], true); // unticked: leave the panel's own bold titles alone
                }
            }
        }

        private void OnDestroy()
        {
            if (leavesTexture != null)
                Destroy(leavesTexture);
        }

        private void Update()
        {

            if (!inputLocked)
            {
                HandleMouseSweeping();
                HandleKeyboardAndGamepad();
                RegrowLeaves();
            }
            else
            {
                strokeActive = false;
            }

            SimulateLeaves(Mathf.Min(Time.unscaledDeltaTime, 0.05f));

            if (leavesDirty)
                DrawLeaves();

            UpdateItemReveal();
            UpdateSweepAudio();
        }

        // ------------------------------------------------------------------
        // UI construction
        // ------------------------------------------------------------------

        private void LoadFonts()
        {
            TMP_FontAsset readable = Resources.Load<TMP_FontAsset>(ReadableFontPath);
            TMP_FontAsset handwriting = Resources.Load<TMP_FontAsset>(HandwritingFontPath);

            if (readable == null)
                Debug.LogWarning($"MainMenuController: TMP font not found at Resources/{ReadableFontPath}; using the default TMP font.");
            if (handwriting == null)
                Debug.LogWarning($"MainMenuController: TMP font not found at Resources/{HandwritingFontPath}; using the readable font.");

            readableFont = readable != null ? ResolveFont(null, readable, "Resources/Fonts/OSerif") : TMP_Settings.defaultFontAsset;
            resolvedPanelFont = ResolveFont(panelFontFile, panelFont, "Panel Font");
            if (resolvedPanelFont != null)
                readableFont = resolvedPanelFont;

            handwritingFont = ResolveFont(entryFontFile, entryFont, "Entry Font");
            if (handwritingFont == null)
                handwritingFont = handwriting != null ? ResolveFont(null, handwriting, "Resources/Fonts/ErraticCursive") : readableFont;

            resolvedHintFont = ResolveFont(hintFontFile, hintFont, "Hint Font");
        }

        private const string FontSampleLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        /// <summary>
        /// Returns a TextMeshPro font that really contains the letters.
        /// 1. A font FILE (.ttf/.otf) is turned into a dynamic TMP font at runtime.
        /// 2. A TMP Font Asset is used as it is if it has the letters.
        /// 3. A TMP Font Asset WITHOUT letters (empty static atlas: TextMeshPro would
        ///    silently draw a fallback font) is rebuilt from its own source font, if it has one.
        /// </summary>
        private static TMP_FontAsset ResolveFont(Font file, TMP_FontAsset asset, string fieldName)
        {
            if (file != null)
            {
                TMP_FontAsset fromFile = CreateDynamicFont(file);
                if (fromFile != null)
                {
                    CopyMaterialLook(asset, fromFile);
                    return fromFile;
                }
                Debug.LogWarning($"MainMenuController ({fieldName}): could not build a font from '{file.name}'. In the font file's import settings, tick 'Include Font Data' and press Apply.");
            }

            if (asset == null)
                return null;

            if (asset.HasCharacters(FontSampleLetters, out uint[] _, false, true))
                return asset;

            Font source = asset.sourceFontFile;
            if (source != null)
            {
                TMP_FontAsset rebuilt = CreateDynamicFont(source);
                if (rebuilt != null)
                {
                    CopyMaterialLook(asset, rebuilt);
                    return rebuilt;
                }
            }

            Debug.LogWarning(
                $"MainMenuController ({fieldName}): the font asset '{asset.name}' has no letters in it, so TextMeshPro draws a fallback font instead. " +
                "Fix: drag the .ttf/.otf file into the matching 'Font File' field of the MainMenuController, " +
                "or select the font asset and set Atlas Population Mode to Dynamic with its Source Font File assigned.");
            return asset;
        }

        /// <summary>
        /// A font built at runtime gets a brand new material, which would lose the
        /// Outline / Underlay / Glow / Dilate settings made on the font asset's
        /// material. Copy them over, keeping the new font's own atlas.
        /// </summary>
        private static void CopyMaterialLook(TMP_FontAsset from, TMP_FontAsset to)
        {
            if (from == null || to == null || from.material == null || to.material == null)
                return;

            Material source = from.material;
            Material target = to.material;

            Texture atlas = target.GetTexture(ShaderUtilities.ID_MainTex);
            float width = target.GetFloat(ShaderUtilities.ID_TextureWidth);
            float height = target.GetFloat(ShaderUtilities.ID_TextureHeight);
            float gradient = target.GetFloat(ShaderUtilities.ID_GradientScale);

            target.shader = source.shader;
            target.CopyPropertiesFromMaterial(source);
            target.shaderKeywords = source.shaderKeywords; // Outline / Underlay / Glow on-off switches

            target.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            target.SetFloat(ShaderUtilities.ID_TextureWidth, width);
            target.SetFloat(ShaderUtilities.ID_TextureHeight, height);
            target.SetFloat(ShaderUtilities.ID_GradientScale, gradient);
        }

        private static TMP_FontAsset CreateDynamicFont(Font file)
        {
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(file);
            if (created == null)
                return null;
            created.name = file.name + " (runtime)";
            return created.HasCharacters(FontSampleLetters, out uint[] _, false, true) ? created : null;
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("Main Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)canvasObject.transform;

            // 1. Floor, full screen.
            CreateFloor(root);

            // 2. Logo and menu entries written on the floor, in a fixed 1920x1080 frame.
            RectTransform floorFrame = CreateFrame(root, "Floor Writing");
            logoObject = CreateLogo(floorFrame);
            menuGroup = new GameObject("Menu Entries", typeof(RectTransform));
            menuGroup.transform.SetParent(floorFrame, false);
            StretchFull((RectTransform)menuGroup.transform);

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
            // In the browser Application.Quit() does nothing: the entry is not written.
            entries.Add(("Quit", QuitGame));
#endif
            float y = 40f;
            for (int index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                FloorMenuItem item = AddItem(entry.label, MenuOrderIndex(entry.label), y, entry.action);
                if (entry.label == "Continue")
                    continueItem = item;
                y -= entrySpacing;
            }
            LinkNavigation();

            // 3. The leaf layer, on top of the writing.
            CreateLeafLayer(root);

            // 4. Overlay: hint, status and panels stay above the leaves.
            RectTransform overlay = CreateFrame(root, "Overlay");

            CreateHint(overlay);

            statusText = CreatePositionedText(overlay, "Status", readableFont, 24f, new Vector2(0f, 500f), new Vector2(1700f, 50f));
            statusText.color = OverlayTextColor;
            statusText.text = string.Empty;

            // Shared OPTIONS + KEY BINDINGS panel. BACK returns here to the main menu.
            // OptionsPanel (shared with the rest of the game) may choose its own
            // fonts: remember the objects it creates, so the Panel Font can be
            // forced on every text inside them.
            int childrenBefore = overlay.childCount;
            optionsPanel = new OptionsPanel(overlay, readableFont, readableFont, ShowMainMenu);
            for (int i = childrenBefore; i < overlay.childCount; i++)
                optionsRoots.Add(overlay.GetChild(i).gameObject);
            if (optionsRoots.Count == 0)
                Debug.LogWarning("MainMenuController: the Options panel was not built inside the menu, so the Panel Font cannot reach it.");
            ApplyPanelFontToOptions();
            optionsPanel.Hide();

            loadPanel = BuildLoadPanel(overlay);
            loadPanel.SetActive(false);

            creditsPanel = BuildCreditsPanel(overlay);
            creditsPanel.SetActive(false);

            SetBold(statusText, panelBold);
            SetBoldInChildren(loadPanel, panelBold);
            SetBoldInChildren(creditsPanel, panelBold);
            if (panelBold)
            {
                for (int i = 0; i < optionsRoots.Count; i++)
                    SetBoldInChildren(optionsRoots[i], true);
            }

            RefreshContinueAvailability();
        }

        private void CreateFloor(RectTransform parent)
        {
            var floorObject = new GameObject("Floor", typeof(RectTransform), typeof(RawImage));
            floorObject.transform.SetParent(parent, false);
            StretchFull((RectTransform)floorObject.transform);

            RawImage image = floorObject.GetComponent<RawImage>();
            image.raycastTarget = false;

            Texture2D floor = Resources.Load<Texture2D>(FloorPath);
            if (floor != null)
            {
                floor.wrapMode = TextureWrapMode.Repeat;
                image.texture = floor;
                image.uvRect = new Rect(0f, 0f, floorTiling, floorTiling * 9f / 16f);
            }
            else
            {
                image.color = fallbackFloorColor;
                Debug.LogWarning($"MainMenuController: floor texture not found at Resources/{FloorPath}; using a plain colour.");
            }
        }

        private GameObject CreateLogo(RectTransform parent)
        {
            Texture2D logo = Resources.Load<Texture2D>(LogoPath);
            if (logo != null)
            {
                var logoObj = new GameObject("Logo", typeof(RectTransform), typeof(RawImage));
                logoObj.transform.SetParent(parent, false);
                RectTransform rect = (RectTransform)logoObj.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                float height = logoWidth * logo.height / Mathf.Max(1f, logo.width);
                rect.sizeDelta = new Vector2(logoWidth, height);
                rect.anchoredPosition = new Vector2(0f, 320f);

                RawImage image = logoObj.GetComponent<RawImage>();
                image.texture = logo;
                image.raycastTarget = false;
                return logoObj;
            }

            Debug.LogWarning($"MainMenuController: logo not found at Resources/{LogoPath}; writing the title as text.");
            TMP_Text title = CreatePositionedText(parent, "Logo", handwritingFont, 140f, new Vector2(0f, 320f), new Vector2(1400f, 240f));
            title.text = "Leaves of War";
            title.color = inkColor;
            return title.gameObject;
        }

        private FloorMenuItem AddItem(string label, int orderIndex, float y, System.Action action)
        {
            var itemObject = new GameObject(label, typeof(RectTransform), typeof(Image));
            itemObject.transform.SetParent(menuGroup.transform, false);
            RectTransform rect = (RectTransform)itemObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = entrySize;
            rect.anchoredPosition = new Vector2(0f, y);

            // Invisible hit area.
            Image hitArea = itemObject.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);

            // Everything you see sits in "Visual": it grows and fades as one piece.
            var visualObject = new GameObject("Visual", typeof(RectTransform), typeof(CanvasGroup));
            visualObject.transform.SetParent(rect, false);
            RectTransform visual = (RectTransform)visualObject.transform;
            StretchFull(visual);
            CanvasGroup visualGroup = visualObject.GetComponent<CanvasGroup>();
            visualGroup.interactable = false;
            visualGroup.blocksRaycasts = false;

            Image background = null;
            if (useEntryBackground)
            {
                Sprite sprite = entryBackground;
                if (entryBackgroundOverrides != null && orderIndex >= 0 &&
                    orderIndex < entryBackgroundOverrides.Length && entryBackgroundOverrides[orderIndex] != null)
                    sprite = entryBackgroundOverrides[orderIndex];
                background = CreateBackground(visual, sprite, entryBackgroundColor);
            }

            if (entryBorderWidth > 0f)
                CreateBorder(visual, entryBorderWidth, entryBorderColor);

            TMP_Text text = CreatePositionedText(visual, "Word", handwritingFont, menuWordSize, Vector2.zero, entrySize);
            text.text = label;
            text.color = inkColor;
            ApplyTextOutline(text, entryTextOutlineWidth, entryTextOutlineColor);
            WarnIfFontLacksCharacters(handwritingFont, label, "Entry Font");
            SetBold(text, entryBold);

            FloorMenuItem item = itemObject.AddComponent<FloorMenuItem>();
            item.Init(text, action, visual, visualGroup, inkColor, entryHighlightColor, entryHighlightScale,
                background, entryBackgroundColor, entryBackgroundHighlightColor);
            items.Add(item);
            return item;
        }

        /// <summary>Position of an entry in the full list (New Game, Continue, Load Game, Options, Credits, Quit), for the per-entry backgrounds.</summary>
        private static int MenuOrderIndex(string label)
        {
            switch (label)
            {
                case "New Game": return 0;
                case "Continue": return 1;
                case "Load Game": return 2;
                case "Options": return 3;
                case "Credits": return 4;
                case "Quit": return 5;
                default: return -1;
            }
        }

        private void CreateHint(RectTransform overlay)
        {
            TMP_FontAsset font = resolvedHintFont != null ? resolvedHintFont : readableFont;

            var groupObject = new GameObject("Hint", typeof(RectTransform), typeof(CanvasGroup));
            groupObject.transform.SetParent(overlay, false);
            RectTransform groupRect = (RectTransform)groupObject.transform;
            groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = hintPosition;
            hintGroup = groupObject.GetComponent<CanvasGroup>();
            hintGroup.interactable = false;
            hintGroup.blocksRaycasts = false;

            hintText = CreatePositionedText(groupRect, "Text", font, hintSize, Vector2.zero, new Vector2(1700f, hintSize * 2f));
            hintText.text = hintMessage;
            hintText.color = hintColor;
            WarnIfFontLacksCharacters(font, hintMessage, "Hint Font");
            SetBold(hintText, hintBold);
            ApplyTextOutline(hintText, hintTextOutlineWidth, hintTextOutlineColor);

            // The box fits the text, plus padding.
            Vector2 preferred = hintText.GetPreferredValues(hintMessage, 1700f, 0f);
            Vector2 boxSize = new Vector2(Mathf.Min(preferred.x, 1700f), preferred.y) + hintPadding * 2f;
            groupRect.sizeDelta = boxSize;
            ((RectTransform)hintText.transform).sizeDelta = boxSize - hintPadding * 2f;

            if (useHintBackground)
            {
                Image background = CreateBackground(groupRect, hintBackground, hintBackgroundColor);
                background.transform.SetAsFirstSibling();
            }
            if (hintBorderWidth > 0f)
                CreateBorder(groupRect, hintBorderWidth, hintBorderColor);
        }

        private void FadeOutHint(float duration)
        {
            if (hintGroup == null || !isActiveAndEnabled)
                return;
            if (hintFade != null)
                StopCoroutine(hintFade);
            hintFade = StartCoroutine(FadeHintRoutine(duration));
        }

        private IEnumerator FadeHintRoutine(float duration)
        {
            float start = hintGroup.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                hintGroup.alpha = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            hintGroup.alpha = 0f;
            hintFade = null;
        }

        /// <summary>Background filling the parent: a sprite (9-sliced if it has borders) or a plain colour.</summary>
        private static Image CreateBackground(RectTransform parent, Sprite sprite, Color color)
        {
            var bgObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObject.transform.SetParent(parent, false);
            StretchFull((RectTransform)bgObject.transform);
            Image image = bgObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Thin frame around the parent, made of four lines (works with or without a background).</summary>
        private static void CreateBorder(RectTransform parent, float width, Color color)
        {
            var frame = new GameObject("Border", typeof(RectTransform));
            frame.transform.SetParent(parent, false);
            RectTransform frameRect = (RectTransform)frame.transform;
            StretchFull(frameRect);

            CreateLine(frameRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, width), color);
            CreateLine(frameRect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, width), color);
            CreateLine(frameRect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(width, 0f), color);
            CreateLine(frameRect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(width, 0f), color);
        }

        private static void CreateLine(RectTransform parent, string lineName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
        {
            var lineObject = new GameObject(lineName, typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)lineObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            Image image = lineObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>
        /// If a font asset has no glyphs for these letters, TextMeshPro silently
        /// draws them with a FALLBACK font: the component shows your font, but the
        /// letters you see belong to another one. This says so in the Console.
        /// </summary>
        private static void WarnIfFontLacksCharacters(TMP_FontAsset font, string sample, string fieldName)
        {
            if (font == null || string.IsNullOrEmpty(sample))
                return;

            if (!font.HasCharacters(sample, out uint[] missing, false, true) && missing != null && missing.Length > 0)
            {
                var unique = new HashSet<char>();
                foreach (uint code in missing)
                {
                    if (code != ' ')
                        unique.Add((char)code);
                }
                if (unique.Count == 0)
                    return;
                Debug.LogWarning(
                    $"MainMenuController ({fieldName}): the font asset '{font.name}' has no glyphs for \"{new string(new List<char>(unique).ToArray())}\". " +
                    "TextMeshPro is drawing them with a fallback font, so the font looks unchanged. " +
                    "Select the font asset and set Atlas Population Mode to Dynamic (with its Source Font File assigned), " +
                    "or regenerate it in Window > TextMeshPro > Font Asset Creator with Character Set: ASCII.");
            }
        }

        private static void SetBold(TMP_Text text, bool bold)
        {
            if (text == null)
                return;
            FontStyles style = text.fontStyle;
            style = bold ? style | FontStyles.Bold : style & ~FontStyles.Bold;
            if (text.fontStyle != style)
                text.fontStyle = style;
        }

        private static void SetBoldInChildren(GameObject root, bool bold)
        {
            if (root == null)
                return;
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                SetBold(texts[i], bold);
        }

        private static void ApplyTextOutline(TMP_Text text, float width, Color color)
        {
            if (width <= 0f)
                return;
            text.outlineWidth = width;
            text.outlineColor = color;
        }

        private void LinkNavigation()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = items[(i - 1 + items.Count) % items.Count];
                nav.selectOnDown = items[(i + 1) % items.Count];
                items[i].navigation = nav;
            }
        }

        private void CreateLeafLayer(RectTransform parent)
        {
            int w = Mathf.Max(16, maskResolution.x);
            int h = Mathf.Max(9, maskResolution.y);

            var leavesObject = new GameObject("Leaves", typeof(RectTransform), typeof(RawImage));
            leavesObject.transform.SetParent(parent, false);
            leavesRect = (RectTransform)leavesObject.transform;
            StretchFull(leavesRect);

            leavesTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Menu Leaf Layer"
            };

            leafColors = BuildLeafColors(w, h);
            LoadGoldenLeaf();
            leafBuffer = new Color32[w * h];
            leafRandom = new System.Random(1918);

            // The carpet: one leaf every few pixels, jittered, randomly rotated.
            // A few thinner spots show that something is written underneath.
            float spacing = Mathf.Max(2f, leafSpacing);
            var carpet = new List<Leaf>();
            for (float y = -1f; y < h + 1f; y += spacing)
            {
                for (float x = -1f; x < w + 1f; x += spacing)
                {
                    var p = new Vector2(x + RandomRange(-1f, 1f) * spacing * 0.5f, y + RandomRange(-1f, 1f) * spacing * 0.5f);
                    float thin = Mathf.PerlinNoise(p.x * 0.08f + 13.7f, p.y * 0.08f + 4.2f);
                    if (thin < 0.28f && leafRandom.NextDouble() < 0.45)
                        continue;
                    carpet.Add(MakeLeaf(p));
                }
            }

            // Shuffle the drawing order so the carpet does not look like roof tiles.
            for (int i = carpet.Count - 1; i > 0; i--)
            {
                int j = leafRandom.Next(i + 1);
                Leaf tmp = carpet[i];
                carpet[i] = carpet[j];
                carpet[j] = tmp;
            }

            leafCount = carpet.Count;
            leaves = new Leaf[leafCount + Mathf.Max(0, maxGoldLeaves)];
            carpet.CopyTo(leaves);

            RawImage image = leavesObject.GetComponent<RawImage>();
            image.texture = leavesTexture;
            image.raycastTarget = false; // clicks go through to the entries below

            DrawLeaves();
        }

        /// <summary>
        /// One leaf: the golden leaf image with its own length, width, brightness
        /// and colour. Used for the starting carpet and for the leaves that fall.
        /// </summary>
        private Leaf MakeLeaf(Vector2 position)
        {
            if (goldPixels == null)
                return MakeFallbackLeaf(position);

            float longest = Mathf.Max(goldWidth, goldHeight);
            float lengthStretch = RandomRange(goldLeafLength.x, goldLeafLength.y);
            float widthStretch = RandomRange(goldLeafWidth.x, goldLeafWidth.y);
            bool lengthAlongX = goldWidth >= goldHeight;

            return new Leaf
            {
                Pos = position,
                Vel = Vector2.zero,
                Half = new Vector2(
                    goldLeafSize * goldWidth / longest * (lengthAlongX ? lengthStretch : widthStretch),
                    goldLeafSize * goldHeight / longest * (lengthAlongX ? widthStretch : lengthStretch)),
                Src = Vector2.zero,
                Angle = RandomRange(0f, Mathf.PI * 2f),
                Spin = 0f,
                Shade = RandomRange(goldLeafBrightness.x, goldLeafBrightness.y),
                Gold = true,
                Tint = SampleCarpetColour()
            };
        }

        /// <summary>Plain pointed leaf, used only when the golden leaf image is missing.</summary>
        private Leaf MakeFallbackLeaf(Vector2 position)
        {
            return new Leaf
            {
                Pos = position,
                Vel = Vector2.zero,
                Half = new Vector2(
                    RandomRange(leafHalfLength.x, leafHalfLength.y),
                    RandomRange(leafHalfWidth.x, leafHalfWidth.y)),
                Src = new Vector2(RandomRange(0f, leavesTexture.width), RandomRange(0f, leavesTexture.height)),
                Angle = RandomRange(0f, Mathf.PI * 2f),
                Spin = 0f,
                Shade = RandomRange(0.78f, 1.08f)
            };
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)leafRandom.NextDouble() * (max - min);
        }

        private void LoadGoldenLeaf()
        {
            Texture2D gold = Resources.Load<Texture2D>(GoldenLeafPath);
            if (gold == null)
            {
                Debug.LogWarning($"MainMenuController: golden leaf not found at Resources/{GoldenLeafPath}. Using plain fallback leaves.");
                return;
            }
            if (!gold.isReadable)
            {
                Debug.LogWarning($"MainMenuController: Resources/{GoldenLeafPath} is not readable. Enable 'Read/Write' in its import settings. Using plain fallback leaves.");
                return;
            }

            goldPixels = gold.GetPixels32();
            goldWidth = gold.width;
            goldHeight = gold.height;
        }

        private Color32[] BuildLeafColors(int w, int h)
        {
            var colors = new Color32[w * h];
            Texture2D source = Resources.Load<Texture2D>(LeavesPath);

            if (source != null && source.isReadable)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Square pixels: the same tiling scale on both axes.
                        float u = Mathf.Repeat((float)x / w * leavesTiling, 1f);
                        float v = Mathf.Repeat((float)y / w * leavesTiling, 1f);
                        Color32 c = source.GetPixelBilinear(u, v);
                        c.a = 255;
                        colors[y * w + x] = c;
                    }
                }
                return colors;
            }

            if (source != null)
                Debug.LogWarning($"MainMenuController: Resources/{LeavesPath} is not readable. Enable 'Read/Write' in its import settings. Using procedural leaves.");
            else
                Debug.LogWarning($"MainMenuController: leaf texture not found at Resources/{LeavesPath}. Using procedural leaves.");

            // Procedural fallback: patches of brown, ochre and dark red.
            Color[] palette =
            {
                new Color(0.45f, 0.27f, 0.12f), new Color(0.62f, 0.42f, 0.16f),
                new Color(0.38f, 0.17f, 0.10f), new Color(0.70f, 0.55f, 0.25f),
                new Color(0.30f, 0.20f, 0.10f)
            };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    float m = Mathf.PerlinNoise(x * 0.09f + 50f, y * 0.09f + 50f);
                    int index = Mathf.Clamp((int)((n * 0.7f + m * 0.3f) * palette.Length), 0, palette.Length - 1);
                    colors[y * w + x] = palette[index];
                }
            }
            return colors;
        }

        private GameObject BuildLoadPanel(Transform parent)
        {
            GameObject panel = CreateCenteredPanel(
                parent,
                "Load Game",
                new Vector2(1360f, 900f),
                new Color(0.035f, 0.03f, 0.024f, 0.98f));

            TMP_Text header = CreatePositionedText(
                panel.transform, "Header", readableFont, 52f, new Vector2(0f, 230f), new Vector2(1000f, 72f));
            header.text = "LOAD GAME";
            header.color = new Color(0.88f, 0.84f, 0.72f);

            for (int index = 0; index < SaveGameStore.ManualSlotCount; index++)
            {
                int slotNumber = index + 1;
                float x = index % 2 == 0 ? -245f : 245f;
                float y = 130f - ((index / 2) * 65f);
                manualLoadSlots.Add(CreateLoadSlotButton(panel.transform, slotNumber, x, y));
            }

            autosaveLoadButton = CreateLoadButton(
                panel.transform,
                "Autosave",
                new Vector2(0f, -210f),
                ContinueGame);
            autosaveLoadLabel = autosaveLoadButton.GetComponentInChildren<TMP_Text>();
            autosaveLoadLabel.text = "AUTOSAVE";

            loadStatusText = CreatePositionedText(
                panel.transform, "Status", readableFont, 20f, new Vector2(0f, -295f), new Vector2(1120f, 62f));
            loadStatusText.textWrappingMode = TextWrappingModes.Normal;
            loadStatusText.color = new Color(0.73f, 0.69f, 0.57f);

            Button backButton = CreateLoadButton(
                panel.transform,
                "Back",
                new Vector2(455f, 230f),
                ShowMainMenu,
                width: 250f);
            backButton.GetComponentInChildren<TMP_Text>().text = "BACK";
            return panel;
        }

        private ManualSlotButton CreateLoadSlotButton(Transform parent, int slotNumber, float x, float y)
        {
            Button button = CreateLoadButton(
                parent,
                $"Slot {slotNumber:D2}",
                new Vector2(x, y),
                () => LoadManualSlot(slotNumber));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            return new ManualSlotButton(slotNumber, button, label);
        }

        private Button CreateLoadButton(
            Transform parent,
            string objectName,
            Vector2 position,
            UnityEngine.Events.UnityAction action,
            float width = 440f)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 58f);
            rect.anchoredPosition = position;

            buttonObject.GetComponent<Image>().color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            colors.disabledColor = new Color(0.34f, 0.32f, 0.28f, 0.65f);
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text label = CreatePositionedText(
                buttonObject.transform, "Label", readableFont, 23f, Vector2.zero, new Vector2(width - 30f, 46f));
            label.color = PanelTextColor;
            return button;
        }

        private GameObject BuildCreditsPanel(RectTransform parent)
        {
            GameObject panel = CreatePanel(parent, "Credits", new Color(0.02f, 0.02f, 0.025f, 0.94f));

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.sizeDelta = new Vector2(1400f, 900f);

            TMP_Text text = CreatePositionedText(viewportRect, "Credits Text", readableFont, 34f, Vector2.zero, new Vector2(1300f, 100f));
            text.alignment = TextAlignmentOptions.Top;
            text.color = OverlayTextColor;
            text.textWrappingMode = TextWrappingModes.Normal;

            TextAsset credits = Resources.Load<TextAsset>(CreditsPath);
            text.text = credits != null ? credits.text : "LEAVES OF WAR\n\nA game by TheCelticArtist";
            if (credits == null)
                Debug.LogWarning($"MainMenuController: credits not found at Resources/{CreditsPath} (.txt).");

            creditsScroll = (RectTransform)text.transform;
            creditsScroll.pivot = new Vector2(0.5f, 1f);

            TMP_Text back = CreatePositionedText(panel.transform, "Back Hint", readableFont, 24f, new Vector2(0f, -500f), new Vector2(1200f, 40f));
            back.text = "Click or press Esc to go back";
            back.color = new Color(OverlayTextColor.r, OverlayTextColor.g, OverlayTextColor.b, 0.7f);

            return panel;
        }

        // ------------------------------------------------------------------
        // Sweeping
        // ------------------------------------------------------------------

        private void HandleMouseSweeping()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed)
            {
                strokeActive = false;
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(leavesRect, screen, null, out Vector2 local))
                return;

            Rect r = leavesRect.rect;
            var point = new Vector2(
                (local.x - r.xMin) / r.width * leavesTexture.width,
                (local.y - r.yMin) / r.height * leavesTexture.height);

            if (!strokeActive)
            {
                // New stroke: nothing is swept until the mouse moves.
                strokeActive = true;
                lastStrokePoint = point;
                return;
            }

            Vector2 delta = point - lastStrokePoint;
            float distance = delta.magnitude;
            if (distance < minStroke)
                return;

            float speed = distance / Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
            SweepStroke(lastStrokePoint, point, delta / distance, speed);
            lastStrokePoint = point;
            lastStrokeTime = Time.unscaledTime;
            lastStrokeSpeed = speed;
            lastStrokeDir = delta / distance;
        }

        /// <summary>
        /// Moves the broom head from <paramref name="from"/> to <paramref name="to"/>.
        /// The head is a wide, shallow rectangle across the stroke direction. Every
        /// leaf it touches is moved to the front of the head (the pile) and given
        /// part of the broom's speed, so it keeps sliding. Near the ends of the head
        /// some leaves slip out sideways. No leaf is ever deleted here.
        /// </summary>
        private void SweepStroke(Vector2 from, Vector2 to, Vector2 dir, float speed)
        {
            var perp = new Vector2(-dir.y, dir.x);
            float length = Vector2.Distance(from, to);
            float step = Mathf.Max(1f, broomHalfDepth * 0.5f);
            int stamps = Mathf.Max(1, Mathf.CeilToInt(length / step));
            float front = broomHalfDepth;
            float kick = speed * pushKick;
            pushedThisStroke = 0;

            for (int s = 1; s <= stamps; s++)
            {
                Vector2 center = Vector2.Lerp(from, to, (float)s / stamps);

                for (int n = 0; n < leafCount; n++)
                {
                    ref Leaf leaf = ref leaves[n];
                    if (leaf.Fall > 0f)
                        continue; // still in the air
                    Vector2 d = leaf.Pos - center;
                    float across = d.x * perp.x + d.y * perp.y;
                    if (across > broomHalfWidth || across < -broomHalfWidth)
                        continue;
                    float along = d.x * dir.x + d.y * dir.y;
                    if (along < -broomHalfDepth || along > front)
                        continue;

                    float side = across >= 0f ? 1f : -1f;
                    bool nearEnd = Mathf.Abs(across) > broomHalfWidth * 0.82f;

                    if (nearEnd && leafRandom.NextDouble() < endSpill)
                    {
                        // Slips out past the end of the head and rolls aside.
                        float outAcross = side * (broomHalfWidth + RandomRange(0.5f, 2.5f));
                        leaf.Pos = center + dir * Mathf.Max(along, 0f) + perp * outAcross;
                        leaf.Vel = perp * side * kick * RandomRange(0.2f, 0.45f) + dir * kick * 0.3f;
                    }
                    else
                    {
                        // Pushed into the pile in front of the head.
                        float newAlong = front + RandomRange(0f, pileDepth);
                        float newAcross = across + RandomRange(-0.6f, 0.6f);
                        leaf.Pos = center + dir * newAlong + perp * newAcross;
                        leaf.Vel = dir * kick * RandomRange(0.75f, 1.05f) + perp * kick * RandomRange(-0.12f, 0.12f);
                    }

                    leaf.Spin = RandomRange(-6f, 6f);
                    leaf.Angle += RandomRange(-0.4f, 0.4f);
                    leavesMoving = true;
                    pushedThisStroke++;
                    if (!regrowStarted)
                    {
                        // The first leaf touched: the first golden leaf falls right away.
                        regrowStarted = true;
                        regrowAccumulator = 1f;
                    }
                }
            }

            RemoveOffscreenLeaves();
            lastSweepTime = Time.unscaledTime;
            leavesDirty = true;

            if (!hasSwept)
            {
                hasSwept = true;
                FadeOutHint(1.5f);
            }
        }

        /// <summary>Leaves slide with their own momentum and slow down on the wood.</summary>
        private void SimulateLeaves(float dt)
        {
            if (!leavesMoving || dt <= 0f)
                return;

            float damp = gustActive ? 1f : Mathf.Exp(-leafFriction * dt);
            bool stillMoving = false;

            for (int n = 0; n < leafCount; n++)
            {
                ref Leaf leaf = ref leaves[n];

                if (leaf.Fall > 0f)
                {
                    // Flutters down: swings from side to side, less and less, then lands.
                    leaf.Phase += dt * 3.2f;
                    leaf.Pos += new Vector2(Mathf.Sin(leaf.Phase) * 16f, Mathf.Cos(leaf.Phase * 0.7f) * 5f) * leaf.Fall * dt;
                    leaf.Angle += Mathf.Sin(leaf.Phase * 1.3f) * 2.5f * leaf.Fall * dt;
                    leaf.Fall = Mathf.Max(0f, leaf.Fall - dt / goldLeafFallTime);
                    stillMoving = true;
                    continue;
                }

                if (leaf.Vel.sqrMagnitude < 0.04f)
                {
                    leaf.Vel = Vector2.zero;
                    leaf.Spin = 0f;
                    continue;
                }

                leaf.Pos += leaf.Vel * dt;
                leaf.Angle += leaf.Spin * dt;
                leaf.Vel *= damp;
                leaf.Spin *= damp;
                stillMoving = true;
            }

            leavesMoving = stillMoving;
            leavesDirty = true;
            RemoveOffscreenLeaves();
        }

        /// <summary>A leaf is deleted only when it is completely out of view.</summary>
        private void RemoveOffscreenLeaves()
        {
            float maxX = leavesTexture.width + OffscreenMargin;
            float maxY = leavesTexture.height + OffscreenMargin;
            int write = 0;
            for (int read = 0; read < leafCount; read++)
            {
                Vector2 p = leaves[read].Pos;
                if (p.x < -OffscreenMargin || p.y < -OffscreenMargin || p.x > maxX || p.y > maxY)
                    continue;
                if (write != read)
                    leaves[write] = leaves[read];
                write++;
            }

            if (write != leafCount)
            {
                leafCount = write;
                leavesDirty = true;
            }
        }

        /// <summary>
        /// As soon as the broom first moves a leaf, small golden leaves start
        /// falling from above, one at a time, and settle at random spots.
        /// </summary>
        private void RegrowLeaves()
        {
            if (!regrowStarted || regrowLeavesPerSecond <= 0f || leafCount >= leaves.Length)
                return;

            regrowAccumulator += regrowLeavesPerSecond * Time.unscaledDeltaTime;
            while (regrowAccumulator >= 1f && leafCount < leaves.Length)
            {
                regrowAccumulator -= 1f;
                var spot = new Vector2(
                    RandomRange(4f, leavesTexture.width - 4f),
                    RandomRange(4f, leavesTexture.height - 4f));

                Leaf leaf = MakeLeaf(spot);
                leaf.Fall = 1f;
                leaf.Phase = RandomRange(0f, Mathf.PI * 2f);
                leaves[leafCount++] = leaf;
                leavesMoving = true;
            }
        }

        /// <summary>Average colour of a small random patch of the starting leaves.</summary>
        private Color SampleCarpetColour()
        {
            int w = leavesTexture.width;
            int h = leavesTexture.height;
            int cx = leafRandom.Next(w);
            int cy = leafRandom.Next(h);
            float r = 0f, g = 0f, b = 0f;
            int count = 0;
            for (int y = -2; y <= 2; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    Color32 c = leafColors[Wrap(cy + y, h) * w + Wrap(cx + x, w)];
                    r += c.r;
                    g += c.g;
                    b += c.b;
                    count++;
                }
            }
            return new Color(r / (count * 255f), g / (count * 255f), b / (count * 255f), 1f);
        }

        /// <summary>Redraws every leaf as a small pointed shape carrying a patch of the leaf texture.</summary>
        private void DrawLeaves()
        {
            int w = leavesTexture.width;
            int h = leavesTexture.height;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < leafBuffer.Length; i++)
                leafBuffer[i] = clear;

            for (int n = 0; n < leafCount; n++)
            {
                Leaf leaf = leaves[n];
                float c = Mathf.Cos(leaf.Angle);
                float s = Mathf.Sin(leaf.Angle);
                // A falling leaf is closer to the camera, so it looks bigger.
                float scale = 1f + leaf.Fall * leaf.Fall * 1.4f;
                float hx = leaf.Half.x * scale;
                float hy = leaf.Half.y * scale;
                float ex = Mathf.Abs(c) * hx + Mathf.Abs(s) * hy + 0.5f;
                float ey = Mathf.Abs(s) * hx + Mathf.Abs(c) * hy + 0.5f;

                int x0 = Mathf.Max(0, Mathf.FloorToInt(leaf.Pos.x - ex));
                int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(leaf.Pos.x + ex));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(leaf.Pos.y - ey));
                int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(leaf.Pos.y + ey));
                if (x0 > x1 || y0 > y1)
                    continue;

                float invX = 1f / hx;
                float invY = 1f / hy;

                for (int y = y0; y <= y1; y++)
                {
                    float dy = y + 0.5f - leaf.Pos.y;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - leaf.Pos.x;
                        float lx = dx * c + dy * s;
                        float ly = -dx * s + dy * c;

                        if (leaf.Gold)
                        {
                            // Golden leaf: the image itself, stretched to the leaf size.
                            float gu = (lx * invX + 1f) * 0.5f;
                            float gv = (ly * invY + 1f) * 0.5f;
                            if (gu < 0f || gu >= 1f || gv < 0f || gv >= 1f)
                                continue;
                            Color32 gc = goldPixels[(int)(gv * goldHeight) * goldWidth + (int)(gu * goldWidth)];
                            if (gc.a < 128)
                                continue;
                            // Recolour towards the starting leaves, keeping the image's light and shade.
                            float lum = (gc.r + gc.g + gc.b) / (3f * 255f);
                            float shading = 0.35f + 1.3f * lum;
                            float gr = Mathf.Lerp(gc.r / 255f, leaf.Tint.r * shading, goldLeafTint);
                            float gg = Mathf.Lerp(gc.g / 255f, leaf.Tint.g * shading, goldLeafTint);
                            float gb = Mathf.Lerp(gc.b / 255f, leaf.Tint.b * shading, goldLeafTint);
                            gc.r = (byte)Mathf.Clamp(gr * leaf.Shade * 255f, 0f, 255f);
                            gc.g = (byte)Mathf.Clamp(gg * leaf.Shade * 255f, 0f, 255f);
                            gc.b = (byte)Mathf.Clamp(gb * leaf.Shade * 255f, 0f, 255f);
                            gc.a = 255;
                            leafBuffer[y * w + x] = gc;
                            continue;
                        }

                        // Pointed leaf: width shrinks to zero at both tips.
                        float u = lx * invX;
                        float profile = 1f - u * u;
                        if (profile <= 0f)
                            continue;
                        float v = Mathf.Abs(ly * invY);
                        if (v > profile)
                            continue;

                        int sx = Wrap(Mathf.FloorToInt(leaf.Src.x + lx), w);
                        int sy = Wrap(Mathf.FloorToInt(leaf.Src.y + ly), h);
                        Color32 col = leafColors[sy * w + sx];

                        // Darker rim so every leaf reads as a leaf, not as a smear.
                        float k = leaf.Shade * (v > profile * 0.68f ? 0.68f : 1f);
                        col.r = (byte)Mathf.Min(255f, col.r * k);
                        col.g = (byte)Mathf.Min(255f, col.g * k);
                        col.b = (byte)Mathf.Min(255f, col.b * k);
                        col.a = 255;
                        leafBuffer[y * w + x] = col;
                    }
                }
            }

            leavesTexture.SetPixels32(leafBuffer);
            leavesTexture.Apply(false);
            leavesDirty = false;
        }

        private static int Wrap(int value, int size)
        {
            int m = value % size;
            return m < 0 ? m + size : m;
        }

        /// <summary>Share of the target's area (0-1) that is still covered by a leaf.</summary>
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
            int count = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (leafBuffer[y * w + x].a > 0)
                        covered++;
                    count++;
                }
            }
            return count > 0 ? (float)covered / count : 1f;
        }

        private void UpdateItemReveal()
        {
            bool menuVisible = menuGroup.activeInHierarchy && !inputLocked;
            for (int i = 0; i < items.Count; i++)
            {
                FloorMenuItem item = items[i];
                bool revealed = menuVisible && CoverageUnder((RectTransform)item.transform) < revealThreshold;
                item.SetRevealed(revealed);
            }
        }

        // ------------------------------------------------------------------
        // Keyboard / gamepad
        // ------------------------------------------------------------------

        private void HandleKeyboardAndGamepad()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool confirm =
                (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            // With leaves still covering every entry, the confirm button blows them away.
            // Once an entry is uncovered and selected, the same button activates it
            // (handled by FloorMenuItem.OnSubmit).
            if (confirm && !AnyItemRevealed())
                StartCoroutine(Gust(null));
        }

        private bool AnyItemRevealed()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Revealed)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Menu actions
        // ------------------------------------------------------------------

        private void StartNewGame()
        {
            if (starting || inputLocked)
                return;

            if (string.IsNullOrEmpty(newGameScene))
            {
                statusText.text = "NO NEW GAME SCENE IS SET.";
                Debug.LogError("MainMenuController: set 'New Game Scene' in the Inspector.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(newGameScene))
            {
                statusText.text = $"SCENE '{newGameScene}' IS NOT IN THE BUILD SETTINGS.";
                Debug.LogError($"New game scene '{newGameScene}' not found. Add it to File > Build Settings > Scenes In Build.");
                return;
            }

            starting = true;
            StartCoroutine(Gust(() => SceneManager.LoadScene(newGameScene)));
        }

        private void ContinueGame()
        {
            if (starting || !TryLoadConfiguration(out NarrativeCatalog catalog, out PresentationSettings settings))
                return;

            GameSession session = GameSession.Create(catalog, settings);
            if (session.ContinueGame(out SaveReadStatus readStatus))
            {
                starting = true;
                return;
            }

            switch (readStatus)
            {
                case SaveReadStatus.TransientFailure:
                    statusText.text = "THE SAVE IS TEMPORARILY UNAVAILABLE.";
                    break;
                case SaveReadStatus.Invalid:
                    statusText.text = "THE SAVE COULD NOT BE READ. IT HAS BEEN DISCARDED.";
                    break;
                default:
                    statusText.text = "THE SAVE COULD NOT BE READ.";
                    break;
            }

            RefreshContinueAvailability(false);
        }

        private void LoadManualSlot(int slotNumber)
        {
            if (starting || !TryLoadConfiguration(out NarrativeCatalog catalog, out PresentationSettings settings))
                return;

            GameSession session = GameSession.Create(catalog, settings);
            if (session.LoadManualSlot(slotNumber, out SaveReadStatus readStatus))
            {
                starting = true;
                return;
            }

            loadStatusText.text = $"SLOT {slotNumber:D2}: {SlotStatusLabel(readStatus)}.";
            RefreshLoadAvailability();
        }

        private void ShowLoadMenu()
        {
            if (inputLocked)
                return;

            inputLocked = true;
            SetFloorWritingVisible(false);
            RefreshLoadAvailability();
            loadStatusText.text = "SELECT A SAVE TO LOAD. CURRENT PROGRESS WILL BE REPLACED.";
            loadPanel.SetActive(true);
            statusText.text = string.Empty;
        }

        private void ShowOptions()
        {
            if (inputLocked)
                return;

            inputLocked = true;
            SetFloorWritingVisible(false);
            optionsPanel.Show();
            ApplyPanelFontToOptions();
            statusText.text = string.Empty;
        }

        private void ShowCredits()
        {
            if (inputLocked)
                return;

            inputLocked = true;
            SetFloorWritingVisible(false);
            creditsPanel.SetActive(true);
            statusText.text = string.Empty;
            creditsRoutine = StartCoroutine(RunCredits());
        }

        private void ShowMainMenu()
        {
            if (creditsRoutine != null)
            {
                StopCoroutine(creditsRoutine);
                creditsRoutine = null;
            }

            creditsPanel.SetActive(false);
            loadPanel.SetActive(false);
            optionsPanel.Hide();
            SetFloorWritingVisible(true);
            inputLocked = false;
            lastSweepTime = Time.unscaledTime;
            RefreshContinueAvailability();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
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
            if (hintGroup != null)
                hintGroup.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------
        // Saves (same behaviour as every other game: Continue + Load Game)
        // ------------------------------------------------------------------

        private void RefreshContinueAvailability(bool showReadFailure = true)
        {
            NarrativeCatalog catalog = Resources.Load<NarrativeCatalog>("Narrative/NarrativeCatalog");
            SaveReadStatus status = SaveGameStore.GetReadOnlyLoadStatus(catalog, out _);
            bool hasInMemoryProgress =
                GameSession.Instance != null &&
                GameSession.Instance.HasRecoverableInMemoryProgress;
            bool canLoad = GameSession.Instance?.CanLoadSavedGame != false;

            if (continueItem != null)
            {
                continueItem.SetAvailable(canLoad &&
                    (hasInMemoryProgress ||
                     status == SaveReadStatus.Valid ||
                     status == SaveReadStatus.RecoverableBackup));
            }

            if (!showReadFailure || statusText == null)
                return;

            if (hasInMemoryProgress)
            {
                statusText.text = "UNSAVED PROGRESS IS AVAILABLE TO RETRY.";
                return;
            }

            switch (status)
            {
                case SaveReadStatus.RecoverableBackup:
                    statusText.text = "A PREVIOUS SAVE IS AVAILABLE FOR RECOVERY.";
                    break;
                case SaveReadStatus.Invalid:
                    statusText.text = "THE SAVE IS INVALID. STARTING A NEW GAME WILL CLEAR IT.";
                    break;
                case SaveReadStatus.TransientFailure:
                    statusText.text = "THE SAVE IS TEMPORARILY UNAVAILABLE.";
                    break;
                default:
                    statusText.text = string.Empty;
                    break;
            }
        }

        private void RefreshLoadAvailability()
        {
            NarrativeCatalog catalog = Resources.Load<NarrativeCatalog>("Narrative/NarrativeCatalog");
            bool canLoad = GameSession.Instance?.CanLoadSavedGame != false;

            if (autosaveLoadButton != null)
            {
                SaveReadStatus autosaveStatus = SaveGameStore.GetReadOnlyLoadStatus(catalog, out _);
                autosaveLoadButton.interactable = canLoad &&
                    (autosaveStatus == SaveReadStatus.Valid ||
                     autosaveStatus == SaveReadStatus.RecoverableBackup);
                if (autosaveLoadLabel != null)
                {
                    autosaveLoadLabel.color = autosaveLoadButton.interactable ? PanelTextColor : PanelTextDisabledColor;
                    autosaveLoadLabel.text = "AUTOSAVE  —  " + SlotStatusLabel(autosaveStatus);
                }
            }

            for (int i = 0; i < manualLoadSlots.Count; i++)
            {
                ManualSlotButton slot = manualLoadSlots[i];
                SaveReadStatus readStatus = SaveGameStore.GetManualSlotReadOnlyLoadStatus(slot.SlotNumber, catalog, out _);
                slot.Button.interactable = canLoad &&
                    (readStatus == SaveReadStatus.Valid ||
                     readStatus == SaveReadStatus.RecoverableBackup);
                slot.Label.color = slot.Button.interactable ? PanelTextColor : PanelTextDisabledColor;
                slot.Label.text = $"SLOT {slot.SlotNumber:D2}  —  {SlotStatusLabel(readStatus)}";
            }
        }

        internal void RefreshSaveAvailability()
        {
            RefreshContinueAvailability();
            RefreshLoadAvailability();
        }

        private bool TryLoadConfiguration(out NarrativeCatalog catalog, out PresentationSettings settings)
        {
            catalog = Resources.Load<NarrativeCatalog>("Narrative/NarrativeCatalog");
            settings = Resources.Load<PresentationSettings>("Narrative/PresentationSettings");
            if (catalog != null && settings != null)
                return true;

            statusText.text = "NARRATIVE DATA IS MISSING.";
            return false;
        }

        private static string SlotStatusLabel(SaveReadStatus status)
        {
            switch (status)
            {
                case SaveReadStatus.Valid:
                    return "SAVED";
                case SaveReadStatus.RecoverableBackup:
                    return "RECOVERABLE";
                case SaveReadStatus.Missing:
                    return "EMPTY";
                case SaveReadStatus.Invalid:
                    return "INVALID";
                default:
                    return "UNAVAILABLE";
            }
        }

        private sealed class ManualSlotButton
        {
            public ManualSlotButton(int slotNumber, Button button, TMP_Text label)
            {
                SlotNumber = slotNumber;
                Button = button;
                Label = label;
            }

            public int SlotNumber { get; }
            public Button Button { get; }
            public TMP_Text Label { get; }
        }

        // ------------------------------------------------------------------
        // Credits and gust
        // ------------------------------------------------------------------

        private IEnumerator RunCredits()
        {
            const float speed = 60f; // reference pixels per second
            Canvas.ForceUpdateCanvases();
            TMP_Text text = creditsScroll.GetComponent<TMP_Text>();
            float textHeight = text.preferredHeight;
            creditsScroll.sizeDelta = new Vector2(creditsScroll.sizeDelta.x, textHeight);

            float start = -450f;            // just below the viewport
            float end = textHeight + 450f;   // fully scrolled out at the top
            float y = start;
            creditsScroll.anchoredPosition = new Vector2(0f, y);

            // Skip the frame of the click that opened the credits.
            yield return null;

            while (true)
            {
                y += speed * Time.unscaledDeltaTime;
                if (y > end)
                    y = start;
                creditsScroll.anchoredPosition = new Vector2(0f, y);

                bool close =
                    (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                    (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                    (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame));
                if (close)
                {
                    creditsRoutine = null;
                    ShowMainMenu();
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator Gust(System.Action onComplete)
        {
            inputLocked = true;
            gustActive = true;
            if (gustClip != null && sfxSource != null)
                sfxSource.PlayOneShot(gustClip, gustVolume);
            FadeOutHint(0.3f);

            // The wind enters from the window (left) and sweeps across the floor:
            // every leaf it reaches is blown off the right side of the screen.
            float w = leavesTexture.width;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / gustDuration;
                float frontX = Mathf.Lerp(-0.1f * w, 1.2f * w, t * t);
                for (int n = 0; n < leafCount; n++)
                {
                    // (No "ref" locals here: they are not allowed inside a coroutine.)
                    if (leaves[n].Pos.x <= frontX && leaves[n].Vel.x < 200f)
                    {
                        leaves[n].Vel = new Vector2(RandomRange(320f, 520f), RandomRange(-70f, 70f));
                        leaves[n].Spin = RandomRange(-14f, 14f);
                        leaves[n].Fall = 0f; // a golden leaf still in the air is blown away too
                        leavesMoving = true;
                    }
                }
                yield return null;
            }

            // Let the last leaves fly out, then make sure the floor is clean.
            float wait = 0f;
            while (leafCount > 0 && wait < 1f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            leafCount = 0;
            leavesMoving = false;
            gustActive = false;
            leavesDirty = true;
            hasSwept = true;
            if (!regrowStarted)
            {
                regrowStarted = true;
                regrowAccumulator = 1f;
            }
            lastSweepTime = Time.unscaledTime;
            inputLocked = false;

            if (onComplete != null)
            {
                onComplete();
                yield break;
            }

            // Keyboard / gamepad players: select the first entry so they can navigate.
            yield return null;
            UpdateItemReveal();
            if (items.Count > 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(items[0].gameObject);
        }

        // ------------------------------------------------------------------
        // Audio
        // ------------------------------------------------------------------

        private void StartAudio()
        {
            if (menuMusic != null)
                AudioManager.Instance?.PlayMusic(menuMusic, true, musicVolume);

            CreateSource("Wind", windAmbience, true, windVolume);
            AudioClip sweepClip = sweepLoop != null ? sweepLoop : Resources.Load<AudioClip>(SweepSoundPath);
            if (sweepClip == null)
                Debug.LogWarning($"MainMenuController: no sweep sound. Assign 'Sweep Loop' in the Inspector or add Resources/{SweepSoundPath}.");
            sweepSource = CreateSource("Sweep", sweepClip, true, 0f);
            if (sweepSource.clip != null)
                sweepSource.Stop(); // starts with the first stroke
            sfxSource = CreateSource("SFX", null, false, 1f);
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip, bool loop, float volume)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.volume = volume;
            source.clip = clip;
            if (clip != null && loop)
                source.Play();
            return source;
        }

        private void UpdateSweepAudio()
        {
            if (sweepSource == null || sweepSource.clip == null)
                return;

            // Short hold so tiny pauses between mouse samples do not chop the sound.
            bool active = !inputLocked && Time.unscaledTime - lastStrokeTime < 0.1f;
            float speed01 = Mathf.Clamp01(lastStrokeSpeed / sweepFullSpeed);
            float leaves01 = Mathf.Clamp01(pushedThisStroke / 40f);

            float target = 0f;
            if (active)
            {
                float surface = Mathf.Lerp(sweepBareWoodVolume, 1f, leaves01);
                target = sweepVolume * Mathf.Lerp(0.3f, 1f, speed01) * surface;
            }

            // Quick attack, softer release.
            float rate = target > sweepSource.volume ? 8f : 3f;
            sweepSource.volume = Mathf.MoveTowards(sweepSource.volume, target, Time.unscaledDeltaTime * rate);

            float pitchTarget = Mathf.Lerp(sweepPitch.x, sweepPitch.y, speed01);
            sweepSource.pitch = Mathf.MoveTowards(sweepSource.pitch, pitchTarget, Time.unscaledDeltaTime * 1.5f);

            if (target > 0f && !sweepSource.isPlaying)
            {
                // Start from a random point so every stroke sounds a bit different.
                sweepSource.time = Random.Range(0f, sweepSource.clip.length * 0.9f);
                sweepSource.Play();
            }
            else if (sweepSource.isPlaying && sweepSource.volume <= 0.001f && target <= 0f)
            {
                sweepSource.Stop();
            }
        }

        // ------------------------------------------------------------------
        // Cursor
        // ------------------------------------------------------------------

        /// <summary>
        /// The broom is drawn as an image on its own canvas, above everything,
        /// and the system cursor is hidden. A system cursor cannot be larger than
        /// 32-128 px (browsers and operating systems shrink or drop it); this one
        /// can be any size and scales with the window.
        /// </summary>
        private void ApplyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            Texture2D texture = Resources.Load<Texture2D>(CursorResourcePath);
            if (texture == null)
            {
                Cursor.visible = true;
                Debug.LogWarning($"Custom cursor not found at Resources/{CursorResourcePath}; the system cursor will be used.");
                return;
            }

            if (cursorCanvas == null)
            {
                var canvasObject = new GameObject("Cursor", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObject.transform.SetParent(transform, false);
                cursorCanvas = canvasObject.GetComponent<Canvas>();
                cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cursorCanvas.overrideSorting = true;
                cursorCanvas.sortingOrder = 32000; // above every panel
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var imageObject = new GameObject("Broom", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(canvasObject.transform, false);
                cursorRect = (RectTransform)imageObject.transform;
                cursorRect.anchorMin = cursorRect.anchorMax = Vector2.zero;
                RawImage image = imageObject.GetComponent<RawImage>();
                image.raycastTarget = false; // never blocks clicks
                image.texture = texture;
            }

            float aspect = (float)texture.width / texture.height;
            cursorRect.sizeDelta = new Vector2(cursorSize * aspect, cursorSize);
            // Hotspot in image pixels from the top-left -> pivot (0-1 from the bottom-left).
            cursorRect.pivot = new Vector2(
                Mathf.Clamp01(cursorHotspot.x / texture.width),
                Mathf.Clamp01(1f - cursorHotspot.y / texture.height));

            cursorCanvas.gameObject.SetActive(true);
            Cursor.visible = false;
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            if (cursorCanvas == null || !cursorCanvas.gameObject.activeSelf)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            // Keep the system arrow hidden even if another panel turned it back on.
            if (Cursor.visible)
                Cursor.visible = false;

            RectTransform canvasRect = (RectTransform)cursorCanvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue(), null, out Vector2 local))
            {
                // Anchors are at the bottom-left corner of the canvas.
                cursorRect.anchoredPosition = local - canvasRect.rect.min;
            }

            AnimateCursor(mouse.leftButton.isPressed && !inputLocked);
        }

        /// <summary>
        /// Sweeping animation: while the broom moves, the handle swings back and
        /// forth around the bristles and leans against the stroke; holding the
        /// button presses the broom down a little. At rest it eases back upright.
        /// </summary>
        private void AnimateCursor(bool pressed)
        {
            float dt = Time.unscaledDeltaTime;
            bool sweeping = pressed && Time.unscaledTime - lastStrokeTime < 0.15f;
            float speed01 = Mathf.Clamp01(lastStrokeSpeed / sweepFullSpeed);

            float target = 0f;
            if (sweeping)
            {
                cursorSwingPhase += dt * Mathf.PI * 2f * Mathf.Lerp(cursorSwingRate.x, cursorSwingRate.y, speed01);
                float swing = Mathf.Sin(cursorSwingPhase) * cursorSwingAngle * Mathf.Lerp(0.5f, 1f, speed01);
                // Moving right tilts the handle back to the left, and vice versa.
                float lean = -lastStrokeDir.x * cursorLeanAngle * Mathf.Lerp(0.4f, 1f, speed01);
                target = swing + lean;
            }

            cursorAngle = Mathf.Lerp(cursorAngle, target, 1f - Mathf.Exp(-14f * dt));
            cursorRect.localEulerAngles = new Vector3(0f, 0f, cursorAngle);

            float squashTarget = pressed ? cursorPressSquash : 0f;
            cursorSquash = Mathf.Lerp(cursorSquash, squashTarget, 1f - Mathf.Exp(-20f * dt));
            cursorRect.localScale = new Vector3(1f + cursorSquash * 0.5f, 1f - cursorSquash, 1f);
        }

        private void ResetCursor()
        {
            if (cursorCanvas != null)
                cursorCanvas.gameObject.SetActive(false);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static RectTransform CreateFrame(RectTransform parent, string frameName)
        {
            // Fixed 1920x1080 frame, centred: children placed with fixed offsets
            // never drift when the window's aspect ratio changes.
            var frameObject = new GameObject(frameName, typeof(RectTransform));
            frameObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)frameObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static GameObject CreatePanel(Transform parent, string panelName, Color color)
        {
            var panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            StretchFull((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static GameObject CreateCenteredPanel(Transform parent, string panelName, Vector2 size, Color color)
        {
            var panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static TMP_Text CreatePositionedText(
            Transform parent,
            string objectName,
            TMP_FontAsset font,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                DontDestroyOnLoad(existing.gameObject);
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static void EnsureSettingsApplier()
        {
            if (FindAnyObjectByType<SettingsApplier>() != null)
                return;

            var applierObject = new GameObject("Settings Applier");
            applierObject.AddComponent<SettingsApplier>();
        }
    }

    /// <summary>
    /// An entry written on the floor of the title screen. It can be clicked or
    /// submitted only once it has been uncovered from the leaves AND is available
    /// (e.g. Continue with no save stays written but faded). A click that is
    /// really the end of a sweeping drag is ignored, so sweeping over an entry
    /// never triggers it by accident.
    /// </summary>
    internal sealed class FloorMenuItem : Selectable, IPointerClickHandler, ISubmitHandler
    {
        private const float MaxClickTravel = 20f; // pixels between press and release
        private const float UnavailableAlpha = 0.4f;

        private TMP_Text label;
        private System.Action action;
        private RectTransform visual;
        private CanvasGroup visualGroup;
        private Color baseColor;
        private Color highlightColor;
        private float highlightScale;
        private Image background;
        private Color backgroundColor;
        private Color backgroundHighlightColor;
        private bool available = true;

        public bool Revealed { get; private set; }

        public void Init(
            TMP_Text text, System.Action onChosen,
            RectTransform visualRoot, CanvasGroup visualCanvasGroup,
            Color normal, Color highlight, float scale,
            Image backgroundImage, Color backgroundNormal, Color backgroundHighlight)
        {
            label = text;
            action = onChosen;
            visual = visualRoot;
            visualGroup = visualCanvasGroup;
            baseColor = normal;
            highlightColor = highlight;
            highlightScale = scale;
            background = backgroundImage;
            backgroundColor = backgroundNormal;
            backgroundHighlightColor = backgroundHighlight;
            transition = Transition.None;
            interactable = false;
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
            if (!Revealed || !available || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            if ((eventData.position - eventData.pressPosition).sqrMagnitude > MaxClickTravel * MaxClickTravel)
                return; // it was a sweep, not a click

            action?.Invoke();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (Revealed && available && IsInteractable())
                action?.Invoke();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            bool active = state == SelectionState.Highlighted || state == SelectionState.Selected || state == SelectionState.Pressed;
            ApplyLook(active);
        }

        private void ApplyLook(bool active)
        {
            if (label == null)
                return;

            bool lit = active && available;
            label.color = lit ? highlightColor : baseColor;
            if (background != null)
                background.color = lit ? backgroundHighlightColor : backgroundColor;
            if (visualGroup != null)
                visualGroup.alpha = available ? 1f : UnavailableAlpha;
            if (visual != null)
                visual.localScale = lit ? Vector3.one * highlightScale : Vector3.one;
        }
    }
}