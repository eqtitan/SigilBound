// Roster tools on the Heroes screen (owner decisions 2026-11-28): the saved hero-list sort, the Release and
// Rank-up-all chips, the Undo bar, and the shared widgets of the Release Heroes and Rank Up panels
// (ReleasePanel.cs, RankUpPanel.cs). The panels draw inside the Heroes screen (no new ScreenId), so the
// bottom nav, Back and the menu layout work as they do for the hero list. Rules live in Core/Roster.cs.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    // Layout numbers for the roster panels (the 390-wide menu column; tap targets use Ui.Tap, 48 dp).
    public static class RosterLayout
    {
        public static float Tap => Ui.Tap;  // chip, button and check-row height: 48 dp on a phone, never under 44
        public const float Gap = 6;         // between chips in a row
        public const float RowGap = 10;     // between rows
        public const float LabelH = 22;     // section label
        public const float BackW = 104;     // "‹ Heroes" chip
        public const float CheckBox = 26;   // the box of a check row
        public const float ReleaseFooterH = 112;
        public const float ReviewFooterH = 64;
        public const int ReviewCols = 4;
        public const float ReviewCardH = 104, ReviewCardGap = 8;
        public const float SlotH = 116, SlotGap = 8;
        public const int SlotCols = 3;
        public static float PickRowH => Mathf.Max(58, Ui.Tap + 10);
        public static float HoldSeconds => Access.HoldSeconds; // Settings > Accessibility > Hold to confirm (1 s by default)
        public static float UndoBarH => Ui.Tap + 8;
    }

    public sealed partial class App
    {
        enum RosterPanel { None, Release, Review, RankUp, RankPick, RankAll }
        RosterPanel _rp;
        ScreenId _rpBack = ScreenId.Heroes;
        Vector2 _rpScroll;

        // The Heroes list sort, saved (roster.heroSort) instead of resetting each launch. Team prep shares it.
        string _sort
        {
            get => G.S.roster.heroSort;
            set { if (G.S.roster.heroSort == value || System.Array.IndexOf(RosterPrefs.Sorts, value) < 0) return; G.S.roster.heroSort = value; Save(); }
        }

        float RW => Ui.W - Pad * 2; // the panels' inner width

        // ---------- hooks used by Screens.cs / App.cs ----------
        // Heroes list: sort chips, Release… and Rank up all, the Undo bar. Returns the y below them.
        float HeroListTools(float y)
        {
            float T = RosterLayout.Tap; const float G6 = RosterLayout.Gap;
            float w3 = (RW - G6 * 2) / 3, w2 = (RW - G6) / 2;
            for (int i = 0; i < RosterPrefs.Sorts.Length; i++)
            {
                var so = RosterPrefs.Sorts[i];
                if (Chip(new Rect(Pad + i * (w3 + G6), y, w3, T), char.ToUpper(so[0]) + so.Substring(1), _sort == so, 13)) _sort = so;
            }
            y += T + G6;
            if (Ui.Button(new Rect(Pad, y, w2, T), "Release…", Ui.Kind.Secondary, 13)) OpenRelease();
            var ra = new Rect(Pad + w2 + G6, y, w2, T);
            if (Ui.Button(ra, "Rank up all", Ui.Kind.Secondary, 13)) OpenRankAll();
            int ready = RankReadyCount();
            if (ready > 0) Badge(new Rect(ra.xMax - 24, ra.y - 6, 22, 22), ready);
            y += T + 12;
            if (UndoLive) { DrawUndoBar(new Rect(Pad, y, RW, RosterLayout.UndoBarH)); y += RosterLayout.UndoBarH + 8; }
            return y;
        }
        // Extra height the tools take over the old single row of sort chips (44).
        float HeroToolsExtra => RosterLayout.Tap * 2 + RosterLayout.Gap + 12 - 44 + (UndoLive ? RosterLayout.UndoBarH + 8 : 0);

        // Draws an open panel instead of the hero list. False when no panel is open.
        bool DrawRosterPanel(Rect area)
        {
            switch (_rp)
            {
                case RosterPanel.Release: DrawReleasePanel(area); return true;
                case RosterPanel.Review: DrawReleaseReview(area); return true;
                case RosterPanel.RankUp: case RosterPanel.RankAll: DrawRankUpPanel(area); return true;
                case RosterPanel.RankPick: DrawFodderPicker(area); return true;
                default: return false;
            }
        }

        // Android Back / Escape inside a panel. True when it was handled.
        bool RosterBack()
        {
            if (Screen != ScreenId.Heroes || _rp == RosterPanel.None) return false;
            switch (_rp)
            {
                case RosterPanel.Review: OpenPanel(RosterPanel.Release); break;
                case RosterPanel.RankPick: OpenPanel(RosterPanel.RankUp); break;
                case RosterPanel.RankUp when _rpBack == ScreenId.Hero && G.HeroByUid(_rankUid) != null: Go(ScreenId.Hero); break;
                default: OpenPanel(RosterPanel.None); break;
            }
            return true;
        }

        void OpenPanel(RosterPanel p) { _rp = p; _rpScroll = Vector2.zero; _scroll = Vector2.zero; }
        void OpenHeroesPanel(RosterPanel p, ScreenId back)
        {
            Go(ScreenId.Heroes); // closes any modal and resets the panel
            _codex = false; _rpBack = back;
            OpenPanel(p);
        }

        // ---------- undo (in memory only, 2 minutes) ----------
        ReleaseUndo _undo; Game _undoGame; int _undoCount;
        bool UndoLive => _undo != null && _undoGame == G && !_undo.Done && _undo.SecondsLeft(G.Now()) > 0;
        void SetUndo(ReleaseUndo u) { _undo = u; _undoGame = G; _undoCount = u?.Heroes.Count ?? 0; _relVersion++; }
        void DoUndo()
        {
            if (G.UndoRelease(_undo, G.Now(), out var why)) { Save(); Toast(_undoCount == 1 ? "Undone: the hero is back" : $"Undone: {_undoCount} heroes are back"); }
            else Toast(why);
            _undo = null; _relVersion++;
        }
        void DrawUndoBar(Rect r)
        {
            Ui.Panel(r, new Color(0.08f, 0.06f, 0.11f, 0.95f), Palette.Gold, 12);
            int left = Mathf.CeilToInt((float)_undo.SecondsLeft(G.Now()));
            Ui.Text(new Rect(r.x + 12, r.y, r.width - 150, r.height), $"Released {_undoCount} · +{_undo.Essence} essence", 13, Palette.Text, FontStyle.Bold);
            if (Ui.Button(new Rect(r.xMax - 134, r.y + (r.height - RosterLayout.Tap) / 2, 126, RosterLayout.Tap), $"Undo {left / 60}:{left % 60:00}", Ui.Kind.Primary, 14)) DoUndo();
        }

        // ---------- shared widgets ----------
        static bool Chip(Rect r, string text, bool on, int size = 12, bool enabled = true) => Ui.Button(r, text, on ? Ui.Kind.ChipOn : Ui.Kind.Chip, size, enabled);

        static void Badge(Rect r, int n)
        {
            Ui.Round(r, Palette.Bad, null, (int)(r.height / 2));
            Ui.Text(r, n > 99 ? "99" : n.ToString(), 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        // A tick box with a label; the whole row (44 tall) is the tap target. Returns the new value.
        static bool CheckRow(Rect r, bool on, string label, Color? color = null, bool enabled = true, int size = 13)
        {
            float b = RosterLayout.CheckBox;
            var box = new Rect(r.x + 2, r.y + (r.height - b) / 2, b, b);
            var c = color ?? Palette.Text;
            if (!enabled) c = new Color(c.r, c.g, c.b, 0.6f);
            Ui.Round(box, on ? Palette.Hex("#3d3020") : Palette.WellFocus, on ? (color ?? Palette.Gold) : Palette.Bronze, 5);
            if (on) Ui.Text(box, "✓", 17, color ?? Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(box.xMax + 10, r.y, r.width - b - 14, r.height), label + (enabled ? "" : "  (always)"), size, c);
            if (enabled && Ui.Button(r, "", Ui.Kind.Ghost)) return !on;
            return on;
        }

        // Small gold section label (the mock-ups' RARITY / STARS ...).
        static void Section(Rect r, string text, Color? color = null) => Ui.Text(r, text.ToUpperInvariant(), 12, color ?? Palette.Hex("#d9c79f"), FontStyle.Bold, TextAnchor.MiddleLeft, false, true);

        // The hero's portrait, or a coloured disc with their initial.
        static void HeroFace(Rect r, HeroDef t)
        {
            if (Portraits.Thumb(r, t.Id, Palette.Rarity(t.Rarity))) return;
            Ui.Round(r, Palette.Element(t.Element), Palette.Rarity(t.Rarity), Mathf.Max(4, (int)(r.width / 2)));
            Ui.Text(r, t.ShortName.Substring(0, 1), Mathf.Max(12, (int)(r.height * 0.5f)), Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        // A red button that fires only after being held for a second (Epic+ or last copies involved).
        string _holdKey; float _holdStart;
        bool HoldButton(Rect r, string text, string key, bool enabled)
        {
            Ui.Button(r, text, Ui.Kind.Red, 15, enabled);
            bool down = enabled && GUI.enabled && Input.GetMouseButton(0) && r.Contains(Event.current.mousePosition) && !Scroller.WasDragging;
            string done = "done:" + key;
            if (!down) { if ((_holdKey == key || _holdKey == done) && Event.current.type == EventType.Repaint) _holdKey = null; return false; }
            if (_holdKey == done) return false;
            if (_holdKey != key) { _holdKey = key; _holdStart = Time.unscaledTime; }
            float p = (Time.unscaledTime - _holdStart) / RosterLayout.HoldSeconds;
            Ui.Round(new Rect(r.x + 4, r.yMax - 8, (r.width - 8) * Mathf.Clamp01(p), 4), Color.white, null, 2);
            if (p < 1) return false;
            _holdKey = done;
            return true;
        }

        // Heroes the auto-pick could rank up right now (team only, safe fodder, enough gold): the chip's badge.
        int _readyFrame = -1, _ready;
        int RankReadyCount()
        {
            if (_readyFrame == Time.frameCount) return _ready;
            _readyFrame = Time.frameCount;
            _ready = G.Team.Count(h => h.stars < MaxStars && h.level >= MaxLevelFor(h.stars) && G.S.gold >= RankUpGold(h.stars) && G.AutoFill(h).Count >= h.stars);
            return _ready;
        }

        static string RarityShort(Rarity r) => r == Rarity.Legendary ? "Legend." : r.ToString();
    }
}
