// The skeleton library's data side (docs/sigilbound/art/SKELETON_LIBRARY.md): which standard skeleton each migrated
// hero is built on, how it moves (gait style as muscle-space offsets and tempo), its joint limits (muscle ranges) and
// the add-on chains (wings, tails, ears, hair, cloth) that hang off the standard bones.
// A hero with a profile here plays the shared clips as Unity Humanoid (muscle-space) clips on a runtime avatar
// (Client/HumanoidRig.cs). Every other hero keeps the legacy path (one rig, clips by bone name, RigExtras /
// DigitigradeLegs) until its batch migrates: Migrated(id) is the per-hero flag.
// Plain C#: the tests read the same table.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilbound.Core
{
    public enum SkeletonKind { Biped, BipedDigitigrade, Brute, MultiLeg }
    public enum GaitStyle { Neutral, Heavy, Light, Prowling, Regal }
    public enum AddonKind { Tail, Wings, Ears, Hair, Cloth }

    // A clamp on one Unity muscle (HumanTrait.MuscleName spelling), in normalized muscle units (-1..1).
    public readonly struct MuscleRange
    {
        public readonly string Muscle; public readonly float Min, Max;
        public MuscleRange(string muscle, float min, float max) { Muscle = muscle; Min = min; Max = max; }
    }

    // A chain of extra bones attached to a standard bone. Wings and ears flutter (procedural), tails, hair and cloth
    // swing (Core/SpringSolver via spring nodes sb_*); the library never maps them to human bones.
    public sealed class AddonChain
    {
        public AddonKind Kind; public string[] Bones; public float Amplitude = 12f, Frequency = 1.6f;
    }

    public sealed class GaitSpec
    {
        public GaitStyle Style;
        public float Tempo = 1f;        // locomotion clip speed (walk, run)
        public float IdleTempo = 1f;    // idle and ready loops
        public float ActionWeight = 0.35f; // how much of the posture stays on during strikes, casts and hits
        public readonly Dictionary<string, float> Offsets = new Dictionary<string, float>(); // muscle -> added value
    }

    public sealed class RigProfile
    {
        public string Id;
        public SkeletonKind Skeleton = SkeletonKind.Biped;
        public GaitStyle Gait = GaitStyle.Neutral;
        public string[] ClipSets = { "biped" };            // searched in order for a clip name
        public readonly List<MuscleRange> Limits = new List<MuscleRange>();
        public readonly List<AddonChain> Addons = new List<AddonChain>();
        public readonly Dictionary<string, string> Bones = new Dictionary<string, string>(); // human bone -> node (overrides the mapper)
        public float GaitAmount = 1f;                       // scales the gait's offsets for this hero
        public float HockFold = 0f;                         // digitigrade: metatarsal fold per degree of knee bend
        // Physics standard (Core/Rig/RigPhysics.cs): body colliders from the skeleton, chains for what should move,
        // rigid binding for weapons, shields and headgear.
        public float Girth = 1f;                            // collider girth on top of the skeleton's (RigPhysics.SkeletonGirth)
        public readonly List<ChainSpec> Chains = new List<ChainSpec>();
        public readonly List<RigidSpec> Rigid = new List<RigidSpec>();
        public List<ColliderSpec> Colliders => RigPhysics.BodySet(Skeleton);
    }

    public static class RigProfiles
    {
        // Runtime switch for the before/after tour (ShotsMode): true plays every hero on the legacy path.
        public static bool ForceLegacy;

        // Human bones a complete library biped maps (Unity humanoid; fingers optional per profile).
        public static readonly string[] BipedBones =
        {
            "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes", "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
        };
        public static readonly string[] FingerBones = new[] { "Left", "Right" }
            .SelectMany(s => new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.SelectMany(f => new[] { "Proximal", "Intermediate", "Distal" }.Select(p => s + f + p))).ToArray();

        // Natural ranges every library skeleton starts from (on top of Unity's own muscle limits).
        static IEnumerable<MuscleRange> BaseLimits(SkeletonKind k)
        {
            yield return new MuscleRange("Neck Turn Left-Right", -0.8f, 0.8f);
            yield return new MuscleRange("Head Turn Left-Right", -0.8f, 0.8f);
            switch (k)
            {
                case SkeletonKind.Brute:
                    // a stone colossus: a stiff trunk, short legs that can't split, a thick neck
                    yield return new MuscleRange("Spine Twist Left-Right", -0.55f, 0.55f);
                    yield return new MuscleRange("Chest Twist Left-Right", -0.55f, 0.55f);
                    yield return new MuscleRange("UpperChest Twist Left-Right", -0.55f, 0.55f);
                    yield return new MuscleRange("Left Upper Leg In-Out", -0.6f, 0.6f);
                    yield return new MuscleRange("Right Upper Leg In-Out", -0.6f, 0.6f);
                    yield return new MuscleRange("Neck Nod Down-Up", -0.6f, 0.6f);
                    break;
                case SkeletonKind.BipedDigitigrade:
                    // the hock does the work the human ankle does: less ankle roll
                    yield return new MuscleRange("Left Foot Twist In-Out", -0.5f, 0.5f);
                    yield return new MuscleRange("Right Foot Twist In-Out", -0.5f, 0.5f);
                    break;
            }
        }

        public static GaitSpec Gait(GaitStyle s)
        {
            var g = new GaitSpec { Style = s };
            void O(string m, float v) { g.Offsets[m] = v; }
            void Both(string m, float v) { O("Left " + m, v); O("Right " + m, v); }
            switch (s)
            {
                case GaitStyle.Heavy:       // weight carried low and forward, arms held off the body, slow tread
                    g.Tempo = 0.82f; g.IdleTempo = 0.78f; g.ActionWeight = 0.5f;
                    O("Spine Front-Back", 0.14f); O("Chest Front-Back", 0.08f);
                    Both("Arm Down-Up", 0.10f); Both("Shoulder Down-Up", -0.10f);
                    Both("Lower Leg Stretch", -0.10f); Both("Upper Leg Front-Back", 0.05f); Both("Upper Leg In-Out", 0.10f);
                    break;
                case GaitStyle.Light:       // springy and upright, quick feet
                    g.Tempo = 1.08f; g.IdleTempo = 1.06f;
                    O("UpperChest Front-Back", -0.05f); Both("Lower Leg Stretch", -0.04f); Both("Arm Down-Up", -0.03f);
                    break;
                case GaitStyle.Prowling:    // low and coiled, head level
                    g.Tempo = 0.95f; g.IdleTempo = 0.9f;
                    O("Spine Front-Back", 0.16f); O("Neck Nod Down-Up", -0.10f);
                    Both("Lower Leg Stretch", -0.22f); Both("Upper Leg Front-Back", 0.10f); Both("Foot Up-Down", 0.10f);
                    break;
                case GaitStyle.Regal:       // chest up, shoulders back and down, measured steps
                    g.Tempo = 0.9f; g.IdleTempo = 0.88f;
                    O("Spine Front-Back", -0.06f); O("Chest Front-Back", -0.06f); O("Neck Nod Down-Up", 0.05f);
                    Both("Shoulder Down-Up", -0.08f); Both("Shoulder Front-Back", -0.06f);
                    break;
            }
            return g;
        }

        static RigProfile P(string id, SkeletonKind k, GaitStyle gait, Action<RigProfile> more = null)
        {
            var p = new RigProfile { Id = id, Skeleton = k, Gait = gait };
            p.Limits.AddRange(BaseLimits(k));
            more?.Invoke(p);
            return p;
        }

        // The pilot (owner decision 2026-11-28): a human, a lion beast-folk, a goat with animal legs, a moth with
        // wings, and the Titan (brute). Every other hero joins in batches (SKELETON_LIBRARY.md, migration plan).
        public static readonly Dictionary<string, RigProfile> Table = new Dictionary<string, RigProfile>
        {
            ["ysolde"] = P("ysolde", SkeletonKind.Biped, GaitStyle.Light, p => p.Rigid.Add(new RigidSpec(RigidKind.Weapon, "hand_r"))),
            ["augustin"] = P("augustin", SkeletonKind.Biped, GaitStyle.Regal, p => { p.GaitAmount = 1.2f; p.Girth = 1.15f; p.Rigid.Add(new RigidSpec(RigidKind.Weapon, "hand_r")); }),
            ["corran"] = P("corran", SkeletonKind.BipedDigitigrade, GaitStyle.Light, p => { p.HockFold = 0.6f; p.GaitAmount = 0.8f; p.Rigid.Add(new RigidSpec(RigidKind.Weapon, "hand_r")); }),
            ["nyssa"] = P("nyssa", SkeletonKind.Biped, GaitStyle.Light, p =>
            {
                p.Addons.Add(new AddonChain { Kind = AddonKind.Wings, Bones = new[] { "wing_l", "wing_r" }, Amplitude = 14f, Frequency = 1.4f });
                // soft wings: each wing bone lags and swings on top of its flutter, kept off the body by the colliders
                p.Chains.Add(new ChainSpec { Kind = ChainKind.SoftWings, Bones = new[] { "wing_l" }, Stiffness = 0.9f, Drag = 0.3f, Gravity = 0.05f, Radius = 0.05f, TipLength = 0.35f });
                p.Chains.Add(new ChainSpec { Kind = ChainKind.SoftWings, Bones = new[] { "wing_r" }, Stiffness = 0.9f, Drag = 0.3f, Gravity = 0.05f, Radius = 0.05f, TipLength = 0.35f });
            }),
            ["titan"] = P("titan", SkeletonKind.Brute, GaitStyle.Heavy, p =>
            {
                p.ClipSets = new[] { "titan", "biped" }; p.GaitAmount = 0.8f; p.Girth = 1.1f;
                // the rock fists and the crystal ridges are stone: rigid on their bones (blenderforge/titan_moves.py)
                p.Rigid.Add(new RigidSpec(RigidKind.Weapon, "hand_l")); p.Rigid.Add(new RigidSpec(RigidKind.Weapon, "hand_r")); p.Rigid.Add(new RigidSpec(RigidKind.Headgear, "spine_03"));
            }),
        };

        public static RigProfile Of(string id) => id != null && Table.TryGetValue(id, out var p) ? p : null;
        public static bool Migrated(string id) => !ForceLegacy && Of(id) != null;

        // The physics standard on the legacy path (feat/physics-standard): a hero without a library rig (or whose
        // humanoid setup failed) still gets the body collider set - a plain biped profile (the Titan's brute girth kept),
        // no chains (its spring chains are sb_* nodes in its GLB, Client/SpringBones.cs).
        public static RigProfile Legacy(string id)
        {
            var t = Of(id);
            var p = new RigProfile { Id = id, Skeleton = t != null && t.Skeleton == SkeletonKind.Brute ? SkeletonKind.Brute : SkeletonKind.Biped, Gait = GaitStyle.Neutral };
            if (t != null) p.Girth = t.Girth;
            return p;
        }

        // Clip sets the editor bakes: set name -> source file under unity/Tools~/models (the reference rigs' clips).
        public static readonly (string Set, string Source, string Prefix)[] ClipSources =
        {
            ("biped", "ubc_anims.glb", null),
            ("titan", "heroes_hd/SB_Hero_titan_HD.glb", "Titan_"),
        };
        // Unity muscle name (HumanTrait.MuscleName) -> a Humanoid clip's curve name: finger muscles are spelled
        // "LeftHand.Index.1 Stretched" in clips, every other muscle as its name.
        public static string CurveName(string muscle)
        {
            var m = System.Text.RegularExpressions.Regex.Match(muscle ?? "", @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");
            return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : muscle;
        }

        // Unity's body muscles (HumanTrait.MuscleName, before the fingers), for checking the tables offline.
        public static readonly string[] BodyMuscles = new[]
        {
            "Spine Front-Back", "Spine Left-Right", "Spine Twist Left-Right", "Chest Front-Back", "Chest Left-Right", "Chest Twist Left-Right",
            "UpperChest Front-Back", "UpperChest Left-Right", "UpperChest Twist Left-Right", "Neck Nod Down-Up", "Neck Tilt Left-Right", "Neck Turn Left-Right",
            "Head Nod Down-Up", "Head Tilt Left-Right", "Head Turn Left-Right", "Left Eye Down-Up", "Left Eye In-Out", "Right Eye Down-Up", "Right Eye In-Out",
            "Jaw Close", "Jaw Left-Right",
        }.Concat(new[] { "Left", "Right" }.SelectMany(sd => new[] { "Upper Leg Front-Back", "Upper Leg In-Out", "Upper Leg Twist In-Out", "Lower Leg Stretch", "Lower Leg Twist In-Out", "Foot Up-Down", "Foot Twist In-Out", "Toes Up-Down" }.Select(x => sd + " " + x)))
         .Concat(new[] { "Left", "Right" }.SelectMany(sd => new[] { "Shoulder Down-Up", "Shoulder Front-Back", "Arm Down-Up", "Arm Front-Back", "Arm Twist In-Out", "Forearm Stretch", "Forearm Twist In-Out", "Hand Down-Up", "Hand In-Out" }.Select(x => sd + " " + x)))
         .ToArray();

        public const string ResourcesDir = "HumanoidClips"; // Resources/HumanoidClips/<set>/<clip>.anim
        public const float BakeFps = 30f;
    }
}
