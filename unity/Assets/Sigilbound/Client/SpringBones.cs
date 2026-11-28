// Spring bones at runtime (character TD pilot 2026-11-28; physics standard, docs/sigilbound/art/PHYSICS.md): capes,
// skirts, robes, coat tails, tails, hair, ears and flaps that swing and slide off the body instead of being glued to it.
// The chains are nodes sb_<chain>_<bone> in the hero's GLB (added by blenderforge/physics_rig.py or the pilot's
// spring_rig.py; the last node of each chain is its tip; the chain number gives its kind, Core/Rig/RigChains.cs).
// Each kind runs its own solver (Core/SpringSolver.cs, after UniVRM's VRMSpringBone, MIT) with its own settings
// (Core/Kit/SpringRigs.cs) against its own groups of the hero's body colliders (Client/BodyPhysics.cs, the library's
// physics standard Core/Rig/RigPhysics.cs; groups and kinds Core/Rig/RigChains.cs): a skirt meets the hips and legs,
// hair the head, neck, chest and upper arms.
// Runs after the clip, RigExtras and DigitigradeLegs (execution order), in the model holder's own space, so the
// battle's moves, scale and camera never feed into the cloth and the result matches the offline previews.
// Low quality, or more simulated joints in the battle than ChainKinds.BattleBudget: kinematic (no swing, the cloth
// still pushed out of the body), a handful of capsule tests.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    [DefaultExecutionOrder(200)]
    public sealed class SpringBones : MonoBehaviour
    {
        sealed class Group
        {
            public ChainKind Kind; public SpringSolver Solver;
            public Transform[] Joints, Anchors; public SQuat[] ARot; public SVec[] APos;
            public int[] Shape; public float[] R;          // body shapes this kind collides with (radius override or <= 0)
            public Transform[,] Extra; public float[] ExtraR; // optional foot capsules / hip sphere (SpringRig.FootR, HipR)
            public SCapsule[] Caps; public bool Ground;
        }

        Transform _space;
        BodyPhysics _body;
        readonly List<Group> _groups = new List<Group>();
        int _joints;
        bool _counted;

        static int s_active;   // simulated joints of all enabled SpringBones (battle budget)
        public static int ActiveJoints => s_active;
        public int Joints => _joints;
        public bool Kinematic { get; private set; }
        public static System.Func<bool> LowQuality = () => Quality.I != null && Quality.I.Tier == 0;

        public static bool Needed(Transform root) => FirstChainRoot(root) != null;
        static Transform FirstChainRoot(Transform t)
        {
            if (t.name.StartsWith("sb_") && t.name.EndsWith("_0")) return t;
            foreach (Transform c in t) { var f = FirstChainRoot(c); if (f != null) return f; }
            return null;
        }

        static bool Parse(string n, out int chain, out int bone)
        {
            chain = bone = -1;
            if (!n.StartsWith("sb_")) return false;
            var p = n.Split('_');
            return p.Length == 3 && int.TryParse(p[1], out chain) && int.TryParse(p[2], out bone);
        }

        // model: the holder the GLB was instantiated under (unscaled glTF space = the previews' space).
        // body: the hero's body collider set (made here if the caller has none).
        public void Init(Transform model, string heroId, BodyPhysics body = null)
        {
            _space = model;
            _body = body != null ? body : GetComponent<BodyPhysics>();
            if (_body == null) { _body = gameObject.AddComponent<BodyPhysics>(); _body.Init(model, RigProfiles.Legacy(heroId)); }
            var chains = new SortedDictionary<int, SortedDictionary<int, Transform>>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (Parse(t.name, out int c, out int b)) { if (!chains.TryGetValue(c, out var d)) chains[c] = d = new SortedDictionary<int, Transform>(); d[b] = t; }
            var byKind = new SortedDictionary<ChainKind, List<List<Transform>>>();
            foreach (var kv in chains)
            {
                var k = ChainKinds.Of(kv.Key);
                if (!byKind.TryGetValue(k, out var l)) byKind[k] = l = new List<List<Transform>>();
                l.Add(new List<Transform>(kv.Value.Values));
            }
            Count(false); _groups.Clear(); _joints = 0;
            foreach (var kv in byKind) _groups.Add(MakeGroup(model, heroId, kv.Key, kv.Value));
            if (isActiveAndEnabled) Count(true); // AddComponent ran OnEnable before the chains existed
        }

        Group MakeGroup(Transform model, string heroId, ChainKind kind, List<List<Transform>> chains)
        {
            var joints = new List<Transform>(); var parent = new List<int>(); var anchorIdx = new List<int>(); var anchors = new List<Transform>();
            var lpos = new List<SVec>(); var lrot = new List<SQuat>(); var axis = new List<SVec>();
            foreach (var bones in chains)
                for (int s = 0; s < bones.Count - 1; s++) // the last node is the tip
                {
                    var t = bones[s];
                    if (s == 0)
                    {
                        int ai = anchors.IndexOf(t.parent); if (ai < 0) { anchors.Add(t.parent); ai = anchors.Count - 1; }
                        parent.Add(-1); anchorIdx.Add(ai);
                    }
                    else { parent.Add(joints.Count - 1); anchorIdx.Add(-1); }
                    joints.Add(t); lpos.Add(V(t.localPosition)); lrot.Add(Q(t.localRotation)); axis.Add(V(bones[s + 1].localPosition));
                }
            var rig = SpringRigs.Of(heroId, kind);
            var g = new Group { Kind = kind, Joints = joints.ToArray(), Anchors = anchors.ToArray() };
            g.Solver = new SpringSolver(parent.ToArray(), anchorIdx.ToArray(), lpos.ToArray(), lrot.ToArray(), axis.ToArray(), rig);
            g.ARot = new SQuat[g.Anchors.Length]; g.APos = new SVec[g.Anchors.Length];
            var mask = rig.Mask != 0 ? (BodyGroup)rig.Mask : ChainKinds.Mask(kind);
            var shape = new List<int>(); var rr = new List<float>();
            for (int i = 0; i < _body.Bodies.Count; i++)
            {
                var grp = RigPhysics.Group(_body.Bodies[i].Name);
                if ((grp & mask) == 0) continue;
                float r = grp == BodyGroup.Thighs && rig.ThighR > 0 ? rig.ThighR : grp == BodyGroup.Calves && rig.CalfR > 0 ? rig.CalfR : _body.Bodies[i].R + rig.Inflate;
                shape.Add(i); rr.Add(r);
            }
            g.Shape = shape.ToArray(); g.R = rr.ToArray();
            var ex = new List<(Transform, Transform, float)>();
            foreach (var s in new[] { "l", "r" })
            {
                var fo = BodyShaper.Find(model, "foot_" + s); var ba = BodyShaper.Find(model, "ball_" + s);
                if (fo != null && ba != null && rig.FootR > 0) ex.Add((fo, ba, rig.FootR));
            }
            var pel = BodyShaper.Find(model, "pelvis");
            if (pel != null && rig.HipR > 0) ex.Add((pel, pel, rig.HipR));
            g.Extra = new Transform[ex.Count, 2]; g.ExtraR = new float[ex.Count];
            for (int i = 0; i < ex.Count; i++) { g.Extra[i, 0] = ex[i].Item1; g.Extra[i, 1] = ex[i].Item2; g.ExtraR[i] = ex[i].Item3; }
            g.Ground = (mask & BodyGroup.Ground) != 0;
            g.Caps = new SCapsule[g.Shape.Length + ex.Count + (g.Ground ? 1 : 0)];
            _joints += g.Joints.Length;
            return g;
        }

        void Count(bool on)
        {
            if (on == _counted) return;
            _counted = on; s_active += on ? _joints : -_joints;
        }

        void OnEnable() { foreach (var g in _groups) g.Solver.Reset(); Count(true); if (!s_order.Contains(this)) s_order.Add(this); }
        void OnDisable() { Count(false); s_order.Remove(this); }

        void LateUpdate()
        {
            if (_space == null || _groups.Count == 0) return;
            // over the battle budget: the heroes enabled last go kinematic (they still keep cloth out of the body)
            Kinematic = LowQuality() || s_active > ChainKinds.BattleBudget && _joints > 0 && OverBudget();
            var inv = Quaternion.Inverse(_space.rotation);
            foreach (var g in _groups)
            {
                for (int i = 0; i < g.Anchors.Length; i++)
                {
                    g.ARot[i] = Q(inv * g.Anchors[i].rotation);
                    g.APos[i] = V(_space.InverseTransformPoint(g.Anchors[i].position));
                }
                int n = 0;
                for (int i = 0; i < g.Shape.Length; i++)
                {
                    var b = _body.Bodies[g.Shape[i]];
                    g.Caps[n++] = new SCapsule { A = V(_space.InverseTransformPoint(b.A.position)), B = V(_space.InverseTransformPoint(b.B.position)), R = g.R[i] };
                }
                for (int i = 0; i < g.ExtraR.Length; i++)
                    g.Caps[n++] = new SCapsule { A = V(_space.InverseTransformPoint(g.Extra[i, 0].position)), B = V(_space.InverseTransformPoint(g.Extra[i, 1].position)), R = g.ExtraR[i] };
                if (g.Ground) g.Caps[n++] = ChainKinds.Ground();
                g.Solver.Update(Time.deltaTime, g.ARot, g.APos, g.Caps, n, Kinematic);
                for (int i = 0; i < g.Joints.Length; i++) g.Joints[i].localRotation = U(g.Solver.Local[i]);
            }
        }

        // Deterministic choice of who drops to kinematic: instances in enable order fill the budget first.
        static readonly List<SpringBones> s_order = new List<SpringBones>();
        bool OverBudget()
        {
            s_order.RemoveAll(x => x == null);
            int sum = 0;
            foreach (var x in s_order) { sum += x._joints; if (x == this) return sum > ChainKinds.BattleBudget; }
            return false;
        }

        static SVec V(Vector3 v) => new SVec(v.x, v.y, v.z);
        static SQuat Q(Quaternion q) => new SQuat(q.x, q.y, q.z, q.w);
        static Quaternion U(SQuat q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
