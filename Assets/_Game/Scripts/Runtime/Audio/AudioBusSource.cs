using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioBusSource : MonoBehaviour
    {
        [SerializeField] private AudioBus bus = AudioBus.SoundEffects;

        public AudioBus Bus => bus;

        private void Awake()
        {
            Route();
        }

        public bool Route()
        {
            AudioSource source = GetComponent<AudioSource>();
            return source != null && AudioManager.Route(source, bus);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                Route();
        }
#endif
    }
}
