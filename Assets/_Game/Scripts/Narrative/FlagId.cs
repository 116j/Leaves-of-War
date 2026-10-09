using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Flag", fileName = "Flag")]
    public sealed class FlagId : ScriptableObject
    {
        [SerializeField] private string id;

        public string Id => id;
    }
}
