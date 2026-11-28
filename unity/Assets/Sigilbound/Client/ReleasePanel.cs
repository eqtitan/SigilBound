// Release Heroes panel (owner decisions 2026-11-28; mock-ups 1 and 2 of research BULK_RELEASE_AUTO_RANKUP,
// laid out for the 390-wide menu column): filter chips, keep-best-N, protections with red danger toggles,
// presets saved in the save, a review grid where tapping a hero keeps them, and Release with a 2-minute Undo.
// Rules: Core/Roster.cs (PlanRelease, ReleaseMany, UndoRelease).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        ReleaseFilter _relF;                                  // the filter being edited (danger toggles live only here)
        readonly HashSet<int> _relKeep = new HashSet<int>();  // heroes kept by a tap on the review screen
        int _relVersion; string _relKey; ReleasePlan _relPlan;
        string _presetName = "";
        float _relH = 1500;
        static readonly int[] LevelSteps = { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 };

        void OpenRelease()
        {
            _relF = G.S.roster.Active.Clone();
            _relKeep.Clear();
            OpenHeroesPanel(RosterPanel.Release, ScreenId.Heroes);
        }

        // The plan for the current filter, worked out again only when something changed (IMGUI draws often).
        ReleasePlan RelPlan()
        {
            var key = $"{_relF.Key}|{string.Join(",", _relKeep)}|{G.S.heroes.Count}|{string.Join(",", G.S.team)}|{_relVersion}|{G.GetHashCode()}";
            if (key != _relKey || _relPlan == null) { _relKey = key; _relPlan = G.PlanRelease(_relF, _relKeep); }
            return _relPlan;
        }

        void DrawReleasePanel(Rect area)
        {
            if (_relF == null) _relF = G.S.roster.Active.Clone();
            float T = RosterLayout.Tap, G6 = RosterLayout.Gap, footH = RosterLayout.ReleaseFooterH;
            var view = new Rect(area.x, area.y, area.width, area.height - footH);
            float content = _relH; // measured on the previous frame
            _rpScroll = Scroller.Begin(view, _rpScroll, content);
            float y = 8;
            if (Ui.Button(new Rect(Pad, y, RosterLayout.BackW, T), "‹ Heroes", Ui.Kind.Secondary, 13)) OpenPanel(RosterPanel.None);
            Ui.Banner(new Rect(Pad + RosterLayout.BackW + 8, y, RW - RosterLayout.BackW - 8, T), "Release Heroes", 18);
            y += T + 6;
            Ui.Text(new Rect(Pad, y, RW, 34), "Pick a preset or set your own. Locked heroes and your team are never picked.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += 38;

            // Presets: the built-in Safe clean-up, up to 4 of the player's own, "+ Save as…".
            var prefs = G.S.roster;
            bool edited = !_relF.SameRules(prefs.Active);
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), "Preset" + (edited ? "  (edited)" : "")); y += RosterLayout.LabelH;
            var chips = new List<(string label, int idx)> { (ReleaseFilter.SafeName, -1) };
            for (int i = 0; i < prefs.presets.Count; i++) chips.Add((prefs.presets[i].name, i));
            if (prefs.presets.Count < RosterPrefs.MaxPresets) chips.Add(("+ Save as…", -2));
            float w2 = (RW - G6) / 2;
            for (int i = 0; i < chips.Count; i++)
            {
                var (label, idx) = chips[i];
                var r = new Rect(Pad + (i % 2) * (w2 + G6), y + (i / 2) * (T + G6), w2, T);
                bool on = idx == prefs.activePreset && idx != -2;
                if (on && idx >= 0)
                {
                    // The active user preset gets a delete button (drawn first, so it gets the tap).
                    var del = new Rect(r.xMax - T, r.y, T, T);
                    if (Ui.Button(del, "×", Ui.Kind.Secondary, 18)) AskDeletePreset(idx);
                    r.width -= T + 4;
                }
                if (Chip(r, label, on, 12))
                {
                    if (idx == -2) AskPresetName();
                    else { prefs.activePreset = idx; _relF = prefs.Active.Clone(); _relKeep.Clear(); Save(); }
                }
            }
            y += ((chips.Count + 1) / 2) * (T + G6) + RosterLayout.RowGap - G6;

            // Rarity, stars, element, role.
            y = MaskRow(y, "Rarity", new[] { "Common", "Rare", "Epic", "Legendary" }, ref _relF.rarityMask, false, 4);
            y = MaskRow(y, "Stars", Enumerable.Range(1, MaxStars).Select(n => n + "★").ToArray(), ref _relF.starMask, false, 6);
            y = MaskRow(y, "Element", Elements.Select(e => e.ToString()).ToArray(), ref _relF.elementMask, true, 3);
            y = MaskRow(y, "Role", new[] { "Attack", "Defense", "HP", "Support" }, ref _relF.roleMask, true, 5);

            // Level range: steppers (a slider thumb is too small to drag on a phone).
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), $"Level  ·  Lv {_relF.minLevel} – {_relF.maxLevel}"); y += RosterLayout.LabelH;
            // Both steppers on one row when they fit (40 + 3 taps each); with big phone taps "To" goes below.
            float x = Pad;
            _relF.minLevel = Stepper(ref x, y, "From", _relF.minLevel, 1, _relF.maxLevel);
            if (2 * (40 + 3 * T) + 12 <= RW) x += 12; else { x = Pad; y += T + G6; }
            _relF.maxLevel = Stepper(ref x, y, "To", _relF.maxLevel, _relF.minLevel, ReleaseFilter.MaxLevelAll);
            y += T + RosterLayout.RowGap;

            // Keep the best N copies of each hero.
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), "Keep best copies"); y += RosterLayout.LabelH;
            float kw = (RW - 3 * G6 - 130) / 4;
            for (int k = 0; k <= ReleaseFilter.MaxKeep; k++)
                if (Chip(new Rect(Pad + k * (kw + G6), y, kw, T), k.ToString(), _relF.keepBest == k, 14)) _relF.keepBest = k;
            Ui.Text(new Rect(Pad + 4 * (kw + G6) + 4, y, RW - 4 * (kw + G6) - 4, T), "per hero (highest ★, then level)", 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            y += T + 4;
            _relF.dupesOnly = CheckRow(new Rect(Pad, y, RW, T), _relF.dupesOnly, "Only heroes I own more than once"); y += T + RosterLayout.RowGap;

            // Protections. Locked and team can't be switched off.
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), "Always protect"); y += RosterLayout.LabelH;
            CheckRow(new Rect(Pad, y, RW, T), true, "Locked heroes", null, false); y += T;
            CheckRow(new Rect(Pad, y, RW, T), true, "Current team", null, false); y += T;
            y = ProtectRow(y, Protect.SavedTeams, "Heroes in saved teams", null);
            y = ProtectRow(y, Protect.Geared, "Heroes with gear on", null);
            y = ProtectRow(y, Protect.RankedUp, "Ranked-up (above natural ★)", null);
            y = ProtectRow(y, Protect.MaxLevel, "Max level at their ★", null);
            y += RosterLayout.RowGap;
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), "Danger (off by default)", Palette.Bad); y += RosterLayout.LabelH;
            // Danger toggles read the other way round: ticked = allowed = the protection is off.
            y = DangerRow(y, Protect.HighRarity, "Allow Epic / Legendary");
            y = DangerRow(y, Protect.LastCopy, "Allow last copy of a hero");
            _relH = y + 16;
            Scroller.End();
            if (content > view.height + 1) ScrollFade(view, _rpScroll.y, content);

            DrawReleaseFooter(new Rect(area.x, view.yMax, area.width, footH));
        }

        float MaskRow(float y, string label, string[] names, ref int mask, bool allChip, int perRow)
        {
            float T = RosterLayout.Tap, G6 = RosterLayout.Gap;
            Section(new Rect(Pad, y, RW, RosterLayout.LabelH), label); y += RosterLayout.LabelH;
            int n = names.Length + (allChip ? 1 : 0);
            float w = (RW - (perRow - 1) * G6) / perRow;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(Pad + (i % perRow) * (w + G6), y + (i / perRow) * (T + G6), w, T);
                if (allChip && i == 0) { if (Chip(r, "All", mask == 0, 12)) mask = 0; continue; }
                int bit = 1 << (i - (allChip ? 1 : 0));
                string text = names[i - (allChip ? 1 : 0)];
                int size = Ui.FitSize(text, w - 12, 12, 9, true);
                if (Chip(r, text, (mask & bit) != 0, size)) mask ^= bit;
            }
            return y + ((n + perRow - 1) / perRow) * (T + G6) + RosterLayout.RowGap - G6;
        }

        int Stepper(ref float x, float y, string label, int value, int lo, int hi)
        {
            float T = RosterLayout.Tap;
            Ui.Text(new Rect(x, y, 40, T), label, 12, Palette.Dim);
            x += 40;
            int i = System.Array.FindLastIndex(LevelSteps, s => s <= value);
            if (Ui.Button(new Rect(x, y, T, T), "−", Ui.Kind.Secondary, 18, value > lo)) value = Mathf.Max(lo, i > 0 && LevelSteps[i] == value ? LevelSteps[i - 1] : LevelSteps[Mathf.Max(0, i)]);
            x += T;
            Ui.Panel(new Rect(x + 2, y + 4, T - 4, T - 8), Palette.Hex("#0d0a12"), Palette.Bronze, 8);
            Ui.Text(new Rect(x, y, T, T), value.ToString(), 14, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            x += T;
            if (Ui.Button(new Rect(x, y, T, T), "+", Ui.Kind.Secondary, 18, value < hi)) value = Mathf.Min(hi, i + 1 < LevelSteps.Length ? LevelSteps[i + 1] : hi);
            x += T;
            return value;
        }

        float ProtectRow(float y, Protect bit, string label, Color? c)
        {
            bool on = (_relF.protect & (int)bit) != 0;
            bool now = CheckRow(new Rect(Pad, y, RW, RosterLayout.Tap), on, label, c);
            if (now != on) _relF.protect ^= (int)bit;
            return y + RosterLayout.Tap;
        }

        float DangerRow(float y, Protect bit, string label)
        {
            bool allowed = (_relF.protect & (int)bit) == 0;
            bool now = CheckRow(new Rect(Pad, y, RW, RosterLayout.Tap), allowed, label, Palette.Bad);
            if (now != allowed) _relF.protect ^= (int)bit;
            return y + RosterLayout.Tap;
        }

        void DrawReleaseFooter(Rect r)
        {
            float T = RosterLayout.Tap;
            var plan = RelPlan();
            Ui.Panel(new Rect(r.x + 8, r.y + 4, r.width - 16, r.height - 8), new Color(0.07f, 0.05f, 0.1f, 0.96f), Palette.Trim, 12);
            int best = plan.Protected.Count(p => p.why == "Best copy");
            Ui.Text(new Rect(Pad + 6, r.y + 8, RW - 12, 24), $"Matches: {Ui.ColorTag(plan.Release.Count + (plan.Release.Count == 1 ? " hero" : " heroes"), Palette.Gold)}   ·   You get {Ui.ColorTag(plan.Essence + " essence", Palette.Hex("#8fd8ff"))}", 14, Palette.Text, FontStyle.Bold);
            Ui.Text(new Rect(Pad + 6, r.y + 30, RW - 12, 20), $"Protected: {plan.Protected.Count - best}" + (best > 0 ? $" · {best} kept as best copy" : "") + (plan.Risky ? Ui.ColorTag("  · includes Epic+ or last copies", Palette.Bad) : ""), 12, Palette.Dim);
            float by = r.yMax - T - 10, sw = Mathf.Floor(RW * 0.4f);
            bool userActive = G.S.roster.activePreset >= 0;
            if (Ui.Button(new Rect(Pad, by, sw, T), userActive ? "Save preset" : "Save as preset", Ui.Kind.Secondary, 13))
            {
                if (userActive) { SavePresetInto(G.S.roster.activePreset, G.S.roster.presets[G.S.roster.activePreset].name); Toast($"Saved \"{G.S.roster.presets[G.S.roster.activePreset].name}\""); }
                else AskPresetName();
            }
            bool any = plan.Release.Count > 0;
            if (Ui.Button(new Rect(Pad + sw + 8, by, RW - sw - 8, T), any ? $"Review {plan.Release.Count} ›" : "Nothing matches", Ui.Kind.Primary, 15, any)) { OpenPanel(RosterPanel.Review); }
        }

        void SavePresetInto(int idx, string name)
        {
            var p = _relF.Clone(); p.name = name; p.Clamp(); // the red danger toggles are never saved off
            var prefs = G.S.roster;
            if (idx >= 0 && idx < prefs.presets.Count) prefs.presets[idx] = p; else { prefs.presets.Add(p); idx = prefs.presets.Count - 1; }
            prefs.activePreset = idx;
            Save();
        }

        void AskPresetName()
        {
            if (G.S.roster.presets.Count >= RosterPrefs.MaxPresets) { Toast($"You can keep {RosterPrefs.MaxPresets} presets. Delete one first."); return; }
            _presetName = $"My filter {G.S.roster.presets.Count + 1}";
            Modal("Save preset", 230, b =>
            {
                float T = RosterLayout.Tap;
                Ui.Text(new Rect(b.x, b.y, b.width, 22), "Name (up to 16 letters). Red danger ticks are not saved.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleLeft, true);
                _presetName = Ui.Field(new Rect(b.x, b.y + 28, b.width, T), _presetName, RosterPrefs.MaxNameLength, false);
                if (Ui.Button(new Rect(b.x, b.yMax - T, b.width / 2 - 5, T), "Cancel", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - T, b.width / 2 - 5, T), "Save", Ui.Kind.Primary, 15, _presetName.Trim().Length > 0))
                {
                    SavePresetInto(-1, _presetName); CloseModal(); Toast($"Saved \"{G.S.roster.Active.name}\"");
                }
            });
        }

        void AskDeletePreset(int idx)
        {
            var name = G.S.roster.presets[idx].name;
            Modal("Delete preset?", 190, b =>
            {
                float T = RosterLayout.Tap;
                Ui.Text(new Rect(b.x, b.y, b.width, 40), $"\"{name}\" will be removed. Safe clean-up becomes the active preset.", 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(b.x, b.yMax - T, b.width / 2 - 5, T), "Cancel", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - T, b.width / 2 - 5, T), "Delete", Ui.Kind.Red))
                {
                    var prefs = G.S.roster;
                    if (idx < prefs.presets.Count) prefs.presets.RemoveAt(idx);
                    prefs.activePreset = -1; _relF = prefs.Active.Clone(); Save(); CloseModal();
                }
            });
        }

        // ---------- review ----------
        void DrawReleaseReview(Rect area)
        {
            var plan = RelPlan();
            if (plan.Release.Count == 0 && plan.Kept.Count == 0) { OpenPanel(RosterPanel.Release); return; }
            float T = RosterLayout.Tap, footH = RosterLayout.ReviewFooterH;
            var cards = plan.Release.Concat(plan.Kept).OrderByDescending(h => (int)Hero(h.id).Rarity).ThenByDescending(h => h.stars).ThenByDescending(h => h.level).ThenBy(h => h.uid).ToList();
            int cols = RosterLayout.ReviewCols; float cg = RosterLayout.ReviewCardGap, ch = RosterLayout.ReviewCardH;
            float cw = (RW - (cols - 1) * cg) / cols;
            int rows = (cards.Count + cols - 1) / cols;
            const float summaryH = 128;
            float gridTop = 8 + T + 8 + summaryH + 10 + 26;
            float content = gridTop + rows * (ch + cg) + 16;
            var view = new Rect(area.x, area.y, area.width, area.height - footH);
            _rpScroll = Scroller.Begin(view, _rpScroll, content);
            float y = 8;
            if (Ui.Button(new Rect(Pad, y, RosterLayout.BackW, T), "‹ Filters", Ui.Kind.Secondary, 13)) OpenPanel(RosterPanel.Release);
            Ui.Banner(new Rect(Pad + RosterLayout.BackW + 8, y, RW - RosterLayout.BackW - 8, T), "Review", 18);
            y += T + 8;

            // What you get, before anything happens.
            var s = new Rect(Pad, y, RW, summaryH);
            Ui.Card(s);
            Ui.Text(new Rect(s.x + 14, s.y + 8, s.width - 28, 24), $"Releasing {plan.Release.Count} {(plan.Release.Count == 1 ? "hero" : "heroes")}", 16, Palette.Text, FontStyle.Bold);
            var parts = new List<string>();
            for (int i = 0; i < 4; i++) if (plan.ByRarity[i] > 0) parts.Add(Ui.ColorTag($"{plan.ByRarity[i]} {(Rarity)i}", Palette.Rarity((Rarity)i)));
            Ui.Text(new Rect(s.x + 14, s.y + 32, s.width - 28, 20), parts.Count > 0 ? string.Join(" · ", parts) : "Nobody (everyone is kept)", 13, Palette.Text);
            Ui.Text(new Rect(s.x + 14, s.y + 54, s.width - 28, 20), Ui.ColorTag($"Epic or Legendary: {plan.HighRarity}", plan.HighRarity > 0 ? Palette.Bad : Palette.Dim) + "   ·   " + Ui.ColorTag($"Last copies: {plan.LastCopies}", plan.LastCopies > 0 ? Palette.Bad : Palette.Dim), 13, Palette.Text);
            Ui.Text(new Rect(s.x + 14, s.y + 76, s.width - 28, 20), $"Gear back to your bag: {plan.GearReturned}", 13, Palette.Dim);
            Ui.Text(new Rect(s.x + 14, s.y + 98, s.width - 28, 24), $"You get {Ui.ColorTag(plan.Essence + " essence", Palette.Hex("#8fd8ff"))}", 15, Palette.Text, FontStyle.Bold);
            y += summaryH + 10;
            Ui.Text(new Rect(Pad, y, RW, 22), "Tap a hero to keep them. Tap again to release.", 12, Palette.Dim);
            y += 26;

            // The grid; only the rows on screen are drawn (big rosters stay cheap).
            int first = Mathf.Max(0, Mathf.FloorToInt((_rpScroll.y - gridTop) / (ch + cg)) - 1);
            int last = Mathf.Min(rows - 1, Mathf.CeilToInt((_rpScroll.y + view.height - gridTop) / (ch + cg)) + 1);
            for (int row = first; row <= last; row++)
                for (int c = 0; c < cols; c++)
                {
                    int i = row * cols + c; if (i >= cards.Count) break;
                    var h = cards[i];
                    var r = new Rect(Pad + c * (cw + cg), gridTop + row * (ch + cg), cw, ch);
                    if (ReviewCard(r, h, _relKeep.Contains(h.uid), plan))
                    {
                        if (!_relKeep.Remove(h.uid)) _relKeep.Add(h.uid);
                    }
                }
            Scroller.End();
            if (content > view.height + 1) ScrollFade(view, _rpScroll.y, content);

            // Footer: Back and Release (held for a second when Epic+ or last copies are in it).
            var f = new Rect(area.x, view.yMax, area.width, footH);
            Ui.Panel(new Rect(f.x + 8, f.y + 4, f.width - 16, f.height - 8), new Color(0.07f, 0.05f, 0.1f, 0.96f), Palette.Trim, 12);
            float by = f.y + (footH - T) / 2, bw = Mathf.Floor(RW * 0.32f);
            if (Ui.Button(new Rect(Pad, by, bw, T), "Back", Ui.Kind.Secondary, 14)) OpenPanel(RosterPanel.Release);
            var rr = new Rect(Pad + bw + 8, by, RW - bw - 8, T);
            bool any = plan.Release.Count > 0;
            bool go = plan.Risky ? HoldButton(rr, $"Hold to release {plan.Release.Count}", "release", any) : Ui.Button(rr, $"Release {plan.Release.Count}", Ui.Kind.Red, 15, any);
            if (go) DoRelease(plan);
        }

        bool ReviewCard(Rect r, HeroSave h, bool kept, ReleasePlan plan)
        {
            var t = Hero(h.id);
            // "!" on Epic+ and on every card of a hero whose copies all go (not only a single copy owned).
            bool risky = t.Rarity >= Rarity.Epic || plan.LastCopyIds.Contains(h.id);
            Ui.SmallCard(r, kept ? Palette.Hex("#16251b") : Palette.Panel2, kept ? Palette.Good : Palette.Rarity(t.Rarity), 10);
            Ui.RarityTab(r, t.Rarity, 15);
            float fs = Mathf.Min(44, r.width - 30);
            HeroFace(new Rect(r.center.x - fs / 2, r.y + 8, fs, fs), t);
            Ui.Text(new Rect(r.x + 2, r.y + 56, r.width - 4, 20), t.ShortName, 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(r.x + 2, r.y + 76, r.width - 4, 20), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv{h.level}", 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (risky && !kept) { var w = new Rect(r.x + 4, r.y + 4, 18, 18); Ui.Round(w, Palette.Bad, null, 9); Ui.Text(w, "!", 12, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter); }
            if (kept)
            {
                Ui.Round(new Rect(r.x + 6, r.y + r.height / 2 - 14, r.width - 12, 26), new Color(0.05f, 0.18f, 0.09f, 0.92f), Palette.Good, 8);
                Ui.Text(new Rect(r.x, r.y + r.height / 2 - 14, r.width, 26), "KEEP", 13, Palette.Good, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            return Ui.Button(r, "", Ui.Kind.Ghost);
        }

        void DoRelease(ReleasePlan plan)
        {
            int n = plan.Release.Count; bool risky = plan.Risky;
            var u = G.ReleaseMany(plan);
            if (u == null) { Toast("Nothing was released."); return; }
            SetUndo(u); Save();
            _relKeep.Clear();
            Toast($"+{u.Essence} essence · {u.Heroes.Count} released · Undo for 2 minutes" + (risky ? " (included Epic+ or last copies)" : ""));
            OpenPanel(RosterPanel.None);
        }
    }
}
