using System.Collections;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Player;
using UnityEngine;

namespace Subnet.Interaction
{
    /// <summary>
    /// A ladder / fire escape / door to another area of the same scene (the rooftop above the shopping street).
    /// Fades to black, moves Leo (companions catch up on their own), fades back in. Optional lines before leaving.
    /// </summary>
    public class AreaTeleport : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform destination;
        [Tooltip("Only usable while this flag is set (prefix ! for 'while NOT set'). Empty = always.")]
        [SerializeField] private string requiredFlag = string.Empty;
        [TextArea(1, 3)] [SerializeField] private string[] lockedLines = { };
        [TextArea(1, 3)] [SerializeField] private string[] leavingLines = { };
        [SerializeField] private string flagOnUse = string.Empty;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip useClip;

        private Collider cachedCollider;
        private bool busy;

        private void Awake() => cachedCollider = GetComponentInChildren<Collider>();

        public bool CanInteract => isActiveAndEnabled && !busy;

        public Vector3 PromptAnchor => cachedCollider != null
            ? new Vector3(cachedCollider.bounds.center.x, cachedCollider.bounds.max.y + 0.3f, cachedCollider.bounds.center.z)
            : transform.position + Vector3.up;

        public void SetFocused(bool focused) { }

        private bool Unlocked()
        {
            if (string.IsNullOrEmpty(requiredFlag)) return true;
            return requiredFlag[0] == '!' ? !StoryFlags.Has(requiredFlag.Substring(1)) : StoryFlags.Has(requiredFlag);
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (busy || destination == null) return;
            DialogueManager dialogue = DialogueManager.Instance;
            if (!Unlocked())
            {
                if (dialogue != null && lockedLines.Length > 0) dialogue.PlayLines(lockedLines);
                return;
            }
            if (dialogue != null && leavingLines.Length > 0) dialogue.PlayLines(leavingLines, null, () => StartCoroutine(Go(interactor)));
            else StartCoroutine(Go(interactor));
        }

        private IEnumerator Go(PlayerInteractor interactor)
        {
            busy = true;
            InputLock.Acquire(this);
            if (audioSource != null && useClip != null) audioSource.PlayOneShot(useClip);
            ScreenFader fader = FindFirstObjectByType<ScreenFader>();
            if (fader != null) yield return fader.FadeTo(1f, 0.45f);

            GameObject leo = interactor != null ? interactor.gameObject : GameObject.FindGameObjectWithTag("Player");
            if (leo != null)
            {
                var controller = leo.GetComponent<IsometricPlayerController>();
                if (controller != null) controller.Teleport(destination.position, destination.rotation);
                else leo.transform.SetPositionAndRotation(destination.position, destination.rotation);
                // Companions jump along (they would teleport to their slot anyway; this avoids a visible pop).
                int i = 0;
                foreach (CompanionFollower follower in FindObjectsByType<CompanionFollower>(FindObjectsSortMode.None))
                {
                    if (!follower.IsFollowing) continue;
                    follower.transform.position = destination.position - destination.forward * 1.2f + destination.right * (i++ == 0 ? -0.7f : 0.7f);
                }
            }
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.SnapToTarget();
            StoryFlags.Set(flagOnUse);
            yield return new WaitForSeconds(0.15f);
            if (fader != null) yield return fader.FadeTo(0f, 0.6f);
            InputLock.Release(this);
            busy = false;
        }

        private void OnDestroy() => InputLock.Release(this);
    }
}
