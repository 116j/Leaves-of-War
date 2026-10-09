using System.Text;
using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Carry Category", fileName = "CarryCategory")]
    public sealed class CarryCategory : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string playerFacingLabel;
        [SerializeField] private TaskObjective objective;
        [SerializeField] private bool retainedAfterUse;

        public string Id => id;
        public string PlayerFacingLabel =>
            PlayerFacingLabelUtility.Resolve(playerFacingLabel, id, name);
        public TaskObjective Objective => objective;
        public bool RetainedAfterUse => retainedAfterUse;
    }

    internal static class PlayerFacingLabelUtility
    {
        public static string Resolve(string authoredLabel, string stableId, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(authoredLabel))
                return authoredLabel.Trim();

            string source = !string.IsNullOrWhiteSpace(stableId)
                ? stableId
                : fallbackName;
            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            var result = new StringBuilder(source.Length + 4);
            bool previousWasSeparator = true;
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                if (current == '_' || current == '-' || char.IsWhiteSpace(current))
                {
                    if (!previousWasSeparator && result.Length > 0)
                        result.Append(' ');
                    previousWasSeparator = true;
                    continue;
                }

                if (char.IsUpper(current) && !previousWasSeparator && i > 0 &&
                    char.IsLower(source[i - 1]))
                {
                    result.Append(' ');
                }

                result.Append(current);
                previousWasSeparator = false;
            }

            return result.ToString().Trim();
        }
    }
}
