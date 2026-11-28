// Physics standard (owner 2026-11-28: "colliders as a base standard for all models and game physics to everything";
// docs/sigilbound/art/PHYSICS.md). One place that says which body colliders every hero has and which colliders each
// kind of spring chain collides with.
//
// The body collider set is defined by humanoid bone names only (the UBC biped every hero shares, which the skeleton
// library keeps), so any rig path can build it: give Build a function that returns a bone's model-space rest
// position (or null when the bone is missing). Radii come from the hero's own mesh girth, measured at build time by
// blenderforge/physics_classify.py into BodyColliderData (generated); a hero without a line gets the fallback
// ratios of its own bone lengths. Spheres are capsules with A == B. Engine-free (SVec), tested in unity/Tests~.
// Collider shapes after UniVRM's VRMSpringBoneColliderGroup (spheres on bones; MIT, VRM Consortium) and its
// later capsule colliders; the per-chain collider groups follow the same idea.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core.Physics
{
    [Flags]
    public enum BodyGroup { None = 0, Head = 1, Neck = 2, Torso = 4, Hips = 8, Arms = 16, Forearms = 32, Thighs = 64, Calves = 128, Extra = 256, Ground = 512, All = 1023 }

    public struct ColliderShape
    {
        public string Name, BoneA, BoneB; // capsule from BoneA's joint to BoneB's joint; BoneB == null: sphere on BoneA
        public SVec Offset;               // sphere centre minus BoneA's joint, model space at rest (zero for capsules)
        public float Radius;              // model space (the glTF's metres)
        public BodyGroup Group;
        public bool Sphere => BoneB == null;
    }

    public static class ColliderSet
    {
        // Per-hero data order (BodyColliderData lines, physics_classify.DATA).
        public static readonly string[] Data = { "head", "headUp", "neck", "chest", "spine", "hips", "upperarm", "lowerarm", "thigh", "calf" };
        // Fallback radius / reference length (medians over the shipped heroes; headUp is a multiple of the head radius).
        public static readonly float[] Fallback = { 0.54f, 0.9f, 0.34f, 0.23f, 0.22f, 0.15f, 0.21f, 0.17f, 0.2f, 0.14f };
        // Sane band per radius as a fraction of the reference length (tests; the build tool clamps to the same band).
        public static readonly float[] BandLo = { 0.35f, 0.5f, 0.3f, 0.15f, 0.12f, 0.06f, 0.08f, 0.07f, 0.09f, 0.07f };
        public static readonly float[] BandHi = { 0.9f, 1.5f, 0.9f, 0.4f, 0.38f, 0.25f, 0.3f, 0.26f, 0.3f, 0.24f };

        struct Spec { public string Name, A, B; public int D; public BodyGroup G; public Spec(string n, string a, string b, int d, BodyGroup g) { Name = n; A = a; B = b; D = d; G = g; } }
        static readonly Spec[] Body =
        {
            new Spec("head", "Head", null, 0, BodyGroup.Head), new Spec("neck", "neck_01", "Head", 2, BodyGroup.Neck),
            new Spec("chest", "spine_02", "neck_01", 3, BodyGroup.Torso), new Spec("spine", "pelvis", "spine_02", 4, BodyGroup.Torso),
            new Spec("hips", "thigh_l", "thigh_r", 5, BodyGroup.Hips),
            new Spec("upperarm_l", "upperarm_l", "lowerarm_l", 6, BodyGroup.Arms), new Spec("upperarm_r", "upperarm_r", "lowerarm_r", 6, BodyGroup.Arms),
            new Spec("lowerarm_l", "lowerarm_l", "hand_l", 7, BodyGroup.Forearms), new Spec("lowerarm_r", "lowerarm_r", "hand_r", 7, BodyGroup.Forearms),
            new Spec("thigh_l", "thigh_l", "calf_l", 8, BodyGroup.Thighs), new Spec("thigh_r", "thigh_r", "calf_r", 8, BodyGroup.Thighs),
            new Spec("calf_l", "calf_l", "foot_l", 9, BodyGroup.Calves), new Spec("calf_r", "calf_r", "foot_r", 9, BodyGroup.Calves),
        };

        // Every bone the standard body set needs (a hero missing one loses that collider; tests require all).
        public static readonly string[] RequiredBones = { "pelvis", "spine_02", "neck_01", "Head", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r",
            "hand_l", "hand_r", "thigh_l", "thigh_r", "calf_l", "calf_r", "foot_l", "foot_r" };

        static float Dist(SVec a, SVec b) => (a - b).Length;

        // The length a radius is measured against (same rule as physics_classify.ref_len).
        public static float RefLength(int d, Func<string, SVec?> pos)
        {
            float L(string a, string b) { var pa = pos(a); var pb = pos(b); return pa.HasValue && pb.HasValue ? Dist(pa.Value, pb.Value) : 0f; }
            switch (d)
            {
                case 0: case 2: return L("neck_01", "Head") + 0.08f;
                case 1: return 1f;
                case 3: case 4: case 5: return L("pelvis", "neck_01");
                case 6: return L("upperarm_l", "lowerarm_l");
                case 7: return L("lowerarm_l", "hand_l");
                case 8: return L("thigh_l", "calf_l");
                default: return L("calf_l", "foot_l");
            }
        }

        // The radii for a hero: its measured line, else the fallback ratios of its own bone lengths.
        public static float[] Radii(string heroId, Func<string, SVec?> pos, IDictionary<string, float[]> data = null)
        {
            data = data ?? BodyColliderData.ById;
            if (heroId != null && data.TryGetValue(heroId, out var r) && r != null && r.Length == Data.Length) return r;
            var f = new float[Data.Length];
            for (int i = 0; i < f.Length; i++) f[i] = i == 1 ? Fallback[1] : Fallback[i] * RefLength(i, pos);
            return f;
        }

        // The body collider set of one hero. pos: bone name -> model-space rest position (null when missing).
        // missing (optional) receives the names of required bones the rig lacks.
        public static ColliderShape[] Build(string heroId, Func<string, SVec?> pos, List<string> missing = null, IDictionary<string, float[]> data = null)
        {
            var R = Radii(heroId, pos, data);
            var outp = new List<ColliderShape>(16);
            if (missing != null) foreach (var b in RequiredBones) if (!pos(b).HasValue) missing.Add(b);
            foreach (var s in Body)
            {
                var a = pos(s.A); var b = s.B != null ? pos(s.B) : null;
                if (!a.HasValue || (s.B != null && !b.HasValue)) continue;
                var sh = new ColliderShape { Name = s.Name, BoneA = s.A, BoneB = s.B, Radius = R[s.D], Group = s.G };
                if (s.B == null)
                {   // head sphere: lifted from the head joint along neck -> head by headUp x radius
                    var n = pos("neck_01"); var up = n.HasValue ? (a.Value - n.Value).Normalized : new SVec(0, 1, 0);
                    if (up.Length < 0.5f) up = new SVec(0, 1, 0);
                    sh.Offset = up * (R[1] * R[0]);
                }
                outp.Add(sh);
            }
            // Optional extras: tail base, wing roots (rig-time add-on bones)
            if (pos("tail_01").HasValue && pos("tail_02").HasValue)
                outp.Add(new ColliderShape { Name = "tail", BoneA = "tail_01", BoneB = "tail_02", Radius = 0.6f * R[9], Group = BodyGroup.Extra });
            foreach (var side in new[] { "l", "r" })
                if (pos("wing_" + side).HasValue)
                    outp.Add(new ColliderShape { Name = "wing_" + side, BoneA = "wing_" + side, BoneB = null, Radius = 0.8f * R[6], Group = BodyGroup.Extra });
            return outp.ToArray();
        }

        // Capsules (model space) for a set of shapes from the bones' current positions: posA/posB per shape.
        public static SCapsule Capsule(ColliderShape s, SVec a, SVec b) => new SCapsule { A = a, B = s.Sphere ? a : b, R = s.Radius };

        // Signed distance of p to the nearest shape of the given groups (negative = inside), rest pose. Tests, tools.
        public static float Outside(SVec p, ColliderShape[] set, Func<string, SVec?> pos, BodyGroup mask)
        {
            float best = 9f;
            foreach (var s in set)
            {
                if ((s.Group & mask) == 0) continue;
                var a = pos(s.BoneA).Value + s.Offset; var b = s.Sphere ? a : pos(s.BoneB).Value;
                var ab = b - a; float l2 = SVec.Dot(ab, ab);
                float t = l2 > 1e-12f ? Math.Max(0f, Math.Min(1f, SVec.Dot(p - a, ab) / l2)) : 0f;
                best = Math.Min(best, (p - (a + ab * t)).Length - s.Radius);
            }
            return best;
        }
    }

    // Spring chains by kind. A chain is the GLB nodes sb_<k>_<i>; its number k says its kind, so the data needs no
    // other field: 0-19 skirt/robe panels/coat tails, 20-29 cape, 30-34 tail, 35-39 soft wing, 40-49 hair,
    // 50-54 ear, 55-69 flap (sash, belt flap, tassel, pouch, banner, chain).
    public enum ChainKind { Skirt, Cape, Tail, Wing, Hair, Ear, Flap }

    public static class ChainKinds
    {
        // Budgets (tests check them): spring joints per hero, and simulated joints in one battle at once (beyond it a
        // hero's chains run kinematic, like Low quality). 10 heroes x 48 = 480 joints ~ 0.25 ms per frame on a phone core.
        public const int MaxJointsPerHero = 48;
        public const int BattleBudget = 480;

        public static ChainKind Of(int k) =>
            k < 20 ? ChainKind.Skirt : k < 30 ? ChainKind.Cape : k < 35 ? ChainKind.Tail : k < 40 ? ChainKind.Wing :
            k < 50 ? ChainKind.Hair : k < 55 ? ChainKind.Ear : ChainKind.Flap;

        public static int First(ChainKind c) => c switch { ChainKind.Skirt => 0, ChainKind.Cape => 20, ChainKind.Tail => 30, ChainKind.Wing => 35, ChainKind.Hair => 40, ChainKind.Ear => 50, _ => 55 };

        // Which body colliders a chain of this kind collides with (a skirt never meets the arms; hair never the calves).
        public static BodyGroup Mask(ChainKind c) => c switch
        {
            ChainKind.Skirt => BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Calves | BodyGroup.Ground,
            ChainKind.Cape => BodyGroup.Torso | BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Calves | BodyGroup.Extra | BodyGroup.Ground,
            ChainKind.Tail => BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Calves | BodyGroup.Ground,
            ChainKind.Wing => BodyGroup.Torso | BodyGroup.Arms | BodyGroup.Head,
            ChainKind.Hair => BodyGroup.Head | BodyGroup.Neck | BodyGroup.Torso | BodyGroup.Arms | BodyGroup.Ground,
            ChainKind.Ear => BodyGroup.Head,
            _ => BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Ground,
        };

        // Default motion per kind (UniVRM terms; a hero's SpringRigs entry overrides).
        public static SpringRig Defaults(ChainKind c) => c switch
        {
            ChainKind.Cape => new SpringRig { Stiffness = 2.2f, Drag = 0.55f, Gravity = 0.3f, JointRadius = 0.025f, MaxAngle = 20f },
            ChainKind.Tail => new SpringRig { Stiffness = 2.0f, Drag = 0.35f, Gravity = 0f, JointRadius = 0.04f },
            ChainKind.Wing => new SpringRig { Stiffness = 3.0f, Drag = 0.5f, Gravity = 0f, JointRadius = 0.03f },
            ChainKind.Hair => new SpringRig { Stiffness = 2.5f, Drag = 0.5f, Gravity = 0.1f, JointRadius = 0.02f },
            ChainKind.Ear => new SpringRig { Stiffness = 4.0f, Drag = 0.6f, Gravity = 0f, JointRadius = 0.015f },
            ChainKind.Flap => new SpringRig { Stiffness = 1.5f, Drag = 0.5f, Gravity = 0.2f, JointRadius = 0.02f },
            _ => new SpringRig(),
        };

        public static string Name(ChainKind c) => c.ToString().ToLowerInvariant();

        // The ground (holder y = 0, where the feet stand) as one huge sphere, so the solver needs no new shape: a cape or
        // tail stays above the floor when the hero kneels or lies down. Centre straight below the holder's origin.
        public const float GroundR = 1000f;
        public static SCapsule Ground(float x = 0f, float z = 0f) => new SCapsule { A = new SVec(x, -GroundR, z), B = new SVec(x, -GroundR, z), R = GroundR };
    }
}
