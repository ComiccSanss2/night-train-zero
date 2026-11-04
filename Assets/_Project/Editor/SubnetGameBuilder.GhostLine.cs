#if UNITY_EDITOR
using System.Collections.Generic;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Hacking;
using Subnet.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Subnet.EditorTools
{
    /// <summary>
    /// Act IV (author's bible): the Ghost Line. Night Train Zero heads for the Core; the passengers are Leo's silences.
    /// Halfway down the tunnel the Algorithm starts purging the oldest memories, and Byte is an old memory.
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        public const string GhostLineScene = "04b_GhostLine";

        private static void BuildGhostLineScene()
        {
            VolumeProfile profile = LookProfile("PP_Subnet_GhostLine", 4f, 1.5f, 0.5f, 0.25f, 0.35f, new Color(0.85f, 0.86f, 1f));
            Ctx ctx = BeginGameplayScene(new Vector3(-8.2f, 0f, 0f), 90f, profile,
                new Color(0.06f, 0.06f, 0.11f), new Color(0.55f, 0.6f, 0.95f), 0.15f, false);
            AddSceneMusic(true);
            ctx.removeAmbience = true;

            Material floorMat = LitMaterial("Subnet/M_Ghost_Floor", new Color(0.16f, 0.15f, 0.22f), 0.5f);
            Material wallMat = LitMaterial("Subnet/M_Ghost_Wall", new Color(0.32f, 0.3f, 0.42f));
            Material seatMat = LitMaterial("Subnet/M_Ghost_Seat", new Color(0.42f, 0.24f, 0.4f));
            Material windowMat = EmissiveMaterial("Subnet/M_Ghost_Window", new Color(0.02f, 0.02f, 0.05f), new Color(0.25f, 0.3f, 0.8f) * 0.6f);
            Material ghostMat = EmissiveMaterial("Subnet/M_Ghost_Passenger", new Color(0.78f, 0.8f, 0.95f), new Color(0.35f, 0.4f, 0.7f) * 0.5f);
            Material lightMat = EmissiveMaterial("Subnet/M_Ghost_Light", new Color(0.9f, 0.85f, 1f), new Color(1f, 0.9f, 1f) * 2f);
            Material staticMat = EmissiveMaterial("Subnet/M_Ghost_Static", new Color(0.2f, 0.2f, 0.25f), new Color(0.8f, 0.85f, 1f) * 1.4f);
            Material collarMat = EmissiveMaterial("Subnet/M_Byte_Collar", new Color(0.1f, 0.3f, 0.3f), Cyan * 3f);

            // ---------------------------------------------------------------- The carriage
            Block("Floor", ctx.env, new Vector3(0f, -0.1f, 0f), new Vector3(20.4f, 0.2f, 3.8f), floorMat, ctx.groundLayer);
            Block("Wall_N", ctx.env, new Vector3(0f, 1.3f, 1.95f), new Vector3(20.4f, 2.6f, 0.1f), wallMat, 0);
            Block("Wall_S", ctx.env, new Vector3(0f, 1.3f, -1.95f), new Vector3(20.4f, 2.6f, 0.1f), null, 0); // invisible: the camera looks in from here
            Block("Wall_W", ctx.env, new Vector3(-10.2f, 1.3f, 0f), new Vector3(0.1f, 2.6f, 3.8f), wallMat, 0);
            Block("Wall_E", ctx.env, new Vector3(10.2f, 1.3f, 0f), new Vector3(0.1f, 2.6f, 3.8f), wallMat, 0);
            for (float x = -8f; x <= 8f; x += 4f)
            {
                Deco("Window", ctx.env, new Vector3(x, 1.55f, 1.89f), new Vector3(2.4f, 0.9f, 0.02f), windowMat);
                Deco("CeilingLight", ctx.env, new Vector3(x, 2.55f, 0f), new Vector3(1.6f, 0.05f, 0.25f), lightMat);
                PointLight(ctx.env, new Vector3(x, 2.3f, 0f), new Color(0.85f, 0.8f, 1f), 1.1f, 4.5f);
            }
            var seats = new List<Vector3>();
            for (float x = -8.5f; x <= 8.5f; x += 2.4f)
            {
                Block("Seat", ctx.props, new Vector3(x, 0.24f, 1.5f), new Vector3(1.6f, 0.48f, 0.6f), seatMat, 0);
                Deco("SeatBack", ctx.props, new Vector3(x, 0.75f, 1.78f), new Vector3(1.6f, 0.6f, 0.12f), seatMat);
                seats.Add(new Vector3(x, 0.5f, 1.5f));
            }

            // ---------------------------------------------------------------- The passengers: Leo's silences
            GameObject mom = Character(ctx, "Mom", "character-female-b", "mini-characters", new Vector3(-4.4f, 0f, 1.1f), 180f, 1.0f, ghostMat);
            Inspectable(mom, "Ghost_Mom",
                L("Mm. {p}Mm-hm. {p}No, it's fine.",
                  "{slow}Do what you want, then.{/slow}",
                  "* Mom, on the phone. The last time we talked. {p}She never once said don't go."),
                L("* She's still holding the phone. {p}Waiting for the part where I say something back."), "+openness#ghost_mom", spkObject);
            GameObject friend = Character(ctx, "Friend", "character-male-d", "mini-characters", new Vector3(-0.2f, 0f, 1.1f), 200f, 1.0f, ghostMat);
            Inspectable(friend, "Ghost_Friend",
                L("We said we'd start a band. {p}You moved. {p}{small}I still have the drumsticks.{/small}",
                  "* Dani. High school. I never answered his last message. {p}It was a photo of the drumsticks."),
                L("* The drumsticks are on his lap. He taps the seat. {p}One, two. {p}One, two."), "+openness#ghost_friend", spkObject);
            GameObject mia = Character(ctx, "Mia", "character-female-e", "mini-characters", new Vector3(3.8f, 0f, 1.1f), 190f, 0.72f, ghostMat);
            Inspectable(mia, "Ghost_Mia",
                L("Are you coming to my recital? {p}You have to clap the loudest. {p}{bounce}Promise?{/bounce}",
                  "* Mia. Nine years old. Front teeth missing. {p}I said yes. {p}{slow}I said yes for three years.{/slow}"),
                L("* She's practicing the bow. Over and over. {p}For someone in the front row."), "+openness#ghost_mia", spkObject);

            // ---------------------------------------------------------------- Companions
            GameObject byteNpc = Character(ctx, "Byte", "animal-cat", "cube-pets", new Vector3(-9.2f, 0f, -0.8f), 90f, 0.45f, null, false);
            Inspectable(byteNpc, "Byte_Train",
                L("this train smells like old phone calls.", "dont sit there. {p}{small}some seats are taken even when theyre empty.{/small}"),
                L("im fine. {p}cats are always fine. {p}{small}its a whole thing.{/small}"), null, spkByte);
            Follower(byteNpc, string.Empty, -0.6f);
            GameObject kiraNpc = Character(ctx, "Kira", "character-female-c", "mini-characters", new Vector3(-9.2f, 0f, 0.8f), 90f, 1.05f, null, false);
            Inspectable(kiraNpc, "Kira_Train",
                L("Nobody's getting off. {p}{small}I keep checking.{/small}"),
                L("Every stop is just more tunnel. {p}I hate that I'm used to it."), null, spkKira);
            Follower(kiraNpc, string.Empty, 0.75f);

            // A truthful voice for one line: Byte, for once, with capital letters.
            DialogueSpeaker byteTrue = CreateSpeaker("Speaker_ByteTrue", "BYTE", "click_004", new Vector2(1.2f, 1.3f), 0.55f, new Color(0.65f, 1f, 0.78f),
                SpeakerStyle.Default, new Color(0.9f, 1f, 0.94f));

            // ---------------------------------------------------------------- The stop
            DialogueNode askTwo = Ask(spkByte, "leo. {p}im a hallucination with a collar. {p}this is the least weird thing ive done all night.",
                Opt("[\"I said no.\"]", "no2", "+closeness#byte_no2"),
                Opt("[...]", "let"));
            askTwo.id = "no1";
            DialogueNode askThree = Ask(byteTrue, "Leo. {p=1.2}{slow}Please.{/slow}",
                Opt("[Let him go]", "let"));
            askThree.id = "no2";
            DialogueSequence goodbye = SeqAsset("Act4/Byte_Goodbye",
                Say(null, "The train stops. {p}{quake}Halfway down the tunnel."),
                Say(spkAlgorithm, "MEMORY PURGE IN PROGRESS. {p}OLDEST FILES FIRST."),
                Say(null, "At the back of the carriage, the seats start going grey. {p}One row at a time."),
                Say(spkByte, "huh. {p}oldest files first."),
                Say(spkByte, "guess whos the oldest file in here. {p}{small}hint: collar.{/small}"),
                Say(spkKira, "What does that... {p}Byte. {p}{shake}What does that mean.{/shake}"),
                Ask(spkByte, "someone has to stay back and be in its way. {p}cats are {bounce}incredible{/bounce} at being in the way.",
                    Opt("[\"No.\"]", "no1", "+closeness#byte_no1"),
                    Opt("[...]", "let")),
                askTwo,
                askThree,
                Say(spkByte, "thanks. {p}{small}...dont make it weird.{/small}", "let"),
                Say(spkKira, "{small}...I'll walk with you the rest of the way.{/small} {p}Okay? {p}Okay.", "", "", "byte_stays"));
            Event(ctx, "TrainStops", StoryEvent.TriggerMode.OnPlayerEnter, string.Empty, 0.2f, null, null, null, null, "train_stopped",
                new Vector3(5.6f, 1f, 0f), new Vector3(1f, 2f, 3.6f), goodbye);

            // ---------------------------------------------------------------- The grief hack (can't be won by dodging)
            var doorRoot = new GameObject("Obstacle_MemoryPurge");
            doorRoot.transform.SetParent(ctx.props, false);
            doorRoot.transform.position = new Vector3(9.2f, 0f, 0f);
            doorRoot.layer = ctx.interactableLayer;
            GameObject purge = Block("PurgeStatic", doorRoot.transform, doorRoot.transform.position + new Vector3(0f, 1.3f, 0f), new Vector3(0.4f, 2.6f, 3.6f), staticMat, ctx.interactableLayer);
            PixelTears(purge.transform, doorRoot.transform.position + new Vector3(0f, 2.2f, 0f), staticMat, 25f);

            GameObject collar = Block("Prop_Collar", ctx.props, seats[0] + new Vector3(0.2f, 0.03f, -0.05f), new Vector3(0.28f, 0.05f, 0.18f), collarMat, ctx.interactableLayer);
            Inspectable(collar, "Collar",
                L("* Byte's collar. Left on the seat. {p}The tiny screen is scrolling old texts from 2003. {p}It's still warm."),
                L("* still here. kinda. {p}The screen says it over and over, in lowercase."), null);
            collar.SetActive(false);

            HackableObstacle griefHack = Hackable(doorRoot, "MEMORY_PURGE.exe  //  'OLDEST FILES FIRST'", spkAlgorithm,
                L("* Behind me, Byte sits down in the aisle and faces the grey.",
                  "* Byte, from the back: {p}go. ill be loud about it.",
                  "OLDEST FILES FIRST."),
                new[]
                {
                    new HackWave(HackPattern.Crossfire, 9f, 1.3f),
                    new HackWave(HackPattern.Spiral, 9f, 1.4f),
                    new HackWave(HackPattern.Aimed, 9f, 1.6f),
                    new HackWave(HackPattern.Ripple, 10f, 1.3f, true),
                },
                new HackPuzzle[0],
                L("* The static lets go of the door.",
                  "* Behind me, the aisle is empty."),
                "byte_stays",
                L("* The door to the next carriage is sealed with static. {p}Something at the back is getting closer."),
                "ghostline_done", new[] { purge, byteNpc }, new[] { collar });
            griefHack.portraitTarget = purge.transform;
            griefHack.portraitDistance = 3.2f;
            griefHack.fadeCompanion = true;
            griefHack.companionLabel = "byte";
            griefHack.tauntLines = L("OLDEST FILES FIRST.", "A CAT. HOW SENTIMENTAL.", "HE WAS NEVER REAL.", "LETTING GO IS EFFICIENT.", "YOU CANNOT OUTRUN THIS.");
            griefHack.betweenRoundLines = L("* Somewhere behind me, a collar beeps. {p}Once.",
                "* The collar beeps again. {p}{small}Fainter.{/small}",
                "* I can't hear it anymore.");
            EditorUtility.SetDirty(griefHack);

            // After: twenty seconds of total silence. Then the collar on the seat vibrates.
            Event(ctx, "Silence", StoryEvent.TriggerMode.OnFlag, "ghostline_done", 0.1f, null, null, null, null, "silence_long");
            Event(ctx, "Collar", StoryEvent.TriggerMode.OnFlag, "ghostline_done", 21f, "BYTE",
                L("still here. kinda. in your head. gross"), null, null, "byte_gone");
            Event(ctx, "Arrive", StoryEvent.TriggerMode.OnStart, string.Empty, 1.2f, null, null,
                L("* Night Train Zero leaves again. {p}Toward the Core.",
                  "* The carriage isn't empty anymore. {p}{slow}I know every one of these people.{/slow}"), null, string.Empty);

            ExitZone(ctx, "Exit_ToCentralCore", new Vector3(9.9f, 1f, 0f), new Vector3(0.4f, 2f, 3.6f), CoreScene, "ch6", "ghostline_done",
                L("* Not yet."));

            EndGameplayScene(ctx, GhostLineScene);
        }
    }
}
#endif
