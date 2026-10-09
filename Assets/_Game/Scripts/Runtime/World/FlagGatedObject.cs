using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FlagGatedObject : MonoBehaviour
    {
        [SerializeField] private FlagId flag;
        [SerializeField] private bool activeWhenSet = true;

        public FlagId Flag => flag;
        public bool ActiveWhenSet => activeWhenSet;

        private void Start()
        {
            Refresh(GameSession.Instance != null ? GameSession.Instance.State : null);
        }

        public void Refresh(NarrativeState state)
        {
            if (state == null || flag == null)
                return;

            bool shouldBeActive = state.HasFlag(flag) == activeWhenSet;
            if (gameObject.activeSelf != shouldBeActive)
                gameObject.SetActive(shouldBeActive);
        }

        public static void RefreshSceneObjects(NarrativeState state)
        {
            FlagGatedObject[] gatedObjects =
                FindObjectsByType<FlagGatedObject>(FindObjectsInactive.Include);
            for (int i = 0; i < gatedObjects.Length; i++)
            {
                if (gatedObjects[i] != null)
                    gatedObjects[i].Refresh(state);
            }
        }
    }
}
