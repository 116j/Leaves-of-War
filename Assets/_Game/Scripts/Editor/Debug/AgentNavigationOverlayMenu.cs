using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;

namespace Hortensia.EditorTools
{
    internal static class AgentNavigationOverlayMenu
    {
        [MenuItem("Tools/Hortensia/Agent Navigation/Show Overlay")]
        private static void ShowOverlay()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Agent Navigation is available while the Editor is in Play Mode.");
                return;
            }

            AgentNavigationOverlay.SetVisible(true);
        }

        [MenuItem("Tools/Hortensia/Agent Navigation/Hide Overlay")]
        private static void HideOverlay()
        {
            if (EditorApplication.isPlaying)
                AgentNavigationOverlay.SetVisible(false);
        }
    }
}
