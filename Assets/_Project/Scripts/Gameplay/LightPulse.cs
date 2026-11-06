using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Makes a light breathe, flicker like a paper lantern, or blink like a ringing phone.
    /// Optional <see cref="stopFlag"/>: once that story flag is set the light settles at its base intensity.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightPulse : MonoBehaviour
    {
        public enum Mode { Breathe, Flicker, Blink }

        public Mode mode = Mode.Flicker;
        public float speed = 1f;
        [Range(0f, 1f)] public float depth = 0.35f;
        public string stopFlag = string.Empty;

        private Light lightSource;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            lightSource = GetComponent<Light>();
            baseIntensity = lightSource.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(stopFlag) && StoryFlags.Has(stopFlag))
            {
                lightSource.intensity = baseIntensity * (mode == Mode.Blink ? 0.35f : 1f);
                return;
            }
            float t = Time.time * speed + seed;
            float k;
            switch (mode)
            {
                case Mode.Breathe: k = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f); break;
                case Mode.Blink: k = Mathf.Repeat(t, 1f) < 0.5f ? 1f : 0f; break;
                default: k = Mathf.PerlinNoise(t * 3f, seed); break;
            }
            lightSource.intensity = baseIntensity * (1f - depth + depth * k);
        }
    }
}
