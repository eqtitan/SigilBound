// Auto Climb (owner request 2026-11-28): after each win the next battle of the same area starts by itself,
// in Auto battle, until the first loss or until there is nothing next. Unity-free so Tests~ can check it;
// Client/BattleController.cs drives the fights, and its Climb Summary shows the ClimbLog.
//   Normal  -> the next campaign stage in order (across chapters and into Act II as they unlock)
//   Hard    -> the next Hard stage, while it is unlocked (the Normal stage must be cleared)
//   Dungeon -> the next floor of the same dungeon, while today's winning runs remain
//   Arena   -> the next opponent (same slot: Easy, Even or Hard), while today's fights remain
// Climb and Replay (same stage again) are mutually exclusive; Climb keeps Auto battle on while it runs.
// Between fights nothing else happens: no upgrades, no team changes.
using System;
using System.Collections.Generic;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Core
{
    public enum ClimbArea { Normal, Hard, Dungeon, Arena }

    // Why a climb stopped (None while it goes on).
    public enum ClimbStop { None, Lost, ReachedEnd, Locked, OutOfRuns, OutOfTries, ByPlayer, NeedsYou }

    public readonly struct ClimbTarget
    {
        public readonly bool Ok; public readonly int Index; public readonly ClimbStop Why;
        public ClimbTarget(bool ok, int index, ClimbStop why) { Ok = ok; Index = index; Why = why; }
        public static ClimbTarget Go(int index) => new ClimbTarget(true, index, ClimbStop.None);
        public static ClimbTarget Stop(ClimbStop why) => new ClimbTarget(false, -1, why);
    }

    public static class AutoClimb
    {
        // The next fight after winning `current` (a stage index, a floor, or the arena slot 0-2), or why there is none.
        public static ClimbTarget NextTarget(Game g, ClimbArea area, int current, DungeonKey dungeon = DungeonKey.Vault)
        {
            switch (area)
            {
                case ClimbArea.Normal:
                {
                    int next = current + 1;
                    if (next >= TotalStages) return ClimbTarget.Stop(ClimbStop.ReachedEnd);
                    if (next > g.S.cleared) return ClimbTarget.Stop(ClimbStop.Locked);
                    return ClimbTarget.Go(next);
                }
                case ClimbArea.Hard:
                {
                    int next = current + 1;
                    if (next >= TotalStages) return ClimbTarget.Stop(ClimbStop.ReachedEnd);
                    if (!g.HardOpen(next)) return ClimbTarget.Stop(ClimbStop.Locked);
                    return ClimbTarget.Go(next);
                }
                case ClimbArea.Dungeon:
                {
                    int next = current + 1;
                    if (g.DungeonRunsLeft(dungeon) <= 0) return ClimbTarget.Stop(ClimbStop.OutOfRuns);
                    if (next >= Trials.DungeonFloors) return ClimbTarget.Stop(ClimbStop.ReachedEnd);
                    if (!g.CanEnterFloor(dungeon, next)) return ClimbTarget.Stop(ClimbStop.Locked);
                    return ClimbTarget.Go(next);
                }
                default:
                {
                    if (!g.ArenaOpen) return ClimbTarget.Stop(ClimbStop.Locked);
                    g.EnsureModes();
                    if (g.S.daily.arenaTries <= 0) return ClimbTarget.Stop(ClimbStop.OutOfTries);
                    return ClimbTarget.Go(Math.Max(0, Math.Min(2, current))); // the same slot of the new rivals
                }
            }
        }

        // After a fight: a loss always stops the climb; a win goes on to NextTarget.
        public static ClimbTarget AfterFight(Game g, ClimbArea area, int current, bool won, DungeonKey dungeon = DungeonKey.Vault) =>
            won ? NextTarget(g, area, current, dungeon) : ClimbTarget.Stop(ClimbStop.Lost);

        // ---------- the Replay / Climb / Auto switches ----------
        // Turning Climb on turns Replay off and Auto battle on; turning Replay on turns Climb off.
        public static void SetClimb(SettingsSave s, bool on)
        {
            s.autoClimb = on;
            if (on) { s.autoReplay = false; s.auto = true; }
        }
        public static void SetReplay(SettingsSave s, bool on)
        {
            s.autoReplay = on;
            if (on) s.autoClimb = false;
        }
        // Auto battle as the fight sees it: the player's switch, or forced on while climbing.
        public static bool AutoBattle(SettingsSave s, bool climbing) => s.auto || climbing;

        // ---------- presentation while climbing ----------
        // A lair reveal plays once: skipped when seen this session, and while climbing also when that boss stage
        // was already cleared before this fight (the player has watched it on the way through before).
        public static bool SkipLairReveal(bool climbing, bool seenThisSession, bool clearedBefore) => seenThisSession || (climbing && clearedBefore);

        // The one-time Act II intro plays when a Normal climb crosses from Act I into Act II and it hasn't been seen.
        public static bool PlaysActIntro(ClimbArea area, int from, int to, bool seen) =>
            area == ClimbArea.Normal && !seen && from < ClassicStages && to >= ClassicStages;

        public static string Where(ClimbArea area, int index, DungeonKey dungeon = DungeonKey.Vault, string rival = null) => area switch
        {
            ClimbArea.Normal => $"Stage {Game.StageLabel(index)}",
            ClimbArea.Hard => $"Hard {Game.StageLabel(index)}",
            ClimbArea.Dungeon => $"{Trials.Dungeon(dungeon).Name} floor {index + 1}",
            _ => string.IsNullOrEmpty(rival) ? "the arena" : $"vs {rival}",
        };

        public static string AreaName(ClimbArea area, DungeonKey dungeon = DungeonKey.Vault) => area switch
        {
            ClimbArea.Normal => "Campaign",
            ClimbArea.Hard => "Hard campaign",
            ClimbArea.Dungeon => Trials.Dungeon(dungeon).Name,
            _ => "Arena",
        };

        // What the summary says about the stop.
        public static string StopText(ClimbStop why, string where) => why switch
        {
            ClimbStop.Lost => $"Lost at {where}",
            ClimbStop.ReachedEnd => "Reached the end",
            ClimbStop.Locked => $"The next stage after {where} is locked",
            ClimbStop.OutOfRuns => "Out of dungeon runs for today",
            ClimbStop.OutOfTries => "Out of arena fights for today",
            ClimbStop.ByPlayer => "Stopped by you",
            ClimbStop.NeedsYou => "Stopped: a message needs you",
            _ => "Climbing",
        };
    }

    // One climb's running totals: fights, clears and every reward, for the Climb Summary.
    public sealed class ClimbLog
    {
        public ClimbArea Area; public DungeonKey Dungeon;
        public int Fights, Cleared, Lost;
        public int Gold, Xp, Common, Mystic, Radiant, Essence, Tome, Gear;
        public readonly List<string> LevelUps = new List<string>();
        public string First = "", Last = "";            // where the climb started, and the last fight
        public ClimbStop Stop = ClimbStop.None; public string StopWhere = "";

        public ClimbLog(ClimbArea area, DungeonKey dungeon = DungeonKey.Vault) { Area = area; Dungeon = dungeon; }

        public bool Stopped => Stop != ClimbStop.None;

        public void Win(Reward r, int gear, IEnumerable<string> levelUps, string where)
        {
            Fights++; Cleared++; Note(where);
            if (r != null) { Gold += r.Gold; Xp += r.Xp; Common += r.Common; Mystic += r.Mystic; Radiant += r.Radiant; Essence += r.Essence; Tome += r.Tome; }
            Gear += Math.Max(0, gear);
            if (levelUps != null) foreach (var l in levelUps) if (!LevelUps.Contains(l)) LevelUps.Add(l);
        }
        public void Loss(string where) { Fights++; Lost++; Note(where); }
        void Note(string where) { if (Fights == 1) First = where ?? ""; Last = where ?? ""; }

        // The first stop wins (a later "stopped by you" doesn't hide the loss that ended it).
        public void Finish(ClimbStop why, string where)
        {
            if (Stopped || why == ClimbStop.None) return;
            Stop = why; StopWhere = where ?? Last;
        }

        public string ClearedNoun => Area switch
        {
            ClimbArea.Dungeon => Cleared == 1 ? "floor cleared" : "floors cleared",
            ClimbArea.Arena => Cleared == 1 ? "fight won" : "fights won",
            _ => Cleared == 1 ? "stage cleared" : "stages cleared",
        };
        public string Reason => AutoClimb.StopText(Stop, StopWhere);

        // Reward lines for the summary (shards by name: worn, mystic, radiant).
        public List<string> RewardLines()
        {
            var l = new List<string> { $"{Gold:N0} gold" };
            if (Xp > 0) l.Add($"{Xp:N0} XP each");
            if (Common > 0) l.Add($"{Common} {ShardName(Shard.Common, Common)}");
            if (Mystic > 0) l.Add($"{Mystic} {ShardName(Shard.Mystic, Mystic)}");
            if (Radiant > 0) l.Add($"{Radiant} {ShardName(Shard.Radiant, Radiant)}");
            if (Essence > 0) l.Add($"{Essence} essence");
            if (Tome > 0) l.Add($"{Tome} Skill Tome{(Tome == 1 ? "" : "s")}");
            if (Gear > 0) l.Add($"{Gear} gear piece{(Gear == 1 ? "" : "s")}");
            return l;
        }
    }
}
