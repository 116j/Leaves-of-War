using Hortensia.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class NarrativeRuntimeTests
    {
        [Test]
        public void NameVariants_SubstituteUsesTheSelectedDisplayName()
        {
            Assert.That(
                NameVariants.Substitute("My dear {name}.", NameVariant.Laura),
                Is.EqualTo("My dear Laura."));
            Assert.That(
                NameVariants.Substitute("My dear {name}.", NameVariant.Everie),
                Is.EqualTo("My dear Everie."));
            Assert.That(NameVariants.ContainsToken("My dear {name}."), Is.True);
        }

        [Test]
        public void LineChunker_PreservesBeatMarkersAsExplicitPauses()
        {
            var chunks = LineChunker.Split("The garden is waiting. (beat, quieter) Do not answer it.", 64);

            Assert.That(chunks, Has.Count.EqualTo(2));
            Assert.That(chunks[0].Text, Is.EqualTo("The garden is waiting."));
            Assert.That(chunks[0].PauseAfter, Is.True);
            Assert.That(chunks[1].Text, Is.EqualTo("Do not answer it."));
            Assert.That(chunks[1].PauseAfter, Is.False);
        }

        [Test]
        public void ChapterDressingGroup_DefaultMaskIncludesAllCanonicalChaptersOnly()
        {
            var group = new ChapterDressingGroup();

            Assert.That(group.IsActiveIn(1), Is.True);
            Assert.That(group.IsActiveIn(7), Is.True);
            Assert.That(group.IsActiveIn(0), Is.False);
            Assert.That(group.IsActiveIn(8), Is.False);
        }

        [Test]
        public void NarrativeCatalog_ResolvesChapterAndFinalChapterFromAuthoredOrder()
        {
            NarrativeCatalog catalog = ScriptableObject.CreateInstance<NarrativeCatalog>();
            ChapterDefinition first = ScriptableObject.CreateInstance<ChapterDefinition>();
            ChapterDefinition final = ScriptableObject.CreateInstance<ChapterDefinition>();

            try
            {
                SetInt(first, "index", 1);
                SetInt(final, "index", 7);
                SerializedObject serializedCatalog = new SerializedObject(catalog);
                SerializedProperty chapters = serializedCatalog.FindProperty("chapters");
                chapters.arraySize = 2;
                chapters.GetArrayElementAtIndex(0).objectReferenceValue = first;
                chapters.GetArrayElementAtIndex(1).objectReferenceValue = final;
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(catalog.ChapterAt(1), Is.SameAs(first));
                Assert.That(catalog.ChapterAt(7), Is.SameAs(final));
                Assert.That(catalog.FinalChapter, Is.SameAs(final));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(final);
            }
        }

        private static void SetInt(Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
