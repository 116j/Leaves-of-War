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
    public sealed class ChapterRunnerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private MemorySaveFileSystem fileSystem;
        private IDisposable fileSystemOverride;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new MemorySaveFileSystem();
            fileSystemOverride = SaveGameStore.OverrideFileSystemForTests(
                fileSystem,
                "chapter-runner-test");
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
            fileSystemOverride.Dispose();
        }

        [Test]
        public void SequenceBeat_PlaysRegisteredSequenceUnderAuthoredPlayerLock()
        {
            SequenceBeat beat = CreateSequenceBeat("study_1910_seat", true);
            GameSession session = CreateSession(beat, out NarrativeState state);
            var sequencePlayer = new RecordingSequencePlayer(session, "study_1910_seat");
            session.RegisterSequencePlayer(sequencePlayer);

            RunToCompletion(InvokeRunChapter(session, session.CurrentChapter));

            Assert.That(sequencePlayer.PlayCount, Is.EqualTo(1));
            Assert.That(sequencePlayer.LockedAtStart, Is.True);
            Assert.That(sequencePlayer.LockedBeforeFinish, Is.True);
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(state.BeatIndex, Is.EqualTo(1));
        }

        [Test]
        public void SequenceBeat_CanLeavePlayerUnlockedWhenAuthored()
        {
            SequenceBeat beat = CreateSequenceBeat("study_1910_reveal", false);
            GameSession session = CreateSession(beat, out NarrativeState state);
            var sequencePlayer = new RecordingSequencePlayer(session, "study_1910_reveal");
            session.RegisterSequencePlayer(sequencePlayer);

            RunToCompletion(InvokeRunChapter(session, session.CurrentChapter));

            Assert.That(sequencePlayer.PlayCount, Is.EqualTo(1));
            Assert.That(sequencePlayer.LockedAtStart, Is.False);
            Assert.That(sequencePlayer.LockedBeforeFinish, Is.False);
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(state.BeatIndex, Is.EqualTo(1));
        }

        [Test]
        public void SequenceBeat_MissingPlayerWarnsAndAdvances()
        {
            SequenceBeat beat = CreateSequenceBeat("dream_2_flood", true);
            GameSession session = CreateSession(beat, out NarrativeState state);
            LogAssert.Expect(
                LogType.Warning,
                "No ISequencePlayer is registered for sequence 'dream_2_flood'. " +
                "Advancing the narrative beat.");

            RunToCompletion(InvokeRunChapter(session, session.CurrentChapter));

            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(state.BeatIndex, Is.EqualTo(1));
        }

        [Test]
        public void PatientSession_IsPlayerLockedEvenWhenOrdinaryLinesAreNot()
        {
            PatientDefinition patient = Track(ScriptableObject.CreateInstance<PatientDefinition>());
            SetField(patient, "displayName", "Norbury");
            object boxedContent = new LineContent();
            SetField(boxedContent, "lineId", "patient_norbury");
            SetField(boxedContent, "text", "The greenhouse was breathing.");
            SetField(patient, "content", boxedContent);

            var beat = new PatientSessionBeat();
            SetField(beat, "roster", new List<PatientDefinition> { patient });

            GameSession session = CreateSession(beat, out NarrativeState state);
            var presenter = new LockObservingPresenter(session);
            var patientVisuals = new RecordingPatientVisualPresenter();
            session.RegisterPresenter(presenter);
            session.RegisterPatientVisualPresenter(patientVisuals);

            RunToCompletion(InvokeRunChapter(session, session.CurrentChapter));

            Assert.That(presenter.PresentedLines, Is.EqualTo(1));
            Assert.That(presenter.PlayerWasLocked, Is.True);
            Assert.That(patientVisuals.ShownPatients, Is.EqualTo(new[] { patient }));
            Assert.That(patientVisuals.ClearCount, Is.EqualTo(1));
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(state.BeatIndex, Is.EqualTo(1));
        }

        [Test]
        public void RunEnding_ExposesTransientEndingAndSequenceContextsThenClearsThem()
        {
            SequenceBeat beat = CreateSequenceBeat("boss_verdant_mirror", false);
            GameSession session = CreateSession(beat, out _);
            var sequencePlayer = new ContextRecordingSequencePlayer(
                session,
                "boss_verdant_mirror");
            session.RegisterSequencePlayer(sequencePlayer);

            EndingDefinition ending = Track(ScriptableObject.CreateInstance<EndingDefinition>());
            SetField(ending, "id", "confession");
            SetField(ending, "title", "Confession");
            SetField(ending, "beats", new List<NarrativeBeat> { beat });
            SetField(
                session.Catalog,
                "endings",
                new List<EndingDefinition> { ending });

            object result = CreateNarrativeOperationResult();
            RunToCompletion(InvokeRunEnding(session, ending, result));

            Assert.That(ResultSucceeded(result), Is.True);
            Assert.That(sequencePlayer.EndingAtStart, Is.EqualTo("confession"));
            Assert.That(sequencePlayer.SequenceAtStart, Is.EqualTo("boss_verdant_mirror"));
            Assert.That(sequencePlayer.EndingBeforeFinish, Is.EqualTo("confession"));
            Assert.That(sequencePlayer.SequenceBeforeFinish, Is.EqualTo("boss_verdant_mirror"));
            Assert.That(session.CompletedEndingId, Is.EqualTo("confession"));
            Assert.That(session.CurrentEndingId, Is.Null);
            Assert.That(session.ActiveSequenceId, Is.Null);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(session.IsPlayerLocked, Is.False);
        }

        [Test]
        public void FailedEnding_RollsBackCompletedEffectsAndPersistsThePreEndingSnapshot()
        {
            var effect = new AdjustGardenCounterEffect();
            SetField(effect, "target", GardenCounter.Blooms);
            SetField(effect, "delta", 5);
            var effectBeat = new TitleCardBeat();
            SetField(effectBeat, "text", "THE GARDEN TAKES ITS DUE");
            SetField(effectBeat, "onComplete", new List<StateEffect> { effect });

            SequenceBeat failingBeat = CreateSequenceBeat("ending_failure", false);
            GameSession session = CreateSession(effectBeat, out NarrativeState originalState);
            originalState.Garden.Adjust(GardenCounter.Blooms, 3);
            session.RegisterSequencePlayer(new FailingSequencePlayer("ending_failure"));

            EndingDefinition ending = Track(ScriptableObject.CreateInstance<EndingDefinition>());
            SetField(ending, "id", "confession");
            SetField(ending, "title", "Confession");
            SetField(
                ending,
                "beats",
                new List<NarrativeBeat> { effectBeat, failingBeat });
            SetField(
                session.Catalog,
                "endings",
                new List<EndingDefinition> { ending });

            Assert.That(
                SaveGameStore.TrySave(originalState),
                Is.EqualTo(SaveWriteStatus.Succeeded));
            string preEndingJson = fileSystem.Files[SaveGameStore.SavePath];
            LogAssert.Expect(LogType.Error, "Injected ending sequence failure.");

            object result = CreateNarrativeOperationResult();
            RunToCompletion(InvokeRunEnding(session, ending, result));

            Assert.That(ResultSucceeded(result), Is.False);
            Assert.That(
                originalState.Garden.BloomCount,
                Is.EqualTo(8),
                "The first ending beat must complete before the injected later failure.");
            Assert.That(session.State, Is.Not.SameAs(originalState));
            Assert.That(session.State.Garden.BloomCount, Is.EqualTo(3));
            Assert.That(session.State.ChapterIndex, Is.EqualTo(1));
            Assert.That(session.State.BeatIndex, Is.Zero);
            Assert.That(session.CompletedEndingId, Is.Null);
            Assert.That(session.CurrentEndingId, Is.Null);
            Assert.That(
                fileSystem.Files[SaveGameStore.SavePath],
                Is.EqualTo(preEndingJson),
                "A failed ending must not checkpoint its partial effects.");

            InvokePrivate(session, "PersistState");

            Assert.That(
                SaveGameStore.TryDeserialize(
                    fileSystem.Files[SaveGameStore.SavePath],
                    out SaveGameData persisted),
                Is.True);
            Assert.That(persisted.bloomCount, Is.EqualTo(3));
            Assert.That(persisted.chapterIndex, Is.EqualTo(1));
            Assert.That(persisted.beatIndex, Is.Zero);
            Assert.That(persisted.completedEndingId, Is.Null.Or.Empty);
        }

        [Test]
        public void StopActiveRunner_CancelsSequenceAndClearsBothLocksAndContexts()
        {
            SequenceBeat beat = CreateSequenceBeat("boss_verdant_mirror", true);
            GameSession session = CreateSession(beat, out _);
            var sequencePlayer = new BlockingSequencePlayer("boss_verdant_mirror");
            session.RegisterSequencePlayer(sequencePlayer);
            InvokePrivate(session, "SetCurrentEnding", "sacrifice");

            IEnumerator sequenceRoutine = InvokePlaySequence(session, beat);
            Assert.That(sequenceRoutine.MoveNext(), Is.True);
            Assert.That(sequenceRoutine.Current, Is.Null);

            Assert.That(session.IsPlayerLocked, Is.True);
            Assert.That(session.IsNarrativeInputCaptured, Is.True);
            Assert.That(session.CurrentEndingId, Is.EqualTo("sacrifice"));
            Assert.That(session.ActiveSequenceId, Is.EqualTo("boss_verdant_mirror"));

            InvokePrivate(session, "StopActiveRunner");

            Assert.That(sequencePlayer.CancelCount, Is.EqualTo(1));
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(session.CurrentEndingId, Is.Null);
            Assert.That(session.ActiveSequenceId, Is.Null);

            (sequenceRoutine as IDisposable)?.Dispose();
        }

        private SequenceBeat CreateSequenceBeat(string sequenceId, bool locksPlayer)
        {
            var beat = new SequenceBeat();
            SetField(beat, "sequenceId", sequenceId);
            SetField(beat, "locksPlayer", locksPlayer);
            return beat;
        }

        private GameSession CreateSession(NarrativeBeat beat, out NarrativeState state)
        {
            ChapterDefinition chapter = Track(ScriptableObject.CreateInstance<ChapterDefinition>());
            SetField(chapter, "index", 1);
            SetField(chapter, "beats", new List<NarrativeBeat> { beat });

            NarrativeCatalog catalog = Track(ScriptableObject.CreateInstance<NarrativeCatalog>());
            SetField(catalog, "chapters", new List<ChapterDefinition> { chapter });

            GameObject sessionObject = Track(new GameObject("Chapter Runner Test Session"));
            GameSession session = sessionObject.AddComponent<GameSession>();
            session.Configure(catalog, null);

            state = new NarrativeState(NameVariant.Laura, 0)
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

        private static IEnumerator InvokeRunChapter(
            GameSession session,
            ChapterDefinition chapter)
        {
            MethodInfo method = typeof(GameSession).GetMethod(
                "RunChapter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (IEnumerator)method.Invoke(session, new object[] { chapter });
        }

        private static IEnumerator InvokeRunEnding(
            GameSession session,
            EndingDefinition ending,
            object result)
        {
            object runner = GetRunner(session);
            MethodInfo method = runner.GetType().GetMethod(
                "RunEnding",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (IEnumerator)method.Invoke(runner, new[] { ending, result });
        }

        private static IEnumerator InvokePlaySequence(
            GameSession session,
            SequenceBeat beat)
        {
            object runner = GetRunner(session);
            MethodInfo method = runner.GetType().GetMethod(
                "PlaySequence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object result = CreateNarrativeOperationResult();
            return (IEnumerator)method.Invoke(runner, new[] { (object)beat, result });
        }

        private static object GetRunner(GameSession session)
        {
            PropertyInfo property = typeof(GameSession).GetProperty(
                "Runner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(session);
        }

        private static object CreateNarrativeOperationResult()
        {
            Type resultType = typeof(GameSession).Assembly.GetType(
                "Hortensia.Runtime.NarrativeOperationResult");
            Assert.That(resultType, Is.Not.Null);
            return Activator.CreateInstance(resultType, true);
        }

        private static bool ResultSucceeded(object result)
        {
            PropertyInfo property = result.GetType().GetProperty(
                "Succeeded",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(result);
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static void RunToCompletion(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            int steps = 0;

            while (stack.Count > 0)
            {
                Assert.That(
                    ++steps,
                    Is.LessThan(1000),
                    "Coroutine did not finish in the synchronous test driver.");
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

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            Type type = target.GetType();
            while (field == null && type != null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class RecordingSequencePlayer : ISequencePlayer
        {
            private readonly GameSession session;

            public RecordingSequencePlayer(GameSession session, string sequenceId)
            {
                this.session = session;
                SequenceId = sequenceId;
            }

            public string SequenceId { get; }
            public SequenceLockFlags RequiredLocks => SequenceLockFlags.None;
            public int PlayCount { get; private set; }
            public bool LockedAtStart { get; private set; }
            public bool LockedBeforeFinish { get; private set; }

            public IEnumerator Play(SequencePlaybackHandle playback)
            {
                PlayCount++;
                LockedAtStart = session.IsPlayerLocked;
                yield return null;
                LockedBeforeFinish = session.IsPlayerLocked;
            }
        }

        private sealed class ContextRecordingSequencePlayer : ISequencePlayer
        {
            private readonly GameSession session;

            public ContextRecordingSequencePlayer(GameSession session, string sequenceId)
            {
                this.session = session;
                SequenceId = sequenceId;
            }

            public string SequenceId { get; }
            public SequenceLockFlags RequiredLocks => SequenceLockFlags.None;
            public string EndingAtStart { get; private set; }
            public string SequenceAtStart { get; private set; }
            public string EndingBeforeFinish { get; private set; }
            public string SequenceBeforeFinish { get; private set; }

            public IEnumerator Play(SequencePlaybackHandle playback)
            {
                EndingAtStart = session.CurrentEndingId;
                SequenceAtStart = session.ActiveSequenceId;
                yield return null;
                EndingBeforeFinish = session.CurrentEndingId;
                SequenceBeforeFinish = session.ActiveSequenceId;
            }
        }

        private sealed class BlockingSequencePlayer : ISequencePlayer
        {
            public BlockingSequencePlayer(string sequenceId)
            {
                SequenceId = sequenceId;
            }

            public string SequenceId { get; }
            public SequenceLockFlags RequiredLocks => SequenceLockFlags.NarrativeInput;
            public int CancelCount { get; private set; }

            public IEnumerator Play(SequencePlaybackHandle playback)
            {
                playback.RegisterCleanup(() => CancelCount++);
                while (!playback.IsCancellationRequested)
                    yield return null;
            }
        }

        private sealed class FailingSequencePlayer : ISequencePlayer
        {
            public FailingSequencePlayer(string sequenceId)
            {
                SequenceId = sequenceId;
            }

            public string SequenceId { get; }
            public SequenceLockFlags RequiredLocks => SequenceLockFlags.None;

            public IEnumerator Play(SequencePlaybackHandle playback)
            {
                playback.Fail("Injected ending sequence failure.");
                yield break;
            }
        }

        private sealed class LockObservingPresenter : INarrativePresenter
        {
            private readonly GameSession session;

            public LockObservingPresenter(GameSession session)
            {
                this.session = session;
            }

            public int PresentedLines { get; private set; }
            public bool PlayerWasLocked { get; private set; }

            public IEnumerator PresentLine(
                ResolvedLine line,
                string speakerName,
                float visionIntensity)
            {
                PresentedLines++;
                PlayerWasLocked = session.IsPlayerLocked;
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
            }

            public void ClearPresentation()
            {
            }
        }

        private sealed class RecordingPatientVisualPresenter : IPatientVisualPresenter
        {
            public readonly List<PatientDefinition> ShownPatients =
                new List<PatientDefinition>();
            public int ClearCount { get; private set; }

            public void ShowPatient(PatientDefinition patient, float baselineVisionIntensity)
            {
                ShownPatients.Add(patient);
            }

            public void ClearPatient()
            {
                ClearCount++;
            }

            // This test double swaps synchronously (no real transition to
            // wait for), matching the pre-Vision-Bleed behaviour.
            public bool IsTransitioning => false;
        }

        private sealed class MemorySaveFileSystem : ISaveGameFileSystem
        {
            public readonly Dictionary<string, string> Files =
                new Dictionary<string, string>();

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
                Files.Remove(path);
            }
        }
    }
}