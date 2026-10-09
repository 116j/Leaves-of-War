using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Stateless-ish helper that drives the OPTIONS > KEY BINDINGS screen.
    ///
    /// The menu often runs with no <see cref="FirstPersonController"/> in the
    /// scene (you are at the main menu, before a game exists), so this helper
    /// owns its own throwaway "capture" action map, built from the exact same
    /// definition the gameplay controller uses (<see cref="FirstPersonController.BuildActionMap"/>).
    /// Because both maps come from that one builder, the binding indices in
    /// <see cref="FirstPersonController.RebindableActions"/> line up in both.
    ///
    /// The menu edits binding overrides here, in memory, and serialises them to
    /// a JSON string. That string is stored in the settings draft and only ever
    /// written to disk when the player presses APPLY, exactly like every other
    /// option. When a game then starts, the live controller loads the same JSON
    /// and applies it. Nothing here touches disk on its own.
    /// </summary>
    public sealed class KeyRebinding : IDisposable
    {
        // These controls always advance or dismiss narrative presentation. They
        // must not become a gameplay shortcut even if the player moves the
        // ordinary Interact binding elsewhere.
        private static readonly HashSet<string> ReservedNarrativePaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "<Keyboard>/space",
                "<Keyboard>/e"
            };

        private readonly InputActionMap captureMap;

        public KeyRebinding()
        {
            // A private mirror of the gameplay map. It is never enabled for
            // gameplay; it exists only so we can attach overrides and read back
            // effective binding paths for display and interactive rebinding.
            captureMap = FirstPersonController.BuildActionMap();
        }

        /// <summary>The rows the screen should render, in order.</summary>
        public IReadOnlyList<FirstPersonController.RebindableAction> Entries =>
            FirstPersonController.RebindableActions;

        /// <summary>
        /// Loads a serialized override string into the capture map. Empty clears
        /// back to defaults. Call this whenever the screen (re)opens so it mirrors
        /// the current draft.
        /// </summary>
        public void LoadFromJson(string overridesJson)
        {
            captureMap.Disable();
            if (string.IsNullOrEmpty(overridesJson))
                captureMap.RemoveAllBindingOverrides();
            else
                captureMap.LoadBindingOverridesFromJson(overridesJson);
        }

        /// <summary>Serializes the capture map's current overrides for the draft.</summary>
        public string ToJson() => captureMap.SaveBindingOverridesAsJson();

        /// <summary>Clears every override, restoring all default keys.</summary>
        public void ResetToDefaults()
        {
            captureMap.Disable();
            captureMap.RemoveAllBindingOverrides();
        }

        /// <summary>
        /// If the just-rebound entry now shares its effective path with another
        /// rebindable action (a duplicate), or resolved to nothing, reverts that
        /// entry's binding to its default and returns true. Otherwise false.
        /// </summary>
        public bool RevertIfBindingConflicts(FirstPersonController.RebindableAction entry)
        {
            InputAction action = captureMap.FindAction(entry.ActionName);
            if (action == null || entry.BindingIndex >= action.bindings.Count)
                return false;

            string path = action.bindings[entry.BindingIndex].effectivePath;

            bool conflict = string.IsNullOrEmpty(path) ||
                ReservedNarrativePaths.Contains(path);
            if (!conflict)
            {
                foreach (FirstPersonController.RebindableAction other in Entries)
                {
                    if (other.Id == entry.Id)
                        continue;

                    InputAction otherAction = captureMap.FindAction(other.ActionName);
                    if (otherAction == null || other.BindingIndex >= otherAction.bindings.Count)
                        continue;

                    if (otherAction.bindings[other.BindingIndex].effectivePath == path)
                    {
                        conflict = true;
                        break;
                    }
                }
            }

            if (conflict)
            {
                bool wasEnabled = captureMap.enabled;
                if (wasEnabled)
                    captureMap.Disable();
                action.RemoveBindingOverride(entry.BindingIndex);
                if (wasEnabled)
                    captureMap.Enable();
            }

            return conflict;
        }


        /// <summary>
        /// Human-readable key currently bound for the given entry, e.g. "W" or
        /// "LEFT BUTTON". Reflects any override loaded via <see cref="LoadFromJson"/>.
        /// </summary>
        public string DisplayKeyFor(FirstPersonController.RebindableAction entry)
        {
            InputAction action = captureMap.FindAction(entry.ActionName);
            if (action == null || entry.BindingIndex >= action.bindings.Count)
                return "\u2014"; // em dash

            string display = action.GetBindingDisplayString(
                entry.BindingIndex,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);

            return string.IsNullOrWhiteSpace(display)
                ? "\u2014"
                : display.ToUpperInvariant();
        }

        /// <summary>
        /// Starts an interactive rebind for one entry. The next key/button the
        /// player presses becomes the new binding (subject to <paramref name="allowMouse"/>).
        /// On completion the capture map holds the new override; call
        /// <see cref="ToJson"/> to persist it into the draft.
        /// </summary>
        /// <param name="entry">Which row is being rebound.</param>
        /// <param name="allowMouse">When false, mouse controls are excluded so only keyboard keys are captured.</param>
        /// <param name="onComplete">Invoked (success or cancel) after the operation ends, so the UI can refresh.</param>
        /// <returns>The running operation, so the caller can dispose it if the screen closes early.</returns>
        public InputActionRebindingExtensions.RebindingOperation StartRebind(
            FirstPersonController.RebindableAction entry,
            bool allowMouse,
            Action onComplete)
        {
            InputAction action = captureMap.FindAction(entry.ActionName);
            if (action == null)
            {
                onComplete?.Invoke();
                return null;
            }

            // The action must be disabled while rebinding.
            captureMap.Disable();

            var operation = action.PerformInteractiveRebinding(entry.BindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .OnComplete(op =>
                {
                    op.Dispose();
                    onComplete?.Invoke();
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    onComplete?.Invoke();
                });

            // No keyboard cancel key: Escape (and every other key) must be freely
            // bindable, so cancelling a rebind is done via the screen's CANCEL
            // button instead of a reserved key.

            if (!allowMouse)
            {
                // Keyboard only: exclude the mouse device entirely rather than
                // constraining the expected control type, so any keyboard key
                // (including Escape) is accepted.
                operation = operation.WithControlsExcluding("<Mouse>");
            }
            // When mouse is allowed we intentionally do NOT set an expected control
            // type. Keyboard keys report as "Key" and mouse buttons as "Button";
            // pinning one type would reject the other. Leaving it open accepts both.

            operation.Start();
            return operation;
        }

        public void Dispose()
        {
            captureMap?.Disable();
            captureMap?.Dispose();
        }
    }
}
