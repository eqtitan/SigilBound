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
    public enum FxType { AtkUp, DefUp, SpdUp, Regen, Shield, Taunt, AtkDown, DefDown, Slow, Poison, Stun, Heal, Tm, TmDrain, Cleanse, Strip, Burn, Pull, Counter, Revive }
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
            H("brannoc", "Brannoc Spearhand", Rarity.Common, Element.Gale, Role.Attack, S("Spear Thrust", E, 1.0, 0), S("Whirling Haft", AE, 0.7, 3)),
            H("mira", "Mira Saltwhistle", Rarity.Common, Element.Tide, Role.Defense, S("Shell Bash", E, 0.9, 0, 1, Scale.Def), S("Saltwall", AA, 0, 4, F(FxType.DefUp, 2))),
            H("pell", "Pell Cinderbrook", Rarity.Common, Element.Flame, Role.Support, S("Ember Flick", E, 1.0, 0), S("Kindle", AA, 0, 4, Heal(0.15))),
            H("odo", "Odo Mudlark", Rarity.Common, Element.Gale, Role.Hp, S("Mud Slap", E, 0.08, 0, 1, Scale.Hp, F(FxType.Slow, 2, 0.3)), S("Thick Hide", SELF, 0, 4, Shield(0.25), F(FxType.Taunt, 2))),
            H("wren", "Sister Wren", Rarity.Common, Element.Light, Role.Support, S("Lantern Strike", E, 1.0, 0), S("Vesper Hymn", AA, 0, 4, Heal(0.18))),
            H("kettle", "Kettle Gravejaw", Rarity.Common, Element.Shadow, Role.Attack, S("Grave Hook", E, 1.0, 0, F(FxType.Poison, 2, 0.3)), S("Rattle Bones", E, 1.6, 3)),
            // Rare
            H("ysolde", "Ysolde Emberlash", Rarity.Rare, Element.Flame, Role.Attack, S("Lash", E, 0.55, 0, 2, Scale.Atk), S("Searing Coil", E, 1.9, 3, F(FxType.DefDown, 2, 0.6)), S("Wildfire", AE, 0.9, 4, F(FxType.Poison, 2, 0.4))),
            H("corvan", "Corvan Tidecaller", Rarity.Rare, Element.Tide, Role.Support, S("Undertow", E, 1.0, 0, Drain(0.15, 0.5)), S("Rising Tide", AA, 0, 3, Heal(0.2)), S("Tidal Hymn", AA, 0, 4, F(FxType.AtkUp, 2), TmUp(0.15))),
            H("hask", "Hask the Unbowed", Rarity.Rare, Element.Gale, Role.Defense, S("Shield Rush", E, 0.9, 0, 1, Scale.Def), S("Unbowed", SELF, 0, 4, F(FxType.Taunt, 2), F(FxType.DefUp, 2)), S("Gale Wall", AA, 0, 4, Shield(0.15))),
            H("lumen", "Lumen Varro", Rarity.Rare, Element.Light, Role.Attack, S("Radiant Edge", E, 1.0, 0), S("Judgment", E, 2.2, 3), S("Dawnbreak", AE, 1.0, 4, F(FxType.AtkDown, 2, 0.5))),
            H("nyx", "Nyx Hollowstep", Rarity.Rare, Element.Shadow, Role.Attack, S("Hollow Cut", E, 0.38, 0, 3, Scale.Atk), S("Vanish", E, 1.8, 3, Strip()), S("Night Toll", AE, 0.85, 4, Drain(0.2, 0.5))),
            H("tamsin", "Tamsin Reedvane", Rarity.Rare, Element.Gale, Role.Support, S("Reed Dart", E, 1.0, 0, F(FxType.Slow, 2, 0.4)), S("Tailwind", AA, 0, 4, F(FxType.SpdUp, 2)), S("Field Remedy", AA, 0, 4, Heal(0.15), Cleanse())),
            H("gorrum", "Gorrum Slagheart", Rarity.Rare, Element.Flame, Role.Defense, S("Slag Fist", E, 0.9, 0, 1, Scale.Def, F(FxType.DefDown, 2, 0.25)), S("Molten Hide", SELF, 0, 4, Shield(0.3), F(FxType.Taunt, 2)), S("Eruption", AE, 0.7, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.2))),
            H("velka", "Velka Driftmourn", Rarity.Rare, Element.Tide, Role.Hp, S("Drift Strike", E, 0.08, 0, 1, Scale.Hp), S("Mournful Wave", AE, 0.06, 4, 1, Scale.Hp, F(FxType.Slow, 2, 0.4)), S("Deep Mending", AA, 0, 4, F(FxType.Regen, 2))),
            // Epic
            H("aurelia", "Aurelia Dawnwarden", Rarity.Epic, Element.Light, Role.Defense, S("Warden's Blow", E, 0.95, 0, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3)), S("Aegis of Dawn", AA, 0, 4, Shield(0.2), F(FxType.DefUp, 2)), S("Sunfall", AE, 0.8, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.25))),
            H("sable", "Sable Nightglass", Rarity.Epic, Element.Shadow, Role.Support, S("Glass Shard", E, 1.0, 0, F(FxType.DefDown, 2, 0.4)), S("Mirror Veil", AE, 0.6, 4, F(FxType.DefDown, 2, 0.75), F(FxType.AtkDown, 2, 0.75)), S("Night Grace", AA, 0, 5, Heal(0.25), TmUp(0.2))),
            H("kaito", "Kaito Stormrend", Rarity.Epic, Element.Gale, Role.Attack, S("Rending Gust", E, 0.6, 0, 2, Scale.Atk), S("Thousand Cuts", E, 0.55, 3, 4, Scale.Atk), S("Stormrend", AE, 1.3, 4, F(FxType.Slow, 2, 0.6))),
            H("maelis", "Maelis Deepcurrent", Rarity.Epic, Element.Tide, Role.Attack, S("Trident", E, 1.1, 0), S("Riptide", E, 2.4, 3, Drain(0.3)), S("Maelstrom", AE, 1.2, 4, F(FxType.DefDown, 2, 0.5))),
            H("thrain", "Thrain Ashforge", Rarity.Epic, Element.Flame, Role.Hp, S("Forge Hammer", E, 0.085, 0, 1, Scale.Hp, F(FxType.Stun, 1, 0.15)), S("Ashen Ward", AA, 0, 4, Shield(0.15), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Bellows", AA, 0, 5, F(FxType.Regen, 2), F(FxType.AtkUp, 2))),
            H("oriel", "Oriel Sunveil", Rarity.Epic, Element.Light, Role.Support, S("Veil Light", E, 1.0, 0), S("Sunveil", AA, 0, 4, Heal(0.3), Cleanse()), S("Ascend", AA, 0, 5, F(FxType.AtkUp, 2), F(FxType.SpdUp, 2), TmUp(0.25))),
            // Legendary
            H("vaelith", "Vaelith, the Last Pyre", Rarity.Legendary, Element.Flame, Role.Attack, S("Pyre Brand", E, 1.2, 0, F(FxType.Poison, 2, 0.5)), S("Cinder Rain", AE, 0.7, 3, 2, Scale.Atk, F(FxType.DefDown, 2, 0.5)), S("Last Pyre", E, 3.2, 4, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            H("morwenna", "Morwenna Abyss-Crowned", Rarity.Legendary, Element.Shadow, Role.Attack, S("Abyssal Touch", E, 1.1, 0, Drain(0.15)), S("Crown of Night", AE, 1.1, 4, F(FxType.Stun, 1, 0.35)), S("Drown in Dark", E, 2.8, 4, Strip(), F(FxType.AtkDown, 2))),
            H("seraphine", "Seraphine of the Seventh Bell", Rarity.Legendary, Element.Light, Role.Support, S("Bell Toll", E, 1.0, 0, F(FxType.AtkDown, 2, 0.4)), S("Seventh Bell", AA, 0, 4, Heal(0.35), Shield(0.15)), S("Requiem", AA, 0, 5, TmUp(0.35), F(FxType.AtkUp, 2), F(FxType.DefUp, 2), Cleanse())),
            H("ulfgar", "Ulfgar Stormwall", Rarity.Legendary, Element.Gale, Role.Defense, S("Stormwall Bash", E, 1.0, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.5)), S("Hold the Line", AA, 0, 4, F(FxType.DefUp, 3), F(FxType.Taunt, 2, -1, FxTo.Self), Shield(0.25, FxTo.Self)), S("Avalanche", AE, 0.9, 5, 1, Scale.Def, F(FxType.Stun, 1, 0.3), F(FxType.DefDown, 2, 0.5))),

            // ---- Roster phase 1, Roadwardens (docs/sigilbound/ROSTER.md). Unity only: not in the
            // frozen web game's data.js, so they come after the original 24 (see Classic below).
            // Step 21:
            H("bram", "Bram Wetherby", Rarity.Common, Element.Tide, Role.Hp, S("Oar Swing", E, 0.08, 0, 1, Scale.Hp), S("Weather the Storm", SELF, 0, 4, Shield(0.2), F(FxType.Regen, 2), F(FxType.Taunt, 2))),
            H("nettle", "Nettle Quickfinch", Rarity.Common, Element.Gale, Role.Attack, S("Twin Sting", E, 0.48, 0, 2, Scale.Atk, F(FxType.Slow, 2, 0.2)), S("Hedge Dash", E, 1.4, 3)),
            H("hobb", "Hobb Lanternjaw", Rarity.Common, Element.Light, Role.Defense, S("Lamp Bash", E, 0.9, 0, 1, Scale.Def), S("Waystone Ward", AA, 0, 4, Shield(0.04), F(FxType.DefUp, 2))),
            H("greta", "Greta Brineknot", Rarity.Common, Element.Tide, Role.Support, S("Net Toss", E, 1.0, 0, F(FxType.Slow, 2, 0.4)), S("Mend the Nets", AA, 0, 4, Heal(0.12), Cleanse())),
            // Step 22:
            H("cole", "Cole Ashworth", Rarity.Common, Element.Flame, Role.Attack, S("Cleave", E, 0.85, 0), S("Ash Split", E, 1.1, 3, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            H("tilly", "Tilly Mothwing", Rarity.Common, Element.Shadow, Role.Support, S("Moth Swarm", E, 1.0, 0, F(FxType.DefDown, 2, 0.3)), S("Dusk Wings", AE, 0.3, 4, F(FxType.DefDown, 2, 0.75), Drain(0.15, 0.5))),
            H("ronan", "Ronan Stoutbarrow", Rarity.Common, Element.Gale, Role.Hp, S("Tankard Swing", E, 0.08, 0, 1, Scale.Hp), S("Hearth Feast", AA, 0, 4, F(FxType.Regen, 2), F(FxType.Taunt, 2, -1, FxTo.Self), Shield(0.2, FxTo.Self))),
            H("isla", "Isla Fenlight", Rarity.Common, Element.Light, Role.Support, S("Chime", E, 1.0, 0), S("Fenlight Peal", AA, 0, 4, Heal(0.08), F(FxType.SpdUp, 2))),
            // Step 23 (the first new Rares):
            H("dunstan", "Dunstan Oaken", Rarity.Rare, Element.Gale, Role.Defense, S("Shield Bash", E, 0.9, 0, 1, Scale.Def), S("Muster", AA, 0, 4, F(FxType.DefUp, 2), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Old Guard", AA, 0, 5, Shield(0.18))),
            H("marisol", "Marisol Wavecrest", Rarity.Rare, Element.Tide, Role.Attack, S("Pearl Strike", E, 1.0, 0), S("Riptide Dive", E, 1.9, 3, F(FxType.Slow, 2, 0.5)), S("Wavebreak", AE, 0.85, 4, F(FxType.Slow, 2, 0.4))),
            H("vesper", "Vesper Gloam", Rarity.Rare, Element.Shadow, Role.Attack, S("Gloom Cut", E, 0.6, 0, 2, Scale.Atk), S("Poisoned Edge", E, 2.1, 3, F(FxType.Poison, 2, 0.7)), S("Nightfall Feint", AE, 0.85, 4, F(FxType.DefDown, 2, 0.5))),
            H("brega", "Brega Ironmoss", Rarity.Rare, Element.Tide, Role.Defense, S("Barnacle Bash", E, 0.9, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.25)), S("Lockkeeper", SELF, 0, 4, Shield(0.2), F(FxType.Taunt, 2)), S("Floodgate", AE, 0.6, 5, 1, Scale.Def, F(FxType.Slow, 2, 0.5))),
            // Step 24:
            H("fennick", "Fennick Sootpaw", Rarity.Rare, Element.Flame, Role.Support, S("Spark Toss", E, 1.0, 0, F(FxType.Poison, 2, 0.3)), S("Firework Volley", AE, 0.6, 4, F(FxType.Poison, 2, 0.6)), S("Bright Burst", AA, 0, 4, F(FxType.AtkUp, 2), Heal(0.22))),
            H("orla", "Orla Greywater", Rarity.Rare, Element.Tide, Role.Hp, S("Weed Lash", E, 0.08, 0, 1, Scale.Hp), S("River Brew", AA, 0, 4, Heal(0.17)), S("Marsh Tonic", AA, 0, 5, F(FxType.Regen, 2))),
            H("silas", "Silas Penumbra", Rarity.Rare, Element.Shadow, Role.Defense, S("Gate Strike", E, 0.9, 0, 1, Scale.Def, F(FxType.DefDown, 2, 0.3)), S("Penumbra Ward", SELF, 0, 4, Shield(0.32), F(FxType.Taunt, 2)), S("Black Gate", AE, 0.7, 5, 1, Scale.Def, F(FxType.DefDown, 2, 0.4))),
            H("wynn", "Wynn Brightfeather", Rarity.Rare, Element.Light, Role.Attack, S("Falcon Shot", E, 1.0, 0), S("Stooping Dive", E, 2.1, 3, Strip()), S("Feather Storm", AE, 0.8, 4, F(FxType.AtkDown, 2, 0.3))),
            // ---- Roster phase 2, Glass and Storm. Step 26:
            H("kestrel", "Kestrel Vane", Rarity.Rare, Element.Gale, Role.Attack, new SkillDef { Name = "Lance Thrust", Target = E, Mult = 0.9, Pierce = true }, new SkillDef { Name = "Skyfall Lance", Target = E, Mult = 1.8, Cooldown = 3, Pierce = true }, S("Gale Charge", AE, 0.8, 4, F(FxType.Slow, 2, 0.3))),
            H("sorrel", "Sorrel Duskmere", Rarity.Rare, Element.Shadow, Role.Hp, S("Pick Strike", E, 0.08, 0, 1, Scale.Hp, F(FxType.Poison, 2, 0.3)), S("Deep Breath", AA, 0, 4, F(FxType.Regen, 2), Shield(0.15, FxTo.Self)), S("Cave-in", AE, 0.06, 4, 1, Scale.Hp, F(FxType.Poison, 2, 0.5))),
            H("amber", "Amber Glasswright", Rarity.Rare, Element.Flame, Role.Defense, S("Glass Bash", E, 0.9, 0, 1, Scale.Def, F(FxType.Burn, 2, 0.3)), S("Kiln Shield", AA, 0, 4, Shield(0.15)), S("Molten Glass", SELF, 0, 5, Shield(0.2), F(FxType.Taunt, 2), F(FxType.Counter, 2))),
            H("tobias", "Tobias Reed", Rarity.Rare, Element.Gale, Role.Support, S("Discord", E, 1.0, 0, F(FxType.AtkDown, 2, 0.3)), S("Marching Song", AA, 0, 4, F(FxType.SpdUp, 2), TmUp(0.2)), S("Hearthsong", AA, 0, 4, Heal(0.2), Cleanse())),
            // Step 27:
            H("nerys", "Nerys Coldspring", Rarity.Rare, Element.Tide, Role.Support, S("Tide Mirror", E, 1.0, 0, F(FxType.Slow, 2, 0.3)), S("Still Waters", AA, 0, 4, Cleanse(), F(FxType.DefUp, 2), TmUp(0.2)), S("Spring Rite", AA, 0, 3, Heal(0.22))),
            H("hale", "Hale Sunforge", Rarity.Rare, Element.Light, Role.Hp, S("Forge Strike", E, 0.08, 0, 1, Scale.Hp), S("Anvil Heart", SELF, 0, 4, Shield(0.05), F(FxType.Taunt, 2)), S("Lamplight", AA, 0, 5, F(FxType.Regen, 2))),
            H("juno", "Juno Ashveil", Rarity.Rare, Element.Flame, Role.Attack, S("Fire Whirl", E, 1.0, 0, F(FxType.Burn, 2, 0.4)), new SkillDef { Name = "Ember Lash", Target = E, Mult = 1.9, Cooldown = 3, Spread = true, Fx = new[] { F(FxType.Burn, 2, 0.6) } }, S("Pyre Dance", AE, 0.8, 4, F(FxType.Burn, 2, 0.4))),
            H("pike", "Pike Saltgrave", Rarity.Rare, Element.Tide, Role.Attack, S("Hook Swing", E, 1.05, 0, new FxDef { Type = FxType.Pull, Val = 0.15, Chance = 0.5 }), S("Reel In", E, 2.0, 3, new FxDef { Type = FxType.Pull, Val = 0.3 }), S("Smuggler's Volley", AE, 0.9, 4, F(FxType.DefDown, 2, 0.3))),
            // Step 28 (the first new Epics: Astrid, Caspian):
            H("rhea", "Rhea Lumen-Kin", Rarity.Rare, Element.Light, Role.Defense, S("Lumen Bash", E, 0.9, 0, 1, Scale.Def, F(FxType.AtkDown, 2, 0.3)), S("Sister's Ward", SELF, 0, 4, Shield(0.32), F(FxType.Taunt, 2), Cleanse()), S("Dawnguard", AA, 0, 5, F(FxType.DefUp, 2), Shield(0.1))),
            H("corwin", "Corwin Blackthorn", Rarity.Rare, Element.Shadow, Role.Attack, S("Duel Cut", E, 1.0, 0), S("Shadow Lunge", E, 1.8, 3, F(FxType.DefDown, 2, 0.5)), new SkillDef { Name = "Final Verdict", Target = E, Mult = 1.6, Cooldown = 4, Execute = 0.25 }),
            H("astrid", "Astrid Windrider", Rarity.Epic, Element.Gale, Role.Attack, new SkillDef { Name = "Sky Lance", Target = E, Mult = 1.1, Pierce = true }, S("Windrider", E, 1.7, 3, F(FxType.SpdUp, 2, -1, FxTo.Self)), S("Stormdive", AE, 1.2, 4, F(FxType.Slow, 2, 0.5))),
            H("caspian", "Caspian Deepwell", Rarity.Epic, Element.Tide, Role.Hp, S("Tide Crown", E, 0.085, 0, 1, Scale.Hp), S("Deep Ward", AA, 0, 4, Shield(0.16), F(FxType.Taunt, 2, -1, FxTo.Self)), S("Drowned King's Grace", AA, 0, 5, F(FxType.Regen, 2), F(FxType.DefUp, 2))),
            // Step 29:
            H("ignis", "Ignis Kael", Rarity.Epic, Element.Flame, Role.Support, S("Pyre Spark", E, 1.0, 0, F(FxType.Burn, 2, 0.4)), S("Eternal Flame", AA, 0, 4, F(FxType.AtkUp, 2), TmUp(0.2), Heal(0.28)), S("Firestorm Rite", AE, 0.6, 4, F(FxType.Burn, 2, 0.7))),
            H("lyra", "Lyra Starglass", Rarity.Epic, Element.Light, Role.Support, S("Glass Beam", E, 1.0, 0, F(FxType.AtkDown, 2, 0.3)), S("Starglass Hymn", AA, 0, 4, Heal(0.25), Cleanse()), S("Oracle's Sight", AA, 0, 5, F(FxType.AtkUp, 2), F(FxType.SpdUp, 2))),
            H("malachar", "Malachar Gloomhand", Rarity.Epic, Element.Shadow, Role.Defense, S("Gloom Bash", E, 0.95, 0, 1, Scale.Def, F(FxType.DefDown, 2, 0.4)), S("Broken Chains", SELF, 0, 4, Shield(0.05), F(FxType.Taunt, 2), F(FxType.Counter, 2)), S("Hollow Quake", AE, 0.8, 5, 1, Scale.Def, F(FxType.DefDown, 2, 0.5))),
            H("rowan", "Rowan Thornheart", Rarity.Epic, Element.Gale, Role.Defense, S("Thorn Bash", E, 0.95, 0, 1, Scale.Def, F(FxType.Slow, 2, 0.3)), S("Old Wood Ward", AA, 0, 5, Shield(0.04)), S("Wardenroot", SELF, 0, 5, F(FxType.Taunt, 2), F(FxType.DefUp, 1))),
            // Step 30 (finishes phase 2):
            H("selene", "Selene Tidewhisper", Rarity.Epic, Element.Tide, Role.Support, S("Moonlit Wave", E, 1.0, 0, Drain(0.15, 0.5)), S("Ebb and Flow", AE, 0.4, 4, Drain(0.25), new FxDef { Type = FxType.Tm, Val = 0.2, To = FxTo.Allies }), S("Moonrise", AA, 0, 4, Heal(0.25), Cleanse())),
            H("brann", "Brann Emberfist", Rarity.Epic, Element.Flame, Role.Hp, S("Iron Fist", E, 0.085, 0, 1, Scale.Hp, F(FxType.Stun, 1, 0.15)), S("Pit Stance", AA, 0, 4, Shield(0.12), F(FxType.Taunt, 2, -1, FxTo.Self), F(FxType.Counter, 2, -1, FxTo.Self)), S("Ember Brawl", AE, 0.06, 4, 1, Scale.Hp, F(FxType.Burn, 2, 0.5), F(FxType.AtkUp, 2, -1, FxTo.Allies))),
            H("ysra", "Ysra Nightbloom", Rarity.Epic, Element.Shadow, Role.Attack, S("Thorn Kiss", E, 1.1, 0, F(FxType.Poison, 2, 0.5)), S("Nightbloom", AE, 1.0, 4, F(FxType.Poison, 2, 0.8)), S("Petal Strike", E, 2.4, 4, F(FxType.Stun, 1, 0.4))),
            H("oswin", "Oswin Dawnspear", Rarity.Epic, Element.Light, Role.Attack, new SkillDef { Name = "Dawnspear", Target = E, Mult = 1.1, Pierce = true }, new SkillDef { Name = "Bellspire Charge", Target = E, Mult = 2.2, Cooldown = 3, Pierce = true, Fx = new[] { F(FxType.AtkUp, 2, -1, FxTo.Self) } }, S("Daybreak", AE, 1.25, 4, F(FxType.AtkDown, 2, 0.4))),
            // ---- Roster phase 3, The Hollow Unbound. Step 32:
            H("grimsby", "Grimsby Boneweaver", Rarity.Rare, Element.Shadow, Role.Support, S("Bone Toss", E, 1.0, 0, F(FxType.DefDown, 2, 0.3)), S("Crack the Sigil", AE, 0.4, 4, F(FxType.DefDown, 2, 0.7), new FxDef { Type = FxType.Strip, Chance = 0.5 }), S("Bone Rite", AA, 0, 4, Heal(0.2))),
            H("marrow", "Marrow Cinderhulk", Rarity.Epic, Element.Flame, Role.Attack, new SkillDef { Name = "Cinder Fist", Target = E, Mult = 1.1, Rage = 0.6 }, new SkillDef { Name = "Wrath of the Deep", Target = E, Mult = 2.2, Cooldown = 3, Rage = 1.0 }, S("Hollow Roar", AE, 1.0, 4, F(FxType.AtkUp, 2, -1, FxTo.Self))),
            new HeroDef { Id = "ember", Name = "Ember Tallow", Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Hp, FallHeal = 0.15, Skills = new[] { S("Wax Brand", E, 0.08, 0, 1, Scale.Hp, F(FxType.Burn, 2, 0.3)), S("Vigil Flame", AA, 0, 4, F(FxType.Regen, 2)), S("Candle Ward", SELF, 0, 5, Shield(0.2)) } },
            H("quill", "Quill Inkwell", Rarity.Rare, Element.Light, Role.Support, S("Quill Jab", E, 1.0, 0, new FxDef { Type = FxType.Strip, Chance = 0.4 }), S("Annotate", AA, 0, 4, F(FxType.AtkUp, 2), Heal(0.15)), S("Redact", E, 1.2, 4, Strip(), F(FxType.AtkDown, 2, 0.5))),
            // Step 33:
            H("drift", "Drift Coldfoot", Rarity.Common, Element.Tide, Role.Attack, S("Skate Cut", E, 0.45, 0, 2, Scale.Atk, F(FxType.Slow, 2, 0.2)), S("Frost Glide", E, 1.3, 3, F(FxType.Slow, 2, 0.5))),
            H("sprig", "Sprig Mosslight", Rarity.Common, Element.Gale, Role.Support, S("Twig Poke", E, 1.0, 0), S("Bloom Song", AA, 0, 4, Heal(0.07), F(FxType.SpdUp, 2))),
            H("tallow", "Tallow Firth", Rarity.Common, Element.Flame, Role.Defense, S("Torch Bash", E, 0.85, 0, 1, Scale.Def, F(FxType.Burn, 2, 0.1)), S("Watch Fire", AA, 0, 4, F(FxType.DefUp, 2))),
            H("hollis", "Hollis Shade", Rarity.Common, Element.Shadow, Role.Hp, S("Grave Thump", E, 0.08, 0, 1, Scale.Hp), S("Gravedigger's Rest", SELF, 0, 4, Shield(0.2), F(FxType.Regen, 2), F(FxType.Taunt, 2))),
        };
        // The original 24 heroes, as in the web game. Campaign, Hard, dungeon and rival enemies are
        // drawn from these with seeded picks, so stages stay exactly as they were (and match the web
        // fixtures) as the roster grows. New heroes join the enemy pools deliberately, later.
        public static readonly HeroDef[] Classic = Heroes.Take(24).ToArray();
        static readonly Dictionary<string, HeroDef> _byId = Heroes.ToDictionary(h => h.Id);
        public static HeroDef Hero(string id) => _byId.TryGetValue(id, out var h) ? h : null;
        public static readonly string[] Starters = { "ysolde", "hask", "corvan" };

        public static readonly HeroDef Titan = H("titan", "Kharvok the Ashen Titan", Rarity.Legendary, Element.Flame, Role.Defense,
            S("Crushing Palm", E, 1.3, 0), S("Quake", AE, 0.8, 3, F(FxType.Slow, 2, 0.5)), S("Cinder Breath", AE, 1.0, 4, F(FxType.Poison, 2, 0.5), F(FxType.DefDown, 2, 0.4)));
        public const int TitanTurns = 20;
        public sealed class TitanTier { public long Dmg; public Reward Reward; }
        public static readonly TitanTier[] TitanTiers =
        {
            new TitanTier { Dmg = 0, Reward = new Reward { Gold = 500, Common = 1 } },
            new TitanTier { Dmg = 20000, Reward = new Reward { Gold = 1500, Common = 2, Mystic = 1 } },
            new TitanTier { Dmg = 60000, Reward = new Reward { Gold = 3000, Common = 3, Mystic = 1 } },
            new TitanTier { Dmg = 150000, Reward = new Reward { Gold = 6000, Common = 3, Mystic = 2 } },
            new TitanTier { Dmg = 400000, Reward = new Reward { Gold = 10000, Common = 4, Mystic = 3, Radiant = 1 } },
            new TitanTier { Dmg = 1000000, Reward = new Reward { Gold = 20000, Common = 5, Mystic = 4, Radiant = 1 } },
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
        };
        public const int StagesPerChapter = 6;
        public static int TotalStages => Chapters.Length * StagesPerChapter;
        // Enemy stat multiplier curve, tuned with the progression sim (see tools/sigilbound-balance-sim.cjs).
        public static class Curve
        {
            public const double Start = 1.0, End = 1.75, Shape = 1.3, SpdPerStage = 0.3, BossStat = 1.1, BossHp = 2.2;
            public static readonly double[] Chapter = { 1, 1.14, 0.97, 1, 1.08, 0.94, 1, 0.98, 0.95, 1.04 };
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
