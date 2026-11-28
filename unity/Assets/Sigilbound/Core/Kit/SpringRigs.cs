// Spring-bone settings per hero (character TD pilot 2026-11-28). The chains themselves live in the hero's GLB
// (nodes sb_<chain>_<bone>, the last node of a chain is its tip; blenderforge/spring_rig.py adds them and moves
// the skirt/robe/coat weights onto them). Client/SpringBones.cs simulates them with Core/SpringSolver.cs, the
// thighs and calves as capsule colliders. The preview tool reads these numbers from this file, so keep the
// one-line `["id"] = new SpringRig { ... }` form.
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public sealed class SpringRig
    {
        public float Stiffness = 1.5f;   // pull back to the rest direction (UniVRM stiffnessForce, per second)
        public float Drag = 0.45f;       // velocity kept per step = 1 - Drag
        public float Gravity = 0f;       // downward pull (m/s per step scale, UniVRM gravityPower)
        public float JointRadius = 0.03f;// cloth thickness at each chain joint (m, model space)
        public float ThighR = 0.09f;     // capsule radius thigh -> calf (m)
        public float CalfR = 0.07f;      // capsule radius calf -> foot (m)
        public float FootR = 0f;         // capsule radius foot -> ball (toes under a hem), 0 = none
        public float HipR = 0f;          // optional sphere at the pelvis joint (m), 0 = none
    }

    public static class SpringRigs
    {
        public static readonly Dictionary<string, SpringRig> ById = new Dictionary<string, SpringRig>
        {
            ["jasper"] = new SpringRig { Stiffness = 1.5f, Drag = 0.45f, Gravity = 0f, JointRadius = 0.03f, ThighR = 0.09f, CalfR = 0.07f },
            // the separate-parts cape (blenderforge/accessory_attach.py, 5 chains x 4 from spine_03): a little looser than a
            // skirt so it trails; the hip sphere keeps it off the tail and seat, the legs push it back as he strides
            ["augustin"] = new SpringRig { Stiffness = 1.2f, Drag = 0.4f, Gravity = 0f, JointRadius = 0.035f, ThighR = 0.11f, CalfR = 0.08f, HipR = 0.15f },
        };

        public static SpringRig Of(string heroId) => heroId != null && ById.TryGetValue(heroId, out var r) ? r : new SpringRig();
    }
}
