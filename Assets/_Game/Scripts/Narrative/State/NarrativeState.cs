using System;
using System.Collections.Generic;

namespace Hortensia.Narrative
{
    public sealed class NarrativeState
    {
        private readonly HashSet<FlagId> flags = new HashSet<FlagId>();

        public NarrativeState(NameVariant chosenName, int initialBloomCount)
        {
            ChosenName = chosenName;
            Garden = new GardenState(initialBloomCount);
            Tasks = new TaskProgress();
            Inventory = new InventoryState();
        }

        public NameVariant ChosenName { get; }
        public GardenState Garden { get; }
        public TaskProgress Tasks { get; }
        public InventoryState Inventory { get; }
        /// <summary>
        /// Indicates whether this state originated from a save that explicitly
        /// recorded inventory data. Older v2 saves did not, so GameSession can
        /// reconstruct their one-time inline inventory grants on load.
        /// </summary>
        public bool HasSavedInventoryState { get; private set; }
        public int ChapterIndex { get; set; }
        public int BeatIndex { get; set; }
        public IReadOnlyCollection<FlagId> Flags => flags;

        public event Action<FlagId, bool> FlagChanged;

        public bool HasFlag(FlagId flag) => flag != null && flags.Contains(flag);

        public bool SetFlag(FlagId flag)
        {
            if (flag == null || !flags.Add(flag))
                return false;

            FlagChanged?.Invoke(flag, true);
            return true;
        }

        public bool ClearFlag(FlagId flag)
        {
            if (flag == null || !flags.Remove(flag))
                return false;

            FlagChanged?.Invoke(flag, false);
            return true;
        }

        public void RestoreFlags(IEnumerable<FlagId> restoredFlags)
        {
            flags.Clear();
            if (restoredFlags == null)
                return;

            foreach (FlagId flag in restoredFlags)
            {
                if (flag != null)
                    flags.Add(flag);
            }
        }

        public void RestoreInventory(
            bool unlocked,
            IEnumerable<string> itemIds,
            string carriedItemId,
            string carriedCategoryId,
            bool wasRecordedInSave)
        {
            Inventory.Restore(unlocked, itemIds, carriedItemId, carriedCategoryId);
            HasSavedInventoryState = wasRecordedInSave;
        }
    }
}
