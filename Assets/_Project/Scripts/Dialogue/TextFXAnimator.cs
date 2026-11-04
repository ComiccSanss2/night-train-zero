using Subnet.Core;
using TMPro;
using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>
    /// Per-letter vertex animation for a TMP label: inline {tag} spans, a base effect for the whole line,
    /// and a springy pop-in for each letter as the typewriter reveals it.
    /// </summary>
    [DisallowMultipleComponent]
    public class TextFXAnimator : MonoBehaviour
    {
        public float shakeStrength = 1.8f;
        public float waveAmplitude = 4f;
        public float waveSpeed = 6f;
        public float popDuration = 0.16f;

        private TMP_Text label;
        private TextFX.Parsed parsed = new TextFX.Parsed();
        private TextEffect baseEffect;
        private float[] revealTimes = new float[0];
        private bool dirty;

        public TextFX.Parsed Parsed => parsed;
        public TMP_Text Label => label != null ? label : (label = GetComponent<TMP_Text>());

        /// <summary>Sets the text. revealAll=false: letters stay hidden until <see cref="Reveal"/> is called.</summary>
        public void SetText(string raw, bool revealAll, TextEffect effect)
        {
            parsed = TextFX.Parse(raw ?? string.Empty);
            baseEffect = effect;
            Label.text = parsed.text;
            Label.maxVisibleCharacters = revealAll ? 99999 : 0;
            Label.ForceMeshUpdate();
            int count = Label.textInfo.characterCount;
            revealTimes = new float[Mathf.Max(count, 1)];
            float now = GameTime.UnscaledTime;
            for (int i = 0; i < revealTimes.Length; i++)
            {
                // Untyped labels still get a quick left-to-right cascade.
                revealTimes[i] = revealAll ? now + i * 0.012f : float.MaxValue;
            }
            dirty = true;
        }

        public void SetBaseEffect(TextEffect effect)
        {
            baseEffect = effect;
            dirty = true;
        }

        /// <summary>Marks a character as just revealed (starts its pop-in).</summary>
        public void Reveal(int index)
        {
            if (index >= 0 && index < revealTimes.Length) revealTimes[index] = GameTime.UnscaledTime;
            dirty = true;
        }

        public void RevealAll()
        {
            float now = GameTime.UnscaledTime - popDuration;
            for (int i = 0; i < revealTimes.Length; i++)
            {
                if (revealTimes[i] == float.MaxValue) revealTimes[i] = now;
            }
            Label.maxVisibleCharacters = 99999;
            dirty = true;
        }

        public void Clear()
        {
            parsed = new TextFX.Parsed();
            revealTimes = new float[0];
            baseEffect = TextEffect.None;
        }

        private bool NeedsAnimation(float now)
        {
            if (baseEffect != TextEffect.None) return true;
            for (int i = 0; i < parsed.spans.Count; i++)
            {
                TextFX.Kind k = parsed.spans[i].kind;
                if (k == TextFX.Kind.Shake || k == TextFX.Kind.Wave || k == TextFX.Kind.Glitch || k == TextFX.Kind.Bounce) return true;
            }
            for (int i = 0; i < revealTimes.Length; i++)
            {
                if (revealTimes[i] != float.MaxValue && now - revealTimes[i] < popDuration * 2f) return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            if (Label == null || string.IsNullOrEmpty(Label.text)) return;
            float now = GameTime.UnscaledTime;
            if (!NeedsAnimation(now))
            {
                if (dirty) { Label.ForceMeshUpdate(); dirty = false; }
                return;
            }
            dirty = true;
            Animate(now);
        }

        private void Animate(float now)
        {
            Label.ForceMeshUpdate();
            TMP_TextInfo info = Label.textInfo;
            int max = Label.maxVisibleCharacters;
            bool colorsChanged = false;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible || i >= max) continue;

                Vector3 offset = Vector3.zero;
                float scale = 1f;
                float rotation = 0f;

                // Pop-in: letters spring up from slightly below and smaller.
                float revealed = i < revealTimes.Length ? revealTimes[i] : 0f;
                if (revealed != float.MaxValue)
                {
                    float t = (now - revealed) / popDuration;
                    if (t < 0f) { scale = 0f; }
                    else if (t < 1f)
                    {
                        bool big = parsed.Has(TextFX.Kind.Big, i);
                        float from = big ? 2.1f : 0.35f;
                        scale = Mathf.LerpUnclamped(from, 1f, Easing.Evaluate(big ? EaseType.EaseOutCubic : EaseType.EaseOutBack, t));
                        offset.y -= (1f - t) * (big ? 0f : 7f);
                        if (big) rotation = (1f - t) * 12f * (i % 2 == 0 ? 1f : -1f);
                    }
                }

                bool shake = baseEffect == TextEffect.Shake || parsed.Has(TextFX.Kind.Shake, i);
                bool wave = baseEffect == TextEffect.Wave || parsed.Has(TextFX.Kind.Wave, i);
                bool strongGlitch = parsed.Has(TextFX.Kind.Glitch, i);
                bool glitch = strongGlitch || baseEffect == TextEffect.Glitch;
                bool bounce = baseEffect == TextEffect.Bounce || parsed.Has(TextFX.Kind.Bounce, i);
                bool floaty = baseEffect == TextEffect.Float;

                if (shake)
                {
                    offset += new Vector3(
                        (Mathf.PerlinNoise(now * 25f, i * 1.37f) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(i * 2.11f, now * 25f) - 0.5f) * 2f, 0f) * shakeStrength;
                }
                if (wave) offset.y += Mathf.Sin(now * waveSpeed + i * 0.55f) * waveAmplitude;
                if (floaty) offset.y += Mathf.Sin(now * 2.2f + i * 0.35f) * 1.4f;
                if (bounce) offset.y += Mathf.Abs(Mathf.Sin(now * 7f + i * 0.5f)) * 5f;
                if (glitch)
                {
                    // Mostly still, then a burst of displaced letters a few times per second.
                    int bucket = Mathf.FloorToInt(now * 14f);
                    float roll = Hash(bucket * 31 + i * 7);
                    // Explicit {glitch} words are loud; a glitchy voice (the Algorithm) only crackles now and then.
                    float threshold = strongGlitch ? 0.8f : 0.975f;
                    if (roll > threshold)
                    {
                        offset += new Vector3((Hash(bucket + i) - 0.5f) * 10f, (Hash(bucket * 3 + i) - 0.5f) * 5f, 0f);
                        Color32 flicker = roll > (1f + threshold) * 0.5f ? new Color32(51, 255, 242, 255) : new Color32(255, 79, 216, 255);
                        Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                        int cv = ch.vertexIndex;
                        colors[cv] = colors[cv + 1] = colors[cv + 2] = colors[cv + 3] = flicker;
                        colorsChanged = true;
                    }
                }

                if (offset == Vector3.zero && Mathf.Approximately(scale, 1f) && rotation == 0f) continue;

                Vector3[] vertices = info.meshInfo[ch.materialReferenceIndex].vertices;
                int v = ch.vertexIndex;
                Vector3 center = (vertices[v] + vertices[v + 2]) * 0.5f;
                Quaternion rot = Quaternion.Euler(0f, 0f, rotation);
                for (int k = 0; k < 4; k++)
                {
                    vertices[v + k] = center + rot * ((vertices[v + k] - center) * scale) + offset;
                }
            }

            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                Mesh mesh = info.meshInfo[m].mesh;
                if (mesh == null) continue;
                mesh.vertices = info.meshInfo[m].vertices;
                if (colorsChanged) mesh.colors32 = info.meshInfo[m].colors32;
                Label.UpdateGeometry(mesh, m);
            }
        }

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}
