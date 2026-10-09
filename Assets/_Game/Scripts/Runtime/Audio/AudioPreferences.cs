using System;
using UnityEngine;

namespace Hortensia.Runtime
{
    public static class AudioPreferences
    {
        private const string VoiceVolumeKey = "Hortensia.Audio.VoiceVolume";
        private const string MusicVolumeKey = "Hortensia.Audio.MusicVolume";
        private const string SoundEffectsVolumeKey = "Hortensia.Audio.SoundEffectsVolume";
        private const string VideoVolumeKey = "Hortensia.Audio.VideoVolume";

        public static float LoadVolume(AudioBus bus, float defaultValue)
        {
            float fallback = Sanitize(defaultValue, 1f);
            float stored = PlayerPrefs.GetFloat(KeyFor(bus), fallback);
            return Sanitize(stored, fallback);
        }

        public static void StoreVolume(AudioBus bus, float value)
        {
            PlayerPrefs.SetFloat(KeyFor(bus), Sanitize(value, 1f));
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        private static string KeyFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Voice:
                    return VoiceVolumeKey;
                case AudioBus.Music:
                    return MusicVolumeKey;
                case AudioBus.SoundEffects:
                    return SoundEffectsVolumeKey;
                case AudioBus.Video:
                    return VideoVolumeKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }
        }

        private static float Sanitize(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = fallback;

            return Mathf.Clamp01(value);
        }
    }
}