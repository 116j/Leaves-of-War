using UnityEngine;

namespace Hortensia.Runtime
{
    [RequireComponent(typeof(Observable))]
    public sealed class LookAwayCue : MonoBehaviour
    {
        [Tooltip("Played once, the first time the player looks away from this object. Never again.")]
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [Tooltip("0 = everywhere at the same volume, 1 = from this object.")]
        [SerializeField, Range(0f, 1f)] private float spatial = 0.6f;

        private bool played;

        private void Start()
        {
            GetComponent<Observable>().OnChangeAllowed.AddListener(Play);
        }

        private void Play()
        {
            if (played || clip == null)
                return;
            played = true;
            var go = new GameObject("Look Away Cue");
            go.transform.position = transform.position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = spatial;
            source.minDistance = 4f;
            source.maxDistance = 25f;
            AudioManager.Route(source, AudioBus.SoundEffects);
            source.Play();
            Destroy(go, clip.length + 0.2f);
        }
    }
}
