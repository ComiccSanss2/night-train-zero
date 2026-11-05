using UnityEngine;

namespace Subnet.Audio
{
    /// <summary>
    /// While Leo is inside this box, the city stems are rebalanced (the rooftop goes quiet, an apartment hears
    /// the street through its walls). The song never restarts: only the mix moves.
    /// </summary>
    public class StemZone : MonoBehaviour
    {
        [SerializeField] private StemMix mix = StemMix.Interior;
        [SerializeField] private Vector3 size = new Vector3(10f, 6f, 10f);

        private Transform player;
        private bool inside;

        private void Start()
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        private void Update()
        {
            if (player == null) return;
            Vector3 local = player.position - transform.position;
            bool now = Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
            if (now == inside) return;
            inside = now;
            if (inside) MusicDirector.Ensure().PushMix(mix);
            else MusicDirector.Ensure().PopMix(mix);
        }

        private void OnDisable()
        {
            if (inside && MusicDirector.Instance != null) MusicDirector.Instance.PopMix(mix);
            inside = false;
        }
    }
}
