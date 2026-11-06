using System;
using System.Collections.Generic;

namespace Subnet.Core
{
    /// <summary>
    /// Leo's pockets: TOKENS (old metro tokens, the Subnet's currency), healing items, key items and his HP.
    /// Everything lives in StoryFlags counters/flags, so it's saved with the rest of the run.
    ///
    /// Story flag shortcuts (usable anywhere a flag string goes: inspectables, dialogue nodes, events):
    ///   "$5"            → +5 tokens
    ///   "give:potion"   → +1 potion (also "give:potion*2")
    ///   "key:mia_drawing" → key item
    ///   "heal:all" / "heal:8"
    /// </summary>
    public static class Inventory
    {
        public const int MaxHP = 20;
        private const string TokensKey = "tokens";
        private const string DamageKey = "hp_lost";
        private const string ItemPrefix = "item.";
        private const string KeyPrefix = "key.";

        public class ItemDef
        {
            public string id;
            public string name;
            public string description;
            public int heal;
            public int price;
            public bool key;
        }

        /// <summary>Every item in the game. Names and descriptions are localization keys (English).</summary>
        public static readonly ItemDef[] Items =
        {
            new ItemDef { id = "potion", name = "Fizzy Potion", heal = 8, price = 5,
                description = "Pink, carbonated, slightly radioactive. Heals 8 HP." },
            new ItemDef { id = "megapotion", name = "Neon Elixir", heal = MaxHP, price = 12,
                description = "Tastes like the first day of summer break. Heals all HP." },
            new ItemDef { id = "ramen", name = "Cup Ramen", heal = 5, price = 3,
                description = "Three minutes of hope. Heals 5 HP." },

            new ItemDef { id = "warm_roll", name = "Warm Roll", heal = 4, price = 2,
                description = "From the bakery downstairs. Still warm. Heals 4 HP." },

            new ItemDef { id = "hometown_ticket", name = "Train Ticket", key = true,
                description = "HALDEN to CITY CENTRAL. 21:40. One way. Mia drew a sad face on the back, then crossed it out." },
            new ItemDef { id = "recital_program", name = "Recital Program", key = true,
                description = "Mia's dance recital, next spring. Front row, seat 1. A crayon heart on the back." },
            new ItemDef { id = "care_package", name = "Kira's Care Package", key = true,
                description = "A small warm box. FRAGILE. CONTENTS: YOU. Not one to deliver: Kira says it's yours." },
            new ItemDef { id = "old_polaroid", name = "Old Polaroid", key = true,
                description = "Two kids on a station platform, the boy holding the girl up to see the trains. 'L + M. The day we moved.'" },
            new ItemDef { id = "old_headphones", name = "Old Headphones", key = true,
                description = "Lost at Central Station the week Leo moved. Still tangled exactly the same way." },
            new ItemDef { id = "voicemail_tape", name = "Voicemail Tape", key = true,
                description = "Found in apartment 0451. A little girl sings happy birthday to a dad who never came home in time." },
            new ItemDef { id = "mia_drawing", name = "Mia's Drawing", key = true,
                description = "Leo with an enormous head. 'BECAUSE YOU THINK TOO MUCH' written underneath in purple crayon." },
            new ItemDef { id = "drumsticks", name = "Dani's Drumsticks", key = true,
                description = "The tape on them is still sticky. One, two. One, two." },
            new ItemDef { id = "plush_cat", name = "Plush Cat", key = true,
                description = "Won from a claw machine. Looks exactly like Byte. Byte denies everything." },
            new ItemDef { id = "cloud_cup", name = "Cup With a Cloud", key = true,
                description = "A paper cup from the street café. Somebody drew a little cloud on it, the way they do every day." },
            new ItemDef { id = "garden_key", name = "Brass Key", key = true,
                description = "Small and warm, on a faded ribbon. The tag says: GARDEN. PLEASE CLOSE THE GATE." },
            new ItemDef { id = "paper_boat", name = "Paper Boat", key = true,
                description = "Folded out of an old receipt. Mia made a whole fleet of these once and sank every one in the bath." },
            new ItemDef { id = "first_payslip", name = "First Payslip", key = true,
                description = "Leo's first salary, folded into a tiny square. 'Send Mia something' scribbled on the back. He never did." },
        };

        /// <summary>Raised whenever tokens, items or HP change (text for a toast, may be null).</summary>
        public static event Action<string> Changed;

        public static ItemDef Find(string id)
        {
            foreach (ItemDef item in Items) if (item.id == id) return item;
            return null;
        }

        // ------------------------------------------------------------------ Tokens

        public static int Tokens => StoryFlags.GetCount(TokensKey);

        public static void AddTokens(int amount, bool toast = true)
        {
            if (amount == 0) return;
            StoryFlags.SetCount(TokensKey, Math.Max(0, Tokens + amount));
            Changed?.Invoke(toast && amount > 0 ? Loc.F("+{0} TOKENS", amount) : null);
        }

        public static bool Spend(int amount)
        {
            if (Tokens < amount) return false;
            StoryFlags.SetCount(TokensKey, Tokens - amount);
            Changed?.Invoke(null);
            return true;
        }

        // ------------------------------------------------------------------ HP

        public static int HP => Math.Max(0, MaxHP - StoryFlags.GetCount(DamageKey));

        public static void SetHP(int value)
        {
            value = Math.Max(0, Math.Min(MaxHP, value));
            StoryFlags.SetCount(DamageKey, MaxHP - value);
            Changed?.Invoke(null);
        }

        public static void Heal(int amount) => SetHP(HP + amount);

        // ------------------------------------------------------------------ Items

        public static int Count(string id) => StoryFlags.GetCount(ItemPrefix + id);

        public static bool HasKey(string id) => StoryFlags.Has(KeyPrefix + id);

        public static void Add(string id, int amount = 1, bool toast = true)
        {
            ItemDef def = Find(id);
            if (def == null || amount <= 0) return;
            if (def.key)
            {
                if (HasKey(id)) return;
                StoryFlags.Set(KeyPrefix + id);
                Changed?.Invoke(toast ? Loc.F("KEY ITEM: {0}", Loc.T(def.name)) : null);
                return;
            }
            StoryFlags.SetCount(ItemPrefix + id, Count(id) + amount);
            Changed?.Invoke(toast ? (amount > 1 ? Loc.F("GOT: {0} x{1}", Loc.T(def.name), amount) : Loc.F("GOT: {0}", Loc.T(def.name))) : null);
        }

        /// <summary>Uses one healing item. Returns false if Leo has none (or it's a key item).</summary>
        public static bool Use(string id)
        {
            ItemDef def = Find(id);
            if (def == null || def.key || Count(id) <= 0) return false;
            StoryFlags.SetCount(ItemPrefix + id, Count(id) - 1);
            Heal(def.heal);
            return true;
        }

        /// <summary>Healing items Leo carries right now, in table order.</summary>
        public static List<ItemDef> Consumables()
        {
            var list = new List<ItemDef>();
            foreach (ItemDef item in Items) if (!item.key && Count(item.id) > 0) list.Add(item);
            return list;
        }

        public static List<ItemDef> KeyItems()
        {
            var list = new List<ItemDef>();
            foreach (ItemDef item in Items) if (item.key && HasKey(item.id)) list.Add(item);
            return list;
        }

        /// <summary>The best item to drink in a hurry (smallest heal that fills the gap, else the biggest).</summary>
        public static ItemDef BestHeal()
        {
            ItemDef best = null;
            int missing = MaxHP - HP;
            foreach (ItemDef item in Consumables())
            {
                if (best == null) { best = item; continue; }
                bool itemFills = item.heal >= missing, bestFills = best.heal >= missing;
                if (itemFills && (!bestFills || item.heal < best.heal)) best = item;
                else if (!itemFills && !bestFills && item.heal > best.heal) best = item;
            }
            return best;
        }

        /// <summary>Handles the inventory flag shortcuts. Returns true if the flag was one of them.</summary>
        public static bool TryHandleFlag(string flag)
        {
            if (flag.Length > 1 && flag[0] == '$' && int.TryParse(flag.Substring(1), out int tokens))
            {
                AddTokens(tokens);
                return true;
            }
            if (flag.StartsWith("give:"))
            {
                string id = flag.Substring(5);
                int amount = 1;
                int star = id.IndexOf('*');
                if (star > 0) { int.TryParse(id.Substring(star + 1), out amount); id = id.Substring(0, star); }
                Add(id, amount);
                return true;
            }
            if (flag.StartsWith("key:"))
            {
                Add(flag.Substring(4));
                return true;
            }
            if (flag.StartsWith("take:"))
            {
                // Hands a key item over (it leaves the handheld).
                string id = flag.Substring(5);
                if (HasKey(id)) { StoryFlags.Clear(KeyPrefix + id); Changed?.Invoke(null); }
                return true;
            }
            if (flag.StartsWith("heal:"))
            {
                string v = flag.Substring(5);
                if (v == "all") SetHP(MaxHP);
                else if (int.TryParse(v, out int amount)) Heal(amount);
                Changed?.Invoke(Loc.T("HP RESTORED"));
                return true;
            }
            return false;
        }
    }
}
