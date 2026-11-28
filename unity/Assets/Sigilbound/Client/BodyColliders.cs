// The physics standard's body collider set at runtime (docs/sigilbound/art/PHYSICS.md): every high-detail hero gets
// one at load (HeroModels.BuildHd), whatever rig path drives it - the legacy Animation path today, the skeleton
// library's Animator later (it calls the same Init on the instance, at rest, by bone name). The shapes come from
// Core/Physics/ColliderSet.cs; this component only binds them to the bones and reports them in any space.
// No per-frame cost of its own: SpringBones (and anything else that wants the body, e.g. a future hit test) asks
// for the capsules when it needs them.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;
using Sigilbound.Core.Physics;

namespace Sigilbound.Client
{
    public sealed class BodyColliders : MonoBehaviour
    {
        public ColliderShape[] Shapes = new ColliderShape[0];
        public readonly List<string> Missing = new List<string>();
        Transform[] _a, _b; Vector3[] _sphereLocal;

        // model: the holder the GLB was instantiated under (unscaled glTF space). Call with the bones at rest.
        public void Init(Transform model, string heroId)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            SVec? Pos(string n) { if (n == null || !bones.TryGetValue(n, out var t)) return null; var p = model.InverseTransformPoint(t.position); return new SVec(p.x, p.y, p.z); }
            Missing.Clear();
            Shapes = ColliderSet.Build(heroId, Pos, Missing);
            _a = new Transform[Shapes.Length]; _b = new Transform[Shapes.Length]; _sphereLocal = new Vector3[Shapes.Length];
            for (int i = 0; i < Shapes.Length; i++)
            {
                _a[i] = bones[Shapes[i].BoneA];
                if (Shapes[i].Sphere)
                {   // the sphere's centre rides on its bone: store it in the bone's own space
                    var o = Shapes[i].Offset; var c = model.TransformPoint(model.InverseTransformPoint(_a[i].position) + new Vector3(o.X, o.Y, o.Z));
                    _sphereLocal[i] = _a[i].InverseTransformPoint(c);
                }
                else _b[i] = bones[Shapes[i].BoneB];
            }
        }

        public int Count => Shapes.Length;

        // Shape i as a capsule in `space` (the model holder for SpringBones), from the bones' current pose.
        public SCapsule Capsule(int i, Transform space)
        {
            Vector3 a = Shapes[i].Sphere ? space.InverseTransformPoint(_a[i].TransformPoint(_sphereLocal[i])) : space.InverseTransformPoint(_a[i].position);
            Vector3 b = Shapes[i].Sphere ? a : space.InverseTransformPoint(_b[i].position);
            return new SCapsule { A = new SVec(a.x, a.y, a.z), B = new SVec(b.x, b.y, b.z), R = Shapes[i].Radius };
        }

        public int IndexOf(string name) { for (int i = 0; i < Shapes.Length; i++) if (Shapes[i].Name == name) return i; return -1; }
    }
}
