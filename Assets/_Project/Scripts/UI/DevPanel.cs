using Subnet.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Subnet.UI
{
    /// <summary>
    /// In-game test panel (F1). Only exists in the Unity editor and in Development builds, never in the real game.
    /// Jump between chapters, see and change the hidden values (Closeness / Openness), give flags, reset the run.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        private static readonly string[] Chapters = { "00_MainMenu", "01_Tenement", "02_Station", "03_Subnet_Commercial", "04_ConcretePark", "05_CentralCore" };

        // Flags a later chapter normally inherits from earlier ones, so you can start anywhere.
        private static readonly string[][] ChapterFlags =
        {
            new string[0],
            new string[0],
            new[] { "handheld_checked" },
            new[] { "handheld_checked", "read_timetable", "train_arrived", "ticket_punched" },
            new[] { "handheld_checked", "met_byte", "met_kira", "hack1_done", "got_care_package" },
            new[] { "handheld_checked", "met_byte", "met_kira", "hack1_done", "got_care_package", "park_puzzle_solved", "hack2_done", "kira_joined" },
        };

        private bool visible;
        private string flagInput = string.Empty;
        private Vector2 scroll;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("DevPanel (F1)");
            DontDestroyOnLoad(go);
            go.AddComponent<DevPanel>();
        }
#endif

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            GUI.depth = -1000;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(12, 12, 330, 620), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("<b>TEST PANEL</b>  (F1 to hide)", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 });
            GUILayout.Label("Scene: " + SceneManager.GetActiveScene().name);
            GUILayout.Label($"Closeness (Byte/Kira): {Trust.Closeness}   tier {Trust.Tier(Trust.Closeness)}");
            GUILayout.Label($"Openness (Leo): {Trust.Openness}   tier {Trust.Tier(Trust.Openness)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Closeness")) Trust.AddCloseness();
            if (GUILayout.Button("+1 Openness")) Trust.AddOpenness();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Jump to chapter (keeps the story flags it needs):");
            for (int i = 0; i < Chapters.Length; i++)
            {
                if (GUILayout.Button(i + "  " + Chapters[i]))
                {
                    foreach (string flag in ChapterFlags[i]) StoryFlags.Set(flag);
                    visible = false;
                    SceneLoader.Load(Chapters[i], null, false);
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Set a flag (e.g. met_kira, +openness):");
            GUILayout.BeginHorizontal();
            flagInput = GUILayout.TextField(flagInput, GUILayout.Width(200));
            if (GUILayout.Button("Set") && !string.IsNullOrWhiteSpace(flagInput)) { StoryFlags.Set(flagInput.Trim()); flagInput = string.Empty; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Give the handheld (Tab menu)")) StoryFlags.Set("handheld_checked");
            if (GUILayout.Button("Reset run (flags, values, messages)")) StoryFlags.ResetAll();
            if (GUILayout.Button("Reload this scene")) { visible = false; SceneLoader.Load(SceneManager.GetActiveScene().name, null, false); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
