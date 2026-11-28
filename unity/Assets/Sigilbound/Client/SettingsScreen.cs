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
        string _feedback = ""; bool _feedbackErrors = true;
        void OpenFeedback()
        {
            Modal("Send feedback", 360, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 36), "What happened, or what would you like? Your note and some device details are copied so you can paste them to the developer.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                _feedback = Ui.Field(new Rect(b.x, b.y + 42, b.width, 120), _feedback, 2000, true);
                string errs = Feedback.ErrorCount > 0 ? $"Attach the last {Feedback.ErrorCount} error lines" : "Attach recent errors (none so far)";
                if (Ui.Button(new Rect(b.x, b.y + 170, b.width, 30), (_feedbackErrors ? "☑ " : "☐ ") + errs, Ui.Kind.Ghost, 12)) _feedbackErrors = !_feedbackErrors;
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width / 2 - 5, 44), "Cancel", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - 46, b.width / 2 - 5, 44), "Copy report"))
                {
                    var path = Feedback.Send(_feedback, _feedbackErrors);
                    CloseModal(); _feedback = "";
                    Toast(path != null ? "Report copied. Paste it in a message to the developer." : "Report copied to the clipboard.");
                }
            });
        }

        static float Tap => Mathf.Max(44, Ui.Tap);

        void SettingRow(ref float y, Rect b, string title, string sub)
        {
            Ui.Text(new Rect(b.x, y, b.width, 22), title, 15, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            if (!string.IsNullOrEmpty(sub)) Ui.Text(new Rect(b.x, y + 21, b.width, 18), sub, 12, Palette.Dim);
            y += string.IsNullOrEmpty(sub) ? 26 : 44;
        }

        void Chips(ref float y, Rect b, string[] labels, int on, System.Action<int> pick)
        {
            float x = b.x;
            for (int i = 0; i < labels.Length; i++)
            {
                // 48 dp tall chips (UX review #1); the chosen one is gilded (gold frame and label).
                float w = Mathf.Max(Tap, labels[i].Length * 8.2f + 34);
                if (x + w > b.xMax) { x = b.x; y += Tap + 6; }
                if (Ui.Button(new Rect(x, y, w, Tap), labels[i], i == on ? Ui.Kind.ChipOn : Ui.Kind.Chip, 13)) pick(i);
                x += w + 6;
            }
            y += Tap + 10;
        }

        void Slider(ref float y, Rect b, string title, double value, System.Action<double> set)
        {
            int pct = Mathf.RoundToInt((float)value * 100);
            Ui.Text(new Rect(b.x, y, b.width - 60, 22), title, 15, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(b.xMax - 60, y, 60, 22), pct > 0 ? pct + "%" : "Off", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
            // A visible gold track with its filled part and a 28-unit thumb in a 48 dp tall hit area (UX review #15,
            // art review H5: the stock track was invisible and the thumb 12 px).
            var area = new Rect(b.x, y + 24, b.width, Tap);
            var track = new Rect(area.x + 14, area.center.y - 4, area.width - 28, 8);
            Ui.Round(track, Palette.Hex("#0d0a12"), Palette.Bronze, 4);
            if (pct > 0) Ui.Round(new Rect(track.x, track.y, track.width * pct / 100f, track.height), Palette.GoldDark, Palette.Trim, 4);
            var thumbSt = Ui.Style("sliderThumb", () => new GUIStyle { normal = { background = Ui.RoundTex(Palette.Gold, Palette.TrimHi, 14) }, active = { background = Ui.RoundTex(Palette.TrimHi, Palette.Gold, 14) }, fixedWidth = 28, fixedHeight = 28 });
            var barSt = Ui.Style("sliderBar", () => new GUIStyle { fixedHeight = 0, stretchHeight = true, padding = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0) });
            float v = GUI.HorizontalSlider(area, pct, 0, 100, barSt, thumbSt);
            int nv = Mathf.RoundToInt(v / 5) * 5; // 5% steps like the web slider
            if (nv != pct) { set(nv / 100.0); Save(); }
            y += 30 + Tap;
        }

        // The rows scroll (drag) inside the modal; the version line and Done stay pinned below.
        void DrawSettings(Rect outer)
        {
            var view = new Rect(outer.x, outer.y, outer.width, outer.height - 78);
            _settingsScroll = Scroller.Begin(view, _settingsScroll, _settingsH);
            var b = new Rect(0, 0, outer.width - 1, view.height);
            float y = 0;
            var st = G.S.settings;
            // Volume sliders (0 = off) that apply instantly, as audio.js.
            Slider(ref y, b, "Music", st.musicVol, v => { st.musicVol = v; Sound.I?.ApplyVolumes(); });
            Slider(ref y, b, "Sound effects", st.sfxVol, v => { st.sfxVol = v; Sound.I?.ApplyVolumes(); });
            var cams = BattleLayout.Cameras;
            SettingRow(ref y, b, "Camera angle", "Also switchable during battle with Cam.");
            Chips(ref y, b, System.Array.ConvertAll(cams, c => c.Name), System.Array.FindIndex(cams, c => c.Key == BattleLayout.Camera(st.cam).Key), i => { st.cam = cams[i].Key; Save(); Toast("Camera: " + cams[i].Name); });

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
            Ui.Text(new Rect(b.x, y - 6, b.width, 18), "Auto-replay always runs at 30 fps.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft);
            y += 14;

            SettingRow(ref y, b, "Screen shake", "How much the camera shakes on big hits.");
            string[] shakes = { "full", "reduced", "off" };
            Chips(ref y, b, new[] { "Full", "Reduced", "Off" }, System.Array.IndexOf(shakes, Quality.SavedShake) is int si && si >= 0 ? si : 0, i =>
            {
                Quality.SavedShake = shakes[i]; Toast("Screen shake: " + new[] { "full", "reduced", "off" }[i]);
            });

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
            }
            _settingsH = y + 8;
            Scroller.End();
            if (_settingsH > view.height + 1) // a hint that there's more below
                GUI.DrawTexture(new Rect(view.x, view.yMax - 18, view.width, 18), Ui.GradientTex(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.45f), null, 2));
            Ui.Text(new Rect(outer.x, outer.yMax - 70, outer.width, 18), $"Sigilbound (Unity) v{Application.version}{(Updater.MyBuild > 0 ? $" · build {Updater.MyBuild}" : "")}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (Ui.Button(new Rect(outer.x, outer.yMax - 46, outer.width, 44), "Done")) CloseModal();
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
