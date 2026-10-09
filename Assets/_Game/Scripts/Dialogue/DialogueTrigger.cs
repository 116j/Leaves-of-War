using UnityEngine;
using UnityEngine.Events;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DialogueTrigger : MonoBehaviour, IInteractable
    {
        [Tooltip("Text shown when the player looks at this object.")]
        [SerializeField] private string prompt = "TALK";
        [Tooltip("Start as soon as the player walks into a trigger collider on this object, without pressing Interact.")]
        [SerializeField] private bool startOnEnter = false;
        [SerializeField] private bool playOnce = true;
        [SerializeField] private DialogueLine[] lines = new DialogueLine[0];
        [Tooltip("Called when the whole dialogue is over.")]
        [SerializeField] private UnityEvent onFinished;

        private bool played;

        public string Prompt => CanPlay ? prompt : string.Empty;

        private bool CanPlay => lines != null && lines.Length > 0 && !(playOnce && played) && !DialogueRunner.IsPlaying;

        public void Interact() => Play();

        public void ResetPlayed() => played = false;

        public void Play()
        {
            if (CanPlay && DialogueRunner.GetOrCreate().Play(lines, () => onFinished?.Invoke()))
                played = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (startOnEnter && other.GetComponentInParent<FirstPersonController>() != null)
                Play();
        }
    }
}
