using System.Collections;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Player;
using Subnet.UI;
using UnityEngine;

namespace Subnet.Interaction
{
    /// <summary>
    /// A real door into a building: the door swings open on its hinge (warm light spills out), Leo walks
    /// through, the screen fades, and he steps out of the matching door on the other side, which closes
    /// behind him. Interiors live elsewhere in the same scene, so there's no loading.
    /// </summary>
    public class HouseDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("Rotates around its local Y axis when the door opens.")]
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = -105f;
        [Tooltip("Light inside the doorway that brightens as the door opens (optional).")]
        [SerializeField] private Light spill;
        [SerializeField] private float spillIntensity = 2.5f;

        [Header("Where Leo goes")]
        [Tooltip("The door on the other side (opens as Leo arrives, closes behind him).")]
        [SerializeField] private HouseDoor otherSide;
        [Tooltip("Optional: name shown in the location toast when entering (e.g. 'NEON MART').")]
        [SerializeField] private string placeName = string.Empty;

        [Header("Rules")]
        [Tooltip("Only opens while this flag is set (prefix ! for 'while NOT set'). Empty = always.")]
        [SerializeField] private string requiredFlag = string.Empty;
        [TextArea(1, 3)] [SerializeField] private string[] lockedLines = { };
        [TextArea(1, 3)] [SerializeField] private string[] enterLines = { };
        [SerializeField] private string flagOnUse = string.Empty;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioClip lockedClip;

        private Collider cachedCollider;
        private bool busy;
        private Quaternion closedRotation;

        /// <summary>Point just outside this door, where Leo stands after coming through it.</summary>
        public Vector3 Doorstep => transform.position + transform.forward * 1.0f;

        /// <summary>Point inside the doorway (Leo walks here before the fade).</summary>
        public Vector3 Threshold => transform.position - transform.forward * 0.25f;

        public bool CanInteract => isActiveAndEnabled && !busy;

        public Vector3 PromptAnchor => cachedCollider != null
            ? new Vector3(cachedCollider.bounds.center.x, cachedCollider.bounds.max.y + 0.2f, cachedCollider.bounds.center.z)
            : transform.position + Vector3.up * 2.4f;

        public void SetFocused(bool focused) { }

        private void Awake()
        {
            cachedCollider = GetComponentInChildren<Collider>();
            if (hinge != null) closedRotation = hinge.localRotation;
            if (spill != null) spill.intensity = 0f;
        }

        private bool Unlocked()
        {
            if (string.IsNullOrEmpty(requiredFlag)) return true;
            foreach (string part in requiredFlag.Split(';'))
            {
                string f = part.Trim();
                if (f.Length == 0) continue;
                bool ok = f[0] == '!' ? !StoryFlags.Has(f.Substring(1)) : StoryFlags.Has(f);
                if (!ok) return false;
            }
            return true;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (busy || otherSide == null) return;
            DialogueManager dialogue = DialogueManager.Instance;
            if (!Unlocked())
            {
                StartCoroutine(Rattle());
                if (dialogue != null && lockedLines.Length > 0) dialogue.PlayLines(lockedLines);
                return;
            }
            if (dialogue != null && enterLines.Length > 0) dialogue.PlayLines(enterLines, null, () => StartCoroutine(Enter(interactor)));
            else StartCoroutine(Enter(interactor));
        }

        private IEnumerator Rattle()
        {
            busy = true;
            Play(lockedClip);
            if (hinge != null)
            {
                for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    hinge.localRotation = closedRotation * Quaternion.Euler(0f, Mathf.Sin(t * 60f) * 2.5f * (1f - t / 0.35f), 0f);
                    yield return null;
                }
                hinge.localRotation = closedRotation;
            }
            busy = false;
        }

        private IEnumerator Enter(PlayerInteractor interactor)
        {
            busy = true;
            otherSide.busy = true;
            InputLock.Acquire(this);
            GameObject leo = interactor != null ? interactor.gameObject : GameObject.FindGameObjectWithTag("Player");
            var controller = leo != null ? leo.GetComponent<IsometricPlayerController>() : null;

            // 1) Step in front of the door, turn to it.
            if (controller != null)
            {
                Vector3 front = transform.position + transform.forward * 0.75f;
                if (Vector3.Distance(Flat(leo.transform.position), Flat(front)) > 0.15f) yield return controller.ScriptedWalk(front, 2.6f);
                controller.FaceTowards(transform.position);
            }

            // 2) The door swings open, light spills out.
            Play(openClip);
            yield return Swing(this, true, 0.5f);

            // 3) Leo walks through, the screen fades halfway.
            ScreenFader fader = FindFirstObjectByType<ScreenFader>();
            if (controller != null)
            {
                Coroutine walk = StartCoroutine(controller.ScriptedWalk(Threshold, 1.8f));
                yield return new WaitForSeconds(0.25f);
                if (fader != null) yield return fader.FadeTo(1f, 0.35f);
                yield return walk;
            }
            else if (fader != null) yield return fader.FadeTo(1f, 0.35f);

            // 4) Swap sides in the dark.
            Snap(this, false);
            Snap(otherSide, true);
            if (leo != null)
            {
                Vector3 arrive = otherSide.Threshold;
                Quaternion facing = Quaternion.LookRotation(otherSide.transform.forward, Vector3.up);
                if (controller != null) controller.Teleport(arrive, facing);
                else leo.transform.SetPositionAndRotation(arrive, facing);
                int i = 0;
                foreach (CompanionFollower follower in FindObjectsByType<CompanionFollower>(FindObjectsSortMode.None))
                {
                    if (!follower.IsFollowing) continue;
                    follower.transform.position = otherSide.Doorstep + otherSide.transform.forward * 0.6f + otherSide.transform.right * (i++ == 0 ? -0.7f : 0.7f);
                }
            }
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.SnapToTarget();
            StoryFlags.Set(flagOnUse);

            // 5) Fade in while stepping out (the walk starts this frame, so Leo is never "inside" the wall); the door closes behind.
            Coroutine stepOut = controller != null ? StartCoroutine(controller.ScriptedWalk(otherSide.Doorstep, 2.0f)) : null;
            if (fader != null) fader.FadeTo(0f, 0.55f);
            if (stepOut != null) yield return stepOut;
            otherSide.Play(otherSide.closeClip != null ? otherSide.closeClip : closeClip);
            yield return Swing(otherSide, false, 0.35f);
            if (!string.IsNullOrEmpty(otherSide.placeName)) LocationToast.Show(otherSide.placeName);

            InputLock.Release(this);
            busy = false;
            otherSide.busy = false;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static IEnumerator Swing(HouseDoor door, bool open, float duration)
        {
            if (door.hinge == null) yield break;
            Quaternion from = door.hinge.localRotation;
            Quaternion to = open ? door.closedRotation * Quaternion.Euler(0f, door.openAngle, 0f) : door.closedRotation;
            float lightFrom = door.spill != null ? door.spill.intensity : 0f;
            float lightTo = open ? door.spillIntensity : 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float n = t / duration;
                float e = open ? Easing.Evaluate(EaseType.EaseOutBack, n) : Easing.Evaluate(EaseType.EaseInCubic, n);
                door.hinge.localRotation = Quaternion.SlerpUnclamped(from, to, e);
                if (door.spill != null) door.spill.intensity = Mathf.Lerp(lightFrom, lightTo, n);
                yield return null;
            }
            door.hinge.localRotation = to;
            if (door.spill != null) door.spill.intensity = lightTo;
            if (!open && IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(0.08f);
        }

        private static void Snap(HouseDoor door, bool open)
        {
            if (door.hinge != null) door.hinge.localRotation = open ? door.closedRotation * Quaternion.Euler(0f, door.openAngle, 0f) : door.closedRotation;
            if (door.spill != null) door.spill.intensity = open ? door.spillIntensity : 0f;
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void OnDestroy() => InputLock.Release(this);
    }
}
