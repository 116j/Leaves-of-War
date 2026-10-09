using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class AssetlessSequencePlayerTests
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
        public void GreyboxSequencePlayer_CanonicalRevealWaitsForInputAndLeavesVisibleResult()
        {
            CreateOutputCanvas();
            GameSession session = CreateSession();
            GameObject reveal = Track(new GameObject("Study 1910 Reveal"));
            reveal.SetActive(false);
            GreyboxSequencePlayer player = CreateGreyboxPlayer("study_1910_reveal", reveal);
            var input = new FakeSequenceInput { AdvancePressed = true };
            var clock = new FakeSequenceClock(1f);
            SequencePlaybackHandle handle = BeginPlayback(session, player, clock, input);

            RunToCompletion(player.Play(handle));
            handle.Complete();

            Assert.That(reveal.activeSelf, Is.True);
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(FindOverlay("Asset-less Sequence — study_1910_reveal"), Is.Null);
        }

        [Test]
        public void GreyboxSequencePlayer_CancelRestoresAttackGeometryAndInputCapture()
        {
            CreateOutputCanvas();
            GameSession session = CreateSession();
            GameObject cameraObject = Track(new GameObject("Sequence Camera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.6f, -3f);
            float originalFieldOfView = cameraObject.GetComponent<Camera>().fieldOfView;
            RegisterPlayerCamera(session, cameraObject.GetComponent<Camera>());

            GameObject reveal = Track(new GameObject("Attack Dream Reveal"));
            GameObject thorn = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            thorn.name = "Reaching Thorn";
            thorn.transform.SetParent(reveal.transform, false);
            thorn.transform.localPosition = new Vector3(1f, 0.5f, 5f);
            Vector3 originalPosition = thorn.transform.localPosition;
            Vector3 originalScale = thorn.transform.localScale;
            reveal.SetActive(false);

            GreyboxSequencePlayer player = CreateGreyboxPlayer("dream_3_attack", reveal);
            var input = new FakeSequenceInput();
            var clock = new FakeSequenceClock(0.6f);
            SequencePlaybackHandle handle = BeginPlayback(session, player, clock, input);
            IEnumerator playback = player.Play(handle);
            Assert.That(playback.MoveNext(), Is.True);
            IEnumerator staging = playback.Current as IEnumerator;
            Assert.That(staging, Is.Not.Null);
            Assert.That(staging.MoveNext(), Is.True);
            IEnumerator motion = staging.Current as IEnumerator;
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.MoveNext(), Is.True);

            Assert.That(session.IsNarrativeInputCaptured, Is.True);
            Assert.That(reveal.activeSelf, Is.True);
            Assert.That(thorn.transform.localPosition, Is.Not.EqualTo(originalPosition));
            Assert.That(FindOverlay("Asset-less Sequence — dream_3_attack"), Is.Not.Null);

            handle.Cancel();
            (playback as IDisposable)?.Dispose();

            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(reveal.activeSelf, Is.False);
            Assert.That(thorn.transform.localPosition, Is.EqualTo(originalPosition));
            Assert.That(thorn.transform.localScale, Is.EqualTo(originalScale));
            Assert.That(cameraObject.GetComponent<Camera>().fieldOfView, Is.EqualTo(originalFieldOfView));
            Assert.That(FindOverlay("Asset-less Sequence — dream_3_attack"), Is.Null);
        }

        [Test]
        public void GreyboxSequencePlayer_WitheringMovesBrownedBloomsAndKeepsThemRevealed()
        {
            CreateOutputCanvas();
            GameSession session = CreateSession();
            GameObject reveal = Track(new GameObject("Browned Hydrangeas"));
            GameObject bloom = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            bloom.name = "Dead Bloom";
            bloom.transform.SetParent(reveal.transform, false);
            bloom.transform.localPosition = new Vector3(0f, 1f, 0f);
            reveal.SetActive(false);

            GreyboxSequencePlayer player = CreateGreyboxPlayer("blooms_brown_and_fall", reveal);
            var input = new FakeSequenceInput { AdvancePressed = true };
            var clock = new FakeSequenceClock(1f);
            SequencePlaybackHandle handle = BeginPlayback(session, player, clock, input);
            RunToCompletion(player.Play(handle));
            handle.Complete();

            Assert.That(reveal.activeSelf, Is.True);
            Assert.That(bloom.transform.localPosition.y, Is.LessThan(0.5f));
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [Test]
        public void GreyboxSequencePlayer_DenialRestoresTemporaryCameraAfterCompletion()
        {
            CreateOutputCanvas();
            GameSession session = CreateSession();
            GameObject cameraObject = Track(new GameObject("Denial Camera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 1.6f, -3f),
                Quaternion.identity);
            Vector3 originalPosition = cameraObject.transform.position;
            Quaternion originalRotation = cameraObject.transform.rotation;
            RegisterPlayerCamera(session, cameraObject.GetComponent<Camera>());

            GameObject reveal = Track(new GameObject("Portrait Passage"));
            GameObject opening = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            opening.name = "Dark Opening Within Living Portrait";
            opening.transform.SetParent(reveal.transform, false);
            opening.transform.localPosition = new Vector3(0f, 1.6f, 5f);
            reveal.SetActive(false);

            GreyboxSequencePlayer player = CreateGreyboxPlayer("walks_into_the_painting", reveal);
            var input = new FakeSequenceInput { AdvancePressed = true };
            var clock = new FakeSequenceClock(1f);
            SequencePlaybackHandle handle = BeginPlayback(session, player, clock, input);
            RunToCompletion(player.Play(handle));
            handle.Complete();

            Assert.That(reveal.activeSelf, Is.True);
            Assert.That(cameraObject.transform.position, Is.EqualTo(originalPosition));
            Assert.That(cameraObject.transform.rotation, Is.EqualTo(originalRotation));
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [TestCase("credits_confession", "CONFESSION")]
        [TestCase("credits_denial", "DENIAL")]
        [TestCase("credits_sacrifice", "SACRIFICE")]
        public void CreditsSequencePlayer_UsesExactIdTreatmentAndCompletesWithoutLeaks(
            string sequenceId,
            string expectedTitle)
        {
            CreateOutputCanvas();
            GameSession session = CreateSession();
            var input = new FakeSequenceInput();
            var clock = new FakeSequenceClock(0.5f);
            CreditsSequencePlayer player = CreateCreditsPlayer(sequenceId);
            SequencePlaybackHandle handle = BeginPlayback(session, player, clock, input);

            IEnumerator playback = player.Play(handle);
            Assert.That(playback.MoveNext(), Is.True);
            GameObject overlay = FindOverlay($"Text-only Credits — {expectedTitle}");
            Assert.That(overlay, Is.Not.Null);
            TMP_Text heading = overlay
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "Heading");
            TMP_Text body = overlay
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "Body");
            Assert.That(heading.text, Is.EqualTo(expectedTitle));
            Assert.That(body.text, Does.Contain("CREDITS — ART/ROSTER PENDING"));
            Assert.That(body.text, Does.Not.Contain("John Doe"));
            Assert.That(session.IsNarrativeInputCaptured, Is.True);

            input.AdvancePressed = true;
            RunToCompletion(playback);
            handle.Complete();

            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(FindOverlay($"Text-only Credits — {expectedTitle}"), Is.Null);
            Assert.That(CreditsSequencePlayer.SupportsSequenceId(sequenceId), Is.True);
        }

        [Test]
        public void CreditsSequencePlayer_CancelRemovesUiAndReleasesNarrativeInput()
        {
            CreateOutputCanvas();
            GameSession session = CreateSession();
            var input = new FakeSequenceInput();
            var clock = new FakeSequenceClock(0.1f);
            CreditsSequencePlayer player = CreateCreditsPlayer("credits_denial");
            SequencePlaybackHandle handle = BeginPlayback(session, player, clock, input);

            IEnumerator playback = player.Play(handle);
            Assert.That(playback.MoveNext(), Is.True);
            Assert.That(session.IsNarrativeInputCaptured, Is.True);
            Assert.That(FindOverlay("Text-only Credits — DENIAL"), Is.Not.Null);

            handle.Cancel();
            (playback as IDisposable)?.Dispose();

            Assert.That(session.IsNarrativeInputCaptured, Is.False);
            Assert.That(session.IsPlayerLocked, Is.False);
            Assert.That(FindOverlay("Text-only Credits — DENIAL"), Is.Null);
        }

        private GreyboxSequencePlayer CreateGreyboxPlayer(
            string sequenceId,
            GameObject reveal)
        {
            GameObject playerObject = Track(new GameObject("Greybox Sequence Player"));
            playerObject.SetActive(false);
            GreyboxSequencePlayer player = playerObject.AddComponent<GreyboxSequencePlayer>();
            SetField(player, "sequenceId", sequenceId);
            SetField(player, "revealRoot", reveal);
            SetField(player, "unscaledHoldSeconds", 0.1f);
            playerObject.SetActive(true);
            return player;
        }

        private CreditsSequencePlayer CreateCreditsPlayer(string sequenceId)
        {
            GameObject playerObject = Track(new GameObject("Credits Sequence Player"));
            playerObject.SetActive(false);
            CreditsSequencePlayer player = playerObject.AddComponent<CreditsSequencePlayer>();
            SetField(player, "sequenceId", sequenceId);
            SetField(player, "minimumHoldSeconds", 0.25f);
            SetField(player, "scrollDurationSeconds", 2f);
            playerObject.SetActive(true);
            return player;
        }

        private void RegisterPlayerCamera(GameSession session, Camera camera)
        {
            GameObject playerObject = Track(new GameObject("Sequence Test Player"));
            playerObject.SetActive(false);
            FirstPersonController controller = playerObject.AddComponent<FirstPersonController>();
            session.SceneServices.RegisterPlayer(controller, camera);
        }

        private static SequencePlaybackHandle BeginPlayback(
            GameSession session,
            ISequencePlayer player,
            ISequenceClock clock,
            ISequenceInput input)
        {
            var handle = new SequencePlaybackHandle(
                session,
                player.SequenceId,
                null,
                player.RequiredLocks,
                clock,
                input);
            handle.Begin();
            return handle;
        }

        private GameSession CreateSession()
        {
            Assert.That(GameSession.Instance, Is.Null, "A previous test left a GameSession alive.");
            GameObject sessionObject = Track(new GameObject("Asset-less Sequence Test Session"));
            return sessionObject.AddComponent<GameSession>();
        }

        private void CreateOutputCanvas()
        {
            GameObject canvasObject = Track(new GameObject(
                "Sequence Test Output",
                typeof(RectTransform),
                typeof(Canvas)));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject output = Track(new GameObject("World Output", typeof(RectTransform)));
            output.SetActive(false);
            output.AddComponent<CanvasRenderer>();
            output.AddComponent<RawImage>();
            output.AddComponent<LowResolutionPresenter>();
            output.transform.SetParent(canvasObject.transform, false);
            output.SetActive(true);
        }

        private static void RunToCompletion(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            int steps = 0;
            while (stack.Count > 0)
            {
                Assert.That(++steps, Is.LessThan(1000), "Sequence did not reach its completion condition.");
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }

        private static GameObject FindOverlay(string name)
        {
            GameObject found = GameObject.Find(name);
            return found;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private T Track<T>(T target) where T : Object
        {
            createdObjects.Add(target);
            return target;
        }

        private sealed class FakeSequenceClock : ISequenceClock
        {
            public FakeSequenceClock(float unscaledDeltaTime)
            {
                UnscaledDeltaTime = unscaledDeltaTime;
            }

            public float UnscaledTime { get; set; }
            public float UnscaledDeltaTime { get; set; }
        }

        private sealed class FakeSequenceInput : ISequenceInput
        {
            public bool AdvancePressed { get; set; }
            public bool PointerIsCaptured => false;

            public bool WasPressed(SequenceInputAction action) =>
                action == SequenceInputAction.Advance && AdvancePressed;
        }
    }
}
