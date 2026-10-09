using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// The OPTIONS screen and its KEY BINDINGS sub-screen, extracted so both the
    /// main menu and the pause menu can show the exact same panel. Built once by
    /// the owner (parented under the owner's own canvas) and toggled via
    /// <see cref="Show"/>/<see cref="Hide"/>. BACK calls the <c>onBack</c>
    /// callback supplied at construction instead of assuming a main-menu context.
    /// </summary>
    internal sealed class OptionsPanel
    {
        private readonly TMP_FontAsset menuFont;
        private readonly TMP_FontAsset authorialFont;
        private readonly Action onBack;

        private GameObject optionsPanelObject;
        private RectTransform optionsContent;
        private Button applyButton;
        private TMP_Text optionsStatusText;

        private GameObject keyBindingsPanel;
        private KeyRebinding rebinding;
        private TMP_Text keyBindingsStatusText;
        private UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation activeRebind;
        private readonly Dictionary<string, TMP_Text> keyValueLabels = new Dictionary<string, TMP_Text>();

        // Whether the KEY BINDINGS screen allows mouse buttons.
        private const bool AllowMouseRebinding = true;

        private SettingsData draft;
        private bool draftDirty;
        private AudioSettingsDraft audioDraft;
        private Action refreshUiFromDraft;
        private Action previewChanged;
        private RectTransform currentContent;
        private GameObject subtitleStylePanel;
        private GameObject screenStylePanel;
        private readonly List<TMP_Text> styleStatusTexts = new List<TMP_Text>();
        private static Sprite hueSprite;
        private static Sprite backdropSprite;

        private static readonly Color[] PresetColors =
        {
            Color.white, new Color(0.96f, 0.93f, 0.88f), new Color(0.86f, 0.66f, 0.2f), new Color(1f, 0.9f, 0.3f),
            new Color(0.85f, 0.25f, 0.2f), new Color(0.45f, 0.75f, 1f), new Color(0.5f, 0.8f, 0.45f), Color.black
        };

        public OptionsPanel(Transform parent, TMP_FontAsset menuFont, TMP_FontAsset authorialFont, Action onBack)
        {
            this.menuFont = menuFont;
            this.authorialFont = authorialFont;
            this.onBack = onBack;
            BuildOptionsPanel(parent);
        }

        /// <summary>Opens to the OPTIONS root screen with a fresh draft.</summary>
        public void Show()
        {
            draft = GameSettings.CreateEditableCopy();
            audioDraft = AudioSettingsDraft.From(AudioManager.Instance);
            draftDirty = false;
            LoadDraftIntoUi();
            UpdateApplyState();

            if (keyBindingsPanel != null)
                keyBindingsPanel.SetActive(false);
            HideStylePanels();
            optionsPanelObject.SetActive(true);
        }

        /// <summary>Hides both the OPTIONS and KEY BINDINGS screens.</summary>
        public void Hide()
        {
            optionsPanelObject.SetActive(false);
            if (keyBindingsPanel != null)
                keyBindingsPanel.SetActive(false);
            HideStylePanels();
        }

        // ----------------------------------------------------------------------
        //  Options sub-menu
        // ----------------------------------------------------------------------

        private void BuildOptionsPanel(Transform parent)
        {
            optionsPanelObject = CreatePanel(
                parent,
                "Options",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-560f, -370f),
                new Vector2(560f, 370f),
                new Color(0.035f, 0.03f, 0.024f, 0.98f));

            TMP_Text header = CreateText(
                optionsPanelObject.transform,
                "Header",
                48f,
                new Vector2(40f, -96f),
                new Vector2(-40f, -20f),
                TextAlignmentOptions.Top,
                new Color(0.88f, 0.84f, 0.72f));
            header.text = "OPTIONS";
            RectTransform optionsHeaderRect = (RectTransform)header.transform;
            optionsHeaderRect.anchorMin = new Vector2(0f, 1f);
            optionsHeaderRect.anchorMax = new Vector2(1f, 1f);
            optionsHeaderRect.pivot = new Vector2(0.5f, 1f);
            optionsHeaderRect.offsetMin = new Vector2(40f, -96f);
            optionsHeaderRect.offsetMax = new Vector2(-40f, -48f);

            var scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(optionsPanelObject.transform, false);
            RectTransform scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(40f, 150f);
            scrollRect.offsetMax = new Vector2(-40f, -110f);
            scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.018f, 0.014f, 0.6f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            optionsContent = (RectTransform)contentObject.transform;
            optionsContent.anchorMin = new Vector2(0f, 1f);
            optionsContent.anchorMax = new Vector2(1f, 1f);
            optionsContent.pivot = new Vector2(0.5f, 1f);
            optionsContent.offsetMin = Vector2.zero;
            optionsContent.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.content = optionsContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scrollObject.AddComponent<ArrowKeyScroll>();

            currentContent = optionsContent;
            PopulateOptionsRows();

            optionsStatusText = CreateText(
                optionsPanelObject.transform,
                "OptionsStatus",
                22f,
                new Vector2(40f, 100f),
                new Vector2(-40f, 140f),
                TextAlignmentOptions.Center,
                new Color(0.72f, 0.77f, 0.58f));
            optionsStatusText.text = string.Empty;
            RectTransform optionsStatusRect = (RectTransform)optionsStatusText.transform;
            optionsStatusRect.anchorMin = new Vector2(0f, 0f);
            optionsStatusRect.anchorMax = new Vector2(1f, 0f);
            optionsStatusRect.pivot = new Vector2(0.5f, 0f);
            optionsStatusRect.offsetMin = new Vector2(40f, 100f);
            optionsStatusRect.offsetMax = new Vector2(-40f, 140f);

            CreateFooterButton(optionsPanelObject.transform, "BACK", new Vector2(-360f, 0f), OnOptionsBack);
            CreateFooterButton(optionsPanelObject.transform, "DEFAULTS", new Vector2(0f, 0f), OnResetDefaults);
            applyButton = CreateFooterButton(optionsPanelObject.transform, "APPLY", new Vector2(360f, 0f), OnApply);

            optionsPanelObject.SetActive(false);
        }

        private void PopulateOptionsRows()
        {
            CreateSectionLabel("CONTROLS");
            CreateButtonRow("KEY BINDINGS", "CONFIGURE", OpenKeyBindings);
            CreateSliderRow("MOUSE SENSITIVITY", 0.02f, 1f, () => draft.mouseSensitivity, false,
                v => draft.mouseSensitivity = v);

            CreateSectionLabel("DISPLAY");
            CreateDropdownRow("RESOLUTION", ResolutionOptionLabels(), () => draft.resolutionIndex,
                i => draft.resolutionIndex = i);
            CreateToggleRow("FULL SCREEN", () => draft.fullScreen, v => draft.fullScreen = v);
            CreateToggleRow("V-SYNC", () => draft.vSync, v => draft.vSync = v);
            CreateSliderRow("FPS LIMIT", 30f, 300f, () => draft.fpsLimit, true,
                v => draft.fpsLimit = Mathf.RoundToInt(v));
            CreateSliderRow("FIELD OF VIEW", 60f, 110f, () => draft.fieldOfView, true,
                v => draft.fieldOfView = v);

            CreateSectionLabel("GRAPHICS");
            CreateDropdownRow("SHADOW QUALITY",
                new[] { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" }, () => draft.shadowQuality,
                i => draft.shadowQuality = i);
            CreateToggleRow("ANTI-ALIASING", () => draft.antiAliasing, v => draft.antiAliasing = v);

            CreateSectionLabel("AUDIO");
            CreateSliderRow("MASTER VOLUME", 0f, 1f, () => audioDraft.master, false,
                v => audioDraft.master = v);
            CreateSliderRow("VOICE / DIALOGUE VOLUME", 0f, 1f, () => audioDraft.voice, false,
                v => audioDraft.voice = v);
            CreateSliderRow("MUSIC VOLUME", 0f, 1f, () => audioDraft.music, false,
                v => audioDraft.music = v);
            CreateSliderRow("SOUND EFFECTS VOLUME", 0f, 1f, () => audioDraft.soundEffects, false,
                v => audioDraft.soundEffects = v);
            CreateSliderRow("VIDEO VOLUME", 0f, 1f, () => audioDraft.video, false,
                v => audioDraft.video = v);

            CreateSectionLabel("TEXT & SUBTITLES");
            CreateToggleRow("SHOW SUBTITLES", () => draft.subtitlesEnabled, v => draft.subtitlesEnabled = v);
            CreateButtonRow("SUBTITLE STYLE", "CUSTOMIZE", () => OpenStylePanel(true));
            CreateToggleRow("SHOW IN-GAME TEXT", () => draft.inGameTextEnabled, v => draft.inGameTextEnabled = v);
            CreateButtonRow("SCREEN TEXT STYLE", "CUSTOMIZE", () => OpenStylePanel(false));
        }

        // ----------------------------------------------------------------------
        //  Draft <-> UI synchronisation
        // ----------------------------------------------------------------------

        private void LoadDraftIntoUi()
        {
            refreshUiFromDraft?.Invoke();
            previewChanged?.Invoke();
        }

        private void MarkDirty()
        {
            draftDirty = true;
            UpdateApplyState();
            previewChanged?.Invoke();
        }

        private void SetStatus(string message)
        {
            if (optionsStatusText != null)
                optionsStatusText.text = message;
            foreach (TMP_Text text in styleStatusTexts)
                text.text = message;
        }

        private void UpdateApplyState()
        {
            if (applyButton != null)
                applyButton.interactable = draftDirty;

            SetStatus(draftDirty ? "UNSAVED CHANGES \u2014 PRESS APPLY TO SAVE." : string.Empty);
        }

        private void OnApply()
        {
            GameSettings.Apply(draft);
            ApplyAudioDraft();
            draft = GameSettings.CreateEditableCopy();
            draftDirty = false;
            LoadDraftIntoUi();
            SetStatus("SETTINGS SAVED.");
            if (applyButton != null)
                applyButton.interactable = false;
        }

        private void ApplyAudioDraft()
        {
            audioDraft.ApplyMaster();

            AudioManager manager = AudioManager.Instance;
            if (manager == null)
                return;

            manager.SetVolume(AudioBus.Voice, audioDraft.voice);
            manager.SetVolume(AudioBus.Music, audioDraft.music);
            manager.SetVolume(AudioBus.SoundEffects, audioDraft.soundEffects);
            manager.SetVolume(AudioBus.Video, audioDraft.video);
            manager.SavePreferences();
        }

        private struct AudioSettingsDraft
        {
            private const string MasterVolumeKey = "hortensia.audio.masterVolume";

            public float master;
            public float voice;
            public float music;
            public float soundEffects;
            public float video;

            public static AudioSettingsDraft From(AudioManager manager)
            {
                return manager == null
                    ? Defaults(null)
                    : new AudioSettingsDraft
                    {
                        master = PlayerPrefs.GetFloat(MasterVolumeKey, 0.25f),
                        voice = manager.GetVolume(AudioBus.Voice),
                        music = manager.GetVolume(AudioBus.Music),
                        soundEffects = manager.GetVolume(AudioBus.SoundEffects),
                        video = manager.GetVolume(AudioBus.Video),
                    };
            }

            public static AudioSettingsDraft Defaults(AudioManager manager)
            {
                return new AudioSettingsDraft
                {
                    master = 0.25f,
                    voice = 0.35f,
                    music = 0.35f,
                    soundEffects = 0.35f,
                    video = 0.05f,
                };
            }

            public void ApplyMaster()
            {
                AudioListener.volume = Mathf.Clamp01(master);
                PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
            }
        }

        private void OnResetDefaults()
        {
            draft = SettingsData.Defaults();
            audioDraft = AudioSettingsDraft.Defaults(AudioManager.Instance);
            LoadDraftIntoUi();
            MarkDirty();
            SetStatus("DEFAULTS LOADED \u2014 PRESS APPLY TO SAVE.");
        }

        private void OnOptionsBack()
        {
            if (draftDirty)
            {
                if (optionsStatusText != null &&
                    optionsStatusText.text != "DISCARD UNSAVED CHANGES? PRESS BACK AGAIN.")
                {
                    optionsStatusText.text = "DISCARD UNSAVED CHANGES? PRESS BACK AGAIN.";
                    return;
                }
            }

            draftDirty = false;
            Hide();
            onBack?.Invoke();
        }

        // ----------------------------------------------------------------------
        //  Key bindings sub-screen
        // ----------------------------------------------------------------------

        private void OpenKeyBindings()
        {
            if (keyBindingsPanel == null)
                BuildKeyBindingsPanel(optionsPanelObject.transform.parent);

            rebinding.LoadFromJson(draft.inputOverridesJson);
            RefreshKeyBindingRows();

            keyBindingsStatusText.text =
                "PRESS REBIND, THEN A NEW KEY OR MOUSE BUTTON. USE CANCEL TO ABORT.";
            optionsPanelObject.SetActive(false);
            keyBindingsPanel.SetActive(true);
        }

        private void BuildKeyBindingsPanel(Transform parent)
        {
            rebinding = new KeyRebinding();

            keyBindingsPanel = CreatePanel(
                parent,
                "Key Bindings",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-560f, -370f),
                new Vector2(560f, 370f),
                new Color(0.035f, 0.03f, 0.024f, 0.98f));

            TMP_Text header = CreateText(
                keyBindingsPanel.transform,
                "Header",
                48f,
                new Vector2(40f, -96f),
                new Vector2(-40f, -20f),
                TextAlignmentOptions.Top,
                new Color(0.88f, 0.84f, 0.72f));
            header.text = "KEY BINDINGS";
            RectTransform keyHeaderRect = (RectTransform)header.transform;
            keyHeaderRect.anchorMin = new Vector2(0f, 1f);
            keyHeaderRect.anchorMax = new Vector2(1f, 1f);
            keyHeaderRect.pivot = new Vector2(0.5f, 1f);
            keyHeaderRect.offsetMin = new Vector2(40f, -96f);
            keyHeaderRect.offsetMax = new Vector2(-40f, -24f);

            var scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(keyBindingsPanel.transform, false);
            RectTransform scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(40f, 150f);
            scrollRect.offsetMax = new Vector2(-40f, -110f);
            scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.018f, 0.014f, 0.6f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scrollObject.AddComponent<ArrowKeyScroll>();

            keyValueLabels.Clear();
            foreach (FirstPersonController.RebindableAction entry in rebinding.Entries)
                CreateKeyBindingRow(content, entry);

            keyBindingsStatusText = CreateText(
                keyBindingsPanel.transform,
                "KeyBindingsStatus",
                22f,
                new Vector2(40f, 100f),
                new Vector2(-40f, 148f),
                TextAlignmentOptions.Center,
                new Color(0.72f, 0.77f, 0.58f));
            keyBindingsStatusText.text = string.Empty;
            RectTransform keyStatusRect = (RectTransform)keyBindingsStatusText.transform;
            keyStatusRect.anchorMin = new Vector2(0f, 0f);
            keyStatusRect.anchorMax = new Vector2(1f, 0f);
            keyStatusRect.pivot = new Vector2(0.5f, 0f);
            keyStatusRect.offsetMin = new Vector2(40f, 100f);
            keyStatusRect.offsetMax = new Vector2(-40f, 148f);

            CreateFooterButton(keyBindingsPanel.transform, "BACK", new Vector2(-390f, 24f), OnKeyBindingsBack, 250f);
            CreateFooterButton(keyBindingsPanel.transform, "RESET KEYS", new Vector2(-130f, 24f), OnResetKeyBindings, 250f);
            CreateFooterButton(keyBindingsPanel.transform, "CANCEL", new Vector2(130f, 24f), OnCancelActiveRebind, 250f);
            CreateFooterButton(keyBindingsPanel.transform, "APPLY", new Vector2(390f, 24f), OnApplyKeyBindings, 250f);

            keyBindingsPanel.SetActive(false);
        }

        private void CreateKeyBindingRow(Transform contentParent, FirstPersonController.RebindableAction entry)
        {
            var rowObject = new GameObject(entry.Id, typeof(RectTransform), typeof(LayoutElement));
            rowObject.transform.SetParent(contentParent, false);
            rowObject.GetComponent<LayoutElement>().preferredHeight = 60f;
            RectTransform row = (RectTransform)rowObject.transform;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(row, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(6f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = menuFont;
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(0.88f, 0.84f, 0.72f);
            label.raycastTarget = false;
            label.text = entry.DisplayName;

            var valueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            valueObject.transform.SetParent(row, false);
            RectTransform valueRect = (RectTransform)valueObject.transform;
            valueRect.anchorMin = new Vector2(0.5f, 0f);
            valueRect.anchorMax = new Vector2(0.78f, 1f);
            valueRect.offsetMin = new Vector2(10f, 0f);
            valueRect.offsetMax = new Vector2(-10f, 0f);
            TextMeshProUGUI value = valueObject.GetComponent<TextMeshProUGUI>();
            value.font = menuFont;
            value.fontSize = 26f;
            value.alignment = TextAlignmentOptions.Center;
            value.color = new Color(0.82f, 0.78f, 0.64f);
            value.raycastTarget = false;
            value.text = "\u2014";
            keyValueLabels[entry.Id] = value;

            var buttonObject = new GameObject("Rebind", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(row, false);
            RectTransform buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = new Vector2(0.78f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.offsetMin = new Vector2(6f, -22f);
            buttonRect.offsetMax = new Vector2(-6f, 22f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            button.colors = colors;

            FirstPersonController.RebindableAction captured = entry;
            button.onClick.AddListener(() => BeginRebind(captured));

            TMP_Text buttonText = CreateText(
                buttonObject.transform,
                "Label",
                22f,
                new Vector2(8f, 4f),
                new Vector2(-8f, -4f),
                TextAlignmentOptions.Center,
                new Color(0.9f, 0.86f, 0.74f));
            buttonText.text = "REBIND";
        }

        private void RefreshKeyBindingRows()
        {
            foreach (FirstPersonController.RebindableAction entry in rebinding.Entries)
            {
                if (keyValueLabels.TryGetValue(entry.Id, out TMP_Text label) && label != null)
                    label.text = rebinding.DisplayKeyFor(entry);
            }
        }

        private void BeginRebind(FirstPersonController.RebindableAction entry)
        {
            if (activeRebind != null)
                return;

            if (keyValueLabels.TryGetValue(entry.Id, out TMP_Text label) && label != null)
                label.text = "PRESS A KEY...";
            keyBindingsStatusText.text = $"REBINDING {entry.DisplayName} \u2014 PRESS A KEY.";

            activeRebind = rebinding.StartRebind(entry, AllowMouseRebinding, () =>
            {
                activeRebind = null;
                draft.inputOverridesJson = rebinding.ToJson();
                MarkDirty();
                RefreshKeyBindingRows();
                keyBindingsStatusText.text =
                    "PRESS REBIND, THEN A NEW KEY OR MOUSE BUTTON. USE CANCEL TO ABORT.";
            });
        }

        private void OnCancelActiveRebind()
        {
            if (activeRebind != null)
            {
                activeRebind.Cancel();
                return;
            }

            keyBindingsStatusText.text = "NO REBIND IN PROGRESS.";
        }

        private void OnResetKeyBindings()
        {
            if (activeRebind != null)
                activeRebind.Cancel();

            rebinding.ResetToDefaults();
            draft.inputOverridesJson = rebinding.ToJson();
            MarkDirty();
            RefreshKeyBindingRows();
            keyBindingsStatusText.text = "DEFAULT KEYS RESTORED \u2014 PRESS APPLY TO SAVE.";
        }

        private void OnApplyKeyBindings()
        {
            if (activeRebind != null)
                activeRebind.Cancel();

            draft.inputOverridesJson = rebinding.ToJson();
            GameSettings.Apply(draft);

            draft = GameSettings.CreateEditableCopy();
            draftDirty = false;
            rebinding.LoadFromJson(draft.inputOverridesJson);
            RefreshKeyBindingRows();
            if (applyButton != null)
                applyButton.interactable = false;
            keyBindingsStatusText.text = "KEY BINDINGS SAVED.";
        }

        private void OnKeyBindingsBack()
        {
            if (activeRebind != null)
                activeRebind.Cancel();

            keyBindingsPanel.SetActive(false);
            optionsPanelObject.SetActive(true);
        }

        private void HideStylePanels()
        {
            if (subtitleStylePanel != null)
                subtitleStylePanel.SetActive(false);
            if (screenStylePanel != null)
                screenStylePanel.SetActive(false);
        }

        private void OpenStylePanel(bool subtitles)
        {
            if (subtitles && subtitleStylePanel == null)
                subtitleStylePanel = BuildStylePanel(true);
            if (!subtitles && screenStylePanel == null)
                screenStylePanel = BuildStylePanel(false);

            optionsPanelObject.SetActive(false);
            (subtitles ? subtitleStylePanel : screenStylePanel).SetActive(true);
            LoadDraftIntoUi();
            UpdateApplyState();
        }

        private void CloseStylePanel()
        {
            HideStylePanels();
            optionsPanelObject.SetActive(true);
        }

        private GameObject BuildStylePanel(bool subtitles)
        {
            Transform parent = optionsPanelObject.transform.parent;
            GameObject panel = CreatePanel(parent, subtitles ? "Subtitle Style" : "Screen Text Style",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-560f, -440f), new Vector2(560f, 440f),
                new Color(0.035f, 0.03f, 0.024f, 0.98f));

            TMP_Text header = CreateText(panel.transform, "Header", 44f, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Top, new Color(0.88f, 0.84f, 0.72f));
            header.text = subtitles ? "SUBTITLE STYLE" : "SCREEN TEXT STYLE";
            SetTopStrip(header.rectTransform, 20f, 70f);

            RectTransform previewArea = (RectTransform)new GameObject("Preview", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).transform;
            previewArea.SetParent(panel.transform, false);
            SetTopStrip(previewArea, 80f, 300f);
            Image backdrop = previewArea.GetComponent<Image>();
            backdrop.sprite = BackdropSprite();
            backdrop.raycastTarget = false;

            TMP_Text previewTag = CreateText(previewArea, "Tag", 18f, new Vector2(10f, 6f), new Vector2(-10f, -6f),
                TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.55f));
            previewTag.text = "PREVIEW";

            if (subtitles)
                BuildSubtitlePreview(previewArea);
            else
                BuildScreenPreview(previewArea);

            var scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(panel.transform, false);
            RectTransform scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(40f, 150f);
            scrollRect.offsetMax = new Vector2(-40f, -315f);
            scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.018f, 0.014f, 0.6f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(scrollObject.transform, false);
            RectTransform content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = scrollRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scrollObject.AddComponent<ArrowKeyScroll>();

            RectTransform previousContent = currentContent;
            currentContent = content;
            PopulateStyleRows(subtitles);
            currentContent = previousContent;

            TMP_Text status = CreateText(panel.transform, "Status", 22f, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, new Color(0.72f, 0.77f, 0.58f));
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.offsetMin = new Vector2(40f, 100f);
            statusRect.offsetMax = new Vector2(-40f, 140f);
            styleStatusTexts.Add(status);

            CreateFooterButton(panel.transform, "BACK", new Vector2(-360f, 20f), CloseStylePanel);
            CreateFooterButton(panel.transform, "DEFAULTS", new Vector2(0f, 20f), () => ResetStyle(subtitles));
            CreateFooterButton(panel.transform, "APPLY", new Vector2(360f, 20f), OnApply);

            panel.SetActive(false);
            return panel;
        }

        private TextStyle GetStyle(bool subtitles) => subtitles ? draft.subtitleStyle : draft.screenStyle;

        private void EditStyle(bool subtitles, Func<TextStyle, TextStyle> edit)
        {
            if (subtitles)
                draft.subtitleStyle = edit(draft.subtitleStyle);
            else
                draft.screenStyle = edit(draft.screenStyle);
        }

        private void ResetStyle(bool subtitles)
        {
            EditStyle(subtitles, _ => subtitles ? TextStyleDefaults.SubtitleStyle : TextStyleDefaults.ScreenStyle);
            LoadDraftIntoUi();
            MarkDirty();
            SetStatus("DEFAULT STYLE LOADED — PRESS APPLY TO SAVE.");
        }

        private void PopulateStyleRows(bool subtitles)
        {
            bool sub = subtitles;
            float minScale = sub ? GameSettings.SubtitleMinScale : GameSettings.ScreenMinScale;
            float maxScale = sub ? GameSettings.SubtitleMaxScale : GameSettings.ScreenMaxScale;
            Func<float, string> percent = v => $"{Mathf.RoundToInt(v * 100f)}%";

            CreateSectionLabel("TEXT");
            CreateSliderRow("SIZE", minScale, maxScale, () => GetStyle(sub).scale, false,
                v => EditStyle(sub, s => { s.scale = v; return s; }), percent);
            CreateColorRow("COLOUR", () => GetStyle(sub).textColor,
                c => EditStyle(sub, s => { s.textColor = c; return s; }));
            CreateSliderRow("OPACITY", 0f, 1f, () => GetStyle(sub).textColor.a, false,
                v => EditStyle(sub, s => { s.textColor.a = v; return s; }), percent);
            CreateToggleRow("BOLD", () => GetStyle(sub).bold,
                v => EditStyle(sub, s => { s.bold = v; return s; }));
            CreateSliderRow("FONT WEIGHT", -1f, 1f, () => GetStyle(sub).thickness, false,
                v => EditStyle(sub, s => { s.thickness = v; return s; }),
                v => { int n = Mathf.RoundToInt(v * 100f); return n > 0 ? $"+{n}" : n.ToString(); });

            CreateSectionLabel("OUTLINE");
            CreateSliderRow("OUTLINE THICKNESS", 0f, 1f, () => GetStyle(sub).outlineWidth, false,
                v => EditStyle(sub, s => { s.outlineWidth = v; return s; }), percent);
            CreateColorRow("OUTLINE COLOUR", () => GetStyle(sub).outlineColor,
                c => EditStyle(sub, s => { s.outlineColor = c; return s; }));

            CreateSectionLabel(sub ? "SPEAKER NAME" : "MESSAGES");
            CreateColorRow(sub ? "NAME COLOUR" : "MESSAGE COLOUR", () => GetStyle(sub).accentColor,
                c => EditStyle(sub, s => { s.accentColor = c; return s; }));
            CreateSliderRow(sub ? "NAME OPACITY" : "MESSAGE OPACITY", 0f, 1f, () => GetStyle(sub).accentColor.a, false,
                v => EditStyle(sub, s => { s.accentColor.a = v; return s; }), percent);

            CreateSectionLabel(sub ? "BACKGROUND" : "BUTTONS & LABELS");
            CreateColorRow("BACKGROUND COLOUR", () => GetStyle(sub).backgroundColor,
                c => EditStyle(sub, s => { s.backgroundColor = c; return s; }));
            CreateSliderRow("BACKGROUND OPACITY", 0f, 1f, () => GetStyle(sub).backgroundColor.a, false,
                v => EditStyle(sub, s => { s.backgroundColor.a = v; return s; }), percent);
        }

        private void CreateColorRow(string labelText, Func<Color> getter, Action<Color> setter)
        {
            CreateRow(labelText, labelText, out Transform anchor);

            var swatchObject = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            swatchObject.transform.SetParent(anchor, false);
            RectTransform swatchRect = (RectTransform)swatchObject.transform;
            swatchRect.anchorMin = new Vector2(0f, 0f);
            swatchRect.anchorMax = new Vector2(0.3f, 1f);
            swatchRect.offsetMin = swatchRect.offsetMax = Vector2.zero;
            Image swatch = swatchObject.GetComponent<Image>();

            TMP_Text hex = CreateText(anchor, "Hex", 24f, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Left, new Color(0.82f, 0.78f, 0.64f), authorialFont);
            hex.rectTransform.anchorMin = new Vector2(0.34f, 0f);
            hex.rectTransform.offsetMin = Vector2.zero;
            hex.rectTransform.offsetMax = Vector2.zero;

            var editorObject = new GameObject(labelText + " Editor", typeof(RectTransform), typeof(VerticalLayoutGroup));
            editorObject.transform.SetParent(currentContent, false);
            VerticalLayoutGroup editorLayout = editorObject.GetComponent<VerticalLayoutGroup>();
            editorLayout.padding = new RectOffset(40, 0, 0, 6);
            editorLayout.spacing = 6f;
            editorLayout.childControlWidth = true;
            editorLayout.childControlHeight = true;
            editorLayout.childForceExpandWidth = true;
            editorLayout.childForceExpandHeight = false;

            float h = 0f, s = 0f, v = 1f;
            void Sync()
            {
                Color c = getter();
                Color.RGBToHSV(c, out float nh, out float ns, out float nv);
                if (ns > 0.001f && nv > 0.001f)
                    h = nh;
                if (nv > 0.001f)
                    s = ns;
                v = nv;
                swatch.color = new Color(c.r, c.g, c.b, 1f);
                hex.text = TextStyling.Hex(c) + "   ▼";
            }
            void Push()
            {
                Color c = Color.HSVToRGB(h, s, v);
                c.a = getter().a;
                setter(c);
                swatch.color = new Color(c.r, c.g, c.b, 1f);
                hex.text = TextStyling.Hex(c) + "   ▼";
            }

            refreshUiFromDraft += Sync;

            RectTransform previousContent = currentContent;
            currentContent = (RectTransform)editorObject.transform;
            Slider hue = CreateSliderRow("HUE", 0f, 360f, () => h * 360f, false, x => { h = x / 360f; Push(); },
                x => $"{Mathf.RoundToInt(x)}°");
            Image hueBackground = hue.transform.Find("Background").GetComponent<Image>();
            hueBackground.sprite = HueSprite();
            hueBackground.color = Color.white;
            hue.transform.Find("Fill Area").gameObject.SetActive(false);
            CreateSliderRow("SATURATION", 0f, 1f, () => s, false, x => { s = x; Push(); },
                x => $"{Mathf.RoundToInt(x * 100f)}%");
            CreateSliderRow("BRIGHTNESS", 0f, 1f, () => v, false, x => { v = x; Push(); },
                x => $"{Mathf.RoundToInt(x * 100f)}%");
            CreatePresetRow(c =>
            {
                c.a = getter().a;
                setter(c);
                refreshUiFromDraft?.Invoke();
                MarkDirty();
            });
            currentContent = previousContent;

            editorObject.SetActive(false);
            swatchObject.GetComponent<Button>().onClick.AddListener(() =>
            {
                editorObject.SetActive(!editorObject.activeSelf);
                hex.text = TextStyling.Hex(getter()) + (editorObject.activeSelf ? "   ▲" : "   ▼");
            });
        }

        private void CreatePresetRow(Action<Color> pick)
        {
            CreateRow("Presets", "PRESETS", out Transform anchor);
            HorizontalLayoutGroup row = anchor.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            foreach (Color preset in PresetColors)
            {
                var swatchObject = new GameObject("Preset", typeof(RectTransform), typeof(Image), typeof(Button));
                swatchObject.transform.SetParent(anchor, false);
                swatchObject.GetComponent<Image>().color = preset;
                Color captured = preset;
                swatchObject.GetComponent<Button>().onClick.AddListener(() => pick(captured));
            }
        }

        private void BuildSubtitlePreview(RectTransform area)
        {
            TMP_FontAsset font = DialogueRunner.ActiveFont != null ? DialogueRunner.ActiveFont : menuFont;
            RectTransform box = (RectTransform)new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).transform;
            box.SetParent(area, false);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0f);
            box.anchoredPosition = new Vector2(0f, 16f);
            Image background = box.GetComponent<Image>();
            background.raycastTarget = false;
            CanvasGroup group = box.GetComponent<CanvasGroup>();

            TMP_Text speaker = CreateText(box, "Speaker", 20f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Top, Color.white, font);
            TMP_Text body = CreateText(box, "Body", 24f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Top, Color.white, font);
            body.textWrappingMode = TextWrappingModes.Normal;
            speaker.text = "UNGARETTI";
            body.text = "Sfòrilo... lumàrde scrènzia. Gòrvalo dù brùnzo.";

            previewChanged += () =>
            {
                if (subtitleStylePanel == null || !subtitleStylePanel.activeSelf)
                    return;
                TextStyle style = draft.subtitleStyle;
                const float preview = 0.55f;
                float k = preview * style.scale;
                Vector2 size = DialogueRunner.ActiveBoxSize * preview;
                box.sizeDelta = size;
                background.color = style.backgroundColor;
                group.alpha = draft.subtitlesEnabled ? 1f : 0.3f;

                float speakerSize = DialogueRunner.ActiveSpeakerSize * k;
                float textSize = DialogueRunner.ActiveTextSize * k;
                speaker.fontSize = speakerSize;
                body.fontSize = textSize;
                speaker.rectTransform.offsetMin = new Vector2(16f, size.y - 12f - speakerSize * 1.3f);
                speaker.rectTransform.offsetMax = new Vector2(-16f, -12f);
                body.rectTransform.offsetMin = new Vector2(16f + DialogueRunner.ActiveTextSize * preview, 10f);
                body.rectTransform.offsetMax = new Vector2(-16f - DialogueRunner.ActiveTextSize * preview, -12f - speakerSize * 1.4f);
                TextStyling.Apply(speaker, style, style.accentColor, DialogueRunner.ActiveBold);
                TextStyling.Apply(body, style, style.textColor, DialogueRunner.ActiveBold);
            };
        }

        private void BuildScreenPreview(RectTransform area)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/OSerif-Regular") ?? menuFont;

            Image button = CreatePreviewPlate(area, new Vector2(-230f, -20f));
            TMP_Text buttonLabel = CreateText(button.transform, "Label", 30f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white, font);
            buttonLabel.text = "CONTINUE";

            Image prompt = CreatePreviewPlate(area, new Vector2(230f, -20f));
            TMP_Text promptLabel = CreateText(prompt.transform, "Label", 30f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white, font);
            promptLabel.text = "TALK";

            TMP_Text message = CreateText(area, "Message", 22f, Vector2.zero, Vector2.zero, TextAlignmentOptions.Bottom, Color.white, font);
            message.rectTransform.offsetMin = new Vector2(10f, 14f);
            message.rectTransform.offsetMax = new Vector2(-10f, -10f);
            message.text = "SLOT 01 SAVED.";

            previewChanged += () =>
            {
                if (screenStylePanel == null || !screenStylePanel.activeSelf)
                    return;
                TextStyle style = draft.screenStyle;
                float k = style.scale;
                button.rectTransform.sizeDelta = new Vector2(380f, 64f);
                button.color = style.backgroundColor;
                buttonLabel.fontSize = 30f * k;
                TextStyling.Apply(buttonLabel, style, style.textColor);

                prompt.color = style.backgroundColor;
                promptLabel.fontSize = 30f * k;
                TextStyling.Apply(promptLabel, style, style.textColor);
                prompt.rectTransform.sizeDelta = promptLabel.GetPreferredValues("TALK") + new Vector2(36f, 12f);

                message.fontSize = 22f * k;
                TextStyling.Apply(message, style, style.accentColor);
                message.alpha = draft.inGameTextEnabled ? message.alpha : message.alpha * 0.3f;
            };
        }

        private static Image CreatePreviewPlate(RectTransform area, Vector2 position)
        {
            RectTransform plate = (RectTransform)new GameObject("Plate", typeof(RectTransform), typeof(Image)).transform;
            plate.SetParent(area, false);
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = position;
            Image image = plate.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void SetTopStrip(RectTransform rect, float top, float bottom)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(40f, -bottom);
            rect.offsetMax = new Vector2(-40f, -top);
        }

        private static Sprite HueSprite()
        {
            if (hueSprite != null)
                return hueSprite;
            var texture = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
                texture.SetPixel(x, 0, Color.HSVToRGB(x / 63f, 1f, 1f));
            texture.Apply();
            hueSprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 1f), new Vector2(0.5f, 0.5f));
            return hueSprite;
        }

        private static Sprite BackdropSprite()
        {
            if (backdropSprite != null)
                return backdropSprite;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixel(0, 0, new Color(0.16f, 0.14f, 0.1f));
            texture.SetPixel(1, 0, new Color(0.3f, 0.22f, 0.12f));
            texture.SetPixel(0, 1, new Color(0.55f, 0.62f, 0.68f));
            texture.SetPixel(1, 1, new Color(0.82f, 0.74f, 0.55f));
            texture.Apply();
            backdropSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            return backdropSprite;
        }

        private static string[] ResolutionOptionLabels()
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
                return new[] { $"{Screen.width} X {Screen.height}" };

            var labels = new string[resolutions.Length];
            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution r = resolutions[i];
                labels[i] = $"{r.width} X {r.height} @ {Mathf.RoundToInt((float)r.refreshRateRatio.value)}HZ";
            }

            return labels;
        }

        // ----------------------------------------------------------------------
        //  Row builders (bound to the draft via getters/setters)
        // ----------------------------------------------------------------------

        private void CreateSectionLabel(string text)
        {
            var rowObject = new GameObject(text, typeof(RectTransform), typeof(LayoutElement));
            rowObject.transform.SetParent(currentContent, false);
            rowObject.GetComponent<LayoutElement>().preferredHeight = 46f;

            TMP_Text label = CreateStretchText(rowObject.transform, 30f,
                TextAlignmentOptions.Left, new Color(0.72f, 0.77f, 0.58f));
            label.text = text;
            label.fontStyle = FontStyles.UpperCase;
        }

        private GameObject CreateRow(string name, string labelText, out Transform controlAnchor)
        {
            var rowObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            rowObject.transform.SetParent(currentContent, false);
            rowObject.GetComponent<LayoutElement>().preferredHeight = 60f;
            RectTransform row = (RectTransform)rowObject.transform;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(row, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(6f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = menuFont;
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(0.88f, 0.84f, 0.72f);
            label.raycastTarget = false;
            label.text = labelText;

            var anchorObject = new GameObject("Control", typeof(RectTransform));
            anchorObject.transform.SetParent(row, false);
            RectTransform anchorRect = (RectTransform)anchorObject.transform;
            anchorRect.anchorMin = new Vector2(0.5f, 0f);
            anchorRect.anchorMax = new Vector2(1f, 1f);
            anchorRect.offsetMin = new Vector2(10f, 8f);
            anchorRect.offsetMax = new Vector2(-6f, -8f);
            controlAnchor = anchorObject.transform;

            return rowObject;
        }

        private Slider CreateSliderRow(string labelText, float min, float max, Func<float> getter,
            bool wholeNumbers, Action<float> setter, Func<float, string> format = null)
        {
            Func<float, string> formatter = format ?? (v => FormatSliderValue(v, min, max, wholeNumbers));
            CreateRow(labelText, labelText, out Transform anchor);

            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(anchor, false);
            RectTransform sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.78f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.offsetMin = new Vector2(0f, -8f);
            sliderRect.offsetMax = new Vector2(0f, 8f);

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            RectTransform bgRect = (RectTransform)backgroundObject.transform;
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            backgroundObject.GetComponent<Image>().color = new Color(0.12f, 0.11f, 0.09f, 1f);

            var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform fillArea = (RectTransform)fillAreaObject.transform;
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(0f, 0f);
            fillArea.offsetMax = new Vector2(0f, 0f);

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            RectTransform fillRect = (RectTransform)fillObject.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(10f, 0f);
            fillObject.GetComponent<Image>().color = new Color(0.5f, 0.55f, 0.38f, 1f);

            var handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform handleArea = (RectTransform)handleAreaObject.transform;
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(handleAreaObject.transform, false);
            RectTransform handleRect = (RectTransform)handleObject.transform;
            handleRect.sizeDelta = new Vector2(18f, 0f);
            handleObject.GetComponent<Image>().color = new Color(0.82f, 0.78f, 0.64f, 1f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleObject.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;

            TMP_Text valueText = CreateText(
                anchor,
                "Value",
                24f,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                TextAlignmentOptions.Right,
                new Color(0.82f, 0.78f, 0.64f),
                authorialFont);
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(0.8f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = formatter(v);
                setter(v);
                MarkDirty();
            });

            refreshUiFromDraft += () =>
            {
                float value = Mathf.Clamp(getter(), min, max);
                slider.SetValueWithoutNotify(value);
                valueText.text = formatter(value);
            };
            return slider;
        }

        private void CreateToggleRow(string labelText, Func<bool> getter, Action<bool> setter)
        {
            CreateRow(labelText, labelText, out Transform anchor);

            var toggleObject = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(anchor, false);
            RectTransform toggleRect = (RectTransform)toggleObject.transform;
            toggleRect.anchorMin = new Vector2(0f, 0.5f);
            toggleRect.anchorMax = new Vector2(0f, 0.5f);
            toggleRect.pivot = new Vector2(0f, 0.5f);
            toggleRect.sizeDelta = new Vector2(56f, 40f);
            toggleRect.anchoredPosition = new Vector2(0f, 0f);

            var checkBackgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            checkBackgroundObject.transform.SetParent(toggleObject.transform, false);
            RectTransform checkBg = (RectTransform)checkBackgroundObject.transform;
            checkBg.anchorMin = new Vector2(0f, 0.5f);
            checkBg.anchorMax = new Vector2(0f, 0.5f);
            checkBg.pivot = new Vector2(0f, 0.5f);
            checkBg.sizeDelta = new Vector2(40f, 40f);
            checkBackgroundObject.GetComponent<Image>().color = new Color(0.12f, 0.11f, 0.09f, 1f);

            var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmarkObject.transform.SetParent(checkBackgroundObject.transform, false);
            RectTransform checkRect = (RectTransform)checkmarkObject.transform;
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(26f, 26f);
            checkmarkObject.GetComponent<Image>().color = new Color(0.72f, 0.77f, 0.58f, 1f);

            TMP_Text stateText = CreateText(
                anchor,
                "State",
                24f,
                Vector2.zero,
                Vector2.zero,
                TextAlignmentOptions.Left,
                new Color(0.82f, 0.78f, 0.64f));
            RectTransform stateRect = stateText.rectTransform;
            stateRect.anchorMin = new Vector2(0f, 0f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.offsetMin = new Vector2(60f, 0f);
            stateRect.offsetMax = new Vector2(0f, 0f);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = checkBackgroundObject.GetComponent<Image>();
            toggle.graphic = checkmarkObject.GetComponent<Image>();
            toggle.onValueChanged.AddListener(v =>
            {
                stateText.text = v ? "ON" : "OFF";
                setter(v);
                MarkDirty();
            });

            refreshUiFromDraft += () =>
            {
                bool value = getter();
                toggle.SetIsOnWithoutNotify(value);
                stateText.text = value ? "ON" : "OFF";
            };
        }

        private void CreateDropdownRow(string labelText, string[] options, Func<int> getter,
            Action<int> setter)
        {
            CreateRow(labelText, labelText, out Transform anchor);

            var dropdownObject = new GameObject("Dropdown",
                typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            dropdownObject.transform.SetParent(anchor, false);
            RectTransform dropdownRect = (RectTransform)dropdownObject.transform;
            dropdownRect.anchorMin = new Vector2(0f, 0.5f);
            dropdownRect.anchorMax = new Vector2(1f, 0.5f);
            dropdownRect.pivot = new Vector2(0.5f, 0.5f);
            dropdownRect.offsetMin = new Vector2(0f, -20f);
            dropdownRect.offsetMax = new Vector2(0f, 20f);
            dropdownObject.GetComponent<Image>().color = new Color(0.12f, 0.11f, 0.09f, 1f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-30f, 0f);
            TextMeshProUGUI captionText = labelObject.GetComponent<TextMeshProUGUI>();
            captionText.font = menuFont;
            captionText.fontSize = 22f;
            captionText.alignment = TextAlignmentOptions.Left;
            captionText.color = new Color(0.82f, 0.78f, 0.64f);

            var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            arrowObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform arrowRect = (RectTransform)arrowObject.transform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(28f, 28f);
            arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            TextMeshProUGUI arrowText = arrowObject.GetComponent<TextMeshProUGUI>();
            arrowText.font = menuFont;
            arrowText.fontSize = 22f;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.color = new Color(0.82f, 0.78f, 0.64f);
            arrowText.text = "v";

            var templateObject = new GameObject("Template",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Canvas), typeof(GraphicRaycaster));
            templateObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform templateRect = (RectTransform)templateObject.transform;
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 220f);
            templateObject.GetComponent<Image>().color = new Color(0.06f, 0.055f, 0.045f, 1f);

            var templateViewportObject = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(Mask));
            templateViewportObject.transform.SetParent(templateObject.transform, false);
            RectTransform templateViewport = (RectTransform)templateViewportObject.transform;
            templateViewport.anchorMin = new Vector2(0f, 0f);
            templateViewport.anchorMax = new Vector2(1f, 1f);
            templateViewport.offsetMin = Vector2.zero;
            templateViewport.offsetMax = Vector2.zero;
            templateViewport.pivot = new Vector2(0f, 1f);
            templateViewportObject.GetComponent<Mask>().showMaskGraphic = false;
            templateViewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var templateContentObject = new GameObject("Content", typeof(RectTransform));
            templateContentObject.transform.SetParent(templateViewportObject.transform, false);
            RectTransform templateContent = (RectTransform)templateContentObject.transform;
            templateContent.anchorMin = new Vector2(0f, 1f);
            templateContent.anchorMax = new Vector2(1f, 1f);
            templateContent.pivot = new Vector2(0.5f, 1f);
            templateContent.sizeDelta = new Vector2(0f, 44f);

            var templateItemObject = new GameObject("Item",
                typeof(RectTransform), typeof(Toggle));
            templateItemObject.transform.SetParent(templateContentObject.transform, false);
            RectTransform templateItem = (RectTransform)templateItemObject.transform;
            templateItem.anchorMin = new Vector2(0f, 0.5f);
            templateItem.anchorMax = new Vector2(1f, 0.5f);
            templateItem.sizeDelta = new Vector2(0f, 44f);

            var itemBackgroundObject = new GameObject("Item Background",
                typeof(RectTransform), typeof(Image));
            itemBackgroundObject.transform.SetParent(templateItemObject.transform, false);
            RectTransform itemBg = (RectTransform)itemBackgroundObject.transform;
            itemBg.anchorMin = Vector2.zero;
            itemBg.anchorMax = Vector2.one;
            itemBg.offsetMin = Vector2.zero;
            itemBg.offsetMax = Vector2.zero;
            itemBackgroundObject.GetComponent<Image>().color = new Color(0.1f, 0.09f, 0.07f, 1f);

            var itemCheckObject = new GameObject("Item Checkmark",
                typeof(RectTransform), typeof(Image));
            itemCheckObject.transform.SetParent(templateItemObject.transform, false);
            RectTransform itemCheck = (RectTransform)itemCheckObject.transform;
            itemCheck.anchorMin = new Vector2(0f, 0.5f);
            itemCheck.anchorMax = new Vector2(0f, 0.5f);
            itemCheck.pivot = new Vector2(0f, 0.5f);
            itemCheck.sizeDelta = new Vector2(24f, 24f);
            itemCheck.anchoredPosition = new Vector2(8f, 0f);
            itemCheckObject.GetComponent<Image>().color = new Color(0.72f, 0.77f, 0.58f, 1f);

            var itemLabelObject = new GameObject("Item Label",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            itemLabelObject.transform.SetParent(templateItemObject.transform, false);
            RectTransform itemLabel = (RectTransform)itemLabelObject.transform;
            itemLabel.anchorMin = Vector2.zero;
            itemLabel.anchorMax = Vector2.one;
            itemLabel.offsetMin = new Vector2(40f, 2f);
            itemLabel.offsetMax = new Vector2(-10f, -2f);
            TextMeshProUGUI itemLabelText = itemLabelObject.GetComponent<TextMeshProUGUI>();
            itemLabelText.font = menuFont;
            itemLabelText.fontSize = 22f;
            itemLabelText.alignment = TextAlignmentOptions.Left;
            itemLabelText.color = new Color(0.86f, 0.82f, 0.7f);

            Toggle itemToggle = templateItemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackgroundObject.GetComponent<Image>();
            itemToggle.graphic = itemCheckObject.GetComponent<Image>();

            ScrollRect templateScroll = templateObject.GetComponent<ScrollRect>();
            templateScroll.content = templateContent;
            templateScroll.viewport = templateViewport;
            templateScroll.horizontal = false;
            templateScroll.vertical = true;

            templateObject.SetActive(false);

            TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.template = templateRect;
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabelText;
            dropdown.ClearOptions();
            foreach (string option in options)
                dropdown.options.Add(new TMP_Dropdown.OptionData(option));
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(i =>
            {
                setter(i);
                MarkDirty();
            });

            int optionCount = options.Length;
            refreshUiFromDraft += () =>
            {
                int value = Mathf.Clamp(getter(), 0, Mathf.Max(0, optionCount - 1));
                dropdown.SetValueWithoutNotify(value);
                dropdown.RefreshShownValue();
            };
        }

        private void CreateButtonRow(string labelText, string buttonLabel, UnityEngine.Events.UnityAction action)
        {
            CreateRow(labelText, labelText, out Transform anchor);

            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(anchor, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, -22f);
            rect.offsetMax = new Vector2(0f, 22f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text text = CreateText(
                buttonObject.transform,
                "Label",
                24f,
                new Vector2(10f, 4f),
                new Vector2(-10f, -4f),
                TextAlignmentOptions.Center,
                new Color(0.9f, 0.86f, 0.74f));
            text.text = buttonLabel;
        }

        private Button CreateFooterButton(Transform parent, string label, Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action, float width = 320f)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, 64f);
            rect.anchoredPosition = anchoredPosition;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.115f, 0.1f, 0.075f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.77f, 0.58f);
            colors.pressedColor = new Color(0.48f, 0.52f, 0.37f);
            colors.disabledColor = new Color(0.34f, 0.32f, 0.28f, 0.65f);
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text text = CreateText(
                buttonObject.transform,
                "Label",
                28f,
                new Vector2(10f, 6f),
                new Vector2(-10f, -6f),
                TextAlignmentOptions.Center,
                new Color(0.9f, 0.86f, 0.74f));
            text.text = label;
            return button;
        }

        private static string FormatSliderValue(float value, float min, float max, bool wholeNumbers)
        {
            if (wholeNumbers)
                return Mathf.RoundToInt(value).ToString();

            float normalized = Mathf.Approximately(max, min) ? 0f : (value - min) / (max - min);
            return $"{Mathf.RoundToInt(normalized * 100f)}%";
        }

        private TMP_Text CreateStretchText(Transform parent, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-6f, 0f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = menuFont;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private TMP_Text CreateText(
            Transform parent,
            string objectName,
            float size,
            Vector2 offsetMin,
            Vector2 offsetMax,
            TextAlignmentOptions alignment,
            Color color,
            TMP_FontAsset font = null)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font != null ? font : menuFont;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }
    }
}