using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Starts this AudioSource exactly once when the scene begins. Use this
    /// instead of "Play On Awake" on background music: Play On Awake replays
    /// from the start whenever the GameObject is re-enabled (e.g. after the
    /// pause menu closes), which is why music was restarting instead of
    /// resuming. This script only ever calls Play() once, in Start().
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayMusicOnce : MonoBehaviour
    {
        private AudioSource source;

        private void Start()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; // safety net in case it's still ticked
            if (!source.isPlaying)
                source.Play();
        }

        /// <summary>Pauses in place (keeps position). Call <see cref="Resume"/> to continue.</summary>
        public void Pause()
        {
            if (source != null && source.isPlaying)
                source.Pause();
        }

        /// <summary>Resumes playback from where <see cref="Pause"/> left it.</summary>
        public void Resume()
        {
            source?.UnPause();
        }
    }
}