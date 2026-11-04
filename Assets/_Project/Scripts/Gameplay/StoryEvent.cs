using System.Collections;
using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Subnet.Gameplay
{
    /// <summary>
    /// A scripted story beat: SMS messages on Leo's handheld, a few lines of dialogue,
    /// a flag to set. Fires on scene start, when a flag becomes true, or when the player enters a trigger.
    /// Always waits for cutscenes and open dialogue to finish first, so beats never overlap.
    /// </summary>
    public class StoryEvent : MonoBehaviour
    {
        public enum TriggerMode { OnStart, OnFlag, OnPlayerEnter }

        [SerializeField] private TriggerMode trigger = TriggerMode.OnStart;
        [Tooltip("OnFlag: fires when this flag is set. Other modes: optional extra condition (prefix ! = must NOT be set).")]
        [SerializeField] private string requiredFlag = string.Empty;
        [SerializeField] private float delay = 0.8f;

        [Header("SMS (handheld)")]
        [SerializeField] private string phoneSender = "BYTE";
        [TextArea(1, 3)] [SerializeField] private string[] phoneMessages = { };

        [Header("Dialogue")]
        [TextArea(2, 4)]
        [Tooltip("Lines starting with '* ' are Leo's inner voice; others use the speaker below.")]
        [SerializeField] private string[] lines = { };
        [SerializeField] private DialogueSpeaker speaker;
        [SerializeField] private DialogueSequence sequence;

        [Header("Result")]
        [SerializeField] private string flagToSet = string.Empty;
        public UnityEvent onFired = new UnityEvent();

        private bool fired;
        private bool started;

        private void Start()
        {
            started = true;
            // Already happened in a saved game.
            if (!string.IsNullOrEmpty(flagToSet) && StoryFlags.Has(flagToSet)) fired = true;
        }

        private void Update()
        {
            if (fired || !started) return;
            switch (trigger)
            {
                case TriggerMode.OnStart:
                    if (ConditionMet()) Fire();
                    break;
                case TriggerMode.OnFlag:
                    if (!string.IsNullOrEmpty(requiredFlag) && ConditionMet()) Fire();
                    break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired || trigger != TriggerMode.OnPlayerEnter || !other.CompareTag("Player")) return;
            if (ConditionMet()) Fire();
        }

        private bool ConditionMet()
        {
            if (string.IsNullOrEmpty(requiredFlag)) return true;
            return requiredFlag[0] == '!' ? !StoryFlags.Has(requiredFlag.Substring(1)) : StoryFlags.Has(requiredFlag);
        }

        /// <summary>Fires the event manually (e.g. from a UnityEvent).</summary>
        public void Fire()
        {
            if (fired) return;
            fired = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(delay);

            // Never interrupt a cutscene, a conversation or a hacking session.
            while (IsBusy()) yield return null;

            if (phoneMessages != null)
            {
                foreach (string message in phoneMessages)
                {
                    if (!string.IsNullOrWhiteSpace(message)) PhoneNotifier.Show(phoneSender, message);
                }
            }

            DialogueManager dialogue = DialogueManager.Instance;
            if (dialogue != null && (sequence != null || (lines != null && lines.Length > 0)))
            {
                if (phoneMessages != null && phoneMessages.Length > 0) yield return new WaitForSeconds(1.2f);
                bool done = false;
                if (sequence != null) dialogue.Play(sequence, () => done = true);
                else dialogue.PlayLines(lines, speaker, () => done = true);
                while (!done) yield return null;
            }

            StoryFlags.Set(flagToSet);
            onFired?.Invoke();
        }

        private static bool IsBusy()
        {
            if (SceneLoader.IsLoading) return true;
            if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) return true;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return true;
            if (Hacking.HackingManager.Instance != null && Hacking.HackingManager.Instance.IsActive) return true;
            return false;
        }
    }
}
