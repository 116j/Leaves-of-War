using NUnit.Framework;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class DreamTransitionSettingsTests
    {
        [TestCase(1, "Wisterleigh Hall, the 4th of November, 1899.", "first_dream_transition")]
        [TestCase(4, "THE SECOND DREAM", "second_dream_transition")]
        [TestCase(6, "THE THIRD DREAM", "third_dream_transition")]
        public void DreamScene_UsesTheAuthoredChapterCue(
            int chapterIndex,
            string cardText,
            string clipName)
        {
            DreamTransitionSettings settings = LoadSettings();

            Assert.That(
                settings.TryGetCue(
                    chapterIndex,
                    "DreamGreenhouse",
                    DreamTransitionTrigger.OnEnterScene,
                    out DreamTransitionCue cue),
                Is.True);
            Assert.That(cue.CardText, Does.StartWith(cardText));
            Assert.That(cue.Music, Is.Not.Null);
            Assert.That(cue.Music.name, Is.EqualTo(clipName));
            Assert.That(cue.VolumeScale, Is.EqualTo(0.74692f).Within(0.0001f));
        }

        [Test]
        public void OrdinaryTravel_DoesNotReuseDreamTransitionMusic()
        {
            DreamTransitionSettings settings = LoadSettings();

            Assert.That(
                settings.TryGetCue(1, "Manor", DreamTransitionTrigger.OnEnterScene, out _),
                Is.False);
            Assert.That(
                settings.TryGetCue(3, "DreamGreenhouse", DreamTransitionTrigger.OnEnterScene, out _),
                Is.False);
        }

        [Test]
        public void TransitionFrame_ReusesTheMenuCornice()
        {
            Assert.That(Resources.Load<Texture2D>("MainMenu/Cornice"), Is.Not.Null);
        }

        private static DreamTransitionSettings LoadSettings()
        {
            DreamTransitionSettings settings =
                Resources.Load<DreamTransitionSettings>(DreamTransitionSettings.ResourcePath);
            Assert.That(
                settings,
                Is.Not.Null,
                $"Missing Resources/{DreamTransitionSettings.ResourcePath}.");
            return settings;
        }
    }
}
