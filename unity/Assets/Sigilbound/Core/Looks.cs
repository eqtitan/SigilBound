// Per-hero looks for the 3D models: the LOOKS table from public/game/art.js plus each hero's base
// model, height and hidden parts from game3d/three3d.mjs (BASE, BODY), and the material-name ->
// colour-role mapping (ROLES) used to recolour the CC0 Quaternius models. roleColors() is ported
// with three.js colour management (mixes happen in linear space), so colours match the web.
// The table rows were generated from those files; keep them in sync when hero art changes.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sigilbound.Core
{
    public sealed class HeroLook
    {
        public string Skin, Hair, Style, Gear, Armor, Accent, Cloth, Pants, Trim, Boot, Hood, Prop, Beard, Glow;
        public bool Cape; public string CapeColor, Robe, RobeColor;
        public string Base; public double Height = 1; public string[] Hide = new string[0]; public bool Shave, Bald;
    }

    public static class Looks
    {
        public static readonly Dictionary<string, string> Skins = new Dictionary<string, string>
        {
            ["pale"] = "#f3d6c1", ["fair"] = "#e9c09f", ["tan"] = "#c99068", ["brown"] = "#9a6544", ["deep"] = "#6b412b", ["ash"] = "#b9b4c4", ["blue"] = "#8fb3d9",
        };

        // GLB file (StreamingAssets/Models) per base model.
        public static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            ["hooded"] = "hooded_adventurer.glb", ["adventurer"] = "adventurer.glb", ["monarch"] = "monarch.glb", ["titan"] = "yeti.glb",
            // Quaternius Ultimate Modular Women (CC0, phase E step 45): the "hooded" rig and clips.
            ["ubc"] = "ubc_anims.glb", // high-detail rig: skeleton + the retargeted UAL clips (phase F)
            ["hooded_f"] = "women_medieval.glb", ["witch"] = "women_witch.glb", ["adventurer_f"] = "women_adventurer.glb",
        };

        // Female heroes' baked models use a women's body (owner decision, step 45). Same list as
        // quat.py WOMEN (audit.py checks they match). The code-built stand-in keeps Base.
        public static readonly Dictionary<string, string> BakedBases = Women(
            ("hooded_f", "ysolde aurelia rhea thessaly solenne wren vesper nyx ysra saoirse amber mira greta nell"),
            ("witch", "morwenna velka sable seraphine tilly nerys juno lyra selene umbra aelira kaelith orla liora"),
            ("adventurer_f", "tamsin isla marisol astrid zephyrine elowen azura kestrel sienna briar sprig oriel"));

        static Dictionary<string, string> Women(params (string body, string ids)[] lists)
        {
            var d = new Dictionary<string, string>();
            foreach (var (body, ids) in lists) foreach (var id in ids.Split(' ')) d[id] = body;
            return d;
        }

        // The base model a hero's baked GLB was built on (its skeleton, clips and head height).
        public static string BakedBaseOf(string heroId) => BakedBases.TryGetValue(heroId, out var b) ? b : Of(heroId).Base;

        // Material name -> colour role, per base model ("head:Black" = only on the head mesh).
        public static readonly Dictionary<string, Dictionary<string, string>> Roles = new Dictionary<string, Dictionary<string, string>>
        {
            ["hooded"] = new Dictionary<string, string> { ["DarkBrown"] = "armor", ["Black"] = "cloth", ["Brown"] = "cloth", ["LightBrown"] = "accent", ["Gold"] = "accent", ["Metal"] = "trim", ["Metal_Dark"] = "blade", ["Brown2"] = "wood", ["Skin"] = "skin",
                ["head:Black"] = "cloth", ["head:Brown"] = "eye", ["head:DarkBrown"] = "hood", ["White"] = "hair" },
            ["adventurer"] = new Dictionary<string, string> { ["LightGreen"] = "armor", ["Green"] = "cloth", ["Brown"] = "pants", ["Brown2"] = "pants", ["Black"] = "boot", ["Grey"] = "boot", ["Hair"] = "hair", ["Eyebrows"] = "hair", ["Gold"] = "accent", ["Skin"] = "skin", ["Eye"] = "eye" },
            ["monarch"] = new Dictionary<string, string> { ["Metal"] = "armor", ["Metal_Dark"] = "trim", ["Blue"] = "cloth", ["DarkBrown"] = "pants", ["Beige"] = "accent", ["Gold"] = "crown", ["Hair_White"] = "hair", ["Skin"] = "skin", ["Eye"] = "eye" },
            ["hooded_f"] = new Dictionary<string, string> { ["DarkBrown"] = "armor", ["Black"] = "cloth", ["LightBrown"] = "accent", ["Gold"] = "accent", ["Metal"] = "trim", ["Skin"] = "skin",
                ["head:Brown"] = "eye", ["head:DarkBrown"] = "hood", ["head:Black"] = "eye", ["White"] = "hair" },
            ["witch"] = new Dictionary<string, string> { ["Purple"] = "armor", ["Gold"] = "accent", ["Brown2"] = "boot", ["Brown"] = "pants", ["Skin"] = "skin",
                ["head:Purple"] = "hat", ["head:Gold"] = "hat", ["head:Brown"] = "eye", ["Hair_Black"] = "hair" },
            ["adventurer_f"] = new Dictionary<string, string> { ["LightGreen"] = "armor", ["Green"] = "cloth", ["White"] = "accent", ["Brown2"] = "pants", ["Brown_02"] = "boot", ["Gold"] = "accent",
                ["Skin"] = "skin", ["Hair_Brown"] = "hair", ["head:Brown"] = "eye" },
            ["titan"] = new Dictionary<string, string> { ["Yeti_Main"] = "stone", ["Yeti_Secondary"] = "stoneDark", ["Eye_White"] = "socket", ["Eye_Black"] = "maw", ["Yeti_Teeth"] = "fang", ["Tongue"] = "tongue" },
        };
        // Mesh node names per base, to tell the head from the body ("head:" roles).
        public static readonly Dictionary<string, string> HeadMesh = new Dictionary<string, string> { ["monarch"] = "King_Head", ["hooded"] = "Medieval_Head", ["adventurer"] = "Adventurer_Head",
            ["hooded_f"] = "Medieval_Head", ["witch"] = "Witch_Head", ["adventurer_f"] = "Adventurer_Head" };

        public static string RoleOf(string baseModel, bool isHead, string material)
        {
            if (material == null || !Roles.TryGetValue(baseModel, out var map)) return null;
            if (isHead && map.TryGetValue("head:" + material, out var hr)) return hr;
            return map.TryGetValue(material, out var r) ? r : null;
        }

        public static HeroLook Of(string heroId) => Table.TryGetValue(heroId ?? "", out var l) ? l : Table["brannoc"];
        public static readonly string[] Ranged = { "staff", "orb", "bell", "lantern", "candle", "mirror", "bow" };
        public static bool IsCaster(string heroId) => Array.IndexOf(Ranged, Of(heroId).Prop) >= 0;

        // ---------- colour maths (three.js Color with colour management) ----------
        static double ToLinear(double c) => c < 0.04045 ? c * 0.0773993808 : Math.Pow(c * 0.9478672986 + 0.0521327014, 2.4);
        static double ToSrgb(double c) => c < 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 0.41666) - 0.055;
        static double[] Lin(string hex)
        {
            int v = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new[] { ToLinear((v >> 16 & 255) / 255.0), ToLinear((v >> 8 & 255) / 255.0), ToLinear((v & 255) / 255.0) };
        }
        static string Hex(double[] lin)
        {
            int B(double c) => (int)Math.Max(0, Math.Min(255, ToSrgb(c) * 255)); // getHex() truncates (<<)
            return "#" + B(lin[0]).ToString("x2") + B(lin[1]).ToString("x2") + B(lin[2]).ToString("x2");
        }
        public static string Mix(string a, string b, double k) { var x = Lin(a); var y = Lin(b); return Hex(new[] { x[0] + (y[0] - x[0]) * k, x[1] + (y[1] - x[1]) * k, x[2] + (y[2] - x[2]) * k }); }
        public static string Mul(string a, double k) { var x = Lin(a); return Hex(new[] { x[0] * k, x[1] * k, x[2] * k }); }

        // roleColors(L, hollow) from three3d.mjs. Enemies ("Hollow") are tinted toward violet-grey.
        public static Dictionary<string, string> RoleColors(HeroLook L, bool hollow)
        {
            string T(string c) => hollow ? Mix(c, "#2e1f33", 0.5) : c;
            return new Dictionary<string, string>
            {
                ["armor"] = T(L.Armor), ["cloth"] = T(L.Cloth ?? Mul(L.Armor, 0.62)), ["pants"] = T(L.Pants ?? Mix(L.Cloth ?? L.Armor, "#2a2522", L.Cloth != null ? 0.35 : 0.6)),
                ["accent"] = T(L.Accent), ["trim"] = T(L.Trim ?? Mix(L.Accent, "#c8ccd4", 0.4)), ["blade"] = T("#c8d0dc"), ["wood"] = T("#6b4a29"), ["boot"] = T(L.Boot ?? "#2a1f1a"),
                ["hair"] = T(L.Hair), ["brow"] = T(Mul(L.Hair, 0.8)), ["hood"] = T(L.Hood ?? L.Armor), ["skin"] = T(Skins[L.Skin]), ["crown"] = T(L.Accent),
                ["eye"] = hollow ? "#ff2a2a" : L.Glow ?? "#1a141f",
            };
        }
        public static readonly Dictionary<string, string> TitanColors = new Dictionary<string, string>
        {
            ["stone"] = "#16130f", ["stoneDark"] = "#0c0a09", ["fang"] = "#0e0c0a", ["socket"] = "#040303", ["tongue"] = "#0a0606",
        };

        public static readonly Dictionary<string, HeroLook> Table = new Dictionary<string, HeroLook>
        {
            ["brannoc"] = new HeroLook { Skin = "tan", Hair = "#5a3a22", Style = "short", Gear = "bandana", Armor = "#4f7a34", Accent = "#d8b04a", Cloth = "#7a5232", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "spear", Beard = "stubble", Glow = null, Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "adventurer", Height = 1, Hide = new string[0], Shave = false, Bald = false },
            ["mira"] = new HeroLook { Skin = "fair", Hair = "#2f8f8f", Style = "ponytail", Gear = "none", Armor = "#3f7f9a", Accent = "#e8e0c8", Cloth = "#2a5a70", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "shield", Beard = null, Glow = null, Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "hooded", Height = 0.95, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["pell"] = new HeroLook { Skin = "pale", Hair = "#c8642a", Style = "messy", Gear = "none", Armor = "#8a4a2a", Accent = "#ffb347", Cloth = "#6a3420", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "candle", Beard = null, Glow = null, Cape = false, CapeColor = null, Robe = "short", RobeColor = "#8a4a2a", Base = "adventurer", Height = 0.9, Hide = new string[0], Shave = true, Bald = false },
            ["odo"] = new HeroLook { Skin = "brown", Hair = "#1e1610", Style = "bald", Gear = "none", Armor = "#4e3626", Accent = "#9ab84a", Cloth = "#6b7a3a", Pants = "#3a3026", Trim = null, Boot = null, Hood = null, Prop = "club", Beard = "full", Glow = null, Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "monarch", Height = 1.03, Hide = new string[0], Shave = false, Bald = true },
            ["wren"] = new HeroLook { Skin = "fair", Hair = "#e8e2d0", Style = "hood", Gear = "none", Armor = "#7a5a3e", Accent = "#ffc84a", Cloth = "#5c4330", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "lantern", Beard = null, Glow = null, Cape = false, CapeColor = null, Robe = "long", RobeColor = "#6e5038", Base = "hooded", Height = 0.94, Hide = new string[0], Shave = false, Bald = false },
            ["kettle"] = new HeroLook { Skin = "ash", Hair = "#2a2a30", Style = "long", Gear = "none", Armor = "#5a5560", Accent = "#9a5ae8", Cloth = "#2a2a30", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "hook", Beard = null, Glow = null, Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "adventurer", Height = 1.02, Hide = new string[0], Shave = true, Bald = false },
            ["ysolde"] = new HeroLook { Skin = "fair", Hair = "#c8281e", Style = "long", Gear = "circlet", Armor = "#5a1e1e", Accent = "#ff7a3a", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "whip", Beard = null, Glow = "#ffb347", Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "hooded", Height = 1, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["corvan"] = new HeroLook { Skin = "brown", Hair = "#d0e6f0", Style = "long", Gear = "none", Armor = "#20507a", Accent = "#7fd4ff", Cloth = "#173a5a", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "staff", Beard = "full", Glow = null, Cape = false, CapeColor = null, Robe = "short", RobeColor = "#20507a", Base = "monarch", Height = 0.97, Hide = new string[0], Shave = false, Bald = false },
            ["hask"] = new HeroLook { Skin = "tan", Hair = "#4a4036", Style = "short", Gear = "helm", Armor = "#5c656e", Accent = "#d8e8c8", Cloth = "#2f6b3a", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "shield", Beard = "full", Glow = null, Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "monarch", Height = 1, Hide = new string[0], Shave = false, Bald = false },
            ["lumen"] = new HeroLook { Skin = "fair", Hair = "#f2d27a", Style = "swept", Gear = "none", Armor = "#c8d0dc", Accent = "#ffcc33", Cloth = "#4f5a6c", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "sword", Beard = null, Glow = "#fff2b0", Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "adventurer", Height = 1.06, Hide = new string[0], Shave = true, Bald = false },
            ["nyx"] = new HeroLook { Skin = "pale", Hair = "#1e1a2a", Style = "hood", Gear = "mask", Armor = "#26203a", Accent = "#b48cff", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "daggers", Beard = null, Glow = "#c9a3ff", Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "hooded", Height = 0.97, Hide = new string[0], Shave = false, Bald = false },
            ["tamsin"] = new HeroLook { Skin = "tan", Hair = "#8a6a2a", Style = "braid", Gear = "none", Armor = "#5f7f3a", Accent = "#e8d07a", Cloth = "#4a6a2a", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "bow", Beard = null, Glow = null, Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "hooded", Height = 0.98, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["gorrum"] = new HeroLook { Skin = "deep", Hair = "#1a1410", Style = "bald", Gear = "horns", Armor = "#3a2a22", Accent = "#ff5a1a", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "hammer", Beard = "full", Glow = "#ff7a2a", Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "monarch", Height = 1.1, Hide = new string[0], Shave = false, Bald = true },
            ["velka"] = new HeroLook { Skin = "blue", Hair = "#e8f4ff", Style = "long", Gear = "circlet", Armor = "#3a6a8a", Accent = "#bfefff", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "staff", Beard = null, Glow = null, Cape = false, CapeColor = null, Robe = "long", RobeColor = "#3a6a8a", Base = "hooded", Height = 1.03, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["aurelia"] = new HeroLook { Skin = "fair", Hair = "#e8b84a", Style = "long", Gear = "helm", Armor = "#d9b24a", Accent = "#f5f0e0", Cloth = "#2a3f8f", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "shield", Beard = null, Glow = "#fff2b0", Cape = true, CapeColor = "#2a3f8f", Robe = null, RobeColor = null, Base = "monarch", Height = 1.03, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["sable"] = new HeroLook { Skin = "pale", Hair = "#3a2050", Style = "swept", Gear = "veil", Armor = "#232a48", Accent = "#c8d8ff", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "mirror", Beard = null, Glow = null, Cape = true, CapeColor = "#161a30", Robe = "long", RobeColor = "#1c2240", Base = "hooded", Height = 1, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["kaito"] = new HeroLook { Skin = "fair", Hair = "#1a1a1a", Style = "spiky", Gear = "bandana", Armor = "#2f5a4a", Accent = "#7affc0", Cloth = "#1e3a30", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "katana", Beard = null, Glow = "#a8ffdf", Cape = false, CapeColor = null, Robe = null, RobeColor = null, Base = "adventurer", Height = 1.02, Hide = new string[0], Shave = true, Bald = false },
            ["maelis"] = new HeroLook { Skin = "brown", Hair = "#163a5a", Style = "long", Gear = "crown", Armor = "#1a4a6a", Accent = "#4ad0ff", Cloth = "#123048", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "trident", Beard = null, Glow = null, Cape = true, CapeColor = null, Robe = null, RobeColor = null, Base = "monarch", Height = 1.04, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["thrain"] = new HeroLook { Skin = "tan", Hair = "#a8321a", Style = "short", Gear = "none", Armor = "#5a3a2a", Accent = "#ffa040", Cloth = "#7a5034", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "hammer", Beard = "braided", Glow = null, Cape = true, CapeColor = "#5a2a1a", Robe = null, RobeColor = null, Base = "monarch", Height = 0.88, Hide = new string[0], Shave = false, Bald = false },
            ["oriel"] = new HeroLook { Skin = "deep", Hair = "#f0e6c8", Style = "swept", Gear = "halo", Armor = "#e08a2c", Accent = "#ffe27a", Cloth = "#b5521f", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "staff", Beard = null, Glow = "#ffe27a", Cape = false, CapeColor = null, Robe = "long", RobeColor = "#c4561e", Base = "adventurer", Height = 1.04, Hide = new string[0], Shave = true, Bald = false },
            ["vaelith"] = new HeroLook { Skin = "fair", Hair = "#ff5a2a", Style = "flame", Gear = "crown", Armor = "#3a1010", Accent = "#ffb020", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "sword", Beard = null, Glow = "#ffd060", Cape = true, CapeColor = null, Robe = null, RobeColor = null, Base = "adventurer", Height = 1.06, Hide = new[] { "hair" }, Shave = false, Bald = false },
            ["morwenna"] = new HeroLook { Skin = "ash", Hair = "#120a1a", Style = "long", Gear = "crown", Armor = "#1a0f24", Accent = "#a050ff", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "orb", Beard = null, Glow = "#d090ff", Cape = true, CapeColor = null, Robe = "long", RobeColor = "#140b1e", Base = "hooded", Height = 1.06, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["seraphine"] = new HeroLook { Skin = "pale", Hair = "#fff4d8", Style = "long", Gear = "halo", Armor = "#f0e8d8", Accent = "#ffd870", Cloth = null, Pants = null, Trim = null, Boot = null, Hood = null, Prop = "bell", Beard = null, Glow = "#fff0a0", Cape = false, CapeColor = null, Robe = "long", RobeColor = "#f4eee0", Base = "hooded", Height = 1.02, Hide = new[] { "hood" }, Shave = false, Bald = false },
            ["ulfgar"] = new HeroLook { Skin = "tan", Hair = "#c8ccd0", Style = "long", Gear = "helm", Armor = "#8a9aac", Accent = "#bfe8ff", Cloth = "#3a4a62", Pants = null, Trim = null, Boot = null, Hood = null, Prop = "shield", Beard = "braided", Glow = null, Cape = true, CapeColor = "#2a3a52", Robe = null, RobeColor = null, Base = "monarch", Height = 1.08, Hide = new string[0], Shave = false, Bald = false },
            // Roster phase 1 (Unity only, docs/sigilbound/ROSTER.md); the baked models are in Tools~/models/heroes.
            ["bram"] = new HeroLook { Skin = "tan", Hair = "#6a5a4a", Style = "short", Gear = "none", Armor = "#2f5a6e", Accent = "#c8b070", Cloth = "#3a4a52", Prop = "club", Beard = "full", Base = "monarch", Height = 1.05, Hide = new[] { "crown" } },
            ["nettle"] = new HeroLook { Skin = "fair", Hair = "#8a5a2a", Style = "messy", Gear = "bandana", Armor = "#6a8a3a", Accent = "#e8c84a", Cloth = "#4a5a2a", Prop = "daggers", Base = "adventurer", Height = 0.92, Shave = true },
            ["hobb"] = new HeroLook { Skin = "brown", Hair = "#3a2a1a", Style = "short", Gear = "helm", Armor = "#8a8070", Accent = "#ffd870", Cloth = "#5a4a3a", Prop = "shield", Beard = "stubble", Base = "monarch", Height = 1.0, Hide = new[] { "crown" } },
            ["greta"] = new HeroLook { Skin = "fair", Hair = "#c8b8a0", Style = "braid", Gear = "none", Armor = "#4a7a8a", Accent = "#e8d8b0", Cloth = "#2a4a5a", Prop = "staff", Base = "hooded", Height = 0.96, Hide = new[] { "hood" } },
            ["cole"] = new HeroLook { Skin = "fair", Hair = "#2a2420", Style = "spiky", Gear = "none", Armor = "#5a4a3a", Accent = "#ff8a3a", Cloth = "#3a2a22", Prop = "hammer", Base = "adventurer", Height = 1.0, Shave = true },
            ["tilly"] = new HeroLook { Skin = "pale", Hair = "#5a4a6a", Style = "long", Gear = "veil", Armor = "#3a3048", Accent = "#c8a8ff", Cloth = "#2a2238", Prop = "candle", Glow = "#e0c8ff", Base = "hooded", Height = 0.92, Hide = new[] { "hood" } },
            ["ronan"] = new HeroLook { Skin = "tan", Hair = "#8a5a2a", Style = "short", Gear = "none", Armor = "#6a5a3a", Accent = "#c8d870", Cloth = "#4a6a3a", Prop = "hammer", Beard = "full", Base = "monarch", Height = 1.04, Hide = new[] { "crown" } },
            ["isla"] = new HeroLook { Skin = "fair", Hair = "#e8d890", Style = "long", Gear = "none", Armor = "#e8e0c8", Accent = "#ffd870", Cloth = "#c8c0a8", Prop = "bell", Glow = "#fff0a0", Base = "hooded", Height = 0.97, Hide = new[] { "hood" } },
            ["dunstan"] = new HeroLook { Skin = "tan", Hair = "#8a8a8a", Style = "short", Gear = "helm", Armor = "#6a7a5a", Accent = "#d8c890", Cloth = "#3a5a3a", Prop = "shield", Beard = "full", Cape = true, CapeColor = "#2a4a2a", Base = "monarch", Height = 1.04, Hide = new[] { "crown" } },
            ["marisol"] = new HeroLook { Skin = "brown", Hair = "#1a1a2a", Style = "ponytail", Gear = "none", Armor = "#2a6a8a", Accent = "#7ae0ff", Cloth = "#1a4a5a", Prop = "trident", Glow = "#7ae0ff", Base = "adventurer", Height = 0.98, Shave = true },
            ["vesper"] = new HeroLook { Skin = "pale", Hair = "#2a2030", Style = "hood", Gear = "mask", Armor = "#2a2238", Accent = "#7ad07a", Cloth = "#1a1622", Prop = "daggers", Glow = "#9aff9a", Base = "hooded", Height = 0.96 },
            ["brega"] = new HeroLook { Skin = "deep", Hair = "#3a5a5a", Style = "short", Gear = "helm", Armor = "#4a6a6a", Accent = "#c8e8d0", Cloth = "#2a4a4a", Prop = "shield", Base = "monarch", Height = 1.0, Hide = new[] { "crown" } },
            ["fennick"] = new HeroLook { Skin = "pale", Hair = "#d86a2a", Style = "messy", Gear = "bandana", Armor = "#7a3a2a", Accent = "#ffd040", Cloth = "#5a2a1a", Prop = "candle", Glow = "#ffb040", Base = "adventurer", Height = 0.92, Shave = true },
            ["orla"] = new HeroLook { Skin = "tan", Hair = "#5a7a5a", Style = "long", Gear = "none", Armor = "#3a6a6a", Accent = "#a8e0c8", Cloth = "#2a5050", Prop = "staff", Glow = "#a8e0c8", Robe = "long", RobeColor = "#2a5050", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["silas"] = new HeroLook { Skin = "deep", Hair = "#1a1a1a", Style = "short", Gear = "veil", Armor = "#2a2a3a", Accent = "#8a7aff", Cloth = "#1a1a28", Prop = "shield", Glow = "#a898ff", Base = "hooded", Height = 1.02, Hide = new[] { "hood" } },
            ["wynn"] = new HeroLook { Skin = "fair", Hair = "#e8e0c0", Style = "spiky", Gear = "none", Armor = "#c8a860", Accent = "#fff0a0", Cloth = "#8a6a3a", Prop = "bow", Glow = "#fff2b0", Base = "adventurer", Height = 1.0, Shave = true },
            ["kestrel"] = new HeroLook { Skin = "tan", Hair = "#3a2a1a", Style = "short", Gear = "bandana", Armor = "#5a7a9a", Accent = "#e0f0ff", Cloth = "#3a4a6a", Prop = "spear", Glow = "#c0e8ff", Base = "adventurer", Height = 1.02, Shave = true },
            ["sorrel"] = new HeroLook { Skin = "brown", Hair = "#2a2a2a", Style = "short", Gear = "none", Armor = "#4a3a4a", Accent = "#9aff6a", Cloth = "#3a2a3a", Prop = "club", Beard = "full", Cape = true, CapeColor = "#2a1a2a", Base = "monarch", Height = 1.06, Hide = new[] { "crown" } },
            ["amber"] = new HeroLook { Skin = "fair", Hair = "#e8a040", Style = "braid", Gear = "helm", Armor = "#c87a30", Accent = "#ffe0a0", Cloth = "#7a4a20", Prop = "shield", Glow = "#ffb060", Base = "hooded", Height = 0.98, Hide = new[] { "hood" } },
            ["tobias"] = new HeroLook { Skin = "fair", Hair = "#a07040", Style = "long", Gear = "none", Armor = "#6a8a5a", Accent = "#ffd080", Cloth = "#4a6a3a", Prop = "staff", Base = "adventurer", Height = 1.0, Shave = true },
            ["nerys"] = new HeroLook { Skin = "pale", Hair = "#9ac8e8", Style = "long", Gear = "circlet", Armor = "#4a7aa0", Accent = "#e0f4ff", Cloth = "#2a4a6a", Prop = "mirror", Glow = "#bfefff", Robe = "long", RobeColor = "#2a4a6a", Base = "hooded", Height = 0.98, Hide = new[] { "hood" } },
            ["hale"] = new HeroLook { Skin = "tan", Hair = "#c8a060", Style = "short", Gear = "none", Armor = "#a89070", Accent = "#ffe08a", Cloth = "#6a5030", Prop = "hammer", Beard = "full", Glow = "#ffe08a", Cape = true, CapeColor = "#8a3a1a", Base = "monarch", Height = 1.06, Hide = new[] { "crown" } },
            ["juno"] = new HeroLook { Skin = "tan", Hair = "#e8501a", Style = "long", Gear = "veil", Armor = "#8a2a1a", Accent = "#ffb030", Cloth = "#5a1a10", Prop = "whip", Glow = "#ffb030", Base = "hooded", Height = 0.97, Hide = new[] { "hood" } },
            ["pike"] = new HeroLook { Skin = "tan", Hair = "#4a3a2a", Style = "short", Gear = "bandana", Armor = "#4a5a6a", Accent = "#c8d8e0", Cloth = "#2a3a4a", Prop = "hook", Beard = "stubble", Base = "adventurer", Height = 1.0 },
            ["rhea"] = new HeroLook { Skin = "fair", Hair = "#f2d27a", Style = "long", Gear = "helm", Armor = "#d8dce4", Accent = "#ffd860", Cloth = "#5a6a8a", Prop = "shield", Glow = "#fff2b0", Cape = true, CapeColor = "#5a6a8a", Base = "monarch", Height = 1.0, Hide = new[] { "crown" } },
            ["corwin"] = new HeroLook { Skin = "pale", Hair = "#1a1a24", Style = "spiky", Gear = "none", Armor = "#2a2238", Accent = "#c070ff", Cloth = "#1a1428", Prop = "katana", Beard = "stubble", Glow = "#d090ff", Base = "adventurer", Height = 1.03 },
            ["astrid"] = new HeroLook { Skin = "fair", Hair = "#e8e8f0", Style = "ponytail", Gear = "none", Armor = "#6a90b0", Accent = "#c8f0ff", Cloth = "#3a5a7a", Prop = "spear", Glow = "#c8f0ff", Cape = true, CapeColor = "#2a4a6a", Base = "adventurer", Height = 1.04, Shave = true },
            ["caspian"] = new HeroLook { Skin = "brown", Hair = "#1a3a4a", Style = "long", Gear = "crown", Armor = "#1a5a6a", Accent = "#5ae0e0", Cloth = "#0e3a48", Prop = "trident", Beard = "full", Glow = "#5ae0e0", Cape = true, CapeColor = "#0e3a48", Base = "monarch", Height = 1.08 },
            ["ignis"] = new HeroLook { Skin = "deep", Hair = "#ff6a1a", Style = "flame", Gear = "none", Armor = "#6a1a10", Accent = "#ffc040", Cloth = "#3a0e08", Prop = "staff", Glow = "#ffb030", Robe = "long", RobeColor = "#3a0e08", Base = "adventurer", Height = 1.03, Shave = true },
            ["lyra"] = new HeroLook { Skin = "pale", Hair = "#e8f0ff", Style = "long", Gear = "halo", Armor = "#c8d8f0", Accent = "#e0f0ff", Cloth = "#8aa0c8", Prop = "mirror", Glow = "#e8f4ff", Robe = "long", RobeColor = "#8aa0c8", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["malachar"] = new HeroLook { Skin = "ash", Hair = "#2a2030", Style = "short", Gear = "horns", Armor = "#2a2434", Accent = "#a070ff", Cloth = "#1a1424", Prop = "shield", Glow = "#b080ff", Cape = true, CapeColor = "#140e1a", Base = "monarch", Height = 1.1, Hide = new[] { "crown" } },
            ["rowan"] = new HeroLook { Skin = "tan", Hair = "#5a4a2a", Style = "long", Gear = "none", Armor = "#3a5a2a", Accent = "#c8e080", Cloth = "#2a4020", Prop = "shield", Beard = "full", Cape = true, CapeColor = "#1e3418", Base = "monarch", Height = 1.05, Hide = new[] { "crown" } },
            ["selene"] = new HeroLook { Skin = "pale", Hair = "#c8d0ff", Style = "long", Gear = "circlet", Armor = "#2a3a6a", Accent = "#a8c8ff", Cloth = "#1a2448", Prop = "orb", Glow = "#c8d8ff", Robe = "long", RobeColor = "#1a2448", Base = "hooded", Height = 1.02, Hide = new[] { "hood" } },
            ["brann"] = new HeroLook { Skin = "tan", Hair = "#8a2a1a", Style = "bald", Gear = "none", Armor = "#5a2a1a", Accent = "#ff8a30", Cloth = "#3a1a10", Prop = "club", Beard = "full", Glow = "#ff8a30", Base = "monarch", Height = 1.08, Hide = new[] { "crown" }, Bald = true },
            ["ysra"] = new HeroLook { Skin = "ash", Hair = "#3a1a3a", Style = "long", Gear = "veil", Armor = "#2a1428", Accent = "#ff6ac8", Cloth = "#1a0a1a", Prop = "daggers", Glow = "#ff8ad8", Base = "hooded", Height = 0.98, Hide = new[] { "hood" } },
            ["oswin"] = new HeroLook { Skin = "brown", Hair = "#f0e0a0", Style = "short", Gear = "helm", Armor = "#e8d8a8", Accent = "#ffd860", Cloth = "#8a6a2a", Prop = "spear", Glow = "#fff2b0", Base = "adventurer", Height = 1.05, Shave = true },
            ["grimsby"] = new HeroLook { Skin = "ash", Hair = "#d8d0c0", Style = "messy", Gear = "mask", Armor = "#4a4450", Accent = "#e8dcc0", Cloth = "#2a2630", Prop = "staff", Glow = "#b8a8ff", Base = "adventurer", Height = 0.96, Shave = true },
            ["marrow"] = new HeroLook { Skin = "ash", Hair = "#b8b4ac", Style = "long", Gear = "none", Armor = "#6a6660", Accent = "#ff6a2a", Cloth = "#3a3632", Prop = "club", Glow = "#ff7a2a", Cape = true, CapeColor = "#2a2622", Base = "monarch", Height = 1.15, Hide = new[] { "crown" } },
            ["ember"] = new HeroLook { Skin = "fair", Hair = "#f0e0c0", Style = "long", Gear = "veil", Armor = "#e8d8c0", Accent = "#ffb050", Cloth = "#c8b090", Prop = "candle", Glow = "#ffb050", Robe = "long", RobeColor = "#c8b090", Base = "hooded", Height = 0.97, Hide = new[] { "hood" } },
            ["quill"] = new HeroLook { Skin = "fair", Hair = "#5a3a2a", Style = "short", Gear = "circlet", Armor = "#3a4a6a", Accent = "#e8d8a0", Cloth = "#2a3450", Prop = "staff", Base = "adventurer", Height = 0.98, Shave = true },
            ["drift"] = new HeroLook { Skin = "fair", Hair = "#c8e8ff", Style = "messy", Gear = "bandana", Armor = "#5a8aaa", Accent = "#e8f8ff", Cloth = "#3a5a7a", Prop = "daggers", Glow = "#c8f0ff", Base = "adventurer", Height = 0.92, Shave = true },
            ["sprig"] = new HeroLook { Skin = "fair", Hair = "#7aa04a", Style = "braid", Gear = "none", Armor = "#5a8a3a", Accent = "#e8d870", Cloth = "#3a5a2a", Prop = "bell", Base = "hooded", Height = 0.86, Hide = new[] { "hood" } },
            ["tallow"] = new HeroLook { Skin = "tan", Hair = "#4a3020", Style = "short", Gear = "helm", Armor = "#7a5a3a", Accent = "#ff9a40", Cloth = "#4a3020", Prop = "shield", Cape = true, CapeColor = "#a04a1a", Base = "monarch", Height = 1.0, Hide = new[] { "crown" } },
            ["hollis"] = new HeroLook { Skin = "deep", Hair = "#2a2a2a", Style = "hood", Gear = "none", Armor = "#3a3a3a", Accent = "#8aa08a", Cloth = "#2a2a2a", Prop = "club", Base = "hooded", Height = 1.04 },
            ["auren"] = new HeroLook { Skin = "fair", Hair = "#e8f0e0", Style = "long", Gear = "none", Armor = "#7ab0a0", Accent = "#e0fff0", Cloth = "#4a8070", Prop = "staff", Glow = "#c8ffe8", Robe = "long", RobeColor = "#4a8070", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["cinder"] = new HeroLook { Skin = "fair", Hair = "#ff7a2a", Style = "spiky", Gear = "none", Armor = "#4a2418", Accent = "#ffa040", Cloth = "#2a120c", Prop = "sword", Glow = "#ffb040", Base = "adventurer", Height = 1.04, Shave = true },
            ["thessaly"] = new HeroLook { Skin = "tan", Hair = "#3a5a6a", Style = "short", Gear = "helm", Armor = "#2a6a7a", Accent = "#a8f0f0", Cloth = "#1a4a5a", Prop = "shield", Glow = "#a8f0f0", Cape = true, CapeColor = "#123a48", Base = "monarch", Height = 1.06, Hide = new[] { "crown" } },
            ["umbra"] = new HeroLook { Skin = "ash", Hair = "#3a3a4a", Style = "hood", Gear = "mask", Armor = "#2a2a3a", Accent = "#9a8aff", Cloth = "#1a1a28", Prop = "orb", Glow = "#b0a0ff", Robe = "long", RobeColor = "#1a1a28", Base = "hooded", Height = 1.0 },
            ["lucan"] = new HeroLook { Skin = "fair", Hair = "#e8d8a0", Style = "short", Gear = "helm", Armor = "#e8e0c8", Accent = "#ffd860", Cloth = "#8a6a2a", Prop = "shield", Glow = "#fff2b0", Cape = true, CapeColor = "#f0e8d0", Base = "monarch", Height = 1.06, Hide = new[] { "crown" } },
            ["kharra"] = new HeroLook { Skin = "ash", Hair = "#2a1a14", Style = "bald", Gear = "horns", Armor = "#3a2a24", Accent = "#ff5a1a", Cloth = "#24140e", Prop = "hammer", Beard = "full", Glow = "#ff7a2a", Cape = true, CapeColor = "#4a1a0e", Base = "monarch", Height = 1.18, Hide = new[] { "crown" }, Bald = true },
            ["nocturne"] = new HeroLook { Skin = "ash", Hair = "#141020", Style = "hood", Gear = "mask", Armor = "#1a1424", Accent = "#c060ff", Cloth = "#0e0a16", Prop = "katana", Glow = "#d080ff", Cape = true, CapeColor = "#0e0a16", Base = "hooded", Height = 1.06 },
            ["aelira"] = new HeroLook { Skin = "brown", Hair = "#0e3a5a", Style = "long", Gear = "crown", Armor = "#1a5a8a", Accent = "#7ae8ff", Cloth = "#0e3048", Prop = "orb", Glow = "#9af0ff", Cape = true, CapeColor = "#0e3048", Robe = "long", RobeColor = "#0e3048", Base = "hooded", Height = 1.06, Hide = new[] { "hood" } },
            ["zephyrine"] = new HeroLook { Skin = "pale", Hair = "#d8f0e8", Style = "ponytail", Gear = "none", Armor = "#4a8a7a", Accent = "#c8fff0", Cloth = "#2a5a50", Prop = "katana", Glow = "#c8fff0", Cape = true, CapeColor = "#2a5a50", Base = "adventurer", Height = 1.04, Shave = true },
            ["solenne"] = new HeroLook { Skin = "deep", Hair = "#f8e8b0", Style = "long", Gear = "halo", Armor = "#f0d890", Accent = "#fff0a0", Cloth = "#c89040", Prop = "hammer", Glow = "#fff4b0", Cape = true, CapeColor = "#e8c870", Base = "monarch", Height = 1.08 },
            ["nell"] = new HeroLook { Skin = "tan", Hair = "#2a3020", Style = "hood", Gear = "mask", Armor = "#3a4030", Accent = "#9ac060", Cloth = "#2a3020", Prop = "hook", Base = "hooded", Height = 0.95 },
            ["briar"] = new HeroLook { Skin = "fair", Hair = "#4a6a2a", Style = "braid", Gear = "none", Armor = "#3a5a2a", Accent = "#d07050", Cloth = "#2a4020", Prop = "whip", Glow = "#a0ff80", Base = "hooded", Height = 0.98, Hide = new[] { "hood" } },
            ["perrin"] = new HeroLook { Skin = "tan", Hair = "#3a2a1a", Style = "short", Gear = "bandana", Armor = "#3a6a8a", Accent = "#e8e0c0", Cloth = "#2a4a5a", Prop = "shield", Beard = "stubble", Base = "adventurer", Height = 1.0 },
            ["loam"] = new HeroLook { Skin = "tan", Hair = "#7a5a30", Style = "short", Gear = "none", Armor = "#6a7a3a", Accent = "#d8c070", Cloth = "#4a5a2a", Prop = "shield", Beard = "full", Base = "monarch", Height = 1.04, Hide = new[] { "crown" } },
            ["merrit"] = new HeroLook { Skin = "fair", Hair = "#d8b060", Style = "spiky", Gear = "none", Armor = "#c8c0b0", Accent = "#ffd860", Cloth = "#5a6a8a", Prop = "sword", Base = "adventurer", Height = 0.96, Shave = true },
            ["sienna"] = new HeroLook { Skin = "tan", Hair = "#c84a2a", Style = "braid", Gear = "none", Armor = "#7a3a20", Accent = "#ffb050", Cloth = "#4a2010", Prop = "bow", Glow = "#ffb040", Base = "hooded", Height = 0.98, Hide = new[] { "hood" } },
            ["calder"] = new HeroLook { Skin = "tan", Hair = "#8a8a8a", Style = "short", Gear = "none", Armor = "#3a4a6a", Accent = "#fff0a0", Cloth = "#2a3450", Prop = "lantern", Beard = "full", Glow = "#fff0a0", Base = "monarch", Height = 1.06, Hide = new[] { "crown" } },
            ["dorian"] = new HeroLook { Skin = "brown", Hair = "#4a4a4a", Style = "short", Gear = "none", Armor = "#2a4a6a", Accent = "#c8b070", Cloth = "#1a2a40", Prop = "club", Beard = "full", Base = "monarch", Height = 1.08, Hide = new[] { "crown" } },
            ["ivo"] = new HeroLook { Skin = "fair", Hair = "#d8c080", Style = "short", Gear = "helm", Armor = "#d8d0b8", Accent = "#ffd860", Cloth = "#8a6a3a", Prop = "hammer", Cape = true, CapeColor = "#c8a050", Base = "monarch", Height = 1.04, Hide = new[] { "crown" } },
            ["saoirse"] = new HeroLook { Skin = "pale", Hair = "#2a3a3a", Style = "long", Gear = "veil", Armor = "#2a3434", Accent = "#9ad0c0", Cloth = "#1a2424", Prop = "shield", Glow = "#a8e8d8", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["gideon"] = new HeroLook { Skin = "tan", Hair = "#5a2a1a", Style = "short", Gear = "helm", Armor = "#7a2a1a", Accent = "#ffa040", Cloth = "#4a1a10", Prop = "shield", Glow = "#ffa040", Cape = true, CapeColor = "#5a1a10", Base = "monarch", Height = 1.08, Hide = new[] { "crown" } },
            ["liora"] = new HeroLook { Skin = "fair", Hair = "#5ad0d0", Style = "long", Gear = "none", Armor = "#1a6a7a", Accent = "#a8ffff", Cloth = "#0e4048", Prop = "trident", Glow = "#a8ffff", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["bastian"] = new HeroLook { Skin = "ash", Hair = "#6a6a6a", Style = "short", Gear = "none", Armor = "#3a3440", Accent = "#b080ff", Cloth = "#24202a", Prop = "club", Beard = "stubble", Glow = "#b080ff", Cape = true, CapeColor = "#1a161e", Base = "monarch", Height = 1.1, Hide = new[] { "crown" } },
            ["elowen"] = new HeroLook { Skin = "fair", Hair = "#f0e0a0", Style = "ponytail", Gear = "none", Armor = "#e8e0c0", Accent = "#fff2b0", Cloth = "#6a8a5a", Prop = "bow", Glow = "#fff2b0", Base = "adventurer", Height = 1.0, Shave = true },
            ["faelan"] = new HeroLook { Skin = "tan", Hair = "#a05a2a", Style = "long", Gear = "none", Armor = "#4a6a5a", Accent = "#c8f0d0", Cloth = "#2a4a3a", Prop = "spear", Beard = "braided", Cape = true, CapeColor = "#2a4a3a", Base = "monarch", Height = 1.1, Hide = new[] { "crown" } },
            ["wick"] = new HeroLook { Skin = "fair", Hair = "#e8a050", Style = "messy", Gear = "halo", Armor = "#a0502a", Accent = "#ffd060", Cloth = "#6a3018", Prop = "candle", Glow = "#ffd060", Cape = true, CapeColor = "#c8602a", Base = "adventurer", Height = 0.96, Shave = true },
            ["azura"] = new HeroLook { Skin = "brown", Hair = "#1a4a8a", Style = "long", Gear = "none", Armor = "#1a3a7a", Accent = "#7ad0ff", Cloth = "#0e2050", Prop = "trident", Glow = "#9ae0ff", Cape = true, CapeColor = "#0e2050", Base = "adventurer", Height = 1.06, Shave = true },
            ["pyrrhus"] = new HeroLook { Skin = "deep", Hair = "#2a1410", Style = "short", Gear = "horns", Armor = "#5a1a0e", Accent = "#ff8a20", Cloth = "#2a0e08", Prop = "hammer", Glow = "#ffa030", Cape = true, CapeColor = "#3a0e08", Base = "monarch", Height = 1.12, Hide = new[] { "crown" } },
            ["kaelith"] = new HeroLook { Skin = "ash", Hair = "#1a1424", Style = "long", Gear = "crown", Armor = "#1a1028", Accent = "#c070ff", Cloth = "#0e0818", Prop = "orb", Glow = "#d090ff", Cape = true, CapeColor = "#0e0818", Robe = "long", RobeColor = "#0e0818", Base = "hooded", Height = 1.08, Hide = new[] { "hood" } },
            ["aurex"] = new HeroLook { Skin = "tan", Hair = "#f8f0d0", Style = "short", Gear = "halo", Armor = "#f0e8d0", Accent = "#ffe070", Cloth = "#c89a40", Prop = "sword", Glow = "#fff4b0", Cape = true, CapeColor = "#f0e0b0", Base = "adventurer", Height = 1.08, Shave = true },
            ["stormbearer"] = new HeroLook { Skin = "fair", Hair = "#c8d8e0", Style = "short", Gear = "helm", Armor = "#6a8aa0", Accent = "#c8f0ff", Cloth = "#3a5a70", Prop = "shield", Glow = "#c8f0ff", Cape = true, CapeColor = "#2a4a60", Base = "monarch", Height = 1.1, Hide = new[] { "crown" } },
            ["vex"] = new HeroLook { Skin = "ash", Hair = "#2a2a34", Style = "long", Gear = "crown", Armor = "#24202c", Accent = "#ff4a6a", Cloth = "#14101a", Prop = "orb", Glow = "#ff6a8a", Cape = true, CapeColor = "#14101a", Base = "monarch", Height = 1.12 },
            // --- phase H looks: generated by docs/sigilbound/newheroes.py (do not edit by hand) ---
            ["dace"] = new HeroLook { Skin = "fair", Hair = "#1e1a18", Style = "short", Gear = "none", Armor = "#2b2725", Accent = "#ff8a3a", Cloth = "#5a4636", Prop = "shield", Glow = "#ff6a2a", Cape = false, CapeColor = "#5a4636", Base = "adventurer", Height = 0.86, Hide = new[] { "hood" } },
            ["dulcie"] = new HeroLook { Skin = "fair", Hair = "#8a3a1e", Style = "long", Gear = "none", Armor = "#f2e6d0", Accent = "#ffb347", Cloth = "#a8452a", Prop = "sword", Glow = "#ff8a3a", Cape = false, CapeColor = "#a8452a", Base = "hooded", Height = 0.95, Hide = new[] { "hood" } },
            ["holt"] = new HeroLook { Skin = "fair", Hair = "#4a2e1c", Style = "short", Gear = "none", Armor = "#3a3634", Accent = "#e8a040", Cloth = "#7a3a22", Prop = "hammer", Glow = "#ff6a1a", Cape = false, CapeColor = "#7a3a22", Base = "monarch", Height = 1.05, Hide = new[] { "hood" } },
            ["rhosyn"] = new HeroLook { Skin = "fair", Hair = "#1e1410", Style = "long", Gear = "none", Armor = "#4a3a2a", Accent = "#ffb030", Cloth = "#7a2a1a", Prop = "sword", Glow = "#ff7a2a", Cape = false, CapeColor = "#7a2a1a", Base = "hooded", Height = 0.97, Hide = new[] { "hood" } },
            ["ansel"] = new HeroLook { Skin = "fair", Hair = "#6a4a30", Style = "bald", Gear = "none", Armor = "#8a2a1e", Accent = "#e8b040", Cloth = "#d8c8a8", Prop = "sword", Glow = "#ff9a40", Cape = false, CapeColor = "#d8c8a8", Base = "adventurer", Height = 1.05, Hide = new[] { "hood" } },
            ["saskia"] = new HeroLook { Skin = "fair", Hair = "#3a1a10", Style = "long", Gear = "none", Armor = "#4a2e1e", Accent = "#ff9a30", Cloth = "#7a4a2a", Prop = "sword", Glow = "#ff6a2a", Cape = false, CapeColor = "#7a4a2a", Base = "hooded", Height = 1.08, Hide = new[] { "hood" } },
            ["kasimir"] = new HeroLook { Skin = "fair", Hair = "#2a1a12", Style = "short", Gear = "none", Armor = "#5a2018", Accent = "#ffb040", Cloth = "#2a2422", Prop = "sword", Glow = "#ff6a1a", Cape = false, CapeColor = "#2a2422", Base = "monarch", Height = 1.08, Hide = new[] { "hood" } },
            ["imogen"] = new HeroLook { Skin = "fair", Hair = "#e05a20", Style = "long", Gear = "none", Armor = "#c8402a", Accent = "#e8b040", Cloth = "#f0e0c0", Prop = "sword", Glow = "#ff8a3a", Cape = false, CapeColor = "#f0e0c0", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["ilsabet"] = new HeroLook { Skin = "fair", Hair = "#141010", Style = "long", Gear = "none", Armor = "#1e1614", Accent = "#ffa020", Cloth = "#5a1a10", Prop = "hammer", Glow = "#ff5a1a", Cape = false, CapeColor = "#5a1a10", Base = "hooded", Height = 1.08, Hide = new[] { "hood" } },
            ["obrecht"] = new HeroLook { Skin = "fair", Hair = "#3a3430", Style = "short", Gear = "none", Armor = "#6a5a4a", Accent = "#ffb050", Cloth = "#8a3a1e", Prop = "sword", Glow = "#ff7a2a", Cape = false, CapeColor = "#8a3a1e", Base = "adventurer", Height = 1.15, Hide = new[] { "hood" } },
            ["tansy"] = new HeroLook { Skin = "fair", Hair = "#c8902a", Style = "long", Gear = "none", Armor = "#6a8a3a", Accent = "#e8d070", Cloth = "#c8a860", Prop = "sword", Glow = "#5ad08a", Cape = false, CapeColor = "#c8a860", Base = "hooded", Height = 0.9, Hide = new[] { "hood" } },
            ["barley"] = new HeroLook { Skin = "fair", Hair = "#e0dcd0", Style = "short", Gear = "none", Armor = "#e8e2d2", Accent = "#c89a3a", Cloth = "#5a7a4a", Prop = "sword", Glow = "#5ad08a", Cape = false, CapeColor = "#5a7a4a", Base = "adventurer", Height = 1.0, Hide = new[] { "hood" } },
            ["clover"] = new HeroLook { Skin = "fair", Hair = "#2a1a12", Style = "long", Gear = "none", Armor = "#3fa070", Accent = "#f08a4a", Cloth = "#e8d8a0", Prop = "sword", Glow = "#7ae0a0", Cape = false, CapeColor = "#e8d8a0", Base = "hooded", Height = 0.88, Hide = new[] { "hood" } },
            ["edric"] = new HeroLook { Skin = "fair", Hair = "#7a5a3a", Style = "short", Gear = "none", Armor = "#d8cfb8", Accent = "#c8a050", Cloth = "#4a6a3a", Prop = "sword", Glow = "#5ad08a", Cape = false, CapeColor = "#4a6a3a", Base = "monarch", Height = 1.05, Hide = new[] { "hood" } },
            ["corran"] = new HeroLook { Skin = "fair", Hair = "#e8e0d0", Style = "short", Gear = "none", Armor = "#3a5a3a", Accent = "#e8e0c8", Cloth = "#a87a4a", Prop = "sword", Glow = "#5ad08a", Cape = false, CapeColor = "#a87a4a", Base = "adventurer", Height = 1.1, Hide = new[] { "hood" } },
            ["linnet"] = new HeroLook { Skin = "fair", Hair = "#a0602a", Style = "long", Gear = "none", Armor = "#c8a860", Accent = "#e86a6a", Cloth = "#5a6a70", Prop = "sword", Glow = "#5ad08a", Cape = false, CapeColor = "#5a6a70", Base = "hooded", Height = 1.04, Hide = new[] { "hood" } },
            ["ysabeau"] = new HeroLook { Skin = "fair", Hair = "#3a2418", Style = "long", Gear = "none", Armor = "#2f6a4a", Accent = "#e8c060", Cloth = "#6a4a30", Prop = "sword", Glow = "#5ae08a", Cape = false, CapeColor = "#6a4a30", Base = "hooded", Height = 1.03, Hide = new[] { "hood" } },
            ["wendel"] = new HeroLook { Skin = "fair", Hair = "#6a4a2a", Style = "bald", Gear = "none", Armor = "#d8b060", Accent = "#f0f0e0", Cloth = "#4a5a3a", Prop = "sword", Glow = "#7ae08a", Cape = false, CapeColor = "#4a5a3a", Base = "adventurer", Height = 1.0, Hide = new[] { "hood" } },
            ["hilde"] = new HeroLook { Skin = "fair", Hair = "#c86a2a", Style = "long", Gear = "none", Armor = "#5a6a70", Accent = "#e8e0c8", Cloth = "#3a5a40", Prop = "sword", Glow = "#5ad08a", Cape = false, CapeColor = "#3a5a40", Base = "hooded", Height = 1.08, Hide = new[] { "hood" } },
            ["aveline"] = new HeroLook { Skin = "fair", Hair = "#e8e4d8", Style = "long", Gear = "none", Armor = "#e8eee0", Accent = "#d8b860", Cloth = "#3a7a5a", Prop = "sword", Glow = "#5aff9a", Cape = false, CapeColor = "#3a7a5a", Base = "hooded", Height = 1.1, Hide = new[] { "hood" } },
            ["jory"] = new HeroLook { Skin = "fair", Hair = "#3a2a1e", Style = "short", Gear = "none", Armor = "#2a5a7a", Accent = "#c8642a", Cloth = "#d8d0b8", Prop = "lute", Glow = "#5ab0ff", Cape = false, CapeColor = "#d8d0b8", Base = "adventurer", Height = 1.02, Hide = new[] { "hood" } },
            ["morag"] = new HeroLook { Skin = "fair", Hair = "#8a2a1a", Style = "long", Gear = "none", Armor = "#3a7a70", Accent = "#f0e8d8", Cloth = "#c8b080", Prop = "sword", Glow = "#5ab0ff", Cape = false, CapeColor = "#c8b080", Base = "hooded", Height = 0.96, Hide = new[] { "hood" } },
            ["wenna"] = new HeroLook { Skin = "fair", Hair = "#3a5a4a", Style = "long", Gear = "none", Armor = "#3a6a5a", Accent = "#e8805a", Cloth = "#c8b890", Prop = "club", Glow = "#5ab0ff", Cape = false, CapeColor = "#c8b890", Base = "hooded", Height = 0.92, Hide = new[] { "hood" } },
            ["lazlo"] = new HeroLook { Skin = "fair", Hair = "#3a5a6a", Style = "short", Gear = "none", Armor = "#1e5a7a", Accent = "#4ae0d0", Cloth = "#e8e0d0", Prop = "sword", Glow = "#5ab0ff", Cape = false, CapeColor = "#e8e0d0", Base = "monarch", Height = 1.06, Hide = new[] { "hood" } },
            ["mungo"] = new HeroLook { Skin = "fair", Hair = "#1a1410", Style = "short", Gear = "none", Armor = "#6a4a30", Accent = "#e8c060", Cloth = "#2a4a6a", Prop = "sword", Glow = "#5ab0ff", Cape = false, CapeColor = "#2a4a6a", Base = "adventurer", Height = 1.12, Hide = new[] { "hood" } },
            ["neve"] = new HeroLook { Skin = "fair", Hair = "#e8f0f0", Style = "long", Gear = "horns", Armor = "#2a7a8a", Accent = "#f08a7a", Cloth = "#f0e8dc", Prop = "sword", Glow = "#6ab8ff", Cape = false, CapeColor = "#f0e8dc", Base = "hooded", Height = 1.02, Hide = new[] { "hood" } },
            ["hendrick"] = new HeroLook { Skin = "fair", Hair = "#8a8a8a", Style = "short", Gear = "none", Armor = "#1e3a5a", Accent = "#e8c050", Cloth = "#8a6a40", Prop = "axe", Glow = "#5ab0ff", Cape = false, CapeColor = "#8a6a40", Base = "monarch", Height = 1.08, Hide = new[] { "hood" } },
            ["kaija"] = new HeroLook { Skin = "fair", Hair = "#3a2a20", Style = "long", Gear = "none", Armor = "#e8eef4", Accent = "#7ad8ff", Cloth = "#2a4a6a", Prop = "sword", Glow = "#6ac0ff", Cape = false, CapeColor = "#2a4a6a", Base = "hooded", Height = 1.05, Hide = new[] { "hood" } },
            ["morvoren"] = new HeroLook { Skin = "fair", Hair = "#1a4a5a", Style = "long", Gear = "none", Armor = "#1a4a6a", Accent = "#f0e8d8", Cloth = "#2a8a8a", Prop = "staff", Glow = "#5ac8ff", Cape = false, CapeColor = "#2a8a8a", Base = "hooded", Height = 1.1, Hide = new[] { "hood" } },
            ["leofric"] = new HeroLook { Skin = "fair", Hair = "#1a2a3a", Style = "short", Gear = "helm", Armor = "#2a4a6a", Accent = "#4ae0e0", Cloth = "#e8e8e0", Prop = "spear", Glow = "#5ab8ff", Cape = false, CapeColor = "#e8e8e0", Base = "monarch", Height = 1.12, Hide = new[] { "hood" } },
            ["hettie"] = new HeroLook { Skin = "fair", Hair = "#e8c060", Style = "long", Gear = "none", Armor = "#e8d090", Accent = "#5a8ac8", Cloth = "#8a5a3a", Prop = "sword", Glow = "#ffe070", Cape = false, CapeColor = "#8a5a3a", Base = "hooded", Height = 0.92, Hide = new[] { "hood" } },
            ["abel"] = new HeroLook { Skin = "fair", Hair = "#c8c0b0", Style = "short", Gear = "none", Armor = "#f0e8d0", Accent = "#e8c060", Cloth = "#8a6a4a", Prop = "sword", Glow = "#ffe070", Cape = false, CapeColor = "#8a6a4a", Base = "adventurer", Height = 1.02, Hide = new[] { "hood" } },
            ["poppy"] = new HeroLook { Skin = "fair", Hair = "#e0a020", Style = "long", Gear = "none", Armor = "#e0b030", Accent = "#6a3a1a", Cloth = "#4a6a3a", Prop = "staff", Glow = "#ffe070", Cape = false, CapeColor = "#4a6a3a", Base = "hooded", Height = 0.85, Hide = new[] { "hood" } },
            ["benedict"] = new HeroLook { Skin = "fair", Hair = "#b89a70", Style = "short", Gear = "none", Armor = "#e8e0c8", Accent = "#d8a83a", Cloth = "#6a5a40", Prop = "hammer", Glow = "#ffe070", Cape = false, CapeColor = "#6a5a40", Base = "adventurer", Height = 1.05, Hide = new[] { "hood" } },
            ["emeric"] = new HeroLook { Skin = "fair", Hair = "#1a1410", Style = "short", Gear = "none", Armor = "#f0e8d0", Accent = "#e8b83a", Cloth = "#4a6a9a", Prop = "sword", Glow = "#ffe070", Cape = false, CapeColor = "#4a6a9a", Base = "adventurer", Height = 1.06, Hide = new[] { "hood" } },
            ["agathe"] = new HeroLook { Skin = "fair", Hair = "#8a8a8a", Style = "long", Gear = "none", Armor = "#f0e8d8", Accent = "#d8a83a", Cloth = "#5a6a8a", Prop = "staff", Glow = "#ffe070", Cape = false, CapeColor = "#5a6a8a", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["ottavia"] = new HeroLook { Skin = "fair", Hair = "#141010", Style = "long", Gear = "none", Armor = "#f0e8d0", Accent = "#e8b83a", Cloth = "#3a5a9a", Prop = "spear", Glow = "#ffe070", Cape = false, CapeColor = "#3a5a9a", Base = "hooded", Height = 1.1, Hide = new[] { "hood" } },
            ["fabian"] = new HeroLook { Skin = "fair", Hair = "#e8e0d0", Style = "short", Gear = "none", Armor = "#3a5a9a", Accent = "#e8b83a", Cloth = "#f0e8d0", Prop = "sword", Glow = "#ffe070", Cape = false, CapeColor = "#f0e8d0", Base = "adventurer", Height = 0.86, Hide = new[] { "hood" } },
            ["augustin"] = new HeroLook { Skin = "fair", Hair = "#c8902a", Style = "short", Gear = "none", Armor = "#c8902a", Accent = "#a83a2a", Cloth = "#f0e8d0", Prop = "sword", Glow = "#ffe070", Cape = false, CapeColor = "#f0e8d0", Base = "adventurer", Height = 1.12, Hide = new[] { "hood" } },
            ["clemence"] = new HeroLook { Skin = "fair", Hair = "#f0d890", Style = "long", Gear = "helm", Armor = "#f4f0e4", Accent = "#e8b83a", Cloth = "#3a6aa8", Prop = "sword", Glow = "#fff0a0", Cape = false, CapeColor = "#3a6aa8", Base = "hooded", Height = 1.1, Hide = new[] { "hood" } },
            ["rook"] = new HeroLook { Skin = "fair", Hair = "#1a1a24", Style = "short", Gear = "none", Armor = "#3a3448", Accent = "#c8c0b0", Cloth = "#5a5a3a", Prop = "sword", Glow = "#b080ff", Cape = false, CapeColor = "#5a5a3a", Base = "adventurer", Height = 0.98, Hide = new[] { "hood" } },
            ["pernille"] = new HeroLook { Skin = "fair", Hair = "#4a2a5a", Style = "long", Gear = "none", Armor = "#4a3a6a", Accent = "#e8d8a0", Cloth = "#c8c8d8", Prop = "sword", Glow = "#b080ff", Cape = false, CapeColor = "#c8c8d8", Base = "hooded", Height = 0.94, Hide = new[] { "hood" } },
            ["perpetua"] = new HeroLook { Skin = "fair", Hair = "#141018", Style = "long", Gear = "none", Armor = "#1e1e3a", Accent = "#c8c8e0", Cloth = "#2a2430", Prop = "shield", Glow = "#a070ff", Cape = false, CapeColor = "#2a2430", Base = "hooded", Height = 1.04, Hide = new[] { "hood" } },
            ["jasper"] = new HeroLook { Skin = "fair", Hair = "#1a1420", Style = "short", Gear = "none", Armor = "#2a1e3a", Accent = "#e8e0c8", Cloth = "#3a3a4a", Prop = "sword", Glow = "#b07aff", Cape = false, CapeColor = "#3a3a4a", Base = "monarch", Height = 1.04, Hide = new[] { "hood" } },
            ["mabon"] = new HeroLook { Skin = "fair", Hair = "#9a9a9a", Style = "bald", Gear = "none", Armor = "#24284a", Accent = "#c8a8ff", Cloth = "#7a6a50", Prop = "sword", Glow = "#b080ff", Cape = false, CapeColor = "#7a6a50", Base = "adventurer", Height = 0.98, Hide = new[] { "hood" } },
            ["tindra"] = new HeroLook { Skin = "fair", Hair = "#141010", Style = "long", Gear = "none", Armor = "#4a4058", Accent = "#e0c8a0", Cloth = "#3a4a3a", Prop = "sword", Glow = "#b070ff", Cape = false, CapeColor = "#3a4a3a", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["ambrose"] = new HeroLook { Skin = "fair", Hair = "#3a3030", Style = "short", Gear = "none", Armor = "#2a2a40", Accent = "#c8b070", Cloth = "#5a4a3a", Prop = "club", Glow = "#a070ff", Cape = false, CapeColor = "#5a4a3a", Base = "monarch", Height = 1.12, Hide = new[] { "hood" } },
            ["lune"] = new HeroLook { Skin = "fair", Hair = "#c8c8d8", Style = "long", Gear = "none", Armor = "#2a3050", Accent = "#c8a060", Cloth = "#e8e4dc", Prop = "staff", Glow = "#b490ff", Cape = false, CapeColor = "#e8e4dc", Base = "hooded", Height = 1.0, Hide = new[] { "hood" } },
            ["aldric"] = new HeroLook { Skin = "fair", Hair = "#c8c8d0", Style = "short", Gear = "helm", Armor = "#1e1a30", Accent = "#d0d4e8", Cloth = "#2a3050", Prop = "sword", Glow = "#a070ff", Cape = false, CapeColor = "#2a3050", Base = "monarch", Height = 1.1, Hide = new[] { "hood" } },
            ["ondessa"] = new HeroLook { Skin = "fair", Hair = "#140e1e", Style = "long", Gear = "crown", Armor = "#1a1428", Accent = "#e8d8f8", Cloth = "#3a4a3a", Prop = "staff", Glow = "#a070ff", Cape = false, CapeColor = "#3a4a3a", Base = "hooded", Height = 1.08, Hide = new[] { "hood" } },
            // --- end phase H looks ---
            // Creature heroes (fallback look only: the game shows the high-detail creature build).
            ["vessa"] = new HeroLook { Skin = "#a2a2c6", Hair = "#0e0c12", Style = "long", Gear = "none", Armor = "#141218", Accent = "#c8cad8", Cloth = "#2a2236", Prop = "sickle", Glow = "#8a4cff", Cape = false, CapeColor = "#2a2236", Base = "hooded", Height = 1.15, Hide = new[] { "hood" } },
        };
    }
}
