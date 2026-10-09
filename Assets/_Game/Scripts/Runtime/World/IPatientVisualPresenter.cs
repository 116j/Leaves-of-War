using Hortensia.Narrative;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Optional scene service that synchronizes a patient-session line with
    /// the character model occupying the consulting chair.
    /// </summary>
    public interface IPatientVisualPresenter
    {
        // baselineVisionIntensity is what the vision bleed should settle at
        // for this patient's own dialogue (from PatientSessionBeat's Vision
        // Curve) - the swap transition fades up to a multiple of this and
        // back down to it, rather than to/from zero, so the effect never
        // drops out between patients.
        void ShowPatient(PatientDefinition patient, float baselineVisionIntensity);
        void ClearPatient();

        // True while a patient swap transition (e.g. a vision-bleed
        // distortion covering the model change) is still in progress -
        // callers that need the new patient's model/animator already in
        // place should wait for this to become false before proceeding.
        bool IsTransitioning { get; }
    }
}