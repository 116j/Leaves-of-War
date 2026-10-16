using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class MoveSfx : MonoBehaviour
    {
        [Tooltip("Played where the object lands when it is moved (e.g. the chair that moves while unseen).")]
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Min(1f)] private float maxDistance = 10f;

        private Vector3 last;

        private void Start() => last = transform.position;

        private void LateUpdate()
        {
            if ((transform.position - last).sqrMagnitude > 0.0025f && clip != null)
            {
                var go = new GameObject(clip.name);
                go.transform.position = transform.position;
                AudioSource source = go.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.maxDistance = maxDistance;
                source.volume = volume;
                source.clip = clip;
                AudioManager.Route(source, AudioBus.SoundEffects);
                source.Play();
                Destroy(go, clip.length + 0.2f);
            }
            last = transform.position;
        }
    }
}
