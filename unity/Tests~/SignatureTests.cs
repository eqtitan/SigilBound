using System.Linq;

// Phase E 53-55: every hero has its own signature motif from its design (docs/sigilbound/sigfx.py),
// and the roster uses a spread of motifs, not one per element.
static class SignatureTests
{
    public static void Run(System.Action<bool, string> check)
    {
        var heroes = Sigilbound.Core.GameData.Heroes;
        var missing = heroes.Where(h => !Sigilbound.Core.HeroSignatures.Table.ContainsKey(h.Id)).Select(h => h.Id).ToList();
        check(missing.Count == 0, $"signatures: all {heroes.Count()} heroes have a motif from their design" + (missing.Count > 0 ? " — missing: " + string.Join(", ", missing.Take(5)) : ""));
        int kinds = heroes.Select(h => Sigilbound.Core.HeroSignatures.Table.TryGetValue(h.Id, out var m) ? m : default).Distinct().Count();
        check(kinds >= 15, $"signatures: {kinds} different motifs across the roster (at least 15)");

        // Status icons (owner-approved legend): every status that shows a tag has its icon file.
        string dir = System.AppContext.BaseDirectory, icons = null;
        for (int i = 0; i < 8 && dir != null && icons == null; i++, dir = System.IO.Path.GetDirectoryName(dir))
        { var p0 = System.IO.Path.Combine(dir, "Assets", "Resources", "StatusIcons"); if (System.IO.Directory.Exists(p0)) icons = p0; }
        if (icons != null)
        {
            var missingIcons = System.Enum.GetValues(typeof(Sigilbound.Core.FxType)).Cast<Sigilbound.Core.FxType>()
                .Where(t => Sigilbound.Core.SkillText.EffectShort(t) != "" && !System.IO.File.Exists(System.IO.Path.Combine(icons, t + ".png"))).ToList();
            check(missingIcons.Count == 0, "status icons: every status with a tag has its icon" + (missingIcons.Count > 0 ? " — missing: " + string.Join(", ", missingIcons) : ""));
        }
    }
}
