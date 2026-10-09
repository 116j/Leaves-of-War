using System.Collections.Generic;
using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class GameplayWorldComponentTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            SetSessionInstance(null);
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void TaskReceiver_HidesWrongDestinationAndReportsSuccessfulUseOnlyOnce()
        {
            TaskObjective objective = CreateObjective("unpacking");
            CarryCategory clothes = CreateCategory("clothes", objective, false);
            CarryCategory books = CreateCategory("books", objective, false);
            NarrativeState state = CreateSessionState();
            state.Tasks.Register(objective, 1);
            CarriedItemHolder holder = CreateHolder();
            Carryable item = CreateCarryable("Clothes", clothes);
            TaskReceiver receiver = CreateReceiver("Bookshelf", books);

            Assert.That(holder.TryCarry(item), Is.True);
            Assert.That(receiver.Prompt, Is.Empty);
            receiver.Interact();

            Assert.That(holder.CarriedItem, Is.SameAs(item));
            Assert.That(state.Tasks.CountFor(objective), Is.Zero);

            SetField(receiver, "accepts", clothes);
            Assert.That(item.Prompt, Is.Empty);
            Assert.That(receiver.Prompt, Is.EqualTo("PLACE CLOTHES"));
            receiver.Interact();
            receiver.Interact();

            Assert.That(receiver.IsCompleted, Is.True);
            Assert.That(holder.HasItem, Is.False);
            Assert.That(item.IsPlaced, Is.True);
            Assert.That(item.transform.parent, Is.SameAs(receiver.transform));
            Assert.That(state.Tasks.CountFor(objective), Is.EqualTo(1));
        }

        [Test]
        public void TaskReceiver_UsesPlacementAnchorForSuccessfulUse()
        {
            TaskObjective objective = CreateObjective("unpacking");
            CarryCategory books = CreateCategory("books", objective, false);
            NarrativeState state = CreateSessionState();
            state.Tasks.Register(objective, 1);
            CarriedItemHolder holder = CreateHolder();
            Carryable item = CreateCarryable("Book Parcel", books);
            TaskReceiver receiver = CreateReceiver("Bookshelf", books);
            GameObject anchor = Track(new GameObject("Bookshelf Placement Anchor"));
            anchor.transform.position = new Vector3(3f, 2f, 1f);
            SetField(receiver, "placementAnchor", anchor.transform);

            Assert.That(holder.TryCarry(item), Is.True);
            receiver.Interact();

            Assert.That(receiver.IsCompleted, Is.True);
            Assert.That(item.transform.parent, Is.SameAs(anchor.transform));
            Assert.That(item.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(item.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void TaskReceiver_AppliesItsAuthoredVisualSwapOnSuccessfulUse()
        {
            TaskObjective objective = CreateObjective("gardening");
            CarryCategory broom = CreateCategory("broom", objective, true);
            NarrativeState state = CreateSessionState();
            state.Tasks.Register(objective, 1);
            CarriedItemHolder holder = CreateHolder();
            Carryable tool = CreateCarryable("Broom", broom);
            TaskReceiver receiver = CreateReceiver("Leaf-covered path", broom);
            GameObject untended = Track(new GameObject("Untended"));
            GameObject tended = Track(new GameObject("Tended"));
            tended.SetActive(false);
            SetField(receiver, "objectsToHide", new[] { untended });
            SetField(receiver, "objectsToShow", new[] { tended });
            SetField(receiver, "interactionPrompt", "Sweep path");

            Assert.That(holder.TryCarry(tool), Is.True);
            Assert.That(receiver.Prompt, Is.EqualTo("SWEEP PATH"));
            receiver.Interact();

            Assert.That(receiver.IsCompleted, Is.True);
            Assert.That(untended.activeSelf, Is.False);
            Assert.That(tended.activeSelf, Is.True);
            Assert.That(holder.Has(broom), Is.True);
        }

        [Test]
        public void CarryFeedback_UsesLabelsAndClearsAcrossReplacementDropAndDisable()
        {
            CarryCategory broom = CreateCategory("garden_broom", null, true);
            CarryCategory shears = CreateCategory("shears", null, true);
            SetField(broom, "playerFacingLabel", "Old broom");
            CarriedItemHolder holder = CreateHolder();
            Carryable first = CreateCarryable("Broom", broom);
            Carryable second = CreateCarryable("Shears", shears);
            var changes = new List<Carryable>();
            holder.CarriedItemChanged += changes.Add;

            Assert.That(first.PlayerFacingLabel, Is.EqualTo("Old broom"));
            Assert.That(first.Prompt, Is.EqualTo("TAKE OLD BROOM"));
            Assert.That(second.Prompt, Is.EqualTo("TAKE SHEARS"));

            Assert.That(holder.TryCarry(first), Is.True);
            Assert.That(holder.PlayerFacingLabel, Is.EqualTo("Old broom"));
            Assert.That(changes, Is.EqualTo(new[] { first }));

            Assert.That(holder.TryCarry(second), Is.True);
            Assert.That(changes, Is.EqualTo(new Carryable[] { first, null, second }));
            Assert.That(first.IsCarried, Is.False);
            Assert.That(holder.PlayerFacingLabel, Is.EqualTo("shears"));

            holder.enabled = false;
            // EditMode does not dispatch the component lifecycle here.
            InvokePrivate(holder, "OnDisable");
            Assert.That(changes[changes.Count - 1], Is.Null);

            holder.enabled = true;
            InvokePrivate(holder, "OnEnable");
            Assert.That(changes[changes.Count - 1], Is.SameAs(second));
            Assert.That(holder.DropCarriedItem(), Is.True);
            Assert.That(changes[changes.Count - 1], Is.Null);
            Assert.That(holder.HasItem, Is.False);
        }

        [Test]
        public void RetainedCarryable_CanBeSwappedAndReturnsToItsAuthoredTransform()
        {
            TaskObjective objective = CreateObjective("gardening");
            CarryCategory broom = CreateCategory("broom", objective, true);
            CarryCategory shears = CreateCategory("shears", objective, true);
            CarriedItemHolder holder = CreateHolder();
            GameObject firstObject = Track(new GameObject("Broom"));
            firstObject.transform.position = new Vector3(3f, 0f, 2f);
            Carryable first = firstObject.AddComponent<Carryable>();
            SetField(first, "category", broom);
            Carryable second = CreateCarryable("Shears", shears);

            Assert.That(holder.TryCarry(first), Is.True);
            Assert.That(holder.TryCarry(second), Is.True);

            Assert.That(holder.CarriedItem, Is.SameAs(second));
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(first.IsCarried, Is.False);
            Assert.That(first.transform.position, Is.EqualTo(new Vector3(3f, 0f, 2f)));
        }

        [Test]
        public void CarryableAuthoredUnderHolder_IsAdoptedAsAnUnsavedGrantedItem()
        {
            CarryCategory poison = CreateCategory("poison", null, true);
            CarriedItemHolder holder = CreateHolder();
            GameObject caseObject = Track(new GameObject("Poison Case"));
            caseObject.transform.SetParent(holder.transform, false);
            caseObject.SetActive(false);
            Carryable caseItem = caseObject.AddComponent<Carryable>();
            SetField(caseItem, "category", poison);

            // Chapter VII dressing enables the granted case after the player
            // and holder already exist. EditMode does not dispatch the normal
            // runtime OnEnable callback for this inactive test object.
            caseObject.SetActive(true);
            InvokePrivate(caseItem, "OnEnable");

            Assert.That(holder.CarriedItem, Is.SameAs(caseItem));
            Assert.That(holder.Has(poison), Is.True);
            Assert.That(caseItem.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RegisterSceneTasks_RestoresAggregateProgressInStableSiblingOrder()
        {
            TaskObjective objective = CreateObjective("maze_tidying");
            CarryCategory category = CreateCategory("maze_object", objective, false);
            TaskReceiver first = CreateReceiver("Receiver 1", category);
            TaskReceiver second = CreateReceiver("Receiver 2", category);
            TaskReceiver third = CreateReceiver("Receiver 3", category);
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 2 };
            state.Tasks.RestoreCount(objective.Id, 2);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(state.Tasks.RequiredFor(objective, 0), Is.EqualTo(3));
            Assert.That(first.IsCompleted, Is.True);
            Assert.That(second.IsCompleted, Is.True);
            Assert.That(third.IsCompleted, Is.False);
        }

        [Test]
        public void RegisterSceneTasks_ReappliesSavedVisualStateToCompletedAndPendingReceivers()
        {
            TaskObjective objective = CreateObjective("gardening");
            CarryCategory category = CreateCategory("broom", objective, true);
            TaskReceiver completed = CreateReceiver("Receiver 1", category);
            TaskReceiver pending = CreateReceiver("Receiver 2", category);
            GameObject completedBefore = Track(new GameObject("Completed Before"));
            GameObject completedAfter = Track(new GameObject("Completed After"));
            GameObject pendingBefore = Track(new GameObject("Pending Before"));
            GameObject pendingAfter = Track(new GameObject("Pending After"));
            completedBefore.SetActive(true);
            completedAfter.SetActive(false);
            pendingBefore.SetActive(false);
            pendingAfter.SetActive(true);
            SetField(completed, "objectsToHide", new[] { completedBefore });
            SetField(completed, "objectsToShow", new[] { completedAfter });
            SetField(pending, "objectsToHide", new[] { pendingBefore });
            SetField(pending, "objectsToShow", new[] { pendingAfter });
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 1 };
            state.Tasks.RestoreCount(objective.Id, 1);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(completed.IsCompleted, Is.True);
            Assert.That(completedBefore.activeSelf, Is.False);
            Assert.That(completedAfter.activeSelf, Is.True);
            Assert.That(pending.IsCompleted, Is.False);
            Assert.That(pendingBefore.activeSelf, Is.True);
            Assert.That(pendingAfter.activeSelf, Is.False);
        }

        [Test]
        public void RegisterSceneTasks_HidesRestoredNonRetainedSourceBehindCompletionVisual()
        {
            TaskObjective objective = CreateObjective("unpacking");
            CarryCategory category = CreateCategory("books", objective, false);
            TaskReceiver receiver = CreateReceiver("Receiver", category);
            Carryable source = CreateCarryable("Source", category);
            GameObject untended = Track(new GameObject("Empty Shelf"));
            GameObject tended = Track(new GameObject("Filled Shelf"));
            tended.SetActive(false);
            SetField(receiver, "objectsToHide", new[] { untended });
            SetField(receiver, "objectsToShow", new[] { tended });
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 1 };
            state.Tasks.RestoreCount(objective.Id, 1);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(receiver.IsCompleted, Is.True);
            Assert.That(untended.activeSelf, Is.False);
            Assert.That(tended.activeSelf, Is.True);
            Assert.That(source.IsRestoredConsumed, Is.True);
            Assert.That(source.transform.parent, Is.SameAs(receiver.transform));
            Assert.That(source.gameObject.activeSelf, Is.False);

            TaskReceiver.RegisterSceneTasks(state);
            Assert.That(source.IsRestoredConsumed, Is.True);
            Assert.That(source.transform.parent, Is.SameAs(receiver.transform));
            Assert.That(source.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RegisterSceneTasks_RestoresConsumedSourceAtPlacementAnchor()
        {
            TaskObjective objective = CreateObjective("unpacking");
            CarryCategory category = CreateCategory("books", objective, false);
            TaskReceiver receiver = CreateReceiver("Receiver", category);
            Carryable source = CreateCarryable("Source", category);
            GameObject anchor = Track(new GameObject("Receiver Placement Anchor"));
            anchor.transform.position = new Vector3(-2f, 1f, 4f);
            SetField(receiver, "placementAnchor", anchor.transform);
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 1 };
            state.Tasks.RestoreCount(objective.Id, 1);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(receiver.IsCompleted, Is.True);
            Assert.That(source.IsRestoredConsumed, Is.True);
            Assert.That(source.transform.parent, Is.SameAs(anchor.transform));
            Assert.That(source.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(source.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void RegisterSceneTasks_PlacesOnlyTheNonRetainedSourcesRepresentedByProgress()
        {
            TaskObjective objective = CreateObjective("unpacking");
            CarryCategory category = CreateCategory("books", objective, false);
            CreateReceiver("Receiver 1", category);
            CreateReceiver("Receiver 2", category);
            CreateReceiver("Receiver 3", category);
            Carryable first = CreateCarryable("Source 1", category);
            Carryable second = CreateCarryable("Source 2", category);
            Carryable third = CreateCarryable("Source 3", category);
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 1 };
            state.Tasks.RestoreCount(objective.Id, 2);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(first.IsRestoredConsumed, Is.True);
            Assert.That(second.IsRestoredConsumed, Is.True);
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(second.gameObject.activeSelf, Is.True);
            Assert.That(first.transform.parent.name, Is.EqualTo("Receiver 1"));
            Assert.That(second.transform.parent.name, Is.EqualTo("Receiver 2"));
            Assert.That(first.CanBeCarried, Is.False);
            Assert.That(second.CanBeCarried, Is.False);
            Assert.That(third.IsRestoredConsumed, Is.False);
            Assert.That(third.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void RegisterSceneTasks_RestoresMixedNonRetainedCategoriesDeterministically()
        {
            TaskObjective objective = CreateObjective("unpacking");
            CarryCategory clothes = CreateCategory("clothes", objective, false);
            CarryCategory books = CreateCategory("books", objective, false);
            TaskReceiver first = CreateReceiver("Receiver 1 Clothes", clothes);
            TaskReceiver second = CreateReceiver("Receiver 2 Books", books);
            TaskReceiver third = CreateReceiver("Receiver 3 Clothes", clothes);
            TaskReceiver fourth = CreateReceiver("Receiver 4 Books", books);
            Carryable firstClothes = CreateCarryable("Clothes 1", clothes);
            Carryable firstBooks = CreateCarryable("Books 1", books);
            Carryable secondClothes = CreateCarryable("Clothes 2", clothes);
            Carryable secondBooks = CreateCarryable("Books 2", books);
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 1 };
            state.Tasks.RestoreCount(objective.Id, 3);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(state.Tasks.RequiredFor(objective, 0), Is.EqualTo(4));
            Assert.That(first.IsCompleted, Is.True);
            Assert.That(second.IsCompleted, Is.True);
            Assert.That(third.IsCompleted, Is.True);
            Assert.That(fourth.IsCompleted, Is.False);
            Assert.That(firstClothes.transform.parent, Is.SameAs(first.transform));
            Assert.That(secondClothes.transform.parent, Is.SameAs(third.transform));
            Assert.That(firstBooks.transform.parent, Is.SameAs(second.transform));
            Assert.That(secondBooks.IsRestoredConsumed, Is.False);
            Assert.That(secondBooks.CanBeCarried, Is.True);
        }

        [Test]
        public void RegisterSceneTasks_LeavesRetainedToolsAtTheirAuthoredOrigins()
        {
            TaskObjective objective = CreateObjective("gardening");
            CarryCategory broom = CreateCategory("broom", objective, true);
            CreateReceiver("Garden Patch 1", broom);
            CreateReceiver("Garden Patch 2", broom);
            Carryable tool = CreateCarryable("Broom", broom);
            tool.transform.position = new Vector3(4f, 0f, -2f);
            var state = new NarrativeState(NameVariant.Laura, 0) { ChapterIndex = 1 };
            state.Tasks.RestoreCount(objective.Id, 2);

            TaskReceiver.RegisterSceneTasks(state);

            Assert.That(tool.IsRestoredConsumed, Is.False);
            Assert.That(tool.CanBeCarried, Is.True);
            Assert.That(tool.transform.position, Is.EqualTo(new Vector3(4f, 0f, -2f)));
        }

        [Test]
        public void GreyboxSequencePlayer_RevealsItsRootAndCompletesWithoutScaledTime()
        {
            CreateSessionState();
            GameSession session = GameSession.Instance;
            GameObject playerObject = Track(new GameObject("Sequence Player"));
            GameObject reveal = Track(new GameObject("Greybox Reveal"));
            reveal.SetActive(false);
            GreyboxSequencePlayer player = playerObject.AddComponent<GreyboxSequencePlayer>();
            SetField(player, "sequenceId", "dream_2_flood");
            SetField(player, "revealRoot", reveal);
            SetField(player, "unscaledHoldSeconds", 0f);

            var handle = new SequencePlaybackHandle(
                session,
                player.SequenceId,
                null,
                player.RequiredLocks,
                new FixedSequenceClock(),
                new NoSequenceInput());
            handle.Begin();
            var playback = player.Play(handle);

            Assert.That(player.SequenceId, Is.EqualTo("dream_2_flood"));
            Assert.That(playback.MoveNext(), Is.False);
            Assert.That(reveal.activeSelf, Is.True);
            handle.Complete();
            Assert.That(session.IsNarrativeInputCaptured, Is.False);
        }

        [Test]
        public void FlagGatedObject_ReactivatesAfterItsFlagChangesWhileInactive()
        {
            FlagId flag = CreateFlag("sheet_pulled");
            NarrativeState state = CreateSessionState();
            GameObject gatedObject = Track(new GameObject("Gated Diary"));
            FlagGatedObject gated = gatedObject.AddComponent<FlagGatedObject>();
            SetField(gated, "flag", flag);
            SetField(gated, "activeWhenSet", true);

            FlagGatedObject.RefreshSceneObjects(state);
            Assert.That(gatedObject.activeSelf, Is.False);

            state.SetFlag(flag);

            Assert.That(gatedObject.activeSelf, Is.True);
        }

        [Test]
        public void TaskProgressListener_RevealsImmediatelyWhenThresholdIsReported()
        {
            TaskObjective objective = CreateObjective("gardening");
            NarrativeState state = CreateSessionState();
            state.Tasks.Register(objective, 2);
            GameObject controller = Track(new GameObject("Progress Listener"));
            GameObject revealed = Track(new GameObject("Settled Room"));
            TaskProgressListener listener = controller.AddComponent<TaskProgressListener>();
            SetField(listener, "objective", objective);
            SetField(listener, "threshold", 1);
            SetField(listener, "revealed", revealed);

            TaskProgressListener.RefreshSceneObjects(state);
            Assert.That(revealed.activeSelf, Is.False);

            state.Tasks.Report(objective);

            Assert.That(revealed.activeSelf, Is.True);
        }

        [Test]
        public void FlagInteractable_RequiresAndConsumesANonRetainedCarriedCategory()
        {
            FlagId flag = CreateFlag("journal_placed");
            CarryCategory journal = CreateCategory("journal", null, false);
            NarrativeState state = CreateSessionState();
            CarriedItemHolder holder = CreateHolder();
            Carryable item = CreateCarryable("Journal", journal);
            GameObject target = Track(new GameObject("Place Journal"));
            GameObject placement = Track(new GameObject("Placed Journal Pose"));
            placement.transform.position = new Vector3(2f, 3f, 4f);
            FlagInteractable interactable = target.AddComponent<FlagInteractable>();
            SetField(interactable, "prompt", "PLACE JOURNAL");
            SetField(interactable, "flag", flag);
            SetField(interactable, "requiresCarried", journal);
            SetField(interactable, "usedItemDestination", placement.transform);

            Assert.That(interactable.Prompt, Is.Empty);
            Assert.That(holder.TryCarry(item), Is.True);
            Assert.That(interactable.Prompt, Is.EqualTo("PLACE JOURNAL"));

            interactable.Interact();

            Assert.That(state.HasFlag(flag), Is.True);
            Assert.That(holder.HasItem, Is.False);
            Assert.That(item.IsPlaced, Is.True);
            Assert.That(item.transform.parent, Is.EqualTo(placement.transform));
            Assert.That(item.transform.position, Is.EqualTo(placement.transform.position));
        }

        [Test]
        public void FlagInteractable_RestoresPreexistingNonRetainedUseAtItsAuthoredPose()
        {
            FlagId flag = CreateFlag("poison_used");
            CarryCategory poison = CreateCategory("poison", null, false);
            NarrativeState state = CreateSessionState();
            CarriedItemHolder holder = CreateHolder();
            Carryable item = CreateCarryable("Poison", poison);
            GameObject target = Track(new GameObject("Use Poison"));
            GameObject placement = Track(new GameObject("Poured Poison Pose"));
            FlagInteractable interactable = target.AddComponent<FlagInteractable>();
            SetField(interactable, "prompt", "USE POISON");
            SetField(interactable, "flag", flag);
            SetField(interactable, "requiresCarried", poison);
            SetField(interactable, "usedItemDestination", placement.transform);

            Assert.That(holder.TryCarry(item), Is.True);
            state.SetFlag(flag);
            InvokePrivate(interactable, "Start");

            Assert.That(interactable.Prompt, Is.Empty);
            Assert.That(holder.HasItem, Is.False);
            Assert.That(item.IsPlaced, Is.True);
            Assert.That(item.transform.parent, Is.EqualTo(placement.transform));
        }

        private NarrativeState CreateSessionState()
        {
            var state = new NarrativeState(NameVariant.Laura, 0)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            GameObject sessionObject = Track(new GameObject("World Component Test Session"));
            GameSession session = sessionObject.AddComponent<GameSession>();
            SetSessionInstance(session);
            InvokePrivate(session, "SetState", state);
            SetField(session, "saveBatchDepth", 1);
            return state;
        }

        private CarriedItemHolder CreateHolder()
        {
            GameObject holderObject = Track(new GameObject("Carried Item Holder"));
            CarriedItemHolder holder = holderObject.AddComponent<CarriedItemHolder>();
            // Plain EditMode tests do not dispatch MonoBehaviour OnEnable in
            // every runner. Register the fixture explicitly; the separate
            // lifecycle PlayMode coverage verifies automatic registration.
            GameSession.Instance?.RegisterCarriedItemHolder(holder);
            return holder;
        }

        private Carryable CreateCarryable(
            string name,
            CarryCategory category,
            Transform parent = null)
        {
            GameObject itemObject = Track(new GameObject(name));
            if (parent != null)
                itemObject.transform.SetParent(parent, false);
            Carryable item = itemObject.AddComponent<Carryable>();
            SetField(item, "category", category);
            return item;
        }

        private TaskReceiver CreateReceiver(string name, CarryCategory category)
        {
            GameObject receiverObject = Track(new GameObject(name));
            TaskReceiver receiver = receiverObject.AddComponent<TaskReceiver>();
            SetField(receiver, "accepts", category);
            return receiver;
        }

        private TaskObjective CreateObjective(string id)
        {
            TaskObjective objective = Track(ScriptableObject.CreateInstance<TaskObjective>());
            SetField(objective, "id", id);
            return objective;
        }

        private CarryCategory CreateCategory(
            string id,
            TaskObjective objective,
            bool retainedAfterUse)
        {
            CarryCategory category = Track(ScriptableObject.CreateInstance<CarryCategory>());
            SetField(category, "id", id);
            SetField(category, "objective", objective);
            SetField(category, "retainedAfterUse", retainedAfterUse);
            return category;
        }

        private FlagId CreateFlag(string id)
        {
            FlagId flag = Track(ScriptableObject.CreateInstance<FlagId>());
            SetField(flag, "id", id);
            return flag;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string fieldName, object value)
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

        private static void SetSessionInstance(GameSession session)
        {
            PropertyInfo property = typeof(GameSession).GetProperty(
                nameof(GameSession.Instance),
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            property.SetValue(null, session);
        }

        private sealed class FixedSequenceClock : ISequenceClock
        {
            public float UnscaledTime => 0f;
            public float UnscaledDeltaTime => 1f;
        }

        private sealed class NoSequenceInput : ISequenceInput
        {
            public bool PointerIsCaptured => false;

            public bool WasPressed(SequenceInputAction action) => false;
        }
    }
}
