#if UNITY_EDITOR
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

            AddSceneMusic(true);
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
            ending.credits = "NIGHT TRAIN ZERO\n\nA story about trains, cats,\nand the static we carry.\n\nMade with Unity and Kenney assets.\n\nThank you for playing.";
            EditorUtility.SetDirty(ending);

            HackableObstacle file = Hackable(fileRoot, "leo_last_summer.mem  //  CORRUPTED", spkAlgorithm,
                L("ACCESSING: leo_last_summer.mem",
                  "THIS FILE IS 94% PAIN. RECOMMENDED ACTION: DELETE.",
                  "* My handheld trembles. If I want to understand this file, I have to read it myself."),
                new[]
                {
                    new HackWave(HackPattern.Aimed, 9f, 1.1f),
                    new HackWave(HackPattern.Spiral, 9f, 1.1f),
                    new HackWave(HackPattern.Crossfire, 9f, 1.2f),
                    new HackWave(HackPattern.Ripple, 10f, 1.2f),
                },
                new[]
                {
                    new HackPuzzle("FILE HEADER CORRUPTED. Why did you leave home?",
                        new[] { "[FOR A BETTER JOB]", "[I THOUGHT THEY'D BE BETTER OFF WITHOUT ME]", "[I DON'T REMEMBER]" }, 1,
                        "The header repairs itself. It wasn't the job. It was never the job.",
                        "THE ALGORITHM: 'A CONVENIENT STORY. I APPROVE.' The file stays locked."),
                    new HackPuzzle("Who called you on the night of the last train?",
                        new[] { "[THE OFFICE]", "[NOBODY. NOBODY EVER CALLS.]", "[MIA. ABOUT HER RECITAL.]" }, 2,
                        "A voicemail plays, crackling: 'Leo? It's okay if you can't come. ...Can you come?'",
                        "The phone in the memory keeps ringing. You know who it is. You just don't want to."),
                    new HackPuzzle("Why haven't you called back?",
                        new[] { "[EVERY DAY OF SILENCE MADE THE CALL HEAVIER]", "[THEY'RE MAD AT ME]", "[I'M TOO BUSY]" }, 0,
                        "Every unanswered day stacked on the last, like boxes. Fourteen of them.",
                        "The excuse dissolves as soon as you say it."),
                    new HackPuzzle("THE ALGORITHM: 'DEFINE THE STATIC.'",
                        new[] { "[A MALFUNCTION]", "[NOISE]", "[THE PART OF ME THAT STILL CARES]" }, 2,
                        "The static isn't damage. It's a signal. It's the part of you that's still trying to call home.",
                        "THE ALGORITHM: 'CORRECT. LET ME CLEAN IT.' ...No. That's not it."),
                },
                L("ERROR. ERROR. EMOTION CANNOT BE COMPRESSED FURTHER.",
                  "WHY... DOES IT HURT LESS, NOW THAT YOU HAVE READ IT?"),
                "algorithm_met",
                L("* A floating box behind a wall of light. Something tells me to face whatever's at the end of this room first."),
                "core_file_read", new GameObject[0]);
            file.portraitTarget = algorithm.transform;
            file.musicOverride = Clip("freedom"); // final boss theme
            file.portraitDistance = 7f;
            file.tauntLines = L("FRICTION DETECTED.", "WHY DO YOU KEEP IT?", "DELETING IS EASIER.",
                "YOU COULD JUST STOP FEELING.", "EFFICIENCY IS KINDNESS.", "3 YEARS. 0 REPLIES.", "SHE STOPPED CALLING, LEO.");
            file.betweenRoundLines = L("HEADER RESTORED. ...YOU ARE READING IT ANYWAY. INEFFICIENT.",
                "AUDIO RECOVERED. THIS VOICE INCREASES YOUR HEART RATE. I DO NOT UNDERSTAND.",
                "THE BOXES. FOURTEEN OF THEM. YOU PACKED SILENCE AND CALLED IT MOVING ON.");
            UnityEventTools.AddPersistentListener(file.onResolved, ending.Begin);
            EditorUtility.SetDirty(file);

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
