#if UNITY_EDITOR
using Subnet.Audio;
using System.Collections.Generic;
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
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== 01_Tenement (Act I)

        private static void BuildTenementScene()
        {
            VolumeProfile profile = LookProfile("PP_Reality_Tenement", -30f, 0.6f, 0.38f, 0.2f, 0.08f, new Color(0.92f, 0.95f, 1f));
            Ctx ctx = BeginGameplayScene(new Vector3(0.9f, 0f, -0.9f), -120f, profile,
                new Color(0.12f, 0.14f, 0.2f), new Color(0.56f, 0.64f, 0.78f), 0.45f, true);

            AddSceneMusic(false);
            ctx.ambience.masterVolume = 0.4f;
            ctx.ambience.rain = 0.5f;
            ctx.ambience.hum = 0.25f;
            ctx.ambience.humFrequency = 52f;

            Material floorMat = LitMaterial("Reality/M_Floor_Boards", new Color(0.32f, 0.25f, 0.2f));
            Material wallMat = LitMaterial("Reality/M_Wall_Peeling", new Color(0.36f, 0.38f, 0.4f));
            Material doorMat = LitMaterial("Reality/M_Door", new Color(0.18f, 0.14f, 0.12f));
            Material windowMat = EmissiveMaterial("Reality/M_Window_Neon", new Color(0.12f, 0.14f, 0.25f), new Color(0.45f, 0.4f, 0.9f) * 1.2f);
            Material screenMat = EmissiveMaterial("Reality/M_Handheld_Screen", new Color(0.05f, 0.1f, 0.1f), Cyan * 3f);

            // Room shell (the camera looks toward +X/+Z, so only the far walls are visible).
            Block("Floor", ctx.env, new Vector3(0, -0.1f, 0), new Vector3(8, 0.2f, 6), floorMat, ctx.groundLayer);
            Block("Wall_North", ctx.env, new Vector3(0, 1.25f, 3.1f), new Vector3(8.4f, 2.5f, 0.2f), wallMat, 0);
            Block("Wall_East", ctx.env, new Vector3(4.1f, 1.25f, 0), new Vector3(0.2f, 2.5f, 6.4f), wallMat, 0);
            Block("Bound_South", ctx.env, new Vector3(0, 1f, -3.1f), new Vector3(8.4f, 2f, 0.2f), null, 0);
            Block("Bound_West", ctx.env, new Vector3(-4.1f, 1f, 0), new Vector3(0.2f, 2f, 6.4f), null, 0);

            const string F = "furniture";

            // Kitchen corner
            Prop(ctx, "Fridge", "kitchenFridgeSmall", F, new Vector3(-3.4f, 0, 2.55f), 180, 1.3f, false,
                L("Inside: one lemon, a jar of something, and the cold, clean light of good intentions."),
                L("The lemon hasn't moved. Neither have you."));
            GameObject cabinet = Prop(ctx, "Cabinet", "kitchenCabinet", F, new Vector3(-2.55f, 0, 2.6f), 180, 0.9f, false, null);
            Prop(ctx, "CoffeeMachine", "kitchenCoffeeMachine", F, new Vector3(-2.55f, Top(cabinet), 2.65f), 180, 0.35f, false,
                L("The coffee machine hums like it's the only one here with a schedule.", "* Below it, a cabinet with two plates and one fork. A set for a person who doesn't expect guests."),
                L("It's judging you. Politely. In espresso."));
            Prop(ctx, "Trashcan", "trashcan", F, new Vector3(-1.75f, 0, 2.6f), 0, 0.45f, false,
                L("Takeout boxes and a crumpled train ticket. Destination: home. Departure date: three weeks ago."),
                L("You never used that ticket."));

            GameObject window = Block("Prop_Window", ctx.props, new Vector3(-0.8f, 1.45f, 2.97f), new Vector3(1.4f, 0.9f, 0.06f), windowMat, ctx.interactableLayer);
            {
                Material frameMat = LitMaterial("Reality/M_WindowFrame", new Color(0.82f, 0.8f, 0.74f));
                Material curtainMat = LitMaterial("Reality/M_Curtain", new Color(0.35f, 0.3f, 0.45f));
                Vector3 w = new Vector3(-0.8f, 1.45f, 2.93f);
                Deco("WinFrameTop", ctx.props, w + new Vector3(0f, 0.49f, 0f), new Vector3(1.56f, 0.08f, 0.1f), frameMat);
                Deco("WinFrameBottom", ctx.props, w + new Vector3(0f, -0.49f, 0f), new Vector3(1.56f, 0.08f, 0.1f), frameMat);
                Deco("WinFrameL", ctx.props, w + new Vector3(-0.74f, 0f, 0f), new Vector3(0.08f, 1.06f, 0.1f), frameMat);
                Deco("WinFrameR", ctx.props, w + new Vector3(0.74f, 0f, 0f), new Vector3(0.08f, 1.06f, 0.1f), frameMat);
                Deco("WinBarV", ctx.props, w + new Vector3(0f, 0f, -0.01f), new Vector3(0.05f, 0.92f, 0.06f), frameMat);
                Deco("WinBarH", ctx.props, w + new Vector3(0f, 0.05f, -0.01f), new Vector3(1.42f, 0.05f, 0.06f), frameMat);
                Deco("WinSill", ctx.props, w + new Vector3(0f, -0.56f, -0.09f), new Vector3(1.7f, 0.06f, 0.24f), frameMat);
                Deco("CurtainL", ctx.props, w + new Vector3(-0.95f, 0.05f, -0.08f), new Vector3(0.32f, 1.3f, 0.05f), curtainMat);
                Deco("CurtainR", ctx.props, w + new Vector3(0.95f, 0.05f, -0.08f), new Vector3(0.32f, 1.3f, 0.05f), curtainMat);
                Deco("CurtainRod", ctx.props, w + new Vector3(0f, 0.74f, -0.1f), new Vector3(2.3f, 0.03f, 0.03f), frameMat);
            }
            Inspectable(window, "Window",
                L("Central Station glows below, wet and neon. Trains come and go like thoughts you can't hold onto."),
                L("One of the trains has no number. You blink. It's gone."), "saw_ghost_train");
            PointLight(window.transform, window.transform.position + new Vector3(0, 0, -0.6f), new Color(0.85f, 0.35f, 1f), 3.5f, 5f);

            // Desk corner
            GameObject desk = Prop(ctx, "Desk", "desk", F, new Vector3(0.6f, 0, 2.55f), 180, 0.75f, false, null);
            Prop(ctx, "Laptop", "laptop", F, new Vector3(0.6f, Top(desk), 2.5f), 180, 0.42f, true,
                L("Your laptop. Seventeen unread emails. The subject lines all sound like sighs.", "* The desk under it wobbles when you think too hard. You assembled it with the wrong screws."),
                L("Closing the lid doesn't make them go away. But it helps."));
            Prop(ctx, "TokenJar", "glass", "food", new Vector3(1.05f, Top(desk), 2.5f), 0, 0.18f, false,
                L("A jar of old metro tokens. Mia and I used to collect them. {p}The city stopped taking them years ago.",
                  "* You pocket a handful. No reason. {p}(It feels like a reason.)"),
                L("A few tokens left at the bottom. {p}Enough for one more trip. Somewhere."), "$6#token_jar");
            Prop(ctx, "Plant", "pottedPlant", F, new Vector3(1.6f, 0, 2.6f), 0, 0.8f, false,
                L("A plant from the corner shop. The tag says 'impossible to kill.' It's taking that as a challenge."),
                L("You water it. It doesn't thank you. Fair."));
            GameObject lamp = Prop(ctx, "Lamp", "lampRoundFloor", F, new Vector3(2.4f, 0, 2.6f), 0, 1.5f, false,
                L("A floor lamp with a tired, yellow light. It's trying its best. You know the feeling."),
                L("Hang in there, buddy."));
            PointLight(lamp.transform, lamp.transform.position + new Vector3(0, 1.45f, 0), new Color(1f, 0.72f, 0.42f), 2.5f, 4.5f);

            // East wall
            Prop(ctx, "Bed", "bedSingle", F, new Vector3(3.2f, 0, 1.3f), 0, 1.9f, true,
                L("An unmade bed. The sheets still have the shape of a night you didn't sleep through."),
                L("Not yet. If you lie down now, the ceiling starts talking."));
            Prop(ctx, "TV", "televisionVintage", F, new Vector3(3.5f, 0, -0.5f), -90, 0.75f, false,
                L("An old TV the last tenant left behind. The static sounds like a crowd whispering your name, but slightly wrong."),
                L("Lo. Leeo. Leeeo. ...It's just static. Probably."));
            Prop(ctx, "Mirror", "bathroomMirror", F, new Vector3(3.93f, 1.05f, -1.45f), -90, 0.6f, false,
                L("A cracked mirror. The crack runs right through your reflection, like it's trying to split you into a before and an after.", "You look away first."),
                L("Still cracked. Still you. Both of those feel like problems."), "inspected_mirror", false);
            {
                // Bible: looked at three times, the crack moves.
                var mirror = GameObject.Find("Prop_Mirror").GetComponent<InspectableObject>();
                Set(mirror, "specialInspectionAt", 3);
                SetStrings(mirror, "specialInspection", L(
                    "You look again. {p}The crack has moved. {p}{slow}It runs a little to the left of you now.{/slow}",
                    "* Like it's making room."));
            }

            // Front door → Central Station (only after checking the handheld).
            GameObject door = Block("Prop_FrontDoor", ctx.props, new Vector3(3.97f, 0.9f, -2.3f), new Vector3(0.08f, 1.8f, 0.9f), doorMat, ctx.interactableLayer);
            door.tag = "Inspectable";
            Exit(door, StationScene, "ch2", "handheld_checked",
                L("* The front door. A doormat says WELCOME. It came with the apartment. It doesn't mean me.",
                  "* ...My handheld keeps buzzing over by the boxes. I should check it before I go anywhere."),
                L("* The stairwell is dark. The building hums like a sleeping animal.",
                  "* Central Station is five minutes away. The last train leaves at midnight."),
                null, false, null, ctx.sfx, Clip("open_002"));

            Prop(ctx, "Doormat", "rugDoormat", F, new Vector3(3.3f, 0, -2.3f), 90, 0.9f, true, null, null, null, false);

            // Moving boxes
            Prop(ctx, "OpenBox", "cardboardBoxOpen", F, new Vector3(-1.3f, 0, -0.3f), 20, 0.5f, false,
                L("A half-unpacked box. Everything inside is wrapped in newspaper from a city you don't live in anymore."),
                L("You'll unpack it tomorrow. You said that yesterday."));
            GameObject boxMisc = Prop(ctx, "MiscBox", "cardboardBoxClosed", F, new Vector3(-2.2f, 0, 0.1f), -10, 0.5f, false, null);
            Prop(ctx, "Radio", "radio", F, new Vector3(-2.2f, Top(boxMisc), 0.1f), 30, 0.25f, true,
                L("A radio stuck between stations. A weather report, a love song, and a sound like breathing, all at once.", "* It sits on a box labeled 'MISC'. That's what you write when you don't want to remember."),
                L("The breathing is still there."));
            GameObject boxHandheld = Prop(ctx, "Box14", "cardboardBoxClosed", F, new Vector3(-0.6f, 0, -1.3f), 5, 0.5f, false, null);
            Prop(ctx, "Books", "books", F, new Vector3(1.9f, 0, -1.7f), 40, 0.35f, true,
                L("Books you meant to read in your old life. They made the move. You're not sure you did."),
                L("Chapter one, again."));
            Prop(ctx, "Rug", "rugRectangle", F, new Vector3(0.7f, 0, 0.3f), 0, 2.6f, true, null, null, null, false);

            Vector3 handheldPos = new Vector3(-0.6f, Top(boxHandheld) + 0.025f, -1.3f);
            GameObject handheld = Block("Prop_Handheld", ctx.props, handheldPos, new Vector3(0.26f, 0.05f, 0.16f), screenMat, ctx.interactableLayer);
            handheld.transform.rotation = Quaternion.Euler(0, 25, 0);
            // A generous trigger around the box so the handheld is easy to target from any side.
            var handheldReach = new GameObject("Reach");
            handheldReach.layer = ctx.interactableLayer;
            handheldReach.transform.SetParent(handheld.transform, false);
            var reach = handheldReach.AddComponent<BoxCollider>();
            reach.isTrigger = true;
            reach.size = new Vector3(4.2f, 14f, 6.5f); // local units (parent is 0.26 x 0.05 x 0.16) ≈ 1.1 x 0.7 x 1.0 m
            reach.center = new Vector3(0, -5f, 0);
            // Bible, prologue: the phone as a character. Two old voicemails from Mia, plus the anonymous train message.
            DialogueSequence handheldSeq = SeqAsset("Act1/Handheld_First",
                Say(null, "Your handheld. You modded it yourself, back when you still had weekends."),
                Say(null, "It's lying on box fourteen. The last box. The lightest, and somehow the hardest to open."),
                Ask(null, "Three unread. {p}Two voicemails from Mia, my little sister, from months ago. {p}And one new message. No sender.",
                    Opt("[Listen to Mia]", "vm", "+openness;heard_mia;heard_mia_0;heard_mia_1"),
                    Opt("[Not now]", "msg")),
                Say(null, "VOICEMAIL  //  MIA  //  MARCH 3", "vm"),
                Say(spkMia, "Leo? It's me. Mom says you got the job. {p}That's... {p}that's huge."),
                Say(spkMia, "Call me back, okay? {p}I want to hear you say it. {wave}With your stupid proud voice.{/wave}"),
                Say(null, "VOICEMAIL  //  MIA  //  MAY 28"),
                Say(spkMia, "Hey. It's me again. {p}The recital's on the twelfth."),
                Say(spkMia, "You don't have to come. {p=0.8}{slow}I'm just saying it's on the twelfth.{/slow}"),
                Say(spkMia, "{small}...okay. bye.{/small}", "", "", "silence_request"),
                Say(null, "One new message. No sender. Just: <color=#33FFF2>'last train leaves at 00:00. platform zero. don't miss it.'</color>", "msg"),
                Say(null, "There's no platform zero at Central Station. {p}{small}...is there?{/small}"),
                Say(null, "(The front door. It's time.)"));
            Inspectable(handheld, "Handheld", L("..."),
                L("The message is still there. 23:51. The door is that way. {p}It's not getting any closer on its own.",
                  "* (Tab opens the handheld: messages and voicemails.)"),
                "handheld_checked", null, handheldSeq);
            PointLight(handheld.transform, handheldPos + new Vector3(0, 0.25f, 0), Cyan, 1.2f, 1.6f);

            // Guidance after the intro.
            Event(ctx, "WakeUp", Subnet.Gameplay.StoryEvent.TriggerMode.OnStart, "!handheld_checked", 1.2f, null, null,
                L("* My handheld won't stop buzzing. It's on the last box."), null, string.Empty);
            Event(ctx, "Checked", Subnet.Gameplay.StoryEvent.TriggerMode.OnFlag, "handheld_checked", 0.6f, "UNKNOWN",
                L("platform zero. 00:00. bring the handheld."), null, null, string.Empty);

            // Intro cinematic: "The Moving Boxes"
            Transform points = new GameObject("CinematicPoints").transform;
            Transform cp1 = CinematicPoint("CP_01_Ceiling", points, new Vector3(0.5f, 8.5f, -6.5f), new Vector3(0, 0, 0.6f));
            Transform cp2 = CinematicPoint("CP_02_Boxes", points, new Vector3(-3.3f, 1.1f, -2.7f), new Vector3(-1.4f, 0.35f, -0.3f));
            Transform cp3 = CinematicPoint("CP_03_Handheld", points, handheldPos + new Vector3(-0.75f, 0.55f, -0.75f), handheldPos);

            CinematicSequence intro = Cinematic("Cinematic_Intro_MovingBoxes", ctx.story,
                Shot("Ceiling", cp1, 0f, 2.5f, 30f, null, null, true, Clip("bong_001")),
                Shot("Boxes", cp2, 6f, 0.4f, 24f, L("Fourteen boxes. I packed my whole life into fourteen boxes.", "Somehow it still feels like something's missing.")),
                Shot("Handheld", cp3, 4f, 0.8f, 18f, L("The only thing still turned on in this apartment is my handheld.", "...and me. Barely.")));
            intro.fadeFromBlack = true;
            intro.blackHoldBeforeFade = 1.5f;
            intro.fadeInDuration = 3.5f;
            intro.blendToGameplay = true;
            intro.blendToGameplayDuration = 2.5f;
            EditorUtility.SetDirty(intro);
            SetIntro(ctx, intro);

            EndGameplayScene(ctx, TenementScene);
        }

        // ====================================================================== 02_Station (Act I)

        private static void BuildStationScene()
        {
            VolumeProfile profile = LookProfile("PP_Reality_Station", -40f, 0.7f, 0.4f, 0.25f, 0.1f, new Color(0.9f, 0.95f, 1f));
            Ctx ctx = BeginGameplayScene(new Vector3(-10.5f, 0f, -0.5f), 90f, profile,
                new Color(0.1f, 0.12f, 0.16f), new Color(0.5f, 0.58f, 0.72f), 0.35f, false);

            AddSceneMusic(true, StemMix.Station);
            ctx.removeAmbience = true;
            ctx.ambience.masterVolume = 0.5f;
            ctx.ambience.rain = 0.1f;
            ctx.ambience.hum = 0.6f;
            ctx.ambience.humFrequency = 38f;
            ctx.ambience.wind = 0.25f;

            Material platformMat = LitMaterial("Reality/M_Platform", new Color(0.33f, 0.34f, 0.36f));
            Material edgeMat = LitMaterial("Reality/M_Platform_Edge", new Color(0.85f, 0.75f, 0.2f));
            Material trackBedMat = LitMaterial("Reality/M_TrackBed", new Color(0.12f, 0.11f, 0.11f));
            Material tileMat = LitMaterial("Reality/M_Station_Tiles", new Color(0.55f, 0.58f, 0.6f));
            Material pillarMat = LitMaterial("Reality/M_Pillar", new Color(0.4f, 0.42f, 0.45f));
            Material boardMat = LitMaterial("Reality/M_Timetable", new Color(0.04f, 0.04f, 0.05f));
            Material signalMat = EmissiveMaterial("Reality/M_RedSignal", new Color(0.3f, 0f, 0f), new Color(1f, 0.1f, 0.1f) * 3f);
            Material zeroMat = EmissiveMaterial("Reality/M_ZeroSign", new Color(0.1f, 0.05f, 0.15f), Magenta * 2.5f);
            Material grayMat = LitMaterial("Reality/M_Calcified", new Color(0.55f, 0.55f, 0.55f));

            // Platform: x -12..12, z -2.2..3; tracks at z 3..6.5 (lower); far wall at z 7.
            Block("Platform", ctx.env, new Vector3(0, -0.1f, 0.4f), new Vector3(25, 0.2f, 5.2f), platformMat, ctx.groundLayer);
            Deco("PlatformEdge", ctx.env, new Vector3(0, 0.005f, 2.85f), new Vector3(25, 0.02f, 0.3f), edgeMat);
            Block("TrackBed", ctx.env, new Vector3(0, -0.9f, 4.8f), new Vector3(25, 0.2f, 4f), trackBedMat, 0);
            Block("FarWall", ctx.env, new Vector3(0, 1.2f, 7f), new Vector3(25, 4.5f, 0.3f), tileMat, 0);
            Block("Wall_East", ctx.env, new Vector3(12.6f, 1.2f, 2.5f), new Vector3(0.3f, 4.5f, 9.5f), tileMat, 0);
            Block("Bound_Edge", ctx.env, new Vector3(0, 1f, 3.05f), new Vector3(25, 2f, 0.15f), null, 0);
            Block("Bound_South", ctx.env, new Vector3(0, 1f, -2.3f), new Vector3(25, 2f, 0.2f), null, 0);
            Block("Bound_West", ctx.env, new Vector3(-12.6f, 1f, 0.4f), new Vector3(0.2f, 2f, 5.4f), null, 0);

            // Rails
            GameObject rail = Model("railroad-straight", "train-kit");
            if (rail != null)
            {
                for (float x = -12f; x <= 12f; x += 2f)
                {
                    var holder = new GameObject("Rail");
                    holder.transform.SetParent(ctx.env, false);
                    holder.transform.position = new Vector3(x, -0.8f, 4.8f);
                    SpawnModel(rail, holder.transform, 2f, true, 90f);
                }
            }

            for (int i = -2; i <= 2; i++)
            {
                Block("Pillar_" + i, ctx.env, new Vector3(i * 4.5f, 1.8f, 2.3f), new Vector3(0.5f, 3.6f, 0.5f), pillarMat, 0);
            }

            // Timetable (the key clue)
            GameObject board = Block("Prop_Timetable", ctx.props, new Vector3(-6.5f, 1.6f, 2.0f), new Vector3(1.6f, 0.9f, 0.1f), boardMat, ctx.interactableLayer);
            Inspectable(board, "Timetable",
                L("DEPARTURES. Line 3: cancelled. Line 7: delayed. Line 9: delayed indefinitely, like everything else.",
                  "At the bottom, a line flickers that wasn't there a second ago: <color=#FF4FD8>NIGHT TRAIN ZERO - PLATFORM 0 - 00:00 - ON TIME</color>.",
                  "* On time. That makes one of us."),
                L("NIGHT TRAIN ZERO. On time. The only thing in this city that is."), "read_timetable");
            PointLight(board.transform, board.transform.position + new Vector3(0, 0, -0.8f), new Color(1f, 0.6f, 0.2f), 1.0f, 3f);
            Deco("BoardLegL", ctx.props, new Vector3(-7.15f, 0.58f, 2.02f), new Vector3(0.07f, 1.16f, 0.07f), pillarMat);
            Deco("BoardLegR", ctx.props, new Vector3(-5.85f, 0.58f, 2.02f), new Vector3(0.07f, 1.16f, 0.07f), pillarMat);
            Deco("BoardFrame", ctx.props, new Vector3(-6.5f, 1.6f, 2.03f), new Vector3(1.72f, 1.02f, 0.06f), pillarMat);
            string[] boardLines = { "DEPARTURES", "LINE 3      CANCELLED", "LINE 7      DELAYED", "LINE 9      DELAYED" };
            for (int i = 0; i < boardLines.Length; i++)
            {
                TextMeshPro line = WorldSign(board.transform, boardLines[i], new Vector3(-6.5f, 1.93f - i * 0.17f, 1.94f), 180f,
                    i == 0 ? Color.white : new Color(1f, 0.7f, 0.25f), i == 0 ? 1.3f : 1.05f);
                line.rectTransform.sizeDelta = new Vector2(1.45f, 0.2f);
                line.alignment = i == 0 ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
                line.fontStyle = i == 0 ? FontStyles.Bold : FontStyles.Normal;
            }
            TextMeshPro zeroLine = WorldSign(board.transform, "NIGHT TRAIN ZERO   PLATF. 0   00:00", new Vector3(-6.5f, 1.25f, 1.94f), 180f, new Color(1f, 0.45f, 0.85f), 0.95f);
            zeroLine.rectTransform.sizeDelta = new Vector2(1.45f, 0.2f);
            zeroLine.fontStyle = FontStyles.Bold;

            GameObject zeroSign = Block("Prop_PlatformZero", ctx.props, new Vector3(4.5f, 2.6f, 2.0f), new Vector3(1.2f, 0.5f, 0.08f), zeroMat, ctx.interactableLayer);
            Inspectable(zeroSign, "PlatformZeroSign",
                L("A sign that reads PLATFORM 0. The paint is still wet. It smells like a promise someone forgot to keep."),
                L("Platform zero. Between one and nothing."));
            PointLight(zeroSign.transform, zeroSign.transform.position + new Vector3(0, -0.5f, -0.6f), Magenta, 2f, 4f);

            GameObject signal = Block("Prop_RedSignal", ctx.props, new Vector3(10.5f, 1.4f, 2.6f), new Vector3(0.25f, 0.25f, 0.25f), signalMat, ctx.interactableLayer);
            Inspectable(signal, "RedSignal",
                L("A red signal light. It whispers: 'What if you're going the wrong way? What if there is no right way?'",
                  "* ...It's a light. Lights don't whisper.",
                  "It whispers: 'Says you.'"),
                L("It's still red. It's always red. That's kind of its whole thing."), null, spkObject);
            PointLight(signal.transform, signal.transform.position + new Vector3(0, 0, -0.4f), Color.red, 1.2f, 2.5f);

            const string F = "furniture";
            Prop(ctx, "Bench1", "bench", F, new Vector3(-3f, 0, 1.6f), 0, 1.6f, true,
                L("A station bench. Designed with armrests in the middle so nobody can lie down. Even the furniture is tired of you."),
                L("You sit for a second. The bench sighs. Or maybe you do."));
            Prop(ctx, "Bench2", "bench", F, new Vector3(7.5f, 0, 1.6f), 0, 1.6f, true,
                L("Someone left a coffee cup here. It's still warm. It smells like overdue deadlines and mornings you wish never arrived."),
                L("The cup is cold now. Time moves, even here."));
            Prop(ctx, "Trash", "trashcan", F, new Vector3(1.5f, 0, 2.2f), 0, 0.6f, false,
                L("A trash can full of unread free newspapers. Every headline says EVERYTHING IS FINE in a slightly bigger font."),
                L("EVERYTHING IS FINE. EVERYTHING IS FINE."));
            GameObject lampA = Prop(ctx, "StationLamp", "lampSquareFloor", F, new Vector3(-8.5f, 0, 2.2f), 0, 2.4f, false,
                L("A silent lamp post. It says it's tired of telling everyone where to go."),
                L("It still points nowhere in particular. Respect."), null, true, spkObject);
            PointLight(lampA.transform, lampA.transform.position + Vector3.up * 2.3f, new Color(0.8f, 0.9f, 1f), 2f, 6f);
            Prop(ctx, "Stairs", "stairs", F, new Vector3(-12.0f, 0, -1.5f), 90, 1.2f, false,
                L("The stairs back up to the street. Home is up there. The boxes are waiting.", "* They can wait."),
                L("* Not going back. Not tonight."));

            // Calcifying commuters
            Material paperMat = LitMaterial("Reality/M_Paper", new Color(0.95f, 0.94f, 0.9f));
            GameObject c1 = Character(ctx, "Commuter_Phone", "character-male-d", "mini-characters", new Vector3(-1.5f, 0, 0.6f), 160, 1.0f, grayMat);
            Inspectable(c1, "Commuter_Phone",
                L("Can't talk. On a call. Have been on a call since Tuesday.",
                  "* His skin is slowly turning to paper. Little sticky notes flutter off his shoulders."),
                L("Yes. Yes. Let's circle back. Let's circle back. Let's circle..."), null, spkCommuter);
            for (int i = 0; i < 6; i++)
            {
                Deco("StickyNote", c1.transform, c1.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1.1f), Random.Range(-0.3f, 0.3f)),
                    new Vector3(0.12f, 0.12f, 0.01f), paperMat, new Vector3(Random.Range(0, 360), Random.Range(0, 360), 0));
            }
            GameObject c2 = Character(ctx, "Commuter_Clock", "character-female-d", "mini-characters", new Vector3(5.8f, 0, -0.6f), 200, 1.0f, grayMat);
            Inspectable(c2, "Commuter_Clock",
                L("Is it late? It feels late. It's always felt late.",
                  "* A wristwatch on each arm. Neither of them shows the same time.",
                  "I'll rest when everything's done. Everything is never done. So."),
                L("Tick. Tick. That's me. I'm the tick."), null, spkCommuter);
            GameObject c3 = Character(ctx, "Commuter_Sleeping", "character-male-e", "mini-characters", new Vector3(9.5f, 0, 0.8f), 250, 1.0f, grayMat);
            Inspectable(c3, "Commuter_Sleeping",
                L("Zzz... 'per my last email'... zzz...",
                  "* He's asleep standing up. A thin layer of dust has settled on his briefcase, like he's been here for years."),
                L("Zzz... 'hope this finds you well'... zzz..."), null, spkCommuter);

            // Night Train Zero (arrives once the timetable has been read)
            var trainRoot = new GameObject("NightTrainZero");
            trainRoot.transform.position = new Vector3(0, -0.8f, 4.8f);
            BuildTrain(trainRoot.transform, zeroMat);

            var arrival = new GameObject("TrainArrival").AddComponent<TrainArrival>();
            arrival.transform.SetParent(ctx.story, false);
            Set(arrival, "train", trainRoot.transform);
            Set(arrival, "triggerFlag", "read_timetable");
            Set(arrival, "arrivedFlag", "train_arrived");
            Set(arrival, "audioSource", ctx.sfx);
            Set(arrival, "arrivalClip", Clip("maximize_005"));
            Set(arrival, "doorsClip", Clip("open_004"));

            // Boarding: the train doors.
            GameObject doors = Block("TrainDoors", ctx.story, new Vector3(1.5f, 0.9f, 2.75f), new Vector3(2.2f, 1.8f, 0.3f), null, ctx.interactableLayer);
            doors.GetComponent<Collider>().isTrigger = true;
            Transform points = new GameObject("CinematicPoints").transform;
            Transform cpA = CinematicPoint("CP_Board_Side", points, new Vector3(-2.5f, 1.4f, -1.5f), new Vector3(1.5f, 0.8f, 3.2f));
            Transform cpB = CinematicPoint("CP_Board_Close", points, new Vector3(1.5f, 1.1f, 0.6f), new Vector3(1.5f, 0.9f, 4f));
            CinematicSequence boarding = Cinematic("Cinematic_Boarding_NightTrainZero", ctx.story,
                Shot("Doors", cpA, 2.5f, 0.3f, 24f, L("The doors open with a sharp hiss. Nobody gets off. Nobody else gets on.",
                    "The carriage inside is empty, and much brighter than it should be."), null, false, Clip("open_004")),
                Shot("Warp", cpB, 3f, 0.6f, 16f, L("* As the doors close, the colors outside drain away... and come back wrong.",
                    "* Pastel. Neon. Like a city drawn from memory by someone who missed it."), null, false, Clip("glitch_003"), 0.5f));
            boarding.fadeFromBlack = false;
            boarding.blendToGameplay = false;
            boarding.fadeToBlackAtEnd = true;
            boarding.fadeOutDuration = 1.5f;
            EditorUtility.SetDirty(boarding);
            // Bible, act I: the Conductor punches the ticket home Leo never used.
            GameObject conductor = Character(ctx, "Conductor", "character-male-c", "mini-characters", new Vector3(3.6f, 0, 1.9f), 200, 1.05f);
            DialogueSequence conductorSeq = SeqAsset("Act1/Conductor",
                Say(spkConductor, "{small}Ticket, please.{/small}"),
                Say(null, "My pocket. The ticket home. {p}Three weeks old. Never used."),
                Ask(spkConductor, "Ticket.",
                    Opt("[Show him the ticket home]", "punch", "+openness"),
                    Opt("[\"I don't have one.\"]", "lie")),
                Say(null, "He looks at my pocket like he can see straight through it.", "lie"),
                Say(null, "{quake}Click. {p}He punches it anyway.", "punch"),
                Say(spkConductor, "This one's still valid. {p=0.9}{slow}They usually are.{/slow}", "", "", "ticket_punched"),
                Say(spkConductor, "Mind the gap. {p}{small}All of them.{/small}"));
            InspectableObject conductorTalk = Inspectable(conductor, "Conductor", L("..."),
                L("This one's still valid. {p}Don't lose it.", "* He tips his cap. It doesn't have a train company's logo. It has a tiny zero."), null, spkConductor, conductorSeq);
            Set(conductorTalk, "requiredFlag", "train_arrived");

            SceneExit trainDoors = Exit(doors, SubnetScene, "ch3", "ticket_punched",
                L("* The edge of the platform. The tracks hum below.", "* Something about tonight feels scheduled. {p}And someone in a cap is watching my pocket."),
                null, boarding, false, null, ctx.sfx, Clip("confirmation_003"));
            Set(trainDoors, "interactableAfterFlag", "train_arrived"); // no "train door" prompt in front of an empty track

            Event(ctx, "Arrive", Subnet.Gameplay.StoryEvent.TriggerMode.OnStart, string.Empty, 1.5f, "UNKNOWN",
                L("ur here. good. check the timetable."), L("* Central Station at 23:57. Empty, except for the people who look like they've forgotten how to leave."), null, string.Empty);
            Event(ctx, "TrainComing", Subnet.Gameplay.StoryEvent.TriggerMode.OnFlag, "train_arrived", 0.5f, "UNKNOWN",
                L("get on. dont think about it too much. thats the trick."), L("* A train with no number. Its doors are open. It's waiting for me.", "* A conductor stands by the doors. {p}He's looking at my pocket."), null, string.Empty);

            BuildStationReturn(ctx);
            EndGameplayScene(ctx, StationScene);
        }

        /// <summary>Assembles a 3-car subway train from the Kenney train kit along +X.</summary>
        private static void BuildTrain(Transform root, Material accent)
        {
            string[] parts = { "train-electric-subway-a", "train-electric-subway-b", "train-electric-subway-c" };
            float cursor = -8f;
            foreach (string part in parts)
            {
                GameObject asset = Model(part, "train-kit");
                var holder = new GameObject(part);
                holder.transform.SetParent(root, false);
                holder.transform.position = root.position;

                if (asset == null)
                {
                    Deco("CarPlaceholder", holder.transform, root.position + new Vector3(cursor + 2.5f, 1.1f, 0), new Vector3(5f, 2.2f, 2.2f), accent);
                    cursor += 5.2f;
                    continue;
                }

                GameObject probe = SpawnModel(asset, holder.transform, 2.3f, false, 0f);
                Bounds b = WorldBounds(probe);
                if (b.size.z > b.size.x)
                {
                    Object.DestroyImmediate(probe);
                    probe = SpawnModel(asset, holder.transform, 2.3f, false, 90f);
                    b = WorldBounds(probe);
                }
                probe.transform.position += new Vector3(cursor - b.min.x, 0, 0);
                cursor += b.size.x + 0.15f;
            }

            // Neon underglow: this is no ordinary train.
            Deco("Underglow", root, root.position + new Vector3(cursor * 0.5f - 4f, 0.05f, 0), new Vector3(cursor + 8f, 0.05f, 2.4f), accent);
        }
    }
}
#endif
