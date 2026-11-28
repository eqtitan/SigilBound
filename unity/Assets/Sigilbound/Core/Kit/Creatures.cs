// Creature heroes: heroes whose body is not the 65-joint humanoid (spider-folk, crab-folk, ...). Shared by
// every creature build (blenderforge/creature.py makes the model and its own clips):
//   - the model carries its own clips (it can't use the humanoid UAL library), played by name through Style;
//   - it is scaled by a fixed factor (no head-bone fitting: a spider's head says nothing about its size);
//   - it takes more room in a lineup (SlotWidth) and in melee (MeleeReach);
//   - its legs (any number, any chain) keep their feet planted at runtime (Client/CreatureRig.cs, CreatureLegs);
//   - optional overrides of humanoid-only reactions (knock-down falls over: KnockDownHoldsHit holds the hit pose).
// Add a creature: one CreatureSpec in Table, a model with the bones named in Legs/BodyBone, and the clips its
// Style and Aliases name.
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public sealed class CreatureSpec
    {
        public string Id, Rig;                   // Rig: "spider", "crab", ... (informational, and for tests)
        public double Scale = 1.0;               // model metres -> battle units
        public double ModelHeight = 2.3;         // HeroModel.Height: the head marker sits at 0.87 x this
        public double SlotWidth = 1.0;           // lineup spacing next to this unit (1 = a normal hero)
        public double MeleeReach;                // extra metres kept between centres in melee (its body's half-depth)
        public bool KnockDownHoldsHit;           // knock-downs hold the Hit pose instead of falling over
        public string BodyBone;                  // the creature body bone; feet are planted relative to the unit
        public string[][] Legs = new string[0][]; // each leg: its bones hip to foot, then a tip marker bone at the foot (3-5 names)
        public double StepTime = 0.18, StepLift = 0.14, StepThreshold = 0.12; // runtime foot planting (battle units)
        public AnimStyle Style;
        public Dictionary<string, string> Aliases = new Dictionary<string, string>();
    }

    public static class Creatures
    {
        static string[][] Legs(string prefix, params string[] sides)
        {
            var legs = new List<string[]>();
            foreach (var s in sides)
                legs.Add(new[] { $"{prefix}_{s}_coxa", $"{prefix}_{s}_femur", $"{prefix}_{s}_tibia", $"{prefix}_{s}_tarsus", $"{prefix}_{s}_tip" });
            return legs.ToArray();
        }

        public static readonly Dictionary<string, CreatureSpec> Table = new Dictionary<string, CreatureSpec>
        {
            // Vessa Silkmourne (owner concept 2026-11-27): the UBC upper body on a spider body with eight legs.
            ["vessa"] = new CreatureSpec
            {
                Id = "vessa", Rig = "spider", Scale = 1.3, ModelHeight = 2.55, SlotWidth = 1.35, MeleeReach = 0.45,
                KnockDownHoldsHit = true, BodyBone = "spider_body",
                Legs = Legs("leg", "L1", "L2", "L3", "L4", "R1", "R2", "R3", "R4"),
                Style = AnimStyle.Spider, Aliases = AnimStyle.SpiderAliases,
            },
        };

        public static CreatureSpec Of(string heroId) => heroId != null && Table.TryGetValue(heroId, out var c) ? c : null;
        public static bool Is(string heroId) => Of(heroId) != null;
        public static double SlotWidth(string heroId) => Of(heroId)?.SlotWidth ?? 1.0;
        public static double MeleeReach(string heroId) => Of(heroId)?.MeleeReach ?? 0.0;

        // Room in the menu and gallery lineups (Home, the hero gallery shots): a creature's SlotWidth, or a width for
        // humanoid heroes whose wings, tails, big shields or long blades reach past a normal hero (in-house report,
        // 2026-11-28: Vorgrim's sword and wings ran through Talvo; Wystan, Crusk, Hartwin and Nyssa overlapped).
        // Battles use it too (BattleController.UnitWidth, BattleLayout.Slot): it widens their gaps and keeps those heroes'
        // footprints inside the rune circle (art review M1).
        public static readonly Dictionary<string, double> WideHeroes = new Dictionary<string, double>
        {
            ["vorgrim"] = 1.35, ["talvo"] = 1.2, ["wystan"] = 1.15, ["crusk"] = 1.3, ["hartwin"] = 1.2, ["nyssa"] = 1.35,
        };
        public static double LineupWidth(string heroId) => Of(heroId)?.SlotWidth ?? (heroId != null && WideHeroes.TryGetValue(heroId, out var w) ? w : 1.0);

        // Menu lineup x positions: 1.4 m between two normal heroes (4.2 m for four, as before), each gap scaled by
        // the mean LineupWidth of its neighbours, centred on 0; the whole row is held within MaxLineupSpan.
        public const double LineupGap = 1.4, MaxLineupSpan = 5.2;
        public static double[] LineupXs(IList<string> ids)
        {
            int n = ids.Count; var xs = new double[n];
            if (n <= 1) return xs;
            double total = 0;
            for (int k = 1; k < n; k++) { total += LineupGap * (LineupWidth(ids[k - 1]) + LineupWidth(ids[k])) / 2; xs[k] = total; }
            double scale = total > MaxLineupSpan ? MaxLineupSpan / total : 1;
            for (int k = 0; k < n; k++) xs[k] = (xs[k] - total / 2) * scale;
            return xs;
        }
    }
}
