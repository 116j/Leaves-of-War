using Hortensia.Narrative;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class Carryable : MonoBehaviour, IInteractable
    {
        [SerializeField] private CarryCategory category;
        [SerializeField] private string playerFacingLabel;

        [Header("Sound")]
        [Tooltip("Played once when the player picks this up.")]
        [SerializeField] private AudioClip pickupSound;
        [Range(0f, 1f)]
        [SerializeField] private float pickupSoundVolume = 1f;

        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private bool originCaptured;
        private bool isCarried;
        private bool isPlaced;
        private bool grantedChildAdopted;
        private bool restoredConsumed;
        private bool activeBeforeRestoration;
        private string persistentSaveId;

        public CarryCategory Category => category;
        public string PlayerFacingLabel => !string.IsNullOrWhiteSpace(playerFacingLabel)
            ? playerFacingLabel.Trim()
            : category != null
                ? category.PlayerFacingLabel
                : string.Empty;
        public bool IsCarried => isCarried;
        public bool IsPlaced => isPlaced;
        public bool IsRestoredConsumed => restoredConsumed;
        /// <summary>
        /// Stable identity from the authored scene hierarchy, captured before
        /// the object is re-parented into the player's carried-item holder.
        /// </summary>
        public string PersistentSaveId
        {
            get
            {
                // Inactive chapter-dressing objects may not have received
                // Awake when a restored holder searches the scene. Their
                // authored hierarchy is still available and is the identity
                // the save captured, so resolve it lazily as well.
                CaptureOrigin();
                return persistentSaveId;
            }
        }
        public bool CanBeCarried => category != null && !isCarried && !isPlaced;
        public bool CanAutoAdopt => CanBeCarried && !grantedChildAdopted;
        public string Prompt => CanBeCarried
            ? $"TAKE {PlayerFacingLabel.ToUpperInvariant()}"
            : string.Empty;

        private void Awake()
        {
            CaptureOrigin();
        }

        private void OnEnable()
        {
            if (!CanAutoAdopt)
                return;

            CarriedItemHolder parentHolder = GetComponentInParent<CarriedItemHolder>();
            if (parentHolder != null)
                parentHolder.TryAdoptChild(this);
        }

        public void Interact()
        {
            if (!CanBeCarried)
                return;

            GameSession session = GameSession.Instance;
            if (session != null && session.TryGetCarriedItemHolder(out CarriedItemHolder holder))
                holder.TryCarry(this);
        }

        internal void AttachTo(CarriedItemHolder holder)
        {
            if (holder == null)
                return;

            CaptureOrigin();
            isPlaced = false;
            isCarried = true;
            transform.SetParent(holder.transform, false);
            gameObject.SetActive(false);

            // Played through the shared SFX bus (not a source on this object),
            // since this object is deactivated immediately above and its own
            // AudioSource would be cut off before the sound could finish.
            if (pickupSound != null)
                AudioManager.Instance?.PlaySoundEffect(pickupSound, pickupSoundVolume);
        }

        internal void PlaceAt(Transform destination)
        {
            CaptureOrigin();
            isCarried = false;
            isPlaced = true;
            transform.SetParent(destination, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = originalLocalScale;
            gameObject.SetActive(true);
        }

        internal void ReturnHome()
        {
            CaptureOrigin();
            isCarried = false;
            isPlaced = false;
            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
            gameObject.SetActive(true);
        }

        internal void MarkGrantedChildAdopted()
        {
            grantedChildAdopted = true;
        }

        internal void ClearRestoredPlacement()
        {
            if (!restoredConsumed)
                return;

            CaptureOrigin();
            restoredConsumed = false;
            isCarried = false;
            isPlaced = false;
            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
            gameObject.SetActive(activeBeforeRestoration);
        }

        internal void RestorePlacementAt(Transform destination)
        {
            if (category == null || category.RetainedAfterUse ||
                destination == null || restoredConsumed)
            {
                return;
            }

            CaptureOrigin();
            activeBeforeRestoration = gameObject.activeSelf;
            restoredConsumed = true;
            isCarried = false;
            isPlaced = true;
            transform.SetParent(destination, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = originalLocalScale;
            gameObject.SetActive(true);
            // No sound here: this restores saved state on scene load, not a
            // live player action.
        }

        private void CaptureOrigin()
        {
            if (originCaptured)
                return;

            originalParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            originalLocalScale = transform.localScale;
            persistentSaveId = BuildPersistentSaveId();
            originCaptured = true;
        }

        private string BuildPersistentSaveId()
        {
            var hierarchy = new List<string>();
            for (Transform current = transform; current != null; current = current.parent)
                hierarchy.Add($"{current.GetSiblingIndex()}:{current.name}");
            hierarchy.Reverse();
            return gameObject.scene.name + "|" + string.Join("/", hierarchy);
        }
    }
}
