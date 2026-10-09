using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class DebugMenuTests
    {
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(6)]
        public void DreamShortcut_ResolvesTheAuthoredDreamTravel(int chapterIndex)
        {
            NarrativeCatalog catalog =
                Resources.Load<NarrativeCatalog>("Narrative/NarrativeCatalog");
            Assert.That(catalog, Is.Not.Null);

            ChapterDefinition chapter = catalog.ChapterAt(chapterIndex);
            object[] arguments = { chapter, -1 };
            bool found = (bool)FindDreamBeatMethod().Invoke(null, arguments);

            Assert.That(found, Is.True);
            int beatIndex = (int)arguments[1];
            Assert.That(beatIndex, Is.InRange(0, chapter.Beats.Count - 1));
            Assert.That(chapter.Beats[beatIndex], Is.TypeOf<TravelBeat>());
            Assert.That(
                ((TravelBeat)chapter.Beats[beatIndex]).TargetScene,
                Is.EqualTo("DreamGreenhouse"));
        }

        [Test]
        public void DreamShortcut_RejectsAChapterWithoutDreamTravel()
        {
            ChapterDefinition chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
            try
            {
                object[] arguments = { chapter, -1 };
                bool found = (bool)FindDreamBeatMethod().Invoke(null, arguments);

                Assert.That(found, Is.False);
                Assert.That(arguments[1], Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(chapter);
            }
        }

        private static MethodInfo FindDreamBeatMethod()
        {
            MethodInfo method = typeof(DebugMenu).GetMethod(
                "TryFindDreamBeatIndex",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
