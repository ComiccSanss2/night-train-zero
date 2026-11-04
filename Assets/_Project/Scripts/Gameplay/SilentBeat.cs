using System.Collections;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// "Stanza di silenzio": after something heavy (Mia's voicemails), Leo can't move for a few seconds.
    /// No text, no music change. The camera drifts in slightly, then lets go.
    /// Trigger it from code with SilentBeat.Hold(4f), or from any dialogue node / choice with the flag "silence_request".
    /// </summary>
    public class SilentBeat : MonoBehaviour
    {
        public const string RequestFlag = "silence_request";
        /// <summary>Twenty seconds: after Byte stays behind.</summary>
        public const string LongRequestFlag = "silence_long";

        private static SilentBeat instance;
        private float pending;

        public static bool IsHolding { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("SilentBeat");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SilentBeat>();
        }

        public static void Hold(float seconds)
        {
            if (instance == null) Bootstrap();
            instance.pending = Mathf.Max(instance.pending, seconds);
        }

        private void Update()
        {
            if (StoryFlags.Has(RequestFlag))
            {
                StoryFlags.Clear(RequestFlag);
                pending = Mathf.Max(pending, 4f);
            }
            if (StoryFlags.Has(LongRequestFlag))
            {
                StoryFlags.Clear(LongRequestFlag);
                pending = Mathf.Max(pending, 20f);
            }
            if (pending <= 0f || IsHolding) return;
            // Wait for the conversation to close: the silence is what comes after the words.
            if (DialogueManager.Instance != null && (DialogueManager.Instance.IsOpen || DialogueManager.Instance.ClosedRecently)) return;
            StartCoroutine(HoldRoutine(pending));
            pending = 0f;
        }

        private IEnumerator HoldRoutine(float seconds)
        {
            IsHolding = true;
            InputLock.Acquire(this);
            GameObject leo = GameObject.FindGameObjectWithTag("Player");
            IsometricCameraRig rig = IsometricCameraRig.Main;
            for (float t = 0f; t < seconds; t += GameTime.UnscaledDelta)
            {
                if (rig != null && leo != null) rig.SetDialogueFocus(leo.transform.position, Mathf.Lerp(0.95f, 0.8f, t / seconds));
                yield return null;
            }
            if (rig != null) rig.SetDialogueFocus(null);
            InputLock.Release(this);
            IsHolding = false;
        }

        private void OnDestroy()
        {
            InputLock.Release(this);
            if (instance == this) instance = null;
        }
    }
}
