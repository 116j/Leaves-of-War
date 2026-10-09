using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Hortensia.Runtime
{
    [CreateAssetMenu(menuName = "Hortensia/Audio Routing Settings", fileName = "AudioRoutingSettings")]
    public sealed class AudioRoutingSettings : ScriptableObject
    {
        public const string ResourcePath = "Audio/AudioRoutingSettings";
        public const string VoiceVolumeParameter = "VoiceVolume";
        public const string MusicVolumeParameter = "MusicVolume";
        public const string SoundEffectsVolumeParameter = "SfxVolume";
        public const string VideoVolumeParameter = "VideoVolume";

        [Header("Routing")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup voiceGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup soundEffectsGroup;
        [SerializeField] private AudioMixerGroup videoGroup;

        [Header("Defaults")]
        [SerializeField, Range(0f, 1f)] private float defaultVoiceVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float defaultSoundEffectsVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float defaultVideoVolume = 0.20f;

        public AudioMixer Mixer => mixer;
        public AudioMixerGroup VoiceGroup => voiceGroup;
        public AudioMixerGroup MusicGroup => musicGroup;
        public AudioMixerGroup SoundEffectsGroup => soundEffectsGroup;
        public AudioMixerGroup VideoGroup => videoGroup;

        public bool IsConfigured =>
            mixer != null &&
            voiceGroup != null &&
            musicGroup != null &&
            soundEffectsGroup != null &&
            videoGroup != null;

        public AudioMixerGroup GroupFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Voice:
                    return voiceGroup;
                case AudioBus.Music:
                    return musicGroup;
                case AudioBus.SoundEffects:
                    return soundEffectsGroup;
                case AudioBus.Video:
                    return videoGroup;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }
        }

        public string VolumeParameterFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Voice:
                    return VoiceVolumeParameter;
                case AudioBus.Music:
                    return MusicVolumeParameter;
                case AudioBus.SoundEffects:
                    return SoundEffectsVolumeParameter;
                case AudioBus.Video:
                    return VideoVolumeParameter;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }
        }

        public float DefaultVolumeFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Voice:
                    return defaultVoiceVolume;
                case AudioBus.Music:
                    return defaultMusicVolume;
                case AudioBus.SoundEffects:
                    return defaultSoundEffectsVolume;
                case AudioBus.Video:
                    return defaultVideoVolume;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }
        }
    }
}