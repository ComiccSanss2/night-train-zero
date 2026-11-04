#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>
    /// One window with every button needed to test the game (works the same on Windows and macOS).
    /// Menu: Night Train Zero → Dashboard. Opens by itself the first time the project is opened on a machine.
    /// </summary>
    [InitializeOnLoad]
    public class NightTrainZeroDashboard : EditorWindow
    {
        private const string Scenes = "Assets/_Project/Scenes/";
        private const string OpenedKey = "NightTrainZero.DashboardShown";

        private static readonly (string label, string scene)[] Chapters =
        {
            ("00  Main menu", "00_MainMenu"),
            ("01  The boxes (apartment)", "01_Tenement"),
            ("02  Central Station", "02_Station"),
            ("03  Subnet: shopping district", "03_Subnet_Commercial"),
            ("04  Concrete Park", "04_ConcretePark"),
            ("05  Central Core (final boss)", "05_CentralCore"),
        };

        private Vector2 scroll;

        static NightTrainZeroDashboard()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(OpenedKey, false)) return;
                EditorPrefs.SetBool(OpenedKey, true);
                Open();
            };
        }

        [MenuItem("Night Train Zero/Dashboard", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<NightTrainZeroDashboard>("Night Train Zero");
            window.minSize = new Vector2(320, 460);
        }

        [MenuItem("Night Train Zero/Build Full Game", priority = 20)]
        public static void BuildFullGame() => SubnetGameBuilder.Build();

        [MenuItem("Night Train Zero/Play from Main Menu", priority = 21)]
        public static void PlayFromMenu() => PlayScene("00_MainMenu");

        [MenuItem("Night Train Zero/Reset Save Data", priority = 40)]
        public static void ResetSave()
        {
            foreach (string key in new[] { "save.flags", "save.counters", "save.phone", "save.checkpointScene" }) PlayerPrefs.DeleteKey(key);
            for (int i = 1; i <= 9; i++) PlayerPrefs.DeleteKey("save.chapter.ch" + i);
            PlayerPrefs.Save();
            Debug.Log("[Night Train Zero] Save data cleared (settings kept).");
        }

        private static bool ScenesBuilt => File.Exists(Scenes + "00_MainMenu.unity");

        private static void PlayScene(string scene)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            string path = Scenes + scene + ".unity";
            if (!File.Exists(path))
            {
                if (EditorUtility.DisplayDialog("Night Train Zero", "The scenes haven't been generated yet on this computer.\nBuild the full game now?", "Build", "Cancel"))
                {
                    SubnetGameBuilder.Build();
                }
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
            EditorApplication.isPlaying = true;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            GUILayout.Label("NIGHT TRAIN ZERO", title);
            EditorGUILayout.HelpBox(ScenesBuilt
                ? "Scenes are ready. Press a chapter to jump straight in.\nIn game: Esc = pause, Tab = handheld, F1 = test panel."
                : "First time on this computer? Press BUILD FULL GAME once (takes ~1 minute).", ScenesBuilt ? MessageType.Info : MessageType.Warning);

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(1f, 0.55f, 0.85f);
            if (GUILayout.Button("BUILD FULL GAME", GUILayout.Height(40))) SubnetGameBuilder.Build();
            GUI.backgroundColor = new Color(0.45f, 1f, 0.9f);
            if (GUILayout.Button("▶  PLAY FROM MAIN MENU", GUILayout.Height(34))) PlayScene("00_MainMenu");
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);
            GUILayout.Label("Jump to a chapter (starts there directly)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!ScenesBuilt))
            {
                foreach (var chapter in Chapters)
                {
                    if (GUILayout.Button(chapter.label, GUILayout.Height(24))) PlayScene(chapter.scene);
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset save data (keeps settings)")) ResetSave();
            if (GUILayout.Button("Open Build Profiles (export for Windows / macOS)"))
            {
                if (!EditorApplication.ExecuteMenuItem("File/Build Profiles")) EditorApplication.ExecuteMenuItem("File/Build Settings...");
            }
            if (GUILayout.Button("Stop playing")) EditorApplication.isPlaying = false;
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
