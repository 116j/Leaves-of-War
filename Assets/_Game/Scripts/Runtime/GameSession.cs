using System;
using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        private const string EmptyChoiceFallbackEndingId = "confession";
        private const string SaveFailureStatus =
            "PROGRESS COULD NOT BE SAVED.\nRETRYING AT THE NEXT CHECKPOINT.";

        public static GameSession Instance { get; private set; }
        public static event Action<GameSession> InstanceChanged;

        public static event Action<DreamTransitionCue> DreamTransitionMusicFinished;

        public static event Action<DreamTransitionCue> DreamTransitionStarting;

        private NarrativeCatalog catalog;
        private PresentationSettings presentationSettings;
        private NarrativeState state;
        private Coroutine runner;
        private Coroutine documentRoutine;
        private DocumentDefinition awaitedDocument;
        private IReadableDocument requestedDocument;
        private int narrativeInputLocks;
        private int playerLocks;
        private int saveBatchDepth;
        private bool saveQueued;
        private bool saveDirty;
        private bool autosaveDiscardPending;
        private string pendingStatusMessage;
        private ISceneLoader sceneLoader;
        private SavedPlayerLocation lastValidatedPlayerLocation;
        private SavedPlayerLocation pendingLoadedPlayerLocation;
        private bool initialPlacementPending;
        private bool sceneLoadInProgress;
        private bool playerPlacementValidated;
        private SaveGameData endingRollbackSnapshot;
        private ChapterRunner chapterRunner;
        private SceneTransitionSettings sceneTransitionSettings;
        private DreamTransitionSettings dreamTransitionSettings;
        private DreamTransitionScreen activeDreamTransitionScreen;
        private bool dreamTransitionMusicActive;
        private Coroutine dreamTransitionMusicWaitRoutine;
        private float dreamTransitionMusicStartedAt;
        private ISequenceClock sequenceClock = UnitySequenceClock.Instance;
        private ISequenceInput sequenceInput = UnitySequenceInput.Instance;
        private readonly LoggingNarrativePresenter loggingPresenter = new LoggingNarrativePresenter();
        private readonly SceneServiceRegistry sceneServices = new SceneServiceRegistry();

        public NarrativeCatalog Catalog => catalog;
        public NarrativeState State => state;
        public ChapterDefinition CurrentChapter => state == null || catalog == null
            ? null
            : catalog.ChapterAt(state.ChapterIndex);
        public bool IsNarrativeInputCaptured => narrativeInputLocks > 0;
        public bool IsPlayerLocked => playerLocks > 0;
        public bool SkipGates { get; set; }
        public bool IsSaveDirty => saveDirty;
        public bool CanLoadSavedGame => !sceneLoadInProgress;
        public bool HasRecoverableInMemoryProgress =>
            saveDirty && state != null && CurrentChapter != null;
        public bool CanSaveManually =>
            state != null &&
            CurrentChapter != null &&
            string.IsNullOrWhiteSpace(CurrentEndingId);
        public string CompletedEndingId { get; private set; }
        public string CurrentEndingId { get; private set; }
        /// <summary>
        /// The beat currently being dispatched by the chapter runner. This is
        /// runtime-only so presentation can identify a pending objective without
        /// changing saved narrative state.
        /// </summary>
        public NarrativeBeat ActiveNarrativeBeat { get; private set; }
        public string ActiveSequenceId { get; private set; }
        public SceneServiceRegistry SceneServices => sceneServices;
        public ISequenceClock SequenceClock => sequenceClock;
        public ISequenceInput SequenceInput => sequenceInput;
        public bool IsSequencePaused =>
            sequenceClock is IPausableSequenceClock pausableClock && pausableClock.IsPaused;
        public bool IsDreamTransitionActive => dreamTransitionMusicActive;
        public bool HasActiveSequencePlayback =>
            chapterRunner != null && chapterRunner.HasActiveSequencePlayback;
        public bool HasActiveNarrativeRunner => runner != null;

        private ISceneLoader SceneLoader => sceneLoader ?? UnitySceneLoader.Instance;
        private ChapterRunner Runner
        {
            get
            {
                if (chapterRunner == null)
                    chapterRunner = new ChapterRunner(this);

                return chapterRunner;
            }
        }

        public event Action<string> CompletedEndingChanged;
        public event Action InventoryStateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            InstanceChanged = null;
        }

        public static GameSession Create(NarrativeCatalog narrativeCatalog, PresentationSettings settings)
        {
            if (Instance != null)
            {
                Instance.Configure(narrativeCatalog, settings);
                return Instance;
            }

            var gameObject = new GameObject("GameSession");
            GameSession session = gameObject.AddComponent<GameSession>();
            session.Configure(narrativeCatalog, settings);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<DebugMenu>();
#endif
            return session;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstanceChanged?.Invoke(this);
        }

        private void OnDestroy()
        {
            chapterRunner?.CancelActiveSequence();
            DisposeActiveDreamTransitionScreen();
            StopDreamTransitionMusic();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (state != null)
            {
                state.FlagChanged -= HandleFlagChanged;
                state.Tasks.ProgressChanged -= HandleTaskProgressChanged;
                state.Inventory.Changed -= HandleInventoryStateChanged;
            }
            if (Instance == this)
            {
                Instance = null;
                InstanceChanged?.Invoke(null);
            }
            sceneServices.Clear();
        }

        public void Configure(NarrativeCatalog narrativeCatalog, PresentationSettings settings)
        {
            catalog = narrativeCatalog;
            presentationSettings = settings;
            if (sceneServices.TryGetPresenter(
                    out INarrativePresenter registeredPresenter,
                    out _) &&
                registeredPresenter is NarrativePresenter concretePresenter)
            {
                concretePresenter.Configure(
                    settings,
                    sequenceClock,
                    sequenceInput);
            }
        }

#if UNITY_EDITOR
        public void SetSceneLoaderForTests(ISceneLoader testSceneLoader)
        {
            sceneLoader = testSceneLoader ?? throw new ArgumentNullException(nameof(testSceneLoader));
        }
#endif

        public void ConfigureSequenceServices(ISequenceClock clock, ISequenceInput input)
        {
            sequenceClock = clock ?? UnitySequenceClock.Instance;
            sequenceInput = input ?? UnitySequenceInput.Instance;

            if (sceneServices.TryGetPresenter(
                    out INarrativePresenter registeredPresenter,
                    out _) &&
                registeredPresenter is NarrativePresenter concretePresenter)
            {
                concretePresenter.Configure(
                    presentationSettings,
                    sequenceClock,
                    sequenceInput);
            }
        }

        /// <summary>
        /// Pauses only the production sequence clock. Injected test clocks remain
        /// deterministic and need not implement a global pause switch.
        /// </summary>
        public void SetSequencePaused(bool paused)
        {
            if (sequenceClock is IPausableSequenceClock pausableClock)
                pausableClock.SetPaused(paused);
        }

        private void EnsureProductionPauseController()
        {
            // Injected clocks/input deliberately isolate non-production runs
            // from physical player input. The persistent pause UI belongs
            // only to the production service pair.
            if (ReferenceEquals(sequenceClock, UnitySequenceClock.Instance) &&
                ReferenceEquals(sequenceInput, UnitySequenceInput.Instance))
            {
                PauseController.EnsureInstance();
            }
        }

        public void BeginNewGame(NameVariant chosenName)
        {
            if (!CanStart() || !CanLoadSavedGame)
                return;

            EnsureProductionPauseController();
            StopActiveRunner();
            SetCompletedEnding(null);
            autosaveDiscardPending =
                SaveGameStore.TryDiscard() != SaveWriteStatus.Succeeded;
            saveDirty = autosaveDiscardPending;
            pendingStatusMessage = saveDirty ? SaveFailureStatus : null;
            PrepareInitialPlacement(null, clearLastValidated: true);
            SetState(new NarrativeState(chosenName, catalog.InitialBloomCount)
            {
                ChapterIndex = catalog.Chapters[0].Index,
                BeatIndex = 0
            });
            StartAtCurrentChapter();
        }

        public bool ContinueGame() => ContinueGame(out _);

        public bool ContinueGame(out SaveReadStatus readStatus)
        {
            if (!CanStart())
            {
                readStatus = SaveReadStatus.CatalogUnavailable;
                return false;
            }

            if (!CanLoadSavedGame)
            {
                readStatus = SaveReadStatus.TransientFailure;
                ShowStatus("LOAD UNAVAILABLE WHILE TRAVEL IS IN PROGRESS.");
                return false;
            }

            EnsureProductionPauseController();
            StopActiveRunner();

            // A scene-load recovery may have returned to the menu while the
            // latest cursor was still dirty. Retry that exact in-memory state
            // before consulting an older on-disk slot.
            if (TryResumeDirtyState(out readStatus))
            {
                StartAtCurrentChapter(playLocationTransition: false);
                return true;
            }

            readStatus = SaveGameStore.GetLoadStatus(
                catalog,
                out NarrativeState restoredState,
                out SaveWriteStatus promotionStatus,
                out string completedEndingId,
                out SavedPlayerLocation restoredPlayerLocation);
            if (readStatus != SaveReadStatus.Valid && readStatus != SaveReadStatus.Recovered)
            {
                // A Continue click is an explicit player action. Keep transient
                // failures intact, but discard a save that cannot be restored.
                if (readStatus == SaveReadStatus.Invalid &&
                    SaveGameStore.TryDiscard() != SaveWriteStatus.Succeeded)
                {
                    readStatus = SaveReadStatus.TransientFailure;
                }
                return false;
            }

            saveDirty = readStatus == SaveReadStatus.Recovered &&
                promotionStatus != SaveWriteStatus.Succeeded;
            autosaveDiscardPending = false;
            pendingStatusMessage = readStatus == SaveReadStatus.Recovered
                ? saveDirty
                    ? "PREVIOUS SAVE RECOVERED.\nPROGRESS COULD NOT BE SAVED YET."
                    : "PREVIOUS SAVE RECOVERED."
                : null;
            PrepareInitialPlacement(restoredPlayerLocation, clearLastValidated: true);
            SetState(restoredState);
            SetCompletedEnding(completedEndingId);
            if (RestoreLegacyInventoryState())
                PersistState();
            StartAtCurrentChapter(playLocationTransition: false);
            return true;
        }

        /// <summary>
        /// Writes the current progress to one of the player's explicit pause-menu
        /// slots. Saves are disabled while an ending is running because ending
        /// beat progress is deliberately not represented by the save format.
        /// </summary>
        public SaveWriteStatus SaveManualSlot(int slotNumber)
        {
            if (!CanSaveManually)
            {
                ShowStatus("SAVING IS UNAVAILABLE DURING AN ENDING.");
                return SaveWriteStatus.TransientFailure;
            }

            if (!CanPersistCurrentState(out string validationError))
            {
                Debug.LogError(validationError);
                ShowStatus("SAVE FAILED.\nINVALID PROGRESSION STATE.");
                return SaveWriteStatus.TransientFailure;
            }

            SaveWriteStatus status = SaveGameStore.TrySaveManualSlot(
                slotNumber,
                state,
                CompletedEndingId,
                CapturePlayerLocationForSave());
            ShowStatus(status == SaveWriteStatus.Succeeded
                ? $"SAVED TO SLOT {slotNumber:D2}."
                : "SAVE FAILED.\nPLEASE TRY AGAIN.");
            return status;
        }

        /// <summary>
        /// Replaces the active narrative state with a selected manual save and
        /// restarts normal chapter loading at that save's authored location.
        /// </summary>
        public bool LoadManualSlot(int slotNumber, out SaveReadStatus readStatus)
        {
            if (!CanStart())
            {
                readStatus = SaveReadStatus.CatalogUnavailable;
                return false;
            }

            if (!CanLoadSavedGame)
            {
                readStatus = SaveReadStatus.TransientFailure;
                ShowStatus("LOAD UNAVAILABLE WHILE TRAVEL IS IN PROGRESS.");
                return false;
            }

            EnsureProductionPauseController();
            readStatus = SaveGameStore.GetManualSlotLoadStatus(
                slotNumber,
                catalog,
                out NarrativeState restoredState,
                out SaveWriteStatus promotionStatus,
                out string completedEndingId,
                out SavedPlayerLocation restoredPlayerLocation);
            if (readStatus != SaveReadStatus.Valid && readStatus != SaveReadStatus.Recovered)
                return false;

            // Loading a slot is a timeline replacement. Cancel an active
            // ending and restore its transaction before applying the selected
            // slot, otherwise StopActiveRunner would later overwrite that slot.
            StopActiveRunner();
            saveDirty = readStatus == SaveReadStatus.Recovered &&
                promotionStatus != SaveWriteStatus.Succeeded;
            // A manual slot replaces the active timeline. Clear the old
            // autosave pair before writing this selection so its backup can
            // never recover an unrelated playthrough.
            autosaveDiscardPending = true;
            pendingStatusMessage = null;
            PrepareInitialPlacement(restoredPlayerLocation, clearLastValidated: true);
            SetState(restoredState);
            SetCompletedEnding(completedEndingId);
            RestoreLegacyInventoryState();

            // The selected manual slot is now the active timeline. Make it the
            // Continue target immediately instead of waiting for another world
            // change or authored checkpoint.
            PersistState();
            StartAtCurrentChapter(playLocationTransition: false);

            // StartAtCurrentChapter clears runner-owned locks. The pause menu's
            // lock is deliberately restored so a loaded game stays paused until
            // the player explicitly chooses Continue.
            if (PauseController.Instance != null && PauseController.Instance.IsPaused)
                PushPlayerLock();

            return true;
        }

        private bool TryResumeDirtyState(out SaveReadStatus readStatus)
        {
            readStatus = SaveReadStatus.Missing;
            if (!HasRecoverableInMemoryProgress)
                return false;

            readStatus = SaveReadStatus.ResumedInMemory;
            PrepareInitialPlacement(lastValidatedPlayerLocation, clearLastValidated: false);
            PersistState();
            return true;
        }

        public void RegisterPresenter(INarrativePresenter narrativePresenter)
        {
            sceneServices.RegisterPresenter(narrativePresenter);
            if (narrativePresenter is NarrativePresenter concretePresenter)
            {
                concretePresenter.Configure(
                    presentationSettings,
                    sequenceClock,
                    sequenceInput);
            }

            ReplayPendingStatus();
        }

        public void UnregisterPresenter(INarrativePresenter narrativePresenter)
        {
            sceneServices.UnregisterPresenter(narrativePresenter);
            ReplayPendingStatus();
        }

        public void RegisterSequencePlayer(ISequencePlayer sequencePlayer)
        {
            Runner.RegisterSequencePlayer(sequencePlayer);
        }

        public void UnregisterSequencePlayer(ISequencePlayer sequencePlayer)
        {
            if (chapterRunner != null)
                chapterRunner.UnregisterSequencePlayer(sequencePlayer);
            else
                sceneServices.UnregisterSequencePlayer(sequencePlayer);
        }

        public void RegisterPatientVisualPresenter(IPatientVisualPresenter presenter) =>
            sceneServices.RegisterPatientVisualPresenter(presenter);

        public void UnregisterPatientVisualPresenter(IPatientVisualPresenter presenter) =>
            sceneServices.UnregisterPatientVisualPresenter(presenter);

        public void RegisterPlayer(global::FirstPersonController player, Camera camera)
        {
            sceneServices.RegisterPlayer(player, camera);

            // Players enabling during a managed scene load are not safe save
            // sources until the authored spawn (or restored pose) is applied.
            // A player registered in an already-stable scene, including focused
            // tests and the SampleScene sandbox, can be captured immediately.
            if (!sceneLoadInProgress && !initialPlacementPending)
                playerPlacementValidated = true;
        }

        public void UnregisterPlayer(global::FirstPersonController player) =>
            sceneServices.UnregisterPlayer(player);

        public void RegisterCarriedItemHolder(CarriedItemHolder holder) =>
            sceneServices.RegisterCarriedItemHolder(holder);

        public void UnregisterCarriedItemHolder(CarriedItemHolder holder) =>
            sceneServices.UnregisterCarriedItemHolder(holder);

        public bool TryGetCarriedItemHolder(out CarriedItemHolder holder) =>
            sceneServices.TryGetCarriedItemHolder(out holder, out _);

        public void RegisterVisionBleed(VisionBleedController visionBleed) =>
            sceneServices.RegisterVisionBleed(visionBleed);

        public void UnregisterVisionBleed(VisionBleedController visionBleed) =>
            sceneServices.UnregisterVisionBleed(visionBleed);

        public void RegisterWorldOutput(global::LowResolutionPresenter output) =>
            sceneServices.RegisterWorldOutput(output);

        public void UnregisterWorldOutput(global::LowResolutionPresenter output) =>
            sceneServices.UnregisterWorldOutput(output);

        public void RegisterSpawnPoint(SpawnPoint spawnPoint) =>
            sceneServices.RegisterSpawnPoint(spawnPoint);

        public void UnregisterSpawnPoint(SpawnPoint spawnPoint) =>
            sceneServices.UnregisterSpawnPoint(spawnPoint);

        public void RegisterDocument(DocumentInteractable document) =>
            sceneServices.RegisterDocument(document);

        public void UnregisterDocument(DocumentInteractable document) =>
            sceneServices.UnregisterDocument(document);

        public void SetFlag(FlagId flag)
        {
            if (state == null || flag == null)
                return;

            // A configured production session must never accept a flag that
            // its catalog cannot restore. Lightweight editor/sandbox sessions
            // may intentionally omit a catalog; persistence still rejects
            // those incomplete states at the write boundary below.
            if (catalog != null && !IsCataloguedFlag(flag))
            {
                Debug.LogError(
                    $"Refusing to set uncatalogued flag '{flag.Id}'. " +
                    "Persisting it would create a save that cannot be restored.");
                return;
            }

            state.SetFlag(flag);
        }

        public void ClearFlag(FlagId flag)
        {
            if (state == null || flag == null)
                return;

            if (catalog != null && !IsCataloguedFlag(flag))
            {
                Debug.LogError(
                    $"Refusing to clear uncatalogued flag '{flag.Id}'.");
                return;
            }

            state.ClearFlag(flag);
        }

        public void UnlockInventory()
        {
            state?.Inventory.Unlock();
        }

        public void GiveInventoryItem(string itemId)
        {
            state?.Inventory.AddItem(itemId);
        }

        public void SetCarriedItem(Carryable item)
        {
            state?.Inventory.SetCarriedItem(
                item != null ? item.PersistentSaveId : null,
                item != null && item.Category != null ? item.Category.Id : null);
        }

        public void OpenDocument(IReadableDocument document)
        {
            if (document == null || state == null)
                return;

            if (ReferenceEquals(document, awaitedDocument))
            {
                requestedDocument = document;
                return;
            }

            if (documentRoutine != null || IsNarrativeInputCaptured)
                return;

            documentRoutine = StartCoroutine(OpenDocumentRoutine(document));
        }

        public void JumpToChapter(int chapterIndex, int beatIndex = 0)
        {
            ChapterDefinition chapter = catalog != null ? catalog.ChapterAt(chapterIndex) : null;
            if (chapter == null || state == null || sceneLoadInProgress)
                return;

            StopActiveRunner();
            SetCompletedEnding(null);
            PrepareInitialPlacement(null, clearLastValidated: true);
            RebuildFlagsForDebugChapterJump(chapter.Index);
            state.ChapterIndex = chapter.Index;
            state.BeatIndex = Mathf.Clamp(beatIndex, 0, chapter.Beats.Count);
            SaveCheckpoint();
            StartAtCurrentChapter();
        }

        public void JumpToBeat(int beatIndex)
        {
            if (CurrentChapter != null)
                JumpToChapter(CurrentChapter.Index, beatIndex);
        }

        public void StartEnding(EndingDefinition ending)
        {
            if (ending == null || state == null || sceneLoadInProgress)
                return;

            if (!IsCataloguedEnding(ending))
            {
                Debug.LogError(
                    $"Refusing to start uncatalogued ending '{ending.Id}'. " +
                    "Completing it would create a save that cannot be restored.");
                return;
            }

            EnsureProductionPauseController();
            StopActiveRunner();
            BeginEndingTransaction();

            ChapterDefinition finalChapter = catalog != null ? catalog.FinalChapter : null;
            if (finalChapter != null)
                RebuildFlagsForDebugChapterJump(finalChapter.Index);

            SetCurrentEnding(ending.Id);
            runner = StartCoroutine(LoadFinalChapterAndRunEnding(ending));
        }

        public void PushNarrativeInputLock() => narrativeInputLocks++;

        public void PopNarrativeInputLock() =>
            narrativeInputLocks = Mathf.Max(0, narrativeInputLocks - 1);

        public void PushPlayerLock() => playerLocks++;

        public void PopPlayerLock() =>
            playerLocks = Mathf.Max(0, playerLocks - 1);

        public void SaveCheckpoint()
        {
            PersistState();
        }

        public void DebugSetTaskCount(TaskObjective objective, int count)
        {
            if (state == null || objective == null || catalog == null ||
                !ReferenceEquals(catalog.TaskObjectiveWithId(objective.Id), objective))
            {
                return;
            }

            state.Tasks.SetCount(objective, count);
            TaskReceiver.RegisterSceneTasks(state);
            TaskProgressListener.RefreshSceneObjects(state);
        }

        private bool CanStart()
        {
            if (catalog != null && catalog.Chapters.Count > 0 && catalog.Chapters[0] != null)
                return true;

            Debug.LogError("GameSession cannot start without a populated NarrativeCatalog.");
            return false;
        }

        private void SetState(NarrativeState newState)
        {
            if (state != null)
            {
                state.FlagChanged -= HandleFlagChanged;
                state.Tasks.ProgressChanged -= HandleTaskProgressChanged;
                state.Inventory.Changed -= HandleInventoryStateChanged;
            }

            state = newState;
            if (state != null)
            {
                state.FlagChanged += HandleFlagChanged;
                state.Tasks.ProgressChanged += HandleTaskProgressChanged;
                state.Inventory.Changed += HandleInventoryStateChanged;
            }

            InventoryStateChanged?.Invoke();
        }

        internal void PersistState()
        {
            if (state == null)
                return;

            SavedPlayerLocation playerLocation = CapturePlayerLocationForSave();
            bool wasDirty = saveDirty;
            saveQueued = false;
            if (autosaveDiscardPending)
            {
                if (SaveGameStore.TryDiscard() != SaveWriteStatus.Succeeded)
                {
                    saveDirty = true;
                    ShowStatus(SaveFailureStatus);
                    return;
                }

                autosaveDiscardPending = false;
            }

            if (!CanPersistCurrentState(out string validationError))
            {
                Debug.LogError(validationError);
                saveDirty = true;
                ShowStatus(SaveFailureStatus);
                return;
            }

            SaveWriteStatus status = SaveGameStore.TrySave(
                state,
                CompletedEndingId,
                playerLocation);
            saveDirty = status != SaveWriteStatus.Succeeded;

            if (saveDirty)
            {
                ShowStatus(SaveFailureStatus);
            }
            else if (wasDirty)
            {
                ShowStatus("PROGRESS SAVED.");
            }
        }

        private void PrepareInitialPlacement(
            SavedPlayerLocation restoredPlayerLocation,
            bool clearLastValidated)
        {
            pendingLoadedPlayerLocation = restoredPlayerLocation?.Copy();
            initialPlacementPending = true;
            playerPlacementValidated = false;
            if (clearLastValidated)
                lastValidatedPlayerLocation = restoredPlayerLocation?.Copy();
        }

        private SavedPlayerLocation CapturePlayerLocationForSave()
        {
            // Loading a slot immediately rewrites the active autosave. Until its
            // destination is placed, preserve the loaded pose rather than
            // accidentally capturing the outgoing scene's player.
            if (initialPlacementPending)
                return pendingLoadedPlayerLocation?.Copy();

            // OnEnable/Start callbacks can autosave while a new scene's player
            // still has a prefab/default transform. Retain only the last pose
            // that completed the placement contract.
            if (sceneLoadInProgress || !playerPlacementValidated)
                return lastValidatedPlayerLocation?.Copy();

            if (!sceneServices.TryGetPlayer(
                    out FirstPersonController player,
                    out int playerCount) ||
                playerCount != 1 ||
                player == null)
            {
                return lastValidatedPlayerLocation?.Copy();
            }

            Scene playerScene = player.gameObject.scene;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!playerScene.IsValid() ||
                !activeScene.IsValid() ||
                playerScene != activeScene ||
                string.IsNullOrWhiteSpace(playerScene.name))
            {
                return lastValidatedPlayerLocation?.Copy();
            }

            var captured = new SavedPlayerLocation
            {
                sceneName = playerScene.name,
                position = player.transform.position,
                bodyRotation = player.transform.rotation,
                viewPitch = player.ViewPitch
            };
            lastValidatedPlayerLocation = captured.Copy();
            return captured;
        }

        private bool CanPersistCurrentState(out string error)
        {
            if (state == null || catalog == null)
            {
                error = "Refusing to persist progression without active state and catalog data.";
                return false;
            }

            ChapterDefinition chapter = catalog.ChapterAt(state.ChapterIndex);
            if (chapter == null ||
                state.BeatIndex < 0 ||
                state.BeatIndex > chapter.Beats.Count)
            {
                error =
                    $"Refusing to persist invalid narrative cursor " +
                    $"{state.ChapterIndex}:{state.BeatIndex}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CompletedEndingId) &&
                catalog.EndingWithId(CompletedEndingId) == null)
            {
                error =
                    $"Refusing to persist uncatalogued completed ending " +
                    $"'{CompletedEndingId}'.";
                return false;
            }

            foreach (FlagId flag in state.Flags)
            {
                if (IsCataloguedFlag(flag))
                    continue;

                string flagId = flag != null ? flag.Id : "<null>";
                error =
                    $"Refusing to persist uncatalogued flag '{flagId}'. " +
                    "The resulting save would fail its own catalog validation.";
                return false;
            }

            foreach (KeyValuePair<string, int> taskCount in state.Tasks.Counts)
            {
                if (!string.IsNullOrWhiteSpace(taskCount.Key) &&
                    taskCount.Value >= 0 &&
                    catalog.TaskObjectiveWithId(taskCount.Key) != null)
                {
                    continue;
                }

                error =
                    $"Refusing to persist invalid task progress " +
                    $"'{taskCount.Key}' ({taskCount.Value}).";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool IsCataloguedFlag(FlagId flag) =>
            flag != null &&
            catalog != null &&
            !string.IsNullOrWhiteSpace(flag.Id) &&
            ReferenceEquals(catalog.FlagWithId(flag.Id), flag);

        internal bool IsCataloguedEnding(EndingDefinition ending) =>
            ending != null &&
            catalog != null &&
            !string.IsNullOrWhiteSpace(ending.Id) &&
            ReferenceEquals(catalog.EndingWithId(ending.Id), ending);

        private void ShowStatus(string message)
        {
            if (sceneServices.TryGetPresenter(
                    out INarrativePresenter activePresenter,
                    out _))
            {
                activePresenter.ShowStatus(message);
                return;
            }

            pendingStatusMessage = message;
        }

        private void ReplayPendingStatus()
        {
            if (!sceneServices.TryGetPresenter(
                    out INarrativePresenter activePresenter,
                    out _))
                return;

            if (!string.IsNullOrWhiteSpace(pendingStatusMessage))
            {
                string message = pendingStatusMessage;
                pendingStatusMessage = null;
                activePresenter.ShowStatus(message);
                return;
            }

            if (saveDirty)
                activePresenter.ShowStatus(SaveFailureStatus);
        }

        private void HandleFlagChanged(FlagId flag, bool present)
        {
            FlagGatedObject.RefreshSceneObjects(state);
            QueueOrPersistStateChange();
        }

        private void HandleInventoryStateChanged()
        {
            InventoryStateChanged?.Invoke();
            QueueOrPersistStateChange();
        }

        private bool RestoreLegacyInventoryState()
        {
            if (state == null || state.HasSavedInventoryState || catalog == null)
                return false;

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            bool unlocked = false;
            for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
            {
                ChapterDefinition chapter = catalog.Chapters[chapterIndex];
                if (chapter == null || chapter.Index > state.ChapterIndex)
                    continue;

                int completedBeatCount = chapter.Index < state.ChapterIndex
                    ? chapter.Beats.Count
                    : Mathf.Clamp(state.BeatIndex, 0, chapter.Beats.Count);
                for (int beatIndex = 0; beatIndex < completedBeatCount; beatIndex++)
                {
                    if (!(chapter.Beats[beatIndex] is LineBeat lineBeat))
                        continue;

                    ChapterRunner.ExtractInlineTags(
                        lineBeat.Resolve(state.ChosenName),
                        out bool unlockInventory,
                        out string itemId);
                    unlocked |= unlockInventory;
                    if (!string.IsNullOrWhiteSpace(itemId))
                        itemIds.Add(itemId);
                }
            }

            state.RestoreInventory(
                unlocked,
                itemIds,
                carriedItemId: null,
                carriedCategoryId: null,
                wasRecordedInSave: true);
            return true;
        }

        // Chapter jumps model a completed playthrough up to, but not including,
        // the destination chapter. This lets a jump forward open prior-world
        // progression while a jump backward removes future-world progression.
        private void RebuildFlagsForDebugChapterJump(int destinationChapterIndex)
        {
            if (catalog == null || state == null)
                return;

            var completedFlags = new HashSet<FlagId>();
            for (int i = 0; i < catalog.Chapters.Count; i++)
            {
                ChapterDefinition chapter = catalog.Chapters[i];
                if (chapter != null && chapter.Index < destinationChapterIndex)
                    CollectCompletedChapterFlags(chapter, completedFlags);
            }

            saveBatchDepth++;
            try
            {
                var existingFlags = new List<FlagId>(state.Flags);
                for (int i = 0; i < existingFlags.Count; i++)
                    state.ClearFlag(existingFlags[i]);

                foreach (FlagId flag in completedFlags)
                    state.SetFlag(flag);
            }
            finally
            {
                saveBatchDepth--;
                // JumpToChapter saves the rebuilt state immediately afterwards.
                // Do not leave a deferred flag-change save queued for a later,
                // unrelated state change.
                if (saveBatchDepth == 0)
                    saveQueued = false;
            }
        }

        private static void CollectCompletedChapterFlags(
            ChapterDefinition chapter,
            ISet<FlagId> completedFlags)
        {
            if (chapter == null || completedFlags == null)
                return;

            for (int i = 0; i < chapter.Beats.Count; i++)
            {
                NarrativeBeat beat = chapter.Beats[i];
                if (beat == null)
                    continue;

                for (int effectIndex = 0; effectIndex < beat.OnComplete.Count; effectIndex++)
                {
                    if (beat.OnComplete[effectIndex] is SetFlagEffect setFlag)
                        AddFlag(setFlag.Flag, completedFlags);
                }

                if (beat is GateBeat gate)
                    CollectRequiredFlags(gate.Condition, completedFlags);

                if (beat is DocumentSequenceBeat documentSequence)
                    AddFlag(documentSequence.Document?.SetWhenRead, completedFlags);
            }
        }

        private static void CollectRequiredFlags(
            BeatCondition condition,
            ISet<FlagId> completedFlags)
        {
            switch (condition)
            {
                case FlagCondition flag when flag.MustBePresent:
                    AddFlag(flag.Flag, completedFlags);
                    break;
                case AllOfCondition all:
                    CollectRequiredFlags(all.Conditions, completedFlags);
                    break;
                case AnyOfCondition any:
                    CollectRequiredFlags(any.Conditions, completedFlags);
                    break;
            }
        }

        private static void CollectRequiredFlags(
            IReadOnlyList<BeatCondition> conditions,
            ISet<FlagId> completedFlags)
        {
            if (conditions == null)
                return;

            for (int i = 0; i < conditions.Count; i++)
                CollectRequiredFlags(conditions[i], completedFlags);
        }

        private static void AddFlag(FlagId flag, ISet<FlagId> flags)
        {
            if (flag != null)
                flags.Add(flag);
        }

        private void HandleTaskProgressChanged(TaskObjective objective, int count)
        {
            TaskProgressListener.RefreshSceneObjects(state);
            QueueOrPersistStateChange();
        }

        private void QueueOrPersistStateChange()
        {
            // Runtime sessions are configured with a catalog before state can
            // change. Editor component fixtures and the SampleScene can also
            // use a bare GameSession as a scene-service container; let them
            // project state without attempting an invalid persistence write.
            if (state == null || catalog == null)
                return;

            if (saveBatchDepth > 0 || !string.IsNullOrWhiteSpace(CurrentEndingId))
            {
                saveQueued = true;
                return;
            }

            PersistState();
        }

        private void StartAtCurrentChapter(bool playLocationTransition = true)
        {
            StopActiveRunner();
            ChapterDefinition chapter = CurrentChapter;
            if (chapter == null)
                return;

            runner = StartCoroutine(LoadChapterAndRun(chapter, playLocationTransition));
        }

        private void StopActiveRunner()
        {
            chapterRunner?.CancelActiveSequence();
            if (runner != null)
                StopCoroutine(runner);
            runner = null;
            bool interruptedSceneLoad = sceneLoadInProgress;
            sceneLoadInProgress = false;
            if (interruptedSceneLoad)
                playerPlacementValidated = false;
            RollbackEndingTransaction();
            DisposeActiveDreamTransitionScreen();
            StopDreamTransitionMusic();

            if (documentRoutine != null)
                StopCoroutine(documentRoutine);
            documentRoutine = null;
            awaitedDocument = null;
            requestedDocument = null;

            if (sceneServices.TryGetPresenter(
                    out INarrativePresenter activePresenter,
                    out _))
            {
                activePresenter.ClearPresentation();
            }
            narrativeInputLocks = 0;
            playerLocks = 0;
            saveBatchDepth = 0;
            saveQueued = false;
            SetActiveNarrativeBeat(null);
            SetCurrentEnding(null);
            SetActiveSequence(null);
        }

        private IEnumerator LoadChapterAndRun(
            ChapterDefinition chapter,
            bool playLocationTransition)
        {
            var loadResult = new NarrativeOperationResult();
            yield return LoadChapterLocation(
                chapter,
                loadResult,
                playLocationTransition);
            if (!loadResult.Succeeded)
            {
                yield return RecoverFromNarrativeFailure(loadResult);
                runner = null;
                yield break;
            }

            yield return RunChapter(chapter);
            runner = null;
        }

        private IEnumerator RunChapter(ChapterDefinition chapter)
        {
            var result = new NarrativeOperationResult();
            yield return Runner.RunChapter(chapter, result);
            if (!result.Succeeded)
                yield return RecoverFromNarrativeFailure(result);
        }

        internal IEnumerator LoadChapterLocation(
            ChapterDefinition chapter,
            NarrativeOperationResult result,
            bool playTransition = true)
        {
            if (chapter == null || state == null)
            {
                FailOperation(result, "Narrative chapter travel cannot run without a chapter and active state.");
                yield break;
            }

            state.BeatIndex = Mathf.Clamp(state.BeatIndex, 0, chapter.Beats.Count);

            // A cursor on an unexecuted TravelBeat must let that beat perform
            // the sole scene load. This applies to any pending beat, not only
            // a chapter's first one.
            if (PendingTravelWillLoad(chapter))
            {
                result.Succeed();
                yield break;
            }

            ResolveResumeLocation(chapter, out string sceneName, out string spawnPointId);
            yield return LoadScene(
                sceneName,
                spawnPointId,
                result,
                playTransition);
        }

        private bool PendingTravelWillLoad(ChapterDefinition chapter) =>
            chapter != null &&
            state != null &&
            state.BeatIndex >= 0 &&
            state.BeatIndex < chapter.Beats.Count &&
            chapter.Beats[state.BeatIndex] is TravelBeat;

        private void ResolveResumeLocation(
            ChapterDefinition chapter,
            out string sceneName,
            out string spawnPointId)
        {
            sceneName = chapter.BaseScene;
            spawnPointId = chapter.BaseSpawnPointId;

            for (int i = state.BeatIndex - 1; i >= 0; i--)
            {
                if (!(chapter.Beats[i] is TravelBeat travelBeat))
                    continue;

                sceneName = travelBeat.TargetScene;
                spawnPointId = travelBeat.SpawnPointId;
                return;
            }
        }

        private IEnumerator LoadFinalChapterAndRunEnding(EndingDefinition ending)
        {
            ChapterDefinition finalChapter = catalog != null ? catalog.FinalChapter : null;
            if (finalChapter == null)
            {
                RollbackEndingTransaction();
                SetCurrentEnding(null);
                Debug.LogError("Cannot start an ending because the narrative catalog has no final chapter.");
                yield return RecoverFromNarrativeFailure(NarrativeFailureKind.Data);
                runner = null;
                yield break;
            }

            SetCompletedEnding(null);
            state.ChapterIndex = finalChapter.Index;
            state.BeatIndex = finalChapter.Beats.Count;

            var loadResult = new NarrativeOperationResult();
            yield return LoadScene(finalChapter.BaseScene, finalChapter.EndingSpawnPointId, loadResult);
            if (!loadResult.Succeeded)
            {
                RollbackEndingTransaction();
                SetCurrentEnding(null);
                yield return RecoverFromNarrativeFailure(loadResult);
                runner = null;
                yield break;
            }

            var endingResult = new NarrativeOperationResult();
            yield return Runner.RunEnding(ending, endingResult);
            if (!endingResult.Succeeded)
                yield return RecoverFromNarrativeFailure(endingResult);
            runner = null;
        }

        internal void SetCompletedEnding(string endingId)
        {
            CompletedEndingId = endingId;
            CompletedEndingChanged?.Invoke(endingId);
        }

        internal void SetCurrentEnding(string endingId)
        {
            CurrentEndingId = endingId;
        }

        internal void FinishEndingSave(bool completed)
        {
            saveQueued = false;
            if (completed)
            {
                endingRollbackSnapshot = null;
                PersistState();
            }
            else
            {
                RollbackEndingTransaction();
            }
        }

        internal void BeginEndingTransaction()
        {
            if (state == null || endingRollbackSnapshot != null)
                return;

            endingRollbackSnapshot = SaveGameStore.Capture(
                state,
                CompletedEndingId,
                CapturePlayerLocationForSave());
        }

        private void RollbackEndingTransaction()
        {
            SaveGameData snapshot = endingRollbackSnapshot;
            if (snapshot == null)
                return;

            // Clear first so state-change listeners or cancellation cleanup
            // cannot recursively attempt the same rollback.
            endingRollbackSnapshot = null;
            if (!SaveGameStore.TryRestoreSnapshot(
                    catalog,
                    snapshot,
                    out NarrativeState restoredState,
                    out string restoredEndingId,
                    out SavedPlayerLocation restoredPlayerLocation))
            {
                Debug.LogError("Could not restore the pre-ending save snapshot after ending playback stopped.");
                return;
            }

            saveBatchDepth = 0;
            saveQueued = false;
            PrepareInitialPlacement(
                restoredPlayerLocation,
                clearLastValidated: true);
            SetState(restoredState);
            SetCompletedEnding(restoredEndingId);
        }

        internal void ApplyInlineInventoryGrants(bool unlockInventory, string itemId)
        {
            if (state == null)
                return;

            // Inline grants belong to the line beat that contains their tags.
            // Defer their save until CompleteBeat advances the cursor so a
            // snapshot can never contain the reward while replaying its line.
            saveBatchDepth++;
            try
            {
                if (unlockInventory)
                    state.Inventory.Unlock();
                if (!string.IsNullOrWhiteSpace(itemId))
                    state.Inventory.AddItem(itemId);
            }
            finally
            {
                saveBatchDepth--;
            }
        }

        internal void SetActiveNarrativeBeat(NarrativeBeat beat)
        {
            ActiveNarrativeBeat = beat;
        }

        internal void SetActiveSequence(string sequenceId)
        {
            ActiveSequenceId = sequenceId;
        }

        internal IEnumerator PresentLine(ResolvedLine line, string speakerName, float intensity)
        {
            INarrativePresenter activePresenter = ResolvePresenter();
            PushNarrativeInputLock();
            bool lockPlayer = presentationSettings != null && presentationSettings.LockPlayerDuringLines;
            if (lockPlayer)
                PushPlayerLock();

            try
            {
                yield return activePresenter.PresentLine(line, speakerName, intensity);
            }
            finally
            {
                if (lockPlayer)
                    PopPlayerLock();
                PopNarrativeInputLock();
            }
        }

        internal IEnumerator PresentCard(string text)
        {
            INarrativePresenter activePresenter = ResolvePresenter();
            PushNarrativeInputLock();
            PushPlayerLock();
            try
            {
                yield return activePresenter.PresentCard(text);
            }
            finally
            {
                PopPlayerLock();
                PopNarrativeInputLock();
            }
        }

        internal IEnumerator PresentChoice(
            IReadOnlyList<ChoiceOption> options,
            Action<EndingDefinition> selected)
        {
            int optionCount = options?.Count ?? 0;
            if (optionCount != 3)
            {
                Debug.LogError(
                    $"Narrative choice received {optionCount} options; expected exactly three canonical endings.");
            }

            if (options == null || options.Count == 0)
            {
                yield return ResolveChoiceFailure(selected, "Narrative choice has no options.");
                yield break;
            }

            INarrativePresenter activePresenter = ResolvePresenter();
            PushNarrativeInputLock();
            PushPlayerLock();
            EndingDefinition selectedEnding = null;
            try
            {
                yield return activePresenter.PresentChoice(
                    options,
                    ending => selectedEnding = ending);
            }
            finally
            {
                PopPlayerLock();
                PopNarrativeInputLock();
            }

            if (selectedEnding == null || !ContainsChoiceEnding(options, selectedEnding))
            {
                yield return ResolveChoiceFailure(selected, "Narrative choice did not return one of its authored endings.");
                yield break;
            }

            selected?.Invoke(selectedEnding);
        }

        private static bool ContainsChoiceEnding(IReadOnlyList<ChoiceOption> options, EndingDefinition ending)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (ReferenceEquals(options[i].Ending, ending))
                    return true;
            }

            return false;
        }

        private IEnumerator ResolveChoiceFailure(Action<EndingDefinition> selected, string reason)
        {
            Debug.LogError($"{reason} Attempting the defined Confession fallback.");
            EndingDefinition fallback = catalog != null
                ? catalog.EndingWithId(EmptyChoiceFallbackEndingId)
                : null;
            if (fallback != null)
            {
                selected?.Invoke(fallback);
                yield break;
            }

            Debug.LogError(
                "Narrative choice has no fallback ending. The chapter runner will recover without advancing the choice.");
            yield break;
        }

        private IEnumerator ReturnToMainMenuAfterNarrativeError()
        {
            chapterRunner?.CancelActiveSequence();
            StopDreamTransitionMusic();
            SetCurrentEnding(null);
            SetActiveSequence(null);
            const string mainMenuScene = "MainMenu";
            if (!SceneLoader.CanLoad(mainMenuScene))
            {
                Debug.LogError($"Cannot recover from narrative data error because '{mainMenuScene}' is not in Build Settings.");
                yield break;
            }

            sceneLoadInProgress = true;
            try
            {
                ISceneLoadOperation operation = SceneLoader.LoadSingleAsync(mainMenuScene);
                if (operation == null)
                {
                    Debug.LogError($"Cannot recover from narrative data error because loading '{mainMenuScene}' did not start.");
                    yield break;
                }

                while (!operation.IsDone)
                    yield return null;

                // Let the activated menu finish its normal lifecycle before
                // re-enabling timeline replacement controls.
                yield return null;
            }
            finally
            {
                sceneLoadInProgress = false;
            }

            MainMenuController menu = FindFirstObjectByType<MainMenuController>();
            menu?.RefreshSaveAvailability();
        }

        private IEnumerator RecoverFromNarrativeFailure(NarrativeOperationResult result)
        {
            NarrativeFailureKind failureKind = result != null
                ? result.FailureKind
                : NarrativeFailureKind.Data;
            yield return RecoverFromNarrativeFailure(failureKind);
        }

        private IEnumerator RecoverFromNarrativeFailure(NarrativeFailureKind failureKind)
        {
            // Persist the unchanged cursor before leaving gameplay so Continue
            // retries the failed transition instead of replaying later beats or
            // treating the failed operation as complete.
            PersistState();
            yield return PresentCard(RecoveryCardFor(failureKind));
            yield return ReturnToMainMenuAfterNarrativeError();
        }

        private static string RecoveryCardFor(NarrativeFailureKind failureKind)
        {
            switch (failureKind)
            {
                case NarrativeFailureKind.Travel:
                    return "NARRATIVE DATA ERROR\nTRAVEL CANNOT CONTINUE.";
                case NarrativeFailureKind.Sequence:
                    return "NARRATIVE DATA ERROR\nSEQUENCE CANNOT CONTINUE.";
                default:
                    return "NARRATIVE DATA ERROR\nNARRATIVE CANNOT CONTINUE.";
            }
        }

        private IEnumerator OpenDocumentRoutine(IReadableDocument document)
        {
            yield return PresentDocument(document);
            documentRoutine = null;
        }

        internal IEnumerator PresentDocumentSequence(DocumentSequenceBeat beat)
        {
            DocumentDefinition document = beat.Document;
            if (document == null)
            {
                Debug.LogWarning($"{nameof(DocumentSequenceBeat)} has no document assigned.");
                yield break;
            }

            if (!HasActiveDocumentInteractable(document))
            {
                Debug.LogError(
                    $"{nameof(DocumentSequenceBeat)} for '{document.Title}' has no active " +
                    $"{nameof(DocumentInteractable)} in the loaded scene. Waiting for its authored interaction.");
            }

            awaitedDocument = document;
            while (!ReferenceEquals(requestedDocument, document))
                yield return null;

            requestedDocument = null;
            awaitedDocument = null;

            yield return PresentDocument(document);

            ResolvedLine line = beat.Resolve(state.ChosenName);
            string speakerName = beat.Speaker != null ? beat.Speaker.DisplayName : string.Empty;
            yield return PresentLine(line, speakerName, 0f);
        }

        private IEnumerator PresentDocument(IReadableDocument document)
        {
            INarrativePresenter activePresenter = ResolvePresenter();
            PushNarrativeInputLock();
            PushPlayerLock();
            try
            {
                yield return activePresenter.PresentDocument(document, null);
                SetFlag(document.SetWhenRead);
            }
            finally
            {
                PopPlayerLock();
                PopNarrativeInputLock();
            }
        }

        internal IEnumerator LoadScene(
            string sceneName,
            string spawnPointId,
            NarrativeOperationResult result,
            bool playTransition = true)
        {
            result.Reset();
            if (!SceneLoader.CanLoad(sceneName))
            {
                FailOperation(result, $"Narrative travel target is not in Build Settings: '{sceneName}'.");
                yield break;
            }

            sceneLoadInProgress = true;
            playerPlacementValidated = false;

            // Loading a save restores an already-established location. Replaying
            // its arrival card (especially a dream cue) is both narratively wrong
            // and can strand a pause-menu load behind a transition whose clock is
            // intentionally frozen. Authored TravelBeats and chapter changes keep
            // the default and still present their transitions normally.
            SceneTransitionCard transitionCard = playTransition
                ? ResolveSceneTransitionCard(sceneName, spawnPointId)
                : null;
            DreamTransitionCue transitionCue = playTransition
                ? ResolveDreamTransitionCue(sceneName)
                : null;
            bool hasSceneTransition = transitionCard != null || transitionCue != null;
            string transitionText = transitionCard != null
                ? transitionCard.CardText
                : transitionCue != null ? transitionCue.CardText : string.Empty;
            AudioClip transitionMusic = transitionCue != null
                ? transitionCue.Music
                : transitionCard?.Music;
            float transitionMusicVolumeScale = transitionCue != null
                ? transitionCue.VolumeScale
                : transitionCard?.VolumeScale ?? 1f;
            // Only a card explicitly authored to show it keeps the skip
            // prompt (intended for the game's true opening card only) -
            // every other transition (dream or plain card, with or without
            // music) holds for its full authored/music duration. Reading
            // this from the card itself, rather than "is this the first
            // LoadScene since the process started", is what makes it
            // correct even when resuming from a save made partway through
            // the game.
            bool showSkipPrompt = transitionCard != null && transitionCard.ShowSkipPrompt;
            StopDreamTransitionMusic();

            DreamTransitionScreen transitionScreen = null;
            bool transitionLocksHeld = false;
            try
            {
                if (hasSceneTransition)
                {
                    PushNarrativeInputLock();
                    PushPlayerLock();
                    transitionLocksHeld = true;

                    transitionScreen = DreamTransitionScreen.Create(
                        transitionText,
                        sequenceClock);
                    activeDreamTransitionScreen = transitionScreen;
                    AudioManager audioManager = AudioManager.Instance;
                    if (audioManager != null && transitionMusic != null)
                    {
                        dreamTransitionMusicActive = audioManager.PlayExclusiveMusic(
                            transitionMusic,
                            loop: false,
                            volumeScale: transitionMusicVolumeScale);
                        if (dreamTransitionMusicActive)
                        {
                            dreamTransitionMusicStartedAt = sequenceClock.UnscaledTime;
                            // The cleanup routine runs for ANY transition
                            // music (card or cue) so exclusive audio mode
                            // always ends once it finishes playing -
                            // DreamTransitionMusicFinished itself is a
                            // dream-cue-specific contract, and only fires
                            // when there actually is one (see below).
                            dreamTransitionMusicWaitRoutine = StartCoroutine(
                                WaitForDreamTransitionMusicThenNotify(transitionCue));
                        }
                    }

                    // Silent arrival cards deliberately bypass the dream-audio
                    // events. Existing dream cues retain their established
                    // scene-audio handoff, including its fallback behavior.
                    if (transitionCue != null)
                        DreamTransitionStarting?.Invoke(transitionCue);

                    if (transitionScreen != null)
                    {
                        yield return transitionScreen.FadeIn(
                            dreamTransitionSettings.FadeInSeconds);
                    }
                }

                ISceneLoadOperation operation = SceneLoader.LoadSingleAsync(sceneName);
                if (operation == null)
                {
                    if (hasSceneTransition)
                        StopDreamTransitionMusic();
                    FailOperation(result, $"Narrative travel to '{sceneName}' did not start.");
                    yield break;
                }

                while (!operation.IsDone)
                    yield return null;

                yield return null;
                if (!SceneLoader.IsLoadedAndActive(sceneName))
                {
                    if (hasSceneTransition)
                        StopDreamTransitionMusic();
                    FailOperation(result, $"Narrative travel did not activate the requested scene '{sceneName}'.");
                    yield break;
                }

                ResolvePresenter();
                if (!TryPlacePlayerAt(spawnPointId, out string placementError))
                {
                    if (hasSceneTransition)
                        StopDreamTransitionMusic();
                    FailOperation(result, placementError);
                    yield break;
                }

                TaskReceiver.RegisterSceneTasks(state);
                TaskProgressListener.RefreshSceneObjects(state);
                FlagGatedObject.RefreshSceneObjects(state);

                if (transitionScreen != null)
                {
                    if (showSkipPrompt)
                    {
                        yield return transitionScreen.HoldToContinueAndFadeOut(
                            dreamTransitionSettings.FadeOutSeconds);
                    }
                    else
                    {
                        float holdSeconds = dreamTransitionSettings.MinimumVisibleSeconds;
                        if (transitionMusic != null)
                        {
                            float elapsedSinceMusicStart = sequenceClock.UnscaledTime - dreamTransitionMusicStartedAt;
                            float remainingMusicSeconds = Mathf.Max(0f, transitionMusic.length - elapsedSinceMusicStart);
                            holdSeconds = Mathf.Max(holdSeconds, remainingMusicSeconds);
                        }

                        yield return transitionScreen.HoldAndFadeOut(
                            holdSeconds,
                            dreamTransitionSettings.FadeOutSeconds);
                    }
                }

                result.Succeed();
            }
            finally
            {
                sceneLoadInProgress = false;
                if (hasSceneTransition && !result.Succeeded)
                    StopDreamTransitionMusic();
                if (ReferenceEquals(activeDreamTransitionScreen, transitionScreen))
                    activeDreamTransitionScreen = null;
                transitionScreen?.Dispose();
                if (transitionLocksHeld)
                {
                    PopPlayerLock();
                    PopNarrativeInputLock();
                }
            }
        }

        private SceneTransitionCard ResolveSceneTransitionCard(
            string targetSceneName,
            string targetSpawnPointId)
        {
            if (state == null)
                return null;

            if (sceneTransitionSettings == null)
            {
                sceneTransitionSettings = Resources.Load<SceneTransitionSettings>(
                    SceneTransitionSettings.ResourcePath);
            }

            return sceneTransitionSettings != null &&
                   sceneTransitionSettings.TryGetCard(
                       state.ChapterIndex,
                       targetSceneName,
                       targetSpawnPointId,
                       out SceneTransitionCard card)
                ? card
                : null;
        }

        private DreamTransitionCue ResolveDreamTransitionCue(string targetSceneName)
        {
            if (state == null)
                return null;

            if (dreamTransitionSettings == null)
            {
                dreamTransitionSettings = Resources.Load<DreamTransitionSettings>(
                    DreamTransitionSettings.ResourcePath);
            }

            if (dreamTransitionSettings == null)
                return null;

            // Entering a scene configured for the CURRENT chapter (e.g. falling
            // asleep into a dream) takes priority.
            if (dreamTransitionSettings.TryGetCue(
                    state.ChapterIndex,
                    targetSceneName,
                    DreamTransitionTrigger.OnEnterScene,
                    out DreamTransitionCue enterCue))
            {
                return enterCue;
            }

            // Otherwise, check whether the scene we're currently LEAVING is
            // configured to transition out at the end of the current chapter (e.g.
            // waking up). SceneManager reports the scene still active right up
            // until the new one takes over, so this reads correctly here, before
            // the load below begins.
            string currentSceneName = SceneManager.GetActiveScene().name;
            if (dreamTransitionSettings.TryGetCue(
                    state.ChapterIndex,
                    currentSceneName,
                    DreamTransitionTrigger.OnExitScene,
                    out DreamTransitionCue exitCue))
            {
                return exitCue;
            }

            return null;
        }

        private void StopDreamTransitionMusic()
        {
            if (dreamTransitionMusicWaitRoutine != null)
            {
                StopCoroutine(dreamTransitionMusicWaitRoutine);
                dreamTransitionMusicWaitRoutine = null;
            }

            if (!dreamTransitionMusicActive)
                return;

            AudioManager audioManager = AudioManager.ExistingInstance;
            audioManager?.EndExclusiveMusic(stopMusic: true);
            dreamTransitionMusicActive = false;
        }

        private void DisposeActiveDreamTransitionScreen()
        {
            DreamTransitionScreen transitionScreen = activeDreamTransitionScreen;
            activeDreamTransitionScreen = null;
            transitionScreen?.Dispose();
        }

        private IEnumerator WaitForDreamTransitionMusicThenNotify(DreamTransitionCue cue)
        {
            yield return null;

            AudioManager audioManager = AudioManager.ExistingInstance;
            while (audioManager != null &&
                   (audioManager.IsMusicPlaying || audioManager.IsGameplayAudioPaused))
                yield return null;

            dreamTransitionMusicWaitRoutine = null;
            if (dreamTransitionMusicActive)
            {
                dreamTransitionMusicActive = false;
                audioManager?.EndExclusiveMusic(stopMusic: false);
                if (cue != null)
                    DreamTransitionMusicFinished?.Invoke(cue);
            }
        }

        internal void CompleteBeat(NarrativeBeat beat, bool advanceBeatIndex)
        {
            if (beat == null || state == null)
                return;

            saveBatchDepth++;
            try
            {
                beat.ApplyCompletionEffects(state);
                if (advanceBeatIndex)
                    state.BeatIndex++;
            }
            finally
            {
                saveBatchDepth--;
                if (saveBatchDepth == 0 &&
                    saveQueued &&
                    string.IsNullOrWhiteSpace(CurrentEndingId))
                {
                    saveQueued = false;
                    PersistState();
                }
            }
        }

        private bool HasActiveDocumentInteractable(DocumentDefinition document) =>
            sceneServices.HasActiveDocument(document);

        private INarrativePresenter ResolvePresenter()
        {
            if (sceneServices.TryGetPresenter(
                    out INarrativePresenter activePresenter,
                    out _))
                return activePresenter;

            return loggingPresenter;
        }

        private bool TryPlacePlayerAt(string spawnPointId, out string error)
        {
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                error = "Narrative travel has no spawn-point id.";
                return false;
            }

            if (!sceneServices.TryGetSpawnPoint(
                    spawnPointId,
                    out SpawnPoint destination,
                    out int spawnMatches))
            {
                error = spawnMatches == 0
                    ? $"No {nameof(SpawnPoint)} with id '{spawnPointId}' exists in scene '{SceneManager.GetActiveScene().name}'."
                    : $"Spawn-point id '{spawnPointId}' is ambiguous in scene '{SceneManager.GetActiveScene().name}' ({spawnMatches} matches).";
                return false;
            }

            if (!sceneServices.TryGetPlayer(
                    out FirstPersonController player,
                    out int playerCount))
            {
                error = playerCount == 0
                    ? $"Cannot place player at spawn '{spawnPointId}' because no {nameof(FirstPersonController)} exists in scene '{SceneManager.GetActiveScene().name}'."
                    : $"Cannot place player at spawn '{spawnPointId}' because scene '{SceneManager.GetActiveScene().name}' contains {playerCount} first-person controllers.";
                return false;
            }

            CharacterController character = player.GetComponent<CharacterController>();
            if (character == null)
            {
                error = $"Cannot place player at spawn '{spawnPointId}' because the first-person controller has no {nameof(CharacterController)}.";
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Vector3 targetPosition = destination.transform.position;
            Quaternion targetRotation = destination.transform.rotation;
            float targetPitch = 0f;
            if (initialPlacementPending &&
                pendingLoadedPlayerLocation != null &&
                string.Equals(
                    pendingLoadedPlayerLocation.sceneName,
                    activeScene.name,
                    StringComparison.Ordinal))
            {
                targetPosition = pendingLoadedPlayerLocation.position;
                targetRotation = pendingLoadedPlayerLocation.bodyRotation;
                targetPitch = pendingLoadedPlayerLocation.viewPitch;
            }

            if (!player.TrySetPose(
                    targetPosition,
                    targetRotation,
                    targetPitch,
                    out string poseError))
            {
                error =
                    $"Cannot place player at spawn '{spawnPointId}' in scene " +
                    $"'{activeScene.name}': {poseError}";
                return false;
            }

            lastValidatedPlayerLocation = new SavedPlayerLocation
            {
                sceneName = activeScene.name,
                position = player.transform.position,
                bodyRotation = player.transform.rotation,
                viewPitch = player.ViewPitch
            };
            playerPlacementValidated = true;

            // A restored pose belongs only to the first successfully resolved
            // location. If the cursor is on a pending TravelBeat, its scene will
            // differ and we intentionally consume the stale source pose after
            // placing at the destination spawn.
            if (initialPlacementPending)
            {
                initialPlacementPending = false;
                pendingLoadedPlayerLocation = null;
            }

            error = string.Empty;
            return true;
        }

        private static void FailOperation(NarrativeOperationResult result, string message)
        {
            result.Fail(message, NarrativeFailureKind.Travel);
            Debug.LogError(message);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneServices.PruneToScene(scene);
        }

    }

    internal sealed class LoggingNarrativePresenter : INarrativePresenter
    {
        public IEnumerator PresentLine(ResolvedLine line, string speakerName, float visionIntensity)
        {
            Debug.Log($"[{line.LineId}] {speakerName}: {line.Text}");
            yield return null;
        }

        public IEnumerator PresentCard(string text)
        {
            Debug.Log(text);
            yield return null;
        }

        public IEnumerator PresentChoice(IReadOnlyList<ChoiceOption> options, Action<EndingDefinition> selected)
        {
            if (options == null || options.Count == 0)
            {
                Debug.LogError("Logging narrative presenter cannot resolve an empty choice.");
                yield break;
            }

            EndingDefinition ending = options[0].Ending;
            if (ending == null)
            {
                Debug.LogError("Logging narrative presenter cannot resolve the first choice option's ending.");
                yield break;
            }

            Debug.LogWarning(
                $"No scene narrative presenter was found. Automatically selecting first choice ending '{ending.Id}'.");
            selected?.Invoke(ending);
            yield return null;
        }

        public IEnumerator PresentDocument(IReadableDocument document, Action closed)
        {
            Debug.Log(document != null ? document.Title : "Document");
            closed?.Invoke();
            yield return null;
        }

        public void ShowStatus(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Debug.LogWarning(message);
        }

        public void ClearPresentation()
        {
        }
    }
}