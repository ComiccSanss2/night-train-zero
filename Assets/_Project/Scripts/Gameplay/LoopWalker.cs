using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Walks forever around a circle: consumerist mannequins trapped in a shopping time-loop.
    /// </summary>
    public class LoopWalker : MonoBehaviour
    {
        [SerializeField] private Vector3 center;
        [SerializeField] private float radius = 2f;
        [Tooltip("Degrees per second. Negative = clockwise.")]
        [SerializeField] private float angularSpeed = 25f;
        [SerializeField] private float startAngle;
        [SerializeField] private Animator animator;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private float angle;
        private Interaction.InspectableObject talker;

        public void Configure(Vector3 loopCenter, float loopRadius, float degreesPerSecond, float startDegrees)
        {
            center = loopCenter;
            radius = loopRadius;
            angularSpeed = degreesPerSecond;
            startAngle = startDegrees;
        }

        private void Start()
        {
            angle = startAngle;
            talker = GetComponent<Interaction.InspectableObject>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.nameHash == SpeedId) animator.SetFloat(SpeedId, 0.6f);
                }
            }
        }

        private void Update()
        {
            // Stop the loop (and let the mannequin look at Leo) while it's being talked to.
            if (talker != null && talker.IsTalking) return;
            angle += angularSpeed * Time.deltaTime;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
            transform.position = new Vector3(center.x + offset.x, transform.position.y, center.z + offset.z);

            // Face along the tangent of the circle.
            Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * Mathf.Sign(angularSpeed);
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }
}
