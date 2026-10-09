using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Single source of truth for the game's pixel resolutions.
    /// Change these constants instead of editing presenters, scenes, or render textures.
    /// </summary>
    public static class RetroResolution
    {
        public const int WorldWidth = 240;
        public const int WorldHeight = 240;
        public const int DiegeticUiScale = 2;

        public static readonly Vector2Int WorldSize = new(WorldWidth, WorldHeight);
        public static readonly Vector2Int DiegeticUiSize =
            new(WorldWidth * DiegeticUiScale, WorldHeight * DiegeticUiScale);

        public static string Format(Vector2Int resolution)
        {
            return $"{resolution.x}x{resolution.y}";
        }
    }
}
