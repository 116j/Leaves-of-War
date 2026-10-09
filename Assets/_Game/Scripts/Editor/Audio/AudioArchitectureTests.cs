using Hortensia.Runtime;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Hortensia.Editor.Tests
{
    public sealed class AudioArchitectureTests
    {
        [Test]
        public void LinearToDecibels_MapsSliderRangeWithoutInvalidValues()
        {
            Assert.That(AudioManager.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(AudioManager.LinearToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.001f));
            Assert.That(AudioManager.LinearToDecibels(0f), Is.EqualTo(AudioManager.MinimumDecibels));
            Assert.That(AudioManager.LinearToDecibels(-1f), Is.EqualTo(AudioManager.MinimumDecibels));
            Assert.That(AudioManager.LinearToDecibels(float.NaN), Is.EqualTo(AudioManager.MinimumDecibels));
        }

        [Test]
        public void Configuration_MapsEveryBusToItsOwnMixerGroup()
        {
            AudioRoutingSettings routingSettings = LoadRoutingSettings();

            Assert.That(routingSettings.IsConfigured, Is.True);
            Assert.That(routingSettings.GroupFor(AudioBus.Voice), Is.SameAs(routingSettings.VoiceGroup));
            Assert.That(routingSettings.GroupFor(AudioBus.Music), Is.SameAs(routingSettings.MusicGroup));
            Assert.That(
                routingSettings.GroupFor(AudioBus.SoundEffects),
                Is.SameAs(routingSettings.SoundEffectsGroup));
            Assert.That(routingSettings.VoiceGroup, Is.Not.SameAs(routingSettings.MusicGroup));
            Assert.That(routingSettings.VoiceGroup, Is.Not.SameAs(routingSettings.SoundEffectsGroup));
            Assert.That(routingSettings.MusicGroup, Is.Not.SameAs(routingSettings.SoundEffectsGroup));
        }

        [Test]
        public void Configuration_GroupsBelongToSharedMixer()
        {
            AudioRoutingSettings routingSettings = LoadRoutingSettings();

            AssertGroup(routingSettings.VoiceGroup, "Voice", routingSettings.Mixer);
            AssertGroup(routingSettings.MusicGroup, "Music", routingSettings.Mixer);
            AssertGroup(routingSettings.SoundEffectsGroup, "SFX", routingSettings.Mixer);
        }

        [Test]
        public void Configuration_ExposesOneVolumeParameterPerBus()
        {
            AudioRoutingSettings routingSettings = LoadRoutingSettings();

            Assert.That(
                routingSettings.Mixer.GetFloat(AudioRoutingSettings.VoiceVolumeParameter, out _),
                Is.True);
            Assert.That(
                routingSettings.Mixer.GetFloat(AudioRoutingSettings.MusicVolumeParameter, out _),
                Is.True);
            Assert.That(
                routingSettings.Mixer.GetFloat(AudioRoutingSettings.SoundEffectsVolumeParameter, out _),
                Is.True);
        }

        [Test]
        public void ExclusiveMusic_SuspendsAllOtherAudioAndRestoresListenerState()
        {
            bool originalListenerPause = AudioListener.pause;
            GameObject managerObject = null;
            AudioClip clip = null;
            try
            {
                AudioListener.pause = false;
                managerObject = new GameObject("Exclusive Music Test Audio Manager");
                AudioManager manager = managerObject.AddComponent<AudioManager>();
                AudioSource managedMusic = CreateManagedMusicSource(managerObject, manager);
                clip = AudioClip.Create("Transition Test", 64, 1, 8000, false);

                Assert.That(
                    manager.PlayExclusiveMusic(clip, loop: false),
                    Is.True);
                Assert.That(manager.IsExclusiveMusicActive, Is.True);
                Assert.That(AudioListener.pause, Is.True);

                Assert.That(managedMusic.ignoreListenerPause, Is.True);

                manager.EndExclusiveMusic(stopMusic: false);

                Assert.That(manager.IsExclusiveMusicActive, Is.False);
                Assert.That(AudioListener.pause, Is.False);
                Assert.That(managedMusic.ignoreListenerPause, Is.False);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                AudioListener.pause = originalListenerPause;
            }
        }

        [Test]
        public void StopMusic_CancelsExclusiveMusicWithoutUnpausingAnExistingPause()
        {
            bool originalListenerPause = AudioListener.pause;
            GameObject managerObject = null;
            AudioClip clip = null;
            try
            {
                AudioListener.pause = true;
                managerObject = new GameObject("Exclusive Music Cancellation Test Audio Manager");
                AudioManager manager = managerObject.AddComponent<AudioManager>();
                CreateManagedMusicSource(managerObject, manager);
                clip = AudioClip.Create("Cancelled Transition Test", 64, 1, 8000, false);

                Assert.That(manager.PlayExclusiveMusic(clip, loop: false), Is.True);
                manager.StopMusic();

                Assert.That(manager.IsExclusiveMusicActive, Is.False);
                Assert.That(
                    AudioListener.pause,
                    Is.True,
                    "Cancelling a transition must restore the pause state owned by another system.");
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                AudioListener.pause = originalListenerPause;
            }
        }

        [Test]
        public void StopMusicIfCurrent_DoesNotCancelAReplacementCue()
        {
            bool originalListenerPause = AudioListener.pause;
            GameObject managerObject = null;
            AudioClip menuClip = null;
            AudioClip transitionClip = null;
            try
            {
                AudioListener.pause = false;
                managerObject = new GameObject("Conditional Music Stop Test Audio Manager");
                AudioManager manager = managerObject.AddComponent<AudioManager>();
                AudioSource managedMusic = CreateManagedMusicSource(managerObject, manager);
                menuClip = AudioClip.Create("Menu Test", 64, 1, 8000, false);
                transitionClip = AudioClip.Create("Replacement Transition Test", 64, 1, 8000, false);

                manager.PlayMusic(menuClip);
                Assert.That(manager.PlayExclusiveMusic(transitionClip, loop: false), Is.True);

                manager.StopMusicIfCurrent(menuClip);

                Assert.That(managedMusic.clip, Is.SameAs(transitionClip));
                Assert.That(manager.IsExclusiveMusicActive, Is.True);

                manager.StopMusicIfCurrent(transitionClip);

                Assert.That(managedMusic.clip, Is.Null);
                Assert.That(manager.IsExclusiveMusicActive, Is.False);
            }
            finally
            {
                if (menuClip != null)
                    Object.DestroyImmediate(menuClip);
                if (transitionClip != null)
                    Object.DestroyImmediate(transitionClip);
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                AudioListener.pause = originalListenerPause;
            }
        }

        [Test]
        public void GameplayPause_AndExclusiveMusicReleaseOnlyTheirOwnPauseReason()
        {
            bool originalListenerPause = AudioListener.pause;
            GameObject managerObject = null;
            AudioClip clip = null;
            try
            {
                AudioListener.pause = false;
                managerObject = new GameObject("Overlapping Audio Pause Test Manager");
                AudioManager manager = managerObject.AddComponent<AudioManager>();
                AudioSource managedMusic = CreateManagedMusicSource(managerObject, manager);
                clip = AudioClip.Create("Paused Transition Test", 64, 1, 8000, false);

                Assert.That(manager.PlayExclusiveMusic(clip, loop: false), Is.True);
                Assert.That(managedMusic.ignoreListenerPause, Is.True);

                manager.SetGameplayAudioPaused(true);
                Assert.That(AudioListener.pause, Is.True);
                Assert.That(
                    managedMusic.ignoreListenerPause,
                    Is.False,
                    "The pause menu must also freeze the foreground transition cue.");

                manager.EndExclusiveMusic(stopMusic: false);
                Assert.That(
                    AudioListener.pause,
                    Is.True,
                    "Finishing a transition must not release the pause menu's audio pause.");

                manager.SetGameplayAudioPaused(false);
                Assert.That(AudioListener.pause, Is.False);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                AudioListener.pause = originalListenerPause;
            }
        }

        private static AudioSource CreateManagedMusicSource(
            GameObject managerObject,
            AudioManager manager)
        {
            var sourceObject = new GameObject("Music");
            sourceObject.transform.SetParent(managerObject.transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();

            FieldInfo field = typeof(AudioManager).GetField(
                "musicSource",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(manager, source);
            return source;
        }

        private static AudioRoutingSettings LoadRoutingSettings()
        {
            AudioRoutingSettings routingSettings =
                Resources.Load<AudioRoutingSettings>(AudioRoutingSettings.ResourcePath);
            Assert.That(
                routingSettings,
                Is.Not.Null,
                $"Missing Resources/{AudioRoutingSettings.ResourcePath}.");
            return routingSettings;
        }

        private static void AssertGroup(AudioMixerGroup group, string name, AudioMixer mixer)
        {
            Assert.That(group, Is.Not.Null);
            Assert.That(group.name, Is.EqualTo(name));
            Assert.That(group.audioMixer, Is.SameAs(mixer));
        }
    }
}
