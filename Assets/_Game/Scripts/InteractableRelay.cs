using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Forwards interaction to another IInteractable elsewhere in the scene.
    /// Useful when the collider that should trigger an interaction (e.g. a
    /// door handle, which needs to be a child of the door itself so it moves
    /// with it) can't simply be a child of the object holding the real
    /// IInteractable script (interaction lookup only walks up an object's own
    /// parent chain, not sideways to unrelated objects).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractableRelay : MonoBehaviour, IInteractable
    {
        [Tooltip("The object with the real IInteractable script (e.g. the Wardrobe Openable on 'Wardrobe Interactable'). Must be a MonoBehaviour implementing IInteractable.")]
        [SerializeField] private MonoBehaviour target;

        private IInteractable Target => target as IInteractable;

        public string Prompt => Target != null ? Target.Prompt : string.Empty;

        public void Interact()
        {
            Target?.Interact();
        }
    }
}