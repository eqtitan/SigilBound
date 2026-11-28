// "What's new" window (player-facing changelog): opens once on the home screen after an update and
// from Settings. Entries come from Assets/Resources/Changelog.txt (Core/Changelog.cs).
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        const string SeenPref = "sb.changelogSeen";
        static List<ChangelogEntry> _changelog;
        bool _whatsNewChecked;
        Vector2 _whatsNewScroll;

        static List<ChangelogEntry> ChangelogAll()
        {
            if (_changelog == null)
            {
                var ta = Resources.Load<TextAsset>("Changelog");
                _changelog = Changelog.Parse(ta != null ? ta.text : "");
            }
            return _changelog;
        }

        // Once per launch after an update, on the home screen, when the pop-up queue says it's its turn
        // (App.CheckPopups). Never on the very first launch: that marks every note seen instead.
        List<ChangelogEntry> _unseenNotes;
        bool WhatsNewPending()
        {
            if (_whatsNewChecked || NoSave) return false;
            _unseenNotes ??= Changelog.Unseen(ChangelogAll(), PlayerPrefs.GetString(SeenPref, ""));
            if (PopupQueue.WhatsNewOwed(_firstLaunch, _unseenNotes.Count)) return true;
            if (_firstLaunch) MarkChangelogSeen();
            _whatsNewChecked = true;
            return false;
        }

        void ShowWhatsNewOnce()
        {
            if (_whatsNewChecked) return;
            _whatsNewChecked = true;
            var unseen = _unseenNotes ?? Changelog.Unseen(ChangelogAll(), PlayerPrefs.GetString(SeenPref, ""));
            MarkChangelogSeen();
            if (unseen.Count > 0) OpenWhatsNew(unseen, "What's New");
        }

        void MarkChangelogSeen()
        {
            if (NoSave) return;
            var all = ChangelogAll();
            if (all.Count > 0) { PlayerPrefs.SetString(SeenPref, all[0].Key); PlayerPrefs.Save(); }
        }

        void OpenWhatsNew() => OpenWhatsNew(ChangelogAll(), "Update History");

        void OpenWhatsNew(List<ChangelogEntry> entries, string title)
        {
            _whatsNewScroll = Vector2.zero;
            Modal(title, Mathf.Min(Ui.Safe.height - 40, 620), b =>
            {
                var view = new Rect(b.x, b.y, b.width, b.height - 56);
                float contentH = Layout(entries, view.width, null);
                _whatsNewScroll = Scroller.Begin(view, _whatsNewScroll, contentH);
                Layout(entries, view.width - 1, new Vector2(0, 0));
                Scroller.End();
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "Continue")) CloseModal();
            });
        }

        // Lays out (and, with an origin, draws) the entries; returns the height used.
        static float Layout(List<ChangelogEntry> entries, float w, Vector2? origin)
        {
            bool draw = origin.HasValue;
            float y = 0;
            if (entries.Count == 0)
            {
                if (draw) Ui.Text(new Rect(0, y, w, 40), "No notes yet.", 14, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
                return 40;
            }
            foreach (var e in entries)
            {
                if (draw)
                {
                    Ui.Text(new Rect(0, y, w, 24), e.Title, 17, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                    if (!string.IsNullOrEmpty(e.Date)) Ui.Text(new Rect(0, y + 22, w, 16), e.Date, 11, Palette.Dim);
                    GUI.DrawTexture(new Rect(0, y + 42, w, 1), Ui.Solid(new Color(Palette.Trim.r, Palette.Trim.g, Palette.Trim.b, 0.5f)));
                }
                y += 50;
                foreach (var (tag, text) in e.Items)
                {
                    float textW = w - 86;
                    float h = Mathf.Max(24, LinesFor(text, textW, 13) * 18 + 6);
                    if (draw)
                    {
                        var chip = new Rect(0, y + 2, 76, 20);
                        Ui.Panel(chip, TagColor(tag) * new Color(1, 1, 1, 0.22f), TagColor(tag), 10);
                        Ui.Text(chip, TagLabel(tag), 11, TagColor(tag), FontStyle.Bold, TextAnchor.MiddleCenter);
                        Ui.Text(new Rect(86, y, textW, h), text, 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                    }
                    y += h + 6;
                }
                y += 14;
            }
            return y;
        }

        static int LinesFor(string text, float width, int size)
        {
            float perLine = Mathf.Max(10, width / (size * 0.52f)); // average glyph width of the UI font
            int lines = 0;
            foreach (var para in text.Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(para.Length / perLine));
            return lines;
        }

        static string TagLabel(string tag) => tag == "new" ? "NEW" : tag == "fix" ? "FIX" : tag == "balance" ? "BALANCE" : "CHANGE";
        static Color TagColor(string tag) => tag == "new" ? Palette.Gold : tag == "fix" ? Palette.Good : tag == "balance" ? Palette.Blue : Palette.Hex("#c08cff");
    }
}
