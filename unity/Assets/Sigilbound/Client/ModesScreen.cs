// Game modes hub (docs/sigilbound/PLAN.md step 7): big code-drawn cards for every way to fight,
// each with its live status. Landscape: one row across the full width. Portrait: two columns.
// All art is drawn in code (gradients, a rune ring, Icons.cs shapes); nothing is copied.
using System;
using System.Linq;
using Sigilbound.Core;
using UnityEngine;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        struct ModeCard
        {
            public string Title, Status, Locked;
            public Icon Icon; public Color Tint; public bool Open;
            public Action Go;
        }

        ModeCard[] ModeCards()
        {
            var M = G.EnsureModes(); var A = M.arena; var tier = Trials.TierFor(A.points);
            int total = GameData.TotalStages, cleared = G.S.cleared;
            int runs = Enum.GetValues(typeof(DungeonKey)).Cast<DungeonKey>().Sum(k => G.DungeonRunsLeft(k));
            return new[]
            {
                new ModeCard { Title = "Campaign", Icon = Icon.Map, Tint = Palette.Hex("#c08a3a"), Open = true,
                    Status = cleared >= total ? $"All {total} stages cleared" : $"Next: stage {Game.StageLabel(cleared)}\n{cleared}/{total} cleared",
                    Go = () => { _hardTab = false; _chapterOpen = -1; Go(ScreenId.Campaign); } },
                new ModeCard { Title = "Hard Campaign", Icon = Icon.Skull, Tint = Palette.Hex("#b0434f"), Open = cleared > 0,
                    Locked = Words.Unlock("1-1"), Status = $"Tougher foes, double gold\n{G.S.hardCleared}/{cleared} cleared",
                    Go = () => { _hardTab = true; _chapterOpen = -1; Go(ScreenId.Campaign); } },
                new ModeCard { Title = "Dungeons", Icon = Icon.Portal, Tint = Palette.Hex("#3f9aa8"), Open = G.DungeonsOpen,
                    Locked = Words.Unlock("1-6"), Status = $"Gold, XP and gear sets\n{runs} runs left today",
                    Go = () => Go(ScreenId.Dungeon) },
                new ModeCard { Title = "Arena", Icon = Icon.Sword, Tint = Palette.Hex("#8a5ac0"), Open = G.ArenaOpen,
                    Locked = Words.Unlock("2-6"), Status = $"{tier.Name} · {A.points} pts\n{G.S.daily.arenaTries}/{Trials.ArenaTries} fights today",
                    Go = () => Go(ScreenId.ArenaPvp) },
                new ModeCard { Title = Titan.Name, Icon = Icon.Crown, Tint = Palette.Hex("#d6a940"), Open = true,
                    Status = $"Daily boss · {ElName(TitanElement(DateTime.Now))} today\n{G.S.daily.titanTries}/3 tries",
                    Go = () => Go(ScreenId.Titan) },
            };
        }

        void DrawModes(Rect area)
        {
            var cards = ModeCards();
            if (Ui.Landscape)
            {
                // One row across the whole width (this screen lays out full width, see OnGUI); two rows on 4:3
                // tablets and narrow windows, where the cards were 113 units wide (Core/UiLayout.Modes).
                var boxes = UiLayout.Modes(Ui.ToBox(area), cards.Length, Ui.FullW);
                for (int i = 0; i < cards.Length; i++) DrawModeCard(Ui.ToRect(boxes[i]), cards[i]);
                return;
            }
            // Portrait: two columns, scrolling.
            float cw = (area.width - 3 * 12) / 2, ch = 230;
            int rows = (cards.Length + 1) / 2;
            _scroll = Scroller.Begin(area, _scroll, rows * (ch + 12) + 12);
            for (int i = 0; i < cards.Length; i++)
                DrawModeCard(new Rect(12 + (i % 2) * (cw + 12), 12 + (i / 2) * (ch + 12), cw, ch), cards[i]);
            Scroller.End();
        }

        void DrawModeCard(Rect r, ModeCard c)
        {
            var dim = c.Open ? 1f : 0.45f;
            Color deep = Color.Lerp(Palette.Hex("#120e19"), c.Tint, 0.28f * dim), mid = Color.Lerp(Palette.Hex("#1d1726"), c.Tint, 0.5f * dim);
            // Card body: tinted gradient plate with the gilded frame.
            Ui.GradientBox(r, mid, deep, c.Open ? Palette.Gold : Palette.Bronze, 14);
            // Title band.
            var band = new Rect(r.x + 6, r.y + 6, r.width - 12, 38);
            Ui.Round(band, new Color(0, 0, 0, 0.35f), null, 10);
            Ui.Text(band, c.Title, Ui.FitSize(c.Title, band.width - 12, 17, 12, true), c.Open ? Palette.Gold : Palette.Hex("#cfc5b2"), FontStyle.Bold, TextAnchor.MiddleCenter, false, true); // shrinks to fit (the Titan's name was cut off)
            // Emblem: glow, rune ring and the mode's icon.
            float s = Mathf.Max(0, Mathf.Min(r.width * 0.62f, (r.height - 150) * 0.9f));
            var em = new Rect(r.center.x - s / 2, band.yMax + Mathf.Max(8, (r.height - 150 - s) / 2), s, s);
            Ui.Round(new Rect(em.x - s * 0.15f, em.y - s * 0.15f, s * 1.3f, s * 1.3f), new Color(c.Tint.r, c.Tint.g, c.Tint.b, 0.16f * dim), null, Mathf.RoundToInt(s * 0.6f));
            Icons.Draw(em, Icon.Portal, new Color(c.Tint.r, c.Tint.g, c.Tint.b, 0.35f * dim));
            var ic = new Rect(em.center.x - s * 0.3f, em.center.y - s * 0.3f, s * 0.6f, s * 0.6f);
            // The owner's painted emblem (map, skull, portal, sword, crown) when the skin is on.
            string pn = c.Icon == Icon.Map ? "map" : c.Icon == Icon.Skull ? "skull" : c.Icon == Icon.Portal ? "portal" : c.Icon == Icon.Sword ? "sword" : c.Icon == Icon.Crown ? "crown" : null;
            var big = new Rect(em.center.x - s * 0.42f, em.center.y - s * 0.42f, s * 0.84f, s * 0.84f);
            if (pn == null || !Icons.Painted(big, pn, c.Open ? 1f : 0.45f)) Icons.Draw(ic, c.Icon, c.Open ? Color.Lerp(c.Tint, Color.white, 0.35f) : Palette.Dim);
            if (!c.Open) Icons.Draw(new Rect(em.xMax - s * 0.28f, em.yMax - s * 0.3f, s * 0.26f, s * 0.26f), Icon.Lock, Palette.Dim);
            // Status footer.
            var foot = new Rect(r.x + 8, r.yMax - 74, r.width - 16, 66);
            Ui.Round(foot, new Color(0, 0, 0, c.Open ? 0.4f : 0.62f), null, 10);
            Ui.Text(new Rect(foot.x + 8, foot.y + 4, foot.width - 16, foot.height - 8), c.Open ? c.Status : c.Locked, 13, c.Open ? Palette.Text : Ui.DisabledText, FontStyle.Normal, TextAnchor.MiddleCenter, true); // the unlock hint stays readable
            // Runs, fights and tries left are plain text in the status (one badge style, and it is for things to
            // collect: UI/UX review #13).
            if (Ui.Button(r, "", Ui.Kind.Ghost)) { if (c.Open) c.Go(); else Toast(c.Locked); } // a locked card says why
        }
    }
}
