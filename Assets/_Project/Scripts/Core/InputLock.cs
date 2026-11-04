using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Core
{
    /// <summary>
    /// Owner-based gameplay input lock. Cinematics, dialogue and hacking encounters each
    /// acquire the lock with themselves as the owner; the player can move again only when
    /// every owner has released it. This prevents one system from accidentally unlocking
    /// input while another one still needs it (e.g. dialogue ending mid-cutscene).
    /// </summary>
    public static class InputLock
    {
        private static readonly HashSet<object> owners = new HashSet<object>();

        /// <summary>True while at least one system holds the lock.</summary>
        public static bool IsLocked => owners.Count > 0;

        /// <summary>Locks gameplay input on behalf of <paramref name="owner"/>.</summary>
        public static void Acquire(object owner)
        {
            if (owner != null) owners.Add(owner);
        }

        /// <summary>Releases the lock held by <paramref name="owner"/>.</summary>
        public static void Release(object owner)
        {
            if (owner != null) owners.Remove(owner);
        }

        /// <summary>Emergency release used on scene changes.</summary>
        public static void ReleaseAll() => owners.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => owners.Clear();
    }
}
