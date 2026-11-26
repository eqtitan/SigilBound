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
    }
}
