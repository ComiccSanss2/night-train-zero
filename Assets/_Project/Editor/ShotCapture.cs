#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>
    /// Debug helper: renders still shots of the built scenes to Logs/shots/*.png so the layout can be checked
    /// without playing. Reads Logs/shots.txt, one shot per line:
    ///   scene | name | iso | focusX,focusY,focusZ [| distance | yaw]
    ///   scene | name | free | posX,posY,posZ | lookX,lookY,lookZ [| fov]
    /// Lines starting with # are ignored.
    /// </summary>
    public static class ShotCapture
    {
        [MenuItem("Night Train Zero/Debug/Capture Shots")]
        public static void Capture()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string list = Path.Combine(root, "Logs", "shots.txt");
            if (!File.Exists(list)) { Debug.LogWarning("[Shots] No Logs/shots.txt"); return; }
            string outDir = Path.Combine(root, "Logs", "shots");
            Directory.CreateDirectory(outDir);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string currentScene = null;
            int count = 0;
            foreach (string raw in File.ReadAllLines(list))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] p = line.Split('|');
                for (int i = 0; i < p.Length; i++) p[i] = p[i].Trim();
                string scene = p[0];
                if (scene != currentScene)
                {
                    string path = "Assets/_Project/Scenes/" + scene + ".unity";
                    if (!File.Exists(Path.Combine(root, path))) { Debug.LogWarning("[Shots] Missing scene " + path); continue; }
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    currentScene = scene;
                }

                var go = new GameObject("__ShotCam");
                Camera cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 200f;
                Camera main = Camera.main;
                if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }

                if (p[2] == "iso")
                {
                    Vector3 focus = V(p[3]);
                    float distance = p.Length > 4 ? F(p[4]) : 13f;
                    float yaw = p.Length > 5 ? F(p[5]) : 45f;
                    Quaternion rot = Quaternion.Euler(35f, yaw, 0f);
                    go.transform.SetPositionAndRotation(focus + new Vector3(0f, 0.8f, 0f) - rot * Vector3.forward * distance, rot);
                    cam.fieldOfView = 28f;
                }
                else
                {
                    go.transform.position = V(p[3]);
                    go.transform.LookAt(V(p[4]));
                    cam.fieldOfView = p.Length > 5 ? F(p[5]) : 40f;
                }

                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, p[1] + ".png"), tex.EncodeToJPG(85));
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(go);
                count++;
            }
            Debug.Log("[Shots] Captured " + count + " shots to " + outDir);
        }

        [MenuItem("Night Train Zero/Debug/Play Station")] private static void PlayStation() => PlayScene("02_Station");
        [MenuItem("Night Train Zero/Debug/Play Subnet")] private static void PlaySubnet() => PlayScene("03_Subnet_Commercial");
        [MenuItem("Night Train Zero/Debug/Play Park")] private static void PlayPark() => PlayScene("04_ConcretePark");
        [MenuItem("Night Train Zero/Debug/Play Ghost Line")] private static void PlayGhost() => PlayScene("04b_GhostLine");
        [MenuItem("Night Train Zero/Debug/Play Core")] private static void PlayCore() => PlayScene("05_CentralCore");

        private const string StoryToPark = "handheld_checked;met_byte;met_kira;hack1_done;got_care_package";
        [MenuItem("Night Train Zero/Debug/Test Roof")] private static void TestRoof() => PlayAt("03_Subnet_Commercial", "69.4,6.05,0.2|" + StoryToPark + Skip("03_Subnet_Commercial") + "|Prop_RoofCrate");
        [MenuItem("Night Train Zero/Debug/Test Roof Ledge")] private static void TestLedge() => PlayAt("03_Subnet_Commercial", "72.2,6.05,2.6|" + StoryToPark + Skip("03_Subnet_Commercial") + "|Prop_RoofLedge");
        [MenuItem("Night Train Zero/Debug/Test Roof Tank")] private static void TestTank() => PlayAt("03_Subnet_Commercial", "66.6,6.05,1.0|" + StoryToPark + Skip("03_Subnet_Commercial") + "|Prop_RoofTank");
        [MenuItem("Night Train Zero/Debug/Test Weeper Fight")] private static void TestWeeper() => PlayAt("04_ConcretePark", "9.6,0.05,0|" + StoryToPark + ";park_puzzle_solved" + Skip("04_ConcretePark") + "|Obstacle_WeepingCommuter");
        [MenuItem("Night Train Zero/Debug/Test Park Map")] private static void TestMap() => PlayAt("04_ConcretePark", "-9.5,0.05,1.0|" + StoryToPark + Skip("04_ConcretePark") + "|Prop_ParkMap");
        [MenuItem("Night Train Zero/Debug/Test Station Board")] private static void TestBoard() => PlayAt("02_Station", "-6.5,0.05,1.0|handheld_checked;evt.02_Station.Event_Arrive" + "|Prop_Timetable");

        // Flags that stop a scene's arrival beats from playing during a quick test.
        private static string Skip(string scene) => ";intro." + scene + ";evt." + scene + ".Event_Arrive;evt." + scene + ".Event_AfterHack1;evt." + scene + ".Event_ByteHint;evt." + scene + ".Event_RoofInvite;evt." + scene + ".Event_WalkByte";

        private static void PlayAt(string scene, string spawn)
        {
            EditorPrefs.SetString("ntz.debug.spawn", spawn);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            PlayScene(scene);
        }

        private static void PlayScene(string scene)
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + scene + ".unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private static Vector3 V(string s)
        {
            string[] c = s.Split(',');
            return new Vector3(F(c[0]), F(c[1]), F(c[2]));
        }
    }
}
#endif
