using Subnet.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Subnet.Player
{
    /// <summary>
    /// Camera-relative 8-directional movement for the fixed isometric camera.
    /// - "Up" on the stick / W always moves toward the top of the screen, regardless of camera yaw.
    /// - Separate acceleration and deceleration give weight without feeling floaty.
    /// - Optional snapping of input to 8 directions (classic RPG feel) while keeping analog speed.
    /// - Stops gracefully (decelerates) whenever InputLock is held by dialogue or cinematics.
    /// Requires a CharacterController. Drives an optional Animator float parameter for idle/walk blending.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class IsometricPlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Camera that defines 'up' on screen. Empty = Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Optional animator on the character model (e.g. Kenney mini-character).")]
        [SerializeField] private Animator animator;
        [Tooltip("Float parameter receiving normalized speed (0 = idle, 1 = full walk).")]
        [SerializeField] private string animatorSpeedParameter = "Speed";

        [Header("Movement")]
        [SerializeField] private float maxSpeed = 4.2f;
        [Tooltip("Units/second² when speeding up.")]
        [SerializeField] private float acceleration = 28f;
        [Tooltip("Units/second² when slowing down or releasing the stick.")]
        [SerializeField] private float deceleration = 36f;
        [Tooltip("Extra multiplier on acceleration when reversing direction (snappier turns).")]
        [SerializeField] private float turnAroundBoost = 1.6f;
        [Tooltip("Degrees per second the character rotates toward its movement direction.")]
        [SerializeField] private float turnSpeed = 900f;
        [Tooltip("Quantize input to 8 directions (N, NE, E...). Analog magnitude is preserved.")]
        [SerializeField] private bool snapToEightDirections = true;
        [Range(0f, 0.5f)] [SerializeField] private float inputDeadzone = 0.15f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -25f;
        [Tooltip("Small downward force while grounded so the controller hugs slopes and stairs.")]
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Footsteps")]
        [Tooltip("Distance travelled between footstep events.")]
        [SerializeField] private float strideLength = 0.75f;
        [Tooltip("Raised every stride while grounded. Hook the footstep audio system here.")]
        public UnityEvent onFootstep = new UnityEvent();

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float strideAccumulator;
        private int speedParameterHash;
        private bool hasSpeedParameter;

        /// <summary>Current horizontal velocity in world space.</summary>
        public Vector3 PlanarVelocity => planarVelocity;

        /// <summary>0..1 of max speed.</summary>
        public float NormalizedSpeed => maxSpeed > 0f ? planarVelocity.magnitude / maxSpeed : 0f;

        /// <summary>When false, the controller ignores input entirely (used by teleports, ragdoll beats...).</summary>
        public bool MovementEnabled { get; set; } = true;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            CacheAnimatorParameter();
        }

        private void CacheAnimatorParameter()
        {
            hasSpeedParameter = false;
            if (animator == null || string.IsNullOrEmpty(animatorSpeedParameter) || animator.runtimeAnimatorController == null) return;

            speedParameterHash = Animator.StringToHash(animatorSpeedParameter);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == speedParameterHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParameter = true;
                    break;
                }
            }
        }

        private bool scripted;

        /// <summary>
        /// Walks Leo to a point on his own (door entrances), ignoring colliders. Plays the walk animation.
        /// </summary>
        public System.Collections.IEnumerator ScriptedWalk(Vector3 target, float speed = 2.2f)
        {
            scripted = true;
            controller.enabled = false;
            planarVelocity = Vector3.zero;
            Vector3 start = transform.position;
            target.y = start.y;
            FaceTowards(target);
            float duration = Mathf.Max(0.05f, Vector3.Distance(start, target) / Mathf.Max(0.1f, speed));
            float stride = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                Vector3 previous = transform.position;
                transform.position = Vector3.Lerp(start, target, t / duration);
                if (hasSpeedParameter) animator.SetFloat(speedParameterHash, 0.75f, 0.05f, Time.deltaTime);
                stride += Vector3.Distance(previous, transform.position);
                if (strideLength > 0f && stride >= strideLength) { stride -= strideLength; onFootstep?.Invoke(); }
                yield return null;
            }
            transform.position = target;
            if (hasSpeedParameter) animator.SetFloat(speedParameterHash, 0f);
            controller.enabled = true;
            scripted = false;
        }

        private void Update()
        {
            if (scripted) return;
            float dt = Time.deltaTime;
            Vector3 desiredVelocity = ComputeDesiredVelocity();

            // Pick acceleration rate: decelerate when releasing input, boost when reversing direction.
            float rate;
            if (desiredVelocity.sqrMagnitude < 0.0001f)
            {
                rate = deceleration;
            }
            else
            {
                bool reversing = Vector3.Dot(desiredVelocity, planarVelocity) < 0f;
                rate = acceleration * (reversing ? turnAroundBoost : 1f);
            }
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, rate * dt);

            // Gravity.
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = groundedStickForce;
            else verticalVelocity += gravity * dt;

            Vector3 motion = (planarVelocity + Vector3.up * verticalVelocity) * dt;
            CollisionFlags flags = controller.Move(motion);

            // Kill velocity pushing into walls so we don't "stick" and re-accelerate oddly.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = controller.velocity;
                actual.y = 0f;
                if (actual.sqrMagnitude < planarVelocity.sqrMagnitude) planarVelocity = actual;
            }

            RotateTowardsVelocity(dt, desiredVelocity);
            UpdateFootsteps(dt);
            UpdateAnimator();
        }

        private Vector3 ComputeDesiredVelocity()
        {
            if (!MovementEnabled || InputLock.IsLocked) return Vector3.zero;

            Vector2 input = GameInput.MoveVector;
            float magnitude = input.magnitude;
            if (magnitude < inputDeadzone) return Vector3.zero;

            // Remap so speed ramps from 0 at the deadzone edge to 1 at full tilt.
            float strength = Mathf.InverseLerp(inputDeadzone, 1f, magnitude);
            Vector2 direction = input / magnitude;

            if (snapToEightDirections)
            {
                float angle = Mathf.Atan2(direction.y, direction.x);
                const float step = Mathf.PI / 4f;
                angle = Mathf.Round(angle / step) * step;
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            GetCameraBasis(out Vector3 forward, out Vector3 right);
            Vector3 worldDirection = (forward * direction.y + right * direction.x).normalized;
            return worldDirection * (maxSpeed * strength);
        }

        private void GetCameraBasis(out Vector3 forward, out Vector3 right)
        {
            if (cameraTransform == null)
            {
                forward = Vector3.forward;
                right = Vector3.right;
                return;
            }

            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward);
        }

        private void RotateTowardsVelocity(float dt, Vector3 desired)
        {
            // Face where the player is pushing, not where a wall makes Leo slide:
            // pressing "up" against a wall keeps him looking at the wall (and what's on it).
            Vector3 flat = desired.sqrMagnitude > 0.04f ? desired : planarVelocity;
            if (flat.sqrMagnitude < 0.04f) return;
            Quaternion target = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }

        private void UpdateFootsteps(float dt)
        {
            if (!controller.isGrounded || strideLength <= 0f)
            {
                strideAccumulator = 0f;
                return;
            }

            strideAccumulator += planarVelocity.magnitude * dt;
            if (strideAccumulator >= strideLength)
            {
                strideAccumulator -= strideLength;
                onFootstep?.Invoke();
            }
        }

        private void UpdateAnimator()
        {
            if (!hasSpeedParameter) return;
            animator.SetFloat(speedParameterHash, NormalizedSpeed, 0.08f, Time.deltaTime);
        }

        /// <summary>Instantly rotates the character to look at <paramref name="worldPoint"/> (flattened).</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>Moves the character safely (CharacterController must be disabled to teleport).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
