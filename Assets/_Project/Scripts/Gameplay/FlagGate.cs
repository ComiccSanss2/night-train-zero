using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>A blockade (static wall, concrete barrier) that dissolves once a story flag is set.</summary>
    public class FlagGate : MonoBehaviour
    {
        [SerializeField] private string openFlag = string.Empty;
        [SerializeField] private Color dissolveColor = new Color(1f, 0.6f, 0.9f);
        [SerializeField] private AudioClip openClip;

        private bool opened;

        private void Start()
        {
            // Already opened in a saved game: no animation, just gone.
            if (!string.IsNullOrEmpty(openFlag) && StoryFlags.Has(openFlag))
            {
                opened = true;
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (opened || string.IsNullOrEmpty(openFlag) || !StoryFlags.Has(openFlag)) return;
            opened = true;
            if (openClip != null) AudioSource.PlayClipAtPoint(openClip, Camera.main != null ? Camera.main.transform.position : transform.position, 0.8f);
            StartCoroutine(Sink());
        }

        /// <summary>
        /// The barrier rumbles, then sinks smoothly into the ground, shedding glowing dust along its base
        /// (no more flickering renderers: the old dissolve read as a rendering bug).
        /// </summary>
        private System.Collections.IEnumerator Sink()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            Vector3 start = transform.position;
            if (CameraSystem.IsometricCameraRig.Main != null) CameraSystem.IsometricCameraRig.Main.AddTrauma(0.35f);

            // Rumble.
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                transform.position = start + new Vector3(Random.Range(-0.03f, 0.03f), 0f, Random.Range(-0.03f, 0.03f));
                if (Random.value < 0.3f) PixelMote.Spawn(new Vector3(Random.Range(b.min.x, b.max.x), b.min.y + 0.05f, Random.Range(b.min.z, b.max.z)), dissolveColor);
                yield return null;
            }

            // Sink below the floor.
            float depth = b.size.y + 0.1f;
            const float duration = 1.6f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float n = t / duration;
                float eased = n * n * (3f - 2f * n);
                transform.position = start + Vector3.down * (depth * eased) + new Vector3(Random.Range(-0.012f, 0.012f), 0f, 0f);
                if (Random.value < 0.6f) PixelMote.Spawn(new Vector3(Random.Range(b.min.x, b.max.x), b.min.y + 0.05f, Random.Range(b.min.z, b.max.z)), dissolveColor);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
