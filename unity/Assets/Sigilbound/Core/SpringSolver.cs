// Spring bones for cloth (skirts, robes, coat tails), tails and hair: a small Verlet solver after UniVRM's
// VRMSpringBone (MIT licence, Copyright (c) 2020 VRM Consortium, Copyright (c) 2018 Masataka SUMI for
// MToon; https://github.com/vrm-c/UniVRM). Self-contained and allocation-free so it runs (and is tested) outside
// Unity; Client/SpringBones.cs feeds it the animated pose and writes the bones back.
//
// Each joint is a bone with a fixed length: its head follows the parent, its tail is simulated. Per fixed 1/60 s
// step: tail += inertia (1 - Drag) + Stiffness pull toward the animated rest direction + Gravity, the length is
// restored, the tail is pushed out of every capsule collider (thighs, calves; two passes), and the bone is rotated from its
// rest direction onto the tail. Fixed steps make it deterministic for a given frame sequence and keep it the
// same as the Blender/numpy previews (scratchpad ctd/tools/springsim.py mirrors this file line by line).
// Kinematic mode (Low quality): no inertia or history, the rest direction pushed out of the colliders once per
// frame - cloth still slides off the legs, nothing swings.
using System;

namespace Sigilbound.Core
{
    public struct SVec
    {
        public float X, Y, Z;
        public SVec(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static SVec operator +(SVec a, SVec b) => new SVec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static SVec operator -(SVec a, SVec b) => new SVec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static SVec operator *(SVec a, float s) => new SVec(a.X * s, a.Y * s, a.Z * s);
        public static float Dot(SVec a, SVec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static SVec Cross(SVec a, SVec b) => new SVec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public SVec Normalized { get { float l = Length; return l > 1e-9f ? this * (1f / l) : new SVec(0, 0, 0); } }
    }

    public struct SQuat
    {
        public float X, Y, Z, W;
        public SQuat(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        public static readonly SQuat Identity = new SQuat(0, 0, 0, 1);
        public static SQuat operator *(SQuat a, SQuat b) => new SQuat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y, a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W, a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        public SQuat Inverse => new SQuat(-X, -Y, -Z, W);
        public SQuat Normalized { get { float l = (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W); return l > 1e-9f ? new SQuat(X / l, Y / l, Z / l, W / l) : Identity; } }
        public SVec Rotate(SVec v)
        {
            // v + 2w(q x v) + 2 q x (q x v)
            var q = new SVec(X, Y, Z); var t = SVec.Cross(q, v) * 2f;
            return v + t * W + SVec.Cross(q, t);
        }
        public static SQuat FromTo(SVec a, SVec b)
        {
            a = a.Normalized; b = b.Normalized; float d = SVec.Dot(a, b);
            if (d > 0.999999f) return Identity;
            if (d < -0.999999f)
            {
                var ax = SVec.Cross(new SVec(1, 0, 0), a); if (ax.Length < 1e-6f) ax = SVec.Cross(new SVec(0, 1, 0), a);
                ax = ax.Normalized; return new SQuat(ax.X, ax.Y, ax.Z, 0);
            }
            var c = SVec.Cross(a, b); return new SQuat(c.X, c.Y, c.Z, 1 + d).Normalized;
        }
    }

    public struct SCapsule { public SVec A, B; public float R; }

    public sealed class SpringSolver
    {
        public const float Step = 1f / 60f;
        public const int MaxSteps = 4;      // a slow frame catches up at most this many steps, then drops the rest
        public const float ResetDt = 0.25f;
        public const int CollisionPasses = 2; // push-out then length restore, twice (a tail between two capsules settles) // a longer gap (paused, off screen, first frame) restarts from rest

        readonly int[] _parent, _anchor;     // parent joint (-1: the chain root, then _anchor names its anchor)
        readonly SVec[] _lpos, _axis;        // head offset in the parent's space; tail offset in the joint's rest space
        readonly SQuat[] _lrot;              // rest local rotation
        readonly float[] _len;
        readonly SVec[] _cur, _prev, _head;
        readonly SQuat[] _rot;
        public readonly SQuat[] Local;       // output: local rotation per joint (relative to its parent)
        public readonly SpringRig Rig;
        bool _started; float _acc;
        public int Joints => _parent.Length;
        public int StepsTaken { get; private set; }

        public SpringSolver(int[] parent, int[] anchor, SVec[] lpos, SQuat[] lrot, SVec[] axis, SpringRig rig)
        {
            int n = parent.Length;
            _parent = parent; _anchor = anchor; _lpos = lpos; _lrot = lrot; _axis = axis; Rig = rig ?? new SpringRig();
            _len = new float[n]; for (int i = 0; i < n; i++) _len[i] = Math.Max(1e-5f, axis[i].Length);
            _cur = new SVec[n]; _prev = new SVec[n]; _head = new SVec[n]; _rot = new SQuat[n]; Local = new SQuat[n];
            for (int i = 0; i < n; i++) Local[i] = lrot[i];
        }

        public void Reset() { _started = false; _acc = 0; }

        // anchorRot/anchorPos: the chain roots' parents in model space (after the clip and RigExtras).
        // Returns the number of fixed steps run (0 when the frame was shorter than a step: the bones are re-aimed).
        public int Update(float dt, SQuat[] anchorRot, SVec[] anchorPos, SCapsule[] caps, int nCaps, bool kinematic)
        {
            StepsTaken = 0;
            if (kinematic) { Solve(anchorRot, anchorPos, caps, nCaps, 0f, true); _started = false; return 0; }
            if (!_started || dt > ResetDt || dt < 0)
            {
                _started = false; _acc = 0;
                Solve(anchorRot, anchorPos, caps, nCaps, Step, false); _started = true;
                return StepsTaken = 1;
            }
            _acc += dt; int steps = 0;
            while (_acc >= Step - 1e-7f && steps < MaxSteps) { Solve(anchorRot, anchorPos, caps, nCaps, Step, false); _acc -= Step; steps++; }
            if (steps == MaxSteps) _acc = 0;
            if (steps == 0) Aim(anchorRot, anchorPos);
            return StepsTaken = steps;
        }

        void Solve(SQuat[] anchorRot, SVec[] anchorPos, SCapsule[] caps, int nCaps, float dt, bool kinematic)
        {
            float drag = Rig.Drag, stiff = Rig.Stiffness * dt, grav = Rig.Gravity * dt, jr = Rig.JointRadius;
            for (int i = 0; i < _parent.Length; i++)
            {
                SQuat pr; SVec pp;
                if (_parent[i] < 0) { pr = anchorRot[_anchor[i]]; pp = anchorPos[_anchor[i]]; }
                else { pr = _rot[_parent[i]]; pp = _head[_parent[i]]; }
                var head = pp + pr.Rotate(_lpos[i]);
                var restR = (pr * _lrot[i]).Normalized;
                float L = _len[i];
                var restDir = restR.Rotate(_axis[i]);
                SVec next;
                if (kinematic || !_started) next = head + restDir;
                else next = _cur[i] + (_cur[i] - _prev[i]) * (1f - drag) + restDir * (stiff / L) + new SVec(0, -grav, 0);
                next = head + (next - head).Normalized * L;
                for (int it = 0; it < CollisionPasses; it++)
                for (int k = 0; k < nCaps; k++)
                {
                    var ab = caps[k].B - caps[k].A; float abl = SVec.Dot(ab, ab);
                    float t = abl > 1e-12f ? Math.Max(0f, Math.Min(1f, SVec.Dot(next - caps[k].A, ab) / abl)) : 0f;
                    var c = caps[k].A + ab * t; float rr = caps[k].R + jr; var dv = next - c; float dl = dv.Length;
                    if (dl < rr)
                    {
                        next = c + (dl > 1e-9f ? dv * (1f / dl) : new SVec(0, 0, 0)) * rr;
                        next = head + (next - head).Normalized * L;
                    }
                }
                if (!kinematic)
                {
                    _prev[i] = _started ? _cur[i] : next;
                    _cur[i] = next;
                }
                var q = (SQuat.FromTo(restDir, next - head) * restR).Normalized;
                _rot[i] = q; _head[i] = head;
                Local[i] = (pr.Inverse * q).Normalized;
            }
        }

        // No step due this frame: keep the simulated tails, re-aim the bones from the parents' new pose.
        void Aim(SQuat[] anchorRot, SVec[] anchorPos)
        {
            for (int i = 0; i < _parent.Length; i++)
            {
                SQuat pr; SVec pp;
                if (_parent[i] < 0) { pr = anchorRot[_anchor[i]]; pp = anchorPos[_anchor[i]]; }
                else { pr = _rot[_parent[i]]; pp = _head[_parent[i]]; }
                var head = pp + pr.Rotate(_lpos[i]);
                var restR = (pr * _lrot[i]).Normalized;
                var q = (SQuat.FromTo(restR.Rotate(_axis[i]), _cur[i] - head) * restR).Normalized;
                _rot[i] = q; _head[i] = head; Local[i] = (pr.Inverse * q).Normalized;
            }
        }

        // Model-space tail of joint i after the last update (tests, debugging).
        public SVec Tail(int i) => _head[i] + _rot[i].Rotate(_axis[i]);
    }
}
