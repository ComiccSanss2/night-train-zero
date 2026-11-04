using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>Constant rotation plus a gentle vertical bob (floating clocks, The Algorithm, memory files).</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 45f, 0f);
        public float bobAmplitude = 0.15f;
        public float bobSpeed = 1.5f;

        private Vector3 basePosition;
        private float seed;

        private void Start()
        {
            basePosition = transform.localPosition;
            seed = Random.value * 10f;
        }

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            if (bobAmplitude > 0f)
            {
                transform.localPosition = basePosition + Vector3.up * (Mathf.Sin((Time.time + seed) * bobSpeed) * bobAmplitude);
            }
        }
    }
}
