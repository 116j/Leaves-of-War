using System;
using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TherapyPatientPresenter : MonoBehaviour, IPatientVisualPresenter
    {
        [Serializable]
        public sealed class PatientModelBinding
        {
            [SerializeField] private PatientDefinition patient;
            [SerializeField] private GameObject model;
            [Tooltip("Local position offset applied on top of the anchor, in case this model's own pivot isn't centred the same way as the others.")]
            [SerializeField] private Vector3 positionOffset;
            [Tooltip("Local rotation offset (Euler angles) applied on top of the anchor.")]
            [SerializeField] private Vector3 rotationOffsetEuler;

            public PatientDefinition Patient => patient;
            public GameObject Model => model;
            public Vector3 PositionOffset => positionOffset;
            public Quaternion RotationOffset => Quaternion.Euler(rotationOffsetEuler);
        }

        [SerializeField] private Transform patientAnchor;
        [SerializeField]
        private List<PatientModelBinding> bindings =
            new List<PatientModelBinding>();

        [Header("Patient Swap Transition")]
        [Tooltip("Peak intensity during a swap, as a MULTIPLE of the incoming patient's own baseline (e.g. 3 = triple). The peak and the settle point both scale with whatever the Vision Curve says for that patient - it never drops to zero.")]
        [SerializeField, Min(1f)] private float swapPeakMultiplier = 3f;
        [Tooltip("Seconds to fade the distortion in before swapping the model.")]
        [SerializeField, Min(0.01f)] private float swapFadeInSeconds = 0.35f;
        [Tooltip("Seconds to fade the distortion back out after swapping the model.")]
        [SerializeField, Min(0.01f)] private float swapFadeOutSeconds = 0.45f;

        private Coroutine swapRoutine;

        private GameSessionRegistration sessionRegistration;
        private PatientDefinition currentPatient;
        private GameObject currentInstance;
        private CharacterTalkAnimator currentTalkAnimator;

        public Transform PatientAnchor => patientAnchor != null ? patientAnchor : transform;
        public IReadOnlyList<PatientModelBinding> Bindings => bindings;
        public PatientDefinition CurrentPatient => currentPatient;
        public GameObject CurrentInstance => currentInstance;
        public bool IsTransitioning => swapRoutine != null;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterPatientVisualPresenter(this),
                session => session.UnregisterPatientVisualPresenter(this));
        }

        private void OnEnable()
        {
            sessionRegistration?.Enable();
            ChapterRunner.PatientLineStarted += HandlePatientLineStarted;
            ChapterRunner.PatientLineFinished += HandlePatientLineFinished;
        }

        private void OnDisable()
        {
            sessionRegistration?.Disable();
            ChapterRunner.PatientLineStarted -= HandlePatientLineStarted;
            ChapterRunner.PatientLineFinished -= HandlePatientLineFinished;
            ClearPatient();
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
            ClearPatient();
        }

        // The presenter (not CharacterTalkAnimator itself) owns this link,
        // since it's the one that knows which instance is currently on
        // screen - CharacterTalkAnimator on a patient model doesn't need a
        // Speaker assigned at all, it's driven directly here instead.
        private void HandlePatientLineStarted(PatientDefinition patient)
        {
            if (ReferenceEquals(patient, currentPatient))
                currentTalkAnimator?.BeginTurn();
        }

        private void HandlePatientLineFinished(PatientDefinition patient)
        {
            if (ReferenceEquals(patient, currentPatient))
                currentTalkAnimator?.EndTurn();
        }

        public void ShowPatient(PatientDefinition patient, float baselineVisionIntensity)
        {
            if (patient == null)
            {
                ClearPatient();
                return;
            }

            if (ReferenceEquals(currentPatient, patient) && currentInstance != null)
                return;

            PatientModelBinding binding = FindBinding(patient);
            if (binding == null || binding.Model == null)
            {
                Debug.LogWarning(
                    $"No therapy patient model is assigned for '{patient.DisplayName}'.",
                    this);
                return;
            }

            if (swapRoutine != null)
                StopCoroutine(swapRoutine);
            swapRoutine = StartCoroutine(SwapPatientRoutine(patient, binding, baselineVisionIntensity));
        }

        /// <summary>
        /// Fades the shared vision-bleed distortion from wherever it
        /// currently sits UP to a multiple of the incoming patient's own
        /// baseline (spiking through the model swap, hidden behind the murk
        /// instead of a hard visual cut), then back DOWN to that same
        /// baseline - never to zero, so the effect stays present across the
        /// whole session instead of dropping out between patients.
        /// </summary>
        private IEnumerator SwapPatientRoutine(
            PatientDefinition patient,
            PatientModelBinding binding,
            float baselineVisionIntensity)
        {
            VisionBleedController visionBleed = TryGetVisionBleed();
            GifOverlayController gifOverlay = TryGetGifOverlay();
            gifOverlay?.SpawnScatteredDuplicates();

            float peakIntensity = baselineVisionIntensity * swapPeakMultiplier;

            if (visionBleed != null)
            {
                float startIntensity = visionBleed.Intensity;
                float elapsed = 0f;
                while (elapsed < swapFadeInSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    visionBleed.SetIntensity(Mathf.Lerp(startIntensity, peakIntensity, elapsed / swapFadeInSeconds));
                    yield return null;
                }
                visionBleed.SetIntensity(peakIntensity);
            }

            SwapModelImmediate(patient, binding);

            if (visionBleed != null)
            {
                float elapsed = 0f;
                while (elapsed < swapFadeOutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    visionBleed.SetIntensity(Mathf.Lerp(peakIntensity, baselineVisionIntensity, elapsed / swapFadeOutSeconds));
                    yield return null;
                }
                visionBleed.SetIntensity(baselineVisionIntensity);
            }

            gifOverlay?.ClearScatteredDuplicates();
            swapRoutine = null;
        }

        private void SwapModelImmediate(PatientDefinition patient, PatientModelBinding binding)
        {
            ClearPatientVisualOnly();

            currentPatient = patient;
            currentInstance = Instantiate(binding.Model, PatientAnchor, false);
            currentInstance.name = $"Patient — {patient.name}";
            Transform instanceTransform = currentInstance.transform;
            instanceTransform.localPosition = binding.PositionOffset;
            instanceTransform.localRotation = binding.RotationOffset;
            instanceTransform.localScale = Vector3.one;
            currentTalkAnimator = currentInstance.GetComponentInChildren<CharacterTalkAnimator>();
        }

        private VisionBleedController TryGetVisionBleed()
        {
            GameSession session = GameSession.Instance;
            if (session != null &&
                session.SceneServices.TryGetVisionBleed(out VisionBleedController controller, out _))
            {
                return controller;
            }

            return null;
        }

        private GifOverlayController TryGetGifOverlay()
        {
            GameSession session = GameSession.Instance;
            if (session != null &&
                session.SceneServices.TryGetGifOverlay(out GifOverlayController controller, out _))
            {
                return controller;
            }

            return null;
        }

        public void ClearPatient()
        {
            if (swapRoutine != null)
            {
                StopCoroutine(swapRoutine);
                swapRoutine = null;
                // Don't leave the screen stuck mid-distortion if a swap gets
                // interrupted (e.g. scene teardown while it's mid-fade).
                TryGetVisionBleed()?.Clear();
                TryGetGifOverlay()?.ClearScatteredDuplicates();
            }

            ClearPatientVisualOnly();
        }

        private void ClearPatientVisualOnly()
        {
            currentPatient = null;
            currentTalkAnimator = null;
            if (currentInstance == null)
                return;

            GameObject instance = currentInstance;
            currentInstance = null;
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }

        public bool TryGetModel(PatientDefinition patient, out GameObject model)
        {
            PatientModelBinding binding = FindBinding(patient);
            model = binding?.Model;
            return model != null;
        }

        private PatientModelBinding FindBinding(PatientDefinition patient)
        {
            if (bindings == null)
                return null;

            for (int i = 0; i < bindings.Count; i++)
            {
                PatientModelBinding candidate = bindings[i];
                if (candidate != null && ReferenceEquals(candidate.Patient, patient))
                    return candidate;
            }

            return null;
        }
    }
}