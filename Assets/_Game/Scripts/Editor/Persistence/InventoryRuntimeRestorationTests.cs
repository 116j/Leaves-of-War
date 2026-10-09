using System.Collections.Generic;
using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class InventoryRuntimeRestorationTests
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
        public void InventoryHolder_ProjectsRestoredItemIdsIntoNewScenePlayer()
        {
            InventoryItemDefinition letter =
                Resources.Load<InventoryItemDefinition>(
                    "Inventory Items/Osmund's letter");
            Assert.That(letter, Is.Not.Null);

            var state = new NarrativeState(NameVariant.Laura, 0);
            state.RestoreInventory(
                unlocked: true,
                itemIds: new[] { letter.ItemId },
                carriedItemId: null,
                carriedCategoryId: null,
                wasRecordedInSave: true);
            GameSession session = CreateSession(state);
            InventoryHolder holder = Track(
                new GameObject("Restored Inventory Player"))
                .AddComponent<InventoryHolder>();

            InvokeNonPublic(holder, "SynchronizeFromSession", session);

            Assert.That(holder.Items, Is.EqualTo(new[] { letter }));
            Assert.That(holder.HasItem(letter.ItemId), Is.True);
        }

        [Test]
        public void InventoryController_ProjectsRestoredUnlockState()
        {
            var state = new NarrativeState(NameVariant.Everie, 0);
            state.RestoreInventory(
                unlocked: true,
                itemIds: null,
                carriedItemId: null,
                carriedCategoryId: null,
                wasRecordedInSave: true);
            GameSession session = CreateSession(state);
            EventSystem existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            InventoryController controller = Track(
                new GameObject("Restored Inventory Controller"))
                .AddComponent<InventoryController>();
            if (existingEventSystem == null && EventSystem.current != null)
                Track(EventSystem.current.gameObject);

            InvokeNonPublic(controller, "SynchronizeFromSession", session);

            Assert.That(GetField<bool>(controller, "unlocked"), Is.True);
        }

        [Test]
        public void CarriedItemHolder_RestoresExactSavedItemAndCategory()
        {
            CarryCategory category = Track(
                ScriptableObject.CreateInstance<CarryCategory>());
            SetField(category, "id", "garden_broom");
            GameObject itemObject = Track(new GameObject("Saved Broom"));
            Carryable item = itemObject.AddComponent<Carryable>();
            SetField(item, "category", category);

            var state = new NarrativeState(NameVariant.Laura, 0);
            state.RestoreInventory(
                unlocked: false,
                itemIds: null,
                carriedItemId: item.PersistentSaveId,
                carriedCategoryId: category.Id,
                wasRecordedInSave: true);
            GameSession session = CreateSession(state);
            CarriedItemHolder holder = Track(
                new GameObject("Restored Carried Item Player"))
                .AddComponent<CarriedItemHolder>();

            bool restored = (bool)InvokeNonPublic(
                holder,
                "SynchronizeSavedCarriedItem",
                session);

            Assert.That(restored, Is.True);
            Assert.That(holder.CarriedItem, Is.SameAs(item));
            Assert.That(holder.Category, Is.SameAs(category));
            Assert.That(state.Inventory.CarriedItemId, Is.EqualTo(item.PersistentSaveId));
        }

        private GameSession CreateSession(NarrativeState state)
        {
            GameSession session = Track(
                new GameObject("Inventory Restoration Test Session"))
                .AddComponent<GameSession>();
            InvokeNonPublic(session, "SetState", state);
            SetField(session, "saveBatchDepth", 1);
            return session;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static object InvokeNonPublic(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"Missing method '{methodName}' on {target.GetType().Name}.");
            return method.Invoke(target, arguments);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
