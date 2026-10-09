using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class NarrativePresenterPlaybackTests
    {
        private const string TwoChunkLine =
            "The first subtitle frame ends here. The second shares its recording.";

        [UnityTest]
        public IEnumerator PresentLine_PreservesVoiceBetweenChunksAndStopsAtTheLineBoundary()
        {
            yield return new EnterPlayMode();

            AudioClip clip = null;
            PresentationSettings settings = null;
            GameObject outputCanvasObject = null;
            GameObject cameraObject = null;
            GameObject presenterObject = null;
            GameObject sessionObject = null;

            try
            {
                sessionObject = new GameObject("Narrative Presenter Playback Session");
                GameSession session = sessionObject.AddComponent<GameSession>();
                outputCanvasObject = CreateOutputCanvas();
                cameraObject = new GameObject("Narrative Presenter Test Camera", typeof(Camera));
                cameraObject.GetComponent<Camera>().enabled = false;

                settings = CreateSettings();
                var clock = new ManualSequenceClock();
                var input = new ManualSequenceInput();
                session.ConfigureSequenceServices(clock, input);
                presenterObject = new GameObject("Narrative Presenter Playback Test");
                presenterObject.SetActive(false);
                NarrativePresenter presenter = presenterObject.AddComponent<NarrativePresenter>();
                SetObjectReference(presenter, "lowResolutionCamera", cameraObject.GetComponent<Camera>());
                presenter.Configure(settings, clock, input);
                presenterObject.SetActive(true);
                yield return null;

                IReadOnlyList<LinePresentationChunk> chunks =
                    LineChunker.Split(TwoChunkLine, settings.ChunkCharacterLimit);
                Assert.That(chunks, Has.Count.EqualTo(2), "The regression fixture must exercise two text frames.");

                clip = AudioClip.Create("Two Chunk Voice Test", 80000, 1, 8000, false);
                var line = new ResolvedLine("test_two_chunk_voice", TwoChunkLine, clip);
                AudioSource voiceSource = presenter.GetComponent<AudioSource>();
                AudioRoutingSettings routing =
                    Resources.Load<AudioRoutingSettings>(AudioRoutingSettings.ResourcePath);
                Assert.That(AudioManager.Instance.IsConfigured, Is.True);
                Assert.That(routing, Is.Not.Null);
                Assert.That(
                    voiceSource.outputAudioMixerGroup,
                    Is.SameAs(routing.VoiceGroup),
                    "The facade-owned narration source must remain routed to AudioBus.Voice.");
                TMP_Text subtitle = presenter
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "Line");
                GameObject subtitlePanel = subtitle.transform.parent.gameObject;

                IEnumerator lineRoutine = presenter.PresentLine(line, "Test", 0f);
                Assert.That(lineRoutine.MoveNext(), Is.True);
                var firstChunk = (IEnumerator)lineRoutine.Current;
                Assert.That(firstChunk.MoveNext(), Is.True);
                var firstReveal = (IEnumerator)firstChunk.Current;
                Assert.That(firstReveal.MoveNext(), Is.True);
                Assert.That(subtitle.text, Is.EqualTo(chunks[0].Text));

                voiceSource.timeSamples = 16000;
                Assert.That(voiceSource.timeSamples, Is.GreaterThan(0));

                clock.UnscaledTime = 1f;
                input.AdvancePressed = true;
                Assert.That(firstReveal.MoveNext(), Is.False);
                input.AdvancePressed = false;
                Assert.That(firstChunk.MoveNext(), Is.True);
                var secondReveal = (IEnumerator)firstChunk.Current;
                Assert.That(secondReveal.MoveNext(), Is.True);
                Assert.That(subtitle.text, Is.EqualTo(chunks[1].Text));

                Assert.That(voiceSource.clip, Is.SameAs(clip));
                Assert.That(
                    voiceSource.timeSamples,
                    Is.GreaterThan(0),
                    "Advancing an intermediate text frame must not stop the authored line's clip.");

                clock.UnscaledTime = 2f;
                input.AdvancePressed = true;
                Assert.That(secondReveal.MoveNext(), Is.False);
                input.AdvancePressed = false;
                Assert.That(firstChunk.MoveNext(), Is.False);
                Assert.That(lineRoutine.MoveNext(), Is.False);

                Assert.That(voiceSource.clip, Is.Null);
                Assert.That(voiceSource.isPlaying, Is.False);
                Assert.That(subtitlePanel.activeSelf, Is.False);

                IEnumerator cancelledLine = presenter.PresentLine(line, "Test", 0f);
                Assert.That(cancelledLine.MoveNext(), Is.True);
                var cancelledChunk = (IEnumerator)cancelledLine.Current;
                Assert.That(cancelledChunk.MoveNext(), Is.True);
                presenter.ClearPresentation();

                Assert.That(voiceSource.clip, Is.Null);
                Assert.That(voiceSource.isPlaying, Is.False);
                Assert.That(subtitlePanel.activeSelf, Is.False);
            }
            finally
            {
                if (presenterObject != null)
                    Object.Destroy(presenterObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (outputCanvasObject != null)
                    Object.Destroy(outputCanvasObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (clip != null)
                    Object.Destroy(clip);
                if (settings != null)
                    Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator PresentLine_SegmentedVoiceFallsBackWhenItsAudioTimelineStalls()
        {
            yield return new EnterPlayMode();

            AudioClip clip = null;
            PresentationSettings settings = null;
            GameObject outputCanvasObject = null;
            GameObject cameraObject = null;
            GameObject presenterObject = null;
            GameObject sessionObject = null;

            try
            {
                sessionObject = new GameObject("Narrative Presenter Segmented Playback Session");
                GameSession session = sessionObject.AddComponent<GameSession>();
                outputCanvasObject = CreateOutputCanvas();
                cameraObject = new GameObject("Narrative Presenter Segmented Test Camera", typeof(Camera));
                cameraObject.GetComponent<Camera>().enabled = false;

                settings = CreateSettings();
                var clock = new ManualSequenceClock();
                var input = new ManualSequenceInput();
                session.ConfigureSequenceServices(clock, input);
                presenterObject = new GameObject("Narrative Presenter Segmented Playback Test");
                presenterObject.SetActive(false);
                NarrativePresenter presenter = presenterObject.AddComponent<NarrativePresenter>();
                SetObjectReference(presenter, "lowResolutionCamera", cameraObject.GetComponent<Camera>());
                presenter.Configure(settings, clock, input);
                presenterObject.SetActive(true);
                yield return null;

                clip = AudioClip.Create("Stalled Segmented Voice Test", 32000, 1, 8000, false);
                var line = new ResolvedLine(
                    "test_stalled_segmented_voice",
                    "FIRST|SECOND",
                    clip,
                    new[]
                    {
                        new SegmentTime { start = 0f, end = 1f },
                        new SegmentTime { start = 1f, end = 2f }
                    });
                AudioSource voiceSource = presenter.GetComponent<AudioSource>();
                TMP_Text subtitle = presenter
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "Line");

                IEnumerator lineRoutine = presenter.PresentLine(line, "Test", 0f);
                Assert.That(lineRoutine.MoveNext(), Is.True);
                var segmentedRoutine = (IEnumerator)lineRoutine.Current;
                Assert.That(segmentedRoutine, Is.Not.Null);
                Assert.That(segmentedRoutine.MoveNext(), Is.True);
                var firstReveal = (IEnumerator)segmentedRoutine.Current;
                Assert.That(firstReveal, Is.Not.Null);
                Assert.That(firstReveal.MoveNext(), Is.True);

                // Complete the text crawl, then deliberately reset AudioSource.time
                // before each segment-boundary tick. The source remains playing,
                // reproducing the batch-mode state that previously held the input
                // lock indefinitely.
                clock.UnscaledTime = 0.25f;
                input.AdvancePressed = true;
                Assert.That(firstReveal.MoveNext(), Is.False);
                input.AdvancePressed = false;
                Assert.That(subtitle.text, Is.EqualTo("FIRST"));
                Assert.That(voiceSource.isPlaying, Is.True,
                    "The stalled-timeline fixture requires an active voice source.");

                voiceSource.time = 0f;
                Assert.That(segmentedRoutine.MoveNext(), Is.True);

                clock.UnscaledTime = 1.1f;
                voiceSource.time = 0f;
                Assert.That(segmentedRoutine.MoveNext(), Is.True,
                    "The clock watchdog should leave the frozen audio boundary for input.");

                input.AdvancePressed = true;
                Assert.That(segmentedRoutine.MoveNext(), Is.True);
                input.AdvancePressed = false;
                var secondReveal = (IEnumerator)segmentedRoutine.Current;
                Assert.That(secondReveal, Is.Not.Null,
                    "The segment should advance after its authored duration even when audio time freezes.");
                Assert.That(secondReveal.MoveNext(), Is.True);
                Assert.That(subtitle.text, Is.EqualTo("SECOND"));

                clock.UnscaledTime = 1.35f;
                input.AdvancePressed = true;
                Assert.That(secondReveal.MoveNext(), Is.False);
                input.AdvancePressed = false;

                voiceSource.time = 1f;
                Assert.That(segmentedRoutine.MoveNext(), Is.True);

                clock.UnscaledTime = 2.2f;
                voiceSource.time = 1f;
                Assert.That(segmentedRoutine.MoveNext(), Is.True,
                    "The final frozen segment should also reach its input boundary.");

                input.AdvancePressed = true;
                Assert.That(segmentedRoutine.MoveNext(), Is.False);
                input.AdvancePressed = false;
                Assert.That(lineRoutine.MoveNext(), Is.False);
                Assert.That(voiceSource.clip, Is.Null);
                Assert.That(voiceSource.isPlaying, Is.False);
            }
            finally
            {
                if (presenterObject != null)
                    Object.Destroy(presenterObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (outputCanvasObject != null)
                    Object.Destroy(outputCanvasObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (clip != null)
                    Object.Destroy(clip);
                if (settings != null)
                    Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator PresentDocument_IgnoresOpeningInputAndClosesOnlyOnCancel()
        {
            yield return new EnterPlayMode();

            PresentationSettings settings = null;
            GameObject outputCanvasObject = null;
            GameObject cameraObject = null;
            GameObject presenterObject = null;
            GameObject sessionObject = null;

            try
            {
                sessionObject = new GameObject("Narrative Presenter Document Session");
                GameSession session = sessionObject.AddComponent<GameSession>();
                outputCanvasObject = CreateOutputCanvas();
                cameraObject = new GameObject("Narrative Presenter Document Camera", typeof(Camera));
                cameraObject.GetComponent<Camera>().enabled = false;

                settings = CreateSettings();
                var clock = new ManualSequenceClock();
                var input = new ManualSequenceInput();
                session.ConfigureSequenceServices(clock, input);
                presenterObject = new GameObject("Narrative Presenter Document Test");
                presenterObject.SetActive(false);
                NarrativePresenter presenter = presenterObject.AddComponent<NarrativePresenter>();
                SetObjectReference(presenter, "lowResolutionCamera", cameraObject.GetComponent<Camera>());
                presenter.Configure(settings, clock, input);
                presenterObject.SetActive(true);
                yield return null;

                var document = new TestReadableDocument(
                    "Test Letter",
                    new[] { "FIRST PAGE", "SECOND PAGE" });
                bool closed = false;
                TMP_Text body = presenter
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "Body");

                // The click that triggered the interaction exists on the opening
                // frame. It must not also advance this reader to page two.
                input.NextPagePressed = true;
                IEnumerator routine = presenter.PresentDocument(document, () => closed = true);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(body.text, Is.EqualTo("FIRST PAGE"));
                Assert.That(presenter.IsDocumentOpen, Is.True);

                input.NextPagePressed = false;
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(body.text, Is.EqualTo("FIRST PAGE"));

                input.NextPagePressed = true;
                Assert.That(routine.MoveNext(), Is.True);
                input.NextPagePressed = false;
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(body.text, Is.EqualTo("SECOND PAGE"));

                // Next Page at the final page keeps the reader open; only the
                // document Cancel action closes it and allows the narrative to continue.
                input.NextPagePressed = true;
                Assert.That(routine.MoveNext(), Is.True);
                input.NextPagePressed = false;
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(body.text, Is.EqualTo("SECOND PAGE"));
                Assert.That(closed, Is.False);
                Assert.That(presenter.IsDocumentOpen, Is.True);

                input.CancelPressed = true;
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(closed, Is.True);
                Assert.That(presenter.IsDocumentOpen, Is.False);
            }
            finally
            {
                if (presenterObject != null)
                    Object.Destroy(presenterObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (outputCanvasObject != null)
                    Object.Destroy(outputCanvasObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (settings != null)
                    Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator DocumentReader_SizesEveryAuthoredPageInsideItsBodyViewport()
        {
            yield return new EnterPlayMode();

            PresentationSettings settings = null;
            GameObject outputCanvasObject = null;
            GameObject cameraObject = null;
            GameObject presenterObject = null;
            GameObject sessionObject = null;

            try
            {
                sessionObject = new GameObject("Narrative Presenter Document Layout Session");
                GameSession session = sessionObject.AddComponent<GameSession>();
                outputCanvasObject = CreateOutputCanvas();
                cameraObject = new GameObject("Narrative Presenter Document Layout Camera", typeof(Camera));
                cameraObject.GetComponent<Camera>().enabled = false;

                settings = CreateSettings();
                session.ConfigureSequenceServices(new ManualSequenceClock(), new ManualSequenceInput());
                presenterObject = new GameObject("Narrative Presenter Document Layout Test");
                presenterObject.SetActive(false);
                NarrativePresenter presenter = presenterObject.AddComponent<NarrativePresenter>();
                SetObjectReference(presenter, "lowResolutionCamera", cameraObject.GetComponent<Camera>());
                presenter.Configure(settings);
                presenterObject.SetActive(true);
                yield return null;

                NarrativeCatalog catalog = Resources.Load<NarrativeCatalog>("Narrative/NarrativeCatalog");
                Assert.That(catalog, Is.Not.Null);

                TMP_Text body = presenter
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "Body");
                RectTransform bodyRect = (RectTransform)body.transform;
                body.transform.parent.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                Assert.That(body.enableAutoSizing, Is.True);
                Assert.That(body.fontSizeMin, Is.GreaterThan(0f));

                foreach (DocumentDefinition document in catalog.Documents)
                {
                    foreach (string page in document.PagesFor(null, catalog))
                    {
                        body.text = NameVariants.Substitute(page, NameVariant.Laura);
                        body.ForceMeshUpdate();
                        Assert.That(
                            body.GetRenderedValues(false).y,
                            Is.LessThanOrEqualTo(bodyRect.rect.height + 0.01f),
                            $"{document.name} has a page that would overlap the reader controls.");
                        Assert.That(
                            body.isTextTruncated,
                            Is.False,
                            $"{document.name} must remain fully readable at the reader's minimum font size.");
                    }
                }
            }
            finally
            {
                if (presenterObject != null)
                    Object.Destroy(presenterObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (outputCanvasObject != null)
                    Object.Destroy(outputCanvasObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (settings != null)
                    Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator DiegeticFeedback_TracksCarryAndTaskEventsAndClearsOnCancellation()
        {
            yield return new EnterPlayMode();

            TaskObjective objective = null;
            CarryCategory category = null;
            GameObject sessionObject = null;
            GameObject holderObject = null;
            GameObject itemObject = null;
            GameObject outputCanvasObject = null;
            GameObject cameraObject = null;
            GameObject presenterObject = null;

            try
            {
                objective = ScriptableObject.CreateInstance<TaskObjective>();
                SetPrivateField(objective, "id", "unpacking");
                category = ScriptableObject.CreateInstance<CarryCategory>();
                SetPrivateField(category, "id", "books");
                SetPrivateField(category, "objective", objective);

                sessionObject = new GameObject("Narrative Presenter Feedback Session");
                GameSession session = sessionObject.AddComponent<GameSession>();
                var state = new NarrativeState(NameVariant.Laura, 0)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };
                state.Tasks.Register(objective, 5);
                InvokePrivate(session, "SetState", state);
                SetPrivateField(session, "saveBatchDepth", 1);

                holderObject = new GameObject("Feedback Holder");
                CarriedItemHolder holder = holderObject.AddComponent<CarriedItemHolder>();
                itemObject = new GameObject("Books");
                Carryable item = itemObject.AddComponent<Carryable>();
                SetPrivateField(item, "category", category);

                outputCanvasObject = CreateOutputCanvas();
                cameraObject = new GameObject("Feedback Camera", typeof(Camera));
                cameraObject.GetComponent<Camera>().enabled = false;
                presenterObject = new GameObject("Narrative Presenter Feedback Test");
                presenterObject.SetActive(false);
                NarrativePresenter presenter = presenterObject.AddComponent<NarrativePresenter>();
                SetObjectReference(presenter, "lowResolutionCamera", cameraObject.GetComponent<Camera>());
                presenterObject.SetActive(true);
                yield return null;

                Dictionary<string, TMP_Text> feedbackText = presenter
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Where(text => text.name == "Carrying" || text.name == "Status" ||
                        text.name == "GameplayHud")
                    .ToDictionary(text => text.name);

                Assert.That(holder.TryCarry(item), Is.True);
                Assert.That(feedbackText["Carrying"].gameObject.activeSelf, Is.True);
                Assert.That(feedbackText["Carrying"].text, Is.EqualTo("CARRYING: BOOKS"));

                state.Tasks.Report(objective);
                Assert.That(feedbackText["Status"].gameObject.activeSelf, Is.True);
                Assert.That(feedbackText["Status"].text, Is.EqualTo("UNPACKING 1/5"));

                presenter.SetGameplayHud("PLAYER 4/5\nMIRROR 3/6");
                Assert.That(feedbackText["GameplayHud"].gameObject.activeSelf, Is.True);
                Assert.That(
                    feedbackText["GameplayHud"].text,
                    Is.EqualTo("PLAYER 4/5\nMIRROR 3/6"));

                Assert.That(holder.DropCarriedItem(), Is.True);
                Assert.That(feedbackText["Carrying"].gameObject.activeSelf, Is.False);

                Assert.That(holder.TryCarry(item), Is.True);
                holder.enabled = false;
                Assert.That(feedbackText["Carrying"].gameObject.activeSelf, Is.False);

                presenter.ClearPresentation();
                Assert.That(feedbackText["Status"].gameObject.activeSelf, Is.False);
                Assert.That(feedbackText["GameplayHud"].gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (presenterObject != null)
                    Object.Destroy(presenterObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (outputCanvasObject != null)
                    Object.Destroy(outputCanvasObject);
                if (itemObject != null)
                    Object.Destroy(itemObject);
                if (holderObject != null)
                    Object.Destroy(holderObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (category != null)
                    Object.Destroy(category);
                if (objective != null)
                    Object.Destroy(objective);
            }
        }

        [UnityTest]
        public IEnumerator ObjectiveDirection_IsAHiddenNonBlockingTopRightDiegeticElement()
        {
            yield return new EnterPlayMode();

            GameObject outputCanvasObject = null;
            GameObject cameraObject = null;
            GameObject presenterObject = null;
            GameObject sessionObject = null;
            PresentationSettings settings = null;

            try
            {
                sessionObject = new GameObject("Objective Direction UI Session");
                sessionObject.AddComponent<GameSession>();
                outputCanvasObject = CreateOutputCanvas();
                cameraObject = new GameObject("Objective Direction UI Camera", typeof(Camera));
                cameraObject.GetComponent<Camera>().enabled = false;
                settings = CreateSettings();

                presenterObject = new GameObject("Objective Direction UI Presenter");
                presenterObject.SetActive(false);
                NarrativePresenter presenter = presenterObject.AddComponent<NarrativePresenter>();
                SetObjectReference(presenter, "lowResolutionCamera", cameraObject.GetComponent<Camera>());
                presenter.Configure(settings, new ManualSequenceClock(), new ManualSequenceInput());
                presenterObject.SetActive(true);
                yield return null;

                Graphic arrow = presenter.GetComponentsInChildren<Graphic>(true)
                    .Single(graphic => graphic.gameObject.name == "ObjectiveDirection");
                RectTransform rect = arrow.rectTransform;
                Canvas canvas = arrow.GetComponentInParent<Canvas>();
                TMP_Text gameplayHud = presenter.GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "GameplayHud");

                Assert.That(arrow.gameObject.activeSelf, Is.False,
                    "The guide remains hidden until a targetable objective is pending.");
                Assert.That(arrow.raycastTarget, Is.False,
                    "The guide must never capture an interaction or subtitle-advance click.");
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera),
                    "The guide belongs inside the point-filtered diegetic frame.");
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rect.anchoredPosition.x, Is.LessThan(0f));
                Assert.That(rect.anchoredPosition.y, Is.LessThan(0f));
                float arrowLeftInset = -rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f;
                float gameplayHudRightInset = -gameplayHud.rectTransform.offsetMax.x;
                Assert.That(gameplayHudRightInset, Is.GreaterThan(arrowLeftInset),
                    "The gameplay HUD must reserve a top-right gutter for the arrow.");
            }
            finally
            {
                if (presenterObject != null)
                    Object.Destroy(presenterObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (outputCanvasObject != null)
                    Object.Destroy(outputCanvasObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (settings != null)
                    Object.Destroy(settings);
            }
        }

        [Test]
        public void ObjectiveDirectionMath_UsesCameraRelativeBearingAndDistanceBands()
        {
            GameObject cameraObject = null;
            RetroUiTheme theme = null;

            try
            {
                cameraObject = new GameObject("Objective Direction Math Camera");
                Transform cameraTransform = cameraObject.transform;
                cameraTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                Assert.That(
                    ObjectiveDirectionArrowController.TryGetDirection(
                        Vector3.zero,
                        cameraTransform,
                        Vector3.forward * 8f,
                        out float forwardAngle,
                        out float forwardDistance),
                    Is.True);
                Assert.That(forwardAngle, Is.EqualTo(0f).Within(0.01f));
                Assert.That(forwardDistance, Is.EqualTo(8f).Within(0.01f));

                Assert.That(
                    ObjectiveDirectionArrowController.TryGetDirection(
                        Vector3.zero,
                        cameraTransform,
                        Vector3.right * 8f,
                        out float rightAngle,
                        out _),
                    Is.True);
                Assert.That(rightAngle, Is.EqualTo(-90f).Within(0.01f));

                Assert.That(
                    ObjectiveDirectionArrowController.TryGetDirection(
                        Vector3.zero,
                        cameraTransform,
                        Vector3.back * 8f,
                        out float rearAngle,
                        out _),
                    Is.True);
                Assert.That(Mathf.Abs(rearAngle), Is.EqualTo(180f).Within(0.01f));

                Assert.That(
                    ObjectiveDirectionArrowController.TryGetDirection(
                        Vector3.zero,
                        cameraTransform,
                        Vector3.up * 8f,
                        out _,
                        out _),
                    Is.False,
                    "A destination directly above or below should not show a misleading bearing.");

                theme = ScriptableObject.CreateInstance<RetroUiTheme>();
                var serializedTheme = new SerializedObject(theme);
                serializedTheme.FindProperty("objectiveArrowNearDistance").floatValue = 3f;
                serializedTheme.FindProperty("objectiveArrowMediumDistance").floatValue = 12f;
                serializedTheme.FindProperty("objectiveArrowNearColor").colorValue = Color.green;
                serializedTheme.FindProperty("objectiveArrowMediumColor").colorValue = Color.yellow;
                serializedTheme.FindProperty("objectiveArrowFarColor").colorValue = Color.red;
                serializedTheme.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    ObjectiveDirectionArrowController.ColorForDistance(3f, theme),
                    Is.EqualTo(Color.green));
                Assert.That(
                    ObjectiveDirectionArrowController.ColorForDistance(3.01f, theme),
                    Is.EqualTo(Color.yellow));
                Assert.That(
                    ObjectiveDirectionArrowController.ColorForDistance(12f, theme),
                    Is.EqualTo(Color.yellow));
                Assert.That(
                    ObjectiveDirectionArrowController.ColorForDistance(12.01f, theme),
                    Is.EqualTo(Color.red));
            }
            finally
            {
                if (cameraObject != null)
                    Object.DestroyImmediate(cameraObject);
                if (theme != null)
                    Object.DestroyImmediate(theme);
            }
        }

        [UnityTest]
        public IEnumerator ObjectiveDirection_RetargetsFromTaskSourceToReceiver()
        {
            yield return new EnterPlayMode();

            TaskObjective objective = null;
            CarryCategory category = null;
            CarryCategory unrelatedCategory = null;
            GameObject sessionObject = null;
            GameObject holderObject = null;
            GameObject sourceObject = null;
            GameObject receiverObject = null;
            GameObject unrelatedObject = null;

            try
            {
                objective = ScriptableObject.CreateInstance<TaskObjective>();
                SetPrivateField(objective, "id", "objective_direction_task");
                category = ScriptableObject.CreateInstance<CarryCategory>();
                SetPrivateField(category, "id", "objective_direction_item");
                SetPrivateField(category, "objective", objective);
                unrelatedCategory = ScriptableObject.CreateInstance<CarryCategory>();
                SetPrivateField(unrelatedCategory, "id", "unrelated_item");

                sessionObject = new GameObject("Objective Direction Task Session");
                GameSession session = sessionObject.AddComponent<GameSession>();
                var state = new NarrativeState(NameVariant.Laura, 0)
                {
                    ChapterIndex = 1,
                    BeatIndex = 0
                };
                state.Tasks.Register(objective, 1);
                InvokePrivate(session, "SetState", state);

                var taskCondition = new TaskCondition();
                SetPrivateField(taskCondition, "objective", objective);
                SetPrivateField(taskCondition, "requiredCount", 1);
                var gate = new GateBeat();
                SetPrivateField(gate, "condition", taskCondition);
                InvokePrivate(session, "SetActiveNarrativeBeat", gate);

                holderObject = new GameObject("Objective Direction Holder");
                CarriedItemHolder holder = holderObject.AddComponent<CarriedItemHolder>();
                sourceObject = new GameObject("Objective Direction Source");
                sourceObject.transform.position = new Vector3(8f, 0f, 0f);
                Carryable source = sourceObject.AddComponent<Carryable>();
                SetPrivateField(source, "category", category);
                receiverObject = new GameObject("Objective Direction Receiver");
                receiverObject.transform.position = new Vector3(16f, 0f, 0f);
                TaskReceiver receiver = receiverObject.AddComponent<TaskReceiver>();
                SetPrivateField(receiver, "accepts", category);
                yield return null;

                Assert.That(
                    ObjectiveDirectionResolver.TryResolve(
                        session,
                        holder.transform.position,
                        holder,
                        out Vector3 sourceTarget),
                    Is.True);
                Assert.That(Vector3.Distance(sourceTarget, source.transform.position),
                    Is.LessThan(0.01f));

                Assert.That(holder.TryCarry(source), Is.True);
                Assert.That(
                    ObjectiveDirectionResolver.TryResolve(
                        session,
                        holder.transform.position,
                        holder,
                        out Vector3 receiverTarget),
                    Is.True);
                Assert.That(Vector3.Distance(receiverTarget, receiver.transform.position),
                    Is.LessThan(0.01f));

                Assert.That(holder.DropCarriedItem(), Is.True);
                unrelatedObject = new GameObject("Objective Direction Unrelated Item");
                Carryable unrelated = unrelatedObject.AddComponent<Carryable>();
                SetPrivateField(unrelated, "category", unrelatedCategory);
                Assert.That(holder.TryCarry(unrelated), Is.True);
                Assert.That(
                    ObjectiveDirectionResolver.TryResolve(
                        session,
                        holder.transform.position,
                        holder,
                        out _),
                    Is.False,
                    "A held item for another objective must not redirect the guide to an item " +
                    "the player cannot pick up.");
            }
            finally
            {
                if (unrelatedObject != null)
                    Object.Destroy(unrelatedObject);
                if (receiverObject != null)
                    Object.Destroy(receiverObject);
                if (sourceObject != null)
                    Object.Destroy(sourceObject);
                if (holderObject != null)
                    Object.Destroy(holderObject);
                if (sessionObject != null)
                    Object.Destroy(sessionObject);
                if (unrelatedCategory != null)
                    Object.Destroy(unrelatedCategory);
                if (category != null)
                    Object.Destroy(category);
                if (objective != null)
                    Object.Destroy(objective);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }

        private static GameObject CreateOutputCanvas()
        {
            var canvasObject = new GameObject(
                "Narrative Presenter Test Output",
                typeof(RectTransform),
                typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var worldOutput = new GameObject(
                "World Output",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(LowResolutionPresenter));
            worldOutput.transform.SetParent(canvasObject.transform, false);
            return canvasObject;
        }

        private static PresentationSettings CreateSettings()
        {
            PresentationSettings settings = ScriptableObject.CreateInstance<PresentationSettings>();
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("revealCharactersPerSecond").floatValue = 100000f;
            serializedSettings.FindProperty("minimumHoldSeconds").floatValue = 0f;
            serializedSettings.FindProperty("chunkCharacterLimit").intValue = 40;
            serializedSettings.FindProperty("beatPauseSeconds").floatValue = 0f;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, arguments);
        }

        private sealed class ManualSequenceClock : ISequenceClock
        {
            public float UnscaledTime { get; set; }
            public float UnscaledDeltaTime { get; set; }
        }

        private sealed class ManualSequenceInput : ISequenceInput
        {
            public bool AdvancePressed { get; set; }
            public bool NextPagePressed { get; set; }
            public bool CancelPressed { get; set; }
            public bool PointerIsCaptured => false;

            public bool WasPressed(SequenceInputAction action)
            {
                switch (action)
                {
                    case SequenceInputAction.Advance:
                        return AdvancePressed;
                    case SequenceInputAction.NextPage:
                        return NextPagePressed;
                    case SequenceInputAction.Cancel:
                        return CancelPressed;
                    default:
                        return false;
                }
            }
        }

        private sealed class TestReadableDocument : IReadableDocument
        {
            private readonly IReadOnlyList<string> pages;

            public TestReadableDocument(string title, IReadOnlyList<string> pages)
            {
                Title = title;
                this.pages = pages;
            }

            public string Title { get; }
            public ILineContent Narration => null;
            public FlagId SetWhenRead => null;

            public IReadOnlyList<string> PagesFor(
                NarrativeState state,
                NarrativeCatalog catalog) => pages;
        }
    }
}
