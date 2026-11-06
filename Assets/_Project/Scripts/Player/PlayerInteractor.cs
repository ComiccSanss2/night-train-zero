using System.Text;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Interaction;
using TMPro;
using UnityEngine;

namespace Subnet.Player
{
    /// <summary>
    /// Finds the best interactable in front of Leo, shows ONE prompt above it ("Z  Front Door"),
    /// and triggers it on the confirm button.
    /// Targeting rules (so clustered props don't fight each other):
    ///  - only objects inside a forward cone count,
    ///  - what Leo is facing matters more than raw distance,
    ///  - the current target is "sticky": another object must be clearly better to steal focus.
    /// </summary>
    [RequireComponent(typeof(IsometricPlayerController))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float detectionRadius = 1.4f;
        [Tooltip("Height of the detection sphere center above the player's pivot.")]
        [SerializeField] private float detectionHeight = 0.6f;
        [Tooltip("Objects outside this cone (degrees, total) in front of Leo are ignored.")]
        [Range(30f, 360f)] [SerializeField] private float facingAngle = 170f;
        [Tooltip("How much facing direction weighs against distance (higher = aim matters more).")]
        [SerializeField] private float angleWeight = 0.04f;
        [Tooltip("A new target must beat the current one by this score margin to take focus.")]
        [SerializeField] private float stickiness = 0.18f;
        [Tooltip("Set to the 'Interactable' layer.")]
        [SerializeField] private LayerMask interactableMask = ~0;

        [Header("Prompt UI")]
        [Tooltip("Screen-space UI element shown above the focused object.")]
        [SerializeField] private RectTransform promptUI;
        [Tooltip("Canvas the prompt lives in (Screen Space - Overlay or Camera).")]
        [SerializeField] private Canvas promptCanvas;
        [Tooltip("Optional label inside the prompt; shows the key and the target's name.")]
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private string keyHint = "E";
        [SerializeField] private float promptBobAmplitude = 4f;
        [SerializeField] private float promptBobSpeed = 4f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Kenney Interface Sounds: select_001 or switch_002.")]
        [SerializeField] private AudioClip interactClip;

        private readonly Collider[] overlapBuffer = new Collider[32];
        private readonly StringBuilder nameBuilder = new StringBuilder(32);
        private IsometricPlayerController movement;
        private Camera cachedCamera;
        private IInteractable current;
        private IInteractable manual;   // picked with Q among several close targets
        private readonly System.Collections.Generic.List<(IInteractable target, float score)> candidates = new System.Collections.Generic.List<(IInteractable, float)>();
        private int lastCandidateCount;
        private float promptScaleVelocity;
        private float promptScale;

        /// <summary>The object currently under focus, if any.</summary>
        public IInteractable Current => current;

        private void Awake()
        {
            movement = GetComponent<IsometricPlayerController>();
            cachedCamera = Camera.main;
            if (promptUI != null) promptUI.localScale = Vector3.zero;
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= RefreshPromptLabel;
            SetFocus(null);
        }

        private void Update()
        {
            bool blocked = InputLock.IsLocked
                || (DialogueManager.Instance != null && DialogueManager.Instance.ClosedRecently)
                || Time.frameCount - UI.PauseMenu.LastClosedFrame <= 1;
            IInteractable best = InputLock.IsLocked ? null : FindBestInteractable();
            // Q (or a shoulder button) cycles through everything in reach, nearest first.
            if (!blocked && candidates.Count > 1 && CyclePressed())
            {
                candidates.Sort((x, y) => x.score.CompareTo(y.score));
                int at = candidates.FindIndex(c => ReferenceEquals(c.target, current));
                manual = candidates[(at + 1) % candidates.Count].target;
                best = manual;
            }
            if (manual != null && !candidates.Exists(c => ReferenceEquals(c.target, manual))) manual = null;
            if (manual != null) best = manual;
            SetFocus(best);
            if (candidates.Count != lastCandidateCount)
            {
                lastCandidateCount = candidates.Count;
                RefreshPromptLabel();
            }

            if (!blocked && IsAlive(current) && GameInput.SubmitPressed)
            {
                if (audioSource != null && interactClip != null) audioSource.PlayOneShot(interactClip);
                current.Interact(this);
            }
        }

        private void LateUpdate()
        {
            UpdatePrompt();
        }

        private static bool CyclePressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && kb.qKey.wasPressedThisFrame) || (gp != null && (gp.rightShoulder.wasPressedThisFrame || gp.leftShoulder.wasPressedThisFrame));
        }

        private IInteractable FindBestInteractable()
        {
            candidates.Clear();
            Vector3 origin = transform.position + Vector3.up * detectionHeight;
            int count = Physics.OverlapSphereNonAlloc(origin, detectionRadius, overlapBuffer, interactableMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.MaxValue;
            float currentScore = float.MaxValue;
            float halfAngle = facingAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null || hit.transform.IsChildOf(transform)) continue;

                IInteractable candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract) continue;

                // Use the closest point of the collider (big objects like doors work from anywhere along them).
                Vector3 closest = hit.ClosestPoint(origin);
                Vector3 toTarget = closest - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;

                // Aim at the object's center for the angle, so stacked props are told apart by where Leo looks.
                Vector3 toCenter = hit.bounds.center - origin;
                toCenter.y = 0f;
                float angle = toCenter.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(transform.forward, toCenter);
                // Long things (walls, counters, boards): what counts is the nearest part, not the far-away center.
                if (toTarget.sqrMagnitude > 0.0001f) angle = Mathf.Min(angle, Vector3.Angle(transform.forward, toTarget));
                if (angle > halfAngle && distance > 0.15f) continue;

                float score = distance + angle * angleWeight;
                int known = candidates.FindIndex(c => ReferenceEquals(c.target, candidate));
                if (known < 0) candidates.Add((candidate, score));
                else if (score < candidates[known].score) candidates[known] = (candidate, score);
                if (ReferenceEquals(candidate, current)) currentScore = Mathf.Min(currentScore, score);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            // Hysteresis: keep the current target unless something is clearly better.
            if (IsAlive(current) && currentScore < float.MaxValue && !ReferenceEquals(best, current) && bestScore > currentScore - stickiness)
            {
                return current;
            }
            return best;
        }

        private static bool IsAlive(IInteractable interactable)
        {
            return interactable != null && interactable is Object unityObject && unityObject != null;
        }

        private void SetFocus(IInteractable next)
        {
            if (ReferenceEquals(next, current)) return;
            if (IsAlive(current)) current.SetFocused(false);
            current = next;
            if (IsAlive(current))
            {
                current.SetFocused(true);
                RefreshPromptLabel();
            }
        }

        private void OnEnable() => Loc.LanguageChanged += RefreshPromptLabel;

        private void RefreshPromptLabel()
        {
            if (promptLabel != null && IsAlive(current))
                promptLabel.text = $"<b>{keyHint}</b>  {Loc.T(DisplayName(current))}" + (candidates.Count > 1 ? "   <size=75%><color=#8A8F99><b>Q</b> " + Loc.T("next") + "</color></size>" : string.Empty);
        }

        /// <summary>"Prop_FrontDoor" → "Front Door", "NPC_Commuter_Phone" → "Commuter", "Lamp_Waited" → "Lamp".</summary>
        private string DisplayName(IInteractable interactable)
        {
            string raw = interactable is Component c ? c.gameObject.name : "";
            if (raw.StartsWith("Pedestal_")) return "Statue";
            if (raw.StartsWith("Lamp_")) return "Lamp";
            foreach (string prefix in new[] { "Prop_", "NPC_", "Obstacle_" })
            {
                if (raw.StartsWith(prefix))
                {
                    raw = raw.Substring(prefix.Length);
                    break;
                }
            }
            int underscore = raw.IndexOf('_');
            if (underscore > 0) raw = raw.Substring(0, underscore);

            nameBuilder.Clear();
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsDigit(ch)) continue;
                if (i > 0 && char.IsUpper(ch) && char.IsLower(raw[i - 1])) nameBuilder.Append(' ');
                nameBuilder.Append(ch);
            }
            return nameBuilder.ToString().Trim();
        }

        private void UpdatePrompt()
        {
            if (promptUI == null) return;

            bool visible = IsAlive(current) && !InputLock.IsLocked;
            promptScale = Mathf.SmoothDamp(promptScale, visible ? 1f : 0f, ref promptScaleVelocity, 0.07f, Mathf.Infinity, GameTime.UnscaledDelta);
            promptUI.localScale = Vector3.one * promptScale;
            if (!visible || cachedCamera == null) return;

            Vector3 screen = cachedCamera.WorldToScreenPoint(current.PromptAnchor);
            if (screen.z < 0f) return;
            screen.y += Mathf.Sin(GameTime.UnscaledTime * promptBobSpeed) * promptBobAmplitude;

            if (promptCanvas != null && promptCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)promptCanvas.transform, screen, promptCanvas.worldCamera, out Vector2 local);
                promptUI.anchoredPosition = local;
            }
            else
            {
                promptUI.position = screen;
            }
        }

        /// <summary>Turns Leo toward a target (used by inspectables and NPCs).</summary>
        public void FaceTowards(Vector3 worldPoint) => movement.FaceTowards(worldPoint);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.85f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * detectionHeight, detectionRadius);
        }
    }
}
