using System;
using System.Collections.Generic;
using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class GameplayLayerDataTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void TaskCondition_UsesRegisteredTotalUnlessSubsetIsAuthored()
        {
            TaskObjective objective = CreateObjective("gardening");
            var state = new NarrativeState(NameVariant.Laura, 0);
            state.Tasks.Register(objective, 6);

            var allItems = new TaskCondition();
            SetField(allItems, "objective", objective);
            var firstFour = new TaskCondition();
            SetField(firstFour, "objective", objective);
            SetField(firstFour, "requiredCount", 4);

            for (int i = 0; i < 4; i++)
                state.Tasks.Report(objective);

            Assert.That(state.Tasks.CountFor(objective), Is.EqualTo(4));
            Assert.That(state.Tasks.RequiredFor(objective, 0), Is.EqualTo(6));
            Assert.That(allItems.IsSatisfied(state), Is.False);
            Assert.That(firstFour.IsSatisfied(state), Is.True);

            state.Tasks.Report(objective);
            state.Tasks.Report(objective);

            Assert.That(allItems.IsSatisfied(state), Is.True);
        }

        [Test]
        public void TaskCondition_DoesNotOpenForMissingOrUnregisteredObjective()
        {
            TaskObjective objective = CreateObjective("gardening");
            var state = new NarrativeState(NameVariant.Laura, 0);
            var missing = new TaskCondition();
            var unregistered = new TaskCondition();
            SetField(unregistered, "objective", objective);

            Assert.That(missing.IsSatisfied(state), Is.False);
            Assert.That(unregistered.IsSatisfied(state), Is.False);

            state.Tasks.RestoreCount(objective.Id, 1);
            Assert.That(unregistered.IsSatisfied(state), Is.False);

            state.Tasks.Register(objective, 1);
            Assert.That(unregistered.IsSatisfied(state), Is.True);
        }

        [Test]
        public void SaveData_RoundTripsTaskCountsByCataloguedStableId()
        {
            TaskObjective objective = CreateObjective("gardening");
            NarrativeCatalog catalog = CreateCatalog(objective);
            var original = new NarrativeState(NameVariant.Everie, 3)
            {
                ChapterIndex = 1,
                BeatIndex = 0
            };
            original.Tasks.RestoreCount(objective.Id, 4);

            SaveGameData data = SaveGameStore.Capture(original);

            Assert.That(data.taskCounts, Has.Count.EqualTo(1));
            Assert.That(data.taskCounts[0].objectiveId, Is.EqualTo("gardening"));
            Assert.That(data.taskCounts[0].count, Is.EqualTo(4));
            Assert.That(TryRestore(catalog, data, out NarrativeState restored), Is.True);
            Assert.That(restored.Tasks.CountFor(objective), Is.EqualTo(4));
        }

        [TestCase("unknown", 1)]
        [TestCase("gardening", -1)]
        public void SaveData_RejectsUnknownOrNegativeTaskCounts(string objectiveId, int count)
        {
            TaskObjective objective = CreateObjective("gardening");
            NarrativeCatalog catalog = CreateCatalog(objective);
            SaveGameData data = ValidData();
            data.taskCounts.Add(new SavedTaskCount { objectiveId = objectiveId, count = count });

            Assert.That(TryRestore(catalog, data, out _), Is.False);
        }

        [Test]
        public void SaveData_RejectsDuplicateTaskObjectiveIds()
        {
            TaskObjective objective = CreateObjective("gardening");
            NarrativeCatalog catalog = CreateCatalog(objective);
            SaveGameData data = ValidData();
            data.taskCounts.Add(new SavedTaskCount { objectiveId = objective.Id, count = 1 });
            data.taskCounts.Add(new SavedTaskCount { objectiveId = objective.Id, count = 2 });

            Assert.That(TryRestore(catalog, data, out _), Is.False);
        }

        [Test]
        public void CompiledDocument_DerivesReadDocumentsAndCompletedPatientSessionsInOrder()
        {
            FlagId readFlag = Track(ScriptableObject.CreateInstance<FlagId>());
            SetField(readFlag, "id", "osmund_letter_read");
            DocumentDefinition authoredDocument = Track(
                ScriptableObject.CreateInstance<DocumentDefinition>());
            SetField(authoredDocument, "title", "Osmund's Letter");
            SetField(authoredDocument, "pages", new List<string> { "The garden remembers." });
            SetField(authoredDocument, "setWhenRead", readFlag);

            PatientDefinition patient = Track(ScriptableObject.CreateInstance<PatientDefinition>());
            SetField(patient, "displayName", "Mr Norbury");
            object boxedContent = new LineContent();
            SetField(boxedContent, "text", "I dreamt of a glass house.");
            SetField(patient, "content", boxedContent);
            var session = new PatientSessionBeat();
            SetField(session, "roster", new List<PatientDefinition> { patient });

            ChapterDefinition chapter = Track(ScriptableObject.CreateInstance<ChapterDefinition>());
            SetField(chapter, "index", 4);
            SetField(chapter, "beats", new List<NarrativeBeat> { session });
            NarrativeCatalog catalog = Track(ScriptableObject.CreateInstance<NarrativeCatalog>());
            SetField(catalog, "chapters", new List<ChapterDefinition> { chapter });
            SetField(catalog, "documents", new List<DocumentDefinition> { authoredDocument });

            var documents = new DocumentsReadSection();
            SetField(documents, "heading", "OSMUND'S WRITINGS");
            var patients = new PatientsHeardSection();
            SetField(patients, "heading", "PATIENT TRANSCRIPTS");
            CompiledDocument compiled = Track(ScriptableObject.CreateInstance<CompiledDocument>());
            SetField(compiled, "sections", new List<DocumentSection> { documents, patients });

            var state = new NarrativeState(NameVariant.Laura, 0)
            {
                ChapterIndex = 4,
                BeatIndex = 1
            };
            state.SetFlag(readFlag);

            IReadOnlyList<string> pages = compiled.PagesFor(state, catalog);

            Assert.That(pages, Is.EqualTo(new[]
            {
                "OSMUND'S WRITINGS\n\nOsmund's Letter\n\nThe garden remembers.",
                "PATIENT TRANSCRIPTS\n\nMr Norbury\n\nI dreamt of a glass house."
            }));
            Assert.That(compiled.Narration, Is.Null);
            Assert.That(compiled.SetWhenRead, Is.Null);
        }

        private TaskObjective CreateObjective(string id)
        {
            TaskObjective objective = Track(ScriptableObject.CreateInstance<TaskObjective>());
            SetField(objective, "id", id);
            return objective;
        }

        private NarrativeCatalog CreateCatalog(TaskObjective objective)
        {
            ChapterDefinition chapter = Track(ScriptableObject.CreateInstance<ChapterDefinition>());
            SetField(chapter, "index", 1);
            NarrativeCatalog catalog = Track(ScriptableObject.CreateInstance<NarrativeCatalog>());
            SetField(catalog, "chapters", new List<ChapterDefinition> { chapter });
            SetField(catalog, "taskObjectives", new List<TaskObjective> { objective });
            return catalog;
        }

        private static SaveGameData ValidData()
        {
            return new SaveGameData
            {
                version = SaveGameStore.CurrentVersion,
                chosenName = NameVariant.Laura.ToString(),
                chapterIndex = 1,
                beatIndex = 0
            };
        }

        private static bool TryRestore(
            NarrativeCatalog catalog,
            SaveGameData data,
            out NarrativeState state)
        {
            MethodInfo method = typeof(SaveGameStore).GetMethod(
                "TryRestore",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { catalog, data, null, null, null };
            bool restored = (bool)method.Invoke(null, arguments);
            state = arguments[2] as NarrativeState;
            return restored;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
