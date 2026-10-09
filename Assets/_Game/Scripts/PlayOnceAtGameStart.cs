using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Plays an AudioSource exactly once per game session - the FIRST time a
    /// GameObject carrying this component with a given Track Id is ever
    /// loaded, and never again, even if the scene it lives in (e.g. Manor)
    /// is reloaded later for a different chapter. Use this instead of the
    /// AudioSource's own "Play On Awake" checkbox whenever a scene-level
    /// ambience should only be heard at the true start of the game
    /// (chapter 1's first load), not every time the player returns to that
    /// scene in a later chapter.
    ///
    /// Multiple overlapping tracks are supported: give each
    /// PlayOnceAtGameStart instance in the project its own unique Track Id
    /// (e.g. "Manor Ambience", "Study Clock"). Each id is tracked
    /// independently, so several different tracks can each play once at
    /// game start without blocking one another - a single shared flag would
    /// only let the FIRST one to load play at all.
    ///
    /// The played-ids set resets via RuntimeInitializeOnLoadMethod, the same
    /// pattern GameSession and AudioManager use - so it correctly resets
    /// between actual Play sessions (Editor Play, or a fresh app launch),
    /// but stays "already played" per id across every subsequent scene load
    /// within one continuous session.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayOnceAtGameStart : MonoBehaviour
    {
        [Tooltip("Unique identifier for THIS track across the whole game. Give every PlayOnceAtGameStart instance a different id - e.g. \"Manor Ambience\", \"Study Clock\" - so multiple overlapping tracks are tracked independently instead of sharing one flag.")]
        [SerializeField] private string trackId;
        [SerializeField] private AudioSource audioSource;

        private static readonly HashSet<string> playedTrackIds = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            playedTrackIds.Clear();
        }

        private void Awake()
        {
            // Arriving here mid-transition (e.g. the Manor scene has just
            // finished loading as part of a dream transition still in
            // progress, with its own music still playing out) means
            // PlayOnDreamTransitionEnd's matching entry will start this
            // track once that transition's music has GENUINELY finished.
            // Starting it here too, immediately on scene load, would
            // overlap it with the still-playing transition music - defer
            // entirely to that system in this case, rather than double
            // triggering. The track's id is not marked as played, so a
            // later, genuinely transition-free load of this scene can
            // still use this component normally.
            if (GameSession.Instance != null && GameSession.Instance.IsDreamTransitionActive)
                return;

            if (string.IsNullOrEmpty(trackId))
            {
                Debug.LogWarning(
                    $"'{name}': PlayOnceAtGameStart has no Track Id set - skipped, to avoid ambiguously matching some other track.",
                    this);
                return;
            }

            if (playedTrackIds.Contains(trackId))
                return;

            if (audioSource == null)
            {
                Debug.LogWarning($"'{name}': no Audio Source assigned - nothing to play.", this);
                return;
            }

            playedTrackIds.Add(trackId);
            audioSource.Play();
        }
    }
}