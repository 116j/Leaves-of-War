using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Drives a character's Idle/Talking animation state via a single
    /// "IsTalking" bool parameter on its Animator Controller. Attach to any
    /// NPC that appears in dialogue, alongside its Animator component.
    ///
    /// Two independent things have to both be true for Talking to play:
    ///   1. It's this character's TURN to speak - either automatically, by
    ///      assigning the same SpeakerDefinition its LineBeats use (via
    ///      ChapterRunner's LineStarted/LineFinished events), or manually via
    ///      BeginTurn()/EndTurn() (e.g. TherapyPatientPresenter calls these
    ///      directly, since patients don't have a SpeakerDefinition).
    ///   2. Voice audio is ACTUALLY PLAYING right now, per
    ///      NarrativePresenter's VoiceStarted/VoiceStopped events.
    ///
    /// This means Talking switches to Idle not just when the whole line
    /// ends, but also mid-line for a segmented line pausing between segments
    /// while it waits for the player to advance - matching "idle whenever
    /// it's waiting for me to proceed or for the next line to start".
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterTalkAnimator : MonoBehaviour
    {
        [Tooltip("The SpeakerDefinition this character represents in dialogue. Must match the Speaker assigned on the LineBeats this character speaks. Leave empty for characters driven manually via BeginTurn()/EndTurn() instead (e.g. therapy patients).")]
        [SerializeField] private SpeakerDefinition speaker;

        private static readonly int IsTalkingHash = Animator.StringToHash("IsTalking");

        private Animator animator;
        private bool isMyTurn;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            ChapterRunner.LineStarted += HandleLineStarted;
            ChapterRunner.LineFinished += HandleLineFinished;
            NarrativePresenter.VoiceStarted += HandleVoiceStarted;
            NarrativePresenter.VoiceStopped += HandleVoiceStopped;
        }

        private void OnDisable()
        {
            ChapterRunner.LineStarted -= HandleLineStarted;
            ChapterRunner.LineFinished -= HandleLineFinished;
            NarrativePresenter.VoiceStarted -= HandleVoiceStarted;
            NarrativePresenter.VoiceStopped -= HandleVoiceStopped;
            isMyTurn = false;
            SetTalking(false);
        }

        private void HandleLineStarted(SpeakerDefinition lineSpeaker)
        {
            if (speaker != null && lineSpeaker == speaker)
                BeginTurn();
        }

        private void HandleLineFinished(SpeakerDefinition lineSpeaker)
        {
            if (speaker != null && lineSpeaker == speaker)
                EndTurn();
        }

        private void HandleVoiceStarted()
        {
            if (isMyTurn)
                SetTalking(true);
        }

        private void HandleVoiceStopped()
        {
            if (isMyTurn)
                SetTalking(false);
        }

        /// <summary>
        /// Marks this character as the one currently speaking. Talking
        /// itself only starts once voice audio actually plays (VoiceStarted)
        /// - call this right as (or just before) that happens.
        /// </summary>
        public void BeginTurn()
        {
            isMyTurn = true;
        }

        /// <summary>
        /// Marks this character as done speaking and forces Idle immediately
        /// as a safety net, even if VoiceStopped didn't fire for some reason
        /// (e.g. a line with no audio clip).
        /// </summary>
        public void EndTurn()
        {
            isMyTurn = false;
            SetTalking(false);
        }

        private void SetTalking(bool talking)
        {
            if (animator != null)
                animator.SetBool(IsTalkingHash, talking);
        }
    }
}