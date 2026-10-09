using System;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Sets the scene's skybox AND sun (Directional Light) together, based
    /// on story-progress flags rather than time of day. Attach once
    /// anywhere in the scene. List entries from earliest to latest story
    /// progress - entries are checked in order and the LAST one whose flag
    /// is set wins, so a later, more-advanced flag always overrides an
    /// earlier one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryProgressSkybox : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            [Tooltip("This skybox and sun lighting apply once this flag is set.")]
            public FlagId flag;
            public Material skybox;
            [Header("Sun Light")]
            public Color sunColor;
            [Min(0f)] public float sunIntensity;
            [Tooltip("Sun's world rotation (X = altitude angle, Y = azimuth).")]
            public Vector3 sunRotation;
        }

        [Tooltip("The Directional Light this component drives alongside the skybox.")]
        [SerializeField] private Light sun;

        [Header("Default (before any flag below is set)")]
        [SerializeField] private Material defaultSkybox;
        [SerializeField] private Color defaultSunColor = Color.white;
        [SerializeField, Min(0f)] private float defaultSunIntensity = 1f;
        [SerializeField] private Vector3 defaultSunRotation;

        [Tooltip("Checked in order; the LAST entry whose flag is set wins - list flags from earliest to latest story progress.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private NarrativeState subscribedState;

        private void OnEnable()
        {
            TrySubscribeToState();
        }

        private void OnDisable()
        {
            if (subscribedState != null)
                subscribedState.FlagChanged -= HandleFlagChanged;
            subscribedState = null;
        }

        private void Update()
        {
            // Checked every frame (cheap reference comparison), not just
            // when null: this object is persistent (DontDestroyOnLoad,
            // alongside PauseController/SettingsApplier), so a new game or
            // a loaded save can swap GameSession.State to a brand new
            // instance at any time - staying subscribed to the old, now-
            // abandoned one would silently stop reacting to any further
            // flag changes.
            TrySubscribeToState();
        }

        private void TrySubscribeToState()
        {
            GameSession session = GameSession.Instance;
            NarrativeState state = session != null ? session.State : null;
            if (ReferenceEquals(state, subscribedState))
                return;

            if (subscribedState != null)
                subscribedState.FlagChanged -= HandleFlagChanged;

            subscribedState = state;
            if (subscribedState == null)
                return;

            subscribedState.FlagChanged += HandleFlagChanged;

            // Apply immediately in case relevant flags were already set
            // before this component found the session (e.g. loaded from a
            // save, or re-entering this scene later).
            Apply();
        }

        private void HandleFlagChanged(FlagId flag, bool isSet)
        {
            Apply();
        }

        private void Apply()
        {
            if (subscribedState == null)
                return;

            Material chosenSkybox = defaultSkybox;
            Color chosenSunColor = defaultSunColor;
            float chosenSunIntensity = defaultSunIntensity;
            Vector3 chosenSunRotation = defaultSunRotation;

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry.flag == null || !subscribedState.HasFlag(entry.flag))
                    continue;

                if (entry.skybox != null)
                    chosenSkybox = entry.skybox;
                chosenSunColor = entry.sunColor;
                chosenSunIntensity = entry.sunIntensity;
                chosenSunRotation = entry.sunRotation;
            }

            Debug.Log($"[SkyboxDebug] Apply() su '{gameObject.name}' - skybox scelto: '{(chosenSkybox != null ? chosenSkybox.name : "NULL")}', RenderSettings.skybox PRIMA: '{(RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL")}'");

            if (chosenSkybox != null)
                RenderSettings.skybox = chosenSkybox;

            if (sun != null)
            {
                sun.color = chosenSunColor;
                sun.intensity = chosenSunIntensity;
                sun.transform.rotation = Quaternion.Euler(chosenSunRotation);
            }
        }
    }
}