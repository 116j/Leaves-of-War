using UnityEngine;
using UnityEngine.InputSystem;

namespace Hortensia.Runtime
{
    public interface ISequenceClock
    {
        float UnscaledTime { get; }
        float UnscaledDeltaTime { get; }
    }

    /// <summary>
    /// Optional capability of the production clock. Test clocks stay plain
    /// <see cref="ISequenceClock"/> implementations, while runtime pause can
    /// freeze every system that receives the shared production clock.
    /// </summary>
    public interface IPausableSequenceClock : ISequenceClock
    {
        bool IsPaused { get; }
        void SetPaused(bool paused);
    }

    public enum SequenceInputAction
    {
        Advance,
        Choice1,
        Choice2,
        Choice3,
        NextPage,
        PreviousPage,
        Cancel,
        Fire,
        Interact
    }

    /// <summary>
    /// Small frame-input boundary shared by authored set-pieces and runtime
    /// narrative presentation. Tests can provide deterministic implementations
    /// without replacing private delegates through reflection.
    /// </summary>
    public interface ISequenceInput
    {
        bool WasPressed(SequenceInputAction action);
        bool PointerIsCaptured { get; }
    }

    public sealed class UnitySequenceClock : IPausableSequenceClock
    {
        public static readonly UnitySequenceClock Instance = new UnitySequenceClock();

        private bool paused;
        private float pausedAt;
        private float pausedOffset;

        private UnitySequenceClock()
        {
        }

        public bool IsPaused => paused;
        public float UnscaledTime => paused ? pausedAt : Time.unscaledTime - pausedOffset;
        public float UnscaledDeltaTime => paused ? 0f : Time.unscaledDeltaTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Instance.paused = false;
            Instance.pausedAt = 0f;
            Instance.pausedOffset = 0f;
        }

        public void SetPaused(bool value)
        {
            if (paused == value)
                return;

            if (value)
            {
                pausedAt = Time.unscaledTime - pausedOffset;
                paused = true;
                return;
            }

            pausedOffset = Time.unscaledTime - pausedAt;
            paused = false;
        }
    }

    public sealed class UnitySequenceInput : ISequenceInput
    {
        public static readonly UnitySequenceInput Instance = new UnitySequenceInput();

        private UnitySequenceInput()
        {
        }

        public bool PointerIsCaptured =>
            !UnitySequenceClock.Instance.IsPaused &&
            Cursor.lockState == CursorLockMode.Locked;

        public bool WasPressed(SequenceInputAction action)
        {
            // Sequence players receive this service directly. Suppressing input
            // here keeps every production consumer inert behind the pause menu,
            // including set-pieces that do not present through NarrativeContentPresenter.
            if (UnitySequenceClock.Instance.IsPaused)
                return false;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            switch (action)
            {
                case SequenceInputAction.Advance:
                    return (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                        (keyboard != null &&
                         (keyboard.spaceKey.wasPressedThisFrame ||
                          keyboard.enterKey.wasPressedThisFrame ||
                          keyboard.numpadEnterKey.wasPressedThisFrame));

                case SequenceInputAction.Choice1:
                    return keyboard != null && keyboard.digit1Key.wasPressedThisFrame;
                case SequenceInputAction.Choice2:
                    return keyboard != null && keyboard.digit2Key.wasPressedThisFrame;
                case SequenceInputAction.Choice3:
                    return keyboard != null && keyboard.digit3Key.wasPressedThisFrame;

                case SequenceInputAction.NextPage:
                    return (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                        (keyboard != null &&
                         (keyboard.rightArrowKey.wasPressedThisFrame ||
                          keyboard.spaceKey.wasPressedThisFrame));

                case SequenceInputAction.PreviousPage:
                    return (mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                        (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame);

                case SequenceInputAction.Cancel:
                    // Documents used to close on a hardcoded Escape, which meant
                    // Escape could not also open the pause menu while reading
                    // one. Closing a document now uses its own dedicated,
                    // rebindable key (default Backspace), freeing Escape to
                    // always open Pause instead.
                    return WasLiveCloseDocumentPressed(keyboard);

                case SequenceInputAction.Fire:
                    return mouse != null && mouse.leftButton.wasPressedThisFrame;

                case SequenceInputAction.Interact:
                    return WasLiveInteractPressed(keyboard);

                default:
                    return false;
            }
        }

        private static bool WasLiveInteractPressed(Keyboard keyboard)
        {
            GameSession session = GameSession.Instance;
            if (session != null &&
                session.SceneServices.TryGetPlayer(
                    out global::FirstPersonController player,
                    out _))
            {
                InputAction interactAction = player.InteractAction;
                return interactAction != null && interactAction.WasPressedThisFrame();
            }

            // A scene without a registered player has no rebindable input map.
            // Preserve the authored fallback for that unusual direct-play path.
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
        }

        private static bool WasLiveCloseDocumentPressed(Keyboard keyboard)
        {
            GameSession session = GameSession.Instance;
            if (session != null &&
                session.SceneServices.TryGetPlayer(
                    out global::FirstPersonController player,
                    out _))
            {
                InputAction closeAction = player.CloseDocumentAction;
                return closeAction != null && closeAction.WasPressedThisFrame();
            }

            // A scene without a registered player has no rebindable input map.
            // Fall back to the authored default (Backspace) for that unusual
            // direct-play path.
            return keyboard != null && keyboard.backspaceKey.wasPressedThisFrame;
        }
    }
}