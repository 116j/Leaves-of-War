using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Owns the Unity-facing presentation and reset rules for the production
    /// Verdant Mirror arena. Combat decisions and timing live elsewhere.
    /// </summary>
    public sealed class BossArenaView
    {
        private readonly Transform runtimeParent;
        private readonly Transform mirrorRoot;
        private readonly Collider[] configuredMirrorHitColliders;
        private readonly GameObject thornShooterView;
        private readonly Transform thornShooterMuzzle;
        private readonly GameObject confessionPoisonTarget;
        private readonly GameObject defeatedMirrorRoot;
        private readonly Material thornMaterial;
        private readonly Material dangerMaterial;
        private readonly float shooterRecoilDistance;
        private readonly float shooterRecoilDegrees;
        private readonly List<HitColliderState> mirrorHitColliders = new List<HitColliderState>();
        private readonly List<GameObject> attackProjectiles = new List<GameObject>();
        private readonly List<TimedVisual> shotVisuals = new List<TimedVisual>();

        private global::FirstPersonController player;
        private Camera playerCamera;
        private GameObject telegraphVisual;
        private GameObject sacrificeThornObject;
        private SacrificeThornInteractable sacrificeThorn;
        private Vector3 initialPlayerPosition;
        private Quaternion initialPlayerRotation;
        private bool capturedPlayerPose;
        private bool capturedMirrorState;
        private bool mirrorWasActive;
        private bool capturedShooterState;
        private bool shooterWasActive;
        private Vector3 shooterRestLocalPosition;
        private Quaternion shooterRestLocalRotation;
        private float shooterKickEndsAt;

        public BossArenaView(
            Transform runtimeParent,
            Transform mirrorRoot,
            Collider[] mirrorHitColliders,
            GameObject thornShooterView,
            Transform thornShooterMuzzle,
            GameObject confessionPoisonTarget,
            GameObject defeatedMirrorRoot,
            Material thornMaterial,
            Material dangerMaterial,
            float shooterRecoilDistance = 0.055f,
            float shooterRecoilDegrees = 5f)
        {
            this.runtimeParent = runtimeParent;
            this.mirrorRoot = mirrorRoot;
            configuredMirrorHitColliders = mirrorHitColliders ?? System.Array.Empty<Collider>();
            this.thornShooterView = thornShooterView;
            this.thornShooterMuzzle = thornShooterMuzzle;
            this.confessionPoisonTarget = confessionPoisonTarget;
            this.defeatedMirrorRoot = defeatedMirrorRoot;
            this.thornMaterial = thornMaterial;
            this.dangerMaterial = dangerMaterial;
            this.shooterRecoilDistance = Mathf.Max(0f, shooterRecoilDistance);
            this.shooterRecoilDegrees = Mathf.Max(0f, shooterRecoilDegrees);
        }

        public global::FirstPersonController Player => player;
        public Camera PlayerCamera => playerCamera;
        public Vector3 PlayerShotOrigin =>
            thornShooterMuzzle != null && thornShooterView != null &&
            thornShooterView.activeSelf
                ? thornShooterMuzzle.position
                : playerCamera != null
                    ? playerCamera.transform.position
                    : runtimeParent != null
                        ? runtimeParent.position
                        : Vector3.zero;
        public Vector3 AttackOrigin => mirrorRoot != null
            ? mirrorRoot.position + Vector3.up * 0.35f
            : runtimeParent != null
                ? runtimeParent.position
                : Vector3.zero;

        public bool TryPrepare(
            global::FirstPersonController encounterPlayer,
            Camera encounterCamera,
            out string error)
        {
            RestoreMirrorHitColliders();
            RestoreShooterPresentation();
            ClearTransientCombatObjects();
            DestroySacrificeThorn();

            player = encounterPlayer;
            playerCamera = encounterCamera;
            if (player == null || playerCamera == null)
            {
                error = "The Verdant Mirror encounter requires one registered player and camera.";
                return false;
            }

            if (mirrorRoot == null)
            {
                error = "The Verdant Mirror encounter has no living Portrait root.";
                return false;
            }

            if (thornShooterView == null || thornShooterMuzzle == null ||
                !thornShooterMuzzle.IsChildOf(thornShooterView.transform))
            {
                error = "The Verdant Mirror encounter has no complete rose-thorn shooter view.";
                return false;
            }

            if (!thornShooterView.transform.IsChildOf(playerCamera.transform))
            {
                error = "The rose-thorn shooter view is not parented beneath the registered player camera.";
                return false;
            }

            capturedShooterState = true;
            shooterWasActive = thornShooterView.activeSelf;
            shooterRestLocalPosition = thornShooterView.transform.localPosition;
            shooterRestLocalRotation = thornShooterView.transform.localRotation;
            SetShooterVisible(true);

            capturedMirrorState = true;
            mirrorWasActive = mirrorRoot.gameObject.activeSelf;
            SetLivingMirror(true);

            capturedPlayerPose = true;
            initialPlayerPosition = player.transform.position;
            initialPlayerRotation = player.transform.rotation;

            PrepareMirrorHitColliders();
            if (mirrorHitColliders.Count == 0)
            {
                error = "The Verdant Mirror encounter has no usable Mirror hit colliders.";
                return false;
            }

            SetConfessionTarget(false);
            SetDefeatedMirror(false);
            error = string.Empty;
            return true;
        }

        public bool IsMirrorHit(Collider candidate)
        {
            if (candidate == null)
                return false;

            for (int i = 0; i < mirrorHitColliders.Count; i++)
            {
                Collider collider = mirrorHitColliders[i].Collider;
                if (ReferenceEquals(collider, candidate) &&
                    collider.enabled &&
                    collider.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetMirrorTargetPosition(out Vector3 position)
        {
            if (mirrorRoot != null && mirrorRoot.gameObject.activeInHierarchy)
            {
                position = mirrorRoot.position + Vector3.up * 0.35f;
                return true;
            }

            position = default;
            return false;
        }

        public void RefreshMirrorState(BossCombatState combat)
        {
            if (combat == null)
                return;

            SetLivingMirror(!combat.MirrorIsDefeated);
        }

        public void ResetEncounter(BossCombatState combat, bool restorePlayerPose)
        {
            ClearTransientCombatObjects();
            SetShooterVisible(true);
            SetConfessionTarget(false);
            DisarmSacrificeThorn();
            SetDefeatedMirror(false);
            SetLivingMirror(true);

            if (restorePlayerPose)
                RestorePlayerPose();

            RefreshMirrorState(combat);
        }

        public void PrepareRetryPrompt()
        {
            ClearTransientCombatObjects();
            SetShooterVisible(false);
            SetConfessionTarget(false);
            DisarmSacrificeThorn();
            SetDefeatedMirror(false);
            SetLivingMirror(true);
        }

        public void ShowAttackTelegraph(Vector3 direction, float range)
        {
            ClearAttackTelegraph();
            Vector3 origin = AttackOrigin;
            telegraphVisual = CreateLinePrimitive(
                "Mirror Attack Telegraph",
                origin,
                origin + direction * Mathf.Min(range, 14f),
                0.07f,
                dangerMaterial);
        }

        public void ClearAttackTelegraph()
        {
            DestroyRuntimeObject(telegraphVisual);
            telegraphVisual = null;
        }

        public GameObject CreateAttackProjectile()
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Mirror Thorn Projectile";
            projectile.transform.position = AttackOrigin;
            projectile.transform.localScale = Vector3.one * 0.28f;
            ParentRuntimeObject(projectile);
            DisablePrimitiveCollider(projectile);
            SetMaterial(projectile, dangerMaterial);
            attackProjectiles.Add(projectile);
            return projectile;
        }

        public bool TryGetProjectilePosition(GameObject projectile, out Vector3 position)
        {
            if (projectile == null)
            {
                position = default;
                return false;
            }

            position = projectile.transform.position;
            return true;
        }

        public void SetProjectilePosition(GameObject projectile, Vector3 position)
        {
            if (projectile != null)
                projectile.transform.position = position;
        }

        public void DestroyAttackProjectile(GameObject projectile)
        {
            attackProjectiles.Remove(projectile);
            DestroyRuntimeObject(projectile);
        }

        public void CreatePlayerShot(Vector3 start, Vector3 end, float expiresAt)
        {
            GameObject visual = CreateLinePrimitive(
                "Rose-Thorn Shot",
                start,
                end,
                0.025f,
                thornMaterial);
            shotVisuals.Add(new TimedVisual(visual, expiresAt));
            KickShooter(expiresAt);
        }

        public void UpdatePlayerShots(float now)
        {
            for (int i = shotVisuals.Count - 1; i >= 0; i--)
            {
                TimedVisual shot = shotVisuals[i];
                if (shot.Visual != null && now < shot.ExpiresAt)
                    continue;

                DestroyRuntimeObject(shot.Visual);
                shotVisuals.RemoveAt(i);
            }

            if (shooterKickEndsAt > 0f && now >= shooterKickEndsAt)
                ResetShooterPose();
        }

        public void SetShooterVisible(bool visible)
        {
            if (thornShooterView == null)
                return;

            if (!visible)
                ResetShooterPose();
            thornShooterView.SetActive(visible);
        }

        public SacrificeThornInteractable ArmSacrificeThorn()
        {
            if (player == null || playerCamera == null)
                return null;

            if (sacrificeThornObject == null)
            {
                sacrificeThornObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sacrificeThornObject.name = "Sacrifice Thorn — Transient";
                sacrificeThornObject.transform.localScale = new Vector3(0.08f, 0.85f, 0.08f);
                sacrificeThornObject.transform.rotation = Quaternion.Euler(90f, 0f, 18f);
                ParentRuntimeObject(sacrificeThornObject);
                SetMaterial(sacrificeThornObject, thornMaterial);
                sacrificeThorn = sacrificeThornObject.AddComponent<SacrificeThornInteractable>();
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                playerCamera.transform.forward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f)
                forward = player.transform.forward;

            sacrificeThornObject.transform.position =
                playerCamera.transform.position + forward * 1.6f - Vector3.up * 0.45f;
            sacrificeThorn.Arm();
            return sacrificeThorn;
        }

        public void DisarmSacrificeThorn()
        {
            if (sacrificeThorn != null)
                sacrificeThorn.Disarm();
        }

        public void SetConfessionTarget(bool visible)
        {
            if (confessionPoisonTarget != null)
                confessionPoisonTarget.SetActive(visible);
        }

        public void SetDefeatedMirror(bool visible)
        {
            if (defeatedMirrorRoot != null)
                defeatedMirrorRoot.SetActive(visible);
        }

        public void Cleanup(bool resetArena)
        {
            ClearTransientCombatObjects();
            DestroySacrificeThorn();
            RestoreShooterPresentation();

            if (resetArena)
            {
                SetConfessionTarget(false);
                SetDefeatedMirror(false);
                RestoreMirrorHitColliders();
            }

            player = null;
            playerCamera = null;
            capturedPlayerPose = false;
        }

        public void ClearTransientCombatObjects()
        {
            ClearAttackTelegraph();
            ResetShooterPose();

            for (int i = 0; i < attackProjectiles.Count; i++)
                DestroyRuntimeObject(attackProjectiles[i]);
            attackProjectiles.Clear();

            for (int i = 0; i < shotVisuals.Count; i++)
                DestroyRuntimeObject(shotVisuals[i].Visual);
            shotVisuals.Clear();
        }

        private void KickShooter(float expiresAt)
        {
            if (!capturedShooterState || thornShooterView == null)
                return;

            thornShooterView.transform.localPosition =
                shooterRestLocalPosition +
                shooterRestLocalRotation * Vector3.back * shooterRecoilDistance;
            thornShooterView.transform.localRotation =
                shooterRestLocalRotation * Quaternion.Euler(-shooterRecoilDegrees, 0f, 0f);
            shooterKickEndsAt = expiresAt;
        }

        private void ResetShooterPose()
        {
            if (!capturedShooterState || thornShooterView == null)
                return;

            thornShooterView.transform.localPosition = shooterRestLocalPosition;
            thornShooterView.transform.localRotation = shooterRestLocalRotation;
            shooterKickEndsAt = 0f;
        }

        private void RestoreShooterPresentation()
        {
            if (!capturedShooterState || thornShooterView == null)
                return;

            ResetShooterPose();
            thornShooterView.SetActive(shooterWasActive);
            capturedShooterState = false;
        }

        private void PrepareMirrorHitColliders()
        {
            mirrorHitColliders.Clear();
            for (int i = 0; i < configuredMirrorHitColliders.Length; i++)
            {
                Collider candidate = configuredMirrorHitColliders[i];
                if (candidate != null &&
                    candidate.gameObject.scene == mirrorRoot.gameObject.scene)
                {
                    bool colliderWasEnabled = candidate.enabled;
                    candidate.enabled = true;
                    mirrorHitColliders.Add(
                        new HitColliderState(candidate, colliderWasEnabled));
                }
            }
        }

        private void RestorePlayerPose()
        {
            if (!capturedPlayerPose || player == null)
                return;

            CharacterController character = player.GetComponent<CharacterController>();
            bool wasEnabled = character != null && character.enabled;
            if (character != null)
                character.enabled = false;

            player.transform.SetPositionAndRotation(initialPlayerPosition, initialPlayerRotation);

            if (character != null)
                character.enabled = wasEnabled;
        }

        private void RestoreMirrorHitColliders()
        {
            for (int i = 0; i < mirrorHitColliders.Count; i++)
            {
                HitColliderState hitCollider = mirrorHitColliders[i];
                if (hitCollider.Collider != null)
                    hitCollider.Collider.enabled = hitCollider.ColliderWasEnabled;
            }

            mirrorHitColliders.Clear();
            if (capturedMirrorState && mirrorRoot != null)
                mirrorRoot.gameObject.SetActive(mirrorWasActive);
            capturedMirrorState = false;
        }

        private void SetLivingMirror(bool visible)
        {
            if (mirrorRoot != null)
                mirrorRoot.gameObject.SetActive(visible);
        }

        private void DestroySacrificeThorn()
        {
            DestroyRuntimeObject(sacrificeThornObject);
            sacrificeThornObject = null;
            sacrificeThorn = null;
        }

        private GameObject CreateLinePrimitive(
            string objectName,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            ParentRuntimeObject(visual);
            DisablePrimitiveCollider(visual);
            Vector3 delta = end - start;
            visual.transform.position = (start + end) * 0.5f;
            visual.transform.rotation = delta.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(delta.normalized)
                : Quaternion.identity;
            visual.transform.localScale = new Vector3(thickness, thickness, delta.magnitude);
            SetMaterial(visual, material);
            return visual;
        }

        private void ParentRuntimeObject(GameObject target)
        {
            if (target != null && runtimeParent != null)
                target.transform.SetParent(runtimeParent, true);
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            if (target != null && material != null && target.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;
        }

        private static void DisablePrimitiveCollider(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out Collider collider))
                return;

            collider.enabled = false;
            DestroyRuntimeObject(collider);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private readonly struct HitColliderState
        {
            public HitColliderState(Collider collider, bool colliderWasEnabled)
            {
                Collider = collider;
                ColliderWasEnabled = colliderWasEnabled;
            }

            public Collider Collider { get; }
            public bool ColliderWasEnabled { get; }
        }

        private readonly struct TimedVisual
        {
            public TimedVisual(GameObject visual, float expiresAt)
            {
                Visual = visual;
                ExpiresAt = expiresAt;
            }

            public GameObject Visual { get; }
            public float ExpiresAt { get; }
        }
    }
}
