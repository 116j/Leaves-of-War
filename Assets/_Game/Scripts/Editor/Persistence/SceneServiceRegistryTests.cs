using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class SceneServiceRegistryTests
    {
        private GameObject spawnObject;
        private GameObject holderObject;
        private GameObject playerObject;
        private GameObject sessionObject;

        [TearDown]
        public void TearDown()
        {
            if (sessionObject != null)
                Object.DestroyImmediate(sessionObject);
            if (holderObject != null)
                Object.DestroyImmediate(holderObject);
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
            if (spawnObject != null)
                Object.DestroyImmediate(spawnObject);

            Assert.That(GameSession.Instance, Is.Null);
        }

        [UnityTest]
        public IEnumerator ComponentsEnabledBeforeSession_RegisterWhenSessionAppears()
        {
            yield return new EnterPlayMode();

            Assert.That(GameSession.Instance, Is.Null);
            spawnObject = new GameObject("Pre-session Spawn");
            SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
            SetSerializedString(spawn, "id", "foyer");
            holderObject = new GameObject("Pre-session Holder");
            CarriedItemHolder holder = holderObject.AddComponent<CarriedItemHolder>();
            playerObject = new GameObject("Pre-session Player");
            playerObject.SetActive(false);
            playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Pre-session Player Camera", typeof(Camera));
            cameraObject.transform.SetParent(playerObject.transform, false);
            Camera playerCamera = cameraObject.GetComponent<Camera>();
            playerCamera.enabled = false;
            FirstPersonController player = playerObject.AddComponent<FirstPersonController>();
            playerObject.SetActive(true);

            sessionObject = new GameObject("Scene Service Session");
            GameSession session = sessionObject.AddComponent<GameSession>();

            Assert.That(
                session.SceneServices.TryGetSpawnPoint(
                    "foyer",
                    out SpawnPoint registeredSpawn,
                    out int spawnCount),
                Is.True);
            Assert.That(spawnCount, Is.EqualTo(1));
            Assert.That(registeredSpawn, Is.SameAs(spawn));
            Assert.That(
                session.SceneServices.TryGetCarriedItemHolder(
                    out CarriedItemHolder registeredHolder,
                    out int holderCount),
                Is.True);
            Assert.That(holderCount, Is.EqualTo(1));
            Assert.That(registeredHolder, Is.SameAs(holder));
            Assert.That(
                session.SceneServices.TryGetPlayer(
                    out FirstPersonController registeredPlayer,
                    out int playerCount),
                Is.True);
            Assert.That(playerCount, Is.EqualTo(1));
            Assert.That(registeredPlayer, Is.SameAs(player));
            Assert.That(
                session.SceneServices.TryGetPlayerCamera(
                    out Camera registeredCamera,
                    out int cameraCount),
                Is.True);
            Assert.That(cameraCount, Is.EqualTo(1));
            Assert.That(registeredCamera, Is.SameAs(playerCamera));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }

        [Test]
        public void DuplicateSequenceIds_RemainAmbiguousUntilOneUnregisters()
        {
            var registry = new SceneServiceRegistry();
            var first = new FakeSequencePlayer("shared_sequence");
            var second = new FakeSequencePlayer("shared_sequence");
            var matches = new System.Collections.Generic.List<ISequencePlayer>();

            registry.RegisterSequencePlayer(first);
            registry.RegisterSequencePlayer(second);

            Assert.That(
                registry.FindSequencePlayers("shared_sequence", matches),
                Is.EqualTo(2));
            Assert.That(matches, Is.EquivalentTo(new[] { first, second }));

            registry.UnregisterSequencePlayer(first);
            matches.Clear();

            Assert.That(
                registry.FindSequencePlayers("shared_sequence", matches),
                Is.EqualTo(1));
            Assert.That(matches[0], Is.SameAs(second));
        }

        [Test]
        public void DestroyedUnityServices_ArePrunedInsteadOfReturned()
        {
            var registry = new SceneServiceRegistry();
            spawnObject = new GameObject("Disposable Spawn");
            SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
            SetSerializedString(spawn, "id", "arrival");
            registry.RegisterSpawnPoint(spawn);

            Object.DestroyImmediate(spawnObject);
            spawnObject = null;

            Assert.That(
                registry.TryGetSpawnPoint("arrival", out _, out int matchCount),
                Is.False);
            Assert.That(matchCount, Is.Zero);
        }

        private static void SetSerializedString(
            Object target,
            string propertyName,
            string value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class FakeSequencePlayer : ISequencePlayer
        {
            public FakeSequencePlayer(string sequenceId)
            {
                SequenceId = sequenceId;
            }

            public string SequenceId { get; }
            public SequenceLockFlags RequiredLocks => SequenceLockFlags.None;

            public IEnumerator Play(SequencePlaybackHandle playback)
            {
                yield break;
            }
        }
    }
}
