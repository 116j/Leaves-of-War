using System;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TaskProgressListener : MonoBehaviour
    {
        [SerializeField] private TaskObjective objective;
        [SerializeField, Min(0)] private int threshold;
        [SerializeField] private GameObject revealed;

        private TaskProgress subscribedProgress;

        public TaskObjective Objective => objective;
        public int Threshold => threshold;
        public GameObject Revealed => revealed;

        private void OnEnable()
        {
            Rebind();
            Refresh(GameSession.Instance != null ? GameSession.Instance.State : null);
        }

        private void Start()
        {
            Rebind();
            Refresh(GameSession.Instance != null ? GameSession.Instance.State : null);
        }

        private void Update()
        {
            TaskProgress current = GameSession.Instance != null && GameSession.Instance.State != null
                ? GameSession.Instance.State.Tasks
                : null;
            if (!ReferenceEquals(current, subscribedProgress))
                Rebind();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Refresh(NarrativeState state)
        {
            if (revealed == null)
                return;

            bool shouldReveal = state != null && objective != null &&
                state.Tasks.CountFor(objective) >= Mathf.Max(0, threshold);
            revealed.SetActive(shouldReveal);
        }

        public static void RefreshSceneObjects(NarrativeState state)
        {
            TaskProgressListener[] listeners =
                FindObjectsByType<TaskProgressListener>(FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null)
                    listeners[i].Refresh(state);
            }
        }

        private void Rebind()
        {
            Unsubscribe();
            GameSession session = GameSession.Instance;
            if (session == null || session.State == null)
                return;

            subscribedProgress = session.State.Tasks;
            subscribedProgress.ProgressChanged += HandleProgressChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedProgress != null)
                subscribedProgress.ProgressChanged -= HandleProgressChanged;
            subscribedProgress = null;
        }

        private void HandleProgressChanged(TaskObjective changedObjective, int count)
        {
            if (objective == null || changedObjective == null ||
                !string.Equals(objective.Id, changedObjective.Id, StringComparison.Ordinal))
            {
                return;
            }

            Refresh(GameSession.Instance != null ? GameSession.Instance.State : null);
        }
    }
}
