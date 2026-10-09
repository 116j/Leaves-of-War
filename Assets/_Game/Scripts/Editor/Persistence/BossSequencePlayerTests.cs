using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class BossSequencePlayerTests
    {
        [Test]
        public void Shot_AlwaysCostsHealthAndDirectMirrorHitsTakeDamage()
        {
            var state = new BossCombatState(
                playerHealth: 8,
                mirrorHealth: 3,
                shotHealthCost: 1,
                shotDamage: 1);

            BossShotResult miss = state.Fire(hitValidMirrorTarget: false);

            Assert.That(miss.HasFlag(BossShotResult.Fired), Is.True);
            Assert.That(miss.HasFlag(BossShotResult.HitMirror), Is.False);
            Assert.That(state.PlayerHealth, Is.EqualTo(7));
            Assert.That(state.MirrorHealth, Is.EqualTo(3));

            BossShotResult hit = state.Fire(hitValidMirrorTarget: true);

            Assert.That(hit.HasFlag(BossShotResult.HitMirror), Is.True);
            Assert.That(state.PlayerHealth, Is.EqualTo(6));
            Assert.That(state.MirrorHealth, Is.EqualTo(2));
        }

        [Test]
        public void DeathAndRetry_ResetOnlyTheEncounterHealth()
        {
            var state = new BossCombatState(
                playerHealth: 5,
                mirrorHealth: 4,
                shotHealthCost: 1,
                shotDamage: 1);

            state.Fire(hitValidMirrorTarget: true);
            state.ApplyMirrorAttack(99);

            Assert.That(state.PlayerIsDead, Is.True);
            Assert.That(state.MirrorHealth, Is.EqualTo(3));

            state.Reset();

            Assert.That(state.PlayerHealth, Is.EqualTo(5));
            Assert.That(state.MirrorHealth, Is.EqualTo(4));
            Assert.That(state.PlayerIsDead, Is.False);
            Assert.That(state.MirrorIsDefeated, Is.False);
        }

        [Test]
        public void SimultaneousFatalShot_ReportsBothOutcomesForDeathFirstResolution()
        {
            var state = new BossCombatState(
                playerHealth: 1,
                mirrorHealth: 1,
                shotHealthCost: 1,
                shotDamage: 1);

            BossShotResult result = state.Fire(hitValidMirrorTarget: true);

            Assert.That(result.HasFlag(BossShotResult.PlayerDied), Is.True);
            Assert.That(result.HasFlag(BossShotResult.MirrorDefeated), Is.True);
            Assert.That(state.PlayerIsDead, Is.True);
            Assert.That(state.MirrorIsDefeated, Is.True);
        }

        [Test]
        public void SacrificeThorn_CommitsExactlyOnceAndIsNotPersistentState()
        {
            var gameObject = new GameObject("Sacrifice Thorn Test");
            try
            {
                SacrificeThornInteractable thorn =
                    gameObject.AddComponent<SacrificeThornInteractable>();
                int commits = 0;
                thorn.Committed += () => commits++;

                thorn.Arm();
                Assert.That(thorn.Prompt, Is.EqualTo("COMMIT SACRIFICE"));

                thorn.Interact();
                thorn.Interact();

                Assert.That(thorn.IsCommitted, Is.True);
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ArenaView_UsesWholeFightHitVolumesAndRestoresTheirAuthoredState()
        {
            var owner = new GameObject("Boss Arena Owner");
            var playerObject = new GameObject(
                "Boss Arena Player",
                typeof(CharacterController));
            var cameraObject = new GameObject("Boss Arena Camera", typeof(Camera));
            var shooterObject = new GameObject("Rose-Thorn Shooter");
            var muzzleObject = new GameObject("Muzzle");
            var mirrorObject = new GameObject("Living Portrait");
            GameObject hitVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var confessionTarget = new GameObject("Confession Target");
            var defeatedRoot = new GameObject("Defeated Root");
            try
            {
                playerObject.SetActive(false);
                cameraObject.transform.SetParent(playerObject.transform, false);
                shooterObject.transform.SetParent(cameraObject.transform, false);
                muzzleObject.transform.SetParent(shooterObject.transform, false);
                shooterObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                shooterObject.SetActive(false);
                FirstPersonController player = playerObject.AddComponent<FirstPersonController>();
                Camera camera = cameraObject.GetComponent<Camera>();
                hitVolume.name = "Mirror Hit Volume";
                hitVolume.transform.SetParent(mirrorObject.transform, false);
                Renderer renderer = hitVolume.GetComponent<Renderer>();
                Collider collider = hitVolume.GetComponent<Collider>();
                collider.enabled = false;

                var arena = new BossArenaView(
                    owner.transform,
                    mirrorObject.transform,
                    new[] { collider },
                    shooterObject,
                    muzzleObject.transform,
                    confessionTarget,
                    defeatedRoot,
                    null,
                    null);

                Assert.That(arena.TryPrepare(player, camera, out string error), Is.True, error);
                var combat = new BossCombatState(4, 2, 1, 1);
                arena.ResetEncounter(combat, restorePlayerPose: false);

                Assert.That(renderer.enabled, Is.True);
                Assert.That(collider.enabled, Is.True);
                Assert.That(arena.IsMirrorHit(collider), Is.True);
                Assert.That(arena.TryGetMirrorTargetPosition(out _), Is.True);
                Assert.That(shooterObject.activeSelf, Is.True);

                combat.Fire(hitValidMirrorTarget: true);
                arena.RefreshMirrorState(combat);
                Assert.That(collider.enabled, Is.True,
                    "Direct-hit volume must remain active until the boss is defeated.");

                Vector3 shooterRest = shooterObject.transform.localPosition;
                Quaternion shooterRestRotation = shooterObject.transform.localRotation;
                arena.CreatePlayerShot(Vector3.zero, Vector3.forward, expiresAt: 1f);
                Vector3 expectedRecoil = shooterRest +
                    shooterRestRotation * Vector3.back * 0.055f;
                Assert.That(
                    Vector3.Distance(shooterObject.transform.localPosition, expectedRecoil),
                    Is.LessThan(0.0001f));
                arena.UpdatePlayerShots(1f);
                Assert.That(shooterObject.transform.localPosition, Is.EqualTo(shooterRest));

                arena.Cleanup(resetArena: true);

                Assert.That(renderer.enabled, Is.True);
                Assert.That(collider.enabled, Is.False);
                Assert.That(confessionTarget.activeSelf, Is.False);
                Assert.That(defeatedRoot.activeSelf, Is.False);
                Assert.That(shooterObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(mirrorObject);
                Object.DestroyImmediate(confessionTarget);
                Object.DestroyImmediate(defeatedRoot);
            }
        }

        [Test]
        public void AttackController_UsesInjectedClockForTelegraphAndLaunch()
        {
            var owner = new GameObject("Boss Attack Owner");
            var playerObject = new GameObject(
                "Boss Attack Player",
                typeof(CharacterController));
            var cameraObject = new GameObject("Boss Attack Camera", typeof(Camera));
            var shooterObject = new GameObject("Rose-Thorn Shooter");
            var muzzleObject = new GameObject("Muzzle");
            var mirrorObject = new GameObject("Living Portrait");
            GameObject hitVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                playerObject.SetActive(false);
                cameraObject.transform.SetParent(playerObject.transform, false);
                shooterObject.transform.SetParent(cameraObject.transform, false);
                muzzleObject.transform.SetParent(shooterObject.transform, false);
                shooterObject.SetActive(false);
                FirstPersonController player = playerObject.AddComponent<FirstPersonController>();
                Camera camera = cameraObject.GetComponent<Camera>();
                mirrorObject.transform.position = new Vector3(0f, 0f, 8f);
                hitVolume.name = "Mirror Hit Volume";
                hitVolume.transform.SetParent(mirrorObject.transform, false);
                Collider collider = hitVolume.GetComponent<Collider>();

                var arena = new BossArenaView(
                    owner.transform,
                    mirrorObject.transform,
                    new[] { collider },
                    shooterObject,
                    muzzleObject.transform,
                    null,
                    null,
                    null,
                    null);
                Assert.That(arena.TryPrepare(player, camera, out string error), Is.True, error);
                var combat = new BossCombatState(4, 2, 1, 1);
                arena.ResetEncounter(combat, restorePlayerPose: false);
                var clock = new FakeSequenceClock();
                var input = new FakeSequenceInput();
                var attacks = new BossAttackController(
                    combat,
                    arena,
                    player,
                    camera,
                    clock,
                    input,
                    new BossAttackSettings(
                        mirrorAttackDamage: 1,
                        firstAttackDelay: 0.5f,
                        attackInterval: 1f,
                        attackTelegraphSeconds: 0.2f,
                        attackProjectileSpeed: 2f,
                        attackHitRadius: 0.1f,
                        shotRange: 30f,
                        shotVisualSeconds: 0.1f,
                        shotMask: ~0,
                        enemyHitSound: null,
                        enemyHitSoundVolume: 1f,
                        playerHitSound: null));

                attacks.Reset();
                Physics.SyncTransforms();
                BossShotResult directHit = attacks.FirePlayerShot();
                Assert.That(directHit.HasFlag(BossShotResult.HitMirror), Is.True);
                Assert.That(combat.MirrorHealth, Is.EqualTo(1));

                clock.Time = 0.49f;
                attacks.Tick();
                Assert.That(attacks.TelegraphActive, Is.False);

                clock.Time = 0.5f;
                attacks.Tick();
                Assert.That(attacks.TelegraphActive, Is.True);

                clock.Time = 0.7f;
                attacks.Tick();
                Assert.That(attacks.TelegraphActive, Is.False);
                Assert.That(attacks.PendingProjectileCount, Is.EqualTo(1));

                attacks.Stop();
                Assert.That(attacks.PendingProjectileCount, Is.Zero);
                arena.Cleanup(resetArena: true);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(mirrorObject);
            }
        }

        [Test]
        public void BossFacade_RequestsNarrativeInputWithoutLockingMovement()
        {
            var gameObject = new GameObject("Boss Sequence Facade");
            gameObject.SetActive(false);
            try
            {
                BossSequencePlayer player = gameObject.AddComponent<BossSequencePlayer>();

                Assert.That(player.SequenceId, Is.EqualTo(BossSequencePlayer.VerdantMirrorSequenceId));
                Assert.That(player.RequiredLocks, Is.EqualTo(SequenceLockFlags.NarrativeInput));
                Assert.That(player.TryGetMirrorTargetPosition(out _), Is.False);
                Assert.That(BossEndingContinuation.Supports("confession"), Is.True);
                Assert.That(BossEndingContinuation.Supports("sacrifice"), Is.True);
                Assert.That(BossEndingContinuation.Supports("denial"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BossFacade_UnsupportedEndingFailsHandleAndReleasesItsInputLock()
        {
            Assert.That(GameSession.Instance, Is.Null, "A previous test left a GameSession alive.");
            var sessionObject = new GameObject("Boss Playback Session");
            var bossObject = new GameObject("Boss Playback Facade");
            bossObject.SetActive(false);
            try
            {
                GameSession session = sessionObject.AddComponent<GameSession>();
                BossSequencePlayer boss = bossObject.AddComponent<BossSequencePlayer>();
                var playback = new SequencePlaybackHandle(
                    session,
                    BossSequencePlayer.VerdantMirrorSequenceId,
                    "denial",
                    boss.RequiredLocks,
                    new FakeSequenceClock(),
                    new FakeSequenceInput());
                playback.Begin();

                IEnumerator routine = boss.Play(playback);
                while (routine.MoveNext())
                {
                }

                Assert.That(playback.Status, Is.EqualTo(SequencePlaybackStatus.Failed));
                Assert.That(playback.Error, Does.Contain("Confession or Sacrifice"));
                Assert.That(session.IsNarrativeInputCaptured, Is.False);
                Assert.That(session.IsPlayerLocked, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(bossObject);
                Object.DestroyImmediate(sessionObject);
            }
        }

        private sealed class FakeSequenceClock : ISequenceClock
        {
            public float Time { get; set; }
            public float DeltaTime { get; set; }
            public float UnscaledTime => Time;
            public float UnscaledDeltaTime => DeltaTime;
        }

        private sealed class FakeSequenceInput : ISequenceInput
        {
            public bool PointerIsCaptured => true;

            public bool WasPressed(SequenceInputAction action) => false;
        }
    }
}