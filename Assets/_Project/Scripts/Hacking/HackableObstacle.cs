using System.Collections.Generic;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using Subnet.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Subnet.Hacking
{
    /// <summary>
    /// An emotional / physical blockage (a commuter turned into a wall of paperwork, a weeping statue,
    /// a corrupted memory file). Interacting connects Leo's handheld to its "Psychological Source Code"
    /// and starts an Obstacle Hacking session. Resolution is always peaceful.
    /// </summary>
    public class HackableObstacle : MonoBehaviour, IInteractable
    {
        [Header("Encounter")]
        public string encounterTitle = "OBSTACLE.exe";
        public DialogueSpeaker speaker;
        [TextArea(2, 4)] public string[] introLines = { };
        public List<HackWave> waves = new List<HackWave>();
        public List<HackPuzzle> puzzles = new List<HackPuzzle>();
        [TextArea(2, 4)] public string[] resolutionLines = { };

        [Header("Presentation (Undertale-style)")]
        [Tooltip("What the live 'source feed' above the arena films. Empty = this object.")]
        public Transform portraitTarget;
        [Tooltip("Distance of the portrait camera from the target.")]
        public float portraitDistance = 2.6f;
        [Tooltip("Lines the obstacle says in its speech bubble after each firewall segment breaks.")]
        [TextArea(1, 3)] public string[] betweenRoundLines = { };
        [Tooltip("Short taunts popping up in the bubble during the dodge waves.")]
        [TextArea(1, 2)] public string[] tauntLines = { };

        [Tooltip("Music for this fight (e.g. the final boss). Empty = the scene's fight track.")]
        public AudioClip musicOverride;
        [Tooltip("A companion (Byte) is drawn at the edge of the screen and loses a pixel at a time as the fight goes on.")]
        public bool fadeCompanion;
        public string companionLabel = "byte";

        [Header("Requirement")]
        [Tooltip("Story flag needed before hacking. Empty = always available.")]
        public string requiredFlag = string.Empty;
        [TextArea(2, 4)] public string[] lockedLines = { "* Not yet." };

        [Header("Resolution")]
        public string flagOnResolved = "obstacle_resolved";
        [Tooltip("Dissolved into warm pixels when resolved (the wall, the stone shell...).")]
        public GameObject[] hideOnResolved = { };
        [Tooltip("Activated when resolved (the freed person, an open path...).")]
        public GameObject[] showOnResolved = { };
        public Color dissolveColor = new Color(1f, 0.75f, 0.45f);
        public Transform promptAnchor;
        public UnityEvent onResolved = new UnityEvent();

        private bool resolved;

        public bool IsResolved => resolved;

        public bool CanInteract => !resolved && isActiveAndEnabled && (HackingManager.Instance == null || !HackingManager.Instance.IsActive);

        public Vector3 PromptAnchor => promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 2f;

        private void Awake()
        {
            foreach (GameObject go in showOnResolved)
            {
                if (go != null) go.SetActive(false);
            }
        }

        private void Start()
        {
            // Loaded from a save after this obstacle was already resolved: skip straight to the result.
            if (!string.IsNullOrEmpty(flagOnResolved) && StoryFlags.Has(flagOnResolved))
            {
                resolved = true;
                foreach (GameObject go in hideOnResolved) if (go != null) go.SetActive(false);
                foreach (GameObject go in showOnResolved) if (go != null) go.SetActive(true);
                foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
            }
        }

        public void SetFocused(bool focused) { }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract) return;
            if (interactor != null) interactor.FaceTowards(transform.position);

            bool unlocked = string.IsNullOrEmpty(requiredFlag) || StoryFlags.Has(requiredFlag);
            if (!unlocked)
            {
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.PendingSource = portraitTarget != null ? portraitTarget : transform;
                    DialogueManager.Instance.PlayLines(lockedLines, speaker);
                }
                return;
            }

            if (HackingManager.Instance == null)
            {
                Debug.LogError("[HackableObstacle] No HackingManager in the scene.", this);
                return;
            }
            HackingManager.Instance.Begin(this, Resolve);
        }

        private void Resolve()
        {
            resolved = true;
            StoryFlags.Set(flagOnResolved);

            foreach (GameObject go in hideOnResolved)
            {
                if (go != null) DissolveFX.Play(go, dissolveColor);
            }
            foreach (GameObject go in showOnResolved)
            {
                if (go != null) go.SetActive(true);
            }
            foreach (Collider c in GetComponents<Collider>()) c.enabled = false;

            onResolved?.Invoke();
        }
    }
}
