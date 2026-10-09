using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Attach to any scene object the player should be able to examine (not
    /// carry). Assign an InspectableItemDefinition with its model and
    /// description; interacting opens ObjectInspector's 3D view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InspectableInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "EXAMINE";
        [SerializeField] private InspectableItemDefinition item;

        public string Prompt => item != null && item.Model != null ? prompt : string.Empty;

        public void Interact()
        {
            if (item == null)
                return;

            ObjectInspector.EnsureInstance().Inspect(item);
        }
    }
}