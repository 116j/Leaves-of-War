using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class GameSessionTravelTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        private readonly List<FirstPersonController> players =
            new List<FirstPersonController>();
        private IDisposable fileSystemOverride;
        private GameSession currentSession;

        [SetUp]
        public void SetUp()
        {
            fileSystemOverride = SaveGameStore.OverrideFileSystemForTests(
                new MemorySaveFileSystem(),
                "travel-test-persistent-data");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
            spawnPoints.Clear();
            players.Clear();
            currentSession = null;
            fileSystemOverride.Dispose();
        }

        [Test]
        public void UnavailableTravel_DoesNotAdvanceBeatCursor()
        {
            GameSession session = CreateSession(
                out ChapterDefinition chapter,
                out NarrativeState state,
                out ImmediatePresenter presenter);
            var loader = new FakeSceneLoader();
            session.SetSceneLoaderForTests(loader);

            ExpectTravelRecoveryErrors("MissingScene");
            RunToCompletion(InvokeRunChapter(session, chapter));

            Assert.That(state.BeatIndex, Is.Zero);
            Assert.That(loader.LoadAttempts, Is.Zero);
            Assert.That(
                presenter.PresentedCards,
                Does.Contain("NARRATIVE DATA ERROR\nTRAVEL CANNOT CONTINUE."));
        }

        [Test]
        public void FailedSequence_UsesSequenceRecoveryCardAndReleasesLocks()
        {
            GameSession session = CreateSession(
                out ChapterDefinition chapter,
                out NarrativeState state,
                out ImmediatePresenter presenter);
            var sequenceBeat = new SequenceBeat();
            SetField(sequenceBeat, "sequenceId", "test_sequence_failure");
            SetField(sequenceBeat, "locksPlayer", true);
            SetField(
                chapter,
                "beats",
                new List<NarrativeBeat> { sequenceBeat });
            session.RegisterSequencePlayer(
                new FailingSequencePlayer("test_sequence_failure"));

            LogAssert.Expect(LogType.Error, "Injected sequence failure.");
            LogAssert.Expect(
                LogType.Error,
                "Cannot recover from narrative data error because 'MainMenu' is not in Build Settings.");
            RunToCompletion(InvokeRunChapter(session, chapter));

            Assert.That(state.BeatIndex, Is.Zero);
            Assert.That(
                presenter.PresentedCards,
                Does.Contain("NARRATIVE DATA ERROR\nSEQUENCE CANNOT CONTINUE."));
            Assert.That(
                presenter.PresentedCards,
                Does.Not.Contain("NARRATIVE DATA ERROR\nTRAVEL CANNOT CONTINUE."));
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(session.ActiveSequenceId, Is.Null);
            Assert.That(session.HasActiveSequencePlayback, Is.False);
        }

        [Test]
        public void NullSceneOperation_DoesNotDispatchFollowingBeat()
        {
            GameSession session = CreateSession(
                out ChapterDefinition chapter,
                out NarrativeState state,
                out ImmediatePresenter presenter);
            var followingBeat = new TitleCardBeat();
            SetField(followingBeat, "text", "FOLLOWING BEAT RAN");
            SetField(
                chapter,
                "beats",
                new List<NarrativeBeat> { chapter.Beats[0], followingBeat });
            var loader = new FakeSceneLoader
            {
                TargetLoadable = true,
                ReturnNullOperation = true
            };
            session.SetSceneLoaderForTests(loader);

            LogAssert.Expect(LogType.Error, "Narrative travel to 'MissingScene' did not start.");
            LogAssert.Expect(LogType.Error, "Cannot recover from narrative data error because 'MainMenu' is not in Build Settings.");
            RunToCompletion(InvokeRunChapter(session, chapter));

            Assert.That(state.BeatIndex, Is.Zero);
            Assert.That(loader.LoadAttempts, Is.EqualTo(1));
            Assert.That(presenter.PresentedCards, Does.Not.Contain("FOLLOWING BEAT RAN"));
        }

        [Test]
        public void MissingSpawn_DoesNotCompleteTravel()
        {
            GameSession session = CreateSession(out ChapterDefinition chapter, out NarrativeState state);
            var loader = SuccessfulTargetLoader();
            session.SetSceneLoaderForTests(loader);

            LogAssert.Expect(
                LogType.Error,
                $"No SpawnPoint with id 'arrival' exists in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'.");
            LogAssert.Expect(LogType.Error, "Cannot recover from narrative data error because 'MainMenu' is not in Build Settings.");
            RunToCompletion(InvokeRunChapter(session, chapter));

            Assert.That(state.BeatIndex, Is.Zero);
        }

        [Test]
        public void AmbiguousSpawn_DoesNotCompleteTravel()
        {
            CreateSpawn("arrival");
            CreateSpawn("arrival");
            GameSession session = CreateSession(out ChapterDefinition chapter, out NarrativeState state);
            var loader = SuccessfulTargetLoader();
            session.SetSceneLoaderForTests(loader);

            LogAssert.Expect(
                LogType.Error,
                $"Spawn-point id 'arrival' is ambiguous in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' (2 matches).");
            LogAssert.Expect(LogType.Error, "Cannot recover from narrative data error because 'MainMenu' is not in Build Settings.");
            RunToCompletion(InvokeRunChapter(session, chapter));

            Assert.That(state.BeatIndex, Is.Zero);
        }

        [Test]
        public void CorrectedTravel_RetriesOnceAndCompletesOnce()
        {
            GameSession session = CreateSession(out ChapterDefinition chapter, out NarrativeState state);
            var loader = new FakeSceneLoader();
            session.SetSceneLoaderForTests(loader);

            ExpectTravelRecoveryErrors("MissingScene");
            RunToCompletion(InvokeRunChapter(session, chapter));
            Assert.That(state.BeatIndex, Is.Zero);

            loader.TargetLoadable = true;
            loader.TargetActive = true;
            CreateSpawn("arrival");
            CreatePlayer();
            RunToCompletion(InvokeRunChapter(session, chapter));

            Assert.That(loader.LoadAttempts, Is.EqualTo(1));
            Assert.That(state.BeatIndex, Is.EqualTo(1));
        }

        [Test]
        public void RestoredLocation_SuppressesAlreadyCompletedArrivalTransition()
        {
            GameSession session = CreateSession(
                out ChapterDefinition chapter,
                out NarrativeState state);
            SetField(chapter, "baseScene", "MissingScene");
            SetField(chapter, "baseSpawnPointId", "arrival");
            SetField(
                chapter,
                "beats",
                new List<NarrativeBeat> { new GateBeat() });
            state.BeatIndex = 0;

            DreamTransitionSettings settings =
                Track(ScriptableObject.CreateInstance<DreamTransitionSettings>());
            var cue = new DreamTransitionCue();
            SetField(cue, "chapterIndex", 1);
            SetField(cue, "sceneName", "MissingScene");
            SetField(cue, "trigger", DreamTransitionTrigger.OnEnterScene);
            SetField(settings, "cues", new List<DreamTransitionCue> { cue });
            SetField(session, "dreamTransitionSettings", settings);

            var loader = SuccessfulTargetLoader();
            session.SetSceneLoaderForTests(loader);
            CreateSpawn("arrival");
            CreatePlayer();

            int transitionStarts = 0;
            Action<DreamTransitionCue> onTransitionStarting = _ => transitionStarts++;
            GameSession.DreamTransitionStarting += onTransitionStarting;
            object result;
            try
            {
                result = CreateOperationResult();
                RunToCompletion(InvokeLoadChapterLocation(
                    session,
                    chapter,
                    result,
                    playTransition: false));
            }
            finally
            {
                GameSession.DreamTransitionStarting -= onTransitionStarting;
            }

            Assert.That(OperationSucceeded(result), Is.True);
            Assert.That(loader.LoadAttempts, Is.EqualTo(1));
            Assert.That(transitionStarts, Is.Zero);
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [Test]
        public void LegacySaveWithoutPlayerLocation_UsesTheAuthoredSpawnPose()
        {
            SpawnPoint spawn = CreateSpawn("arrival");
            var spawnPosition = new Vector3(14.5f, 2.25f, -31.75f);
            Quaternion spawnRotation = Quaternion.Euler(8f, 122f, -6f);
            spawn.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            FirstPersonController player = CreatePlayer();
            Assert.That(
                player.TrySetPose(
                    new Vector3(-90f, 6f, 70f),
                    Quaternion.Euler(-12f, 300f, 4f),
                    37f,
                    out string poseError),
                Is.True,
                poseError);

            GameSession session = CreateSession(
                out ChapterDefinition chapter,
                out NarrativeState _);
            SetField(chapter, "baseScene", "MissingScene");
            SetField(chapter, "baseSpawnPointId", "arrival");
            SetField(chapter, "beats", new List<NarrativeBeat> { new GateBeat() });
            session.SetSceneLoaderForTests(SuccessfulTargetLoader());
            InvokePrivate(session, "PrepareInitialPlacement", null, true);

            object result = CreateOperationResult();
            RunToCompletion(InvokeLoadChapterLocation(
                session,
                chapter,
                result,
                playTransition: false));

            Assert.That(OperationSucceeded(result), Is.True);
            Assert.That(Vector3.Distance(player.transform.position, spawnPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, spawnRotation), Is.LessThan(0.001f));
            Assert.That(player.ViewPitch, Is.Zero.Within(0.0001f));
        }

        private GameSession CreateSession(
            out ChapterDefinition chapter,
            out NarrativeState state)
        {
            return CreateSession(out chapter, out state, out _);
        }

        private GameSession CreateSession(
            out ChapterDefinition chapter,
            out NarrativeState state,
            out ImmediatePresenter presenter)
        {
            var travel = new TravelBeat();
            SetField(travel, "targetScene", "MissingScene");
            SetField(travel, "spawnPointId", "arrival");

            chapter = Track(ScriptableObject.CreateInstance<ChapterDefinition>());
            SetField(chapter, "index", 1);
            SetField(chapter, "beats", new List<NarrativeBeat> { travel });

            NarrativeCatalog catalog = Track(ScriptableObject.CreateInstance<NarrativeCatalog>());
            SetField(catalog, "chapters", new List<ChapterDefinition> { chapter });

            GameObject sessionObject = Track(new GameObject("Travel Test Session"));
            GameSession session = sessionObject.AddComponent<GameSession>();
            currentSession = session;
            for (int i = 0; i < spawnPoints.Count; i++)
                session.RegisterSpawnPoint(spawnPoints[i]);
            for (int i = 0; i < players.Count; i++)
                session.RegisterPlayer(players[i], players[i].PlayerCamera);
            session.Configure(catalog, null);
            presenter = new ImmediatePresenter();
            session.RegisterPresenter(presenter);

            state = new NarrativeState(NameVariant.Laura, 0)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            InvokePrivate(session, "SetState", state);
            return session;
        }

        private SpawnPoint CreateSpawn(string id)
        {
            GameObject spawnObject = Track(new GameObject($"Spawn {id}"));
            SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
            SetField(spawn, "id", id);
            spawnPoints.Add(spawn);
            currentSession?.RegisterSpawnPoint(spawn);
            return spawn;
        }

        private FirstPersonController CreatePlayer()
        {
            GameObject playerObject = Track(new GameObject("Travel Test Player"));
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
            players.Add(player);
            currentSession?.RegisterPlayer(player, player.PlayerCamera);
            return player;
        }

        private static FakeSceneLoader SuccessfulTargetLoader() => new FakeSceneLoader
        {
            TargetLoadable = true,
            TargetActive = true
        };

        private static void ExpectTravelRecoveryErrors(string sceneName)
        {
            LogAssert.Expect(LogType.Error, $"Narrative travel target is not in Build Settings: '{sceneName}'.");
            LogAssert.Expect(LogType.Error, "Cannot recover from narrative data error because 'MainMenu' is not in Build Settings.");
        }

        private static IEnumerator InvokeRunChapter(GameSession session, ChapterDefinition chapter)
        {
            MethodInfo method = typeof(GameSession).GetMethod(
                "RunChapter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (IEnumerator)method.Invoke(session, new object[] { chapter });
        }

        private static IEnumerator InvokeLoadChapterLocation(
            GameSession session,
            ChapterDefinition chapter,
            object result,
            bool playTransition)
        {
            MethodInfo method = typeof(GameSession).GetMethod(
                "LoadChapterLocation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (IEnumerator)method.Invoke(
                session,
                new[] { (object)chapter, result, playTransition });
        }

        private static object CreateOperationResult()
        {
            Type resultType = typeof(GameSession).Assembly.GetType(
                "Hortensia.Runtime.NarrativeOperationResult");
            Assert.That(resultType, Is.Not.Null);
            return Activator.CreateInstance(resultType);
        }

        private static bool OperationSucceeded(object result)
        {
            PropertyInfo property = result.GetType().GetProperty(
                "Succeeded",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(result);
        }

        private static void RunToCompletion(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            int steps = 0;

            while (stack.Count > 0)
            {
                Assert.That(++steps, Is.LessThan(1000), "Coroutine did not finish in the synchronous test driver.");
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, arguments);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public bool TargetLoadable { get; set; }
            public bool TargetActive { get; set; }
            public bool ReturnNullOperation { get; set; }
            public int LoadAttempts { get; private set; }

            public bool CanLoad(string sceneName) =>
                string.Equals(sceneName, "MissingScene", StringComparison.Ordinal) && TargetLoadable;

            public ISceneLoadOperation LoadSingleAsync(string sceneName)
            {
                LoadAttempts++;
                return ReturnNullOperation ? null : new CompletedSceneLoadOperation();
            }

            public bool IsLoadedAndActive(string sceneName) => TargetActive;
        }

        private sealed class CompletedSceneLoadOperation : ISceneLoadOperation
        {
            public bool IsDone => true;
        }

        private sealed class ImmediatePresenter : INarrativePresenter
        {
            public readonly List<string> PresentedCards = new List<string>();

            public IEnumerator PresentLine(ResolvedLine line, string speakerName, float visionIntensity)
            {
                yield break;
            }

            public IEnumerator PresentCard(string text)
            {
                PresentedCards.Add(text);
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
            }

            public void ClearPresentation()
            {
            }
        }

        private sealed class FailingSequencePlayer : ISequencePlayer
        {
            public FailingSequencePlayer(string sequenceId)
            {
                SequenceId = sequenceId;
            }

            public string SequenceId { get; }
            public SequenceLockFlags RequiredLocks => SequenceLockFlags.NarrativeInput;

            public IEnumerator Play(SequencePlaybackHandle playback)
            {
                playback.Fail("Injected sequence failure.");
                yield break;
            }
        }

        private sealed class MemorySaveFileSystem : ISaveGameFileSystem
        {
            private readonly Dictionary<string, string> files =
                new Dictionary<string, string>();

            public void CreateDirectory(string path)
            {
            }

            public bool FileExists(string path) => files.ContainsKey(path);

            public string ReadAllText(string path)
            {
                if (!files.TryGetValue(path, out string contents))
                    throw new FileNotFoundException("Missing test file.", path);
                return contents;
            }

            public void WriteAllText(string path, string contents)
            {
                files[path] = contents;
            }

            public void Move(string sourcePath, string destinationPath)
            {
                string contents = files[sourcePath];
                files.Remove(sourcePath);
                files[destinationPath] = contents;
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string destinationBackupPath)
            {
                if (destinationBackupPath != null)
                    files[destinationBackupPath] = files[destinationPath];
                files[destinationPath] = files[sourcePath];
                files.Remove(sourcePath);
            }

            public void Delete(string path)
            {
                files.Remove(path);
            }
        }
    }
}
