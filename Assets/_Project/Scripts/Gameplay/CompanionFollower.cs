using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Byte (and Kira) trail behind Leo once a story flag is set.
    /// - Moves with its own smoothed velocity (no jitter), never blocks the player.
    /// - While moving, faces its travel direction; while idle, faces Leo.
    /// - Drives an optional Animator "Speed" float.
    /// </summary>
    public class CompanionFollower : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Start following once this flag is set. Empty = follow from the start.")]
        [SerializeField] private string followFlag = string.Empty;
        [SerializeField] private float followDistance = 1.3f;
        [Tooltip("Sideways offset so two companions don't overlap (-1 left, +1 right).")]
        [SerializeField] private float sideOffset = 0.6f;
        [SerializeField] private float maxSpeed = 5.5f;
        [SerializeField] private float acceleration = 18f;
        [Tooltip("Closer than this to its slot, the companion stops and idles.")]
        [SerializeField] private float arriveRadius = 0.35f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float teleportDistance = 9f;
        [Tooltip("Rotation offset if the model's nose doesn't point along +Z.")]
        [SerializeField] private float modelYawOffset = 0f;
        [SerializeField] private Animator animator;

        private static readonly int SpeedId = Animator.StringToHash("Speed");

        private bool following;
        private Vector3 velocity;
        private Vector3 slotDirection;
        private Vector3 lastTargetPosition;
        private bool hasSpeedParameter;
        private Interaction.InspectableObject talker;

        public bool IsFollowing => following;

        private void Awake()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            if (animator == null) animator = GetComponentInChildren<Animator>();
            talker = GetComponent<Interaction.InspectableObject>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.nameHash == SpeedId) hasSpeedParameter = true;
                }
            }
        }

        private void Update()
        {
            if (target == null) return;
            // During any conversation the DialogueDirector stages companions (turning, walking over).
            if ((talker != null && talker.IsTalking) ||
                (Dialogue.DialogueManager.Instance != null && Dialogue.DialogueManager.Instance.IsOpen))
            {
                velocity = Vector3.zero;
                lastTargetPosition = target.position;
                return;
            }
            if (!following)
            {
                following = string.IsNullOrEmpty(followFlag) || StoryFlags.Has(followFlag);
                if (!following) return;
                slotDirection = -target.forward;
                lastTargetPosition = target.position;
            }

            float dt = Time.deltaTime;

            // The slot sits behind Leo's direction of TRAVEL, not his facing: when Leo stops and turns
            // around to talk, the companion stays put in front of him instead of swinging behind again.
            Vector3 playerVelocity = dt > 0f ? (target.position - lastTargetPosition) / dt : Vector3.zero;
            lastTargetPosition = target.position;
            playerVelocity.y = 0f;
            if (playerVelocity.sqrMagnitude > 1f)
            {
                slotDirection = Vector3.Slerp(slotDirection, -playerVelocity.normalized, 2.5f * dt);
            }
            Vector3 right = Vector3.Cross(Vector3.up, -slotDirection).normalized;
            Vector3 slot = target.position + slotDirection.normalized * followDistance + right * sideOffset;
            slot.y = target.position.y;

            Vector3 toSlot = slot - transform.position;
            toSlot.y = 0f;
            float distance = toSlot.magnitude;

            if (distance > teleportDistance)
            {
                transform.position = slot;
                velocity = Vector3.zero;
                return;
            }

            // Arrive steering: full speed far away, gentle slow-down near the slot, stop inside the radius.
            Vector3 desiredVelocity = Vector3.zero;
            if (distance > arriveRadius)
            {
                float speed = Mathf.Min(maxSpeed, distance * 3f);
                desiredVelocity = toSlot / distance * speed;
            }
            velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * dt);
            transform.position += velocity * dt;
            transform.position = new Vector3(transform.position.x, target.position.y, transform.position.z);

            // Facing: travel direction while moving, otherwise look at Leo.
            Vector3 look = velocity.sqrMagnitude > 0.09f ? velocity : target.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                Quaternion goal = Quaternion.LookRotation(look.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * dt);
            }

            if (hasSpeedParameter) animator.SetFloat(SpeedId, Mathf.Clamp01(velocity.magnitude / 3.5f), 0.1f, dt);
        }
    }
}
