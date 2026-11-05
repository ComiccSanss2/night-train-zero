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
        /// <summary>Same as Capture, without asking to save scenes (used right after a build).</summary>
        public static void CaptureSilently() => Capture(false);

        [MenuItem("Night Train Zero/Debug/Capture Shots")]
        public static void Capture() => Capture(true);

        private static void Capture(bool askToSave)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string list = Path.Combine(root, "Logs", "shots.txt");
            if (!File.Exists(list)) { Debug.LogWarning("[Shots] No Logs/shots.txt"); return; }
            string outDir = Path.Combine(root, "Logs", "shots");
            Directory.CreateDirectory(outDir);
            if (askToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

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

                var temp = new System.Collections.Generic.List<GameObject>();
                bool bars = false;
                if (p[2] == "sit")
                {
                    // Seat preview: copies of the sitters posed on the seat, framed like the game frames them.
                    if (!StageSit(p[3], temp, out Vector3 camPos, out Vector3 look, out float fov)) { Object.DestroyImmediate(go); continue; }
                    go.transform.position = camPos;
                    go.transform.LookAt(look);
                    cam.fieldOfView = fov;
                    bars = true;
                }
                else if (p[2] == "iso")
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
                if (bars)
                {
                    // The cinema bars the game shows (140 of 1080 at the top and bottom).
                    int barPx = Mathf.RoundToInt(720f * 140f / 1080f);
                    for (int y = 0; y < barPx; y++)
                        for (int x = 0; x < 1280; x++) { tex.SetPixel(x, y, Color.black); tex.SetPixel(x, 719 - y, Color.black); }
                }
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, p[1] + ".png"), tex.EncodeToJPG(85));
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(go);
                foreach (GameObject t in temp) if (t != null) Object.DestroyImmediate(t);
                foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                hidden.Clear();
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

        private static readonly System.Collections.Generic.List<Renderer> hidden = new System.Collections.Generic.List<Renderer>();

        private static bool StageSit(string propName, System.Collections.Generic.List<GameObject> temp, out Vector3 camPos, out Vector3 look, out float fov)
        {
            camPos = look = Vector3.zero;
            fov = 30f;
            GameObject prop = GameObject.Find(propName);
            var spot = prop != null ? prop.GetComponent<Subnet.Gameplay.SitSpot>() : null;
            if (spot == null) { Debug.LogWarning("[Shots] No SitSpot " + propName); return false; }
            var so = new SerializedObject(spot);
            var sitters = new System.Collections.Generic.List<(Transform body, Transform seat, float surface)>();
            GameObject leo = GameObject.FindGameObjectWithTag("Player");
            var leoSeat = so.FindProperty("leoSeat").objectReferenceValue as Transform;
            if (leo != null && leoSeat != null) sitters.Add((leo.transform, leoSeat, so.FindProperty("leoSurface").floatValue));
            SerializedProperty ids = so.FindProperty("sitterIds"), seats = so.FindProperty("sitterSeats"), surfaces = so.FindProperty("sitterSurfaces");
            for (int i = 0; i < ids.arraySize && i < seats.arraySize; i++)
            {
                string id = ids.GetArrayElementAtIndex(i).stringValue;
                Subnet.Dialogue.DialogueActor best = null;
                float bestD = 30f;
                foreach (var a in Object.FindObjectsByType<Subnet.Dialogue.DialogueActor>(FindObjectsSortMode.None))
                {
                    if (a.Id != id) continue;
                    float d = Vector3.Distance(a.transform.position, prop.transform.position);
                    if (d < bestD) { best = a; bestD = d; }
                }
                var seat = seats.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (best != null && seat != null) sitters.Add((best.transform, seat, i < surfaces.arraySize ? surfaces.GetArrayElementAtIndex(i).floatValue : float.NaN));
            }
            if (sitters.Count == 0) return false;

            bool any = false;
            Bounds group = default;
            foreach (var st in sitters)
            {
                GameObject copy = Object.Instantiate(st.body.gameObject);
                temp.Add(copy);
                copy.transform.SetPositionAndRotation(st.seat.position, st.seat.rotation);
                Animator anim = copy.GetComponentInChildren<Animator>();
                AnimationClip sit = null;
                if (anim != null && anim.runtimeAnimatorController != null)
                    foreach (AnimationClip c in anim.runtimeAnimatorController.animationClips) if (c.name.ToLowerInvariant() == "sit") sit = c;
                if (sit != null) sit.SampleAnimation(anim.gameObject, sit.length * 0.5f);
                Bounds b = Subnet.CameraSystem.IsometricCameraRig.BodyBounds(copy.transform);
                if (!float.IsNaN(st.surface)) copy.transform.position += Vector3.up * (st.surface + 0.01f - b.min.y);
                b = Subnet.CameraSystem.IsometricCameraRig.BodyBounds(copy.transform);
                if (!any) { group = b; any = true; } else group.Encapsulate(b);
                // Hide the original so it doesn't stand in the shot.
                foreach (Renderer r in st.body.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            }

            var shotPoint = so.FindProperty("shotPoint").objectReferenceValue as Transform;
            var shotLook = so.FindProperty("shotLook").objectReferenceValue as Transform;
            fov = so.FindProperty("shotFov").floatValue;
            Vector3 dir = shotPoint != null && shotLook != null ? (shotLook.position - shotPoint.position).normalized : new Vector3(0.6f, -0.3f, 0.7f).normalized;
            // Same fit as IsometricCameraRig.FrameBounds.
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 up = Vector3.Cross(dir, right).normalized;
            Vector3 e = group.extents;
            float halfW = Mathf.Abs(right.x) * e.x + Mathf.Abs(right.y) * e.y + Mathf.Abs(right.z) * e.z;
            float halfH = Mathf.Abs(up.x) * e.x + Mathf.Abs(up.y) * e.y + Mathf.Abs(up.z) * e.z;
            float halfD = Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.y) * e.y + Mathf.Abs(dir.z) * e.z;
            float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float distance = Mathf.Max(halfH * 1.12f / (tanV * Subnet.CameraSystem.IsometricCameraRig.FramedHeight), halfW * 1.12f / (tanV * (16f / 9f) * 0.92f), 1.2f) + halfD;
            look = group.center;
            camPos = group.center - dir * distance;
            return true;
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
