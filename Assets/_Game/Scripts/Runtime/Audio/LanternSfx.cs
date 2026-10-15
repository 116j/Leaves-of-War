using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class LanternSfx : MonoBehaviour
    {
        [SerializeField] private AudioClip flickerLoop;
        [SerializeField, Range(0f, 1f)] private float flickerVolume = 0.35f;
        [SerializeField] private AudioClip[] hangClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float hangVolume = 0.8f;
        [SerializeField, Min(1f)] private float maxDistance = 8f;
        [Tooltip("Flicker only while a Light on this object (or its children) is on.")]
        [SerializeField] private bool followLight = true;

        private AudioSource loop;
        private AudioSource oneShot;
        private Light lightSource;

        private void Awake()
        {
            lightSource = GetComponentInChildren<Light>(true);
            loop = NewSource(true);
            oneShot = NewSource(false);
            loop.clip = flickerLoop;
            loop.volume = flickerVolume;
        }

        private AudioSource NewSource(bool looping)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = looping;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.maxDistance = maxDistance;
            AudioManager.Route(s, AudioBus.SoundEffects);
            return s;
        }

        private void Update()
        {
            if (loop.clip == null)
                return;
            bool on = !followLight || lightSource == null || lightSource.isActiveAndEnabled && lightSource.intensity > 0.01f;
            if (on && !loop.isPlaying)
            {
                loop.time = Random.Range(0f, loop.clip.length * 0.9f);
                loop.Play();
            }
            else if (!on && loop.isPlaying)
                loop.Stop();
        }

        public void Hang()
        {
            if (hangClips == null || hangClips.Length == 0)
                return;
            AudioClip clip = hangClips[Random.Range(0, hangClips.Length)];
            if (clip != null)
                oneShot.PlayOneShot(clip, hangVolume);
        }
    }
}
