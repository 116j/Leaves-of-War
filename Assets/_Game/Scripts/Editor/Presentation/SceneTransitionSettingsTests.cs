using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class SceneTransitionSettingsTests
    {
        [Test]
        public void EveryNormalNarrativeArrival_HasOneDescriptiveCard()
        {
            SceneTransitionSettings settings = LoadSettings();

            foreach (Arrival expected in NormalArrivals)
            {
                var matches = new List<SceneTransitionCard>();
                foreach (SceneTransitionCard card in settings.Cards)
                {
                    if (card != null &&
                        card.ChapterIndex == expected.ChapterIndex &&
                        string.Equals(card.TargetSceneName, expected.SceneName, StringComparison.Ordinal) &&
                        string.Equals(card.SpawnPointId, expected.SpawnPointId, StringComparison.Ordinal))
                    {
                        matches.Add(card);
                    }
                }

                Assert.That(
                    matches,
                    Has.Count.EqualTo(1),
                    $"Expected exactly one arrival card for {expected}.");
                Assert.That(
                    matches[0].CardText,
                    Is.Not.Null.And.Not.Empty,
                    $"Arrival card for {expected} needs destination prose.");
            }
        }

        [Test]
        public void EndingDebugArrival_HasOneDescriptiveCard()
        {
            SceneTransitionSettings settings = LoadSettings();

            Assert.That(
                settings.TryGetCard(7, "Manor", "greenhouse", out SceneTransitionCard card),
                Is.True);
            Assert.That(card.CardText, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ArrivalCards_AreUniqueAndContainNoAudioReferences()
        {
            SceneTransitionSettings settings = LoadSettings();
            var keys = new HashSet<string>();

            foreach (SceneTransitionCard card in settings.Cards)
            {
                Assert.That(card, Is.Not.Null);
                string key = $"{card.ChapterIndex}|{card.TargetSceneName}|{card.SpawnPointId}";
                Assert.That(keys.Add(key), Is.True, $"Duplicate arrival-card key: {key}.");
            }

            foreach (FieldInfo field in typeof(SceneTransitionCard).GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.That(
                    typeof(AudioClip).IsAssignableFrom(field.FieldType),
                    Is.False,
                    $"Arrival cards must stay silent; move '{field.Name}' to DreamTransitionSettings instead.");
            }
        }

        private static SceneTransitionSettings LoadSettings()
        {
            SceneTransitionSettings settings =
                Resources.Load<SceneTransitionSettings>(SceneTransitionSettings.ResourcePath);
            Assert.That(
                settings,
                Is.Not.Null,
                $"Missing Resources/{SceneTransitionSettings.ResourcePath}.");
            return settings;
        }

        private readonly struct Arrival
        {
            public Arrival(int chapterIndex, string sceneName, string spawnPointId)
            {
                ChapterIndex = chapterIndex;
                SceneName = sceneName;
                SpawnPointId = spawnPointId;
            }

            public int ChapterIndex { get; }
            public string SceneName { get; }
            public string SpawnPointId { get; }

            public override string ToString() =>
                $"chapter {ChapterIndex} / {SceneName} / {SpawnPointId}";
        }

        private static readonly Arrival[] NormalArrivals =
        {
            new Arrival(1, "Manor", "front_gate"),
            new Arrival(1, "DreamGreenhouse", "dream_entry"),
            new Arrival(1, "Merridew", "willowet"),
            new Arrival(1, "Manor", "garden"),
            new Arrival(2, "Manor", "garden"),
            new Arrival(3, "Merridew", "graduation"),
            new Arrival(4, "TherapyOffice", "therapy_chair"),
            new Arrival(4, "Manor", "consulting_room"),
            new Arrival(4, "DreamGreenhouse", "dream_entry"),
            new Arrival(5, "TherapyOffice", "therapy_chair"),
            new Arrival(5, "Manor", "consulting_room"),
            new Arrival(6, "Manor", "entry_hall"),
            new Arrival(6, "Lynwarre", "examination"),
            new Arrival(6, "Manor", "bedroom"),
            new Arrival(6, "DreamGreenhouse", "dream_entry"),
            new Arrival(7, "Manor", "front_gate"),
        };
    }
}
