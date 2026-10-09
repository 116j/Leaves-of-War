using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class GameSessionSaveTests
    {
        private const string TestPath = "session-save-test";

        private readonly List<Object> createdObjects = new List<Object>();
        private ToggleSaveFileSystem fileSystem;
        private IDisposable fileSystemOverride;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new ToggleSaveFileSystem();
            fileSystemOverride = SaveGameStore.OverrideFileSystemForTests(fileSystem, TestPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (PauseController.Instance != null)
                Object.DestroyImmediate(PauseController.Instance.gameObject);

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
            fileSystemOverride.Dispose();
        }

        [Test]
        public void FailedFlagAutosave_KeepsProgressAndRetriesAtNextBoundary()
        {
            var presenter = new StatusPresenter();
            GameSession session = CreateSession(presenter, out NarrativeState state);
            FlagId flag = Track(ScriptableObject.CreateInstance<FlagId>());
            SetField(flag, "id", "test_flag");
            SetField(session.Catalog, "flags", new List<FlagId> { flag });
            fileSystem.FailWrites = true;
            ExpectWriteFailureLog();

            session.SetFlag(flag);

            Assert.That(state.HasFlag(flag), Is.True);
            Assert.That(session.IsSaveDirty, Is.True);
            Assert.That(session.HasRecoverableInMemoryProgress, Is.True);
            Assert.That(presenter.StatusMessages, Does.Contain(
                "PROGRESS COULD NOT BE SAVED.\nRETRYING AT THE NEXT CHECKPOINT."));

            InvokePrivate(session, "StopActiveRunner");
            Assert.That(session.IsSaveDirty, Is.True, "Runner cancellation must not forget unsaved progress.");

            fileSystem.FailWrites = false;
            object[] resumeArguments = { SaveReadStatus.Missing };
            bool resumed = (bool)InvokePrivate(
                session,
                "TryResumeDirtyState",
                resumeArguments);

            Assert.That(resumed, Is.True);
            Assert.That(resumeArguments[0], Is.EqualTo(SaveReadStatus.ResumedInMemory));
            Assert.That(session.IsSaveDirty, Is.False);
            Assert.That(session.HasRecoverableInMemoryProgress, Is.False);
            Assert.That(presenter.StatusMessages, Does.Contain("PROGRESS SAVED."));
            Assert.That(fileSystem.Files.ContainsKey(SaveGameStore.SavePath), Is.True);
        }

        [Test]
        public void UncataloguedFlag_IsRejectedWithoutWritingAnInvalidSave()
        {
            GameSession session = CreateSession(new StatusPresenter(), out NarrativeState state);
            FlagId flag = Track(ScriptableObject.CreateInstance<FlagId>());
            SetField(flag, "id", "orphan_flag");
            LogAssert.Expect(
                LogType.Error,
                "Refusing to set uncatalogued flag 'orphan_flag'. " +
                "Persisting it would create a save that cannot be restored.");

            session.SetFlag(flag);

            Assert.That(state.HasFlag(flag), Is.False);
            Assert.That(session.IsSaveDirty, Is.False);
            Assert.That(fileSystem.Files.ContainsKey(SaveGameStore.SavePath), Is.False);
        }

        [Test]
        public void FailedNewGameDiscard_BlocksAutosaveUntilTheOldTimelineIsRemoved()
        {
            GameSession session = CreateSession(new StatusPresenter(), out _);
            var oldPrimaryState = new NarrativeState(NameVariant.Laura, 91)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            var oldBackupState = new NarrativeState(NameVariant.Laura, 73)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            string oldPrimaryJson = JsonUtility.ToJson(SaveGameStore.Capture(oldPrimaryState));
            string oldBackupJson = JsonUtility.ToJson(SaveGameStore.Capture(oldBackupState));
            fileSystem.Files[SaveGameStore.SavePath] = oldPrimaryJson;
            fileSystem.Files[SaveGameStore.BackupPath] = oldBackupJson;
            fileSystem.FailDeletes = true;
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Save write failed during Discard.*storage will be retried",
                    RegexOptions.Singleline));
            LogAssert.Expect(
                LogType.Error,
                "Narrative travel target is not in Build Settings: ''.");

            session.BeginNewGame(NameVariant.Everie);

            Assert.That(fileSystem.Files[SaveGameStore.SavePath], Is.EqualTo(oldPrimaryJson));
            Assert.That(fileSystem.Files[SaveGameStore.BackupPath], Is.EqualTo(oldBackupJson));
            Assert.That(session.IsSaveDirty, Is.True);

            InvokePrivate(session, "PersistState");

            Assert.That(
                fileSystem.Files[SaveGameStore.SavePath],
                Is.EqualTo(oldPrimaryJson),
                "A checkpoint must not overwrite the old timeline while cleanup still fails.");
            Assert.That(fileSystem.Files[SaveGameStore.BackupPath], Is.EqualTo(oldBackupJson));

            fileSystem.FailDeletes = false;
            InvokePrivate(session, "PersistState");

            Assert.That(session.IsSaveDirty, Is.False);
            Assert.That(fileSystem.Files.ContainsKey(SaveGameStore.BackupPath), Is.False);
            Assert.That(
                SaveGameStore.TryDeserialize(
                    fileSystem.Files[SaveGameStore.SavePath],
                    out SaveGameData saved),
                Is.True);
            Assert.That(saved.chosenName, Is.EqualTo(NameVariant.Everie.ToString()));
            Assert.That(saved.bloomCount, Is.EqualTo(session.Catalog.InitialBloomCount));
        }

        [Test]
        public void InventoryStateChanges_ArePersistedWithTheNarrativeState()
        {
            GameSession session = CreateSession(new StatusPresenter(), out _);

            session.UnlockInventory();
            session.GiveInventoryItem("LetterOsmund");

            Assert.That(fileSystem.Files.ContainsKey(SaveGameStore.SavePath), Is.True);
            Assert.That(
                SaveGameStore.TryDeserialize(
                    fileSystem.Files[SaveGameStore.SavePath],
                    out SaveGameData saved),
                Is.True);
            Assert.That(saved.inventoryStateRecorded, Is.True);
            Assert.That(saved.inventoryUnlocked, Is.True);
            Assert.That(saved.inventoryItemIds, Is.EquivalentTo(new[] { "LetterOsmund" }));
        }

        [Test]
        public void InlineInventoryGrant_IsSavedOnlyWithItsAdvancedBeatCursor()
        {
            GameSession session = CreateSession(new StatusPresenter(), out NarrativeState state);
            var beat = new SpokenLineBeat();

            InvokePrivate(session, "ApplyInlineInventoryGrants", true, "LetterOsmund");

            Assert.That(state.Inventory.IsUnlocked, Is.True);
            Assert.That(state.Inventory.HasItem("LetterOsmund"), Is.True);
            Assert.That(
                fileSystem.Files.ContainsKey(SaveGameStore.SavePath),
                Is.False,
                "The inline grant must wait for the containing beat to complete.");

            InvokePrivate(session, "CompleteBeat", beat, true);

            Assert.That(
                SaveGameStore.TryDeserialize(
                    fileSystem.Files[SaveGameStore.SavePath],
                    out SaveGameData saved),
                Is.True);
            Assert.That(saved.beatIndex, Is.EqualTo(1));
            Assert.That(saved.inventoryUnlocked, Is.True);
            Assert.That(saved.inventoryItemIds, Is.EquivalentTo(new[] { "LetterOsmund" }));
        }

        [Test]
        public void ManualSlotSave_CapturesLivePlayerScenePositionRotationAndPitch()
        {
            Scene captureScene = SceneManager.GetActiveScene();
            string previousSceneName = captureScene.name;
            captureScene.name = "SaveCaptureScene";
            try
            {
                GameSession session = CreateSession(new StatusPresenter(), out _);
                FirstPersonController player = CreatePlayer(session);
                var position = new Vector3(-12.5f, 1.75f, 34.25f);
                Quaternion rotation = Quaternion.Euler(11f, 137f, -9f);
                const float viewPitch = -26.5f;
                Assert.That(
                    player.TrySetPose(position, rotation, viewPitch, out string poseError),
                    Is.True,
                    poseError);

                Assert.That(session.SaveManualSlot(2), Is.EqualTo(SaveWriteStatus.Succeeded));

                Assert.That(
                    SaveGameStore.TryDeserialize(
                        fileSystem.Files[SaveGameStore.ManualSavePath(2)],
                        out SaveGameData saved),
                    Is.True);
                Assert.That(saved.playerLocationRecorded, Is.True);
                Assert.That(saved.playerLocation, Is.Not.Null);
                Assert.That(saved.playerLocation.sceneName, Is.EqualTo(captureScene.name));
                Assert.That(
                    Vector3.Distance(saved.playerLocation.position, position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(saved.playerLocation.bodyRotation, rotation),
                    Is.LessThan(0.001f));
                Assert.That(saved.playerLocation.viewPitch, Is.EqualTo(viewPitch).Within(0.0001f));
            }
            finally
            {
                if (captureScene.IsValid())
                    captureScene.name = previousSceneName;
            }
        }

        [Test]
        public void ManualSlotLoad_ImmediatelyBecomesTheAutosaveContinuePoint()
        {
            GameSession session = CreateSession(new StatusPresenter(), out _);
            FirstPersonController outgoingPlayer = CreatePlayer(session);
            Assert.That(
                outgoingPlayer.TrySetPose(
                    new Vector3(90f, 4f, -70f),
                    Quaternion.Euler(0f, 250f, 0f),
                    41f,
                    out string poseError),
                Is.True,
                poseError);
            var manualState = new NarrativeState(NameVariant.Everie, 9)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            manualState.Inventory.Unlock();
            manualState.Inventory.AddItem("LetterOsmund");
            var loadedLocation = new SavedPlayerLocation
            {
                sceneName = "Manor",
                position = new Vector3(-22.5f, 0.12f, 13.75f),
                bodyRotation = Quaternion.Euler(7f, 33f, -4f),
                viewPitch = -18f
            };
            Assert.That(
                SaveGameStore.TrySaveManualSlot(4, manualState, null, loadedLocation),
                Is.EqualTo(SaveWriteStatus.Succeeded));

            LogAssert.Expect(
                LogType.Error,
                "Narrative travel target is not in Build Settings: ''.");

            Assert.That(session.LoadManualSlot(4, out SaveReadStatus readStatus), Is.True);

            Assert.That(readStatus, Is.EqualTo(SaveReadStatus.Valid));
            Assert.That(session.State.ChosenName, Is.EqualTo(NameVariant.Everie));
            Assert.That(session.State.Garden.BloomCount, Is.EqualTo(9));
            Assert.That(
                SaveGameStore.TryDeserialize(
                    fileSystem.Files[SaveGameStore.SavePath],
                    out SaveGameData autosave),
                Is.True);
            Assert.That(autosave.chosenName, Is.EqualTo(NameVariant.Everie.ToString()));
            Assert.That(autosave.bloomCount, Is.EqualTo(9));
            Assert.That(autosave.inventoryItemIds, Is.EquivalentTo(new[] { "LetterOsmund" }));
            Assert.That(autosave.playerLocationRecorded, Is.True);
            Assert.That(autosave.playerLocation.sceneName, Is.EqualTo(loadedLocation.sceneName));
            Assert.That(
                Vector3.Distance(autosave.playerLocation.position, loadedLocation.position),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    autosave.playerLocation.bodyRotation,
                    loadedLocation.bodyRotation),
                Is.LessThan(0.001f));
            Assert.That(
                autosave.playerLocation.viewPitch,
                Is.EqualTo(loadedLocation.viewPitch).Within(0.0001f));
        }

        private GameSession CreateSession(StatusPresenter presenter, out NarrativeState state)
        {
            GameObject sessionObject = Track(new GameObject("Save Test Session"));
            GameSession session = sessionObject.AddComponent<GameSession>();
            ChapterDefinition chapter = Track(ScriptableObject.CreateInstance<ChapterDefinition>());
            SetField(chapter, "index", 1);
            SetField(
                chapter,
                "beats",
                new List<NarrativeBeat> { new SpokenLineBeat() });
            NarrativeCatalog catalog = Track(ScriptableObject.CreateInstance<NarrativeCatalog>());
            SetField(catalog, "chapters", new List<ChapterDefinition> { chapter });
            session.Configure(catalog, null);
            session.RegisterPresenter(presenter);
            state = new NarrativeState(NameVariant.Laura, 3)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            InvokePrivate(session, "SetState", state);
            return session;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private FirstPersonController CreatePlayer(GameSession session)
        {
            GameObject playerObject = Track(new GameObject("Save Test Player"));
            playerObject.SetActive(false);
            CharacterController character = playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.enabled = false;
            FirstPersonController player = playerObject.AddComponent<FirstPersonController>();
            playerObject.SetActive(true);
            SetField(player, "controller", character);
            SetField(player, "playerCamera", playerCamera);
            session.RegisterPlayer(player, player.PlayerCamera);
            return player;
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void ExpectWriteFailureLog()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Save write failed during .*Progress remains in memory and will be retried",
                    RegexOptions.Singleline));
        }

        private sealed class ToggleSaveFileSystem : ISaveGameFileSystem
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

            public bool FailWrites { get; set; }
            public bool FailDeletes { get; set; }

            public void CreateDirectory(string path)
            {
            }

            public bool FileExists(string path) => Files.ContainsKey(path);

            public string ReadAllText(string path)
            {
                if (!Files.TryGetValue(path, out string contents))
                    throw new FileNotFoundException("Missing test file.", path);
                return contents;
            }

            public void WriteAllText(string path, string contents)
            {
                if (FailWrites)
                    throw new IOException("Injected session save failure.");
                Files[path] = contents;
            }

            public void Move(string sourcePath, string destinationPath)
            {
                string contents = Files[sourcePath];
                Files.Remove(sourcePath);
                Files[destinationPath] = contents;
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string destinationBackupPath)
            {
                if (destinationBackupPath != null)
                    Files[destinationBackupPath] = Files[destinationPath];
                Files[destinationPath] = Files[sourcePath];
                Files.Remove(sourcePath);
            }

            public void Delete(string path)
            {
                if (FailDeletes)
                    throw new IOException("Injected autosave discard failure.");
                Files.Remove(path);
            }
        }

        private sealed class StatusPresenter : INarrativePresenter
        {
            public readonly List<string> StatusMessages = new List<string>();

            public IEnumerator PresentLine(ResolvedLine line, string speakerName, float visionIntensity)
            {
                yield break;
            }

            public IEnumerator PresentCard(string text)
            {
                yield break;
            }

            public IEnumerator PresentChoice(
                IReadOnlyList<ChoiceOption> options,
                Action<EndingDefinition> selected)
            {
                yield break;
            }

            public IEnumerator PresentDocument(IReadableDocument document, Action closed)
            {
                yield break;
            }

            public void ShowStatus(string message)
            {
                StatusMessages.Add(message);
            }

            public void ClearPresentation()
            {
            }
        }
    }
}
