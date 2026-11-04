using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Core
{
    public enum Language
    {
        English = 0,
        Italian = 1,
        French = 2
    }

    /// <summary>
    /// Localization. Every text is authored in English and that English text IS the key:
    ///   Loc.T("Hang in there, buddy.")  →  "Tieni duro, amico." when Italian is selected.
    /// Translations live in Resources/Localization/strings.txt (blocks of en| / it| / fr| lines).
    /// Missing translations fall back to English, so nothing ever shows empty.
    /// UI that should refresh live listens to <see cref="LanguageChanged"/>.
    /// </summary>
    public static class Loc
    {
        private const string PrefKey = "settings.language";

        private static Dictionary<string, string[]> table;
        private static Language? current;

        /// <summary>Raised after the language changes (UI refreshes itself).</summary>
        public static event Action LanguageChanged;

        public static Language Current
        {
            get
            {
                if (current.HasValue) return current.Value;
                if (PlayerPrefs.HasKey(PrefKey))
                {
                    current = (Language)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, 2);
                }
                else
                {
                    switch (Application.systemLanguage)
                    {
                        case SystemLanguage.Italian: current = Language.Italian; break;
                        case SystemLanguage.French: current = Language.French; break;
                        default: current = Language.English; break;
                    }
                }
                return current.Value;
            }
        }

        public static void SetLanguage(Language language)
        {
            current = language;
            PlayerPrefs.SetInt(PrefKey, (int)language);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        /// <summary>Next language in the list (for menu rows with left/right).</summary>
        public static void Cycle(int direction)
        {
            int count = 3;
            SetLanguage((Language)(((int)Current + direction + count) % count));
        }

        /// <summary>Name of a language written in that language.</summary>
        public static string NativeName(Language language)
        {
            switch (language)
            {
                case Language.Italian: return "ITALIANO";
                case Language.French: return "FRANÇAIS";
                default: return "ENGLISH";
            }
        }

        /// <summary>Translates an English text (the key). Unknown keys come back unchanged.</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            EnsureLoaded();
            if (table.TryGetValue(Normalize(english), out string[] row))
            {
                string translated = row[(int)Current];
                if (!string.IsNullOrEmpty(translated)) return translated;
            }
            return english;
        }

        /// <summary>Translates a format string, then fills it: Loc.F("FIREWALL SEGMENT {0} DOWN.", 2).</summary>
        public static string F(string englishFormat, params object[] args)
        {
            try
            {
                return string.Format(T(englishFormat), args);
            }
            catch (FormatException)
            {
                return string.Format(englishFormat, args);
            }
        }

        private static string Normalize(string text) => text.Replace("\r", string.Empty).Trim();

        private static void EnsureLoaded()
        {
            if (table != null) return;
            table = new Dictionary<string, string[]>();
            TextAsset asset = Resources.Load<TextAsset>("Localization/strings");
            if (asset == null)
            {
                Debug.LogWarning("[Loc] Resources/Localization/strings.txt not found; showing English.");
                return;
            }

            string[] row = null;
            foreach (string rawLine in asset.text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length < 3 || line[2] != '|' || line.StartsWith("#")) continue;
                string tag = line.Substring(0, 2);
                string value = Unescape(line.Substring(3));
                switch (tag)
                {
                    case "id":
                        // Explicit key (the original authored English); the en| line that follows is the
                        // display text, so English can be rewritten without touching the scenes.
                        row = new string[3];
                        table[Normalize(value)] = row;
                        break;
                    case "en":
                        if (row == null || row[0] != null)
                        {
                            row = new string[3];
                            table[Normalize(value)] = row;
                        }
                        row[0] = value;
                        break;
                    case "it":
                        if (row != null) row[1] = value;
                        break;
                    case "fr":
                        if (row != null) row[2] = value;
                        row = null;
                        break;
                }
            }
        }

        private static string Unescape(string value) => value.Replace("\\n", "\n");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            table = null;
            current = null;
            LanguageChanged = null;
        }
    }
}
