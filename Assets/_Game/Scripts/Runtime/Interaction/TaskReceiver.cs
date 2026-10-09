using System;
using System.Collections.Generic;
using System.Text;
using Hortensia.Narrative;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TaskReceiver : MonoBehaviour, IInteractable
    {
        [SerializeField] private CarryCategory accepts;
        [Tooltip("Optional verb-led prompt for an authored action, such as SWEEP PATH. " +
            "Falls back to PLACE <ITEM> for ordinary placement tasks.")]
        [SerializeField] private string interactionPrompt;
        [Tooltip("Optional transform where the accepted item is placed. " +
            "Defaults to this receiver when unset.")]
        [SerializeField] private Transform placementAnchor;

        [Header("Visual Swap (optional)")]
        [Tooltip("Objects hidden when the item is successfully placed here - the carried item's own mesh is always hidden automatically in addition to these, so you don't need to list it.")]
        [SerializeField] private GameObject[] objectsToHide = System.Array.Empty<GameObject>();
        [Tooltip("Objects shown when the item is successfully placed here - e.g. a 'wardrobe full' model, a highlight effect, anything that should appear once this task completes.")]
        [SerializeField] private GameObject[] objectsToShow = System.Array.Empty<GameObject>();

        [Header("Sound")]
        [Tooltip("Played once when an accepted item is successfully placed here.")]
        [SerializeField] private AudioClip placeSound;
        [Range(0f, 1f)]
        [SerializeField] private float placeSoundVolume = 1f;

        [Header("On Completed")]
        [Tooltip("Fires once, right when this task is successfully completed - wire this to anything that should react (e.g. a wardrobe's Open() method) without this script needing to know about it.")]
        [SerializeField] private UnityEvent onCompleted;

        private bool completed;
        private AudioSource audioSource;

        public CarryCategory Accepts => accepts;
        public bool IsCompleted => completed;

        public string Prompt
        {
            get
            {
                if (completed || accepts == null)
                    return string.Empty;

                GameSession session = GameSession.Instance;
                return session != null &&
                    session.TryGetCarriedItemHolder(out CarriedItemHolder holder) &&
                    holder.Has(accepts)
                        ? ResolveInteractionPrompt()
                        : string.Empty;
            }
        }

        private string ResolveInteractionPrompt() =>
            !string.IsNullOrWhiteSpace(interactionPrompt)
                ? interactionPrompt.Trim().ToUpperInvariant()
                : $"PLACE {accepts.PlayerFacingLabel.ToUpperInvariant()}";

        private void Awake()
        {
            EnsureAudioSource();
            ApplyCompletionVisualState(completed);
        }

        public void Interact()
        {
            Debug.Log($"[TaskReceiver] '{name}' Interact() called. completed={completed}, accepts={(accepts != null ? accepts.name : "NULL")}");
            if (completed || accepts == null || accepts.Objective == null)
                return;

            GameSession session = GameSession.Instance;
            if (session == null || session.State == null)
                return;

            TaskObjective objective = accepts.Objective;
            if (session.Catalog != null &&
                !ReferenceEquals(session.Catalog.TaskObjectiveWithId(objective.Id), objective))
            {
                Debug.LogError(
                    $"{nameof(TaskReceiver)} on '{name}' cannot report uncatalogued task objective " +
                    $"'{objective.Id}'.",
                    this);
                return;
            }

            if (!session.TryGetCarriedItemHolder(out CarriedItemHolder holder))
            {
                Debug.Log($"[TaskReceiver] '{name}' no CarriedItemHolder found - aborting.");
                return;
            }

            // Captured before TryUse, which clears the holder's reference on success.
            Carryable itemBeingPlaced = holder.CarriedItem;

            if (!holder.TryUse(accepts, PlacementDestination))
            {
                Debug.Log($"[TaskReceiver] '{name}' TryUse FAILED. holder.HasItem={holder.HasItem}, holder category={(holder.Category != null ? holder.Category.name : "NULL")}");
                return;
            }

            Debug.Log($"[TaskReceiver] '{name}' TryUse succeeded, task completed. Invoking onCompleted now.");
            completed = true;
            session.State.Tasks.Report(objective);

            // Fired immediately, before any presentation logic below - so a
            // problem in the visual swap or sound doesn't silently prevent
            // this from ever running.
            onCompleted?.Invoke();
            Debug.Log($"[TaskReceiver] '{name}' onCompleted invoked (listener count check: {onCompleted?.GetPersistentEventCount() ?? 0}).");

            // The carried item's own mesh is always hidden (PlaceAt already
            // made it visible at this receiver) - it's the default behaviour
            // when no swap is configured, but with a swap it's redundant/
            // wrong to show both the raw item and the swapped-in visuals.
            if (itemBeingPlaced != null && HasCompletionVisualSwap)
                itemBeingPlaced.gameObject.SetActive(false);

            ApplyCompletionVisualState(true);

            if (placeSound != null)
                audioSource.PlayOneShot(placeSound, placeSoundVolume);
        }

        /// <summary>
        /// Rebuilds the loaded scene's aggregate requirements and maps saved
        /// counts back onto receivers in stable hierarchy order. Receivers in
        /// inactive progressive roots still count; receivers inside a chapter
        /// dressing group for another chapter do not.
        /// </summary>
        public static void RegisterSceneTasks(NarrativeState state)
        {
            if (state == null)
                return;

            state.Tasks.ClearRegistrations();

            Scene activeScene = SceneManager.GetActiveScene();
            TaskReceiver[] candidates =
                FindObjectsByType<TaskReceiver>(FindObjectsInactive.Include);
            ChapterDressing[] dressings =
                FindObjectsByType<ChapterDressing>(FindObjectsInactive.Include);
            var groups = new Dictionary<string, ReceiverGroup>(StringComparer.Ordinal);
            var restoredReceivers =
                new Dictionary<CarryCategory, List<TaskReceiver>>();

            for (int i = 0; i < candidates.Length; i++)
            {
                TaskReceiver receiver = candidates[i];
                if (receiver == null || !receiver.enabled ||
                    receiver.gameObject.scene != activeScene ||
                    receiver.accepts == null || receiver.accepts.Objective == null ||
                    string.IsNullOrWhiteSpace(receiver.accepts.Objective.Id) ||
                    !IsAllowedByChapterDressing(receiver.transform, state.ChapterIndex, dressings))
                {
                    continue;
                }

                string objectiveId = receiver.accepts.Objective.Id;
                if (!groups.TryGetValue(objectiveId, out ReceiverGroup group))
                {
                    group = new ReceiverGroup(receiver.accepts.Objective);
                    groups.Add(objectiveId, group);
                }

                group.Receivers.Add(receiver);
            }

            foreach (ReceiverGroup group in groups.Values)
            {
                group.Receivers.Sort(CompareByHierarchy);
                state.Tasks.Register(group.Objective, group.Receivers.Count);
                int restored = Mathf.Clamp(
                    state.Tasks.CountFor(group.Objective),
                    0,
                    group.Receivers.Count);

                for (int i = 0; i < group.Receivers.Count; i++)
                {
                    TaskReceiver receiver = group.Receivers[i];
                    receiver.completed = i < restored;
                    receiver.ApplyCompletionVisualState(receiver.completed);
                    if (!receiver.completed || receiver.accepts.RetainedAfterUse)
                        continue;

                    if (!restoredReceivers.TryGetValue(
                        receiver.accepts,
                        out List<TaskReceiver> categoryReceivers))
                    {
                        categoryReceivers = new List<TaskReceiver>();
                        restoredReceivers.Add(receiver.accepts, categoryReceivers);
                    }

                    categoryReceivers.Add(receiver);
                }
            }

            RestoreConsumedSources(
                activeScene,
                state.ChapterIndex,
                dressings,
                restoredReceivers);

            state.Tasks.NotifyRequirementsChanged();
        }

        private void ApplyCompletionVisualState(bool isCompleted)
        {
            for (int i = 0; i < objectsToHide.Length; i++)
            {
                GameObject before = objectsToHide[i];
                if (before != null)
                    before.SetActive(!isCompleted);
            }

            for (int i = 0; i < objectsToShow.Length; i++)
            {
                GameObject after = objectsToShow[i];
                if (after != null)
                    after.SetActive(isCompleted);
            }
        }

        private bool HasCompletionVisualSwap =>
            objectsToHide.Length > 0 || objectsToShow.Length > 0;

        private Transform PlacementDestination =>
            placementAnchor != null ? placementAnchor : transform;

        private static void RestoreConsumedSources(
            Scene activeScene,
            int chapterIndex,
            IReadOnlyList<ChapterDressing> dressings,
            IReadOnlyDictionary<CarryCategory, List<TaskReceiver>> restoredReceivers)
        {
            Carryable[] candidates =
                FindObjectsByType<Carryable>(FindObjectsInactive.Include);
            var groups = new Dictionary<CarryCategory, List<Carryable>>();

            for (int i = 0; i < candidates.Length; i++)
            {
                Carryable source = candidates[i];
                if (source == null || !source.enabled ||
                    source.gameObject.scene != activeScene ||
                    source.Category == null || source.Category.RetainedAfterUse ||
                    !IsAllowedByChapterDressing(source.transform, chapterIndex, dressings))
                {
                    continue;
                }

                // RegisterSceneTasks normally runs once on scene load, but
                // making restoration repeatable keeps debug/reload paths from
                // accumulating stale placed sources.
                source.ClearRestoredPlacement();
                if (!groups.TryGetValue(source.Category, out List<Carryable> sources))
                {
                    sources = new List<Carryable>();
                    groups.Add(source.Category, sources);
                }

                sources.Add(source);
            }

            foreach (KeyValuePair<CarryCategory, List<Carryable>> pair in groups)
            {
                pair.Value.Sort(CompareCarryablesByHierarchy);
                if (!restoredReceivers.TryGetValue(
                    pair.Key,
                    out List<TaskReceiver> receivers))
                {
                    continue;
                }

                receivers.Sort(CompareByHierarchy);
                int restored = Mathf.Min(receivers.Count, pair.Value.Count);
                for (int i = 0; i < restored; i++)
                {
                    Carryable source = pair.Value[i];
                    TaskReceiver receiver = receivers[i];
                    source.RestorePlacementAt(receiver.PlacementDestination);
                    if (receiver.HasCompletionVisualSwap)
                        source.gameObject.SetActive(false);
                }
            }
        }

        private static bool IsAllowedByChapterDressing(
            Transform target,
            int chapterIndex,
            IReadOnlyList<ChapterDressing> dressings)
        {
            for (int dressingIndex = 0; dressingIndex < dressings.Count; dressingIndex++)
            {
                ChapterDressing dressing = dressings[dressingIndex];
                if (dressing == null || dressing.gameObject.scene != target.gameObject.scene)
                    continue;

                for (int groupIndex = 0; groupIndex < dressing.Groups.Count; groupIndex++)
                {
                    ChapterDressingGroup group = dressing.Groups[groupIndex];
                    if (group == null || group.Root == null ||
                        !IsSelfOrChildOf(target, group.Root.transform))
                    {
                        continue;
                    }

                    if (!group.IsActiveIn(chapterIndex))
                        return false;
                }
            }

            return true;
        }

        private static bool IsSelfOrChildOf(Transform target, Transform possibleAncestor)
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, possibleAncestor))
                    return true;
            }

            return false;
        }

        private static int CompareByHierarchy(TaskReceiver left, TaskReceiver right) =>
            string.Compare(
                HierarchyKey(left),
                HierarchyKey(right),
                StringComparison.Ordinal);

        private static int CompareCarryablesByHierarchy(Carryable left, Carryable right) =>
            string.Compare(
                HierarchyKey(left),
                HierarchyKey(right),
                StringComparison.Ordinal);

        private static string HierarchyKey(Component component)
        {
            var ancestry = new Stack<Transform>();
            for (Transform current = component.transform; current != null; current = current.parent)
                ancestry.Push(current);

            var key = new StringBuilder(ancestry.Count * 16);
            while (ancestry.Count > 0)
            {
                Transform current = ancestry.Pop();
                key.Append(current.GetSiblingIndex().ToString("D6"));
                key.Append('/');
            }

            Component[] components = component.GetComponents(component.GetType());
            for (int i = 0; i < components.Length; i++)
            {
                if (ReferenceEquals(components[i], component))
                {
                    key.Append(i.ToString("D3"));
                    break;
                }
            }

            return key.ToString();
        }

        private void EnsureAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D - the receiver is a world object.
            AudioManager.Route(audioSource, AudioBus.SoundEffects);
        }

        private sealed class ReceiverGroup
        {
            public ReceiverGroup(TaskObjective objective)
            {
                Objective = objective;
            }

            public TaskObjective Objective { get; }
            public List<TaskReceiver> Receivers { get; } = new List<TaskReceiver>();
        }
    }
}
