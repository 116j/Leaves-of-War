using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Continuously rotates this character (yaw only, stays upright) to
    /// face the player, for as long as this component is enabled. Attach
    /// alongside CharacterTalkAnimator on any character model that should
    /// maintain eye contact with the player while present in the scene.
    ///
    /// For a therapy patient, this is naturally "look at the player the
    /// whole time you're talking to them" - the model only exists in the
    /// scene for the duration of that patient's session anyway.
    ///
    /// For an ambient character that's also present OUTSIDE of dialogue
    /// (e.g. Willowet wandering the manor), attaching this would make them
    /// stare at the player all the time, not just mid-conversation. If you
    /// want it scoped to dialogue only for a character like that, toggle
    /// `enabled` off/on from the same place you call BeginTalking/EndTalking
    /// or the LineStarted/LineFinished handlers, instead of leaving it
    /// always-on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LookAtPlayer : MonoBehaviour
    {
        [Tooltip("Degrees per second the character turns to face the player. Lower feels less snappy/robotic.")]
        [SerializeField] private float turnSpeedDegreesPerSecond = 240f;

        private Transform playerTransform;

        private void LateUpdate()
        {
            if (playerTransform == null && !TryFindPlayer())
                return;

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f; // keep the character upright, no pitching up/down
            if (toPlayer.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private bool TryFindPlayer()
        {
            GameSession session = GameSession.Instance;
            if (session == null ||
                !session.SceneServices.TryGetPlayer(out global::FirstPersonController player, out _))
            {
                return false;
            }

            playerTransform = player.transform;
            return true;
        }
    }
}