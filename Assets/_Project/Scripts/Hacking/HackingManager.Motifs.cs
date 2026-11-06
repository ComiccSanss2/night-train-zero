using System.Collections.Generic;
using Subnet.Core;
using Subnet.UI;
using UnityEngine;
using UnityEngine.UI;
using Shape = Subnet.Hacking.HackSprites.Shape;

namespace Subnet.Hacking
{
    /// <summary>
    /// Fight backdrop: big, faint copies of the enemy's own bullet art drift behind the arena (paper and clock hands
    /// for the Commuter, petals and tears for the Weeper, error icons for the Purge, cursors for the Algorithm), and
    /// each new wave opens with a shockwave ring in the wave's colour. They swell with every attack.
    /// </summary>
    public partial class HackingManager
    {
        private class Motif
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float spin;
            public float baseSize;
            public float phase;
        }

        private const int MotifCount = 11;
        private readonly List<Motif> motifs = new List<Motif>();
        private RectTransform motifLayer;
        private Image shockRing;
        private float shockAge = 99f;
        private float motifAlpha;
        private Color motifTint = Color.white;
        private static Sprite[] allSkins;

        private void EnsureMotifLayer()
        {
            if (motifLayer != null || canvas == null || background == null) return;
            motifLayer = RuntimeUI.Rect("Motifs", canvas.transform);
            RuntimeUI.Stretch(motifLayer);
            motifLayer.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
            for (int i = 0; i < MotifCount; i++)
            {
                Image img = RuntimeUI.Image("Motif" + i, motifLayer, Color.clear);
                img.raycastTarget = false;
                img.preserveAspect = true;
                motifs.Add(new Motif { rect = img.rectTransform, image = img });
            }
            shockRing = RuntimeUI.Image("ShockRing", motifLayer, Color.clear);
            shockRing.sprite = HackSprites.Get(Shape.Ring);
            shockRing.raycastTarget = false;
            RuntimeUI.Place(shockRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
        }

        /// <summary>Called when a wave starts: pick this enemy's art for this pattern and scatter it.</summary>
        private void RefreshMotifs(HackWave wave)
        {
            EnsureMotifLayer();
            if (motifLayer == null || wave == null) return;
            if (allSkins == null) allSkins = Resources.LoadAll<Sprite>("HackSkins");

            var pool = new List<Sprite>();
            string exact = skinPrefix + "_" + wave.pattern + "_";
            foreach (Sprite s in allSkins) if (!string.IsNullOrEmpty(skinPrefix) && s.name.StartsWith(exact) && !s.name.EndsWith("T")) pool.Add(s);
            if (pool.Count == 0) foreach (Sprite s in allSkins) if (!string.IsNullOrEmpty(skinPrefix) && s.name.StartsWith(skinPrefix + "_") && !s.name.EndsWith("T")) pool.Add(s);

            motifTint = PatternColor(wave.pattern);
            Vector2 half = ((RectTransform)canvas.transform).rect.size * 0.5f;
            for (int i = 0; i < motifs.Count; i++)
            {
                Motif m = motifs[i];
                bool hasArt = pool.Count > 0;
                m.image.sprite = hasArt ? pool[i % pool.Count] : HackSprites.Get(i % 2 == 0 ? Shape.Diamond : Shape.Ring);
                m.baseSize = Random.Range(110f, 230f);
                m.rect.sizeDelta = new Vector2(m.baseSize, m.baseSize);
                m.rect.anchoredPosition = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                m.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                m.velocity = new Vector2(Random.Range(-28f, 28f), Random.Range(-40f, -12f));
                if (wave.pattern == HackPattern.Bloom || wave.pattern == HackPattern.Fireworks) m.velocity.y = -m.velocity.y; // petals and sparks rise
                m.spin = Random.Range(-22f, 22f);
                m.phase = Random.value * 10f;
            }
            shockAge = 0f;
        }

        /// <summary>Per-frame drift (called from UpdateDecor).</summary>
        private void UpdateMotifs(float dt)
        {
            if (motifLayer == null) return;
            float target = waveActive && background != null ? background.color.a * 0.16f : 0f;
            motifAlpha = Mathf.MoveTowards(motifAlpha, target, dt * 0.4f);
            Vector2 half = ((RectTransform)canvas.transform).rect.size * 0.5f + new Vector2(140f, 140f);
            float t = GameTime.UnscaledTime;
            foreach (Motif m in motifs)
            {
                if (motifAlpha <= 0.001f) { m.image.color = Color.clear; continue; }
                Vector2 p = m.rect.anchoredPosition + (m.velocity * (1f + attackPulse * 2.5f)) * dt;
                p.x += Mathf.Sin(t * 0.7f + m.phase) * 12f * dt;
                if (p.y < -half.y) p.y = half.y;
                if (p.y > half.y) p.y = -half.y;
                if (p.x < -half.x) p.x = half.x;
                if (p.x > half.x) p.x = -half.x;
                m.rect.anchoredPosition = p;
                m.rect.localRotation *= Quaternion.Euler(0f, 0f, m.spin * dt);
                float s = m.baseSize * (1f + attackPulse * 0.25f + Mathf.Sin(t * 1.3f + m.phase) * 0.04f);
                m.rect.sizeDelta = new Vector2(s, s);
                Color c = Color.Lerp(Color.white, motifTint, 0.45f);
                c.a = motifAlpha * (0.7f + 0.3f * Mathf.Sin(t + m.phase));
                m.image.color = c;
            }

            if (shockRing != null)
            {
                shockAge += dt;
                const float life = 0.7f;
                if (shockAge < life && arena != null)
                {
                    float n = shockAge / life;
                    float e = 1f - (1f - n) * (1f - n);
                    shockRing.rectTransform.position = arena.position;
                    float size = Mathf.Lerp(120f, 1500f, e);
                    shockRing.rectTransform.sizeDelta = new Vector2(size, size);
                    Color c = motifTint;
                    c.a = (1f - n) * 0.55f;
                    shockRing.color = c;
                }
                else shockRing.color = Color.clear;
            }
        }
    }
}
