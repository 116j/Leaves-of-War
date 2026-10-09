using System;
using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Presents authored narrative content through an already-built view. UI
    /// construction and reusable HUD subscriptions live outside this class.
    /// </summary>
    internal sealed class NarrativeContentPresenter
    {
        private readonly NarrativeUiView view;
        private readonly Action<AudioClip, float> playVoice;
        private readonly Action stopVoice;
        private readonly Action<float> setVision;
        // Segment playback needs seek/pause on the underlying voice source without
        // exposing it directly.
        private readonly Action pauseVoice;
        private readonly Action resumeVoice;
        private readonly Func<float> getVoiceTime;
        private readonly Action<float> seekVoice;
        private readonly Func<bool> isVoicePlaying;
        private PresentationSettings settings;
        private ISequenceClock clock;
        private ISequenceInput input;
        private bool cursorOverrideActive;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        // Font scaling: base (design) sizes captured once, then re-derived on every
        // display so repeated applies never compound. Speaker/Subtitle follow
        // SUBTITLE SIZE; every other reading text follows the general FONT SIZE.
        private readonly Dictionary<TMPro.TMP_Text, float> baseFontSizes =
            new Dictionary<TMPro.TMP_Text, float>();
        private readonly HashSet<TMPro.TMP_Text> subtitleFamily = new HashSet<TMPro.TMP_Text>();
        private bool fontSizesCaptured;

        public bool IsDocumentOpen =>
            view.DocumentPanel != null && view.DocumentPanel.activeSelf;

        public NarrativeContentPresenter(
            NarrativeUiView view,
            PresentationSettings settings,
            ISequenceClock clock,
            ISequenceInput input,
            Action<AudioClip, float> playVoice,
            Action stopVoice,
            Action<float> setVision,
            Action pauseVoice = null,
            Action resumeVoice = null,
            Func<float> getVoiceTime = null,
            Action<float> seekVoice = null,
            Func<bool> isVoicePlaying = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.playVoice = playVoice ?? throw new ArgumentNullException(nameof(playVoice));
            this.stopVoice = stopVoice ?? throw new ArgumentNullException(nameof(stopVoice));
            this.setVision = setVision ?? throw new ArgumentNullException(nameof(setVision));
            this.pauseVoice = pauseVoice;
            this.resumeVoice = resumeVoice;
            this.getVoiceTime = getVoiceTime;
            this.seekVoice = seekVoice;
            this.isVoicePlaying = isVoicePlaying;
            Configure(settings, clock, input);
            GameSettings.Applied += ApplyTextScale;
        }

        /// <summary>Unsubscribes from settings updates. Call when the owning presenter is destroyed.</summary>
        public void Dispose()
        {
            GameSettings.Applied -= ApplyTextScale;
        }

        public void Configure(
            PresentationSettings presentationSettings,
            ISequenceClock sequenceClock,
            ISequenceInput sequenceInput)
        {
            settings = presentationSettings;
            clock = sequenceClock ?? UnitySequenceClock.Instance;
            input = sequenceInput ?? UnitySequenceInput.Instance;
        }

        public IEnumerator PresentLine(
            ResolvedLine line,
            string speakerName,
            float visionIntensity)
        {
            ApplyTextScale();
            view.SubtitlePanel.SetActive(GameSettings.Current.subtitlesEnabled);
            view.SpeakerText.text = speakerName ?? string.Empty;
            view.SubtitleText.text = string.Empty;
            setVision(visionIntensity);

            if (line.Clip != null)
            {
                playVoice(line.Clip, line.Volume);
            }
            else
            {
                stopVoice();
                Debug.LogWarning($"Missing voice clip for narrative line '{line.LineId}'.");
            }

            // Authored-segment path: text split on '|', each segment's audio plays
            // from its own start to its own end-time, then playback pauses (audio
            // frozen, text held) until the player advances. Used only when the
            // line carries valid segment timings and the seek/pause delegates are
            // available; otherwise the classic chunked path runs unchanged.
            bool canSeek = pauseVoice != null && resumeVoice != null &&
                getVoiceTime != null && seekVoice != null && isVoicePlaying != null;
            if (line.HasSegmentTimings && line.Clip != null && canSeek)
                yield return PresentSegmentedLine(line);
            else
                yield return PresentChunkedLine(line);

            stopVoice();
            view.SubtitlePanel.SetActive(false);
            setVision(0f);
        }

        private IEnumerator PresentChunkedLine(ResolvedLine line)
        {
            IReadOnlyList<LinePresentationChunk> chunks =
                LineChunker.Split(line.Text, Settings.ChunkCharacterLimit);
            for (int i = 0; i < chunks.Count; i++)
            {
                LinePresentationChunk chunk = chunks[i];
                yield return RevealChunk(chunk.Text);

                // Visual chunks are pages of one authored recording. Only the
                // final dismissal cuts that recording.
                if (i + 1 == chunks.Count)
                    stopVoice();

                if (chunk.PauseAfter && Settings.BeatPauseSeconds > 0f)
                {
                    float pauseUntil = clock.UnscaledTime + Settings.BeatPauseSeconds;
                    while (clock.UnscaledTime < pauseUntil)
                        yield return null;
                }
            }
        }

        // Authored-segment behaviour: one recording, several text pages, each with
        // its own audio start/end. On advance, the next segment's audio is sought
        // to its own start (skipping any breath between takes) and resumes.
        private IEnumerator PresentSegmentedLine(ResolvedLine line)
        {
            string[] segments = line.Text.Split('|');
            SegmentTime[] times = line.Segments;

            if (times.Length != segments.Length)
            {
                Debug.LogWarning(
                    $"Line '{line.LineId}' has {segments.Length} text segments but " +
                    $"{times.Length} segment times (expected one Start/End entry per " +
                    $"segment). Falling back to continuous playback.");
                yield return PresentChunkedLine(line);
                yield break;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                string segmentText = segments[i].Trim();
                float segmentStart = times[i].start;
                float segmentEnd = times[i].end;

                seekVoice(segmentStart);
                if (!isVoicePlaying())
                    resumeVoice();

                // AudioSource.time can remain frozen on some platforms while
                // the source still reports that it is playing.  The authored
                // duration is also our clock-based escape hatch so a stuck
                // timeline cannot keep narrative input locked forever.
                float segmentDeadline = clock.UnscaledTime +
                    Mathf.Max(0f, segmentEnd - segmentStart);

                // Crawl the text in while the segment's audio plays. This reveal
                // does not wait for a click at the end - the loop below owns the
                // single advance press, so we avoid requiring two clicks.
                yield return RevealTextOnly(segmentText);

                while (isVoicePlaying() &&
                       getVoiceTime() < segmentEnd &&
                       clock.UnscaledTime < segmentDeadline)
                    yield return null;

                if (isVoicePlaying())
                    pauseVoice();

                while (!Pressed(SequenceInputAction.Advance))
                    yield return null;
            }
        }

        public IEnumerator PresentCard(string text)
        {
            ApplyTextScale();
            view.AuthorialText.text = text ?? string.Empty;
            view.AuthorialPanel.SetActive(true);
            BeginModalCursor();

            float availableAt = clock.UnscaledTime + Settings.MinimumHoldSeconds;
            while (clock.UnscaledTime < availableAt || !Pressed(SequenceInputAction.Advance))
                yield return null;

            EndModalCursor();
            view.AuthorialPanel.SetActive(false);
        }

        public IEnumerator PresentChoice(
            IReadOnlyList<ChoiceOption> options,
            Action<EndingDefinition> selected)
        {
            ApplyTextScale();
            if (options == null || options.Count == 0)
            {
                Debug.LogError(
                    "NarrativePresenter received an empty choice. GameSession should handle this data error.");
                yield break;
            }

            int selectedIndex = -1;
            view.ChoicePanel.SetActive(true);
            BeginModalCursor();

            for (int i = 0; i < view.ChoiceButtons.Count; i++)
            {
                Button button = view.ChoiceButtons[i];
                bool visible = i < options.Count;
                button.gameObject.SetActive(visible);
                button.onClick.RemoveAllListeners();
                if (!visible)
                    continue;

                int capturedIndex = i;
                view.ChoiceLabels[i].text = $"{i + 1}. {options[i].Label}";
                button.onClick.AddListener(() => selectedIndex = capturedIndex);
            }

            while (selectedIndex < 0)
            {
                if (Pressed(SequenceInputAction.Choice1) && options.Count > 0)
                    selectedIndex = 0;
                else if (Pressed(SequenceInputAction.Choice2) && options.Count > 1)
                    selectedIndex = 1;
                else if (Pressed(SequenceInputAction.Choice3) && options.Count > 2)
                    selectedIndex = 2;

                yield return null;
            }

            EndingDefinition ending = selectedIndex < options.Count
                ? options[selectedIndex].Ending
                : null;
            selected?.Invoke(ending);
            EndModalCursor();
            view.ChoicePanel.SetActive(false);
        }

        public IEnumerator PresentDocument(IReadableDocument document, Action closed)
        {
            if (document == null)
                yield break;

            ApplyTextScale();

            int page = 0;
            GameSession session = GameSession.Instance;
            NarrativeState state = session != null ? session.State : null;
            NarrativeCatalog catalog = session != null ? session.Catalog : null;
            IReadOnlyList<string> pages = document.PagesFor(state, catalog);
            int pageCount = Mathf.Max(1, pages != null ? pages.Count : 0);
            NameVariant chosenName = state != null ? state.ChosenName : NameVariant.Laura;

            view.DocumentPanel.SetActive(true);
            BeginModalCursor();
            stopVoice();

            ILineContent narration = document.Narration;
            string[] narrationSegments = null;
            SegmentTime[] narrationTimes = null;
            int narrationSegmentIndex = 0;
            bool useNarrationSegments = false;

            if (narration != null && !string.IsNullOrWhiteSpace(narration.LineId))
            {
                AudioClip narrationClip = narration.ClipFor(chosenName);
                if (narrationClip != null)
                {
                    playVoice(narrationClip, narration.VolumeFor(chosenName));

                    // Independent of page navigation: if the narration is authored
                    // with '|' segments and matching Start/End times, skip the gap
                    // (breath) between segments automatically as playback reaches
                    // each boundary. No click required - this only trims dead air,
                    // it never pauses for input.
                    bool canSeek = getVoiceTime != null && seekVoice != null && isVoicePlaying != null;
                    if (canSeek)
                    {
                        string narrationText = narration.TextFor(chosenName) ?? string.Empty;
                        narrationSegments = narrationText.Split('|');
                        narrationTimes = narration.SegmentsFor(chosenName);
                        useNarrationSegments = narrationSegments.Length > 1 &&
                            narrationTimes.Length == narrationSegments.Length;
                        Debug.Log(
                            $"[DocSegments] '{narration.LineId}': text-segments={narrationSegments.Length}, " +
                            $"Segments-count={narrationTimes.Length}, useNarrationSegments={useNarrationSegments}");
                        if (useNarrationSegments)
                            seekVoice(narrationTimes[0].start);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"Missing voice clip for document narration '{narration.LineId}'.");
                }
            }

            bool openingFrame = true;
            string closeKeyLabel = GetCloseDocumentKeyLabel();
            while (true)
            {
                view.DocumentTitleText.text = document.Title;
                string pageText = pages != null && pages.Count > 0
                    ? pages[page]
                    : string.Empty;
                view.DocumentBodyText.text = NameVariants.Substitute(pageText, chosenName);
                view.DocumentPageText.text =
                    $"{page + 1}/{pageCount}   \u2190/\u2192 PAGE   {closeKeyLabel} CLOSE";

                // The click that opened a document must not also turn a page or
                // close the reader on its first frame.
                if (openingFrame)
                {
                    openingFrame = false;
                    yield return null;
                    continue;
                }

                if (Pressed(SequenceInputAction.Cancel))
                    break;
                if (Pressed(SequenceInputAction.PreviousPage))
                    page = Mathf.Max(0, page - 1);
                if (Pressed(SequenceInputAction.NextPage))
                {
                    if (page + 1 < pageCount)
                        page++;
                }

                // Advance narration to its next segment start once the current
                // segment's audio reaches its end, skipping any authored gap.
                // Independent of page turning - never waits for input.
                if (useNarrationSegments &&
                    narrationSegmentIndex + 1 < narrationSegments.Length &&
                    isVoicePlaying() &&
                    getVoiceTime() >= narrationTimes[narrationSegmentIndex].end)
                {
                    Debug.Log(
                        $"[DocSegments] skipping gap: segment {narrationSegmentIndex} " +
                        $"end={narrationTimes[narrationSegmentIndex].end} -> " +
                        $"segment {narrationSegmentIndex + 1} start={narrationTimes[narrationSegmentIndex + 1].start} " +
                        $"(voiceTime={getVoiceTime()})");
                    narrationSegmentIndex++;
                    seekVoice(narrationTimes[narrationSegmentIndex].start);
                }

                yield return null;
            }

            stopVoice();
            EndModalCursor();
            view.DocumentPanel.SetActive(false);
            closed?.Invoke();
        }

        public void Clear()
        {
            stopVoice();
            EndModalCursor();
            view.HideNarrativePanels();
            setVision(0f);
        }

        // Progressive reveal that returns as soon as the text is fully shown,
        // without waiting for a click. Used by segmented lines, where the audio
        // boundary and the single advance press are handled by the caller.
        private IEnumerator RevealTextOnly(string text)
        {
            view.SubtitleText.text = text ?? string.Empty;
            view.SubtitleText.maxVisibleCharacters = 0;
            view.SubtitleText.ForceMeshUpdate();
            int characterCount = view.SubtitleText.textInfo.characterCount;
            float startedAt = clock.UnscaledTime;

            while (view.SubtitleText.maxVisibleCharacters < characterCount)
            {
                float elapsed = clock.UnscaledTime - startedAt;
                view.SubtitleText.maxVisibleCharacters = Mathf.Min(
                    characterCount,
                    Mathf.FloorToInt(elapsed * Settings.RevealCharactersPerSecond));

                if (elapsed >= Settings.MinimumHoldSeconds &&
                    Pressed(SequenceInputAction.Advance))
                {
                    view.SubtitleText.maxVisibleCharacters = characterCount;
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator RevealChunk(string text)
        {
            view.SubtitleText.text = text ?? string.Empty;
            view.SubtitleText.maxVisibleCharacters = 0;
            view.SubtitleText.ForceMeshUpdate();
            int characterCount = view.SubtitleText.textInfo.characterCount;
            float startedAt = clock.UnscaledTime;

            while (view.SubtitleText.maxVisibleCharacters < characterCount)
            {
                float elapsed = clock.UnscaledTime - startedAt;
                view.SubtitleText.maxVisibleCharacters = Mathf.Min(
                    characterCount,
                    Mathf.FloorToInt(elapsed * Settings.RevealCharactersPerSecond));

                if (elapsed >= Settings.MinimumHoldSeconds &&
                    Pressed(SequenceInputAction.Advance))
                {
                    yield break;
                }

                yield return null;
            }

            while (clock.UnscaledTime - startedAt < Settings.MinimumHoldSeconds ||
                   !Pressed(SequenceInputAction.Advance))
            {
                yield return null;
            }
        }

        private void ApplyTextScale()
        {
            if (!fontSizesCaptured)
                CaptureBaseFontSizes();

            SettingsData s = GameSettings.Current;
            foreach (KeyValuePair<TMPro.TMP_Text, float> entry in baseFontSizes)
            {
                TMPro.TMP_Text text = entry.Key;
                if (text == null)
                    continue;

                float scale = subtitleFamily.Contains(text) ? s.subtitleScale : s.fontScale;
                text.fontSize = entry.Value * scale;
            }
        }

        private void CaptureBaseFontSizes()
        {
            fontSizesCaptured = true;
            Register(view.SpeakerText, isSubtitle: true);
            Register(view.SubtitleText, isSubtitle: true);
            Register(view.InteractionPromptText, isSubtitle: false);
            Register(view.CarryStatusText, isSubtitle: false);
            Register(view.StatusText, isSubtitle: false);
            Register(view.GameplayHudText, isSubtitle: false);
            Register(view.DocumentTitleText, isSubtitle: false);
            Register(view.DocumentBodyText, isSubtitle: false);
            Register(view.DocumentPageText, isSubtitle: false);
            Register(view.AuthorialText, isSubtitle: false);
            foreach (TMPro.TMP_Text label in view.ChoiceLabels)
                Register(label, isSubtitle: false);
        }

        private void Register(TMPro.TMP_Text text, bool isSubtitle)
        {
            if (text == null || baseFontSizes.ContainsKey(text))
                return;

            baseFontSizes[text] = text.fontSize;
            if (isSubtitle)
                subtitleFamily.Add(text);
        }

        private PresentationSettings Settings
        {
            get
            {
                if (settings != null)
                    return settings;

                settings = ScriptableObject.CreateInstance<PresentationSettings>();
                settings.hideFlags = HideFlags.HideAndDontSave;
                return settings;
            }
        }

        private bool Pressed(SequenceInputAction action)
        {
            if (clock is IPausableSequenceClock pausableClock && pausableClock.IsPaused)
                return false;

            return input.WasPressed(action);
        }

        /// <summary>
        /// Human-readable label for the live player's current Close Document
        /// binding (e.g. "BACKSPACE", "E", "RMB"), so the document hint always
        /// matches whatever the player has actually bound, not a hardcoded key.
        /// </summary>
        private static string GetCloseDocumentKeyLabel()
        {
            GameSession session = GameSession.Instance;
            if (session == null ||
                !session.SceneServices.TryGetPlayer(out global::FirstPersonController player, out _))
            {
                return "BACKSPACE";
            }

            InputAction closeAction = player.CloseDocumentAction;
            if (closeAction == null || closeAction.bindings.Count == 0)
                return "BACKSPACE";

            string path = closeAction.bindings[0].effectivePath;
            if (string.IsNullOrEmpty(path))
                return "BACKSPACE";

            string readable = InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            if (string.IsNullOrEmpty(readable))
                return "BACKSPACE";

            // Shorten the common mouse buttons; everything else (keyboard
            // keys) reads fine as-is.
            switch (readable)
            {
                case "Left Button": return "LMB";
                case "Right Button": return "RMB";
                case "Middle Button": return "MMB";
                default: return readable.ToUpperInvariant();
            }
        }

        private void BeginModalCursor()
        {
            if (cursorOverrideActive)
                return;

            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorOverrideActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void EndModalCursor()
        {
            if (!cursorOverrideActive)
                return;

            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            cursorOverrideActive = false;
        }
    }
}