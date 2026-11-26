// Additive buff/debuff posture (MECHANICS.md 3.1 and 4.5), on top of whatever clip is playing:
// a buff lifts the chest 4 degrees, a debuff drops the shoulders 4 degrees (and bows the chest a
// little), each as a 0.4 s pulse when the effect lands. While effects last a weaker version holds
// (40%), and poison or burn add a faint tremor. Runs in LateUpdate after the Animation, on spine and
// clavicle bones only (HandGrip owns the fingers). Time-based, so the same at 30/60/120 fps.
using System;
using UnityEngine;

namespace Sigilbound.Client
{
    public sealed class PostureLayer : MonoBehaviour
    {
        public Func<(float buff, float debuff, bool tremor)> Persistent;
        Transform _chest, _clavL, _clavR;
        float _buffPulseAt = -9, _debuffPulseAt = -9;
        float _buff, _debuff; // smoothed weights 0..1
        readonly Quaternion[] _written = new Quaternion[3], _base = new Quaternion[3];
        readonly bool[] _haveWritten = new bool[3];

        static Transform FindAny(Transform root, params string[] names)
        {
            foreach (var n in names) { var t = BodyShaper.Find(root, n); if (t != null) return t; }
            return null;
        }

        void Start()
        {
            _chest = FindAny(transform, "spine_03", "Chest", "UpperChest", "Spine2", "spine.003");
            _clavL = FindAny(transform, "clavicle_l", "Shoulder.L", "LeftShoulder", "shoulder.L");
            _clavR = FindAny(transform, "clavicle_r", "Shoulder.R", "RightShoulder", "shoulder.R");
            if (_chest == null && _clavL == null && _clavR == null) enabled = false;
        }

        public void Pulse(bool buff) { if (buff) _buffPulseAt = Time.time; else _debuffPulseAt = Time.time; }

        // 0..1..0 over 0.4 s (sine bump).
        static float Bump(float since) => since < 0 || since > 0.4f ? 0 : Mathf.Sin(since / 0.4f * Mathf.PI);

        void LateUpdate()
        {
            (float buff, float debuff, bool tremor) p = Persistent != null ? Persistent() : (0f, 0f, false);
            float k = 1 - Mathf.Exp(-Time.deltaTime / 0.1f); // 0.10 s blend
            _buff = Mathf.Lerp(_buff, Mathf.Clamp01(p.buff * 0.4f + Bump(Time.time - _buffPulseAt)), k);
            _debuff = Mathf.Lerp(_debuff, Mathf.Clamp01(p.debuff * 0.4f + Bump(Time.time - _debuffPulseAt)), k);
            float tremor = p.tremor ? Mathf.Sin(Time.time * 2 * Mathf.PI * 9f) * 1.2f : 0f;
            Vector3 right = transform.right, fwd = transform.forward;
            // chest: lift back on buffs (negative about the right axis), bow 2 degrees on debuffs, tremor
            Apply(0, _chest, Quaternion.AngleAxis(-4f * _buff + 2f * _debuff + tremor, right));
            // shoulders: drop the outer end (left: + about forward, right: - about forward)
            Apply(1, _clavL, Quaternion.AngleAxis(4f * _debuff, fwd));
            Apply(2, _clavR, Quaternion.AngleAxis(-4f * _debuff, fwd));
        }

        void Apply(int i, Transform t, Quaternion worldDelta)
        {
            if (t == null) return;
            // If no clip wrote this bone since our last frame (paused clip, finished hold), start from the
            // pose before our offset so offsets never pile up.
            if (_haveWritten[i] && t.localRotation == _written[i]) t.localRotation = _base[i];
            _base[i] = t.localRotation;
            t.rotation = worldDelta * t.rotation;
            _written[i] = t.localRotation; _haveWritten[i] = true;
        }
    }
}
