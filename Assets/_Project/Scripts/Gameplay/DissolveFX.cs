using System.Collections;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// "Dissolves into warm pixels": the target flickers, shrinks and sheds small glowing cubes
    /// that drift upward, then deactivates. Used when an emotional obstacle is resolved.
    /// Usage: DissolveFX.Play(gameObject, warmColor);
    /// </summary>
    public class DissolveFX : MonoBehaviour
    {
        private Color color;
        private float duration;

        public static void Play(GameObject target, Color color, float duration = 1.4f)
        {
            if (target == null || !target.activeInHierarchy) return;
            foreach (Collider c in target.GetComponentsInChildren<Collider>()) c.enabled = false;
            var fx = target.AddComponent<DissolveFX>();
            fx.color = color;
            fx.duration = duration;
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            Vector3 startScale = transform.localScale;
            int motes = Mathf.Clamp(Mathf.RoundToInt(bounds.size.magnitude * 10f), 16, 60);
            float spawnInterval = duration / motes;
            float spawnTimer = 0f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float n = t / duration;

                // Flicker harder as it fades.
                bool visible = Random.value > n * 0.8f;
                foreach (Renderer r in renderers) if (r != null) r.enabled = visible;

                transform.localScale = Vector3.Lerp(startScale, startScale * 0.05f, n * n);

                spawnTimer -= Time.deltaTime;
                while (spawnTimer <= 0f)
                {
                    spawnTimer += spawnInterval;
                    Vector3 p = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        Random.Range(bounds.min.y, bounds.max.y),
                        Random.Range(bounds.min.z, bounds.max.z));
                    PixelMote.Spawn(p, color);
                }
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
