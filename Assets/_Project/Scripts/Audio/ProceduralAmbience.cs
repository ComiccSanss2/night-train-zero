using Subnet.Core;
using UnityEngine;

namespace Subnet.Audio
{
    /// <summary>
    /// Real-time synthesized ambience, so every scene has atmosphere without audio files:
    /// - Rain: filtered noise + random droplets.
    /// - Hum: low subway / fridge drone with a slow rumble.
    /// - Wind: brown noise with gusts.
    /// - Pad: soft detuned chord (Subnet's pastel dream).
    /// - Crackle: glitchy static bursts (The Algorithm).
    /// Generated on the audio thread in OnAudioFilterRead. Replace or layer with real loops later.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralAmbience : MonoBehaviour
    {
        [Range(0f, 1f)] public float masterVolume = 0.5f;
        [Range(0f, 1f)] public float rain = 0.3f;
        [Range(0f, 1f)] public float hum = 0.3f;
        [Range(0f, 1f)] public float wind = 0f;
        [Range(0f, 1f)] public float pad = 0f;
        [Range(0f, 1f)] public float crackle = 0f;

        [Tooltip("Fundamental of the hum in Hz (50–60 = electrical, 35–45 = subway rumble).")]
        public float humFrequency = 48f;

        [Tooltip("Pad chord in Hz.")]
        public float[] padNotes = { 220f, 261.63f, 329.63f, 392f };

        [Tooltip("Seconds to fade in when the scene starts.")]
        public float fadeInSeconds = 3f;

        private AudioSource source;
        private System.Random random;
        private double sampleRate = 48000;
        private float fade;
        private float fadeStep;

        // Oscillator / filter state (audio thread only).
        private double humPhase1, humPhase2, humPhase3, lfoPhase, slowLfoPhase;
        private double[] padPhases = new double[8];
        private float rainLowpass, rainHighpassState, brown, dropEnvelope, crackleEnvelope;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            random = new System.Random(GetInstanceID());
            sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            fadeStep = fadeInSeconds > 0f ? (float)(1.0 / (fadeInSeconds * sampleRate)) : 1f;
            if (padNotes != null && padNotes.Length > padPhases.Length) padPhases = new double[padNotes.Length];

            // A silent looping clip guarantees the source keeps the filter running on every platform.
            int rate = (int)sampleRate;
            AudioClip silence = AudioClip.Create("ProceduralAmbience_Silence", rate, 1, rate, false);
            source.clip = silence;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
        }

        private void OnEnable()
        {
            AudioBootstrap.AudioRestarted += Restart;
            if (source != null && !source.isPlaying) source.Play();
        }

        private void OnDisable()
        {
            AudioBootstrap.AudioRestarted -= Restart;
        }

        private void Start()
        {
            if (!source.isPlaying) source.Play();
        }

        // The SFX slider, copied on the main thread for the audio thread (rain, wind and hum follow it live).
        private volatile float settingsScale = 1f;

        private void Update()
        {
            settingsScale = Core.GameSettings.SfxVolume;
            if (source != null && source.volume != 1f) source.volume = 1f;
        }

        private void Restart()
        {
            sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            if (source != null) source.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (random == null) return;
            double twoPi = System.Math.PI * 2.0;
            double inv = 1.0 / sampleRate;
            int frames = data.Length / channels;
            float[] notes = padNotes;

            for (int i = 0; i < frames; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);

                // Slow modulators.
                lfoPhase += 0.13 * inv * twoPi;
                slowLfoPhase += 0.031 * inv * twoPi;
                float lfo = (float)System.Math.Sin(lfoPhase);
                float slowLfo = (float)System.Math.Sin(slowLfoPhase);

                // Rain: band-limited noise + droplets.
                rainLowpass += (white - rainLowpass) * 0.35f;
                rainHighpassState += (rainLowpass - rainHighpassState) * 0.02f;
                float rainBed = (rainLowpass - rainHighpassState) * 0.55f;
                if (random.NextDouble() < 0.0009 * rain) dropEnvelope = 0.6f + (float)random.NextDouble() * 0.4f;
                dropEnvelope *= 0.9965f;
                float rainSignal = (rainBed + white * dropEnvelope * 0.35f) * rain;

                // Wind: brown noise with gusts.
                brown = (brown + 0.02f * white) / 1.02f;
                float windSignal = brown * 3.2f * wind * (0.55f + 0.45f * slowLfo);

                // Hum: three harmonics with a slow rumble.
                humPhase1 += humFrequency * inv * twoPi;
                humPhase2 += humFrequency * 2.0 * inv * twoPi;
                humPhase3 += humFrequency * 3.01 * inv * twoPi;
                float humSignal = ((float)System.Math.Sin(humPhase1) * 0.6f +
                                   (float)System.Math.Sin(humPhase2) * 0.22f +
                                   (float)System.Math.Sin(humPhase3) * 0.08f) * hum * (0.7f + 0.3f * lfo);

                // Pad: detuned chord with slow tremolo.
                float padSignal = 0f;
                if (pad > 0f && notes != null)
                {
                    for (int n = 0; n < notes.Length && n < padPhases.Length; n++)
                    {
                        double detune = 1.0 + 0.0015 * System.Math.Sin(slowLfoPhase * (n + 1));
                        padPhases[n] += notes[n] * detune * inv * twoPi;
                        padSignal += (float)System.Math.Sin(padPhases[n]) * (0.6f + 0.4f * (float)System.Math.Sin(lfoPhase + n));
                    }
                    padSignal *= 0.12f * pad;
                }

                // Crackle: rare static bursts.
                if (random.NextDouble() < 0.00004 * crackle) crackleEnvelope = 1f;
                crackleEnvelope *= 0.9994f;
                float crackleSignal = (random.NextDouble() < 0.15 ? white : 0f) * crackleEnvelope * crackle * 0.5f;

                // Global fade-in.
                if (fade < 1f) fade = Mathf.Min(1f, fade + fadeStep);

                float sample = (rainSignal + windSignal + humSignal + padSignal + crackleSignal) * masterVolume * settingsScale * fade;
                sample = sample / (1f + Mathf.Abs(sample)); // soft clip

                for (int c = 0; c < channels; c++) data[i * channels + c] = sample;
            }
        }
    }
}
