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
            int c = ((chapter % 10) + 10) % 10;
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
        };

        public static bool IsCaster(string heroId) => Looks.TryGetValue(heroId, out var l) &&
            (l.Prop == Prop.Staff || l.Prop == Prop.Orb || l.Prop == Prop.Bell || l.Prop == Prop.Lantern || l.Prop == Prop.Candle || l.Prop == Prop.Mirror || l.Prop == Prop.Bow);
    }
}
