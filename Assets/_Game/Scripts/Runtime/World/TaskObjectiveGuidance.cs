using System;
using System.Collections;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Displays a concise, authored objective while its scene root is active.
    /// Intended for a task that needs more direction than a contextual prompt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskObjectiveGuidance : MonoBehaviour
    {
        [SerializeField] private TaskObjective objective;
        [SerializeField, TextArea(2, 3)] private string instruction;
        [SerializeField, TextArea(2, 3)] private string completedInstruction;

        private GameSessionRegistration sessionRegistration;
        private GameSession session;
        private TaskProgress subscribedTaskProgress;
        private bool isDisplaying;

        public TaskObjective Objective => objective;
        public string Instruction => instruction ?? string.Empty;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(BindSession, UnbindSession);
        }

        private void OnEnable()
        {
            sessionRegistration?.Enable();
        }

        private void Start()
        {
            StartCoroutine(RefreshGuidanceWithRetry());
        }

        /// <summary>
        /// On a (re)load - especially loading a save into the same scene,
        /// where every scene object is freshly reconstructed - the
        /// narrative presenter this guidance needs (via IDiegeticHud) may
        /// not have registered itself yet at this exact point in the
        /// scene's startup order. A single failed attempt here used to
        /// leave the HUD permanently blank until some unrelated event (e.g.
        /// further progress on this same task) happened to trigger another
        /// refresh. Retrying for a handful of frames instead of giving up
        /// after one too-early attempt fixes that without needing to touch
        /// scene-service registration itself.
        /// </summary>
        private IEnumerator RefreshGuidanceWithRetry()
        {
            for (int attempt = 0; attempt < 10 && !isDisplaying; attempt++)
            {
                RefreshGuidance();
                if (isDisplaying)
                    yield break;
                yield return null;
            }
        }

        private void OnDisable()
        {
            sessionRegistration?.Disable();
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }

        private void BindSession(GameSession current)
        {
            session = current;
            if (session == null)
                return;

            session.SceneServices.Changed += RefreshGuidance;
            RebindTaskProgress();
            RefreshGuidance();
        }

        private void UnbindSession(GameSession previous)
        {
            if (previous != null)
                previous.SceneServices.Changed -= RefreshGuidance;

            UnbindTaskProgress();
            ClearGuidance();
            session = null;
        }

        private void RebindTaskProgress()
        {
            TaskProgress current = session != null && session.State != null
                ? session.State.Tasks
                : null;
            if (ReferenceEquals(current, subscribedTaskProgress))
                return;

            UnbindTaskProgress();
            subscribedTaskProgress = current;
            if (subscribedTaskProgress != null)
            {
                subscribedTaskProgress.ProgressChanged += HandleTaskProgressChanged;
                subscribedTaskProgress.RequirementsChanged += RefreshGuidance;
            }
        }

        private void UnbindTaskProgress()
        {
            if (subscribedTaskProgress != null)
            {
                subscribedTaskProgress.ProgressChanged -= HandleTaskProgressChanged;
                subscribedTaskProgress.RequirementsChanged -= RefreshGuidance;
            }
            subscribedTaskProgress = null;
        }

        private void HandleTaskProgressChanged(TaskObjective changedObjective, int count)
        {
            if (objective == null || changedObjective == null ||
                !string.Equals(objective.Id, changedObjective.Id, StringComparison.Ordinal))
            {
                return;
            }

            RefreshGuidance();
        }

        private void RefreshGuidance()
        {
            RebindTaskProgress();
            if (!CanDisplayGuidance(out int count, out int required) ||
                !TryGetHud(out IDiegeticHud hud))
            {
                ClearGuidance();
                return;
            }

            hud.SetGameplayHud(FormatMessage(count, required));
            isDisplaying = true;
        }

        private bool CanDisplayGuidance(out int count, out int required)
        {
            count = 0;
            required = 0;
            if (objective == null || session == null || session.State == null ||
                !IsFlagGateSatisfied())
            {
                return false;
            }

            TaskProgress tasks = session.State.Tasks;
            count = tasks.CountFor(objective);
            required = tasks.RequiredFor(objective, 0);
            return required > 0 &&
                (count < required || !string.IsNullOrWhiteSpace(completedInstruction));
        }

        private bool IsFlagGateSatisfied()
        {
            FlagGatedObject gate = GetComponent<FlagGatedObject>();
            return gate == null || gate.Flag == null ||
                session.State.HasFlag(gate.Flag) == gate.ActiveWhenSet;
        }

        private bool TryGetHud(out IDiegeticHud hud)
        {
            hud = null;
            return session != null &&
                session.SceneServices.TryGetPresenter(
                    out INarrativePresenter presenter,
                    out _) &&
                (hud = presenter as IDiegeticHud) != null;
        }

        private string FormatMessage(int count, int required)
        {
            bool isComplete = count >= required;
            string guidance = (isComplete ? completedInstruction : instruction).Trim();
            if (isComplete && !string.IsNullOrWhiteSpace(guidance))
                return guidance;

            string label = objective.PlayerFacingLabel;
            return string.IsNullOrWhiteSpace(guidance)
                ? $"{label} {count}/{required}"
                : $"{label} {count}/{required}\n{guidance}";
        }

        private void ClearGuidance()
        {
            if (!isDisplaying)
                return;

            if (TryGetHud(out IDiegeticHud hud))
                hud.ClearGameplayHud();
            isDisplaying = false;
        }
    }
}