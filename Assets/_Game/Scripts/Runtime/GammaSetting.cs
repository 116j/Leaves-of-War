using System;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Broadcasts the current gamma value to whichever component actually
    /// renders it (the world output's material). Deliberately independent of
    /// URP's Volume system, which proved unreliable for this in this project.
    /// </summary>
    internal static class GammaSetting
    {
        public static float Current { get; private set; } = 1f;

        public static event Action<float> Applied;

        public static void Set(float gamma)
        {
            Current = gamma;
            Applied?.Invoke(gamma);
        }
    }

    /// <summary>
    /// Broadcasts whether the retro filter (pixelation, PS1 dithering,
    /// chromatic aberration, grain) is enabled. Same rationale as GammaSetting:
    /// applied directly on the world output's material, bypassing URP.
    /// </summary>
    internal static class RetroFilterSetting
    {
        public static bool Current { get; private set; }

        public static event Action<bool> Applied;

        public static void Set(bool enabled)
        {
            Current = enabled;
            Applied?.Invoke(enabled);
        }
    }
}