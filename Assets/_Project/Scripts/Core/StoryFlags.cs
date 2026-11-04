using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Core
{
    /// <summary>
    /// Session-wide narrative flags ("inspected_mirror", "met_byte", "kira_trusts_leo"...).
    /// Dialogue nodes read them for branching; inspectables write them.
    /// Flags live for the current run; persist them later via SaveData if needed.
    /// </summary>
    public static class StoryFlags
    {
        private static readonly HashSet<string> flags = new HashSet<string>();
        private static readonly Dictionary<string, int> counters = new Dictionary<string, int>();

        public static bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        /// <summary>
        /// Sets a flag. Several flags can be given at once separated by ';'.
        /// Special forms: "+closeness" / "+openness" raise a hidden value by one (Trust),
        /// "+key" raises any counter. Raising happens once per call, so put it on choices / one-shot lines.
        /// </summary>
        public static void Set(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (flag.IndexOf(';') >= 0)
            {
                foreach (string part in flag.Split(';')) Set(part.Trim());
                return;
            }
            if (flag.Length > 1 && flag[0] == '+')
            {
                Increment(flag.Substring(1));
                return;
            }
            flags.Add(flag);
        }

        public static void Clear(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) flags.Remove(flag);
        }

        /// <summary>Returns how many times <paramref name="key"/> has been incremented.</summary>
        public static int GetCount(string key)
        {
            return !string.IsNullOrEmpty(key) && counters.TryGetValue(key, out int value) ? value : 0;
        }

        /// <summary>Increments a counter and returns the new value.</summary>
        public static int Increment(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            int value = GetCount(key) + 1;
            counters[key] = value;
            return value;
        }

        private const string PrefFlags = "save.flags";
        private const string PrefCounters = "save.counters";

        /// <summary>Writes flags and counters into the save (called on checkpoints and SAVE GAME).</summary>
        public static void SaveToPrefs()
        {
            PlayerPrefs.SetString("save.phone", PhoneLog.Serialize());
            PlayerPrefs.SetString(PrefFlags, string.Join("|", flags));
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> pair in counters) parts.Add(pair.Key + "=" + pair.Value);
            PlayerPrefs.SetString(PrefCounters, string.Join("|", parts));
            PlayerPrefs.Save();
        }

        /// <summary>Restores flags and counters from the save (Continue / LOAD LAST SAVE).</summary>
        public static void LoadFromPrefs()
        {
            ResetAll();
            PhoneLog.Deserialize(PlayerPrefs.GetString("save.phone", string.Empty));
            foreach (string flag in PlayerPrefs.GetString(PrefFlags, string.Empty).Split('|'))
            {
                if (!string.IsNullOrEmpty(flag)) flags.Add(flag);
            }
            foreach (string entry in PlayerPrefs.GetString(PrefCounters, string.Empty).Split('|'))
            {
                int eq = entry.LastIndexOf('=');
                if (eq > 0 && int.TryParse(entry.Substring(eq + 1), out int value)) counters[entry.Substring(0, eq)] = value;
            }
        }

        public static void ResetAll()
        {
            PhoneLog.Clear();
            flags.Clear();
            counters.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetAll();
    }
}
