using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Hacking
{
    /// <summary>
    /// Procedural sprites for the fights (no texture assets needed): soft circles, glows, diamonds, stars,
    /// rings, shards, a heart for Leo's soul, and tileable grid / scanline textures for the arena.
    /// Generated once from signed-distance shapes, cached for the session.
    /// </summary>
    public static class HackSprites
    {
        public enum Shape { Square, Circle, Glow, Diamond, Star, Ring, Shard, Heart, Rounded }

        private static readonly Dictionary<Shape, Sprite> cache = new Dictionary<Shape, Sprite>();
        private static Texture2D grid;
        private static Texture2D lines;

        public static Sprite Get(Shape shape)
        {
            if (shape == Shape.Square) return null; // plain UI image
            if (cache.TryGetValue(shape, out Sprite sprite) && sprite != null) return sprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Hack_" + shape, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // p in [-1, 1]
                    Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float a = Alpha(shape, p);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            cache[shape] = sprite;
            return sprite;
        }

        private static float Edge(float distance) => Mathf.Clamp01(0.5f - distance * 20f); // ~1.5px anti-aliasing

        private static float Alpha(Shape shape, Vector2 p)
        {
            switch (shape)
            {
                case Shape.Circle: return Edge(p.magnitude - 0.9f);
                case Shape.Glow:
                {
                    float d = Mathf.Clamp01(1f - p.magnitude);
                    return d * d * d;
                }
                case Shape.Diamond: return Edge((Mathf.Abs(p.x) + Mathf.Abs(p.y)) * 0.7071f - 0.64f);
                case Shape.Ring:
                {
                    float d = Mathf.Abs(p.magnitude - 0.68f) - 0.2f;
                    return Edge(d);
                }
                case Shape.Shard: return Edge(Mathf.Abs(p.x) * 2.6f + Mathf.Abs(p.y) - 0.92f);
                case Shape.Rounded:
                {
                    Vector2 q = new Vector2(Mathf.Max(Mathf.Abs(p.x) - 0.55f, 0f), Mathf.Max(Mathf.Abs(p.y) - 0.55f, 0f));
                    return Edge(q.magnitude - 0.35f);
                }
                case Shape.Star:
                {
                    float angle = Mathf.Atan2(p.y, p.x) - Mathf.PI * 0.5f;
                    float r = p.magnitude;
                    float spikes = Mathf.Cos(angle * 5f) * 0.5f + 0.5f; // 1 at the five tips
                    float radius = Mathf.Lerp(0.45f, 0.95f, Mathf.Pow(spikes, 1.6f));
                    return Edge(r - radius);
                }
                case Shape.Heart:
                {
                    // Classic implicit heart, flipped so the point is at the bottom.
                    float x = p.x * 1.3f, y = p.y * 1.3f + 0.2f;
                    float v = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
                    return Edge(v * 0.6f);
                }
                default: return 1f;
            }
        }

        /// <summary>Tileable faint grid (for the arena floor).</summary>
        public static Texture2D Grid()
        {
            if (grid != null) return grid;
            const int size = 32;
            grid = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HackGrid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool line = x == 0 || y == 0;
                    bool dot = x == 16 && y == 16;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(line ? 60 : dot ? 90 : 0));
                }
            grid.SetPixels32(pixels);
            grid.Apply(false, true);
            return grid;
        }

        /// <summary>Tileable diagonal speed-lines (for the full-screen backdrop).</summary>
        public static Texture2D Lines()
        {
            if (lines != null) return lines;
            const int size = 64;
            lines = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HackLines", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int d = (x + y) % 32;
                    byte a = (byte)(d < 3 ? 40 : d < 5 ? 15 : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            lines.SetPixels32(pixels);
            lines.Apply(false, true);
            return lines;
        }
    }
}
