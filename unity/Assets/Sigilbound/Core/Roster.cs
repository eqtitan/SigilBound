// Roster tools (owner decisions 2026-11-28; design: research BULK_RELEASE_AUTO_RANKUP): one protection rule
// set shared by bulk release and rank-up fodder, release filters and presets, Release + Undo, and
// "Rank up all" planning. Pure C# so Tests~ checks it (ReleaseTests.cs, RankUpTests.cs).
//   - Locked heroes (the Lock is the favourite) and the current team are ALWAYS protected.
//   - Saved teams, geared and ranked-up heroes are protected by default and can be switched off.
//   - Epic/Legendary and the last copy of a hero are protected unless a red danger toggle is ticked;
//     danger toggles are never saved (a preset always loads with them on).
//   - Undo lives in memory only, for 2 minutes, and is refused once the essence has been spent.
using System;
using System.Collections.Generic;
using System.Linq;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Core
{
    [Flags]
    public enum Protect
    {
        None = 0, Locked = 1, Team = 2, SavedTeams = 4, Geared = 8, RankedUp = 16, MaxLevel = 32,
        HighRarity = 64,   // Epic + Legendary
        LastCopy = 128,    // the only copy of that hero id
        Always = Locked | Team,
        Danger = HighRarity | LastCopy,
        Safe = Locked | Team | SavedTeams | Geared | RankedUp | HighRarity | LastCopy,
        // Rank-up auto-picks: the same rules minus RankedUp. Fodder must have the target's stars, so every
        // 3★+ Common or Rare is ranked up; those prepared fodder heroes are exactly what auto-fill should use.
        Fodder = Safe & ~RankedUp,
        All = 255,
    }

    [Serializable] public sealed class ReleaseFilter
    {
        public const string SafeName = "Safe clean-up";
        public string name = SafeName;
        public int rarityMask = 1;          // bit per Rarity; default Common only
        public int starMask = 0b11;         // bit per star 1..6 (bit 0 = 1★); default 1★ and 2★
        public int elementMask;             // bit per Element; 0 = all
        public int roleMask;                // bit per Role; 0 = all
        public int minLevel = 1, maxLevel = 60;
        public bool dupesOnly;
        public int keepBest = 1;            // copies of each hero id to keep (0..3)
        public int protect = (int)Protect.Safe;

        public const int MaxKeep = 3, MaxLevelAll = 60;
        public static ReleaseFilter SafeCleanup() => new ReleaseFilter();
        public Protect Protect => (Protect)protect | Protect.Always;
        public ReleaseFilter Clone() => (ReleaseFilter)MemberwiseClone();
        public bool Has(Protect p) => (Protect & p) == p;
        public string Key => $"{name}|{rarityMask}|{starMask}|{elementMask}|{roleMask}|{minLevel}|{maxLevel}|{dupesOnly}|{keepBest}|{protect}";
        public bool SameRules(ReleaseFilter o) => o != null && Key.Substring(Key.IndexOf('|')) == o.Key.Substring(o.Key.IndexOf('|'));

        // Values from a save code or a hand edit: masks and ranges clamped, Locked/Team and the danger bits forced on.
        public void Clamp()
        {
            name = string.IsNullOrWhiteSpace(name) ? "My filter" : name.Trim();
            if (name.Length > RosterPrefs.MaxNameLength) name = name.Substring(0, RosterPrefs.MaxNameLength);
            rarityMask &= 0b1111; starMask &= 0b111111; elementMask &= 0b11111; roleMask &= 0b1111;
            minLevel = Math.Max(1, Math.Min(MaxLevelAll, minLevel));
            maxLevel = Math.Max(minLevel, Math.Min(MaxLevelAll, maxLevel));
            keepBest = Math.Max(0, Math.Min(MaxKeep, keepBest));
            protect = (protect & (int)Protect.All) | (int)Protect.Always | (int)Protect.Danger;
        }
    }

    // The player's roster settings, saved under the optional "roster" key (only when not the defaults).
    [Serializable] public sealed class RosterPrefs
    {
        public const int MaxPresets = 4, MaxNameLength = 16;
        public static readonly string[] Sorts = { "power", "level", "rarity" };
        public List<ReleaseFilter> presets = new List<ReleaseFilter>(); // user presets (the built-in one lives in code)
        public int activePreset = -1;       // -1 = built-in "Safe clean-up"
        public string heroSort = "power";   // the Heroes list sort
        public int rankAllTarget;           // 0 = next star, else 2..6
        public bool rankAllTeamOnly = true;

        public bool IsDefault => presets.Count == 0 && activePreset == -1 && heroSort == "power" && rankAllTarget == 0 && rankAllTeamOnly;
        public ReleaseFilter Active => activePreset >= 0 && activePreset < presets.Count ? presets[activePreset] : ReleaseFilter.SafeCleanup();
        public void Clamp()
        {
            presets = (presets ?? new List<ReleaseFilter>()).Where(p => p != null).Take(MaxPresets).ToList();
            foreach (var p in presets) p.Clamp();
            activePreset = Math.Max(-1, Math.Min(presets.Count - 1, activePreset));
            if (Array.IndexOf(Sorts, heroSort) < 0) heroSort = "power";
            rankAllTarget = rankAllTarget >= 2 && rankAllTarget <= MaxStars ? rankAllTarget : 0;
        }
    }

    public sealed class ReleasePlan
    {
        public Protect Protect = Protect.Always;
        public List<HeroSave> Release = new List<HeroSave>();
        public List<(HeroSave h, string why)> Protected = new List<(HeroSave, string)>(); // matched but kept by a rule
        public List<HeroSave> Kept = new List<HeroSave>();   // matched, kept by a tap on the review screen
        public int Essence, GearReturned, LastCopies, HighRarity;
        public int[] ByRarity = new int[4];
        public bool Risky => LastCopies > 0 || HighRarity > 0;
    }

    public sealed class ReleaseUndo
    {
        public List<(HeroSave h, int index)> Heroes = new List<(HeroSave, int)>();
        public List<(int heroUid, Slot slot, int gearUid)> Gear = new List<(int, Slot, int)>();
        public List<(int preset, int pos, int uid)> Presets = new List<(int, int, int)>();
        public int Essence; public DateTime At; public bool Done;
        public const double Seconds = 120;
        public double SecondsLeft(DateTime now) => Done ? 0 : Math.Max(0, Seconds - (now - At).TotalSeconds);
    }

    public sealed class RankStep { public HeroSave Hero; public int From, To; public List<HeroSave> Fodder = new List<HeroSave>(); public int Gold, TrainGold; }
    public sealed class RankAllPlan
    {
        public List<RankStep> Steps = new List<RankStep>();
        public int Gold, FodderUsed; public bool GoldShort;
        public List<(HeroSave h, string why)> Skipped = new List<(HeroSave, string)>();
        public int Heroes => Steps.Select(s => s.Hero.uid).Distinct().Count();
    }

    public sealed partial class Game
    {
        // ---------- protection (one rule set for release and rank-up) ----------
        public static bool RankedUp(HeroSave h) => h.stars > NaturalStars(Hero(h.id).Rarity);
        public bool InSavedTeam(HeroSave h) => S.teams.Any(t => t.uids.Contains(h.uid));
        public static bool Geared(HeroSave h) => h.weapon != 0 || h.helm != 0 || h.chest != 0 || h.boots != 0;

        // Is this hero protected under rule set p? Locked and current team always are. LastCopy here is the
        // static check (the only copy owned); the planners also keep the last remaining copy of a group.
        public bool IsProtected(HeroSave h, Protect p, out string why)
        {
            p |= Protect.Always;
            var t = Hero(h.id);
            why = h.locked ? "Locked"
                : S.team.Contains(h.uid) ? "In your team"
                : (p & Protect.SavedTeams) != 0 && InSavedTeam(h) ? "In a saved team"
                : (p & Protect.Geared) != 0 && Geared(h) ? "Wearing gear"
                : (p & Protect.RankedUp) != 0 && RankedUp(h) ? "Ranked up"
                : (p & Protect.MaxLevel) != 0 && h.level >= MaxLevelFor(h.stars) ? "Max level"
                : (p & Protect.HighRarity) != 0 && t.Rarity >= Rarity.Epic ? t.Rarity.ToString()
                : (p & Protect.LastCopy) != 0 && CopiesOf(h.id) <= 1 ? "Last copy"
                : null;
            return why != null;
        }
        public bool IsProtected(HeroSave h, Protect p) => IsProtected(h, p, out _);
        // What the auto-pick rules say about a fodder hero the player picks by hand: null when it's a safe pick.
        public string ProtectWarning(HeroSave h) => IsProtected(h, Protect.Fodder, out var why) ? why : null;
        public static bool Dangerous(string why) => why == "Epic" || why == "Legendary" || why == "Last copy";

        public static int EssenceOf(HeroSave h) => EssenceValue(Hero(h.id).Rarity) * h.stars;
        public static int EssenceFor(IEnumerable<HeroSave> hs) => hs.Sum(EssenceOf);

        // ---------- bulk release ----------
        public bool Matches(HeroSave h, ReleaseFilter f)
        {
            var t = Hero(h.id);
            return (f.rarityMask & (1 << (int)t.Rarity)) != 0
                && h.stars >= 1 && h.stars <= MaxStars && (f.starMask & (1 << (h.stars - 1))) != 0
                && (f.elementMask == 0 || (f.elementMask & (1 << (int)t.Element)) != 0)
                && (f.roleMask == 0 || (f.roleMask & (1 << (int)t.Role)) != 0)
                && h.level >= f.minLevel && h.level <= f.maxLevel
                && (!f.dupesOnly || CopiesOf(h.id) > 1);
        }

        // Best copies first: highest stars, then level, then power, then oldest.
        IEnumerable<HeroSave> BestFirst(IEnumerable<HeroSave> hs) =>
            hs.OrderByDescending(x => x.stars).ThenByDescending(x => x.level).ThenByDescending(Power).ThenBy(x => x.uid);

        public ReleasePlan PlanRelease(ReleaseFilter f, ICollection<int> manualKeep = null)
        {
            var plan = new ReleasePlan { Protect = f.Protect };
            var p = f.Protect;
            // Keep-best: the top keepBest copies of each hero id across the WHOLE roster stay.
            var best = new HashSet<int>();
            if (f.keepBest > 0)
                foreach (var grp in S.heroes.GroupBy(x => x.id))
                    foreach (var x in BestFirst(grp).Take(f.keepBest)) best.Add(x.uid);
            foreach (var h in S.heroes)
            {
                if (!Matches(h, f)) continue;
                if (IsProtected(h, p, out var why)) plan.Protected.Add((h, why));
                else if (best.Contains(h.uid)) plan.Protected.Add((h, "Best copy"));
                else if (manualKeep != null && manualKeep.Contains(h.uid)) plan.Kept.Add(h);
                else plan.Release.Add(h);
            }
            // Last copy (unless the danger toggle is ticked): never release every copy of a hero.
            if ((p & Protect.LastCopy) != 0)
                foreach (var grp in plan.Release.GroupBy(x => x.id).ToList())
                    if (grp.Count() >= CopiesOf(grp.Key))
                    {
                        var keep = BestFirst(grp).First();
                        plan.Release.Remove(keep); plan.Protected.Add((keep, "Last copy"));
                    }
            // You must keep at least one hero.
            if (plan.Release.Count > 0 && plan.Release.Count >= S.heroes.Count)
            {
                var keep = plan.Release.OrderByDescending(Power).First();
                plan.Release.Remove(keep); plan.Protected.Add((keep, "You must keep one hero"));
            }
            Summarise(plan);
            return plan;
        }

        void Summarise(ReleasePlan plan)
        {
            plan.Essence = EssenceFor(plan.Release);
            plan.GearReturned = plan.Release.Sum(h => (h.weapon != 0 ? 1 : 0) + (h.helm != 0 ? 1 : 0) + (h.chest != 0 ? 1 : 0) + (h.boots != 0 ? 1 : 0));
            plan.HighRarity = plan.Release.Count(h => Hero(h.id).Rarity >= Rarity.Epic);
            plan.LastCopies = plan.Release.GroupBy(h => h.id).Count(g => g.Count() >= CopiesOf(g.Key));
            plan.ByRarity = new int[4];
            foreach (var h in plan.Release) plan.ByRarity[(int)Hero(h.id).Rarity]++;
        }

        // A plan for one hero (the hero screen's Release button): only the always-on rules apply.
        public ReleasePlan PlanReleaseOne(HeroSave h)
        {
            var plan = new ReleasePlan { Protect = Protect.Always };
            if (h != null && S.heroes.Contains(h) && !IsProtected(h, Protect.Always) && S.heroes.Count > 1) plan.Release.Add(h);
            Summarise(plan);
            return plan;
        }

        // Releases the plan's heroes: unequips their gear (back to the bag), takes them out of saved team
        // presets, pays essence. Every hero is checked again against the plan's rules, and the last hero in
        // the roster is never released. The caller saves once afterwards. Null if nothing was released.
        public ReleaseUndo ReleaseMany(ReleasePlan plan)
        {
            if (plan == null) return null;
            var u = new ReleaseUndo { At = Now() };
            // Undo puts everything back at its original index (ascending), so record positions in the
            // roster and presets as they were before this release, not after the earlier removals.
            var heroOrder = S.heroes.ToList();
            var teamOrder = S.teams.Select(t => t.uids.ToList()).ToList();
            foreach (var h in plan.Release.ToList())
            {
                int idx = S.heroes.Contains(h) ? heroOrder.IndexOf(h) : -1;
                if (idx < 0 || S.heroes.Count <= 1 || IsProtected(h, plan.Protect & ~Protect.LastCopy)) continue;
                if ((plan.Protect & Protect.LastCopy) != 0 && CopiesOf(h.id) <= 1) continue;
                foreach (Slot s in Enum.GetValues(typeof(Slot)))
                    if (h.GetGear(s) != 0) { u.Gear.Add((h.uid, s, h.GetGear(s))); h.SetGear(s, 0); }
                for (int i = 0; i < S.teams.Count; i++)
                    if (S.teams[i].uids.RemoveAll(x => x == h.uid) > 0) u.Presets.Add((i, teamOrder[i].IndexOf(h.uid), h.uid));
                u.Heroes.Add((h, idx));
                u.Essence += Release(h);
            }
            return u.Heroes.Count == 0 ? null : u;
        }

        // Puts released heroes back (same uids: uids are never reused) and takes the essence back. Refused
        // after 2 minutes, or once the essence has been spent. Gear goes back on only if it's still free.
        public bool UndoRelease(ReleaseUndo u, DateTime now, out string why)
        {
            why = null;
            if (u == null || u.Done) { why = "Nothing to undo"; return false; }
            if ((now - u.At).TotalSeconds > ReleaseUndo.Seconds) { why = "Too late to undo (2 minutes)"; return false; }
            if (S.essence < u.Essence) { why = "You've already spent that essence"; return false; }
            S.essence -= u.Essence;
            foreach (var (h, index) in u.Heroes.OrderBy(x => x.index))
                if (HeroByUid(h.uid) == null) S.heroes.Insert(Math.Min(index, S.heroes.Count), h);
            foreach (var (heroUid, slot, gearUid) in u.Gear)
            {
                var h = HeroByUid(heroUid); var g = GearByUid(gearUid);
                if (h != null && g != null && GearOwner(g) == null && h.GetGear(slot) == 0) h.SetGear(slot, gearUid);
            }
            foreach (var (preset, pos, uid) in u.Presets.OrderBy(x => x.preset).ThenBy(x => x.pos))
                if (preset < S.teams.Count && !S.teams[preset].uids.Contains(uid)) S.teams[preset].uids.Insert(Math.Min(pos, S.teams[preset].uids.Count), uid);
            u.Done = true;
            return true;
        }
        public bool UndoRelease(ReleaseUndo u, DateTime now) => UndoRelease(u, now, out _);

        // ---------- rank-up fodder ----------
        // Same-star heroes that are not protected under p (Fodder for auto-picks; manual picks use Always).
        public List<HeroSave> RankUpFodder(HeroSave h, Protect p) =>
            S.heroes.Where(o => o.uid != h.uid && o.stars == h.stars && !IsProtected(o, p)).ToList();

        IEnumerable<HeroSave> Cheapest(IEnumerable<HeroSave> hs) =>
            hs.OrderBy(f => (int)Hero(f.id).Rarity).ThenByDescending(f => CopiesOf(f.id)).ThenBy(f => f.level).ThenBy(f => f.uid);

        // Picks up to `count` fodder at `stars`, cheapest first, never the heroes in `skip`, and (with
        // LastCopy) never the last remaining copy of a hero once earlier picks and `skip` are counted.
        List<HeroSave> PickFodder(int stars, int count, Protect p, ICollection<int> skip, HashSet<int> gone)
        {
            var picked = new List<HeroSave>();
            var left = S.heroes.Where(x => !gone.Contains(x.uid)).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.Count());
            foreach (var f in Cheapest(S.heroes.Where(o => o.stars == stars && !skip.Contains(o.uid) && !gone.Contains(o.uid) && !IsProtected(o, p & ~Protect.LastCopy))))
            {
                if (picked.Count >= count) break;
                if ((p & Protect.LastCopy) != 0 && left[f.id] <= 1) continue;
                picked.Add(f); left[f.id]--;
            }
            return picked;
        }

        // Auto-fill: the cheapest safe fodder for h (may be fewer than h.stars when not enough is safe).
        // `keep` are slots the player already filled by hand.
        public List<HeroSave> AutoFill(HeroSave h, IList<HeroSave> keep = null, Protect p = Protect.Fodder)
        {
            keep ??= new List<HeroSave>();
            var chosen = keep.Where(x => x != null).ToList();
            var gone = new HashSet<int>(chosen.Select(x => x.uid));
            var more = PickFodder(h.stars, h.stars - chosen.Count, p, new HashSet<int> { h.uid }, gone);
            return chosen.Concat(more).ToList();
        }

        // ---------- rank up all ----------
        // Promotes every eligible max-level hero (or the team only) towards targetStars (0 = one star each),
        // strongest first, with cheapest safe fodder. Simulated: nothing changes until ApplyRankAll. Fodder
        // is never used twice and a promoted hero is never fodder. Stops a hero when gold would run out.
        // trainToCap also buys the levels needed (gold to XP), off by default.
        public RankAllPlan PlanRankAll(int targetStars, bool teamOnly, bool trainToCap, Protect p = Protect.Fodder)
        {
            var plan = new RankAllPlan();
            int gold = S.gold;
            var gone = new HashSet<int>();      // used as fodder
            var promoted = new HashSet<int>();
            var pool = (teamOnly ? Team : S.heroes.ToList()).OrderByDescending(Power).ThenBy(x => x.uid).ToList();
            foreach (var h in pool)
            {
                if (gone.Contains(h.uid)) continue;
                int goal = targetStars <= 0 ? Math.Min(MaxStars, h.stars + 1) : Math.Min(MaxStars, targetStars);
                int stars = h.stars, level = h.level, xp = h.xp;
                if (stars >= goal) { if (teamOnly && stars >= MaxStars) plan.Skipped.Add((h, "Already 6★")); continue; }
                while (stars < goal)
                {
                    int cap = MaxLevelFor(stars), train = 0;
                    if (level < cap)
                    {
                        if (!trainToCap) { if (teamOnly || stars > h.stars) plan.Skipped.Add((h, stars > h.stars ? $"Needs training to Lv{cap} for {stars + 1}★" : $"Below max level (Lv{level}/{cap})")); break; }
                        int need = -xp; for (int l = level; l < cap; l++) need += XpToNext(l);
                        train = (int)Math.Ceiling(Math.Max(0, need) / (double)TrainXpPerGold);
                    }
                    var skip = new HashSet<int>(promoted) { h.uid };
                    var fod = PickFodder(stars, stars, p, skip, gone);
                    if (fod.Count < stars) { plan.Skipped.Add((h, $"Not enough safe fodder at {stars}★ ({fod.Count} of {stars})")); break; }
                    int cost = RankUpGold(stars) + train;
                    if (cost > gold) { plan.Skipped.Add((h, "Gold runs out")); plan.GoldShort = true; break; }
                    gold -= cost;
                    plan.Steps.Add(new RankStep { Hero = h, From = stars, To = stars + 1, Fodder = fod, Gold = RankUpGold(stars), TrainGold = train });
                    foreach (var f in fod) gone.Add(f.uid);
                    promoted.Add(h.uid);
                    stars++; level = cap; xp = 0;
                }
            }
            plan.Gold = plan.Steps.Sum(s => s.Gold + s.TrainGold);
            plan.FodderUsed = plan.Steps.Sum(s => s.Fodder.Count);
            return plan;
        }

        // Carries out the plan step by step, checking each again; stops at the first step that no longer
        // works. Returns how many steps were done. The caller saves once afterwards.
        public int ApplyRankAll(RankAllPlan plan)
        {
            int done = 0;
            foreach (var st in plan.Steps)
            {
                var h = st.Hero;
                if (!S.heroes.Contains(h) || h.stars != st.From || st.Fodder.Any(f => !S.heroes.Contains(f))) break;
                if (st.TrainGold > 0 && h.level < MaxLevelFor(h.stars) && !Train(h, true)) break;
                if (!RankUp(h, st.Fodder)) break;
                done++;
            }
            return done;
        }
    }
}
