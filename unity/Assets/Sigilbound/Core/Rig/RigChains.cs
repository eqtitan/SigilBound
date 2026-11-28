// Spring chains baked into a hero's GLB (feat/physics-standard, docs/sigilbound/art/PHYSICS.md) on top of the library's
// physics standard (Core/Rig/RigPhysics.cs): the legacy roster's capes, skirts, tails and hair are nodes sb_<k>_<i>
// added by blenderforge/physics_rig.py (the pilot's spring_rig.py for skirts), simulated by Client/SpringBones.cs
// against the hero's body colliders (Client/BodyPhysics.cs). The chain number k says the kind, so the data needs no
// other field. Each kind has its own collider groups, motion defaults and the shared budgets.
// Collider groups per chain after UniVRM's VRMSpringBone collider groups (MIT, VRM Consortium).
namespace Sigilbound.Core
{
    public static class ChainKinds
    {
        // Budgets (tests check them): spring joints per hero, and simulated joints in one battle at once (beyond it a
        // hero's chains run kinematic, like Low quality). 10 heroes x 48 = 480 joints ~ 0.2 ms per frame on CI.
        public const int MaxJointsPerHero = 48;
        public const int BattleBudget = 480;

        // 0-19 skirt/robe panels/coat tails, 20-29 cape, 30-34 tail, 35-39 soft wing, 40-49 hair, 50-54 ears,
        // 55-59 sash/belt flap/tassel, 60-63 pouch, 64-66 chain, 67-69 banner.
        public static ChainKind Of(int k) =>
            k < 20 ? ChainKind.Skirt : k < 30 ? ChainKind.Cape : k < 35 ? ChainKind.Tail : k < 40 ? ChainKind.SoftWings :
            k < 50 ? ChainKind.Hair : k < 55 ? ChainKind.Ears : k < 60 ? ChainKind.Sash : k < 64 ? ChainKind.Pouch : k < 67 ? ChainKind.Chain : ChainKind.Banner;

        public static int First(ChainKind c) => c switch
        {
            ChainKind.Skirt => 0, ChainKind.Cape => 20, ChainKind.Tail => 30, ChainKind.SoftWings => 35, ChainKind.Hair => 40,
            ChainKind.Ears => 50, ChainKind.Sash => 55, ChainKind.Pouch => 60, ChainKind.Chain => 64, _ => 67,
        };

        // Which body colliders a chain of this kind collides with (a skirt never meets the arms; hair never the calves).
        public static BodyGroup Mask(ChainKind c) => c switch
        {
            ChainKind.Skirt => BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Calves | BodyGroup.Ground,
            // no ground: a hero lying on its back squeezed the cape between back and floor (stretched; L029)
            ChainKind.Cape => BodyGroup.Torso | BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Calves | BodyGroup.Extra,
            ChainKind.Tail => BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Calves | BodyGroup.Ground,
            ChainKind.SoftWings => BodyGroup.Torso | BodyGroup.Arms | BodyGroup.Head,
            ChainKind.Hair => BodyGroup.Head | BodyGroup.Neck | BodyGroup.Torso | BodyGroup.Arms | BodyGroup.Ground,
            ChainKind.Ears => BodyGroup.Head,
            ChainKind.Banner => BodyGroup.Torso | BodyGroup.Hips,
            _ => BodyGroup.Hips | BodyGroup.Thighs | BodyGroup.Ground,   // sash, pouch, chain
        };

        // Default motion per kind (UniVRM terms; a hero's SpringRigs entry overrides).
        public static SpringRig Defaults(ChainKind c) => c switch
        {
            ChainKind.Cape => new SpringRig { Stiffness = 2.2f, Drag = 0.55f, Gravity = 0.3f, JointRadius = 0.025f, MaxAngle = 20f },
            ChainKind.Tail => new SpringRig { Stiffness = 2.0f, Drag = 0.35f, Gravity = 0f, JointRadius = 0.04f },
            ChainKind.SoftWings => new SpringRig { Stiffness = 3.0f, Drag = 0.5f, Gravity = 0f, JointRadius = 0.03f },
            ChainKind.Hair => new SpringRig { Stiffness = 2.5f, Drag = 0.5f, Gravity = 0.1f, JointRadius = 0.02f, MaxAngle = 35f },
            ChainKind.Ears => new SpringRig { Stiffness = 4.0f, Drag = 0.6f, Gravity = 0f, JointRadius = 0.015f, MaxAngle = 25f },
            ChainKind.Skirt => new SpringRig(),
            _ => new SpringRig { Stiffness = 1.5f, Drag = 0.5f, Gravity = 0.2f, JointRadius = 0.02f, MaxAngle = 40f },
        };

        public static string Name(ChainKind c) => c.ToString().ToLowerInvariant();

        // The ground (holder y = 0, where the feet stand) as one huge sphere, so the solver needs no new shape: a tail or
        // hem stays above the floor when the hero kneels. Centre straight below the holder's origin.
        public const float GroundR = 1000f;
        public static SCapsule Ground(float x = 0f, float z = 0f) => new SCapsule { A = new SVec(x, -GroundR, z), B = new SVec(x, -GroundR, z), R = GroundR };
    }
}
