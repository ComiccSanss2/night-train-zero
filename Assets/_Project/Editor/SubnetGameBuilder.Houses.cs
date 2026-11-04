#if UNITY_EDITOR
using Subnet.Audio;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using Subnet.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>
    /// Buildings you can walk into: animated doors, interiors built far away in the same scene,
    /// the NEON MART shop (with its own music), apartment 0451, and Leo's childhood room.
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        private static DialogueSpeaker spkClerk;

        private static DialogueSpeaker Clerk()
        {
            if (spkClerk == null)
            {
                spkClerk = CreateSpeaker("Speaker_Clerk", "SUNNY", "pluck_001", new Vector2(1.15f, 1.3f), 1.15f, new Color(1f, 0.85f, 0.35f),
                    SpeakerStyle.Default, new Color(1f, 0.95f, 0.8f));
            }
            return spkClerk;
        }

        // ====================================================================== Doors

        /// <summary>
        /// A door with frame, hinge, knob, window and a warm light that spills out when it opens.
        /// <paramref name="yaw"/> turns the door so its front faces where Leo stands.
        /// With <paramref name="backing"/>, a small facade panel sits behind it (so it never floats in a gap between buildings).
        /// </summary>
        private static HouseDoor BuildDoor(Ctx ctx, string key, Vector3 position, float yaw, Material doorMat, Color glow,
            Material backing = null, string placeName = null)
        {
            var root = new GameObject("Door_" + key);
            root.transform.SetParent(ctx.props, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            root.layer = ctx.interactableLayer;
            Transform t = root.transform;
            Vector3 euler = new Vector3(0f, yaw, 0f);

            Material frameMat = LitMaterial("Houses/M_DoorFrame", new Color(0.16f, 0.13f, 0.18f));
            Material knobMat = EmissiveMaterial("Houses/M_DoorKnob", new Color(0.6f, 0.5f, 0.2f), new Color(1f, 0.8f, 0.3f) * 0.6f);
            Material voidMat = EmissiveMaterial("Houses/M_DoorVoid_" + key, new Color(0.05f, 0.03f, 0.04f), glow * 0.55f);
            Material windowMat = EmissiveMaterial("Houses/M_DoorWindow_" + key, new Color(0.2f, 0.18f, 0.15f), glow * 1.6f);

            if (backing != null)
            {
                GameObject panel = Block("Facade", t, t.TransformPoint(new Vector3(0f, 1.8f, -0.25f)), new Vector3(2.4f, 3.6f, 0.3f), backing, 0);
                panel.transform.rotation = Quaternion.Euler(euler);
                Deco("Step", t, t.TransformPoint(new Vector3(0f, 0.04f, 0.35f)), new Vector3(1.5f, 0.08f, 0.6f), frameMat, euler);
            }
            Deco("PostL", t, t.TransformPoint(new Vector3(-0.58f, 1.12f, 0f)), new Vector3(0.14f, 2.24f, 0.18f), frameMat, euler);
            Deco("PostR", t, t.TransformPoint(new Vector3(0.58f, 1.12f, 0f)), new Vector3(0.14f, 2.24f, 0.18f), frameMat, euler);
            Deco("Lintel", t, t.TransformPoint(new Vector3(0f, 2.3f, 0f)), new Vector3(1.3f, 0.14f, 0.18f), frameMat, euler);
            Deco("Void", t, t.TransformPoint(new Vector3(0f, 1.1f, -0.07f)), new Vector3(1.02f, 2.18f, 0.02f), voidMat, euler);

            // Hinge on the left post; the panel swings inward (away from Leo).
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-0.5f, 0f, -0.01f);
            hinge.localRotation = Quaternion.identity;
            GameObject leaf = Deco("Leaf", hinge, hinge.TransformPoint(new Vector3(0.5f, 1.09f, 0f)), new Vector3(0.98f, 2.12f, 0.07f), doorMat, euler);
            leaf.transform.SetParent(hinge, true);
            GameObject knob = Deco("Knob", hinge, hinge.TransformPoint(new Vector3(0.86f, 1.02f, 0.06f)), new Vector3(0.08f, 0.08f, 0.08f), knobMat, euler);
            knob.transform.SetParent(hinge, true);
            GameObject pane = Deco("Window", hinge, hinge.TransformPoint(new Vector3(0.5f, 1.62f, 0.04f)), new Vector3(0.42f, 0.36f, 0.02f), windowMat, euler);
            pane.transform.SetParent(hinge, true);

            // Lamp above the door.
            Deco("Lamp", t, t.TransformPoint(new Vector3(0f, 2.55f, 0.12f)), new Vector3(0.3f, 0.12f, 0.16f), windowMat, euler);
            PointLight(t, t.TransformPoint(new Vector3(0f, 2.45f, 0.5f)), glow, 1.2f, 3f);
            Light spill = PointLight(t, t.TransformPoint(new Vector3(0f, 1.1f, 0.45f)), glow, 0f, 3.5f);
            spill.name = "Spill";

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.1f, 0.25f);
            collider.size = new Vector3(1.3f, 2.2f, 0.6f);
            collider.isTrigger = true;

            var door = root.AddComponent<HouseDoor>();
            Set(door, "hinge", hinge);
            Set(door, "spill", spill);
            Set(door, "audioSource", ctx.sfx);
            Set(door, "openClip", Clip("open_002"));
            Set(door, "closeClip", Clip("close_003"));
            Set(door, "lockedClip", Clip("error_004"));
            if (!string.IsNullOrEmpty(placeName)) Set(door, "placeName", placeName);
            return door;
        }

        private static DialogueNode OnlyIf(string flag, DialogueNode node)
        {
            node.requiredFlag = flag;
            return node;
        }

        private static void LinkDoors(HouseDoor a, HouseDoor b)
        {
            Set(a, "otherSide", b);
            Set(b, "otherSide", a);
        }

        /// <summary>A locked door with a funny note (the door rattles).</summary>
        private static void LockedDoor(Ctx ctx, string key, Vector3 position, float yaw, Material doorMat, Color glow, Material backing, string[] lines)
        {
            HouseDoor door = BuildDoor(ctx, key, position, yaw, doorMat, glow, backing);
            SetStrings(door, "lockedLines", lines);
            Set(door, "requiredFlag", "__never");
        }

        /// <summary>Floating 3D neon text (shop signs, apartment numbers).</summary>
        private static TextMeshPro WorldSign(Transform parent, string text, Vector3 position, float yaw, Color color, float size)
        {
            var go = new GameObject("Sign_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            // TextMeshPro faces -Z by default; turn it so it reads from the door's front side.
            go.transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            Localize(tmp);
            return tmp;
        }

        /// <summary>
        /// An interior shell: floor, the two walls the camera can see (north, east), invisible bounds on the others.
        /// Returns the north wall's inner z.
        /// </summary>
        private static float Room(Ctx ctx, string key, Vector3 center, float width, float depth, Material floor, Material wall, float height = 2.8f)
        {
            Block(key + "_Floor", ctx.env, center + new Vector3(0f, -0.1f, 0f), new Vector3(width, 0.2f, depth), floor, ctx.groundLayer);
            Block(key + "_WallN", ctx.env, center + new Vector3(0f, height * 0.5f, depth * 0.5f + 0.1f), new Vector3(width + 0.4f, height, 0.2f), wall, 0);
            Block(key + "_WallE", ctx.env, center + new Vector3(width * 0.5f + 0.1f, height * 0.5f, 0f), new Vector3(0.2f, height, depth + 0.4f), wall, 0);
            Block(key + "_BoundS", ctx.env, center + new Vector3(0f, 1f, -depth * 0.5f - 0.1f), new Vector3(width + 0.4f, 2f, 0.2f), null, 0);
            Block(key + "_BoundW", ctx.env, center + new Vector3(-width * 0.5f - 0.1f, 1f, 0f), new Vector3(0.2f, 2f, depth + 0.4f), null, 0);
            // Baseboards give the walls a finished edge.
            Material trim = LitMaterial("Houses/M_Trim", new Color(0.12f, 0.1f, 0.14f));
            Deco(key + "_TrimN", ctx.env, center + new Vector3(0f, 0.06f, depth * 0.5f - 0.01f), new Vector3(width, 0.12f, 0.04f), trim);
            Deco(key + "_TrimE", ctx.env, center + new Vector3(width * 0.5f - 0.01f, 0.06f, 0f), new Vector3(0.04f, 0.12f, depth), trim);
            return center.z + depth * 0.5f;
        }

        private static void SetPrizes(TokenMachine machine, params (string reward, float weight, string line)[] prizes)
        {
            var so = new SerializedObject(machine);
            SerializedProperty list = so.FindProperty("prizes");
            list.arraySize = prizes.Length;
            for (int i = 0; i < prizes.Length; i++)
            {
                SerializedProperty p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("reward").stringValue = prizes[i].reward;
                p.FindPropertyRelative("weight").floatValue = prizes[i].weight;
                p.FindPropertyRelative("line").stringValue = prizes[i].line;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ====================================================================== NEON MART (act II)

        private static void BuildNeonMart(Ctx ctx, Vector3 streetDoor, Material facade)
        {
            const string F = "furniture";
            const string A = "mini-arcade";
            Color pink = new Color(1f, 0.45f, 0.8f);
            Material doorMat = LitMaterial("Houses/M_Door_Mart", new Color(0.15f, 0.55f, 0.6f));

            HouseDoor outside = BuildDoor(ctx, "NeonMart", streetDoor, 180f, doorMat, pink, facade, null);
            SetStrings(outside, "enterLines", new string[0]);
            WorldSign(outside.transform, "NEON MART", streetDoor + new Vector3(0f, 3.25f, -0.4f), 180f, new Color(1f, 0.5f, 0.85f), 7f);
            WorldSign(outside.transform, "OPEN 24/7", streetDoor + new Vector3(0f, 2.85f, -0.4f), 180f, Cyan, 3.2f);
            Prop(ctx, "MartAwning", "detail-awning", "commercial", streetDoor + new Vector3(0f, 2.62f, -0.55f), 180f, 2.2f, true, null, null, null, false);

            // Interior, far away from the street.
            Vector3 c = new Vector3(40f, 0f, -30f);
            Material floor = LitMaterial("Houses/M_Mart_Floor", new Color(0.35f, 0.85f, 0.85f), 0.6f);
            Material wall = LitMaterial("Houses/M_Mart_Wall", new Color(0.55f, 0.4f, 0.75f));
            Material neonPink = EmissiveMaterial("Houses/M_Mart_NeonPink", new Color(0.3f, 0.05f, 0.2f), pink * 3f);
            Material neonCyan = EmissiveMaterial("Houses/M_Mart_NeonCyan", new Color(0.05f, 0.25f, 0.25f), Cyan * 3f);
            Material tile = LitMaterial("Houses/M_Mart_Tile", new Color(0.95f, 0.6f, 0.85f), 0.6f);
            float north = Room(ctx, "Mart", c, 10f, 7f, floor, wall);
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 4; z++)
                {
                    if ((x + z) % 2 == 0) Deco("MartTile", ctx.env, c + new Vector3(-4f + x * 2f, 0.005f, -2.6f + z * 1.75f), new Vector3(1f, 0.01f, 0.87f), tile);
                }
            }
            Deco("NeonStripN", ctx.env, new Vector3(c.x, 2.5f, north - 0.02f), new Vector3(9.6f, 0.06f, 0.03f), neonPink);
            Deco("NeonStripE", ctx.env, new Vector3(c.x + 4.98f, 2.5f, c.z), new Vector3(0.03f, 0.06f, 6.6f), neonCyan);
            WorldSign(ctx.env, "NEON MART", new Vector3(c.x + 1.8f, 2.05f, north - 0.05f), 180f, new Color(1f, 0.5f, 0.85f), 6f);

            HouseDoor inside = BuildDoor(ctx, "NeonMart_In", new Vector3(c.x - 3.6f, 0f, north - 0.12f), 180f, doorMat, Warm, null, "NEON MART");
            LinkDoors(outside, inside);

            // Counter + Sunny.
            GameObject bar1 = Prop(ctx, "MartCounter1", "kitchenBar", F, c + new Vector3(1.2f, 0f, 1.6f), 0f, 1f, false, null);
            Prop(ctx, "MartCounter2", "kitchenBarEnd", F, c + new Vector3(2.2f, 0f, 1.6f), 0f, 1f, false, null);
            Prop(ctx, "Register", "cash-register", A, c + new Vector3(1.2f, Top(bar1), 1.6f), 180f, 0.4f, false, null);
            GameObject sunny = Character(ctx, "Sunny", "character-employee", A, c + new Vector3(1.7f, 0f, 2.6f), 180f, 1.05f, null, true);
            PointLight(sunny.transform, c + new Vector3(1.7f, 2.2f, 1.6f), new Color(1f, 0.85f, 0.6f), 2.2f, 4f);

            GameObject counterZone = Block("Prop_ShopCounter", ctx.props, c + new Vector3(1.7f, 0.6f, 1.3f), new Vector3(2.4f, 1.2f, 0.8f), null, ctx.interactableLayer);
            counterZone.GetComponent<BoxCollider>().isTrigger = true;
            var shop = counterZone.AddComponent<ShopCounter>();
            Set(shop, "keeper", Clerk());
            SetStrings(shop, "firstGreeting", L(
                "Welcome to NEON MART! {p}Open 24/7. {p}{small}Mostly the 7.{/small}",
                "I'm Sunny. Night shift. {p}{slow}Every shift is the night shift down here.{/slow}",
                "We take metro tokens. {p}You get them by calming down angry feelings. {p}{small}Gently. Don't punch the feelings.{/small}",
                "Fun fact: I've never seen the sun. {p}My parents named me Sunny anyway. {p}{wave}Optimists.{/wave}"));
            SetStrings(shop, "greeting", L("Oh hey, it's you! {p}My favorite customer. {p}{small}My only customer, but still.{/small}",
                "If you see a cat with a screen on his collar, tell him he still owes me for a can of tuna."));
            Set(shop, "audioSource", ctx.sfx);
            Set(shop, "moveClip", Clip("click_002"));
            Set(shop, "buyClip", Clip("confirmation_002"));
            Set(shop, "errorClip", Clip("error_002"));

            // Claw machine (the Byte plush!).
            GameObject claw = Prop(ctx, "ClawMachine", "claw-machine", A, c + new Vector3(-3.9f, 0f, -0.6f), 90f, 1.9f, false, null);
            claw.layer = ctx.interactableLayer;
            var clawGame = claw.AddComponent<TokenMachine>();
            Set(clawGame, "machineId", "claw");
            Set(clawGame, "cost", 2);
            Set(clawGame, "prompt", "A claw machine full of plush cats. {p}One of them looks a LOT like Byte. {p}Play for 2 tokens?");
            Set(clawGame, "playOption", "[Play  ◊2]");
            Set(clawGame, "leaveOption", "[Leave]");
            SetStrings(clawGame, "playingLines", L("* The claw wobbles down. {p}{shake}Grabs...{/shake}"));
            Set(clawGame, "brokeLine", "* You pat your pockets. {p}Not enough tokens.");
            SetPrizes(clawGame,
                ("", 45f, "* ...air. {p}It grabbed air. Very confidently."),
                ("ramen", 25f, "* A Cup Ramen?! {p}That wasn't even in there."),
                ("potion", 15f, "* A Fizzy Potion rolls into the chute. {p}{bounce}Nice.{/bounce}"),
                ("plush_cat", 15f, "* The Byte-looking plush! It wobbles... {p}and drops into the chute!"));
            Set(clawGame, "guaranteedOnTry", 3);
            Set(clawGame, "guaranteedReward", "plush_cat");
            Set(clawGame, "guaranteedLine", "* The claw grabs the Byte-looking plush. It wobbles... {p}{bounce}and drops it in the chute!{/bounce} {p}Byte pretends not to see.");
            Set(clawGame, "wobble", claw.transform.Find("Model"));
            Set(clawGame, "audioSource", ctx.sfx);
            Set(clawGame, "playClip", Clip("switch_005"));
            Set(clawGame, "winClip", Clip("confirmation_003"));

            // Vending machine (always works, unlike the one outside).
            GameObject vending = Prop(ctx, "MartVending", "vending-machine", A, c + new Vector3(-3.9f, 0f, 1.4f), 90f, 1.9f, false, null);
            vending.layer = ctx.interactableLayer;
            var vend = vending.AddComponent<TokenMachine>();
            Set(vend, "machineId", "vending");
            Set(vend, "cost", 3);
            Set(vend, "prompt", "A vending machine. Every slot is Cup Ramen. {p}Buy one for 3 tokens?");
            Set(vend, "playOption", "[Buy  ◊3]");
            Set(vend, "leaveOption", "[Leave]");
            Set(vend, "brokeLine", "* Not enough tokens. {p}The machine hums sympathetically.");
            SetPrizes(vend, ("ramen", 1f, "* Clunk. {p}A Cup Ramen tumbles out. Somehow it's already warm."));
            Set(vend, "guaranteedOnTry", 0);
            Set(vend, "wobble", vending.transform.Find("Model"));
            Set(vend, "audioSource", ctx.sfx);
            Set(vend, "playClip", Clip("drop_002"));
            Set(vend, "winClip", Clip("confirmation_001"));

            // Arcade cabinet: Mia's high score.
            Prop(ctx, "ArcadeCabinet", "arcade-machine", A, c + new Vector3(3.9f, 0f, 2.6f), 180f, 1.9f, false,
                L("NIGHT RUNNER. {p}HIGH SCORES:",
                  "* 1. MIA ..... 99,850 {p}2. LEO ..... 99,840 {p}3. AAA ..... 00,010",
                  "Mia beat my score the summer before I left. {p}I said I'd win it back. {p}{slow}I never played again.{/slow}"),
                L("The screen loops: INSERT COIN. INSERT COIN. {p}{small}Someday.{/small}"), "+openness#arcade_mia", true, spkLeoInner);

            // Dance machine (pure joy, zero tokens).
            Prop(ctx, "DanceMachine", "dance-machine", A, c + new Vector3(3.9f, 0f, -1.2f), -90f, 1.6f, false,
                L("* A dance machine. The arrows blink at you, expectantly.", "* You do one (1) step.", "{bounce}i saw that.{/bounce} {p}{small}im telling everyone.{/small}"),
                L("* The machine still remembers your one step. {p}It gave you a B."), "+closeness#dance_step", true, spkByte);

            // Shelves of snacks, fridge of potions.
            for (int i = 0; i < 3; i++)
            {
                GameObject shelf = Prop(ctx, "MartShelf" + i, "bookcaseOpenLow", F, c + new Vector3(-1.6f + i * 1.3f, 0f, -1.7f), 0f, 0.9f, false, null);
                string[] snacks = { "soda-can", "candy-bar", "bag", "donut-sprinkles", "can-small", "cookie-chocolate" };
                for (int s = 0; s < 3; s++)
                {
                    Prop(ctx, "Snack" + i + s, snacks[(i * 2 + s) % snacks.Length], "food", c + new Vector3(-2f + i * 1.3f + s * 0.35f, Top(shelf), -1.7f), 0f, 0.22f, false, null, null, null, false);
                }
            }
            Prop(ctx, "PotionFridge", "kitchenFridgeLarge", F, c + new Vector3(-1.2f, 0f, 3.05f), 180f, 2f, false,
                L("* Rows of Fizzy Potions, glowing pink. {p}A handwritten sign: DON'T SHAKE. (WE MEAN IT.)"),
                L("* One of the potions is fizzing angrily. You leave it alone."));
            PointLight(ctx.env, c + new Vector3(-1.2f, 1.5f, 2.4f), pink, 1.5f, 3f);
            Prop(ctx, "PrizeWheel", "prize-wheel", A, c + new Vector3(0.2f, 0f, 3.1f), 180f, 1.6f, false, null);
            Prop(ctx, "MartPlant", "pottedPlant", F, c + new Vector3(4.3f, 0f, 0.8f), 0f, 0.9f, false, null);

            PointLight(ctx.env, c + new Vector3(-2f, 2.6f, 0f), pink, 2.4f, 7f);
            PointLight(ctx.env, c + new Vector3(2.5f, 2.6f, -1f), Cyan, 2.2f, 7f);

            // The shop has its own song.
            var zone = new GameObject("MusicZone_NeonMart");
            zone.transform.SetParent(ctx.story, false);
            zone.transform.position = c + new Vector3(0f, 1f, 0f);
            var music = zone.AddComponent<MusicZone>();
            Set(music, "track", Clip("funkygameost"));
            Set(music, "size", new Vector3(11f, 5f, 8f));
        }

        // ====================================================================== Apartment 0451 (act II)

        private static void BuildApartment0451(Ctx ctx, Vector3 streetDoor, Material facade)
        {
            const string F = "furniture";
            Color cold = new Color(0.55f, 0.7f, 1f);
            Material doorMat = LitMaterial("Houses/M_Door_0451", new Color(0.35f, 0.33f, 0.3f));
            HouseDoor outside = BuildDoor(ctx, "Apt0451", streetDoor, 180f, doorMat, cold, facade, null);
            WorldSign(outside.transform, "0451", streetDoor + new Vector3(0f, 2.75f, -0.4f), 180f, new Color(0.85f, 0.9f, 1f), 4.5f);

            Vector3 c = new Vector3(40f, 0f, -55f);
            Material floor = LitMaterial("Houses/M_0451_Floor", new Color(0.3f, 0.3f, 0.33f));
            Material wall = LitMaterial("Houses/M_0451_Wall", new Color(0.62f, 0.64f, 0.66f));
            Material paper = LitMaterial("Subnet/M_Paperwork", new Color(0.96f, 0.95f, 0.9f));
            Material screen = EmissiveMaterial("Houses/M_0451_Screen", new Color(0.05f, 0.08f, 0.12f), cold * 2f);
            float north = Room(ctx, "Apt", c, 8f, 6f, floor, wall);
            HouseDoor inside = BuildDoor(ctx, "Apt0451_In", new Vector3(c.x - 2.8f, 0f, north - 0.12f), 180f, doorMat, Warm, null, "APARTMENT 0451");
            LinkDoors(outside, inside);

            Prop(ctx, "AptBed", "bedSingle", F, c + new Vector3(3f, 0f, 1.7f), 180f, 0.7f, false,
                L("A single bed, made so tight you could bounce a coin off it. {p}It doesn't look slept in. {p}{slow}It looks waited in.{/slow}"),
                L("* The pillow still has the fold from the shop."));
            GameObject desk = Prop(ctx, "AptDesk", "desk", F, c + new Vector3(0.6f, 0f, 2.55f), 180f, 0.75f, false, null);
            GameObject monitor = Prop(ctx, "AptComputer", "computerScreen", F, c + new Vector3(0.6f, Top(desk), 2.6f), 180f, 0.45f, false,
                L("His computer is still on. 2,341 unread emails.",
                  "The oldest one is from his daughter. {p}Subject: <color=#FF4FD8>are you coming saturday?</color>"),
                L("You don't open it. {p}It isn't yours to open."));
            PointLight(monitor.transform, monitor.transform.position + new Vector3(0f, 0.4f, -0.5f), cold, 1.6f, 3f);

            // Kitchen corner: the cake.
            Prop(ctx, "AptFridge", "kitchenFridgeSmall", F, c + new Vector3(-3.4f, 0f, 0.4f), 90f, 1.3f, false,
                L("* A photo on the fridge: him and a little girl with a gap-toothed grin, at the beach.", "He's wearing a tie. {p}At the beach."),
                L("* The magnet holding the photo is shaped like a sun."));
            GameObject table = Prop(ctx, "AptTable", "table", F, c + new Vector3(-1.4f, 0f, -0.6f), 0f, 0.75f, false, null);
            Prop(ctx, "AptChair", "chair", F, c + new Vector3(-1.4f, 0f, -1.4f), 0f, 0.8f, false, null);
            Prop(ctx, "BirthdayCake", "cake-birthday", "food", c + new Vector3(-1.4f, Top(table), -0.6f), 0f, 0.35f, false,
                L("A birthday cake, untouched. <color=#FF4FD8>HAPPY BIRTHDAY DAD</color> in wobbly icing.", "The candles were never lit."),
                L("* There's a lighter next to it. {p}Still in the packaging."));

            // The jar he never spent.
            Prop(ctx, "VacationJar", "glass", "food", c + new Vector3(-3.4f, 0f, -1.6f), 0f, 0.35f, false,
                L("A jar of metro tokens labelled <color=#FFD84F>VACATION</color>. {p}It's been filling up for eleven years.",
                  "* You take a few. {p}He'd want somebody to go somewhere."),
                L("The jar says VACATION. {p}You hope he uses the rest."), "$4#apt0451_jar");
            Deco("JarShelf", ctx.env, c + new Vector3(-3.4f, -0.05f, -1.6f), new Vector3(0.5f, 0.1f, 0.5f), LitMaterial("Houses/M_Trim", new Color(0.12f, 0.1f, 0.14f)));

            // Calendar on the wall.
            GameObject calendar = Block("Prop_Calendar", ctx.props, new Vector3(c.x + 3.98f, 1.55f, c.z - 0.6f), new Vector3(0.04f, 0.7f, 0.55f), paper, ctx.interactableLayer);
            Inspectable(calendar, "Calendar0451",
                L("A calendar. Every single day says <color=#FF4FD8>OT</color>.", "Except one Saturday, circled in red. {p}It just says: <color=#FFD84F>HER BIRTHDAY</color>."),
                L("* The circle is drawn three times. Like he was afraid it would fade."));

            Prop(ctx, "AptCoat", "coatRackStanding", F, c + new Vector3(-1.8f, 0f, 2.5f), 0f, 1.7f, false,
                L("* One coat. One umbrella. One pair of shoes by the door. {p}One of everything."), L("* One of everything."));

            // The answering machine: a voicemail that should reach him.
            DialogueSequence tape = SeqAsset("Houses/Apt0451_Voicemail",
                Say(null, "An answering machine. A tiny red light blinks. {p}<color=#FF4FD8>1 NEW MESSAGE.</color>"),
                Say(null, "You press play."),
                Say(null, "{small}'Hi daddy! It's me! Mom says you're working late again.'{/small}"),
                Say(null, "{wave}'Happy birthday to youuu... happy birthday to youuu...'{/wave}"),
                Say(null, "{small}'...Okay. Bye. I saved you the piece with the most frosting.'{/small}"),
                Say(spkLeo, "...He needs to hear this. {p}Not from a machine. {p}{slow}From her.{/slow}"),
                Say(spkByte, "take the tape. {p}{small}hes right outside. sitting on a curb, probably.{/small}"),
                Say(null, "(You got the VOICEMAIL TAPE.)", "", "", "key:voicemail_tape;+closeness#tape_found"));
            GameObject side = Prop(ctx, "AptSideTable", "sideTable", F, c + new Vector3(1.9f, 0f, 2.6f), 180f, 0.55f, false, null);
            GameObject machine = Prop(ctx, "AnsweringMachine", "radio", F, c + new Vector3(1.9f, Top(side), 2.6f), 180f, 0.3f, false, null);
            Inspectable(machine, "AnsweringMachine", L("..."),
                L("* The tape is gone. The red light has stopped blinking."), null, null, tape);
            PointLight(machine.transform, machine.transform.position + new Vector3(0f, 0.3f, -0.3f), new Color(1f, 0.2f, 0.3f), 0.8f, 1.5f);

            PointLight(ctx.env, c + new Vector3(-1f, 2.5f, 0f), new Color(0.8f, 0.85f, 1f), 1.4f, 7f);
            PointLight(ctx.env, c + new Vector3(2.8f, 2.2f, 1.5f), new Color(1f, 0.8f, 0.6f), 0.9f, 4f);
        }

        // ====================================================================== Leo's old room (act III)

        private static void BuildLeosRoom(Ctx ctx, Vector3 parkDoor, Material facade)
        {
            const string F = "furniture";
            Color evening = new Color(1f, 0.7f, 0.4f);
            Material doorMat = LitMaterial("Houses/M_Door_Home", new Color(0.55f, 0.3f, 0.22f));
            HouseDoor outside = BuildDoor(ctx, "LeoHome", parkDoor, 180f, doorMat, evening, facade, null);
            SetStrings(outside, "enterLines", L("* This door. {p}The scratch at knee height, where I kicked it every morning for twelve years.",
                "{slow}...That's my house.{/slow}"));
            Prop(ctx, "HomeHouse", "building-type-c", "suburban", parkDoor + new Vector3(0f, 0f, 3.2f), 180f, 5.5f, false, null, null, null, false);
            Prop(ctx, "HomeMailbox", "planter", "suburban", parkDoor + new Vector3(1.6f, 0f, -0.5f), 180f, 0.5f, false, null, null, null, false);

            Vector3 c = new Vector3(40f, 0f, -30f);
            Material floor = LitMaterial("Houses/M_Home_Floor", new Color(0.55f, 0.38f, 0.26f));
            Material wall = LitMaterial("Houses/M_Home_Wall", new Color(0.55f, 0.65f, 0.8f));
            Material windowMat = EmissiveMaterial("Houses/M_Home_Window", new Color(0.4f, 0.3f, 0.2f), evening * 2.5f);
            Material poster = EmissiveMaterial("Houses/M_Home_Poster", new Color(0.2f, 0.1f, 0.3f), new Color(0.5f, 0.3f, 1f) * 0.6f);
            Material pink = LitMaterial("Houses/M_PiggyBank", new Color(1f, 0.6f, 0.75f));
            Material wood = LitMaterial("Houses/M_Drumsticks", new Color(0.85f, 0.7f, 0.45f));
            float north = Room(ctx, "Home", c, 7f, 5.5f, floor, wall);
            HouseDoor inside = BuildDoor(ctx, "LeoHome_In", new Vector3(c.x - 2.4f, 0f, north - 0.12f), 180f, doorMat, Warm, null, "LEO'S ROOM");
            LinkDoors(outside, inside);

            GameObject window = Block("Prop_HomeWindow", ctx.props, new Vector3(c.x + 3.48f, 1.5f, c.z + 0.4f), new Vector3(0.04f, 1f, 1.4f), windowMat, ctx.interactableLayer);
            Inspectable(window, "HomeWindow",
                L("Outside the window it's always six p.m., in summer.", "The street smells like bread from the bakery downstairs. {p}{slow}I'd forgotten that smell.{/slow}"),
                L("* Somebody's riding a bike past. Bell, bell. {p}Gone."), "+openness#home_window", spkLeoInner);
            PointLight(window.transform, new Vector3(c.x + 2.6f, 1.6f, c.z + 0.4f), evening, 3f, 6f);

            Deco("Poster", ctx.env, new Vector3(c.x + 0.8f, 1.7f, north - 0.02f), new Vector3(0.8f, 1f, 0.02f), poster);

            // The bed: rest here (full HP).
            Prop(ctx, "HomeBed", "bedSingle", F, c + new Vector3(2.4f, 0f, 1.6f), 180f, 0.7f, false,
                L("Your old bed. The blanket with the rockets on it.", "* You lie down for a second. Just a second.", "* ...", "* You feel rested. <color=#FFD84F>(HP fully restored.)</color>"),
                L("* You lie down again. The ceiling still has glow-in-the-dark stars. {p}<color=#FFD84F>(HP fully restored.)</color>"), "heal:all");
            Prop(ctx, "MrBear", "bear", F, c + new Vector3(2.9f, 0.45f, 2.1f), 200f, 0.35f, false,
                L("Mr. Bear. One eye. {p}He's seen things."), L("Mr. Bear judges you gently."), null, false);

            // Mia's drawing.
            DialogueSequence drawing = SeqAsset("Houses/Home_Drawing",
                Say(null, "A drawing taped to the desk. A stick figure with an enormous head."),
                Say(null, "<color=#B9A7FF>LEO</color>, in purple crayon. And under it: <color=#B9A7FF>BECAUSE YOU THINK TOO MUCH.</color>"),
                OnlyIf("kira_joined", Say(spkKira, "Oh my god. {p}{bounce}The head.{/bounce} {p}She nailed it.")),
                Say(spkByte, "accurate. {p}{small}keep it. some things you dont throw away.{/small}"),
                Say(null, "(You got MIA'S DRAWING.)", "", "", "key:mia_drawing;+openness#mia_drawing"));
            GameObject desk = Prop(ctx, "HomeDesk", "desk", F, c + new Vector3(-0.6f, 0f, 2.3f), 180f, 0.7f, false, null);
            GameObject sheet = Block("Prop_MiaDrawing", ctx.props, new Vector3(c.x - 0.6f, Top(desk) + 0.01f, c.z + 2.2f), new Vector3(0.36f, 0.01f, 0.26f), LitMaterial("Subnet/M_Paperwork", new Color(0.96f, 0.95f, 0.9f)), ctx.interactableLayer);
            Inspectable(sheet, "MiaDrawing", L("..."), L("* The tape on the corners has gone yellow. {p}The crayon hasn't."), null, null, drawing);
            Prop(ctx, "HomeLamp", "lampRoundTable", F, c + new Vector3(-1.1f, Top(desk), 2.4f), 180f, 0.4f, false, null);

            // Dani's drumsticks (the band that was going to be called NIGHT TRAIN).
            GameObject shelf = Prop(ctx, "HomeShelf", "bookcaseOpenLow", F, c + new Vector3(-2.6f, 0f, 2.4f), 180f, 0.9f, false, null);
            var sticks = new GameObject("Prop_Drumsticks");
            sticks.transform.SetParent(ctx.props, false);
            sticks.transform.position = new Vector3(c.x - 2.6f, Top(shelf) + 0.03f, c.z + 2.35f);
            sticks.layer = ctx.interactableLayer;
            Deco("Stick1", sticks.transform, sticks.transform.position + new Vector3(-0.04f, 0f, 0f), new Vector3(0.03f, 0.03f, 0.42f), wood, new Vector3(0f, 80f, 0f));
            Deco("Stick2", sticks.transform, sticks.transform.position + new Vector3(0.04f, 0.03f, 0f), new Vector3(0.03f, 0.03f, 0.42f), wood, new Vector3(0f, 95f, 0f));
            var stickBox = sticks.AddComponent<BoxCollider>();
            stickBox.size = new Vector3(0.6f, 0.3f, 0.5f);
            Inspectable(sticks, "Drumsticks",
                L("Two drumsticks, held together with tape. {p}Dani's.",
                  "We were going to start a band. He wrote the name on the tape in silver marker: <color=#33FFF2>NIGHT TRAIN</color>.",
                  "* You take them. They still feel like a promise. {p}(You got DANI'S DRUMSTICKS.)"),
                L("* The shelf has a clean spot where they used to be."), "key:drumsticks#drumsticks", spkLeoInner);

            // Piggy bank: the first tokens of this house.
            GameObject piggy = Block("Prop_PiggyBank", ctx.props, c + new Vector3(-2.3f, 0.15f, -1.9f), new Vector3(0.32f, 0.26f, 0.22f), pink, ctx.interactableLayer);
            Inspectable(piggy, "PiggyBank",
                L("Your piggy bank. {p}Shake, shake.", "Metro tokens. You used to save them to visit Grandma.", "* (You take the tokens. Grandma would approve.)"),
                L("* Empty. It sounds lonely when you shake it."), "$5#piggy_bank");

            // A box marked FOR MIA.
            Prop(ctx, "ForMiaBox", "cardboardBoxClosed", F, c + new Vector3(-1.2f, 0f, -1.8f), 20f, 0.5f, false,
                L("A box with <color=#FF4FD8>FOR MIA</color> written on it in my handwriting.",
                  "Inside: my first payslip, folded into a tiny square. On the back: 'send Mia something nice'.",
                  "{slow}I never did.{/slow}", "* (You got the FIRST PAYSLIP.)"),
                L("* The box is empty now. It still says FOR MIA."), "key:first_payslip#payslip;+openness#payslip_open", true, spkLeoInner);

            // The old console: second place, always.
            GameObject tvStand = Prop(ctx, "HomeTVStand", "cabinetTelevision", F, c + new Vector3(0.9f, 0f, -2f), 0f, 0.55f, false, null);
            Prop(ctx, "HomeTV", "televisionVintage", F, c + new Vector3(0.9f, Top(tvStand), -2f), 0f, 0.6f, false,
                L("My old console. The save file is still there.", "* NIGHT RUNNER — LEO — 99,840. {p}Second place. {p}{slow}Always second place to her.{/slow}"),
                L("* The TV hums the way only old TVs hum."), null, true, spkLeoInner);
            Prop(ctx, "HomeRug", "rugRound", F, c + new Vector3(0.4f, 0.01f, 0f), 0f, 0.05f, false, null, null, null, false);

            PointLight(ctx.env, c + new Vector3(-0.5f, 2.4f, 0f), new Color(1f, 0.85f, 0.65f), 1.5f, 6f);
        }
    }
}
#endif
