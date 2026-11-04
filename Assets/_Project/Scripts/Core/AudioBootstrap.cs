using UnityEngine;

namespace Subnet.Core
{
    /// <summary>
    /// Keeps audio alive when the output device changes (headphones plugged in/out, Bluetooth,
    /// display audio...). On macOS this is what causes the FMOD message
    /// "failed to switch back to normal output" and total silence: Unity falls back to "no sound".
    /// Re-applying the current configuration re-opens the default output device.
    /// </summary>
    public static class AudioBootstrap
    {
        /// <summary>Raised after audio was re-initialized; looping sources should call Play() again.</summary>
        public static event System.Action AudioRestarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += OnConfigurationChanged;
            AudioListener.pause = false;
            if (AudioListener.volume <= 0.001f) AudioListener.volume = 1f;
        }

        private static void OnConfigurationChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged) return;
            Restart();
        }

        /// <summary>Re-opens the audio output with the current settings.</summary>
        public static void Restart()
        {
            AudioConfiguration config = AudioSettings.GetConfiguration();
            if (AudioSettings.Reset(config))
            {
                Debug.Log("[Audio] Output device changed: audio re-initialized.");
                AudioRestarted?.Invoke();
            }
            else
            {
                Debug.LogWarning("[Audio] Could not re-initialize audio. Check the macOS sound output device and restart Unity.");
            }
        }
    }
}
