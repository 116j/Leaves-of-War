using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DialogueSpeaker : MonoBehaviour
    {
        public enum State { Idle, Talking, Waiting }

        [Tooltip("Id used by the dialogue lines to point at this character (lower case, no spaces).")]
        [SerializeField] private string speakerId = "carlo";
        [Tooltip("Name shown above the subtitles.")]
        [SerializeField] private string displayName = "Carlo";

        [Header("Animation")]
        [Tooltip("Empty = the first Animator found on this object or its children.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleState = "Idle";
        [Tooltip("Played while the voice of a piece is playing.")]
        [SerializeField] private string talkingState = "talking";
        [Tooltip("Played while waiting for the player to move on to the next piece. If the state does not exist, Idle is used.")]
        [SerializeField] private string waitingState = "waiting";
        [Tooltip("Played while the character walks (enters or leaves). If the state does not exist, nothing changes.")]
        [SerializeField] private string walkingState = "Walking";
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.2f;

        [Header("Voice")]
        [Tooltip("Optional 3D AudioSource on the character. Empty = the voice plays in 2D.")]
        [SerializeField] private AudioSource voiceSource;

        private static readonly int IsTalkingHash = Animator.StringToHash("IsTalking");
        private static readonly int IsWaitingHash = Animator.StringToHash("IsWaiting");

        private LookAtPlayer lookAtPlayer;
        private State current = State.Idle;

        public string SpeakerId => speakerId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? speakerId : displayName;
        public AudioSource VoiceSource => voiceSource;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false;
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            lookAtPlayer = GetComponentInChildren<LookAtPlayer>();
            if (voiceSource != null)
            {
                voiceSource.playOnAwake = false;
                AudioManager.Route(voiceSource, AudioBus.Voice);
            }
        }

        public void SetInDialogue(bool inDialogue)
        {
            if (lookAtPlayer != null)
                lookAtPlayer.SetDialogueActive(inDialogue);
            if (!inDialogue)
                SetState(State.Idle);
        }

        public void SetState(State state)
        {
            if (animator == null || !animator.isActiveAndEnabled || state == current)
                return;
            current = state;

            SetBoolIfPresent(IsTalkingHash, state == State.Talking);
            SetBoolIfPresent(IsWaitingHash, state == State.Waiting);

            string stateName = state == State.Talking ? talkingState : state == State.Waiting ? waitingState : idleState;
            int hash = Animator.StringToHash(stateName ?? string.Empty);
            if (!animator.HasState(0, hash))
            {
                if (state != State.Waiting)
                    return;
                hash = Animator.StringToHash(idleState ?? string.Empty);
                if (!animator.HasState(0, hash))
                    return;
            }
            animator.CrossFadeInFixedTime(hash, crossFadeSeconds, 0);
        }

        public void SetWalking(bool walking)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return;
            int hash = Animator.StringToHash((walking ? walkingState : idleState) ?? string.Empty);
            if (!animator.HasState(0, hash))
                return;
            current = State.Idle;
            animator.CrossFadeInFixedTime(hash, crossFadeSeconds, 0);
        }

        private void SetBoolIfPresent(int hash, bool value)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(hash, value);
                    return;
                }
            }
        }
    }
}
