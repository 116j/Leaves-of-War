using System;
using System.Collections;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Continues from a defeated Verdant Mirror into the exact authored ending
    /// interaction without writing persistent narrative state.
    /// </summary>
    public sealed class BossEndingContinuation
    {
        public const string ConfessionEndingId = "confession";
        public const string SacrificeEndingId = "sacrifice";

        private readonly BossArenaView arena;
        private readonly IDiegeticHud hud;

        public BossEndingContinuation(BossArenaView arena, IDiegeticHud hud)
        {
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.hud = hud;
        }

        public static bool Supports(string endingId) =>
            string.Equals(endingId, ConfessionEndingId, StringComparison.Ordinal) ||
            string.Equals(endingId, SacrificeEndingId, StringComparison.Ordinal);

        public IEnumerator ContinueAfterVictory(
            string endingId,
            SequencePlaybackHandle playback)
        {
            if (playback == null)
                yield break;

            if (string.Equals(endingId, ConfessionEndingId, StringComparison.Ordinal))
            {
                arena.SetConfessionTarget(true);
                hud?.ShowStatus("VERDANT MIRROR DEFEATED\nUSE POISON ON THE ROOTS");
                yield break;
            }

            if (!string.Equals(endingId, SacrificeEndingId, StringComparison.Ordinal))
            {
                playback.Fail(
                    $"The Verdant Mirror encounter cannot continue unsupported ending " +
                    $"'{endingId ?? "<none>"}'.");
                yield break;
            }

            // The player must regain normal world interaction to focus and use
            // the local thorn, while movement remains authored as unlocked.
            playback.ReleaseNarrativeInput();
            SacrificeThornInteractable thorn = arena.ArmSacrificeThorn();
            if (thorn == null)
            {
                playback.Fail("The Sacrifice ending could not arm its thorn interaction.");
                yield break;
            }

            hud?.SetGameplayHud(
                "VERDANT MIRROR DEFEATED\nAPPROACH THE THORN — COMMIT SACRIFICE");
            while (!playback.IsCancellationRequested && !thorn.IsCommitted)
                yield return null;

            if (playback.IsCancellationRequested)
                yield break;

            arena.SetDefeatedMirror(true);
        }
    }
}
