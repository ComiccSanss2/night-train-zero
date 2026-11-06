using UnityEngine;
using UnityEngine.Rendering;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Roofs you can see under: while Leo is inside the zone (a carport, a bandstand), the listed renderers stop
    /// drawing but keep casting their shadow, so the space underneath stays visible from the high camera
    /// without the light changing.
    /// </summary>
    public class CutawayZone : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(6f, 4f, 6f);
        [SerializeField] private Renderer[] hide = { };

        private Transform player;
        private bool hidden;

        private void Start()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            Apply(false);
        }

        private void Update()
        {
            if (player == null) return;
            Vector3 local = player.position - transform.position;
            bool inside = Mathf.Abs(local.x) < size.x * 0.5f && Mathf.Abs(local.z) < size.z * 0.5f && Mathf.Abs(local.y) < size.y * 0.5f;
            if (inside != hidden) Apply(inside);
        }

        private void Apply(bool cut)
        {
            hidden = cut;
            foreach (Renderer r in hide)
                if (r != null) r.shadowCastingMode = cut ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.3f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
