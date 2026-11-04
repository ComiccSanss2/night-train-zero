using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Subnet.Core;
using TMPro;
using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>
    /// Inline text effects written straight into dialogue strings (in every language), Undertale-style.
    ///
    ///   {shake}scared{/shake}   jittery letters
    ///   {wave}dreamy{/wave}     sine wave
    ///   {glitch}ERROR{/glitch}  jumps + colour flicker (the Algorithm)
    ///   {bounce}yay{/bounce}    happy hops
    ///   {big}NO{/big}           bigger letters that slam in (with a little box punch)
    ///   {small}sorry{/small}    tiny letters
    ///   {slow}...{/slow}        typed slower    {fast}...{/fast} typed faster
    ///   {p} / {p=1.2}           dramatic pause while typing
    ///   {quake}                 screen shake at that point
    ///
    /// Tags are removed before display; TMP rich text (&lt;color&gt;, &lt;i&gt;...) still works alongside.
    /// </summary>
    public static class TextFX
    {
        public enum Kind { Shake, Wave, Glitch, Bounce, Big, Small, Slow, Fast }

        public struct Span
        {
            public Kind kind;
            public int start; // first character index (TMP characterInfo index)
            public int end;   // exclusive
        }

        public struct Cue
        {
            public int index;     // fires right after this many characters are visible
            public float pause;   // seconds (0 = none)
            public bool quake;
        }

        public class Parsed
        {
            public string text = string.Empty;
            public readonly List<Span> spans = new List<Span>();
            public readonly List<Cue> cues = new List<Cue>();

            public bool Has(Kind kind, int index)
            {
                for (int i = 0; i < spans.Count; i++)
                {
                    if (spans[i].kind == kind && index >= spans[i].start && index < spans[i].end) return true;
                }
                return false;
            }
        }

        /// <summary>Parses our {tags}; returns display text (with TMP tags) plus spans and cues.</summary>
        public static Parsed Parse(string raw)
        {
            var result = new Parsed();
            if (string.IsNullOrEmpty(raw)) return result;

            var sb = new StringBuilder(raw.Length);
            var open = new Dictionary<Kind, int>();
            int visible = 0;
            int i = 0;
            while (i < raw.Length)
            {
                char c = raw[i];

                // TMP rich text tag: copy verbatim, it is not a visible character.
                if (c == '<')
                {
                    int close = raw.IndexOf('>', i);
                    if (close > i && raw.IndexOf('<', i + 1, close - i - 1 < 0 ? 0 : close - i - 1) < 0)
                    {
                        sb.Append(raw, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }

                if (c == '{')
                {
                    int close = raw.IndexOf('}', i);
                    if (close > i)
                    {
                        string tag = raw.Substring(i + 1, close - i - 1).Trim().ToLowerInvariant();
                        if (TryHandleTag(tag, visible, open, result, sb))
                        {
                            i = close + 1;
                            continue;
                        }
                    }
                }

                sb.Append(c);
                visible++;
                i++;
            }

            foreach (var pair in open)
            {
                result.spans.Add(new Span { kind = pair.Key, start = pair.Value, end = int.MaxValue });
                if (pair.Key == Kind.Big || pair.Key == Kind.Small) sb.Append("</size>");
            }
            result.text = sb.ToString();
            return result;
        }

        private static bool TryHandleTag(string tag, int visible, Dictionary<Kind, int> open, Parsed result, StringBuilder sb)
        {
            // Stage directions are read by the DialogueDirector from the key; never displayed.
            if (tag.StartsWith("approach=") || tag.StartsWith("beside=")) return true;
            if (tag == "quake")
            {
                result.cues.Add(new Cue { index = visible, quake = true });
                return true;
            }
            if (tag == "p" || tag.StartsWith("p="))
            {
                float pause = 0.45f;
                if (tag.Length > 2) float.TryParse(tag.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out pause);
                result.cues.Add(new Cue { index = visible, pause = Mathf.Clamp(pause, 0.05f, 4f) });
                return true;
            }

            bool closing = tag.StartsWith("/");
            string name = closing ? tag.Substring(1) : tag;
            Kind kind;
            switch (name)
            {
                case "shake": kind = Kind.Shake; break;
                case "wave": kind = Kind.Wave; break;
                case "glitch": kind = Kind.Glitch; break;
                case "bounce": kind = Kind.Bounce; break;
                case "big": kind = Kind.Big; break;
                case "small": kind = Kind.Small; break;
                case "slow": kind = Kind.Slow; break;
                case "fast": kind = Kind.Fast; break;
                default: return false; // not ours (e.g. "{0}"): leave it in the text
            }

            if (!closing)
            {
                open[kind] = visible;
                if (kind == Kind.Big) sb.Append("<size=138%>");
                else if (kind == Kind.Small) sb.Append("<size=78%>");
            }
            else if (open.TryGetValue(kind, out int start))
            {
                open.Remove(kind);
                result.spans.Add(new Span { kind = kind, start = start, end = visible });
                if (kind == Kind.Big || kind == Kind.Small) sb.Append("</size>");
            }
            return true;
        }

        /// <summary>Removes our tags (for places that cannot animate).</summary>
        public static string Strip(string raw) => Parse(raw).text;

        /// <summary>Localizes, then shows text with animated effects on any TMP label.</summary>
        public static TextFXAnimator Show(TMP_Text label, string key, TextEffect baseEffect = TextEffect.None)
        {
            if (label == null) return null;
            TextFXAnimator animator = label.GetComponent<TextFXAnimator>();
            if (animator == null) animator = label.gameObject.AddComponent<TextFXAnimator>();
            animator.SetText(Loc.T(key), true, baseEffect);
            return animator;
        }
    }
}
