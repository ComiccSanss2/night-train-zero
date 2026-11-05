#if UNITY_EDITOR
using Subnet.Audio;
using Subnet.Cinematics;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Hacking;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace Subnet.EditorTools
{
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== 05_CentralCore (Act IV)

        private static void BuildCoreScene()
        {
            VolumeProfile profile = LookProfile("PP_Subnet_CentralCore", 5f, 1.8f, 0.5f, 0.2f, 0.35f, new Color(0.95f, 0.88f, 1f));
            Ctx ctx = BeginGameplayScene(new Vector3(-12f, 0f, 0f), 90f, profile,
                new Color(0.08f, 0.06f, 0.14f), new Color(0.6f, 0.4f, 0.9f), 0.3f, false);

            AddSceneMusic(true, StemMix.Core);
            ctx.removeAmbience = true;
            ctx.ambience.masterVolume = 0.5f;
            ctx.ambience.hum = 0.55f;
            ctx.ambience.humFrequency = 33f;
            ctx.ambience.crackle = 0.6f;
            ctx.ambience.pad = 0.25f;
            ctx.ambience.padNotes = new[] { 110f, 116.54f, 164.81f };
            ctx.ambience.wind = 0.15f;
            ctx.ambience.rain = 0f;

            Material floor = LitMaterial("Subnet/M_Core_Floor", new Color(0.05f, 0.04f, 0.08f), 0.7f);
            Material grid = EmissiveMaterial("Subnet/M_Core_Grid", new Color(0.05f, 0.1f, 0.12f), Cyan * 0.9f);
            Material monolith = EmissiveMaterial("Subnet/M_Monolith", new Color(0.06f, 0.05f, 0.1f), new Color(0.35f, 0.1f, 0.5f) * 0.4f);
            Material algoCore = EmissiveMaterial("Subnet/M_Algorithm_Core", new Color(0.3f, 0.05f, 0.25f), Magenta * 4f);
            Material algoRing = EmissiveMaterial("Subnet/M_Algorithm_Ring", new Color(0.05f, 0.25f, 0.25f), Cyan * 3f);
            Material memoryMat = EmissiveMaterial("Subnet/M_MemoryFile", new Color(0.6f, 0.45f, 0.3f), new Color(0.4f, 0.9f, 1f) * 1.5f);
            Material staticParticles = ParticleMaterial("Subnet/M_StaticPixels", new Color(0.4f, 1f, 1f, 1f));
            Material notifMat = EmissiveMaterial("Subnet/M_Notifications", new Color(0.1f, 0.05f, 0.1f), new Color(1f, 0.3f, 0.4f) * 2f);

            Block("CoreFloor", ctx.env, new Vector3(2f, -0.1f, 0f), new Vector3(32f, 0.2f, 12.4f), floor, ctx.groundLayer);
            for (float x = -14f; x <= 18f; x += 2f) Deco("GridX", ctx.env, new Vector3(x, 0.005f, 0f), new Vector3(0.04f, 0.01f, 12.4f), grid);
            for (float z = -6f; z <= 6f; z += 2f) Deco("GridZ", ctx.env, new Vector3(2f, 0.006f, z), new Vector3(32f, 0.01f, 0.04f), grid);
            Block("Bound_North", ctx.env, new Vector3(2f, 1f, 6.3f), new Vector3(32f, 2f, 0.2f), null, 0);
            Block("Bound_South", ctx.env, new Vector3(2f, 1f, -6.3f), new Vector3(32f, 2f, 0.2f), null, 0);
            Block("Bound_West", ctx.env, new Vector3(-14.2f, 1f, 0f), new Vector3(0.2f, 2f, 12.4f), null, 0);
            Block("Bound_East", ctx.env, new Vector3(18.2f, 1f, 0f), new Vector3(0.2f, 2f, 12.4f), null, 0);

            // Monoliths of unread notifications (kept well behind the play area).
            string[] towers = { "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d", "building-skyscraper-e" };
            BuildingRow(ctx.env, towers, "commercial", -15f, 19f, 8f, 9f, 3f, 2.5f, 6f, 0f, 7);
            foreach (Transform t in ctx.env)
            {
                if (t.name.StartsWith("Building_")) TintModel(t.gameObject, monolith);
            }

            GameObject notif = Block("Prop_NotificationPillar", ctx.props, new Vector3(-6f, 1.1f, 3.5f), new Vector3(0.6f, 2.2f, 0.6f), notifMat, ctx.interactableLayer);
            Inspectable(notif, "NotificationPillar",
                L("A pillar of unread notifications. It grows a little every time someone types 'just checking in!'",
                  "* 9,999+. The counter stopped counting a long time ago."),
                L("It pings. You don't look. Growth."));
            GameObject cursorObj = Block("Prop_FloatingCursor", ctx.props, new Vector3(0f, 1.6f, -3.5f), new Vector3(0.3f, 0.5f, 0.05f), grid, ctx.interactableLayer);
            cursorObj.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 60, 0);
            Inspectable(cursorObj, "FloatingCursor",
                L("A giant floating loading cursor. It has been loading since 2019.", "* Relatable."),
                L("Still loading. Respect the commitment."));

            // Companions
            GameObject byteNpc = Character(ctx, "Byte", "animal-cat", "cube-pets", new Vector3(-13f, 0f, -1f), 90f, 0.45f, null, false);
            Inspectable(byteNpc, "Byte_Core", L("Don't look directly at it. It's like a phone screen at 4 a.m.", "...I'm right here. You know that, right?"),
                L("ur doing fine. dont make it weird."), null, spkByte);
            Follower(byteNpc, string.Empty, -0.6f);
            // Act IV: Byte stayed behind on the Ghost Line. He's only in Leo's head now (and on his collar).
            Set(byteNpc.AddComponent<HideIfFlag>(), "flag", "ghostline_done");
            GameObject kiraNpc = Character(ctx, "Kira", "character-female-c", "mini-characters", new Vector3(-13f, 0f, 1f), 90f, 1.05f, null, false);
            Inspectable(kiraNpc, "Kira_Core", L("I'm not delivering anything today. Weird. My hands don't know what to do."),
                L("We go together. That's the whole plan."), null, spkKira);
            Follower(kiraNpc, string.Empty, 0.75f);

            // The Algorithm
            var algorithm = new GameObject("TheAlgorithm");
            algorithm.transform.position = new Vector3(14.5f, 3.2f, 0f);
            var algoSpin = algorithm.AddComponent<Spinner>();
            algoSpin.degreesPerSecond = new Vector3(0, 25, 0);
            algoSpin.bobAmplitude = 0.4f;
            algoSpin.bobSpeed = 0.8f;
            Deco("Core", algorithm.transform, algorithm.transform.position, new Vector3(1.6f, 1.6f, 1.6f), algoCore, new Vector3(45, 45, 0));
            var ring = new GameObject("Ring");
            ring.transform.SetParent(algorithm.transform, false);
            ring.transform.localRotation = Quaternion.Euler(70, 0, 0);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var cube = Deco("RingCube", ring.transform, Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), algoRing);
                cube.transform.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.8f;
            }
            ring.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 80, 0);
            ring.GetComponent<Spinner>().bobAmplitude = 0f;
            PointLight(algorithm.transform, algorithm.transform.position, Magenta, 8f, 16f);
            var algoActor = algorithm.AddComponent<DialogueActor>();
            algoActor.speaker = spkAlgorithm;
            algoActor.actorId = "algorithm";
            algoActor.turnToFace = false;
            PixelTears(algorithm.transform, algorithm.transform.position, staticParticles, 25f);

            // Arrival of The Algorithm
            Transform points = new GameObject("CinematicPoints").transform;
            Transform cp1 = CinematicPoint("CP_Algo_Wide", points, new Vector3(-0.5f, 2.6f, -5.5f), new Vector3(13f, 3f, 0f));
            Transform cp2 = CinematicPoint("CP_Algo_Close", points, new Vector3(9.5f, 3.6f, -4.2f), new Vector3(14.5f, 3.2f, 0f));
            Transform cp3 = CinematicPoint("CP_MemoryFile", points, new Vector3(6.3f, 1.7f, -2.6f), new Vector3(9f, 1.1f, 0f));
            CinematicSequence arrival = Cinematic("Cinematic_AlgorithmArrival", ctx.story,
                Shot("Wide", cp1, 2.5f, 0.3f, 30f,
                    L("WELCOME, LEO.", "YOUR EMOTIONAL LATENCY IS UNACCEPTABLE. YOU CARRY THREE YEARS OF UNSENT REPLIES."),
                    spkAlgorithm, false, Clip("glitch_004"), 0.5f),
                Shot("Close", cp2, 3f, 0.3f, 22f,
                    L("I AM WHAT THIS CITY BUILT SO IT WOULD STOP FEELING TIRED. I REMOVE THE FRICTION.",
                      "NO MORE MISSING HOME. NO MORE STATIC. NO MORE ANYTHING."),
                    spkAlgorithm, false, Clip("glitch_002"), 0.35f),
                Shot("File", cp3, 3f, 0.5f, 20f,
                    L("* Between us, a box floats in the air. Box fourteen. The one I never opened.",
                      "* It's labeled: leo_last_summer.mem. CORRUPTED.")));
            arrival.fadeFromBlack = false;
            arrival.blendToGameplay = true;
            arrival.blendToGameplayDuration = 2f;
            EditorUtility.SetDirty(arrival);

            var triggerGo = new GameObject("Trigger_AlgorithmArrival");
            triggerGo.transform.SetParent(ctx.story, false);
            triggerGo.transform.position = new Vector3(1.5f, 1f, 0f);
            var triggerBox = triggerGo.AddComponent<BoxCollider>();
            triggerBox.isTrigger = true;
            triggerBox.size = new Vector3(1.5f, 2f, 12.4f);
            var trigger = triggerGo.AddComponent<CinematicTrigger>();
            Set(trigger, "sequence", arrival);
            Set(trigger, "flagOnFinished", "algorithm_met");

            // The corrupted memory file (final Obstacle Hacking)
            var fileRoot = new GameObject("Obstacle_MemoryFile");
            fileRoot.transform.SetParent(ctx.story, false);
            fileRoot.transform.position = new Vector3(9f, 0f, 0f);
            fileRoot.layer = ctx.interactableLayer;
            var fileCollider = fileRoot.AddComponent<BoxCollider>();
            fileCollider.center = new Vector3(0, 1.2f, 0);
            fileCollider.size = new Vector3(1.2f, 2.4f, 12.4f);
            var floating = new GameObject("FloatingBox");
            floating.transform.SetParent(fileRoot.transform, false);
            floating.transform.localPosition = new Vector3(0, 0.7f, 0);
            GameObject box = Model("cardboardBoxClosed", "furniture");
            if (box != null)
            {
                GameObject boxModel = SpawnModel(box, floating.transform, 0.7f, false, 20f);
                TintModel(boxModel, memoryMat);
            }
            else
            {
                Deco("Box", floating.transform, floating.transform.position + Vector3.up * 0.35f, Vector3.one * 0.7f, memoryMat);
            }
            var floatSpin = floating.AddComponent<Spinner>();
            floatSpin.degreesPerSecond = new Vector3(0, 30, 0);
            floatSpin.bobAmplitude = 0.2f;
            PixelTears(floating.transform, floating.transform.position + Vector3.up * 0.4f, staticParticles, 18f);
            PointLight(fileRoot.transform, fileRoot.transform.position + new Vector3(-1f, 1.5f, 0), Cyan, 3f, 6f);
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                Deco("FirewallPane", fileRoot.transform, fileRoot.transform.position + new Vector3(0, 1.2f, i * 1.7f), new Vector3(0.05f, 2.4f, 1.5f), algoRing);
            }

            var ending = new GameObject("EndingController").AddComponent<EndingController>();
            ending.transform.SetParent(ctx.story, false);
            AudioSource endingAudio = ending.gameObject.AddComponent<AudioSource>();
            endingAudio.playOnAwake = false;
            ending.audioSource = endingAudio;
            ending.choiceClip = Clip("confirmation_004");
            ending.pageClip = Clip("click_005");
            ending.algorithmSpeaker = spkAlgorithm;
            ending.preChoiceLines = L(
                "FINAL COMMAND AVAILABLE. THE FILE IS OPEN.",
                "YOU MAY DELETE IT. THE PAIN WILL STOP. EVERYTHING WILL STOP.",
                "* {beside=byte}Byte's collar flickers: <color=#33FFF2>'ur call. not mine. not its.'</color>",
                "* {beside=kira}Kira takes my hand. She doesn't say anything. She doesn't have to.");
            ending.choicePrompt = "The memory hums in your hands. Mia's recital. The last train. Three years of silence.";
            ending.deleteOption = "[DELETE] Let the void make it quiet.";
            ending.keepOption = "[KEEP] Learn to live with the glitch.";
            ending.voidEpilogue = L(
                "You pressed DELETE.\nThe static stopped. So did the rain, the trains, the ache.",
                "Leo woke up on time every day after that.\nHe was never late. Never sad. Never anything.",
                "His handheld filled with messages he no longer understood.\nOne of them was from someone named Mia.",
                "He archived it. Efficiently.");
            ending.voidTitle = "ENDING  //  FRICTIONLESS";
            ending.glitchEpilogue = L(
                "You pressed KEEP.\nIt still hurts. It's supposed to. That's how you know it mattered.",
                "The next morning, Leo opened box fourteen.\nInside: a recital program, with a crayon heart drawn on the back.",
                "He called home. Mia answered on the first ring.\n'You're late,' she said.\n'I know,' he said. 'I'm on my way.'",
                "Somewhere in Subnet, a cat's collar flickered one last message:\n<color=#33FFF2>'proud of u. idiot.'</color>");
            ending.glitchTitle = "ENDING  //  LIVING WITH GLITCHES";
            ending.trueOption = "[SHOW IT MIA'S DRAWING] 'Because you think too much.'";
            ending.trueEpilogue = L(
                "Leo held up a crayon drawing. A boy with an enormous head.\nThe Algorithm looked at it for a long time.",
                "'I DO NOT UNDERSTAND,' it said. 'WHY KEEP SOMETHING THAT MAKES YOU HEAVIER?'\n'Because it's mine,' Leo said. 'Because someone made it for me.'",
                "The Algorithm went quiet.\nFor the first time since the city built it, it rested.\nAcross Subnet, the lights dimmed to something softer.",
                "Night Train Zero took Leo home.\nIn the last carriage, on the seat by the window, a cat was waiting.\n<color=#33FFF2>'saved u a seat.'</color>",
                "Leo knew that cat.\nA grey kitten Mia found under a car in 2003. She named him Byte,\nbecause her big brother was 'always on the computer'.",
                "He used to sleep on Leo's keyboard.\nHe was the first thing Leo ever let himself forget.\nHe never forgot Leo.",
                "The train arrived at dawn.\nMia was on the platform in her pajamas, holding a sign:\n<color=#FF4FD8>WELCOME HOME, BIG HEAD</color>",
                "Leo cried in front of everyone.\nIt was very inefficient.\nIt was the best morning of his life.");
            ending.trueTitle = "TRUE ENDING  //  THE LAST TRAIN HOME";
            ending.kiraPackagePage = "Kira delivered one last package that week, to a hospital up north.\nThen she took off her skates, sat down on a bench, and stayed.";
            ending.kiraNoPackagePage = "Kira kept skating. One day she'll open that package.\nCouriers always find the address eventually.";
            ending.credits = "NIGHT TRAIN ZERO\n\nA story about trains, cats,\nand the static we carry.\n\nMade with Unity and Kenney assets.\n\nThank you for playing.";
            EditorUtility.SetDirty(ending);

            HackableObstacle file = Hackable(fileRoot, "leo_last_summer.mem  //  CORRUPTED", spkAlgorithm,
                L("ACCESSING: leo_last_summer.mem",
                  "THIS FILE IS 94% PAIN. RECOMMENDED ACTION: DELETE.",
                  "* My handheld trembles. If I want to understand this file, I have to read it myself."),
                new[]
                {
                    new HackWave(HackPattern.Lasers, 9f, 1f),
                    new HackWave(HackPattern.Homing, 9f, 1.05f),
                    new HackWave(HackPattern.Serpent, 9f, 1.1f),
                    new HackWave(HackPattern.Fireworks, 10f, 1.15f),
                },
                new[]
                {
                    new HackPuzzle("FILE HEADER CORRUPTED. The file asks you one thing: why did you leave home?",
                        new[] { "[FOR A BETTER JOB]", "[I'M NOT SURE ANYMORE]", "[I THOUGHT THEY'D BE BETTER OFF WITHOUT ME]" }, 2,
                        "The header repairs itself. It wasn't the job. It was never the job.",
                        "THE ALGORITHM: 'THE OFFICIAL STORY. I APPROVE.' It's what you tell everyone. The file stays locked.",
                        "The Algorithm loves tidy excuses, the kind you'd put on a form. The file only opens for something true, even 'I don't know'.",
                        1, "'Not sure.' The file accepts that: honesty is a key too. The header repairs itself and shows what you couldn't say. {p}You thought they'd be better off without you."),
                    new HackPuzzle("A phone buzzes inside the memory. The screen reads: <color=#FF4FD8>MIA (my little sister) - 14 missed calls</color>. Who have you been avoiding?",
                        new[] { "[THE OFFICE]", "[NOBODY. NOBODY EVER CALLS.]", "[MIA. MY SISTER.]" }, 2,
                        "A voicemail plays, crackling: 'Leo? It's okay if you can't come. ...Can you come?'",
                        "The phone in the memory keeps ringing. You know who it is. You just don't want to.",
                        "Look at the phone in the memory again. The name is right there on the screen."),
                    new HackPuzzle("Fourteen missed calls. Why haven't you called back?",
                        new[] { "[EVERY DAY OF SILENCE MADE THE CALL HEAVIER]", "[THEY'RE MAD AT ME]", "[I'M TOO BUSY]" }, 0,
                        "Every unanswered day stacked on the last, like boxes. Fourteen of them.",
                        "The excuse dissolves as soon as you say it.",
                        "Nobody ever said they were mad, and you're not busy: you're on a ghost train. Think of the boxes in your apartment, piling up a little more every day."),
                    new HackPuzzle("THE ALGORITHM: 'DEFINE THE STATIC.'",
                        new[] { "[A MALFUNCTION]", "[NOISE]", "[THE PART OF ME THAT STILL CARES]" }, 2,
                        "The static isn't damage. It's a signal. It's the part of you that's still trying to call home.",
                        "THE ALGORITHM: 'CORRECT. LET ME CLEAN IT.' ...No. That's not it.",
                        "The Algorithm wants you to call it a defect so it can delete it. What has that static in your chest been doing all night? It's been pulling you somewhere."),
                },
                L("ERROR. ERROR. EMOTION CANNOT BE COMPRESSED FURTHER.",
                  "WHY... DOES IT HURT LESS, NOW THAT YOU HAVE READ IT?"),
                "algorithm_met",
                L("* A floating box behind a wall of light. Something tells me to face whatever's at the end of this room first."),
                "core_file_read", new GameObject[0]);
            file.portraitTarget = algorithm.transform;
            EnemyAura(fileRoot, null, new Vector3(-0.4f, 0f, 0f), 1.6f, 2.9f, Magenta, "MemoryFile");
            file.musicOverride = Clip("freedom"); // final boss theme
            file.portraitDistance = 7f;
            file.tauntLines = L("FRICTION DETECTED.", "WHY DO YOU KEEP IT?", "DELETING IS EASIER.",
                "YOU COULD JUST STOP FEELING.", "EFFICIENCY IS KINDNESS.", "3 YEARS. 0 REPLIES.", "SHE STOPPED CALLING, LEO.");
            file.betweenRoundLines = L("HEADER RESTORED. ...YOU ARE READING IT ANYWAY. INEFFICIENT.",
                "AUDIO RECOVERED. THIS VOICE INCREASES YOUR HEART RATE. I DO NOT UNDERSTAND.",
                "THE BOXES. FOURTEEN OF THEM. YOU PACKED SILENCE AND CALLED IT MOVING ON.");
            EditorUtility.SetDirty(file);

            // ---------------------------------------------------------------- Act V: after the file is read
            // 1) Kira's package (only if Leo offered to deliver it with her in the park).
            DialogueSequence package = SeqAsset("Act5/Kira_Package",
                OnlyIf("!kira_package_accepted", Say(spkKira, "There's one package in my bag nobody ever claimed. {p}{small}Maybe someday I'll be brave enough to open it.{/small}", "", DialogueSequence.EndNodeId, "act5_package_done")),
                OnlyIf("kira_package_accepted", Say(spkKira, "Hey. {p}Remember the package with no name? {p}You said we'd deliver it together.")),
                OnlyIf("kira_package_accepted", Say(spkKira, "I think... {p}{slow}I think it was for me.{/slow}")),
                OnlyIf("kira_package_accepted", Say(null, "She opens it. Inside: a postcard, soft from being carried. A stamp from a hospital gift shop.")),
                OnlyIf("kira_package_accepted", Say(null, "<color=#FFD84F>'To the girl who brought me mail that wasn't mine. {p}It was always mine. Thank you. {p}Stop running sometime. - Room 9'</color>")),
                OnlyIf("kira_package_accepted", Say(spkKira, "{shake}...Ha.{/shake} {p}He knew. {p}{slow}He knew the whole time.{/slow}")),
                OnlyIf("kira_package_accepted", Say(spkKira, "Okay. {p}I'm going to stop running. {p}{small}After this. Right after this.{/small}", "", "", "kira_package_opened;+closeness#package_open;act5_package_done")));
            Event(ctx, "Act5_Package", StoryEvent.TriggerMode.OnFlag, "core_file_read", 2f, null, null, null, null, string.Empty, default, default, package);

            // 2) The call. Mia, live, for the first time in fourteen missed calls.
            DialogueSequence call = SeqAsset("Act5/Mia_Call",
                Say(null, "My handheld buzzes. {p}Not a message. {p}{slow}A call.{/slow}"),
                Ask(null, "<color=#FF4FD8>MIA</color> is calling.",
                    Opt("[Answer]", "answer", "answered_mia"),
                    Opt("[Let it ring]", "ring")),
                Say(null, "It rings. And rings. {p}Then it stops.", "ring"),
                Say(spkMia, "{small}Hey. It's me. I don't know why I keep calling this number at midnight.{/small}"),
                Say(spkMia, "{small}...Okay. Bye.{/small}", "", DialogueSequence.EndNodeId, "act5_call_done"),
                Say(spkMia, "...Leo?", "answer"),
                Say(spkMia, "Oh my god. You picked up. {p}{small}You actually picked up.{/small}"),
                Ask(null, "...",
                    Opt("[\"Hi, Mia.\"]", "hi"),
                    Opt("[\"I'm sorry I missed your recital.\"]", "sorry", "+openness#call_sorry"),
                    Opt("[Say nothing. Just listen.]", "listen")),
                Say(spkMia, "Hi. {p}{wave}Hi hi hi.{/wave} {p}Okay. Sorry. You sound tired.", "hi", "middle"),
                Say(spkMia, "...Yeah. You did. {p}I bowed to an empty chair, you know. {p}{small}I'm still mad. I'm also really, really happy you called.{/small}", "sorry", "middle"),
                Say(spkMia, "You don't have to talk. I'll talk. {p}Mom made lasagna on Sunday. Dad fell asleep during the news. {p}{slow}Normal stuff. You're missing all the normal stuff.{/slow}", "listen", "middle"),
                WithId("middle", Ask(spkMia, "Where are you? {p}It sounds like a train.",
                    Opt("[\"Somewhere weird. I'm with friends.\"]", "friends", "+closeness#call_friends"),
                    Opt("[\"I think I'm on my way home.\"]", "home", "+openness#call_home"))),
                Say(spkMia, "Friends? {p}{bounce}Leo has friends?{/bounce} {p}Tell them I said thanks. For keeping you.", "friends", "battery"),
                Say(spkMia, "{slow}...Really?{/slow} {p}I'll wait at the station. {p}{small}I'll bring a sign. A stupid one.{/small}", "home", "battery"),
                Say(spkMia, "The battery's dying. {p}Leo, whatever you're doing right now... {p}{slow}don't delete anything. Okay?{/slow}", "battery"),
                Say(null, "The line goes quiet. {p}Kira is looking at me like she heard every word."),
                Say(spkKira, "{small}She sounds like you. {p}When you're not pretending.{/small}", "", "", "act5_call_done"));
            Event(ctx, "Act5_Call", StoryEvent.TriggerMode.OnFlag, "act5_package_done", 1.5f, null, null, null, null, string.Empty, default, default, call);

            // 3) The last train pulls into the Core. Walking to its doors starts the ending.
            var lastTrain = new GameObject("LastTrain");
            lastTrain.transform.SetParent(ctx.story, false);
            lastTrain.transform.position = new Vector3(15.5f, 0f, -4.6f);
            Set(lastTrain.AddComponent<ShowWhenFlag>(), "flag", "act5_call_done");
            var trainVisual = new GameObject("Visual");
            trainVisual.transform.SetParent(lastTrain.transform, false);
            trainVisual.transform.position = new Vector3(15.5f, 0f, -5.4f);
            GameObject carAsset = Model("train-electric-subway-b", "train-kit");
            if (carAsset != null) SpawnModel(carAsset, trainVisual.transform, 6f, true, 0f);
            else Deco("Car", trainVisual.transform, new Vector3(15.5f, 1.1f, -5.4f), new Vector3(6f, 2.2f, 1.8f), monolith);
            PointLight(trainVisual.transform, new Vector3(15.5f, 1.6f, -4.2f), Warm, 3f, 6f);
            Material platformGlow = EmissiveMaterial("Subnet/M_LastTrain_Platform", new Color(0.2f, 0.15f, 0.1f), Warm * 1.5f);
            Deco("PlatformLine", trainVisual.transform, new Vector3(15.5f, 0.01f, -4.2f), new Vector3(6f, 0.02f, 0.12f), platformGlow);
            TMPro.TextMeshPro zero = WorldSign(trainVisual.transform, "PLATFORM 0", new Vector3(15.5f, 2.9f, -4.4f), 180f, Warm, 3f);
            zero.rectTransform.sizeDelta = new Vector2(4f, 0.6f);

            Event(ctx, "Act5_TrainSms", StoryEvent.TriggerMode.OnFlag, "act5_call_done", 1.2f, "BYTE",
                L("trains here. last one. {p}get on. {p}{small}u know where i am.{/small}"), null, null, string.Empty);
            Event(ctx, "Act5_Board", StoryEvent.TriggerMode.OnPlayerEnter, "act5_call_done", 0.1f, null, null,
                L("* Night Train Zero. The doors are open. {p}It's waiting for me. One last time.",
                  "* Behind me, the Algorithm hums. It's not attacking anymore. {p}It's just watching. Like it's waiting for my answer too."),
                null, "board_last_train",
                new Vector3(15.5f, 1f, -3.6f), new Vector3(6f, 2f, 1.6f));
            ending.beginOnFlag = "board_last_train";

            Event(ctx, "Arrive", StoryEvent.TriggerMode.OnStart, string.Empty, 1.2f, "BYTE",
                L("this is it. the core. whatever happens, im a cat, i land on my feet. u? less sure."),
                L("It's so quiet here. Like a phone on silent that never stops vibrating.",
                  "* At the far end of the room, something enormous is waiting. It's been waiting for a long time."),
                spkKira, string.Empty);
            Event(ctx, "AfterArrival", StoryEvent.TriggerMode.OnFlag, "algorithm_met", 0.5f, "BYTE",
                L("the box. open it. read it. dont let it read u first."), null, null, string.Empty);

            EndGameplayScene(ctx, CoreScene);
        }
    }
}
#endif
