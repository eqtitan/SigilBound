// Which cached high-detail hero models to free (HeroModels in the client; code review H1). Unity-free
// so Tests~ can check it.
using System.Collections.Generic;
using System.Linq;

namespace Sigilbound.Core
{
    public static class CachePolicy
    {
        public struct Entry
        {
            public string Key; public int Live; public float Used; // figures using it; last time used
            public Entry(string key, int live, float used) { Key = key; Live = live; Used = used; }
        }

        // Entries in use are never freed. Unused ones are freed least recently used first until at most
        // `cap` remain; one used within `recentSeconds` (just preloaded, or its battle just ended) is kept
        // unless more than `hardCap` unused entries would remain.
        public static List<string> Evict(IEnumerable<Entry> entries, float now, int cap, int hardCap, float recentSeconds)
        {
            var idle = entries.Where(e => e.Live <= 0).OrderBy(e => e.Used).ToList();
            var outList = new List<string>();
            int count = idle.Count;
            foreach (var e in idle)
            {
                if (count <= cap) break;
                if (now - e.Used < recentSeconds && count <= hardCap) break; // oldest first: the rest are recent too
                outList.Add(e.Key); count--;
            }
            return outList;
        }
    }
}
