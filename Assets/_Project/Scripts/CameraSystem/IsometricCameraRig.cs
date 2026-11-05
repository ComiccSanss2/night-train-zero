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

        // ------------------------------------------------------------------ Framed shots (sitting, big moments)
        private Vector3 shotPosition;
        private Vector3 shotLook;
        private float shotFov = 30f;
        private float shotTarget;
        private float shotBlend;
        private float shotAge;
        private bool fovTouched;

        /// <summary>True while a framed shot is showing (or blending).</summary>
        public bool InShot => shotBlend > 0f;

        /// <summary>
        /// Leaves the isometric angle for a framed shot (a close-up, two people on a bench). Blends over ~1 s
        /// and keeps drifting in very slowly, so a long line feels like the camera is leaning in to listen.
        /// </summary>
        public void SetShot(Vector3 position, Vector3 lookAt, float fov = 30f)
        {
            if (shotTarget <= 0f || (position - shotPosition).sqrMagnitude > 0.04f) shotAge = 0f;
            shotPosition = position;
            shotLook = lookAt;
            shotFov = fov;
            shotTarget = 1f;
        }

        /// <summary>Back to the isometric follow camera.</summary>
        public void ClearShot() => shotTarget = 0f;

        /// <summary>World box around a character's visible meshes (particles ignored).</summary>
        public static Bounds BodyBounds(Transform body)
        {
            bool any = false;
            Bounds b = new Bounds(body.position + Vector3.up * 0.5f, Vector3.one * 0.5f);
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Share of the screen height left between the cinema bars (dialogue sits inside the bottom bar).</summary>
        public const float FramedHeight = 0.72f;

        /// <summary>
        /// Frames <paramref name="subject"/> (a box around the characters) seen along <paramref name="viewDirection"/>:
        /// close enough to feel intimate, far enough that nobody's head or feet get cut by the bars.
        /// </summary>
        public void FrameBounds(Bounds subject, Vector3 viewDirection, float fov = 30f, float margin = 1.12f)
        {
            Camera c = cam != null ? cam : GetComponent<Camera>();
            float aspect = c != null ? c.aspect : 16f / 9f;
            Vector3 dir = viewDirection.sqrMagnitude > 0.0001f ? viewDirection.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(dir, right).normalized;
            Vector3 e = subject.extents;
            // Half-size of the box as seen by the camera.
            float halfW = Mathf.Abs(right.x) * e.x + Mathf.Abs(right.y) * e.y + Mathf.Abs(right.z) * e.z;
            float halfH = Mathf.Abs(up.x) * e.x + Mathf.Abs(up.y) * e.y + Mathf.Abs(up.z) * e.z;
            float halfD = Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.y) * e.y + Mathf.Abs(dir.z) * e.z;
            float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float distV = halfH * margin / (tanV * FramedHeight);
            float distH = halfW * margin / (tanV * aspect * 0.92f);
            float distance = Mathf.Max(distV, distH, 1.2f) + halfD;
            SetShot(subject.center - dir * distance, subject.center, fov);
        }

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

            // Framed shot on top of the follow pose.
            shotBlend = Mathf.MoveTowards(shotBlend, shotTarget, dt / 0.9f);
            if (shotBlend > 0f)
            {
                shotAge += dt;
                float k = Easing.Evaluate(EaseType.EaseInOutCubic, shotBlend);
                Vector3 toLook = shotLook - shotPosition;
                Vector3 drift = toLook.normalized * Mathf.Min(0.15f, shotAge * 0.02f);
                Vector3 shotPos = shotPosition + drift;
                position = Vector3.Lerp(position, shotPos, k);
                // Aim a little below the subject so faces sit in the upper part of the frame, clear of the dialogue box.
                Quaternion aim = Quaternion.LookRotation(shotLook - shotPos, Vector3.up);
                rotation = Quaternion.Slerp(rotation, aim, k);
                if (cam != null && !cam.orthographic)
                {
                    cam.fieldOfView = Mathf.Lerp(fieldOfView, shotFov, k);
                    fovTouched = true;
                }
            }
            else if (fovTouched && cam != null && !cam.orthographic)
            {
                cam.fieldOfView = fieldOfView;
                fovTouched = false;
            }

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
