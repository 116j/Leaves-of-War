using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Maps ground material names (matched by substring, case-insensitive) to
    /// a set of footstep clips. Works directly with the project's existing
    /// materials (Stone, Wood, Moss, Plaster, etc.) - no need to tag every
    /// ground object individually.
    /// </summary>
    [CreateAssetMenu(menuName = "Hortensia/Audio/Footstep Surface Library", fileName = "FootstepSurfaceLibrary")]
    public sealed class FootstepSurfaceLibrary : ScriptableObject
    {
        [System.Serializable]
        public sealed class SurfaceEntry
        {
            [Tooltip("Matches if the ground material's name CONTAINS this text (case-insensitive). E.g. 'Stone' matches a material named 'Stone' or 'Stone_Wet'.")]
            public string materialNameContains;
            public AudioClip[] clips;
        }

        [SerializeField] private SurfaceEntry[] surfaces = System.Array.Empty<SurfaceEntry>();
        [Tooltip("Used when no surface entry matches the ground material's name.")]
        [SerializeField] private AudioClip[] defaultClips = System.Array.Empty<AudioClip>();

        /// <summary>Picks a random clip for the given ground material name, falling back to Default Clips if nothing matches.</summary>
        public AudioClip GetRandomClip(string materialName)
        {
            if (!string.IsNullOrEmpty(materialName))
            {
                foreach (SurfaceEntry entry in surfaces)
                {
                    if (string.IsNullOrEmpty(entry.materialNameContains))
                        continue;
                    if (materialName.IndexOf(entry.materialNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return RandomFrom(entry.clips);
                }
            }

            return RandomFrom(defaultClips);
        }

        private static AudioClip RandomFrom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}