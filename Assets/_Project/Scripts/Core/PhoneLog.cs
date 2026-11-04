using System.Collections.Generic;
using System.Text;

namespace Subnet.Core
{
    /// <summary>Every SMS Leo's handheld has received (saved with the game). Stored as keys; translated when shown.</summary>
    public static class PhoneLog
    {
        public struct Entry
        {
            public string sender;
            public string text;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        public static IReadOnlyList<Entry> Entries => entries;

        public static void Add(string sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // The same beat can fire twice after loading a save: keep the log clean.
            if (entries.Count > 0 && entries[entries.Count - 1].text == text) return;
            entries.Add(new Entry { sender = sender ?? string.Empty, text = text });
            if (entries.Count > 60) entries.RemoveAt(0);
        }

        public static void Clear() => entries.Clear();

        public static string Serialize()
        {
            var sb = new StringBuilder();
            foreach (Entry e in entries) sb.Append(e.sender.Replace('\u001F', ' ')).Append('\u001F').Append(e.text.Replace('\u001E', ' ')).Append('\u001E');
            return sb.ToString();
        }

        public static void Deserialize(string data)
        {
            entries.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (string row in data.Split('\u001E'))
            {
                int cut = row.IndexOf('\u001F');
                if (cut < 0) continue;
                entries.Add(new Entry { sender = row.Substring(0, cut), text = row.Substring(cut + 1) });
            }
        }
    }
}
