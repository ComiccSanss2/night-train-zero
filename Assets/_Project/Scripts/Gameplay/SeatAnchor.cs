using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// A seat a character can be told to sit on mid-conversation ({sit=kira}, {sit=leo}): train seats, a bench.
    /// The transform faces the way a sitter looks; <see cref="seatHeight"/> is the seat surface above the transform.
    /// </summary>
    public class SeatAnchor : MonoBehaviour
    {
        private static readonly List<SeatAnchor> all = new List<SeatAnchor>();
        public static IReadOnlyList<SeatAnchor> All => all;

        public float seatHeight = 0.45f;
        [System.NonSerialized] public Transform occupant;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        /// <summary>The free seat closest to <paramref name="from"/> within <paramref name="range"/> metres.</summary>
        public static SeatAnchor Nearest(Vector3 from, float range)
        {
            SeatAnchor best = null;
            float bestDistance = range;
            foreach (SeatAnchor s in all)
            {
                if (s == null || s.occupant != null) continue;
                float d = Vector3.Distance(from, s.transform.position);
                if (d < bestDistance) { best = s; bestDistance = d; }
            }
            return best;
        }
    }
}
