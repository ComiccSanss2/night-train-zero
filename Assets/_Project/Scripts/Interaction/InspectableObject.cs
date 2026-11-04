using System.Collections;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Subnet.Interaction
{
    /// <summary>
    /// Any prop that answers back when inspected. Every object in the game uses this:
    /// cardboard boxes, cracked mirrors, steaming manholes, tired streetlamps.
    ///
    /// Writing rule: never describe, always reflect.
    ///   ✗ "A wooden table."
    ///   ✓ "A cracked coffee mug. It smells like overdue deadlines and mornings you wish never arrived."
    ///
    /// Content priority: Sequence Override (branching asset) &gt; First / Repeat inline lines.
    /// </summary>
    [DisallowMultipleComponent]
    public class InspectableObject : MonoBehaviour, IInteractable
    {
        [Header("Identity")]
        [Tooltip("Unique key for remembering how many times this was inspected. Defaults to the object name.")]
        [SerializeField] private string inspectionId = string.Empty;

        [Header("Introspection (inline)")]
        [Tooltip("Lines shown the first time. Each entry is one dialogue box.")]
        [TextArea(2, 4)]
        [SerializeField] private string[] firstInspection =
        {
            "A thing you haven't looked at properly in a while. It looks back, politely."
        };

        [Tooltip("Lines shown on every later inspection. Leave empty to repeat the first lines.")]
        [TextArea(2, 4)]
        [SerializeField] private string[] repeatInspection = { };

        [Tooltip("Speaker for inline lines. Empty = DialogueManager default (Leo's inner voice).")]
        [SerializeField] private DialogueSpeaker speaker;

        [Tooltip("Letter animation for inline lines.")]
        [SerializeField] private TextEffect textEffect = TextEffect.None;

        [Header("Introspection (asset)")]
        [Tooltip("Optional branching dialogue asset. Plays on the first inspection; later inspections use Repeat lines if any.")]
        [SerializeField] private DialogueSequence sequenceOverride;

        [Header("Story")]
        [Tooltip("Story flag set after the first inspection finishes (e.g. 'inspected_mirror').")]
        [SerializeField] private string flagOnInspect = string.Empty;
        [Tooltip("Only inspectable while this flag is set. Prefix with ! for 'while NOT set'.")]
        [SerializeField] private string requiredFlag = string.Empty;
        [Tooltip("Disable interaction after the first inspection (one-shot story beats).")]
        [SerializeField] private bool singleUse;

        [Header("Presentation")]
        [Tooltip("Where the interaction prompt floats. Empty = top of the collider bounds.")]
        [SerializeField] private Transform promptAnchor;
        [Tooltip("Optional child (outline mesh, glow decal, light) enabled while focused.")]
        [SerializeField] private GameObject focusHighlight;
        [Tooltip("Squash punch applied to this transform when inspected.")]
        [SerializeField] private Transform punchTarget;
        [SerializeField] private float punchStrength = 0.12f;
        [SerializeField] private bool turnPlayerToFace = true;

        [Header("Characters")]
        [Tooltip("Characters: turn (Y axis only) to look at Leo while talking.")]
        [SerializeField] private bool faceInteractor;
        [Tooltip("Turn back to the original facing when the conversation ends (shopkeepers, statues...).")]
        [SerializeField] private bool returnToOriginalFacing;
        [SerializeField] private float turnSpeed = 540f;

        [Header("Events")]
        public UnityEvent onInspectStarted = new UnityEvent();
        public UnityEvent onInspectFinished = new UnityEvent();

        private Collider cachedCollider;
        private bool consumed;
        private Vector3 punchBaseScale;
        private Coroutine punchRoutine;
        private Coroutine faceRoutine;

        public DialogueSpeaker Speaker => speaker;
        public bool FacesInteractor => faceInteractor;
        public bool ReturnsToOriginalFacing => returnToOriginalFacing;

        /// <summary>True while this character is in a conversation with Leo (movement scripts pause).</summary>
        public bool IsTalking { get; private set; }

        /// <summary>How many times the player has finished inspecting this object.</summary>
        public int TimesInspected => StoryFlags.GetCount(CounterKey);

        private string CounterKey => "inspect:" + (string.IsNullOrEmpty(inspectionId) ? gameObject.name : inspectionId);

        public bool CanInteract
        {
            get
            {
                if (!isActiveAndEnabled || consumed) return false;
                if (string.IsNullOrEmpty(requiredFlag)) return true;
                return requiredFlag[0] == '!' ? !StoryFlags.Has(requiredFlag.Substring(1)) : StoryFlags.Has(requiredFlag);
            }
        }

        public Vector3 PromptAnchor
        {
            get
            {
                if (promptAnchor != null) return promptAnchor.position;
                if (cachedCollider != null) return new Vector3(cachedCollider.bounds.center.x, cachedCollider.bounds.max.y + 0.3f, cachedCollider.bounds.center.z);
                return transform.position + Vector3.up;
            }
        }

        private void Reset()
        {
            inspectionId = gameObject.name;
            punchTarget = transform;
        }

        private void Awake()
        {
            cachedCollider = GetComponentInChildren<Collider>();
            if (punchTarget == null) punchTarget = transform;
            punchBaseScale = punchTarget.localScale;
            if (focusHighlight != null) focusHighlight.SetActive(false);

            if (cachedCollider == null)
            {
                Debug.LogWarning($"[InspectableObject] '{name}' has no Collider; the player can't detect it.", this);
            }
        }

        public void SetFocused(bool focused)
        {
            if (focusHighlight != null) focusHighlight.SetActive(focused);
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract || DialogueManager.Instance == null) return;

            if (turnPlayerToFace && interactor != null) interactor.FaceTowards(transform.position);
            IsTalking = true;
            DialogueManager.Instance.Closed -= HandleDialogueClosed;
            DialogueManager.Instance.Closed += HandleDialogueClosed;
            DialogueManager.PendingSource = transform;
            // The DialogueDirector (when present) handles who looks at whom.
            if (faceInteractor && interactor != null && DialogueDirector.Instance == null)
            {
                if (faceRoutine != null) StopCoroutine(faceRoutine);
                faceRoutine = StartCoroutine(FaceWhileTalking(interactor.transform));
            }
            Punch();
            onInspectStarted?.Invoke();

            bool firstTime = TimesInspected == 0;
            bool hasRepeat = repeatInspection != null && repeatInspection.Length > 0;

            // The branching asset plays on the first inspection (or always, if no repeat lines exist).
            if (sequenceOverride != null && (firstTime || !hasRepeat))
            {
                DialogueManager.Instance.Play(sequenceOverride, OnDialogueFinished);
                return;
            }

            string[] lines = !firstTime && hasRepeat ? repeatInspection : firstInspection;
            DialogueManager.Instance.PlayLines(lines, speaker, OnDialogueFinished, textEffect);
        }

        /// <summary>Ends the "talking" state however the box closed (finished, skipped, replaced).</summary>
        private void HandleDialogueClosed()
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.Closed -= HandleDialogueClosed;
            IsTalking = false;
        }

        /// <summary>Turns smoothly toward Leo and keeps looking at him until the conversation ends.</summary>
        private IEnumerator FaceWhileTalking(Transform leo)
        {
            Quaternion original = transform.rotation;
            while (IsTalking && leo != null)
            {
                Vector3 toLeo = leo.position - transform.position;
                toLeo.y = 0f;
                if (toLeo.sqrMagnitude > 0.0001f)
                {
                    Quaternion goal = Quaternion.LookRotation(toLeo.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * Time.deltaTime);
                }
                yield return null;
            }

            if (returnToOriginalFacing)
            {
                yield return new WaitForSeconds(0.4f);
                while (Quaternion.Angle(transform.rotation, original) > 0.5f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, original, turnSpeed * 0.5f * Time.deltaTime);
                    yield return null;
                }
                transform.rotation = original;
            }
            faceRoutine = null;
        }

        private void OnDialogueFinished()
        {
            IsTalking = false;
            StoryFlags.Increment(CounterKey);
            StoryFlags.Set(flagOnInspect);
            if (singleUse)
            {
                consumed = true;
                SetFocused(false);
            }
            onInspectFinished?.Invoke();
        }

        private void Punch()
        {
            if (punchTarget == null || punchStrength <= 0f) return;
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(PunchRoutine());
        }

        /// <summary>Squash-and-stretch: flattens, then springs back with a damped wobble.</summary>
        private IEnumerator PunchRoutine()
        {
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                float n = t / duration;
                float wobble = Mathf.Sin(n * Mathf.PI * 3f) * (1f - n) * punchStrength;
                punchTarget.localScale = new Vector3(
                    punchBaseScale.x * (1f + wobble),
                    punchBaseScale.y * (1f - wobble),
                    punchBaseScale.z * (1f + wobble));
                yield return null;
            }
            punchTarget.localScale = punchBaseScale;
            punchRoutine = null;
        }
    }
}
