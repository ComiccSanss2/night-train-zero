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
        Ripple
    }

    /// <summary>One evasion phase.</summary>
    [Serializable]
    public class HackWave
    {
        public HackPattern pattern = HackPattern.PaperRain;
        [Min(2f)] public float duration = 8f;
        [Range(0.5f, 2f)] public float intensity = 1f;

        public HackWave() { }

        public HackWave(HackPattern pattern, float duration, float intensity)
        {
            this.pattern = pattern;
            this.duration = duration;
            this.intensity = intensity;
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

        public HackPuzzle() { }

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
