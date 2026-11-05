#if UNITY_EDITOR
using Subnet.Audio;
using Subnet.Cinematics;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using Subnet.Hacking;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== 03_Subnet_Commercial (Act II)

        private static void BuildSubnetScene()
        {
            VolumeProfile profile = LookProfile("PP_Subnet_Commercial", 15f, 1.4f, 0.3f, 0.15f, 0.25f, new Color(1f, 0.92f, 1f));
            Ctx ctx = BeginGameplayScene(new Vector3(-14f, 0f, 0f), 90f, profile,
                new Color(0.36f, 0.3f, 0.46f), new Color(1f, 0.72f, 0.92f), 0.6f, true);

            AddSceneMusic(true);
            ctx.removeAmbience = true;
            ctx.ambience.masterVolume = 0.45f;
            ctx.ambience.pad = 0.55f;
            ctx.ambience.hum = 0.12f;
            ctx.ambience.humFrequency = 55f;
            ctx.ambience.crackle = 0.05f;
            ctx.ambience.wind = 0.08f;
            ctx.ambience.rain = 0f;

            Material groundMat = LitMaterial("Subnet/M_Street_Lilac", new Color(0.52f, 0.44f, 0.72f), 0.4f);
            Material sidewalkMat = LitMaterial("Subnet/M_Sidewalk_Pink", new Color(0.95f, 0.62f, 0.82f));
            Material neonCyan = EmissiveMaterial("Subnet/M_Neon_Cyan", new Color(0.1f, 0.3f, 0.3f), Cyan * 3f);
            Material neonMagenta = EmissiveMaterial("Subnet/M_Neon_Magenta", new Color(0.3f, 0.1f, 0.25f), Magenta * 3f);
            Material staticMat = EmissiveMaterial("Subnet/M_StaticWall", new Color(0.2f, 0.1f, 0.25f), new Color(0.8f, 0.4f, 1f) * 2f);
            Material mannequinMat = LitMaterial("Subnet/M_Mannequin", new Color(0.92f, 0.9f, 0.95f), 0.6f);
            Material paperMat = LitMaterial("Subnet/M_Paperwork", new Color(0.96f, 0.95f, 0.9f));
            Material stoneGray = LitMaterial("Subnet/M_Calcified_Commuter", new Color(0.62f, 0.62f, 0.6f));
            Material clockMat = EmissiveMaterial("Subnet/M_ClockFace", new Color(0.9f, 0.9f, 0.85f), new Color(1f, 0.95f, 0.8f) * 0.8f);
            Material handMat = LitMaterial("Subnet/M_ClockHand", new Color(0.1f, 0.1f, 0.12f));
            Material signMat = EmissiveMaterial("Subnet/M_SaleSign", new Color(0.3f, 0.05f, 0.2f), new Color(1f, 0.4f, 0.7f) * 2.2f);
            Material redMat = EmissiveMaterial("Subnet/M_TrafficRed", new Color(0.3f, 0f, 0f), new Color(1f, 0.15f, 0.2f) * 3f);
            Material poleMat = LitMaterial("Subnet/M_Pole", new Color(0.3f, 0.28f, 0.38f));

            // Street shell
            Block("Street", ctx.env, new Vector3(3f, -0.1f, 0f), new Vector3(42f, 0.2f, 8.4f), groundMat, ctx.groundLayer);
            Block("Sidewalk", ctx.env, new Vector3(3f, 0.02f, 3.7f), new Vector3(42f, 0.1f, 1.2f), sidewalkMat, ctx.groundLayer);
            Block("BackLot", ctx.env, new Vector3(3f, -0.1f, 10f), new Vector3(42f, 0.2f, 12f), sidewalkMat, 0);
            for (float x = -16f; x <= 22f; x += 3f)
            {
                Deco("LaneMark", ctx.env, new Vector3(x, 0.005f, 0f), new Vector3(1.4f, 0.01f, 0.12f), neonCyan);
            }
            Deco("CurbNeon", ctx.env, new Vector3(3f, 0.08f, 3.08f), new Vector3(42f, 0.04f, 0.06f), neonMagenta);
            Block("Bound_North", ctx.env, new Vector3(3f, 1f, 4.4f), new Vector3(42f, 2f, 0.2f), null, 0);
            Block("Bound_South", ctx.env, new Vector3(3f, 1f, -4.3f), new Vector3(42f, 2f, 0.2f), null, 0);
            Block("Bound_West", ctx.env, new Vector3(-17f, 1f, 0f), new Vector3(0.2f, 2f, 8.6f), null, 0);
            Block("Bound_East", ctx.env, new Vector3(23.5f, 1f, 0f), new Vector3(0.2f, 2f, 8.6f), null, 0);

            // Buildings: a front row whose facades start behind the sidewalk boundary, then a skyline behind it.
            string[] front = { "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g", "building-h", "building-i", "building-j" };
            float frontBack = BuildingRow(ctx.env, front, "commercial", -17f, 24f, 4.8f, 5.5f, 1.5f, 0.5f, 7f, 180f);
            string[] skyline = { "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d", "building-skyscraper-e" };
            BuildingRow(ctx.env, skyline, "commercial", -17f, 24f, frontBack + 1.5f, 11f, 3f, 1f, 9f, 180f, 8);

            // Neon lights along the street
            for (int i = 0; i < 6; i++)
            {
                float x = -13f + i * 6.5f;
                PointLight(ctx.env, new Vector3(x, 2.6f, 2.6f), i % 2 == 0 ? Magenta : Cyan, 3f, 7f);
            }

            const string F = "furniture";

            // Streetlamps
            GameObject lamp = Prop(ctx, "Streetlamp", "lampSquareFloor", F, new Vector3(-11f, 0, 3.4f), 0, 2.6f, false,
                L("A silent streetlamp. It says it's tired of telling everyone where to go.", "* Same, buddy. Same."),
                L("It flickers once, like a shrug."), null, true, spkObject);
            PointLight(lamp.transform, lamp.transform.position + Vector3.up * 2.5f, Cyan, 2f, 5f);
            Prop(ctx, "Streetlamp2", "lampSquareFloor", F, new Vector3(16f, 0, 3.4f), 0, 2.6f, false, null);

            // Byte the cat
            DialogueSequence byteMeeting = SeqAsset("Act2/Byte_Meeting",
                Say(spkByte, "Oh good. You made it. I was starting to think you'd stay on the train forever. People do that, you know."),
                Say(null, "A cat. Talking. Its collar is a tiny holographic screen scrolling old text messages from 2003."),
                Ask(spkByte, "Name's Byte. Before you ask: yes, I'm a cat. No, I won't explain the collar. Questions?",
                    Opt("Where am I?", "where"),
                    Opt("Did you send me those messages?", "msgs"),
                    Opt("I'm dreaming, right?", "dream")),
                Say(spkByte, "Subnet. The city under the city. Everything people throw away ends up here: dreams, habits, the version of themselves they gave up on.", "where", "cont"),
                Say(spkByte, "Guilty. I text in lowercase. It's a whole aesthetic.", "msgs", "cont"),
                Say(spkByte, "Sure. And I'm a lucid hallucination with excellent taste in collars. Doesn't make the static in your chest any less real.", "dream", "cont"),
                Say(spkByte, "Look. The city's sick. Something called The Algorithm is eating feelings and calling it efficiency. And for some reason, your handheld can talk to it.", "cont"),
                Say(spkByte, "So I'm coming with you. Not because I care. Because I'm bored. ...Mostly bored.", "", "", "met_byte"),
                Say(null, "(BYTE joined you. Its collar will text you when you look lost.)"));

            GameObject byteNpc = Character(ctx, "Byte", "animal-cat", "cube-pets", new Vector3(-9f, 0f, 1.6f), -90f, 0.45f, null, false);
            Set(Inspectable(byteNpc, "Byte", L("..."),
                L("keep walking. the interesting stuff is always behind the scariest wall. || and dont pet me. {p}...ok. {bounce}once.{/bounce}",
                  "you want to know how i got here? {p}same as everyone. {p}{small}somebody stopped thinking about me.{/small} || ...dont look at me like that. its a good gig. free rent.",
                  "my collar shows texts from 2003. {p}nobody i know. {p}i read them anyway. {p}'r u up' was big that year.",
                  "kira talks fast so she doesnt have to hear herself. {p}you talk slow so you dont have to say anything. {p}{small}im the only normal one here.{/small}",
                  "if you get lost, look for the brightest sign and go the other way. {p}{small}thats where the good stuff hides.{/small}",
                  "...you can pet me. {p}{slow}not because i want it.{/slow} {p}for your mental health."),
                "met_byte", spkByte, byteMeeting), "cycleRepeat", true);
            Follower(byteNpc, "met_byte", -0.6f);

            // Static wall that opens after meeting Byte
            GameObject staticWall = Block("StaticWall", ctx.props, new Vector3(-6.5f, 1.2f, 0f), new Vector3(0.3f, 2.4f, 8.4f), staticMat, ctx.interactableLayer);
            Inspectable(staticWall, "StaticWall",
                L("A wall of pure static. It hisses like a TV left on at 3 a.m.", "* Behind me, a cat is staring at me like I'm supposed to say hello first."),
                L("* Still static. Still a cat staring at me."));
            Set(staticWall.AddComponent<FlagGate>(), "openFlag", "met_byte");
            PointLight(staticWall.transform, staticWall.transform.position + new Vector3(-0.8f, 0.5f, 0), new Color(0.8f, 0.4f, 1f), 2.5f, 5f);

            // Café
            Prop(ctx, "CafeTable", "tableRound", F, new Vector3(-3f, 0, 2.6f), 0, 0.7f, false,
                L("A café table where nobody sits down. The chairs look relieved."),
                L("The menu lists one item: 'A MOMENT TO YOURSELF - SOLD OUT'."));
            Prop(ctx, "CafeChair1", "chair", F, new Vector3(-3.7f, 0, 2.6f), 90, 0.8f, false, null);
            Prop(ctx, "CafeChair2", "chair", F, new Vector3(-2.3f, 0, 2.6f), -90, 0.8f, false, null);
            Prop(ctx, "Parasol", "detail-parasol-a", "commercial", new Vector3(-3f, 0, 2.6f), 0, 2.2f, false, null, null, null, false);
            Prop(ctx, "Vending", "kitchenFridgeLarge", F, new Vector3(-0.5f, 0, 3.5f), 180, 1.6f, false,
                L("A vending machine that sells MOTIVATION in tiny cans. Out of stock. It's always out of stock."),
                L("You press the button anyway. It dispenses a receipt that says 'MAYBE TOMORROW'."));

            // Sale sign + mannequin time-loop
            GameObject sign = Block("Prop_SaleSign", ctx.props, new Vector3(3f, 2.4f, 3.9f), new Vector3(3f, 0.7f, 0.1f), signMat, ctx.interactableLayer);
            Inspectable(sign, "SaleSign",
                L("A neon sign: SALE! YOUR FREE TIME, 50% OFF!", "* Terms and conditions: all of them."),
                L("The sign blinks faster when you look at it. It knows."));
            Vector3 plaza = new Vector3(3f, 0f, 0f);
            string[] mannequinLines =
            {
                "Buy. Return. Buy. Return. If I stop walking, I'll have to ask myself what I actually wanted.",
                "SALE! SALE! Everything must go! ...Including me, apparently.",
                "I came here for one thing. I forgot what. So I'm buying everything, just in case.",
                "Don't look at me. I'm not a person, I'm a lifestyle. A limited edition.",
                "We've walked this circle four thousand times. The music never changes. The music never changes."
            };
            string[] mannequinModels = { "character-female-a", "character-male-a", "character-female-e", "character-male-c", "character-female-f" };
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f;
                Vector3 pos = plaza + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)) * 2.3f;
                GameObject m = Character(ctx, "Mannequin_" + i, mannequinModels[i], "mini-characters", pos, 0f, 1.05f, mannequinMat, false);
                if (i == 0)
                {
                    // Bible, act II side quest "Lost receipt": this one keeps looking for a receipt.
                    DialogueSequence receiptSeq = SeqAsset("Act2/Mannequin_Receipt",
                        new DialogueNode { speaker = spkCommuter, text = "Receipt. {p}Have you seen my receipt? {p}I returned something. Without the receipt, it never happened.", requiredFlag = "!receipt_found", flagToSet = "receipt_quest" },
                        new DialogueNode { speaker = spkCommuter, text = "{shake}It has to be somewhere.{/shake} {p}Somewhere you throw away the things you used to want.", requiredFlag = "!receipt_found", nextNodeId = DialogueSequence.EndNodeId },
                        new DialogueNode { speaker = spkCommuter, text = "I gave it away. I remember now. {p}{wave}That's enough.{/wave}", requiredFlag = "receipt_returned", nextNodeId = DialogueSequence.EndNodeId },
                        new DialogueNode { speaker = null, text = "(You hold out the crumpled receipt from the café bin.)" },
                        new DialogueNode { speaker = spkCommuter, text = "RETURNED: one scarf. Hand-knitted. {p}{slow}...Hand-knitted.{/slow}" },
                        new DialogueNode { speaker = spkCommuter, text = "I didn't return it. {p}I gave it to someone who was always cold. {p}{wave}I remember their face.{/wave}" },
                        new DialogueNode { speaker = spkCommuter, text = "The circle stops. {p}{big}It stops.{/big}", flagToSet = "receipt_returned;+openness" },
                        new DialogueNode { speaker = null, text = "Mia knitted me a scarf once. It had a hole by the left end. {p}She said it was a feature." },
                        new DialogueNode { speaker = null, text = "I wore it every winter. {p}{slow}It's in box fourteen. I think.{/slow}", flagToSet = "memory_scarf" });
                    Inspectable(m, "Mannequin_" + i, L("..."), null, null, spkCommuter, receiptSeq);
                    var release = m.AddComponent<FlagGate>();
                    Set(release, "openFlag", "receipt_returned");
                }
                else
                {
                    Inspectable(m, "Mannequin_" + i, L(mannequinLines[i]), L("The circle continues. The circle always continues."), null, spkCommuter);
                }
                var walker = m.AddComponent<LoopWalker>();
                walker.Configure(plaza, 2.3f, 22f, angle);
                EditorUtility.SetDirty(walker);
            }

            // Traffic light
            Deco("TrafficPole", ctx.props, new Vector3(7.5f, 1.2f, 3.5f), new Vector3(0.12f, 2.4f, 0.12f), poleMat);
            GameObject red = Block("Prop_TrafficLight", ctx.props, new Vector3(7.5f, 2.4f, 3.4f), new Vector3(0.3f, 0.3f, 0.2f), redMat, ctx.interactableLayer);
            Inspectable(red, "TrafficLight",
                L("A red traffic light. It whispers: 'What if you're going the wrong way? What if there is no right way?'",
                  "It turns green. It doesn't apologize."),
                L("Red again. It's going through something."), null, spkObject);

            // Kira
            DialogueSequence kiraMeeting = SeqAsset("Act2/Kira_Meeting",
                Say(spkKira, "Whoa, careful! I'm on a deadline. I'm always on a deadline."),
                Say(null, "A courier on jet-powered inline skates. Her thermal backpack is glowing and humming softly, like it's purring."),
                Say(spkKira, "I'm Kira. I deliver care packages. Little boxes of self-esteem, addressed to people nobody sees anymore."),
                Ask(spkKira, "Problem is, my route's blocked. Some guy turned into a wall of overtime and paperwork. You look... less busy than me. Can you help?",
                    Opt("I'll talk to him.", "yes", "kira_trusts_leo;+closeness"),
                    Opt("Why don't YOU do it?", "why")),
                Say(spkKira, "Because if I stop moving, I have to feel things. And I'm booked solid until, uh, forever.", "why", "after"),
                Say(spkKira, "Really? Okay. Okay! That's... nobody ever offers.", "yes", "after"),
                Say(spkByte, "Use the handheld, genius. It can read the source code of sad things.", "after"),
                Say(spkKira, "The wall's at the end of the street. Don't let the clocks get in your head. They bite."));

            GameObject kira = Character(ctx, "Kira", "character-female-c", "mini-characters", new Vector3(9.5f, 0f, -1.6f), -90f, 1.05f);
            Set(Inspectable(kira, "Kira", L("..."),
                L("Still here! Technically on break. {p}{small}Don't tell my rating.{/small} || The wall's that way. Be nice to it. It's having a rough decade.",
                  "Five-star tip: never deliver on an empty stomach. {p}{fast}I always deliver on an empty stomach.{/fast}",
                  "I've got forty-two packages in this bag. {p}Forty-one addresses. {p}{small}Don't do the math.{/small}",
                  "Your cat looks at me like I owe him money. {p}...Do I owe him money?",
                  "You walk like you're carrying boxes. Even when your hands are empty. {p}{small}I notice stuff. Occupational hazard.{/small}",
                  "Hey. {p}Thanks for staying. {p}{fast}Most people take the package and go, which, fair, that's the job, but still.{/fast}"),
                "met_kira", spkKira, kiraMeeting), "cycleRepeat", true);
            // Jet-skate glow under her feet.
            Deco("SkateGlow", kira.transform, kira.transform.position + new Vector3(0, 0.03f, 0), new Vector3(0.45f, 0.03f, 0.45f), neonCyan);
            PointLight(kira.transform, kira.transform.position + new Vector3(0, 0.8f, -0.6f), new Color(1f, 0.7f, 0.3f), 1.2f, 2.5f);

            // The Wall of Overtime (Obstacle Hacking #1)
            var wallRoot = new GameObject("Obstacle_WallOfOvertime");
            wallRoot.transform.SetParent(ctx.story, false);
            wallRoot.transform.position = new Vector3(14f, 0f, 0f);
            wallRoot.layer = ctx.interactableLayer;
            var wallCollider = wallRoot.AddComponent<BoxCollider>();
            wallCollider.center = new Vector3(0, 1.5f, 0);
            wallCollider.size = new Vector3(1.6f, 3f, 8.6f);

            var shell = new GameObject("Shell");
            shell.transform.SetParent(wallRoot.transform, false);
            var rng = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                float y = (float)rng.NextDouble() * 2.8f + 0.1f;
                float z = (float)rng.NextDouble() * 8.2f - 4.1f;
                float x = (float)rng.NextDouble() * 0.9f - 0.45f;
                Deco("Paper", shell.transform, wallRoot.transform.position + new Vector3(x, y, z),
                    new Vector3(0.08f, 0.55f, 0.42f), paperMat, new Vector3((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 60f - 30f, (float)rng.NextDouble() * 360f));
            }
            GameObject commuter = Character(ctx, "WallCommuter", "character-male-f", "mini-characters", wallRoot.transform.position + new Vector3(-0.4f, 0, 0), -90f, 1.1f, stoneGray, false, shell.transform);
            Object.DestroyImmediate(commuter.GetComponent<Collider>());
            for (int i = 0; i < 4; i++)
            {
                var clock = new GameObject("Clock_" + i);
                clock.transform.SetParent(shell.transform, false);
                clock.transform.position = wallRoot.transform.position + new Vector3(-0.7f, 1.4f + (i % 2) * 1.1f, -3f + i * 2f);
                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(face.GetComponent<Collider>());
                face.transform.SetParent(clock.transform, false);
                face.transform.localScale = new Vector3(0.7f, 0.03f, 0.7f);
                face.transform.localRotation = Quaternion.Euler(0, 0, 90);
                face.GetComponent<Renderer>().sharedMaterial = clockMat;
                var hand = Deco("Hand", clock.transform, clock.transform.position + new Vector3(-0.05f, 0.12f, 0), new Vector3(0.02f, 0.28f, 0.04f), handMat);
                hand.transform.SetParent(clock.transform, true);
                var spinner = clock.AddComponent<Spinner>();
                spinner.degreesPerSecond = new Vector3(i % 2 == 0 ? 90f : -140f, 0, 0);
                spinner.bobAmplitude = 0.12f;
            }
            PointLight(wallRoot.transform, wallRoot.transform.position + new Vector3(-1.5f, 2f, 0), new Color(1f, 0.95f, 0.8f), 2.5f, 6f);

            GameObject freed = Character(ctx, "FreedCommuter", "character-male-f", "mini-characters", new Vector3(13.2f, 0f, -2.8f), -140f, 1.05f, null, false);
            Inspectable(freed, "FreedCommuter",
                L("I'm going to take a lunch break. A real one. With a sandwich and everything.", "Thank you for... not fighting me. Nobody's ever not fought me.",
                  "My place is just down the street. 0451. {p}Go in, if you want. The door's open. {p}{small}I haven't been home in... I don't know.{/small}"),
                L("Did you know the sky here is pink? I never looked up before."), null, spkCommuter);

            HackableObstacle wallHack = Hackable(wallRoot, "COMMUTER_0451.exe  //  'THE WALL OF OVERTIME'", spkCommuter,
                L("Can't talk. Behind. Always behind.",
                  "If I stop moving, the paperwork catches up. So I became the paperwork.",
                  "* My handheld vibrates. It wants to connect to him. To read whatever he's become."),
                new[]
                {
                    new HackWave(HackPattern.PaperRain, 9f, 0.9f),
                    new HackWave(HackPattern.ClockHands, 9f, 0.9f),
                    new HackWave(HackPattern.TrainRush, 10f, 1f),
                },
                new[]
                {
                    new HackPuzzle("His thoughts loop: 'If I stop, I fall behind.' Which memory fragment answers him?",
                        new[] { "[DEADLINE: TOMORROW 9:00]", "[A LUNCH BREAK IN THE SUN, 2009]", "[REPLY ALL]" }, 1,
                        "The paper wall shivers. Somewhere inside, a man remembers sunlight on a sandwich.",
                        "The fragment bounces off. He files it under 'later'.",
                        "He's drowning in work. Another task won't help him. What would remind him that stopping can feel good?"),
                    new HackPuzzle("The wall whispers: 'Nobody would notice if I disappeared.' Choose a fragment.",
                        new[] { "[STATISTICALLY TRUE]", "[THE BARISTA WHO KNOWS HIS ORDER]", "[OUT OF OFFICE: FOREVER]" }, 1,
                        "'...She always draws a little cloud on my cup.' A layer of paper peels away.",
                        "The thought hardens into another sheet of paper.",
                        "He thinks nobody sees him. Is there anyone who notices him every single day? Think of the café outside, and the little clouds on the cups."),
                    new HackPuzzle("Last firewall: 'Who am I, if I'm not busy?'",
                        new[] { "[PRODUCTIVITY: 87%]", "[UNKNOWN]", "[SOMEONE WHO USED TO DRAW ON NAPKINS]" }, 2,
                        "He laughs, a small rusty sound. 'I did, didn't I? Little rockets. Little dogs.'",
                        "'Unknown' echoes through the wall. It sounds too much like the truth he's afraid of.",
                        "Not a number, not a blank. Who was he before the paperwork? Something he did just because he loved it."),
                },
                L("...Oh. It's lunchtime. When did it get so late?",
                  "I think I'll sit down for a minute. Just a minute.",
                  "* The paperwork dissolves into warm pixels. The street ahead is open."),
                "met_kira",
                L("* A wall of paperwork, humming with dread. Clocks tick inside it, all at different speeds.",
                  "* That courier on the skates seemed to know what this is. I should talk to her first."),
                "hack1_done", new[] { shell }, new[] { freed });
            wallHack.portraitTarget = commuter.transform;
            EnemyAura(wallRoot, shell.transform, new Vector3(-0.8f, 0f, 0f), 1.5f, 3.6f, new Color(1f, 0.85f, 0.5f), "Commuter");
            wallHack.portraitDistance = 2.4f;
            wallHack.tauntLines = L("Deadline! Deadline!", "Per my last email...", "Can't stop. Won't stop. Shouldn't stop.",
                "Circle back! Circle BACK!", "Is it Friday yet? It's never Friday.", "Let's take this offline. Forever.");
            wallHack.betweenRoundLines = L("...A sandwich. In the sun. I forgot food could be warm.",
                "She draws clouds on my cup. Every day. I never said thank you.");
            EditorUtility.SetDirty(wallHack);

            Event(ctx, "AfterHack1", StoryEvent.TriggerMode.OnFlag, "hack1_done", 1.5f, "BYTE",
                L("next stop: concrete park. east. follow the cold."),
                L("You didn't fight him. You just... listened. Is that allowed?",
                  "{approach=kira}Here. Take one of these. Somebody should get a care package delivered to them, for once.",
                  "* (Kira hands you a small warm box. The label says: FRAGILE. CONTENTS: YOU.)"),
                spkKira, "got_care_package");
            Event(ctx, "ByteHint", StoryEvent.TriggerMode.OnFlag, "met_byte", 6f, "BYTE",
                L("the courier by the mannequins. talk to her. she knows the route."), null, null, string.Empty);

            // The café bin hides the receipt.
            Prop(ctx, "CafeBin", "trashcan", F, new Vector3(-4.3f, 0, 3.3f), 0, 0.55f, false,
                L("A bin full of paper cups. Every single one has a little cloud drawn on it.",
                  "* Under the cups: a crumpled receipt. <color=#FF4FD8>RETURNED: 1 SCARF, HAND-KNITTED.</color>",
                  "* Somebody in the circle out there is looking for this."),
                L("Just cups now. {p}Every one has a little cloud on it."), "receipt_found");

            // ---------------------------------------------------------------- Bible, act II: the rooftop (breathing room)
            // A roof high above the street, reached by a fire escape once Kira has handed over the care package.
            Vector3 roof = new Vector3(70f, 6f, 0f);
            Block("RoofFloor", ctx.env, roof + new Vector3(0, -0.1f, 0), new Vector3(12f, 0.2f, 8f), poleMat, ctx.groundLayer);
            Block("RoofRail_N", ctx.env, roof + new Vector3(0, 0.45f, 4.05f), new Vector3(12f, 0.9f, 0.1f), poleMat, 0);
            Block("RoofRail_S", ctx.env, roof + new Vector3(0, 0.45f, -4.05f), new Vector3(12f, 0.9f, 0.1f), poleMat, 0);
            Block("RoofRail_E", ctx.env, roof + new Vector3(6.05f, 0.45f, 0), new Vector3(0.1f, 0.9f, 8f), poleMat, 0);
            Block("RoofRail_W", ctx.env, roof + new Vector3(-6.05f, 0.45f, 0), new Vector3(0.1f, 0.9f, 8f), poleMat, 0);
            Deco("WaterTank", ctx.env, roof + new Vector3(-4.2f, 1.1f, 2.6f), new Vector3(1.6f, 2.2f, 1.6f), stoneGray);
            Deco("Antenna", ctx.env, roof + new Vector3(4.6f, 1.6f, 3f), new Vector3(0.08f, 3.2f, 0.08f), poleMat);
            PointLight(ctx.env, roof + new Vector3(4.6f, 3.3f, 3f), new Color(1f, 0.2f, 0.3f), 2f, 4f);
            for (int b = 0; b < 9; b++)
            {
                // The city below, all windows and signs.
                float bx = roof.x - 16f + b * 4f;
                float h = 6f + (b * 37 % 9);
                Deco("Skyline_" + b, ctx.env, new Vector3(bx, roof.y - 6f + h * 0.5f, 14f + (b % 3) * 3f), new Vector3(3f, h, 3f), b % 2 == 0 ? neonCyan : neonMagenta);
            }
            PointLight(ctx.env, roof + new Vector3(0, 3f, 0), new Color(1f, 0.75f, 0.9f), 1.6f, 12f);
            AddStemZone(ctx, "Roof", roof + new Vector3(0f, 1f, 0f), new Vector3(13f, 6f, 9f), StemMix.Quiet);

            Transform roofArrival = new GameObject("RoofArrival").transform;
            roofArrival.SetParent(ctx.story, false);
            roofArrival.position = roof + new Vector3(-4.5f, 0.05f, -2.5f);
            roofArrival.rotation = Quaternion.Euler(0, 45, 0);
            Transform streetReturn = new GameObject("StreetReturn").transform;
            streetReturn.SetParent(ctx.story, false);
            streetReturn.position = new Vector3(14.6f, 0.05f, 2.2f);
            streetReturn.rotation = Quaternion.Euler(0, 180, 0);

            GameObject fireEscape = Block("Prop_FireEscape", ctx.props, new Vector3(14.6f, 1.6f, 3.9f), new Vector3(1.2f, 3.2f, 0.2f), poleMat, ctx.interactableLayer);
            var up = fireEscape.AddComponent<AreaTeleport>();
            Set(up, "destination", roofArrival);
            Set(up, "requiredFlag", "got_care_package");
            SetStrings(up, "lockedLines", L("* A fire escape. It goes all the way up. {p}Not yet."));
            SetStrings(up, "leavingLines", L("* Kira's already halfway up, skates over her shoulder. {p}Byte is somehow ahead of her."));
            Set(up, "flagOnUse", "visited_roof");
            Set(up, "audioSource", ctx.sfx);
            Set(up, "useClip", Clip("switch_003"));
            GameObject ladder = Block("Prop_Ladder", ctx.props, roof + new Vector3(-5.6f, 0.8f, -3.6f), new Vector3(0.8f, 1.6f, 0.2f), poleMat, ctx.interactableLayer);
            var down = ladder.AddComponent<AreaTeleport>();
            Set(down, "destination", streetReturn);
            Set(down, "audioSource", ctx.sfx);
            Set(down, "useClip", Clip("switch_003"));

            GameObject roofKira = Character(ctx, "Kira", "character-female-c", "mini-characters", roof + new Vector3(1.5f, 0, 1.2f), 200f, 1.05f, null, true);
            roofKira.name = "NPC_Kira_Roof";
            Inspectable(roofKira, "Kira_Roof",
                L("No deliveries. No route. {p}Just a roof and too many lights.", "Sit anywhere. {p}{small}Or don't. Nobody's timing you up here.{/small}"),
                L("I come up here when the route gets loud. {p}It never gets quiet. But it gets far."), null, spkKira);

            // Three places to sit, three conversations (no objective, no prompt).
            Prop(ctx, "RoofCrate", "cardboardBoxClosed", F, roof + new Vector3(-1.5f, 0, 2.6f), 15, 0.6f, true,
                L("* You sit on the crate. Kira sits on the next one, swinging her skates.",
                  "Fun fact: I've delivered to every building you can see from here. {p}{small}Except one.{/small}",
                  "Don't ask which one. {p}{fast}You're going to ask. Don't ask.{/fast}"),
                L("* The crate creaks. Kira pretends it was her."), "roof_seat_crate;+closeness#roof_seat_crate", true, spkKira);
            Prop(ctx, "RoofLedge", "bench", F, roof + new Vector3(3.8f, 0, -2.8f), 180, 1.4f, true,
                L("* You sit on the ledge. Byte sits next to you, tail hanging over the drop like it's nothing.",
                  "dont look down. {p}look out. {p}{small}different thing.{/small}"),
                L("* Byte's tail swings over the edge. {p}He is very obviously not worried. {p}You are, a little."), "roof_seat_ledge;+closeness#roof_seat_ledge", true, spkByte);
            Prop(ctx, "RoofTank", "chair", F, roof + new Vector3(-3.2f, 0, 1.2f), 120, 0.8f, true,
                L("* You sit under the water tank. Kira drops down next to you. Then Byte. Then nobody says anything.",
                  "...This is nice. {p}{slow}Is it weird that this is nice?{/slow}"),
                L("* Still nice. {p}Still a little weird."), "roof_seat_tank;+closeness#roof_seat_tank", true, spkKira);

            // Stay still for 30 seconds and Kira starts talking about the hospital, in pieces.
            DialogueSequence hospital = SeqAsset("Act2/Kira_Hospital",
                Say(null, "Nobody moves. The signs below change color, one by one."),
                Say(spkKira, "{small}I used to work nights at a hospital.{/small}"),
                Say(spkKira, "Not a doctor. {p}I pushed the trolleys. Brought people their dinner. {p}Their mail."),
                Say(spkKira, "There was this one guy. Room 9. {p}Nobody ever came. {p}So I started delivering him things that weren't for him."),
                Say(spkKira, "Flyers. Menus. A postcard I wrote myself and signed 'your cousin'. {p}{wave}He knew. He kept it anyway.{/wave}"),
                Say(spkKira, "And then one night room 9 was empty. {p=1}{slow}And I just... kept walking.{/slow}"),
                Say(spkKira, "{fast}Out of the ward, out of the building, onto the skates, and I haven't stopped since, so. Ha.{/fast}"),
                Say(spkKira, "...Sorry. {p}That's a lot of words for a roof."),
                Say(spkByte, "{small}no it wasnt.{/small}", "", "", "+closeness"));
            var idleZone = new GameObject("RoofStillness");
            idleZone.transform.SetParent(ctx.story, false);
            idleZone.transform.position = roof + new Vector3(0, 1f, 0);
            var idleBox = idleZone.AddComponent<BoxCollider>();
            idleBox.size = new Vector3(12f, 3f, 8f);
            idleBox.isTrigger = true;
            var idle = idleZone.AddComponent<IdleScene>();
            Set(idle, "sequence", hospital);
            Set(idle, "flagWhenPlayed", "kira_hospital_story");
            Set(idle, "secondsOfStillness", 30f);

            Event(ctx, "RoofInvite", StoryEvent.TriggerMode.OnFlag, "got_care_package", 1.2f, "KIRA",
                L("roof. fire escape by the freed guy. come up. no reason."), null, null, string.Empty);

            DialogueSequence walkByte = SeqAsset("Act2/Walk_Byte",
                Say(spkByte, "so. {p}leo. {p}what do you do. {p}{small}besides look sad at vending machines.{/small}"),
                Ask(spkLeo, "...",
                    Opt("[\"I move boxes. In a warehouse.\"]", "boxes"),
                    Opt("[\"I don't really know anymore.\"]", "dunno", "+openness#walk_dunno")),
                Say(spkByte, "boxes. {p}{bounce}nice.{/bounce} {p}cats respect boxes. {p}{small}youre basically a cat landlord.{/small}", "boxes", "end"),
                Say(spkByte, "thats fine. {p}i dont know either. {p}{small}i just walk next to people until it makes sense.{/small}", "dunno"),
                Say(spkByte, "anyway. {p}stay close. this district bites. {p}{small}not me. i nibble.{/small}", "end", "", "+closeness#walk_byte"));
            Event(ctx, "WalkByte", StoryEvent.TriggerMode.OnPlayerEnter, "met_byte", 0.2f, null, null, null, null, "walk_byte",
                new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 9f), walkByte);

            // ---------------------------------------------------------------- Buildings you can enter
            BuildNeonMart(ctx, new Vector3(5.8f, 0f, 4.72f), LitMaterial("Houses/M_Facade_Mart", new Color(0.24f, 0.12f, 0.34f)));
            BuildApartment0451(ctx, new Vector3(18.8f, 0f, 4.72f), LitMaterial("Houses/M_Facade_0451", new Color(0.4f, 0.42f, 0.46f)));
            LockedDoor(ctx, "BackIn5", new Vector3(-12.6f, 0f, 4.72f), 180f, LitMaterial("Houses/M_Door_Closed", new Color(0.5f, 0.2f, 0.3f)), Magenta,
                LitMaterial("Houses/M_Facade_Closed", new Color(0.3f, 0.2f, 0.35f)),
                L("* A note on the door: BACK IN 5 MINUTES.", "* The note is dated 1998."));
            Event(ctx, "GiveTape", StoryEvent.TriggerMode.OnPlayerEnter, "key.voicemail_tape", 0.1f, null, null,
                L("* You hand him the tape. He listens. {p}Then he listens again.",
                  "* The third time, he sits down on the curb and laughs. Then cries. Then laughs.",
                  "...The piece with the most frosting. {p}{slow}She saved me the piece with the most frosting.{/slow}",
                  "I'm going home. {p}Saturday isn't over yet.",
                  "{small}Here. From the vacation jar. {p}You need a vacation more than I do.{/small}"),
                spkCommuter, "gave_tape;+openness#gave_tape;$6#gave_tape_tokens",
                new Vector3(13.2f, 1f, -2.8f), new Vector3(3.2f, 2f, 3.2f));

            ExitZone(ctx, "Exit_ToConcretePark", new Vector3(22.3f, 1f, 0f), new Vector3(1.5f, 2f, 8.4f), ParkScene, "ch4", "hack1_done",
                L("* The street goes on, but something's still blocking Kira's route."));

            // Arrival cinematic
            Transform points = new GameObject("CinematicPoints").transform;
            Transform cp1 = CinematicPoint("CP_Sky", points, new Vector3(-6f, 10f, -10f), new Vector3(-10f, 0f, 2f));
            Transform cp2 = CinematicPoint("CP_Street", points, new Vector3(-17f, 1.4f, -3.2f), new Vector3(-11f, 0.8f, 1f));
            CinematicSequence arrival = Cinematic("Cinematic_Arrival_Subnet", ctx.story,
                Shot("Sky", cp1, 0f, 1.5f, 34f, null, null, true, Clip("glitch_001")),
                Shot("Street", cp2, 5f, 0.5f, 26f,
                    L("...This isn't Central Station.",
                      "Everything is the right shape, but the wrong color. Like a memory of a city, drawn by someone who missed it.",
                      "Somewhere ahead, a cat is waiting. I don't know how I know that.")));
            arrival.fadeFromBlack = true;
            arrival.blackHoldBeforeFade = 0.8f;
            arrival.fadeInDuration = 2.5f;
            arrival.blendToGameplay = true;
            arrival.blendToGameplayDuration = 2f;
            EditorUtility.SetDirty(arrival);
            SetIntro(ctx, arrival);

            EndGameplayScene(ctx, SubnetScene);
        }
    }
}
#endif
