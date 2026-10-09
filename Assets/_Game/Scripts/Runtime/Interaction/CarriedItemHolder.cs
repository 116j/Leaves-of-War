using System;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CarriedItemHolder : MonoBehaviour
    {
        private Carryable carriedItem;
        private GameSessionRegistration sessionRegistration;
        private GameSession boundSession;
        private string unresolvedSavedItemId;

        public Carryable CarriedItem => carriedItem;
        public CarryCategory Category => carriedItem != null ? carriedItem.Category : null;
        public string PlayerFacingLabel =>
            carriedItem != null ? carriedItem.PlayerFacingLabel : string.Empty;
        public bool HasItem => carriedItem != null;

        public event Action<Carryable> CarriedItemChanged;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                BindSession,
                UnbindSession);
        }

        private void OnEnable()
        {
            Carryable beforeRestoration = carriedItem;
            sessionRegistration?.Enable();

            if (!HasPendingSavedCarriedItem())
                AdoptEligibleChildren();
            if (ReferenceEquals(beforeRestoration, carriedItem))
                NotifyCarriedItemChanged(carriedItem);
        }

        private void Start()
        {
            // Chapter dressing can enable a granted item after this holder's
            // OnEnable. A second pass also covers an already-active authored
            // child regardless of scene object initialization order.
            SynchronizeSavedCarriedItem();
            if (!HasPendingSavedCarriedItem())
                AdoptEligibleChildren();
        }

        private void OnDisable()
        {
            sessionRegistration?.Disable();

            // The holder and hidden carried child may be on their way out with
            // the scene. Clear presentation immediately without trying to
            // re-parent an object during scene teardown.
            NotifyCarriedItemChanged(null);
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }

        public bool Has(CarryCategory category) =>
            category != null && carriedItem != null &&
            ReferenceEquals(carriedItem.Category, category);

        public bool TryCarry(Carryable item)
        {
            if (item == null || !item.CanBeCarried)
                return false;

            if (ReferenceEquals(carriedItem, item))
                return true;

            if (carriedItem != null)
            {
                if (carriedItem.Category == null || !carriedItem.Category.RetainedAfterUse)
                    return false;

                Carryable previous = carriedItem;
                carriedItem = null;
                previous.ReturnHome();
                NotifyCarriedItemChanged(null);
            }

            carriedItem = item;
            item.AttachTo(this);
            GameSession.Instance?.SetCarriedItem(item);
            NotifyCarriedItemChanged(item);
            return true;
        }

        public bool TryUse(CarryCategory expected, Transform destination)
        {
            if (!Has(expected) || destination == null)
                return false;

            if (expected.RetainedAfterUse)
                return true;

            Carryable used = carriedItem;
            carriedItem = null;
            used.PlaceAt(destination);
            GameSession.Instance?.SetCarriedItem(null);
            NotifyCarriedItemChanged(null);
            return true;
        }

        public bool DropCarriedItem()
        {
            if (carriedItem == null)
                return false;

            Carryable dropped = carriedItem;
            carriedItem = null;
            dropped.ReturnHome();
            GameSession.Instance?.SetCarriedItem(null);
            NotifyCarriedItemChanged(null);
            return true;
        }

        internal bool TryAdoptChild(Carryable item)
        {
            if (item == null || carriedItem != null ||
                item.transform.parent == null ||
                item.GetComponentInParent<CarriedItemHolder>() != this)
            {
                return false;
            }

            if (!TryCarry(item))
                return false;

            item.MarkGrantedChildAdopted();
            return true;
        }

        private void AdoptEligibleChildren()
        {
            if (carriedItem != null)
                return;

            Carryable[] children = GetComponentsInChildren<Carryable>(false);
            for (int i = 0; i < children.Length; i++)
            {
                Carryable candidate = children[i];
                if (candidate != null && candidate.CanAutoAdopt && TryAdoptChild(candidate))
                    return;
            }
        }

        private void BindSession(GameSession session)
        {
            boundSession = session;
            session.RegisterCarriedItemHolder(this);
            session.InventoryStateChanged += HandleInventoryStateChanged;
            SynchronizeSavedCarriedItem();
        }

        private void UnbindSession(GameSession session)
        {
            session.InventoryStateChanged -= HandleInventoryStateChanged;
            session.UnregisterCarriedItemHolder(this);
            if (ReferenceEquals(boundSession, session))
                boundSession = null;
        }

        private void HandleInventoryStateChanged() => SynchronizeSavedCarriedItem();

        private bool HasPendingSavedCarriedItem()
        {
            GameSession session = boundSession ?? GameSession.Instance;
            return carriedItem == null && session != null && session.State != null &&
                !string.IsNullOrWhiteSpace(session.State.Inventory.CarriedItemId);
        }

        internal bool SynchronizeSavedCarriedItem(GameSession session = null)
        {
            GameSession source = session ?? boundSession ?? GameSession.Instance;
            string carriedItemId = source != null && source.State != null
                ? source.State.Inventory.CarriedItemId
                : null;
            string carriedCategoryId = source != null && source.State != null
                ? source.State.Inventory.CarriedCategoryId
                : null;

            if (carriedItem != null)
            {
                bool itemMatches = string.Equals(
                    carriedItem.PersistentSaveId,
                    carriedItemId,
                    StringComparison.Ordinal);
                bool categoryMatches = string.IsNullOrWhiteSpace(carriedCategoryId) ||
                    carriedItem.Category != null && string.Equals(
                        carriedItem.Category.Id,
                        carriedCategoryId,
                        StringComparison.Ordinal);
                if (itemMatches && categoryMatches)
                {
                    unresolvedSavedItemId = null;
                    return true;
                }

                Carryable staleItem = carriedItem;
                carriedItem = null;
                staleItem.ReturnHome();
                NotifyCarriedItemChanged(null);
            }

            if (string.IsNullOrWhiteSpace(carriedItemId))
            {
                unresolvedSavedItemId = null;
                return true;
            }

            Carryable[] candidates = FindObjectsByType<Carryable>(FindObjectsInactive.Include);
            for (int i = 0; i < candidates.Length; i++)
            {
                Carryable candidate = candidates[i];
                if (candidate == null ||
                    !string.Equals(candidate.PersistentSaveId, carriedItemId, StringComparison.Ordinal) ||
                    !MatchesSavedCategory(candidate, carriedCategoryId) ||
                    !candidate.CanBeCarried || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                bool restored = TryCarry(candidate);
                if (restored)
                    unresolvedSavedItemId = null;
                return restored;
            }

            if (!string.Equals(
                    unresolvedSavedItemId,
                    carriedItemId,
                    StringComparison.Ordinal))
            {
                unresolvedSavedItemId = carriedItemId;
                Debug.LogWarning(
                    $"Saved carried item '{carriedItemId}'" +
                    (string.IsNullOrWhiteSpace(carriedCategoryId)
                        ? string.Empty
                        : $" (category '{carriedCategoryId}')") +
                    $" could not be restored in scene '{gameObject.scene.name}'.");
            }

            return false;
        }

        private static bool MatchesSavedCategory(
            Carryable candidate,
            string carriedCategoryId)
        {
            return string.IsNullOrWhiteSpace(carriedCategoryId) ||
                candidate.Category != null && string.Equals(
                    candidate.Category.Id,
                    carriedCategoryId,
                    StringComparison.Ordinal);
        }

        private void NotifyCarriedItemChanged(Carryable item)
        {
            CarriedItemChanged?.Invoke(item);
        }
    }
}
