using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>Visual voice of a speaker: picks the typeface and the look of the text box.</summary>
    public enum SpeakerStyle
    {
        Default,
        /// <summary>Leo's inner voice (narration).</summary>
        Thought,
        Leo,
        Byte,
        Kira,
        Algorithm,
        /// <summary>Hackable "enemies" and the people stuck inside them.</summary>
        Enemy,
        /// <summary>Talking objects, mannequins, background voices.</summary>
        Object,
        Unknown
    }

    /// <summary>
    /// Voice + presentation profile for anyone (or anything) that talks: Leo's inner voice,
    /// Byte, Kira, a tired streetlamp. Create via Assets → Create → Subnet → Dialogue Speaker.
    /// </summary>
    [CreateAssetMenu(fileName = "Speaker_New", menuName = "Subnet/Dialogue Speaker", order = 0)]
    public class DialogueSpeaker : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Name shown on the name plate. Leave empty for narration / Leo's introspection (no name plate).")]
        public string displayName = string.Empty;

        [Tooltip("Name plate text color.")]
        public Color nameColor = new Color(0.55f, 1f, 0.95f);

        [Tooltip("Optional portrait shown left of the text box.")]
        public Sprite portrait;

        [Header("Look")]
        [Tooltip("Typeface + text styling (see DialogueFonts).")]
        public SpeakerStyle style = SpeakerStyle.Default;

        [Tooltip("Body text color.")]
        public Color textColor = Color.white;

        [Tooltip("Effect applied to every line of this speaker (on top of inline {tags}).")]
        public TextEffect signatureEffect = TextEffect.None;

        [Header("Voice (typewriter blips)")]
        [Tooltip("Short blip played while text types out. Kenney Interface Sounds: tick_002 / click_002 / pluck_001 work well.")]
        public AudioClip voiceBlip;

        [Range(0f, 1f)] public float voiceVolume = 0.55f;

        [Tooltip("Random pitch range per blip. Narrow = robotic, wide = nervous.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Play a blip every N visible characters.")]
        [Min(1)] public int blipEveryNCharacters = 2;

        [Header("Pacing")]
        [Tooltip("Multiplier on the global typing speed (Byte talks fast, the Algorithm talks slowly).")]
        [Min(0.05f)] public float typingSpeedMultiplier = 1f;
    }
}
