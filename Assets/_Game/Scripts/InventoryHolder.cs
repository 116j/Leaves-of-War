using System;
using System.Collections.Generic;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Holds the set of items the player has collected. Attach to the player.
    /// Other systems (pickups, narrative beats, etc.) call <see cref="AddItem"/>
    /// to grant an item; the inventory UI reads <see cref="Items"/> to build
    /// its grid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryHolder : MonoBehaviour
    {
        private readonly List<InventoryItemDefinition> items = new List<InventoryItemDefinition>();
        private readonly HashSet<string> unresolvedItemIds =
            new HashSet<string>(StringComparer.Ordinal);

        private GameSessionRegistration sessionRegistration;
        private GameSession boundSession;
        private InventoryItemDefinition[] itemLibrary;

        public IReadOnlyList<InventoryItemDefinition> Items => items;

        /// <summary>Raised whenever an item is added, so the UI can refresh.</summary>
        public event System.Action ItemsChanged;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                BindSession,
                UnbindSession);
        }

        private void OnEnable() => sessionRegistration?.Enable();

        private void OnDisable() => sessionRegistration?.Disable();

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }

        public bool HasItem(InventoryItemDefinition item) =>
            item != null && HasItem(item.ItemId);

        public bool HasItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;

            foreach (InventoryItemDefinition item in items)
            {
                if (item != null &&
                    string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Adds an item if not already carried. Returns true if it was actually added.</summary>
        public bool AddItem(InventoryItemDefinition item)
        {
            if (item == null || HasItem(item))
                return false;

            items.Add(item);
            ActiveSession?.GiveInventoryItem(item.ItemId);
            ItemsChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(InventoryItemDefinition item)
        {
            bool removed = items.Remove(item);
            if (removed)
                ItemsChanged?.Invoke();
            return removed;
        }

        /// <summary>
        /// Rebuilds the scene-local definition references from the saveable ids
        /// owned by <see cref="InventoryState"/>. A player object is recreated
        /// on scene travel, so its component list cannot itself be authoritative.
        /// </summary>
        internal void SynchronizeFromSession(GameSession session = null)
        {
            GameSession source = session ?? ActiveSession;
            InventoryState inventory = source != null && source.State != null
                ? source.State.Inventory
                : null;
            if (inventory == null)
                return;

            if (itemLibrary == null)
            {
                itemLibrary = Resources.LoadAll<InventoryItemDefinition>(
                    "Inventory Items");
            }

            var restoredItems = new List<InventoryItemDefinition>();
            foreach (string itemId in inventory.ItemIds)
            {
                InventoryItemDefinition definition = FindDefinition(itemId);
                if (definition != null)
                {
                    if (!ContainsId(restoredItems, definition.ItemId))
                        restoredItems.Add(definition);
                    unresolvedItemIds.Remove(itemId);
                    continue;
                }

                if (unresolvedItemIds.Add(itemId))
                {
                    Debug.LogWarning(
                        $"Saved inventory item '{itemId}' has no " +
                        $"{nameof(InventoryItemDefinition)} under Resources/Inventory Items.",
                        this);
                }
            }

            if (HasSameItems(restoredItems))
                return;

            items.Clear();
            items.AddRange(restoredItems);
            ItemsChanged?.Invoke();
        }

        private GameSession ActiveSession => boundSession ?? GameSession.Instance;

        private void BindSession(GameSession session)
        {
            boundSession = session;
            session.InventoryStateChanged += HandleInventoryStateChanged;
            SynchronizeFromSession(session);
        }

        private void UnbindSession(GameSession session)
        {
            session.InventoryStateChanged -= HandleInventoryStateChanged;
            if (ReferenceEquals(boundSession, session))
                boundSession = null;
        }

        private void HandleInventoryStateChanged() => SynchronizeFromSession();

        private InventoryItemDefinition FindDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || itemLibrary == null)
                return null;

            for (int i = 0; i < itemLibrary.Length; i++)
            {
                InventoryItemDefinition candidate = itemLibrary[i];
                if (candidate != null && string.Equals(
                        candidate.ItemId,
                        itemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool HasSameItems(IReadOnlyList<InventoryItemDefinition> other)
        {
            if (other == null || items.Count != other.Count)
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                if (!ReferenceEquals(items[i], other[i]))
                    return false;
            }

            return true;
        }

        private static bool ContainsId(
            IReadOnlyList<InventoryItemDefinition> definitions,
            string itemId)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                InventoryItemDefinition candidate = definitions[i];
                if (candidate != null && string.Equals(
                        candidate.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
