using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// A single, pausable time source for narrative presentation.
    ///
    /// The dialogue coroutines in <see cref="NarrativePresenter"/> deliberately
    /// run on unscaled time so they keep working while <c>Time.timeScale</c> is 0
    /// (for hit-stops, slow-mo, and so on). That same property means a plain
    /// <c>Time.timeScale = 0</c> pause would NOT stop the text crawl or the beat
    /// timers. Rather than sprinkle pause checks through every coroutine, they all
    /// read <see cref="Time"/> here instead of <c>Time.unscaledTime</c>.
    ///
    /// While not paused, <see cref="Time"/> tracks unscaled time exactly (it is
    /// simply offset so it is continuous). When <see cref="SetPaused"/> is called
    /// with true, the clock freezes: every <c>while (clock.Time &lt; ...)</c> loop
    /// stops advancing until the game resumes. No per-coroutine edits required.
    /// </summary>
    public static class NarrativeClock
    {
        private static bool paused;
        private static float pausedAt;
        // Accumulated offset so Time stays continuous across pause/resume: it is
        // the total unscaled seconds spent paused, subtracted from the raw clock.
        private static float pausedOffset;

        /// <summary>True while the clock (and thus narrative timing) is frozen.</summary>
        public static bool IsPaused => paused;

        /// <summary>
        /// The narrative time, in seconds. Equivalent to unscaled time minus any
        /// time spent paused, so it advances normally when running and holds still
        /// when paused.
        /// </summary>
        public static float Time =>
            paused ? pausedAt : UnityEngine.Time.unscaledTime - pausedOffset;

        /// <summary>
        /// Freezes or resumes the clock. Idempotent: calling it with the value it
        /// already holds does nothing, so it is safe to drive from a pause toggle.
        /// </summary>
        public static void SetPaused(bool value)
        {
            if (value == paused)
                return;

            if (value)
            {
                // Remember the frozen reading so Time returns a stable value.
                pausedAt = UnityEngine.Time.unscaledTime - pausedOffset;
                paused = true;
            }
            else
            {
                // Fold the paused span into the offset so Time resumes seamlessly.
                pausedOffset = UnityEngine.Time.unscaledTime - pausedAt;
                paused = false;
            }
        }
    }
}