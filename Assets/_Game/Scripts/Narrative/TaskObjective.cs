using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Task Objective", fileName = "TaskObjective")]
    public sealed class TaskObjective : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string playerFacingLabel;

        public string Id => id;
        public string PlayerFacingLabel =>
            PlayerFacingLabelUtility.Resolve(playerFacingLabel, id, name);
    }
}
