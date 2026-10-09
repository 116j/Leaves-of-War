using System;
using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    internal sealed class ChapterRunner
    {
        private readonly GameSession session;
        private readonly List<ISequencePlayer> sequenceMatches = new List<ISequencePlayer>();
        private ISequencePlayer activeSequencePlayer;
        private SequencePlaybackHandle activeSequencePlayback;

        /// <summary>
        /// Fired right after any regular chapter beat completes (not choice/
        /// ending beats). Lets other systems react to a specific point in the
        /// narrative - e.g. unlocking the inventory prompt once a particular
        /// LineBeat's LineId has played - without ChapterRunner needing to
        /// know about them. Subscribers should filter by whatever identifies
        /// the beat they care about (LineId, etc.).
        /// </summary>
        public static event Action<NarrativeBeat> BeatCompleted;

        /// <summary>
        /// Fired when a LineBeat's text contains the [unlock inventory] tag,
        /// after that tag is stripped from the displayed subtitle. Simplest
        /// way to trigger the inventory prompt: just add the tag to the end
        /// of a line's authored text in the Inspector - no LineId lookup or
        /// code editing needed per-use.
        /// </summary>
        public static event Action UnlockInventoryRequested;

        /// <summary>
        /// Fired when a LineBeat's text contains a [give item:SOME_ID] tag,
        /// after that tag is stripped from the displayed subtitle and the
        /// line has finished playing. SOME_ID should match an
        /// InventoryItemDefinition's ItemId field.
        /// </summary>
        public static event Action<string> GiveItemRequested;

        /// <summary>
        /// Fired right before a regular (non-document) LineBeat starts
        /// presenting - subtitle panel, voice clip, the whole thing,
        /// including any internal reveal/advance waits. Carries the line's
        /// SpeakerDefinition (may be null for unattributed lines). Meant for
        /// a character's talk animation: subscribe and compare against your
        /// own SpeakerDefinition to know when to switch to Talking.
        /// </summary>
        public static event Action<SpeakerDefinition> LineStarted;

        /// <summary>
        /// Fired right after the same LineBeat's presentation finishes
        /// (subtitle hidden). Pairs with LineStarted - switch back to Idle
        /// here. Not fired for DocumentSequenceBeat (reading a document
        /// isn't a character speaking).
        /// </summary>
        public static event Action<SpeakerDefinition> LineFinished;

        /// <summary>
        /// Same idea as LineStarted/LineFinished but for PatientSessionBeat
        /// lines, which don't go through LineBeat/SpeakerDefinition at all.
        /// Carries the PatientDefinition currently speaking. Fired around
        /// each patient's single line inside RunPatientSession.
        /// </summary>
        public static event Action<PatientDefinition> PatientLineStarted;

        /// <summary>Pairs with PatientLineStarted - switch back to Idle here.</summary>
        public static event Action<PatientDefinition> PatientLineFinished;

        public ChapterRunner(GameSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool HasActiveSequencePlayback =>
            activeSequencePlayback != null && !activeSequencePlayback.IsTerminal;

        public void RegisterSequencePlayer(ISequencePlayer sequencePlayer)
        {
            session.SceneServices.RegisterSequencePlayer(sequencePlayer);
        }

        public void UnregisterSequencePlayer(ISequencePlayer sequencePlayer)
        {
            session.SceneServices.UnregisterSequencePlayer(sequencePlayer);
            if (ReferenceEquals(activeSequencePlayer, sequencePlayer))
                activeSequencePlayback?.Cancel();
        }

        public void CancelActiveSequence()
        {
            activeSequencePlayer = null;
            SequencePlaybackHandle playback = activeSequencePlayback;
            activeSequencePlayback = null;
            playback?.Cancel();

            session.SetActiveSequence(null);
        }

        public IEnumerator RunChapter(ChapterDefinition chapter, NarrativeOperationResult result)
        {
            result.Reset();
            NarrativeState state = session.State;
            if (chapter == null || state == null)
            {
                Fail(result, "Narrative chapter runner cannot run without a chapter and active state.");
                yield break;
            }

            IReadOnlyList<NarrativeBeat> beats = chapter.Beats;
            state.BeatIndex = Mathf.Clamp(state.BeatIndex, 0, beats.Count);

            while (state.BeatIndex < beats.Count)
            {
                NarrativeBeat beat = beats[state.BeatIndex];
                if (beat == null)
                {
                    state.BeatIndex++;
                    continue;
                }

                if (beat is ChoiceBeat choiceBeat)
                {
                    session.SetActiveNarrativeBeat(choiceBeat);
                    EndingDefinition selectedEnding = null;
                    yield return session.PresentChoice(
                        choiceBeat.Options,
                        ending => selectedEnding = ending);
                    if (selectedEnding == null)
                    {
                        Fail(
                            result,
                            "The finale choice completed without a catalogued ending selection.");
                        yield break;
                    }

                    if (!session.IsCataloguedEnding(selectedEnding))
                    {
                        Fail(
                            result,
                            $"The finale choice selected uncatalogued ending '{selectedEnding.Id}'.");
                        yield break;
                    }

                    session.BeginEndingTransaction();
                    session.SetCurrentEnding(selectedEnding.Id);
                    session.CompleteBeat(choiceBeat, true);

                    var endingResult = new NarrativeOperationResult();
                    yield return RunEnding(selectedEnding, endingResult);
                    if (!endingResult.Succeeded)
                    {
                        result.Fail(
                            endingResult.Error,
                            endingResult.FailureKind);
                        yield break;
                    }

                    result.Succeed();
                    yield break;
                }

                var dispatchResult = new NarrativeOperationResult();
                session.SetActiveNarrativeBeat(beat);
                yield return DispatchBeat(beat, dispatchResult);
                if (!dispatchResult.Succeeded)
                {
                    result.Fail(
                        dispatchResult.Error,
                        dispatchResult.FailureKind);
                    yield break;
                }

                session.CompleteBeat(beat, true);
                BeatCompleted?.Invoke(beat);
            }

            if (!string.IsNullOrWhiteSpace(chapter.ClosingCard))
                yield return session.PresentCard(chapter.ClosingCard);

            // A new chapter usually represents a narrative time skip (often
            // years, per the closing card just shown) - whatever tool/item
            // the player was still holding from the chapter that just ended
            // wouldn't make sense to carry forward (e.g. garden shears
            // surviving a "three years later" jump). Drop it here, once per
            // chapter boundary, regardless of whether the player ever
            // placed it back themselves.
            if (session.TryGetCarriedItemHolder(out CarriedItemHolder carriedItemHolder))
                carriedItemHolder.DropCarriedItem();

            int nextChapterIndex = chapter.Index + 1;
            ChapterDefinition nextChapter = session.Catalog != null
                ? session.Catalog.ChapterAt(nextChapterIndex)
                : null;
            if (nextChapter == null)
            {
                result.Succeed();
                yield break;
            }

            int previousChapterIndex = state.ChapterIndex;
            int previousBeatIndex = state.BeatIndex;
            state.ChapterIndex = nextChapter.Index;
            state.BeatIndex = 0;

            var loadResult = new NarrativeOperationResult();
            yield return session.LoadChapterLocation(nextChapter, loadResult);
            if (!loadResult.Succeeded)
            {
                state.ChapterIndex = previousChapterIndex;
                state.BeatIndex = previousBeatIndex;
                result.Fail(loadResult.Error, loadResult.FailureKind);
                yield break;
            }

            session.PersistState();

            var nextChapterResult = new NarrativeOperationResult();
            yield return RunChapter(nextChapter, nextChapterResult);
            if (nextChapterResult.Succeeded)
                result.Succeed();
            else
                result.Fail(
                    nextChapterResult.Error,
                    nextChapterResult.FailureKind);
        }

        public IEnumerator RunEnding(EndingDefinition ending, NarrativeOperationResult result)
        {
            result.Reset();
            if (ending == null || session.State == null)
            {
                Fail(result, "Narrative ending runner cannot run without an ending and active state.");
                yield break;
            }

            if (!session.IsCataloguedEnding(ending))
            {
                session.SetCurrentEnding(null);
                session.FinishEndingSave(completed: false);
                Fail(
                    result,
                    $"Narrative ending '{ending.Id}' is not the catalogued ending asset for that id.");
                yield break;
            }

            session.BeginEndingTransaction();
            session.SetCompletedEnding(null);
            session.SetCurrentEnding(ending.Id);
            bool endingCompleted = false;
            NarrativeState state = session.State;
            try
            {
                session.SetActiveNarrativeBeat(null);
                yield return session.PresentCard(ending.Title);

                for (int i = 0; i < ending.Beats.Count; i++)
                {
                    NarrativeBeat beat = ending.Beats[i];
                    if (beat == null)
                        continue;

                    var dispatchResult = new NarrativeOperationResult();
                    session.SetActiveNarrativeBeat(beat);
                    yield return DispatchBeat(beat, dispatchResult);
                    if (!dispatchResult.Succeeded)
                    {
                        result.Fail(
                            dispatchResult.Error,
                            dispatchResult.FailureKind);
                        yield break;
                    }

                    session.CompleteBeat(beat, false);
                }

                session.SetCompletedEnding(ending.Id);
                session.SetActiveNarrativeBeat(null);
                yield return PlayEndingVideoAndCredits(ending, state.ChosenName);
                result.Succeed();
                endingCompleted = true;
            }
            finally
            {
                session.SetActiveNarrativeBeat(null);
                session.SetCurrentEnding(null);
                session.FinishEndingSave(endingCompleted);
            }
        }

        private const string CreditsResourcePath = "Narrative/CreditsSequence";

        /// <summary>
        /// Plays this ending's video (if it has one) from StreamingAssets,
        /// then the shared credits sequence prefab (loaded from
        /// Resources/Narrative/CreditsSequence) - waiting for each to
        /// genuinely finish before moving on to the next.
        /// </summary>
        private IEnumerator PlayEndingVideoAndCredits(EndingDefinition ending, NameVariant variant)
        {
            string videoFileName = ending.EndingVideoFileNameFor(variant);
            if (!string.IsNullOrWhiteSpace(videoFileName))
            {
                // Silences the level's own ambient/background audio only
                // for the video's own duration - VideoScreen's audio source
                // opts out via ignoreListenerPause. Scoped to just the video
                // (not credits too): AudioManager treats
                // gameplayAudioPauseActive and exclusiveMusicActive both
                // being true as "gameplay pause wins", which would silence
                // the credits' own exclusive music if this stayed on that
                // long - credits already manage their own suppression via
                // PlayExclusiveMusic.
                AudioManager.Instance?.SetGameplayAudioPaused(true);
                try
                {
                    yield return PlayEndingVideo(videoFileName, ending.EndingVideoVolume);
                }
                finally
                {
                    AudioManager.Instance?.SetGameplayAudioPaused(false);
                }
            }

            CreditsSequenceController creditsPrefab =
                Resources.Load<CreditsSequenceController>(CreditsResourcePath);
            if (creditsPrefab == null)
            {
                Debug.LogWarning(
                    $"No credits prefab found at Resources/{CreditsResourcePath} - skipping credits.");
                yield break;
            }

            CreditsSequenceController credits = UnityEngine.Object.Instantiate(creditsPrefab);
            bool creditsDone = false;
            void OnCreditsFinished() => creditsDone = true;
            credits.CreditsFinished += OnCreditsFinished;

            credits.Play();
            while (!creditsDone)
                yield return null;

            credits.CreditsFinished -= OnCreditsFinished;
            UnityEngine.Object.Destroy(credits.gameObject);

            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private IEnumerator PlayEndingVideo(string videoFileName, float volume)
        {
            var videoHost = new GameObject("Ending Video");
            VideoScreen videoScreen = videoHost.AddComponent<VideoScreen>();

            bool videoDone = false;
            void OnVideoFinished() => videoDone = true;
            void OnVideoFailed(string message) => videoDone = true;
            videoScreen.Finished += OnVideoFinished;
            videoScreen.Failed += OnVideoFailed;

            videoScreen.Play(videoFileName, volume);
            while (!videoDone)
                yield return null;

            videoScreen.Finished -= OnVideoFinished;
            videoScreen.Failed -= OnVideoFailed;
            UnityEngine.Object.Destroy(videoHost);
        }

        private IEnumerator DispatchBeat(NarrativeBeat beat, NarrativeOperationResult result)
        {
            result.Succeed();
            NarrativeState state = session.State;
            beat.ApplyStartEffects(state);

            switch (beat)
            {
                case DocumentSequenceBeat documentSequenceBeat:
                    yield return session.PresentDocumentSequence(documentSequenceBeat);
                    break;

                case LineBeat lineBeat:
                    ResolvedLine line = lineBeat.Resolve(state.ChosenName);
                    // The protagonist's SpeakerDefinition holds only the surname
                    // ("Hortensia"); NameVariants composes the chosen first name
                    // with it. Every other speaker's name is returned unchanged.
                    string speakerName = NameVariants.SpeakerDisplayName(
                        lineBeat.Speaker,
                        state.ChosenName);

                    // Authors can drop inline tags anywhere in a line's text
                    // (e.g. its very end) to trigger events right after that
                    // line has been read - no LineId lookup or code editing
                    // needed per use. Tags are stripped before the subtitle is
                    // shown; the events they queue only fire AFTER the line
                    // finishes playing, not before.
                    line = ExtractInlineTags(line, out bool unlockInventory, out string giveItemId);

                    LineStarted?.Invoke(lineBeat.Speaker);
                    yield return session.PresentLine(line, speakerName, 0f);
                    LineFinished?.Invoke(lineBeat.Speaker);

                    session.ApplyInlineInventoryGrants(unlockInventory, giveItemId);
                    if (unlockInventory)
                        UnlockInventoryRequested?.Invoke();
                    if (!string.IsNullOrEmpty(giveItemId))
                    {
                        GiveItemRequested?.Invoke(giveItemId);
                    }
                    break;

                case GateBeat gateBeat:
                    while (!session.SkipGates && !gateBeat.IsOpen(state))
                        yield return null;
                    break;

                case TitleCardBeat titleCardBeat:
                    yield return session.PresentCard(titleCardBeat.Text);
                    break;

                case PatientSessionBeat patientSessionBeat:
                    yield return RunPatientSession(patientSessionBeat, state);
                    break;

                case SequenceBeat sequenceBeat:
                    yield return PlaySequence(sequenceBeat, result);
                    break;

                case TravelBeat travelBeat:
                    result.Reset();
                    yield return session.LoadScene(
                        travelBeat.TargetScene,
                        travelBeat.SpawnPointId,
                        result);
                    break;

                default:
                    Debug.LogWarning($"Unsupported narrative beat type: {beat.GetType().Name}");
                    break;
            }
        }

        private static readonly System.Text.RegularExpressions.Regex UnlockInventoryPattern =
            new System.Text.RegularExpressions.Regex(
                @"\[unlock inventory\]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static readonly System.Text.RegularExpressions.Regex GiveItemPattern =
            new System.Text.RegularExpressions.Regex(
                @"\[give item:\s*([A-Za-z0-9_\-]+)\s*\]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Finds and strips [unlock inventory] / [give item:ID] tags from a
        /// resolved line's text, returning a clean copy plus what was found.
        /// The line's own LineId/Clip/Segments are preserved unchanged.
        /// </summary>
        internal static ResolvedLine ExtractInlineTags(
            ResolvedLine line,
            out bool unlockInventory,
            out string giveItemId)
        {
            unlockInventory = false;
            giveItemId = null;

            if (string.IsNullOrEmpty(line.Text))
                return line;

            string text = line.Text;

            if (UnlockInventoryPattern.IsMatch(text))
            {
                unlockInventory = true;
                text = UnlockInventoryPattern.Replace(text, string.Empty);
            }

            System.Text.RegularExpressions.Match giveItemMatch = GiveItemPattern.Match(text);
            if (giveItemMatch.Success)
            {
                giveItemId = giveItemMatch.Groups[1].Value;
                text = GiveItemPattern.Replace(text, string.Empty);
            }

            if (!unlockInventory && giveItemId == null)
                return line;

            text = text.TrimEnd();
            return new ResolvedLine(line.LineId, text, line.Clip, line.Segments);
        }

        private IEnumerator RunPatientSession(
            PatientSessionBeat patientSessionBeat,
            NarrativeState state)
        {
            session.SceneServices.TryGetPatientVisualPresenter(
                out IPatientVisualPresenter patientVisuals,
                out _);
            session.SceneServices.TryGetGifOverlay(out GifOverlayController gifOverlay, out _);
            gifOverlay?.StartForChapter(state.ChapterIndex);
            session.PushPlayerLock();
            try
            {
                IReadOnlyList<PatientDefinition> roster = patientSessionBeat.Roster;
                if (roster == null)
                    yield break;

                for (int i = 0; i < roster.Count; i++)
                {
                    PatientDefinition patient = roster[i];
                    if (patient == null)
                        continue;

                    patientVisuals?.ShowPatient(patient, patientSessionBeat.VisionIntensityAt(i));
                    // ShowPatient may be running an async transition (e.g. a
                    // vision-bleed distortion covering the model swap) -
                    // wait for it to finish so the new patient's model and
                    // animator are actually in place before the Talking
                    // animation is triggered below.
                    while (patientVisuals != null && patientVisuals.IsTransitioning)
                        yield return null;

                    LineContent content = patient.Content;
                    var patientLine = new ResolvedLine(
                        content.LineId,
                        content.TextFor(state.ChosenName),
                        content.ClipFor(state.ChosenName),
                        content.SegmentsFor(state.ChosenName),
                        content.VolumeFor(state.ChosenName));
                    PatientLineStarted?.Invoke(patient);
                    yield return session.PresentLine(
                        patientLine,
                        patient.DisplayName,
                        patientSessionBeat.VisionIntensityAt(i));
                    PatientLineFinished?.Invoke(patient);
                }
            }
            finally
            {
                patientVisuals?.ClearPatient();
                gifOverlay?.StopLoop();
                session.PopPlayerLock();
            }
        }

        private IEnumerator PlaySequence(
            SequenceBeat sequenceBeat,
            NarrativeOperationResult result)
        {
            ISequencePlayer player = ResolveSequencePlayer(sequenceBeat.SequenceId);
            if (player == null)
                yield break;

            SequenceLockFlags locks = player.RequiredLocks;
            if (sequenceBeat.LocksPlayer)
                locks |= SequenceLockFlags.PlayerMovement;

            var playback = new SequencePlaybackHandle(
                session,
                sequenceBeat.SequenceId,
                session.CurrentEndingId,
                locks,
                session.SequenceClock,
                session.SequenceInput);
            activeSequencePlayer = player;
            activeSequencePlayback = playback;
            session.SetActiveSequence(sequenceBeat.SequenceId);
            playback.Begin();
            Stack<IEnumerator> routines = null;

            try
            {
                IEnumerator routine = null;
                try
                {
                    routine = player.Play(playback);
                }
                catch (Exception exception)
                {
                    playback.Fail(
                        $"Sequence player '{sequenceBeat.SequenceId}' threw {exception.GetType().Name}: " +
                        exception.Message);
                    Debug.LogException(exception);
                }

                if (routine == null)
                {
                    if (playback.Status == SequencePlaybackStatus.Running)
                    {
                        playback.Fail(
                            $"Sequence player '{sequenceBeat.SequenceId}' returned no playback routine.");
                    }
                }
                else
                {
                    routines = new Stack<IEnumerator>();
                    routines.Push(routine);
                    while (playback.Status == SequencePlaybackStatus.Running &&
                           routines.Count > 0)
                    {
                        IEnumerator activeRoutine = routines.Peek();
                        bool movedNext;
                        object current = null;
                        try
                        {
                            movedNext = activeRoutine.MoveNext();
                            if (movedNext)
                                current = activeRoutine.Current;
                        }
                        catch (Exception exception)
                        {
                            playback.Fail(
                                $"Sequence player '{sequenceBeat.SequenceId}' threw {exception.GetType().Name}: " +
                                exception.Message);
                            Debug.LogException(exception);
                            break;
                        }

                        if (!movedNext)
                        {
                            routines.Pop();
                            DisposeSequenceRoutine(
                                activeRoutine,
                                playback,
                                sequenceBeat.SequenceId);
                            continue;
                        }

                        if (current is IEnumerator nestedRoutine)
                        {
                            routines.Push(nestedRoutine);
                            continue;
                        }

                        yield return current;
                    }

                    if (playback.Status == SequencePlaybackStatus.Running &&
                        routines.Count == 0)
                    {
                        playback.Complete();
                    }
                }
            }
            finally
            {
                if (routines != null)
                {
                    while (routines.Count > 0)
                    {
                        DisposeSequenceRoutine(
                            routines.Pop(),
                            playback,
                            sequenceBeat.SequenceId);
                    }
                }

                if (ReferenceEquals(activeSequencePlayer, player))
                    activeSequencePlayer = null;
                if (ReferenceEquals(activeSequencePlayback, playback))
                    activeSequencePlayback = null;
                if (string.Equals(
                    session.ActiveSequenceId,
                    sequenceBeat.SequenceId,
                    StringComparison.Ordinal))
                {
                    session.SetActiveSequence(null);
                }

                playback.Dispose();
            }

            if (playback.Status == SequencePlaybackStatus.Failed)
            {
                string error = string.IsNullOrWhiteSpace(playback.Error)
                    ? $"Sequence '{sequenceBeat.SequenceId}' failed."
                    : playback.Error;
                result.Fail(error, NarrativeFailureKind.Sequence);
                Debug.LogError(error);
            }
            else if (playback.Status == SequencePlaybackStatus.Cancelled)
            {
                result.Fail(
                    $"Sequence '{sequenceBeat.SequenceId}' was cancelled.",
                    NarrativeFailureKind.Sequence);
            }
        }

        private static void DisposeSequenceRoutine(
            IEnumerator routine,
            SequencePlaybackHandle playback,
            string sequenceId)
        {
            if (!(routine is IDisposable disposable))
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                if (playback.Status == SequencePlaybackStatus.Running)
                {
                    playback.Fail(
                        $"Sequence player '{sequenceId}' failed while cleaning up: " +
                        exception.Message);
                }
                Debug.LogException(exception);
            }
        }

        private ISequencePlayer ResolveSequencePlayer(string sequenceId)
        {
            sequenceMatches.Clear();
            int matchCount = session.SceneServices.FindSequencePlayers(
                sequenceId,
                sequenceMatches);

            if (matchCount == 1)
                return sequenceMatches[0];

            string displayId = string.IsNullOrWhiteSpace(sequenceId) ? "<blank>" : sequenceId;
            if (matchCount == 0)
            {
                Debug.LogWarning(
                    $"No {nameof(ISequencePlayer)} is registered for sequence '{displayId}'. " +
                    "Advancing the narrative beat.");
            }
            else
            {
                Debug.LogWarning(
                    $"Sequence '{displayId}' has {matchCount} registered {nameof(ISequencePlayer)} instances. " +
                    "Advancing the narrative beat without choosing one.");
            }

            return null;
        }

        private static void Fail(NarrativeOperationResult result, string message)
        {
            result.Fail(message);
            Debug.LogError(message);
        }
    }

    internal enum NarrativeFailureKind
    {
        None,
        Data,
        Travel,
        Sequence
    }

    internal sealed class NarrativeOperationResult
    {
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }
        public NarrativeFailureKind FailureKind { get; private set; }

        public void Reset()
        {
            Succeeded = false;
            Error = string.Empty;
            FailureKind = NarrativeFailureKind.None;
        }

        public void Succeed()
        {
            Succeeded = true;
            Error = string.Empty;
            FailureKind = NarrativeFailureKind.None;
        }

        public void Fail(
            string error,
            NarrativeFailureKind failureKind = NarrativeFailureKind.Data)
        {
            Succeeded = false;
            Error = error;
            FailureKind = failureKind == NarrativeFailureKind.None
                ? NarrativeFailureKind.Data
                : failureKind;
        }
    }
}