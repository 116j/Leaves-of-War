using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class EndingEpilogueReveal : MonoBehaviour
    {
        [SerializeField] private string endingId;
        [SerializeField] private GameObject epilogueRoot;

        private GameSession subscribedSession;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (subscribedSession != null)
                subscribedSession.CompletedEndingChanged -= HandleEndingChanged;
            subscribedSession = null;
        }

        private void Subscribe()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session == subscribedSession)
                return;

            if (subscribedSession != null)
                subscribedSession.CompletedEndingChanged -= HandleEndingChanged;

            subscribedSession = session;
            subscribedSession.CompletedEndingChanged += HandleEndingChanged;
        }

        private void Refresh()
        {
            HandleEndingChanged(subscribedSession != null
                ? subscribedSession.CompletedEndingId
                : null);
        }

        private void HandleEndingChanged(string completedEndingId)
        {
            if (epilogueRoot == null)
                return;

            epilogueRoot.SetActive(
                !string.IsNullOrWhiteSpace(endingId) &&
                string.Equals(endingId, completedEndingId, StringComparison.Ordinal));
        }
    }
}
