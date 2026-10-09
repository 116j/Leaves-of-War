using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Projects the resolver's current world destination into a camera-relative
    /// arrow without adding text or a second HUD channel.
    /// </summary>
    public sealed class ObjectiveDirectionArrowController
    {
        private const float ResolveIntervalSeconds = 0.1f;
        private const float MinimumHorizontalDistance = 0.05f;

        private readonly ObjectiveDirectionArrowGraphic arrow;
        private readonly RetroUiTheme theme;
        private GameSession previousSession;
        private NarrativeBeat previousBeat;
        private string previousSequenceId;
        private CarryCategory previousCarriedCategory;
        private int previousChapterIndex = int.MinValue;
        private int previousBeatIndex = int.MinValue;
        private float nextResolveAt;
        private bool hasTarget;
        private Vector3 targetPosition;

        internal ObjectiveDirectionArrowController(
            ObjectiveDirectionArrowGraphic arrow,
            RetroUiTheme theme)
        {
            this.arrow = arrow;
            this.theme = RetroUiTheme.Resolve(theme);
        }

        public void Tick(
            GameSession session,
            global::FirstPersonController player,
            Camera camera,
            CarriedItemHolder holder)
        {
            if (arrow == null || session == null || player == null || camera == null)
            {
                Clear();
                return;
            }

            if (ShouldResolve(session, holder))
            {
                hasTarget = ObjectiveDirectionResolver.TryResolve(
                    session,
                    player.transform.position,
                    holder,
                    out targetPosition);
                CaptureResolutionState(session, holder);
                nextResolveAt = Time.unscaledTime + ResolveIntervalSeconds;
            }

            if (!hasTarget || !TryGetDirection(
                    player.transform.position,
                    camera.transform,
                    targetPosition,
                    out float angle,
                    out float distance))
            {
                SetVisible(false);
                return;
            }

            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            arrow.color = ColorForDistance(distance, theme);
            SetVisible(true);
        }

        public void Invalidate()
        {
            nextResolveAt = 0f;
        }

        public void Clear()
        {
            hasTarget = false;
            SetVisible(false);
            Invalidate();
        }

        public static Color ColorForDistance(float distance, RetroUiTheme theme)
        {
            theme = RetroUiTheme.Resolve(theme);
            if (distance <= theme.ObjectiveArrowNearDistance)
                return theme.ObjectiveArrowNearColor;
            if (distance <= theme.ObjectiveArrowMediumDistance)
                return theme.ObjectiveArrowMediumColor;
            return theme.ObjectiveArrowFarColor;
        }

        public static bool TryGetDirection(
            Vector3 playerPosition,
            Transform cameraTransform,
            Vector3 target,
            out float angle,
            out float distance)
        {
            angle = 0f;
            distance = 0f;
            if (cameraTransform == null)
                return false;

            Vector3 horizontal = Vector3.ProjectOnPlane(target - playerPosition, Vector3.up);
            distance = horizontal.magnitude;
            if (distance < MinimumHorizontalDistance)
                return false;

            horizontal /= distance;
            Vector3 forward = Vector3.ProjectOnPlane(
                cameraTransform.forward,
                Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(
                cameraTransform.right,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f || right.sqrMagnitude < 0.001f)
                return false;

            float horizontalDirection = Vector3.Dot(horizontal, right);
            float verticalDirection = Vector3.Dot(horizontal, forward);
            angle = Mathf.Atan2(-horizontalDirection, verticalDirection) * Mathf.Rad2Deg;
            return true;
        }

        private bool ShouldResolve(GameSession session, CarriedItemHolder holder)
        {
            NarrativeState state = session.State;
            return !ReferenceEquals(previousSession, session) ||
                !ReferenceEquals(previousBeat, session.ActiveNarrativeBeat) ||
                !string.Equals(previousSequenceId, session.ActiveSequenceId, System.StringComparison.Ordinal) ||
                !ReferenceEquals(previousCarriedCategory, holder != null ? holder.Category : null) ||
                previousChapterIndex != (state != null ? state.ChapterIndex : int.MinValue) ||
                previousBeatIndex != (state != null ? state.BeatIndex : int.MinValue) ||
                Time.unscaledTime >= nextResolveAt;
        }

        private void CaptureResolutionState(GameSession session, CarriedItemHolder holder)
        {
            previousSession = session;
            previousBeat = session.ActiveNarrativeBeat;
            previousSequenceId = session.ActiveSequenceId;
            previousCarriedCategory = holder != null ? holder.Category : null;
            NarrativeState state = session.State;
            previousChapterIndex = state != null ? state.ChapterIndex : int.MinValue;
            previousBeatIndex = state != null ? state.BeatIndex : int.MinValue;
        }

        private void SetVisible(bool visible)
        {
            if (arrow != null && arrow.gameObject.activeSelf != visible)
                arrow.gameObject.SetActive(visible);
        }
    }
}
