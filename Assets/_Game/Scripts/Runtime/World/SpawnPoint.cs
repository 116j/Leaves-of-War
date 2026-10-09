using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id = "default";
        private GameSessionRegistration sessionRegistration;

        public string Id => id;

        public bool Matches(string spawnPointId) =>
            !string.IsNullOrEmpty(spawnPointId) &&
            string.Equals(id, spawnPointId, StringComparison.Ordinal);

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterSpawnPoint(this),
                session => session.UnregisterSpawnPoint(this));
        }

        private void OnEnable() => sessionRegistration?.Enable();

        private void OnDisable() => sessionRegistration?.Disable();

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
        }
    }
}
