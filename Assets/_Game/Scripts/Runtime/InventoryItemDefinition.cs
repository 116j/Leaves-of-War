using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Data for one inventory item. Works for both generic items (shown with
    /// a description) and documents/letters (opened as a readable document
    /// when selected, reusing the existing document presentation system).
    /// </summary>
    [CreateAssetMenu(menuName = "Hortensia/Inventory/Item", fileName = "InventoryItem")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [Tooltip("Shown in the detail panel for generic (non-document) items.")]
        [TextArea]
        [SerializeField] private string description;

        [Header("Document (optional)")]
        [Tooltip("If set, selecting this item in the inventory opens it as a document instead of showing the description above.")]
        [SerializeField] private DocumentDefinition linkedDocument;
        [Tooltip("Played once when this item is opened, right before the document appears (e.g. an envelope-opening sound for a letter). Only used if Linked Document is set.")]
        [SerializeField] private AudioClip openSound;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
        public DocumentDefinition LinkedDocument => linkedDocument;
        public AudioClip OpenSound => openSound;
        public bool IsDocument => linkedDocument != null;
    }
}