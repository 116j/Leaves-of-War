using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Sets a custom cursor once for the whole game, applied wherever
    /// Cursor.visible is later set to true (inventory, pause menu, object
    /// inspection, etc.) - no per-system wiring needed, it's a single
    /// global call.
    ///
    /// Runs automatically at game start (same "just works" pattern as
    /// AudioManager's bootstrap); nothing needs to be placed in any scene.
    /// To use it, add your cursor image at
    /// Assets/Resources/UI/CustomCursor.png (or .psd/.jpg, any Texture2D
    /// Unity can import) - Texture Type "Cursor" if your Unity version
    /// offers it, otherwise "Sprite (2D and UI)" with Read/Write enabled.
    /// </summary>
    internal static class CursorAppearance
    {
        // Resources-relative path (no extension) to the cursor texture.
        private const string ResourcePath = "UI/CustomCursor";

        // Which pixel of the texture is the exact click point - (0,0) is
        // the top-left corner. Edit this if your cursor image isn't a
        // simple top-left-anchored pointer (e.g. a centered crosshair would
        // want half the texture's width/height here instead).
        private static readonly Vector2 Hotspot = Vector2.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Texture2D cursorTexture = Resources.Load<Texture2D>(ResourcePath);
            if (cursorTexture == null)
            {
                Debug.LogWarning(
                    $"CursorAppearance: no texture found at Resources/{ResourcePath} - " +
                    "keeping the system cursor. Add your cursor image there, or change " +
                    "ResourcePath in CursorAppearance.cs.");
                return;
            }

            Cursor.SetCursor(cursorTexture, Hotspot, CursorMode.Auto);
        }
    }
}