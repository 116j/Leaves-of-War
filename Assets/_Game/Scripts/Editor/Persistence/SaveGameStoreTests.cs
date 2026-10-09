using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class SaveGameStoreTests
    {
        private const string TestPersistentDataPath = "virtual-persistent-data";

        private FakeSaveGameFileSystem fileSystem;
        private IDisposable fileSystemOverride;
        private string savePath;
        private string backupPath;
        private string temporaryPath;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new FakeSaveGameFileSystem();
            fileSystemOverride = SaveGameStore.OverrideFileSystemForTests(
                fileSystem,
                TestPersistentDataPath);
            savePath = SaveGameStore.SavePath;
            backupPath = SaveGameStore.BackupPath;
            temporaryPath = savePath + ".tmp";
        }

        [TearDown]
        public void TearDown()
        {
            fileSystemOverride.Dispose();
        }

        [Test]
        public void TryDeserialize_ParsesCurrentVersionedSave()
        {
            string json =
                "{\"version\":" + SaveGameStore.CurrentVersion +
                ",\"chosenName\":\"Laura\",\"chapterIndex\":3,\"beatIndex\":4" +
                ",\"flagIds\":[\"opened_letter\",\"met_willowet\"]" +
                ",\"bloomCount\":5,\"liesTold\":2,\"patientsFed\":1}";

            bool parsed = SaveGameStore.TryDeserialize(json, out SaveGameData data);

            Assert.That(parsed, Is.True);
            Assert.That(data, Is.Not.Null);
            Assert.That(data.version, Is.EqualTo(SaveGameStore.CurrentVersion));
            Assert.That(data.chosenName, Is.EqualTo("Laura"));
            Assert.That(data.chapterIndex, Is.EqualTo(3));
            Assert.That(data.beatIndex, Is.EqualTo(4));
            Assert.That(data.flagIds, Is.EquivalentTo(new[] { "opened_letter", "met_willowet" }));
            Assert.That(data.bloomCount, Is.EqualTo(5));
            Assert.That(data.liesTold, Is.EqualTo(2));
            Assert.That(data.patientsFed, Is.EqualTo(1));
        }

        [Test]
        public void TryDeserialize_RejectsNullAndWhitespaceJson()
        {
            Assert.That(SaveGameStore.TryDeserialize(null, out _), Is.False);
            Assert.That(SaveGameStore.TryDeserialize(string.Empty, out _), Is.False);
            Assert.That(SaveGameStore.TryDeserialize("   ", out _), Is.False);
        }

        [TestCase("this is not JSON")]
        [TestCase("{\"version\":")]
        public void TryDeserialize_RejectsMalformedJson(string json)
        {
            bool parsed = SaveGameStore.TryDeserialize(json, out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryDeserialize_RejectsSaveWithoutFormatVersion()
        {
            const string json = "{\"chosenName\":\"Everie\",\"chapterIndex\":1,\"beatIndex\":0}";

            bool parsed = SaveGameStore.TryDeserialize(json, out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryDeserialize_RejectsMismatchedFormatVersion()
        {
            string json = "{\"version\":" + (SaveGameStore.CurrentVersion + 1) + "}";

            bool parsed = SaveGameStore.TryDeserialize(json, out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void GetReadStatus_ValidPrimaryWinsWithoutReadingBackup()
        {
            fileSystem.Files[savePath] = ValidJson(bloomCount: 3);
            fileSystem.Files[backupPath] = ValidJson(bloomCount: 9);

            SaveReadStatus status = SaveGameStore.GetReadStatus();

            Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
            Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { savePath }));
            Assert.That(fileSystem.MutationCount, Is.Zero);
        }

        [Test]
        public void GetReadStatus_TransientPrimaryDoesNotReadOrPromoteBackup()
        {
            fileSystem.ReadFailures[savePath] = new IOException("primary is locked");
            string backupJson = ValidJson(bloomCount: 9);
            fileSystem.Files[backupPath] = backupJson;

            SaveReadStatus status = SaveGameStore.GetReadStatus();

            Assert.That(status, Is.EqualTo(SaveReadStatus.TransientFailure));
            Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { savePath }));
            Assert.That(fileSystem.Files[backupPath], Is.EqualTo(backupJson));
            Assert.That(fileSystem.MutationCount, Is.Zero);
        }

        [TestCase(RawSlotState.Missing)]
        [TestCase(RawSlotState.Invalid)]
        public void GetReadStatus_ValidBackupIsRecoverableAndProbeRemainsReadOnly(
            RawSlotState primaryState)
        {
            ConfigureRawSlot(savePath, primaryState);
            string originalPrimary = fileSystem.Files.TryGetValue(savePath, out string primary)
                ? primary
                : null;
            string backupJson = ValidJson(bloomCount: 8);
            fileSystem.Files[backupPath] = backupJson;

            SaveReadStatus status = SaveGameStore.GetReadStatus();

            Assert.That(status, Is.EqualTo(SaveReadStatus.RecoverableBackup));
            Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { savePath, backupPath }));
            Assert.That(fileSystem.MutationCount, Is.Zero);
            Assert.That(fileSystem.Files.TryGetValue(savePath, out string finalPrimary),
                Is.EqualTo(primaryState != RawSlotState.Missing));
            Assert.That(finalPrimary, Is.EqualTo(originalPrimary));
            Assert.That(fileSystem.Files[backupPath], Is.EqualTo(backupJson));
        }

        [Test]
        public void HasSave_IncludesRawValidBackup()
        {
            fileSystem.Files[backupPath] = ValidJson();

            Assert.That(SaveGameStore.HasSave, Is.True);
            Assert.That(fileSystem.MutationCount, Is.Zero);
        }

        [Test]
        public void GetReadOnlyLoadStatus_RestoresBackupWithoutPromotingIt()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[savePath] = "not valid json";
                string backupJson = ValidJson(bloomCount: 8);
                fileSystem.Files[backupPath] = backupJson;

                SaveReadStatus status = SaveGameStore.GetReadOnlyLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state);

                Assert.That(status, Is.EqualTo(SaveReadStatus.RecoverableBackup));
                Assert.That(state, Is.Not.Null);
                Assert.That(state.Garden.BloomCount, Is.EqualTo(8));
                Assert.That(fileSystem.Files[savePath], Is.EqualTo("not valid json"));
                Assert.That(fileSystem.Files[backupPath], Is.EqualTo(backupJson));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [Test]
        public void ManualSlots_AreSeparateFromAutosaveAndSupportAllTenSlots()
        {
            fileSystem.Files[savePath] = ValidJson(bloomCount: 3);

            for (int slotNumber = 1; slotNumber <= SaveGameStore.ManualSlotCount; slotNumber++)
            {
                string manualPath = SaveGameStore.ManualSavePath(slotNumber);
                fileSystem.Files[manualPath] = ValidJson(bloomCount: slotNumber);

                Assert.That(
                    SaveGameStore.GetManualSlotReadStatus(slotNumber),
                    Is.EqualTo(SaveReadStatus.Valid));
            }

            Assert.That(SaveGameStore.GetReadStatus(), Is.EqualTo(SaveReadStatus.Valid));
            Assert.That(SaveGameStore.ManualSavePath(1), Does.EndWith("hortensia-save-slot-01.json"));
            Assert.That(SaveGameStore.ManualSavePath(10), Does.EndWith("hortensia-save-slot-10.json"));
        }

        [Test]
        public void TrySaveManualSlot_WritesOnlyTheSelectedManualSlot()
        {
            var state = new NarrativeState(NameVariant.Everie, 6)
            {
                ChapterIndex = 3,
                BeatIndex = 2
            };

            SaveWriteStatus status = SaveGameStore.TrySaveManualSlot(7, state);

            string manualPath = SaveGameStore.ManualSavePath(7);
            Assert.That(status, Is.EqualTo(SaveWriteStatus.Succeeded));
            Assert.That(fileSystem.Files.ContainsKey(manualPath), Is.True);
            Assert.That(fileSystem.Files.ContainsKey(savePath), Is.False);
            Assert.That(SaveGameStore.TryDeserialize(fileSystem.Files[manualPath], out SaveGameData data), Is.True);
            Assert.That(data.chosenName, Is.EqualTo(NameVariant.Everie.ToString()));
            Assert.That(data.chapterIndex, Is.EqualTo(3));
            Assert.That(data.beatIndex, Is.EqualTo(2));
        }

        [Test]
        public void TrySaveAndLoad_RoundTripsOptionalPlayerLocationWithoutChangingVersion()
        {
            using (var catalog = new CatalogFixture())
            {
                var original = new NarrativeState(NameVariant.Laura, 3)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };
                var playerLocation = new SavedPlayerLocation
                {
                    sceneName = "  Manor  ",
                    position = new Vector3(-12.5f, 1.25f, 34.75f),
                    bodyRotation = Quaternion.Euler(0f, 123f, 0f),
                    viewPitch = -17.5f
                };

                Assert.That(
                    SaveGameStore.TrySave(original, playerLocation: playerLocation),
                    Is.EqualTo(SaveWriteStatus.Succeeded));

                Assert.That(
                    SaveGameStore.TryDeserialize(fileSystem.Files[savePath], out SaveGameData savedData),
                    Is.True);
                Assert.That(savedData.version, Is.EqualTo(SaveGameStore.CurrentVersion));
                Assert.That(savedData.playerLocationRecorded, Is.True);

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out SaveWriteStatus promotionStatus,
                    out string completedEndingId,
                    out SavedPlayerLocation restoredPlayerLocation);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(restored, Is.Not.Null);
                Assert.That(completedEndingId, Is.Null);
                Assert.That(restoredPlayerLocation, Is.Not.Null);
                Assert.That(restoredPlayerLocation.sceneName, Is.EqualTo("Manor"));
                Assert.That(restoredPlayerLocation.position, Is.EqualTo(playerLocation.position));
                Assert.That(
                    Quaternion.Angle(restoredPlayerLocation.bodyRotation, playerLocation.bodyRotation),
                    Is.LessThan(0.001f));
                Assert.That(restoredPlayerLocation.viewPitch, Is.EqualTo(playerLocation.viewPitch));
            }
        }

        [Test]
        public void GetLoadStatus_LegacyVersionTwoSaveWithoutPlayerLocationRemainsValid()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[savePath] = ValidJson(bloomCount: 7);

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out SaveWriteStatus promotionStatus,
                    out string completedEndingId,
                    out SavedPlayerLocation restoredPlayerLocation);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Garden.BloomCount, Is.EqualTo(7));
                Assert.That(completedEndingId, Is.Null);
                Assert.That(restoredPlayerLocation, Is.Null);
            }
        }

        [Test]
        public void TrySave_NonFiniteOptionalPlayerLocationIsIgnoredWithoutInvalidatingNarrativeSave()
        {
            using (var catalog = new CatalogFixture())
            {
                var original = new NarrativeState(NameVariant.Everie, 5)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };
                var invalidPlayerLocation = new SavedPlayerLocation
                {
                    sceneName = "Manor",
                    position = new Vector3(float.NaN, 1f, 2f),
                    bodyRotation = Quaternion.identity,
                    viewPitch = 10f
                };

                Assert.That(
                    SaveGameStore.TrySave(original, playerLocation: invalidPlayerLocation),
                    Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(
                    SaveGameStore.TryDeserialize(fileSystem.Files[savePath], out SaveGameData savedData),
                    Is.True);
                Assert.That(savedData.playerLocationRecorded, Is.False);

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out SaveWriteStatus promotionStatus,
                    out string completedEndingId,
                    out SavedPlayerLocation restoredPlayerLocation);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Garden.BloomCount, Is.EqualTo(5));
                Assert.That(completedEndingId, Is.Null);
                Assert.That(restoredPlayerLocation, Is.Null);

                savedData.playerLocationRecorded = true;
                savedData.playerLocation = new SavedPlayerLocation
                {
                    sceneName = "Manor",
                    position = new Vector3(1f, 2f, 3f),
                    bodyRotation = Quaternion.identity,
                    viewPitch = 100f
                };
                fileSystem.Files[savePath] = JsonUtility.ToJson(savedData, true);

                status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out restored,
                    out promotionStatus,
                    out completedEndingId,
                    out restoredPlayerLocation);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Garden.BloomCount, Is.EqualTo(5));
                Assert.That(restoredPlayerLocation, Is.Null);
            }
        }

        [Test]
        public void TrySaveAndLoad_PreservesInventoryAndCarriedItemState()
        {
            using (var catalog = new CatalogFixture())
            {
                var original = new NarrativeState(NameVariant.Laura, 3)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };
                original.Inventory.Unlock();
                original.Inventory.AddItem("LetterOsmund");
                original.Inventory.SetCarriedItem("Manor|0:tools/2:broom", "garden_broom");

                Assert.That(SaveGameStore.TrySave(original), Is.EqualTo(SaveWriteStatus.Succeeded));
                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out _);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(restored.Inventory.IsUnlocked, Is.True);
                Assert.That(restored.Inventory.HasItem("LetterOsmund"), Is.True);
                Assert.That(restored.Inventory.CarriedItemId, Is.EqualTo("Manor|0:tools/2:broom"));
                Assert.That(restored.Inventory.CarriedCategoryId, Is.EqualTo("garden_broom"));
                Assert.That(restored.HasSavedInventoryState, Is.True);
            }
        }

        [Test]
        public void TrySaveAndLoad_PreservesCataloguedCompletedEnding()
        {
            using (var catalog = new CatalogFixture())
            {
                var original = new NarrativeState(NameVariant.Everie, 3)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };

                Assert.That(
                    SaveGameStore.TrySave(original, "sacrifice"),
                    Is.EqualTo(SaveWriteStatus.Succeeded));
                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out _,
                    out string completedEndingId);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(restored, Is.Not.Null);
                Assert.That(completedEndingId, Is.EqualTo("sacrifice"));
            }
        }

        [Test]
        public void Load_RejectsUnknownCompletedEndingId()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[savePath] = ValidJson(completedEndingId: "unknown_ending");

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out _,
                    out string completedEndingId);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Invalid));
                Assert.That(restored, Is.Null);
                Assert.That(completedEndingId, Is.Null);
            }
        }

        [Test]
        public void ManualSlotLoad_RecoversItsOwnBackupWithoutTouchingAutosave()
        {
            using (var catalog = new CatalogFixture())
            {
                string manualPath = SaveGameStore.ManualSavePath(4);
                string manualBackupPath = SaveGameStore.ManualBackupPath(4);
                fileSystem.Files[savePath] = ValidJson(bloomCount: 2);
                fileSystem.Files[manualPath] = "not json";
                fileSystem.Files[manualBackupPath] = ValidJson(bloomCount: 8);

                SaveReadStatus status = SaveGameStore.GetManualSlotLoadStatus(
                    4,
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Recovered));
                Assert.That(state.Garden.BloomCount, Is.EqualTo(8));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.Files[manualPath], Is.EqualTo(fileSystem.Files[manualBackupPath]));
                Assert.That(fileSystem.Files[savePath], Is.EqualTo(ValidJson(bloomCount: 2)));
            }
        }

        [Test]
        public void ManualSlotReadOnlyLoad_CatalogInvalidPrimaryUsesValidBackupWithoutPromotion()
        {
            using (var catalog = new CatalogFixture())
            {
                string manualPath = SaveGameStore.ManualSavePath(6);
                string manualBackupPath = SaveGameStore.ManualBackupPath(6);
                string invalidPrimary = ValidJson(
                    flagIdsJson: "[\"unknown_flag\"]",
                    bloomCount: 2);
                string validBackup = ValidJson(bloomCount: 9);
                fileSystem.Files[manualPath] = invalidPrimary;
                fileSystem.Files[manualBackupPath] = validBackup;

                SaveReadStatus status = SaveGameStore.GetManualSlotReadOnlyLoadStatus(
                    6,
                    catalog.Catalog,
                    out NarrativeState restored);

                Assert.That(status, Is.EqualTo(SaveReadStatus.RecoverableBackup));
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Garden.BloomCount, Is.EqualTo(9));
                Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { manualPath, manualBackupPath }));
                Assert.That(fileSystem.Files[manualPath], Is.EqualTo(invalidPrimary));
                Assert.That(fileSystem.Files[manualBackupPath], Is.EqualTo(validBackup));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [TestCase(0)]
        [TestCase(11)]
        public void ManualSlots_RejectNumbersOutsideTheSupportedRange(int slotNumber)
        {
            Assert.That(
                () => SaveGameStore.ManualSavePath(slotNumber),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(RawSlotState.Missing, RawSlotState.Missing, SaveReadStatus.Missing)]
        [TestCase(RawSlotState.Missing, RawSlotState.Invalid, SaveReadStatus.Invalid)]
        [TestCase(RawSlotState.Invalid, RawSlotState.Missing, SaveReadStatus.Invalid)]
        [TestCase(RawSlotState.Invalid, RawSlotState.Invalid, SaveReadStatus.Invalid)]
        [TestCase(RawSlotState.Missing, RawSlotState.Transient, SaveReadStatus.TransientFailure)]
        [TestCase(RawSlotState.Invalid, RawSlotState.Transient, SaveReadStatus.TransientFailure)]
        public void GetReadStatus_CombinesConfirmedPrimaryAndBackupResults(
            RawSlotState primaryState,
            RawSlotState backupState,
            SaveReadStatus expected)
        {
            ConfigureRawSlot(savePath, primaryState);
            ConfigureRawSlot(backupPath, backupState);

            SaveReadStatus status = SaveGameStore.GetReadStatus();

            Assert.That(status, Is.EqualTo(expected));
            Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { savePath, backupPath }));
            Assert.That(fileSystem.MutationCount, Is.Zero);
        }

        [Test]
        public void GetLoadStatus_NullCatalogDoesNotTouchStorage()
        {
            SaveReadStatus status = SaveGameStore.GetLoadStatus(
                null,
                out NarrativeState state,
                out SaveWriteStatus promotionStatus);

            Assert.That(status, Is.EqualTo(SaveReadStatus.CatalogUnavailable));
            Assert.That(state, Is.Null);
            Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
            Assert.That(fileSystem.Operations, Is.Empty);
        }

        [Test]
        public void GetLoadStatus_ValidPrimaryWinsWithoutReadingBackup()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[savePath] = ValidJson(bloomCount: 4);
                fileSystem.Files[backupPath] = ValidJson(bloomCount: 9);

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(state, Is.Not.Null);
                Assert.That(state.Garden.BloomCount, Is.EqualTo(4));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { savePath }));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [TestCase(RawSlotState.Invalid, PromotionOperation.Replace)]
        [TestCase(RawSlotState.Missing, PromotionOperation.Move)]
        public void GetLoadStatus_RecoversAndAtomicallyPromotesExactBackupJson(
            RawSlotState primaryState,
            PromotionOperation expectedOperation)
        {
            using (var catalog = new CatalogFixture())
            {
                ConfigureRawSlot(savePath, primaryState);
                string backupJson = " \n" + ValidJson(bloomCount: 7) + "\n ";
                fileSystem.Files[backupPath] = backupJson;

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Recovered));
                Assert.That(state, Is.Not.Null);
                Assert.That(state.Garden.BloomCount, Is.EqualTo(7));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.Files[savePath], Is.EqualTo(backupJson));
                Assert.That(fileSystem.Files[backupPath], Is.EqualTo(backupJson));
                Assert.That(fileSystem.Files.ContainsKey(temporaryPath), Is.False);

                if (expectedOperation == PromotionOperation.Replace)
                {
                    Assert.That(fileSystem.ReplaceCalls, Has.Count.EqualTo(1));
                    Assert.That(fileSystem.ReplaceCalls[0].SourcePath, Is.EqualTo(temporaryPath));
                    Assert.That(fileSystem.ReplaceCalls[0].DestinationPath, Is.EqualTo(savePath));
                    Assert.That(fileSystem.ReplaceCalls[0].DestinationBackupPath, Is.Null);
                    Assert.That(fileSystem.MoveCalls, Is.Empty);
                }
                else
                {
                    Assert.That(fileSystem.MoveCalls, Is.EqualTo(new[] { (temporaryPath, savePath) }));
                    Assert.That(fileSystem.ReplaceCalls, Is.Empty);
                }
            }
        }

        [Test]
        public void GetLoadStatus_CatalogInvalidPrimaryFallsBackToCatalogValidBackup()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[savePath] = ValidJson(flagIdsJson: "[\"unknown_flag\"]", bloomCount: 3);
                string backupJson = ValidJson(bloomCount: 6);
                fileSystem.Files[backupPath] = backupJson;

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Recovered));
                Assert.That(state.Garden.BloomCount, Is.EqualTo(6));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.Files[savePath], Is.EqualTo(backupJson));
                Assert.That(fileSystem.ReplaceCalls.Single().DestinationBackupPath, Is.Null);
            }
        }

        [Test]
        public void GetLoadStatus_CatalogInvalidBackupIsNotPromoted()
        {
            using (var catalog = new CatalogFixture())
            {
                const string corruptPrimary = "not json";
                string invalidBackup = ValidJson(flagIdsJson: "[\"unknown_flag\"]");
                fileSystem.Files[savePath] = corruptPrimary;
                fileSystem.Files[backupPath] = invalidBackup;

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Invalid));
                Assert.That(state, Is.Null);
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.Files[savePath], Is.EqualTo(corruptPrimary));
                Assert.That(fileSystem.Files[backupPath], Is.EqualTo(invalidBackup));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [TestCase(RawSlotState.Missing, SaveReadStatus.Invalid)]
        [TestCase(RawSlotState.Invalid, SaveReadStatus.Invalid)]
        [TestCase(RawSlotState.Transient, SaveReadStatus.TransientFailure)]
        public void GetLoadStatus_CatalogInvalidCandidatesClearAllRestoreOutputs(
            RawSlotState backupState,
            SaveReadStatus expectedStatus)
        {
            using (var catalog = new CatalogFixture())
            {
                var catalogInvalidPrimary = new SaveGameData
                {
                    version = SaveGameStore.CurrentVersion,
                    chosenName = NameVariant.Laura.ToString(),
                    chapterIndex = 1,
                    beatIndex = 0,
                    flagIds = new List<string> { "unknown_flag" },
                    completedEndingId = "sacrifice",
                    playerLocationRecorded = true,
                    playerLocation = new SavedPlayerLocation
                    {
                        sceneName = "Manor",
                        position = new Vector3(1f, 2f, 3f),
                        bodyRotation = Quaternion.identity,
                        viewPitch = -10f
                    }
                };
                fileSystem.Files[savePath] = JsonUtility.ToJson(catalogInvalidPrimary, true);
                ConfigureRawSlot(backupPath, backupState);

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus,
                    out string completedEndingId,
                    out SavedPlayerLocation playerLocation);

                Assert.That(status, Is.EqualTo(expectedStatus));
                Assert.That(state, Is.Null);
                Assert.That(completedEndingId, Is.Null);
                Assert.That(playerLocation, Is.Null);
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [Test]
        public void GetLoadStatus_TransientPrimaryDoesNotReadBackup()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.ReadFailures[savePath] = new UnauthorizedAccessException("primary denied");
                fileSystem.Files[backupPath] = ValidJson(bloomCount: 9);

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.TransientFailure));
                Assert.That(state, Is.Null);
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.ReadPaths, Is.EqualTo(new[] { savePath }));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [Test]
        public void GetLoadStatus_TransientBackupDoesNotBecomeConfirmedInvalid()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[savePath] = "not json";
                fileSystem.ReadFailures[backupPath] = new SecurityException("backup temporarily denied");

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.TransientFailure));
                Assert.That(state, Is.Null);
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(fileSystem.MutationCount, Is.Zero);
            }
        }

        [TestCase(RawSlotState.Invalid, WriteFailurePoint.CreateDirectory)]
        [TestCase(RawSlotState.Invalid, WriteFailurePoint.TemporaryWrite)]
        [TestCase(RawSlotState.Invalid, WriteFailurePoint.Replace)]
        [TestCase(RawSlotState.Missing, WriteFailurePoint.Move)]
        public void GetLoadStatus_PromotionFailureStillRestoresStateAndPreservesBackup(
            RawSlotState primaryState,
            WriteFailurePoint failurePoint)
        {
            using (var catalog = new CatalogFixture())
            {
                ConfigureRawSlot(savePath, primaryState);
                string originalPrimary = fileSystem.Files.TryGetValue(savePath, out string primary)
                    ? primary
                    : null;
                string backupJson = ValidJson(bloomCount: 8);
                fileSystem.Files[backupPath] = backupJson;
                ConfigureWriteFailure(
                    failurePoint,
                    new IOException("injected promotion failure"));
                ExpectWriteFailureLog();

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Recovered));
                Assert.That(state, Is.Not.Null);
                Assert.That(state.Garden.BloomCount, Is.EqualTo(8));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.TransientFailure));
                Assert.That(fileSystem.Files.TryGetValue(savePath, out string finalPrimary),
                    Is.EqualTo(primaryState != RawSlotState.Missing));
                Assert.That(finalPrimary, Is.EqualTo(originalPrimary));
                Assert.That(fileSystem.Files[backupPath], Is.EqualTo(backupJson));
                Assert.That(fileSystem.Files.ContainsKey(temporaryPath), Is.False);
            }
        }

        [Test]
        public void TryLoad_ReturnsTrueForRecoveredBackup()
        {
            using (var catalog = new CatalogFixture())
            {
                fileSystem.Files[backupPath] = ValidJson(bloomCount: 5);

                bool loaded = SaveGameStore.TryLoad(
                    catalog.Catalog,
                    out NarrativeState state,
                    out SaveReadStatus status);

                Assert.That(loaded, Is.True);
                Assert.That(status, Is.EqualTo(SaveReadStatus.Recovered));
                Assert.That(state.Garden.BloomCount, Is.EqualTo(5));
            }
        }

        [Test]
        public void TrySave_FirstSlotMovesCompleteJsonIntoPrimary()
        {
            var state = new NarrativeState(NameVariant.Laura, 4)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };

            SaveWriteStatus status = SaveGameStore.TrySave(state);

            Assert.That(status, Is.EqualTo(SaveWriteStatus.Succeeded));
            Assert.That(fileSystem.Files.ContainsKey(savePath), Is.True);
            Assert.That(fileSystem.Files.ContainsKey(backupPath), Is.False);
            Assert.That(fileSystem.Files.ContainsKey(temporaryPath), Is.False);
            Assert.That(SaveGameStore.TryDeserialize(fileSystem.Files[savePath], out SaveGameData data), Is.True);
            Assert.That(data.bloomCount, Is.EqualTo(4));
            Assert.That(fileSystem.MoveCalls, Is.EqualTo(new[] { (temporaryPath, savePath) }));
        }

        [Test]
        public void TrySaveAndLoad_RoundTripsTaskCountsThroughJsonAndCatalogValidation()
        {
            using (var catalog = new CatalogFixture())
            {
                var state = new NarrativeState(NameVariant.Laura, 0)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };
                state.Tasks.RestoreCount(catalog.TaskObjective.Id, 4);

                Assert.That(SaveGameStore.TrySave(state), Is.EqualTo(SaveWriteStatus.Succeeded));

                SaveReadStatus status = SaveGameStore.GetLoadStatus(
                    catalog.Catalog,
                    out NarrativeState restored,
                    out SaveWriteStatus promotionStatus);

                Assert.That(status, Is.EqualTo(SaveReadStatus.Valid));
                Assert.That(promotionStatus, Is.EqualTo(SaveWriteStatus.Succeeded));
                Assert.That(restored.Tasks.CountFor(catalog.TaskObjective), Is.EqualTo(4));
            }
        }

        [Test]
        public void TrySave_ReplacementKeepsPreviousCompletePrimaryAsBackup()
        {
            string previousJson = ValidJson(bloomCount: 2);
            fileSystem.Files[savePath] = previousJson;
            var state = new NarrativeState(NameVariant.Everie, 7)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };

            SaveWriteStatus status = SaveGameStore.TrySave(state);

            Assert.That(status, Is.EqualTo(SaveWriteStatus.Succeeded));
            Assert.That(fileSystem.Files[backupPath], Is.EqualTo(previousJson));
            Assert.That(SaveGameStore.TryDeserialize(fileSystem.Files[savePath], out SaveGameData data), Is.True);
            Assert.That(data.chosenName, Is.EqualTo("Everie"));
            Assert.That(data.bloomCount, Is.EqualTo(7));
            Assert.That(fileSystem.ReplaceCalls.Single().DestinationBackupPath, Is.EqualTo(backupPath));
        }

        [TestCase(WriteFailurePoint.CreateDirectory)]
        [TestCase(WriteFailurePoint.TemporaryWrite)]
        [TestCase(WriteFailurePoint.Replace)]
        [TestCase(WriteFailurePoint.Move)]
        public void TrySave_ExpectedFailureKeepsOldSlotReadableAndCleansTemporaryFile(
            WriteFailurePoint failurePoint)
        {
            bool usesExistingPrimary = failurePoint != WriteFailurePoint.Move;
            string previousJson = ValidJson(bloomCount: 2);
            string previousBackup = ValidJson(bloomCount: 1);
            if (usesExistingPrimary)
                fileSystem.Files[savePath] = previousJson;
            fileSystem.Files[backupPath] = previousBackup;
            ConfigureWriteFailure(failurePoint, new IOException("injected filesystem failure"));
            ExpectWriteFailureLog();

            SaveWriteStatus status = SaveGameStore.TrySave(
                new NarrativeState(NameVariant.Laura, 9));

            Assert.That(status, Is.EqualTo(SaveWriteStatus.TransientFailure));
            Assert.That(fileSystem.Files.TryGetValue(savePath, out string currentPrimary),
                Is.EqualTo(usesExistingPrimary));
            Assert.That(currentPrimary, Is.EqualTo(usesExistingPrimary ? previousJson : null));
            Assert.That(fileSystem.Files[backupPath], Is.EqualTo(previousBackup));
            Assert.That(fileSystem.Files.ContainsKey(temporaryPath), Is.False);
            if (usesExistingPrimary)
                Assert.That(SaveGameStore.GetReadStatus(), Is.EqualTo(SaveReadStatus.Valid));
        }

        [TestCase(ExpectedFailureKind.UnauthorizedAccess)]
        [TestCase(ExpectedFailureKind.Security)]
        [TestCase(ExpectedFailureKind.PlatformNotSupported)]
        public void TrySave_CatchesExpectedEnvironmentFailures(ExpectedFailureKind failureKind)
        {
            switch (failureKind)
            {
                case ExpectedFailureKind.UnauthorizedAccess:
                    fileSystem.WriteFailure = new UnauthorizedAccessException("write denied");
                    break;
                case ExpectedFailureKind.Security:
                    fileSystem.WriteFailure = new SecurityException("write forbidden");
                    break;
                case ExpectedFailureKind.PlatformNotSupported:
                    fileSystem.WriteFailure = new PlatformNotSupportedException("filesystem operation unavailable");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null);
            }
            ExpectWriteFailureLog();

            SaveWriteStatus status = SaveGameStore.TrySave(
                new NarrativeState(NameVariant.Laura, 3));

            Assert.That(status, Is.EqualTo(SaveWriteStatus.TransientFailure));
        }

        [Test]
        public void TrySave_ProgrammingExceptionEscapes()
        {
            fileSystem.WriteFailure = new InvalidOperationException("programming failure");

            Assert.Throws<InvalidOperationException>(() =>
                SaveGameStore.TrySave(new NarrativeState(NameVariant.Laura, 3)));
        }

        [Test]
        public void TrySave_LogsOnlyOnceUntilAWriteSucceeds()
        {
            var state = new NarrativeState(NameVariant.Laura, 3);
            fileSystem.WriteFailure = new IOException("first failure episode");
            ExpectWriteFailureLog();

            Assert.That(SaveGameStore.TrySave(state), Is.EqualTo(SaveWriteStatus.TransientFailure));
            Assert.That(SaveGameStore.TrySave(state), Is.EqualTo(SaveWriteStatus.TransientFailure));

            fileSystem.WriteFailure = null;
            Assert.That(SaveGameStore.TrySave(state), Is.EqualTo(SaveWriteStatus.Succeeded));

            fileSystem.WriteFailure = new IOException("second failure episode");
            ExpectWriteFailureLog();
            Assert.That(SaveGameStore.TrySave(state), Is.EqualTo(SaveWriteStatus.TransientFailure));
        }

        [Test]
        public void TrySave_CleanupAccessFailureIsBestEffort()
        {
            fileSystem.Files[savePath] = ValidJson();
            fileSystem.ReplaceFailure = new IOException("replace failed");
            fileSystem.DeleteFailure = new UnauthorizedAccessException("temporary file locked");
            ExpectWriteFailureLog();

            SaveWriteStatus status = SaveGameStore.TrySave(
                new NarrativeState(NameVariant.Laura, 3));

            Assert.That(status, Is.EqualTo(SaveWriteStatus.TransientFailure));
            Assert.That(fileSystem.Files.ContainsKey(temporaryPath), Is.True);
        }

        [Test]
        public void TryDiscard_RemovesPrimaryBackupAndTemporaryFiles()
        {
            fileSystem.Files[savePath] = ValidJson(bloomCount: 3);
            fileSystem.Files[backupPath] = ValidJson(bloomCount: 2);
            fileSystem.Files[temporaryPath] = ValidJson(bloomCount: 4);

            SaveWriteStatus status = SaveGameStore.TryDiscard();

            Assert.That(status, Is.EqualTo(SaveWriteStatus.Succeeded));
            Assert.That(fileSystem.Files, Is.Empty);
        }

        [Test]
        public void TryDiscard_PrimaryDeleteFailureStillAttemptsBackupAndTemporaryDeletes()
        {
            string primaryJson = ValidJson(bloomCount: 3);
            fileSystem.Files[savePath] = primaryJson;
            fileSystem.Files[backupPath] = ValidJson(bloomCount: 2);
            fileSystem.Files[temporaryPath] = ValidJson(bloomCount: 4);
            fileSystem.DeleteFailures[savePath] =
                new UnauthorizedAccessException("primary slot is locked");
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Save write failed during Discard.*slot remains available where possible",
                    RegexOptions.Singleline));

            SaveWriteStatus status = SaveGameStore.TryDiscard();

            Assert.That(status, Is.EqualTo(SaveWriteStatus.TransientFailure));
            Assert.That(fileSystem.Files[savePath], Is.EqualTo(primaryJson));
            Assert.That(fileSystem.Files.ContainsKey(backupPath), Is.False);
            Assert.That(fileSystem.Files.ContainsKey(temporaryPath), Is.False);
            Assert.That(
                fileSystem.Operations.Where(operation =>
                    operation.StartsWith("Delete:", StringComparison.Ordinal)),
                Is.EqualTo(new[]
                {
                    "Delete:" + savePath,
                    "Delete:" + backupPath,
                    "Delete:" + temporaryPath
                }));
        }

        [TestCase(DiscardFailurePoint.Exists)]
        [TestCase(DiscardFailurePoint.Delete)]
        public void TryDiscard_ExpectedStorageFailureIsReportedWithoutEscaping(
            DiscardFailurePoint failurePoint)
        {
            string primaryJson = ValidJson();
            fileSystem.Files[savePath] = primaryJson;
            if (failurePoint == DiscardFailurePoint.Exists)
                fileSystem.FileExistsFailure = new IOException("slot probe failed");
            else
                fileSystem.DeleteFailure = new UnauthorizedAccessException("slot delete failed");
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Save write failed during Discard.*slot remains available where possible",
                    RegexOptions.Singleline));

            SaveWriteStatus status = SaveGameStore.TryDiscard();

            Assert.That(status, Is.EqualTo(SaveWriteStatus.TransientFailure));
            Assert.That(fileSystem.Files[savePath], Is.EqualTo(primaryJson));
        }

        private void ConfigureRawSlot(string path, RawSlotState state)
        {
            switch (state)
            {
                case RawSlotState.Missing:
                    fileSystem.Files.Remove(path);
                    fileSystem.ReadFailures.Remove(path);
                    break;
                case RawSlotState.Invalid:
                    fileSystem.Files[path] = "not valid json";
                    fileSystem.ReadFailures.Remove(path);
                    break;
                case RawSlotState.Valid:
                    fileSystem.Files[path] = ValidJson();
                    fileSystem.ReadFailures.Remove(path);
                    break;
                case RawSlotState.Transient:
                    fileSystem.Files.Remove(path);
                    fileSystem.ReadFailures[path] = new IOException("injected read failure");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void ConfigureWriteFailure(WriteFailurePoint point, Exception exception)
        {
            switch (point)
            {
                case WriteFailurePoint.CreateDirectory:
                    fileSystem.CreateDirectoryFailure = exception;
                    break;
                case WriteFailurePoint.TemporaryWrite:
                    fileSystem.WriteFailure = exception;
                    break;
                case WriteFailurePoint.Replace:
                    fileSystem.ReplaceFailure = exception;
                    break;
                case WriteFailurePoint.Move:
                    fileSystem.MoveFailure = exception;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(point), point, null);
            }
        }

        private static string ValidJson(
            string chosenName = "Laura",
            string flagIdsJson = "[]",
            int bloomCount = 3,
            string completedEndingId = null)
        {
            return
                "{\"version\":" + SaveGameStore.CurrentVersion +
                ",\"chosenName\":\"" + chosenName + "\"" +
                ",\"chapterIndex\":1,\"beatIndex\":0" +
                ",\"flagIds\":" + flagIdsJson +
                ",\"bloomCount\":" + bloomCount +
                ",\"liesTold\":0,\"patientsFed\":0" +
                (completedEndingId == null
                    ? string.Empty
                    : ",\"completedEndingId\":\"" + completedEndingId + "\"") +
                "}";
        }

        private static void ExpectWriteFailureLog()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex("Save write failed during .*Progress remains in memory and will be retried", RegexOptions.Singleline));
        }

        public enum RawSlotState
        {
            Missing,
            Invalid,
            Valid,
            Transient
        }

        public enum PromotionOperation
        {
            Move,
            Replace
        }

        public enum WriteFailurePoint
        {
            CreateDirectory,
            TemporaryWrite,
            Replace,
            Move
        }

        public enum ExpectedFailureKind
        {
            UnauthorizedAccess,
            Security,
            PlatformNotSupported
        }

        public enum DiscardFailurePoint
        {
            Exists,
            Delete
        }

        private sealed class CatalogFixture : IDisposable
        {
            private readonly List<Object> assets = new List<Object>();

            public CatalogFixture(params string[] flagIds)
            {
                Catalog = Create<NarrativeCatalog>();
                ChapterDefinition chapter = Create<ChapterDefinition>();

                var serializedCatalog = new SerializedObject(Catalog);
                SerializedProperty chapters = serializedCatalog.FindProperty("chapters");
                chapters.arraySize = 1;
                chapters.GetArrayElementAtIndex(0).objectReferenceValue = chapter;

                SerializedProperty flags = serializedCatalog.FindProperty("flags");
                flags.arraySize = flagIds.Length;
                for (int i = 0; i < flagIds.Length; i++)
                {
                    FlagId flag = Create<FlagId>();
                    var serializedFlag = new SerializedObject(flag);
                    serializedFlag.FindProperty("id").stringValue = flagIds[i];
                    serializedFlag.ApplyModifiedPropertiesWithoutUndo();
                    flags.GetArrayElementAtIndex(i).objectReferenceValue = flag;
                }

                TaskObjective = Create<TaskObjective>();
                var serializedObjective = new SerializedObject(TaskObjective);
                serializedObjective.FindProperty("id").stringValue = "gardening";
                serializedObjective.ApplyModifiedPropertiesWithoutUndo();
                SerializedProperty taskObjectives = serializedCatalog.FindProperty("taskObjectives");
                taskObjectives.arraySize = 1;
                taskObjectives.GetArrayElementAtIndex(0).objectReferenceValue = TaskObjective;

                EndingDefinition ending = Create<EndingDefinition>();
                var serializedEnding = new SerializedObject(ending);
                serializedEnding.FindProperty("id").stringValue = "sacrifice";
                serializedEnding.ApplyModifiedPropertiesWithoutUndo();
                SerializedProperty endings = serializedCatalog.FindProperty("endings");
                endings.arraySize = 1;
                endings.GetArrayElementAtIndex(0).objectReferenceValue = ending;

                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            }

            public NarrativeCatalog Catalog { get; }
            public TaskObjective TaskObjective { get; }

            public void Dispose()
            {
                for (int i = assets.Count - 1; i >= 0; i--)
                    Object.DestroyImmediate(assets[i]);
            }

            private T Create<T>() where T : ScriptableObject
            {
                T asset = ScriptableObject.CreateInstance<T>();
                assets.Add(asset);
                return asset;
            }
        }

        private sealed class FakeSaveGameFileSystem : ISaveGameFileSystem
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public readonly Dictionary<string, Exception> ReadFailures = new Dictionary<string, Exception>();
            public readonly Dictionary<string, Exception> DeleteFailures = new Dictionary<string, Exception>();
            public readonly List<string> Operations = new List<string>();
            public readonly List<string> ReadPaths = new List<string>();
            public readonly List<(string SourcePath, string DestinationPath)> MoveCalls =
                new List<(string SourcePath, string DestinationPath)>();
            public readonly List<ReplaceCall> ReplaceCalls = new List<ReplaceCall>();

            public Exception CreateDirectoryFailure { get; set; }
            public Exception FileExistsFailure { get; set; }
            public Exception WriteFailure { get; set; }
            public Exception MoveFailure { get; set; }
            public Exception ReplaceFailure { get; set; }
            public Exception DeleteFailure { get; set; }

            public int MutationCount => Operations.Count(operation =>
                operation.StartsWith("CreateDirectory:", StringComparison.Ordinal) ||
                operation.StartsWith("WriteAllText:", StringComparison.Ordinal) ||
                operation.StartsWith("Move:", StringComparison.Ordinal) ||
                operation.StartsWith("Replace:", StringComparison.Ordinal) ||
                operation.StartsWith("Delete:", StringComparison.Ordinal));

            public void CreateDirectory(string path)
            {
                Operations.Add("CreateDirectory:" + path);
                ThrowIfConfigured(CreateDirectoryFailure);
            }

            public bool FileExists(string path)
            {
                Operations.Add("FileExists:" + path);
                ThrowIfConfigured(FileExistsFailure);
                return Files.ContainsKey(path);
            }

            public string ReadAllText(string path)
            {
                Operations.Add("ReadAllText:" + path);
                ReadPaths.Add(path);
                if (ReadFailures.TryGetValue(path, out Exception failure))
                    throw failure;
                if (!Files.TryGetValue(path, out string contents))
                    throw new FileNotFoundException("File not found.", path);
                return contents;
            }

            public void WriteAllText(string path, string contents)
            {
                Operations.Add("WriteAllText:" + path);
                ThrowIfConfigured(WriteFailure);
                Files[path] = contents;
            }

            public void Move(string sourcePath, string destinationPath)
            {
                Operations.Add("Move:" + sourcePath + "->" + destinationPath);
                MoveCalls.Add((sourcePath, destinationPath));
                ThrowIfConfigured(MoveFailure);
                if (!Files.TryGetValue(sourcePath, out string contents))
                    throw new FileNotFoundException("Source file not found.", sourcePath);
                if (Files.ContainsKey(destinationPath))
                    throw new IOException("Destination already exists.");

                Files.Remove(sourcePath);
                Files[destinationPath] = contents;
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string destinationBackupPath)
            {
                Operations.Add(
                    "Replace:" + sourcePath + "->" + destinationPath + "|" +
                    (destinationBackupPath ?? "<null>"));
                ReplaceCalls.Add(new ReplaceCall(
                    sourcePath,
                    destinationPath,
                    destinationBackupPath));
                ThrowIfConfigured(ReplaceFailure);
                if (!Files.TryGetValue(sourcePath, out string sourceContents))
                    throw new FileNotFoundException("Source file not found.", sourcePath);
                if (!Files.TryGetValue(destinationPath, out string destinationContents))
                    throw new FileNotFoundException("Destination file not found.", destinationPath);

                if (destinationBackupPath != null)
                    Files[destinationBackupPath] = destinationContents;
                Files.Remove(sourcePath);
                Files[destinationPath] = sourceContents;
            }

            public void Delete(string path)
            {
                Operations.Add("Delete:" + path);
                if (DeleteFailures.TryGetValue(path, out Exception failure))
                    throw failure;
                ThrowIfConfigured(DeleteFailure);
                Files.Remove(path);
            }

            private static void ThrowIfConfigured(Exception exception)
            {
                if (exception != null)
                    throw exception;
            }
        }

        private sealed class ReplaceCall
        {
            public ReplaceCall(
                string sourcePath,
                string destinationPath,
                string destinationBackupPath)
            {
                SourcePath = sourcePath;
                DestinationPath = destinationPath;
                DestinationBackupPath = destinationBackupPath;
            }

            public string SourcePath { get; }
            public string DestinationPath { get; }
            public string DestinationBackupPath { get; }
        }
    }
}
