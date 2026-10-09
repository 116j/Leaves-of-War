using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FlagInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "INTERACT";
        [SerializeField] private FlagId flag;
        [SerializeField] private bool setOnPlayerTrigger;
        [SerializeField] private bool disableAfterUse;
        [SerializeField] private CarryCategory requiresCarried;
        [Tooltip("Optional unscaled pose used when a non-retained carried item is consumed. " +
            "Defaults to this interaction transform.")]
        [SerializeField] private Transform usedItemDestination;

        [Header("Sound")]
        [Tooltip("Played once when this interaction successfully sets the flag.")]
        [SerializeField] private AudioClip interactSound;
        [Range(0f, 1f)]
        [SerializeField] private float interactSoundVolume = 1f;

        public string Prompt
        {
            get
            {
                // Once the flag is already set, this interactable has
                // nothing left to offer - hide the prompt regardless of
                // whether it requires a carried item or not. Previously this
                // check only ran inside the requiresCarried branch, so a
                // plain flag-setter (like a bed with no item requirement)
                // kept showing its prompt forever after first use.
                GameSession session = GameSession.Instance;
                if (flag != null && session != null && session.State != null &&
                    session.State.HasFlag(flag))
                {
                    return string.Empty;
                }

                if (requiresCarried == null)
                    return prompt;

                return session != null &&
                    session.TryGetCarriedItemHolder(out CarriedItemHolder holder) &&
                    holder.Has(requiresCarried)
                        ? prompt
                        : string.Empty;
            }
        }

        public CarryCategory RequiresCarried => requiresCarried;
        public Transform UsedItemDestination => usedItemDestination;
        public FlagId Flag => flag;
        public bool SetsOnPlayerTrigger => setOnPlayerTrigger;
        public bool DisablesAfterUse => disableAfterUse;

        private void OnEnable()
        {
            RestoreCompletedCarriedUse();
        }

        private void Start()
        {
            RestoreCompletedCarriedUse();

            if (disableAfterUse &&
                flag != null &&
                GameSession.Instance != null &&
                GameSession.Instance.State != null &&
                GameSession.Instance.State.HasFlag(flag))
            {
                gameObject.SetActive(false);
            }
        }

        private void RestoreCompletedCarriedUse()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.State == null || flag == null ||
                !session.State.HasFlag(flag) || requiresCarried == null ||
                requiresCarried.RetainedAfterUse || usedItemDestination == null ||
                !session.TryGetCarriedItemHolder(out CarriedItemHolder holder) ||
                !holder.Has(requiresCarried))
            {
                return;
            }

            holder.TryUse(requiresCarried, usedItemDestination);
        }

        public void Interact()
        {
            SetFlag(null);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!setOnPlayerTrigger || other == null)
                return;

            if (other.GetComponentInParent<global::FirstPersonController>() != null)
                SetFlag(other.GetComponentInParent<CarriedItemHolder>());
        }

        private void SetFlag(CarriedItemHolder suppliedHolder)
        {
            if (flag == null)
            {
                Debug.LogWarning($"{nameof(FlagInteractable)} on '{name}' has no flag assigned.", this);
                return;
            }

            GameSession session = GameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning($"{nameof(FlagInteractable)} on '{name}' cannot set '{flag.name}' without an active {nameof(GameSession)}.", this);
                return;
            }

            if (session.State == null)
                return;

            if (requiresCarried != null && !session.State.HasFlag(flag))
            {
                CarriedItemHolder holder = suppliedHolder;
                if (holder == null && !session.TryGetCarriedItemHolder(out holder))
                    return;
                Transform destination = usedItemDestination != null
                    ? usedItemDestination
                    : transform;
                if (!holder.TryUse(requiresCarried, destination))
                    return;
            }

            session.SetFlag(flag);
            if (interactSound != null)
                AudioManager.Instance?.PlaySoundEffect(interactSound, interactSoundVolume);
            if (disableAfterUse)
                gameObject.SetActive(false);
        }
    }
}