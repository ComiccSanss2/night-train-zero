using System.Collections;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Player;
using UnityEngine;

namespace Subnet.Interaction
{
    /// <summary>
    /// Anything that eats tokens: a claw machine, a vending machine, a fortune booth.
    /// Asks "play for N tokens?", wobbles the machine, then pays out from a weighted prize table.
    /// A guaranteed prize can be set for the Nth try so nobody gets stuck forever.
    /// </summary>
    public class TokenMachine : MonoBehaviour, IInteractable
    {
        [System.Serializable]
        public class Prize
        {
            [Tooltip("Item id (see Inventory.Items). Empty = nothing. Prefix $ for tokens, e.g. $4.")]
            public string reward = string.Empty;
            public float weight = 1f;
            [TextArea(1, 3)] public string line = string.Empty;
        }

        [SerializeField] private string machineId = "claw";
        [SerializeField] private int cost = 2;
        [TextArea(1, 3)] [SerializeField] private string prompt = "Play? It costs 2 tokens.";
        [SerializeField] private string playOption = "[Play  ◊2]";
        [SerializeField] private string leaveOption = "[Leave]";
        [TextArea(1, 3)] [SerializeField] private string[] playingLines = { };
        [TextArea(1, 3)] [SerializeField] private string brokeLine = "* You don't have enough tokens.";
        [SerializeField] private Prize[] prizes = { };
        [Tooltip("On this try (1-based) the guaranteed prize drops if it hasn't yet. 0 = never.")]
        [SerializeField] private int guaranteedOnTry = 4;
        [SerializeField] private string guaranteedReward = string.Empty;
        [TextArea(1, 3)] [SerializeField] private string guaranteedLine = string.Empty;
        [SerializeField] private DialogueSpeaker speaker;
        [SerializeField] private Transform wobble;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip playClip;
        [SerializeField] private AudioClip winClip;

        private const string ChoiceFlag = "__machine_play";
        private Collider cachedCollider;
        private bool busy;

        public bool CanInteract => isActiveAndEnabled && !busy;

        public Vector3 PromptAnchor => cachedCollider != null
            ? new Vector3(cachedCollider.bounds.center.x, cachedCollider.bounds.max.y + 0.2f, cachedCollider.bounds.center.z)
            : transform.position + Vector3.up * 1.8f;

        public void SetFocused(bool focused) { }

        private void Awake() => cachedCollider = GetComponentInChildren<Collider>();

        public void Interact(PlayerInteractor interactor)
        {
            if (busy || DialogueManager.Instance == null) return;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            busy = true;
            DialogueManager dialogue = DialogueManager.Instance;

            var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.hideFlags = HideFlags.DontSave;
            var ask = new DialogueNode { id = "ask", text = prompt, speaker = speaker };
            ask.choices.Add(new DialogueChoice { text = playOption, nextNodeId = DialogueSequence.EndNodeId, flagToSet = ChoiceFlag });
            ask.choices.Add(new DialogueChoice { text = leaveOption, nextNodeId = DialogueSequence.EndNodeId });
            sequence.nodes.Add(ask);
            StoryFlags.Clear(ChoiceFlag);
            bool done = false;
            dialogue.Play(sequence, () => done = true);
            while (!done) yield return null;
            Destroy(sequence);
            bool play = StoryFlags.Has(ChoiceFlag);
            StoryFlags.Clear(ChoiceFlag);
            if (!play) { busy = false; yield break; }

            if (!Inventory.Spend(cost))
            {
                done = false;
                dialogue.PlayLines(new[] { brokeLine }, speaker, () => done = true);
                while (!done) yield return null;
                busy = false;
                yield break;
            }

            InputLock.Acquire(this);
            if (audioSource != null && playClip != null) audioSource.PlayOneShot(playClip);
            if (wobble != null)
            {
                Vector3 baseScale = wobble.localScale;
                for (float t = 0f; t < 0.9f; t += Time.deltaTime)
                {
                    float s = Mathf.Sin(t * 30f) * 0.03f * (1f - t / 0.9f);
                    wobble.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
                    yield return null;
                }
                wobble.localScale = baseScale;
            }
            InputLock.Release(this);

            int tries = StoryFlags.Increment("machine." + machineId);
            string reward;
            string line;
            string wonFlag = "machine." + machineId + ".won";
            if (guaranteedOnTry > 0 && tries >= guaranteedOnTry && !StoryFlags.Has(wonFlag) && !string.IsNullOrEmpty(guaranteedReward))
            {
                reward = guaranteedReward;
                line = guaranteedLine;
            }
            else
            {
                Prize prize = Pick();
                reward = prize != null ? prize.reward : string.Empty;
                line = prize != null ? prize.line : string.Empty;
                // The special prize only drops once; afterwards it pays a few tokens back instead.
                if (reward == guaranteedReward && StoryFlags.Has(wonFlag)) { reward = "$3"; line = "* The claw grabs a handful of tokens instead. {p}Not bad."; }
            }
            if (reward == guaranteedReward && !string.IsNullOrEmpty(reward)) StoryFlags.Set(wonFlag);

            var lines = new System.Collections.Generic.List<string>(playingLines);
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
            done = false;
            dialogue.PlayLines(lines.ToArray(), speaker, () => done = true);
            while (!done) yield return null;
            if (!string.IsNullOrEmpty(reward))
            {
                if (reward[0] == '$') StoryFlags.Set(reward);
                else Inventory.Add(reward);
                if (audioSource != null && winClip != null) audioSource.PlayOneShot(winClip);
            }
            busy = false;
        }

        private Prize Pick()
        {
            float total = 0f;
            foreach (Prize p in prizes) total += Mathf.Max(0f, p.weight);
            if (total <= 0f) return null;
            float roll = Random.value * total;
            foreach (Prize p in prizes)
            {
                roll -= Mathf.Max(0f, p.weight);
                if (roll <= 0f) return p;
            }
            return prizes[prizes.Length - 1];
        }

        private void OnDestroy() => InputLock.Release(this);
    }
}
