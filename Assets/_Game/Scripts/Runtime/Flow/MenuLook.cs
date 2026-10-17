using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    public sealed class MenuLook : ScriptableObject
    {
        public const string ResourcePath = "MainMenu/MenuLook";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".asset";
        private const string HandwritingFontPath = "Fonts/ErraticCursive";
        private const string ReadableFontPath = "Fonts/OSerif";
        private const string FloorPath = "MainMenu/Floor";

        [Header("Entries")]
        public TMP_FontAsset entryFont;
        public Font entryFontFile;
        public bool entryBold;
        public float menuWordSize = 64f;
        public Color inkColor = new Color(0.118f, 0.165f, 0.227f, 1f);
        public Color entryHighlightColor = new Color(0.077f, 0.107f, 0.148f, 1f);
        public float entryHighlightScale = 1.08f;
        public float entryTextOutlineWidth;
        public Color entryTextOutlineColor = Color.black;
        public Vector2 entrySize = new Vector2(520f, 80f);
        public float entrySpacing = 88f;
        public bool useEntryBackground;
        public bool entryBackgroundOnHover = true;
        public float entryBackgroundUnfoldTime = 0.3f;
        public float entryBackgroundCrumpleTime = 0.22f;
        public Sprite entryBackground;
        public Color entryBackgroundColor = new Color(0.9f, 0.85f, 0.72f, 0.85f);
        public Color entryBackgroundHighlightColor = new Color(1f, 0.95f, 0.82f, 0.95f);
        public float entryBorderWidth;
        public Color entryBorderColor = new Color(0.118f, 0.165f, 0.227f, 1f);

        [Header("Panels")]
        public TMP_FontAsset panelFont;
        public Font panelFontFile;
        public bool panelBold;
        public Color panelColor = new Color(0.035f, 0.03f, 0.024f, 0.98f);
        public Color panelHeaderColor = new Color(0.88f, 0.84f, 0.72f);
        public Color panelTextColor = new Color(0.9f, 0.86f, 0.74f);
        public Color panelStatusColor = new Color(0.73f, 0.69f, 0.57f);
        public Color panelButtonColor = new Color(0.115f, 0.1f, 0.075f, 0.96f);
        public Color panelButtonHighlight = new Color(0.72f, 0.77f, 0.58f);
        public Color panelButtonPressed = new Color(0.48f, 0.52f, 0.37f);
        public Color panelButtonDisabled = new Color(0.34f, 0.32f, 0.28f, 0.65f);

        [Header("Hint (in-game screen text)")]
        public TMP_FontAsset hintFont;
        public Font hintFontFile;
        public bool hintBold;
        public Color hintColor = new Color(0.96f, 0.93f, 0.88f, 0.85f);
        public float hintTextOutlineWidth;
        public Color hintTextOutlineColor = Color.black;
        public bool useHintBackground;
        public Sprite hintBackground;
        public Color hintBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("Floor")]
        public Color fallbackFloorColor = new Color(0.36f, 0.25f, 0.16f, 1f);
        public float floorTiling = 2f;

        [Header("Sounds")]
        public AudioClip entryEnterSound;
        public AudioClip entryExitSound;
        public AudioClip entryClickSound;
        public AudioClip[] entryPaperSounds = new AudioClip[0];
        public float entryPaperVolume = 0.7f;
        public float entryPaperFoldPitch = 0.88f;
        public AudioClip[] entryBackSounds = new AudioClip[0];
        public float entrySoundVolume = 0.8f;
        public Vector2 entrySoundPitch = new Vector2(0.94f, 1.06f);

        private static MenuLook cached;
        private static TMP_FontAsset entryFontAsset, panelFontAsset, hintFontAsset;
        private static AudioSource entrySource, paperSource;
        private static int lastPaper = -1;

        public static MenuLook Current
        {
            get
            {
                if (cached == null)
                {
                    cached = Resources.Load<MenuLook>(ResourcePath);
                    if (cached == null)
                    {
                        cached = CreateInstance<MenuLook>();
                        cached.hideFlags = HideFlags.DontSave;
                    }
                }
                return cached;
            }
        }

        public static void Refresh()
        {
            cached = null;
            entryFontAsset = panelFontAsset = hintFontAsset = null;
        }

        public TMP_FontAsset EntryFont => entryFontAsset != null ? entryFontAsset : entryFontAsset =
            MainMenuController.ResolveFont(entryFontFile, entryFont, "Entry Font")
            ?? MainMenuController.ResolveFont(null, Resources.Load<TMP_FontAsset>(HandwritingFontPath), HandwritingFontPath)
            ?? PanelFont;

        public TMP_FontAsset PanelFont => panelFontAsset != null ? panelFontAsset : panelFontAsset =
            MainMenuController.ResolveFont(panelFontFile, panelFont, "Panel Font")
            ?? MainMenuController.ResolveFont(null, Resources.Load<TMP_FontAsset>(ReadableFontPath), ReadableFontPath)
            ?? TMP_Settings.defaultFontAsset;

        public TMP_FontAsset HintFont => hintFontAsset != null ? hintFontAsset : hintFontAsset =
            MainMenuController.ResolveFont(hintFontFile, hintFont, "Hint Font") ?? PanelFont;

        public Texture2D FloorTexture => Resources.Load<Texture2D>(FloorPath);

        public TextStyle ScreenStyle(TextStyle basis)
        {
            basis.scale = 1f;
            basis.textColor = new Color(hintColor.r, hintColor.g, hintColor.b, Mathf.Max(0.85f, hintColor.a));
            basis.bold = hintBold;
            basis.thickness = 0f;
            basis.outlineWidth = hintTextOutlineWidth > 0f ? hintTextOutlineWidth * 2f : 0.15f;
            basis.outlineColor = hintTextOutlineColor;
            basis.backgroundColor = useHintBackground ? hintBackgroundColor : new Color(0f, 0f, 0f, 0f);
            basis.accentColor = panelButtonHighlight;
            return basis;
        }

        public void StyleEntryText(TMP_Text text, bool lit)
        {
            text.font = EntryFont;
            text.color = lit ? entryHighlightColor : inkColor;
            if (entryBold)
                text.fontStyle |= FontStyles.Bold;
            if (entryTextOutlineWidth > 0f)
            {
                text.outlineWidth = entryTextOutlineWidth;
                text.outlineColor = entryTextOutlineColor;
            }
        }

        public void StylePanelText(TMP_Text text)
        {
            if (text.font != PanelFont)
                text.font = PanelFont;
            if (panelBold)
                text.fontStyle |= FontStyles.Bold;
        }

        public void StylePanelButton(Button button)
        {
            if (button.targetGraphic is Image image)
                image.color = panelButtonColor;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = panelButtonHighlight;
            colors.selectedColor = panelButtonHighlight;
            colors.pressedColor = panelButtonPressed;
            colors.disabledColor = panelButtonDisabled;
            button.colors = colors;
        }

        public void PlayEntrySound(int kind)
        {
            Sources();
            AudioClip clip = kind == 0 ? entryEnterSound : kind == 1 ? entryExitSound : entryClickSound;
            if (clip != null)
            {
                entrySource.pitch = Random.Range(entrySoundPitch.x, entrySoundPitch.y);
                entrySource.PlayOneShot(clip, entrySoundVolume);
            }
            if (kind >= 2 || entryPaperSounds == null || entryPaperSounds.Length == 0)
                return;
            int pick = Random.Range(0, entryPaperSounds.Length);
            if (entryPaperSounds.Length > 1 && pick == lastPaper)
                pick = (pick + 1) % entryPaperSounds.Length;
            lastPaper = pick;
            if (entryPaperSounds[pick] == null)
                return;
            paperSource.pitch = Random.Range(entrySoundPitch.x, entrySoundPitch.y) * (kind == 1 ? entryPaperFoldPitch : 1f);
            paperSource.PlayOneShot(entryPaperSounds[pick], entryPaperVolume);
        }

        public void PlayBackSound()
        {
            Sources();
            if (entryBackSounds == null || entryBackSounds.Length == 0)
                return;
            AudioClip clip = entryBackSounds[Random.Range(0, entryBackSounds.Length)];
            if (clip != null)
                entrySource.PlayOneShot(clip, entrySoundVolume);
        }

        private static void Sources()
        {
            if (entrySource != null && paperSource != null)
                return;
            var go = new GameObject("Menu Sounds");
            DontDestroyOnLoad(go);
            entrySource = Source(go);
            paperSource = Source(go);
        }

        private static AudioSource Source(GameObject go)
        {
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            AudioManager.Route(source, AudioBus.SoundEffects);
            return source;
        }
    }
}
