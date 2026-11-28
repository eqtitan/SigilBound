// Body colliders and physics chains as a base standard of every library rig (owner decision 2026-11-28: "Add
// colliders as a base standard for all models moving forward and game physics to everything").
// Bone-name based, like the rest of the library, so it maps onto any library skeleton and merges with the shared
// physics API (feat/physics-standard, docs/sigilbound/art/PHYSICS.md) by bone names: a collider is a capsule from a
// bone to its tip bone (or a sphere on one bone), a chain is a list of bones simulated by Core/SpringSolver with
// the body colliders as obstacles, and rigid items are bound 100% to one bone (blenderforge/prop_rigid.py).
// Sizes are fractions of the bone's own length times the profile's girth, so they follow each hero's build.
// Plain C#: the tests read the same tables; Client/BodyPhysics.cs builds them at runtime.
using System.Collections.Generic;
using System.Linq;

namespace Sigilbound.Core
{
    public sealed class ColliderSpec
    {
        public string Name, Bone, Tip;   // capsule Bone -> Tip; Tip null = sphere on Bone
        public float Radius;             // fraction of the Bone -> Tip length (sphere: of the Bone -> parent length)
        public ColliderSpec(string name, string bone, string tip, float radius) { Name = name; Bone = bone; Tip = tip; Radius = radius; }
    }

    public enum ChainKind { Hair, Cape, Skirt, Sash, Tail, Ears, Chain, Pouch, Banner, SoftWings }

    public sealed class ChainSpec
    {
        public ChainKind Kind;
        public string[] Bones;           // root first; a leaf's tip is TipLength along its bone (model metres)
        public float Stiffness = 1.2f, Drag = 0.4f, Gravity = 0f, Radius = 0.03f, TipLength = 0.25f;
    }

    public enum RigidKind { Weapon, Shield, Headgear }

    public sealed class RigidSpec
    {
        public RigidKind Kind; public string Bone;
        public RigidSpec(RigidKind kind, string bone) { Kind = kind; Bone = bone; }
    }

    // A named attachment point on a bone (weapons, shields, back gear, headgear): offset in model metres along the model axes
    // (glTFast space: X mirrored from glTF), plus the grip pose a held item asks of that hand.
    public sealed class SocketSpec
    {
        public string Name, Bone; public float X, Y, Z;
        public SocketSpec(string name, string bone, float x = 0, float y = 0, float z = 0) { Name = name; Bone = bone; X = x; Y = y; Z = z; }
    }
    public enum GripPose { Fist, OneHandSword, TwoHandHaft, Staff, Bow, OpenCast }

    public static class RigPhysics
    {
        // Standard sockets on every library rig (sockets plan, SKELETON_LIBRARY.md 3.2).
        public static readonly SocketSpec[] StandardSockets =
        {
            new SocketSpec("hand_r", "hand_r"), new SocketSpec("hand_l", "hand_l"),
            new SocketSpec("back", "spine_03", 0, 0.05f, -0.16f), new SocketSpec("hip_l", "pelvis", -0.17f, 0, 0), new SocketSpec("hip_r", "pelvis", 0.17f, 0, 0),
            new SocketSpec("shield_arm", "lowerarm_l", 0, 0.12f, 0), new SocketSpec("head_top", "Head", 0, 0.2f, 0),
        };

        // The standard body set: head, neck, chest, spine, hips, upper and lower arms, thighs, calves.
        public static List<ColliderSpec> BodySet(SkeletonKind k)
        {
            var o = new List<ColliderSpec>
            {
                new ColliderSpec("head", "Head", null, 1.1f),
                new ColliderSpec("neck", "neck_01", "Head", 0.45f),
                new ColliderSpec("chest", "spine_03", "neck_01", 0.8f),
                new ColliderSpec("spine", "spine_01", "spine_03", 0.6f),
                new ColliderSpec("hips", "pelvis", "spine_01", 1.3f),
            };
            foreach (var s in new[] { "l", "r" })
            {
                o.Add(new ColliderSpec("upperarm_" + s, "upperarm_" + s, "lowerarm_" + s, 0.2f));
                o.Add(new ColliderSpec("lowerarm_" + s, "lowerarm_" + s, "hand_" + s, 0.17f));
                o.Add(new ColliderSpec("thigh_" + s, "thigh_" + s, "calf_" + s, 0.22f));
                // a digitigrade calf ends at the hock (the metatarsal), a plantigrade one at the ankle
                o.Add(new ColliderSpec("calf_" + s, "calf_" + s, k == SkeletonKind.BipedDigitigrade ? "meta_" + s : "foot_" + s, 0.17f));
            }
            return o;
        }

        // Girth per skeleton (times the hero's own GaitAmount-independent Girth): a brute is broad.
        public static float SkeletonGirth(SkeletonKind k) => k == SkeletonKind.Brute ? 1.45f : 1f;

        public static readonly string[] ChainKindsThatMove = { "hair/braids", "capes", "skirts", "sashes", "tails", "ears", "chains", "pouches", "banners", "soft wings" };
    }
}
