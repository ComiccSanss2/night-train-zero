using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>
    /// One typeface per voice. Drop free Google Fonts .ttf files into Assets/_Project/Resources/Fonts/ and they are
    /// picked up automatically by name (no renaming needed):
    ///   VT323-Regular.ttf            → Byte (and "???")
    ///   PermanentMarker-Regular.ttf  → Kira
    ///   Audiowide-Regular.ttf        → The Algorithm
    ///   SpecialElite-Regular.ttf     → enemies / people stuck in them / talking objects
    ///   PatrickHand-Regular.ttf      → Leo's thoughts
    /// You can also name a file after the role (Byte.ttf, Kira.ttf, Algorithm.ttf, Enemy.ttf, Thought.ttf).
    /// Without the files every voice still gets its own styling (case, spacing, slant, colour), so nothing breaks.
    /// Missing glyphs (accents) fall back to the default font.
    /// </summary>
    public static class DialogueFonts
    {
        public struct Look
        {
            public TMP_FontAsset font;   // null = keep the box's default font
            public FontStyles fontStyle;
            public float sizeScale;
            public float characterSpacing;
        }

        private static Dictionary<SpeakerStyle, TMP_FontAsset> cache;
        private static Font[] available;

        private static string[] Keywords(SpeakerStyle style)
        {
            switch (style)
            {
                case SpeakerStyle.Byte:
                case SpeakerStyle.Unknown: return new[] { "byte", "vt323" };
                case SpeakerStyle.Kira: return new[] { "kira", "permanentmarker" };
                case SpeakerStyle.Algorithm: return new[] { "algorithm", "audiowide" };
                case SpeakerStyle.Enemy:
                case SpeakerStyle.Object: return new[] { "enemy", "specialelite" };
                case SpeakerStyle.Thought: return new[] { "thought", "patrickhand" };
                default: return new string[0];
            }
        }

        public static Look Get(SpeakerStyle style, TMP_FontAsset defaultFont)
        {
            TMP_FontAsset font = Resolve(style, defaultFont);
            bool custom = font != null;
            var look = new Look { font = font, fontStyle = FontStyles.Normal, sizeScale = 1f, characterSpacing = 0f };

            switch (style)
            {
                case SpeakerStyle.Thought:
                    look.fontStyle = custom ? FontStyles.Normal : FontStyles.Italic;
                    look.sizeScale = custom ? 1.08f : 0.96f;
                    break;
                case SpeakerStyle.Byte:
                    // "i type in lowercase. it's a lifestyle."
                    look.fontStyle = FontStyles.LowerCase;
                    look.sizeScale = custom ? 1.3f : 1f;
                    look.characterSpacing = custom ? 0f : 2f;
                    break;
                case SpeakerStyle.Unknown:
                    look.fontStyle = FontStyles.LowerCase;
                    look.sizeScale = custom ? 1.3f : 1f;
                    look.characterSpacing = 6f;
                    break;
                case SpeakerStyle.Kira:
                    look.fontStyle = custom ? FontStyles.Normal : FontStyles.Bold | FontStyles.Italic;
                    look.sizeScale = custom ? 0.98f : 1f;
                    break;
                case SpeakerStyle.Algorithm:
                    look.fontStyle = custom ? FontStyles.UpperCase : FontStyles.UpperCase | FontStyles.Bold;
                    look.sizeScale = custom ? 0.9f : 0.96f;
                    look.characterSpacing = custom ? 4f : 10f;
                    break;
                case SpeakerStyle.Enemy:
                    look.characterSpacing = custom ? 0f : 3f;
                    break;
                case SpeakerStyle.Object:
                    look.fontStyle = custom ? FontStyles.Normal : FontStyles.Italic;
                    look.sizeScale = 0.94f;
                    break;
            }
            return look;
        }

        private static TMP_FontAsset Resolve(SpeakerStyle style, TMP_FontAsset defaultFont)
        {
            if (cache == null) cache = new Dictionary<SpeakerStyle, TMP_FontAsset>();
            if (cache.TryGetValue(style, out TMP_FontAsset cached)) return cached;

            TMP_FontAsset result = null;
            string[] keys = Keywords(style);
            if (keys.Length > 0)
            {
                if (available == null) available = LoadAvailableFonts();
                foreach (string key in keys)
                {
                    foreach (Font font in available)
                    {
                        if (font == null || !Normalize(font.name).Contains(key)) continue;
                        result = Create(font, defaultFont);
                        break;
                    }
                    if (result != null) break;
                }
            }
            cache[style] = result;
            return result;
        }

        private static Font[] LoadAvailableFonts()
        {
            var list = new List<Font>();
            // Fonts anywhere in the project, collected by the builder.
            DialogueFontLibrary library = Resources.Load<DialogueFontLibrary>("DialogueFontLibrary");
            if (library != null && library.fonts != null)
            {
                foreach (Font f in library.fonts) if (f != null) list.Add(f);
            }
            // Plus anything dropped into Resources/Fonts.
            list.AddRange(Resources.LoadAll<Font>("Fonts"));
            return list.ToArray();
        }

        private static TMP_FontAsset Create(Font font, TMP_FontAsset fallback)
        {
            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset == null) return null;
                asset.name = font.name + " (Runtime SDF)";
                if (fallback != null)
                {
                    if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    asset.fallbackFontAssetTable.Add(fallback);
                }
                return asset;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DialogueFonts] Could not use font '{font.name}': {e.Message}");
                return null;
            }
        }

        private static string Normalize(string s) => s.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache = null;
            available = null;
        }
    }
}
