using System.Collections.Generic;

namespace Subnet.Core
{
    /// <summary>
    /// Every quest in the game, driven only by story flags (no extra save data): a quest shows up once its start
    /// flag is set, each step is done when its flag is set, and the quest is complete when the last step is.
    /// Titles and steps are English localization keys. Read by the handheld (QUESTS tab), the on-screen tracker
    /// and the "new quest" toasts.
    /// </summary>
    public static class QuestLog
    {
        public class Step
        {
            public string text;
            public string doneFlag;
            public Step(string text, string doneFlag) { this.text = text; this.doneFlag = doneFlag; }
        }

        public class Quest
        {
            public string id;
            public string title;
            public string where;
            public bool main;
            public string startFlag;
            /// <summary>Hidden while this flag is set (the main quests stay out of the way during the prologue).</summary>
            public string hiddenWhile;
            public Step[] steps;

            public bool Started => (string.IsNullOrEmpty(startFlag) || StoryFlags.Has(startFlag)) && !Has(hiddenWhile);
            public bool Done => steps.Length > 0 && Has(steps[steps.Length - 1].doneFlag);

            /// <summary>The first step that isn't done yet (null when complete).</summary>
            public Step Current
            {
                get
                {
                    foreach (Step s in steps) if (!Has(s.doneFlag)) return s;
                    return null;
                }
            }

            public int DoneCount
            {
                get
                {
                    int n = 0;
                    foreach (Step s in steps) if (Has(s.doneFlag)) n++;
                    return n;
                }
            }
        }

        private static bool Has(string flag) => !string.IsNullOrEmpty(flag) && StoryFlags.Has(flag);

        private static Step S(string text, string flag) => new Step(text, flag);

        public static readonly Quest[] All =
        {
            // ------------------------------------------------------------------ Prologue (three years earlier)
            new Quest { id = "m0", main = true, title = "The Last Day", where = "Hometown, three years ago", startFlag = "in_prologue",
                steps = new[]
                {
                    S("Your train ticket is gone. Look at your desk.", "hunt_started"),
                    S("Where the morning starts.", "clue_bakery"),
                    S("Boom. Tss.", "clue_garage"),
                    S("Light the way to the swing.", "ticket_found"),
                    S("Mia is at the bandstand.", "mia_promise"),
                    S("Box fourteen is still open.", "box14_sealed"),
                    S("Say goodbye to Mom.", "ready_to_leave"),
                    S("Catch the 21:40.", "boarded_home"),
                } },

            // ------------------------------------------------------------------ Main story
            new Quest { id = "m1", main = true, title = "The Ticket Home", where = "Apartment / Central Station", startFlag = "", hiddenWhile = "in_prologue",
                steps = new[]
                {
                    S("Check your handheld.", "handheld_checked"),
                    S("Read the departures board.", "read_timetable"),
                    S("Get on the train with no number.", "intro.03_Subnet_Commercial"),
                } },
            new Quest { id = "m2", main = true, title = "The Wrong Colours", where = "Subnet", startFlag = "intro.03_Subnet_Commercial",
                steps = new[]
                {
                    S("Follow the cat.", "met_byte"),
                    S("Talk to the courier by the mannequins.", "met_kira"),
                    S("Get past the wall of paperwork.", "hack1_done"),
                    S("Head east, to the Concrete Park.", "evt.04_ConcretePark.Event_Arrive"),
                } },
            new Quest { id = "m3", main = true, title = "Three Statues", where = "Concrete Park", startFlag = "evt.04_ConcretePark.Event_Arrive",
                steps = new[]
                {
                    S("Light the lamps in the order they gave up.", "park_puzzle_solved"),
                    S("Reach the statue crying in the corridor.", "hack2_done"),
                    S("Take the Ghost Line.", "ghostline_done"),
                } },
            new Quest { id = "m4", main = true, title = "The Last Stop", where = "Central Core", startFlag = "ghostline_done",
                steps = new[]
                {
                    S("Find out what the Algorithm wants.", "algorithm_met"),
                    S("Open the memory file.", "core_file_read"),
                    S("Talk to Kira.", "act5_package_done"),
                    S("Wait for the last train.", "act5_call_done"),
                    S("Get on.", "game_finished"),
                } },

            // ------------------------------------------------------------------ Side quests
            new Quest { id = "s_roof", title = "Five Minutes of Nothing", where = "Subnet, the roof", startFlag = "got_care_package",
                steps = new[]
                {
                    S("Climb the fire escape near the freed commuter.", "visited_roof"),
                    S("Sit down somewhere. Stay a while.", "roof_seat_crate"),
                } },
            new Quest { id = "s_receipt", title = "Proof of Purchase", where = "Subnet", startFlag = "receipt_quest",
                steps = new[]
                {
                    S("Find the receipt. Try where people throw away what they used to want.", "receipt_found"),
                    S("Give it back.", "receipt_returned"),
                } },
            new Quest { id = "s_tape", title = "Saturday Isn't Over", where = "Subnet, apartment 0451", startFlag = "key.voicemail_tape",
                steps = new[] { S("Bring the tape to the man from 0451.", "gave_tape") } },
            new Quest { id = "s_noodles", title = "Noodles at Midnight", where = "Subnet, night market", startFlag = "noodle_quest",
                steps = new[]
                {
                    S("Find a cup with a little cloud on it.", "cup_found"),
                    S("Bring it to the noodle cart.", "noodle_done"),
                } },
            new Quest { id = "s_phone", title = "Someone Is Calling", where = "Subnet, then Concrete Park", startFlag = "visited_market",
                steps = new[]
                {
                    S("Answer the payphone in the night market.", "booth_answered"),
                    S("Find the gate the brass key opens.", "garden_opened"),
                    S("Sit by the pond.", "garden_sat"),
                } },
            new Quest { id = "s_mia", title = "Something for Mia", where = "Concrete Park, your old room", startFlag = "evt.04_ConcretePark.Event_Arrive",
                steps = new[] { S("Your old room is in the park, somehow. Look around.", "key.mia_drawing") } },
            new Quest { id = "s_back", title = "Back in Five", where = "Subnet", startFlag = "back_subnet",
                steps = new[] { S("The shop that was 'back in 5 minutes' is open.", "key.old_polaroid") } },
        };

        /// <summary>Started quests: main ones first, unfinished before finished.</summary>
        public static List<Quest> Visible()
        {
            var list = new List<Quest>();
            foreach (Quest q in All) if (q.Started && !q.Done) list.Add(q);
            foreach (Quest q in All) if (q.Started && q.Done) list.Add(q);
            list.Sort((a, b) =>
            {
                int da = a.Done ? 1 : 0, db = b.Done ? 1 : 0;
                if (da != db) return da - db;
                return (b.main ? 1 : 0) - (a.main ? 1 : 0);
            });
            return list;
        }

        /// <summary>The main quest step to show on screen (null when there's nothing to do).</summary>
        public static Quest CurrentMain()
        {
            foreach (Quest q in All) if (q.main && q.Started && !q.Done) return q;
            return null;
        }
    }
}
