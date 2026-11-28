// Creature rig extensions on the UBC humanoid skeleton (blenderforge/kit_rig.py; owner direction
// 2026-11-27, the six Grok heroes). Two jobs, after every animation frame (LateUpdate):
//
// 1. Rest-pose fix. The shared UBC clips key every bone's local rotation and translation absolutely,
//    so a bone whose rest pose was changed for this hero (digitigrade legs, a head moved forward on a
//    longer neck) would be dragged back to the humanoid pose. At build time each bone is compared with
//    the UBC reference skeleton (the clip library's prototype); for those that differ the clip value is
//    re-expressed relative to the new rest: rotation D*q, position T1 + D*(p - T0), D = R1 * R0^-1.
//    This matches how Blender applies the same clips (rest-relative), so previews and the game agree.
//    Digitigrade legs (DigitigradeLegs.cs) are skipped: their baked leg clips are already in the hero's rest.
// 2. Procedural bones no clip names: a tail chain (spring sway that lags body motion and whips on a
//    strike), a jaw (breathing, opening on an attack), wing bones (a gentle flutter).
using System.Collections.Generic;
using UnityEngine;

namespace Sigilbound.Client
{
    public sealed class RigExtras : MonoBehaviour
    {
        struct Fix { public Transform T; public Quaternion D; public Vector3 T0, T1; }
        readonly List<Fix> _fix = new List<Fix>();
        Transform[] _tail = new Transform[0]; Quaternion[] _tailRest = new Quaternion[0];
        Transform _jaw; Quaternion _jawRest;
        Transform _wingL, _wingR; Quaternion _wingLRest, _wingRRest;
        Animation _anim;
        Vector3 _lastPos; float _sway, _swayVel, _strike, _phase;

        // True when the model carries any extension bone (only those heroes get the component).
        public static bool Needed(Transform root) =>
            BodyShaper.Find(root, "tail_01") != null || BodyShaper.Find(root, "jaw") != null ||
            BodyShaper.Find(root, "wing_l") != null || BodyShaper.Find(root, "neck_02") != null ||
            BodyShaper.Find(root, "rest_fit") != null; // image-to-3D heroes (blenderforge/i3d_rig.py): joints moved to the mesh

        // Call right after the model is instantiated, before any clip plays (the bones are at rest).
        public void Init(Transform model, Transform reference, Animation anim)
        {
            _anim = anim; _phase = Random.value * 10f;
            if (reference != null)
            {
                var refBones = new Dictionary<string, Transform>();
                foreach (var t in reference.GetComponentsInChildren<Transform>(true)) if (!refBones.ContainsKey(t.name)) refBones[t.name] = t;
                bool digi = DigitigradeLegs.Needed(model);
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (!refBones.TryGetValue(t.name, out var r) || t == model) continue;
                    if (Quaternion.Angle(t.localRotation, r.localRotation) < 0.5f && (t.localPosition - r.localPosition).sqrMagnitude < 4e-6f) continue;
                    if (t.name == "Armature" || t.name == "root") continue;
                    if (digi && DigitigradeLegs.LegBone(t.name)) continue; // its own baked leg clips own these bones
                    _fix.Add(new Fix { T = t, D = t.localRotation * Quaternion.Inverse(r.localRotation), T0 = r.localPosition, T1 = t.localPosition });
                }
            }
            var tails = new List<Transform>();
            for (int i = 1; i <= 12; i++) { var t = BodyShaper.Find(model, $"tail_{i:00}"); if (t == null) break; tails.Add(t); }
            _tail = tails.ToArray(); _tailRest = new Quaternion[_tail.Length];
            for (int i = 0; i < _tail.Length; i++) _tailRest[i] = _tail[i].localRotation;
            _jaw = BodyShaper.Find(model, "jaw"); if (_jaw != null) _jawRest = _jaw.localRotation;
            _wingL = BodyShaper.Find(model, "wing_l"); if (_wingL != null) _wingLRest = _wingL.localRotation;
            _wingR = BodyShaper.Find(model, "wing_r"); if (_wingR != null) _wingRRest = _wingR.localRotation;
            _lastPos = transform.position;
        }

        public int FixedBones => _fix.Count;

        // An attack is playing: the strike envelope rises while one is active (from the clip names).
        bool Attacking()
        {
            if (_anim == null) return false;
            foreach (AnimationState s in _anim)
            {
                if (!s.enabled || s.weight < 0.5f) continue;
                var n = s.name;
                if (n.Contains("Attack") || n.Contains("Punch") || n.Contains("Shoot") || n.Contains("Thrust") || n.StartsWith("Ult_")) return true;
            }
            return false;
        }

        void LateUpdate()
        {
            foreach (var f in _fix)
            {
                if (f.T == null) continue;
                f.T.localRotation = f.D * f.T.localRotation;
                f.T.localPosition = f.T1 + f.D * (f.T.localPosition - f.T0);
            }
            float dt = Mathf.Max(1e-4f, Time.deltaTime), t = Time.time + _phase;
            _strike = Mathf.MoveTowards(_strike, Attacking() ? 1f : 0f, dt * 4f);
            // tail: a damped spring driven by sideways body motion, plus a travelling idle wave
            var v = (transform.position - _lastPos) / dt; _lastPos = transform.position;
            float side = Vector3.Dot(v, transform.right);
            _swayVel += (-side * 0.08f - _sway) * 30f * dt; _swayVel *= Mathf.Exp(-6f * dt); _sway += _swayVel * dt;
            for (int i = 0; i < _tail.Length; i++)
            {
                if (_tail[i] == null) continue;
                float k = (i + 1f) / _tail.Length;
                float yaw = (6f + 6f * k) * Mathf.Sin(t * 3.2f - i * 0.7f) + _sway * 40f * k + 18f * k * _strike * Mathf.Sin(t * 9f - i);
                float pitch = 3f * Mathf.Sin(t * 2.1f - i * 0.5f);
                _tail[i].localRotation = _tailRest[i] * Quaternion.Euler(pitch, yaw, 0f);
            }
            if (_jaw != null)
                _jaw.localRotation = _jawRest * Quaternion.Euler(3f + 2f * Mathf.Sin(t * 2f) + 24f * _strike, 0f, 0f);
            if (_wingL != null) _wingL.localRotation = _wingLRest * Quaternion.Euler(3f * Mathf.Sin(t * 5f + 1f), 14f * Mathf.Sin(t * 5f), 0f);
            if (_wingR != null) _wingR.localRotation = _wingRRest * Quaternion.Euler(3f * Mathf.Sin(t * 5f + 1f), -14f * Mathf.Sin(t * 5f), 0f);
        }
    }
}
