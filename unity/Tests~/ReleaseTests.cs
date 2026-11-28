// Bulk release (Core/Roster.cs; owner decisions 2026-11-28): the shared protection rules, keep-best-N,
// presets in the save (optional "roster" key), Release + Undo, and old saves loading unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class ReleaseTests
{
    static readonly DateTime T0 = new DateTime(2026, 11, 28, 12, 0, 0);

    // A roster with one of everything the rules care about. Starter team: Ysolde, Pell, Odo (each wears a weapon).
    sealed class R
    {
        public Game G; public DateTime Now = T0;
        public HeroSave B1, B5, B9, Mira, KettleLocked, Kettle, WrenSaved, Wren, ColeGeared, Cole, NettleUp, Nettle, Kaito, Vaelith, HaskA, HaskB;
    }
    static R Make()
    {
        var r = new R();
        var g = r.G = new Game(seed: 11);
        g.Now = () => r.Now;
        g.StartWith("ysolde");
        HeroSave A(string id, int lv, int stars = 0) { var h = g.AddHero(id); h.level = lv; if (stars > 0) h.stars = stars; return h; }
        r.B1 = A("brannoc", 1); r.B5 = A("brannoc", 5); r.B9 = A("brannoc", 9);
        r.Mira = A("mira", 4);
        r.KettleLocked = A("kettle", 3); r.KettleLocked.locked = true; r.Kettle = A("kettle", 7);
        r.WrenSaved = A("wren", 10); r.Wren = A("wren", 2);
        g.S.teams = new List<TeamPreset> { new TeamPreset { uids = { r.WrenSaved.uid } }, new TeamPreset(), new TeamPreset() };
        r.ColeGeared = A("cole", 4); g.Equip(r.ColeGeared, g.MakeGear(2, Slot.Helm)); r.Cole = A("cole", 6);
        r.NettleUp = A("nettle", 12, 2); r.Nettle = A("nettle", 3);
        r.Kaito = A("kaito", 1); r.Vaelith = A("vaelith", 1);
        r.HaskA = A("hask", 8); r.HaskB = A("hask", 3);
        return r;
    }

    public static void Run(Action<bool, string> check)
    {
        SafeDefault(check);
        KeepBest(check);
        DangerToggles(check);
        ReleaseAndUndo(check);
        LastHero(check);
        SaveRoundTrip(check);
        OldSaves(check);
    }

    static void SafeDefault(Action<bool, string> check)
    {
        var r = Make(); var g = r.G;
        var f = ReleaseFilter.SafeCleanup();
        check(f.name == "Safe clean-up" && f.rarityMask == 1 && f.starMask == 0b11 && f.keepBest == 1 && f.Protect == Protect.Safe, "release: built-in default is \"Safe clean-up\" (Common 1-2★, keep 1, Safe rules)");
        var plan = g.PlanRelease(f);
        var ids = new HashSet<int>(plan.Release.Select(h => h.uid));
        check(ids.SetEquals(new[] { r.B1.uid, r.B5.uid, r.Wren.uid, r.Nettle.uid }), $"release: Safe clean-up picks exactly the spare plain Commons ({string.Join(",", plan.Release.Select(h => h.id + h.level))})");
        check(plan.Release.All(h => Hero(h.id).Rarity == Rarity.Common), "release: Safe default never selects Rare or above");
        check(!ids.Contains(r.KettleLocked.uid) && g.Team.All(h => !ids.Contains(h.uid)) && !ids.Contains(r.WrenSaved.uid) && !ids.Contains(r.ColeGeared.uid) && !ids.Contains(r.NettleUp.uid) && !ids.Contains(r.Mira.uid),
            "release: Safe default never selects locked, current team, saved-team, geared, ranked-up or last-copy heroes");
        string Why(HeroSave h) => plan.Protected.FirstOrDefault(p => p.h == h).why;
        check(Why(r.KettleLocked) == "Locked" && Why(g.Team[1]) == "In your team" && Why(r.WrenSaved) == "In a saved team" && Why(r.ColeGeared) == "Wearing gear" && Why(r.NettleUp) == "Ranked up" && Why(r.B9) == "Best copy",
            "release: every protected hero says why (locked, team, saved team, gear, ranked up, best copy)");
        check(plan.Essence == 4 * EssenceValue(Rarity.Common) && plan.Essence == Game.EssenceFor(plan.Release) && plan.ByRarity[0] == 4 && !plan.Risky, $"release: summary essence {plan.Essence} = Σ essence × stars, 4 Commons, nothing risky");
        // A tap on the review screen keeps a hero.
        var kept = g.PlanRelease(f, new[] { r.B1.uid });
        check(kept.Release.Count == 3 && kept.Kept.Single() == r.B1 && kept.Essence == 15, "release: tapping a hero on the review screen keeps them");
        // A filter the player can't switch Locked/Team off in.
        var bare = new ReleaseFilter { rarityMask = 0b1111, starMask = 0b111111, keepBest = 0, protect = 0 };
        var bp = g.PlanRelease(bare);
        check(bp.Release.All(h => !h.locked && !g.S.team.Contains(h.uid)) && bp.Release.Count > 0, "release: Locked and current team stay protected even with protect = 0");
        check(g.IsProtected(r.KettleLocked, Protect.None, out var w1) && w1 == "Locked" && g.IsProtected(g.Team[0], Protect.None), "release: IsProtected always adds Locked and Team");
    }

    static void KeepBest(Action<bool, string> check)
    {
        var g = new Game(seed: 2) { Now = () => T0 };
        var keep = g.AddHero("pell"); keep.locked = true; g.S.team.Add(keep.uid);
        var a = g.AddHero("hask"); a.level = 20;
        var b = g.AddHero("hask"); b.stars = 3; b.level = 5;
        var c = g.AddHero("hask"); c.level = 15;
        var f = new ReleaseFilter { rarityMask = 1 << (int)Rarity.Rare, starMask = 0b110, keepBest = 1, protect = (int)(Protect.Safe & ~Protect.RankedUp) };
        var p1 = g.PlanRelease(f);
        check(p1.Protected.Any(x => x.h == b && x.why == "Best copy") && p1.Release.Contains(a) && p1.Release.Contains(c), "keep-best 1: keeps the highest-star copy (3★ Lv5 over 2★ Lv20)");
        f.keepBest = 2;
        var p2 = g.PlanRelease(f);
        check(p2.Release.SequenceEqual(new[] { c }), "keep-best 2: then the highest level (2★ Lv20 kept, 2★ Lv15 released)");
        f.keepBest = 0; f.dupesOnly = true;
        var p3 = g.PlanRelease(f);
        check(p3.Release.Count == 2 && p3.Protected.Any(x => x.h == b && x.why == "Last copy"), "keep-best 0 + dupes only: the last copy still stays");
        f.protect &= ~(int)Protect.LastCopy;
        var p4 = g.PlanRelease(f);
        check(p4.Release.Count == 3 && p4.LastCopies == 1 && p4.Risky, "…unless the red last-copy toggle is ticked (then the summary flags it)");
        var solo = g.AddHero("velka"); // one copy only
        check(!g.PlanRelease(new ReleaseFilter { rarityMask = 2, starMask = 2, dupesOnly = true, keepBest = 0, protect = 0 }).Release.Contains(solo), "dupes only: a hero you own once never matches");
    }

    static void DangerToggles(Action<bool, string> check)
    {
        var r = Make(); var g = r.G;
        var f = new ReleaseFilter { rarityMask = 0b1100, starMask = 0b111111, keepBest = 0 };
        check(g.PlanRelease(f).Release.Count == 0, "release: Epic and Legendary are protected by default");
        f.protect &= ~(int)Protect.HighRarity;
        var p = g.PlanRelease(f);
        check(p.Release.Count == 0 && p.Protected.All(x => x.why == "Last copy"), "release: allowing Epic/Legendary still keeps their last copy");
        f.protect &= ~(int)Protect.LastCopy;
        p = g.PlanRelease(f);
        check(p.Release.Count == 2 && p.HighRarity == 2 && p.LastCopies == 2 && p.Essence == 80 * 3 + 300 * 4, "release: with both red toggles the summary counts Epic+ and last copies");
        var saved = ReleaseFilter.SafeCleanup(); saved.protect = (int)Protect.Always; saved.Clamp();
        check(saved.Has(Protect.HighRarity) && saved.Has(Protect.LastCopy), "release: a saved preset always comes back with the red toggles off (protected)");
    }

    static void ReleaseAndUndo(Action<bool, string> check)
    {
        var r = Make(); var g = r.G;
        var f = ReleaseFilter.SafeCleanup(); f.protect = (int)(Protect.Locked | Protect.Team | Protect.HighRarity); f.keepBest = 0;
        var plan = g.PlanRelease(f);
        check(plan.Release.Contains(r.ColeGeared) && plan.Release.Contains(r.WrenSaved) && plan.GearReturned == 1, "release: with gear and saved teams unticked, those heroes can go (1 gear returned)");
        var helm = r.ColeGeared.helm;
        int ess0 = g.S.essence, n0 = g.S.heroes.Count, gear0 = g.S.gear.Count;
        var before = plan.Release.Select(h => (h.uid, h.level, h.stars)).ToList();
        var undo = g.ReleaseMany(plan);
        check(undo != null && g.S.essence - ess0 == plan.Essence && plan.Essence == plan.Release.Sum(h => EssenceValue(Hero(h.id).Rarity) * h.stars), $"ReleaseMany: essence +{plan.Essence} = Σ EssenceValue × stars");
        check(g.S.heroes.Count == n0 - plan.Release.Count && g.S.gear.Count == gear0 && g.GearOwner(g.GearByUid(helm)) == null, "ReleaseMany: heroes gone, their gear back in the bag");
        check(!g.S.teams[0].uids.Contains(r.WrenSaved.uid), "ReleaseMany: released heroes leave saved team presets");

        r.Now = T0.AddSeconds(90);
        check(g.UndoRelease(undo, r.Now, out _), "undo: works within 2 minutes");
        check(g.S.essence == ess0 && g.S.heroes.Count == n0 && before.All(b => g.HeroByUid(b.uid) is HeroSave h && h.level == b.level && h.stars == b.stars), "undo: same uids, levels and stars, essence taken back");
        check(r.ColeGeared.helm == helm && g.S.teams[0].uids.Contains(r.WrenSaved.uid), "undo: gear goes back on and saved presets are restored");
        check(!g.UndoRelease(undo, r.Now, out var twice) && twice == "Nothing to undo", "undo: only once");

        // Saved team order and roster order come back exactly (the in-house report's [8,9] -> [9,8]).
        {
            var go = new Game(seed: 5) { Now = () => T0 };
            var lead = go.AddHero("pell"); go.S.team.Add(lead.uid);
            var a = go.AddHero("hask"); var mid = go.AddHero("velka"); var b = go.AddHero("hask"); var c = go.AddHero("nettle");
            go.S.teams.Add(new TeamPreset { uids = new List<int> { a.uid, b.uid } });
            go.S.teams.Add(new TeamPreset { uids = new List<int> { c.uid, mid.uid, b.uid, a.uid } });
            var roster0 = go.S.heroes.Select(h => h.uid).ToList();
            var teams0 = go.S.teams.Select(t => string.Join(",", t.uids)).ToList();
            var po = new ReleasePlan { Protect = Protect.Always }; po.Release.AddRange(new[] { a, b, c });
            var uo = go.ReleaseMany(po);
            bool gone = go.S.teams[0].uids.Count == 0 && go.S.teams[1].uids.SequenceEqual(new[] { mid.uid });
            check(uo != null && gone && go.UndoRelease(uo, T0.AddSeconds(5)) && go.S.heroes.Select(h => h.uid).SequenceEqual(roster0) && go.S.teams.Select(t => string.Join(",", t.uids)).SequenceEqual(teams0),
                $"undo: saved teams and the roster come back in their original order ({string.Join(" / ", go.S.teams.Select(t => string.Join(",", t.uids)))} vs {string.Join(" / ", teams0)})");
        }

        // Too late.
        var u2 = g.ReleaseMany(g.PlanRelease(f));
        check(!g.UndoRelease(u2, T0.AddSeconds(90 + 121), out var late) && late.StartsWith("Too late"), "undo: refused after 2 minutes");
        // Essence spent.
        var r3 = Make(); var g3 = r3.G; g3.S.essence = 0;
        var p3 = g3.PlanRelease(f); var u3 = g3.ReleaseMany(p3);
        g3.S.essence -= 10; int heroes3 = g3.S.heroes.Count;
        check(!g3.UndoRelease(u3, T0.AddSeconds(5), out var spent) && spent == "You've already spent that essence" && g3.S.heroes.Count == heroes3 && g3.S.essence == p3.Essence - 10, "undo: refused cleanly once the essence was spent (nothing changes)");
        // Gear taken by someone else meanwhile stays with them.
        var r4 = Make(); var g4 = r4.G;
        var h4 = r4.ColeGeared.helm; var u4 = g4.ReleaseMany(g4.PlanRelease(f));
        g4.Equip(r4.Kaito, g4.GearByUid(h4));
        check(g4.UndoRelease(u4, T0.AddSeconds(5)) && r4.ColeGeared.helm == 0 && r4.Kaito.helm == h4, "undo: gear equipped elsewhere meanwhile stays there");
        // Single-hero release (hero screen) uses the same path.
        var one = g4.PlanReleaseOne(r4.Vaelith);
        check(one.Release.Single() == r4.Vaelith && g4.PlanReleaseOne(r4.KettleLocked).Release.Count == 0 && g4.PlanReleaseOne(g4.Team[0]).Release.Count == 0, "release one: allowed for a last copy, refused for locked or team heroes");
    }

    static void LastHero(Action<bool, string> check)
    {
        var g = new Game(seed: 1) { Now = () => T0 };
        var a = g.AddHero("pell"); var b = g.AddHero("pell");
        var f = new ReleaseFilter { keepBest = 0, protect = (int)Protect.Always };
        var p = g.PlanRelease(f);
        check(p.Release.Count == 1 && p.Protected.Any(x => x.why == "You must keep one hero"), "release: a plan never takes every hero");
        var forged = new ReleasePlan { Protect = Protect.Always }; forged.Release.AddRange(new[] { a, b });
        g.ReleaseMany(forged);
        check(g.S.heroes.Count == 1, "ReleaseMany: never releases the last hero in the roster");
        var t = g.AddHero("odo"); g.S.team.Add(t.uid); var l = g.AddHero("odo"); l.locked = true;
        var forged2 = new ReleasePlan { Protect = Protect.None }; forged2.Release.AddRange(new[] { t, l });
        check(g.ReleaseMany(forged2) == null && g.S.heroes.Contains(t) && g.S.heroes.Contains(l), "ReleaseMany: re-checks every hero (a team or locked hero in a plan is skipped)");
    }

    static void SaveRoundTrip(Action<bool, string> check)
    {
        var g = new Game(seed: 1); g.StartWith("ysolde");
        var plain = SaveCodec.ToJson(g.S);
        check(!plain.Contains("\"roster\""), "save: default roster settings write no \"roster\" key");
        var s = g.S.roster;
        s.presets.Add(new ReleaseFilter { name = "Spare Rares", rarityMask = 2, starMask = 0b10, elementMask = 0b101, roleMask = 0b1000, minLevel = 5, maxLevel = 30, dupesOnly = true, keepBest = 2, protect = (int)(Protect.Safe & ~Protect.Geared) });
        s.activePreset = 0; s.heroSort = "rarity"; s.rankAllTarget = 4; s.rankAllTeamOnly = false;
        var json = SaveCodec.ToJson(g.S);
        var back = SaveCodec.FromJson(json).roster;
        var p = back.presets.Single();
        check(json.Contains("\"roster\"") && p.name == "Spare Rares" && p.rarityMask == 2 && p.starMask == 2 && p.elementMask == 5 && p.roleMask == 8 && p.minLevel == 5 && p.maxLevel == 30 && p.dupesOnly && p.keepBest == 2
              && p.Protect == (Protect.Safe & ~Protect.Geared) && back.activePreset == 0 && back.heroSort == "rarity" && back.rankAllTarget == 4 && !back.rankAllTeamOnly,
              "save: presets, active preset, hero sort and rank-up-all choices survive a round trip");
        check(SaveCodec.FromCode(SaveCodec.ToCode(g.S)).roster.presets.Single().name == "Spare Rares", "save: …and a save code");
        // Garbage (hand-edited code, or a newer build) clamps to safe values; unknown keys are kept.
        var fx = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "save-web-roster.json"));
        var gs = SaveCodec.FromJson(fx);
        var gp = gs.roster.presets;
        check(gs != null && gp.Count == RosterPrefs.MaxPresets && gp[0].rarityMask == 0b1111 && gp[0].starMask == 0b111111 && gp[0].keepBest == 3 && gp[0].minLevel == 1 && gp[0].maxLevel == 60
              && gp.All(x => x.Has(Protect.Locked | Protect.Team | Protect.HighRarity | Protect.LastCopy)) && gp[1].name.Length == RosterPrefs.MaxNameLength && gp[2].maxLevel >= gp[2].minLevel,
              "save: bad roster values clamp (masks, keep 0-3, levels 1-60, 4 presets max) and Locked/Team/Epic+/last copy are forced on");
        check(gs.roster.activePreset == 3 && gs.roster.heroSort == "power" && gs.roster.rankAllTarget == 0, "save: bad active preset, sort and target fall back");
        var outJson = SaveCodec.ToJson(gs);
        check(outJson.Contains("\"futureKey\"") && outJson.Contains("\"webOnly\""), "save: keys this build doesn't know (inside and beside \"roster\") are kept");
        // Deleting every preset after a save had them still writes the key (so the old presets don't come back).
        gs.roster = new RosterPrefs();
        var cleared = SaveCodec.FromJson(SaveCodec.ToJson(gs));
        check(cleared.roster.presets.Count == 0, "save: clearing the presets is saved too");
    }

    static void OldSaves(Action<bool, string> check)
    {
        // The web game's own save code (fixtures.json, made by public/game/*.js): no "roster" key.
        var fx = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "fixtures.json")));
        var code = Json.Str(fx, "saveCode");
        var s = SaveCodec.FromCode(code);
        check(s != null && s.roster.IsDefault && s.v == 1, "old save: a web save without \"roster\" loads with default roster settings, version 1");
        var json = SaveCodec.ToJson(s);
        check(!json.Contains("\"roster\"") && Json.Int(Json.Parse(json), "v") == 1, "old save: saving it again adds no \"roster\" key and keeps version 1");
        var again = SaveCodec.ToJson(SaveCodec.FromJson(json));
        check(again == json, "old save: load and save are stable");
        // Through the safety store: a save with presets is written and read back like any other.
        var st = new Mem(); var store = new SaveStore(st);
        var loaded = store.Load(T0); loaded.heroes.Add(new HeroSave { uid = 1, id = "pell", stars = 1 }); loaded.roster.heroSort = "level";
        store.Write(loaded);
        var store2 = new SaveStore(st); var re = store2.Load(T0);
        check(store2.State == SaveLoadState.Loaded && re.roster.heroSort == "level", "save store: the hero sort is saved and loads back (Heroes list remembers it)");
    }

    sealed class Mem : ISaveStorage
    {
        readonly Dictionary<string, string> d = new Dictionary<string, string>();
        public string Get(string key) => d.TryGetValue(key, out var v) ? v : "";
        public void Set(string key, string value) => d[key] = value;
        public void Flush() { }
    }
}
