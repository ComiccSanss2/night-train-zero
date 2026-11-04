using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Player;
using UnityEngine;

namespace Subnet.Interaction
{
    /// <summary>
    /// Doors, train doors and walk-in exit zones. If the required story flag is missing,
    /// plays "locked" lines that tell the player what to do next (no waypoint arrows).
    /// Otherwise: optional leaving lines → optional cinematic → fade → next scene.
    /// </summary>
    public class SceneExit : MonoBehaviour, IInteractable
    {
        [Header("Destination")]
        [SerializeField] private string targetScene = "02_Station";
        [Tooltip("Chapter key unlocked in CORRUPTED MEMORIES when leaving through here.")]
        [SerializeField] private string unlockChapterKey = string.Empty;

        [Header("Requirement")]
        [Tooltip("Story flag needed to leave. Empty = always open.")]
        [SerializeField] private string requiredFlag = string.Empty;
        [TextArea(2, 4)]
        [Tooltip("Shown when the requirement isn't met. Lines starting with '* ' are Leo's inner voice.")]
        [SerializeField] private string[] lockedLines = { "* Not yet." };

        [Header("Leaving")]
        [TextArea(2, 4)]
        [SerializeField] private string[] leavingLines = { };
        [SerializeField] private DialogueSpeaker speaker;
        [Tooltip("Optional cutscene played before the scene change (e.g. boarding Night Train Zero).")]
        [SerializeField] private CinematicSequence cinematicBeforeLeaving;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;

        [Header("Mode")]
        [Tooltip("Leave as soon as the player walks into this trigger (needs a trigger Collider).")]
        [SerializeField] private bool triggerOnEnter;
        [SerializeField] private Transform promptAnchor;

        private bool leaving;
        private float lockedMessageCooldown;

        public bool CanInteract => !triggerOnEnter && !leaving && isActiveAndEnabled;

        public Vector3 PromptAnchor => promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 1.2f;

        private bool RequirementMet => string.IsNullOrEmpty(requiredFlag) || StoryFlags.Has(requiredFlag);

        public void SetFocused(bool focused) { }

        public void Interact(PlayerInteractor interactor)
        {
            if (leaving) return;
            if (interactor != null) interactor.FaceTowards(transform.position);
            TryLeave();
        }

        private void Update()
        {
            if (lockedMessageCooldown > 0f) lockedMessageCooldown -= Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter || leaving || !other.CompareTag("Player")) return;
            if (!RequirementMet && lockedMessageCooldown > 0f) return;
            TryLeave();
        }

        private void TryLeave()
        {
            DialogueManager dialogue = DialogueManager.Instance;

            if (!RequirementMet)
            {
                lockedMessageCooldown = 4f;
                if (dialogue != null && lockedLines != null && lockedLines.Length > 0) dialogue.PlayLines(lockedLines, speaker);
                return;
            }

            leaving = true;
            if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);

            if (dialogue != null && leavingLines != null && leavingLines.Length > 0) dialogue.PlayLines(leavingLines, speaker, PlayCinematicThenLoad);
            else PlayCinematicThenLoad();
        }

        private void PlayCinematicThenLoad()
        {
            if (cinematicBeforeLeaving != null && CinematicDirector.Instance != null)
            {
                CinematicDirector.Instance.Play(cinematicBeforeLeaving, Load);
            }
            else
            {
                Load();
            }
        }

        private void Load() => SceneLoader.Load(targetScene, unlockChapterKey);
    }
}
