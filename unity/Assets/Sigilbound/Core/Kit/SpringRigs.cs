// Spring-bone settings per hero (character TD pilot 2026-11-28; physics standard, docs/sigilbound/art/PHYSICS.md).
// The chains themselves live in the hero's GLB (nodes sb_<chain>_<bone>, the last node of a chain is its tip; the
// chain number says its kind, Core/Rig/RigChains.cs ChainKinds). Client/SpringBones.cs simulates them with
// Core/SpringSolver.cs against the hero's body colliders (Client/BodyPhysics.cs, Core/Rig/RigPhysics.cs). Lookup order:
// ["id:kind"] (e.g. "dorian:cape"), then ["id"], then the kind's defaults (ChainKinds.Defaults). The preview tool
// reads these numbers from this file, so keep the one-line `["key"] = new SpringRig { ... }` form.
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public sealed class SpringRig
    {
        public float Stiffness = 1.5f;   // pull back to the rest direction (UniVRM stiffnessForce, per second)
        public float Drag = 0.45f;       // velocity kept per step = 1 - Drag
        public float Gravity = 0f;       // downward pull (m/s per step scale, UniVRM gravityPower)
        public float JointRadius = 0.03f;// cloth thickness at each chain joint (m, model space)
        public float MaxAngle = 0f;      // swing limit from the animated rest direction (degrees), 0 = none
        // Collider overrides (0 = the body collider set's own radius / the kind's own collider groups):
        public float ThighR = 0f;        // thigh capsule radius (m)
        public float CalfR = 0f;         // calf capsule radius (m)
        public float FootR = 0f;         // extra capsule foot -> ball (toes under a hem), 0 = none
        public float HipR = 0f;          // extra sphere at the pelvis joint (m), 0 = none
        public int Mask = 0;             // BodyGroup bits this chain kind collides with, 0 = ChainKinds.Mask(kind)
    }

    public static class SpringRigs
    {
        public static readonly Dictionary<string, SpringRig> ById = new Dictionary<string, SpringRig>
        {
            // pilot (2026-11-28): thigh and calf capsules only, at the pilot's radii
            ["jasper"] = new SpringRig { Stiffness = 1.5f, Drag = 0.45f, Gravity = 0f, JointRadius = 0.03f, ThighR = 0.09f, CalfR = 0.07f, Mask = 192 },
        };

        // Settings for one chain kind of one hero.
        public static SpringRig Of(string heroId, ChainKind kind)
        {
            if (heroId != null && ById.TryGetValue(heroId + ":" + ChainKinds.Name(kind), out var k)) return k;
            if (heroId != null && ById.TryGetValue(heroId, out var r)) return r;
            return ChainKinds.Defaults(kind);
        }

        public static SpringRig Of(string heroId) => Of(heroId, ChainKind.Skirt);

        // True when the hero has any entry (tests: every entry belongs to a spring-rigged hero).
        public static bool Has(string heroId)
        {
            foreach (var key in ById.Keys) if (key == heroId || key.StartsWith(heroId + ":")) return true;
            return false;
        }

        public static string HeroOf(string key) { int i = key.IndexOf(':'); return i < 0 ? key : key.Substring(0, i); }
    }
}
