// The physics standard of a library rig (Core/Rig/RigPhysics.cs; owner decision 2026-11-28): the body collider set
// as Unity capsules on the bones (triggers on a kinematic body: hits, picks and effects can query them, nothing is
// pushed around), and the profile's chains simulated by Core/SpringSolver with those same capsules as obstacles.
// Runs after the clip and HumanoidRig's extras (wing flutter: a chain's swing is added on top of whatever the bone
// already holds), before SpringBones. Low quality: chains go kinematic (SpringSolver's own Low mode).
using System.Collections.Generic;
using Sigilbound.Core;
using UnityEngine;

namespace Sigilbound.Client
{
    [DefaultExecutionOrder(150)]
    public sealed class BodyPhysics : MonoBehaviour
    {
        public struct Body { public string Name; public Transform A, B; public float R; public CapsuleCollider Collider; }
        public readonly List<Body> Bodies = new List<Body>();
        public readonly Dictionary<string, Transform> Sockets = new Dictionary<string, Transform>();
        sealed class Chain { public SpringSolver Solver; public Transform[] Joints; public Quaternion[] Rest, Base, Written; public Transform[] Anchors; public SQuat[] ARot; public SVec[] APos; }
        readonly List<Chain> _chains = new List<Chain>();
        Transform _space; SCapsule[] _caps;
        public int ChainJoints { get { int n = 0; foreach (var c in _chains) n += c.Joints.Length; return n; } }

        // space: the model holder (unscaled glTF metres); called on the rest pose, before the holder is scaled.
        public void Init(Transform space, RigProfile p)
        {
            _space = space;
            float girth = RigPhysics.SkeletonGirth(p.Skeleton) * p.Girth;
            foreach (var c in p.Colliders)
            {
                var a = BodyShaper.Find(space, c.Bone); if (a == null) continue;
                var b = c.Tip != null ? BodyShaper.Find(space, c.Tip) : null;
                if (c.Tip != null && b == null) continue;
                float len = b != null ? Vector3.Distance(space.InverseTransformPoint(a.position), space.InverseTransformPoint(b.position))
                                      : a.parent != null ? Vector3.Distance(space.InverseTransformPoint(a.position), space.InverseTransformPoint(a.parent.position)) : 0.1f;
                float r = Mathf.Clamp(RigPhysics.Radius(p.Id, c, len, girth), 0.02f, 0.6f);   // measured girth when the hero has it
                var col = a.gameObject.AddComponent<CapsuleCollider>();
                col.isTrigger = true; col.radius = r;
                if (b != null)
                {
                    var tip = a.InverseTransformPoint(b.position);   // the bone's local frame (no scale between bones)
                    var ax = new Vector3(Mathf.Abs(tip.x), Mathf.Abs(tip.y), Mathf.Abs(tip.z));
                    col.direction = ax.x >= ax.y && ax.x >= ax.z ? 0 : ax.y >= ax.z ? 1 : 2;
                    col.center = tip * 0.5f; col.height = tip.magnitude + 2f * r;
                }
                else { col.center = Vector3.zero; col.height = 2f * r; col.direction = 1; }
                Bodies.Add(new Body { Name = c.Name, A = a, B = b ?? a, R = r, Collider = col });
            }
            // standard sockets (socket_<name>): attachment points for weapons, shields, back gear, headgear
            foreach (var so in RigPhysics.StandardSockets)
            {
                var bone = BodyShaper.Find(space, so.Bone); if (bone == null || bone.Find("socket_" + so.Name) != null) continue;
                var go = new GameObject("socket_" + so.Name).transform; go.SetParent(bone, false);
                go.position = bone.position + space.TransformVector(new Vector3(so.X, so.Y, so.Z));   // the holder is unscaled here (model metres)
                Sockets[so.Name] = go;
            }
            var rb = gameObject.GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            _caps = new SCapsule[Bodies.Count];

            foreach (var spec in p.Chains)
            {
                var joints = new List<Transform>();
                foreach (var n in spec.Bones) { var t = BodyShaper.Find(space, n); if (t != null) joints.Add(t); }
                if (joints.Count == 0) continue;
                int n2 = joints.Count;
                var parent = new int[n2]; var anchor = new int[n2]; var lpos = new SVec[n2]; var lrot = new SQuat[n2]; var axis = new SVec[n2];
                var anchors = new List<Transform>();
                for (int i = 0; i < n2; i++)
                {
                    var t = joints[i];
                    bool root = i == 0 || joints[i - 1] != t.parent;
                    if (root) { int ai = anchors.IndexOf(t.parent); if (ai < 0) { anchors.Add(t.parent); ai = anchors.Count - 1; } parent[i] = -1; anchor[i] = ai; }
                    else { parent[i] = i - 1; anchor[i] = -1; }
                    lpos[i] = V(t.localPosition); lrot[i] = Q(t.localRotation);
                    // to the next joint, or for a leaf TipLength along the direction it hangs from its parent
                    Vector3 ax = i + 1 < n2 && joints[i + 1].parent == t ? joints[i + 1].localPosition
                               : Quaternion.Inverse(t.localRotation) * (t.localPosition.sqrMagnitude > 1e-8f ? t.localPosition.normalized : Vector3.up) * spec.TipLength;
                    axis[i] = V(ax);
                }
                var rig = new SpringRig { Stiffness = spec.Stiffness, Drag = spec.Drag, Gravity = spec.Gravity, JointRadius = spec.Radius, ThighR = 0, CalfR = 0 };
                var ch = new Chain { Solver = new SpringSolver(parent, anchor, lpos, lrot, axis, rig), Joints = joints.ToArray(), Anchors = anchors.ToArray() };
                ch.Rest = new Quaternion[n2]; ch.Base = new Quaternion[n2]; ch.Written = new Quaternion[n2];
                for (int i = 0; i < n2; i++) ch.Rest[i] = ch.Base[i] = ch.Written[i] = joints[i].localRotation;
                ch.ARot = new SQuat[ch.Anchors.Length]; ch.APos = new SVec[ch.Anchors.Length];
                _chains.Add(ch);
            }
            Debug.Log($"SBPHYS {p.Id}: {Bodies.Count} body colliders (girth {girth:0.00}), {_chains.Count} chains, {ChainJoints} chain joints, {p.Rigid.Count} rigid items, {Sockets.Count} sockets");
        }

        void OnEnable() { foreach (var c in _chains) c.Solver.Reset(); }

        void LateUpdate()
        {
            if (_space == null || _chains.Count == 0) return;
            var inv = Quaternion.Inverse(_space.rotation);
            for (int i = 0; i < Bodies.Count; i++)
                _caps[i] = new SCapsule { A = V(_space.InverseTransformPoint(Bodies[i].A.position)), B = V(_space.InverseTransformPoint(Bodies[i].B.position)), R = Bodies[i].R };
            bool low = SpringBones.LowQuality();
            foreach (var c in _chains)
            {
                for (int i = 0; i < c.Anchors.Length; i++) { c.ARot[i] = Q(inv * c.Anchors[i].rotation); c.APos[i] = V(_space.InverseTransformPoint(c.Anchors[i].position)); }
                c.Solver.Update(Time.deltaTime, c.ARot, c.APos, _caps, _caps.Length, low);
                // the swing on top of what the bone holds now (a flutter, a clip key or its rest); a bone nothing rewrote
                // since last frame still holds our own output, so the base from then is used (no compounding, L002)
                for (int i = 0; i < c.Joints.Length; i++)
                {
                    var cur = c.Joints[i].localRotation;
                    if (Quaternion.Angle(cur, c.Written[i]) > 0.01f) c.Base[i] = cur;
                    c.Written[i] = c.Joints[i].localRotation = c.Base[i] * (Quaternion.Inverse(c.Rest[i]) * U(c.Solver.Local[i]));
                }
            }
        }

        static SVec V(Vector3 v) => new SVec(v.x, v.y, v.z);
        static SQuat Q(Quaternion q) => new SQuat(q.x, q.y, q.z, q.w);
        static Quaternion U(SQuat q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
