using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    public enum SaveReadStatus
    {
        Missing,
        Valid,
        ResumedInMemory,
        Recovered,
        RecoverableBackup,
        Invalid,
        TransientFailure,
        CatalogUnavailable
    }

    public enum SaveWriteStatus
    {
        Succeeded,
        TransientFailure
    }

    public interface ISaveGameFileSystem
    {
        void CreateDirectory(string path);
        bool FileExists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Move(string sourcePath, string destinationPath);
        void Replace(string sourcePath, string destinationPath, string destinationBackupPath);
        void Delete(string path);
    }

    [Serializable]
    public sealed class SavedTaskCount
    {
        public string objectiveId;
        public int count;
    }

    /// <summary>
    /// Optional, scene-bound player placement captured at a save boundary. The
    /// narrative cursor remains the authority for which scene must load; this
    /// snapshot only replaces the authored spawn pose when that scene matches.
    /// </summary>
    [Serializable]
    public sealed class SavedPlayerLocation
    {
        public string sceneName;
        public Vector3 position;
        public Quaternion bodyRotation = Quaternion.identity;
        public float viewPitch;

        public SavedPlayerLocation Copy()
        {
            return new SavedPlayerLocation
            {
                sceneName = sceneName,
                position = position,
                bodyRotation = bodyRotation,
                viewPitch = viewPitch
            };
        }
    }

    [Serializable]
    public sealed class SaveGameData
    {
        // Keep the deserialization default invalid. JsonUtility preserves field
        // initializers for missing JSON fields, so initializing this to the
        // current version would accidentally accept pre-versioned saves.
        public int version;
        public string chosenName;
        public int chapterIndex;
        public int beatIndex;
        public List<string> flagIds = new List<string>();
        public int bloomCount;
        public int liesTold;
        public int patientsFed;
        public List<SavedTaskCount> taskCounts = new List<SavedTaskCount>();
        // Kept separate from the format version so pre-inventory v2 saves can
        // be recovered by replaying their completed inline inventory grants.
        public bool inventoryStateRecorded;
        public bool inventoryUnlocked;
        public List<string> inventoryItemIds = new List<string>();
        public string carriedItemId;
        public string carriedCategoryId;
        // Unfinished playthroughs leave this empty. A non-empty value is
        // catalog-validated during restoration.
        public string completedEndingId;
        // Additive v2 extension. Older v2 files omit both fields and therefore
        // continue at the authored spawn instead of being invalidated.
        public bool playerLocationRecorded;
        public SavedPlayerLocation playerLocation;
    }

    public static class SaveGameStore
    {
        public const int CurrentVersion = 2;
        public const int ManualSlotCount = 10;
        private const string FileName = "hortensia-save.json";
        private const string ManualSlotFileNameFormat = "hortensia-save-slot-{0:D2}.json";

        private static readonly ISaveGameFileSystem SystemFileSystem = new SystemSaveGameFileSystem();
        private static bool writeFailureLogged;

#if UNITY_EDITOR
        private static ISaveGameFileSystem testFileSystem;
        private static string testPersistentDataPath;
#endif

        public static string SavePath => AutosavePaths.PrimaryPath;
        public static string BackupPath => AutosavePaths.BackupPath;
        private static SavePaths AutosavePaths => PathsForFileName(FileName);

        public static string ManualSavePath(int slotNumber) => ManualPaths(slotNumber).PrimaryPath;
        public static string ManualBackupPath(int slotNumber) => ManualPaths(slotNumber).BackupPath;

        public static bool HasSave
        {
            get
            {
                SaveReadStatus status = GetReadStatus();
                return status == SaveReadStatus.Valid || status == SaveReadStatus.RecoverableBackup;
            }
        }

        private static ISaveGameFileSystem FileSystem
        {
            get
            {
#if UNITY_EDITOR
                return testFileSystem ?? SystemFileSystem;
#else
                return SystemFileSystem;
#endif
            }
        }

        private static string PersistentDataPath
        {
            get
            {
#if UNITY_EDITOR
                return testPersistentDataPath ?? Application.persistentDataPath;
#else
                return Application.persistentDataPath;
#endif
            }
        }

        public static SaveGameData Capture(
            NarrativeState state,
            string completedEndingId = null,
            SavedPlayerLocation playerLocation = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var data = new SaveGameData
            {
                version = CurrentVersion,
                chosenName = state.ChosenName.ToString(),
                chapterIndex = state.ChapterIndex,
                beatIndex = state.BeatIndex,
                bloomCount = state.Garden.BloomCount,
                liesTold = state.Garden.LiesTold,
                patientsFed = state.Garden.PatientsFed,
                inventoryStateRecorded = true,
                inventoryUnlocked = state.Inventory.IsUnlocked,
                carriedItemId = state.Inventory.CarriedItemId,
                carriedCategoryId = state.Inventory.CarriedCategoryId,
                completedEndingId = string.IsNullOrWhiteSpace(completedEndingId)
                    ? null
                    : completedEndingId.Trim()
            };

            if (TryNormalizePlayerLocation(playerLocation, out SavedPlayerLocation normalizedLocation))
            {
                data.playerLocationRecorded = true;
                data.playerLocation = normalizedLocation;
            }

            foreach (FlagId flag in state.Flags)
            {
                if (flag != null && !string.IsNullOrWhiteSpace(flag.Id))
                    data.flagIds.Add(flag.Id);
            }

            data.flagIds.Sort(StringComparer.Ordinal);

            foreach (KeyValuePair<string, int> taskCount in state.Tasks.Counts)
            {
                if (string.IsNullOrWhiteSpace(taskCount.Key) || taskCount.Value <= 0)
                    continue;

                data.taskCounts.Add(new SavedTaskCount
                {
                    objectiveId = taskCount.Key,
                    count = taskCount.Value
                });
            }

            data.taskCounts.Sort((left, right) =>
                string.Compare(left.objectiveId, right.objectiveId, StringComparison.Ordinal));

            foreach (string itemId in state.Inventory.ItemIds)
            {
                if (!string.IsNullOrWhiteSpace(itemId))
                    data.inventoryItemIds.Add(itemId);
            }

            data.inventoryItemIds.Sort(StringComparer.Ordinal);
            return data;
        }

        public static void Save(NarrativeState state)
        {
            TrySave(state);
        }

        public static SaveWriteStatus TrySave(
            NarrativeState state,
            string completedEndingId = null,
            SavedPlayerLocation playerLocation = null)
        {
            string json = JsonUtility.ToJson(
                Capture(state, completedEndingId, playerLocation),
                true);
            return TryWriteRaw(json, AutosavePaths, SaveWriteMode.Normal);
        }

        public static SaveWriteStatus TrySaveManualSlot(
            int slotNumber,
            NarrativeState state,
            string completedEndingId = null,
            SavedPlayerLocation playerLocation = null)
        {
            string json = JsonUtility.ToJson(
                Capture(state, completedEndingId, playerLocation),
                true);
            return TryWriteRaw(json, ManualPaths(slotNumber), SaveWriteMode.Normal);
        }

        public static bool TryLoad(NarrativeCatalog catalog, out NarrativeState state)
        {
            return TryLoad(catalog, out state, out _);
        }

        // This probe is strictly read-only. It checks the backup only after a
        // confirmed missing or invalid primary, and never masks a transiently
        // inaccessible primary with an older slot.
        public static SaveReadStatus GetReadStatus()
        {
            return GetReadStatus(AutosavePaths);
        }

        public static SaveReadStatus GetManualSlotReadStatus(int slotNumber)
        {
            return GetReadStatus(ManualPaths(slotNumber));
        }

        /// <summary>
        /// Restores the newest readable autosave state without promoting a backup,
        /// discarding invalid data, or writing to disk. Menu presentation can use
        /// this preview without changing the recovery semantics of Continue.
        /// </summary>
        public static SaveReadStatus GetReadOnlyLoadStatus(
            NarrativeCatalog catalog,
            out NarrativeState state)
        {
            return GetReadOnlyLoadStatus(catalog, AutosavePaths, out state);
        }

        public static SaveReadStatus GetManualSlotReadOnlyLoadStatus(
            int slotNumber,
            NarrativeCatalog catalog,
            out NarrativeState state)
        {
            return GetReadOnlyLoadStatus(catalog, ManualPaths(slotNumber), out state);
        }

        private static SaveReadStatus GetReadOnlyLoadStatus(
            NarrativeCatalog catalog,
            SavePaths paths,
            out NarrativeState state)
        {
            state = null;
            if (catalog == null)
                return SaveReadStatus.CatalogUnavailable;

            RawReadResult primary = ReadRaw(paths.PrimaryPath);
            if (primary.Status == SaveReadStatus.TransientFailure)
                return SaveReadStatus.TransientFailure;

            if (primary.Status == SaveReadStatus.Valid &&
                TryRestore(catalog, primary.Data, out state, out _, out _))
            {
                return SaveReadStatus.Valid;
            }

            RawReadResult backup = ReadRaw(paths.BackupPath);
            if (backup.Status == SaveReadStatus.TransientFailure)
            {
                state = null;
                return SaveReadStatus.TransientFailure;
            }

            if (backup.Status == SaveReadStatus.Valid &&
                TryRestore(catalog, backup.Data, out state, out _, out _))
            {
                return SaveReadStatus.RecoverableBackup;
            }

            state = null;
            return primary.Status == SaveReadStatus.Missing &&
                   backup.Status == SaveReadStatus.Missing
                ? SaveReadStatus.Missing
                : SaveReadStatus.Invalid;
        }

        private static SaveReadStatus GetReadStatus(SavePaths paths)
        {
            RawReadResult primary = ReadRaw(paths.PrimaryPath);
            if (primary.Status == SaveReadStatus.Valid ||
                primary.Status == SaveReadStatus.TransientFailure)
            {
                return primary.Status;
            }

            RawReadResult backup = ReadRaw(paths.BackupPath);
            if (backup.Status == SaveReadStatus.Valid)
                return SaveReadStatus.RecoverableBackup;
            if (backup.Status == SaveReadStatus.TransientFailure)
                return SaveReadStatus.TransientFailure;

            return primary.Status == SaveReadStatus.Missing &&
                   backup.Status == SaveReadStatus.Missing
                ? SaveReadStatus.Missing
                : SaveReadStatus.Invalid;
        }

        public static SaveReadStatus GetLoadStatus(
            NarrativeCatalog catalog,
            out NarrativeState state)
        {
            return GetLoadStatus(catalog, out state, out _);
        }

        // A catalog-valid backup is usable even if promotion cannot currently
        // write. Callers receive the restored state and Recovered status, while
        // promotionStatus tells them whether the slot still needs to be saved.
        public static SaveReadStatus GetLoadStatus(
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus)
        {
            return GetLoadStatus(
                catalog,
                out state,
                out promotionStatus,
                out _);
        }

        public static SaveReadStatus GetLoadStatus(
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus,
            out string completedEndingId)
        {
            return GetLoadStatus(
                catalog,
                out state,
                out promotionStatus,
                out completedEndingId,
                out _);
        }

        public static SaveReadStatus GetLoadStatus(
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus,
            out string completedEndingId,
            out SavedPlayerLocation playerLocation)
        {
            return GetLoadStatus(
                catalog,
                AutosavePaths,
                out state,
                out promotionStatus,
                out completedEndingId,
                out playerLocation);
        }

        public static SaveReadStatus GetManualSlotLoadStatus(
            int slotNumber,
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus)
        {
            return GetManualSlotLoadStatus(
                slotNumber,
                catalog,
                out state,
                out promotionStatus,
                out _);
        }

        public static SaveReadStatus GetManualSlotLoadStatus(
            int slotNumber,
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus,
            out string completedEndingId)
        {
            return GetManualSlotLoadStatus(
                slotNumber,
                catalog,
                out state,
                out promotionStatus,
                out completedEndingId,
                out _);
        }

        public static SaveReadStatus GetManualSlotLoadStatus(
            int slotNumber,
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus,
            out string completedEndingId,
            out SavedPlayerLocation playerLocation)
        {
            return GetLoadStatus(
                catalog,
                ManualPaths(slotNumber),
                out state,
                out promotionStatus,
                out completedEndingId,
                out playerLocation);
        }

        private static SaveReadStatus GetLoadStatus(
            NarrativeCatalog catalog,
            SavePaths paths,
            out NarrativeState state,
            out SaveWriteStatus promotionStatus,
            out string completedEndingId,
            out SavedPlayerLocation playerLocation)
        {
            state = null;
            promotionStatus = SaveWriteStatus.Succeeded;
            completedEndingId = null;
            playerLocation = null;
            if (catalog == null)
                return SaveReadStatus.CatalogUnavailable;

            RawReadResult primary = ReadRaw(paths.PrimaryPath);
            if (primary.Status == SaveReadStatus.TransientFailure)
                return SaveReadStatus.TransientFailure;

            if (primary.Status == SaveReadStatus.Valid &&
                TryRestore(
                    catalog,
                    primary.Data,
                    out state,
                    out completedEndingId,
                    out playerLocation))
            {
                return SaveReadStatus.Valid;
            }

            // A raw-valid primary can still be invalid for this catalog. That
            // is a confirmed-invalid candidate, so the previous slot may be
            // considered just like it is for malformed or missing JSON.
            RawReadResult backup = ReadRaw(paths.BackupPath);
            if (backup.Status == SaveReadStatus.TransientFailure)
            {
                state = null;
                completedEndingId = null;
                playerLocation = null;
                return SaveReadStatus.TransientFailure;
            }

            if (backup.Status == SaveReadStatus.Valid &&
                TryRestore(
                    catalog,
                    backup.Data,
                    out NarrativeState recoveredState,
                    out string recoveredEndingId,
                    out SavedPlayerLocation recoveredPlayerLocation))
            {
                SaveWriteMode promotionMode = primary.Status == SaveReadStatus.Missing
                    ? SaveWriteMode.PromoteToMissingPrimary
                    : SaveWriteMode.PromoteOverExistingPrimary;
                promotionStatus = TryWriteRaw(backup.RawJson, paths, promotionMode);
                state = recoveredState;
                completedEndingId = recoveredEndingId;
                playerLocation = recoveredPlayerLocation;
                return SaveReadStatus.Recovered;
            }

            state = null;
            completedEndingId = null;
            playerLocation = null;
            return primary.Status == SaveReadStatus.Missing &&
                   backup.Status == SaveReadStatus.Missing
                ? SaveReadStatus.Missing
                : SaveReadStatus.Invalid;
        }

        public static bool TryLoad(
            NarrativeCatalog catalog,
            out NarrativeState state,
            out SaveReadStatus readStatus)
        {
            readStatus = GetLoadStatus(catalog, out state, out _);
            return readStatus == SaveReadStatus.Valid || readStatus == SaveReadStatus.Recovered;
        }

        internal static bool TryRestoreSnapshot(
            NarrativeCatalog catalog,
            SaveGameData data,
            out NarrativeState state,
            out string completedEndingId,
            out SavedPlayerLocation playerLocation)
        {
            if (catalog == null || data == null || data.version != CurrentVersion)
            {
                state = null;
                completedEndingId = null;
                playerLocation = null;
                return false;
            }

            return TryRestore(
                catalog,
                data,
                out state,
                out completedEndingId,
                out playerLocation);
        }

        public static bool TryDeserialize(string json, out SaveGameData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                data = JsonUtility.FromJson<SaveGameData>(json);
                return data != null && data.version == CurrentVersion;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static void Discard()
        {
            TryDiscard();
        }

        public static SaveWriteStatus TryDiscard()
        {
            SavePaths paths = AutosavePaths;
            Exception firstFailure = null;
            TryDeleteForDiscard(paths.PrimaryPath, ref firstFailure);
            TryDeleteForDiscard(paths.BackupPath, ref firstFailure);
            TryDeleteForDiscard(paths.TemporaryPath, ref firstFailure);

            if (firstFailure != null)
            {
                LogWriteFailureOnce(SaveWriteMode.Discard, firstFailure);
                return SaveWriteStatus.TransientFailure;
            }

            writeFailureLogged = false;
            return SaveWriteStatus.Succeeded;
        }

        /// <summary>
        /// Deletes one manual slot's file (and its backup/temp file), leaving
        /// every other slot and the autosave untouched.
        /// </summary>
        public static SaveWriteStatus TryDiscardManualSlot(int slotNumber)
        {
            SavePaths paths = ManualPaths(slotNumber);
            Exception firstFailure = null;
            TryDeleteForDiscard(paths.PrimaryPath, ref firstFailure);
            TryDeleteForDiscard(paths.BackupPath, ref firstFailure);
            TryDeleteForDiscard(paths.TemporaryPath, ref firstFailure);

            if (firstFailure != null)
            {
                LogWriteFailureOnce(SaveWriteMode.Discard, firstFailure);
                return SaveWriteStatus.TransientFailure;
            }

            writeFailureLogged = false;
            return SaveWriteStatus.Succeeded;
        }

        /// <summary>
        /// Deletes every manual slot (1 through ManualSlotCount) - the
        /// autosave is untouched. Attempts every slot even if an earlier one
        /// fails, so a single locked file doesn't block the rest.
        /// </summary>
        public static SaveWriteStatus TryDiscardAllManualSlots()
        {
            bool anyFailed = false;
            for (int slotNumber = 1; slotNumber <= ManualSlotCount; slotNumber++)
            {
                if (TryDiscardManualSlot(slotNumber) != SaveWriteStatus.Succeeded)
                    anyFailed = true;
            }

            return anyFailed ? SaveWriteStatus.TransientFailure : SaveWriteStatus.Succeeded;
        }

        /// <summary>
        /// Deletes absolutely everything: the autosave and every manual
        /// slot. Used for a full "start fresh" reset. Attempts every file
        /// even if an earlier one fails.
        /// </summary>
        public static SaveWriteStatus TryDiscardEverything()
        {
            bool autosaveFailed = TryDiscard() != SaveWriteStatus.Succeeded;
            bool manualFailed = TryDiscardAllManualSlots() != SaveWriteStatus.Succeeded;
            return (autosaveFailed || manualFailed) ? SaveWriteStatus.TransientFailure : SaveWriteStatus.Succeeded;
        }

#if UNITY_EDITOR
        // Tests get an isolated, scoped override so they never touch the
        // editor's real Application.persistentDataPath slot.
        public static IDisposable OverrideFileSystemForTests(
            ISaveGameFileSystem fileSystem,
            string persistentDataPath)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("A test persistent-data path is required.", nameof(persistentDataPath));

            return new FileSystemOverrideScope(fileSystem, persistentDataPath);
        }
#endif

        private static bool TryRestore(
            NarrativeCatalog catalog,
            SaveGameData data,
            out NarrativeState state,
            out string completedEndingId,
            out SavedPlayerLocation playerLocation)
        {
            state = null;
            completedEndingId = null;
            playerLocation = null;
            ChapterDefinition chapter = catalog.ChapterAt(data.chapterIndex);
            if (!Enum.TryParse(data.chosenName, out NameVariant chosenName) ||
                !Enum.IsDefined(typeof(NameVariant), chosenName) ||
                chapter == null ||
                data.beatIndex < 0 ||
                data.beatIndex > chapter.Beats.Count)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(data.completedEndingId))
            {
                completedEndingId = data.completedEndingId.Trim();
                if (catalog.EndingWithId(completedEndingId) == null)
                {
                    completedEndingId = null;
                    return false;
                }
            }

            var restoredFlags = new List<FlagId>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.flagIds != null)
            {
                for (int i = 0; i < data.flagIds.Count; i++)
                {
                    string id = data.flagIds[i];
                    FlagId flag = catalog.FlagWithId(id);
                    if (flag == null || !seenIds.Add(id))
                        return false;

                    restoredFlags.Add(flag);
                }
            }

            state = new NarrativeState(chosenName, data.bloomCount)
            {
                ChapterIndex = data.chapterIndex,
                BeatIndex = data.beatIndex
            };
            state.Garden.Restore(data.bloomCount, data.liesTold, data.patientsFed);
            state.RestoreFlags(restoredFlags);
            state.RestoreInventory(
                data.inventoryUnlocked,
                data.inventoryItemIds,
                data.carriedItemId,
                data.carriedCategoryId,
                data.inventoryStateRecorded);

            var seenTaskIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.taskCounts != null)
            {
                for (int i = 0; i < data.taskCounts.Count; i++)
                {
                    SavedTaskCount taskCount = data.taskCounts[i];
                    if (taskCount == null ||
                        string.IsNullOrWhiteSpace(taskCount.objectiveId) ||
                        taskCount.count < 0 ||
                        !seenTaskIds.Add(taskCount.objectiveId) ||
                        catalog.TaskObjectiveWithId(taskCount.objectiveId) == null)
                    {
                        state = null;
                        return false;
                    }

                    state.Tasks.RestoreCount(taskCount.objectiveId, taskCount.count);
                }
            }

            if (data.playerLocationRecorded)
            {
                TryNormalizePlayerLocation(
                    data.playerLocation,
                    out playerLocation);
            }

            return true;
        }

        private static RawReadResult ReadRaw(string path)
        {
            try
            {
                string json = FileSystem.ReadAllText(path);
                return TryDeserialize(json, out SaveGameData data)
                    ? RawReadResult.Valid(data, json)
                    : RawReadResult.Invalid;
            }
            catch (FileNotFoundException)
            {
                return RawReadResult.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return RawReadResult.Missing;
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                return RawReadResult.TransientFailure;
            }
        }

        private static SaveWriteStatus TryWriteRaw(string json, SavePaths paths, SaveWriteMode mode)
        {
            try
            {
                FileSystem.CreateDirectory(PersistentDataPath);
                FileSystem.WriteAllText(paths.TemporaryPath, json);

                switch (mode)
                {
                    case SaveWriteMode.Normal:
                        if (FileSystem.FileExists(paths.PrimaryPath))
                            FileSystem.Replace(paths.TemporaryPath, paths.PrimaryPath, paths.BackupPath);
                        else
                            FileSystem.Move(paths.TemporaryPath, paths.PrimaryPath);
                        break;
                    case SaveWriteMode.PromoteToMissingPrimary:
                        FileSystem.Move(paths.TemporaryPath, paths.PrimaryPath);
                        break;
                    case SaveWriteMode.PromoteOverExistingPrimary:
                        // Preserve the known-good backup. Using BackupPath here
                        // would replace it with the corrupt primary.
                        FileSystem.Replace(paths.TemporaryPath, paths.PrimaryPath, null);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }

                writeFailureLogged = false;
                return SaveWriteStatus.Succeeded;
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                LogWriteFailureOnce(mode, exception);
                return SaveWriteStatus.TransientFailure;
            }
            finally
            {
                TryDeleteTemporaryFile(paths.TemporaryPath);
            }
        }

        private static void LogWriteFailureOnce(SaveWriteMode mode, Exception exception)
        {
            if (writeFailureLogged)
                return;

            writeFailureLogged = true;
            string recovery = mode == SaveWriteMode.Discard
                ? "The slot remains available where possible; storage will be retried at the next checkpoint."
                : "Progress remains in memory and will be retried.";
            Debug.LogError(
                $"Save write failed during {mode}. {recovery}\n{exception}");
        }

        private static bool IsExpectedFileSystemFailure(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is PlatformNotSupportedException;
        }

        private static bool TryNormalizePlayerLocation(
            SavedPlayerLocation location,
            out SavedPlayerLocation normalized)
        {
            normalized = null;
            float rotationMagnitudeSquared = location != null
                ? Quaternion.Dot(location.bodyRotation, location.bodyRotation)
                : 0f;
            if (location == null ||
                string.IsNullOrWhiteSpace(location.sceneName) ||
                !IsFinite(location.position.x) ||
                !IsFinite(location.position.y) ||
                !IsFinite(location.position.z) ||
                !IsFinite(location.bodyRotation.x) ||
                !IsFinite(location.bodyRotation.y) ||
                !IsFinite(location.bodyRotation.z) ||
                !IsFinite(location.bodyRotation.w) ||
                !IsFinite(location.viewPitch) ||
                Mathf.Abs(location.viewPitch) > 90f ||
                !IsFinite(rotationMagnitudeSquared) ||
                rotationMagnitudeSquared < 0.0001f)
            {
                return false;
            }

            normalized = new SavedPlayerLocation
            {
                sceneName = location.sceneName.Trim(),
                position = location.position,
                bodyRotation = location.bodyRotation.normalized,
                viewPitch = location.viewPitch
            };
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void TryDeleteForDiscard(string path, ref Exception firstFailure)
        {
            try
            {
                DeleteIfPresent(path);
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                if (firstFailure == null)
                    firstFailure = exception;
            }
        }

        private static void DeleteIfPresent(string path)
        {
            if (FileSystem.FileExists(path))
                FileSystem.Delete(path);
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                DeleteIfPresent(temporaryPath);
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
            }
        }

        private enum SaveWriteMode
        {
            Normal,
            PromoteToMissingPrimary,
            PromoteOverExistingPrimary,
            Discard
        }

        private static SavePaths ManualPaths(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > ManualSlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slotNumber),
                    slotNumber,
                    $"Manual save slots must be between 1 and {ManualSlotCount}.");
            }

            return PathsForFileName(string.Format(ManualSlotFileNameFormat, slotNumber));
        }

        private static SavePaths PathsForFileName(string fileName)
        {
            string primaryPath = Path.Combine(PersistentDataPath, fileName);
            return new SavePaths(primaryPath, primaryPath + ".bak", primaryPath + ".tmp");
        }

        private readonly struct SavePaths
        {
            public SavePaths(string primaryPath, string backupPath, string temporaryPath)
            {
                PrimaryPath = primaryPath;
                BackupPath = backupPath;
                TemporaryPath = temporaryPath;
            }

            public string PrimaryPath { get; }
            public string BackupPath { get; }
            public string TemporaryPath { get; }
        }

        private readonly struct RawReadResult
        {
            private RawReadResult(SaveReadStatus status, SaveGameData data, string rawJson)
            {
                Status = status;
                Data = data;
                RawJson = rawJson;
            }

            public SaveReadStatus Status { get; }
            public SaveGameData Data { get; }
            public string RawJson { get; }

            public static RawReadResult Missing => new RawReadResult(SaveReadStatus.Missing, null, null);
            public static RawReadResult Invalid => new RawReadResult(SaveReadStatus.Invalid, null, null);
            public static RawReadResult TransientFailure =>
                new RawReadResult(SaveReadStatus.TransientFailure, null, null);

            public static RawReadResult Valid(SaveGameData data, string rawJson)
            {
                return new RawReadResult(SaveReadStatus.Valid, data, rawJson);
            }
        }

        private sealed class SystemSaveGameFileSystem : ISaveGameFileSystem
        {
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public bool FileExists(string path) => File.Exists(path);
            public string ReadAllText(string path) => File.ReadAllText(path);
            public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
            public void Move(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

            public void Replace(
                string sourcePath,
                string destinationPath,
                string destinationBackupPath)
            {
                File.Replace(sourcePath, destinationPath, destinationBackupPath);
            }

            public void Delete(string path) => File.Delete(path);
        }

#if UNITY_EDITOR
        private sealed class FileSystemOverrideScope : IDisposable
        {
            private readonly ISaveGameFileSystem previousFileSystem;
            private readonly string previousPersistentDataPath;
            private readonly bool previousWriteFailureLogged;
            private bool disposed;

            public FileSystemOverrideScope(
                ISaveGameFileSystem fileSystem,
                string persistentDataPath)
            {
                previousFileSystem = testFileSystem;
                previousPersistentDataPath = testPersistentDataPath;
                previousWriteFailureLogged = writeFailureLogged;
                testFileSystem = fileSystem;
                testPersistentDataPath = persistentDataPath;
                writeFailureLogged = false;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                testFileSystem = previousFileSystem;
                testPersistentDataPath = previousPersistentDataPath;
                writeFailureLogged = previousWriteFailureLogged;
            }
        }
#endif
    }
}