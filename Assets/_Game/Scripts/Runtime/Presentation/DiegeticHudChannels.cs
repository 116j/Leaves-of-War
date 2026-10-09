using System;
using Hortensia.Narrative;
using TMPro;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Reusable diegetic feedback channels independent of narrative playback.
    /// Scene-service changes drive player and carry subscriptions; only the
    /// narrative-state reference needs a lightweight per-frame rebind check.
    /// </summary>
    internal sealed class DiegeticHudChannels : IDisposable
    {
        private readonly NarrativeUiView view;
        private readonly RetroUiTheme theme;
        private readonly GameSessionRegistration sessionRegistration;
        private GameSession session;
        private FirstPersonController playerController;
        private CarriedItemHolder carriedItemHolder;
        private TaskProgress subscribedTaskProgress;
        private NarrativeState subscribedState;
        private Camera playerCamera;
        private ISequenceClock clock;
        private float statusVisibleUntil;
        private readonly ObjectiveDirectionArrowController objectiveDirection;

        public DiegeticHudChannels(
            NarrativeUiView view,
            RetroUiTheme theme,
            ISequenceClock clock)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.theme = RetroUiTheme.Resolve(theme);
            this.clock = clock ?? UnitySequenceClock.Instance;
            objectiveDirection = new ObjectiveDirectionArrowController(
                view.ObjectiveDirectionArrow,
                this.theme);
            sessionRegistration = new GameSessionRegistration(BindSession, UnbindSession);
        }

        public void Enable()
        {
            sessionRegistration.Enable();
        }

        public void Disable()
        {
            sessionRegistration.Disable();
            ClearAll();
        }

        public void Dispose()
        {
            sessionRegistration.Dispose();
            ClearAll();
        }

        public void SetClock(ISequenceClock sequenceClock)
        {
            clock = sequenceClock ?? UnitySequenceClock.Instance;
        }

        public void Tick()
        {
            RebindNarrativeState();
            RebindTaskProgress();
            TMP_Text status = view.StatusText;
            if (status != null &&
                status.gameObject.activeSelf &&
                clock.UnscaledTime >= statusVisibleUntil)
            {
                status.gameObject.SetActive(false);
            }

            if (GameSettings.Current.inGameTextEnabled)
            {
                objectiveDirection.Tick(
                    session,
                    playerController,
                    playerCamera,
                    carriedItemHolder);
            }
            else
            {
                objectiveDirection.Clear();
            }
        }

        public void ShowStatus(string message)
        {
            bool visible = SetTextVisible(view.StatusText, message);
            statusVisibleUntil = visible
                ? clock.UnscaledTime + theme.StatusDurationSeconds
                : 0f;
        }

        public void SetCarryLabel(string label)
        {
            string message = string.IsNullOrWhiteSpace(label)
                ? null
                : $"CARRYING: {label.Trim().ToUpperInvariant()}";
            SetTextVisible(view.CarryStatusText, message);
        }

        public void SetGameplayHud(string message)
        {
            SetTextVisible(view.GameplayHudText, message);
        }

        public void ClearGameplayHud()
        {
            SetGameplayHud(null);
        }

        public void ClearTransient()
        {
            ShowStatus(null);
            ClearGameplayHud();
        }

        private void ClearAll()
        {
            ClearTransient();
            objectiveDirection.Clear();
            SetInteractionPrompt(string.Empty);
            SetCarryLabel(null);
        }

        private void BindSession(GameSession current)
        {
            if (current == null)
                return;

            session = current;
            session.SceneServices.Changed += HandleSceneServicesChanged;
            HandleSceneServicesChanged();
            RebindNarrativeState();
            RebindTaskProgress();
        }

        private void UnbindSession(GameSession previous)
        {
            if (previous != null)
                previous.SceneServices.Changed -= HandleSceneServicesChanged;

            UnbindPlayerController();
            UnbindCarriedItemHolder();
            UnbindTaskProgress();
            UnbindNarrativeState();
            objectiveDirection.Clear();
            session = null;
        }

        private void HandleSceneServicesChanged()
        {
            RebindPlayerController();
            RebindCarriedItemHolder();
            objectiveDirection.Invalidate();
        }

        private void RebindPlayerController()
        {
            FirstPersonController current = null;
            session?.SceneServices.TryGetPlayer(out current, out _);
            if (ReferenceEquals(current, playerController))
                return;

            UnbindPlayerController();
            playerController = current;
            playerCamera = null;
            session?.SceneServices.TryGetPlayerCamera(out playerCamera, out _);
            if (playerController != null)
                playerController.InteractionPromptChanged += SetInteractionPrompt;
            SetInteractionPrompt(
                playerController != null ? playerController.InteractionPrompt : string.Empty);
        }

        private void UnbindPlayerController()
        {
            if (playerController != null)
                playerController.InteractionPromptChanged -= SetInteractionPrompt;
            playerController = null;
            playerCamera = null;
            SetInteractionPrompt(string.Empty);
        }

        private void RebindCarriedItemHolder()
        {
            CarriedItemHolder current = null;
            session?.SceneServices.TryGetCarriedItemHolder(out current, out _);
            if (ReferenceEquals(current, carriedItemHolder))
                return;

            UnbindCarriedItemHolder();
            carriedItemHolder = current;
            if (carriedItemHolder != null)
                carriedItemHolder.CarriedItemChanged += HandleCarriedItemChanged;
            HandleCarriedItemChanged(
                carriedItemHolder != null ? carriedItemHolder.CarriedItem : null);
        }

        private void UnbindCarriedItemHolder()
        {
            if (carriedItemHolder != null)
                carriedItemHolder.CarriedItemChanged -= HandleCarriedItemChanged;
            carriedItemHolder = null;
            SetCarryLabel(null);
        }

        private void HandleCarriedItemChanged(Carryable item)
        {
            SetCarryLabel(item != null ? item.PlayerFacingLabel : null);
            objectiveDirection.Invalidate();
        }

        private void RebindNarrativeState()
        {
            NarrativeState current = session != null ? session.State : null;
            if (ReferenceEquals(current, subscribedState))
                return;

            UnbindNarrativeState();
            subscribedState = current;
            if (subscribedState != null)
                subscribedState.FlagChanged += HandleFlagChanged;
            objectiveDirection.Invalidate();
        }

        private void UnbindNarrativeState()
        {
            if (subscribedState != null)
                subscribedState.FlagChanged -= HandleFlagChanged;
            subscribedState = null;
        }

        private void HandleFlagChanged(FlagId flag, bool present) =>
            objectiveDirection.Invalidate();

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
                subscribedTaskProgress.ProgressChanged += HandleTaskProgressChanged;
        }

        private void UnbindTaskProgress()
        {
            if (subscribedTaskProgress != null)
                subscribedTaskProgress.ProgressChanged -= HandleTaskProgressChanged;
            subscribedTaskProgress = null;
        }

        private void HandleTaskProgressChanged(TaskObjective objective, int count)
        {
            objectiveDirection.Invalidate();
        }

        private void SetInteractionPrompt(string prompt)
        {
            if (view.InteractionPromptText != null)
            {
                view.InteractionPromptText.text = string.IsNullOrWhiteSpace(prompt) || !GameSettings.Current.inGameTextEnabled
                    ? string.Empty
                    : prompt;
            }
        }

        private static bool SetTextVisible(TMP_Text target, string message)
        {
            if (target == null)
                return false;

            bool visible = !string.IsNullOrWhiteSpace(message) && GameSettings.Current.inGameTextEnabled;
            target.text = visible ? message : string.Empty;
            target.gameObject.SetActive(visible);
            return visible;
        }
    }
}