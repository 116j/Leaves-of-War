using System;
using System.Collections.Generic;
using Hortensia.Editor.Validation;
using Hortensia.Narrative;
using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Milestone-level validation for the asset-less playable skeleton. This is
/// deliberately editor-only: runtime keeps its warning-and-advance recovery
/// behavior for missing sequence players, while this pass rejects incomplete
/// scene wiring before a build is declared playable.
/// </summary>
public static class PlayableSkeletonValidator
{
    private const string MenuPath = "Tools/Hortensia/Narrative/Validate Playable Skeleton";

    [MenuItem(MenuPath)]
    private static void ValidateFromMenu()
    {
        if (!NarrativeCatalogLocator.TryLoadSingle(
                out NarrativeCatalog catalog,
                out string error) ||
            !TryValidate(catalog, out error))
        {
            Debug.LogError(error);
            EditorUtility.DisplayDialog(
                "Playable skeleton validation failed",
                error,
                "OK");
            return;
        }

        Debug.Log("Playable skeleton validation passed.");
    }

    [MenuItem(MenuPath, true)]
    private static bool CanValidateFromMenu() =>
        !EditorApplication.isPlayingOrWillChangePlaymode;

    /// <summary>
    /// Command-line entry point for -executeMethod. Throws on failure so a
    /// batch editor exits unsuccessfully.
    /// </summary>
    public static void Execute()
    {
        if (!NarrativeCatalogLocator.TryLoadSingle(
                out NarrativeCatalog catalog,
                out string error) ||
            !TryValidate(catalog, out error))
        {
            throw new InvalidOperationException(error);
        }

        Debug.Log("Playable skeleton validation passed.");
    }

    public static bool TryValidate(NarrativeCatalog catalog, out string error)
    {
        var report = new ValidationReport();
        ICollection<string> catalogProblems = report.WithPrefix("Catalog: ");
        int countBeforeStructure = report.Count;
        NarrativeCatalogValidator.CollectStructureProblems(catalog, catalogProblems);
        bool catalogStructureValid = report.Count == countBeforeStructure;

        if (catalog == null)
        {
            return report.TryFormat(
                "Playable skeleton validation failed:",
                true,
                out error);
        }

        PlayableSkeletonValidationRules.ValidateCanonicalFlow(
            BuildCanonicalFlow(catalog),
            report);

        ICollection<string> contextProblems = catalogStructureValid
            ? catalogProblems
            : report;
        using (var context = new ValidationContext(contextProblems))
        {
            // Catalog scene/content validation and strict scene validation share
            // the same additive scene cache. Structural catalog errors retain
            // the catalog validator's scene-inspection short circuit.
            if (catalogStructureValid)
            {
                NarrativeCatalogValidator.CollectContentProblems(
                    catalog,
                    context,
                    catalogProblems);
            }

            Dictionary<string, HashSet<int>> gameplaySceneChapters =
                BuildGameplaySceneChapterIndex(catalog);
            var scenes = new Dictionary<string, SceneContents>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<int>> pair in gameplaySceneChapters)
            {
                SceneContents scene = GetSceneContents(
                    pair.Key,
                    context,
                    scenes,
                    report);
                if (scene == null)
                    continue;

                var orderedChapterIndices = new List<int>(pair.Value);
                orderedChapterIndices.Sort();
                for (int chapterPosition = 0;
                     chapterPosition < orderedChapterIndices.Count;
                     chapterPosition++)
                {
                    int chapterIndex = orderedChapterIndices[chapterPosition];
                    PlayableSkeletonValidationRules.ValidateGameplayScene(
                        scene.BuildGameplaySceneFact(chapterIndex),
                        report);
                    PlayableSkeletonValidationRules.ValidateTaskChapter(
                        scene.BuildTaskChapterFact(chapterIndex, catalog),
                        report);
                }
            }

            ValidateAuthoredSequenceBindings(
                catalog,
                context,
                scenes,
                report);
        }

        return report.TryFormat(
            "Playable skeleton validation failed:",
            true,
            out error);
    }

    private static Dictionary<string, HashSet<int>> BuildGameplaySceneChapterIndex(
        NarrativeCatalog catalog)
    {
        var sceneChapters =
            new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        for (int chapterPosition = 0; chapterPosition < catalog.Chapters.Count; chapterPosition++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterPosition];
            if (chapter == null)
                continue;

            AddSceneChapter(sceneChapters, chapter.BaseScene, chapter.Index);
            for (int beatIndex = 0; beatIndex < chapter.Beats.Count; beatIndex++)
            {
                if (chapter.Beats[beatIndex] is TravelBeat travel)
                    AddSceneChapter(sceneChapters, travel.TargetScene, chapter.Index);
            }
        }

        ChapterDefinition finalChapter = catalog.FinalChapter;
        if (finalChapter == null)
            return sceneChapters;

        for (int endingIndex = 0; endingIndex < catalog.Endings.Count; endingIndex++)
        {
            EndingDefinition ending = catalog.Endings[endingIndex];
            if (ending == null)
                continue;

            AddSceneChapter(sceneChapters, finalChapter.BaseScene, finalChapter.Index);
            for (int beatIndex = 0; beatIndex < ending.Beats.Count; beatIndex++)
            {
                if (ending.Beats[beatIndex] is TravelBeat travel)
                    AddSceneChapter(sceneChapters, travel.TargetScene, finalChapter.Index);
            }
        }

        return sceneChapters;
    }

    private static void AddSceneChapter(
        IDictionary<string, HashSet<int>> sceneChapters,
        string sceneName,
        int chapterIndex)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || chapterIndex < 1 || chapterIndex > 7)
            return;

        if (!sceneChapters.TryGetValue(sceneName, out HashSet<int> chapters))
        {
            chapters = new HashSet<int>();
            sceneChapters.Add(sceneName, chapters);
        }

        chapters.Add(chapterIndex);
    }

    private static void ValidateAuthoredSequenceBindings(
        NarrativeCatalog catalog,
        ValidationContext validationContext,
        Dictionary<string, SceneContents> scenes,
        ICollection<string> problems)
    {
        for (int chapterPosition = 0; chapterPosition < catalog.Chapters.Count; chapterPosition++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterPosition];
            if (chapter == null)
                continue;

            string currentScene = chapter.BaseScene;
            for (int beatIndex = 0; beatIndex < chapter.Beats.Count; beatIndex++)
            {
                NarrativeBeat beat = chapter.Beats[beatIndex];
                if (beat is SequenceBeat sequence)
                {
                    ValidateSequenceBinding(
                        sequence,
                        chapter.Index,
                        currentScene,
                        $"Chapter {chapter.Index}, beat {beatIndex + 1}",
                        validationContext,
                        scenes,
                        problems);
                }

                if (beat is TravelBeat travel)
                    currentScene = travel.TargetScene;
            }
        }

        ChapterDefinition finalChapter = catalog.FinalChapter;
        if (finalChapter == null)
            return;

        for (int endingIndex = 0; endingIndex < catalog.Endings.Count; endingIndex++)
        {
            EndingDefinition ending = catalog.Endings[endingIndex];
            if (ending == null)
                continue;

            string currentScene = finalChapter.BaseScene;
            for (int beatIndex = 0; beatIndex < ending.Beats.Count; beatIndex++)
            {
                NarrativeBeat beat = ending.Beats[beatIndex];
                if (beat is SequenceBeat sequence)
                {
                    ValidateSequenceBinding(
                        sequence,
                        finalChapter.Index,
                        currentScene,
                        $"Ending '{ending.Title}', beat {beatIndex + 1}",
                        validationContext,
                        scenes,
                        problems);
                }

                if (beat is TravelBeat travel)
                    currentScene = travel.TargetScene;
            }
        }
    }

    private static void ValidateSequenceBinding(
        SequenceBeat sequence,
        int chapterIndex,
        string sceneName,
        string context,
        ValidationContext validationContext,
        Dictionary<string, SceneContents> scenes,
        ICollection<string> problems)
    {
        var players = new List<SkeletonSequencePlayerFact>();
        SceneContents scene = GetSceneContents(
            sceneName,
            validationContext,
            scenes,
            problems);
        if (scene != null)
        {
            scene.CollectReachableSequencePlayers(
                sequence.SequenceId,
                chapterIndex,
                players);
        }

        PlayableSkeletonValidationRules.ValidateSequenceBinding(
            context,
            sceneName,
            sequence.SequenceId,
            sequence.LocksPlayer,
            players,
            problems);
    }

    private static SceneContents GetSceneContents(
        string sceneName,
        ValidationContext validationContext,
        Dictionary<string, SceneContents> scenes,
        ICollection<string> problems)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return null;
        if (scenes.TryGetValue(sceneName, out SceneContents loaded))
            return loaded;

        if (!validationContext.Scenes.TryGet(
                sceneName,
                SceneDiagnosticKind.Gameplay,
                problems,
                out SceneSnapshot snapshot))
        {
            return null;
        }

        loaded = new SceneContents(snapshot);
        scenes.Add(sceneName, loaded);
        return loaded;
    }

    private static SkeletonCanonicalFlowFact BuildCanonicalFlow(NarrativeCatalog catalog)
    {
        var fact = new SkeletonCanonicalFlowFact();
        ChapterDefinition finalChapter = catalog.FinalChapter;

        for (int chapterPosition = 0; chapterPosition < catalog.Chapters.Count; chapterPosition++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterPosition];
            if (chapter == null)
                continue;

            for (int beatIndex = 0; beatIndex < chapter.Beats.Count; beatIndex++)
            {
                if (!(chapter.Beats[beatIndex] is ChoiceBeat choice))
                    continue;

                fact.ChoiceCount++;
                if (!ReferenceEquals(chapter, finalChapter))
                    continue;

                fact.FinalChoiceIsLast = beatIndex == chapter.Beats.Count - 1;
                for (int optionIndex = 0; optionIndex < choice.Options.Count; optionIndex++)
                {
                    ChoiceOption option = choice.Options[optionIndex];
                    fact.FinalChoiceEndingIds.Add(
                        option.Ending != null ? option.Ending.Id : string.Empty);
                    fact.FinalChoiceLabels.Add(option.Label ?? string.Empty);
                }
            }
        }

        for (int endingPosition = 0; endingPosition < catalog.Endings.Count; endingPosition++)
        {
            EndingDefinition ending = catalog.Endings[endingPosition];
            if (ending == null)
                continue;

            var endingFact = new SkeletonEndingFlowFact
            {
                EndingId = ending.Id,
                BeatCount = ending.Beats.Count
            };
            for (int beatIndex = 0; beatIndex < ending.Beats.Count; beatIndex++)
            {
                if (ending.Beats[beatIndex] is SequenceBeat sequence)
                {
                    endingFact.Sequences.Add(
                        new SkeletonSequencePosition(sequence.SequenceId, beatIndex));
                }
            }

            fact.Endings.Add(endingFact);
        }

        return fact;
    }

    private sealed class SceneContents
    {
        private readonly SceneSnapshot scene;
        private readonly IReadOnlyList<MonoBehaviour> sequencePlayers;
        private readonly IReadOnlyList<FirstPersonController> players;
        private readonly IReadOnlyList<CarriedItemHolder> holders;
        private readonly IReadOnlyList<NarrativePresenter> presenters;
        private readonly IReadOnlyList<LowResolutionPresenter> lowResolutionOutputs;
        private readonly IReadOnlyList<VisionBleedController> visionBleeds;
        private readonly IReadOnlyList<SpawnPoint> spawnPoints;
        private readonly IReadOnlyList<TaskReceiver> taskReceivers;
        private readonly IReadOnlyList<Carryable> carryables;
        private readonly IReadOnlyList<FlagInteractable> flagInteractables;
        private readonly IReadOnlyList<TaskProgressListener> taskListeners;

        public SceneContents(SceneSnapshot snapshot)
        {
            scene = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            sequencePlayers = scene.Components<MonoBehaviour>();
            players = scene.Components<FirstPersonController>();
            holders = scene.Components<CarriedItemHolder>();
            presenters = scene.Components<NarrativePresenter>();
            lowResolutionOutputs = scene.Components<LowResolutionPresenter>();
            visionBleeds = scene.Components<VisionBleedController>();
            spawnPoints = scene.Components<SpawnPoint>();
            taskReceivers = scene.Components<TaskReceiver>();
            carryables = scene.Components<Carryable>();
            flagInteractables = scene.Components<FlagInteractable>();
            taskListeners = scene.Components<TaskProgressListener>();
        }

        public void CollectReachableSequencePlayers(
            string sequenceId,
            int chapterIndex,
            ICollection<SkeletonSequencePlayerFact> destination)
        {
            for (int i = 0; i < sequencePlayers.Count; i++)
            {
                MonoBehaviour behaviour = sequencePlayers[i];
                if (!scene.WouldRegisterInChapter(behaviour, chapterIndex) ||
                    !(behaviour is ISequencePlayer player) ||
                    !string.Equals(player.SequenceId, sequenceId, StringComparison.Ordinal))
                {
                    continue;
                }

                string bossConfigurationError = string.Empty;
                if (behaviour is BossSequencePlayer bossPlayer &&
                    !bossPlayer.TryValidateProductionConfiguration(out bossConfigurationError))
                {
                    bossConfigurationError = bossConfigurationError ?? "has an unknown configuration error";
                }

                destination.Add(new SkeletonSequencePlayerFact
                {
                    SequenceId = player.SequenceId,
                    Owner = SceneSnapshot.HierarchyPath(behaviour.transform),
                    IsGenericGreybox = behaviour is GreyboxSequencePlayer,
                    IsBossPlayer = behaviour is BossSequencePlayer,
                    BossConfigurationError = bossConfigurationError,
                    IsCreditsPlayer = behaviour is CreditsSequencePlayer
                });
            }
        }

        public SkeletonGameplaySceneFact BuildGameplaySceneFact(int chapterIndex)
        {
            List<FirstPersonController> registeredPlayers =
                RegistrableServices(players, chapterIndex);
            List<CarriedItemHolder> registeredHolders =
                RegistrableServices(holders, chapterIndex);
            List<NarrativePresenter> registeredPresenters =
                RegistrableServices(presenters, chapterIndex);
            var fact = new SkeletonGameplaySceneFact
            {
                SceneName = scene.SceneName,
                ChapterIndex = chapterIndex,
                MissingScriptCount = scene.CountMissingScripts(),
                PlayerCount = registeredPlayers.Count,
                HolderCount = registeredHolders.Count,
                PresenterCount = registeredPresenters.Count
            };

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                SpawnPoint spawn = spawnPoints[i];
                if (!scene.WouldRegisterInChapter(spawn, chapterIndex))
                    continue;

                fact.Spawns.Add(new SkeletonSpawnFact
                {
                    Id = spawn.Id,
                    Owner = SceneSnapshot.HierarchyPath(spawn.transform),
                    Enabled = true
                });
            }

            ValidateRetroOutput(
                fact.RetroProblems,
                chapterIndex,
                registeredPlayers,
                registeredPresenters);
            return fact;
        }

        public SkeletonTaskChapterFact BuildTaskChapterFact(
            int chapterIndex,
            NarrativeCatalog catalog)
        {
            var fact = new SkeletonTaskChapterFact
            {
                SceneName = scene.SceneName,
                ChapterIndex = chapterIndex
            };
            var cataloguedObjectives = new HashSet<TaskObjective>();
            for (int i = 0; i < catalog.TaskObjectives.Count; i++)
            {
                if (catalog.TaskObjectives[i] != null)
                    cataloguedObjectives.Add(catalog.TaskObjectives[i]);
            }

            for (int i = 0; i < taskReceivers.Count; i++)
            {
                TaskReceiver receiver = taskReceivers[i];
                if (receiver == null)
                {
                    continue;
                }

                CarryCategory category = receiver.Accepts;
                TaskObjective objective = category != null ? category.Objective : null;
                bool hierarchyEligible = objective != null
                    ? scene.IsTaskProgressReachable(
                        receiver.transform,
                        objective,
                        chapterIndex)
                    : scene.IsReachableInChapter(
                        receiver.transform,
                        chapterIndex);
                if (!hierarchyEligible)
                    continue;

                fact.Receivers.Add(new SkeletonTaskReceiverFact
                {
                    Owner = SceneSnapshot.HierarchyPath(receiver.transform),
                    Enabled = receiver.enabled,
                    CategoryKey = ObjectKey(category),
                    CategoryName = DisplayCategory(category),
                    RetainedAfterUse = category != null && category.RetainedAfterUse,
                    ObjectiveKey = ObjectKey(objective),
                    ObjectiveName = DisplayObjective(objective),
                    ObjectiveCatalogued = objective != null &&
                        cataloguedObjectives.Contains(objective),
                    HasUsableCollider = objective != null
                        ? scene.HasTaskProgressInteractionCollider(
                            receiver,
                            objective,
                            chapterIndex)
                        : scene.HasUsableInteractionCollider(
                            receiver,
                            chapterIndex)
                });
            }

            for (int i = 0; i < carryables.Count; i++)
            {
                Carryable carryable = carryables[i];
                if (!IsEligibleCarrySource(carryable, chapterIndex))
                    continue;

                fact.Sources.Add(new SkeletonCarrySourceFact
                {
                    Owner = SceneSnapshot.HierarchyPath(carryable.transform),
                    CategoryKey = ObjectKey(carryable.Category),
                    CategoryName = DisplayCategory(carryable.Category)
                });
            }

            for (int i = 0; i < flagInteractables.Count; i++)
            {
                FlagInteractable interactable = flagInteractables[i];
                if (interactable == null || !interactable.enabled ||
                    interactable.RequiresCarried == null ||
                    !scene.IsReachableInChapter(interactable.transform, chapterIndex))
                {
                    continue;
                }

                fact.RequiresCarried.Add(new SkeletonRequiresCarriedFact
                {
                    Owner = SceneSnapshot.HierarchyPath(interactable.transform),
                    CategoryKey = ObjectKey(interactable.RequiresCarried),
                    CategoryName = DisplayCategory(interactable.RequiresCarried)
                });
            }

            for (int i = 0; i < taskListeners.Count; i++)
            {
                TaskProgressListener listener = taskListeners[i];
                if (listener == null ||
                    !scene.IsReachableInChapter(listener.transform, chapterIndex))
                {
                    continue;
                }

                TaskObjective objective = listener.Objective;
                fact.Listeners.Add(new SkeletonTaskListenerFact
                {
                    Owner = SceneSnapshot.HierarchyPath(listener.transform),
                    Enabled = listener.enabled,
                    ObjectiveKey = ObjectKey(objective),
                    ObjectiveName = DisplayObjective(objective),
                    ObjectiveCatalogued = objective != null &&
                        cataloguedObjectives.Contains(objective),
                    Threshold = listener.Threshold,
                    HasRevealTarget = listener.Revealed != null
                });
            }

            return fact;
        }

        private void ValidateRetroOutput(
            ICollection<string> problems,
            int chapterIndex,
            IReadOnlyList<FirstPersonController> registeredPlayers,
            IReadOnlyList<NarrativePresenter> registeredPresenters)
        {
            if (registeredPlayers.Count != 1 || registeredPresenters.Count != 1)
            {
                problems.Add(
                    "the player camera and presenter cannot form one unambiguous retro-output path");
                return;
            }

            FirstPersonController player = registeredPlayers[0];
            NarrativePresenter presenter = registeredPresenters[0];
            Camera playerCamera = SceneSnapshot.ObjectReference<Camera>(player, "playerCamera");
            Camera presenterCamera =
                SceneSnapshot.ObjectReference<Camera>(presenter, "lowResolutionCamera");

            if (player.GetComponent<CharacterController>() == null)
            {
                problems.Add("the sole registered player has no CharacterController");
            }

            if (playerCamera == null)
                problems.Add("FirstPersonController.playerCamera is not assigned");
            if (presenterCamera == null)
                problems.Add("NarrativePresenter.lowResolutionCamera is not assigned");
            if (playerCamera != null && presenterCamera != null &&
                !ReferenceEquals(playerCamera, presenterCamera))
            {
                problems.Add("the player and NarrativePresenter reference different cameras");
            }

            Camera outputCamera = playerCamera != null ? playerCamera : presenterCamera;
            if (outputCamera != null &&
                !scene.WouldRegisterInChapter(outputCamera, chapterIndex))
            {
                problems.Add("the gameplay camera is disabled or inactive for this chapter");
            }

            RenderTexture worldTarget = outputCamera != null ? outputCamera.targetTexture : null;
            if (worldTarget == null)
            {
                problems.Add("the gameplay camera has no RenderTexture target");
            }
            else
            {
                if (worldTarget.width != RetroResolution.WorldSize.x ||
                    worldTarget.height != RetroResolution.WorldSize.y)
                {
                    problems.Add(
                        $"the world RenderTexture is {worldTarget.width}x{worldTarget.height}, " +
                        $"expected {RetroResolution.Format(RetroResolution.WorldSize)}");
                }

                if (worldTarget.filterMode != FilterMode.Point)
                    problems.Add("the world RenderTexture is not point-filtered");
                if (worldTarget.antiAliasing != 1)
                    problems.Add("the world RenderTexture must use one sample");
                if (worldTarget.useMipMap)
                    problems.Add("the world RenderTexture must not use mipmaps");
                if (worldTarget.depth <= 0)
                    problems.Add("the world RenderTexture has no depth buffer");
            }

            List<LowResolutionPresenter> registeredOutputs =
                RegistrableServices(lowResolutionOutputs, chapterIndex);
            for (int i = registeredOutputs.Count - 1; i >= 0; i--)
            {
                if (!registeredOutputs[i].IsSceneWorldOutput)
                    registeredOutputs.RemoveAt(i);
            }

            if (registeredOutputs.Count != 1)
            {
                problems.Add(
                    $"the scene has {registeredOutputs.Count} registered LowResolutionPresenter outputs; expected one");
            }
            else
            {
                LowResolutionPresenter output = registeredOutputs[0];
                RawImage image = output != null ? output.GetComponent<RawImage>() : null;
                Canvas canvas = output != null ? output.GetComponentInParent<Canvas>() : null;
                if (output == null || !output.enabled || image == null || !image.enabled)
                    problems.Add("the low-resolution output is disabled or has no enabled RawImage");
                else if (!ReferenceEquals(image.texture, worldTarget))
                    problems.Add("the low-resolution RawImage does not display the gameplay camera target");

                if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    problems.Add("the low-resolution output is not beneath a Screen Space Overlay canvas");
            }

            List<VisionBleedController> registeredVisionBleeds =
                RegistrableServices(visionBleeds, chapterIndex);
            if (registeredVisionBleeds.Count != 1)
            {
                problems.Add(
                    $"the scene has {registeredVisionBleeds.Count} registered VisionBleedController instances; expected one");
            }
            else
            {
                VisionBleedController bleed = registeredVisionBleeds[0];
                Canvas canvas = bleed != null ? bleed.GetComponentInParent<Canvas>() : null;
                CanvasScaler scaler = bleed != null ? bleed.GetComponentInParent<CanvasScaler>() : null;
                if (bleed == null || !bleed.enabled || bleed.GetComponent<Image>() == null)
                    problems.Add("the VisionBleedController is disabled or has no Image");
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera ||
                    !ReferenceEquals(canvas.worldCamera, outputCamera))
                {
                    problems.Add("the VisionBleed overlay is not rendered by the gameplay camera");
                }

                if (scaler == null ||
                    !Approximately(scaler.referenceResolution, RetroResolution.WorldSize))
                {
                    problems.Add("the VisionBleed canvas does not use RetroResolution.WorldSize");
                }
            }
        }

        private List<T> RegistrableServices<T>(
            IReadOnlyList<T> candidates,
            int chapterIndex)
            where T : Behaviour
        {
            var registered = new List<T>();
            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (scene.WouldRegisterInChapter(candidate, chapterIndex))
                    registered.Add(candidate);
            }

            return registered;
        }

        private bool IsEligibleCarrySource(Carryable source, int chapterIndex)
        {
            if (source == null || !source.enabled || source.Category == null)
            {
                return false;
            }

            TaskObjective objective = source.Category.Objective;
            bool hierarchyEligible = objective != null
                ? scene.IsTaskProgressReachable(
                    source.transform,
                    objective,
                    chapterIndex)
                : scene.IsReachableInChapter(source.transform, chapterIndex);
            if (!hierarchyEligible)
                return false;

            CarriedItemHolder grantedHolder =
                scene.FindAncestor<CarriedItemHolder>(source.transform);
            if (grantedHolder != null && grantedHolder.enabled &&
                scene.IsAllowedByChapterDressing(grantedHolder.transform, chapterIndex))
            {
                return true;
            }

            return objective != null
                ? scene.HasTaskProgressInteractionCollider(
                    source,
                    objective,
                    chapterIndex)
                : scene.HasUsableInteractionCollider(source, chapterIndex);
        }

        private static string ObjectKey(UnityEngine.Object value) =>
            value != null ? value.GetEntityId().ToString() : string.Empty;

        private static string DisplayCategory(CarryCategory category)
        {
            if (category == null)
                return "<missing>";
            return string.IsNullOrWhiteSpace(category.Id) ? category.name : category.Id;
        }

        private static string DisplayObjective(TaskObjective objective)
        {
            if (objective == null)
                return "<missing>";
            return string.IsNullOrWhiteSpace(objective.Id) ? objective.name : objective.Id;
        }

        private static bool Approximately(Vector2 value, Vector2Int expected) =>
            Mathf.Approximately(value.x, expected.x) &&
            Mathf.Approximately(value.y, expected.y);

    }
}

internal static class PlayableSkeletonValidationRules
{
    private const string BossSequenceId = "boss_verdant_mirror";
    private static readonly string[] CreditSequenceIds =
    {
        "credits_confession",
        "credits_denial",
        "credits_sacrifice"
    };
    private static readonly string[] CanonicalEndingIds =
    {
        "confession",
        "denial",
        "sacrifice"
    };
    private static readonly string[] CanonicalChoiceLabels =
    {
        "CONFESSION",
        "DENIAL",
        "SACRIFICE"
    };

    internal static void ValidateSequenceBinding(
        string context,
        string sceneName,
        string sequenceId,
        bool locksPlayer,
        IReadOnlyList<SkeletonSequencePlayerFact> players,
        ICollection<string> problems)
    {
        string displayId = string.IsNullOrWhiteSpace(sequenceId) ? "<blank>" : sequenceId;
        string displayScene = string.IsNullOrWhiteSpace(sceneName) ? "<missing>" : sceneName;
        if (players.Count == 0)
        {
            problems.Add(
                $"{context} sequence '{displayId}' has no reachable ISequencePlayer " +
                $"in scene '{displayScene}' for the active chapter dressing.");
        }
        else if (players.Count > 1)
        {
            problems.Add(
                $"{context} sequence '{displayId}' has {players.Count} reachable players " +
                $"in scene '{displayScene}'; exactly one is required.");
        }

        if (string.Equals(sequenceId, BossSequenceId, StringComparison.Ordinal) && locksPlayer)
        {
            problems.Add(
                $"{context} boss sequence '{BossSequenceId}' still locks player movement.");
        }

        if (string.Equals(sequenceId, BossSequenceId, StringComparison.Ordinal) &&
            players.Count == 1 && !players[0].IsBossPlayer)
        {
            problems.Add(
                $"{context} boss sequence '{BossSequenceId}' is not handled by a " +
                $"{nameof(BossSequencePlayer)}.");
        }

        if (string.Equals(sequenceId, BossSequenceId, StringComparison.Ordinal) &&
            players.Count == 1 && players[0].IsBossPlayer &&
            !string.IsNullOrWhiteSpace(players[0].BossConfigurationError))
        {
            problems.Add(
                $"{context} boss sequence '{BossSequenceId}' player '{players[0].Owner}' " +
                $"{players[0].BossConfigurationError}.");
        }

        if (!IsCreditSequence(sequenceId))
            return;

        if (players.Count == 1 && !players[0].IsCreditsPlayer)
        {
            if (players[0].IsGenericGreybox)
            {
                problems.Add(
                    $"{context} credits sequence '{displayId}' is handled only by the " +
                    $"generic {nameof(GreyboxSequencePlayer)} hook '{players[0].Owner}'.");
            }
            else
            {
                problems.Add(
                    $"{context} credits sequence '{displayId}' is not handled by a " +
                    $"{nameof(CreditsSequencePlayer)}.");
            }
        }
    }

    internal static void ValidateTaskChapter(
        SkeletonTaskChapterFact fact,
        ICollection<string> problems)
    {
        string context = $"Scene '{fact.SceneName}', Chapter {fact.ChapterIndex}";
        var receiverGroups =
            new Dictionary<string, List<SkeletonTaskReceiverFact>>(StringComparer.Ordinal);
        var sourceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var registeredRequirements = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < fact.Sources.Count; i++)
        {
            SkeletonCarrySourceFact source = fact.Sources[i];
            if (string.IsNullOrWhiteSpace(source.CategoryKey))
                continue;
            sourceCounts.TryGetValue(source.CategoryKey, out int count);
            sourceCounts[source.CategoryKey] = count + 1;
        }

        for (int i = 0; i < fact.Receivers.Count; i++)
        {
            SkeletonTaskReceiverFact receiver = fact.Receivers[i];
            if (!receiver.Enabled)
                problems.Add($"{context} task receiver '{receiver.Owner}' is disabled.");
            if (string.IsNullOrWhiteSpace(receiver.CategoryKey))
            {
                problems.Add(
                    $"{context} task receiver '{receiver.Owner}' has no accepted carry category.");
            }
            else
            {
                if (!receiverGroups.TryGetValue(
                    receiver.CategoryKey,
                    out List<SkeletonTaskReceiverFact> group))
                {
                    group = new List<SkeletonTaskReceiverFact>();
                    receiverGroups.Add(receiver.CategoryKey, group);
                }

                group.Add(receiver);
            }

            if (string.IsNullOrWhiteSpace(receiver.ObjectiveKey))
            {
                problems.Add(
                    $"{context} task receiver '{receiver.Owner}' has no task objective.");
            }
            else if (!receiver.ObjectiveCatalogued)
            {
                problems.Add(
                    $"{context} task receiver '{receiver.Owner}' references uncatalogued " +
                    $"objective '{receiver.ObjectiveName}'.");
            }

            if (!receiver.HasUsableCollider)
            {
                problems.Add(
                    $"{context} task receiver '{receiver.Owner}' has no enabled, non-trigger " +
                    "collider that resolves back to that receiver.");
            }

            if (receiver.Enabled &&
                !string.IsNullOrWhiteSpace(receiver.CategoryKey) &&
                !string.IsNullOrWhiteSpace(receiver.ObjectiveKey))
            {
                registeredRequirements.TryGetValue(receiver.ObjectiveKey, out int count);
                registeredRequirements[receiver.ObjectiveKey] = count + 1;
            }
        }

        foreach (KeyValuePair<string, List<SkeletonTaskReceiverFact>> pair in receiverGroups)
        {
            List<SkeletonTaskReceiverFact> receivers = pair.Value;
            SkeletonTaskReceiverFact sample = receivers[0];
            sourceCounts.TryGetValue(pair.Key, out int sourceCount);
            if (sample.RetainedAfterUse)
            {
                if (sourceCount == 0)
                {
                    problems.Add(
                        $"{context} retained tool category '{sample.CategoryName}' has no " +
                        "eligible carryable source.");
                }

                continue;
            }

            if (sourceCount < receivers.Count)
            {
                problems.Add(
                    $"{context} non-retained category '{sample.CategoryName}' has " +
                    $"{sourceCount} eligible source(s) for {receivers.Count} receiver(s).");
            }
        }

        for (int i = 0; i < fact.RequiresCarried.Count; i++)
        {
            SkeletonRequiresCarriedFact requirement = fact.RequiresCarried[i];
            sourceCounts.TryGetValue(requirement.CategoryKey ?? string.Empty, out int count);
            if (count == 0)
            {
                problems.Add(
                    $"{context} requiresCarried interaction '{requirement.Owner}' has no " +
                    $"eligible '{requirement.CategoryName}' source.");
            }
        }

        for (int i = 0; i < fact.Listeners.Count; i++)
        {
            SkeletonTaskListenerFact listener = fact.Listeners[i];
            if (!listener.Enabled)
                problems.Add($"{context} task progress listener '{listener.Owner}' is disabled.");
            if (string.IsNullOrWhiteSpace(listener.ObjectiveKey))
            {
                problems.Add(
                    $"{context} task progress listener '{listener.Owner}' has no objective.");
            }
            else if (!listener.ObjectiveCatalogued)
            {
                problems.Add(
                    $"{context} task progress listener '{listener.Owner}' references " +
                    $"uncatalogued objective '{listener.ObjectiveName}'.");
            }

            registeredRequirements.TryGetValue(
                listener.ObjectiveKey ?? string.Empty,
                out int registeredRequirement);
            if (listener.Threshold <= 0 ||
                (!string.IsNullOrWhiteSpace(listener.ObjectiveKey) &&
                 listener.ObjectiveCatalogued &&
                 listener.Threshold > registeredRequirement))
            {
                problems.Add(
                    $"{context} task progress listener '{listener.Owner}' has invalid threshold " +
                    $"{listener.Threshold}; registered requirement is {registeredRequirement}.");
            }

            if (!listener.HasRevealTarget)
            {
                problems.Add(
                    $"{context} task progress listener '{listener.Owner}' has no reveal target.");
            }
        }
    }

    internal static void ValidateGameplayScene(
        SkeletonGameplaySceneFact fact,
        ICollection<string> problems)
    {
        string context = fact.ChapterIndex > 0
            ? $"Gameplay scene '{fact.SceneName}', Chapter {fact.ChapterIndex}"
            : $"Gameplay scene '{fact.SceneName}'";
        if (fact.MissingScriptCount > 0)
        {
            problems.Add(
                $"{context} contains {fact.MissingScriptCount} missing MonoBehaviour script(s).");
        }

        if (fact.PlayerCount != 1)
            problems.Add($"{context} has {fact.PlayerCount} players; exactly one is required.");
        if (fact.HolderCount > 1)
        {
            problems.Add(
                $"{context} has {fact.HolderCount} CarriedItemHolder instances; at most one is allowed.");
        }

        if (fact.PresenterCount != 1)
        {
            problems.Add(
                $"{context} has {fact.PresenterCount} NarrativePresenter instances; exactly one is required.");
        }

        if (fact.Spawns.Count == 0)
            problems.Add($"{context} has no SpawnPoint.");

        var seenSpawnIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < fact.Spawns.Count; i++)
        {
            SkeletonSpawnFact spawn = fact.Spawns[i];
            if (!spawn.Enabled)
                problems.Add($"{context} spawn '{spawn.Owner}' is disabled.");
            if (string.IsNullOrWhiteSpace(spawn.Id))
            {
                problems.Add($"{context} spawn '{spawn.Owner}' has a blank id.");
                continue;
            }

            if (!seenSpawnIds.Add(spawn.Id))
                problems.Add($"{context} has duplicate spawn id '{spawn.Id}'.");
        }

        for (int i = 0; i < fact.RetroProblems.Count; i++)
            problems.Add($"{context} has a broken retro-output contract: {fact.RetroProblems[i]}.");
    }

    internal static void ValidateCanonicalFlow(
        SkeletonCanonicalFlowFact fact,
        ICollection<string> problems)
    {
        if (fact.ChoiceCount != 1)
        {
            problems.Add(
                $"Canonical flow has {fact.ChoiceCount} ChoiceBeat instances; exactly one is required.");
        }

        if (!fact.FinalChoiceIsLast)
            problems.Add("The Chapter VII ChoiceBeat must be the final chapter beat.");

        ValidateOrderedValues(
            fact.FinalChoiceEndingIds,
            CanonicalEndingIds,
            "Chapter VII choice ending ids",
            problems);
        ValidateOrderedValues(
            fact.FinalChoiceLabels,
            CanonicalChoiceLabels,
            "Chapter VII choice labels",
            problems);

        ValidateEnding(
            fact,
            "confession",
            new[]
            {
                new SkeletonSequencePosition("boss_verdant_mirror", 0),
                new SkeletonSequencePosition("blooms_brown_and_fall", 3),
                new SkeletonSequencePosition("credits_confession", 6)
            },
            problems);
        ValidateEnding(
            fact,
            "denial",
            new[]
            {
                new SkeletonSequencePosition("walks_into_the_painting", 2),
                new SkeletonSequencePosition("credits_denial", 3)
            },
            problems);
        ValidateEnding(
            fact,
            "sacrifice",
            new[]
            {
                new SkeletonSequencePosition("boss_verdant_mirror", 0),
                new SkeletonSequencePosition("credits_sacrifice", 4)
            },
            problems);
    }

    private static void ValidateEnding(
        SkeletonCanonicalFlowFact fact,
        string endingId,
        IReadOnlyList<SkeletonSequencePosition> expected,
        ICollection<string> problems)
    {
        SkeletonEndingFlowFact ending = null;
        int matches = 0;
        for (int i = 0; i < fact.Endings.Count; i++)
        {
            if (!string.Equals(fact.Endings[i].EndingId, endingId, StringComparison.Ordinal))
                continue;
            ending = fact.Endings[i];
            matches++;
        }

        if (matches != 1 || ending == null)
        {
            problems.Add(
                $"Canonical ending '{endingId}' appears {matches} times; exactly one is required.");
            return;
        }

        bool matchesExpected = ending.Sequences.Count == expected.Count;
        if (matchesExpected)
        {
            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(
                        ending.Sequences[i].SequenceId,
                        expected[i].SequenceId,
                        StringComparison.Ordinal) ||
                    ending.Sequences[i].BeatIndex != expected[i].BeatIndex)
                {
                    matchesExpected = false;
                    break;
                }
            }
        }

        if (!matchesExpected)
        {
            problems.Add(
                $"Ending '{endingId}' sequence order/positions differ from the canonical branch.");
        }

        SkeletonSequencePosition credits = expected[expected.Count - 1];
        if (ending.BeatCount == 0 || credits.BeatIndex != ending.BeatCount - 1)
        {
            problems.Add(
                $"Ending '{endingId}' credits sequence must be its final beat.");
        }
    }

    private static void ValidateOrderedValues(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string context,
        ICollection<string> problems)
    {
        if (actual.Count != expected.Count)
        {
            problems.Add(
                $"{context} must contain exactly [{string.Join(", ", expected)}] in that order.");
            return;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            if (string.Equals(actual[i], expected[i], StringComparison.Ordinal))
                continue;
            problems.Add(
                $"{context} must contain exactly [{string.Join(", ", expected)}] in that order.");
            return;
        }
    }

    private static bool IsCreditSequence(string sequenceId)
    {
        for (int i = 0; i < CreditSequenceIds.Length; i++)
        {
            if (string.Equals(sequenceId, CreditSequenceIds[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

internal sealed class SkeletonSequencePlayerFact
{
    public string SequenceId;
    public string Owner;
    public bool IsGenericGreybox;
    public bool IsBossPlayer;
    public string BossConfigurationError;
    public bool IsCreditsPlayer;
}

internal sealed class SkeletonTaskChapterFact
{
    public string SceneName;
    public int ChapterIndex;
    public readonly List<SkeletonTaskReceiverFact> Receivers =
        new List<SkeletonTaskReceiverFact>();
    public readonly List<SkeletonCarrySourceFact> Sources =
        new List<SkeletonCarrySourceFact>();
    public readonly List<SkeletonRequiresCarriedFact> RequiresCarried =
        new List<SkeletonRequiresCarriedFact>();
    public readonly List<SkeletonTaskListenerFact> Listeners =
        new List<SkeletonTaskListenerFact>();
}

internal sealed class SkeletonTaskReceiverFact
{
    public string Owner;
    public bool Enabled = true;
    public string CategoryKey;
    public string CategoryName;
    public bool RetainedAfterUse;
    public string ObjectiveKey;
    public string ObjectiveName;
    public bool ObjectiveCatalogued;
    public bool HasUsableCollider;
}

internal sealed class SkeletonCarrySourceFact
{
    public string Owner;
    public string CategoryKey;
    public string CategoryName;
}

internal sealed class SkeletonRequiresCarriedFact
{
    public string Owner;
    public string CategoryKey;
    public string CategoryName;
}

internal sealed class SkeletonTaskListenerFact
{
    public string Owner;
    public bool Enabled = true;
    public string ObjectiveKey;
    public string ObjectiveName;
    public bool ObjectiveCatalogued;
    public int Threshold;
    public bool HasRevealTarget = true;
}

internal sealed class SkeletonGameplaySceneFact
{
    public string SceneName;
    public int ChapterIndex;
    public int MissingScriptCount;
    public int PlayerCount = 1;
    public int HolderCount;
    public int PresenterCount = 1;
    public readonly List<SkeletonSpawnFact> Spawns = new List<SkeletonSpawnFact>();
    public readonly List<string> RetroProblems = new List<string>();
}

internal sealed class SkeletonSpawnFact
{
    public string Id;
    public string Owner;
    public bool Enabled = true;
}

internal sealed class SkeletonCanonicalFlowFact
{
    public int ChoiceCount;
    public bool FinalChoiceIsLast;
    public readonly List<string> FinalChoiceEndingIds = new List<string>();
    public readonly List<string> FinalChoiceLabels = new List<string>();
    public readonly List<SkeletonEndingFlowFact> Endings =
        new List<SkeletonEndingFlowFact>();
}

internal sealed class SkeletonEndingFlowFact
{
    public string EndingId;
    public int BeatCount;
    public readonly List<SkeletonSequencePosition> Sequences =
        new List<SkeletonSequencePosition>();
}

internal struct SkeletonSequencePosition
{
    public SkeletonSequencePosition(string sequenceId, int beatIndex)
    {
        SequenceId = sequenceId;
        BeatIndex = beatIndex;
    }

    public string SequenceId;
    public int BeatIndex;
}
