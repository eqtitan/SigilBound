// Spring bones at runtime (character TD pilot 2026-11-28): skirts, robes and coat tails that swing and slide off
// the legs instead of being glued to them. The chains are nodes sb_<chain>_<bone> in the hero's GLB (added by
// blenderforge/spring_rig.py; the last node of each chain is its tip); the settings per hero are
// Core/Kit/SpringRigs.cs; the solver is Core/SpringSolver.cs (after UniVRM's VRMSpringBone, MIT).
// Runs after the clip, RigExtras and DigitigradeLegs (execution order), in the model holder's own space, so the
// battle's moves, scale and camera never feed into the cloth and the result matches the offline previews.
// Low quality: kinematic only (no swing, the cloth still pushed off the legs), a handful of capsule tests.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    [DefaultExecutionOrder(200)]
    public sealed class SpringBones : MonoBehaviour
    {
        Transform _space;
        Transform[] _joints, _anchors;
        Transform[,] _capEnds; float[] _capR;
        SpringSolver _solver;
        SQuat[] _aRot; SVec[] _aPos; SCapsule[] _caps;

        public int Joints => _joints?.Length ?? 0;
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
        public void Init(Transform model, string heroId)
        {
            _space = model;
            var chains = new SortedDictionary<int, SortedDictionary<int, Transform>>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (Parse(t.name, out int c, out int b)) { if (!chains.TryGetValue(c, out var d)) chains[c] = d = new SortedDictionary<int, Transform>(); d[b] = t; }
            var joints = new List<Transform>(); var parent = new List<int>(); var anchorIdx = new List<int>(); var anchors = new List<Transform>();
            var lpos = new List<SVec>(); var lrot = new List<SQuat>(); var axis = new List<SVec>();
            foreach (var kv in chains)
            {
                var bones = new List<Transform>(kv.Value.Values);
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
            }
            _joints = joints.ToArray(); _anchors = anchors.ToArray();
            _solver = new SpringSolver(parent.ToArray(), anchorIdx.ToArray(), lpos.ToArray(), lrot.ToArray(), axis.ToArray(), SpringRigs.Of(heroId));
            _aRot = new SQuat[_anchors.Length]; _aPos = new SVec[_anchors.Length];
            var rig = _solver.Rig; var ends = new List<(Transform, Transform, float)>();
            foreach (var s in new[] { "l", "r" })
            {
                var th = BodyShaper.Find(model, "thigh_" + s); var ca = BodyShaper.Find(model, "calf_" + s); var fo = BodyShaper.Find(model, "foot_" + s);
                if (th != null && ca != null && rig.ThighR > 0) ends.Add((th, ca, rig.ThighR));
                if (ca != null && fo != null && rig.CalfR > 0) ends.Add((ca, fo, rig.CalfR));
                var ba = BodyShaper.Find(model, "ball_" + s);
                if (fo != null && ba != null && rig.FootR > 0) ends.Add((fo, ba, rig.FootR));
            }
            var pel = BodyShaper.Find(model, "pelvis");
            if (pel != null && rig.HipR > 0) ends.Add((pel, pel, rig.HipR));
            _capEnds = new Transform[ends.Count, 2]; _capR = new float[ends.Count]; _caps = new SCapsule[ends.Count];
            for (int i = 0; i < ends.Count; i++) { _capEnds[i, 0] = ends[i].Item1; _capEnds[i, 1] = ends[i].Item2; _capR[i] = ends[i].Item3; }
        }

        void OnEnable() { _solver?.Reset(); }

        void LateUpdate()
        {
            if (_solver == null || _space == null) return;
            var inv = Quaternion.Inverse(_space.rotation);
            for (int i = 0; i < _anchors.Length; i++)
            {
                _aRot[i] = Q(inv * _anchors[i].rotation);
                _aPos[i] = V(_space.InverseTransformPoint(_anchors[i].position));
            }
            for (int i = 0; i < _caps.Length; i++)
                _caps[i] = new SCapsule { A = V(_space.InverseTransformPoint(_capEnds[i, 0].position)), B = V(_space.InverseTransformPoint(_capEnds[i, 1].position)), R = _capR[i] };
            _solver.Update(Time.deltaTime, _aRot, _aPos, _caps, _caps.Length, LowQuality());
            for (int i = 0; i < _joints.Length; i++) _joints[i].localRotation = U(_solver.Local[i]);
        }

        static SVec V(Vector3 v) => new SVec(v.x, v.y, v.z);
        static SQuat Q(Quaternion q) => new SQuat(q.x, q.y, q.z, q.w);
        static Quaternion U(SQuat q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
