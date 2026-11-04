using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Subnet.Core
{
    /// <summary>
    /// Persistent player settings (PlayerPrefs-backed). Read by the SYSTEM CONFIG menu,
    /// the camera shake system and the audio mixer.
    /// </summary>
    public static class GameSettings
    {
        // Exposed parameter names expected on the AudioMixer (right-click a group's Volume → "Expose").
        public const string MixerMaster = "MasterVolume";
        public const string MixerMusic = "MusicVolume";
        public const string MixerSfx = "SFXVolume";

        private const string KeyMaster = "settings.masterVolume";
        private const string KeyMusic = "settings.musicVolume";
        private const string KeySfx = "settings.sfxVolume";
        private const string KeyShake = "settings.screenShake";
        private const string KeyFullscreen = "settings.fullscreen";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 0.8f);
            set => PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(value));
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyMusic, 0.7f);
            set => PlayerPrefs.SetFloat(KeyMusic, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KeySfx, 0.8f);
            set => PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(value));
        }

        /// <summary>Accessibility toggle: when false, every camera shake request is ignored.</summary>
        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(KeyShake, 1) == 1;
            set => PlayerPrefs.SetInt(KeyShake, value ? 1 : 0);
        }

        private const string KeyQuality = "settings.graphicsQuality";

        /// <summary>0 = LOW, 1 = MEDIUM, 2 = HIGH. Macs default to MEDIUM (Retina screens are expensive).</summary>
        public static int GraphicsQuality
        {
            get
            {
                bool mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
                return Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, mac ? 1 : 2), 0, 2);
            }
            set => PlayerPrefs.SetInt(KeyQuality, Mathf.Clamp(value, 0, 2));
        }

        public static string QualityName(int quality) => quality == 0 ? "LOW" : quality == 1 ? "MEDIUM" : "HIGH";

        /// <summary>
        /// Applies frame pacing and render cost: resolution scale, shadow distance, MSAA off,
        /// post-processing off on LOW. Called on every scene load and when the setting changes.
        /// </summary>
        public static void ApplyGraphics()
        {
            int q = GraphicsQuality;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = q == 0 ? 0.7f : q == 1 ? 0.85f : 1f;
                urp.shadowDistance = q == 0 ? 12f : q == 1 ? 25f : 40f;
                urp.msaaSampleCount = 1;
            }

            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.targetTexture != null) continue; // fight portrait feed keeps its own settings
                cam.allowMSAA = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = q > 0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeGraphics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyGraphics();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyGraphics();
            AudioListener.volume = MasterVolume;
            ApplySfxToScene();
        }

        /// <summary>Scales every sound-effect source (music is handled by the MusicDirector).</summary>
        public static void ApplySfxToScene()
        {
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.GetComponentInParent<Subnet.Audio.MusicDirector>() != null) continue;
                source.volume = SfxVolume;
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            set => PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
        }

        /// <summary>
        /// Pushes the stored volumes to <paramref name="mixer"/>. If no mixer is provided,
        /// master volume falls back to AudioListener.volume so the slider still works.
        /// </summary>
        public static void ApplyAudio(AudioMixer mixer)
        {
            if (mixer == null)
            {
                AudioListener.volume = MasterVolume;
                ApplySfxToScene();
                return;
            }

            mixer.SetFloat(MixerMaster, LinearToDecibels(MasterVolume));
            mixer.SetFloat(MixerMusic, LinearToDecibels(MusicVolume));
            mixer.SetFloat(MixerSfx, LinearToDecibels(SfxVolume));
        }

        /// <summary>Applies window mode.</summary>
        public static void ApplyDisplay()
        {
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>Converts a 0..1 slider value to mixer decibels (-80 dB at zero).</summary>
        public static float LinearToDecibels(float linear)
        {
            return linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
        }
    }
}
