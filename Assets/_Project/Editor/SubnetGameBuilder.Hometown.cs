#if UNITY_EDITOR
using System.Collections.Generic;
using Subnet.Audio;
using Subnet.Cinematics;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    /// <summary>
    /// 00b_Hometown, the prologue: Halden, the town Leo grew up in, three years before the game. Six in the evening,
    /// the last day of June. Real colours, no glitches. One open map: the residential street, the square with the
    /// bakery (the family flat is upstairs), the riverside park with the bandstand, the footbridge, the old swing
    /// tree on the far bank, and the little station.
    ///
    /// Mia (9) has hidden Leo's train ticket so he can't leave. Her treasure hunt crosses the whole town:
    ///   desk note → the baker → Dani's drums (rhythm puzzle) → lanterns on the footbridge (order puzzle)
    ///   → the tin under the swing → Mia at the bandstand (wind-chime melody) → the recital promise
    ///   → box fourteen → Mom → the 21:40 to the city. Then: three years later (01_Tenement).
    /// Map: x -32..35 (west → east), z -23..17 (south → north). The camera looks from the south-west, so tall
    /// things stand on the north/east sides and everything on the camera side stays low.
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        public const string HometownScene = "00b_Hometown";

        private static DialogueSpeaker spkMom, spkDani, spkBaker, spkHale, spkStationMaster, spkKid, spkOldMan;

        private static void HometownSpeakers()
        {
            spkMom = CreateSpeaker("Speaker_Mom", "MOM", "pluck_001", new Vector2(0.95f, 1.02f), 0.95f, new Color(1f, 0.78f, 0.6f), SpeakerStyle.Default, new Color(1f, 0.94f, 0.88f));
            spkDani = CreateSpeaker("Speaker_Dani", "DANI", "click_004", new Vector2(0.85f, 0.95f), 1.1f, new Color(0.6f, 0.85f, 1f), SpeakerStyle.Default, new Color(0.9f, 0.96f, 1f));
            spkBaker = CreateSpeaker("Speaker_Baker", "MR. RUSSO", "drop_002", new Vector2(0.7f, 0.8f), 0.9f, new Color(1f, 0.85f, 0.55f), SpeakerStyle.Default, new Color(1f, 0.95f, 0.85f));
            spkHale = CreateSpeaker("Speaker_Hale", "MRS. HALE", "tick_002", new Vector2(1.05f, 1.15f), 0.85f, new Color(0.85f, 0.75f, 1f), SpeakerStyle.Default, new Color(0.95f, 0.92f, 1f));
            spkStationMaster = CreateSpeaker("Speaker_StationMaster", "STATION MASTER", "drop_004", new Vector2(0.7f, 0.78f), 0.9f, new Color(0.7f, 0.85f, 0.75f), SpeakerStyle.Default, new Color(0.92f, 0.97f, 0.93f));
            spkKid = CreateSpeaker("Speaker_Kid", "KID", "click_001", new Vector2(1.3f, 1.5f), 1.3f, new Color(1f, 0.9f, 0.5f), SpeakerStyle.Default, new Color(1f, 0.97f, 0.88f));
            spkOldMan = CreateSpeaker("Speaker_OldMan", "OLD MAN", "drop_001", new Vector2(0.65f, 0.72f), 0.8f, new Color(0.8f, 0.8f, 0.72f), SpeakerStyle.Default, new Color(0.95f, 0.94f, 0.9f));
        }

        // ====================================================================== helpers

        /// <summary>A stock building with its facade (front) exactly on <paramref name="facadeZ"/>, facing south, solid.</summary>
        private static Bounds TownBuilding(Ctx ctx, string key, string model, string hint, float x, float facadeZ, float height, float maxWidth, float yaw = 180f)
        {
            var holder = new GameObject("Town_" + key);
            holder.transform.SetParent(ctx.env, false);
            holder.transform.position = new Vector3(x, 0f, facadeZ + 4f);
            GameObject asset = Model(model, hint);
            GameObject b;
            if (asset != null) b = SpawnModel(asset, holder.transform, height, false, yaw);
            else b = Block("Placeholder", holder.transform, holder.transform.position + Vector3.up * height * 0.5f, new Vector3(maxWidth, height, 6f), LitMaterial("Town/M_Placeholder", new Color(0.8f, 0.75f, 0.7f)), 0);
            Bounds bb = WorldBounds(b);
            if (bb.size.x > maxWidth)
            {
                b.transform.localScale *= maxWidth / bb.size.x;
                bb = WorldBounds(b);
            }
            b.transform.position += new Vector3(x - bb.center.x, -bb.min.y, facadeZ - bb.min.z);
            bb = WorldBounds(b);
            var col = holder.AddComponent<BoxCollider>();
            col.center = bb.center - holder.transform.position;
            col.size = bb.size * 0.98f;
            return bb;
        }

        /// <summary>A white picket fence from a to b (straight, along X or Z).</summary>
        private static void Picket(Transform parent, Vector3 a, Vector3 b, Material white)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Quaternion r = Quaternion.LookRotation(d.normalized);
            Vector3 mid = (a + b) * 0.5f;
            GameObject rail1 = Deco("FenceRail", parent, mid + Vector3.up * 0.32f, new Vector3(0.04f, 0.05f, len), white);
            rail1.transform.rotation = r;
            GameObject rail2 = Deco("FenceRail", parent, mid + Vector3.up * 0.62f, new Vector3(0.04f, 0.05f, len), white);
            rail2.transform.rotation = r;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.28f));
            for (int i = 0; i <= n; i++)
            {
                GameObject p = Deco("Picket", parent, Vector3.Lerp(a, b, i / (float)n) + Vector3.up * 0.38f, new Vector3(0.07f, 0.76f, 0.04f), white);
                p.transform.rotation = r;
            }
            GameObject solid = Block("FenceCollider", parent, mid + Vector3.up * 0.5f, new Vector3(0.12f, 1f, len), null, 0);
            solid.transform.rotation = r;
        }

        private static void Flowers(Transform parent, Vector3 center, Vector2 area, int count, int seed, Material leaf, params Material[] petals)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + new Vector3(((float)rng.NextDouble() - 0.5f) * area.x, 0f, ((float)rng.NextDouble() - 0.5f) * area.y);
                Deco("Stem", parent, p + Vector3.up * 0.12f, new Vector3(0.03f, 0.24f, 0.03f), leaf);
                Deco("Bloom", parent, p + Vector3.up * 0.26f, new Vector3(0.11f, 0.08f, 0.11f), petals[rng.Next(petals.Length)], new Vector3(0f, rng.Next(90), 0f));
            }
        }

        private static AudioSource NoteSource(Transform parent, string clip, float pitch)
        {
            var go = new GameObject("Note_" + clip);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.clip = Clip(clip);
            src.pitch = pitch;
            src.volume = 0.8f;
            src.spatialBlend = 0f;
            return src;
        }

        private static SequencePuzzle OrderPuzzle(Ctx ctx, string name, PuzzleSwitch[] order, string solvedFlag, string requiredFlag,
            string[] locked, string[] wrong, string[] solved)
        {
            var puzzle = new GameObject(name).AddComponent<SequencePuzzle>();
            puzzle.transform.SetParent(ctx.story, false);
            var so = new SerializedObject(puzzle);
            SerializedProperty list = so.FindProperty("correctOrder");
            list.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(puzzle, "flagOnSolved", solvedFlag);
            Set(puzzle, "requiredFlag", requiredFlag ?? string.Empty);
            SetStrings(puzzle, "lockedLines", locked ?? new string[0]);
            SetStrings(puzzle, "wrongLines", wrong ?? new string[0]);
            SetStrings(puzzle, "solvedLines", solved ?? new string[0]);
            Set(puzzle, "audioSource", ctx.sfx);
            Set(puzzle, "wrongClip", Clip("error_002"));
            Set(puzzle, "solvedClip", Clip("confirmation_003"));
            return puzzle;
        }

        private static InstrumentPlayer Instrument(GameObject target, PuzzleSwitch[] pads, string[] labels, Color[] colors, Transform cam, Vector3 look, float fov, string title)
        {
            var player = target.AddComponent<InstrumentPlayer>();
            SetArray(player, "pads", pads);
            SetStrings(player, "labels", labels);
            var so = new SerializedObject(player);
            SerializedProperty list = so.FindProperty("colors");
            list.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++) list.GetArrayElementAtIndex(i).colorValue = colors[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            var lookAt = new GameObject("Look").transform;
            lookAt.SetParent(target.transform, true);
            lookAt.position = look;
            Set(player, "cameraPoint", cam);
            Set(player, "lookPoint", lookAt);
            Set(player, "fieldOfView", fov);
            Set(player, "title", title);
            return player;
        }

        private static PuzzleSwitch Switch(GameObject go, Light light, Renderer glow, Color lit, AudioSource note, Transform punch)
        {
            var sw = go.AddComponent<PuzzleSwitch>();
            if (light != null) Set(sw, "switchLight", light);
            if (glow != null) Set(sw, "glowRenderer", glow);
            Set(sw, "litColor", lit);
            if (note != null) Set(sw, "noteSource", note);
            if (punch != null) Set(sw, "punch", punch);
            return sw;
        }

        // ====================================================================== the scene

        private static void BuildHometownScene()
        {
            HometownSpeakers();
            const string F = "furniture";
            Vector3 flat = new Vector3(150f, 0f, 0f); // the family flat (interior), off the edge of the town ground

            VolumeProfile profile = LookProfile("PP_Hometown_Summer", 8f, 0.45f, 0.24f, 0.06f, 0f, new Color(1f, 0.92f, 0.82f));
            Ctx ctx = BeginGameplayScene(flat + new Vector3(1.4f, 0f, -0.6f), 200f, profile,
                new Color(0.46f, 0.42f, 0.46f), new Color(1f, 0.8f, 0.58f), 1.2f, true);
            // Golden hour: low sun from the west-south-west, long shadows to the east.
            GameObject sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                sunGo.transform.rotation = Quaternion.Euler(32f, 68f, 0f);
                sunGo.GetComponent<Light>().shadows = LightShadows.Soft;
            }

            AddSceneMusic(true, StemMix.Home);
            ctx.removeAmbience = true;
            ctx.cam.backgroundColor = new Color(0.32f, 0.26f, 0.3f);
            Transform points = new GameObject("CinematicPoints").transform;

            // ---------------------------------------------------------------- materials
            Material grass = LitMaterial("Town/M_Grass", new Color(0.36f, 0.52f, 0.27f), 0.05f);
            Material asphalt = LitMaterial("Town/M_Asphalt", new Color(0.24f, 0.24f, 0.26f), 0.2f);
            Material lane = LitMaterial("Town/M_LaneWhite", new Color(0.92f, 0.9f, 0.84f));
            Material sidewalk = LitMaterial("Town/M_Sidewalk", new Color(0.66f, 0.64f, 0.6f));
            Material kerb = LitMaterial("Town/M_Kerb", new Color(0.52f, 0.5f, 0.48f));
            Material paving = LitMaterial("Town/M_SquarePaving", new Color(0.74f, 0.66f, 0.56f));
            Material pavingLine = LitMaterial("Town/M_PavingLine", new Color(0.6f, 0.53f, 0.45f));
            Material gravel = LitMaterial("Town/M_Gravel", new Color(0.7f, 0.64f, 0.52f));
            Material white = LitMaterial("Town/M_WhitePaint", new Color(0.94f, 0.93f, 0.9f));
            Material stone = LitMaterial("Town/M_Stone", new Color(0.62f, 0.6f, 0.57f));
            Material darkStone = LitMaterial("Town/M_StoneDark", new Color(0.42f, 0.41f, 0.4f));
            Material wood = LitMaterial("Town/M_Wood", new Color(0.5f, 0.34f, 0.22f));
            Material darkWood = LitMaterial("Town/M_WoodDark", new Color(0.3f, 0.2f, 0.14f));
            Material water = LitMaterial("Town/M_River", new Color(0.16f, 0.32f, 0.38f), 0.9f);
            Material leaf = LitMaterial("Town/M_FlowerLeaf", new Color(0.22f, 0.45f, 0.2f));
            Material hedge = LitMaterial("Town/M_Hedge", new Color(0.2f, 0.38f, 0.18f));
            Material pink = LitMaterial("Town/M_FlowerPink", new Color(1f, 0.55f, 0.7f));
            Material yellow = LitMaterial("Town/M_FlowerYellow", new Color(1f, 0.85f, 0.3f));
            Material whiteFlower = LitMaterial("Town/M_FlowerWhite", new Color(0.98f, 0.97f, 0.93f));
            Material red = LitMaterial("Town/M_FlowerRed", new Color(0.85f, 0.15f, 0.18f));
            Material iron = LitMaterial("Town/M_Iron", new Color(0.18f, 0.18f, 0.2f), 0.5f);
            Material warmWindow = EmissiveMaterial("Town/M_WindowWarm", new Color(0.22f, 0.18f, 0.16f), new Color(1f, 0.72f, 0.42f) * 0.55f);
            Material paper = LitMaterial("Town/M_Paper", new Color(0.97f, 0.95f, 0.88f));
            Color lampGlow = new Color(1f, 0.8f, 0.55f);

            // ---------------------------------------------------------------- ground, road, river
            Block("Ground_North", ctx.env, new Vector3(0f, -0.1f, 26.5f), new Vector3(200f, 0.2f, 79f), grass, ctx.groundLayer);   // z -13 .. 66
            Block("Ground_South", ctx.env, new Vector3(0f, -0.1f, -48.25f), new Vector3(200f, 0.2f, 63.5f), grass, ctx.groundLayer); // z -80 .. -16.5
            Block("RiverBed", ctx.env, new Vector3(0f, -0.9f, -14.75f), new Vector3(200f, 0.2f, 3.5f), darkStone, 0);
            Deco("RiverWater", ctx.env, new Vector3(0f, -0.28f, -14.75f), new Vector3(200f, 0.02f, 3.5f), water);
            Deco("BankWall_N", ctx.env, new Vector3(0f, -0.45f, -13.02f), new Vector3(200f, 0.9f, 0.08f), stone);
            Deco("BankWall_S", ctx.env, new Vector3(0f, -0.45f, -16.48f), new Vector3(200f, 0.9f, 0.08f), stone);
            Drift(ctx.props, new Vector3(6f, 0.4f, -14.75f), new Vector3(60f, 0.6f, 3f), ParticleMaterial("Town/M_RiverGlint", new Color(1f, 0.9f, 0.7f)), 10f, 0.05f, 0f);

            Deco("Road", ctx.env, new Vector3(1.75f, 0.006f, -1f), new Vector3(67.5f, 0.012f, 6f), asphalt);
            for (float x = -30f; x < 31f; x += 3f)
                Deco("LaneDash", ctx.env, new Vector3(x, 0.014f, -1f), new Vector3(1.5f, 0.006f, 0.14f), lane);
            Deco("Sidewalk_N", ctx.env, new Vector3(-0.5f, 0.04f, 2.7f), new Vector3(63f, 0.08f, 1.4f), sidewalk);
            Deco("Sidewalk_S", ctx.env, new Vector3(0f, 0.04f, -4.7f), new Vector3(64f, 0.08f, 1.4f), sidewalk);
            Deco("Kerb_N", ctx.env, new Vector3(-0.5f, 0.06f, 2.02f), new Vector3(63f, 0.12f, 0.12f), kerb);
            Deco("Kerb_S", ctx.env, new Vector3(0f, 0.06f, -4.02f), new Vector3(64f, 0.12f, 0.12f), kerb);
            Deco("ParkPath", ctx.env, new Vector3(0f, 0.012f, -8.2f), new Vector3(62f, 0.02f, 1.6f), gravel);

            // ---------------------------------------------------------------- bounds
            Block("Bound_West", ctx.env, new Vector3(-32f, 1f, -3f), new Vector3(0.2f, 2f, 40f), null, 0);
            Block("Bound_North_Res", ctx.env, new Vector3(-19.75f, 1f, 9.2f), new Vector3(24.5f, 2f, 0.2f), null, 0);   // x -32 .. -7.5
            Block("Bound_North_Res2", ctx.env, new Vector3(-0.25f, 1f, 9.2f), new Vector3(2.5f, 2f, 0.2f), null, 0);    // x -1.5 .. 1
            Block("Bound_West_Square", ctx.env, new Vector3(1f, 1f, 10.7f), new Vector3(0.2f, 2f, 3.2f), null, 0);
            Block("Bound_North_Square", ctx.env, new Vector3(14f, 1f, 12.3f), new Vector3(26f, 2f, 0.2f), null, 0);    // behind the square's buildings
            Block("Bound_North_Station", ctx.env, new Vector3(29.4f, 1f, 16.4f), new Vector3(5.2f, 2f, 0.2f), null, 0);
            Block("Bound_Station_West", ctx.env, new Vector3(26.9f, 1f, 14.3f), new Vector3(0.2f, 2f, 4.2f), null, 0);
            Block("Bound_Platform_Edge", ctx.env, new Vector3(31.95f, 1f, 9.7f), new Vector3(0.2f, 2f, 13.4f), null, 0);
            Block("Bound_Crossing", ctx.env, new Vector3(31.1f, 1f, -1.35f), new Vector3(0.2f, 2f, 8.1f), null, 0);
            Block("Bound_Platform_West", ctx.env, new Vector3(26.95f, 1f, 7.7f), new Vector3(0.2f, 2f, 9.1f), null, 0);
            Block("Bound_Platform_Front", ctx.env, new Vector3(31.5f, 1f, 2.6f), new Vector3(1f, 2f, 0.2f), null, 0);
            Block("Bound_East_Park", ctx.env, new Vector3(32f, 1f, -9.1f), new Vector3(0.2f, 2f, 7.6f), null, 0);
            Block("Bound_Bank_N_W", ctx.env, new Vector3(-9.5f, 1f, -12.85f), new Vector3(45f, 2f, 0.2f), null, 0);    // x -32 .. 13
            Block("Bound_Bank_N_E", ctx.env, new Vector3(23.5f, 1f, -12.85f), new Vector3(17f, 2f, 0.2f), null, 0);    // x 15 .. 32
            Block("Bound_Bank_S_W", ctx.env, new Vector3(-9.5f, 1f, -16.65f), new Vector3(45f, 2f, 0.2f), null, 0);
            Block("Bound_Bank_S_E", ctx.env, new Vector3(25f, 1f, -16.65f), new Vector3(20f, 2f, 0.2f), null, 0);
            Block("Bound_South", ctx.env, new Vector3(8f, 1f, -23f), new Vector3(30f, 2f, 0.2f), null, 0);
            Block("Bound_South_W", ctx.env, new Vector3(-7f, 1f, -19.8f), new Vector3(0.2f, 2f, 6.4f), null, 0);
            Block("Bound_South_E", ctx.env, new Vector3(23f, 1f, -19.8f), new Vector3(0.2f, 2f, 6.4f), null, 0);

            // ---------------------------------------------------------------- the residential street (west)
            Material mailRed = LitMaterial("Town/M_MailboxRed", new Color(0.7f, 0.15f, 0.12f));
            (string key, string model, float x)[] houses =
            {
                ("Corner", "building-type-e", -27f),
                ("Hale", "building-type-c", -19.5f),
                ("Dani", "building-type-k", -12f),
            };
            foreach (var h in houses)
            {
                Bounds hb = TownBuilding(ctx, h.key, h.model, "suburban", h.x, 8f, 5.6f, 7.2f);
                // Front yard: picket fence with a gate gap, a stone path, a mailbox.
                Picket(ctx.props, new Vector3(h.x - 3.6f, 0f, 3.75f), new Vector3(h.x - 0.6f, 0f, 3.75f), white);
                Picket(ctx.props, new Vector3(h.x + 0.6f, 0f, 3.75f), new Vector3(h.x + 3.6f, 0f, 3.75f), white);
                for (int i = 0; i < 4; i++)
                    Deco("PathStone", ctx.env, new Vector3(h.x + (i % 2 == 0 ? -0.08f : 0.08f), 0.015f, 4.3f + i * 0.9f), new Vector3(0.6f, 0.03f, 0.5f), stone, new Vector3(0f, i * 17f, 0f));
                Deco("MailboxPost", ctx.props, new Vector3(h.x + 1.2f, 0.45f, 3.5f), new Vector3(0.07f, 0.9f, 0.07f), darkWood);
                Deco("Mailbox", ctx.props, new Vector3(h.x + 1.2f, 0.98f, 3.5f), new Vector3(0.22f, 0.2f, 0.36f), mailRed);
                Deco("Hedge", ctx.env, new Vector3(h.x - 2.6f, 0.3f, 6.9f), new Vector3(1.6f, 0.6f, 0.7f), hedge);
            }
            // Mrs. Hale's roses.
            Flowers(ctx.props, new Vector3(-17.2f, 0f, 5.8f), new Vector2(2.2f, 1.6f), 16, 3, leaf, red, pink);
            Flowers(ctx.props, new Vector3(-29.2f, 0f, 5.5f), new Vector2(2f, 2f), 12, 5, leaf, yellow, whiteFlower);
            GameObject hale = Character(ctx, "MrsHale", "character-female-a", "mini-characters", new Vector3(-16.4f, 0f, 4.8f), 220f, 0.95f);
            Inspectable(hale, "MrsHale", L("..."),
                L("Leo! So it's today, is it? {p}Your mother's been baking since noon. That's how I knew.",
                  "The city. Well. {p}My Arthur went to the city once. Came back with a hat and an accent. {p}{small}Lost the accent. Kept the hat.{/small}",
                  "These roses are older than you. {p}I talk to them. {small}They're better listeners than Arthur.{/small}",
                  "Your sister asked me for a biscuit tin this morning. {p}Very serious about it. {p}{small}I didn't ask.{/small}",
                  "You'll write to your mother? {p}...Text. Whatever you young people do. {p}Just do it."),
                null, spkHale);
            Set(hale.GetComponent<InspectableObject>(), "cycleRepeat", true);

            // Dani's carport (roof on posts, open to the street and to the camera side).
            Vector3 cp = new Vector3(-4.5f, 0f, 11f);
            Material carportRoof = LitMaterial("Town/M_CarportRoof", new Color(0.4f, 0.36f, 0.34f));
            Material carportWall = LitMaterial("Town/M_CarportWall", new Color(0.78f, 0.72f, 0.64f));
            Block("Carport_Floor", ctx.env, cp + new Vector3(0f, 0.02f, 0f), new Vector3(6f, 0.04f, 6f), LitMaterial("Town/M_Concrete", new Color(0.6f, 0.59f, 0.57f)), ctx.groundLayer);
            Block("Carport_WallN", ctx.env, cp + new Vector3(0f, 1.4f, 3.05f), new Vector3(6.2f, 2.8f, 0.15f), carportWall, 0);
            Block("Carport_WallE", ctx.env, cp + new Vector3(3.05f, 1.4f, 0f), new Vector3(0.15f, 2.8f, 6.2f), carportWall, 0);
            GameObject carRoof = Deco("Carport_Roof", ctx.env, cp + new Vector3(0f, 2.86f, 0f), new Vector3(6.4f, 0.12f, 6.4f), carportRoof);
            var carCut = new GameObject("Cutaway_Carport").AddComponent<CutawayZone>();
            carCut.transform.SetParent(ctx.env, false);
            carCut.transform.position = cp + new Vector3(-0.5f, 1f, -1.5f);
            Set(carCut, "size", new Vector3(9f, 4f, 10f));
            SetArray(carCut, "hide", new Object[] { carRoof.GetComponent<Renderer>() });
            foreach (Vector3 o in new[] { new Vector3(2.95f, 0f, -2.95f), new Vector3(-2.95f, 0f, 2.95f) })
                Block("Carport_Post", ctx.env, cp + o + Vector3.up * 1.4f, new Vector3(0.14f, 2.8f, 0.14f), white, 0);
            Block("Bound_Carport_W", ctx.env, cp + new Vector3(-3.1f, 1f, 1f), new Vector3(0.2f, 2f, 4f), null, 0);
            Deco("Driveway", ctx.env, new Vector3(-4.5f, 0.01f, 5.9f), new Vector3(4.5f, 0.02f, 4.4f), LitMaterial("Town/M_Concrete", new Color(0.6f, 0.59f, 0.57f)));
            Prop(ctx, "Amp", "speaker", F, cp + new Vector3(1.7f, 0f, 2.4f), 200f, 0.9f, false,
                L("A cheap amp with a strip of tape on it. In silver marker: <color=#33FFF2>NIGHT TRAIN</color>.",
                  "* We named the band before we had a song. {p}Then we named the song after the band."),
                L("* NIGHT TRAIN. {p}Kick, tom, snare, crash. {p}That's the whole intro. We were very proud of it."));
            Prop(ctx, "Setlist", "cardboardBoxClosed", F, cp + new Vector3(2.3f, 0f, 1.1f), 10f, 0.55f, true, null);
            Prop(ctx, "CarportShelf", "bookcaseOpenLow", F, cp + new Vector3(-1.6f, 0f, 2.6f), 180f, 1f, false, null);
            GameObject kit = PropModel("Prop_DrumKit", ctx.props, cp + new Vector3(0.1f, 0.04f, 0.8f), 1.25f, 0f);
            GameObject dani = Character(ctx, "Dani", "character-male-d", "mini-characters", cp + new Vector3(-1.4f, 0.04f, 1.6f), 170f, 1.0f);
            Light carportLight = PointLight(ctx.env, cp + new Vector3(0f, 2.5f, 0f), new Color(1f, 0.82f, 0.6f), 1.6f, 6f);

            // ---------------------------------------------------------------- the square
            Deco("SquarePaving", ctx.env, new Vector3(13.25f, 0.02f, 7.75f), new Vector3(24.5f, 0.04f, 8.7f), paving);
            for (float x = 2f; x < 25.5f; x += 1.5f)
                Deco("PavingLine", ctx.env, new Vector3(x, 0.043f, 7.75f), new Vector3(0.03f, 0.005f, 8.6f), pavingLine);
            for (float z = 4f; z < 12f; z += 1.5f)
                Deco("PavingLine", ctx.env, new Vector3(13.25f, 0.043f, z), new Vector3(24.4f, 0.005f, 0.03f), pavingLine);

            TownBuilding(ctx, "Cafe", "building-b", "commercial", 6.4f, 12f, 8f, 6f);
            TownBuilding(ctx, "CornerShop", "building-c", "commercial", 21.6f, 12f, 7f, 6f);

            // The bakery, with the family flat upstairs (built by hand so the doors are real).
            Material plaster = LitMaterial("Town/M_BakeryPlaster", new Color(0.88f, 0.76f, 0.6f));
            Material trim = LitMaterial("Town/M_BakeryTrim", new Color(0.36f, 0.24f, 0.18f));
            Material awningRed = LitMaterial("Town/M_AwningRed", new Color(0.72f, 0.18f, 0.16f));
            Material awningCream = LitMaterial("Town/M_AwningCream", new Color(0.95f, 0.9f, 0.78f));
            Material shopGlass = EmissiveMaterial("Town/M_BakeryGlass", new Color(0.2f, 0.15f, 0.12f), new Color(1f, 0.7f, 0.4f) * 0.4f);
            Block("Bakery_Body", ctx.env, new Vector3(14f, 3.6f, 15.5f), new Vector3(8f, 7.2f, 7f), plaster, 0);
            Deco("Bakery_Base", ctx.env, new Vector3(14f, 0.3f, 11.97f), new Vector3(8.02f, 0.6f, 0.06f), darkStone);
            Deco("Bakery_Cornice", ctx.env, new Vector3(14f, 7.25f, 11.95f), new Vector3(8.3f, 0.22f, 0.3f), trim);
            Deco("Bakery_FloorLine", ctx.env, new Vector3(14f, 3.35f, 11.96f), new Vector3(8.05f, 0.12f, 0.1f), trim);
            Deco("Bakery_Chimney", ctx.env, new Vector3(16.6f, 7.9f, 16.5f), new Vector3(0.7f, 1.4f, 0.7f), darkStone);
            Deco("ShopWindowFrame", ctx.env, new Vector3(12.1f, 1.45f, 11.95f), new Vector3(3.5f, 1.9f, 0.06f), trim);
            Deco("ShopWindow", ctx.env, new Vector3(12.1f, 1.45f, 11.92f), new Vector3(3.3f, 1.7f, 0.04f), shopGlass);
            for (int i = 0; i < 8; i++)
            {
                GameObject stripe = Deco("Awning", ctx.env, new Vector3(10.6f + i * 0.5f, 2.72f, 11.5f), new Vector3(0.5f, 0.04f, 1.05f), i % 2 == 0 ? awningRed : awningCream, new Vector3(-22f, 0f, 0f));
            }
            Deco("AwningValance", ctx.env, new Vector3(12.35f, 2.47f, 11.0f), new Vector3(4f, 0.18f, 0.03f), awningRed);
            Deco("SignBoard", ctx.env, new Vector3(12.1f, 3.0f, 11.93f), new Vector3(3.2f, 0.34f, 0.05f), awningCream);
            TextMeshPro bakerySign = WorldSign(ctx.env, "RUSSO  BAKERY", new Vector3(12.1f, 3.0f, 11.89f), 180f, new Color(0.35f, 0.18f, 0.1f), 2f);
            bakerySign.rectTransform.sizeDelta = new Vector2(3f, 0.3f);
            FitText(bakerySign);
            for (int i = 0; i < 4; i++)
            {
                float wx = 10.9f + i * 2.05f;
                Deco("UpWindowFrame", ctx.env, new Vector3(wx, 4.9f, 11.96f), new Vector3(1.05f, 1.35f, 0.06f), trim);
                Deco("UpWindow", ctx.env, new Vector3(wx, 4.9f, 11.93f), new Vector3(0.9f, 1.2f, 0.04f), warmWindow);
                Deco("UpMullionV", ctx.env, new Vector3(wx, 4.9f, 11.9f), new Vector3(0.05f, 1.2f, 0.03f), trim);
                Deco("UpMullionH", ctx.env, new Vector3(wx, 5.1f, 11.9f), new Vector3(0.9f, 0.05f, 0.03f), trim);
                if (i % 2 == 0)
                {
                    Deco("FlowerBox", ctx.env, new Vector3(wx, 4.18f, 11.85f), new Vector3(1f, 0.18f, 0.22f), trim);
                    Flowers(ctx.props, new Vector3(wx, 4.15f, 11.85f), new Vector2(0.9f, 0.1f), 6, 20 + i, leaf, red, pink, whiteFlower);
                }
            }
            HouseDoor shopDoor = LockedDoor(ctx, "Bakery", new Vector3(14.75f, 0f, 11.88f), 180f, trim, new Color(1f, 0.75f, 0.45f), null,
                L("* CLOSED. BACK AT 5 A.M. {p}Mr. Russo's handwriting. Same sign since we moved in."));
            DoorNote(shopDoor, "CLOSED\nBACK AT 5");
            Material flatDoorMat = LitMaterial("Town/M_FlatDoor", new Color(0.2f, 0.32f, 0.4f));
            HouseDoor flatOutside = BuildDoor(ctx, "FlatStreet", new Vector3(16.95f, 0f, 11.88f), 180f, flatDoorMat, lampGlow, null, "HALDEN");
            PointLight(ctx.env, new Vector3(12.1f, 1.6f, 10.8f), new Color(1f, 0.72f, 0.42f), 1.4f, 4f);
            // The window display: tomorrow's loaves, on a shelf behind the glass.
            Deco("DisplayShelf", ctx.env, new Vector3(12.1f, 0.72f, 11.86f), new Vector3(3.2f, 0.06f, 0.18f), trim);
            string[] breads = { "loaf", "croissant", "loaf-baguette", "loaf-round", "croissant", "bread" };
            for (int i = 0; i < breads.Length; i++)
                Prop(ctx, "Display" + i, breads[i], "food", new Vector3(10.85f + i * 0.5f, 0.75f, 11.83f), 90f + i * 13f, 0.38f, true, null, null, null, false);

            // Baker outside his shop, flour on his sleeves.
            GameObject baker = Character(ctx, "Baker", "character-male-e", "mini-characters", new Vector3(11.3f, 0f, 10.7f), 190f, 1.05f);
            DialogueSequence bakerSeq = SeqAsset("Hometown/Baker",
                OnlyIf("clue_bakery", Say(spkBaker, "Go on, the drummer's waiting. {p}{small}I can hear him from here. Everybody can.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("hunt_started", Say(spkBaker, "Ah. There he is. {p}Your sister came by an hour ago. Very official. {small}She had a clipboard.{/small}")),
                OnlyIf("hunt_started", Say(spkBaker, "She said: when Leo comes, give him this. And a roll. {p}The roll was my idea.")),
                OnlyIf("hunt_started", Say(null, "Another crayon drawing. A drum kit, and two words: <color=#FF4FD8>BOOM TSS</color>.", "", "", "give:warm_roll#russo_roll;clue_bakery")),
                OnlyIf("hunt_started", Say(spkLeoInner, "...Dani's carport. {p}Of course.", "", DialogueSequence.EndNodeId)),
                Say(spkBaker, "Leo! Last night in Halden, eh? {p}Five o'clock tomorrow and nobody steals the first roll. {p}{small}I'll have to eat it myself.{/small}"),
                Say(spkBaker, "Twelve years I've had you upstairs. Stomping. {p}{slow}It's going to be very quiet.{/slow}"));
            Inspectable(baker, "Baker", L("..."), null, null, spkBaker, bakerSeq);

            // The fountain in the middle of the square.
            Vector3 fountain = new Vector3(14f, 0f, 7f);
            GameObject basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basin.name = "Fountain_Basin";
            basin.transform.SetParent(ctx.props, false);
            basin.transform.position = fountain + Vector3.up * 0.12f;
            basin.transform.localScale = new Vector3(3.2f, 0.12f, 3.2f);
            basin.GetComponent<Renderer>().sharedMaterial = stone;
            basin.layer = ctx.interactableLayer;
            GameObject fWater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(fWater.GetComponent<Collider>());
            fWater.name = "Fountain_Water";
            fWater.transform.SetParent(ctx.props, false);
            fWater.transform.position = fountain + Vector3.up * 0.36f;
            fWater.transform.localScale = new Vector3(2.85f, 0.02f, 2.85f);
            fWater.GetComponent<Renderer>().sharedMaterial = LitMaterial("Town/M_FountainWater", new Color(0.3f, 0.55f, 0.65f), 0.95f);
            for (int i = 0; i < 20; i++)
            {
                float ang = i * 18f;
                Vector3 rp = fountain + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 1.5f;
                Deco("Fountain_Rim", ctx.props, rp + Vector3.up * 0.3f, new Vector3(0.52f, 0.36f, 0.22f), stone, new Vector3(0f, ang, 0f));
            }
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(column.GetComponent<Collider>());
            column.name = "Fountain_Column";
            column.transform.SetParent(ctx.props, false);
            column.transform.position = fountain + Vector3.up * 0.95f;
            column.transform.localScale = new Vector3(0.26f, 0.6f, 0.26f);
            column.GetComponent<Renderer>().sharedMaterial = stone;
            GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(bowl.GetComponent<Collider>());
            bowl.name = "Fountain_Bowl";
            bowl.transform.SetParent(ctx.props, false);
            bowl.transform.position = fountain + Vector3.up * 1.58f;
            bowl.transform.localScale = new Vector3(0.95f, 0.06f, 0.95f);
            bowl.GetComponent<Renderer>().sharedMaterial = stone;
            GameObject spout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(spout.GetComponent<Collider>());
            spout.name = "Fountain_Top";
            spout.transform.SetParent(ctx.props, false);
            spout.transform.position = fountain + Vector3.up * 1.78f;
            spout.transform.localScale = new Vector3(0.22f, 0.32f, 0.22f);
            spout.GetComponent<Renderer>().sharedMaterial = stone;
            PixelTears(ctx.props, fountain + Vector3.up * 1.62f, ParticleMaterial("Town/M_FountainDrops", new Color(0.75f, 0.9f, 1f)), 28f);
            Inspectable(basin, "TownFountain",
                L("* Mia throws a coin in here every Sunday. {p}She's never told anyone what she wishes for.",
                  "* There's a coin on the edge, drying in the sun. {p}You leave it. It isn't yours."),
                L("* The water sounds exactly like it did when I was ten."));
            foreach (float a in new[] { 200f, 340f })
            {
                Vector3 bp = fountain + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 2.9f;
                Prop(ctx, "SquareBench" + a, "bench", F, bp, a, 1.5f, true, null);
            }
            for (int i = 0; i < 3; i++)
                Character(ctx, "Pigeon" + i, "animal-chick", "cube-pets", fountain + new Vector3(-1.6f + i * 0.5f, 0f, -2.2f + (i % 2) * 0.4f), 150f + i * 40f, 0.28f, LitMaterial("Town/M_Pigeon", new Color(0.6f, 0.62f, 0.66f)), false);

            // Café terrace and the fruit crates.
            Vector3 cafe = new Vector3(5.5f, 0f, 9.6f);
            Prop(ctx, "CafeTable", "tableRound", F, cafe, 0f, 0.72f, false, null);
            Prop(ctx, "CafeChairA", "chair", F, cafe + new Vector3(-0.65f, 0f, 0f), 90f, 0.82f, false, null);
            Prop(ctx, "CafeChairB", "chair", F, cafe + new Vector3(0.65f, 0f, 0f), -90f, 0.82f, false, null);
            Prop(ctx, "CafeParasol", "detail-parasol-a", "commercial", cafe, 0f, 2.3f, false, null, null, null, false);
            Prop(ctx, "CafeCup", "cup-saucer", "food", cafe + new Vector3(0.1f, 0.72f, 0.05f), 0f, 0.1f, false,
                L("* An espresso somebody abandoned halfway. {p}Very Halden. Nobody here finishes anything in a hurry."), null, null, false);
            GameObject crates = Prop(ctx, "FruitCrates", "cardboardBoxOpen", F, new Vector3(19.4f, 0f, 10.9f), 10f, 0.55f, true,
                L("* Apricots. The first ones of the summer. {p}You take one. Mr. Sana waves from inside, pretending he didn't see."),
                L("* Still the best apricots in the world. {p}{small}I've never checked. I'm sure.{/small}"), "give:potion#apricot");
            for (int i = 0; i < 6; i++)
                Deco("Apricot", ctx.props, new Vector3(19.2f + (i % 3) * 0.14f, 0.58f, 10.8f + (i / 3) * 0.14f), new Vector3(0.11f, 0.11f, 0.11f), LitMaterial("Town/M_Apricot", new Color(1f, 0.6f, 0.25f)));
            Prop(ctx, "ShopAwning", "detail-awning-wide", "commercial", new Vector3(21.6f, 2.4f, 11.4f), 180f, 5f, true, null, null, null, false);

            // NIGHT RUNNER, outside the corner shop.
            Prop(ctx, "NightRunner", "arcade-machine", "mini-arcade", new Vector3(23.6f, 0f, 11.1f), 180f, 1.8f, false,
                L("NIGHT RUNNER. HIGH SCORES:", "1. MIA ..... 99,850 {p}2. LEO ..... 99,840",
                  "* She beat me this morning. She made me watch the whole replay. Twice.",
                  "* I'll win it back next summer."),
                L("* 99,840. {p}{small}Next summer.{/small}"), "+openness#night_runner", true, spkLeoInner);

            // Lamp posts.
            foreach (float x in new[] { -24f, -15.5f, -8f, 0.5f, 9f, 19f, 26f })
                LampPost(ctx, "StreetLamp" + x, new Vector3(x, 0f, 2.75f), 3.2f, lampGlow, 180f);
            foreach (float x in new[] { -20f, -2f, 18f })
                LampPost(ctx, "ParkLamp" + x, new Vector3(x, 0f, -7.2f), 3f, lampGlow, 0f);

            // Parked cars (front = +Z at yaw 0: yaw 90 faces east along the road).
            Prop(ctx, "CarSedan", "sedan", "car-kit", new Vector3(-21f, 0f, 1.05f), 90f, 4.2f, true,
                L("* Mr. Hale's car. It hasn't moved since he got his hip done. {p}Mrs. Hale washes it every Saturday anyway."), null, null, true);
            Prop(ctx, "CarVan", "delivery", "car-kit", new Vector3(10.5f, 0f, -3.1f), -90f, 4.8f, true,
                L("RUSSO BAKERY - DELIVERIES. {p}The van smells like bread even with the doors shut."), null, null, true);
            Prop(ctx, "CarHatch", "hatchback-sports", "car-kit", new Vector3(23.5f, 0f, 1.05f), 90f, 4f, true, null);

            // ---------------------------------------------------------------- the riverside park (south of the road)
            foreach (Vector3 t in new[] { new Vector3(-28f, 0f, -10.5f), new Vector3(-14f, 0f, -11.4f), new Vector3(9f, 0f, -11.2f), new Vector3(27f, 0f, -10.8f) })
                Prop(ctx, "ParkTree" + t.x, "tree-small", "suburban", t, t.x * 7f, 3f, false, null, null, null, true);
            Flowers(ctx.props, new Vector3(-22f, 0f, -11.8f), new Vector2(6f, 1.4f), 26, 7, leaf, yellow, whiteFlower, pink);
            Flowers(ctx.props, new Vector3(24f, 0f, -11.9f), new Vector2(6f, 1.2f), 22, 8, leaf, pink, whiteFlower);
            Flowers(ctx.props, new Vector3(-4f, 0f, -6.2f), new Vector2(5f, 0.8f), 14, 9, leaf, yellow, red);
            Drift(ctx.props, new Vector3(0f, 0.9f, -10f), new Vector3(60f, 1.4f, 5f), ParticleMaterial("Town/M_Firefly", new Color(1f, 0.95f, 0.6f)), 4f, 0.05f, -0.004f);

            // Skate ramp, two kids.
            Prop(ctx, "SkateRamp", "ramp", "mini-skate", new Vector3(3.6f, 0f, -10.4f), -90f, 2.2f, true, null);
            Prop(ctx, "SkateRail", "rail-low", "mini-skate", new Vector3(1.4f, 0f, -10.6f), 0f, 2f, true, null);
            Prop(ctx, "Skateboard", "skateboard", "mini-skate", new Vector3(5.4f, 0f, -8.6f), 30f, 0.7f, true, null, null, null, false);
            GameObject kidA = Character(ctx, "KidA", "character-male-a", "mini-characters", new Vector3(5.8f, 0f, -9.3f), 240f, 0.74f);
            Inspectable(kidA, "KidA", L("..."),
                L("You're Mia's brother! {p}She said you're moving to the city to become a millionaire.",
                  "Watch this. {p}...No, wait, watch THIS. {p}{small}...Okay, I'll do it when you're not looking.{/small}",
                  "Is it true the city has trains every five minutes? {p}{big}Every five minutes?{/big} {p}What do they DO all day?"),
                null, spkKid);
            Set(kidA.GetComponent<InspectableObject>(), "cycleRepeat", true);
            GameObject kidB = Character(ctx, "KidB", "character-female-f", "mini-characters", new Vector3(-0.2f, 0f, -9.4f), 120f, 0.72f);
            Inspectable(kidB, "KidB", L("..."),
                L("Mia's at the bandstand. She's been there since lunch. {p}She said nobody's allowed. {small}Especially you.{/small}",
                  "I'm not telling you anything. I signed a contract. {p}{small}In crayon.{/small}"),
                null, spkKid);
            Set(kidB.GetComponent<InspectableObject>(), "cycleRepeat", true);

            // The old man by the river.
            GameObject oldMan = Character(ctx, "OldMan", "character-male-c", "mini-characters", new Vector3(24.5f, 0f, -12.2f), 180f, 1.0f);
            Inspectable(oldMan, "OldMan", L("..."),
                L("The 21:40. {p}My son took it, twenty years ago. {p}{slow}Every Christmas he says next year.{/slow}",
                  "Don't look at me like that. He's happy. {p}{small}I think. He says so in the cards.{/small}",
                  "River's low this summer. {p}You can see the stones you couldn't see before."),
                null, spkOldMan);
            Set(oldMan.GetComponent<InspectableObject>(), "cycleRepeat", true);
            GameObject riverBench = Prop(ctx, "RiverBench", "bench", F, new Vector3(-19f, 0f, -11.9f), 0f, 1.15f, true,
                L("* The river, the light, the swing on the other side. {p}I could sit here until the train's gone. {p}{small}I could.{/small}"),
                L("* Still here. Still could."), "+openness#river_bench");
            BenchSeat(riverBench, 180f);

            // ---------------------------------------------------------------- the bandstand (Mia, wind chimes)
            Vector3 band = new Vector3(-8f, 0f, -9.6f);
            GameObject floorDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(floorDisc.GetComponent<Collider>());
            floorDisc.name = "Bandstand_Floor";
            floorDisc.transform.SetParent(ctx.env, false);
            floorDisc.transform.position = band + Vector3.up * 0.07f;
            floorDisc.transform.localScale = new Vector3(4.6f, 0.07f, 4.6f);
            floorDisc.GetComponent<Renderer>().sharedMaterial = wood;
            floorDisc.layer = ctx.groundLayer;
            var discCol = floorDisc.AddComponent<MeshCollider>();
            Material bandWhite = LitMaterial("Town/M_BandstandWhite", new Color(0.95f, 0.94f, 0.9f));
            Material bandRoof = LitMaterial("Town/M_BandstandRoof", new Color(0.35f, 0.5f, 0.45f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                Vector3 p = band + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 2.1f;
                Block("Bandstand_Post", ctx.env, p + Vector3.up * 1.3f, new Vector3(0.1f, 2.6f, 0.1f), bandWhite, 0);
                if (i != 5) // the opening faces the path (north-west)
                {
                    Vector3 q = band + Quaternion.Euler(0f, a + 45f, 0f) * Vector3.forward * 2.1f;
                    GameObject railing = Deco("Bandstand_Rail", ctx.env, (p + q) * 0.5f + Vector3.up * 0.75f, new Vector3(0.06f, 0.06f, Vector3.Distance(p, q)), bandWhite);
                    railing.transform.rotation = Quaternion.LookRotation(q - p);
                    GameObject railCol = Block("Bandstand_RailCollider", ctx.env, (p + q) * 0.5f + Vector3.up * 0.5f, new Vector3(0.12f, 1f, Vector3.Distance(p, q)), null, 0);
                    railCol.transform.rotation = Quaternion.LookRotation(q - p);
                }
            }
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.name = "Bandstand_Roof";
            roof.transform.SetParent(ctx.env, false);
            roof.transform.position = band + Vector3.up * 2.68f;
            roof.transform.localScale = new Vector3(5f, 0.08f, 5f);
            roof.GetComponent<Renderer>().sharedMaterial = bandRoof;
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.name = "Bandstand_Cap";
            cap.transform.SetParent(ctx.env, false);
            cap.transform.position = band + Vector3.up * 2.86f;
            cap.transform.localScale = new Vector3(2.4f, 0.12f, 2.4f);
            cap.GetComponent<Renderer>().sharedMaterial = bandRoof;
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.name = "Bandstand_Knob";
            knob.transform.SetParent(ctx.env, false);
            knob.transform.position = band + Vector3.up * 3.08f;
            knob.transform.localScale = Vector3.one * 0.3f;
            knob.GetComponent<Renderer>().sharedMaterial = bandWhite;
            var bandCut = new GameObject("Cutaway_Bandstand").AddComponent<CutawayZone>();
            bandCut.transform.SetParent(ctx.env, false);
            bandCut.transform.position = band + Vector3.up;
            Set(bandCut, "size", new Vector3(7f, 4f, 7f));
            SetArray(bandCut, "hide", new Object[] { roof.GetComponent<Renderer>(), cap.GetComponent<Renderer>(), knob.GetComponent<Renderer>() });

            // Four chimes on the camera side, each with a coloured ribbon.
            (string color, Color tint, string clip, float pitch)[] chimes =
            {
                ("red", new Color(0.9f, 0.2f, 0.2f), "chime_c", 1f),
                ("yellow", new Color(1f, 0.82f, 0.2f), "chime_e", 1f),
                ("green", new Color(0.3f, 0.75f, 0.35f), "chime_g", 1f),
                ("blue", new Color(0.25f, 0.5f, 1f), "chime_a", 1f),
            };
            var chimeSwitches = new Dictionary<string, PuzzleSwitch>();
            Material chimeMetal = LitMaterial("Town/M_ChimeMetal", new Color(0.78f, 0.76f, 0.7f), 0.8f);
            for (int i = 0; i < chimes.Length; i++)
            {
                Vector3 p = band + new Vector3(-1.2f + i * 0.8f, 0f, -1.2f);
                var holder = new GameObject("Chime_" + chimes[i].color);
                holder.transform.SetParent(ctx.props, false);
                holder.transform.position = p + Vector3.up * 1.4f;
                var swing = new GameObject("Swing").transform;
                swing.SetParent(holder.transform, false);
                swing.localPosition = new Vector3(0f, 1.2f, 0f);
                Deco("String", swing, p + Vector3.up * 2.35f, new Vector3(0.01f, 0.5f, 0.01f), iron);
                GameObject tube = Deco("Tube", swing, p + Vector3.up * (1.75f - i * 0.05f), new Vector3(0.06f, 0.7f + i * 0.1f, 0.06f), chimeMetal);
                Material ribbon = EmissiveMaterial("Town/M_Ribbon_" + chimes[i].color, chimes[i].tint * 0.8f, Color.black);
                GameObject tag = Deco("Ribbon", swing, p + Vector3.up * (1.2f - i * 0.1f), new Vector3(0.12f, 0.3f, 0.02f), ribbon);
                Light l = PointLight(holder.transform, p + Vector3.up * 1.6f, chimes[i].tint, 0.8f, 1.6f);
                l.enabled = false;
                var spin = swing.gameObject.AddComponent<Spinner>();
                spin.degreesPerSecond = Vector3.zero;
                spin.bobAmplitude = 0.015f;
                spin.bobSpeed = 1.4f + i * 0.3f;
                chimeSwitches[chimes[i].color] = Switch(holder, l, tag.GetComponent<Renderer>(), chimes[i].tint, NoteSource(holder.transform, chimes[i].clip, chimes[i].pitch), tube.transform);
            }
            OrderPuzzle(ctx, "ChimePuzzle", new[] { chimeSwitches["blue"], chimeSwitches["yellow"], chimeSwitches["red"], chimeSwitches["green"] },
                "chimes_played", "ticket_found",
                L("* Mia's wind chimes. {p}If I touch them before she says so, I'm dead."),
                L("...That's not it. {p}Mia's drawing has the colours in order."),
                null);
            GameObject chimeTarget = Hitbox("Prop_Chimes", ctx.props, band + new Vector3(0f, 1.2f, -1.2f), new Vector3(3.2f, 2.2f, 0.8f), ctx.interactableLayer);
            Transform chimeCam = CinematicPoint("CP_Chimes", points, band + new Vector3(-0.4f, 1.6f, -4.7f), band + new Vector3(0f, 1.55f, -1.2f));
            Instrument(chimeTarget, new[] { chimeSwitches["red"], chimeSwitches["yellow"], chimeSwitches["green"], chimeSwitches["blue"] },
                new[] { "Red", "Yellow", "Green", "Blue" }, new[] { chimes[0].tint, chimes[1].tint, chimes[2].tint, chimes[3].tint },
                chimeCam, band + new Vector3(0f, 1.55f, -1.2f), 44f, "MIA'S SONG");
            GameObject stand = Block("Prop_MusicStand", ctx.props, band + new Vector3(0.9f, 1.05f, 0.1f), new Vector3(0.55f, 0.4f, 0.04f), paper, ctx.interactableLayer);
            stand.transform.rotation = Quaternion.Euler(-20f, 200f, 0f);
            Deco("StandPole", ctx.props, band + new Vector3(0.9f, 0.5f, 0.12f), new Vector3(0.03f, 1f, 0.03f), iron);
            Inspectable(stand, "MusicStand",
                L("Mia's recital song, drawn in crayon. Four dots: <color=#4F8FFF>BLUE</color>, <color=#FFD84F>YELLOW</color>, <color=#FF4F4F>RED</color>, <color=#5FCF6F>GREEN</color>.",
                  "* Underneath: 'TINKLE TINKLE THEN I BOW'."),
                L("<color=#4F8FFF>BLUE</color>, <color=#FFD84F>YELLOW</color>, <color=#FF4F4F>RED</color>, <color=#5FCF6F>GREEN</color>. Then she bows."));

            GameObject mia = Character(ctx, "Mia", "character-female-e", "mini-characters", band + new Vector3(0.2f, 0.14f, 0.5f), 200f, 0.72f);
            DialogueSequence miaSeq = SeqAsset("Hometown/Mia",
                OnlyIf("mia_promise", Say(spkMia, "Front row. Seat one. {p}{small}I'll check.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("!ticket_found", Say(spkMia, "What ticket? {p}I don't know about any ticket. {p}{small}Go away. I'm practising.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("!chimes_played", Say(spkMia, "...You found it. {p}{slow}Okay.{/slow}")),
                OnlyIf("!chimes_played", Say(spkMia, "Then you have to play my song. Once. So I can practise my bow. {p}The colours are on my drawing. {small}Don't mess it up.{/small}", "", DialogueSequence.EndNodeId)),
                Say(spkMia, "Again tomorrow? {p}...Oh. Right."));
            Inspectable(mia, "Mia", L("..."), null, null, spkMia, miaSeq);
            DialogueSequence promise = SeqAsset("Hometown/MiaPromise",
                Say(null, "Mia bows. A very serious, very deep bow. {p}She nearly falls over."),
                Say(spkMia, "{cam=close}That's what I'm doing at the recital. In the spring."),
                Say(spkMia, "Here. It's the program. I wrote your seat on it. {p}Front row. Seat one.", "", "", "key:recital_program"),
                Ask(spkMia, "{cam=two}You have to clap the loudest. {p}{bounce}Promise?{/bounce}",
                    Opt("[\"Promise.\"]", "yes", "+openness"),
                    Opt("[\"I'll try, Mia.\"]", "try")),
                Say(spkMia, "Pinky promise. {p}{small}Those are legally binding.{/small}", "yes", "after"),
                Say(spkMia, "Trying isn't a promise. {p}...Fine. I'm writing down 'yes'.", "try", "after"),
                Say(spkLeoInner, "{cam=off}I said yes. {p}{slow}I'll come back for it. It's three hours.{/slow}", "after", "", "mia_promise"));
            Event(ctx, "MiaPromise", StoryEvent.TriggerMode.OnFlag, "chimes_played", 0.8f, null, null, null, null, string.Empty, default, default, promise);

            // ---------------------------------------------------------------- the footbridge and its lanterns
            float bx = 14f;
            Block("Bridge_Deck", ctx.env, new Vector3(bx, 0.07f, -14.75f), new Vector3(2.4f, 0.14f, 4.6f), wood, ctx.groundLayer);
            for (float z = -12.6f; z > -17f; z -= 0.4f)
                Deco("Bridge_Plank", ctx.env, new Vector3(bx, 0.145f, z), new Vector3(2.38f, 0.01f, 0.05f), darkWood);
            foreach (float sx in new[] { -1.15f, 1.15f })
            {
                Block("Bridge_Rail_" + sx, ctx.env, new Vector3(bx + sx, 0.6f, -14.75f), new Vector3(0.08f, 0.9f, 4.6f), null, 0);
                Deco("Bridge_Handrail", ctx.env, new Vector3(bx + sx, 1.0f, -14.75f), new Vector3(0.1f, 0.08f, 4.6f), wood);
                for (float z = -12.6f; z >= -16.95f; z -= 0.72f)
                    Deco("Bridge_Baluster", ctx.env, new Vector3(bx + sx, 0.57f, z), new Vector3(0.07f, 0.86f, 0.07f), wood);
            }
            (string word, Vector3 pos)[] lanterns =
            {
                ("SWING", new Vector3(bx - 1.15f, 0f, -13.3f)),
                ("BREAD", new Vector3(bx + 1.15f, 0f, -14.75f)),
                ("DRUMS", new Vector3(bx - 1.15f, 0f, -16.2f)),
            };
            var lanternSwitches = new Dictionary<string, PuzzleSwitch>();
            Material lanternPaper = EmissiveMaterial("Town/M_PaperLantern", new Color(0.85f, 0.32f, 0.22f), Color.black);
            foreach (var ln in lanterns)
            {
                var holder = new GameObject("Lantern_" + ln.word);
                holder.transform.SetParent(ctx.props, false);
                holder.transform.position = ln.pos;
                holder.layer = ctx.interactableLayer;
                var col = holder.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 2.2f, 0.6f);
                col.center = new Vector3(0f, 1.1f, 0f);
                col.isTrigger = true;
                Deco("LanternPost", holder.transform, ln.pos + Vector3.up * 0.95f, new Vector3(0.07f, 1.9f, 0.07f), darkWood);
                Deco("LanternArm", holder.transform, ln.pos + new Vector3(0f, 1.88f, 0.18f), new Vector3(0.05f, 0.05f, 0.4f), darkWood);
                GameObject globe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(globe.GetComponent<Collider>());
                globe.name = "Paper";
                globe.transform.SetParent(holder.transform, false);
                globe.transform.position = ln.pos + new Vector3(0f, 1.58f, 0.36f);
                globe.transform.localScale = new Vector3(0.36f, 0.44f, 0.36f);
                globe.GetComponent<Renderer>().sharedMaterial = lanternPaper;
                Light l = PointLight(holder.transform, globe.transform.position, new Color(1f, 0.7f, 0.4f), 1.8f, 4f);
                l.enabled = false;
                GameObject tagGo = Deco("Tag", holder.transform, ln.pos + new Vector3(0f, 1.12f, 0.37f), new Vector3(0.62f, 0.26f, 0.01f), paper);
                TextMeshPro word = WorldSign(holder.transform, ln.word, ln.pos + new Vector3(0f, 1.12f, 0.37f) + new Vector3(0f, 0f, -0.012f), 180f, new Color(0.7f, 0.12f, 0.22f), 1.6f);
                word.rectTransform.sizeDelta = new Vector2(0.58f, 0.22f);
                foreach (float ry in new[] { -0.14f, 0f, 0.14f })
                    Deco("LanternRib", holder.transform, globe.transform.position + Vector3.up * ry, new Vector3(0.37f - Mathf.Abs(ry), 0.015f, 0.37f - Mathf.Abs(ry)), darkWood);
                Deco("LanternCapTop", holder.transform, globe.transform.position + Vector3.up * 0.23f, new Vector3(0.14f, 0.05f, 0.14f), darkWood);
                Deco("LanternCapBottom", holder.transform, globe.transform.position + Vector3.down * 0.23f, new Vector3(0.14f, 0.05f, 0.14f), darkWood);
                FitText(word);
                lanternSwitches[ln.word] = Switch(holder, l, globe.GetComponent<Renderer>(), new Color(1f, 0.72f, 0.4f), null, globe.transform);
            }
            OrderPuzzle(ctx, "LanternPuzzle", new[] { lanternSwitches["BREAD"], lanternSwitches["DRUMS"], lanternSwitches["SWING"] },
                "lanterns_lit", "clue_garage",
                L("* Three paper lanterns, unlit. Somebody tied crayon tags to them. {p}Somebody short."),
                L("* The lanterns go out together. {p}'In the order you found me.'"),
                L("* The last lantern catches. {p}Across the water, something glints under the swing."));

            // ---------------------------------------------------------------- the far bank: the swing tree
            Vector3 tree = new Vector3(16.6f, 0f, -19.6f);
            GameObject swingTree = PropModel("Prop_SwingTree", ctx.props, tree, 7.4f, 210f);
            if (swingTree != null) Hitbox("TreeTrunk", swingTree.transform, tree + Vector3.up * 1.5f, new Vector3(1f, 3f, 1f), 0);
            Flowers(ctx.props, new Vector3(13f, 0f, -20.5f), new Vector2(6f, 3f), 26, 11, leaf, yellow, whiteFlower, pink);
            Prop(ctx, "PicnicBlanket", "rugRectangle", F, new Vector3(12.4f, 0f, -19.2f), 20f, 2f, true, null, null, null, false);
            GameObject tin = Block("Prop_BiscuitTin", ctx.props, tree + new Vector3(-1.1f, 0.09f, 1.2f), new Vector3(0.34f, 0.18f, 0.34f), LitMaterial("Town/M_TinBlue", new Color(0.2f, 0.35f, 0.65f), 0.6f), ctx.interactableLayer);
            Deco("TinString", tin.transform.parent, tin.transform.position + Vector3.up * 0.1f, new Vector3(0.36f, 0.02f, 0.03f), LitMaterial("Town/M_String", new Color(0.9f, 0.85f, 0.7f)));
            DialogueSequence tinSeq = SeqAsset("Hometown/Tin",
                OnlyIf("ticket_found", Say(null, "Mrs. Hale's biscuit tin. Empty now, except for crumbs.", "", DialogueSequence.EndNodeId)),
                OnlyIf("!lanterns_lit", Say(null, "A biscuit tin, tied shut with string. On the lid, in crayon: <color=#FF4FD8>LIGHT THE WAY FIRST</color>.", "", DialogueSequence.EndNodeId)),
                Say(null, "Inside: your train ticket. {p}Under it, one last drawing. A girl on a stage, bowing. An empty seat in the front row."),
                Say(null, "On the back: <color=#FF4FD8>COME SAY BYE PROPERLY. BANDSTAND.</color>", "", "", "key:hometown_ticket;ticket_found"),
                Say(spkLeoInner, "...She planned all of this. {p}{small}She's nine.{/small}"));
            Inspectable(tin, "BiscuitTin", L("..."), null, null, null, tinSeq);
            GameObject swingSpot = Block("Prop_TheSwing", ctx.props, tree + new Vector3(1.2f, 0.5f, 0.6f), new Vector3(1.4f, 1f, 1.4f), null, ctx.interactableLayer);
            swingSpot.GetComponent<Collider>().isTrigger = true;
            Inspectable(swingSpot, "TheSwing",
                L("* Dad hung this swing the summer we moved here. {p}I pushed Mia on it until my arms hurt. She always said higher.",
                  "* From here you can see the whole town. The bakery. The station. {p}It all fits in one look."),
                L("* Higher. {p}{small}Always higher.{/small}"), "+openness#swing_tree");

            // ---------------------------------------------------------------- the station
            Material platformMat = LitMaterial("Town/M_Platform", new Color(0.6f, 0.58f, 0.55f));
            Material edgeYellow = LitMaterial("Town/M_PlatformEdge", new Color(0.92f, 0.78f, 0.2f));
            Block("Platform", ctx.env, new Vector3(29.4f, 0.09f, 9.6f), new Vector3(4.9f, 0.18f, 13.2f), platformMat, ctx.groundLayer);
            Deco("PlatformEdge", ctx.env, new Vector3(31.62f, 0.185f, 9.6f), new Vector3(0.3f, 0.01f, 13.2f), edgeYellow);
            Deco("TrackBed", ctx.env, new Vector3(33.2f, 0.01f, 2f), new Vector3(2.4f, 0.02f, 48f), gravel);
            Deco("RailBridge", ctx.env, new Vector3(33.2f, -0.12f, -14.75f), new Vector3(3f, 0.3f, 4.4f), darkStone);
            Deco("RailBridgeRailW", ctx.env, new Vector3(31.75f, 0.35f, -14.75f), new Vector3(0.1f, 0.5f, 4.4f), iron);
            Deco("RailBridgeRailE", ctx.env, new Vector3(34.65f, 0.35f, -14.75f), new Vector3(0.1f, 0.5f, 4.4f), iron);
            GameObject rail = Model("railroad-straight", "train-kit");
            if (rail != null)
                for (float z = -21f; z <= 25f; z += 2f)
                {
                    var holder = new GameObject("Rail");
                    holder.transform.SetParent(ctx.env, false);
                    holder.transform.position = new Vector3(33.2f, 0.02f, z);
                    SpawnModel(rail, holder.transform, 2f, true, 0f);
                }
            // Shelter.
            foreach (Vector3 o in new[] { new Vector3(28f, 0f, 9f), new Vector3(28f, 0f, 13f), new Vector3(30.6f, 0f, 9f), new Vector3(30.6f, 0f, 13f) })
                Block("ShelterPost", ctx.env, o + Vector3.up * 1.4f, new Vector3(0.12f, 2.8f, 0.12f), iron, 0);
            Deco("ShelterRoof", ctx.env, new Vector3(29.3f, 2.85f, 11f), new Vector3(3.2f, 0.12f, 4.6f), LitMaterial("Town/M_ShelterRoof", new Color(0.32f, 0.42f, 0.36f)));
            GameObject platformBench = Prop(ctx, "PlatformBench", "bench", F, new Vector3(28f, 0.18f, 11f), -90f, 1.15f, true,
                L("* You sit. The rails tick in the heat. {p}Somewhere a long way north, the 21:40 is already on its way."),
                L("* Still sitting. Still on its way."), "+openness#platform_bench");
            BenchSeat(platformBench, 270f);
            GameObject board = Block("Prop_HomeTimetable", ctx.props, new Vector3(29.4f, 1.7f, 15.9f), new Vector3(2f, 0.9f, 0.1f), LitMaterial("Town/M_BoardDark", new Color(0.08f, 0.1f, 0.12f)), ctx.interactableLayer);
            Deco("BoardLegL", ctx.props, new Vector3(28.5f, 0.6f, 15.95f), new Vector3(0.07f, 1.2f, 0.07f), iron);
            Deco("BoardLegR", ctx.props, new Vector3(30.3f, 0.6f, 15.95f), new Vector3(0.07f, 1.2f, 0.07f), iron);
            string[] rows = { "HALDEN", "21:40   CITY CENTRAL   ON TIME", "22:55   COAST LINE     ON TIME" };
            for (int i = 0; i < rows.Length; i++)
            {
                TextMeshPro row = WorldSign(ctx.props, rows[i], new Vector3(29.4f, 1.98f - i * 0.25f, 15.84f), 180f, i == 0 ? Color.white : new Color(1f, 0.8f, 0.35f), i == 0 ? 1.4f : 1.05f);
                row.rectTransform.sizeDelta = new Vector2(1.85f, 0.22f);
                FitText(row);
            }
            Inspectable(board, "HomeTimetable",
                L("21:40, CITY CENTRAL, ON TIME. {p}Even the board is excited for me.", "* Three hours. {p}People do three hours for a concert. I'll be back all the time."),
                L("* 21:40. On time."));
            GameObject master = Character(ctx, "StationMaster", "character-male-f", "mini-characters", new Vector3(30.4f, 0.18f, 6.2f), 220f, 1.05f);
            Inspectable(master, "StationMaster", L("..."),
                L("Evening, Leo. {p}City Central, isn't it? Your mother told the whole platform. {small}It's a small platform.{/small}",
                  "The 21:40's never late. Thirty-one years. {p}{slow}Not once.{/slow} {p}Leaving is always on time. It's the coming back that gets delayed."),
                null, spkStationMaster);
            Set(master.GetComponent<InspectableObject>(), "cycleRepeat", true);

            // Platform gate: opens once Leo has said his goodbyes.
            GameObject gate = Hitbox("PlatformGate", ctx.props, new Vector3(29.4f, 0.7f, 3.15f), new Vector3(4.9f, 1.4f, 0.15f), ctx.interactableLayer);
            Deco("GateRail", gate.transform, new Vector3(29.4f, 1.1f, 3.15f), new Vector3(4.9f, 0.08f, 0.08f), iron);
            Deco("GateRail", gate.transform, new Vector3(29.4f, 0.35f, 3.15f), new Vector3(4.9f, 0.08f, 0.08f), iron);
            for (float x = 27.1f; x <= 31.7f; x += 0.35f)
                Deco("GateBar", gate.transform, new Vector3(x, 0.72f, 3.15f), new Vector3(0.04f, 0.8f, 0.04f), iron);
            DialogueSequence gateSeq = SeqAsset("Hometown/PlatformGate",
                OnlyIf("!ticket_found", Say(null, "The platform gate. You need a ticket. {p}You had a ticket.", "", DialogueSequence.EndNodeId)),
                Say(null, "The gate opens ten minutes before the train. {p}Ten minutes. Enough to go home and do this properly."));
            Inspectable(gate, "PlatformGate", L("..."), null, null, null, gateSeq);
            var gateOpen = gate.AddComponent<FlagGate>();
            Set(gateOpen, "openFlag", "ready_to_leave");
            Set(gateOpen, "dissolveColor", new Color(1f, 0.85f, 0.6f));

            // Level crossing (barriers down: the train's due).
            Material barrierRed = LitMaterial("Town/M_BarrierRed", new Color(0.85f, 0.15f, 0.15f));
            foreach (Vector3 p in new[] { new Vector3(31.6f, 0f, 2.5f), new Vector3(34.8f, 0f, -4.5f) })
            {
                Deco("CrossingPost", ctx.env, p + Vector3.up * 0.7f, new Vector3(0.18f, 1.4f, 0.18f), white);
                for (int s = 0; s < 6; s++)
                    Deco("BarrierArm", ctx.env, p + new Vector3(0f, 1f, (p.z > 0f ? -1f : 1f) * (0.3f + s * 0.5f)), new Vector3(0.1f, 0.1f, 0.5f), s % 2 == 0 ? barrierRed : white);
                Material sig = EmissiveMaterial("Town/M_CrossingLight", new Color(0.4f, 0.05f, 0.05f), new Color(1f, 0.15f, 0.1f) * 3f);
                Deco("CrossingLight", ctx.env, p + new Vector3(0f, 1.55f, 0f), new Vector3(0.16f, 0.16f, 0.16f), sig);
                Pulse(PointLight(ctx.env, p + new Vector3(-0.3f, 1.55f, 0f), new Color(1f, 0.2f, 0.15f), 1.2f, 3f), LightPulse.Mode.Blink, 0.8f, 1f);
            }
            GameObject crossing = Hitbox("Prop_Crossing", ctx.props, new Vector3(30.9f, 0.8f, -1f), new Vector3(0.4f, 1.6f, 6f), ctx.interactableLayer, false);
            Inspectable(crossing, "LevelCrossing",
                L("* The barriers are down. The bell's been ringing on and off all evening. {p}They test it before the 21:40. Every day."),
                L("* Ding. Ding. Ding."));

            // The 21:40 (two coaches), arriving from the north once Leo is ready.
            var trainRoot = new GameObject("Train_2140");
            trainRoot.transform.position = new Vector3(33.2f, 0.02f, 0f);
            float cursor = 3.2f;
            foreach (string part in new[] { "train-electric-city-a", "train-electric-city-c" })
            {
                GameObject asset = Model(part, "train-kit");
                var holder = new GameObject(part);
                holder.transform.SetParent(trainRoot.transform, false);
                holder.transform.position = trainRoot.transform.position;
                if (asset == null) { cursor += 7f; continue; }
                GameObject car = SpawnModel(asset, holder.transform, 2.9f, false, 180f);
                Bounds cb = WorldBounds(car);
                car.transform.position += new Vector3(0f, 0f, cursor - cb.min.z);
                cursor += cb.size.z + 0.2f;
            }
            var arrival = new GameObject("TrainArrival").AddComponent<TrainArrival>();
            arrival.transform.SetParent(ctx.story, false);
            Set(arrival, "train", trainRoot.transform);
            Set(arrival, "triggerFlag", "ready_to_leave");
            Set(arrival, "arrivedFlag", "home_train_arrived");
            Set(arrival, "startOffset", new Vector3(0f, 0f, 70f));
            Set(arrival, "duration", 7f);
            Set(arrival, "delay", 6f);
            Set(arrival, "audioSource", ctx.sfx);
            Set(arrival, "arrivalClip", Clip("maximize_005"));
            Set(arrival, "doorsClip", Clip("open_004"));

            Event(ctx, "Boarding", StoryEvent.TriggerMode.OnPlayerEnter, "home_train_arrived", 0f, null, null, null, null,
                "boarded_home;clear:in_prologue", new Vector3(31.15f, 1f, 8f), new Vector3(0.5f, 2f, 2.6f));
            GameObject doors = Block("TrainDoors_Home", ctx.story, new Vector3(31.62f, 1.1f, 8f), new Vector3(0.4f, 2f, 2.4f), null, ctx.interactableLayer);
            doors.GetComponent<Collider>().isTrigger = true;
            Transform bA = CinematicPoint("CP_Home_Board", points, new Vector3(27.2f, 1.6f, 4.5f), new Vector3(32.6f, 1.2f, 9f));
            Transform bB = CinematicPoint("CP_Home_Away", points, new Vector3(18f, 9f, -8f), new Vector3(30f, 0f, 14f));
            CinematicSequence boarding = Cinematic("Cinematic_Leaving_Halden", ctx.story,
                Shot("Board", bA, 2.5f, 0.4f, 26f, L("* Mom's waving from the window above the bakery. {p}Mia isn't. Mia's arms are crossed. Very crossed."), null, false, Clip("open_004")),
                Shot("Away", bB, 5f, 0.6f, 30f, L("* It's three hours. I'll be back all the time.", "* Three years later."), null, false, Clip("maximize_005")));
            boarding.fadeFromBlack = false;
            boarding.blendToGameplay = false;
            boarding.fadeToBlackAtEnd = true;
            boarding.fadeOutDuration = 2f;
            EditorUtility.SetDirty(boarding);
            Exit(doors, TenementScene, null, "home_train_arrived", L("* Not yet."), null, boarding, true, null, ctx.sfx, Clip("confirmation_003"));

            // ---------------------------------------------------------------- the town sign (west end)
            Deco("TownSignPost", ctx.env, new Vector3(-30.5f, 0.9f, 3.3f), new Vector3(0.08f, 1.8f, 0.08f), iron);
            Deco("TownSign", ctx.env, new Vector3(-30.5f, 1.75f, 3.25f), new Vector3(1.6f, 0.6f, 0.05f), white);
            TextMeshPro town = WorldSign(ctx.env, "HALDEN\npop. 4,212", new Vector3(-30.5f, 1.75f, 3.21f), 180f, new Color(0.15f, 0.3f, 0.2f), 1.4f);
            town.rectTransform.sizeDelta = new Vector2(1.5f, 0.55f);
            FitText(town);
            Prop(ctx, "WestTreeA", "tree-large", "suburban", new Vector3(-33.5f, 0f, -2f), 20f, 6f, false, null, null, null, false);
            Prop(ctx, "WestTreeB", "tree-large", "suburban", new Vector3(-34f, 0f, 6f), 80f, 7f, false, null, null, null, false);
            Prop(ctx, "NorthTreeA", "tree-large", "suburban", new Vector3(24f, 0f, 14.8f), 40f, 6.5f, false, null, null, null, false);

            // ---------------------------------------------------------------- the family flat (interior)
            BuildFamilyFlat(ctx, flat, flatOutside);

            // ---------------------------------------------------------------- the drums (Dani's rhythm)
            var drumSwitches = new Dictionary<string, PuzzleSwitch>();
            (string pad, string clip, float pitch)[] drums = { ("Pad_Kick", "drum_kick", 1f), ("Pad_Tom", "drum_tom", 1f), ("Pad_Snare", "drum_snare", 1f), ("Pad_Crash", "drum_crash", 1f) };
            Vector3 kitCenter = cp + new Vector3(0.1f, 0.04f, 0.8f);
            Vector3[] fallbackPads = { kitCenter + new Vector3(0f, 0.35f, -0.2f), kitCenter + new Vector3(0f, 0.85f, 0.1f), kitCenter + new Vector3(-0.45f, 0.7f, -0.2f), kitCenter + new Vector3(0.5f, 1.15f, 0.3f) };
            for (int i = 0; i < drums.Length; i++)
            {
                Transform pad = kit != null ? FindDeep(kit.transform, drums[i].pad) : null;
                Vector3 p = pad != null ? pad.position : fallbackPads[i];
                var holder = new GameObject("Drum_" + drums[i].pad);
                holder.transform.SetParent(ctx.props, false);
                holder.transform.position = p;
                Light l = PointLight(holder.transform, p + Vector3.up * 0.3f, new Color(1f, 0.75f, 0.45f), 0.9f, 1.4f);
                l.enabled = false;
                drumSwitches[drums[i].pad] = Switch(holder, l, null, Color.white, NoteSource(holder.transform, drums[i].clip, drums[i].pitch), kit != null ? kit.transform : null);
            }
            OrderPuzzle(ctx, "DrumPuzzle", new[] { drumSwitches["Pad_Kick"], drumSwitches["Pad_Tom"], drumSwitches["Pad_Snare"], drumSwitches["Pad_Crash"] },
                "drums_played", "clue_bakery",
                L("* Dani's kit. Nobody touches Dani's kit. {p}Not even me."),
                L("* Wrong. {p}Dani winces like I stepped on his foot."),
                null);
            GameObject kitTarget = Hitbox("Prop_DrumKit", ctx.props, kitCenter + new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.5f), ctx.interactableLayer);
            Transform kitCam = CinematicPoint("CP_DrumKit", points, kitCenter + new Vector3(-1.6f, 1.55f, -2.3f), kitCenter + new Vector3(0f, 0.65f, 0.1f));
            Instrument(kitTarget, new[] { drumSwitches["Pad_Kick"], drumSwitches["Pad_Tom"], drumSwitches["Pad_Snare"], drumSwitches["Pad_Crash"] },
                new[] { "Kick", "Tom", "Snare", "Crash" }, new[] { new Color(1f, 0.55f, 0.3f), new Color(1f, 0.8f, 0.35f), new Color(0.9f, 0.9f, 0.95f), new Color(1f, 0.85f, 0.3f) },
                kitCam, kitCenter + new Vector3(0f, 0.65f, 0.1f), 42f, "NIGHT TRAIN - INTRO");
            DialogueSequence daniSeq = SeqAsset("Hometown/Dani",
                OnlyIf("clue_garage", Say(spkDani, "Summer fest, next year. You, me, NIGHT TRAIN. {p}{small}I'm printing posters. Don't make me waste posters.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("clue_bakery", Say(spkDani, "Your sister left something on my snare. {p}Very official. She said you have to play first. Our intro.")),
                OnlyIf("clue_bakery", Say(spkDani, "You remember it. Kick, tom, snare, crash. {p}{small}It's written on the amp, genius. In case your brain moved to the city already.{/small}", "", DialogueSequence.EndNodeId)),
                Say(spkDani, "There he is. The traitor. {p}...I'm joking. Mostly."),
                Say(spkDani, "One last jam before the big city? {p}{small}Later. I'm tuning. It takes me an hour to make it sound this bad.{/small}"));
            Inspectable(dani, "Dani", L("..."), null, null, spkDani, daniSeq);
            Event(ctx, "DaniAfterDrums", StoryEvent.TriggerMode.OnFlag, "drums_played", 0.6f, null, null,
                L("...Still got it. {p}Sort of. Your crash was late.",
                  "Here. From your sister. {p}I'm not allowed to read it. {small}I read it.{/small}",
                  "* A drawing of the old swing over the river, three little lanterns on the bridge, and: <color=#FF4FD8>IN THE ORDER YOU FOUND ME</color>.",
                  "Summer fest next year, yeah? You come back, we play. {p}...Leo. {p}{slow}Come back.{/slow}",
                  "* Sure, I tell him. {p}Easy."),
                spkDani, "clue_garage");

            // ---------------------------------------------------------------- start of the day
            Event(ctx, "PrologueStart", StoryEvent.TriggerMode.OnStart, string.Empty, 0f, null, null, null, null, "in_prologue");
            Event(ctx, "WhereTicket", StoryEvent.TriggerMode.OnStart, "!hunt_started", 1.4f, null, null,
                L("* My ticket was on the desk. {p}It's not on the desk."), null, string.Empty);
            Event(ctx, "DaniText", StoryEvent.TriggerMode.OnFlag, "hunt_started", 8f, "DANI",
                L("last night in town and u havent come to say bye?? {p}carport. now."), null, null, string.Empty);
            Event(ctx, "MomText", StoryEvent.TriggerMode.OnFlag, "mia_promise", 3f, "MOM",
                L("Dinner's in a box for the train. Come up before you go. Don't argue."), null, null, string.Empty);

            // ---------------------------------------------------------------- intro
            Transform cp1 = CinematicPoint("CP_Halden_Wide", points, new Vector3(-6f, 16f, -34f), new Vector3(12f, 0f, 4f));
            Transform cp2 = CinematicPoint("CP_Halden_Square", points, new Vector3(6f, 2.6f, 2f), new Vector3(14f, 2.2f, 11f));
            Transform cp3 = CinematicPoint("CP_Flat_Boxes", points, flat + new Vector3(-1.6f, 1.7f, -2.6f), flat + new Vector3(2.4f, 0.5f, 0.4f));
            CinematicSequence intro = Cinematic("Cinematic_Intro_Halden", ctx.story,
                Shot("Wide", cp1, 0f, 2.2f, 32f, L("* Three years earlier."), null, true, Clip("bong_001")),
                Shot("Square", cp2, 5f, 0.6f, 30f, L("* Halden. Six in the evening, the last day of June.")),
                Shot("Boxes", cp3, 0f, 1.2f, 30f, L("* Thirteen boxes taped. One to go. {p}The train's at 21:40."), null, true));
            intro.fadeFromBlack = true;
            intro.blackHoldBeforeFade = 1.2f;
            intro.fadeInDuration = 3f;
            intro.blendToGameplay = true;
            intro.blendToGameplayDuration = 2f;
            EditorUtility.SetDirty(intro);
            SetIntro(ctx, intro);

            EndGameplayScene(ctx, HometownScene);
        }

        // ====================================================================== the flat above the bakery

        private static void BuildFamilyFlat(Ctx ctx, Vector3 c, HouseDoor street)
        {
            const string F = "furniture";
            Color evening = new Color(1f, 0.72f, 0.45f);
            Material floor = LitMaterial("Flat/M_Floor", new Color(0.56f, 0.4f, 0.28f));
            Material wall = LitMaterial("Flat/M_Wall", new Color(0.86f, 0.8f, 0.7f));
            Material tape = LitMaterial("Flat/M_Tape", new Color(0.8f, 0.7f, 0.45f));
            Material paper = LitMaterial("Town/M_Paper", new Color(0.97f, 0.95f, 0.88f));
            Material crayonSheet = LitMaterial("Flat/M_Drawing", new Color(0.98f, 0.96f, 0.9f));
            float north = Room(ctx, "Flat", c, 9f, 6.4f, floor, wall);
            HouseDoor inside = BuildDoor(ctx, "FlatStairs", new Vector3(c.x + 2.7f, 0f, north - 0.12f), 180f, LitMaterial("Town/M_FlatDoor", new Color(0.2f, 0.32f, 0.4f)), evening, null, "HOME");
            LinkDoors(street, inside);
            SetStrings(inside, "enterLines", L("* The stairs. Twenty-two steps. {p}I could do them asleep. I have."));
            AddStemZone(ctx, "Flat", c + Vector3.up, new Vector3(9.5f, 4f, 7f), StemMix.Home);

            // Window over the square (east wall).
            GameObject window = Block("Prop_FlatWindow", ctx.props, new Vector3(c.x + 4.47f, 1.5f, c.z - 0.6f), new Vector3(0.04f, 1.1f, 1.5f), EmissiveMaterial("Flat/M_Window", new Color(0.45f, 0.34f, 0.24f), evening * 1.1f), ctx.interactableLayer);
            PointLight(window.transform, new Vector3(c.x + 3.6f, 1.6f, c.z - 0.6f), evening, 3f, 6.5f);
            Inspectable(window, "FlatWindow",
                L("* The square. The fountain. Mr. Russo smoking where Mom can't see him.", "* Six in the evening, in summer. {p}If I could keep one hour, it'd be this one."),
                L("* Somebody rides past on a bike. Bell, bell. {p}Gone."), "+openness#flat_window");

            // Kitchen (north-west), Mom.
            Prop(ctx, "FlatFridge", "kitchenFridgeSmall", F, new Vector3(c.x - 4.0f, 0f, north - 0.4f), 180f, 1.4f, false,
                L("* The fridge door is a museum of Mia. {p}Crayon cats, crayon trains, a crayon Leo with an enormous head.",
                  "* In the corner, an old one: a grey kitten, its name coloured over so many times it's just a dark smudge."),
                L("* The enormous head is accurate, apparently."));
            Prop(ctx, "FlatStove", "kitchenStove", F, new Vector3(c.x - 3.05f, 0f, north - 0.4f), 180f, 0.95f, false, null);
            Prop(ctx, "FlatCabinet", "kitchenCabinet", F, new Vector3(c.x - 2.15f, 0f, north - 0.4f), 180f, 0.95f, false, null);
            GameObject mom = Character(ctx, "Mom", "character-female-b", "mini-characters", new Vector3(c.x - 2.6f, 0f, north - 1.3f), 160f, 1.0f);
            DialogueSequence momSeq = SeqAsset("Hometown/Mom",
                OnlyIf("ready_to_leave", Say(spkMom, "Go on. You'll miss it. {p}{small}Call when you get there.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("box14_sealed", Say(spkMom, "{cam=close}Got everything? {p}Charger. Toothbrush. The box with the dinner in it.")),
                OnlyIf("box14_sealed", Say(spkMom, "Your father's on nights this week. He said to tell you he's proud. {p}{small}He won't say it to your face. You two are the same.{/small}")),
                OnlyIf("box14_sealed", Say(spkMom, "{cam=two}Call when you get there. {p}And eat something that isn't from a machine.")),
                OnlyIf("box14_sealed", Say(spkLeoInner, "{cam=off}She doesn't say don't go. {p}{slow}She never does.{/slow}", "", DialogueSequence.EndNodeId, "ready_to_leave")),
                OnlyIf("mia_promise", Say(spkMom, "She gave you the program, didn't she. {p}She's been drawing that seat for a week. {p}{small}Put it somewhere safe. Box fourteen's still open.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("hunt_started", Say(spkMom, "Your sister's been out since lunch. With a clipboard. {p}I'm staying out of it. {small}I'm her mother, not an accomplice.{/small}", "", DialogueSequence.EndNodeId)),
                Say(spkMom, "There he is. {p}I made too much food. I always make too much food when people leave."),
                Say(spkMom, "Have you checked your ticket? {p}{small}Check your ticket.{/small}"));
            Inspectable(mom, "Mom", L("..."), null, null, spkMom, momSeq);

            Prop(ctx, "FlatTable", "tableRound", F, new Vector3(c.x - 2.2f, 0f, c.z - 0.6f), 0f, 0.75f, false,
                L("* Dinner for the train, in a box, with a note: <color=#FFD84F>EAT THIS. LOVE, MOM.</color> {p}And under it, in different writing: <color=#FF4FD8>AND MIA</color>."),
                L("* EAT THIS. LOVE, MOM. AND MIA."));
            Prop(ctx, "FlatChairA", "chair", F, new Vector3(c.x - 2.9f, 0f, c.z - 0.6f), 90f, 0.85f, false, null);
            Prop(ctx, "FlatChairB", "chair", F, new Vector3(c.x - 1.5f, 0f, c.z - 0.6f), -90f, 0.85f, false, null);
            Prop(ctx, "FlatRug", "rugRounded", F, new Vector3(c.x - 0.2f, 0f, c.z - 0.4f), 0f, 3f, true, null, null, null, false);
            Prop(ctx, "FlatSofa", "loungeSofa", F, new Vector3(c.x - 0.3f, 0f, c.z - 2.4f), 0f, 2f, true,
                L("* The sofa where Dad falls asleep during every film. {p}Every single one."),
                L("* He once fell asleep during the end of the world. On TV. {p}It was a good film."));
            Prop(ctx, "FlatLamp", "lampRoundFloor", F, new Vector3(c.x + 1.65f, 0f, north - 0.35f), 0f, 1.4f, false, null);

            // Leo's corner: the desk (where the ticket was), the boxes.
            GameObject desk = Prop(ctx, "LeoDesk", "desk", F, new Vector3(c.x + 0.6f, 0f, north - 0.4f), 180f, 0.78f, false, null);
            GameObject note = Block("Prop_TicketNote", ctx.props, new Vector3(c.x + 0.6f, Top(desk) + 0.01f, north - 0.45f), new Vector3(0.3f, 0.01f, 0.22f), crayonSheet, ctx.interactableLayer);
            var noteReach = note.AddComponent<BoxCollider>();
            noteReach.isTrigger = true;
            noteReach.size = new Vector3(3.5f, 60f, 4f);
            DialogueSequence noteSeq = SeqAsset("Hometown/DeskNote",
                OnlyIf("hunt_started", Say(null, "The crayon drawing. A loaf of bread with a sun coming up behind it. <color=#FF4FD8>WHERE THE MORNING STARTS</color>.", "", DialogueSequence.EndNodeId)),
                Say(null, "Where the ticket was: a crayon drawing. A loaf of bread, a sun coming up behind it."),
                Say(null, "<color=#FF4FD8>WHERE THE MORNING STARTS. - M</color>"),
                Say(spkLeoInner, "...Mia. {p}{slow}Of course.{/slow}", "", "", "hunt_started"),
                Say(spkLeoInner, "The morning starts at five, downstairs. Mr. Russo's ovens."));
            Inspectable(note, "TicketNote", L("..."), null, null, null, noteSeq);

            var rng = new System.Random(14);
            int n = 0;
            for (int row = 0; row < 2 && n < 13; row++)
                for (int i = 0; i < 5 && n < 13; i++, n++)
                {
                    Vector3 p = new Vector3(c.x + 3.8f - row * 0.62f, 0f, c.z + 2.0f - i * 0.62f);
                    GameObject box = Prop(ctx, "Box" + n, "cardboardBoxClosed", F, p, rng.Next(-8, 8), 0.5f, false, null);
                    if (i % 2 == 0 && n < 12)
                    {
                        n++;
                        Prop(ctx, "Box" + n, "cardboardBoxClosed", F, p + Vector3.up * Top(box), rng.Next(-10, 10), 0.46f, false, null, null, null, false);
                    }
                }
            GameObject box14 = Prop(ctx, "Box14", "cardboardBoxOpen", F, new Vector3(c.x + 2.2f, 0f, c.z - 1.3f), 12f, 0.55f, false, L("..."));
            DialogueSequence boxSeq = SeqAsset("Hometown/Box14",
                OnlyIf("box14_sealed", Say(null, "Box fourteen. Taped shut. Two layers. {p}I'll open it when I'm settled. First thing.", "", DialogueSequence.EndNodeId)),
                OnlyIf("key.recital_program", Say(null, "You put the recital program in, on top. Front row, seat one, the crayon heart facing up.")),
                OnlyIf("key.recital_program", Say(null, "{quake}Rrrip. {p}Tape. Two layers.")),
                OnlyIf("key.recital_program", Say(spkLeoInner, "Fourteen. {p}The lightest one. {p}{slow}I'll open it first.{/slow}", "", DialogueSequence.EndNodeId, "take:recital_program;box14_sealed")),
                Say(null, "Box fourteen. Nearly empty. {p}There's something that should go in here. I don't know what yet."));
            Inspectable(box14, "Box14", L("..."), null, null, null, boxSeq);
            Prop(ctx, "TapeRoll", "cup", "food", new Vector3(c.x + 2.65f, 0f, c.z - 1.75f), 0f, 0.12f, false, null, null, null, false);

            // Mia's door (her room, off limits).
            HouseDoor miaDoor = LockedDoor(ctx, "MiaRoom", new Vector3(c.x - 0.9f, 0f, north - 0.12f), 180f, LitMaterial("Flat/M_MiaDoor", new Color(0.85f, 0.55f, 0.65f)), evening, null,
                L("* MIA'S ROOM. KNOCK. {p}Underneath, smaller: ESPECIALLY LEO."));
            DoorNote(miaDoor, "MIA'S ROOM\nKNOCK!!");
            Prop(ctx, "FlatPlant", "pottedPlant", F, new Vector3(c.x + 4.0f, 0f, north - 0.4f), 0f, 1.1f, false, null);
            PointLight(ctx.env, c + new Vector3(-2.4f, 2.4f, 0.6f), new Color(1f, 0.8f, 0.6f), 1.6f, 6f);
        }
    }
}
#endif
