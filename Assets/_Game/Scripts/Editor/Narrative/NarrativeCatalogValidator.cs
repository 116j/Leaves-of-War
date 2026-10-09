using System;
using System.Collections.Generic;
using Hortensia.Editor.Validation;
using Hortensia.Narrative;
using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;

public static class NarrativeCatalogValidator
{
    private const int CanonicalChapterCount = 7;
    private const string MenuPath = "Tools/Hortensia/Narrative/Validate Catalog";
    private static readonly string[] CanonicalEndingIds =
    {
        "confession",
        "denial",
        "sacrifice"
    };
    private static readonly string[] CanonicalTaskObjectiveIds =
    {
        "unpacking",
        "gardening",
        "maze_tidying"
    };
    private static readonly HashSet<string> CanonicalSequenceIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "study_1910_seat",
            "study_1910_reveal",
            "study_1915_seat",
            "study_1915_reveal",
            "dream_2_flood",
            "dream_3_attack",
            "boss_verdant_mirror",
            "blooms_brown_and_fall",
            "walks_into_the_painting",
            "credits_confession",
            "credits_denial",
            "credits_sacrifice"
        };

    [MenuItem(MenuPath)]
    private static void ValidateFromMenu()
    {
        if (!NarrativeCatalogLocator.TryLoadSingle(
                out NarrativeCatalog catalog,
                out string error))
        {
            Debug.LogError(error);
            EditorUtility.DisplayDialog("Narrative catalog validation failed", error, "OK");
            return;
        }

        if (TryValidate(catalog, out error))
        {
            Debug.Log("Narrative catalog validation passed.");
            return;
        }

        Debug.LogError(error);
        EditorUtility.DisplayDialog("Narrative catalog validation failed", error, "OK");
    }

    public static bool TryValidate(NarrativeCatalog catalog, out string error)
    {
        if (catalog == null)
        {
            error = "Narrative catalog validation requires a NarrativeCatalog.";
            return false;
        }

        var structuralReport = new ValidationReport();
        CollectStructureProblems(catalog, structuralReport);
        if (!structuralReport.TryFormat(
                "Narrative catalog validation failed:",
                false,
                out error))
        {
            return false;
        }

        var report = new ValidationReport();
        using (var context = new ValidationContext(report))
        {
            CollectContentProblems(catalog, context, report);
        }

        return report.TryFormat(
            "Narrative catalog validation failed:",
            false,
            out error);
    }

    /// <summary>
    /// Validates the authored ordering and stable identities that runtime flow
    /// assumes without consulting AssetDatabase, Build Settings, or scenes.
    /// </summary>
    public static bool TryValidateStructure(NarrativeCatalog catalog, out string error)
    {
        if (catalog == null)
        {
            error = "Narrative catalog validation requires a NarrativeCatalog.";
            return false;
        }

        var report = new ValidationReport();
        CollectStructureProblems(catalog, report);
        return report.TryFormat(
            "Narrative catalog validation failed:",
            false,
            out error);
    }

    internal static void CollectStructureProblems(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        if (catalog == null)
        {
            problems.Add("Narrative catalog validation requires a NarrativeCatalog.");
            return;
        }

        ValidateChapterStructure(catalog, problems);
        ValidateEndingIdentities(catalog, problems);
        ValidateFlagIdentities(catalog, problems);
        ValidateTaskObjectiveIdentities(catalog, problems);
        ValidateDocumentIdentities(catalog, problems);
        ValidateBeatReferences(catalog, problems);
    }

    internal static void CollectContentProblems(
        NarrativeCatalog catalog,
        ValidationContext context,
        ICollection<string> problems)
    {
        ValidateChoiceBeats(catalog, problems);
        ValidateNameTokens(catalog, problems);
        ValidateLocationsAndDocumentSequences(catalog, context, problems);
    }

    private static void ValidateChapterStructure(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        if (catalog.Chapters.Count != CanonicalChapterCount)
        {
            problems.Add(
                $"The catalog must contain exactly {CanonicalChapterCount} chapters, " +
                $"but contains {catalog.Chapters.Count}.");
        }

        var seenIndices = new HashSet<int>();
        for (int position = 0; position < catalog.Chapters.Count; position++)
        {
            ChapterDefinition chapter = catalog.Chapters[position];
            if (chapter == null)
            {
                problems.Add($"Chapter entry {position + 1} is missing from the catalog.");
                continue;
            }

            int index = chapter.Index;
            if (index < 1 || index > CanonicalChapterCount)
            {
                problems.Add(
                    $"Chapter entry {position + 1} has out-of-range index {index}; " +
                    $"expected an index from 1 through {CanonicalChapterCount}.");
            }

            if (!seenIndices.Add(index))
                problems.Add($"Chapter index {index} appears more than once in the catalog.");

            int expectedIndex = position + 1;
            if (index != expectedIndex)
            {
                problems.Add(
                    $"Chapter entry {position + 1} has index {index}; " +
                    $"chapters must be authored in index order and this entry must have index {expectedIndex}.");
            }
        }

        for (int expectedIndex = 1; expectedIndex <= CanonicalChapterCount; expectedIndex++)
        {
            if (!seenIndices.Contains(expectedIndex))
                problems.Add($"The catalog is missing Chapter index {expectedIndex}.");
        }

        ChapterDefinition finalChapter = catalog.FinalChapter;
        if (finalChapter == null || finalChapter.Index != CanonicalChapterCount)
        {
            problems.Add(
                $"The catalog's final chapter entry must be Chapter {CanonicalChapterCount}.");
        }
    }

    private static void ValidateEndingIdentities(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        if (catalog.Endings.Count != CanonicalEndingIds.Length)
        {
            problems.Add(
                $"The catalog must contain exactly {CanonicalEndingIds.Length} endings, " +
                $"but contains {catalog.Endings.Count}.");
        }

        var expectedIds = new HashSet<string>(CanonicalEndingIds, StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < catalog.Endings.Count; position++)
        {
            EndingDefinition ending = catalog.Endings[position];
            if (ending == null)
            {
                problems.Add($"Ending entry {position + 1} is missing from the catalog.");
                continue;
            }

            string id = ending.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                problems.Add($"Ending entry {position + 1} has a blank id.");
                continue;
            }

            if (!seenIds.Add(id))
                problems.Add($"Ending id '{id}' appears more than once in the catalog.");
            if (!expectedIds.Contains(id))
                problems.Add($"Ending id '{id}' is not one of the three canonical ending ids.");
        }

        for (int i = 0; i < CanonicalEndingIds.Length; i++)
        {
            string expectedId = CanonicalEndingIds[i];
            if (!seenIds.Contains(expectedId))
                problems.Add($"The catalog is missing canonical ending id '{expectedId}'.");
        }
    }

    private static void ValidateFlagIdentities(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < catalog.Flags.Count; position++)
        {
            FlagId flag = catalog.Flags[position];
            if (flag == null)
            {
                problems.Add($"Flag entry {position + 1} is missing from the catalog.");
                continue;
            }

            string id = flag.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                problems.Add($"Flag entry {position + 1} has a blank id.");
                continue;
            }

            if (!seenIds.Add(id))
                problems.Add($"Flag id '{id}' appears more than once in the catalog.");
        }
    }

    private static void ValidateDocumentIdentities(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < catalog.Documents.Count; position++)
        {
            DocumentDefinition document = catalog.Documents[position];
            if (document == null)
            {
                problems.Add($"Document entry {position + 1} is missing from the catalog.");
                continue;
            }

            string id = document.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                problems.Add($"Document entry {position + 1} has a blank id.");
                continue;
            }

            if (!seenIds.Add(id))
                problems.Add($"Document id '{id}' appears more than once in the catalog.");
        }
    }

    private static void ValidateTaskObjectiveIdentities(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        if (catalog.TaskObjectives.Count != CanonicalTaskObjectiveIds.Length)
        {
            problems.Add(
                $"The catalog must contain exactly {CanonicalTaskObjectiveIds.Length} task objectives, " +
                $"but contains {catalog.TaskObjectives.Count}.");
        }

        var expectedIds = new HashSet<string>(CanonicalTaskObjectiveIds, StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < catalog.TaskObjectives.Count; position++)
        {
            TaskObjective objective = catalog.TaskObjectives[position];
            if (objective == null)
            {
                problems.Add($"Task objective entry {position + 1} is missing from the catalog.");
                continue;
            }

            string id = objective.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                problems.Add($"Task objective entry {position + 1} has a blank id.");
                continue;
            }

            if (!seenIds.Add(id))
                problems.Add($"Task objective id '{id}' appears more than once in the catalog.");
            if (!expectedIds.Contains(id))
                problems.Add($"Task objective id '{id}' is not one of the three canonical task objective ids.");
        }

        for (int i = 0; i < CanonicalTaskObjectiveIds.Length; i++)
        {
            string expectedId = CanonicalTaskObjectiveIds[i];
            if (!seenIds.Contains(expectedId))
                problems.Add($"The catalog is missing canonical task objective id '{expectedId}'.");
        }
    }

    private static void ValidateBeatReferences(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        var cataloguedObjectives = new HashSet<TaskObjective>();
        for (int i = 0; i < catalog.TaskObjectives.Count; i++)
        {
            if (catalog.TaskObjectives[i] != null)
                cataloguedObjectives.Add(catalog.TaskObjectives[i]);
        }

        for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterIndex];
            if (chapter != null)
            {
                ValidateBeatReferences(
                    chapter.Beats,
                    $"Chapter {chapter.Index}",
                    cataloguedObjectives,
                    problems);
            }
        }

        for (int endingIndex = 0; endingIndex < catalog.Endings.Count; endingIndex++)
        {
            EndingDefinition ending = catalog.Endings[endingIndex];
            if (ending != null)
            {
                ValidateBeatReferences(
                    ending.Beats,
                    $"Ending '{ending.Title}'",
                    cataloguedObjectives,
                    problems);
            }
        }
    }

    private static void ValidateBeatReferences(
        IReadOnlyList<NarrativeBeat> beats,
        string source,
        ISet<TaskObjective> cataloguedObjectives,
        ICollection<string> problems)
    {
        for (int beatIndex = 0; beatIndex < beats.Count; beatIndex++)
        {
            NarrativeBeat beat = beats[beatIndex];
            string context = $"{source}, beat {beatIndex + 1}";
            if (beat is SequenceBeat sequence &&
                !CanonicalSequenceIds.Contains(sequence.SequenceId ?? string.Empty))
            {
                string displayId = string.IsNullOrWhiteSpace(sequence.SequenceId)
                    ? "<blank>"
                    : sequence.SequenceId;
                problems.Add($"{context} has unknown sequence id '{displayId}'.");
            }

            if (beat is GateBeat gate)
            {
                ValidateTaskConditionReferences(
                    gate.Condition,
                    context,
                    cataloguedObjectives,
                    problems);
            }
        }
    }

    private static void ValidateTaskConditionReferences(
        BeatCondition condition,
        string context,
        ISet<TaskObjective> cataloguedObjectives,
        ICollection<string> problems)
    {
        if (condition is TaskCondition task)
        {
            if (task.Objective == null)
                problems.Add($"{context} has a TaskCondition with no objective.");
            else if (!cataloguedObjectives.Contains(task.Objective))
            {
                problems.Add(
                    $"{context} references uncatalogued task objective '{task.Objective.Id}'.");
            }

            return;
        }

        IReadOnlyList<BeatCondition> children = null;
        if (condition is AllOfCondition all)
            children = all.Conditions;
        else if (condition is AnyOfCondition any)
            children = any.Conditions;

        if (children == null)
            return;

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] != null)
            {
                ValidateTaskConditionReferences(
                    children[i],
                    context,
                    cataloguedObjectives,
                    problems);
            }
        }
    }

    private static void ValidateChoiceBeats(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        var canonicalEndings = new HashSet<EndingDefinition>();
        for (int i = 0; i < catalog.Endings.Count; i++)
        {
            EndingDefinition ending = catalog.Endings[i];
            if (ending == null)
            {
                problems.Add($"Canonical ending {i + 1} is missing from the catalog.");
                continue;
            }

            if (!canonicalEndings.Add(ending))
                problems.Add($"Canonical ending '{ending.name}' appears more than once in the catalog.");
        }

        if (canonicalEndings.Count != 3)
            problems.Add($"The catalog must define exactly three canonical endings, but defines {canonicalEndings.Count}.");

        ChapterDefinition finalChapter = catalog.FinalChapter;
        int choiceCount = 0;
        for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterIndex];
            if (chapter == null)
                continue;

            for (int beatIndex = 0; beatIndex < chapter.Beats.Count; beatIndex++)
            {
                if (!(chapter.Beats[beatIndex] is ChoiceBeat choice))
                    continue;

                choiceCount++;
                string context = $"Chapter {chapter.Index}, beat {beatIndex + 1}";
                if (!ReferenceEquals(chapter, finalChapter))
                    problems.Add($"{context} is a ChoiceBeat outside the final chapter.");

                if (choice.Options.Count != 3)
                    problems.Add($"{context} must contain exactly three ending options, but contains {choice.Options.Count}.");

                var selectedEndings = new HashSet<EndingDefinition>();
                for (int optionIndex = 0; optionIndex < choice.Options.Count; optionIndex++)
                {
                    EndingDefinition ending = choice.Options[optionIndex].Ending;
                    if (ending == null)
                    {
                        problems.Add($"{context}, option {optionIndex + 1} has no ending.");
                        continue;
                    }

                    if (!canonicalEndings.Contains(ending))
                        problems.Add($"{context}, option {optionIndex + 1} references non-canonical ending '{ending.name}'.");
                    else if (!selectedEndings.Add(ending))
                        problems.Add($"{context} references ending '{ending.name}' more than once.");
                }

                if (!selectedEndings.SetEquals(canonicalEndings))
                {
                    problems.Add($"{context} does not contain the three canonical endings exactly once.");
                }
            }
        }

        for (int endingIndex = 0; endingIndex < catalog.Endings.Count; endingIndex++)
        {
            EndingDefinition ending = catalog.Endings[endingIndex];
            if (ending == null)
                continue;

            for (int beatIndex = 0; beatIndex < ending.Beats.Count; beatIndex++)
            {
                if (ending.Beats[beatIndex] is ChoiceBeat)
                {
                    problems.Add(
                        $"Ending '{ending.Title}', beat {beatIndex + 1} is a ChoiceBeat. " +
                        "The one canonical choice belongs in the final chapter.");
                }
            }
        }

        if (choiceCount != 1)
            problems.Add($"The catalog must contain one final ChoiceBeat, but contains {choiceCount}.");
    }

    private static void ValidateNameTokens(
        NarrativeCatalog catalog,
        ICollection<string> problems)
    {
        for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterIndex];
            if (chapter == null)
                continue;

            ValidateNameTokensInBeats(chapter.Beats, $"Chapter {chapter.Index}", problems);
        }

        for (int endingIndex = 0; endingIndex < catalog.Endings.Count; endingIndex++)
        {
            EndingDefinition ending = catalog.Endings[endingIndex];
            if (ending != null)
                ValidateNameTokensInBeats(ending.Beats, $"Ending '{ending.Title}'", problems);
        }

        for (int documentIndex = 0; documentIndex < catalog.Documents.Count; documentIndex++)
        {
            DocumentDefinition document = catalog.Documents[documentIndex];
            if (document == null || !document.HasNarration || document.NarrationHasNameVariants)
                continue;

            if (NameVariants.ContainsToken(document.Narration.TextFor(NameVariant.Laura)))
            {
                problems.Add(
                    $"Document '{document.Title}' has shared narration and cannot contain {NameVariants.Token} in that narration.");
            }

            for (int pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
            {
                if (NameVariants.ContainsToken(document.Pages[pageIndex]))
                {
                    problems.Add(
                        $"Document '{document.Title}' has shared narration and cannot contain {NameVariants.Token} on page {pageIndex + 1}.");
                }
            }
        }
    }

    private static void ValidateNameTokensInBeats(
        IReadOnlyList<NarrativeBeat> beats,
        string source,
        ICollection<string> problems)
    {
        for (int beatIndex = 0; beatIndex < beats.Count; beatIndex++)
        {
            NarrativeBeat beat = beats[beatIndex];
            string context = $"{source}, beat {beatIndex + 1}";
            if (beat is SpokenLineBeat spoken &&
                NameVariants.ContainsToken(spoken.SharedContent.Text))
            {
                problems.Add($"{context} is a shared-take SpokenLineBeat and cannot contain {NameVariants.Token}.");
            }

            if (!(beat is PatientSessionBeat patientSession))
                continue;

            for (int patientIndex = 0; patientIndex < patientSession.Roster.Count; patientIndex++)
            {
                PatientDefinition patient = patientSession.Roster[patientIndex];
                if (patient != null && NameVariants.ContainsToken(patient.Content.Text))
                {
                    problems.Add(
                        $"{context}, patient '{patient.DisplayName}' has one shared take and cannot contain {NameVariants.Token}.");
                }
            }
        }
    }

    private static void ValidateLocationsAndDocumentSequences(
        NarrativeCatalog catalog,
        ValidationContext validationContext,
        ICollection<string> problems)
    {
        for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
        {
            ChapterDefinition chapter = catalog.Chapters[chapterIndex];
            if (chapter == null)
                continue;

            string chapterContext = $"Chapter {chapter.Index}";
            ValidateLocation(
                chapter.BaseScene,
                chapter.BaseSpawnPointId,
                chapter.Index,
                $"{chapterContext} base location",
                validationContext,
                problems);

            string currentScene = chapter.BaseScene;
            for (int beatIndex = 0; beatIndex < chapter.Beats.Count; beatIndex++)
            {
                NarrativeBeat beat = chapter.Beats[beatIndex];
                string beatContext = $"{chapterContext}, beat {beatIndex + 1}";
                if (beat is DocumentSequenceBeat documentSequence)
                {
                    ValidateDocumentSequence(
                        documentSequence.Document,
                        chapter.Index,
                        currentScene,
                        beatContext,
                        validationContext,
                        problems);
                }

                if (beat is GateBeat gate)
                {
                    ValidateTaskConditionsInScene(
                        gate.Condition,
                        chapter.Index,
                        currentScene,
                        beatContext,
                        validationContext,
                        problems);
                }

                if (beat is TravelBeat travel)
                {
                    ValidateLocation(
                        travel.TargetScene,
                        travel.SpawnPointId,
                        chapter.Index,
                        $"{beatContext} travel location",
                        validationContext,
                        problems);
                    currentScene = travel.TargetScene;
                }
            }
        }

        ChapterDefinition finalChapter = catalog.FinalChapter;
        if (finalChapter != null)
        {
            ValidateLocation(
                finalChapter.BaseScene,
                finalChapter.EndingSpawnPointId,
                finalChapter.Index,
                "final ending location",
                validationContext,
                problems);

            for (int endingIndex = 0; endingIndex < catalog.Endings.Count; endingIndex++)
            {
                EndingDefinition ending = catalog.Endings[endingIndex];
                if (ending != null)
                {
                    ValidateEndingLocations(
                        ending,
                        finalChapter,
                        validationContext,
                        problems);
                }
            }
        }
    }

    private static void ValidateTaskConditionsInScene(
        BeatCondition condition,
        int chapterIndex,
        string sceneName,
        string context,
        ValidationContext validationContext,
        ICollection<string> problems)
    {
        if (condition is TaskCondition task)
        {
            if (task.Objective == null || string.IsNullOrWhiteSpace(sceneName))
                return;

            if (!validationContext.Scenes.TryGet(
                    sceneName,
                    SceneDiagnosticKind.Catalog,
                    problems,
                    out SceneSnapshot scene))
            {
                return;
            }

            int eligibleReceivers = scene.CountTaskReceivers(task.Objective, chapterIndex);
            int required = task.RequiredCount > 0 ? task.RequiredCount : 1;
            if (eligibleReceivers < required)
            {
                problems.Add(
                    $"{context} requires task objective '{task.Objective.Id}', but scene '{sceneName}' " +
                    $"has {eligibleReceivers} reachable matching TaskReceiver(s) for Chapter {chapterIndex}.");
            }

            return;
        }

        IReadOnlyList<BeatCondition> children = null;
        if (condition is AllOfCondition all)
            children = all.Conditions;
        else if (condition is AnyOfCondition any)
            children = any.Conditions;

        if (children == null)
            return;

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] != null)
            {
                ValidateTaskConditionsInScene(
                    children[i],
                    chapterIndex,
                    sceneName,
                    context,
                    validationContext,
                    problems);
            }
        }
    }

    private static void ValidateEndingLocations(
        EndingDefinition ending,
        ChapterDefinition finalChapter,
        ValidationContext validationContext,
        ICollection<string> problems)
    {
        string currentScene = finalChapter.BaseScene;
        for (int beatIndex = 0; beatIndex < ending.Beats.Count; beatIndex++)
        {
            NarrativeBeat beat = ending.Beats[beatIndex];
            string context = $"Ending '{ending.Title}', beat {beatIndex + 1}";
            if (beat is DocumentSequenceBeat documentSequence)
            {
                ValidateDocumentSequence(
                    documentSequence.Document,
                    finalChapter.Index,
                    currentScene,
                    context,
                    validationContext,
                    problems);
            }

            if (beat is TravelBeat travel)
            {
                ValidateLocation(
                    travel.TargetScene,
                    travel.SpawnPointId,
                    finalChapter.Index,
                    $"{context} travel location",
                    validationContext,
                    problems);
                currentScene = travel.TargetScene;
            }
        }
    }

    private static void ValidateLocation(
        string sceneName,
        string spawnPointId,
        int chapterIndex,
        string context,
        ValidationContext validationContext,
        ICollection<string> problems)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            problems.Add($"{context} has no scene name.");
            return;
        }
        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            problems.Add($"{context} has no spawn-point id.");
            return;
        }

        if (!validationContext.Scenes.TryGet(
                sceneName,
                SceneDiagnosticKind.Catalog,
                problems,
                out SceneSnapshot scene))
        {
            return;
        }

        int matchingSpawns = scene.CountSpawns(spawnPointId, chapterIndex);
        if (matchingSpawns == 0)
            problems.Add($"{context} references missing spawn point '{spawnPointId}' in scene '{sceneName}'.");
        else if (matchingSpawns > 1)
            problems.Add($"{context} references ambiguous spawn point '{spawnPointId}' in scene '{sceneName}' ({matchingSpawns} matches).");
    }

    private static void ValidateDocumentSequence(
        DocumentDefinition document,
        int chapterIndex,
        string sceneName,
        string context,
        ValidationContext validationContext,
        ICollection<string> problems)
    {
        if (document == null)
        {
            problems.Add($"{context} has no document assigned.");
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            problems.Add($"{context} has no resolved scene for document '{document.Title}'.");
            return;
        }

        if (!validationContext.Scenes.TryGet(
                sceneName,
                SceneDiagnosticKind.Catalog,
                problems,
                out SceneSnapshot scene))
        {
            return;
        }

        if (!scene.HasReachableDocument(document, chapterIndex))
        {
            problems.Add(
                $"{context} waits for document '{document.Title}', but no reachable DocumentInteractable exists in scene '{sceneName}' for Chapter {chapterIndex}.");
        }
    }

}
