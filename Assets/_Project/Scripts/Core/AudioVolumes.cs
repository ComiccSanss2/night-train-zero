using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subnet.Core
{
    /// <summary>
    /// Makes the SFX slider work on every sound in the game (rain, hum, doors, cutscene ambience, menus), not only
    /// the ones that existed when the scene loaded. Each source keeps its own authored volume; this scales it by
    /// the SFX setting every frame. Scripts that fade a source write its raw volume as usual — the change is
    /// detected and re-scaled. Music has its own channel (MusicDirector); master is the AudioListener.
    /// </summary>
    public class AudioVolumes : MonoBehaviour
    {
        private class Entry
        {
            public AudioSource source;
            public float authored;
            public float applied;
        }

        private static AudioVolumes instance;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly HashSet<int> known = new HashSet<int>();
        private float scanTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("[AudioVolumes]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioVolumes>();
            SceneManager.sceneLoaded += (s, m) => { if (instance != null) instance.scanTimer = 0f; };
        }

        /// <summary>Re-applies right away (after a slider moved).</summary>
        public static void Refresh()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (instance != null) instance.scanTimer = 0f;
        }

        private static bool IsMusic(AudioSource source)
        {
            return source.GetComponentInParent<Audio.MusicDirector>() != null
                || source.GetComponent<Audio.ProceduralAmbience>() != null; // scales itself on the audio thread
        }

        private void LateUpdate()
        {
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.5f;
                AudioListener.volume = GameSettings.MasterVolume;
                foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (s == null || known.Contains(s.GetInstanceID())) continue;
                    known.Add(s.GetInstanceID());
                    if (IsMusic(s)) continue;
                    entries.Add(new Entry { source = s, authored = s.volume, applied = -1f });
                }
            }

            float sfx = GameSettings.SfxVolume;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry e = entries[i];
                if (e.source == null) { entries.RemoveAt(i); continue; }
                // Something else wrote a new raw volume (a fade, a script): that becomes the authored level.
                if (e.applied >= 0f && Mathf.Abs(e.source.volume - e.applied) > 0.0005f) e.authored = e.source.volume;
                e.applied = e.authored * sfx;
                e.source.volume = e.applied;
            }
        }
    }
}
