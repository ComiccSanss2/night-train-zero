#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subnet.Core
{
    /// <summary>
    /// Editor-only test hook: "Night Train Zero/Debug/Test ..." menu items store a spawn ("x,y,z|flags") and play a scene;
    /// on load the flags are set and Leo is moved there, so a moment deep in the story can be checked in seconds.
    /// </summary>
    public static class DebugSpawn
    {
        public const string Key = "ntz.debug.spawn";

        private static string pending;

        // Flags go in before the scene wakes up (intros and arrival beats check them in Awake/Start).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SetFlags()
        {
            pending = UnityEditor.EditorPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(pending)) return;
            UnityEditor.EditorPrefs.DeleteKey(Key);
            string[] parts = pending.Split('|');
            if (parts.Length > 1) foreach (string f in parts[1].Split(';')) if (!string.IsNullOrWhiteSpace(f)) StoryFlags.Set(f.Trim());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            string value = pending;
            pending = null;
            if (string.IsNullOrEmpty(value)) return;
            string[] parts = value.Split('|');
            string[] c = parts[0].Split(',');
            if (c.Length < 3) return;
            var pos = new Vector3(float.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(c[1], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture));
            GameObject leo = GameObject.FindGameObjectWithTag("Player");
            if (leo == null) return;
            var cc = leo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            leo.transform.position = pos;
            if (cc != null) cc.enabled = true;
            var rig = Object.FindFirstObjectByType<CameraSystem.IsometricCameraRig>();
            if (rig != null) rig.SnapToTarget();
            Debug.Log("[DebugSpawn] " + SceneManager.GetActiveScene().name + " at " + pos);
            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                new GameObject("[DebugInteract]").AddComponent<DebugInteract>().target = parts[2];
        }

        /// <summary>Uses an interactable by name a moment after spawning (sit on the crate, read the map...).</summary>
        private class DebugInteract : MonoBehaviour
        {
            public string target;

            private System.Collections.IEnumerator Start()
            {
                yield return new WaitForSeconds(2.5f);
                while (Dialogue.DialogueManager.Instance != null && Dialogue.DialogueManager.Instance.IsOpen) yield return null;
                GameObject go = GameObject.Find(target);
                GameObject leo = GameObject.FindGameObjectWithTag("Player");
                if (go != null && leo != null && go.TryGetComponent(out Interaction.IInteractable it))
                    it.Interact(leo.GetComponent<Player.PlayerInteractor>());
                else Debug.LogWarning("[DebugSpawn] Can't interact with " + target);
                Destroy(gameObject);
            }
        }
    }
}
#endif
