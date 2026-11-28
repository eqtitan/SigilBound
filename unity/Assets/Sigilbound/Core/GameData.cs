// Sigilbound core content: elements, heroes, skills, gear, summoning, campaign.
// Pure C# (no UnityEngine) so it compiles and is tested outside Unity.
// Mirrors public/game/data.js in the web version.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilbound.Core
{
    public enum Element { Flame, Gale, Tide, Light, Shadow }
    public enum Rarity { Common, Rare, Epic, Legendary }
    public enum Role { Attack, Defense, Hp, Support }
    public enum Target { Enemy, AllEnemies, Ally, AllAllies, Self }
    public enum Scale { Atk, Def, Hp }
    // The first 16 are the web game's (in its order: IsStatus/IsBuff compare by order). Burn, Pull and
    // Counter (phase 2) and Revive (phase 3) are Unity-only roster mechanics (docs/sigilbound/ROSTER.md section 4), added at the end.
    public enum FxType { AtkUp, DefUp, SpdUp, Regen, Shield, Taunt, AtkDown, DefDown, Slow, Poison, Stun, Heal, Tm, TmDrain, Cleanse, Strip, Burn, Pull, Counter, Revive, Broken }
    public enum FxTo { Target, Self, Allies, Enemies }

    public sealed class FxDef
    {
        public FxType Type; public int Turns = 1; public double Chance = -1; public double Pct; public double Val; public FxTo To = FxTo.Target;
        public bool IsStatus => Type <= FxType.Stun || Type == FxType.Burn || Type == FxType.Counter || Type == FxType.Revive;
        public static bool IsBuff(FxType t) => t <= FxType.Taunt || t == FxType.Counter || t == FxType.Revive;
    }

    public sealed class SkillDef
    {
        public string Name; public Target Target; public double Mult; public int Cooldown; public int Hits = 1; public Scale Scale = Scale.Atk;
        public FxDef[] Fx = Array.Empty<FxDef>();
        // Roster mechanics (Unity only; all off for the web game's skills):
        public bool Pierce;      // each hit also strikes the next enemy in line for half damage
        public double Execute;   // a target left below this HP fraction falls at once (not bosses or the Titan)
        public bool Spread;      // if the target dies, its Poison and Burn jump to the next enemy
        public double Rage;      // damage x (1 + Rage x the attacker's missing HP fraction)
    }

    public sealed class HeroDef
    {
        public string Id, Name; public Rarity Rarity; public Element Element; public Role Role; public SkillDef[] Skills;
        public double FallHeal;  // passive (Unity-only roster): when an ally falls, heal all living allies this share of their max HP
        public string ShortName => Name.Split(' ', ',')[0];
    }

    public static class GameData
    {
        public static readonly Element[] Elements = (Element[])Enum.GetValues(typeof(Element));

        public static double ElementMultiplier(Element att, Element def)
        {
            if (Strong(att) == def) return 1.3;
            if (Strong(def) == att) return 0.8;
            return 1;
        }
        public static Element Strong(Element e) => e switch
        {
            Element.Flame => Element.Gale, Element.Gale => Element.Tide, Element.Tide => Element.Flame,
            Element.Light => Element.Shadow, _ => Element.Light,
        };

        public static int NaturalStars(Rarity r) => (int)r + 1;
        public static double RarityMult(Rarity r) => r switch { Rarity.Common => 1.0, Rarity.Rare => 1.15, Rarity.Epic => 1.32, _ => 1.5 };
        public static int EssenceValue(Rarity r) => r switch { Rarity.Common => 5, Rarity.Rare => 20, Rarity.Epic => 80, _ => 300 };

        public struct RoleMods { public double Hp, Atk, Def; public int Spd; }
        // Unity-only speed spread by role (PLAN steps 98 and 99): attackers act sooner, tanks and bruisers later.
        // Support gets no shift: Mods already gives it +6 (the old +8 counted it twice and made Support the best
        // role and Defense the worst). Kept apart from Mods (which mirrors the web game) so the web parity
        // checks can add it back.
        // The role as players read it (the enum's "Hp" printed raw: UX review #14).
        public static string RoleName(Role r) => r == Role.Hp ? "HP" : r.ToString();
        public static int RoleSpdShift(Role r) => r switch { Role.Attack => 8, Role.Defense => -2, Role.Hp => -4, _ => 0 };
        public static RoleMods Mods(Role r) => r switch
        {
            Role.Attack => new RoleMods { Hp = 0.9, Atk = 1.25, Def = 0.9 },
            Role.Defense => new RoleMods { Hp = 1.05, Atk = 0.85, Def = 1.4 },
            Role.Hp => new RoleMods { Hp = 1.35, Atk = 0.9, Def = 1.0 },
            _ => new RoleMods { Hp = 1.0, Atk = 0.95, Def = 1.0, Spd = 6 },
        };

        // ---- skill helpers ----
        static SkillDef S(string name, Target t, double mult, int cd, params FxDef[] fx) => new SkillDef { Name = name, Target = t, Mult = mult, Cooldown = cd, Fx = fx };
        static SkillDef S(string name, Target t, double mult, int cd, int hits, Scale scale, params FxDef[] fx) => new SkillDef { Name = name, Target = t, Mult = mult, Cooldown = cd, Hits = hits, Scale = scale, Fx = fx };
        static FxDef F(FxType t, int turns = 1, double chance = -1, FxTo to = FxTo.Target) => new FxDef { Type = t, Turns = turns, Chance = chance, To = to };
        static FxDef Heal(double pct) => new FxDef { Type = FxType.Heal, Pct = pct };
        static FxDef Shield(double pct, FxTo to = FxTo.Target) => new FxDef { Type = FxType.Shield, Pct = pct, To = to };
        static FxDef TmUp(double v) => new FxDef { Type = FxType.Tm, Val = v };
        static FxDef Drain(double v, double chance = -1) => new FxDef { Type = FxType.TmDrain, Val = v, Chance = chance };
        static FxDef Cleanse() => new FxDef { Type = FxType.Cleanse };
        static FxDef Strip() => new FxDef { Type = FxType.Strip };
        static HeroDef H(string id, string name, Rarity r, Element e, Role role, params SkillDef[] sk) => new HeroDef { Id = id, Name = name, Rarity = r, Element = e, Role = role, Skills = sk };

        const Target E = Target.Enemy, AE = Target.AllEnemies, AA = Target.AllAllies, SELF = Target.Self;

        public static readonly HeroDef[] Heroes =
        {
            // Common
            H("brannoc", "Brannoc Spearhand", Rarity.Common, Element.Gale, Role.Attack, S("Spear Thrust", E, 1.162, 0), S("Whirling Haft", AE, 0.8134, 3)),
            H("mira", "Mira Saltwhistle", Rarity.Common, Element.Tide, Role.Defense, S("Shell Bash", E, 0.9, 0, 1, Scale.Def), S("Saltwall", AA, 0, 4, F(FxType.DefUp, 2))),
            H("pell", "Pell Cinderbrook", Rarity.Common, Element.Flame, Role.Support, S("Ember Flick", E, 1.0, 0), S("Kindle", AA, 0, 4, Heal(0.15))),
            H("odo", "Odo Mudlark", Rarity.Common, Element.Gale, Role.Hp, S("Mud Slap", E, 0.08, 0, 1, Scale.Hp, F(FxType.Slow, 2, 0.3)), S("Thick Hide", SELF, 0, 4, Shield(0.25), F(FxType.Taunt, 2))),
            H("wren", "Sister Wren", Rarity.Common, Element.Light, Role.Support, S("Lantern Strike", E, 0.8956, 0), S("Vesper Hymn", AA, 0.0, 4, Heal(0.1612))),
            H("kettle", "Kettle Gravejaw", Rarity.Common, Element.Shadow, Role.Attack, S("Grave Hook", E, 0.7885, 0, F(FxType.Poison, 2, 0.3)), S("Rattle Bones", E, 1.2616, 3)),
            // Rare
            H("ysolde", "Ysolde Emberlash", Rarity.Rare, Element.Flame, Role.Attack, S("Lash", E, 0.6186, 0, 2, Scale.Atk), S("Searing Coil", E, 2.1371, 3, F(FxType.DefDown, 2, 0.6)), S("Wildfire", AE, 1.0123, 4, F(FxType.Poison, 2, 0.4))),
            H("corvan", "Corvan Tidecaller", Rarity.Rare, Element.Tide, Role.Support, S("Undertow", E, 1.2948, 0, Drain(0.15, 0.5)), S("Rising Tide", AA, 0.0, 3, Heal(0.259)), S("Tidal Hymn", AA, 0.0, 4, F(FxType.AtkUp, 2), TmUp(0.15))),
            H("hask", "Hask the Unbowed", Rarity.Rare, Element.Gale, Role.Defense, S("Shield Rush", E, 0.9, 0, 1, Scale.Def), S("Unbowed", SELF, 0, 4, F(FxType.Taunt, 2), F(FxType.DefUp, 2)), S("Gale Wall", AA, 0, 4, Shield(0.15))),
            H("lumen", "Lumen Varro", Rarity.Rare, Element.Light, Role.Attack, S("Radiant Edge", E, 1.0, 0), S("Judgment", E, 2.2, 3), S("Dawnbreak", AE, 1.0, 4, F(FxType.AtkDown, 2, 0.5))),
            H("nyx", "Nyx Hollowstep", Rarity.Rare, Element.Shadow, Role.Attack, S("Hollow Cut", E, 0.4311, 0, 3, Scale.Atk), S("Vanish", E, 2.0419, 3, Strip()), S("Night Toll", AE, 0.9642, 4, Drain(0.2, 0.5))),
            H("tamsin", "Tamsin Reedvane", Rarity.Rare, Element.Gale, Role.Support, S("Reed Dart", E, 1.4304, 0, F(FxType.Slow, 2, 0.4)), S("Tailwind", AA, 0.0, 4, F(FxType.SpdUp, 2)), S("Field Remedy", AA, 0.0, 4, Heal(0.2146), Cleanse())),
            H("gorrum", "Gorrum Slagheart", Rarity.Rare, Element.Flame, Role.Defense, S("Slag Fist", E, 0.9, 0, 1, Scale.Def, F(FxType.DefDown, 2, 0.25)), S("Molten Hide", SELF, 0, 4, Shield(0.3), F(FxType.Taunt, 2)), S("Eruption", AE, 0.7, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.2))),
            H("velka", "Velka Driftmourn", Rarity.Rare, Element.Tide, Role.Hp, S("Drift Strike", E, 0.08, 0, 1, Scale.Hp), S("Mournful Wave", AE, 0.06, 4, 1, Scale.Hp, F(FxType.Slow, 2, 0.4)), S("Deep Mending", AA, 0, 4, F(FxType.Regen, 2))),
            // Epic
            H("aurelia", "Aurelia Dawnwarden", Rarity.Epic, Element.Light, Role.Defense, S("Warden's Blow", E, 0.95, 0, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3)), S("Aegis of Dawn", AA, 0, 4, Shield(0.2), F(FxType.DefUp, 2)), S("Sunfall", AE, 0.8, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.25))),
            H("sable", "Sable Nightglass", Rarity.Epic, Element.Shadow, Role.Support, S("Glass Shard", E, 1.3327, 0, F(FxType.DefDown, 2, 0.4)), S("Mirror Veil", AE, 0.7996, 4, F(FxType.DefDown, 2, 0.75), F(FxType.AtkDown, 2, 0.75)), S("Night Grace", AA, 0.0, 5, Heal(0.3332), TmUp(0.2))),
            H("kaito", "Kaito Stormrend", Rarity.Epic, Element.Gale, Role.Attack, S("Rending Gust", E, 0.6, 0, 2, Scale.Atk), S("Thousand Cuts", E, 0.55, 3, 4, Scale.Atk), S("Stormrend", AE, 1.3, 4, F(FxType.Slow, 2, 0.6))),
            H("maelis", "Maelis Deepcurrent", Rarity.Epic, Element.Tide, Role.Attack, S("Trident", E, 1.1, 0), S("Riptide", E, 2.4, 3, Drain(0.3)), S("Maelstrom", AE, 1.2, 4, F(FxType.DefDown, 2, 0.5))),
            H("thrain", "Thrain Ashforge", Rarity.Epic, Element.Flame, Role.Hp, S("Forge Hammer", E, 0.085, 0, 1, Scale.Hp, F(FxType.Stun, 1, 0.15)), S("Ashen Ward", AA, 0, 4, Shield(0.15), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Bellows", AA, 0, 5, F(FxType.Regen, 2), F(FxType.AtkUp, 2))),
            H("oriel", "Oriel Sunveil", Rarity.Epic, Element.Light, Role.Support, S("Veil Light", E, 0.7173, 0), S("Sunveil", AA, 0.0, 4, Heal(0.2152), Cleanse()), S("Ascend", AA, 0.0, 5, F(FxType.AtkUp, 2), F(FxType.SpdUp, 2), TmUp(0.25))),
            // Legendary
            H("vaelith", "Vaelith, the Last Pyre", Rarity.Legendary, Element.Flame, Role.Attack, S("Pyre Brand", E, 1.2, 0, F(FxType.Poison, 2, 0.5)), S("Cinder Rain", AE, 0.7, 3, 2, Scale.Atk, F(FxType.DefDown, 2, 0.5)), S("Last Pyre", E, 3.2, 4, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            H("morwenna", "Morwenna Abyss-Crowned", Rarity.Legendary, Element.Shadow, Role.Attack, S("Abyssal Touch", E, 1.1, 0, Drain(0.15)), S("Crown of Night", AE, 1.1, 4, F(FxType.Stun, 1, 0.35)), S("Drown in Dark", E, 2.8, 4, Strip(), F(FxType.AtkDown, 2))),
            H("seraphine", "Seraphine of the Seventh Bell", Rarity.Legendary, Element.Light, Role.Support, S("Bell Toll", E, 1.0, 0, F(FxType.AtkDown, 2, 0.4)), S("Seventh Bell", AA, 0, 4, Heal(0.35), Shield(0.15)), S("Requiem", AA, 0, 5, TmUp(0.35), F(FxType.AtkUp, 2), F(FxType.DefUp, 2), Cleanse())),
            H("ulfgar", "Ulfgar Stormwall", Rarity.Legendary, Element.Gale, Role.Defense, S("Stormwall Bash", E, 1.0, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.5)), S("Hold the Line", AA, 0, 4, F(FxType.DefUp, 3), F(FxType.Taunt, 2, -1, FxTo.Self), Shield(0.25, FxTo.Self)), S("Avalanche", AE, 0.9, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.3), F(FxType.DefDown, 2, 0.5))),

            // ---- Roster phase 1, Roadwardens (docs/sigilbound/ROSTER.md). Unity only: not in the
            // frozen web game's data.js, so they come after the original 24 (see Classic below).
            // Step 21:
            H("bram", "Bram Wetherby", Rarity.Common, Element.Tide, Role.Hp, S("Oar Swing", E, 0.0626, 0, 1, Scale.Hp), S("Weather the Storm", SELF, 0.0, 4, Shield(0.1565), F(FxType.Regen, 2), F(FxType.Taunt, 2))),
            H("nettle", "Nettle Quickfinch", Rarity.Common, Element.Gale, Role.Attack, S("Twin Sting", E, 0.4424, 0, 2, Scale.Atk, F(FxType.Slow, 2, 0.2)), S("Hedge Dash", E, 1.2904, 3)),
            H("hobb", "Hobb Lanternjaw", Rarity.Common, Element.Light, Role.Defense, S("Lamp Bash", E, 0.8314, 0, 1, Scale.Def), S("Waystone Ward", AA, 0.0, 4, Shield(0.037), F(FxType.DefUp, 2))),
            H("greta", "Greta Brineknot", Rarity.Common, Element.Tide, Role.Support, S("Net Toss", E, 1.0, 0, F(FxType.Slow, 2, 0.4)), S("Mend the Nets", AA, 0, 4, Heal(0.12), Cleanse())),
            // Step 22:
            H("cole", "Cole Ashworth", Rarity.Common, Element.Flame, Role.Attack, S("Cleave", E, 0.8721, 0), S("Ash Split", E, 1.1286, 3, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            H("tilly", "Tilly Mothwing", Rarity.Common, Element.Shadow, Role.Support, S("Moth Swarm", E, 1.3823, 0, F(FxType.DefDown, 2, 0.3)), S("Dusk Wings", AE, 0.4147, 4, F(FxType.DefDown, 2, 0.75), Drain(0.15, 0.5))),
            H("ronan", "Ronan Stoutbarrow", Rarity.Common, Element.Gale, Role.Hp, S("Tankard Swing", E, 0.0539, 0, 1, Scale.Hp), S("Hearth Feast", AA, 0.0, 4, F(FxType.Regen, 2), F(FxType.Taunt, 2, -1, FxTo.Self), Shield(0.135, FxTo.Self))),
            H("isla", "Isla Fenlight", Rarity.Common, Element.Light, Role.Support, S("Chime", E, 0.9793, 0), S("Fenlight Peal", AA, 0.0, 4, Heal(0.0783), F(FxType.SpdUp, 2))),
            // Step 23 (the first new Rares):
            H("dunstan", "Dunstan Oaken", Rarity.Rare, Element.Gale, Role.Defense, S("Shield Bash", E, 0.7898, 0, 1, Scale.Def), S("Muster", AA, 0.0, 4, F(FxType.DefUp, 2), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Old Guard", AA, 0.0, 5, Shield(0.158))),
            H("marisol", "Marisol Wavecrest", Rarity.Rare, Element.Tide, Role.Attack, S("Pearl Strike", E, 1.1554, 0), S("Riptide Dive", E, 2.1953, 3, F(FxType.Slow, 2, 0.5)), S("Wavebreak", AE, 0.982, 4, F(FxType.Slow, 2, 0.4))),
            H("vesper", "Vesper Gloam", Rarity.Rare, Element.Shadow, Role.Attack, S("Gloom Cut", E, 0.6778, 0, 2, Scale.Atk), S("Poisoned Edge", E, 2.3722, 3, F(FxType.Poison, 2, 0.7)), S("Nightfall Feint", AE, 0.9602, 4, F(FxType.DefDown, 2, 0.5))),
            H("brega", "Brega Ironmoss", Rarity.Rare, Element.Tide, Role.Defense, S("Barnacle Bash", E, 0.7255, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.25)), S("Lockkeeper", SELF, 0.0, 4, Shield(0.1612), F(FxType.Taunt, 2)), S("Floodgate", AE, 0.4836, 5, 1, Scale.Def, F(FxType.Slow, 2, 0.5))),
            // Step 24:
            H("fennick", "Fennick Sootpaw", Rarity.Rare, Element.Flame, Role.Support, S("Spark Toss", E, 1.0047, 0, F(FxType.Poison, 2, 0.3)), S("Firework Volley", AE, 0.6028, 4, F(FxType.Poison, 2, 0.6)), S("Bright Burst", AA, 0.0, 4, F(FxType.AtkUp, 2), Heal(0.221))),
            H("orla", "Orla Greywater", Rarity.Rare, Element.Tide, Role.Hp, S("Weed Lash", E, 0.0934, 0, 1, Scale.Hp), S("River Brew", AA, 0.0, 4, Heal(0.1986)), S("Marsh Tonic", AA, 0.0, 5, F(FxType.Regen, 2))),
            H("silas", "Silas Penumbra", Rarity.Rare, Element.Shadow, Role.Defense, S("Gate Strike", E, 0.9, 0, 1, Scale.Def, F(FxType.DefDown, 2, 0.3)), S("Penumbra Ward", SELF, 0, 4, Shield(0.32), F(FxType.Taunt, 2)), S("Black Gate", AE, 0.7, 5, 1, Scale.Def, F(FxType.DefDown, 2, 0.4))),
            H("wynn", "Wynn Brightfeather", Rarity.Rare, Element.Light, Role.Attack, S("Falcon Shot", E, 1.1572, 0), S("Stooping Dive", E, 2.4301, 3, Strip()), S("Feather Storm", AE, 0.9258, 4, F(FxType.AtkDown, 2, 0.3))),
            // ---- Roster phase 2, Glass and Storm. Step 26:
            H("kestrel", "Kestrel Vane", Rarity.Rare, Element.Gale, Role.Attack, new SkillDef { Name = "Lance Thrust", Target = E, Mult = 0.9, Pierce = true }, new SkillDef { Name = "Skyfall Lance", Target = E, Mult = 1.8, Cooldown = 3, Pierce = true }, S("Gale Charge", AE, 1.1429, 4, F(FxType.Slow, 2, 0.3))),
            H("sorrel", "Sorrel Duskmere", Rarity.Rare, Element.Shadow, Role.Hp, S("Pick Strike", E, 0.0697, 0, 1, Scale.Hp, F(FxType.Poison, 2, 0.3)), S("Deep Breath", AA, 0.0, 4, F(FxType.Regen, 2), Shield(0.1307, FxTo.Self)), S("Cave-in", AE, 0.0523, 4, 1, Scale.Hp, F(FxType.Poison, 2, 0.5))),
            H("amber", "Amber Glasswright", Rarity.Rare, Element.Flame, Role.Defense, S("Glass Bash", E, 0.7736, 0, 1, Scale.Def, F(FxType.Burn, 2, 0.3)), S("Kiln Shield", AA, 0.0, 4, Shield(0.1289)), S("Molten Glass", SELF, 0.0, 5, Shield(0.1719), F(FxType.Taunt, 2), F(FxType.Counter, 2))),
            H("tobias", "Tobias Reed", Rarity.Rare, Element.Gale, Role.Support, S("Discord", E, 1.0482, 0, F(FxType.AtkDown, 2, 0.3)), S("Marching Song", AA, 0.0, 4, F(FxType.SpdUp, 2), TmUp(0.2)), S("Hearthsong", AA, 0.0, 4, Heal(0.2096), Cleanse())),
            // Step 27:
            H("nerys", "Nerys Coldspring", Rarity.Rare, Element.Tide, Role.Support, S("Tide Mirror", E, 0.9093, 0, F(FxType.Slow, 2, 0.3)), S("Still Waters", AA, 0.0, 4, Cleanse(), F(FxType.DefUp, 2), TmUp(0.2)), S("Spring Rite", AA, 0.0, 3, Heal(0.2001))),
            H("hale", "Hale Sunforge", Rarity.Rare, Element.Light, Role.Hp, S("Forge Strike", E, 0.1083, 0, 1, Scale.Hp), S("Anvil Heart", SELF, 0.0, 4, Shield(0.0677), F(FxType.Taunt, 2)), S("Lamplight", AA, 0.0, 5, F(FxType.Regen, 2))),
            H("juno", "Juno Ashveil", Rarity.Rare, Element.Flame, Role.Attack, S("Fire Whirl", E, 1.0, 0, F(FxType.Burn, 2, 0.4)), new SkillDef { Name = "Ember Lash", Target = E, Mult = 1.9, Cooldown = 3, Spread = true, Fx = new[] { F(FxType.Burn, 2, 0.6) } }, S("Pyre Dance", AE, 0.8, 4, F(FxType.Burn, 2, 0.4))),
            H("pike", "Pike Saltgrave", Rarity.Rare, Element.Tide, Role.Attack, S("Hook Swing", E, 1.1911, 0, new FxDef { Type = FxType.Pull, Val = 0.15, Chance = 0.5 }), S("Reel In", E, 2.2688, 3, new FxDef { Type = FxType.Pull, Val = 0.3 }), S("Smuggler's Volley", AE, 1.021, 4, F(FxType.DefDown, 2, 0.3))),
            // Step 28 (the first new Epics: Astrid, Caspian):
            H("rhea", "Rhea Lumen-Kin", Rarity.Rare, Element.Light, Role.Defense, S("Lumen Bash", E, 0.9, 0, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3)), S("Sister's Ward", SELF, 0, 4, Shield(0.32), F(FxType.Taunt, 2), Cleanse()), S("Dawnguard", AA, 0, 5, F(FxType.DefUp, 2), Shield(0.1))),
            H("corwin", "Corwin Blackthorn", Rarity.Rare, Element.Shadow, Role.Attack, S("Duel Cut", E, 1.3736, 0), S("Shadow Lunge", E, 2.4725, 3, F(FxType.DefDown, 2, 0.5)), new SkillDef { Name = "Final Verdict", Target = E, Mult = 1.6, Cooldown = 4, Execute = 0.25 }),
            H("astrid", "Astrid Windrider", Rarity.Epic, Element.Gale, Role.Attack, new SkillDef { Name = "Sky Lance", Target = E, Mult = 1.1, Pierce = true }, S("Windrider", E, 1.7, 3, F(FxType.SpdUp, 2, -1, FxTo.Self)), S("Stormdive", AE, 1.2, 4, F(FxType.Slow, 2, 0.5))),
            H("caspian", "Caspian Deepwell", Rarity.Epic, Element.Tide, Role.Hp, S("Tide Crown", E, 0.085, 0, 1, Scale.Hp), S("Deep Ward", AA, 0, 4, Shield(0.16), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Drowned King's Grace", AA, 0, 5, F(FxType.Regen, 2), F(FxType.DefUp, 2))),
            // Step 29:
            H("ignis", "Ignis Kael", Rarity.Epic, Element.Flame, Role.Support, S("Pyre Spark", E, 0.874, 0, F(FxType.Burn, 2, 0.4)), S("Eternal Flame", AA, 0.0, 4, F(FxType.AtkUp, 2), TmUp(0.2), Heal(0.2447)), S("Firestorm Rite", AE, 0.5244, 4, F(FxType.Burn, 2, 0.7))),
            H("lyra", "Lyra Starglass", Rarity.Epic, Element.Light, Role.Support, S("Glass Beam", E, 0.8406, 0, F(FxType.AtkDown, 2, 0.3)), S("Starglass Hymn", AA, 0.0, 4, Heal(0.2101), Cleanse()), S("Oracle's Sight", AA, 0.0, 5, F(FxType.AtkUp, 2), F(FxType.SpdUp, 2))),
            H("malachar", "Malachar Gloomhand", Rarity.Epic, Element.Shadow, Role.Defense, S("Gloom Bash", E, 1.4162, 0, 1, Scale.Def, F(FxType.DefDown, 2, 0.4)), S("Broken Chains", SELF, 0.0, 4, Shield(0.0746), F(FxType.Taunt, 2), F(FxType.Counter, 2)), S("Hollow Quake", AE, 1.1926, 5, 1, Scale.Def, F(FxType.DefDown, 2, 0.5))),
            H("rowan", "Rowan Thornheart", Rarity.Epic, Element.Gale, Role.Defense, S("Thorn Bash", E, 1.8136, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.3)), S("Old Wood Ward", AA, 0.0, 5, Shield(0.2418)), S("Wardenroot", SELF, 0.0, 5, F(FxType.Taunt, 2), F(FxType.DefUp, 1))),
            // Step 30 (finishes phase 2):
            H("selene", "Selene Tidewhisper", Rarity.Epic, Element.Tide, Role.Support, S("Moonlit Wave", E, 0.8104, 0, Drain(0.15, 0.5)), S("Ebb and Flow", AE, 0.3242, 4, Drain(0.25), new FxDef { Type = FxType.Tm, Val = 0.2, To = FxTo.Allies }), S("Moonrise", AA, 0.0, 4, Heal(0.2026), Cleanse())),
            H("brann", "Brann Emberfist", Rarity.Epic, Element.Flame, Role.Hp, S("Iron Fist", E, 0.085, 0, 1, Scale.Hp, F(FxType.Stun, 1, 0.15)), S("Pit Stance", AA, 0, 4, Shield(0.12), F(FxType.Taunt, 2, -1, FxTo.Self), F(FxType.Counter, 2, -1, FxTo.Self)), S("Ember Brawl", AE, 0.06, 4, 1, Scale.Hp, F(FxType.Burn, 2, 0.5), F(FxType.AtkUp, 2, -1, FxTo.Allies))),
            H("ysra", "Ysra Nightbloom", Rarity.Epic, Element.Shadow, Role.Attack, S("Thorn Kiss", E, 0.935, 0, F(FxType.Poison, 2, 0.5)), S("Nightbloom", AE, 0.85, 4, F(FxType.Poison, 2, 0.8)), S("Petal Strike", E, 2.04, 4, F(FxType.Stun, 1, 0.4))),
            H("oswin", "Oswin Dawnspear", Rarity.Epic, Element.Light, Role.Attack, new SkillDef { Name = "Dawnspear", Target = E, Mult = 1.1, Pierce = true }, new SkillDef { Name = "Bellspire Charge", Target = E, Mult = 2.2, Cooldown = 3, Pierce = true, Fx = new[] { F(FxType.AtkUp, 2, -1, FxTo.Self) } }, S("Daybreak", AE, 1.25, 4, F(FxType.AtkDown, 2, 0.4))),
            // ---- Roster phase 3, The Hollow Unbound. Step 32:
            H("grimsby", "Grimsby Boneweaver", Rarity.Rare, Element.Shadow, Role.Support, S("Bone Toss", E, 1.3786, 0, F(FxType.DefDown, 2, 0.3)), S("Crack the Sigil", AE, 0.5514, 4, F(FxType.DefDown, 2, 0.7), new FxDef { Type = FxType.Strip, Chance = 0.5 }), S("Bone Rite", AA, 0.0, 4, Heal(0.2757))),
            H("marrow", "Marrow Cinderhulk", Rarity.Epic, Element.Flame, Role.Attack, new SkillDef { Name = "Cinder Fist", Target = E, Mult = 1.1, Rage = 0.6 }, new SkillDef { Name = "Wrath of the Deep", Target = E, Mult = 2.2, Cooldown = 3, Rage = 1.0 }, S("Hollow Roar", AE, 1.0, 4, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            new HeroDef { Id = "ember", Name = "Ember Tallow", Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Hp, FallHeal = 0.15, Skills = new[] { S("Wax Brand", E, 0.1031, 0, 1, Scale.Hp, F(FxType.Burn, 2, 0.3)), S("Vigil Flame", AA, 0.0, 4, F(FxType.Regen, 2)), S("Candle Ward", SELF, 0.0, 5, Shield(0.2577)) } },
            H("quill", "Quill Inkwell", Rarity.Rare, Element.Light, Role.Support, S("Quill Jab", E, 1.2936, 0, new FxDef { Type = FxType.Strip, Chance = 0.4 }), S("Annotate", AA, 0.0, 4, F(FxType.AtkUp, 2), Heal(0.1941)), S("Redact", E, 1.5523, 4, Strip(), F(FxType.AtkDown, 2, 0.5))),
            // Step 33:
            H("drift", "Drift Coldfoot", Rarity.Common, Element.Tide, Role.Attack, S("Skate Cut", E, 0.452, 0, 2, Scale.Atk, F(FxType.Slow, 2, 0.2)), S("Frost Glide", E, 1.3057, 3, F(FxType.Slow, 2, 0.5))),
            H("sprig", "Sprig Mosslight", Rarity.Common, Element.Gale, Role.Support, S("Twig Poke", E, 1.0296, 0), S("Bloom Song", AA, 0.0, 4, Heal(0.0721), F(FxType.SpdUp, 2))),
            H("tallow", "Tallow Firth", Rarity.Common, Element.Flame, Role.Defense, S("Torch Bash", E, 0.85, 0, 1, Scale.Def, F(FxType.Burn, 2, 0.1)), S("Watch Fire", AA, 0, 4, F(FxType.DefUp, 2))),
            H("hollis", "Hollis Shade", Rarity.Common, Element.Shadow, Role.Hp, S("Grave Thump", E, 0.0626, 0, 1, Scale.Hp), S("Gravedigger's Rest", SELF, 0.0, 4, Shield(0.1565), F(FxType.Regen, 2), F(FxType.Taunt, 2))),
            // Step 35:
            H("auren", "Auren Halewind", Rarity.Epic, Element.Gale, Role.Support, S("Gust Fan", E, 0.8404, 0, F(FxType.Slow, 2, 0.3)), S("Clearing Wind", AA, 0.0, 4, Cleanse(), F(FxType.SpdUp, 2), Heal(0.1681)), S("Skysong", AA, 0.0, 5, F(FxType.AtkUp, 2), TmUp(0.2))),
            H("cinder", "Cinder Vale", Rarity.Epic, Element.Flame, Role.Attack, S("Ember Edge", E, 1.1, 0, F(FxType.Burn, 2, 0.3)), S("Eruption Slash", AE, 1.2, 4, F(FxType.AtkDown, 2, 0.5)), S("Titan's Scar", E, 2.4, 4, F(FxType.Burn, 2, 0.6))),
            H("thessaly", "Thessaly Reefward", Rarity.Epic, Element.Tide, Role.Defense, S("Reef Strike", E, 1.9361, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.3)), S("Fleet Shield", AA, 0.0, 4, Shield(0.1223), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Undertow Wall", AE, 1.6303, 5, 1, Scale.Def, F(FxType.DefDown, 2, 0.4))),
            H("umbra", "Umbra Veilkin", Rarity.Epic, Element.Shadow, Role.Support, S("Void Touch", E, 1.0, 0, Drain(0.15, 0.5)), S("Hollow Eclipse", AE, 0.5, 4, Drain(0.2), F(FxType.DefDown, 2, 0.6)), S("Seer's Mercy", AA, 0, 4, Heal(0.25), Cleanse(), TmUp(0.2))),
            // Step 36 (the first new Legendaries: Kharra, Nocturne, Aelira):
            H("lucan", "Lucan Brightshield", Rarity.Epic, Element.Light, Role.Defense, S("Cathedral Bash", E, 1.5918, 0, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3)), S("Sanctuary", AA, 0.0, 4, Shield(0.1005), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Bell of Light", AE, 1.1728, 5, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3))),
            H("kharra", "Kharra Titanborn", Rarity.Legendary, Element.Flame, Role.Hp, S("Titan Fist", E, 0.0515, 0, 1, Scale.Hp, F(FxType.Stun, 1, 0.2)), S("Stoneblood", SELF, 0.0, 4, Shield(0.0572), F(FxType.Taunt, 2), F(FxType.Regen, 2)), S("Caldera Quake", AE, 0.04, 4, 1, Scale.Hp, F(FxType.Burn, 2, 0.5), F(FxType.AtkUp, 2, -1, FxTo.Allies))),
            H("nocturne", "Nocturne", Rarity.Legendary, Element.Shadow, Role.Attack, S("Night Edge", E, 1.2, 0, F(FxType.DefDown, 2, 0.3)), S("Eclipse Strike", AE, 1.0, 4, F(FxType.Stun, 1, 0.35)), new SkillDef { Name = "Hunter's End", Target = E, Mult = 2.6, Cooldown = 4, Execute = 0.3 }),
            H("aelira", "Aelira Tidecrowned", Rarity.Legendary, Element.Tide, Role.Support, S("Tidal Grace", E, 1.0, 0, Drain(0.15, 0.5)), S("Queen's Tide", AA, 0, 4, Heal(0.35), Shield(0.12), Cleanse()), S("Sunken Crown", AA, 0, 5, TmUp(0.35), F(FxType.AtkUp, 2), F(FxType.DefUp, 2))),
            // Step 37 (finishes phase 3):
            H("zephyrine", "Zephyrine", Rarity.Legendary, Element.Gale, Role.Attack, S("Wind Edge", E, 1.3, 0, F(FxType.Slow, 2, 0.3)), S("Gale Dance", AE, 1.0, 4, F(FxType.SpdUp, 2, -1, FxTo.Allies)), S("Thousand Winds", E, 0.66, 4, 4, Scale.Atk)),
            H("solenne", "Solenne Dawnborn", Rarity.Legendary, Element.Light, Role.Hp, S("Sun Hammer", E, 0.036, 0, 1, Scale.Hp), S("Dawn Vigil", AA, 0.0, 7, new FxDef { Type = FxType.Revive, Pct = 0.2, Turns = 3 }), S("Saint's Light", AA, 0.0, 5, F(FxType.Regen, 2), Shield(0.04), F(FxType.Taunt, 1, -1, FxTo.Self))),
            H("nell", "Nell Shadowgrass", Rarity.Common, Element.Shadow, Role.Attack, S("Reed Hook", E, 0.7586, 0, F(FxType.Poison, 2, 0.25)), S("Fen Snare", E, 0.9913, 3, F(FxType.Slow, 2, 0.5))),
            H("briar", "Briar Wildroot", Rarity.Rare, Element.Gale, Role.Attack, S("Thorn Lash", E, 0.6734, 0, 2, Scale.Atk, F(FxType.Poison, 2, 0.3)), S("Bramble Whip", E, 2.2448, 3, F(FxType.Slow, 2, 0.5)), S("Wild Thicket", AE, 1.0102, 4, F(FxType.Poison, 2, 0.5))),
            // ---- Roster phase 4, The Sigil Bearers. Step 39:
            H("perrin", "Perrin Gullsby", Rarity.Common, Element.Tide, Role.Defense, S("Harbor Bash", E, 0.85, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.1)), S("Breakwater", AA, 0, 4, Shield(0.04), F(FxType.DefUp, 2))),
            H("loam", "Loam Bracken", Rarity.Common, Element.Gale, Role.Defense, S("Plough Bash", E, 0.8978, 0, 1, Scale.Def), S("Hold the Gate", AA, 0.0, 4, F(FxType.DefUp, 2), TmUp(0.03))),
            H("merrit", "Merrit Vellum", Rarity.Common, Element.Light, Role.Attack, S("Squire's Cut", E, 0.8908, 0, new FxDef { Type = FxType.Strip, Chance = 0.2 }), S("Oath Strike", E, 1.3857, 3)),
            H("sienna", "Sienna Firethorn", Rarity.Rare, Element.Flame, Role.Attack, S("Ember Arrow", E, 1.1284, 0, F(FxType.Burn, 2, 0.4)), S("Grove Volley", AE, 0.9591, 4, F(FxType.Burn, 2, 0.5)), S("Heartshot", E, 2.2568, 3)),
            // Step 40:
            H("calder", "Calder Stormglass", Rarity.Epic, Element.Light, Role.Support, S("Lamp Beam", E, 1.0, 0, F(FxType.AtkDown, 2, 0.3)), S("Guiding Light", AA, 0, 4, Heal(0.25), F(FxType.SpdUp, 2)), S("Beacon", AA, 0, 5, TmUp(0.25), F(FxType.AtkUp, 2))),
            H("dorian", "Dorian Lowe", Rarity.Rare, Element.Tide, Role.Hp, S("Anchor Swing", E, 0.1083, 0, 1, Scale.Hp), S("Harbor Watch", SELF, 0.0, 4, Shield(0.0677), F(FxType.Taunt, 2)), S("Tidebreak", AA, 0.0, 5, F(FxType.Regen, 2))),
            H("ivo", "Ivo Brightmantle", Rarity.Rare, Element.Light, Role.Hp, S("Bell Hammer", E, 0.0917, 0, 1, Scale.Hp, F(FxType.Stun, 1, 0.1)), S("Tower Toll", AA, 0.0, 4, F(FxType.Regen, 2), Shield(0.0459, FxTo.Self), F(FxType.Taunt, 1, -1, FxTo.Self)), S("Vigil", SELF, 0.0, 5, F(FxType.DefUp, 2), Heal(0.172))),
            H("saoirse", "Saoirse Wraithmoor", Rarity.Rare, Element.Shadow, Role.Defense, S("Barrow Strike", E, 0.95, 0, 1, Scale.Def, Drain(0.1, 0.4)), S("Moor Ward", SELF, 0, 4, Shield(0.36), F(FxType.Taunt, 2)), S("Grave Mist", AE, 0.8, 5, 1, Scale.Def, Drain(0.15, 0.5))),
            // Step 41:
            H("gideon", "Gideon Flamecaller", Rarity.Epic, Element.Flame, Role.Defense, S("Pyre Bash", E, 1.4699, 0, 1, Scale.Def, F(FxType.Burn, 2, 0.25)), S("Pyre Guard", AA, 0.0, 4, Shield(0.0928), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Flame Wall", AE, 1.2378, 5, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3))),
            H("liora", "Liora Tidesinger", Rarity.Epic, Element.Tide, Role.Attack, S("Siren Call", E, 1.1, 0, F(FxType.Slow, 2, 0.3)), S("Storm Song", AE, 1.2, 4, F(FxType.Slow, 2, 0.6)), S("Undertow Spear", E, 2.4, 4, Drain(0.3))),
            H("bastian", "Bastian Grey", Rarity.Epic, Element.Shadow, Role.Hp, S("Grey Fist", E, 0.1101, 0, 1, Scale.Hp, F(FxType.DefDown, 2, 0.2)), S("Hollow Blood", AA, 0.0, 4, Shield(0.1553), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Brawl", AE, 0.0777, 4, 1, Scale.Hp, F(FxType.DefDown, 2, 0.5), F(FxType.AtkUp, 2, -1, FxTo.Allies))),
            H("elowen", "Elowen Brightwater", Rarity.Epic, Element.Light, Role.Attack, S("Light Arrow", E, 1.3128, 0, F(FxType.AtkDown, 2, 0.3)), S("Shard Arrow", E, 2.9682, 4, Strip()), S("Rain of Light", AE, 1.3699, 4, F(FxType.AtkDown, 2, 0.4))),
            // Step 42:
            H("faelan", "Faelan Galeheart", Rarity.Epic, Element.Gale, Role.Hp, new SkillDef { Name = "Chieftain's Spear", Target = E, Mult = 0.085, Scale = Scale.Hp, Pierce = true }, S("War Banner", AA, 0.0, 4, Shield(0.044), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Highland Roar", AA, 0.0, 5, F(FxType.Regen, 2), F(FxType.SpdUp, 2))),
            H("wick", "Saint Wick of the Lamps", Rarity.Legendary, Element.Flame, Role.Support, S("Lamp Spark", E, 1.0, 0, F(FxType.Burn, 2, 0.3)), S("Hearth Rite", AA, 0, 4, Heal(0.35), Shield(0.12), Cleanse()), S("Kindle All", AA, 0, 5, TmUp(0.35), F(FxType.AtkUp, 2), F(FxType.DefUp, 2))),
            H("azura", "Azura Deepstar", Rarity.Legendary, Element.Tide, Role.Attack, S("Deepstar Thrust", E, 1.2, 0, F(FxType.Slow, 2, 0.3)), S("Tidal Crash", AE, 1.0, 4, F(FxType.Stun, 1, 0.35)), S("Abyssal Spear", E, 2.8, 4, Drain(0.3))),
            H("pyrrhus", "Pyrrhus Sunderflame", Rarity.Legendary, Element.Flame, Role.Defense, S("Sunder Bash", E, 1.0, 0, 1, Scale.Def, F(FxType.Burn, 2, 0.3)), S("Flame Bulwark", AA, 0, 4, Shield(0.15), F(FxType.DefUp, 2), F(FxType.Taunt, 2, -1, FxTo.Self), F(FxType.Counter, 2, -1, FxTo.Self)), S("Sunder Quake", AE, 0.9, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.25))),
            // Step 43 (completes the roster of 100):
            H("kaelith", "Kaelith Shadowborne", Rarity.Legendary, Element.Shadow, Role.Hp, S("Shadow Orb", E, 0.079, 0, 1, Scale.Hp, Drain(0.15, 0.5)), S("Night Regalia", SELF, 0.0, 4, F(FxType.Taunt, 2), F(FxType.Regen, 2)), S("Eclipse Tide", AE, 0.0614, 4, 1, Scale.Hp, Drain(0.2), F(FxType.DefDown, 2, 0.4))),
            H("aurex", "Aurex Lightsworn", Rarity.Legendary, Element.Light, Role.Attack, S("Lightsworn Edge", E, 1.2, 0, F(FxType.AtkDown, 2, 0.3)), S("Radiant Judgment", AE, 1.1, 4, new FxDef { Type = FxType.Strip, Chance = 0.5 }), S("Dawn Blade", E, 2.6, 4, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            H("stormbearer", "Gale Stormbearer", Rarity.Legendary, Element.Gale, Role.Defense, S("Gale Bash", E, 1.2409, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.4)), S("Storm Aegis", AA, 0.0, 4, Shield(0.1489), F(FxType.DefUp, 2), F(FxType.SpdUp, 2), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Tempest", AE, 1.1168, 5, 1, Scale.Def, F(FxType.DefDown, 2, 0.4))),
            H("vex", "Vex Hollowking", Rarity.Legendary, Element.Shadow, Role.Support, S("Hollow Decree", E, 1.0, 0, F(FxType.DefDown, 2, 0.4)), S("King's Command", AA, 0, 5, Heal(0.38), Shield(0.12), Cleanse(), TmUp(0.25), F(FxType.AtkUp, 2)), S("Crown of Ruin", AE, 0.6, 4, F(FxType.Stun, 1, 0.3), Strip())),
            // --- phase H: generated by docs/sigilbound/newheroes.py from docs/sigilbound/concepts/new_heroes.json (do not edit by hand) ---
            new HeroDef { Id = "dace", Name = "Dace Sootworth", Rarity = Rarity.Common, Element = Element.Flame, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Grille Bash", Target = Target.Enemy, Mult = 0.8004, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.1 } } }, new SkillDef { Name = "Soot Screen", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.DefUp, Turns = 2 }, new FxDef { Type = FxType.Shield, Pct = 0.0377 } } } } },
            new HeroDef { Id = "dulcie", Name = "Dulcie Oventhorne", Rarity = Rarity.Common, Element = Element.Flame, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Peel Smack", Target = Target.Enemy, Mult = 0.0828, Cooldown = 0, Scale = Scale.Hp, Fx = new FxDef[0] }, new SkillDef { Name = "Fresh from the Oven", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.0828 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self }, new FxDef { Type = FxType.Shield, Pct = 0.1552, To = FxTo.Self } } } } },
            new HeroDef { Id = "holt", Name = "Holt Ironpot", Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Pot Slam", Target = Target.Enemy, Mult = 0.9, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.25 } } }, new SkillDef { Name = "Cast-Iron", Target = Target.Self, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.3 }, new FxDef { Type = FxType.Taunt, Turns = 2 } } }, new SkillDef { Name = "Spill the Coals", Target = Target.AllEnemies, Mult = 0.6, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "rhosyn", Name = "Rhosyn Slingcoal", Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Hot Coal", Target = Target.Enemy, Mult = 1.3535, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Double Shot", Target = Target.Enemy, Mult = 0.8121, Cooldown = 3, Hits = 2, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Coal Hail", Target = Target.AllEnemies, Mult = 1.1505, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "ansel", Name = "Brother Ansel Smokewright", Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Censer Swing", Target = Target.Enemy, Mult = 1.3898, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Sweet Smoke", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.2085 }, new FxDef { Type = FxType.Cleanse } } }, new SkillDef { Name = "Kindled Prayer", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.AtkUp, Turns = 2 }, new FxDef { Type = FxType.Tm, Val = 0.15 } } } } },
            new HeroDef { Id = "saskia", Name = "Saskia Emberbark", Rarity = Rarity.Epic, Element = Element.Flame, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Bark Bash", Target = Target.Enemy, Mult = 2.0282, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.25 } } }, new SkillDef { Name = "Grove Wall", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.1281 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Kindling Thorns", Target = Target.Self, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.427 }, new FxDef { Type = FxType.Counter, Turns = 2 } } } } },
            new HeroDef { Id = "kasimir", Name = "Kasimir Brand", Rarity = Rarity.Epic, Element = Element.Flame, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Brand", Target = Target.Enemy, Mult = 1.4399, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Searing Mark", Target = Target.Enemy, Mult = 2.618, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2 }, new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.6 } } }, new SkillDef { Name = "Iron Sweep", Target = Target.AllEnemies, Mult = 1.1781, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "imogen", Name = "Imogen Drumfire", Rarity = Rarity.Epic, Element = Element.Flame, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Drumbeat", Target = Target.Enemy, Mult = 1.7608, Cooldown = 0, Fx = new FxDef[0] }, new SkillDef { Name = "Rally Beat", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.AtkUp, Turns = 2 }, new FxDef { Type = FxType.SpdUp, Turns = 2 } } }, new SkillDef { Name = "Ember Cadence", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Tm, Val = 0.2 }, new FxDef { Type = FxType.Heal, Pct = 0.2641 } } } } },
            new HeroDef { Id = "ilsabet", Name = "Ilsabet Cometheart", Rarity = Rarity.Legendary, Element = Element.Flame, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Comet Lash", Target = Target.Enemy, Mult = 1.2, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.5 } } }, new SkillDef { Name = "Meteor Ring", Target = Target.AllEnemies, Mult = 0.7, Cooldown = 3, Hits = 2, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.5 } } }, new SkillDef { Name = "Starfall", Target = Target.Enemy, Mult = 3.0, Cooldown = 4, Spread = true, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2 }, new FxDef { Type = FxType.AtkUp, Turns = 2, To = FxTo.Self } } } } },
            new HeroDef { Id = "obrecht", Name = "Obrecht", Rarity = Rarity.Legendary, Element = Element.Flame, Role = Role.Hp, FallHeal = 0.12, Skills = new[] { new SkillDef { Name = "Poker Slam", Target = Target.Enemy, Mult = 0.0493, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Hearthguard", Target = Target.AllAllies, Mult = 0.0, Cooldown = 6, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.0658 }, new FxDef { Type = FxType.Taunt, Turns = 1, To = FxTo.Self }, new FxDef { Type = FxType.Regen, Turns = 2 } } }, new SkillDef { Name = "Flue Roar", Target = Target.AllEnemies, Mult = 0.0329, Cooldown = 5, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.5 } } } } },
            new HeroDef { Id = "tansy", Name = "Tansy Burrfoot", Rarity = Rarity.Common, Element = Element.Gale, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Boomerang", Target = Target.Enemy, Mult = 0.443, Cooldown = 0, Hits = 2, Fx = new FxDef[0] }, new SkillDef { Name = "Return Throw", Target = Target.Enemy, Mult = 1.2404, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "barley", Name = "Barley Fleecewell", Rarity = Rarity.Common, Element = Element.Gale, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Shear Snip", Target = Target.Enemy, Mult = 0.1428, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.2 } } }, new SkillDef { Name = "Woolwrap", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.0892 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } } } },
            new HeroDef { Id = "clover", Name = "Clover Kitewright", Rarity = Rarity.Common, Element = Element.Gale, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Kite Dive", Target = Target.Enemy, Mult = 1.5157, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Updraft", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.SpdUp, Turns = 2 }, new FxDef { Type = FxType.Tm, Val = 0.1 } } } } },
            new HeroDef { Id = "edric", Name = "Edric Millsail", Rarity = Rarity.Rare, Element = Element.Gale, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Sail Bash", Target = Target.Enemy, Mult = 0.7762, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Millwall", Target = Target.Self, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.2587 }, new FxDef { Type = FxType.Taunt, Turns = 2 } } }, new SkillDef { Name = "Turning Sails", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.DefUp, Turns = 2 }, new FxDef { Type = FxType.Tm, Val = 0.05 } } } } },
            new HeroDef { Id = "corran", Name = "Corran Highstep", Rarity = Rarity.Rare, Element = Element.Gale, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Staff Sweep", Target = Target.Enemy, Mult = 0.5836, Cooldown = 0, Hits = 2, Fx = new FxDef[0] }, new SkillDef { Name = "Ridge Vault", Target = Target.Enemy, Mult = 2.1008, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.SpdUp, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Gale Spin", Target = Target.AllEnemies, Mult = 0.9337, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "linnet", Name = "Linnet Reedpipe", Rarity = Rarity.Rare, Element = Element.Gale, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Piping Note", Target = Target.Enemy, Mult = 1.2712, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.TmDrain, Chance = 0.4, Val = 0.1 } } }, new SkillDef { Name = "Wayfarer's Air", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.1907 }, new FxDef { Type = FxType.SpdUp, Turns = 2 } } }, new SkillDef { Name = "Swallow Song", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Cleanse }, new FxDef { Type = FxType.Tm, Val = 0.15 } } } } },
            new HeroDef { Id = "ysabeau", Name = "Ysabeau Galecutter", Rarity = Rarity.Epic, Element = Element.Gale, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Cutlass Gust", Target = Target.Enemy, Mult = 1.519, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Dive from Above", Target = Target.Enemy, Mult = 3.038, Cooldown = 3, Pierce = true, Fx = new FxDef[0] }, new SkillDef { Name = "Squall Raid", Target = Target.AllEnemies, Mult = 1.3809, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "wendel", Name = "Wendel Hivewarden", Rarity = Rarity.Epic, Element = Element.Gale, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Smoker Puff", Target = Target.Enemy, Mult = 0.0945, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Royal Jelly", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Regen, Turns = 2 }, new FxDef { Type = FxType.Heal, Pct = 0.1112 } } }, new SkillDef { Name = "Swarm Wall", Target = Target.Self, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.2779 }, new FxDef { Type = FxType.Taunt, Turns = 2 }, new FxDef { Type = FxType.Counter, Turns = 2 } } } } },
            new HeroDef { Id = "hilde", Name = "Hilde Windrose", Rarity = Rarity.Epic, Element = Element.Gale, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Rose Bash", Target = Target.Enemy, Mult = 0.7058, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Plant the Pavise", Target = Target.Self, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.2229 }, new FxDef { Type = FxType.Taunt, Turns = 1 } } }, new SkillDef { Name = "True North", Target = Target.AllAllies, Mult = 0.0, Cooldown = 6, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.0743 }, new FxDef { Type = FxType.Cleanse } } } } },
            new HeroDef { Id = "aveline", Name = "Aveline Skyloom", Rarity = Rarity.Legendary, Element = Element.Gale, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Shuttle Strike", Target = Target.Enemy, Mult = 1.3332, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Woven Gale", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.4 }, new FxDef { Type = FxType.SpdUp, Turns = 2 }, new FxDef { Type = FxType.Cleanse } } }, new SkillDef { Name = "Four Winds", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Tm, Val = 0.37 }, new FxDef { Type = FxType.AtkUp, Turns = 2 }, new FxDef { Type = FxType.DefUp, Turns = 2 } } } } },
            new HeroDef { Id = "jory", Name = "Jory Saltline", Rarity = Rarity.Common, Element = Element.Tide, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Harpoon Jab", Target = Target.Enemy, Mult = 0.8536, Cooldown = 0, Fx = new FxDef[0] }, new SkillDef { Name = "Reel In", Target = Target.Enemy, Mult = 1.1097, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Pull, Val = 0.15 } } } } },
            new HeroDef { Id = "morag", Name = "Morag Kelpwright", Rarity = Rarity.Common, Element = Element.Tide, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Rake", Target = Target.Enemy, Mult = 0.6733, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.2 } } }, new SkillDef { Name = "Kelp Poultice", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.0673 }, new FxDef { Type = FxType.Regen, Turns = 2 } } } } },
            new HeroDef { Id = "wenna", Name = "Wenna Shellback", Rarity = Rarity.Rare, Element = Element.Tide, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Shell Butt", Target = Target.Enemy, Mult = 0.9918, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.25 } } }, new SkillDef { Name = "Tuck In", Target = Target.Self, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.3526 }, new FxDef { Type = FxType.Taunt, Turns = 2 } } }, new SkillDef { Name = "Tidepool Guard", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.DefUp, Turns = 2 }, new FxDef { Type = FxType.Shield, Pct = 0.0661 } } } } },
            new HeroDef { Id = "lazlo", Name = "Lazlo Swordbill", Rarity = Rarity.Rare, Element = Element.Tide, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Bill Thrust", Target = Target.Enemy, Mult = 1.0522, Cooldown = 0, Pierce = true, Fx = new FxDef[0] }, new SkillDef { Name = "Lunge", Target = Target.Enemy, Mult = 1.9992, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.5 } } }, new SkillDef { Name = "Shoal Rush", Target = Target.AllEnemies, Mult = 0.8944, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "mungo", Name = "Mungo Barrelhand", Rarity = Rarity.Rare, Element = Element.Tide, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Barrel Roll", Target = Target.Enemy, Mult = 0.0493, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.15 } } }, new SkillDef { Name = "Salted Rations", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Regen, Turns = 2 } } }, new SkillDef { Name = "Bulkhead", Target = Target.Self, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.1542 }, new FxDef { Type = FxType.Taunt, Turns = 2 } } } } },
            new HeroDef { Id = "neve", Name = "Neve Conchcaller", Rarity = Rarity.Rare, Element = Element.Tide, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Conch Blast", Target = Target.Enemy, Mult = 1.7612, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Calling Tide", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.317 } } }, new SkillDef { Name = "Low Tide", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Cleanse }, new FxDef { Type = FxType.Tm, Val = 0.15 } } } } },
            new HeroDef { Id = "hendrick", Name = "Captain Hendrick Prowe", Rarity = Rarity.Epic, Element = Element.Tide, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Prow Ram", Target = Target.Enemy, Mult = 1.8767, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Hold Fast", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.1185 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Broadside", Target = Target.AllEnemies, Mult = 1.5804, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.5 } } } } },
            new HeroDef { Id = "kaija", Name = "Kaija Floebreaker", Rarity = Rarity.Epic, Element = Element.Tide, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Saw Bite", Target = Target.Enemy, Mult = 0.55, Cooldown = 0, Hits = 2, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Floe Split", Target = Target.Enemy, Mult = 2.3, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.TmDrain, Val = 0.25 } } }, new SkillDef { Name = "Shatter", Target = Target.AllEnemies, Mult = 1.1, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.2 } } } } },
            new HeroDef { Id = "morvoren", Name = "Morvoren", Rarity = Rarity.Legendary, Element = Element.Tide, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Undertow Grip", Target = Target.Enemy, Mult = 0.0465, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.TmDrain, Val = 0.15 } } }, new SkillDef { Name = "Barrow Hymn", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.062 }, new FxDef { Type = FxType.Regen, Turns = 2 } } }, new SkillDef { Name = "Grave Tide", Target = Target.AllEnemies, Mult = 0.0362, Cooldown = 5, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.5 }, new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "leofric", Name = "Leofric Tidewarden", Rarity = Rarity.Legendary, Element = Element.Tide, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Fluke Slam", Target = Target.Enemy, Mult = 1.3535, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Sea Wall", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.203 }, new FxDef { Type = FxType.DefUp, Turns = 2 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Breach", Target = Target.AllEnemies, Mult = 1.2182, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.3 } } } } },
            new HeroDef { Id = "hettie", Name = "Hettie Sunfield", Rarity = Rarity.Common, Element = Element.Light, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Fork Jab", Target = Target.Enemy, Mult = 0.8831, Cooldown = 0, Fx = new FxDef[0] }, new SkillDef { Name = "Harvest Thrust", Target = Target.Enemy, Mult = 1.3247, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } } } },
            new HeroDef { Id = "abel", Name = "Abel Millstone", Rarity = Rarity.Common, Element = Element.Light, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Grindstone", Target = Target.Enemy, Mult = 0.0908, Cooldown = 0, Scale = Scale.Hp, Fx = new FxDef[0] }, new SkillDef { Name = "Daily Bread", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Regen, Turns = 2 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } } } },
            new HeroDef { Id = "poppy", Name = "Poppy Dawnseed", Rarity = Rarity.Common, Element = Element.Light, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Petal Toss", Target = Target.Enemy, Mult = 0.8476, Cooldown = 0, Fx = new FxDef[0] }, new SkillDef { Name = "Sunbloom", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.1017 }, new FxDef { Type = FxType.DefUp, Turns = 2 } } } } },
            new HeroDef { Id = "benedict", Name = "Benedict", Rarity = Rarity.Rare, Element = Element.Light, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Dial Bash", Target = Target.Enemy, Mult = 1.2322, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Noon Ward", Target = Target.Self, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.4107 }, new FxDef { Type = FxType.Taunt, Turns = 2 }, new FxDef { Type = FxType.Cleanse } } }, new SkillDef { Name = "High Sun", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.DefUp, Turns = 2 } } } } },
            new HeroDef { Id = "emeric", Name = "Emeric Larkspur", Rarity = Rarity.Rare, Element = Element.Light, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Javelin", Target = Target.Enemy, Mult = 1.228, Cooldown = 0, Pierce = true, Fx = new FxDef[0] }, new SkillDef { Name = "Volley", Target = Target.AllEnemies, Mult = 0.9824, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Sunlance Throw", Target = Target.Enemy, Mult = 2.456, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Strip } } } } },
            new HeroDef { Id = "agathe", Name = "Mother Agathe", Rarity = Rarity.Rare, Element = Element.Light, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Reliquary Bash", Target = Target.Enemy, Mult = 0.0433, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.15 } } }, new SkillDef { Name = "Relic Glow", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Regen, Turns = 2 } } }, new SkillDef { Name = "Sanctified", Target = Target.Self, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.1354 }, new FxDef { Type = FxType.Taunt, Turns = 2 }, new FxDef { Type = FxType.Cleanse } } } } },
            new HeroDef { Id = "ottavia", Name = "Ottavia Sunhalberd", Rarity = Rarity.Epic, Element = Element.Light, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Halberd Cleave", Target = Target.Enemy, Mult = 1.3746, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Dawn Reaping", Target = Target.AllEnemies, Mult = 1.2496, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Strip, Chance = 0.3 } } }, new SkillDef { Name = "Sunrise Verdict", Target = Target.Enemy, Mult = 2.999, Cooldown = 4, Execute = 0.25, Fx = new FxDef[0] } } },
            new HeroDef { Id = "fabian", Name = "Magister Fabian Orrery", Rarity = Rarity.Epic, Element = Element.Light, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Star Ping", Target = Target.Enemy, Mult = 1.2061, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Alignment", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Tm, Val = 0.2 }, new FxDef { Type = FxType.SpdUp, Turns = 2 } } }, new SkillDef { Name = "Clear Skies", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.3015 }, new FxDef { Type = FxType.Cleanse } } } } },
            new HeroDef { Id = "augustin", Name = "Augustin Goldmane", Rarity = Rarity.Epic, Element = Element.Light, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Lion's Grip", Target = Target.Enemy, Mult = 0.0945, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.2 } } }, new SkillDef { Name = "Pride's Stand", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.0889 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Roar of Dawn", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.AtkUp, Turns = 2 }, new FxDef { Type = FxType.Regen, Turns = 2 } } } } },
            new HeroDef { Id = "clemence", Name = "Clemence Brightlens", Rarity = Rarity.Legendary, Element = Element.Light, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Lens Flash", Target = Target.Enemy, Mult = 1.3703, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Prism Bulwark", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.2055 }, new FxDef { Type = FxType.DefUp, Turns = 2 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Focused Beam", Target = Target.AllEnemies, Mult = 1.2333, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.3 } } } } },
            new HeroDef { Id = "rook", Name = "Rook Fenwhistle", Rarity = Rarity.Common, Element = Element.Shadow, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Snap", Target = Target.Enemy, Mult = 0.7636, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Spring the Trap", Target = Target.Enemy, Mult = 1.069, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2 } } } } },
            new HeroDef { Id = "pernille", Name = "Pernille Strings", Rarity = Rarity.Common, Element = Element.Shadow, Role = Role.Support, Skills = new[] { new SkillDef { Name = "String Pull", Target = Target.Enemy, Mult = 1.3528, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Curtain Call", Target = Target.AllEnemies, Mult = 0.4058, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.7 }, new FxDef { Type = FxType.TmDrain, Val = 0.1 } } } } },
            new HeroDef { Id = "perpetua", Name = "Lady Perpetua Nightshade", Rarity = Rarity.Rare, Element = Element.Shadow, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Ferrule Jab", Target = Target.Enemy, Mult = 0.9886, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Open Canopy", Target = Target.Self, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.3295 }, new FxDef { Type = FxType.Taunt, Turns = 2 } } }, new SkillDef { Name = "Starless Rain", Target = Target.AllEnemies, Mult = 0.659, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.5 } } } } },
            new HeroDef { Id = "jasper", Name = "Jasper Blackcard", Rarity = Rarity.Rare, Element = Element.Shadow, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Card Flick", Target = Target.Enemy, Mult = 0.644, Cooldown = 0, Hits = 2, Fx = new FxDef[0] }, new SkillDef { Name = "Stacked Deck", Target = Target.Enemy, Mult = 2.3182, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Strip } } }, new SkillDef { Name = "Fifty-Two Pickup", Target = Target.AllEnemies, Mult = 1.0947, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.4 } } } } },
            new HeroDef { Id = "mabon", Name = "Mabon Sandglass", Rarity = Rarity.Rare, Element = Element.Shadow, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Grain of Sand", Target = Target.Enemy, Mult = 1.8312, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.TmDrain, Chance = 0.5, Val = 0.15 } } }, new SkillDef { Name = "Turn the Glass", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Tm, Val = 0.2 } } }, new SkillDef { Name = "Borrowed Time", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.2747 }, new FxDef { Type = FxType.Cleanse } } } } },
            new HeroDef { Id = "tindra", Name = "Tindra Mirefang", Rarity = Rarity.Epic, Element = Element.Shadow, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Fen Dart", Target = Target.Enemy, Mult = 1.3772, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Mire Toxin", Target = Target.Enemy, Mult = 2.6167, Cooldown = 3, Spread = true, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2 } } }, new SkillDef { Name = "Cloud of Darts", Target = Target.AllEnemies, Mult = 1.2395, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2, Chance = 0.5 } } } } },
            new HeroDef { Id = "ambrose", Name = "Ambrose Keywarden", Rarity = Rarity.Epic, Element = Element.Shadow, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Key Ring", Target = Target.Enemy, Mult = 0.0969, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Locksmith's Ward", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.0912 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Night Watch", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Regen, Turns = 2 }, new FxDef { Type = FxType.DefUp, Turns = 2 } } } } },
            new HeroDef { Id = "lune", Name = "Lune Duskfeather", Rarity = Rarity.Epic, Element = Element.Shadow, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Night Call", Target = Target.Enemy, Mult = 1.7879, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.TmDrain, Chance = 0.4, Val = 0.15 } } }, new SkillDef { Name = "Night Vigil", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.3576 }, new FxDef { Type = FxType.DefUp, Turns = 2 } } }, new SkillDef { Name = "Silent Wings", Target = Target.AllEnemies, Mult = 0.894, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.6 }, new FxDef { Type = FxType.TmDrain, Val = 0.15 } } } } },
            new HeroDef { Id = "aldric", Name = "Sir Aldric Twinshade", Rarity = Rarity.Legendary, Element = Element.Shadow, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Twin Strike", Target = Target.Enemy, Mult = 1.3424, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Second Shadow", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.2014 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self }, new FxDef { Type = FxType.Counter, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Eclipse Wall", Target = Target.AllEnemies, Mult = 1.2082, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.3 }, new FxDef { Type = FxType.TmDrain, Val = 0.15 } } } } },
            new HeroDef { Id = "ondessa", Name = "Ondessa Mirebloom", Rarity = Rarity.Legendary, Element = Element.Shadow, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Mire Grasp", Target = Target.Enemy, Mult = 0.027, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Wisp Ring", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.036 }, new FxDef { Type = FxType.Regen, Turns = 2 }, new FxDef { Type = FxType.Taunt, Turns = 1, To = FxTo.Self } } }, new SkillDef { Name = "Drown the Light", Target = Target.AllEnemies, Mult = 0.021, Cooldown = 5, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.TmDrain, Val = 0.2 }, new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.5 } } } } },
            new HeroDef { Id = "vorgrim", Name = "Vorgrim Ashscale", Rarity = Rarity.Legendary, Element = Element.Flame, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Cinder Cleave", Target = Target.Enemy, Mult = 1.2, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.5 } } }, new SkillDef { Name = "Dragon's Wrath", Target = Target.AllEnemies, Mult = 1.0, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2, Chance = 0.5 }, new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Molten Execution", Target = Target.Enemy, Mult = 3.1, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2 }, new FxDef { Type = FxType.AtkUp, Turns = 2, To = FxTo.Self } } } } },
            new HeroDef { Id = "talvo", Name = "Talvo Brassfang", Rarity = Rarity.Epic, Element = Element.Flame, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Flick of Flame", Target = Target.Enemy, Mult = 0.9038, Cooldown = 0, Hits = 2, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Riposte", Target = Target.Self, Mult = 0.0, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Counter, Turns = 2 }, new FxDef { Type = FxType.SpdUp, Turns = 2 } } }, new SkillDef { Name = "Blazing Flourish", Target = Target.Enemy, Mult = 2.1691, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Burn, Turns = 2 }, new FxDef { Type = FxType.Tm, Val = 0.3, To = FxTo.Self } } } } },
            new HeroDef { Id = "wystan", Name = "Wystan Quillmere", Rarity = Rarity.Epic, Element = Element.Gale, Role = Role.Support, Skills = new[] { new SkillDef { Name = "Gust of Pages", Target = Target.Enemy, Mult = 1.2244, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.3 } } }, new SkillDef { Name = "Chime of Clarity", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.2449 }, new FxDef { Type = FxType.Cleanse } } }, new SkillDef { Name = "Tailwind Treatise", Target = Target.AllAllies, Mult = 0.0, Cooldown = 5, Fx = new[] { new FxDef { Type = FxType.Tm, Val = 0.25 }, new FxDef { Type = FxType.SpdUp, Turns = 2 } } } } },
            new HeroDef { Id = "crusk", Name = "Crusk Brineclaw", Rarity = Rarity.Epic, Element = Element.Tide, Role = Role.Defense, Skills = new[] { new SkillDef { Name = "Pincer Snap", Target = Target.Enemy, Mult = 1.6669, Cooldown = 0, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.35 } } }, new SkillDef { Name = "Shell Wall", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.1053 }, new FxDef { Type = FxType.Taunt, Turns = 2, To = FxTo.Self } } }, new SkillDef { Name = "Undertow", Target = Target.AllEnemies, Mult = 1.4037, Cooldown = 5, Scale = Scale.Def, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.35 } } } } },
            new HeroDef { Id = "hartwin", Name = "Hartwin Sunhorn", Rarity = Rarity.Legendary, Element = Element.Light, Role = Role.Hp, Skills = new[] { new SkillDef { Name = "Sunhammer", Target = Target.Enemy, Mult = 0.0461, Cooldown = 0, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.25 } } }, new SkillDef { Name = "Toll of Dawn", Target = Target.AllAllies, Mult = 0.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.0554 }, new FxDef { Type = FxType.Regen, Turns = 2 }, new FxDef { Type = FxType.Taunt, Turns = 1, To = FxTo.Self } } }, new SkillDef { Name = "Radiant Antlers", Target = Target.AllEnemies, Mult = 0.0323, Cooldown = 5, Scale = Scale.Hp, Fx = new[] { new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.15 }, new FxDef { Type = FxType.DefUp, Turns = 2, To = FxTo.Allies } } } } },
            new HeroDef { Id = "nyssa", Name = "Nyssa Duskwing", Rarity = Rarity.Legendary, Element = Element.Shadow, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Dusk Slash", Target = Target.Enemy, Mult = 0.862, Cooldown = 0, Hits = 2, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2, Chance = 0.35 } } }, new SkillDef { Name = "Eyes in the Dark", Target = Target.AllEnemies, Mult = 0.6896, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.AtkDown, Turns = 2, Chance = 0.5 }, new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Deathmoth Strike", Target = Target.Enemy, Mult = 2.586, Cooldown = 4, Pierce = true, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2 }, new FxDef { Type = FxType.SpdUp, Turns = 2, To = FxTo.Self } } } } },
            // --- end phase H ---
            // Creature heroes (Core/Kit/Creatures.cs; design in docs/sigilbound/concepts/creatures.json).
            new HeroDef { Id = "vessa", Name = "Vessa Silkmourne", Rarity = Rarity.Legendary, Element = Element.Shadow, Role = Role.Attack, Skills = new[] { new SkillDef { Name = "Silk Snare", Target = Target.Enemy, Mult = 1.0, Cooldown = 0, Fx = new[] { new FxDef { Type = FxType.Slow, Turns = 2, Chance = 0.4 } } }, new SkillDef { Name = "Widow's Venom", Target = Target.AllEnemies, Mult = 0.7, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2, Chance = 0.5 }, new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.15 } } }, new SkillDef { Name = "Silkfall Pounce", Target = Target.Enemy, Mult = 3.0, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2 }, new FxDef { Type = FxType.Stun, Turns = 1, Chance = 0.35 } } } } },
        };
        // The original 24 heroes, as in the web game. Campaign, Hard, dungeon and rival enemies are
        // drawn from these with seeded picks, so stages stay exactly as they were (and match the web
        // fixtures) as the roster grows. New heroes join the enemy pools only deliberately (Act II: Act2EnemyIds).
        public static readonly HeroDef[] Classic = Heroes.Take(24).ToArray();
        // Act II campaign enemies (PLAN step 111). A frozen list: the roster as it was when Act II was
        // tuned (step 99), so adding heroes never reshuffles existing stages. Waves pick from it by index,
        // so never reorder or remove entries; a new hero joins Act II only by appending it here on purpose
        // (which re-rolls Act II and needs a campaign re-tune). Unity only: the web game has no Act II.
        public static readonly string[] Act2EnemyIds =
        {
            "brannoc", "mira", "pell", "odo", "wren", "kettle", "ysolde", "corvan", "hask", "lumen", "nyx", "tamsin",
            "gorrum", "velka", "aurelia", "sable", "kaito", "maelis", "thrain", "oriel", "vaelith", "morwenna", "seraphine", "ulfgar",
            "bram", "nettle", "hobb", "greta", "cole", "tilly", "ronan", "isla", "dunstan", "marisol", "vesper", "brega",
            "fennick", "orla", "silas", "wynn", "kestrel", "sorrel", "amber", "tobias", "nerys", "hale", "juno", "pike",
            "rhea", "corwin", "astrid", "caspian", "ignis", "lyra", "malachar", "rowan", "selene", "brann", "ysra", "oswin",
            "grimsby", "marrow", "ember", "quill", "drift", "sprig", "tallow", "hollis", "auren", "cinder", "thessaly", "umbra",
            "lucan", "kharra", "nocturne", "aelira", "zephyrine", "solenne", "nell", "briar", "perrin", "loam", "merrit", "sienna",
            "calder", "dorian", "ivo", "saoirse", "gideon", "liora", "bastian", "elowen", "faelan", "wick", "azura", "pyrrhus",
            "kaelith", "aurex", "stormbearer", "vex", "dace", "dulcie", "holt", "rhosyn", "ansel", "saskia", "kasimir", "imogen",
            "ilsabet", "obrecht", "tansy", "barley", "clover", "edric", "corran", "linnet", "ysabeau", "wendel", "hilde", "aveline",
            "jory", "morag", "wenna", "lazlo", "mungo", "neve", "hendrick", "kaija", "morvoren", "leofric", "hettie", "abel",
            "poppy", "benedict", "emeric", "agathe", "ottavia", "fabian", "augustin", "clemence", "rook", "pernille", "perpetua", "jasper",
            "mabon", "tindra", "ambrose", "lune", "aldric", "ondessa",
        };
        // Classic heroes whose skill numbers (multipliers, heal and shield sizes) are tuned for Unity's combat
        // rules and so differ from the web game's data.js (PLAN step 109: the 40-60% balance band now covers
        // the original 24 too). Names, targets, cooldowns and effects still match the web.
        public static readonly string[] UnityTuned = { "brannoc", "wren", "kettle", "ysolde", "corvan", "nyx", "tamsin", "sable", "oriel" };
        static readonly Dictionary<string, HeroDef> _byId = Heroes.ToDictionary(h => h.Id);
        public static HeroDef Hero(string id) => _byId.TryGetValue(id, out var h) ? h : null;
        public static readonly HeroDef[] Act2Pool = Act2EnemyIds.Select(id => _byId[id]).ToArray();
        public static readonly string[] Starters = { "ysolde", "hask", "corvan" };

        public static readonly HeroDef Titan = H("titan", "Kharvok the Ashen Titan", Rarity.Legendary, Element.Flame, Role.Defense,
            S("Crushing Palm", E, 1.3, 0), S("Quake", AE, 0.8, 3, F(FxType.Slow, 2, 0.5)), S("Cinder Breath", AE, 1.0, 4, F(FxType.Poison, 2, 0.5), F(FxType.DefDown, 2, 0.4)));
        public const int TitanTurns = 20;
        public sealed class TitanTier { public long Dmg; public Reward Reward; }
        public static readonly TitanTier[] TitanTiers =
        {
            new TitanTier { Dmg = 0, Reward = new Reward { Gold = 500, Common = 1 } },
            // Unity-only thresholds (PLAN step 99): the web game's 20k/60k/150k/400k/1M left the top two tiers out of
            // reach (a maxed team's p90 is about 200k). Rewards are the web game's.
            new TitanTier { Dmg = 15000, Reward = new Reward { Gold = 1500, Common = 2, Mystic = 1 } },
            new TitanTier { Dmg = 40000, Reward = new Reward { Gold = 3000, Common = 3, Mystic = 1 } },
            new TitanTier { Dmg = 80000, Reward = new Reward { Gold = 6000, Common = 3, Mystic = 2 } },
            new TitanTier { Dmg = 140000, Reward = new Reward { Gold = 10000, Common = 4, Mystic = 3, Radiant = 1 } },
            new TitanTier { Dmg = 220000, Reward = new Reward { Gold = 20000, Common = 5, Mystic = 4, Radiant = 1 } },
        };
        public static Element TitanElement(DateTime date) => Elements[(int)date.DayOfWeek % Elements.Length];

        // ---- gear ----
        public enum Slot { Weapon, Helm, Chest, Boots }
        public enum Stat { Atk, Hp, Def, Spd, AtkPct, HpPct, DefPct, SpdPct, Crit, Cdmg }
        public static Stat SlotMainStat(Slot s) => s switch { Slot.Weapon => Stat.Atk, Slot.Helm => Stat.Hp, Slot.Chest => Stat.Def, _ => Stat.Spd };
        public static int SlotBase(Slot s) => s switch { Slot.Weapon => 20, Slot.Helm => 200, Slot.Chest => 18, _ => 4 };
        public static readonly string[] GearRarityNames = { "", "Worn", "Fine", "Superior", "Heroic", "Mythic" };
        public enum GearSet { Fury, Vigor, Bulwark, Haste, Keen }
        public static (Stat stat, int val, string desc) SetBonus(GearSet s) => s switch
        {
            GearSet.Fury => (Stat.AtkPct, 15, "+15% ATK"), GearSet.Vigor => (Stat.HpPct, 15, "+15% HP"),
            GearSet.Bulwark => (Stat.DefPct, 15, "+15% DEF"), GearSet.Haste => (Stat.SpdPct, 12, "+12% SPD"),
            _ => (Stat.Crit, 12, "+12% Crit Rate"),
        };
        public static readonly Stat[] SubStats = { Stat.AtkPct, Stat.HpPct, Stat.DefPct, Stat.Spd, Stat.Crit, Stat.Cdmg };
        public static (int min, int max) SubRoll(Stat s) => s switch { Stat.Spd => (2, 5), Stat.Crit => (3, 6), Stat.Cdmg => (4, 8), _ => (4, 7) };
        public const int GearMaxLevel = 10;

        // ---- summoning ----
        public enum Shard { Common, Mystic, Radiant }
        // One name per shard everywhere in the client (UX review #14: "Worn Shard", "1 worn" and "Common").
        public static string ShardName(Shard s, int count = 1) => (s == Shard.Common ? "Worn" : s == Shard.Mystic ? "Mystic" : "Radiant") + (count == 1 ? " Shard" : " Shards");
        public static double[] ShardRates(Shard s) => s switch // common, rare, epic, legendary
        {
            Shard.Common => new[] { 70, 27, 3, 0.0 },
            Shard.Mystic => new[] { 0, 82.5, 15, 2.5 },
            _ => new[] { 0, 0, 85, 15.0 },
        };
        public static int ShardPity(Shard s) => s == Shard.Mystic ? 60 : s == Shard.Radiant ? 10 : 0;
        public static int ExchangeCost(Shard s) => s switch { Shard.Common => 15, Shard.Mystic => 120, _ => 1500 };

        // ---- campaign ----
        public static readonly string[] Chapters =
        {
            "Thornwick Road", "The Drowned Mill", "Ashgrave Pass", "Saltmarsh Reaches", "Bellspire Ruins",
            "The Glass Wastes", "Stormcrown Heights", "Hollow Deep", "Sunken Cathedral", "The Last Pyre",
            // Act 2 (PLAN step 38; Unity only): the port city and the storm coast, where the Sigil Bearers rise.
            "Saltreach Harbor", "The Black Fens", "Wraithmoor Barrows", "The Ember Groves", "The Storm Coast",
        };
        public const int StagesPerChapter = 6;
        public static int TotalStages => Chapters.Length * StagesPerChapter;
        // Act 1 is the web game's campaign (10 chapters, 60 stages): its difficulty curve, enemy pools and
        // dungeon floors stay pinned to these 60 stages. Act 2 (chapters 11-15) is Unity only.
        public const int ClassicChapters = 10;
        public static int ClassicStages => ClassicChapters * StagesPerChapter;
        public static int ActOf(int chapter) => chapter < ClassicChapters ? 1 : 2;
        // Scenery/colour-grade theme per chapter: act 2 picks act 1 themes that suit the coast and fens.
        // Act 2 chapters have their own themes 10-14 (harbor, fens, barrows, ember groves, storm coast).
        // The Glass Wastes (chapter 6) and Stormcrown Heights (chapter 7) have their own themes, 15 and 16: a pale
        // glass desert and a cold mountain crag (story scenes, PLAN step 85); every other chapter keeps its theme.
        public const int GlassTheme = 15, CragTheme = 16;
        public static int ThemeOf(int chapter)
        {
            int t = chapter < ClassicChapters ? ((chapter % 10) + 10) % 10 : ClassicChapters + (chapter - ClassicChapters) % 5;
            return t == 5 ? GlassTheme : t == 6 ? CragTheme : t;
        }

        // Campaign story (Unity only): the Act II introduction shown once when it opens, and a line per
        // chapter (both acts) at the top of its stage list.
        public const string Act2Title = "Act II · The Sigil Bearers";
        public const string Act2Intro =
            "The Last Pyre is quiet, but the Sigil did not mend. Its five shards fled west on the storm, " +
            "to the harbour city of Saltreach, and each has chosen a bearer. The Hollow follow them, and so do you. " +
            "Find the bearers before the Hollow King does.";
        public static readonly string[] ChapterStory =
        {
            // act 1 (owner-approved drafts, STORY_SCENES.md)
            "The Sigil cracked in the night, and the Hollow came down Thornwick Road with the dark. Follow the wagon tracks: whatever overturned them is still close.",
            "The river rose the night the Sigil broke. The old mill went under, yet its wheel still turns, and the Hollow gather where it groans.",
            "Ash falls like snow over the pass. The mourners fled and left their dead unburied, but the graves are not quiet.",
            "The marsh folk have pulled up their ladders. From their stilt huts they watch lights moving in the reeds, lights that aren't theirs.",
            "The Bellspire has not rung in a hundred years. Tonight it tolls by itself, and every toll calls more Hollow to the ruins.",
            "The first Sigil war burned these sands to glass. The spires sing in the wind, and something Hollow sings back.",
            "Lightning never leaves the Stormcrown. Climb to the crown of rock, where a splinter of the old storm still answers the Hollow.",
            "The Hollow did not come from nowhere. Under the badlands a cavern breathes violet light. Go down, and find where they are made.",
            "The cathedral sank with its choir still singing. Its rose window glows under the water, and the drowned congregation is waking.",
            "At the Last Pyre the Hollow mean to burn what's left of the Sigil. Reach the fire before it takes the shards, or the night never ends.",
            "Saltreach's lamps still burn, but the docks are full of Hollow. A street urchin swears the Flame shard sleeps in the old lighthouse.",
            "Beyond the city walls the fens are black and still. Something drowned here has learned to walk, and it carries the Shadow shard's chill.",
            "The barrows of Wraithmoor hum at night. The dead kings beneath them are restless, and the Tide shard sings under the stones.",
            "The ember groves never stop burning. A Light shard fell among the trees, and the fire answered it.",
            "On the storm coast the last bearer waits, and so does the Hollow King. The Gale shard screams in the lightning. End it here.",
        };
        public static string StoryOf(int chapter) => chapter >= 0 && chapter < ChapterStory.Length ? ChapterStory[chapter] : null;
        // Enemy stat multiplier curve, tuned with the progression sim (see tools/sigilbound-balance-sim.cjs).
        public static class Curve
        {
            public const double Start = 1.0, End = 1.75, Shape = 1.3, SpdPerStage = 0.3, BossStat = 1.1, BossHp = 2.2;
            // Act 2: level-50, 5-star enemies whose multiplier keeps rising from End, by this much per stage
            // (0.02 until PLAN step 99: it ended in a 2% wall at 15-6 even for a maxed team).
            public const double Act2Ramp = 0.015;
            // Act 1 chapter factors (web parity: the web game's ENEMY_CURVE.chapter).
            public static readonly double[] Chapter = { 1, 1.14, 0.97, 1, 1.08, 0.94, 1, 0.98, 0.95, 1.04 };
            // ---- Unity-only campaign easing on top of the web curve (Game.CampaignEase; PLAN step 99) ----
            // Light and Shadow are neutral to Flame/Gale/Tide, so a triangle-only team never has an edge in a
            // Light or Shadow chapter: its enemies are made this much weaker.
            public const double LSEase = 0.92;
            // Chapter factors that replace the web ones: chapter 10 (The Last Pyre) jumped 1.04 after 0.95, so
            // 10-1 was a 5% wall for a mid team between two 90% stages.
            public static readonly System.Collections.Generic.IReadOnlyDictionary<int, double> ChapterFix = new System.Collections.Generic.Dictionary<int, double> { [9] = 0.98 };
            // Per-stage enemy multiplier fixes (stage index -> factor), both acts. 14-4 (index 81) spiked after the
            // combat rules of PLAN step 98; 15-6 (index 89), the finale, is eased so a strong team can finish it.
            // 13-6 (index 77) and 15-6 eased again with the speed trim and the classic-hero retune (PLAN step 109),
            // which cost the strong suite lineup (Oriel's Ascend) most on these two boss stages.
            public static readonly System.Collections.Generic.IReadOnlyDictionary<int, double> StageEase = new System.Collections.Generic.Dictionary<int, double> { [77] = 0.92, [81] = 0.926, [89] = 0.85 };
            // Act 2 gold and XP per clear: the stage level caps at 60 at stage index 61, so past that rewards grow
            // by this share per stage instead (in step with the enemy ramp).
            public const double Act2RewardPerStage = 0.02; public const int RewardCapIdx = 61;
        }

        // ---- progression ----
        public static int XpToNext(int level) => 50 + level * level * 12;
        public static int MaxLevelFor(int stars) => stars * 10;
        public const int MaxStars = 6;
        public static int RankUpGold(int stars) => 2000 * stars;
        public const int TrainXpPerGold = 2;
        public static readonly Reward[] LoginRewards =
        {
            new Reward { Gold = 2000 }, new Reward { Common = 3 }, new Reward { Gold = 4000 }, new Reward { Mystic = 1 },
            new Reward { Gold = 6000, Common = 3 }, new Reward { Mystic = 2 }, new Reward { Radiant = 1 },
        };
        public sealed class QuestDef { public string Id, Name; public int Goal; public Reward Reward; }
        public static readonly QuestDef[] DailyQuests =
        {
            new QuestDef { Id = "win", Name = "Win 5 battles", Goal = 5, Reward = new Reward { Gold = 2000 } },
            new QuestDef { Id = "summon", Name = "Summon 3 heroes", Goal = 3, Reward = new Reward { Common = 2 } },
            new QuestDef { Id = "upgrade", Name = "Upgrade gear 3 times", Goal = 3, Reward = new Reward { Gold = 1500 } },
            new QuestDef { Id = "titan", Name = "Challenge the Titan", Goal = 1, Reward = new Reward { Mystic = 1 } },
            new QuestDef { Id = "all", Name = "Finish all daily quests", Goal = 4, Reward = new Reward { Mystic = 1, Gold = 3000 } },
        };
    }

    public sealed class Reward
    {
        public int Gold, Essence, Common, Mystic, Radiant, Xp, Tome;
        public override string ToString()
        {
            var parts = new List<string>();
            if (Gold > 0) parts.Add($"{Gold:N0} gold");
            if (Essence > 0) parts.Add($"{Essence} essence");
            if (Common > 0) parts.Add($"{Common} worn shard{(Common > 1 ? "s" : "")}");
            if (Mystic > 0) parts.Add($"{Mystic} mystic shard{(Mystic > 1 ? "s" : "")}");
            if (Radiant > 0) parts.Add($"{Radiant} radiant shard{(Radiant > 1 ? "s" : "")}");
            if (Tome > 0) parts.Add($"{Tome} skill tome{(Tome > 1 ? "s" : "")}");
            return string.Join(", ", parts);
        }
    }
}
