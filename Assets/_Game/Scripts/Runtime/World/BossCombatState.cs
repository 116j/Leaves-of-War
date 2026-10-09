using System;

namespace Hortensia.Runtime
{
    [Flags]
    public enum BossShotResult
    {
        None = 0,
        Fired = 1 << 0,
        HitMirror = 1 << 1,
        PlayerDied = 1 << 2,
        MirrorDefeated = 1 << 3
    }

    /// <summary>
    /// Deterministic, scene-independent health rules for the Verdant Mirror encounter.
    /// Narrative state is deliberately not referenced here or by the fight.
    /// </summary>
    public sealed class BossCombatState
    {
        public BossCombatState(
            int playerHealth,
            int mirrorHealth,
            int shotHealthCost,
            int shotDamage)
        {
            MaxPlayerHealth = Math.Max(1, playerHealth);
            MaxMirrorHealth = Math.Max(1, mirrorHealth);
            ShotHealthCost = Math.Max(1, shotHealthCost);
            ShotDamage = Math.Max(1, shotDamage);
            Reset();
        }

        public int MaxPlayerHealth { get; }
        public int MaxMirrorHealth { get; }
        public int ShotHealthCost { get; }
        public int ShotDamage { get; }
        public int PlayerHealth { get; private set; }
        public int MirrorHealth { get; private set; }
        public bool PlayerIsDead => PlayerHealth <= 0;
        public bool MirrorIsDefeated => MirrorHealth <= 0;

        public BossShotResult Fire(bool hitValidMirrorTarget)
        {
            if (PlayerIsDead || MirrorIsDefeated)
                return BossShotResult.None;

            PlayerHealth = Math.Max(0, PlayerHealth - ShotHealthCost);
            BossShotResult result = BossShotResult.Fired;

            if (hitValidMirrorTarget)
            {
                MirrorHealth = Math.Max(0, MirrorHealth - ShotDamage);
                result |= BossShotResult.HitMirror;
            }

            if (PlayerIsDead)
                result |= BossShotResult.PlayerDied;
            if (MirrorIsDefeated)
                result |= BossShotResult.MirrorDefeated;

            return result;
        }

        public bool ApplyMirrorAttack(int damage)
        {
            if (PlayerIsDead || MirrorIsDefeated || damage <= 0)
                return false;

            // The agent navigation overlay is an editor/development playtest
            // aid, not a player-facing accessibility mode. It cannot react to
            // a real-time boss attack reliably, so keep the encounter
            // explorable while F8 navigation is active. Firing still pays its
            // authored health cost, preserving the boss's objective and pacing.
            if (AgentNavigationOverlay.IsVisible)
                return false;

            PlayerHealth = Math.Max(0, PlayerHealth - damage);
            return PlayerIsDead;
        }

        public void Reset()
        {
            PlayerHealth = MaxPlayerHealth;
            MirrorHealth = MaxMirrorHealth;
        }
    }
}
