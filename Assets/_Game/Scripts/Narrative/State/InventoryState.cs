using System;
using System.Collections.Generic;

namespace Hortensia.Narrative
{
    /// <summary>
    /// Saveable, runtime-agnostic inventory facts. This deliberately stores
    /// stable item ids rather than runtime InventoryItemDefinition references,
    /// keeping the narrative model independent of scene and presentation code.
    /// </summary>
    public sealed class InventoryState
    {
        private readonly HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);

        public bool IsUnlocked { get; private set; }
        public string CarriedItemId { get; private set; }
        public string CarriedCategoryId { get; private set; }
        public IReadOnlyCollection<string> ItemIds => itemIds;

        public event Action Changed;

        public bool HasItem(string itemId) =>
            !string.IsNullOrWhiteSpace(itemId) && itemIds.Contains(itemId.Trim());

        public bool Unlock()
        {
            if (IsUnlocked)
                return false;

            IsUnlocked = true;
            Changed?.Invoke();
            return true;
        }

        public bool AddItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !itemIds.Add(itemId.Trim()))
                return false;

            Changed?.Invoke();
            return true;
        }

        public bool SetCarriedItem(string itemId, string categoryId)
        {
            string normalizedItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
            string normalized = string.IsNullOrWhiteSpace(categoryId) ? null : categoryId.Trim();
            if (string.Equals(CarriedItemId, normalizedItemId, StringComparison.Ordinal) &&
                string.Equals(CarriedCategoryId, normalized, StringComparison.Ordinal))
            return false;

            CarriedItemId = normalizedItemId;
            CarriedCategoryId = normalized;
            Changed?.Invoke();
            return true;
        }

        public void Restore(
            bool unlocked,
            IEnumerable<string> restoredItemIds,
            string carriedItemId,
            string carriedCategoryId)
        {
            IsUnlocked = unlocked;
            CarriedItemId = string.IsNullOrWhiteSpace(carriedItemId)
                ? null
                : carriedItemId.Trim();
            CarriedCategoryId = string.IsNullOrWhiteSpace(carriedCategoryId)
                ? null
                : carriedCategoryId.Trim();
            itemIds.Clear();
            if (restoredItemIds != null)
            {
                foreach (string itemId in restoredItemIds)
                {
                    if (!string.IsNullOrWhiteSpace(itemId))
                        itemIds.Add(itemId.Trim());
                }
            }
        }
    }
}
