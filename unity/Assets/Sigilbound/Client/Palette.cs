// Colours and per-hero looks for the placeholder 3D models (matches the web art in art.js).
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public enum Prop { Spear, Sword, Katana, Staff, Hammer, Trident, Shield, Bow, Lantern, Candle, Club, Hook, Whip, Daggers, Mirror, Orb, Bell }
    public enum Headgear { None, Helm, Crown, Circlet, Horns, Bandana, Halo, Veil, Mask, Hood }

    public sealed class Look
    {
        public Color Skin, Hair, Armor, Accent; public Headgear Gear; public Prop Prop; public bool Beard; public bool LongHair; public Color? Glow;
    }

    public static class Palette
    {
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // The web game's gilded skin (style.css :root).
        public static readonly Color Bg = Hex("#0f0c15"), Panel = Hex("#1b1624"), Panel2 = Hex("#251e31"), Line = Hex("#4a3d2e");
        public static readonly Color Text = Hex("#f1e9da"), Dim = Hex("#b0a48f"), Gold = Hex("#f2c45c"), GoldDark = Hex("#b3802a");
        public static readonly Color Trim = Hex("#b88a3e"), TrimHi = Hex("#ffe2a0"), TrimLo = Hex("#6b4b1c"), Bronze = Hex("#5c4a30");
        public static readonly Color Good = Hex("#5bd17a"), Bad = Hex("#ff5a5a"), Blue = Hex("#4aa3ff");

        public static Color Element(Element e) => e switch
        {
            Core.Element.Flame => Hex("#e8553a"), Core.Element.Gale => Hex("#3fb57a"), Core.Element.Tide => Hex("#3a8ee8"),
            Core.Element.Light => Hex("#e8c23a"), _ => Hex("#9a5ae8"),
        };
        public static string ElementName(Element e) => e.ToString();
        public static Color Rarity(Rarity r) => r switch
        {
            Core.Rarity.Common => Hex("#9aa3ad"), Core.Rarity.Rare => Hex("#4aa3ff"), Core.Rarity.Epic => Hex("#b46cff"), _ => Hex("#ffb02e"),
        };
        public static Color GearRarity(int r) => r switch { 1 => Hex("#9aa3ad"), 2 => Hex("#5bc46a"), 3 => Hex("#4aa3ff"), 4 => Hex("#b46cff"), _ => Hex("#ffb02e") };

        // Chapter scenery: sky top, sky bottom/fog, ground, prop colour.
        public static (Color top, Color bottom, Color ground, Color prop) Scene(int chapter)
        {
            string[,] s =
            {
                { "#1f2b46", "#c9805a", "#2a2a22", "#16141f" }, { "#16283a", "#4f7c8a", "#1c2a2a", "#0e1a22" },
                { "#2a1a1e", "#a8483a", "#2e1a1c", "#160c0e" }, { "#1a2e2a", "#7fa088", "#23362a", "#101c16" },
                { "#28203a", "#c8a870", "#2c2640", "#14101e" }, { "#2a2438", "#e0b0d0", "#3a2e4a", "#ffd0f0" },
                { "#101a2e", "#5a7ab0", "#1a2440", "#0c1220" }, { "#0e0a16", "#3a2a5a", "#180f26", "#b080ff" },
                { "#0e1a2a", "#2a5a7a", "#10283c", "#80e0ff" }, { "#1a0a0a", "#ff6a2a", "#24100c", "#100606" },
            };
            int c = GameData.ThemeOf(chapter); // act 2 chapters use their own theme picks
            return (Hex(s[c, 0]), Hex(s[c, 1]), Hex(s[c, 2]), Hex(s[c, 3]));
        }

        static Look L(string skin, string hair, string armor, string accent, Headgear gear, Prop prop, bool beard = false, bool longHair = false, string glow = null) =>
            new Look { Skin = Hex(skin), Hair = Hex(hair), Armor = Hex(armor), Accent = Hex(accent), Gear = gear, Prop = prop, Beard = beard, LongHair = longHair, Glow = glow == null ? (Color?)null : Hex(glow) };

        const string Pale = "#f3d6c1", Fair = "#e9c09f", Tan = "#c99068", Brown = "#9a6544", Deep = "#6b412b", Ash = "#b9b4c4", BlueSkin = "#8fb3d9";

        public static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            ["brannoc"] = L(Tan, "#5a3a22", "#6e7f5a", "#c9b26a", Headgear.Bandana, Prop.Spear, true),
            ["mira"] = L(Fair, "#2f6f8f", "#3f7f9a", "#e8e0c8", Headgear.None, Prop.Shield, false, true),
            ["pell"] = L(Pale, "#c8642a", "#8a4a2a", "#ffb347", Headgear.None, Prop.Candle),
            ["odo"] = L(Brown, "#3b2a1a", "#6b5a3a", "#a8c070", Headgear.None, Prop.Club, true),
            ["wren"] = L(Fair, "#e8e2d0", "#d8d2c0", "#e8c23a", Headgear.Hood, Prop.Lantern),
            ["kettle"] = L(Ash, "#2a2a30", "#3a3442", "#9a5ae8", Headgear.None, Prop.Hook, false, true),
            ["ysolde"] = L(Fair, "#b8241c", "#5a1e1e", "#ff7a3a", Headgear.Circlet, Prop.Whip, false, true, "#ffb347"),
            ["corvan"] = L(Brown, "#d0e6f0", "#20507a", "#7fd4ff", Headgear.None, Prop.Staff, true, true),
            ["hask"] = L(Tan, "#4a4036", "#6a7a6a", "#bfe0b0", Headgear.Helm, Prop.Shield, true),
            ["lumen"] = L(Fair, "#f2d27a", "#d8c48a", "#fff2b0", Headgear.None, Prop.Sword, false, false, "#fff2b0"),
            ["nyx"] = L(Pale, "#1e1a2a", "#26203a", "#b48cff", Headgear.Mask, Prop.Daggers, false, false, "#c9a3ff"),
            ["tamsin"] = L(Tan, "#8a6a2a", "#5f7f3a", "#e8d07a", Headgear.None, Prop.Bow, false, true),
            ["gorrum"] = L(Deep, "#1a1410", "#4a2a1a", "#ff5a1a", Headgear.Horns, Prop.Hammer, true, false, "#ff7a2a"),
            ["velka"] = L(BlueSkin, "#e8f4ff", "#3a6a8a", "#bfefff", Headgear.Circlet, Prop.Staff, false, true),
            ["aurelia"] = L(Fair, "#e8b84a", "#d8c070", "#ffffff", Headgear.Helm, Prop.Shield, false, true, "#fff2b0"),
            ["sable"] = L(Pale, "#2a1a3a", "#2a1f3a", "#c8b0ff", Headgear.Veil, Prop.Mirror),
            ["kaito"] = L(Fair, "#1a1a1a", "#2f5a4a", "#7affc0", Headgear.Bandana, Prop.Katana, false, false, "#a8ffdf"),
            ["maelis"] = L(Brown, "#163a5a", "#1a4a6a", "#4ad0ff", Headgear.Crown, Prop.Trident, false, true),
            ["thrain"] = L(Tan, "#8a2a1a", "#5a3a2a", "#ffa040", Headgear.None, Prop.Hammer, true),
            ["oriel"] = L(Deep, "#f0e6c8", "#e0d4b0", "#ffe27a", Headgear.Halo, Prop.Staff, false, false, "#ffe27a"),
            ["vaelith"] = L(Fair, "#ff5a2a", "#3a1010", "#ffb020", Headgear.Crown, Prop.Sword, false, false, "#ffd060"),
            ["morwenna"] = L(Ash, "#120a1a", "#1a0f24", "#a050ff", Headgear.Crown, Prop.Orb, false, true, "#d090ff"),
            ["seraphine"] = L(Pale, "#fff4d8", "#f0e8d8", "#ffd870", Headgear.Halo, Prop.Bell, false, true, "#fff0a0"),
            ["ulfgar"] = L(Tan, "#c8ccd0", "#5a6a7a", "#bfe8ff", Headgear.Helm, Prop.Shield, true, true),
            ["bram"] = L(Tan, "#6a5a4a", "#2f5a6e", "#c8b070", Headgear.None, Prop.Club, true),
            ["nettle"] = L(Fair, "#8a5a2a", "#6a8a3a", "#e8c84a", Headgear.Bandana, Prop.Daggers),
            ["hobb"] = L(Brown, "#3a2a1a", "#8a8070", "#ffd870", Headgear.Helm, Prop.Shield, true),
            ["greta"] = L(Fair, "#c8b8a0", "#4a7a8a", "#e8d8b0", Headgear.None, Prop.Staff, false, true),
            ["cole"] = L(Fair, "#2a2420", "#5a4a3a", "#ff8a3a", Headgear.None, Prop.Hammer),
            ["tilly"] = L(Pale, "#5a4a6a", "#3a3048", "#c8a8ff", Headgear.Veil, Prop.Candle, false, true, "#e0c8ff"),
            ["ronan"] = L(Tan, "#8a5a2a", "#6a5a3a", "#c8d870", Headgear.None, Prop.Hammer, true),
            ["isla"] = L(Fair, "#e8d890", "#e8e0c8", "#ffd870", Headgear.None, Prop.Bell, false, true, "#fff0a0"),
            ["dunstan"] = L(Tan, "#8a8a8a", "#6a7a5a", "#d8c890", Headgear.Helm, Prop.Shield, true),
            ["marisol"] = L(Brown, "#1a1a2a", "#2a6a8a", "#7ae0ff", Headgear.None, Prop.Trident, false, true, "#7ae0ff"),
            ["vesper"] = L(Pale, "#2a2030", "#2a2238", "#7ad07a", Headgear.Mask, Prop.Daggers, false, false, "#9aff9a"),
            ["brega"] = L(Deep, "#3a5a5a", "#4a6a6a", "#c8e8d0", Headgear.Helm, Prop.Shield),
            ["fennick"] = L(Pale, "#d86a2a", "#7a3a2a", "#ffd040", Headgear.Bandana, Prop.Candle, false, false, "#ffb040"),
            ["orla"] = L(Tan, "#5a7a5a", "#3a6a6a", "#a8e0c8", Headgear.None, Prop.Staff, false, true, "#a8e0c8"),
            ["silas"] = L(Deep, "#1a1a1a", "#2a2a3a", "#8a7aff", Headgear.Veil, Prop.Shield, false, false, "#a898ff"),
            ["wynn"] = L(Fair, "#e8e0c0", "#c8a860", "#fff0a0", Headgear.None, Prop.Bow, false, false, "#fff2b0"),
            ["kestrel"] = L(Tan, "#3a2a1a", "#5a7a9a", "#e0f0ff", Headgear.Bandana, Prop.Spear, false, false, "#c0e8ff"),
            ["sorrel"] = L(Brown, "#2a2a2a", "#4a3a4a", "#9aff6a", Headgear.None, Prop.Club, true),
            ["amber"] = L(Fair, "#e8a040", "#c87a30", "#ffe0a0", Headgear.Helm, Prop.Shield, false, true, "#ffb060"),
            ["tobias"] = L(Fair, "#a07040", "#6a8a5a", "#ffd080", Headgear.None, Prop.Staff, false, true),
            ["nerys"] = L(Pale, "#9ac8e8", "#4a7aa0", "#e0f4ff", Headgear.Circlet, Prop.Mirror, false, true, "#bfefff"),
            ["hale"] = L(Tan, "#c8a060", "#a89070", "#ffe08a", Headgear.None, Prop.Hammer, true, false, "#ffe08a"),
            ["juno"] = L(Tan, "#e8501a", "#8a2a1a", "#ffb030", Headgear.Veil, Prop.Whip, false, true, "#ffb030"),
            ["pike"] = L(Tan, "#4a3a2a", "#4a5a6a", "#c8d8e0", Headgear.Bandana, Prop.Hook, true),
            ["rhea"] = L(Fair, "#f2d27a", "#d8dce4", "#ffd860", Headgear.Helm, Prop.Shield, false, true, "#fff2b0"),
            ["corwin"] = L(Pale, "#1a1a24", "#2a2238", "#c070ff", Headgear.None, Prop.Katana, true, false, "#d090ff"),
            ["astrid"] = L(Fair, "#e8e8f0", "#6a90b0", "#c8f0ff", Headgear.None, Prop.Spear, false, true, "#c8f0ff"),
            ["caspian"] = L(Brown, "#1a3a4a", "#1a5a6a", "#5ae0e0", Headgear.Crown, Prop.Trident, true, true, "#5ae0e0"),
            ["ignis"] = L(Deep, "#ff6a1a", "#6a1a10", "#ffc040", Headgear.None, Prop.Staff, false, false, "#ffb030"),
            ["lyra"] = L(Pale, "#e8f0ff", "#c8d8f0", "#e0f0ff", Headgear.Halo, Prop.Mirror, false, true, "#e8f4ff"),
            ["malachar"] = L(Ash, "#2a2030", "#2a2434", "#a070ff", Headgear.Horns, Prop.Shield, false, false, "#b080ff"),
            ["rowan"] = L(Tan, "#5a4a2a", "#3a5a2a", "#c8e080", Headgear.None, Prop.Shield, true, true),
            ["selene"] = L(Pale, "#c8d0ff", "#2a3a6a", "#a8c8ff", Headgear.Circlet, Prop.Orb, false, true, "#c8d8ff"),
            ["brann"] = L(Tan, "#8a2a1a", "#5a2a1a", "#ff8a30", Headgear.None, Prop.Club, true, false, "#ff8a30"),
            ["ysra"] = L(Ash, "#3a1a3a", "#2a1428", "#ff6ac8", Headgear.Veil, Prop.Daggers, false, true, "#ff8ad8"),
            ["oswin"] = L(Brown, "#f0e0a0", "#e8d8a8", "#ffd860", Headgear.Helm, Prop.Spear, false, false, "#fff2b0"),
            ["grimsby"] = L(Ash, "#d8d0c0", "#4a4450", "#e8dcc0", Headgear.Mask, Prop.Staff, false, false, "#b8a8ff"),
            ["marrow"] = L(Ash, "#b8b4ac", "#6a6660", "#ff6a2a", Headgear.None, Prop.Club, false, true, "#ff7a2a"),
            ["ember"] = L(Fair, "#f0e0c0", "#e8d8c0", "#ffb050", Headgear.Veil, Prop.Candle, false, true, "#ffb050"),
            ["quill"] = L(Fair, "#5a3a2a", "#3a4a6a", "#e8d8a0", Headgear.Circlet, Prop.Staff),
            ["drift"] = L(Fair, "#c8e8ff", "#5a8aaa", "#e8f8ff", Headgear.Bandana, Prop.Daggers, false, false, "#c8f0ff"),
            ["sprig"] = L(Fair, "#7aa04a", "#5a8a3a", "#e8d870", Headgear.None, Prop.Bell, false, true),
            ["tallow"] = L(Tan, "#4a3020", "#7a5a3a", "#ff9a40", Headgear.Helm, Prop.Shield),
            ["hollis"] = L(Deep, "#2a2a2a", "#3a3a3a", "#8aa08a", Headgear.Hood, Prop.Club),
            ["auren"] = L(Fair, "#e8f0e0", "#7ab0a0", "#e0fff0", Headgear.None, Prop.Staff, false, true, "#c8ffe8"),
            ["cinder"] = L(Fair, "#ff7a2a", "#4a2418", "#ffa040", Headgear.None, Prop.Sword, false, false, "#ffb040"),
            ["thessaly"] = L(Tan, "#3a5a6a", "#2a6a7a", "#a8f0f0", Headgear.Helm, Prop.Shield, false, false, "#a8f0f0"),
            ["umbra"] = L(Ash, "#3a3a4a", "#2a2a3a", "#9a8aff", Headgear.Mask, Prop.Orb, false, false, "#b0a0ff"),
            ["lucan"] = L(Fair, "#e8d8a0", "#e8e0c8", "#ffd860", Headgear.Helm, Prop.Shield, false, false, "#fff2b0"),
            ["kharra"] = L(Ash, "#2a1a14", "#3a2a24", "#ff5a1a", Headgear.Horns, Prop.Hammer, true, false, "#ff7a2a"),
            ["nocturne"] = L(Ash, "#141020", "#1a1424", "#c060ff", Headgear.Mask, Prop.Katana, false, false, "#d080ff"),
            ["aelira"] = L(Brown, "#0e3a5a", "#1a5a8a", "#7ae8ff", Headgear.Crown, Prop.Orb, false, true, "#9af0ff"),
            ["zephyrine"] = L(Pale, "#d8f0e8", "#4a8a7a", "#c8fff0", Headgear.None, Prop.Katana, false, true, "#c8fff0"),
            ["solenne"] = L(Deep, "#f8e8b0", "#f0d890", "#fff0a0", Headgear.Halo, Prop.Hammer, false, true, "#fff4b0"),
            ["nell"] = L(Tan, "#2a3020", "#3a4030", "#9ac060", Headgear.Mask, Prop.Hook),
            ["briar"] = L(Fair, "#4a6a2a", "#3a5a2a", "#d07050", Headgear.None, Prop.Whip, false, true, "#a0ff80"),
            ["perrin"] = L(Tan, "#3a2a1a", "#3a6a8a", "#e8e0c0", Headgear.Bandana, Prop.Shield, true),
            ["loam"] = L(Tan, "#7a5a30", "#6a7a3a", "#d8c070", Headgear.None, Prop.Shield, true),
            ["merrit"] = L(Fair, "#d8b060", "#c8c0b0", "#ffd860", Headgear.None, Prop.Sword),
            ["sienna"] = L(Tan, "#c84a2a", "#7a3a20", "#ffb050", Headgear.None, Prop.Bow, false, true, "#ffb040"),
            ["calder"] = L(Tan, "#8a8a8a", "#3a4a6a", "#fff0a0", Headgear.None, Prop.Lantern, true, false, "#fff0a0"),
            ["dorian"] = L(Brown, "#4a4a4a", "#2a4a6a", "#c8b070", Headgear.None, Prop.Club, true),
            ["ivo"] = L(Fair, "#d8c080", "#d8d0b8", "#ffd860", Headgear.Helm, Prop.Hammer),
            ["saoirse"] = L(Pale, "#2a3a3a", "#2a3434", "#9ad0c0", Headgear.Veil, Prop.Shield, false, true, "#a8e8d8"),
            ["gideon"] = L(Tan, "#5a2a1a", "#7a2a1a", "#ffa040", Headgear.Helm, Prop.Shield, false, false, "#ffa040"),
            ["liora"] = L(Fair, "#5ad0d0", "#1a6a7a", "#a8ffff", Headgear.None, Prop.Trident, false, true, "#a8ffff"),
            ["bastian"] = L(Ash, "#6a6a6a", "#3a3440", "#b080ff", Headgear.None, Prop.Club, true, false, "#b080ff"),
            ["elowen"] = L(Fair, "#f0e0a0", "#e8e0c0", "#fff2b0", Headgear.None, Prop.Bow, false, true, "#fff2b0"),
            ["faelan"] = L(Tan, "#a05a2a", "#4a6a5a", "#c8f0d0", Headgear.None, Prop.Spear, true, true),
            ["wick"] = L(Fair, "#e8a050", "#a0502a", "#ffd060", Headgear.Halo, Prop.Candle, false, false, "#ffd060"),
            ["azura"] = L(Brown, "#1a4a8a", "#1a3a7a", "#7ad0ff", Headgear.None, Prop.Trident, false, true, "#9ae0ff"),
            ["pyrrhus"] = L(Deep, "#2a1410", "#5a1a0e", "#ff8a20", Headgear.Horns, Prop.Hammer, false, false, "#ffa030"),
            ["kaelith"] = L(Ash, "#1a1424", "#1a1028", "#c070ff", Headgear.Crown, Prop.Orb, false, true, "#d090ff"),
            ["aurex"] = L(Tan, "#f8f0d0", "#f0e8d0", "#ffe070", Headgear.Halo, Prop.Sword, false, false, "#fff4b0"),
            ["stormbearer"] = L(Fair, "#c8d8e0", "#6a8aa0", "#c8f0ff", Headgear.Helm, Prop.Shield, false, false, "#c8f0ff"),
            ["vex"] = L(Ash, "#2a2a34", "#24202c", "#ff4a6a", Headgear.Crown, Prop.Orb, false, true, "#ff6a8a"),
        };

        public static bool IsCaster(string heroId) => Looks.TryGetValue(heroId, out var l) &&
            (l.Prop == Prop.Staff || l.Prop == Prop.Orb || l.Prop == Prop.Bell || l.Prop == Prop.Lantern || l.Prop == Prop.Candle || l.Prop == Prop.Mirror || l.Prop == Prop.Bow);
    }
}
