using System;
using System.Collections.Generic;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class NarrativeCatalogValidatorTests
    {
        [Test]
        public void AuthoredCatalog_PassesStructuralValidation()
        {
            NarrativeCatalog catalog = AssetDatabase.LoadAssetAtPath<NarrativeCatalog>(
                "Assets/Resources/Narrative/NarrativeCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                NarrativeCatalogValidator.TryValidateStructure(catalog, out string error),
                Is.True,
                error);
        }

        [Test]
        public void TryValidateStructure_AcceptsCanonicalStructureAndOrdinalIds()
        {
            CatalogFixture fixture = CatalogFixture.Create();
            try
            {
                SetString(fixture.Flags[1], "id", "Flag_One");
                SetString(fixture.Documents[1], "id", "Document_One");

                bool valid = NarrativeCatalogValidator.TryValidateStructure(
                    fixture.Catalog,
                    out string error);

                Assert.That(valid, Is.True, error);
                Assert.That(error, Is.Empty);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void TryValidateStructure_RejectsNullCatalog()
        {
            bool valid = NarrativeCatalogValidator.TryValidateStructure(null, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("requires a NarrativeCatalog"));
        }

        [TestCase(ChapterFault.NullEntry, "Chapter entry 4 is missing")]
        [TestCase(ChapterFault.Reordered, "chapters must be authored in index order")]
        [TestCase(ChapterFault.DuplicateIndex, "Chapter index 1 appears more than once")]
        [TestCase(ChapterFault.MissingIndex, "missing Chapter index 7")]
        [TestCase(ChapterFault.OutOfRangeIndex, "out-of-range index 8")]
        [TestCase(ChapterFault.WrongFinalEntry, "final chapter entry must be Chapter 7")]
        public void TryValidateStructure_RejectsInvalidChapterStructure(
            ChapterFault fault,
            string expectedProblem)
        {
            CatalogFixture fixture = CatalogFixture.Create();
            try
            {
                ApplyChapterFault(fixture, fault);

                bool valid = NarrativeCatalogValidator.TryValidateStructure(
                    fixture.Catalog,
                    out string error);

                Assert.That(valid, Is.False);
                Assert.That(error, Does.Contain(expectedProblem));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [TestCase(EndingFault.NullEntry, "Ending entry 2 is missing")]
        [TestCase(EndingFault.BlankId, "Ending entry 1 has a blank id")]
        [TestCase(EndingFault.DuplicateId, "Ending id 'confession' appears more than once")]
        [TestCase(EndingFault.MissingConfession, "missing canonical ending id 'confession'")]
        [TestCase(EndingFault.WrongCase, "missing canonical ending id 'confession'")]
        [TestCase(EndingFault.UnexpectedId, "is not one of the three canonical ending ids")]
        [TestCase(EndingFault.WrongCount, "must contain exactly 3 endings")]
        public void TryValidateStructure_RejectsInvalidEndingIdentities(
            EndingFault fault,
            string expectedProblem)
        {
            CatalogFixture fixture = CatalogFixture.Create();
            try
            {
                ApplyEndingFault(fixture, fault);

                bool valid = NarrativeCatalogValidator.TryValidateStructure(
                    fixture.Catalog,
                    out string error);

                Assert.That(valid, Is.False);
                Assert.That(error, Does.Contain(expectedProblem));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void TryValidateStructure_AllowsCanonicalEndingsInAnyCatalogOrder()
        {
            CatalogFixture fixture = CatalogFixture.Create();
            try
            {
                fixture.Endings.Reverse();
                SetReferences(fixture.Catalog, "endings", fixture.Endings);

                bool valid = NarrativeCatalogValidator.TryValidateStructure(
                    fixture.Catalog,
                    out string error);

                Assert.That(valid, Is.True, error);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [TestCase(IdentityCollection.Flags, IdentityFault.NullEntry, "Flag entry 2 is missing")]
        [TestCase(IdentityCollection.Flags, IdentityFault.BlankId, "Flag entry 2 has a blank id")]
        [TestCase(IdentityCollection.Flags, IdentityFault.DuplicateId, "Flag id 'flag_one' appears more than once")]
        [TestCase(IdentityCollection.Documents, IdentityFault.NullEntry, "Document entry 2 is missing")]
        [TestCase(IdentityCollection.Documents, IdentityFault.BlankId, "Document entry 2 has a blank id")]
        [TestCase(IdentityCollection.Documents, IdentityFault.DuplicateId, "Document id 'document_one' appears more than once")]
        [TestCase(IdentityCollection.TaskObjectives, IdentityFault.NullEntry, "Task objective entry 2 is missing")]
        [TestCase(IdentityCollection.TaskObjectives, IdentityFault.BlankId, "Task objective entry 2 has a blank id")]
        [TestCase(IdentityCollection.TaskObjectives, IdentityFault.DuplicateId, "Task objective id 'unpacking' appears more than once")]
        public void TryValidateStructure_RejectsInvalidStableIdentities(
            IdentityCollection collection,
            IdentityFault fault,
            string expectedProblem)
        {
            CatalogFixture fixture = CatalogFixture.Create();
            try
            {
                ApplyIdentityFault(fixture, collection, fault);

                bool valid = NarrativeCatalogValidator.TryValidateStructure(
                    fixture.Catalog,
                    out string error);

                Assert.That(valid, Is.False);
                Assert.That(error, Does.Contain(expectedProblem));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void TryValidate_StopsAtStructuralErrorsBeforeSceneInspection()
        {
            CatalogFixture fixture = CatalogFixture.Create();
            try
            {
                SetString(fixture.Endings[0], "id", string.Empty);

                bool valid = NarrativeCatalogValidator.TryValidate(fixture.Catalog, out string error);

                Assert.That(valid, Is.False);
                Assert.That(error, Does.Contain("Ending entry 1 has a blank id"));
                Assert.That(error, Does.Not.Contain("base location"));
                Assert.That(error, Does.Not.Contain("Build Settings"));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void ApplyChapterFault(CatalogFixture fixture, ChapterFault fault)
        {
            switch (fault)
            {
                case ChapterFault.NullEntry:
                    fixture.Chapters[3] = null;
                    break;
                case ChapterFault.Reordered:
                    ChapterDefinition first = fixture.Chapters[0];
                    fixture.Chapters[0] = fixture.Chapters[1];
                    fixture.Chapters[1] = first;
                    break;
                case ChapterFault.DuplicateIndex:
                    SetInt(fixture.Chapters[1], "index", 1);
                    break;
                case ChapterFault.MissingIndex:
                    fixture.Chapters.RemoveAt(fixture.Chapters.Count - 1);
                    break;
                case ChapterFault.OutOfRangeIndex:
                    SetInt(fixture.Chapters[6], "index", 8);
                    break;
                case ChapterFault.WrongFinalEntry:
                    ChapterDefinition last = fixture.Chapters[6];
                    fixture.Chapters[6] = fixture.Chapters[5];
                    fixture.Chapters[5] = last;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
            }

            SetReferences(fixture.Catalog, "chapters", fixture.Chapters);
        }

        private static void ApplyEndingFault(CatalogFixture fixture, EndingFault fault)
        {
            switch (fault)
            {
                case EndingFault.NullEntry:
                    fixture.Endings[1] = null;
                    break;
                case EndingFault.BlankId:
                    SetString(fixture.Endings[0], "id", "   ");
                    break;
                case EndingFault.DuplicateId:
                    SetString(fixture.Endings[1], "id", "confession");
                    break;
                case EndingFault.MissingConfession:
                    SetString(fixture.Endings[0], "id", "renunciation");
                    break;
                case EndingFault.WrongCase:
                    SetString(fixture.Endings[0], "id", "Confession");
                    break;
                case EndingFault.UnexpectedId:
                    SetString(fixture.Endings[2], "id", "release");
                    break;
                case EndingFault.WrongCount:
                    fixture.Endings.RemoveAt(fixture.Endings.Count - 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
            }

            SetReferences(fixture.Catalog, "endings", fixture.Endings);
        }

        private static void ApplyIdentityFault(
            CatalogFixture fixture,
            IdentityCollection collection,
            IdentityFault fault)
        {
            if (collection == IdentityCollection.Flags)
            {
                switch (fault)
                {
                    case IdentityFault.NullEntry:
                        fixture.Flags[1] = null;
                        break;
                    case IdentityFault.BlankId:
                        SetString(fixture.Flags[1], "id", "   ");
                        break;
                    case IdentityFault.DuplicateId:
                        SetString(fixture.Flags[1], "id", "flag_one");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
                }

                SetReferences(fixture.Catalog, "flags", fixture.Flags);
                return;
            }

            if (collection == IdentityCollection.TaskObjectives)
            {
                switch (fault)
                {
                    case IdentityFault.NullEntry:
                        fixture.TaskObjectives[1] = null;
                        break;
                    case IdentityFault.BlankId:
                        SetString(fixture.TaskObjectives[1], "id", "   ");
                        break;
                    case IdentityFault.DuplicateId:
                        SetString(fixture.TaskObjectives[1], "id", "unpacking");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
                }

                SetReferences(fixture.Catalog, "taskObjectives", fixture.TaskObjectives);
                return;
            }

            switch (fault)
            {
                case IdentityFault.NullEntry:
                    fixture.Documents[1] = null;
                    break;
                case IdentityFault.BlankId:
                    SetString(fixture.Documents[1], "id", string.Empty);
                    break;
                case IdentityFault.DuplicateId:
                    SetString(fixture.Documents[1], "id", "document_one");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
            }

            SetReferences(fixture.Catalog, "documents", fixture.Documents);
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences<T>(
            UnityEngine.Object target,
            string propertyName,
            IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public enum ChapterFault
        {
            NullEntry,
            Reordered,
            DuplicateIndex,
            MissingIndex,
            OutOfRangeIndex,
            WrongFinalEntry
        }

        public enum EndingFault
        {
            NullEntry,
            BlankId,
            DuplicateId,
            MissingConfession,
            WrongCase,
            UnexpectedId,
            WrongCount
        }

        public enum IdentityCollection
        {
            Flags,
            Documents,
            TaskObjectives
        }

        public enum IdentityFault
        {
            NullEntry,
            BlankId,
            DuplicateId
        }

        private sealed class CatalogFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> ownedObjects =
                new List<UnityEngine.Object>();

            private CatalogFixture()
            {
            }

            public NarrativeCatalog Catalog { get; private set; }
            public List<ChapterDefinition> Chapters { get; } =
                new List<ChapterDefinition>();
            public List<EndingDefinition> Endings { get; } =
                new List<EndingDefinition>();
            public List<FlagId> Flags { get; } = new List<FlagId>();
            public List<TaskObjective> TaskObjectives { get; } = new List<TaskObjective>();
            public List<DocumentDefinition> Documents { get; } =
                new List<DocumentDefinition>();

            public static CatalogFixture Create()
            {
                var fixture = new CatalogFixture();
                fixture.Catalog = fixture.CreateObject<NarrativeCatalog>();

                for (int index = 1; index <= 7; index++)
                {
                    ChapterDefinition chapter = fixture.CreateObject<ChapterDefinition>();
                    SetInt(chapter, "index", index);
                    fixture.Chapters.Add(chapter);
                }

                string[] endingIds = { "confession", "denial", "sacrifice" };
                for (int i = 0; i < endingIds.Length; i++)
                {
                    EndingDefinition ending = fixture.CreateObject<EndingDefinition>();
                    SetString(ending, "id", endingIds[i]);
                    fixture.Endings.Add(ending);
                }

                string[] flagIds = { "flag_one", "flag_two" };
                for (int i = 0; i < flagIds.Length; i++)
                {
                    FlagId flag = fixture.CreateObject<FlagId>();
                    SetString(flag, "id", flagIds[i]);
                    fixture.Flags.Add(flag);
                }

                string[] documentIds = { "document_one", "document_two" };
                for (int i = 0; i < documentIds.Length; i++)
                {
                    DocumentDefinition document = fixture.CreateObject<DocumentDefinition>();
                    SetString(document, "id", documentIds[i]);
                    fixture.Documents.Add(document);
                }

                string[] taskObjectiveIds = { "unpacking", "gardening", "maze_tidying" };
                for (int i = 0; i < taskObjectiveIds.Length; i++)
                {
                    TaskObjective objective = fixture.CreateObject<TaskObjective>();
                    SetString(objective, "id", taskObjectiveIds[i]);
                    fixture.TaskObjectives.Add(objective);
                }

                SetReferences(fixture.Catalog, "chapters", fixture.Chapters);
                SetReferences(fixture.Catalog, "endings", fixture.Endings);
                SetReferences(fixture.Catalog, "flags", fixture.Flags);
                SetReferences(fixture.Catalog, "taskObjectives", fixture.TaskObjectives);
                SetReferences(fixture.Catalog, "documents", fixture.Documents);
                return fixture;
            }

            public void Dispose()
            {
                for (int i = ownedObjects.Count - 1; i >= 0; i--)
                {
                    if (ownedObjects[i] != null)
                        UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            private T CreateObject<T>() where T : ScriptableObject
            {
                T created = ScriptableObject.CreateInstance<T>();
                ownedObjects.Add(created);
                return created;
            }
        }
    }
}
