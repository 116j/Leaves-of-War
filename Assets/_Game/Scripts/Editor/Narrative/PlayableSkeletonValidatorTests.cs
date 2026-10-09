using System;
using System.Collections.Generic;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEditor;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class PlayableSkeletonValidatorTests
    {
        [Test]
        public void AuthoredProject_PassesStrictPlayableSkeletonValidation()
        {
            NarrativeCatalog catalog = AssetDatabase.LoadAssetAtPath<NarrativeCatalog>(
                "Assets/Resources/Narrative/NarrativeCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                PlayableSkeletonValidator.TryValidate(catalog, out string error),
                Is.True,
                error);
        }

        [Test]
        public void TryValidate_NullCatalogUsesSharedCatalogDiagnosticWithoutThrowing()
        {
            bool valid = PlayableSkeletonValidator.TryValidate(null, out string error);

            Assert.That(valid, Is.False);
            Assert.That(
                error,
                Does.Contain("Catalog: Narrative catalog validation requires a NarrativeCatalog."));
        }

        [Test]
        public void SequenceBinding_RejectsMissingReachablePlayer()
        {
            List<string> problems = ValidateSequence(
                "dream_2_flood",
                false,
                Array.Empty<SkeletonSequencePlayerFact>());

            Assert.That(Join(problems), Does.Contain("no reachable ISequencePlayer"));
        }

        [Test]
        public void SequenceBinding_RejectsDuplicateReachablePlayers()
        {
            var players = new[]
            {
                Player("dream_2_flood", "Sequences/One"),
                Player("dream_2_flood", "Sequences/Two")
            };

            List<string> problems = ValidateSequence("dream_2_flood", false, players);

            Assert.That(Join(problems), Does.Contain("2 reachable players"));
        }

        [Test]
        public void SequenceBinding_RejectsBossMovementLock()
        {
            SkeletonSequencePlayerFact player = Player("boss_verdant_mirror", "Boss");
            player.IsBossPlayer = true;
            List<string> problems = ValidateSequence(
                "boss_verdant_mirror",
                true,
                new[] { player });

            Assert.That(Join(problems), Does.Contain("still locks player movement"));
        }

        [Test]
        public void SequenceBinding_RejectsGenericBossHook()
        {
            SkeletonSequencePlayerFact player = Player("boss_verdant_mirror", "Generic Boss");
            player.IsGenericGreybox = true;

            List<string> problems = ValidateSequence(
                "boss_verdant_mirror",
                false,
                new[] { player });

            Assert.That(Join(problems), Does.Contain("not handled by a BossSequencePlayer"));
        }

        [Test]
        public void SequenceBinding_RejectsIncompleteBossProductionConfiguration()
        {
            SkeletonSequencePlayerFact player = Player("boss_verdant_mirror", "Boss");
            player.IsBossPlayer = true;
            player.BossConfigurationError =
                "has no complete rose-thorn shooter view and muzzle";

            List<string> problems = ValidateSequence(
                "boss_verdant_mirror",
                false,
                new[] { player });

            Assert.That(
                Join(problems),
                Does.Contain("has no complete rose-thorn shooter view and muzzle"));
        }

        [TestCase("credits_confession")]
        [TestCase("credits_denial")]
        [TestCase("credits_sacrifice")]
        public void SequenceBinding_RejectsGenericCreditsHook(string sequenceId)
        {
            SkeletonSequencePlayerFact player = Player(sequenceId, "Generic Credits");
            player.IsGenericGreybox = true;

            List<string> problems = ValidateSequence(sequenceId, true, new[] { player });

            Assert.That(Join(problems), Does.Contain("generic GreyboxSequencePlayer hook"));
        }

        [Test]
        public void SequenceBinding_AcceptsDedicatedCreditsPlayer()
        {
            SkeletonSequencePlayerFact player = Player(
                "credits_confession",
                "Confession Credits");
            player.IsCreditsPlayer = true;

            List<string> problems = ValidateSequence(
                "credits_confession",
                true,
                new[] { player });

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void TaskChapter_RejectsReceiverWithoutAcceptedCategory()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Receivers[0].CategoryKey = string.Empty;

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("has no accepted carry category"));
        }

        [Test]
        public void TaskChapter_RejectsReceiverWithoutObjective()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Receivers[0].ObjectiveKey = string.Empty;

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("has no task objective"));
        }

        [Test]
        public void TaskChapter_RejectsReceiverWithUncataloguedObjective()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Receivers[0].ObjectiveCatalogued = false;

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("references uncatalogued objective 'unpacking'"));
        }

        [Test]
        public void TaskChapter_RejectsReceiverWithoutUsableCollider()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Receivers[0].HasUsableCollider = false;

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("no enabled, non-trigger collider"));
        }

        [Test]
        public void TaskChapter_ReportsAllReceiverProblemsInOnePass()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            SkeletonTaskReceiverFact receiver = fact.Receivers[0];
            receiver.CategoryKey = string.Empty;
            receiver.ObjectiveKey = string.Empty;
            receiver.HasUsableCollider = false;

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("has no accepted carry category"));
            Assert.That(error, Does.Contain("has no task objective"));
            Assert.That(error, Does.Contain("no enabled, non-trigger collider"));
        }

        [Test]
        public void TaskChapter_RejectsReceiverWithoutCompatibleSource()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Sources.Clear();

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("0 eligible source(s) for 1 receiver(s)"));
        }

        [Test]
        public void TaskChapter_RejectsTooFewNonRetainedSources()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Receivers.Add(Receiver("Receiver Two"));

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("1 eligible source(s) for 2 receiver(s)"));
        }

        [Test]
        public void TaskChapter_RejectsRetainedToolWithoutSource()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Receivers[0].RetainedAfterUse = true;
            fact.Sources.Clear();

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("retained tool category 'books' has no"));
        }

        [Test]
        public void TaskChapter_RejectsRequiresCarriedInteractionWithoutSource()
        {
            var fact = new SkeletonTaskChapterFact
            {
                SceneName = "Manor",
                ChapterIndex = 5
            };
            fact.RequiresCarried.Add(new SkeletonRequiresCarriedFact
            {
                Owner = "Journal Destination",
                CategoryKey = "journal",
                CategoryName = "journal"
            });

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("requiresCarried interaction"));
            Assert.That(error, Does.Contain("no eligible 'journal' source"));
        }

        [Test]
        public void TaskChapter_RejectsUncataloguedListenerObjective()
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Listeners.Add(new SkeletonTaskListenerFact
            {
                Owner = "Progressive Reveal",
                ObjectiveKey = "foreign",
                ObjectiveName = "foreign",
                ObjectiveCatalogued = false,
                Threshold = 1
            });

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain("references uncatalogued objective 'foreign'"));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void TaskChapter_RejectsInvalidListenerThreshold(int threshold)
        {
            SkeletonTaskChapterFact fact = ValidTaskChapter();
            fact.Listeners.Add(new SkeletonTaskListenerFact
            {
                Owner = "Progressive Reveal",
                ObjectiveKey = "unpacking",
                ObjectiveName = "unpacking",
                ObjectiveCatalogued = true,
                Threshold = threshold
            });

            string error = ValidateTasks(fact);

            Assert.That(error, Does.Contain($"invalid threshold {threshold}"));
        }

        [TestCase(GameplaySceneFault.MissingScript, "missing MonoBehaviour script")]
        [TestCase(GameplaySceneFault.DuplicatePlayer, "2 players; exactly one")]
        [TestCase(GameplaySceneFault.DuplicateHolder, "2 CarriedItemHolder")]
        [TestCase(GameplaySceneFault.MissingPresenter, "0 NarrativePresenter")]
        [TestCase(GameplaySceneFault.BlankSpawn, "has a blank id")]
        [TestCase(GameplaySceneFault.DuplicateSpawn, "duplicate spawn id 'front_gate'")]
        [TestCase(GameplaySceneFault.BrokenRetroOutput, "broken retro-output contract")]
        public void GameplayScene_RejectsStrictContractFaults(
            GameplaySceneFault fault,
            string expectedProblem)
        {
            SkeletonGameplaySceneFact fact = ValidGameplayScene();
            ApplySceneFault(fact, fault);
            var problems = new List<string>();

            PlayableSkeletonValidationRules.ValidateGameplayScene(fact, problems);

            Assert.That(Join(problems), Does.Contain(expectedProblem));
        }

        [Test]
        public void CanonicalFlow_AcceptsExactThreeBranches()
        {
            var problems = new List<string>();

            PlayableSkeletonValidationRules.ValidateCanonicalFlow(
                CanonicalFlow(),
                problems);

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void CanonicalFlow_RejectsChoiceBeforeFinalChapterBeat()
        {
            SkeletonCanonicalFlowFact fact = CanonicalFlow();
            fact.FinalChoiceIsLast = false;
            var problems = new List<string>();

            PlayableSkeletonValidationRules.ValidateCanonicalFlow(fact, problems);

            Assert.That(Join(problems), Does.Contain("ChoiceBeat must be the final chapter beat"));
        }

        [Test]
        public void CanonicalFlow_RejectsReorderedChoiceOptions()
        {
            SkeletonCanonicalFlowFact fact = CanonicalFlow();
            string first = fact.FinalChoiceEndingIds[0];
            fact.FinalChoiceEndingIds[0] = fact.FinalChoiceEndingIds[1];
            fact.FinalChoiceEndingIds[1] = first;
            var problems = new List<string>();

            PlayableSkeletonValidationRules.ValidateCanonicalFlow(fact, problems);

            Assert.That(Join(problems), Does.Contain("choice ending ids"));
            Assert.That(Join(problems), Does.Contain("in that order"));
        }

        [Test]
        public void CanonicalFlow_RejectsEndingSequenceOrderChange()
        {
            SkeletonCanonicalFlowFact fact = CanonicalFlow();
            SkeletonEndingFlowFact confession = fact.Endings[0];
            SkeletonSequencePosition first = confession.Sequences[0];
            confession.Sequences[0] = confession.Sequences[1];
            confession.Sequences[1] = first;
            var problems = new List<string>();

            PlayableSkeletonValidationRules.ValidateCanonicalFlow(fact, problems);

            Assert.That(Join(problems), Does.Contain("sequence order/positions differ"));
        }

        private static List<string> ValidateSequence(
            string sequenceId,
            bool locksPlayer,
            IReadOnlyList<SkeletonSequencePlayerFact> players)
        {
            var problems = new List<string>();
            PlayableSkeletonValidationRules.ValidateSequenceBinding(
                "Test beat",
                "Manor",
                sequenceId,
                locksPlayer,
                players,
                problems);
            return problems;
        }

        private static string ValidateTasks(SkeletonTaskChapterFact fact)
        {
            var problems = new List<string>();
            PlayableSkeletonValidationRules.ValidateTaskChapter(fact, problems);
            return Join(problems);
        }

        private static SkeletonSequencePlayerFact Player(string id, string owner) =>
            new SkeletonSequencePlayerFact
            {
                SequenceId = id,
                Owner = owner
            };

        private static SkeletonTaskChapterFact ValidTaskChapter()
        {
            var fact = new SkeletonTaskChapterFact
            {
                SceneName = "Manor",
                ChapterIndex = 1
            };
            fact.Receivers.Add(Receiver("Receiver One"));
            fact.Sources.Add(new SkeletonCarrySourceFact
            {
                Owner = "Book Source",
                CategoryKey = "books",
                CategoryName = "books"
            });
            return fact;
        }

        private static SkeletonTaskReceiverFact Receiver(string owner) =>
            new SkeletonTaskReceiverFact
            {
                Owner = owner,
                CategoryKey = "books",
                CategoryName = "books",
                ObjectiveKey = "unpacking",
                ObjectiveName = "unpacking",
                ObjectiveCatalogued = true,
                HasUsableCollider = true
            };

        private static SkeletonGameplaySceneFact ValidGameplayScene()
        {
            var fact = new SkeletonGameplaySceneFact
            {
                SceneName = "Manor"
            };
            fact.Spawns.Add(new SkeletonSpawnFact
            {
                Id = "front_gate",
                Owner = "Spawns/Front Gate"
            });
            return fact;
        }

        private static void ApplySceneFault(
            SkeletonGameplaySceneFact fact,
            GameplaySceneFault fault)
        {
            switch (fault)
            {
                case GameplaySceneFault.MissingScript:
                    fact.MissingScriptCount = 1;
                    break;
                case GameplaySceneFault.DuplicatePlayer:
                    fact.PlayerCount = 2;
                    break;
                case GameplaySceneFault.DuplicateHolder:
                    fact.HolderCount = 2;
                    break;
                case GameplaySceneFault.MissingPresenter:
                    fact.PresenterCount = 0;
                    break;
                case GameplaySceneFault.BlankSpawn:
                    fact.Spawns[0].Id = " ";
                    break;
                case GameplaySceneFault.DuplicateSpawn:
                    fact.Spawns.Add(new SkeletonSpawnFact
                    {
                        Id = "front_gate",
                        Owner = "Spawns/Duplicate"
                    });
                    break;
                case GameplaySceneFault.BrokenRetroOutput:
                    fact.RetroProblems.Add("camera target is missing");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
            }
        }

        private static SkeletonCanonicalFlowFact CanonicalFlow()
        {
            var fact = new SkeletonCanonicalFlowFact
            {
                ChoiceCount = 1,
                FinalChoiceIsLast = true
            };
            fact.FinalChoiceEndingIds.AddRange(
                new[] { "confession", "denial", "sacrifice" });
            fact.FinalChoiceLabels.AddRange(
                new[] { "CONFESSION", "DENIAL", "SACRIFICE" });

            fact.Endings.Add(Ending(
                "confession",
                7,
                new SkeletonSequencePosition("boss_verdant_mirror", 0),
                new SkeletonSequencePosition("blooms_brown_and_fall", 3),
                new SkeletonSequencePosition("credits_confession", 6)));
            fact.Endings.Add(Ending(
                "denial",
                4,
                new SkeletonSequencePosition("walks_into_the_painting", 2),
                new SkeletonSequencePosition("credits_denial", 3)));
            fact.Endings.Add(Ending(
                "sacrifice",
                5,
                new SkeletonSequencePosition("boss_verdant_mirror", 0),
                new SkeletonSequencePosition("credits_sacrifice", 4)));
            return fact;
        }

        private static SkeletonEndingFlowFact Ending(
            string id,
            int beatCount,
            params SkeletonSequencePosition[] sequences)
        {
            var fact = new SkeletonEndingFlowFact
            {
                EndingId = id,
                BeatCount = beatCount
            };
            fact.Sequences.AddRange(sequences);
            return fact;
        }

        private static string Join(IReadOnlyList<string> problems) =>
            string.Join("\n", problems);

        public enum GameplaySceneFault
        {
            MissingScript,
            DuplicatePlayer,
            DuplicateHolder,
            MissingPresenter,
            BlankSpawn,
            DuplicateSpawn,
            BrokenRetroOutput
        }
    }
}
