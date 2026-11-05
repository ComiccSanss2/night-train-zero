#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    /// <summary>
    /// The rooftop (act II breathing room), built to match what the dialogue says: a parapet ledge facing the
    /// city with the drop below, a water tank on legs you can sit under, two crates side by side, string lights,
    /// AC units, an antenna — and the city all around, with neon signs. Plus the sitting helpers used by every
    /// bench/seat in the game, and a small build report (Logs/build_report.txt).
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== Build report

        private static readonly List<string> report = new List<string>();

        private static void ReportLine(string line)
        {
            if (!report.Contains(line)) report.Add(line);
        }

        private static void WriteReport()
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                Directory.CreateDirectory(Path.Combine(root, "Logs"));
                File.WriteAllLines(Path.Combine(root, "Logs", "build_report.txt"), report);
            }
            catch (System.Exception e) { Debug.LogWarning("[Subnet] Could not write build report: " + e.Message); }
            report.Clear();
        }

        // ====================================================================== Sitting

        private static readonly Dictionary<string, float> hipCache = new Dictionary<string, float>();

        /// <summary>
        /// How high the hips are above the feet in the model's "sit" pose (sampled from the clip), so a sitter's
        /// root can be placed at seatTop - hip and the character lands exactly on the seat.
        /// </summary>
        private static float SitHip(string model, string hint, float height)
        {
            string key = model + "@" + height;
            if (hipCache.TryGetValue(key, out float cached)) return cached;
            float hip = height * 0.3f;
            GameObject asset = Model(model, hint);
            if (asset != null)
            {
                var holder = new GameObject("__SitProbe");
                GameObject inst = SpawnModel(asset, holder.transform, height, false, 0f);
                AnimationClip sit = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name.ToLowerInvariant() == "sit");
                if (sit != null)
                {
                    sit.SampleAnimation(inst, sit.length * 0.5f);
                    Transform bone = inst.GetComponentsInChildren<Transform>()
                        .FirstOrDefault(t => { string n = t.name.ToLowerInvariant(); return n.Contains("hip") || n.Contains("pelvis"); })
                        ?? inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => { string n = t.name.ToLowerInvariant(); return n.Contains("torso") || n.Contains("body"); });
                    if (bone != null)
                    {
                        hip = Mathf.Clamp(bone.position.y - holder.transform.position.y, height * 0.12f, height * 0.5f);
                        ReportLine($"sit hip {model} h={height}: bone '{bone.name}' → {hip:F3}");
                    }
                    else ReportLine($"sit hip {model}: no hip bone. Bones: " + string.Join(",", inst.GetComponentsInChildren<Transform>().Select(t => t.name)));
                }
                Object.DestroyImmediate(holder);
            }
            hipCache[key] = hip;
            return hip;
        }

        /// <summary>
        /// The way a seat faces, read from its mesh: the backrest is the tall part, the front is the other side.
        /// Returns <paramref name="fallback"/> for symmetric things (crates, stools).
        /// </summary>
        private static float SeatFacing(GameObject prop, float fallback)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter mf in prop.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices) points.Add(mf.transform.TransformPoint(v));
            }
            if (points.Count < 8) return fallback;
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in points) center += p;
            center /= points.Count;
            List<Vector3> top = points.Where(p => p.y > minY + (maxY - minY) * 0.7f).ToList();
            if (top.Count == 0) return fallback;
            Vector3 topCenter = Vector3.zero;
            foreach (Vector3 p in top) topCenter += p;
            topCenter /= top.Count;
            Vector3 front = center - topCenter;
            front.y = 0f;
            float size = Mathf.Max(0.1f, (maxY - minY));
            if (front.magnitude < size * 0.06f) return fallback;
            float yaw = Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg;
            ReportLine($"seat facing {prop.name}: {yaw:F0}° (fallback {fallback:F0})");
            return yaw;
        }

        private struct Sitter
        {
            public string id;        // "leo", "kira", "byte"
            public Vector3 seat;     // horizontal position of the hips; y = seat surface
            public float yaw;
        }

        /// <summary>
        /// Makes an inspectable prop a place to sit: Leo (and companions) sit for real while its lines play,
        /// framed by a camera at <paramref name="camPos"/> looking at <paramref name="camLook"/>.
        /// </summary>
        private static void SitHere(GameObject prop, Sitter[] seats, Vector3 camPos, Vector3 camLook, float fov = 32f)
        {
            if (prop == null || prop.GetComponent<InspectableObject>() == null) return;
            var spot = prop.AddComponent<SitSpot>();
            var ids = new List<string>();
            var others = new List<Transform>();
            foreach (Sitter s in seats)
            {
                float hip = s.id == "leo" ? SitHip("character-male-b", "mini-characters", 1.0f)
                    : s.id == "kira" ? SitHip("character-female-c", "mini-characters", 1.05f)
                    : 0f; // Byte folds down where he is
                var t = new GameObject("Seat_" + s.id).transform;
                t.SetParent(prop.transform, false);
                t.position = new Vector3(s.seat.x, s.seat.y - hip, s.seat.z);
                t.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                if (s.id == "leo") Set(spot, "leoSeat", t);
                else { ids.Add(s.id); others.Add(t); }
            }
            SetStrings(spot, "sitterIds", ids.ToArray());
            var surfaces = new List<float>();
            foreach (Sitter st in seats) if (st.id == "leo") Set(spot, "leoSurface", st.seat.y); else surfaces.Add(st.seat.y);
            var sso = new SerializedObject(spot);
            SerializedProperty sp = sso.FindProperty("sitterSurfaces");
            sp.arraySize = surfaces.Count;
            for (int i = 0; i < surfaces.Count; i++) sp.GetArrayElementAtIndex(i).floatValue = surfaces[i];
            sso.ApplyModifiedPropertiesWithoutUndo();
            var so = new SerializedObject(spot);
            SerializedProperty list = so.FindProperty("sitterSeats");
            list.arraySize = others.Count;
            for (int i = 0; i < others.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = others[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var shot = new GameObject("SitShot").transform;
            shot.SetParent(prop.transform, false);
            shot.position = camPos;
            var look = new GameObject("SitLook").transform;
            look.SetParent(prop.transform, false);
            look.position = camLook;
            Set(spot, "shotPoint", shot);
            Set(spot, "shotLook", look);
            Set(spot, "shotFov", fov + 6f);
        }

        /// <summary>Sit spot on a bench/chair prop: works out the facing and seat height from the model.</summary>
        private static void BenchSeat(GameObject prop, float fallbackYaw, float seatHeightFraction = 0.45f, params string[] companions)
        {
            if (prop == null) return;
            Transform model = prop.transform.Find("Model");
            if (model == null) return;
            Bounds b = WorldBounds(model.gameObject);
            float yaw = SeatFacing(model.gameObject, fallbackYaw);
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Vector3 front = r * Vector3.forward, side = r * Vector3.right;
            float seatY = b.min.y + b.size.y * seatHeightFraction;
            Vector3 c = new Vector3(b.center.x, seatY, b.center.z) + front * 0.05f;
            // Find the real seat surface: cast down onto the mesh just in front of the backrest.
            float? hit = SeatSurface(model.gameObject, new Vector3(c.x, b.max.y + 0.5f, c.z) + front * 0.08f, b.size.y + 1f);
            if (hit.HasValue && hit.Value > b.min.y + 0.1f && hit.Value < b.max.y - 0.05f)
            {
                seatY = hit.Value;
                c.y = seatY;
                ReportLine($"seat {prop.name}: surface at {seatY - b.min.y:F2} m (bench height {b.size.y:F2})");
            }
            var seats = new List<Sitter> { new Sitter { id = "leo", seat = c - side * (companions.Length > 0 ? 0.3f : 0f), yaw = yaw } };
            for (int i = 0; i < companions.Length; i++)
                seats.Add(new Sitter { id = companions[i], seat = c + side * (0.35f + i * 0.4f) + Vector3.up * (companions[i] == "byte" ? 0f : 0f), yaw = yaw });
            Vector3 camPos = c + front * 2.6f + side * 0.9f + Vector3.up * 0.9f;
            SitHere(prop, seats.ToArray(), camPos, c + Vector3.up * 0.35f);
        }

        /// <summary>Raycasts the model's own meshes (temporary colliders) to find the top of the seat.</summary>
        private static float? SeatSurface(GameObject model, Vector3 from, float length)
        {
            var temp = new List<MeshCollider>();
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            Physics.SyncTransforms();
            float? best = null;
            foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, length, ~0, QueryTriggerInteraction.Collide))
            {
                if (!(h.collider is MeshCollider m) || !temp.Contains(m)) continue;
                if (!best.HasValue || h.point.y > best.Value) best = h.point.y;
            }
            foreach (MeshCollider mc in temp) Object.DestroyImmediate(mc);
            return best;
        }

        /// <summary>A seat characters can be told to sit on mid-dialogue with {sit=kira}.</summary>
        private static void SeatAnchorAt(Ctx ctx, Vector3 floorPosition, float yaw, float seatHeight)
        {
            var go = new GameObject("SeatAnchor");
            go.transform.SetParent(ctx.props, false);
            go.transform.SetPositionAndRotation(floorPosition, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<SeatAnchor>().seatHeight = seatHeight;
        }

        /// <summary>
        /// A proper lamp post (base, pole, arm over the path, glowing head) instead of the furniture kit's indoor
        /// floor lamp blown up to street size. Root has the collider; the visuals live under "Model".
        /// </summary>
        private static GameObject LampPost(Ctx ctx, string key, Vector3 position, float height, Color glow, float armYaw = 180f,
            string[] first = null, string[] repeat = null, DialogueSpeaker speaker = null)
        {
            var root = new GameObject("Prop_" + key);
            root.transform.SetParent(ctx.props, false);
            root.transform.position = position;
            root.layer = ctx.interactableLayer;
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localRotation = Quaternion.Euler(0f, armYaw, 0f);
            Material iron = LitMaterial("Props/M_LampIron", new Color(0.16f, 0.15f, 0.2f), 0.45f);
            Material glass = EmissiveMaterial("Props/M_LampGlass_" + key, glow * 0.4f, glow * 2.2f);
            Vector3 P(float x, float y, float z) => model.TransformPoint(new Vector3(x, y, z));
            Vector3 e = new Vector3(0f, armYaw, 0f);
            Deco("LampBase", model, P(0, 0.12f, 0), new Vector3(0.26f, 0.24f, 0.26f), iron, e).transform.SetParent(model, true);
            Deco("LampPole", model, P(0, height * 0.5f, 0), new Vector3(0.08f, height, 0.08f), iron, e).transform.SetParent(model, true);
            Deco("LampArm", model, P(0, height - 0.05f, 0.28f), new Vector3(0.06f, 0.06f, 0.6f), iron, e).transform.SetParent(model, true);
            Deco("LampHood", model, P(0, height - 0.08f, 0.6f), new Vector3(0.34f, 0.1f, 0.26f), iron, e).transform.SetParent(model, true);
            Deco("LampGlass", model, P(0, height - 0.16f, 0.6f), new Vector3(0.26f, 0.06f, 0.2f), glass, e).transform.SetParent(model, true);
            PointLight(root.transform, P(0, height - 0.35f, 0.6f), glow, 2f, 5.5f);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.size = new Vector3(0.45f, height, 0.45f);
            if (first != null && first.Length > 0) Inspectable(root, key, first, repeat, null, speaker);
            return root;
        }

        /// <summary>World-space sign text shrinks to fit its box instead of spilling past the edges.</summary>
        private static void FitText(TMPro.TextMeshPro t)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = t.fontSize * 0.2f;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.overflowMode = TMPro.TextOverflowModes.Truncate;
        }

        /// <summary>Inspecting this sign zooms the camera to it and opens it full-size on screen.</summary>
        private static void ReadableSign(GameObject sign, Subnet.UI.SignBoard.Layout layout, Vector3 cameraPosition, Vector3 lookAt)
        {
            var board = sign.AddComponent<Subnet.UI.SignBoard>();
            var so = new SerializedObject(board);
            so.FindProperty("layout").enumValueIndex = (int)layout;
            so.ApplyModifiedPropertiesWithoutUndo();
            var cam = new GameObject("SignCamera").transform;
            cam.SetParent(sign.transform.parent, false);
            cam.position = cameraPosition;
            var look = new GameObject("SignLook").transform;
            look.SetParent(sign.transform.parent, false);
            look.position = lookAt;
            Set(board, "cameraPoint", cam);
            Set(board, "cameraLook", look);
        }

        // ====================================================================== The rooftop

        private static void BuildRoofSet(Ctx ctx, Vector3 roof)
        {
            const string F = "furniture";
            Material floor = LitMaterial("Roof/M_RoofFloor", new Color(0.26f, 0.24f, 0.32f));
            Material parapet = LitMaterial("Roof/M_Parapet", new Color(0.42f, 0.38f, 0.5f));
            Material cap = LitMaterial("Roof/M_ParapetCap", new Color(0.55f, 0.5f, 0.62f));
            Material metal = LitMaterial("Roof/M_Metal", new Color(0.45f, 0.48f, 0.55f), 0.55f);
            Material tankMat = LitMaterial("Roof/M_Tank", new Color(0.55f, 0.42f, 0.36f), 0.3f);
            Material wood = LitMaterial("Roof/M_CrateWood", new Color(0.62f, 0.45f, 0.3f));
            Material woodDark = LitMaterial("Roof/M_CrateWoodDark", new Color(0.42f, 0.3f, 0.2f));
            Material bulb = EmissiveMaterial("Roof/M_Bulb", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.75f, 0.45f) * 3f);
            Material wire = LitMaterial("Roof/M_Wire", new Color(0.08f, 0.08f, 0.1f));
            Material red = EmissiveMaterial("Roof/M_AntennaRed", new Color(0.4f, 0f, 0f), new Color(1f, 0.15f, 0.2f) * 3f);
            Material street = LitMaterial("Roof/M_StreetBelow", new Color(0.12f, 0.1f, 0.16f));
            Material lane = EmissiveMaterial("Roof/M_LaneBelow", new Color(0.1f, 0.3f, 0.3f), Cyan * 1.5f);

            // Roof slab and parapet (the ledge is the parapet's cap: 0.55 m high, wide enough to sit on).
            Block("RoofFloor", ctx.env, roof + new Vector3(0, -0.1f, 0), new Vector3(12f, 0.2f, 8f), floor, ctx.groundLayer);
            Block("RoofBody", ctx.env, roof + new Vector3(0, -3.1f, 0), new Vector3(12f, 5.8f, 8f), parapet, 0);
            const float ledge = 0.55f;
            void Wall(string n, Vector3 c, Vector2 size)
            {
                Block("Parapet_" + n, ctx.env, roof + c + new Vector3(0, ledge * 0.5f - 0.03f, 0), new Vector3(size.x, ledge - 0.06f, size.y), parapet, 0);
                Deco("ParapetCap_" + n, ctx.env, roof + c + new Vector3(0, ledge - 0.03f, 0), new Vector3(size.x + 0.06f, 0.06f, size.y + 0.06f), cap);
            }
            Wall("N", new Vector3(0, 0, 3.85f), new Vector2(12.3f, 0.35f));
            Wall("S", new Vector3(0, 0, -3.85f), new Vector2(12.3f, 0.35f));
            Wall("E", new Vector3(5.85f, 0, 0), new Vector2(0.35f, 8f));
            Wall("W", new Vector3(-5.85f, 0, 0), new Vector2(0.35f, 8f));
            // Invisible rails so nobody walks off.
            Block("RoofRail_N", ctx.env, roof + new Vector3(0, 1f, 3.85f), new Vector3(12.3f, 2f, 0.35f), null, 0);
            Block("RoofRail_S", ctx.env, roof + new Vector3(0, 1f, -3.85f), new Vector3(12.3f, 2f, 0.35f), null, 0);
            Block("RoofRail_E", ctx.env, roof + new Vector3(5.85f, 1f, 0), new Vector3(0.35f, 2f, 8f), null, 0);
            Block("RoofRail_W", ctx.env, roof + new Vector3(-5.85f, 1f, 0), new Vector3(0.35f, 2f, 8f), null, 0);

            // Stair hut where the fire escape comes up (south-west corner).
            Block("StairHut", ctx.env, roof + new Vector3(-4.6f, 1.1f, -2.6f), new Vector3(2f, 2.2f, 1.9f), parapet, 0);
            Deco("StairHutRoof", ctx.env, roof + new Vector3(-4.6f, 2.25f, -2.6f), new Vector3(2.2f, 0.1f, 2.1f), cap);
            Deco("StairHutDoor", ctx.env, roof + new Vector3(-3.58f, 0.9f, -2.6f), new Vector3(0.04f, 1.8f, 0.9f), metal);

            // Water tank on legs, north-west: you sit in its shadow, back against a leg.
            Vector3 tank = roof + new Vector3(-3.6f, 0f, 2.2f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 leg = new Vector3(i < 2 ? -0.75f : 0.75f, 0.9f, i % 2 == 0 ? -0.75f : 0.75f);
                Deco("TankLeg" + i, ctx.env, tank + leg, new Vector3(0.12f, 1.8f, 0.12f), metal);
            }
            Deco("TankPlatform", ctx.env, tank + new Vector3(0, 1.85f, 0), new Vector3(1.9f, 0.1f, 1.9f), woodDark);
            GameObject drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(drum.GetComponent<Collider>());
            drum.name = "TankDrum";
            drum.transform.SetParent(ctx.env, false);
            drum.transform.position = tank + new Vector3(0, 2.75f, 0);
            drum.transform.localScale = new Vector3(1.7f, 0.85f, 1.7f);
            drum.GetComponent<Renderer>().sharedMaterial = tankMat;
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(lid.GetComponent<Collider>());
            lid.name = "TankLid";
            lid.transform.SetParent(ctx.env, false);
            lid.transform.position = tank + new Vector3(0, 3.65f, 0);
            lid.transform.localScale = new Vector3(1.45f, 0.08f, 1.45f);
            lid.GetComponent<Renderer>().sharedMaterial = metal;
            for (int i = 0; i < 6; i++)
                Deco("TankRung" + i, ctx.env, tank + new Vector3(0.95f, 0.3f + i * 0.3f, 0f), new Vector3(0.04f, 0.04f, 0.45f), metal);

            // Clutter: AC units, a vent, plants, the antenna with its blinking light.
            for (int i = 0; i < 2; i++)
            {
                Vector3 ac = roof + new Vector3(3.2f + i * 1.3f, 0.45f, -2.6f);
                Deco("AC" + i, ctx.env, ac, new Vector3(1f, 0.9f, 0.8f), metal);
                Deco("ACGrille" + i, ctx.env, ac + new Vector3(0, 0.1f, -0.41f), new Vector3(0.7f, 0.5f, 0.02f), wire);
            }
            Deco("Vent", ctx.env, roof + new Vector3(1.2f, 0.5f, -3.1f), new Vector3(0.35f, 1f, 0.35f), metal);
            Deco("VentCap", ctx.env, roof + new Vector3(1.2f, 1.05f, -3.1f), new Vector3(0.55f, 0.1f, 0.55f), metal);
            Prop(ctx, "RoofPlant1", "pottedPlant", F, roof + new Vector3(5.1f, 0f, 3.2f), 0f, 0.9f, false, null);
            Prop(ctx, "RoofPlant2", "pottedPlant", F, roof + new Vector3(-1.9f, 0f, 3.3f), 40f, 0.7f, false, null);
            Deco("Antenna", ctx.env, roof + new Vector3(5.2f, 1.9f, -3.3f), new Vector3(0.08f, 3.8f, 0.08f), metal);
            Deco("AntennaBar", ctx.env, roof + new Vector3(5.2f, 3.2f, -3.3f), new Vector3(0.9f, 0.05f, 0.05f), metal);
            Deco("AntennaLight", ctx.env, roof + new Vector3(5.2f, 3.85f, -3.3f), new Vector3(0.14f, 0.14f, 0.14f), red);
            PointLight(ctx.env, roof + new Vector3(5.2f, 3.85f, -3.3f), new Color(1f, 0.2f, 0.3f), 1.5f, 4f);

            // String lights zig-zagging between three poles.
            Vector3[] poles = { roof + new Vector3(-5.4f, 0, 3.4f), roof + new Vector3(5.4f, 0, 3.4f), roof + new Vector3(5.4f, 0, -1.6f), roof + new Vector3(-1.4f, 0, -0.4f) };
            foreach (Vector3 p in poles) Deco("LightPole", ctx.env, p + new Vector3(0, 1.3f, 0), new Vector3(0.07f, 2.6f, 0.07f), metal);
            for (int s = 0; s < poles.Length - 1; s++)
            {
                Vector3 a = poles[s] + Vector3.up * 2.55f, bnd = poles[s + 1] + Vector3.up * 2.55f;
                int n = Mathf.CeilToInt(Vector3.Distance(a, bnd) / 0.6f);
                for (int i = 1; i < n; i++)
                {
                    float t = i / (float)n;
                    Vector3 q = Vector3.Lerp(a, bnd, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.45f);
                    GameObject g = Deco("Bulb", ctx.env, q, new Vector3(0.09f, 0.11f, 0.09f), bulb);
                    g.transform.rotation = Quaternion.Euler(0, 45, 0);
                }
                if (s % 2 == 0) PointLight(ctx.env, Vector3.Lerp(a, bnd, 0.5f) - Vector3.up * 0.6f, new Color(1f, 0.75f, 0.5f), 1.1f, 5f);
            }

            // The city around: streets far below, buildings whose tops sit around roof level, neon signs.
            Block("StreetBelow", ctx.env, new Vector3(roof.x, -0.05f, roof.z), new Vector3(90f, 0.1f, 70f), street, 0);
            for (int i = 0; i < 14; i++) Deco("LaneBelow" + i, ctx.env, new Vector3(roof.x - 40f + i * 6f, 0.02f, roof.z - 8f), new Vector3(2.4f, 0.02f, 0.18f), lane);
            string[] blocks = { "building-a", "building-c", "building-e", "building-g", "building-i", "building-b", "building-d", "building-h" };
            string[] towers = { "building-skyscraper-a", "building-skyscraper-c", "building-skyscraper-e", "building-skyscraper-b" };
            BuildingRow(ctx.env, blocks, "commercial", roof.x - 26f, roof.x + 28f, roof.z + 7f, 5.5f, 1.5f, 0.6f, 7f, 180f, 10);
            BuildingRow(ctx.env, towers, "commercial", roof.x - 30f, roof.x + 30f, roof.z + 17f, 13f, 4f, 2f, 9f, 180f, 8);
            BuildingRow(ctx.env, blocks.Reverse().ToArray(), "commercial", roof.x + 8f, roof.x + 30f, roof.z - 6f, 4.5f, 1f, 0.6f, 6f, 180f, 4);

            (string text, Color color, Vector3 pos, float size)[] signs =
            {
                ("NEON MART", new Color(1f, 0.45f, 0.85f), roof + new Vector3(-9f, 3.2f, 9f), 9f),
                ("OPEN 24/7", Cyan, roof + new Vector3(-9f, 2.2f, 9f), 5f),
                ("SUBNET", new Color(0.7f, 0.6f, 1f), roof + new Vector3(4f, 5.5f, 19f), 14f),
                ("0451", new Color(0.85f, 0.9f, 1f), roof + new Vector3(10f, 2.6f, 8.5f), 7f),
                ("NO SIGNAL", new Color(1f, 0.3f, 0.35f), roof + new Vector3(16f, 6.5f, 18f), 8f),
                ("ROOM 9", new Color(1f, 0.85f, 0.5f), roof + new Vector3(-16f, 7f, 19f), 7f),
            };
            Material frame = LitMaterial("Roof/M_SignFrame", new Color(0.05f, 0.04f, 0.07f));
            foreach (var s in signs)
            {
                var sign = WorldSign(ctx.env, s.text, s.pos, 180f, s.color, s.size);
                sign.rectTransform.sizeDelta = new Vector2(s.text.Length * s.size * 0.1f + 1f, s.size * 0.16f);
                Deco("SignBack_" + s.text, ctx.env, s.pos + new Vector3(0, 0, 0.08f), new Vector3(s.text.Length * s.size * 0.075f + 0.6f, s.size * 0.15f, 0.08f), frame);
                PointLight(ctx.env, s.pos + new Vector3(0, 0, -1f), s.color, 1.2f, 5f);
            }
        }

        /// <summary>The three roof seats: two crates, the ledge, under the water tank.</summary>
        private static void BuildRoofSeats(Ctx ctx, Vector3 roof)
        {
            Material wood = LitMaterial("Roof/M_CrateWood", new Color(0.62f, 0.45f, 0.3f));
            Material woodDark = LitMaterial("Roof/M_CrateWoodDark", new Color(0.42f, 0.3f, 0.2f));

            // Two crates side by side, facing the camera side of the roof.
            Vector3 crateA = roof + new Vector3(-0.6f, 0f, 1.4f), crateB = roof + new Vector3(0.15f, 0f, 1.25f);
            GameObject crate = Block("Prop_RoofCrate", ctx.props, crateA + new Vector3(0, 0.25f, 0), new Vector3(0.62f, 0.5f, 0.62f), wood, ctx.interactableLayer);
            crate.transform.rotation = Quaternion.Euler(0, 8f, 0);
            Deco("CrateSlat", ctx.props, crateA + new Vector3(0, 0.25f, -0.315f), new Vector3(0.62f, 0.08f, 0.02f), woodDark);
            GameObject crate2 = Block("Prop_RoofCrate2", ctx.props, crateB + new Vector3(0, 0.25f, 0), new Vector3(0.62f, 0.5f, 0.62f), wood, 0);
            crate2.transform.rotation = Quaternion.Euler(0, -12f, 0);
            Deco("CrateSlat2", ctx.props, crateB + new Vector3(0, 0.25f, -0.315f), new Vector3(0.62f, 0.08f, 0.02f), woodDark);
            Inspectable(crate, "RoofCrate",
                L("* You sit on the crate. Kira sits on the next one, swinging her skates.",
                  "{cam=kira}Fun fact: I've delivered to every building you can see from here. {p}{small}Except one.{/small}",
                  "{emote=no}Don't ask which one. {p}{fast}You're going to ask. Don't ask.{/fast}"),
                L("* The crate creaks. Kira pretends it was her."), "roof_seat_crate;+closeness#roof_seat_crate", spkKira);
            SitHere(crate, new[]
            {
                new Sitter { id = "leo", seat = crateA + Vector3.up * 0.5f, yaw = 190f },
                new Sitter { id = "kira", seat = crateB + Vector3.up * 0.5f, yaw = 170f },
            }, roof + new Vector3(-0.6f, 1.35f, -1.4f), roof + new Vector3(-0.2f, 0.85f, 1.3f), 34f);

            // The ledge: sit on the north parapet, facing the city, Byte beside you. The camera sits behind you both.
            float ledgeTop = roof.y + 0.55f;
            Vector3 ledgeSeat = new Vector3(roof.x + 2.2f, ledgeTop, roof.z + 3.82f);
            GameObject ledgeSpot = Block("Prop_RoofLedge", ctx.props, new Vector3(ledgeSeat.x, roof.y + 0.5f, roof.z + 3.4f), new Vector3(1.6f, 1f, 0.6f), null, ctx.interactableLayer);
            ledgeSpot.GetComponent<Collider>().isTrigger = true;
            Inspectable(ledgeSpot, "RoofLedge",
                L("* You sit on the ledge. Byte sits next to you, tail hanging over the drop like it's nothing.",
                  "{cam=byte}dont look down. {p}look out. {p}{small}different thing.{/small}"),
                L("* Byte's tail swings over the edge. {p}He is very obviously not worried. {p}You are, a little."), "roof_seat_ledge;+closeness#roof_seat_ledge", spkByte);
            SitHere(ledgeSpot, new[]
            {
                new Sitter { id = "leo", seat = ledgeSeat, yaw = 0f },
                new Sitter { id = "byte", seat = ledgeSeat + new Vector3(0.6f, 0f, 0f), yaw = 0f },
            }, new Vector3(ledgeSeat.x - 1.1f, ledgeTop + 1.1f, roof.z + 1.3f), new Vector3(ledgeSeat.x + 0.6f, ledgeTop - 0.4f, roof.z + 9f), 40f);

            // Under the water tank: everyone on the floor, backs to the legs, looking at the lights.
            Vector3 tank = roof + new Vector3(-3.6f, 0f, 2.2f);
            GameObject tankSpot = Block("Prop_RoofTank", ctx.props, tank + new Vector3(0, 0.5f, -0.2f), new Vector3(1.6f, 1f, 1.6f), null, ctx.interactableLayer);
            tankSpot.GetComponent<Collider>().isTrigger = true;
            Inspectable(tankSpot, "RoofTank",
                L("* You sit under the water tank. Kira drops down next to you. Then Byte. Then nobody says anything.",
                  "{cam=two}...This is nice. {p}{slow}Is it weird that this is nice?{/slow}"),
                L("* Still nice. {p}Still a little weird."), "roof_seat_tank;+closeness#roof_seat_tank", spkKira);
            float floorY = roof.y + 0.05f;
            SitHere(tankSpot, new[]
            {
                new Sitter { id = "leo", seat = new Vector3(tank.x - 0.35f, floorY + 0.12f, tank.z - 0.55f), yaw = 160f },
                new Sitter { id = "kira", seat = new Vector3(tank.x + 0.35f, floorY + 0.12f, tank.z - 0.6f), yaw = 190f },
                new Sitter { id = "byte", seat = new Vector3(tank.x - 0.0f, floorY, tank.z - 1.05f), yaw = 175f },
            }, tank + new Vector3(0.9f, 1.25f, -3.1f), tank + new Vector3(0f, 0.55f, -0.6f), 36f);
        }
    }
}
#endif
