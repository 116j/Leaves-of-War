using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Thin sequence facade for the Verdant Mirror encounter shared by
    /// Confession and Sacrifice. It composes simulation, arena, attacks, and the
    /// ending continuation without mutating NarrativeState.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossSequencePlayer : MonoBehaviour, ISequencePlayer
    {
        public const string VerdantMirrorSequenceId = "boss_verdant_mirror";
        private const string ConfessionPoisonCategoryId = "poison";
        private const string ConfessionPoisonUsedFlagId = "poison_used";

        // These fields deliberately remain on the facade so the checked-in Manor
        // scene retains its serialized production references and tuning values.
        [Header("Scene")]
        [SerializeField] private string sequenceId = VerdantMirrorSequenceId;
        [SerializeField] private Transform mirrorRoot;
        [FormerlySerializedAs("weakPointColliders")]
        [SerializeField] private Collider[] mirrorHitColliders = Array.Empty<Collider>();
        [SerializeField] private GameObject thornShooterView;
        [SerializeField] private Transform thornShooterMuzzle;
        [SerializeField] private GameObject confessionPoisonTarget;
        [SerializeField] private GameObject defeatedMirrorRoot;
        [SerializeField] private Material thornMaterial;
        [SerializeField] private Material dangerMaterial;

        [Header("Audio")]
        [Tooltip("Replaces whatever music is currently playing for the duration of the fight, restoring it afterward.")]
        [SerializeField] private AudioClip bossMusic;
        [SerializeField, Range(0f, 1f)] private float bossMusicVolume = 1f;
        [SerializeField] private AudioClip enemyHitSound;
        [SerializeField, Range(0f, 1f)] private float enemyHitSoundVolume = 1f;
        [SerializeField] private AudioClip playerHitSound;
        [SerializeField] private AudioClip enemyDeathSound;
        [SerializeField, Range(0f, 1f)] private float enemyDeathSoundVolume = 1f;
        [SerializeField] private AudioClip playerDeathSound;

        [Header("Death Timing")]
        [Tooltip("How long the defeated Mirror lingers, still visually intact, before swapping to the defeated view.")]
        [SerializeField, Min(0f)] private float mirrorDeathLingerSeconds = 1.5f;

        [Header("Health")]
        [SerializeField, Min(1)] private int playerHealth = 12;
        [SerializeField, Min(1)] private int mirrorHealth = 6;
        [SerializeField, Min(1)] private int shotHealthCost = 1;
        [SerializeField, Min(1)] private int shotDamage = 1;
        [SerializeField, Min(1)] private int mirrorAttackDamage = 3;

        [Header("Attack timing")]
        [SerializeField, Min(0.1f)] private float firstAttackDelay = 1.75f;
        [SerializeField, Min(0.1f)] private float attackInterval = 2.8f;
        [SerializeField, Min(0.1f)] private float attackTelegraphSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float attackProjectileSpeed = 7f;
        [SerializeField, Min(0.05f)] private float attackHitRadius = 0.72f;

        [Header("Shot presentation")]
        [SerializeField, Min(1f)] private float shotRange = 30f;
        [SerializeField, Min(0.01f)] private float shotVisualSeconds = 0.12f;
        [SerializeField, Min(0f)] private float shooterRecoilDistance = 0.055f;
        [SerializeField, Min(0f)] private float shooterRecoilDegrees = 5f;
        [SerializeField] private LayerMask shotMask = ~0;

        private GameSessionRegistration sessionRegistration;
        private SequencePlaybackHandle activePlayback;
        private BossArenaView arenaView;
        private BossAttackController attackController;
        private BossEndingContinuation endingContinuation;
        private IDiegeticHud hud;
        private BossCombatState combat;
        private bool isPlaying;
        private bool playbackCompleted;

        public string SequenceId => sequenceId;
        public SequenceLockFlags RequiredLocks => SequenceLockFlags.NarrativeInput;
        public BossCombatState CombatState => combat;
        public int RetryCount { get; private set; }

        public bool TryValidateProductionConfiguration(out string error)
        {
            if (mirrorRoot == null)
            {
                error = "has no living Verdant Mirror root";
                return false;
            }

            if (mirrorHitColliders == null || mirrorHitColliders.Length == 0)
            {
                error = "has no authored Verdant Mirror hit colliders";
                return false;
            }

            for (int i = 0; i < mirrorHitColliders.Length; i++)
            {
                if (mirrorHitColliders[i] == null)
                {
                    error = $"has a missing Verdant Mirror hit collider at index {i}";
                    return false;
                }
            }

            if (thornShooterView == null || thornShooterMuzzle == null)
            {
                error = "has no complete rose-thorn shooter view and muzzle";
                return false;
            }

            if (!thornShooterMuzzle.IsChildOf(thornShooterView.transform))
            {
                error = "has a rose-thorn shooter muzzle outside its weapon view";
                return false;
            }

            Camera shooterCamera = thornShooterView.GetComponentInParent<Camera>(true);
            global::FirstPersonController shooterPlayer = shooterCamera != null
                ? shooterCamera.GetComponentInParent<global::FirstPersonController>(true)
                : null;
            if (shooterCamera == null || shooterPlayer == null ||
                !ReferenceEquals(shooterPlayer.PlayerCamera, shooterCamera))
            {
                error = "has a rose-thorn shooter view outside the registered player camera hierarchy";
                return false;
            }

            if (thornShooterView.activeSelf)
            {
                error = "authors the rose-thorn shooter visible outside the fight";
                return false;
            }

            if (confessionPoisonTarget == null)
            {
                error = "has no Confession poison-use target";
                return false;
            }

            FlagInteractable poisonUse = confessionPoisonTarget.GetComponent<FlagInteractable>();
            if (poisonUse == null || poisonUse.RequiresCarried == null ||
                poisonUse.UsedItemDestination == null)
            {
                error = "has an incomplete Confession poison-use interaction or placement pose";
                return false;
            }

            if (!string.Equals(
                    poisonUse.RequiresCarried.Id,
                    ConfessionPoisonCategoryId,
                    StringComparison.Ordinal) ||
                poisonUse.Flag == null ||
                !string.Equals(
                    poisonUse.Flag.Id,
                    ConfessionPoisonUsedFlagId,
                    StringComparison.Ordinal))
            {
                error = "does not bind the canonical poison category and poison-used flag";
                return false;
            }

            if (poisonUse.UsedItemDestination.gameObject.scene != gameObject.scene)
            {
                error = "places the used Confession poison outside the encounter scene";
                return false;
            }

            if (poisonUse.RequiresCarried.RetainedAfterUse || poisonUse.DisablesAfterUse)
            {
                error = "does not consume and visibly place the Confession poison bottle";
                return false;
            }

            if (defeatedMirrorRoot == null)
            {
                error = "has no defeated Verdant Mirror reveal";
                return false;
            }

            if (thornMaterial == null || dangerMaterial == null)
            {
                error = "has incomplete thorn or danger combat materials";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void Awake()
        {
            EnsureSessionRegistration();
        }

        private void OnEnable()
        {
            SetConfessionTarget(false);
            EnsureSessionRegistration();
            sessionRegistration.Enable();
        }

        private void OnDisable()
        {
            activePlayback?.Cancel();
            sessionRegistration?.Disable();
            RestoreCompletedArena();
        }

        private void OnDestroy()
        {
            activePlayback?.Cancel();
            sessionRegistration?.Dispose();
            sessionRegistration = null;
            RestoreCompletedArena();
        }

        private void OnApplicationQuit()
        {
            activePlayback?.Cancel();
        }

        public IEnumerator Play(SequencePlaybackHandle playback)
        {
            if (playback == null)
            {
                Debug.LogError($"Sequence '{SequenceId}' received no playback handle.", this);
                yield break;
            }

            if (playback.IsTerminal)
                yield break;

            if (isPlaying)
            {
                playback.Fail($"Sequence '{SequenceId}' is already running.");
                yield break;
            }

            RestoreCompletedArena();
            isPlaying = true;
            playbackCompleted = false;
            RetryCount = 0;
            activePlayback = playback;
            playback.RegisterCleanup(() => CleanupPlayback(playback));

            string endingId = playback.EndingId;
            if (!BossEndingContinuation.Supports(endingId))
            {
                playback.Fail(
                    $"Sequence '{SequenceId}' requires the Confession or Sacrifice ending context; " +
                    $"received '{endingId ?? "<none>"}'.");
                yield break;
            }

            if (!TryValidateProductionConfiguration(out string configurationError))
            {
                playback.Fail(
                    $"Sequence '{SequenceId}' is not production-ready: {configurationError}.");
                yield break;
            }

            SceneServiceRegistry services = playback.SceneServices;
            if (!services.TryGetPlayer(out global::FirstPersonController player, out int playerMatches))
            {
                playback.Fail(playerMatches == 0
                    ? $"Sequence '{SequenceId}' requires one registered first-person player."
                    : $"Sequence '{SequenceId}' found {playerMatches} registered first-person players.");
                yield break;
            }

            if (!services.TryGetPlayerCamera(out Camera playerCamera, out int cameraMatches))
            {
                playback.Fail(cameraMatches == 0
                    ? $"Sequence '{SequenceId}' requires one registered first-person camera."
                    : $"Sequence '{SequenceId}' found {cameraMatches} registered first-person cameras.");
                yield break;
            }

            hud = ResolveHud(services);
            arenaView = new BossArenaView(
                transform,
                mirrorRoot,
                mirrorHitColliders,
                thornShooterView,
                thornShooterMuzzle,
                confessionPoisonTarget,
                defeatedMirrorRoot,
                thornMaterial,
                dangerMaterial,
                shooterRecoilDistance,
                shooterRecoilDegrees);
            if (!arenaView.TryPrepare(player, playerCamera, out string prepareError))
            {
                playback.Fail($"Sequence '{SequenceId}' cannot start. {prepareError}");
                yield break;
            }

            combat = new BossCombatState(
                playerHealth,
                mirrorHealth,
                shotHealthCost,
                shotDamage);
            attackController = new BossAttackController(
                combat,
                arenaView,
                player,
                playerCamera,
                playback.Clock,
                playback.Input,
                new BossAttackSettings(
                    mirrorAttackDamage,
                    firstAttackDelay,
                    attackInterval,
                    attackTelegraphSeconds,
                    attackProjectileSpeed,
                    attackHitRadius,
                    shotRange,
                    shotVisualSeconds,
                    shotMask,
                    enemyHitSound,
                    enemyHitSoundVolume,
                    playerHitSound));
            endingContinuation = new BossEndingContinuation(arenaView, hud);

            AudioManager.Instance?.PlayExclusiveMusic(bossMusic, loop: true, volumeScale: bossMusicVolume);

            arenaView.ResetEncounter(combat, restorePlayerPose: false);
            attackController.Reset();
            UpdateHud();

            // The click that dismissed the ending card must not also fire the
            // first health-costing thorn on the same input frame.
            yield return null;

            while (IsRunning(playback))
            {
                // Preserve the existing death-first resolution when one shot is
                // simultaneously fatal to both the player and the Mirror.
                if (combat.PlayerIsDead)
                {
                    yield return WaitForRetry(playback);
                    if (!IsRunning(playback))
                        yield break;

                    continue;
                }

                if (combat.MirrorIsDefeated)
                    break;

                attackController.Tick();
                UpdateHud();
                yield return null;
            }

            if (!IsRunning(playback) || combat == null || combat.PlayerIsDead)
                yield break;

            attackController.Stop();
            AudioManager.Instance?.PlaySoundEffect(enemyDeathSound, enemyDeathSoundVolume);

            if (mirrorDeathLingerSeconds > 0f)
                yield return new WaitForSecondsRealtime(mirrorDeathLingerSeconds);
            if (!IsRunning(playback))
                yield break;

            arenaView.RefreshMirrorState(combat);
            arenaView.SetShooterVisible(false);
            yield return endingContinuation.ContinueAfterVictory(endingId, playback);
            if (!IsRunning(playback))
                yield break;

            playbackCompleted = true;
        }

        public bool TryGetMirrorTargetPosition(out Vector3 position)
        {
            if (arenaView != null)
                return arenaView.TryGetMirrorTargetPosition(out position);

            position = default;
            return false;
        }

        private IEnumerator WaitForRetry(SequencePlaybackHandle playback)
        {
            attackController.Stop();
            arenaView.PrepareRetryPrompt();
            AudioManager.Instance?.PlaySoundEffect(playerDeathSound);
            hud?.SetGameplayHud(
                $"YOU DIED\nLEFT CLICK OR {InteractKeyLabel(playback)} — RETRY VERDANT MIRROR");

            while (IsRunning(playback) &&
                   !playback.Input.WasPressed(SequenceInputAction.Fire) &&
                   !playback.Input.WasPressed(SequenceInputAction.Interact))
            {
                yield return null;
            }

            if (!IsRunning(playback))
                yield break;

            RetryCount++;
            combat.Reset();
            arenaView.ResetEncounter(combat, restorePlayerPose: true);
            attackController.Reset();
            UpdateHud();

            // Consume the retry input boundary before combat resumes so the
            // retry click does not immediately spend one health on a shot.
            yield return null;
        }

        private static string InteractKeyLabel(SequencePlaybackHandle playback)
        {
            if (playback?.SceneServices != null &&
                playback.SceneServices.TryGetPlayer(
                    out global::FirstPersonController player,
                    out _))
            {
                InputAction interactAction = player.InteractAction;
                if (interactAction != null)
                {
                    string label = interactAction.GetBindingDisplayString(0);
                    if (!string.IsNullOrWhiteSpace(label))
                        return label.ToUpperInvariant();
                }
            }

            return "E";
        }

        private void UpdateHud()
        {
            if (combat == null)
                return;

            hud?.SetGameplayHud(
                $"VERDANT MIRROR\n" +
                $"HEALTH {combat.PlayerHealth}/{combat.MaxPlayerHealth}    " +
                $"MIRROR HP {combat.MirrorHealth}/{combat.MaxMirrorHealth}\n" +
                $"LEFT CLICK: FIRE THORN  (COST {combat.ShotHealthCost} HEALTH)");
        }

        private static IDiegeticHud ResolveHud(SceneServiceRegistry services)
        {
            if (services != null &&
                services.TryGetPresenter(out INarrativePresenter presenter, out _) &&
                presenter is IDiegeticHud diegeticHud)
            {
                return diegeticHud;
            }

            return null;
        }

        private bool IsRunning(SequencePlaybackHandle playback) =>
            ReferenceEquals(activePlayback, playback) &&
            playback.Status == SequencePlaybackStatus.Running &&
            !playback.IsCancellationRequested;

        private void CleanupPlayback(SequencePlaybackHandle playback)
        {
            if (!ReferenceEquals(activePlayback, playback))
                return;

            bool preserveArena =
                playback.Status == SequencePlaybackStatus.Completed && playbackCompleted;

            attackController?.Stop();
            arenaView?.Cleanup(resetArena: !preserveArena);
            hud?.ClearGameplayHud();
            AudioManager.Instance?.EndExclusiveMusic(stopMusic: true);

            activePlayback = null;
            attackController = null;
            endingContinuation = null;
            hud = null;
            isPlaying = false;

            if (!preserveArena)
            {
                arenaView = null;
                combat = null;
            }
        }

        private void RestoreCompletedArena()
        {
            if (activePlayback != null || arenaView == null)
                return;

            arenaView.Cleanup(resetArena: true);
            arenaView = null;
            combat = null;
        }

        private void EnsureSessionRegistration()
        {
            if (sessionRegistration != null)
                return;

            sessionRegistration = new GameSessionRegistration(
                session => session.RegisterSequencePlayer(this),
                session => session.UnregisterSequencePlayer(this));
        }

        private void SetConfessionTarget(bool visible)
        {
            if (confessionPoisonTarget != null)
                confessionPoisonTarget.SetActive(visible);
        }
    }
}