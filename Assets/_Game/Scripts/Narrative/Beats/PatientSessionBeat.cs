using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public sealed class PatientSessionBeat : NarrativeBeat
    {
        [SerializeField] private List<PatientDefinition> roster = new List<PatientDefinition>();
        [SerializeField] private AnimationCurve visionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public IReadOnlyList<PatientDefinition> Roster => roster;

        // Escalation is the patient's position in the roster, so reordering
        // the roster reshapes the session without touching any numbers.
        public float VisionIntensityAt(int index)
        {
            if (visionCurve == null || roster == null || roster.Count == 0)
                return 0f;

            float position = roster.Count == 1
                ? 1f
                : Mathf.Clamp01((float)index / (roster.Count - 1));

            return visionCurve.Evaluate(position);
        }
    }
}
