using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    public readonly struct BossAttackSettings
    {
        public BossAttackSettings(
            int mirrorAttackDamage,
            float firstAttackDelay,
            float attackInterval,
            float attackTelegraphSeconds,
            float attackProjectileSpeed,
            float attackHitRadius,
            float shotRange,
            float shotVisualSeconds,
            LayerMask shotMask,
            AudioClip enemyHitSound,
            float enemyHitSoundVolume,
            AudioClip playerHitSound)
        {
            MirrorAttackDamage = Mathf.Max(1, mirrorAttackDamage);
            FirstAttackDelay = Mathf.Max(0.1f, firstAttackDelay);
            AttackInterval = Mathf.Max(0.1f, attackInterval);
            AttackTelegraphSeconds = Mathf.Max(0.1f, attackTelegraphSeconds);
            AttackProjectileSpeed = Mathf.Max(0.1f, attackProjectileSpeed);
            AttackHitRadius = Mathf.Max(0.05f, attackHitRadius);
            ShotRange = Mathf.Max(1f, shotRange);
            ShotVisualSeconds = Mathf.Max(0.01f, shotVisualSeconds);
            ShotMask = shotMask;
            EnemyHitSound = enemyHitSound;
            EnemyHitSoundVolume = enemyHitSoundVolume;
            PlayerHitSound = playerHitSound;
        }

        public int MirrorAttackDamage { get; }
        public float FirstAttackDelay { get; }
        public float AttackInterval { get; }
        public float AttackTelegraphSeconds { get; }
        public float AttackProjectileSpeed { get; }
        public float AttackHitRadius { get; }
        public float ShotRange { get; }
        public float ShotVisualSeconds { get; }
        public LayerMask ShotMask { get; }
        public AudioClip EnemyHitSound { get; }
        public float EnemyHitSoundVolume { get; }
        public AudioClip PlayerHitSound { get; }
    }

    /// <summary>
    /// Converts sequence input and unscaled time into player shots and Mirror
    /// attacks. Scene mutation is delegated to <see cref="BossArenaView"/> and
    /// health mutation to <see cref="BossCombatState"/>.
    /// </summary>
    public sealed class BossAttackController
    {
        private readonly BossCombatState combat;
        private readonly BossArenaView arena;
        private readonly global::FirstPersonController player;
        private readonly Camera playerCamera;
        private readonly ISequenceClock clock;
        private readonly ISequenceInput input;
        private readonly BossAttackSettings settings;
        private readonly List<AttackProjectile> projectiles = new List<AttackProjectile>();

        private float nextAttackAt;
        private float telegraphEndsAt;
        private Vector3 telegraphedDirection;
        private bool telegraphActive;

        public BossAttackController(
            BossCombatState combat,
            BossArenaView arena,
            global::FirstPersonController player,
            Camera playerCamera,
            ISequenceClock clock,
            ISequenceInput input,
            BossAttackSettings settings)
        {
            this.combat = combat;
            this.arena = arena;
            this.player = player;
            this.playerCamera = playerCamera;
            this.clock = clock;
            this.input = input;
            this.settings = settings;
        }

        public bool TelegraphActive => telegraphActive;
        public int PendingProjectileCount => projectiles.Count;

        public void Reset()
        {
            Stop();
            nextAttackAt = clock.UnscaledTime + settings.FirstAttackDelay;
        }

        public void Tick()
        {
            if (combat == null || arena == null || player == null || playerCamera == null)
                return;

            if (!combat.PlayerIsDead && !combat.MirrorIsDefeated &&
                input.WasPressed(SequenceInputAction.Fire) &&
                input.PointerIsCaptured)
            {
                FirePlayerShot();
            }

            UpdateMirrorAttack();
            arena.UpdatePlayerShots(clock.UnscaledTime);
        }

        public BossShotResult FirePlayerShot()
        {
            if (combat == null || arena == null || playerCamera == null)
                return BossShotResult.None;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            Vector3 end = ray.origin + ray.direction * settings.ShotRange;
            bool validTarget = false;

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                settings.ShotRange,
                settings.ShotMask,
                QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                validTarget = arena.IsMirrorHit(hit.collider);
            }

            BossShotResult result = combat.Fire(validTarget);
            if ((result & BossShotResult.Fired) == 0)
                return result;

            arena.CreatePlayerShot(
                arena.PlayerShotOrigin,
                end,
                clock.UnscaledTime + settings.ShotVisualSeconds);
            if ((result & BossShotResult.HitMirror) != 0)
            {
                arena.RefreshMirrorState(combat);
                AudioManager.Instance?.PlaySoundEffect(settings.EnemyHitSound, settings.EnemyHitSoundVolume);
            }
            return result;
        }

        public void Stop()
        {
            telegraphActive = false;
            telegraphedDirection = default;
            projectiles.Clear();
            arena?.ClearTransientCombatObjects();
        }

        private void UpdateMirrorAttack()
        {
            if (combat.PlayerIsDead || combat.MirrorIsDefeated)
                return;

            float now = clock.UnscaledTime;
            if (!telegraphActive && now >= nextAttackAt)
                BeginAttackTelegraph(now);

            if (telegraphActive && now >= telegraphEndsAt)
                LaunchMirrorAttack(now);

            float delta = Mathf.Max(0f, clock.UnscaledDeltaTime);
            Vector3 playerPosition = player.transform.position + Vector3.up * 0.9f;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                AttackProjectile projectile = projectiles[i];
                if (!arena.TryGetProjectilePosition(projectile.Visual, out Vector3 from))
                {
                    projectiles.RemoveAt(i);
                    continue;
                }

                float distance = settings.AttackProjectileSpeed * delta;
                Vector3 to = from + projectile.Direction * distance;
                arena.SetProjectilePosition(projectile.Visual, to);
                projectile.Travelled += distance;

                if (DistancePointToSegment(playerPosition, from, to) <= settings.AttackHitRadius)
                {
                    arena.DestroyAttackProjectile(projectile.Visual);
                    projectiles.RemoveAt(i);
                    combat.ApplyMirrorAttack(settings.MirrorAttackDamage);
                    AudioManager.Instance?.PlaySoundEffect(settings.PlayerHitSound);
                    continue;
                }

                if (projectile.Travelled >= settings.ShotRange)
                {
                    arena.DestroyAttackProjectile(projectile.Visual);
                    projectiles.RemoveAt(i);
                    continue;
                }

                projectiles[i] = projectile;
            }
        }

        private void BeginAttackTelegraph(float now)
        {
            Vector3 origin = arena.AttackOrigin;
            Vector3 target = player.transform.position + Vector3.up * 0.9f;
            telegraphedDirection = (target - origin).normalized;
            telegraphEndsAt = now + settings.AttackTelegraphSeconds;
            telegraphActive = true;
            arena.ShowAttackTelegraph(telegraphedDirection, settings.ShotRange);
        }

        private void LaunchMirrorAttack(float now)
        {
            arena.ClearAttackTelegraph();
            telegraphActive = false;
            nextAttackAt = now + settings.AttackInterval;
            GameObject visual = arena.CreateAttackProjectile();
            projectiles.Add(new AttackProjectile(visual, telegraphedDirection));
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return Vector3.Distance(point, from);

            float t = Mathf.Clamp01(Vector3.Dot(point - from, segment) / lengthSquared);
            return Vector3.Distance(point, from + segment * t);
        }

        private struct AttackProjectile
        {
            public AttackProjectile(GameObject visual, Vector3 direction)
            {
                Visual = visual;
                Direction = direction;
                Travelled = 0f;
            }

            public GameObject Visual;
            public Vector3 Direction;
            public float Travelled;
        }
    }
}