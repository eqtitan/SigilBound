// Per-frame garbage in the UI and battle HUD (PLAN step 109, performance review #5, code review M5): the
// allocation-free helpers in Core/UiCache.cs and Unit.Has/Shield measured with the runtime's allocation
// counter, plus source checks for the client code that can't run here (Ui, Skin, the battle HUD).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Sigilbound.Core;

static class HotPathTests
{
    // Bytes allocated by n calls on this thread, after one warm-up call (JIT, lazy caches).
    static long Alloc(Action a, int n = 20000)
    {
        a();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < n; i++) a();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    enum Fs { Normal, Bold } enum Align { Left, Centre }

    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("\nHot paths (no per-frame garbage):");

        // The old Ui.Label key: size, colour as hex, two enums and two bools in one interpolation, every call.
        int size = 15; string hex = "F1E9DAFF"; Fs fs = Fs.Bold; Align al = Align.Centre; bool wrap = false, serif = true;
        var strings = new Dictionary<string, object>(); strings[$"lbl{size}{hex}{fs}{al}{wrap}{serif}"] = 1;
        long oldKey = Alloc(() => strings.TryGetValue($"lbl{size}{hex}{fs}{al}{wrap}{serif}", out _));

        // The new one: a UiKey struct looked up in a Dictionary<UiKey, ...>.
        var keys = new Dictionary<UiKey, object>();
        for (int i = 0; i < 64; i++) keys[new UiKey(5, UiKey.Rgba(i / 64f, 0.5f, 0.2f, 1), x: i, y: i * 3)] = i;
        object hit = null;
        long newKey = Alloc(() => keys.TryGetValue(new UiKey(5, UiKey.Rgba(0.945f, 0.914f, 0.855f, 1f), x: size, y: (int)fs | (int)al << 4 | UiKey.Bits(wrap, 12) | UiKey.Bits(serif, 13)), out hit));
        check(newKey == 0, $"hot path: a style/texture cache lookup allocates nothing ({newKey / 20000.0:0.#} B per call; the old string key was {oldKey / 20000.0:0} B per call)");
        check(new UiKey(1, UiKey.Rgba(1, 0, 0, 1), 0, x: 12) == new UiKey(1, UiKey.Rgba(1f, 0.001f, 0, 1), 0, x: 12) && new UiKey(1, UiKey.Rgba(1, 0, 0, 1)) != new UiKey(1, UiKey.Rgba(1, 0, 0, 0.5f))
            && new UiKey(2, x: 3).GetHashCode() != new UiKey(2, x: 4).GetHashCode(), "hot path: UiKey compares colours at 8 bits per channel (what draws the same shares a key)");

        // 9-slice: the struct replaces four float[] per draw; same numbers as the old arrays.
        float sum = 0;
        long nine = Alloc(() => { var n = NineSlice.Of(10, 20, 200, 60, 12, 12, 8, 8, 256, 128, 24, 24, 16, 16); for (int i = 0; i < 4; i++) sum += n.X(i) + n.Y(i) + n.U(i) + n.V(i); });
        var s9 = NineSlice.Of(10, 20, 200, 60, 12, 12, 8, 8, 256, 128, 24, 24, 16, 16);
        check(nine == 0 && s9.X1 == 22 && s9.X2 == 198 && s9.Y3 == 80 && Math.Abs(s9.U1 - 24 / 256f) < 1e-6 && Math.Abs(s9.V1 - (1 - 16 / 128f)) < 1e-6 && s9.V0 == 1 && s9.V3 == 0,
            $"hot path: a 9-slice cut allocates nothing and matches the old arrays ({nine} B)");

        // Level badges, turn counts and stack labels: cached strings.
        for (int i = 0; i < 1000; i++) { NumText.Of(i); NumText.Times(i); }
        int k = 0;
        long nums = Alloc(() => { k = (k + 7) % 1000; NumText.Of(k); NumText.Times(k); });
        check(nums == 0 && NumText.Of(42) == "42" && NumText.Times(3) == "\u00d73" && ReferenceEquals(NumText.Of(60), NumText.Of(60)) && NumText.Of(12345) == "12345",
            $"hot path: level, turn and stack numbers come from a string cache ({nums} B)");

        // Unit.Has and Unit.Shield (asked per unit per GUI event by the HUD) are plain loops now.
        var u = Battle.MakeUnit(true, GameData.Heroes[0], new Stats { Hp = 1000, Atk = 100, Def = 90, Spd = 100, Crit = 15, Cdmg = 50 });
        u.Effects.Add(new Effect { Type = FxType.AtkUp, Turns = 2 }); u.Effects.Add(new Effect { Type = FxType.Poison, Turns = 2 }); u.Effects.Add(new Effect { Type = FxType.Shield, Turns = 2, Val = 50 });
        bool has = false; Effect sh = null;
        long unit = Alloc(() => { has = u.Has(FxType.Poison) && !u.Has(FxType.Stun); sh = u.Shield; });
        check(unit == 0 && has && sh != null && sh.Val == 50, $"hot path: Unit.Has and Unit.Shield allocate nothing ({unit} B)");

        // Client sources (Unity code can't run here): no string keys, closures, per-call arrays or LINQ left in the hot helpers.
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "hot path: client sources not found here (skipped)"); return; }
        string Src(string f) => File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", f));
        string ui = Src("Ui.cs"), skin = Src("Skin.cs"), bc = Src("BattleController.cs"), icons = Src("StatusIcons.cs"), pal = Src("Palette.cs");
        check(!ui.Contains("Style($\"") && !ui.Contains("ToHtmlStringRGBA(") && !Regex.IsMatch(ui, @"\(\)\s*=>\s*new GUIStyle") && ui.Contains("Dictionary<UiKey, Texture2D>") && ui.Contains("Dictionary<UiKey, GUIStyle>")
              && !ui.Contains("SkinName(Kind k) => \"button_\""), "hot path: Ui caches textures and styles by UiKey (no string keys or closures per call)");
        check(skin.Contains("_on ??= PlayerPrefs.GetInt") && !skin.Contains("float[]") && !skin.Contains("Tex(name + ") && skin.Contains("NineSlice.Of("),
            "hot path: Skin reads PlayerPrefs once, draws 9-slices without arrays and builds plate variant names once");
        var hud = bc.Substring(bc.IndexOf("---- unit tags", StringComparison.Ordinal));
        hud = hud.Substring(0, Math.Min(hud.Length, 4000));
        check(!hud.Contains("placed.Any(") && !hud.Contains("new List<") && !hud.Contains("u.Level.ToString()") && !bc.Contains(": new List<Unit>();"),
            "hot path: the battle HUD reuses its tag lists, has no LINQ per tag and caches level text");
        check(!icons.Contains("\"\\u00d7\" + e.Stacks") && !icons.Contains("e.Turns.ToString()") && pal.Contains("_hex.TryGetValue(hex"),
            "hot path: status icon counts are cached strings and Palette.Hex parses each literal once");
    }
}
