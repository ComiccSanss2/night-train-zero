using Subnet.Core;
using UnityEngine;

namespace Subnet.CameraSystem
{
    /// <summary>
    /// Fixed-angle isometric follow camera (default 35° pitch, 45° yaw).
    /// Put this on the Main Camera. The CinematicDirector disables it during cutscenes and
    /// blends back to <see cref="GetDesiredPose"/> before re-enabling it.
    /// Also owns trauma-based screen shake (respects the Screen Shake accessibility toggle).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraRig : MonoBehaviour
    {
        /// <summary>The active rig in the scene (for shake calls from anywhere).</summary>
        public static IsometricCameraRig Main { get; private set; }

        [Header("Target")]
        [SerializeField] private Transform target;
        [Tooltip("Offset from the target's pivot the camera looks at (chest height reads best).")]
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);

        [Header("Angles")]
        [Range(10f, 80f)] [SerializeField] private float pitch = 35f;
        [SerializeField] private float yaw = 45f;
        [SerializeField] private float distance = 16f;

        [Header("Lens")]
        [Tooltip("Low FOV + long distance = isometric look with perspective depth. Ignored for orthographic cameras.")]
        [SerializeField] private float fieldOfView = 26f;

        [Header("Follow")]
        [Tooltip("Higher = tighter follow.")]
        [SerializeField] private float followSharpness = 6f;
        [Tooltip("Camera leads slightly in the direction the target moves.")]
        [SerializeField] private float velocityLead = 0.35f;

        [Header("Shake")]
        [SerializeField] private float maxShakeOffset = 0.35f;
        [SerializeField] private float maxShakeRoll = 2.5f;
        [SerializeField] private float traumaDecay = 1.6f;
        [SerializeField] private float shakeFrequency = 22f;

        private Camera cam;
        private Vector3 focusPoint;
        private Vector3 lastTargetPosition;
        private float trauma;
        private Vector3? dialogueFocus;
        private float dialogueZoom = 1f;
        private float currentZoom = 1f;
        private float focusBlend;

        /// <summary>
        /// Leans the camera toward a point (whoever is talking) and zooms in a little. null = back to normal.
        /// </summary>
        public void SetDialogueFocus(Vector3? point, float zoom = 0.82f)
        {
            if (point.HasValue) dialogueFocus = point;
            dialogueZoom = point.HasValue ? zoom : 1f;
            focusTarget = point.HasValue ? 1f : 0f;
        }

        private float focusTarget;

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                SnapToTarget();
            }
        }

        public float FieldOfView => fieldOfView;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            Main = this;
        }

        private void OnEnable()
        {
            Main = this;
            SnapToTarget();
        }

        private void OnDestroy()
        {
            if (Main == this) Main = null;
        }

        /// <summary>Teleports the camera to its resting pose (no smoothing).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            focusPoint = target.position + lookOffset;
            lastTargetPosition = target.position;
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            if (cam != null && !cam.orthographic) cam.fieldOfView = fieldOfView;
        }

        /// <summary>The pose the rig would settle at right now (used for cinematic blends).</summary>
        public void GetDesiredPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 focus = target != null ? target.position + lookOffset : focusPoint;
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            position = focus - rotation * Vector3.forward * distance;
        }

        /// <summary>
        /// Adds screen shake. 0.2 = small bump (close call), 0.5 = firewall break, 1 = huge.
        /// Ignored when the player disabled Screen Shake in SYSTEM CONFIG.
        /// </summary>
        public void AddTrauma(float amount)
        {
            if (!GameSettings.ScreenShake) return;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            // Lead in the direction of travel so the player sees where they're going.
            Vector3 targetVelocity = dt > 0f ? (target.position - lastTargetPosition) / dt : Vector3.zero;
            lastTargetPosition = target.position;
            targetVelocity.y = 0f;

            Vector3 desiredFocus = target.position + lookOffset + targetVelocity * velocityLead;
            focusBlend = Mathf.MoveTowards(focusBlend, focusTarget, dt * 2.5f);
            if (dialogueFocus.HasValue && focusBlend > 0f)
            {
                float k = Easing.Evaluate(EaseType.EaseInOutCubic, focusBlend);
                desiredFocus = Vector3.Lerp(desiredFocus, dialogueFocus.Value + lookOffset, k);
            }
            focusPoint = Vector3.Lerp(focusPoint, desiredFocus, 1f - Mathf.Exp(-followSharpness * dt));
            currentZoom = Mathf.Lerp(currentZoom, dialogueZoom, 1f - Mathf.Exp(-3f * dt));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = focusPoint - rotation * Vector3.forward * (distance * currentZoom);

            // Trauma² shake: perceptually smoother than linear.
            if (trauma > 0f)
            {
                float shake = trauma * trauma;
                float time = Time.time * shakeFrequency;
                Vector3 offset = new Vector3(
                    Mathf.PerlinNoise(time, 0.1f) * 2f - 1f,
                    Mathf.PerlinNoise(0.2f, time) * 2f - 1f,
                    0f) * (maxShakeOffset * shake);
                position += rotation * offset;
                rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(time, time) * 2f - 1f) * maxShakeRoll * shake);
                trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
