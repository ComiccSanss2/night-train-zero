using Subnet.Core;
using Subnet.Dialogue;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// "Stanza di respiro": a hidden scene that only plays if the player stays inside this zone without
    /// moving or touching anything for a while (the rooftop: Kira's hospital story after 30 s of quiet).
    /// Needs a trigger collider. Plays once.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class IdleScene : MonoBehaviour
    {
        [SerializeField] private float secondsOfStillness = 30f;
        [SerializeField] private DialogueSequence sequence;
        [SerializeField] private string flagWhenPlayed = string.Empty;
        [Tooltip("Optional flag needed before the timer starts.")]
        [SerializeField] private string requiredFlag = string.Empty;

        private Transform leo;
        private Vector3 lastPosition;
        private float still;
        private bool inside;

        private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) { inside = true; leo = other.transform; lastPosition = leo.position; still = 0f; } }
        private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) { inside = false; still = 0f; } }

        private void Update()
        {
            if (!inside || leo == null || sequence == null) return;
            if (!string.IsNullOrEmpty(flagWhenPlayed) && StoryFlags.Has(flagWhenPlayed)) return;
            if (!string.IsNullOrEmpty(requiredFlag) && !StoryFlags.Has(requiredFlag)) return;

            bool busy = InputLock.IsLocked || GameTime.Paused;
            bool moved = (leo.position - lastPosition).sqrMagnitude > 0.0004f || GameInput.MoveVector.sqrMagnitude > 0.04f
                         || GameInput.Submit.IsPressed() || GameInput.Cancel.IsPressed();
            lastPosition = leo.position;
            if (busy || moved) { still = 0f; return; }

            still += Time.deltaTime;
            if (still < secondsOfStillness || DialogueManager.Instance == null) return;
            StoryFlags.Set(flagWhenPlayed);
            DialogueManager.Instance.Play(sequence);
        }
    }
}
