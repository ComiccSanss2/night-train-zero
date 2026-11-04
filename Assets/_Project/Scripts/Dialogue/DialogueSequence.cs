using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>Per-line text animation applied by the DialogueManager.</summary>
    public enum TextEffect
    {
        None,
        /// <summary>Jittery letters: fear, glitches, the Algorithm.</summary>
        Shake,
        /// <summary>Gentle sine wave: dreams, Subnet whimsy, warm memories.</summary>
        Wave,
        /// <summary>Displaced, flickering letters: corrupted data, the Algorithm.</summary>
        Glitch,
        /// <summary>Barely-there drift: Leo's thoughts.</summary>
        Float,
        /// <summary>Happy hops.</summary>
        Bounce
    }

    /// <summary>A selectable answer that can branch the conversation.</summary>
    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("Text on the choice button.")]
        public string text = "...";

        [Tooltip("Node id to jump to. Use \"END\" to close the dialogue. Empty = continue to the next node in the list.")]
        public string nextNodeId = string.Empty;

        [Tooltip("Optional story flag set when this choice is picked.")]
        public string flagToSet = string.Empty;
    }

    /// <summary>One line of dialogue.</summary>
    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Unique id inside this sequence (only needed if something jumps here).")]
        public string id = string.Empty;

        [Tooltip("Who says it. Null = the manager's default narrator (Leo's inner voice).")]
        public DialogueSpeaker speaker;

        [TextArea(2, 5)]
        [Tooltip("Supports TextMeshPro rich text, e.g. <color=#FF4FD8>pink</color>, <i>italics</i>.")]
        public string text = string.Empty;

        public TextEffect effect = TextEffect.None;

        [Tooltip("Only show this node if the flag is set. Prefix with ! to require the flag to be ABSENT.")]
        public string requiredFlag = string.Empty;

        [Tooltip("Story flag set as soon as this node is displayed.")]
        public string flagToSet = string.Empty;

        [Tooltip("Choices shown after the text finishes typing. Empty = linear line.")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();

        [Tooltip("Node id to go to after this line (ignored if choices exist). \"END\" closes. Empty = next in list.")]
        public string nextNodeId = string.Empty;
    }

    /// <summary>
    /// A branching conversation or introspection. Create via Assets → Create → Subnet → Dialogue Sequence.
    /// Nodes play top to bottom unless a node or choice redirects with nextNodeId.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "Subnet/Dialogue Sequence", order = 1)]
    public class DialogueSequence : ScriptableObject
    {
        /// <summary>Reserved id that terminates the conversation.</summary>
        public const string EndNodeId = "END";

        [Tooltip("Optional id of the first node. Empty = first node in the list.")]
        public string startNodeId = string.Empty;

        public List<DialogueNode> nodes = new List<DialogueNode>();

        /// <summary>Index of the node with <paramref name="nodeId"/>, or -1.</summary>
        public int IndexOf(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].id == nodeId) return i;
            }
            return -1;
        }

        /// <summary>Builds a temporary linear sequence from plain lines (used by inspectables). Lines starting with "* " use the narrator.</summary>
        public static DialogueSequence FromLines(IList<string> lines, DialogueSpeaker speaker, TextEffect effect = TextEffect.None)
        {
            var sequence = CreateInstance<DialogueSequence>();
            sequence.name = "RuntimeLines";
            sequence.hideFlags = HideFlags.DontSave;
            if (lines == null) return sequence;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                // Convention: a line starting with "* " is spoken by the narrator (Leo's inner voice),
                // so one list can mix an NPC and Leo's introspection.
                bool narrator = line.StartsWith("* ");
                sequence.nodes.Add(new DialogueNode
                {
                    speaker = narrator ? null : speaker,
                    text = narrator ? line.Substring(2) : line,
                    effect = effect
                });
            }
            return sequence;
        }
    }
}
