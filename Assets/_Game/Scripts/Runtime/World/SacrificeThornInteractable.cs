using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SacrificeThornInteractable : MonoBehaviour, IInteractable
    {
        private const string CommitPrompt = "COMMIT SACRIFICE";

        private bool available;

        public string Prompt => available ? CommitPrompt : string.Empty;
        public bool IsCommitted { get; private set; }

        public event Action Committed;

        public void Arm()
        {
            IsCommitted = false;
            available = true;
            gameObject.SetActive(true);
        }

        public void Disarm()
        {
            available = false;
            gameObject.SetActive(false);
        }

        public void Interact()
        {
            if (!available || IsCommitted)
                return;

            available = false;
            IsCommitted = true;
            Committed?.Invoke();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            available = false;
        }
    }
}
