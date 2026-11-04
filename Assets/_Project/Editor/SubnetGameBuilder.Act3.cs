#if UNITY_EDITOR
using System.Collections.Generic;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Hacking;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== 04_ConcretePark (Act III)

        private static void BuildParkScene()
        {
            VolumeProfile profile = LookProfile("PP_Subnet_ConcretePark", -25f, 0.9f, 0.42f, 0.22f, 0.15f, new Color(0.88f, 0.86f, 1f));
            Ctx ctx = BeginGameplayScene(new Vector3(-12f, 0f, 0f), 90f, profile,
                new Color(0.22f, 0.22f, 0.3f), new Color(0.75f, 0.75f, 0.95f), 0.5f, false);

            AddSceneMusic(true);
            ctx.removeAmbience = true;
            ctx.ambience.masterVolume = 0.45f;
            ctx.ambience.wind = 0.5f;
            ctx.ambience.pad = 0.22f;
            ctx.ambience.padNotes = new[] { 196f, 233.08f, 293.66f, 349.23f };
            ctx.ambience.hum = 0.08f;
            ctx.ambience.rain = 0.05f;

            Material concrete = LitMaterial("Subnet/M_Concrete", new Color(0.46f, 0.46f, 0.49f));
            Material concreteDark = LitMaterial("Subnet/M_Concrete_Dark", new Color(0.3f, 0.3f, 0.34f));
            Material stone = LitMaterial("Subnet/M_StatueStone", new Color(0.58f, 0.58f, 0.6f), 0.05f);
            Material tearMat = ParticleMaterial("Subnet/M_PixelTears", new Color(0.02f, 0.02f, 0.03f, 1f));
            Material mapMat = EmissiveMaterial("Subnet/M_ParkMap", new Color(0.1f, 0.12f, 0.15f), new Color(0.5f, 0.8f, 1f) * 1.2f);
            Material lampGlass = EmissiveMaterial("Subnet/M_LampGlass", new Color(0.4f, 0.35f, 0.25f), Color.black);
            Material receiptMat = LitMaterial("Subnet/M_Receipts", new Color(0.95f, 0.95f, 0.9f));

            // Plaza x -14..8.5, corridor x 8.5..14
            Block("Plaza", ctx.env, new Vector3(-2.75f, -0.1f, 0f), new Vector3(23f, 0.2f, 14.4f), concrete, ctx.groundLayer);
            Block("CorridorFloor", ctx.env, new Vector3(11.5f, -0.1f, 0f), new Vector3(6.4f, 0.2f, 4f), concreteDark, ctx.groundLayer);
            Block("Wall_North", ctx.env, new Vector3(-2.75f, 0.6f, 7.3f), new Vector3(23f, 1.2f, 0.4f), concreteDark, 0);
            Block("Bound_South", ctx.env, new Vector3(-2.75f, 1f, -7.3f), new Vector3(23f, 2f, 0.2f), null, 0);
            Block("Bound_West", ctx.env, new Vector3(-14.3f, 1f, 0f), new Vector3(0.2f, 2f, 14.4f), null, 0);
            Block("EastWall_North", ctx.env, new Vector3(8.8f, 1.25f, 4.4f), new Vector3(0.6f, 2.5f, 5.8f), concreteDark, 0);
            Block("EastWall_South", ctx.env, new Vector3(8.8f, 1.25f, -4.4f), new Vector3(0.6f, 2.5f, 5.8f), concreteDark, 0);
            Block("Corridor_North", ctx.env, new Vector3(11.7f, 1.25f, 1.9f), new Vector3(5.8f, 2.5f, 0.3f), concreteDark, 0);
            Block("Corridor_South", ctx.env, new Vector3(11.7f, 1f, -1.9f), new Vector3(5.8f, 2f, 0.2f), null, 0);
            Block("Corridor_End", ctx.env, new Vector3(14.6f, 1f, 0f), new Vector3(0.2f, 2f, 4f), null, 0);

            // Park map (the hint)
            GameObject map = Block("Prop_ParkMap", ctx.props, new Vector3(-9.5f, 1f, 2.2f), new Vector3(1.4f, 1f, 0.1f), mapMat, ctx.interactableLayer);
            Inspectable(map, "ParkMap",
                L("A park map. YOU ARE HERE, it says, with an arrow pointing at a lonely dot.",
                  "Someone scratched a note into the plastic: <color=#B9A7FF>'They gave up in order. Light their lamps in the same order, and maybe they'll remember.'</color>"),
                L("'They gave up in order.' First, then, last."));
            Deco("MapPole", ctx.props, new Vector3(-9.5f, 0.25f, 2.25f), new Vector3(0.1f, 0.5f, 0.1f), concreteDark);

            // Three weeping statues + their lamps.
            var lamps = new Dictionary<string, PuzzleSwitch>();
            (string key, string title, Vector3 pos, string model, string[] lines)[] statues =
            {
                ("Stayed", "THE ONE WHO STAYED", new Vector3(-5f, 0f, 3.6f), "character-female-b",
                    L("Last, I stopped answering the door. Staying in was easier than being seen.",
                      "* The plaque reads: THE ONE WHO STAYED. Black pixels drip from its eyes and dissolve before they hit the ground.")),
                ("Waited", "THE ONE WHO WAITED", new Vector3(0f, 0f, 4.6f), "character-male-a",
                    L("First, I stopped calling. I told myself they'd call me. They were waiting too.",
                      "* The plaque reads: THE ONE WHO WAITED. Its hand is still raised, like it's holding a phone that isn't there.")),
                ("Left", "THE ONE WHO LEFT", new Vector3(5f, 0f, 3.6f), "character-male-c",
                    L("Then, I stopped visiting. The train ride felt longer every year. One year it felt too long.",
                      "* The plaque reads: THE ONE WHO LEFT. It's facing the station. Of course it is.")),
            };

            foreach (var s in statues)
            {
                GameObject pedestal = Block("Pedestal_" + s.key, ctx.props, s.pos + new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.6f, 1.2f), concreteDark, ctx.interactableLayer);
                Inspectable(pedestal, "Statue_" + s.key, s.lines, L("* It's still crying."), null, spkStatue);
                var holder = new GameObject("Statue_" + s.key);
                holder.transform.SetParent(ctx.props, true);
                holder.transform.position = s.pos + new Vector3(0, 0.6f, 0);
                holder.transform.rotation = Quaternion.Euler(0, 180, 0);
                GameObject statue = SpawnCharacterModel(s.model, "mini-characters", holder.transform, 1.15f, out Animator anim);
                TintModel(statue, stone);
                if (anim != null) anim.enabled = false; // frozen in place
                PixelTears(holder.transform, s.pos + new Vector3(0, 1.45f, -0.1f), tearMat, 10f);

                GameObject lamp = Prop(ctx, "Lamp_" + s.key, "lampSquareFloor", "furniture", s.pos + new Vector3(1.1f, 0f, -1.5f), 0, 1.8f, false, null);
                lamp.layer = ctx.interactableLayer;
                Light light = PointLight(lamp.transform, lamp.transform.position + Vector3.up * 1.7f, new Color(1f, 0.82f, 0.55f), 2.5f, 4.5f);
                var sw = lamp.AddComponent<PuzzleSwitch>();
                Set(sw, "switchLight", light);
                lamps[s.key] = sw;
            }

            var puzzle = new GameObject("SequencePuzzle_Lamps").AddComponent<SequencePuzzle>();
            puzzle.transform.SetParent(ctx.story, false);
            var so = new SerializedObject(puzzle);
            SerializedProperty order = so.FindProperty("correctOrder");
            PuzzleSwitch[] correct = { lamps["Waited"], lamps["Left"], lamps["Stayed"] };
            order.arraySize = correct.Length;
            for (int i = 0; i < correct.Length; i++) order.GetArrayElementAtIndex(i).objectReferenceValue = correct[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(puzzle, "flagOnSolved", "park_puzzle_solved");
            SetStrings(puzzle, "wrongLines", L("* The lamps gutter out. The statues sob louder. Wrong order.", "* 'They gave up in order.' First... then... last."));
            SetStrings(puzzle, "solvedLines", L("* All three lamps burn warm. For a moment, the statues stop crying.", "* Somewhere to the east, concrete cracks open."));
            Set(puzzle, "audioSource", ctx.sfx);
            Set(puzzle, "correctClip", Clip("switch_003"));
            Set(puzzle, "wrongClip", Clip("error_002"));
            Set(puzzle, "solvedClip", Clip("confirmation_003"));

            // Barrier that opens with the puzzle.
            GameObject barrier = Block("ConcreteBarrier", ctx.props, new Vector3(8.8f, 1.25f, 0f), new Vector3(0.7f, 2.5f, 3f), concreteDark, ctx.interactableLayer);
            Inspectable(barrier, "Barrier",
                L("A concrete barrier, poured over something that wanted to grow. It's cold to the touch, like a shoulder turned away.",
                  "* There's a faint warmth behind it. Like someone crying very quietly."),
                L("* Still sealed. The statues hold the key, somehow."));
            var gate = barrier.AddComponent<FlagGate>();
            Set(gate, "openFlag", "park_puzzle_solved");
            Set(gate, "dissolveColor", new Color(0.8f, 0.85f, 1f));

            // Props
            const string F = "furniture";
            Prop(ctx, "Bench", "bench", F, new Vector3(-7f, 0f, -3.5f), 0, 1.6f, true,
                L("A concrete bench. Someone carved 'I WAS HERE' into it, then, underneath, smaller: 'I think.'"),
                L("I WAS HERE. I think."));
            Prop(ctx, "Bench2", "bench", F, new Vector3(3f, 0f, -4f), 0, 1.6f, true,
                L("A bench facing nothing. The best kind of bench, honestly."),
                L("You sit. Nothing happens. It's nice."));
            Prop(ctx, "Planter", "pottedPlant", F, new Vector3(-2.5f, 0f, -1.5f), 0, 2f, false,
                L("A concrete planter. The tree inside is made of rebar. It's doing its best."),
                L("A single real leaf has grown on one of the iron branches. Don't tell anyone."));
            Prop(ctx, "Trash", "trashcan", F, new Vector3(6.5f, 0f, -2.5f), 0, 0.6f, false,
                L("A trash can full of birthday cards that were bought and never sent."),
                L("'Happy birthday! Sorry I'm late.' Every single one."));
            GameObject pigeon = Character(ctx, "ReceiptPigeon", "animal-chick", "cube-pets", new Vector3(-4f, 0f, -4.5f), 30f, 0.35f, receiptMat, false);
            Inspectable(pigeon, "ReceiptPigeon",
                L("A pigeon made of crumpled receipts. It coos the total of everything you bought this year.", "* That's... more than I thought."),
                L("Coo. Forty-three ninety-nine. Coo."), null, spkObject);

            // Byte follows from the start of the chapter.
            GameObject byteNpc = Character(ctx, "Byte", "animal-cat", "cube-pets", new Vector3(-13f, 0f, -1f), 90f, 0.45f, null, false);
            Inspectable(byteNpc, "Byte_Park",
                L("This park used to have kids in it. Now it has statues and a pigeon with a spending problem.",
                  "Read the plaques. Sad things always leave instructions."),
                L("first. then. last. i'm a cat, not a puzzle book."), null, spkByte);
            Follower(byteNpc, string.Empty, -0.6f);

            // The Weeping Commuter (Obstacle Hacking #2)
            var obstacleRoot = new GameObject("Obstacle_WeepingCommuter");
            obstacleRoot.transform.SetParent(ctx.story, false);
            obstacleRoot.transform.position = new Vector3(11.2f, 0f, 0f);
            obstacleRoot.layer = ctx.interactableLayer;
            var col = obstacleRoot.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.size = new Vector3(1.2f, 2f, 3.8f);
            var shell = new GameObject("StoneShell");
            shell.transform.SetParent(obstacleRoot.transform, false);
            GameObject weeper = Character(ctx, "Weeper", "character-female-d", "mini-characters", obstacleRoot.transform.position, -90f, 1.1f, stone, false, shell.transform);
            Object.DestroyImmediate(weeper.GetComponent<Collider>());
            Animator weeperAnim = weeper.GetComponentInChildren<Animator>();
            if (weeperAnim != null) weeperAnim.enabled = false;
            PixelTears(shell.transform, obstacleRoot.transform.position + new Vector3(-0.1f, 0.95f, 0), tearMat, 30f);
            for (int i = 0; i < 6; i++)
            {
                Deco("Rubble", shell.transform, obstacleRoot.transform.position + new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, Random.Range(-1.6f, 1.6f)),
                    new Vector3(0.3f, 0.2f, 0.3f), stone, new Vector3(0, Random.Range(0, 90), 0));
            }
            PointLight(obstacleRoot.transform, obstacleRoot.transform.position + new Vector3(-1.2f, 1.6f, 0), new Color(0.7f, 0.75f, 1f), 2f, 5f);

            GameObject freed = Character(ctx, "FreedWeeper", "character-female-d", "mini-characters", new Vector3(12.6f, 0f, -1.1f), -130f, 1.05f, null, false);
            Inspectable(freed, "FreedWeeper",
                L("I can feel my hands again. They're shaking. That's okay. That means they're mine.",
                  "I'm going to call my sister. I don't know what I'll say. Maybe I'll just let her laugh at me."),
                L("Summer rain. I forgot it had a smell."), null, spkCommuter);

            GameObject kiraNpc = Character(ctx, "Kira", "character-female-c", "mini-characters", new Vector3(12.8f, 0f, 1.1f), -110f, 1.05f, null, false);
            Inspectable(kiraNpc, "Kira_Park", L("Ready when you are. The Core's just ahead. I can feel it humming in my teeth."), null, null, spkKira);

            HackableObstacle weepHack = Hackable(obstacleRoot, "WEEPING_COMMUTER.sys  //  'PETRIFIED'", spkStatue,
                L("...",
                  "Don't look at me. I'm not crying. Statues don't cry. Statues are fine.",
                  "* She's carved out of the same stone as the others. But the pixels running down her face are still warm.",
                  "* My handheld finds a memory inside her. Shattered into pieces."),
                new[]
                {
                    new HackWave(HackPattern.Ripple, 9f, 1f),
                    new HackWave(HackPattern.Spiral, 9f, 1f),
                    new HackWave(HackPattern.Aimed, 10f, 1f),
                },
                new[]
                {
                    new HackPuzzle("A broken memory: '...the smell of ____ on hot asphalt.' Reassemble it.",
                        new[] { "[EXHAUST FUMES]", "[SUMMER RAIN]", "[BURNT COFFEE]" }, 1,
                        "The stone around her shoulders softens. 'Summer rain... We used to run through it. On purpose.'",
                        "The fragment doesn't fit. The stone gets a little colder."),
                    new HackPuzzle("Another piece: '...my sister laughing at ____.'",
                        new[] { "[MY PRESENTATION]", "[THE EVENING NEWS]", "[A DOG CHASING ITS OWN TAIL]" }, 2,
                        "A crack runs down the stone. Inside, someone laughs, small and surprised.",
                        "'No... she never laughed at that.' The pixels fall faster."),
                    new HackPuzzle("The last piece. She whispers: 'I'm not broken. I'm ____.'",
                        new[] { "[OBSOLETE]", "[TIRED, AND THAT'S ALLOWED]", "[FINE. I'M FINE.]" }, 1,
                        "'...Tired. And that's allowed.' She says it twice, like she's trying it on.",
                        "'Fine.' The word lands like another layer of concrete."),
                },
                L("The stone cracks open. Underneath, a person. Breathing.",
                  "Thank you. I forgot I was allowed to cry and keep going at the same time.",
                  "* Warm pixels drift up into the gray sky. The corridor ahead is clear."),
                "park_puzzle_solved", L("* Sealed behind concrete."), "hack2_done", new[] { shell }, new[] { freed, kiraNpc });
            weepHack.portraitTarget = weeper.transform;
            weepHack.portraitDistance = 2.2f;
            weepHack.tauntLines = L("Don't look at me.", "I'm fine. I'm FINE.", "Statues don't cry.",
                "Everyone else is coping. Why can't I?", "Go away... no, wait.", "It's just concrete in my eyes.");
            weepHack.betweenRoundLines = L("...We ran through the rain on purpose. Mom was so mad. We were so happy.",
                "She laughed so hard she snorted. I'd forgotten that sound.");
            EditorUtility.SetDirty(weepHack);

            Event(ctx, "Arrive", StoryEvent.TriggerMode.OnStart, string.Empty, 1.2f, "BYTE",
                L("statues cry in order. first, then, last. trust me im a cat."),
                L("* The Concrete Park. Even the air feels poured into a mold.",
                  "* Three statues are weeping black pixels onto the pavement."), null, string.Empty);
            Event(ctx, "AfterHack2", StoryEvent.TriggerMode.OnFlag, "hack2_done", 1.5f, "BYTE",
                L("the core is next. its loud in there. stay close."),
                L("Hey! I followed the sound of someone being kind. It's rare enough to echo.",
                  "I've been delivering care packages for months. Never opened one. Never thought one could be addressed to me.",
                  "...Mind if I come with you to the Core? I think I need to stop running in circles.",
                  "* (KIRA joined you.)"),
                spkKira, "kira_joined");

            ExitZone(ctx, "Exit_ToCentralCore", new Vector3(13.9f, 1f, 0f), new Vector3(1f, 2f, 3.6f), CoreScene, "ch5", "hack2_done",
                L("* Not yet."));

            EndGameplayScene(ctx, ParkScene);
        }
    }
}
#endif
