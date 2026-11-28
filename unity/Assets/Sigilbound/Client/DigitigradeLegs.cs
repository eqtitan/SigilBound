// Digitigrade legs for animal-folk heroes (owner report 2026-11-28: their knees bent like a human's).
//
// A hero built with "legs": "digitigrade" (blenderforge/i3d_legs.py) has a metatarsal bone per leg
// (thigh -> calf -> meta_l -> foot -> ball) and carries, in its own GLB, one animation per shared UBC clip
// with the same name and length: the clip's legs re-solved offline for a reverse-knee leg (hip motion and
// foot plants kept, the hock folding as the humanoid ankle bends) and baked to FK keys. No IK runs here.
//
// Battle code keeps playing the shared clips by name. This component layers each hero leg clip over its
// shared clip: a twin state "<name>~legs" on layer 1, limited to the two leg chains (mixing transforms),
// that copies the shared state's enabled flag, weight, time, speed and wrap mode every frame. Legacy
// Animation gives higher layers their weight first, so the legs follow the baked clip and the body the
// shared one, through cross-fades, queued clips, hit-stop freezes and battle speed changes alike.
using System.Collections.Generic;
using UnityEngine;

namespace Sigilbound.Client
{
    public sealed class DigitigradeLegs : MonoBehaviour
    {
        public const string Suffix = "~legs";
        Animation _anim;
        Transform _thighL, _thighR;
        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();
        readonly Dictionary<AnimationState, AnimationState> _twin = new Dictionary<AnimationState, AnimationState>();
        readonly HashSet<AnimationState> _twins = new HashSet<AnimationState>();
        readonly List<AnimationState> _scan = new List<AnimationState>();
        readonly List<AnimationState> _dead = new List<AnimationState>();

        // A model built with digitigrade legs (the metatarsal bones exist).
        public static bool Needed(Transform root) => BodyShaper.Find(root, "meta_l") != null && BodyShaper.Find(root, "meta_r") != null;

        // Bones the leg clips own: RigExtras must not re-express the shared clip on them.
        public static bool LegBone(string name) =>
            name.StartsWith("thigh_") || name.StartsWith("calf_") || name.StartsWith("meta_") || name.StartsWith("foot_") || name.StartsWith("ball_");

        // The shared clip a state plays: "Armature|Walk_Loop.001 - Queued Clone" -> "Walk_Loop".
        public static string BaseName(string state)
        {
            var n = state;
            int q = n.IndexOf(" - Queued Clone", System.StringComparison.Ordinal); if (q >= 0) n = n.Substring(0, q);
            int bar = n.LastIndexOf('|'); if (bar >= 0) n = n.Substring(bar + 1);
            if (n.Length > 4 && n[n.Length - 4] == '.' && char.IsDigit(n[n.Length - 1]) && char.IsDigit(n[n.Length - 2]) && char.IsDigit(n[n.Length - 3])) n = n.Substring(0, n.Length - 4);
            return n;
        }

        // Call after the shared clips were added, with the hero GLB's own (leg) clips.
        public void Init(Transform model, Animation anim, IEnumerable<AnimationClip> legClips)
        {
            _anim = anim;
            _thighL = BodyShaper.Find(model, "thigh_l"); _thighR = BodyShaper.Find(model, "thigh_r");
            if (legClips != null)
                foreach (var c in legClips) if (c != null) _clips[BaseName(c.name)] = c;
            // glTFast put these clips on the holder's Animation first (the shared clips of the same name replaced
            // them there); never let one auto-play as a whole-body clip
            if (anim != null && anim.clip != null && _clips.ContainsValue(anim.clip)) anim.clip = null;
        }

        public int LegClips => _clips.Count;

        AnimationState MakeTwin(AnimationState s)
        {
            if (!_clips.TryGetValue(BaseName(s.name), out var clip)) return null;
            string name = s.name + Suffix;
            var tw = _anim[name];
            if (tw != null && _twins.Contains(tw)) return tw;   // a queued clone's name comes back: reuse its twin
            if (tw == null) { _anim.AddClip(clip, name); tw = _anim[name]; }
            if (tw == null) return null;
            tw.layer = 1; tw.blendMode = AnimationBlendMode.Blend; tw.enabled = false; tw.weight = 0f;
            if (_thighL != null) tw.AddMixingTransform(_thighL, true);
            if (_thighR != null) tw.AddMixingTransform(_thighR, true);
            _twins.Add(tw);
            return tw;
        }

        void LateUpdate()
        {
            if (_anim == null || _clips.Count == 0) return;
            _scan.Clear();
            foreach (AnimationState s in _anim) if (!_twins.Contains(s)) _scan.Add(s);
            foreach (var s in _scan)
            {
                if (!_twin.TryGetValue(s, out var tw))
                {
                    if (!s.enabled) continue;          // a twin is made the first time its clip plays
                    tw = MakeTwin(s); _twin[s] = tw;
                }
                if (tw == null) continue;               // no leg clip for it (the shared legs play)
                if (!s.enabled) { if (tw.enabled) { tw.enabled = false; tw.weight = 0f; } continue; }
                tw.enabled = true; tw.wrapMode = s.wrapMode; tw.speed = s.speed; tw.weight = s.weight; tw.time = s.time;
            }
            // queued clones come and go: forget the ones Unity removed
            _dead.Clear();
            foreach (var kv in _twin) if (kv.Key == null) _dead.Add(kv.Key);
            foreach (var d in _dead) { if (_twin.TryGetValue(d, out var tw) && tw != null) { tw.enabled = false; tw.weight = 0f; } _twin.Remove(d); }
        }
    }
}
