using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Adds slow, continuous scrolling via the Up/Down arrow keys to a
    /// ScrollRect, alongside its existing mouse wheel scrolling (unaffected).
    /// Uses unscaled time, so it keeps working while the game is paused
    /// (Options is shown both from the main menu and the pause menu).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ArrowKeyScroll : MonoBehaviour
    {
        [Tooltip("How fast the arrow keys scroll, in normalized (0-1) units per second. Kept low for a gentle, controllable scroll.")]
        [SerializeField, Min(0.01f)] private float scrollSpeed = 0.6f;

        private ScrollRect scrollRect;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || scrollRect == null)
                return;

            float direction = 0f;
            if (keyboard.upArrowKey.isPressed)
                direction += 1f;
            if (keyboard.downArrowKey.isPressed)
                direction -= 1f;

            if (direction == 0f)
                return;

            // verticalNormalizedPosition: 1 = scrolled to top, 0 = scrolled to
            // bottom - so "up" increases it, "down" decreases it.
            float delta = direction * scrollSpeed * Time.unscaledDeltaTime;
            scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(scrollRect.verticalNormalizedPosition + delta);
        }
    }
}