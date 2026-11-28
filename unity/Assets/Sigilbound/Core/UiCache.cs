// Allocation-free pieces for the per-frame UI code (performance review #5, code review M5). OnGUI runs at
// least twice a frame, and the Ui/Skin helpers used to build a string key (colours formatted as hex, enums
// and bools boxed into an interpolation) and a closure on every call, plus four float[] per 9-slice and a
// new string per level badge or stack count: a few KB of garbage per battle frame. These types live in Core
// (no UnityEngine) so Tests~/HotPathTests.cs can check that they allocate nothing.
using System;

namespace Sigilbound.Core
{
    // A cache key for generated UI textures and styles: what kind of thing, up to five packed colours and two
    // ints. A readonly struct with its own equality, so Dictionary lookups neither allocate nor box.
    public readonly struct UiKey : IEquatable<UiKey>
    {
        public readonly int Kind, X, Y;
        public readonly uint A, B, C, D, E;
        public UiKey(int kind, uint a = 0, uint b = 0, uint c = 0, uint d = 0, uint e = 0, int x = 0, int y = 0)
        { Kind = kind; A = a; B = b; C = c; D = d; E = e; X = x; Y = y; }
        public bool Equals(UiKey o) => Kind == o.Kind && A == o.A && B == o.B && C == o.C && D == o.D && E == o.E && X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is UiKey k && Equals(k);
        public override int GetHashCode()
        {
            unchecked
            {
                int h = Kind * 397;
                h = (h ^ (int)A) * 16777619; h = (h ^ (int)B) * 16777619; h = (h ^ (int)C) * 16777619;
                h = (h ^ (int)D) * 16777619; h = (h ^ (int)E) * 16777619; h = (h ^ X) * 16777619; h = (h ^ Y) * 16777619;
                return h;
            }
        }
        public static bool operator ==(UiKey a, UiKey b) => a.Equals(b);
        public static bool operator !=(UiKey a, UiKey b) => !a.Equals(b);

        // A colour as 8-bit RGBA (the same rounding as Color32, so colours that draw the same share a key).
        public static uint Rgba(float r, float g, float b, float a) => (uint)(B8(r) << 24 | B8(g) << 16 | B8(b) << 8 | B8(a));
        static int B8(float v) => v <= 0 ? 0 : v >= 1 ? 255 : (int)(v * 255f + 0.5f);
        // A packed set of small flags and enum values (bits from `shift` up).
        public static int Bits(bool v, int shift) => v ? 1 << shift : 0;
    }

    // The 9-slice cut of a stretched frame: x and y edges on screen, u and v edges in the texture. A struct
    // filled in place of the four float[] the skin drew with.
    public struct NineSlice
    {
        public float X0, X1, X2, X3, Y0, Y1, Y2, Y3, U0, U1, U2, U3, V0, V1, V2, V3;

        // rect (x, y, w, h); border sizes on screen (l, r, t, b); texture size and border pixels.
        // GUI y runs down and UV y runs up, so V0 is the top (1).
        public static NineSlice Of(float x, float y, float w, float h, float l, float r, float t, float b,
                                   float texW, float texH, int bl, int br, int bt, int bb)
        {
            return new NineSlice
            {
                X0 = x, X1 = x + l, X2 = x + w - r, X3 = x + w,
                Y0 = y, Y1 = y + t, Y2 = y + h - b, Y3 = y + h,
                U0 = 0, U1 = bl / texW, U2 = 1 - br / texW, U3 = 1,
                V0 = 1, V1 = 1 - bt / texH, V2 = bb / texH, V3 = 0,
            };
        }
        public float X(int i) => i == 0 ? X0 : i == 1 ? X1 : i == 2 ? X2 : X3;
        public float Y(int i) => i == 0 ? Y0 : i == 1 ? Y1 : i == 2 ? Y2 : Y3;
        public float U(int i) => i == 0 ? U0 : i == 1 ? U1 : i == 2 ? U2 : U3;
        public float V(int i) => i == 0 ? V0 : i == 1 ? V1 : i == 2 ? V2 : V3;
    }

    // Small numbers as text without a new string per draw (level badges, turn counts, "×2" stacks).
    public static class NumText
    {
        const int Max = 1000;
        static readonly string[] _n = new string[Max], _times = new string[Max];
        public static string Of(int n)
        {
            if (n < 0 || n >= Max) return n.ToString();
            return _n[n] ??= n.ToString();
        }
        public static string Times(int n)
        {
            if (n < 0 || n >= Max) return "×" + n;
            return _times[n] ??= "×" + n;
        }
    }
}
