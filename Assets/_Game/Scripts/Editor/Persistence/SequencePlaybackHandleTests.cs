using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class SequencePlaybackHandleTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }
            createdObjects.Clear();
        }

        [Test]
        public void Complete_ReleasesOwnedLocksAndRunsCleanupExactlyOnce()
        {
            GameSession session = CreateSession();
            SequencePlaybackHandle playback = CreatePlayback(
                session,
                SequenceLockFlags.PlayerMovement | SequenceLockFlags.NarrativeInput);
            int cleanupCount = 0;
            playback.RegisterCleanup(() => cleanupCount++);

            playback.Begin();
            Assert.That(playback.Status, Is.EqualTo(SequencePlaybackStatus.Running));
            Assert.That(session.IsPlayerLocked, Is.True);
            Assert.That(session.IsNarrativeInputCaptured, Is.True);

            playback.Complete();
            playback.Cancel();
            playback.Dispose();

            Assert.That(playback.Status, Is.EqualTo(SequencePlaybackStatus.Completed));
            Assert.That(cleanupCount, Is.EqualTo(1));
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [Test]
        public void Cancel_IsIdempotentAndRecordsCancellation()
        {
            GameSession session = CreateSession();
            SequencePlaybackHandle playback = CreatePlayback(
                session,
                SequenceLockFlags.PlayerMovement | SequenceLockFlags.NarrativeInput);
            int cleanupCount = 0;
            playback.RegisterCleanup(() => cleanupCount++);
            playback.Begin();

            playback.Cancel();
            playback.Cancel();

            Assert.That(playback.Status, Is.EqualTo(SequencePlaybackStatus.Cancelled));
            Assert.That(playback.IsCancellationRequested, Is.True);
            Assert.That(cleanupCount, Is.EqualTo(1));
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [Test]
        public void Fail_PreservesDiagnosticAndReleasesEveryLease()
        {
            GameSession session = CreateSession();
            SequencePlaybackHandle playback = CreatePlayback(
                session,
                SequenceLockFlags.PlayerMovement | SequenceLockFlags.NarrativeInput);
            playback.Begin();

            playback.Fail("arena could not initialize");

            Assert.That(playback.Status, Is.EqualTo(SequencePlaybackStatus.Failed));
            Assert.That(playback.Error, Is.EqualTo("arena could not initialize"));
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [Test]
        public void ReleaseNarrativeInput_AllowsAuthoredInteractionAndRetainsPlayerLease()
        {
            GameSession session = CreateSession();
            SequencePlaybackHandle playback = CreatePlayback(
                session,
                SequenceLockFlags.PlayerMovement | SequenceLockFlags.NarrativeInput);
            playback.Begin();

            playback.ReleaseNarrativeInput();
            playback.ReleaseNarrativeInput();

            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(session.IsPlayerLocked, Is.True);

            playback.Complete();
            Assert.That(session.IsPlayerLocked, Is.False);
        }

        [Test]
        public void CleanupRegisteredAfterCompletion_RunsImmediately()
        {
            GameSession session = CreateSession();
            SequencePlaybackHandle playback = CreatePlayback(
                session,
                SequenceLockFlags.None);
            playback.Begin();
            playback.Complete();
            int cleanupCount = 0;

            playback.RegisterCleanup(() => cleanupCount++);

            Assert.That(cleanupCount, Is.EqualTo(1));
        }

        private GameSession CreateSession()
        {
            var gameObject = new GameObject("Sequence Playback Handle Test Session");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<GameSession>();
        }

        private static SequencePlaybackHandle CreatePlayback(
            GameSession session,
            SequenceLockFlags locks)
        {
            return new SequencePlaybackHandle(
                session,
                "test_sequence",
                "confession",
                locks,
                new FixedClock(),
                new NoInput());
        }

        private sealed class FixedClock : ISequenceClock
        {
            public float UnscaledTime => 1f;
            public float UnscaledDeltaTime => 0.1f;
        }

        private sealed class NoInput : ISequenceInput
        {
            public bool PointerIsCaptured => true;
            public bool WasPressed(SequenceInputAction action) => false;
        }
    }
}
