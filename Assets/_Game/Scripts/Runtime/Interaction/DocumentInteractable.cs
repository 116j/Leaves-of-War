using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "READ";
        [SerializeField] private ScriptableObject document;

        [Header("Pocket Item (optional)")]
        [Tooltip("If set, interacting picks this up into the inventory instead of opening the document immediately - it can then be read later from the inventory grid. Leave empty to keep the original behaviour (opens straight away).")]
        [SerializeField] private InventoryItemDefinition inventoryItem;

        private GameSessionRegistration sessionRegistration;

        public string Prompt => prompt;
        public DocumentDefinition Document => document as DocumentDefinition;
        public IReadableDocument ReadableDocument => document as IReadableDocument;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterDocument(this),
                session => session.UnregisterDocument(this));
        }

        private void OnEnable() => sessionRegistration?.Enable();

        private void Start()
        {
            // Pocket documents are represented by the saved inventory id, not
            // this scene object's transient active state. A reload therefore
            // cannot put an already-collected letter back into the world.
            if (inventoryItem != null &&
                GameSession.Instance != null &&
                GameSession.Instance.State != null &&
                GameSession.Instance.State.Inventory.HasItem(inventoryItem.ItemId))
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable() => sessionRegistration?.Disable();

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }

        public void Interact()
        {
            IReadableDocument readableDocument = ReadableDocument;
            if (readableDocument == null)
            {
                Debug.LogWarning(
                    $"{nameof(DocumentInteractable)} on '{name}' has no readable document assigned.",
                    this);
                return;
            }

            GameSession session = GameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning(
                    $"{nameof(DocumentInteractable)} on '{name}' cannot open '{readableDocument.Title}' " +
                    $"without an active {nameof(GameSession)}.",
                    this);
                return;
            }

            if (inventoryItem != null)
            {
                PickUp(session);
                return;
            }

            session.OpenDocument(readableDocument);
        }

        /// <summary>
        /// Puts this document into the player's inventory instead of opening
        /// it right away, then removes it from the world (it now lives in the
        /// player's pocket). It can be read later from the inventory grid,
        /// which opens it the same way this object would have.
        /// </summary>
        private void PickUp(GameSession session)
        {
            if (!session.SceneServices.TryGetPlayer(out global::FirstPersonController player, out _))
            {
                Debug.LogWarning(
                    $"{nameof(DocumentInteractable)} on '{name}' cannot pick up '{inventoryItem.DisplayName}' " +
                    "without a registered player.",
                    this);
                return;
            }

            InventoryHolder holder = player.GetComponent<InventoryHolder>();
            if (holder == null)
                holder = player.gameObject.AddComponent<InventoryHolder>();

            if (holder.AddItem(inventoryItem))
            {
                session.GiveInventoryItem(inventoryItem.ItemId);
                gameObject.SetActive(false); // Picked up - remove from the world.
            }
        }

        private void OnValidate()
        {
            if (document != null && !(document is IReadableDocument))
                Debug.LogWarning($"'{document.name}' is not a readable document.", this);
        }
    }
}
