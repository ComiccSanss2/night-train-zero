using UnityEngine;

namespace Subnet.Audio
{
    /// <summary>
    /// While Leo stands inside this box (e.g. a shop interior), its track replaces the city music.
    /// Leaving brings the previous track back, resumed where it was.
    /// </summary>
    public class MusicZone : MonoBehaviour
    {
        [SerializeField] private AudioClip track;
        [SerializeField] private Vector3 size = new Vector3(10f, 6f, 10f);
        [SerializeField] private float fadeIn = 0.8f;
        [SerializeField] private float fadeOut = 1.2f;

        private Transform player;
        private bool inside;
        private AudioClip previous;

        private void Start()
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        private bool Contains(Vector3 point)
        {
            Vector3 local = point - transform.position;
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        private void Update()
        {
            if (player == null || track == null) return;
            MusicDirector director = MusicDirector.Ensure();
            if (director.InFight) return;
            bool now = Contains(player.position);
            if (now == inside) return;
            inside = now;
            if (inside)
            {
                previous = director.CurrentCityClip != track ? director.CurrentCityClip : previous;
                director.PlayCity(track, fadeIn);
            }
            else if (previous != null) director.PlayCity(previous, fadeOut);
            else director.StopCity(fadeOut);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.4f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
