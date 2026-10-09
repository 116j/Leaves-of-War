using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public sealed class TravelBeat : NarrativeBeat
    {
        [SerializeField] private string targetScene;
        [SerializeField] private string spawnPointId;

        public string TargetScene => targetScene;
        public string SpawnPointId => spawnPointId;
    }
}
