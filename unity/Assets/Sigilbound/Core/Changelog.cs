// The player-facing "What's new" list (Assets/Resources/Changelog.txt), shown once after an update
// and from Settings. Format, newest first:
//   ## 2026-11-25 | Women's bodies and a fixed updater
//   new: Forty heroes ...
//   fix: In-app updates ...
// Tags: new, fix, change, balance. Lines starting with "#" (one hash) or blank are ignored.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public sealed class ChangelogEntry
    {
        public string Date, Title;
        public readonly List<(string Tag, string Text)> Items = new List<(string, string)>();
        // What "seen" remembers: the header plus the number of lines, so an entry that gains lines after
        // it was seen shows again (it used to stay silent when notes were added to the same entry).
        public string Key => Date + " | " + Title + " | " + Items.Count;
    }

    public static class Changelog
    {
        public static readonly string[] Tags = { "new", "fix", "change", "balance" };

        public static List<ChangelogEntry> Parse(string text)
        {
            var list = new List<ChangelogEntry>();
            ChangelogEntry cur = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    var head = line.Substring(3);
                    int bar = head.IndexOf('|');
                    cur = new ChangelogEntry { Date = (bar < 0 ? "" : head.Substring(0, bar)).Trim(), Title = (bar < 0 ? head : head.Substring(bar + 1)).Trim() };
                    list.Add(cur);
                    continue;
                }
                if (line.StartsWith("#", StringComparison.Ordinal) || cur == null) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var tag = line.Substring(0, colon).Trim().ToLowerInvariant();
                if (Array.IndexOf(Tags, tag) < 0) continue;
                cur.Items.Add((tag, line.Substring(colon + 1).Trim()));
            }
            return list;
        }

        // Entries the player hasn't seen: those above the last seen one. A first launch (nothing
        // seen yet) shows only the newest entry; an unknown key (an old entry removed) shows the newest.
        public static List<ChangelogEntry> Unseen(List<ChangelogEntry> all, string seenKey, int max = 5)
        {
            var outp = new List<ChangelogEntry>();
            if (all.Count == 0) return outp;
            int idx = string.IsNullOrEmpty(seenKey) ? 1 : all.FindIndex(e => e.Key == seenKey);
            if (idx < 0) idx = 1;
            for (int i = 0; i < idx && i < all.Count && outp.Count < max; i++) outp.Add(all[i]);
            return outp;
        }
    }
}
