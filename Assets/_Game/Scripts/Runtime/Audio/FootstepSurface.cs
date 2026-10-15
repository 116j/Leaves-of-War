using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FootstepSurface : MonoBehaviour
    {
        [Tooltip("Footstep sound on this collider (and its children). Overrides the guess from the material name.")]
        [SerializeField] private FootstepSurfaceType surface = FootstepSurfaceType.Wood;

        public FootstepSurfaceType Surface => surface;
    }
}
