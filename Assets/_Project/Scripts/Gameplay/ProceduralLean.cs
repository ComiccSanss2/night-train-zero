using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Juice for walking characters: leans the model into its movement and adds a small bouncy step bob,
    /// plus a squash when starting/stopping. Put it on the visual child (not the root that moves).
    /// Runs in LateUpdate so it layers on top of any Animator pose.
    /// </summary>
    public class ProceduralLean : MonoBehaviour
    {
        [SerializeField] private float maxLeanDegrees = 9f;
        [SerializeField] private float leanPerSpeed = 3f;
        [SerializeField] private float bobHeight = 0.035f;
        [SerializeField] private float bobFrequency = 10f;
        [SerializeField] private float squashAmount = 0.08f;

        private Vector3 basePosition;
        private Vector3 baseScale;
        private Vector3 lastParentPosition;
        private float lean;
        private float previousSpeed;
        private float squash;
        private float bobPhase;
        private float talk;
        private float emphasis;

        /// <summary>Little hop + stretch while this character talks (called per voice blip).</summary>
        public void Talk(float amount = 0.35f) => talk = Mathf.Min(1f, talk + amount);

        /// <summary>Bigger jolt for {big} words / {quake}.</summary>
        public void Emphasize(float amount = 1f) => emphasis = Mathf.Min(1f, emphasis + amount);

        private void Start()
        {
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            lastParentPosition = transform.parent != null ? transform.parent.position : transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || transform.parent == null) return;

            Vector3 position = transform.parent.position;
            Vector3 velocity = (position - lastParentPosition) / dt;
            lastParentPosition = position;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            // Squash on sudden start / stop.
            float change = Mathf.Abs(speed - previousSpeed);
            if (change > 1.5f) squash = Mathf.Min(1f, squash + change * 0.15f);
            previousSpeed = Mathf.Lerp(previousSpeed, speed, 12f * dt);
            squash = Mathf.MoveTowards(squash, 0f, 4f * dt);

            lean = Mathf.Lerp(lean, Mathf.Clamp(speed * leanPerSpeed, 0f, maxLeanDegrees), 10f * dt);
            transform.localRotation = Quaternion.Euler(lean, 0f, 0f);

            float moving = Mathf.Clamp01(speed / 3f);
            bobPhase += dt * bobFrequency * Mathf.Lerp(0.5f, 1f, moving);
            float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight * moving;
            talk = Mathf.MoveTowards(talk, 0f, 5f * dt);
            emphasis = Mathf.MoveTowards(emphasis, 0f, 3f * dt);
            float talkHop = Mathf.Sin(talk * Mathf.PI) * 0.035f + Mathf.Sin(emphasis * Mathf.PI) * 0.12f;
            transform.localPosition = basePosition + Vector3.up * (bob + talkHop);

            float s = squash * squashAmount - talk * 0.05f - emphasis * 0.12f;
            transform.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
        }
    }
}
