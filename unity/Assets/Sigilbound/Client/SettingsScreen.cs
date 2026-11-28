// Settings (the web game's Settings dialog): camera angle, frame rate, graphics quality, promo
// code, and save codes — copy this device's code, or paste one from the web game to carry
// progress over.
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        string _promoInput = "", _loadInput = "";
        Vector2 _settingsScroll; float _settingsH = 900;

        public void OpenSettings()
        {
            _settingsScroll = Vector2.zero;
            Modal("Settings", Mathf.Min(Ui.Safe.height - 40, 740), DrawSettings);
        }

        // Send feedback: a note plus the technical report, copied to the clipboard and saved (Feedback.cs).
        string _feedback = ""; bool _feedbackErrors = true, _feedbackCheck = true;
        void OpenFeedback()
        {
            bool hasCheck = !string.IsNullOrEmpty(LastPhoneCheck);
            Modal("Send feedback", hasCheck ? 400 : 360, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 36), "What happened, or what would you like? Your note and some device details are copied so you can paste them to the developer.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                _feedback = Ui.Field(new Rect(b.x, b.y + 42, b.width, 120), _feedback, 2000, true);
                string errs = Feedback.ErrorCount > 0 ? $"Attach the last {Feedback.ErrorCount} error lines" : "Attach recent errors (none so far)";
                if (Ui.Button(new Rect(b.x, b.y + 170, b.width, 30), (_feedbackErrors ? "☑ " : "☐ ") + errs, Ui.Kind.Ghost, 12)) _feedbackErrors = !_feedbackErrors;
                if (hasCheck && Ui.Button(new Rect(b.x, b.y + 206, b.width, 30), (_feedbackCheck ? "☑ " : "☐ ") + "Attach the last phone check", Ui.Kind.Ghost, 12)) _feedbackCheck = !_feedbackCheck;
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width / 2 - 5, 44), "Cancel", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - 46, b.width / 2 - 5, 44), "Copy report"))
                {
                    var path = Feedback.Send(_feedback, _feedbackErrors, hasCheck && _feedbackCheck ? LastPhoneCheck : null);
                    CloseModal(); _feedback = "";
                    Toast(path != null ? "Report copied. Paste it in a message to the developer." : "Report copied to the clipboard.");
                }
            });
        }

        static float Tap => Mathf.Max(44, Ui.Tap);

        // Rows grow with the text size (Settings scrolls): the title's line scales and the note wraps.
        static readonly GUIContent _subGc = new GUIContent();
        void SettingRow(ref float y, Rect b, string title, string sub)
        {
            float g = Ui.TextGrow, th = 22 * g;
            Ui.Text(new Rect(b.x, y, b.width, th), title, 15, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            if (string.IsNullOrEmpty(sub)) { y += th + 4; return; }
            _subGc.text = sub;
            float sh = Mathf.Max(18 * g, Ui.Label(12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true).CalcHeight(_subGc, b.width));
            Ui.Text(new Rect(b.x, y + th - 1, b.width, sh), sub, 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += th + sh + 4;
        }

        // A chip's width: its label at the player's text size plus padding (never narrower than a tap target).
        static float ChipW(string label, Rect b)
        {
            _subGc.text = label;
            float w = Ui.Label(13, null, FontStyle.Bold, TextAnchor.MiddleCenter, false, Ui.SerifCanDraw(label)).CalcSize(_subGc).x + 2 * Ui.ButtonInset(Tap, Ui.Kind.Chip) + 4;
            return Mathf.Min(b.width, Mathf.Max(Tap, w));
        }

        void Chips(ref float y, Rect b, string[] labels, int on, System.Action<int> pick)
        {
            float x = b.x;
            for (int i = 0; i < labels.Length; i++)
            {
                // 48 dp tall chips (UX review #1); the chosen one is gilded (gold frame and label).
                float w = ChipW(labels[i], b);
                if (x + w > b.xMax) { x = b.x; y += Tap + 6; }
                if (Ui.Button(new Rect(x, y, w, Tap), labels[i], i == on ? Ui.Kind.ChipOn : Ui.Kind.Chip, 13)) pick(i);
                x += w + 6;
            }
            y += Tap + 10;
        }

        void Slider(ref float y, Rect b, string title, double value, System.Action<double> set)
        {
            int pct = Mathf.RoundToInt((float)value * 100);
            float th = 22 * Ui.TextGrow;
            Ui.Text(new Rect(b.x, y, b.width - 60, th), title, 15, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(b.xMax - 60, y, 60, th), pct > 0 ? pct + "%" : "Off", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
            // A visible gold track with its filled part and a 28-unit thumb in a 48 dp tall hit area (UX review #15,
            // art review H5: the stock track was invisible and the thumb 12 px).
            var area = new Rect(b.x, y + th + 2, b.width, Tap);
            var track = new Rect(area.x + 14, area.center.y - 4, area.width - 28, 8);
            Ui.Round(track, Palette.Hex("#0d0a12"), Palette.Bronze, 4);
            if (pct > 0) Ui.Round(new Rect(track.x, track.y, track.width * pct / 100f, track.height), Palette.GoldDark, Palette.Trim, 4);
            var thumbSt = Ui.Style("sliderThumb", () => new GUIStyle { normal = { background = Ui.RoundTex(Palette.Gold, Palette.TrimHi, 14) }, active = { background = Ui.RoundTex(Palette.TrimHi, Palette.Gold, 14) }, fixedWidth = 28, fixedHeight = 28 });
            var barSt = Ui.Style("sliderBar", () => new GUIStyle { fixedHeight = 0, stretchHeight = true, padding = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0) });
            float v = GUI.HorizontalSlider(area, pct, 0, 100, barSt, thumbSt);
            int nv = Mathf.RoundToInt(v / 5) * 5; // 5% steps like the web slider
            if (nv != pct) { set(nv / 100.0); Save(); }
            y += th + 8 + Tap;
        }

        // A group heading in Settings: gold serif title with a gold rule under it.
        void SettingGroup(ref float y, Rect b, string title)
        {
            float h = 28 * Ui.TextGrow;
            Ui.Heading(new Rect(b.x, y, b.width, h), title, 18);
            Ui.GoldRule(new Rect(b.x, y + h, b.width, 2));
            y += h + 10;
        }

        // Settings > Accessibility (PLAN step 115; UX review #12 and #16, art review L2). All on this device.
        void DrawAccessibility(ref float y, Rect b)
        {
            SettingGroup(ref y, b, "Accessibility");

            SettingRow(ref y, b, "Text size", "Bigger text everywhere. Text that can't grow in place shrinks to fit, never below Default.");
            Chips(ref y, b, Accessibility.TextSizeNames, Access.TextLevel, i => { Access.TextLevel = i; Toast("Text size: " + Accessibility.TextSizeNames[i]); });

            SettingRow(ref y, b, "Reduce motion", Access.ReduceMotion ? "No screen shake; Titan and lair cinematics hold still shots with gentle cuts." : "Cinematic camera moves, zooms and screen shake as designed.");
            Chips(ref y, b, new[] { "Off", "On" }, Access.ReduceMotion ? 1 : 0, i => { Access.ReduceMotion = i == 1; Toast(i == 1 ? "Reduce motion: on" : "Reduce motion: off"); });

            // Screen shake lives here now (UX review #16: it was 7th in the list); Reduce motion turns it off.
            string[] shakes = { "full", "reduced", "off" };
            SettingRow(ref y, b, "Screen shake", Access.ReduceMotion ? "Off while Reduce motion is on." : "How much the camera shakes on big hits.");
            if (Access.ReduceMotion)
            {
                float x = b.x;
                foreach (var l in new[] { "Full", "Reduced", "Off" }) { float w = ChipW(l, b); Ui.Button(new Rect(x, y, w, Tap), l, l == "Off" ? Ui.Kind.ChipOn : Ui.Kind.Chip, 13, false); x += w + 6; }
                y += Tap + 10;
            }
            else Chips(ref y, b, new[] { "Full", "Reduced", "Off" }, System.Array.IndexOf(shakes, Quality.SavedShake) is int si && si >= 0 ? si : 0, i =>
            {
                Quality.SavedShake = shakes[i]; Toast("Screen shake: " + new[] { "full", "reduced", "off" }[i]);
            });

            SettingRow(ref y, b, "Flash effects", Access.Flash == 0 ? "Crit and ultimate flashes, lightning and bright bursts as designed." : Access.Flash == 1 ? "Dimmer, shorter flashes; lightning doesn't flicker." : "No full-screen flashes or sky lightning; bursts kept dim.");
            Chips(ref y, b, Accessibility.FlashNames, Access.Flash, i => { Access.Flash = i; Toast("Flash effects: " + Accessibility.FlashNames[i].ToLowerInvariant()); });

            SettingRow(ref y, b, "Colour-blind palette", "Element and rarity colours picked for each type. Icons and letters stay.");
            Chips(ref y, b, Accessibility.PaletteShort, Access.ColourMode, i => { Access.ColourMode = i; Toast("Palette: " + Accessibility.PaletteNames[i]); });
            PalettePreview(ref y, b);

            SettingRow(ref y, b, "Hold to confirm", $"How long to hold Release and risky Rank Ups: {Access.HoldSeconds:0.#} s.");
            Chips(ref y, b, Accessibility.HoldNames, Access.HoldLevel, i => { Access.HoldLevel = i; Toast($"Hold to confirm: {Access.HoldSeconds:0.#} s"); });

            SettingRow(ref y, b, "Larger tap targets", Access.LargeTaps ? "Buttons are at least 56 dp." : "Buttons are at least 48 dp.");
            Chips(ref y, b, new[] { "Off", "On" }, Access.LargeTaps ? 1 : 0, i => { Access.LargeTaps = i == 1; Toast(i == 1 ? "Larger tap targets: on" : "Larger tap targets: off"); });

            y += 4;
            SettingGroup(ref y, b, "Game");
        }

        // Preview chips for the chosen palette: the five element badges (icon + name) and the four rarity tabs.
        static readonly Element[] _els = { Element.Flame, Element.Gale, Element.Tide, Element.Light, Element.Shadow };
        static readonly Rarity[] _rars = { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
        void PalettePreview(ref float y, Rect b)
        {
            float g = Ui.TextGrow, cw = (b.width - 4 * 6) / 5, ch = 30 + 16 * g;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(b.x + i * (cw + 6), y, cw, ch);
                Ui.Round(r, Palette.Hex("#0d0a12"), Palette.Element(_els[i]), 10);
                Ui.ElementBadge(new Rect(r.center.x - 12, r.y + 4, 24, 24), _els[i]);
                Ui.Text(new Rect(r.x + 2, r.y + 28, r.width - 4, ch - 30), _els[i].ToString(), 11, Palette.Element(_els[i]), FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            y += ch + 6;
            float rw = (b.width - 3 * 6) / 4, rh = 22 * g + 8;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(b.x + i * (rw + 6), y, rw, rh);
                Ui.Round(r, Color.Lerp(Palette.Rarity(_rars[i]), Color.black, 0.7f), Palette.Rarity(_rars[i]), 8);
                Ui.Text(new Rect(r.x + 6, r.y, r.width - 12, r.height), Ui.RarityLetter(_rars[i]) + "  " + _rars[i], 12, Palette.Rarity(_rars[i]), FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            y += rh + 12;
        }

        // The rows scroll (drag) inside the modal; the version line and Done stay pinned below.
        void DrawSettings(Rect outer)
        {
            var view = new Rect(outer.x, outer.y, outer.width, outer.height - 78);
            _settingsScroll = Scroller.Begin(view, _settingsScroll, _settingsH);
            var b = new Rect(0, 0, outer.width - 1, view.height);
            float y = 0;
            var st = G.S.settings;
            DrawAccessibility(ref y, b); // first (UX review #16)
            // Volume sliders (0 = off) that apply instantly, as audio.js.
            Slider(ref y, b, "Music", st.musicVol, v => { st.musicVol = v; Sound.I?.ApplyVolumes(); });
            Slider(ref y, b, "Sound effects", st.sfxVol, v => { st.sfxVol = v; Sound.I?.ApplyVolumes(); });
            var cams = BattleLayout.Cameras;
            SettingRow(ref y, b, "Battle view", "Also switchable during battle with the View button.");
            Chips(ref y, b, System.Array.ConvertAll(cams, c => c.Name), System.Array.FindIndex(cams, c => c.Key == BattleLayout.Camera(st.cam).Key), i => { st.cam = cams[i].Key; Save(); Toast("View: " + cams[i].Name); });

            if (Updater.Included && (Application.platform == RuntimePlatform.Android || Application.isEditor))
            {
                // In-app updates (PLAN step 34): Auto downloads new builds on Wi-Fi and installs them on the home screen.
                SettingRow(ref y, b, "Updates", Updater.AutoUpdate ? "New builds download on Wi-Fi and install on the home screen." : "An Update now button appears when a new build is out.");
                Chips(ref y, b, new[] { "Auto (Wi-Fi)", "When I tap" }, Updater.AutoUpdate ? 0 : 1, i =>
                {
                    Updater.AutoUpdate = i == 0;
                    Toast(i == 0 ? "Updates: automatic on Wi-Fi" : "Updates: when you tap Update now");
                });
            }

            SettingRow(ref y, b, "What's new", "Notes for every update: new features, fixes and changes.");
            if (Ui.Button(new Rect(b.x, y, 170, Tap), "View notes", Ui.Kind.Secondary, 13)) OpenWhatsNew();
            y += Tap + 10;

            // Frame rate (owner decision 2026-11-27): 60 by default; the screen's 90/120 Hz as a choice.
            int hz = Quality.HighHz; string fpsMode = Quality.SavedFpsMode(st.fpsCap);
            if (fpsMode == "high" && !Quality.HighAvailable) fpsMode = "60";
            SettingRow(ref y, b, "Frame rate", fpsMode == "high" ? $"{hz} fps: smoothest, uses more battery and runs warmer." : fpsMode == "60" ? "60 fps: smooth (recommended)." : "30 fps keeps the phone cooler and saves battery.");
            string[] modes = Quality.HighAvailable ? new[] { "60", "high", "30" } : new[] { "60", "30" };
            string[] labels = Quality.HighAvailable ? new[] { "60 · Smooth", $"{hz} · Smoothest", "30 · Battery" } : new[] { "60 · Smooth", "30 · Battery" };
            Chips(ref y, b, labels, System.Array.IndexOf(modes, fpsMode) is int mi && mi >= 0 ? mi : 0, i =>
            {
                Quality.SaveFpsMode(modes[i]); st.fpsCap = modes[i] == "30" ? 30 : 60; Save(); Quality.I?.Set(st.quality, modes[i]);
                Toast(modes[i] == "high" ? $"{hz} fps: uses more battery" : modes[i] == "60" ? "60 fps" : "30 fps: cooler and longer battery");
            });
            Ui.Text(new Rect(b.x, y - 6, b.width, 18 * Ui.TextGrow), "Auto-replay always runs at 30 fps.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft);
            y += 14 * Ui.TextGrow;

            if (Background.Supported)
            {
                // Desktop only: Windows used to freeze battles whenever the window lost focus.
                SettingRow(ref y, b, "When the window isn't focused", Background.KeepRunning ? "The game keeps running, so battles and auto-replay carry on." : "The game pauses until you click back into it.");
                Chips(ref y, b, new[] { "Keep running", "Pause" }, Background.KeepRunning ? 0 : 1, i =>
                {
                    Background.KeepRunning = i == 0; Toast(i == 0 ? "Keeps running in the background" : "Pauses when not focused");
                });
                if (Background.KeepRunning)
                {
                    SettingRow(ref y, b, "Sound in the background", Background.MuteWhenUnfocused ? "Muted while you're in another window." : "Keeps playing while you're in another window.");
                    Chips(ref y, b, new[] { "Mute", "Keep playing" }, Background.MuteWhenUnfocused ? 0 : 1, i =>
                    {
                        Background.MuteWhenUnfocused = i == 0; Toast(i == 0 ? "Background sound: muted" : "Background sound: on");
                    });
                }
            }

            if (Skin.Any)
            {
                SettingRow(ref y, b, "Painted interface", "The painted frames and buttons, or the classic drawn look.");
                Chips(ref y, b, new[] { "Painted", "Classic" }, Skin.On ? 0 : 1, i => { Skin.On = i == 0; Toast(i == 0 ? "Painted interface" : "Classic interface"); });
            }

            var q = Quality.I;
            string[] qKeys = { "auto", "Ultra", "High", "Medium", "Low" };
            string qSub = st.quality == "auto" ? $"Auto picks what your phone runs smoothly{(q != null ? $" (now: {q.TierName})" : "")}"
                : st.quality == "Ultra" ? "Maximum detail at full resolution. Uses more battery." : "Lower it if battles stutter or the phone gets warm.";
            SettingRow(ref y, b, "Graphics quality", qSub);
            Chips(ref y, b, new[] { "Auto", "Ultra", "High", "Medium", "Low" }, System.Array.IndexOf(qKeys, st.quality), i =>
            {
                st.quality = qKeys[i]; Save(); Quality.I?.Set(st.quality, Quality.SavedFpsMode(st.fpsCap));
                Toast("Graphics: " + (i == 0 ? "Auto" : qKeys[i]));
            });

            SettingRow(ref y, b, "Feedback", "Found a bug or have an idea? Send it to the developer.");
            if (Ui.Button(new Rect(b.x, y, 170, Tap), "Send feedback", Ui.Kind.Primary, 13)) OpenFeedback();
            if (Ui.Button(new Rect(b.x + 180, y, 170, Tap), "Replay tutorial", Ui.Kind.Secondary, 13)) { Tutorial.Step = 1; CloseModal(); Go(ScreenId.Home); Toast("Tutorial restarted"); }
            y += Tap + 10;

            if (Screen != ScreenId.Battle)
            {
                // PLAN step 116: a fixed 60-second sequence measured on this phone; the report is copied (PhoneCheckRun.cs).
                SettingRow(ref y, b, "Phone check", "60 seconds, hands off: plays a set route (team screen, a big battle, the Titan) and measures smoothness, memory and battery. Copies a report for the developer.");
                if (Ui.Button(new Rect(b.x, y, 200, Tap), "Run phone check", Ui.Kind.Secondary, 13)) StartPhoneCheck();
                y += Tap + 10;
            }

            SettingRow(ref y, b, "Performance overlay", "Shows frame rate and draw calls. For testing; saved on this device.");
            Chips(ref y, b, new[] { "Off", "On" }, PerfOverlay.Enabled ? 1 : 0, i => PerfOverlay.Enabled = i == 1);

            if (Screen != ScreenId.Battle)
            {
                SettingRow(ref y, b, "Promo code", "Got a code? Enter it here for free rewards.");
                _promoInput = Ui.Field(new Rect(b.x, y, b.width - 108, Tap), _promoInput, 32, false);
                if (Ui.Button(new Rect(b.xMax - 100, y, 100, Tap), "Redeem", Ui.Kind.Primary, 13))
                {
                    switch (Promo.Redeem(G, _promoInput, out var entry))
                    {
                        case PromoResult.Empty: Toast("Enter a code first."); break;
                        case PromoResult.Invalid: Toast("That code isn't valid."); break;
                        case PromoResult.AlreadyUsed: Toast("You've already used this code."); break;
                        default: Save(); _promoInput = ""; Sound.I?.Play("legendary", 1, 1, 0); Toast($"{entry.Label}: {entry.Reward}"); break;
                    }
                }
                y += Tap + 10;

                SettingRow(ref y, b, "Save code", "Paste a code from the web game to load that progress here. It replaces this device's progress.");
                _loadInput = Ui.Field(new Rect(b.x, y, b.width, 64), _loadInput, 200000, true);
                y += 70;
                float bw = (b.width - 12) / 3;
                if (Ui.Button(new Rect(b.x, y, bw, Tap), "Paste", Ui.Kind.Secondary, 13)) _loadInput = GUIUtility.systemCopyBuffer ?? "";
                if (Ui.Button(new Rect(b.x + bw + 6, y, bw, Tap), "Load code", Ui.Kind.Primary, 13)) ConfirmLoad();
                if (Ui.Button(new Rect(b.x + (bw + 6) * 2, y, bw, Tap), "Copy mine", Ui.Kind.Secondary, 13))
                { GUIUtility.systemCopyBuffer = ExportSaveCode(); Toast("Save code copied"); }
                y += Tap + 10;

                // Danger zone (UX review #17: "Reset save" sat on Home beside "How to play").
                y += 6;
                GUI.DrawTexture(new Rect(b.x, y, b.width, 1), Ui.Solid(new Color(Palette.Bad.r, Palette.Bad.g, Palette.Bad.b, 0.5f)));
                y += 10;
                SettingRow(ref y, b, "Danger zone", "Start over from the beginning on this device.");
                if (Ui.Button(new Rect(b.x, y, 170, Tap), "Reset save…", Ui.Kind.Red, 13)) AskResetSave();
                y += Tap + 10;
            }
            _settingsH = y + 8;
            Scroller.End();
            if (_settingsH > view.height + 1) // a hint that there's more below
                GUI.DrawTexture(new Rect(view.x, view.yMax - 18, view.width, 18), Ui.GradientTex(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.45f), null, 2));
            Ui.Text(new Rect(outer.x, outer.yMax - 70, outer.width, 18), $"Sigilbound (Unity) v{Application.version}{(Updater.MyBuild > 0 ? $" · build {Updater.MyBuild}" : "")}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (Ui.Button(new Rect(outer.x, outer.yMax - 46, outer.width, 44), "Done")) CloseModal();
        }

        // Two steps: Settings > Reset save…, then this confirm. The save goes through SaveStore as usual, so the
        // progress being replaced stays in its rolling backup (sigilbound.save.v1.bak) until the next save.
        void AskResetSave()
        {
            Modal("Reset all progress?", 270, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 100), "This starts a new game on this device: heroes, gear and campaign progress are gone.\n\nCopy your save code first if you might want this game back.", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(b.x, b.yMax - 96, b.width, 44), "Copy my save code", Ui.Kind.Secondary, 13)) { GUIUtility.systemCopyBuffer = ExportSaveCode(); Toast("Save code copied"); }
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width / 2 - 5, 44), "Cancel", Ui.Kind.Secondary)) OpenSettings();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - 46, b.width / 2 - 5, 44), "Reset", Ui.Kind.Red))
                {
                    Store?.AcceptFresh(); G = new Game(new SaveData()); G.EnsureDaily(); Save(); ClearLineup(); CloseModal();
                    _undo = null; _rp = RosterPanel.None;
                    _firstLaunch = true; MarkChangelogSeen(); // a new player again: no What's New
                    Go(ScreenId.Intro);
                }
            });
        }

        void ConfirmLoad()
        {
            var code = _loadInput.Trim();
            if (code.Length == 0) { Toast("Paste a save code first."); return; }
            var preview = SaveCodec.FromCode(code);
            if (preview == null) { Toast("That code didn't work. Check that you copied all of it."); return; }
            Modal("Load this save?", 250, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 110),
                    $"{preview.heroes.Count} heroes · campaign {preview.cleared}/{GameData.TotalStages} · {Ui.Fmt(preview.gold)} gold" +
                    (string.IsNullOrEmpty(preview.playerName) ? "" : $"\nPlayer: {preview.playerName}") +
                    "\n\nThis replaces the progress on this device.", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width / 2 - 5, 44), "Cancel", Ui.Kind.Secondary)) OpenSettings();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - 46, b.width / 2 - 5, 44), "Load"))
                {
                    if (ImportSaveCode(code)) { _loadInput = ""; CloseModal(); Toast("Save loaded"); }
                    else Toast("That code didn't work. Check that you copied all of it.");
                }
            });
        }
    }
}
