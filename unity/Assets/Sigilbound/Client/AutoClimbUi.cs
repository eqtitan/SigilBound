// Auto Climb in battle (owner request 2026-11-28; rules in Core/AutoClimb.cs): the Climb switch on the battle
// HUD and the results screens, the pause between fights, and the Climb Summary when the climb stops.
// Climb keeps Auto battle on; it runs at 30 fps like auto-replay (Quality.ReplayCapped via Replaying) and goes
// on in the background on desktop when "keep running" is on (Background.cs), since it only uses real-time waits.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class BattleController
    {
        ClimbLog _climb;                 // this climb's totals; null when no climb is running
        ClimbTarget _climbNext;          // the next fight, while the results screen counts down to it
        bool _climbPending;              // the countdown is running
        float _climbAt;                  // when the next fight starts (realtime)
        bool _climbRetreat;              // the player left mid-fight (an arena retreat is booked as a loss)
        int _arenaSlot = 1;              // which rival the arena climb fights: 0 Easy, 1 Even, 2 Hard
        const float ClimbPause = 3f;     // the same fixed pause as auto-replay, not sped up by 2x/3x

        static ClimbArea? AreaOf(BattleSpec s) => s == null || s.Practice ? (ClimbArea?)null : s.Mode switch
        {
            BattleMode.Stage => ClimbArea.Normal, BattleMode.Hard => ClimbArea.Hard,
            BattleMode.Dungeon => ClimbArea.Dungeon, BattleMode.Arena => ClimbArea.Arena, _ => (ClimbArea?)null,
        };
        bool CanClimb => AreaOf(_spec) != null;
        bool Climbing => CanClimb && G.S.settings.autoClimb;
        int IndexOf(BattleSpec s) => s.Mode == BattleMode.Dungeon ? s.Floor : s.Mode == BattleMode.Arena ? _arenaSlot : s.Stage;
        string WhereOf(BattleSpec s) => AutoClimb.Where(AreaOf(s) ?? ClimbArea.Normal, IndexOf(s), s.Dungeon, s.Rival?.Name);
        static int SlotOf(Rival rv) => rv == null ? 1 : rv.Label == "Easy" ? 0 : rv.Label == "Hard" ? 2 : 1;

        BattleSpec SpecFor(ClimbArea a, int index, DungeonKey key) => a switch
        {
            ClimbArea.Normal => BattleSpec.ForStage(index),
            ClimbArea.Hard => BattleSpec.ForHard(index),
            ClimbArea.Dungeon => BattleSpec.ForDungeon(key, index),
            _ => BattleSpec.ForArena(G.CurrentRivals()[Mathf.Clamp(index, 0, 2)]),
        };

        // The HUD and results switch. Turning it on turns Replay off and Auto on; off during the countdown stops the climb.
        void ToggleClimb()
        {
            bool on = !G.S.settings.autoClimb;
            AutoClimb.SetClimb(G.S.settings, on); App.Save();
            App.Toast(on ? "Auto Climb on: after each win the next fight starts, until a loss" : "Auto Climb off");
            if (!on) { if (_climbPending) StopClimb(ClimbStop.ByPlayer); return; }
            // On at a victory screen: climb on from here (the next fight after this one).
            if (_finished && _b != null && _b.Result == BattleResult.Victory && !_climbPending && CanClimb && _climb == null)
            {
                var area = AreaOf(_spec).Value;
                var next = AutoClimb.NextTarget(G, area, IndexOf(_spec), _spec.Dungeon);
                if (next.Ok) { _climb = new ClimbLog(area, _spec.Dungeon); StartClimbWait(next); }
                else App.Toast(AutoClimb.StopText(next.Why, WhereOf(_spec)));
            }
        }

        // Called by each Finish* once the result is booked. True when the climb took the result over (the summary
        // shows instead of the results screen); false lets the mode show its results, counting down if one is next.
        bool ClimbResult(bool won, Reward r, int gear, List<(HeroSave hero, int levels)> ups)
        {
            var area = AreaOf(_spec);
            if (area == null || (_climb == null && !Climbing)) return false;
            _climb ??= new ClimbLog(area.Value, _spec.Dungeon);
            string where = WhereOf(_spec);
            if (won) _climb.Win(r, gear, ups?.Select(l => $"{Hero(l.hero.id).ShortName} Lv {l.hero.level}"), where);
            else _climb.Loss(where);
            if (_climbRetreat || !Climbing) { _climbRetreat = false; StopClimb(ClimbStop.ByPlayer, where); return true; }
            var next = AutoClimb.AfterFight(G, area.Value, IndexOf(_spec), won, _spec.Dungeon);
            if (!next.Ok) { StopClimb(next.Why, where); return true; }
            StartClimbWait(next);
            return false;
        }

        void StartClimbWait(ClimbTarget next)
        {
            _climbNext = next; _climbPending = true; _climbAt = Time.realtimeSinceStartup + ClimbPause;
            StartCoroutine(ClimbWait());
        }

        IEnumerator ClimbWait()
        {
            yield return null;                    // the results screen opens this frame
            int serial = App.ModalSerial;
            while (_climbPending && Time.realtimeSinceStartup < _climbAt) yield return null;
            if (!_climbPending) yield break;      // stopped from the results screen
            if (App.Screen != ScreenId.Battle || !_finished || _climb == null) { _climbPending = false; yield break; }
            if (!Climbing) { StopClimb(ClimbStop.ByPlayer); yield break; }
            if (App.ModalSerial != serial) { StopClimb(ClimbStop.NeedsYou); yield break; } // something else asked for the player
            if (!App.ModalOpen) { StopClimb(ClimbStop.ByPlayer); yield break; }
            var area = AreaOf(_spec).Value; var key = _spec.Dungeon; int from = IndexOf(_spec);
            // Still there? (A day can roll over during the pause.)
            var check = AutoClimb.NextTarget(G, area, from, key);
            if (!check.Ok) { StopClimb(check.Why); yield break; }
            _climbPending = false;
            if (area == ClimbArea.Arena) _arenaSlot = _climbNext.Index;
            var spec = SpecFor(area, _climbNext.Index, key);
            // A new story beat plays once: the Act II intro when a Normal climb crosses into Act II.
            if (AutoClimb.PlaysActIntro(area, from, spec.Stage, PlayerPrefs.GetInt("sb.act2Intro", 0) == 1))
            {
                PlayerPrefs.SetInt("sb.act2Intro", 1); PlayerPrefs.Save();
                App.Modal(Act2Title, 300 + 56, b =>
                {
                    Ui.Text(new Rect(b.x, b.y, b.width, b.height - 64), Act2Intro, 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                    if (Ui.Button(new Rect(b.x, b.yMax - Ui.Tap, b.width, Ui.Tap), "Keep climbing")) { App.CloseModal(); BeginClimbFight(spec); }
                });
                yield break;
            }
            App.CloseModal();
            BeginClimbFight(spec);
        }

        void BeginClimbFight(BattleSpec spec)
        {
            Begin(spec);
            if (_spec != spec) StopClimb(ClimbStop.NeedsYou); // Begin refused it (a toast says why)
        }

        // The climb ends: the summary replaces the results screen.
        void StopClimb(ClimbStop why, string where = null)
        {
            _climbPending = false;
            if (_climb == null) return;
            _climb.Finish(why, where ?? _climb.Last);
            ShowClimbSummary();
        }

        // Where "Keep climbing" carries on: the lost fight again, or the fight that was next; none at the end.
        BattleSpec ResumeSpec(ClimbLog log)
        {
            var area = log.Area; int at = IndexOf(_spec);
            bool next = log.Stop == ClimbStop.ByPlayer || log.Stop == ClimbStop.NeedsYou;
            if (next && _climbNext.Ok && _b != null && _b.Result == BattleResult.Victory) at = _climbNext.Index;
            else if (log.Stop != ClimbStop.Lost && log.Stop != ClimbStop.ByPlayer && log.Stop != ClimbStop.NeedsYou) return null;
            bool open = area switch
            {
                ClimbArea.Normal => at <= G.S.cleared && at < TotalStages,
                ClimbArea.Hard => G.HardOpen(at),
                ClimbArea.Dungeon => G.CanEnterFloor(log.Dungeon, at),
                _ => G.ArenaOpen && G.S.daily.arenaTries > 0,
            };
            return open ? SpecFor(area, at, log.Dungeon) : null;
        }

        // ---------- Climb Summary ----------
        void ShowClimbSummary()
        {
            var log = _climb; _climb = null; _climbPending = false;
            if (log == null) return;
            if (_loop != null && !_finished) { StopCoroutine(_loop); ResetCinema(); }
            _finished = true; Time.timeScale = 1; _paused = false;
            var resume = ResumeSpec(log);
            var spec = _spec; int slot = _arenaSlot;
            float h = Ui.Landscape ? Mathf.Min(Ui.H - 24, 390) : 430;
            App.Modal("Climb Summary", h, r => DrawClimbSummary(r, log, resume, spec, slot), App.ModalW);
        }

        // replay: the Replay run's summary in the same layout (ShowAutoSummary), with Again instead of Keep climbing.
        void DrawClimbSummary(Rect r, ClimbLog log, BattleSpec resume, BattleSpec spec, int slot, bool replay = false)
        {
            float y = r.y - 6;
            string span = log.First.Length > 0 && log.Last != log.First ? $"{log.First}  →  {log.Last}" : log.Last;
            Ui.Text(new Rect(r.x, y, r.width, 18), $"{(replay ? "Replay" : AutoClimb.AreaName(log.Area, log.Dungeon))}   ·   {span}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            y += 22;
            // The headline: how far the climb went.
            Ui.Text(new Rect(r.x, y, r.width, 34), $"{log.Cleared} {log.ClearedNoun}", 24, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            y += 34;
            Ui.Text(new Rect(r.x, y, r.width, 16), $"{log.Fights} fight{(log.Fights == 1 ? "" : "s")} in all{(log.Lost > 0 ? $" · {log.Lost} lost" : "")}", 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            y += 22;
            // Why it stopped: an icon and words (not only a colour).
            var (ic, col) = log.Stop switch
            {
                ClimbStop.Lost => (Icon.Skull, Palette.Bad),
                ClimbStop.ReachedEnd => (Icon.Crown, Palette.Gold),
                ClimbStop.OutOfRuns or ClimbStop.OutOfTries => (Icon.Clock, Palette.Blue),
                ClimbStop.Locked => (Icon.Lock, Palette.Dim),
                ClimbStop.ByPlayer => (Icon.Pause, Palette.Text),
                _ => (Icon.Info, Palette.Text),
            };
            var why = new Rect(r.x + 10, y, r.width - 20, 32);
            Ui.Round(why, new Color(0.1f, 0.08f, 0.14f, 0.9f), col, 10);
            Icons.Draw(new Rect(why.x + 10, why.y + 7, 18, 18), ic, col);
            Ui.Text(new Rect(why.x + 36, why.y, why.width - 44, why.height), log.Reason, 13, Palette.Text, FontStyle.Bold);
            y += 42;
            // Rewards: two columns of icon + amount, each shard kind named.
            // The same item pictures as the top bar and results (Icons.Currency).
            var items = new List<(Icon icon, int cur, Color c, string text)> { (Icon.Coin, (int)Icons.Cur.Gold, Palette.Gold, $"{Ui.Fmt(log.Gold)} gold") };
            if (log.Xp > 0) items.Add((Icon.Star, -1, Palette.Blue, $"{Ui.Fmt(log.Xp)} XP each"));
            if (log.Common > 0) items.Add((Icon.Shard, (int)Icons.Cur.WornShard, Palette.Hex("#c9a37b"), $"{log.Common} {ShardName(Shard.Common, log.Common)}"));
            if (log.Mystic > 0) items.Add((Icon.Shard, (int)Icons.Cur.MysticShard, Palette.Blue, $"{log.Mystic} {ShardName(Shard.Mystic, log.Mystic)}"));
            if (log.Radiant > 0) items.Add((Icon.Shard, (int)Icons.Cur.RadiantShard, Palette.Hex("#ffb02e"), $"{log.Radiant} {ShardName(Shard.Radiant, log.Radiant)}"));
            if (log.Gear > 0) items.Add((Icon.Shield, -1, Palette.GearRarity(3), $"{log.Gear} gear piece{(log.Gear == 1 ? "" : "s")}"));
            if (log.Essence > 0) items.Add((Icon.Essence, (int)Icons.Cur.Essence, Palette.Hex("#8fd8ff"), $"{log.Essence} essence"));
            if (log.Tome > 0) items.Add((Icon.Tome, (int)Icons.Cur.Tome, Palette.Gold, $"{log.Tome} {TomeName(log.Tome)}"));
            int cols = 2; float cw = (r.width - 20) / cols, rh = 24;
            for (int i = 0; i < items.Count; i++)
            {
                float ix = r.x + 10 + (i % cols) * cw, iy = y + (i / cols) * rh;
                var ir = new Rect(ix + 4, iy + 3, 18, 18);
                if (items[i].cur >= 0) Icons.Currency(ir, (Icons.Cur)items[i].cur); else Icons.Draw(ir, items[i].icon, items[i].c);
                Ui.Text(new Rect(ix + 28, iy, cw - 30, rh), items[i].text, 13, Palette.Text, FontStyle.Bold);
            }
            y += Mathf.Ceil(items.Count / (float)cols) * rh + 6;
            if (log.LevelUps.Count > 0)
                Ui.Text(new Rect(r.x + 10, y, r.width - 20, 34), "Level ups: " + string.Join(", ", log.LevelUps.Take(6)), 12, Palette.Good, FontStyle.Normal, TextAnchor.UpperCenter, true);

            // Buttons through the shared row (design rule 3): Back to <place> and Edit Team, the Primary (Keep
            // climbing, or Again after a Replay run) last; they wrap to two rows on a narrow modal.
            void EditTeam()
            {
                switch (log.Area)
                {
                    case ClimbArea.Dungeon: End(ScreenId.Dungeon); App.EditDungeonTeam(log.Dungeon, resume?.Floor ?? spec.Floor); break;
                    case ClimbArea.Arena:
                        // Team setup for the arena too (it opened the Heroes list).
                        End(ScreenId.ArenaPvp);
                        var rivals = G.CurrentRivals();
                        if (rivals.Count > 0 && G.S.daily.arenaTries > 0) App.OpenArenaPrep(rivals[Mathf.Clamp(slot, 0, rivals.Count - 1)]);
                        break;
                    default: End(ScreenId.Campaign); App.EditTeam(resume?.Stage ?? spec.Stage, log.Area == ClimbArea.Hard); break;
                }
            }
            void Keep() { AutoClimb.SetClimb(G.S.settings, true); App.Save(); App.CloseModal(); _arenaSlot = slot; _climb = null; Begin(resume); }
            var back = log.Area == ClimbArea.Arena ? ScreenId.ArenaPvp : log.Area == ClimbArea.Dungeon ? ScreenId.Dungeon : ScreenId.Campaign;
            string place = log.Area == ClimbArea.Arena ? "Arena" : log.Area == ClimbArea.Dungeon ? "Dungeons" : "Map";
            float bh = Mathf.Max(44, Ui.Tap), rowH = Ui.ButtonRowHeight(r.width, 3);
            var row = new[] { (Words.BackTo(place), UiRole.Secondary, true), (Words.EditTeam, UiRole.Secondary, true), replay ? (Words.Again, UiRole.Primary, true) : ("Keep climbing", UiRole.Primary, resume != null) };
            int hit = Ui.ButtonRow(new Rect(r.x, r.yMax - rowH, r.width, rowH), row);
            if (hit == 0) End(back);
            else if (hit == 1) EditTeam();
            else if (hit == 2) { if (replay) { App.CloseModal(); Begin(spec); } else Keep(); }
        }

        // ---------- the switches on the results screens ----------
        // An on/off chip: the icon and the word On/Off on the shared ChipOn / Chip plates (ChipOn draws the one
        // selected mark: rim, underline and check), not only a colour.
        static bool ToggleChip(Rect r, Icon icon, string name, bool on)
        {
            bool hit = Ui.Button(r, "", on ? Ui.Kind.ChipOn : Ui.Kind.Chip);
            float s = Mathf.Min(20, r.height - 20);
            Icons.Draw(new Rect(r.x + 10, r.center.y - s / 2, s, s), icon, on ? Palette.Gold : Palette.Text);
            Ui.Text(new Rect(r.x + 16 + s, r.y, r.width - s - 24, r.height), $"{name}: {(on ? "On" : "Off")}", 13, on ? Palette.Gold : Palette.Text, FontStyle.Bold);
            return hit;
        }

        // How many switches the results footer shows: Replay (campaign Normal only) and Climb.
        int ToggleCount => !CanClimb ? 0 : _spec != null && _spec.Mode == BattleMode.Stage ? 2 : 1;
        // Replay and Climb, side by side in `row` (Core/UiLayout.ResultsFooter sizes it); returns the width used.
        float ResultToggles(Rect row)
        {
            if (!CanClimb) return 0;
            bool replay = _spec.Mode == BattleMode.Stage;
            int n = replay ? 2 : 1; float g = 8, w = UiLayout.ToggleW(Ui.ToBox(row), n), x = row.x;
            if (replay) { if (ToggleChip(new Rect(x, row.y, w, row.height), Icon.Loop, "Replay", G.S.settings.autoReplay)) ToggleAutoReplay(); x += w + g; }
            if (ToggleChip(new Rect(x, row.y, w, row.height), Icon.Climb, "Climb", G.S.settings.autoClimb)) ToggleClimb();
            return n * w + (n - 1) * g;
        }

        // While the countdown runs, the results screen's own buttons give way to the climb's line and a Stop button.
        bool ClimbBar(Rect r)
        {
            if (!_climbPending) return false;
            float bh = Mathf.Max(44, Ui.Tap);
            int left = Mathf.Max(1, Mathf.CeilToInt(_climbAt - Time.realtimeSinceStartup));
            string next = AutoClimb.Where(_climb?.Area ?? ClimbArea.Normal, _climbNext.Index, _spec.Dungeon, _climb?.Area == ClimbArea.Arena ? "the next rival" : null);
            string line = $"Climbing · {next} in {left}…   ({_climb?.Cleared ?? 0} cleared, {Ui.Fmt(_climb?.Gold ?? 0)} gold so far)";
            var lr = new Rect(r.x, r.yMax - bh - 26, r.width, 22);
            Icons.Draw(new Rect(lr.center.x - Ui.Label(12).CalcSize(new GUIContent(line)).x / 2 - 20, lr.y + 3, 16, 16), Icon.Climb, Palette.Gold);
            Ui.Text(lr, line, 12, Palette.Hex("#e8d9b0"), FontStyle.Bold, TextAnchor.MiddleCenter);
            float bw = Mathf.Min(260, r.width);
            if (Ui.Button(new Rect(r.center.x - bw / 2, r.yMax - bh, bw, bh), "Stop climbing", Ui.Kind.Danger))
            {
                AutoClimb.SetClimb(G.S.settings, false); App.Save();
                StopClimb(ClimbStop.ByPlayer);
            }
            return true;
        }
    }
}
