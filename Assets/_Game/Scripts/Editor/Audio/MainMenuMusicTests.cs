using System.Collections.Generic;
using System.Reflection;
using Hortensia.Narrative;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class MainMenuMusicTests
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
        public void StageFor_ChangesOnlyAfterEachDreamIsComplete()
        {
            NarrativeCatalog catalog = CreateCatalog();
            var state = new NarrativeState(NameVariant.Laura, 3)
            {
                ChapterIndex = 1,
                BeatIndex = 2
            };

            Assert.That(
                MainMenuMusicSettings.StageFor(state, catalog),
                Is.EqualTo(MainMenuMusicStage.BeforeFirstDream),
                "The first dream is not complete until its exit travel finishes.");

            state.BeatIndex = 3;
            Assert.That(
                MainMenuMusicSettings.StageFor(state, catalog),
                Is.EqualTo(MainMenuMusicStage.AfterFirstDream));

            state.ChapterIndex = 4;
            state.BeatIndex = 1;
            Assert.That(
                MainMenuMusicSettings.StageFor(state, catalog),
                Is.EqualTo(MainMenuMusicStage.AfterFirstDream),
                "The second theme remains active while the second dream is running.");

            state.BeatIndex = 2;
            Assert.That(
                MainMenuMusicSettings.StageFor(state, catalog),
                Is.EqualTo(MainMenuMusicStage.AfterSecondDream));

            state.ChapterIndex = 5;
            state.BeatIndex = 0;
            Assert.That(
                MainMenuMusicSettings.StageFor(state, catalog),
                Is.EqualTo(MainMenuMusicStage.AfterSecondDream));
        }

        [Test]
        public void AuthoredSettings_ContainAllThreeDirectorThemes()
        {
            MainMenuMusicSettings settings = Resources.Load<MainMenuMusicSettings>(
                MainMenuMusicSettings.ResourcePath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.BeforeFirstDream, Is.Not.Null);
            Assert.That(settings.BeforeFirstDream.name, Is.EqualTo("Main_menu_1"));
            Assert.That(settings.AfterFirstDream, Is.Not.Null);
            Assert.That(settings.AfterFirstDream.name, Is.EqualTo("Main_menu_2"));
            Assert.That(settings.AfterSecondDream, Is.Not.Null);
            Assert.That(settings.AfterSecondDream.name, Is.EqualTo("Main_menu_3"));
            Assert.That(settings.VolumeScale, Is.EqualTo(0.7f).Within(0.0001f));
        }

        private NarrativeCatalog CreateCatalog()
        {
            ChapterDefinition firstDreamChapter = CreateChapter(
                1,
                CreateTravel("DreamGreenhouse"),
                new SpokenLineBeat(),
                CreateTravel("Merridew"),
                new SpokenLineBeat());
            ChapterDefinition secondDreamChapter = CreateChapter(
                4,
                CreateTravel("DreamGreenhouse"),
                new SequenceBeat());
            ChapterDefinition laterChapter = CreateChapter(5);
            NarrativeCatalog catalog = Track(ScriptableObject.CreateInstance<NarrativeCatalog>());
            SetField(
                catalog,
                "chapters",
                new List<ChapterDefinition>
                {
                    firstDreamChapter,
                    secondDreamChapter,
                    laterChapter
                });
            return catalog;
        }

        private ChapterDefinition CreateChapter(
            int index,
            params NarrativeBeat[] beats)
        {
            ChapterDefinition chapter = Track(ScriptableObject.CreateInstance<ChapterDefinition>());
            SetField(chapter, "index", index);
            SetField(chapter, "beats", new List<NarrativeBeat>(beats));
            return chapter;
        }

        private static TravelBeat CreateTravel(string targetScene)
        {
            var travel = new TravelBeat();
            SetField(travel, "targetScene", targetScene);
            return travel;
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
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
