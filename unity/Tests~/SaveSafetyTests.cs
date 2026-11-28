// Save safety (QA review 2026-11-27, P0-1): a save that fails to load is never overwritten, the previous
// save is kept as a backup and tried on a failed load, writes are blocked while a failed load is
// unresolved, and heroes this build doesn't know survive a load and save.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class SaveSafetyTests
{
    sealed class MemStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> D = new Dictionary<string, string>();
        public int Flushes;
        public string Get(string key) => D.TryGetValue(key, out var v) ? v : "";
        public void Set(string key, string value) => D[key] = value;
        public void Flush() => Flushes++;
    }

    public static void Run(Action<bool, string> check)
    {
        var t0 = new DateTime(2026, 11, 27, 10, 0, 0);
        var g = new Game(seed: 5); g.StartWith("ysolde");
        string good = SaveCodec.ToJson(g.S);

        // Fresh install: nothing stored, writes allowed, the first write makes no backup.
        {
            var st = new MemStorage(); var store = new SaveStore(st);
            var s = store.Load(t0);
            check(store.State == SaveLoadState.Fresh && store.CanWrite && !s.started, "save: a fresh install starts a new game and may save");
            s.gold = 111; store.Write(s);
            check(st.Get(SaveStore.MainKey).Contains("111") && st.Get(SaveStore.BackupKey) == "", "save: the first write makes the save and no backup");
            s.gold = 222; store.Write(s);
            check(st.Get(SaveStore.MainKey).Contains("222") && st.Get(SaveStore.BackupKey).Contains("\"gold\":111"), "save: each later write first keeps the previous save as the backup");
            int before = st.D.Count; var bak = st.Get(SaveStore.BackupKey); store.Write(s);
            check(st.Get(SaveStore.BackupKey) == bak && st.D.Count == before, "save: an unchanged save doesn't replace the backup");
        }

        // A readable save loads normally.
        {
            var st = new MemStorage(); st.Set(SaveStore.MainKey, good);
            var store = new SaveStore(st); var s = store.Load(t0);
            check(store.State == SaveLoadState.Loaded && s.started && s.heroes.Count == g.S.heroes.Count, "save: a good save loads");
        }

        // Unreadable save with a good backup: the backup loads, the bad save is kept, and the bad save
        // never becomes the backup.
        {
            var st = new MemStorage(); string bad = good.Substring(0, good.Length / 2);
            st.Set(SaveStore.MainKey, bad); st.Set(SaveStore.BackupKey, good);
            var store = new SaveStore(st); var s = store.Load(t0);
            check(store.State == SaveLoadState.FromBackup && s.started && store.CanWrite, "save: a truncated save falls back to the backup");
            check(store.CorruptKey.StartsWith(SaveStore.CorruptPrefix) && st.Get(store.CorruptKey) == bad && st.Get(SaveStore.MainKey) == bad, "save: the unreadable save is kept as .corrupt-<time> and left in place");
            check(store.CorruptKey == SaveStore.CorruptPrefix + "20261127-100000", $"save: the corrupt copy is named by time ({store.CorruptKey})");
            s.gold = 777; store.Write(s);
            check(st.Get(SaveStore.BackupKey) == good && st.Get(SaveStore.MainKey).Contains("777"), "save: after a backup restore, the next write keeps the good backup (not the bad save)");
        }

        // Unreadable save and no backup: Failed, every write refused, the real save untouched.
        {
            var st = new MemStorage(); string bad = "\u0001\u0002garbageÿ{{";
            st.Set(SaveStore.MainKey, bad);
            var store = new SaveStore(st); var s = store.Load(t0);
            check(store.State == SaveLoadState.Failed && !store.CanWrite && !s.started, "save: unreadable save with no backup is a failed load");
            s.gold = 5; bool wrote = store.Write(s);
            check(!wrote && st.Get(SaveStore.MainKey) == bad && st.Get(SaveStore.BackupKey) == "", "save: a failed load blocks autosave, so the real save is never overwritten");
            check(store.CorruptText == bad, "save: the damaged save can be copied out");
            // Next launch with the same bad save: still failed, and still one corrupt copy.
            int copies = st.D.Keys.Count(k => k.StartsWith(SaveStore.CorruptPrefix) && k != SaveStore.LastCorruptKey);
            var store2 = new SaveStore(st); store2.Load(t0.AddMinutes(5));
            int copies2 = st.D.Keys.Count(k => k.StartsWith(SaveStore.CorruptPrefix) && k != SaveStore.LastCorruptKey);
            check(store2.State == SaveLoadState.Failed && copies == 1 && copies2 == 1, "save: relaunching with the same bad save keeps one copy and stays blocked");
            // The player chooses to start over: writes allowed, the corrupt copy stays.
            store2.AcceptFresh();
            check(store2.CanWrite && store2.Write(new SaveData()) && st.Get(store2.CorruptKey) == bad, "save: starting over on purpose saves again and keeps the damaged copy");
        }

        // Main key lost but a backup exists: the backup loads.
        {
            var st = new MemStorage(); st.Set(SaveStore.BackupKey, good);
            var store = new SaveStore(st); var s = store.Load(t0);
            check(store.State == SaveLoadState.FromBackup && s.started, "save: a missing save with a backup loads the backup");
        }

        // Garbage never throws.
        {
            string[] junk = { null, "", "{", "{}", "[]", "null", "{\"gold\":\"lots\",\"heroes\":[]}", "{\"gold\":1,\"heroes\":{}}", good.Substring(0, 40), "\0\0\0\0", "{\"gold\":1,\"heroes\":[1,\"x\",null,{\"id\":5}]}" };
            bool ok = true;
            foreach (var j in junk)
            {
                try { SaveStore.TryParse(j, out var _, out var _); } catch (Exception) { ok = false; }
            }
            check(ok, "save: no corrupt or truncated input throws while loading");
            check(!SaveStore.TryParse("{\"gold\":\"lots\",\"heroes\":[]}", out _, out _) && !SaveStore.TryParse(good.Substring(0, good.Length - 3), out _, out _), "save: a string gold or a truncated save is a failed load");
        }

        // Unknown heroes (a newer build's hero) and the gear they wear survive a load and save.
        {
            var src = Json.Parse(good) as Dictionary<string, object>;
            var heroes = (List<object>)src["heroes"]; var gear = (List<object>)src["gear"] ?? new List<object>();
            heroes.Add(new Dictionary<string, object> { ["uid"] = 900.0, ["id"] = "zz_future_hero", ["level"] = 33.0, ["stars"] = 5.0, ["gear"] = new Dictionary<string, object> { ["weapon"] = 901.0 }, ["futureField"] = "x" });
            gear.Add(new Dictionary<string, object> { ["uid"] = 901.0, ["slot"] = "weapon", ["rarity"] = 5.0, ["set"] = "fury", ["level"] = 12.0, ["subs"] = new List<object>() });
            src["gear"] = gear;
            src["cleared"] = (double)(TotalStages + 6);
            var s = SaveCodec.FromJson(Json.Write(src));
            check(s != null && s.heroes.All(h => Hero(h.id) != null) && s.keptHeroes.Count == 1 && s.keptGear.Count == 1, "save: an unknown hero is kept aside, not loaded or dropped");
            check(s.gear.All(x => x.uid != 901) && s.nextUid > 901, "save: its gear stays out of the bag and new uids don't collide with it");
            var g2 = new Game(s, 1); var added = g2.AddHero("ysolde");
            check(added.uid > 901, "save: a hero added later gets a fresh uid");
            var back = Json.Parse(SaveCodec.ToJson(g2.S)) as Dictionary<string, object>;
            var vh = ((List<object>)back["heroes"]).OfType<Dictionary<string, object>>().FirstOrDefault(h => Json.Str(h, "id") == "zz_future_hero");
            var vg = ((List<object>)back["gear"]).OfType<Dictionary<string, object>>().FirstOrDefault(x => Json.Int(x, "uid") == 901);
            check(vh != null && Json.Int(vh, "level") == 33 && Json.Str(vh, "futureField") == "x" && Json.Int(Json.Obj(vh, "gear"), "weapon") == 901 && vg != null && Json.Int(vg, "level") == 12,
                "save: the unknown hero and its gear are written back unchanged");
            check(Json.Int(back, "cleared") == TotalStages + 6, "save: campaign progress past this build's last stage is kept");
            var again = SaveCodec.FromJson(SaveCodec.ToJson(g2.S));
            check(again.keptHeroes.Count == 1 && again.keptGear.Count == 1 && again.heroes.Count == g2.S.heroes.Count, "save: kept heroes survive repeated saves");
        }
    }
}
