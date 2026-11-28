// First-time tutorial (release prep): a short guided first session for NEW saves only. It points at the
// Summon tab, then Heroes, then the Campaign, explains the battle screen (turn order, skills, Auto,
// speed) and the rewards. Skippable at any time and never shown again. Its state lives in PlayerPrefs
// (sb.tutorial), not the save, so saves stay identical to the web version's.
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class Tutorial
    {
        // 0 = not running (finished, skipped or an existing player). 1.. = the current step.
        public static int Step
        {
            get => PlayerPrefs.GetInt("sb.tutorial", 0);
            set { PlayerPrefs.SetInt("sb.tutorial", value); PlayerPrefs.Save(); }
        }
        public static bool Active => Step > 0;
        // Its coach panel is (or is about to be) on screen: other pop-ups wait (Core/PopupQueue). Step 9 is the
        // free play in the first battle, with no panel.
        public static bool Blocking => Active && Step != 9;
        public static void Start() { if (!PlayerPrefs.HasKey("sb.tutorialDone")) Step = 1; }
        public static void Finish() { Step = 0; PlayerPrefs.SetInt("sb.tutorialDone", 1); PlayerPrefs.Save(); }

        static int _heroCount = -1; static bool _sawBattle;

        // Menus: called from App.OnGUI after the screen and nav are drawn (not under a modal).
        public static void Draw(App app, float navTop)
        {
            if (!Active || app.ModalOpen) return;
            var G = app.G; int heroes = G.S.heroes.Count;
            Rect Nav(int i) => App.NavCell(i, navTop); // the same cells the nav draws
            switch (Step)
            {
                case 1:
                    if (app.Screen == ScreenId.Summon) { Step = 2; _heroCount = heroes; break; }
                    Coach(app, "Summon heroes", "Your first team is ready. Shards call new heroes: tap Summon below.", Nav(3), false); break;
                case 2:
                    if (_heroCount < 0) _heroCount = heroes;
                    if (heroes > _heroCount || app.Screen != ScreenId.Summon) { Step = 3; break; }
                    Coach(app, "Call a hero", "Pick a banner and summon with your shards. Rarer shards call rarer heroes.", Rect.zero, true); break;
                case 3:
                    if (app.Screen == ScreenId.Heroes || app.Screen == ScreenId.Hero) { Step = 4; break; }
                    Coach(app, "Your heroes", "Tap Heroes to see everyone you have.", Nav(2), false); break;
                case 4:
                    if (app.Screen != ScreenId.Heroes && app.Screen != ScreenId.Hero) { Step = 5; break; }
                    Coach(app, "Team and levels", "Open a hero to see its skills, level it up and equip gear. Up to four heroes fight as your team.", Rect.zero, true); break;
                case 5:
                    if (app.Screen == ScreenId.Campaign || app.Screen == ScreenId.Prep || app.Screen == ScreenId.Battle) { Step = 6; break; }
                    // The same route as the welcome tip: Continue Campaign on Home (or Battle, then Campaign elsewhere).
                    if (app.Screen == ScreenId.Home && App.HomeCta.width > 0) Coach(app, "To battle", "Press Continue Campaign to start the first stage.", App.HomeCta, false);
                    else Coach(app, "To battle", "Tap Battle, then the Campaign, and start the first stage.", Nav(1), false);
                    break;
                case 6: case 7: case 8: case 9:
                    if (app.Screen == ScreenId.Battle) _sawBattle = true;
                    else if (_sawBattle) { Step = 10; break; }
                    if (app.Screen != ScreenId.Battle && Step == 6) Coach(app, "Start a stage", "Pick the first stage, check your team and press Battle.", Rect.zero, false);
                    break;
                case 10:
                    Coach(app, "Rewards", "Wins give gold, shards and hero XP. Claim daily quests and logins on Home, and try the Trials when you're stronger. Have fun!", Nav(0), true, "Done"); break;
                default: Finish(); break;
            }
        }

        // Battle: called at the end of the battle HUD. Three short pointers, then it lets you play.
        public static void DrawBattle(App app)
        {
            if (!Active || app.ModalOpen || Step < 6 || Step > 9) return;
            _sawBattle = true;
            var safe = Ui.Safe;
            switch (Step)
            {
                // The rings use the rects the HUD drew this frame (BattleController.Hud*), so they land on the controls.
                case 6: Coach(app, "Turn order", "The bar at the top shows who acts next. Faster heroes act more often.", BattleController.HudTurns.width > 0 ? BattleController.HudTurns : new Rect(Ui.W * 0.22f, safe.y + 46, Ui.W * 0.56f, 34), true); break;
                case 7: Coach(app, "Skills", "On your hero's turn, pick a skill at the bottom, then tap a target. Strong skills need a few turns to recharge.", BattleController.HudSkills.width > 0 ? BattleController.HudSkills : new Rect(Ui.W * 0.2f, safe.yMax - 110, Ui.W * 0.6f, 100), true); break;
                case 8: Coach(app, "Auto and speed", "Auto plays your turns for you; the speed button runs the fight at 2x or 3x.", BattleController.HudAutoSpeed.width > 0 ? BattleController.HudAutoSpeed : new Rect(Ui.W - 150, safe.y + 6, 100, 44), true); break;
                case 9: break; // playing: the tutorial resumes on the results screen
            }
        }

        // The coach panel (text, Next/Done and Skip) and, when target isn't empty, a pulsing ring and arrow on it.
        static void Coach(App app, string title, string text, Rect target, bool next, string nextLabel = "Next")
        {
            if (target.width > 0)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                var ring = new Rect(target.x - 4, target.y - 4, target.width + 8, target.height + 8);
                var c = new Color(1f, 0.82f, 0.35f, 0.35f + 0.45f * pulse);
                Ui.Round(ring, new Color(0, 0, 0, 0), c, 12);
                Ui.Round(new Rect(ring.x - 2, ring.y - 2, ring.width + 4, ring.height + 4), new Color(0, 0, 0, 0), new Color(c.r, c.g, c.b, c.a * 0.5f), 14);
                bool below = target.center.y < Ui.H * 0.5f;
                float ay = below ? ring.yMax + 6 + pulse * 6 : ring.y - 30 - pulse * 6;
                // A drawn arrow (no glyph: fonts on the owner's devices lacked them), turned to point at the target.
                var keep = GUI.matrix;
                var ar = new Rect(target.center.x - 11, ay + 1, 22, 22);
                GUIUtility.RotateAroundPivot(below ? -90 : 90, ar.center);
                Icons.Draw(ar, Icon.Next, Palette.Gold);
                GUI.matrix = keep;
            }
            // Auto height from the text (long steps wrapped into the buttons: UI/UX review #26); 48 dp buttons.
            float w = Mathf.Min(Ui.W - 24, 520), tap = Mathf.Max(44, Ui.Tap);
            float textH = Mathf.Max(40, Ui.Label(13, null, FontStyle.Normal, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(text), w - 28));
            float h = 30 + textH + 10 + tap + 10;
            bool low = target.width <= 0 || target.center.y < Ui.H * 0.5f;
            var r = new Rect((Ui.W - w) / 2, low ? Ui.Safe.yMax - 64 - h - 70 : Ui.Safe.y + 60, w, h);
            Ui.Card(r, true);
            Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 28, 22), title, 15, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 14, r.y + 30, r.width - 28, textH + 4), text, 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
            var was = GUI.enabled; GUI.enabled = true;
            // 48 dp buttons; "Skip" a Secondary, Next/Done the Primary on the right (rule 3).
            if (Ui.Button(new Rect(r.x + 14, r.yMax - tap - 8, 132, tap), "Skip tutorial", Ui.Kind.Secondary, 12)) Finish();
            if (next && Ui.Button(new Rect(r.xMax - 124, r.yMax - tap - 8, 110, tap), nextLabel, Ui.Kind.Primary, 14)) { if (Step >= 10) Finish(); else Step = Step + 1; }
            GUI.enabled = was;
        }
    }
}
