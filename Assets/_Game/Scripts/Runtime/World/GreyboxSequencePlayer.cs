using System;
using System.Collections;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Scene-side blockout presentation for one-way authored set-pieces. Known
    /// gameplay-script ids receive a legible card and simple primitive staging;
    /// unknown ids retain the short reveal-only recovery behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GreyboxSequencePlayer : MonoBehaviour, ISequencePlayer
    {
        private const float MinimumLegibleHoldSeconds = 0.75f;
        private const float DefaultActionSeconds = 1.1f;
        private const string AdvancePrompt = "CLICK / ENTER TO CONTINUE";

        [SerializeField] private string sequenceId;
        [SerializeField] private GameObject revealRoot;
        [SerializeField, Min(0f)] private float unscaledHoldSeconds = 0.25f;

        private int playbackGeneration;
        private int activeGeneration;
        private bool playbackActive;
        private bool revealWasActive;
        private bool restoreChildrenOnCompletion;
        private GameSessionRegistration sessionRegistration;
        private SequencePlaybackHandle activePlayback;
        private CenteredAuthorialOverlay activeOverlay;
        private VisionBleedController activeVision;
        private float originalVisionIntensity;
        private Camera activeCamera;
        private TransformSnapshot cameraSnapshot;
        private float originalFieldOfView;
        private TransformSnapshot[] childSnapshots;

        public string SequenceId => sequenceId;
        public SequenceLockFlags RequiredLocks => SequenceLockFlags.NarrativeInput;

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterSequencePlayer(this),
                session => session.UnregisterSequencePlayer(this));
        }

        private void OnEnable()
        {
            sessionRegistration?.Enable();
        }

        private void OnDisable()
        {
            sessionRegistration?.Disable();
            activePlayback?.Cancel();
        }

        private void OnDestroy()
        {
            sessionRegistration?.Dispose();
            sessionRegistration = null;
            activePlayback?.Cancel();
        }

        public IEnumerator Play(SequencePlaybackHandle playback)
        {
            if (playback == null)
                throw new ArgumentNullException(nameof(playback));
            if (playback.IsTerminal)
                return EmptyRoutine();

            activePlayback?.Cancel();
            activePlayback = playback;
            int generation = ++playbackGeneration;
            playback.RegisterCleanup(() => CleanupPlayback(generation, playback));
            return PlayRoutine(generation, playback);
        }

        private static IEnumerator EmptyRoutine()
        {
            yield break;
        }

        private IEnumerator PlayRoutine(int generation, SequencePlaybackHandle playback)
        {
            playbackActive = true;
            activeGeneration = generation;
            revealWasActive = revealRoot != null && revealRoot.activeSelf;
            if (revealRoot != null)
                revealRoot.SetActive(true);

            // Preserve the zero-duration authoring/test path as an immediate
            // one-way reveal. Canonical scene instances use a positive hold.
            if (unscaledHoldSeconds <= 0f)
            {
                yield break;
            }

            if (!TryGetPresentation(sequenceId, out SequencePresentation presentation))
            {
                yield return WaitForDuration(unscaledHoldSeconds, generation);
                yield break;
            }

            activeOverlay = CenteredAuthorialOverlay.Create(
                $"Asset-less Sequence — {sequenceId}",
                presentation.BackgroundColor,
                presentation.AccentColor,
                ResolveTheme(playback.SceneServices));
            activeOverlay.SetContent(
                presentation.Heading,
                presentation.Body,
                AdvancePrompt);
            activeOverlay.Alpha = 0f;

            yield return StagePresentation(presentation.Kind, generation);
            if (!IsCurrent(generation))
                yield break;

            float minimumHold = Mathf.Max(MinimumLegibleHoldSeconds, unscaledHoldSeconds);
            yield return WaitForAdvance(minimumHold, generation);
        }

        private IEnumerator StagePresentation(SequenceKind kind, int generation)
        {
            switch (kind)
            {
                case SequenceKind.StudySeat:
                    activeOverlay.BackgroundAlpha = 0.94f;
                    yield return FadeOverlay(0f, 1f, 0.35f, generation);
                    break;

                case SequenceKind.StudyReveal:
                    activeOverlay.BackgroundAlpha = 0.88f;
                    yield return FadeOverlay(0f, 1f, 0.3f, generation);
                    yield return AnimateValue(
                        0.65f,
                        generation,
                        value => activeOverlay.BackgroundAlpha = Mathf.Lerp(0.88f, 0.26f, value));
                    break;

                case SequenceKind.DreamFlood:
                    activeOverlay.BackgroundAlpha = 0.2f;
                    activeOverlay.Alpha = 1f;
                    PrepareVision();
                    PrepareCamera();
                    Transform water = FindChildContaining(revealRoot, "Water");
                    childSnapshots = SnapshotTransforms(water != null ? new[] { water } : null);
                    Vector3 waterEnd = water != null ? water.localPosition : Vector3.zero;
                    if (water != null)
                        water.localPosition = waterEnd + Vector3.down * 1.25f;

                    yield return AnimateValue(
                        DefaultActionSeconds,
                        generation,
                        value =>
                        {
                            if (water != null)
                                water.localPosition = Vector3.Lerp(
                                    waterEnd + Vector3.down * 1.25f,
                                    waterEnd,
                                    Smooth(value));
                            if (activeVision != null)
                            {
                                activeVision.SetIntensity(Mathf.Lerp(
                                    originalVisionIntensity,
                                    0.72f,
                                    Smooth(value)));
                            }
                            if (activeCamera != null)
                            {
                                float roll = Mathf.Sin(value * Mathf.PI * 4f) * (1f - value) * 2.5f;
                                activeCamera.transform.localRotation =
                                    cameraSnapshot.LocalRotation * Quaternion.Euler(0f, 0f, roll);
                            }
                        });
                    break;

                case SequenceKind.DreamAttack:
                    activeOverlay.BackgroundAlpha = 0.16f;
                    activeOverlay.Alpha = 1f;
                    PrepareVision();
                    PrepareCamera();
                    childSnapshots = SnapshotDirectChildren(revealRoot);
                    restoreChildrenOnCompletion = true;
                    yield return AnimateValue(
                        DefaultActionSeconds,
                        generation,
                        value =>
                        {
                            float eased = Smooth(value);
                            if (activeVision != null)
                            {
                                activeVision.SetIntensity(Mathf.Lerp(
                                    originalVisionIntensity,
                                    0.92f,
                                    eased));
                            }

                            AnimateChildrenTowardCamera(childSnapshots, activeCamera, eased);
                            if (activeCamera != null)
                            {
                                activeCamera.fieldOfView = Mathf.Lerp(
                                    originalFieldOfView,
                                    originalFieldOfView - 8f,
                                    eased);
                            }
                        });
                    break;

                case SequenceKind.Withering:
                    activeOverlay.BackgroundAlpha = 0.24f;
                    activeOverlay.Alpha = 1f;
                    childSnapshots = SnapshotDirectChildren(revealRoot);
                    yield return AnimateValue(
                        DefaultActionSeconds,
                        generation,
                        value => AnimateWithering(childSnapshots, Smooth(value)));
                    break;

                case SequenceKind.EnterPortrait:
                    activeOverlay.BackgroundAlpha = 0.12f;
                    activeOverlay.Alpha = 1f;
                    PrepareCamera();
                    Vector3 cameraEndPosition = activeCamera != null
                        ? ResolvePortraitCameraEnd(activeCamera, revealRoot)
                        : Vector3.zero;
                    Quaternion cameraEndRotation = activeCamera != null
                        ? ResolvePortraitCameraRotation(activeCamera, revealRoot)
                        : Quaternion.identity;
                    yield return AnimateValue(
                        DefaultActionSeconds * 1.35f,
                        generation,
                        value =>
                        {
                            float eased = Smooth(value);
                            if (activeCamera != null)
                            {
                                activeCamera.transform.position = Vector3.Lerp(
                                    cameraSnapshot.WorldPosition,
                                    cameraEndPosition,
                                    eased);
                                activeCamera.transform.rotation = Quaternion.Slerp(
                                    cameraSnapshot.WorldRotation,
                                    cameraEndRotation,
                                    eased);
                            }
                            activeOverlay.BackgroundAlpha = Mathf.Lerp(0.12f, 1f, eased);
                        });
                    break;
            }
        }

        private IEnumerator FadeOverlay(float from, float to, float seconds, int generation)
        {
            yield return AnimateValue(
                seconds,
                generation,
                value => activeOverlay.Alpha = Mathf.Lerp(from, to, value));
        }

        private IEnumerator AnimateValue(float seconds, int generation, Action<float> apply)
        {
            if (seconds <= 0f)
            {
                apply?.Invoke(1f);
                yield break;
            }

            float elapsed = 0f;
            while (IsCurrent(generation) && elapsed < seconds)
            {
                elapsed += FrameDelta();
                apply?.Invoke(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            if (IsCurrent(generation))
                apply?.Invoke(1f);
        }

        private IEnumerator WaitForAdvance(float minimumSeconds, int generation)
        {
            float elapsed = 0f;
            while (IsCurrent(generation))
            {
                elapsed += FrameDelta();
                if (elapsed >= minimumSeconds && AdvancePressed())
                    yield break;
                yield return null;
            }
        }

        private IEnumerator WaitForDuration(float seconds, int generation)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (IsCurrent(generation) && remaining > 0f)
            {
                remaining -= FrameDelta();
                yield return null;
            }
        }

        private void PrepareVision()
        {
            activeVision = null;
            activePlayback?.SceneServices.TryGetVisionBleed(out activeVision, out _);
            originalVisionIntensity = activeVision != null ? activeVision.Intensity : 0f;
        }

        private void PrepareCamera()
        {
            activeCamera = null;
            activePlayback?.SceneServices.TryGetPlayerCamera(out activeCamera, out _);
            if (activeCamera == null)
                return;

            cameraSnapshot = new TransformSnapshot(activeCamera.transform);
            originalFieldOfView = activeCamera.fieldOfView;
        }

        private void CleanupPlayback(int generation, SequencePlaybackHandle playback)
        {
            if (!playbackActive ||
                activeGeneration != generation ||
                !ReferenceEquals(activePlayback, playback))
            {
                return;
            }

            bool cancelled = playback.Status != SequencePlaybackStatus.Completed;

            if (activeCamera != null)
            {
                cameraSnapshot.Restore();
                activeCamera.fieldOfView = originalFieldOfView;
            }

            if (activeVision != null)
                activeVision.SetIntensity(originalVisionIntensity);

            if (childSnapshots != null && (cancelled || restoreChildrenOnCompletion))
                RestoreSnapshots(childSnapshots);

            if (cancelled && revealRoot != null)
                revealRoot.SetActive(revealWasActive);

            activeOverlay?.Dispose();
            activeOverlay = null;

            activeVision = null;
            activeCamera = null;
            childSnapshots = null;
            restoreChildrenOnCompletion = false;
            playbackActive = false;
            activeGeneration = 0;
            activePlayback = null;
        }

        private bool IsCurrent(int generation) =>
            playbackActive &&
            activeGeneration == generation &&
            playbackGeneration == generation &&
            activePlayback != null &&
            !activePlayback.IsCancellationRequested &&
            isActiveAndEnabled;

        private float FrameDelta() => Mathf.Max(
            0f,
            activePlayback != null ? activePlayback.Clock.UnscaledDeltaTime : 0f);

        private bool AdvancePressed() => activePlayback != null &&
            activePlayback.Input.WasPressed(SequenceInputAction.Advance);

        private static RetroUiTheme ResolveTheme(SceneServiceRegistry services)
        {
            if (services != null &&
                services.TryGetPresenter(out INarrativePresenter presenter, out _) &&
                presenter is NarrativePresenter narrativePresenter)
            {
                return narrativePresenter.UiTheme;
            }

            return RetroUiTheme.Resolve(null);
        }

        private static void AnimateChildrenTowardCamera(
            TransformSnapshot[] snapshots,
            Camera camera,
            float amount)
        {
            if (snapshots == null || camera == null)
                return;

            for (int i = 0; i < snapshots.Length; i++)
            {
                TransformSnapshot snapshot = snapshots[i];
                if (snapshot.Target == null)
                    continue;

                float spread = (i - (snapshots.Length - 1) * 0.5f) * 0.08f;
                Vector3 target = camera.transform.position +
                    camera.transform.forward * (0.55f + (i % 3) * 0.08f) +
                    camera.transform.right * spread;
                snapshot.Target.position = Vector3.Lerp(snapshot.WorldPosition, target, amount);
                snapshot.Target.localScale = Vector3.Lerp(
                    snapshot.LocalScale * 0.6f,
                    snapshot.LocalScale * 1.35f,
                    amount);
            }
        }

        private static void AnimateWithering(TransformSnapshot[] snapshots, float amount)
        {
            if (snapshots == null)
                return;

            for (int i = 0; i < snapshots.Length; i++)
            {
                TransformSnapshot snapshot = snapshots[i];
                if (snapshot.Target == null)
                    continue;

                float fall = 0.65f + i * 0.08f;
                snapshot.Target.localPosition = snapshot.LocalPosition + Vector3.down * fall * amount;
                snapshot.Target.localRotation = snapshot.LocalRotation *
                    Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 55f * amount);
            }
        }

        private static Vector3 ResolvePortraitCameraEnd(Camera camera, GameObject root)
        {
            Vector3 target = ResolveVisibleCenter(root, camera.transform.position + camera.transform.forward * 2f);
            Vector3 direction = target - camera.transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                return camera.transform.position;

            return target - direction.normalized * 0.3f;
        }

        private static Quaternion ResolvePortraitCameraRotation(Camera camera, GameObject root)
        {
            Vector3 target = ResolveVisibleCenter(root, camera.transform.position + camera.transform.forward * 2f);
            Vector3 direction = target - camera.transform.position;
            return direction.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : camera.transform.rotation;
        }

        private static Vector3 ResolveVisibleCenter(GameObject root, Vector3 fallback)
        {
            if (root == null)
                return fallback;

            Renderer renderer = root.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.bounds.center : root.transform.position;
        }

        private static Transform FindChildContaining(GameObject root, string nameFragment)
        {
            if (root == null || string.IsNullOrEmpty(nameFragment))
                return null;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null &&
                    child != root.transform &&
                    child.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }

            return null;
        }

        private static TransformSnapshot[] SnapshotDirectChildren(GameObject root)
        {
            if (root == null)
                return Array.Empty<TransformSnapshot>();

            int count = root.transform.childCount;
            var snapshots = new TransformSnapshot[count];
            for (int i = 0; i < count; i++)
                snapshots[i] = new TransformSnapshot(root.transform.GetChild(i));
            return snapshots;
        }

        private static TransformSnapshot[] SnapshotTransforms(Transform[] transforms)
        {
            if (transforms == null || transforms.Length == 0)
                return Array.Empty<TransformSnapshot>();

            var snapshots = new TransformSnapshot[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
                snapshots[i] = new TransformSnapshot(transforms[i]);
            return snapshots;
        }

        private static void RestoreSnapshots(TransformSnapshot[] snapshots)
        {
            for (int i = 0; i < snapshots.Length; i++)
                snapshots[i].Restore();
        }

        private static float Smooth(float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

        private static bool TryGetPresentation(string id, out SequencePresentation presentation)
        {
            switch (id)
            {
                case "study_1910_seat":
                    presentation = new SequencePresentation(
                        SequenceKind.StudySeat,
                        "THE CONSULTING OFFICE · 1910",
                        "SEATED FOR THE PATIENT SESSIONS\nMOVEMENT LOCKED",
                        new Color(0.025f, 0.022f, 0.018f, 0.94f),
                        new Color(0.76f, 0.7f, 0.55f));
                    return true;

                case "study_1910_reveal":
                    presentation = new SequencePresentation(
                        SequenceKind.StudyReveal,
                        "THE STUDY · 1910",
                        "THE LAST PATIENT LEAVES.\nTHE EMPTY STUDY IS OPEN FOR EXAMINATION.",
                        new Color(0.02f, 0.018f, 0.015f, 0.88f),
                        new Color(0.76f, 0.7f, 0.55f));
                    return true;

                case "study_1915_seat":
                    presentation = new SequencePresentation(
                        SequenceKind.StudySeat,
                        "THE CONSULTING OFFICE · 1915",
                        "THE WARTIME PATIENT SESSIONS BEGIN.\nMOVEMENT LOCKED",
                        new Color(0.02f, 0.021f, 0.018f, 0.94f),
                        new Color(0.65f, 0.7f, 0.54f));
                    return true;

                case "study_1915_reveal":
                    presentation = new SequencePresentation(
                        SequenceKind.StudyReveal,
                        "THE STUDY · 1915",
                        "THE LAST PATIENT LEAVES.\nTHE EMPTY STUDY IS OPEN FOR EXAMINATION.",
                        new Color(0.02f, 0.021f, 0.018f, 0.88f),
                        new Color(0.65f, 0.7f, 0.54f));
                    return true;

                case "dream_2_flood":
                    presentation = new SequencePresentation(
                        SequenceKind.DreamFlood,
                        "",
                        "",
                        new Color(0.01f, 0.018f, 0.022f, 0.2f),
                        new Color(0.49f, 0.64f, 0.58f));
                    return true;

                case "dream_3_attack":
                    presentation = new SequencePresentation(
                        SequenceKind.DreamAttack,
                        "",
                        "",
                        new Color(0.055f, 0.008f, 0.012f, 0.16f),
                        new Color(0.73f, 0.38f, 0.36f));
                    return true;

                case "blooms_brown_and_fall":
                    presentation = new SequencePresentation(
                        SequenceKind.Withering,
                        "CONFESSION",
                        "THE BLOOMS BROWN AND FALL.\nTHE VERDANT MIRROR DIES.",
                        new Color(0.035f, 0.025f, 0.015f, 0.24f),
                        new Color(0.67f, 0.51f, 0.34f));
                    return true;

                case "walks_into_the_painting":
                    presentation = new SequencePresentation(
                        SequenceKind.EnterPortrait,
                        "DENIAL",
                        "YOU WALK INTO THE LIVING PORTRAIT.\nTHE FRAME CLOSES BEHIND YOU.",
                        new Color(0.008f, 0.018f, 0.01f, 0.12f),
                        new Color(0.44f, 0.63f, 0.4f));
                    return true;

                default:
                    presentation = default;
                    return false;
            }
        }

        private enum SequenceKind
        {
            StudySeat,
            StudyReveal,
            DreamFlood,
            DreamAttack,
            Withering,
            EnterPortrait
        }

        private readonly struct SequencePresentation
        {
            public SequencePresentation(
                SequenceKind kind,
                string heading,
                string body,
                Color backgroundColor,
                Color accentColor)
            {
                Kind = kind;
                Heading = heading;
                Body = body;
                BackgroundColor = backgroundColor;
                AccentColor = accentColor;
            }

            public SequenceKind Kind { get; }
            public string Heading { get; }
            public string Body { get; }
            public Color BackgroundColor { get; }
            public Color AccentColor { get; }
        }

        private readonly struct TransformSnapshot
        {
            public TransformSnapshot(Transform target)
            {
                Target = target;
                LocalPosition = target != null ? target.localPosition : Vector3.zero;
                LocalRotation = target != null ? target.localRotation : Quaternion.identity;
                LocalScale = target != null ? target.localScale : Vector3.one;
                WorldPosition = target != null ? target.position : Vector3.zero;
                WorldRotation = target != null ? target.rotation : Quaternion.identity;
            }

            public Transform Target { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
            public Vector3 WorldPosition { get; }
            public Quaternion WorldRotation { get; }

            public void Restore()
            {
                if (Target == null)
                    return;

                Target.localPosition = LocalPosition;
                Target.localRotation = LocalRotation;
                Target.localScale = LocalScale;
            }
        }
    }
}
