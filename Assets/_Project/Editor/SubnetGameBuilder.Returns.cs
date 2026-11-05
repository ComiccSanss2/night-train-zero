#if UNITY_EDITOR
using Subnet.Audio;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using UnityEditor;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>
    /// Walking back: exits that lead to the previous scene (Leo arrives at the matching end, not at the start),
    /// and things that only appear the first time you come back somewhere.
    ///   Subnet street, back from the park  → the "BACK IN 5 MINUTES" shop is open (lost and found, an old polaroid).
    ///   Central Station, back from Subnet   → a lost property box with Leo's old headphones.
    ///   Concrete Park, after a round trip   → somebody switched the fountain back on.
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        /// <summary>An arrival point for SceneLoader.LoadAt (see SceneEntry).</summary>
        private static SceneEntry Entry(Ctx ctx, string id, Vector3 position, float yaw, string firstFlag = null, string[] firstLines = null)
        {
            var go = new GameObject("Entry_" + id);
            go.transform.SetParent(ctx.story, false);
            go.transform.position = position + Vector3.up * 0.02f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var entry = go.AddComponent<SceneEntry>();
            Set(entry, "entryId", id);
            Set(entry, "flagOnFirstArrival", firstFlag ?? string.Empty);
            SetStrings(entry, "firstArrivalLines", firstLines ?? new string[0]);
            return entry;
        }

        /// <summary>A walk-in exit back to the previous scene, arriving at <paramref name="entryId"/> there.</summary>
        private static SceneExit BackExit(Ctx ctx, string name, Vector3 center, Vector3 size, string targetScene, string entryId)
        {
            SceneExit exit = ExitZone(ctx, name, center, size, targetScene, null, string.Empty, null);
            Set(exit, "entryId", entryId);
            return exit;
        }

        // ====================================================================== Subnet: the shop that was "back in 5"

        private static void BuildLostAndFound(Ctx ctx, HouseDoor outside)
        {
            const string F = "furniture";
            Color amber = new Color(1f, 0.78f, 0.45f);
            Set(outside, "requiredFlag", "back_subnet");
            Set(outside, "placeName", string.Empty);

            Vector3 c = new Vector3(40f, 0f, -80f);
            Material floor = LitMaterial("Houses/M_Lost_Floor", new Color(0.36f, 0.27f, 0.22f));
            Material wall = LitMaterial("Houses/M_Lost_Wall", new Color(0.55f, 0.48f, 0.42f));
            Material paper = LitMaterial("Houses/M_Note", new Color(0.97f, 0.95f, 0.85f));
            float north = Room(ctx, "Lost", c, 7f, 5f, floor, wall);
            HouseDoor inside = BuildDoor(ctx, "BackIn5_In", new Vector3(c.x - 2.4f, 0f, north - 0.12f), 180f,
                LitMaterial("Houses/M_Door_Closed", new Color(0.5f, 0.2f, 0.3f)), Warm, null, "LOST & FOUND");
            LinkDoors(outside, inside);
            AddStemZone(ctx, "LostAndFound", c + new Vector3(0f, 1f, 0f), new Vector3(7.5f, 4f, 5.5f), StemMix.Interior);

            GameObject owner = Character(ctx, "LostOwner", "character-male-b", "mini-characters", c + new Vector3(1.4f, 0f, 1.1f), 200f, 1.0f, null, true);
            DialogueSequence ownerSeq = SeqAsset("Houses/LostAndFound_Owner",
                Say(spkUnknown, "Oh! A customer. {p}Sorry, sorry. I only stepped out for five minutes."),
                Say(null, "The calendar behind him says 1998. Every page."),
                Say(spkUnknown, "This is the lost and found. Everything the city drops ends up here. {p}Umbrellas. Gloves. {p}{small}Whole afternoons.{/small}"),
                Say(spkUnknown, "Something of yours came in. {p}It's been waiting a long time. {p}{slow}Things wait better than people do.{/slow}"),
                Say(null, "He hands you a polaroid. Two kids on a station platform. The boy is holding the girl up so she can see the trains."),
                Say(null, "On the back, in your handwriting from a long time ago: <color=#FFD84F>L + M. THE DAY WE MOVED.</color>", "", "",
                    "key:old_polaroid;+openness#lostfound_photo"),
                Say(spkUnknown, "And take this. On the house. {p}{small}The house is also lost. Long story.{/small}", "", "",
                    "give:megapotion;$8#lostfound_tokens"));
            Inspectable(owner, "LostOwner", L("..."),
                L("Five minutes. I'll be right back. {p}{small}...I'm already back, aren't I.{/small}",
                  "People come back for their things eventually. {p}You did."), null, spkUnknown, ownerSeq);

            Prop(ctx, "LostCoats", "coatRackStanding", F, c + new Vector3(-0.6f, 0f, 2.1f), 0f, 1.7f, false,
                L("* Forty coats, all different sizes. Every one of them still warm.", "* One very small coat has a name tag. It just says: <color=#FF4FD8>SOMEONE'S</color>."),
                L("* The coats sway a little, like the door just closed."));
            Prop(ctx, "LostBoxes", "cardboardBoxClosed", F, c + new Vector3(3f, 0f, -1.2f), 20f, 0.7f, true,
                L("A box labelled <color=#FFD84F>THINGS PEOPLE MEANT TO SAY</color>. {p}It's very heavy.", "* You don't open it. {p}Some of them might be yours."),
                L("* Still heavy."));
            Prop(ctx, "LostDesk", "desk", F, c + new Vector3(1.6f, 0f, 2.0f), 180f, 0.8f, false,
                L("A ledger. Every line says: <color=#FFD84F>CLAIMED</color>. {p}The handwriting is very patient."), L("* CLAIMED. CLAIMED. CLAIMED."));
            GameObject calendar = Block("Prop_Calendar1998", ctx.props, new Vector3(c.x + 3.48f, 1.55f, c.z + 0.6f), new Vector3(0.04f, 0.7f, 0.55f), paper, ctx.interactableLayer);
            Inspectable(calendar, "Calendar1998",
                L("A calendar from 1998. One day is circled: the day he stepped out.", "* Next to it, in pencil: <color=#FF4FD8>back in 5</color>."),
                L("* Five minutes is a long time in this city."));
            PointLight(ctx.env, c + new Vector3(0f, 2.4f, 0f), amber, 2.2f, 7f);
            PointLight(ctx.env, c + new Vector3(2.2f, 1.4f, 1.6f), amber, 1f, 3f);
        }

        // ====================================================================== Central Station: lost property

        private static void BuildStationReturn(Ctx ctx)
        {
            Entry(ctx, "from_subnet", new Vector3(1.5f, 0f, 1.2f), 180f, "back_station",
                L("* Central Station again. {p}It looks smaller. {p}{small}Or I got bigger. Hard to say.{/small}"));

            var holder = new GameObject("ReturnOnly_LostProperty");
            holder.transform.SetParent(ctx.props, false);
            Set(holder.AddComponent<ShowWhenFlag>(), "flag", "back_station");
            Prop(ctx, "LostProperty", "cardboardBoxOpen", "furniture", new Vector3(-6.2f, 0f, 2.1f), 15f, 0.7f, true,
                L("A box by the wall: <color=#FFD84F>LOST PROPERTY</color>. {p}On top, a pair of tangled headphones.",
                  "* ...They're mine. I lost them here the week I moved. Three years ago.",
                  "* They're still tangled exactly the same way. {p}Somebody kept them."),
                L("* Somebody put a potion in the box. And a note: 'for the round trip'."), "key:old_headphones;give:potion;$5#station_lost",
                true, null, holder.transform);
        }

        // ====================================================================== Concrete Park: the fountain

        private static void BuildParkReturn(Ctx ctx)
        {
            // Coming from the street after a round trip (back_subnet) counts as a return.
            SceneEntry entry = Entry(ctx, "from_subnet", new Vector3(-12f, 0f, 0f), 90f, "back_park",
                L("* The park again. {p}Somebody switched the fountain back on."));
            Set(entry, "firstArrivalRequires", "back_subnet");

            var holder = new GameObject("ReturnOnly_Fountain");
            holder.transform.SetParent(ctx.props, false);
            Set(holder.AddComponent<ShowWhenFlag>(), "flag", "back_park");
            Material stone = LitMaterial("Park/M_FountainStone", new Color(0.55f, 0.55f, 0.6f));
            Material water = EmissiveMaterial("Park/M_FountainWater", new Color(0.2f, 0.45f, 0.8f), new Color(0.3f, 0.7f, 1f) * 1.4f);
            Vector3 p = new Vector3(-9.5f, 0f, -4.6f);
            GameObject basin = Block("Prop_Fountain", holder.transform, p + new Vector3(0f, 0.25f, 0f), new Vector3(1.8f, 0.5f, 1.8f), stone, ctx.interactableLayer);
            Deco("FountainWater", holder.transform, p + new Vector3(0f, 0.48f, 0f), new Vector3(1.5f, 0.05f, 1.5f), water);
            Deco("FountainPillar", holder.transform, p + new Vector3(0f, 0.8f, 0f), new Vector3(0.3f, 0.7f, 0.3f), stone);
            PixelTears(holder.transform, p + new Vector3(0f, 1.2f, 0f), ParticleMaterial("Park/M_FountainDrops", new Color(0.5f, 0.85f, 1f)), 18f);
            PointLight(holder.transform, p + new Vector3(0f, 1f, 0f), new Color(0.4f, 0.75f, 1f), 1.4f, 4f);
            Inspectable(basin, "ParkFountain",
                L("* A fountain. It wasn't running before. {p}The water is warm, and full of coins.",
                  "* Every coin has a wish scratched into it. {p}<color=#FF4FD8>'come home'</color>. <color=#FF4FD8>'call me'</color>. <color=#FF4FD8>'one more day'</color>.",
                  "* A few coins have nothing scratched on them yet. You take those. {p}Somebody left them for whoever needed a wish."),
                L("* The fountain keeps running. {p}Like it's waiting for the next person who comes back."), "$10#park_fountain;+openness#park_fountain");
        }
    }
}
#endif
