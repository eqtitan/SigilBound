// Creature heroes at runtime (Core/Kit/Creatures.cs has the data). Shared by every creature:
//   CreatureRig.Setup  - called by HeroModels.BuildHd for a creature: plays the model's OWN clips (not the
//                        humanoid UAL library), fixed scale, the creature's style and aliases, leg planting;
//   CreatureLegs       - keeps feet planted on the ground while battle code moves the unit (lunges, knock-
//                        backs), on top of the baked clips: a foot the clip puts on the ground stays where it
//                        landed until it drifts StepThreshold from the clip's spot, then steps there on an
//                        arc (legs alternate in two groups, like a walking spider); a foot the clip lifts
//                        follows the clip. Any number of legs, chains of 2-4 bones plus a tip marker.
// Cost: per leg one yaw, one two-bone solve and three FromToRotations: a few microseconds for eight legs.
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using UnityEngine;

namespace Sigilbound.Client
{
    public static class CreatureRig
    {
        // holder: the model's holder (its "Armature" child is the glTF root); clips: the model's own clips.
        public static HeroModel Setup(GameObject holder, GameObject animRoot, IEnumerable<AnimationClip> clips, CreatureSpec spec)
        {
            var anim = animRoot.GetComponent<Animation>() ?? animRoot.AddComponent<Animation>();
            foreach (var c in clips ?? Enumerable.Empty<AnimationClip>()) if (c != null) anim.AddClip(c, c.name);
            anim.playAutomatically = false;
            float k = (float)spec.Scale;
            holder.transform.localScale = new Vector3(k, k, k);
            if (spec.Legs.Length > 0)
            {
                var legs = animRoot.AddComponent<CreatureLegs>();
                legs.Init(holder.transform, spec);
            }
            return new HeroModel(holder, anim, (float)spec.ModelHeight, spec.Style ?? AnimStyle.TitanHd, spec.Aliases);
        }
    }

    public sealed class CreatureLegs : MonoBehaviour
    {
        sealed class Leg
        {
            public Transform[] Bones;  // hip .. foot, then the tip marker
            public int Group;
            public Vector3 Planted; public bool HasPlant;
            public bool Stepping; public float T0; public Vector3 From;
        }
        Transform _holder;
        readonly List<Leg> _legs = new List<Leg>();
        float _stepTime = 0.18f, _lift = 0.14f, _threshold = 0.12f, _contact = 0.07f;
        Vector3 _lastRoot; bool _haveRoot;
        public float Weight = 1f; // 0 = pure clip (e.g. a cinematic), 1 = feet planted

        public void Init(Transform holder, CreatureSpec spec)
        {
            _holder = holder;
            _stepTime = (float)spec.StepTime; _lift = (float)spec.StepLift; _threshold = (float)spec.StepThreshold;
            _contact = 0.07f * (float)spec.Scale;
            for (int i = 0; i < spec.Legs.Length; i++)
            {
                var chain = spec.Legs[i].Select(n => BodyShaper.Find(holder, n)).ToArray();
                if (chain.Length < 3 || chain.Any(t => t == null)) continue;
                // Two alternating groups, as gait.py's tetrapod: first, third legs of one side with second, fourth of the other.
                int perSide = Mathf.Max(1, spec.Legs.Length / 2);
                int side = i / perSide, idx = i % perSide;
                _legs.Add(new Leg { Bones = chain, Group = (idx + side) % 2 });
            }
            enabled = _legs.Count > 0;
        }

        void LateUpdate()
        {
            if (_holder == null) return;
            float ground = _holder.position.y, now = Time.time, dt = Mathf.Max(1e-4f, Time.deltaTime);
            // Fast dashes (battle lunges at several m/s) would stretch planted legs: fade the planting out.
            var root = _holder.position; float speed = _haveRoot ? (root - _lastRoot).magnitude / dt : 0; _lastRoot = root; _haveRoot = true;
            float w = Weight * Mathf.Clamp01(1.6f - speed / 5f);
            bool[] groupStepping = new bool[2];
            foreach (var l in _legs) if (l.Stepping) groupStepping[l.Group] = true;
            foreach (var l in _legs)
            {
                var tip = l.Bones[l.Bones.Length - 1];
                Vector3 anim = tip.position;
                bool contact = anim.y - ground < _contact;
                if (!contact || w <= 0.001f) { l.HasPlant = false; l.Stepping = false; continue; }
                if (!l.HasPlant) { l.Planted = anim; l.HasPlant = true; }
                Vector3 target;
                if (l.Stepping)
                {
                    float u = (now - l.T0) / _stepTime;
                    if (u >= 1) { l.Stepping = false; l.Planted = anim; target = anim; }
                    else
                    {
                        float e = u * u * (3 - 2 * u);
                        target = Vector3.Lerp(l.From, anim, e) + Vector3.up * (_lift * Mathf.Sin(Mathf.PI * u));
                    }
                }
                else
                {
                    var d = anim - l.Planted; d.y = 0;
                    if (d.magnitude > _threshold && !groupStepping[1 - l.Group])
                    {
                        l.Stepping = true; l.T0 = now; l.From = l.Planted; groupStepping[l.Group] = true;
                    }
                    target = new Vector3(l.Planted.x, anim.y, l.Planted.z);
                }
                var goal = Vector3.Lerp(anim, target, w);
                if ((goal - anim).sqrMagnitude > 1e-6f) Solve(l.Bones, goal);
            }
        }

        // Moves the chain so its tip reaches goal, keeping the clip's style: the hip yaws toward the goal, the
        // last segment keeps the clip's direction, the two before it bend in the clip's own knee plane.
        static void Solve(Transform[] b, Vector3 goal)
        {
            int n = b.Length - 1; // bones before the tip marker
            Transform tip = b[n];
            if (n >= 4)
            {
                // Coxa: yaw the whole leg about world up so the tip's direction from the hip points at the goal.
                Vector3 hip = b[0].position, a = tip.position - hip, g = goal - hip; a.y = 0; g.y = 0;
                if (a.sqrMagnitude > 1e-6f && g.sqrMagnitude > 1e-6f)
                    b[0].rotation = Quaternion.AngleAxis(Vector3.SignedAngle(a, g, Vector3.up), Vector3.up) * b[0].rotation;
            }
            // The last bone keeps its world direction: its head must land at goal minus that segment.
            int last = n - 1; Vector3 seg = tip.position - b[last].position; Quaternion lastRot = b[last].rotation;
            Vector3 ankleGoal = n >= 3 ? goal - seg : goal;
            Transform up = n >= 3 ? b[last - 2] : b[0], lo = n >= 3 ? b[last - 1] : b[1], end = n >= 3 ? b[last] : tip;
            TwoBone(up, lo, end, ankleGoal);
            if (n >= 3) b[last].rotation = Quaternion.FromToRotation(tip.position - b[last].position, seg) * b[last].rotation;
        }

        static void TwoBone(Transform upper, Transform lower, Transform end, Vector3 goal)
        {
            Vector3 a = upper.position, k = lower.position, e = end.position;
            float la = (k - a).magnitude, lb = (e - k).magnitude;
            if (la < 1e-5f || lb < 1e-5f) return;
            Vector3 toGoal = goal - a; float c = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(la - lb) + 1e-4f, la + lb - 1e-4f);
            Vector3 dir = toGoal.normalized;
            // Bend plane from the clip's own knee (keeps knees up and the leg's style).
            Vector3 pole = k - a; Vector3 perp = Vector3.ProjectOnPlane(pole, dir);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.ProjectOnPlane(Vector3.up, dir);
            perp.Normalize();
            float cosA = Mathf.Clamp((la * la + c * c - lb * lb) / (2 * la * c), -1f, 1f);
            float A = Mathf.Acos(cosA);
            Vector3 knee = a + dir * (la * Mathf.Cos(A)) + perp * (la * Mathf.Sin(A));
            upper.rotation = Quaternion.FromToRotation(k - a, knee - a) * upper.rotation;
            Vector3 endNow = end.position, kneeNow = lower.position;
            lower.rotation = Quaternion.FromToRotation(endNow - kneeNow, (a + dir * c) - kneeNow) * lower.rotation;
        }
    }
}
