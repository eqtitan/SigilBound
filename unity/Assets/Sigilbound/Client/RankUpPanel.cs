// Rank Up panel (owner decisions 2026-11-28; mock-up 3 of research BULK_RELEASE_AUTO_RANKUP, laid out for the
// 390-wide menu column): the target card, fodder slots the player can change (tap to pick, × to clear),
// Auto-fill with the cheapest safe fodder (Core/Roster.cs, the same protection rules as release), hand-picked
// protected heroes flagged (Epic+ and last copies need a 1-second hold), and "Rank up all to X★" with a
// gold/fodder preview. No auto rank-up after battles in v1.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        int _rankUid;
        readonly List<HeroSave> _slots = new List<HeroSave>();
        int _slotPick;
        bool _rankTrain;                  // "Also train to cap with gold" (not saved: it can spend a lot)
        string _rankKey; RankAllPlan _rankPlan;
        float _rankH = 900;

        // Hero screen → Rank Up panel, slots pre-filled by Auto-fill.
        void OpenRankUp(HeroSave h)
        {
            _rankUid = h.uid;
            FillSlots(h, true);
            OpenHeroesPanel(RosterPanel.RankUp, ScreenId.Hero);
        }
        void OpenRankAll() { _rankUid = 0; OpenHeroesPanel(RosterPanel.RankAll, ScreenId.Heroes); }

        void FillSlots(HeroSave h, bool fresh)
        {
            var keep = fresh ? new List<HeroSave>() : _slots.Where(x => x != null && G.S.heroes.Contains(x)).ToList();
            _slots.Clear();
            if (h == null || h.stars >= MaxStars) return;
            _slots.AddRange(G.AutoFill(h, keep));
            while (_slots.Count < h.stars) _slots.Add(null);
        }

        void DrawRankUpPanel(Rect area)
        {
            float T = RosterLayout.Tap;
            var h = _rp == RosterPanel.RankUp ? G.HeroByUid(_rankUid) : null;
            if (_rp == RosterPanel.RankUp && h == null) { OpenPanel(RosterPanel.RankAll); return; }
            if (h != null && _slots.Count != h.stars) FillSlots(h, false);
            _rpScroll = Scroller.Begin(area, _rpScroll, _rankH);
            float y = 8;
            string back = h != null && _rpBack == ScreenId.Hero ? "‹ " + Hero(h.id).ShortName : "‹ Heroes";
            if (Ui.Button(new Rect(Pad, y, RosterLayout.BackW, T), back, Ui.Kind.Secondary, 13)) RosterBack();
            Ui.Banner(new Rect(Pad + RosterLayout.BackW + 8, y, RW - RosterLayout.BackW - 8, T), h != null ? "Rank Up" : "Rank Up All", 18);
            y += T + 10;
            if (h != null) y = DrawRankTarget(y, h);
            y = DrawRankAll(y, h != null);
            _rankH = y + 16;
            Scroller.End();
            if (_rankH > area.height + 1) ScrollFade(area, _rpScroll.y, _rankH);
        }

        float DrawRankTarget(float y, HeroSave h)
        {
            float T = RosterLayout.Tap, G6 = RosterLayout.Gap;
            var t = Hero(h.id);
            int cap = MaxLevelFor(h.stars); bool atCap = h.level >= cap, maxed = h.stars >= MaxStars;
            // Target card.
            var c = new Rect(Pad, y, RW, 132);
            Ui.Card(c);
            HeroFace(new Rect(c.x + 14, c.y + 16, 80, 80), t);
            Ui.RarityTab(new Rect(c.x + 14, c.y + 16, 80, 80), t.Rarity, 16);
            float tx = c.x + 108, tw = c.width - 122;
            Ui.Text(new Rect(tx, c.y + 10, tw, 24), t.Name, 15, Palette.Text, FontStyle.Bold);
            if (maxed) Ui.Text(new Rect(tx, c.y + 36, tw, 24), Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold) + "  Fully ascended!", 16, Palette.Text);
            else
            {
                Ui.Text(new Rect(tx, c.y + 34, tw, 26), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)}  →  {Ui.ColorTag(Ui.Stars(h.stars + 1), Palette.Gold)}", 18, Palette.Text);
                Ui.Text(new Rect(tx, c.y + 62, tw, 20), $"Lv {h.level}/{cap}  →  cap {MaxLevelFor(h.stars + 1)}", 12, atCap ? Palette.Dim : Palette.Bad);
                int p0 = G.Power(h); h.stars++; int p1 = G.Power(h); h.stars--;
                Ui.Text(new Rect(tx, c.y + 82, tw, 20), Ui.ColorTag("Stats +15%", Palette.Good) + $"  ·  Power {Ui.Fmt(p0)} → {Ui.Fmt(p1)}", 12, Palette.Dim);
                if (!atCap) Ui.Text(new Rect(tx, c.y + 102, tw, 20), $"Reach Lv{cap} first (Train on the hero screen)", 11, Palette.Bad);
            }
            y += c.height + 12;
            if (maxed) return y;

            // Fodder slots.
            var slotWarn = _slots.Select(f => f == null ? null : G.ProtectWarning(f)).ToList();
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), $"Fodder  ({h.stars} {(h.stars == 1 ? "hero" : "heroes")} at {h.stars}★)"); y += RosterLayout.LabelH + 2;
            int cols = Mathf.Min(RosterLayout.SlotCols, h.stars); float sg = RosterLayout.SlotGap, sh = RosterLayout.SlotH;
            float sw = (RW - (cols - 1) * sg) / cols;
            for (int i = 0; i < _slots.Count; i++)
            {
                var r = new Rect(Pad + (i % cols) * (sw + sg), y + (i / cols) * (sh + sg), sw, sh);
                var f = _slots[i];
                if (f != null)
                {
                    // × first, so it gets the tap before the slot underneath.
                    var x = new Rect(r.xMax - T, r.y, T, T);
                    bool clear = Ui.Button(x, "", Ui.Kind.Ghost);
                    DrawSlot(r, f, slotWarn[i]);
                    Ui.Text(new Rect(r.xMax - 26, r.y + 2, 24, 24), "×", 18, Palette.Dim, FontStyle.Bold, TextAnchor.MiddleCenter);
                    if (clear) { _slots[i] = null; continue; }
                }
                else
                {
                    Ui.Panel(r, Palette.Hex("#120e19"), Palette.Bronze, 10);
                    Ui.Text(r, "+ Pick", 14, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
                }
                if (Ui.Button(r, "", Ui.Kind.Ghost)) { _slotPick = i; OpenPanel(RosterPanel.RankPick); }
            }
            y += ((_slots.Count + cols - 1) / cols) * (sh + sg) + 4;

            if (Ui.Button(new Rect(Pad, y, RW, T), "Auto-fill (cheapest)", Ui.Kind.Secondary, 14))
            {
                FillSlots(h, false);
                if (_slots.Any(s => s == null)) Toast($"Only {_slots.Count(s => s != null)} of {h.stars} safe fodder. Pick the rest by hand.");
            }
            y += T + 4;
            Ui.Text(new Rect(Pad, y, RW, 34), "Picks the lowest rarity, then spare copies, then the lowest level. Never locked, team, saved-team, geared, Epic+ or last copies.", 11, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += 38;

            // Cost and the button.
            int gold = RankUpGold(h.stars);
            Ui.Text(new Rect(Pad, y, RW, 24), $"Cost: {Ui.ColorTag(Ui.Fmt(gold) + " gold", G.S.gold >= gold ? Palette.Gold : Palette.Bad)}   (you have {Ui.Fmt(G.S.gold)})", 14, Palette.Text);
            y += 26;
            bool full = _slots.All(s => s != null);
            bool danger = slotWarn.Any(Game.Dangerous);
            if (slotWarn.Any(w => w != null))
            {
                string names = string.Join(", ", Enumerable.Range(0, _slots.Count).Where(i => slotWarn[i] != null).Select(i => $"{Hero(_slots[i].id).ShortName} ({slotWarn[i]})"));
                Ui.Text(new Rect(Pad, y, RW, 36), (danger ? "Careful, picked by hand: " : "Picked by hand: ") + names + (danger ? ". Hold the button to confirm." : ""), 12, danger ? Palette.Bad : Palette.Hex("#e8b45a"), FontStyle.Normal, TextAnchor.UpperLeft, true);
                y += 38;
            }
            bool can = full && atCap && G.S.gold >= gold;
            var br = new Rect(Pad, y, RW, Mathf.Max(T, 48));
            string label = $"Rank Up to {Ui.Stars(h.stars + 1)}";
            bool go = danger ? HoldButton(br, "Hold: " + label, "rankup", can) : Ui.Button(br, label, Ui.Kind.Primary, 16, can);
            if (go) DoRankUp(h);
            y += br.height + 14;
            Ui.GoldRule(new Rect(Pad, y, RW, 1));
            return y + 12;
        }

        void DrawSlot(Rect r, HeroSave f, string warn)
        {
            var t = Hero(f.id);
            bool danger = Game.Dangerous(warn);
            var border = danger ? Palette.Bad : warn != null ? Palette.Hex("#e8b45a") : Palette.Rarity(t.Rarity);
            Ui.SmallCard(r, Palette.Panel2, border, 10);
            if (warn != null) Ui.Round(new Rect(r.x, r.y, r.width, 3), border, null, 1);
            float fs = 44;
            HeroFace(new Rect(r.center.x - fs / 2, r.y + 10, fs, fs), t);
            Ui.Text(new Rect(r.x + 2, r.y + 58, r.width - 4, 20), t.ShortName, 13, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(r.x + 2, r.y + 76, r.width - 4, 18), $"{Ui.ColorTag(Ui.Stars(f.stars), Palette.Gold)} Lv{f.level} · {Ui.ColorTag(RarityShort(t.Rarity), Palette.Rarity(t.Rarity))}", 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (warn != null) Ui.Text(new Rect(r.x + 2, r.y + 94, r.width - 4, 18), warn, 10, border, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        void DoRankUp(HeroSave h)
        {
            if (!G.RankUp(h, _slots.ToList())) { Toast("That rank-up isn't possible any more."); FillSlots(h, false); return; }
            Save();
            Toast($"{Hero(h.id).ShortName} is now {h.stars}★!");
            Vfx.I?.Burst(new Vector3(0, 1.5f, 0.4f), Palette.Gold, true);
            FillSlots(h, true);
        }

        // ---------- fodder picker ----------
        void DrawFodderPicker(Rect area)
        {
            float T = RosterLayout.Tap, rh = RosterLayout.PickRowH;
            var h = G.HeroByUid(_rankUid);
            if (h == null || _slotPick >= _slots.Count) { OpenPanel(RosterPanel.RankAll); return; }
            var others = new HashSet<int>(_slots.Where((s, i) => s != null && i != _slotPick).Select(s => s.uid));
            // Safe picks first, then the cheapest.
            var list = G.RankUpFodder(h).Where(f => !others.Contains(f.uid))
                .OrderBy(f => G.ProtectWarning(f) == null ? 0 : Game.Dangerous(G.ProtectWarning(f)) ? 2 : 1)
                .ThenBy(f => (int)Hero(f.id).Rarity).ThenByDescending(f => G.CopiesOf(f.id)).ThenBy(f => f.level).ThenBy(f => f.uid).ToList();
            float top = 8 + T + 8 + 50;
            float content = top + list.Count * (rh + 6) + 20;
            _rpScroll = Scroller.Begin(area, _rpScroll, content);
            float y = 8;
            if (Ui.Button(new Rect(Pad, y, RosterLayout.BackW, T), "‹ Rank Up", Ui.Kind.Secondary, 13)) OpenPanel(RosterPanel.RankUp);
            Ui.Banner(new Rect(Pad + RosterLayout.BackW + 8, y, RW - RosterLayout.BackW - 8, T), $"Pick a {h.stars}★ hero", 17);
            y += T + 8;
            Ui.Text(new Rect(Pad, y, RW, 46), list.Count == 0 ? $"No {h.stars}★ heroes can be used. Locked heroes and your team never can." : "Marked heroes are ones Auto-fill skips; you can still pick them. Locked heroes and your team can't be used.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y = top;
            int first = Mathf.Max(0, Mathf.FloorToInt((_rpScroll.y - top) / (rh + 6)) - 1);
            int last = Mathf.Min(list.Count - 1, Mathf.CeilToInt((_rpScroll.y + area.height - top) / (rh + 6)) + 1);
            for (int i = first; i <= last; i++)
            {
                var f = list[i]; var t = Hero(f.id);
                var r = new Rect(Pad, top + i * (rh + 6), RW, rh);
                string warn = G.ProtectWarning(f); bool danger = Game.Dangerous(warn);
                Ui.SmallCard(r, Palette.Panel, danger ? Palette.Bad : warn != null ? Palette.Hex("#e8b45a") : Palette.Rarity(t.Rarity), 12);
                HeroFace(new Rect(r.x + 10, r.y + (rh - 42) / 2, 42, 42), t);
                Ui.Text(new Rect(r.x + 62, r.y + 6, r.width - 180, 24), $"{Ui.ColorTag(Ui.Stars(f.stars), Palette.Gold)} {t.ShortName}  {Ui.ColorTag("Lv" + f.level, Palette.Dim)}", 14, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 62, r.y + 30, r.width - 180, 20), $"{Ui.ColorTag(t.Rarity.ToString(), Palette.Rarity(t.Rarity))} · {t.Element} · {t.Role}" + (G.CopiesOf(f.id) > 1 ? $" · ×{G.CopiesOf(f.id)}" : ""), 11, Palette.Dim);
                Ui.Text(new Rect(r.xMax - 118, r.y, 108, rh), warn ?? "Safe pick", 12, danger ? Palette.Bad : warn != null ? Palette.Hex("#e8b45a") : Palette.Good, FontStyle.Bold, TextAnchor.MiddleRight, true);
                if (Ui.Button(r, "", Ui.Kind.Ghost))
                {
                    _slots[_slotPick] = f;
                    if (danger) Toast($"{t.ShortName}: {warn}. You'll need to hold Rank Up to confirm.");
                    OpenPanel(RosterPanel.RankUp);
                }
            }
            Scroller.End();
            if (content > area.height + 1) ScrollFade(area, _rpScroll.y, content);
        }

        // ---------- rank up all ----------
        RankAllPlan RankPlan()
        {
            var p = G.S.roster;
            var key = $"{p.rankAllTarget}|{p.rankAllTeamOnly}|{_rankTrain}|{G.S.gold}|{G.S.heroes.Count}|{G.S.heroes.Sum(x => x.stars * 100 + x.level)}|{string.Join(",", G.S.team)}|{G.GetHashCode()}";
            if (key != _rankKey || _rankPlan == null) { _rankKey = key; _rankPlan = G.PlanRankAll(p.rankAllTarget, p.rankAllTeamOnly, _rankTrain); }
            return _rankPlan;
        }

        float DrawRankAll(float y, bool sub)
        {
            float T = RosterLayout.Tap, G6 = RosterLayout.Gap;
            var prefs = G.S.roster;
            if (sub) { Ui.Heading(new Rect(Pad, y, RW, 28), "Rank up all", 18); y += 30; }
            Ui.Text(new Rect(Pad, y, RW, 34), "Promote every eligible hero at max level, cheapest safe fodder first. Strongest heroes get fodder first.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += 38;
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), "Target"); y += RosterLayout.LabelH;
            string[] labels = { "Next ★", "2★", "3★", "4★", "5★", "6★" };
            int[] vals = { 0, 2, 3, 4, 5, 6 };
            float w = (RW - 5 * G6) / 6;
            for (int i = 0; i < labels.Length; i++)
                if (Chip(new Rect(Pad + i * (w + G6), y, w, T), labels[i], prefs.rankAllTarget == vals[i], Ui.FitSize(labels[i], w - 10, 13, 9, true)) && prefs.rankAllTarget != vals[i]) { prefs.rankAllTarget = vals[i]; Save(); }
            y += T + 6;
            bool team = CheckRow(new Rect(Pad, y, RW, T), prefs.rankAllTeamOnly, "Team heroes only"); y += T;
            if (team != prefs.rankAllTeamOnly) { prefs.rankAllTeamOnly = team; Save(); }
            _rankTrain = CheckRow(new Rect(Pad, y, RW, T), _rankTrain, "Also train to cap with gold"); y += T + 6;

            var plan = RankPlan();
            string preview;
            if (plan.Steps.Count == 0) preview = "Nothing to rank up" + (plan.Skipped.Count > 0 ? $": {Hero(plan.Skipped[0].h.id).ShortName}, {plan.Skipped[0].why.ToLowerInvariant()}." : ". Heroes need max level and same-star fodder.");
            else
            {
                var each = plan.Steps.GroupBy(s => s.Hero).Select(g => $"{Hero(g.Key.id).ShortName} {g.First().From}★→{g.Last().To}★").ToList();
                preview = $"Preview: {plan.Heroes} {(plan.Heroes == 1 ? "hero" : "heroes")} ranked up · {plan.FodderUsed} fodder · {Ui.ColorTag(Ui.Fmt(plan.Gold) + " gold", Palette.Gold)}"
                    + (plan.Steps.Sum(s => s.TrainGold) > 0 ? $" (training {Ui.Fmt(plan.Steps.Sum(s => s.TrainGold))})" : "")
                    + "\n" + string.Join(", ", each.Take(4)) + (each.Count > 4 ? $" +{each.Count - 4} more" : "")
                    + (plan.GoldShort ? Ui.ColorTag("  · gold runs out", Palette.Bad) : "");
            }
            Ui.Text(new Rect(Pad, y, RW, 54), preview, 12, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += 58;
            if (Ui.Button(new Rect(Pad, y, RW, Mathf.Max(T, 48)), "Rank up all ›", Ui.Kind.Primary, 15, plan.Steps.Count > 0)) AskRankAll(plan);
            return y + Mathf.Max(T, 48) + 8;
        }

        void AskRankAll(RankAllPlan plan)
        {
            var lines = plan.Steps.Select(s => $"{Ui.ColorTag(Hero(s.Hero.id).ShortName, Palette.Text)} {s.From}★→{s.To}★ · {string.Join(", ", s.Fodder.Select(f => Hero(f.id).ShortName))} · {Ui.Fmt(s.Gold + s.TrainGold)}").ToList();
            int shown = Mathf.Min(lines.Count, 8);
            float h = 170 + shown * 20 + (lines.Count > shown ? 20 : 0);
            Modal("Rank up all?", h, b =>
            {
                float T = RosterLayout.Tap;
                Ui.Text(new Rect(b.x, b.y, b.width, 22), $"{plan.Steps.Count} rank-ups · {plan.FodderUsed} fodder used up · {Ui.Fmt(plan.Gold)} gold", 13, Palette.Gold, FontStyle.Bold);
                for (int i = 0; i < shown; i++) Ui.Text(new Rect(b.x, b.y + 26 + i * 20, b.width, 20), lines[i], 11, Palette.Dim);
                if (lines.Count > shown) Ui.Text(new Rect(b.x, b.y + 26 + shown * 20, b.width, 20), $"+{lines.Count - shown} more", 11, Palette.Dim);
                if (Ui.Button(new Rect(b.x, b.yMax - T, b.width / 2 - 5, T), "Cancel", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - T, b.width / 2 - 5, T), "Rank up all", Ui.Kind.Primary))
                {
                    int done = G.ApplyRankAll(plan);
                    Save(); CloseModal(); _rankPlan = null;
                    Toast(done == plan.Steps.Count ? $"{done} rank-ups done!" : $"{done} of {plan.Steps.Count} rank-ups done (the rest no longer fit)");
                    if (done > 0) Vfx.I?.Burst(new Vector3(0, 1.5f, 0.4f), Palette.Gold, true);
                    var t = G.HeroByUid(_rankUid); if (t != null) FillSlots(t, true);
                }
            });
        }
    }
}
