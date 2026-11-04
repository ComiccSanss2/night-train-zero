using System;
using UnityEngine;

namespace Subnet.Hacking
{
    /// <summary>Bullet-hell patterns, themed as intrusive thoughts.</summary>
    public enum HackPattern
    {
        /// <summary>Falling paperwork.</summary>
        PaperRain,
        /// <summary>Ticking clock hands sweeping out from the center.</summary>
        ClockHands,
        /// <summary>Notifications flying in from both sides.</summary>
        Crossfire,
        /// <summary>A spiral of looping thoughts.</summary>
        Spiral,
        /// <summary>Doubts aimed straight at you.</summary>
        Aimed,
        /// <summary>Rings of tears / static.</summary>
        Ripple,
        /// <summary>Telegraphed scan-line beams. Read the warning, find the safe cell.</summary>
        Lasers,
        /// <summary>Flowers that open, slow down, and burst into petals.</summary>
        Bloom,
        /// <summary>Sine-wave data serpents swimming across the arena.</summary>
        Serpent,
        /// <summary>Shells rise, burst, and rain sparks with gravity.</summary>
        Fireworks,
        /// <summary>Closing doors: walls with a gap sliding across.</summary>
        Walls,
        /// <summary>Seekers that chase you for a while, leaving trails.</summary>
        Homing,
        /// <summary>Rings that circle you, close in, then explode outward.</summary>
        Orbit,
        /// <summary>Rush hour: a lane lights up, then a train tears through it.</summary>
        TrainRush
    }

    /// <summary>One evasion phase.</summary>
    [Serializable]
    public class HackWave
    {
        public HackPattern pattern = HackPattern.PaperRain;
        [Min(2f)] public float duration = 8f;
        [Range(0.5f, 2f)] public float intensity = 1f;
        [Tooltip("Grief wave: it can't be won by dodging. Time only passes while you stand still, and hits don't hurt.")]
        public bool surrender;

        public HackWave() { }

        public HackWave(HackPattern pattern, float duration, float intensity)
        {
            this.pattern = pattern;
            this.duration = duration;
            this.intensity = intensity;
        }

        public HackWave(HackPattern pattern, float duration, float intensity, bool surrender) : this(pattern, duration, intensity)
        {
            this.surrender = surrender;
        }
    }

    /// <summary>One empathy / logic phase: pick the memory fragment that lowers the firewall.</summary>
    [Serializable]
    public class HackPuzzle
    {
        [TextArea(2, 4)] public string prompt = "Select a memory fragment.";
        public string[] options = { "[A]", "[B]", "[C]" };
        public int correctIndex;
        [TextArea(2, 4)] public string correctLine = "The firewall softens.";
        [TextArea(2, 4)] public string wrongLine = "The fragment bounces off.";
        [Tooltip("Shown when the player picks [Listen closer]. Should point at the clue without giving the answer away.")]
        [TextArea(2, 4)] public string hint = string.Empty;
        [Tooltip("A second answer that also works (an honest 'I don't know', for example). -1 = none.")]
        public int altCorrectIndex = -1;
        [TextArea(2, 4)] public string altCorrectLine = string.Empty;

        public HackPuzzle() { }

        public HackPuzzle(string prompt, string[] options, int correctIndex, string correctLine, string wrongLine, string hint,
            int altCorrectIndex = -1, string altCorrectLine = "") : this(prompt, options, correctIndex, correctLine, wrongLine)
        {
            this.hint = hint;
            this.altCorrectIndex = altCorrectIndex;
            this.altCorrectLine = altCorrectLine;
        }

        public HackPuzzle(string prompt, string[] options, int correctIndex, string correctLine, string wrongLine)
        {
            this.prompt = prompt;
            this.options = options;
            this.correctIndex = correctIndex;
            this.correctLine = correctLine;
            this.wrongLine = wrongLine;
        }
    }
}
