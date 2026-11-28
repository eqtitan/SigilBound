// Skeleton library, runtime side (docs/sigilbound/art/SKELETON_LIBRARY.md). A migrated hero (Core.RigProfiles) gets:
//   - a Humanoid Avatar built from its own skeleton at load (AvatarBuilder.BuildHumanAvatar; the GLB's rest pose is
//     the T-pose; Core.HumanoidMap names the human bones, the profile can override them),
//   - an Animator driven by a PlayableGraph: the shared Humanoid clips (baked in the editor during the build,
//     Editor/HumanoidClipBaker.cs) in a crossfade mixer, then a posture job (gait style as muscle offsets, the
//     profile's joint limits as muscle clamps), evaluated by hand each frame so poses can be frozen exactly,
//   - after the clip: the digitigrade hock fold and add-on chains (wing flutter). Spring chains (SpringBones), the
//     hand grip and the Titan's body effects run after this, as before.
// Why Playables and not an AnimatorController: controllers can't be built at runtime (UnityEditor.Animations), and a
// prebuilt controller would need one state per clip per set; the battle code already asks for clips by name with
// its own fades, loops, holds and queued idles (HeroModel), which map one to one onto a small mixer.
// Retires, for migrated heroes: RigExtras (rest-pose re-expression), DigitigradeLegs (baked layered leg clips) and
// per-hero clip overrides: muscle-space retargeting handles proportions and rest orientations by itself.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sigilbound.Client
{
    // Gait posture and joint limits in muscle space, after the clip mix.
    public struct PostureJob : IAnimationJob
    {
        public NativeArray<MuscleHandle> Offset; public NativeArray<float> OffsetValue;
        public NativeArray<MuscleHandle> Limit; public NativeArray<float> LimitMin, LimitMax;
        public float Weight;
        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isHumanStream) return;
            var h = stream.AsHuman();
            for (int i = 0; i < Offset.Length; i++) h.SetMuscle(Offset[i], h.GetMuscle(Offset[i]) + OffsetValue[i] * Weight);
            for (int i = 0; i < Limit.Length; i++) h.SetMuscle(Limit[i], Mathf.Clamp(h.GetMuscle(Limit[i]), LimitMin[i], LimitMax[i]));
        }
    }

    [DefaultExecutionOrder(-50)]
    public sealed class HumanoidRig : MonoBehaviour
    {
        // ---------- the baked clip library (Resources/HumanoidClips/<set>) ----------
        public static class Clips
        {
            static readonly Dictionary<string, Dictionary<string, AnimationClip>> _sets = new Dictionary<string, Dictionary<string, AnimationClip>>();
            public static Dictionary<string, AnimationClip> Set(string set)
            {
                if (_sets.TryGetValue(set, out var d)) return d;
                d = new Dictionary<string, AnimationClip>();
                foreach (var c in Resources.LoadAll<AnimationClip>($"{RigProfiles.ResourcesDir}/{set}")) if (c != null && c.humanMotion) d[c.name] = c;
                _sets[set] = d;
                Debug.Log($"SBHUMAN clip set {set}: {d.Count} humanoid clips");
                return d;
            }
            public static bool Available => Set("biped").Count > 0;
        }

        sealed class Slot
        {
            public string Name; public AnimationClipPlayable P; public int Input = -1;
            public float Time, Speed = 1f, Length, Weight, Target, Rate; public bool Loop, Clamp, Ambient;
        }

        const int Inputs = 4;
        public Animator Animator { get; private set; }
        public RigProfile Profile { get; private set; }
        public GaitSpec Gait { get; private set; }
        public IReadOnlyDictionary<string, AnimationClip> Library => _clips;
        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();
        readonly List<Slot> _slots = new List<Slot>();
        readonly Slot[] _byInput = new Slot[Inputs];
        PlayableGraph _graph; AnimationMixerPlayable _mixer; AnimationScriptPlayable _posture; PostureJob _job;
        Slot _current; (string name, float speed)? _queued;
        public bool Frozen;
        public bool PostureOn = true;
        float _postureW = 1f;
        Transform _holder;

        // ---------- build ----------
        // avatarRoot: the object above the glTF "Armature" node (the clips' paths and the avatar's root).
        public static HumanoidRig Setup(GameObject avatarRoot, Transform holder, RigProfile profile)
        {
            if (profile == null || !Clips.Available) return null;
            var nodes = avatarRoot.GetComponentsInChildren<Transform>(true).Where(t => t != avatarRoot.transform).ToArray();
            if (nodes.Select(t => t.name).Distinct().Count() != nodes.Length) { Debug.LogWarning($"SBHUMAN {profile.Id}: duplicate node names, legacy path kept"); return null; }
            var list = nodes.Select(t => (t.name, Array.IndexOf(nodes, t.parent))).ToList();
            var map = HumanoidMap.Map(list);
            foreach (var kv in profile.Bones) { int i = Array.FindIndex(nodes, t => t.name == kv.Value); if (i >= 0) map[kv.Key] = i; }
            var missing = HumanoidMap.Missing(map); var problems = HumanoidMap.HierarchyProblems(map, list);
            if (missing.Count > 0 || problems.Count > 0) { Debug.LogWarning($"SBHUMAN {profile.Id}: no humanoid avatar ({string.Join("; ", missing.Concat(problems))}), legacy path kept"); return null; }
            var avatar = AvatarBuilder.BuildHumanAvatar(avatarRoot, Describe(avatarRoot.transform, nodes, map));
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { Debug.LogWarning($"SBHUMAN {profile.Id}: AvatarBuilder refused the skeleton, legacy path kept"); return null; }
            avatar.name = "AV_" + profile.Id;
            // glTFast's own legacy Animation (files with clips) must not fight the Animator.
            var legacy = avatarRoot.GetComponent<Animation>();
            if (legacy != null) { HeroModels.ClearClips(legacy); Destroy(legacy); }
            var rig = avatarRoot.AddComponent<HumanoidRig>();
            rig.Init(avatar, holder, profile, map.Count);
            return rig;
        }

        public static HumanDescription Describe(Transform top, Transform[] nodes, Dictionary<string, int> map)
        {
            var names = HumanTrait.BoneName;
            var human = new List<HumanBone>();
            foreach (var kv in map)
            {
                var hn = names.FirstOrDefault(x => x.Replace(" ", "") == kv.Key);
                if (hn == null) continue;
                human.Add(new HumanBone { boneName = nodes[kv.Value].name, humanName = hn, limit = new HumanLimit { useDefaultValues = true } });
            }
            var skeleton = new List<SkeletonBone> { new SkeletonBone { name = top.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one } };
            skeleton.AddRange(nodes.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }));
            return new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
        }

        void Init(Avatar avatar, Transform holder, RigProfile profile, int mapped)
        {
            _holder = holder; Profile = profile; Gait = RigProfiles.Gait(profile.Gait);
            foreach (var set in profile.ClipSets.Reverse()) foreach (var kv in Clips.Set(set)) _clips[kv.Key] = kv.Value; // earlier sets win
            Animator = gameObject.AddComponent<Animator>();
            Animator.avatar = avatar; Animator.applyRootMotion = false; Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("SB_" + profile.Id);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, Inputs);
            _job = BuildJob();
            _posture = AnimationScriptPlayable.Create(_graph, _job, 1);
            _posture.ConnectInput(0, _mixer, 0, 1f);
            var output = AnimationPlayableOutput.Create(_graph, "out", Animator);
            output.SetSourcePlayable(_posture);
            _graph.Play();
            InitExtras();
            Debug.Log($"SBHUMAN {profile.Id}: avatar {mapped} human bones, {profile.Skeleton}, gait {profile.Gait}, {_clips.Count} clips, {_job.Offset.Length} posture muscles, {_job.Limit.Length} limits");
        }

        PostureJob BuildJob()
        {
            var all = new MuscleHandle[MuscleHandle.muscleHandleCount];
            MuscleHandle.GetMuscleHandles(all);
            var byName = new Dictionary<string, MuscleHandle>();
            foreach (var h in all) byName[h.name] = h;
            var off = Gait.Offsets.Where(kv => byName.ContainsKey(kv.Key)).ToList();
            var lim = Profile.Limits.Where(l => byName.ContainsKey(l.Muscle)).ToList();
            foreach (var kv in Gait.Offsets) if (!byName.ContainsKey(kv.Key)) Debug.LogWarning($"SBHUMAN unknown muscle '{kv.Key}'");
            foreach (var l in Profile.Limits) if (!byName.ContainsKey(l.Muscle)) Debug.LogWarning($"SBHUMAN unknown muscle '{l.Muscle}'");
            return new PostureJob
            {
                Offset = new NativeArray<MuscleHandle>(off.Select(kv => byName[kv.Key]).ToArray(), Allocator.Persistent),
                OffsetValue = new NativeArray<float>(off.Select(kv => kv.Value * Profile.GaitAmount).ToArray(), Allocator.Persistent),
                Limit = new NativeArray<MuscleHandle>(lim.Select(l => byName[l.Muscle]).ToArray(), Allocator.Persistent),
                LimitMin = new NativeArray<float>(lim.Select(l => l.Min).ToArray(), Allocator.Persistent),
                LimitMax = new NativeArray<float>(lim.Select(l => l.Max).ToArray(), Allocator.Persistent),
                Weight = 1f,
            };
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_job.Offset.IsCreated) _job.Offset.Dispose();
            if (_job.OffsetValue.IsCreated) _job.OffsetValue.Dispose();
            if (_job.Limit.IsCreated) _job.Limit.Dispose();
            if (_job.LimitMin.IsCreated) _job.LimitMin.Dispose();
            if (_job.LimitMax.IsCreated) _job.LimitMax.Dispose();
        }

        // ---------- playback (HeroModel's vocabulary) ----------
        public bool Has(string clip) => clip != null && _clips.ContainsKey(clip);
        public float Length(string clip) => Has(clip) ? _clips[clip].length : 0f;
        public string Current => _current?.Name;

        Slot Attach(string clip)
        {
            int free = -1;
            for (int i = 0; i < Inputs; i++) if (_byInput[i] == null) { free = i; break; }
            if (free < 0)
            {
                // reuse the input of the faintest slot that isn't the newest
                var drop = _slots.Where(s => s != _current).OrderBy(s => s.Weight).FirstOrDefault() ?? _slots[0];
                Detach(drop); free = Array.IndexOf(_byInput, null);
            }
            var c = _clips[clip];
            var slot = new Slot { Name = clip, Length = Mathf.Max(1e-3f, c.length), Input = free };
            slot.P = AnimationClipPlayable.Create(_graph, c);
            slot.P.SetApplyFootIK(false);     // baked clips carry no IK goals
            slot.P.SetApplyPlayableIK(false);
            slot.P.SetSpeed(0);               // time is set by hand every frame
            _graph.Connect(slot.P, 0, _mixer, free);
            _byInput[free] = slot; _slots.Add(slot);
            return slot;
        }

        void Detach(Slot s)
        {
            if (s.Input >= 0) { _mixer.DisconnectInput(s.Input); _byInput[s.Input] = null; }
            if (s.P.IsValid()) s.P.Destroy();
            _slots.Remove(s);
        }

        // Crossfade to clip. queueIdle: when a one-shot ends, fade into this loop (legacy CrossFadeQueued).
        public bool Play(string clip, bool loop, float fade, float speed, bool clamp, bool ambient, (string name, float speed)? queueIdle)
        {
            if (!Has(clip)) return false;
            var s = Attach(clip);
            s.Loop = loop; s.Clamp = clamp; s.Speed = speed; s.Ambient = ambient; s.Time = speed < 0 ? s.Length : 0f;
            foreach (var o in _slots) { o.Target = 0f; o.Rate = fade > 1e-3f ? 1f / fade : 1e3f; }
            s.Target = 1f; s.Weight = fade > 1e-3f ? 0f : 1f;
            if (fade <= 1e-3f) foreach (var o in _slots) if (o != s) o.Weight = 0f;
            _current = s; _queued = loop || clamp ? null : queueIdle;
            return true;
        }

        public bool PlayFrom(string clip, float fromT, float speed, float fade = 0.08f)
        {
            if (!Play(clip, false, fade, speed, true, false, null)) return false;
            _current.Time = Mathf.Clamp01(fromT) * _current.Length;
            return true;
        }

        // A frozen pose: clip at normalized time t, full weight, evaluated now (screenshot tour, reveal).
        public void Pose(string clip, float t)
        {
            if (!Play(clip, false, 0f, 0f, true, false, null)) return;
            _current.Time = Mathf.Clamp01(t) * _current.Length;
            Evaluate(0f);
        }

        public float Normalized => _current == null ? 1f : Mathf.Clamp01(_current.Time / _current.Length);
        public void SetNormalized(float t) { if (_current != null) _current.Time = Mathf.Repeat(t, 1f) * _current.Length; }
        // Seconds into the clip while it plays (loops keep counting past the length, as legacy AnimationState.time), else -1.
        public float TimeOf(string clip)
        {
            var s = _slots.LastOrDefault(x => x.Name == clip);
            return s == null ? -1f : s.Loop ? Mathf.Max(0f, s.Time) : SampleTime(s);
        }

        static float SampleTime(Slot s) => s.Loop ? Mathf.Repeat(s.Time, s.Length) : Mathf.Clamp(s.Time, 0f, s.Length);

        void Update()
        {
            if (!_graph.IsValid()) return;
            Evaluate(Time.deltaTime);
        }

        public void Evaluate(float dt)
        {
            float ts = Mathf.Max(0.01f, Time.timeScale);
            if (!Frozen)
                foreach (var s in _slots) s.Time += dt * s.Speed * (s.Ambient ? 1f / ts : 1f);
            // queued idle once the one-shot reaches its end
            if (_queued.HasValue && _current != null && !_current.Loop && !_current.Clamp && _current.Time >= _current.Length - 0.22f * Mathf.Abs(_current.Speed))
            {
                var q = _queued.Value; _queued = null;
                Play(q.name, true, 0.22f, q.speed, false, true, null);
            }
            float sum = 0f;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var s = _slots[i];
                if (!Frozen) s.Weight = Mathf.MoveTowards(s.Weight, s.Target, s.Rate * dt);
                if (s.Weight <= 0f && s.Target <= 0f && s != _current) { Detach(s); continue; }
                sum += s.Weight;
            }
            foreach (var s in _slots)
            {
                s.P.SetTime(SampleTime(s));
                _mixer.SetInputWeight(s.Input, sum > 1e-4f ? s.Weight / sum : (s == _current ? 1f : 0f));
            }
            // posture: full on idle, ready and locomotion, partly off during actions (the clip's own shape leads)
            bool calm = _current != null && (_current.Loop || _current.Ambient);
            float want = !PostureOn ? 0f : calm ? 1f : Gait.ActionWeight;
            _postureW = dt <= 0f ? want : Mathf.MoveTowards(_postureW, want, dt * 3f);
            _job.Weight = _postureW;
            _posture.SetJobData(_job);
            // non-human bones the extras turn: the Animator never writes them, so they start every frame from rest
            foreach (var h in _hocks) h.Meta.localRotation = h.MetaRest;
            foreach (var f in _flutter) f.t.localRotation = f.rest;
            _graph.Evaluate(0f);
            Extras(dt);
        }

        // ---------- after the clip: digitigrade hock, add-on chains ----------
        struct Hock { public Transform Thigh, Calf, Meta, Foot; public Quaternion MetaRest; public float RestBend; }
        readonly List<Hock> _hocks = new List<Hock>();
        readonly List<(Transform t, Quaternion rest, float side, AddonChain chain)> _flutter = new List<(Transform, Quaternion, float, AddonChain)>();
        float _flutterT;

        void InitExtras()
        {
            if (Profile.HockFold > 0f)
                foreach (var sd in new[] { "l", "r" })
                {
                    var h = new Hock { Thigh = BodyShaper.Find(transform, "thigh_" + sd), Calf = BodyShaper.Find(transform, "calf_" + sd), Meta = BodyShaper.Find(transform, "meta_" + sd), Foot = BodyShaper.Find(transform, "foot_" + sd) };
                    if (h.Thigh == null || h.Calf == null || h.Meta == null || h.Foot == null) continue;
                    h.MetaRest = h.Meta.localRotation;
                    h.RestBend = Vector3.Angle(h.Calf.position - h.Thigh.position, h.Meta.position - h.Calf.position);
                    _hocks.Add(h);
                }
            foreach (var a in Profile.Addons)
            {
                if (a.Kind != AddonKind.Wings && a.Kind != AddonKind.Ears) continue;
                foreach (var b in a.Bones)
                {
                    var t = BodyShaper.Find(transform, b); if (t == null) continue;
                    float side = b.EndsWith("_l") ? 1f : b.EndsWith("_r") ? -1f : 0f;
                    _flutter.Add((t, t.localRotation, side, a));
                }
            }
        }

        void Extras(float dt)
        {
            foreach (var h in _hocks)
            {
                var thigh = h.Calf.position - h.Thigh.position; var calf = h.Meta.position - h.Calf.position;
                float bend = Vector3.Angle(thigh, calf) - h.RestBend;
                if (bend <= 0.5f) continue;
                var axis = Vector3.Cross(thigh, calf);
                if (axis.sqrMagnitude < 1e-8f) continue;
                var footRot = h.Foot.rotation;
                // the hock folds against the knee (a Z-shaped leg), so the hoof stays under the body in a crouch
                h.Meta.rotation = Quaternion.AngleAxis(-Profile.HockFold * bend, axis.normalized) * h.Meta.rotation;
                h.Foot.rotation = footRot;
            }
            if (_flutter.Count > 0)
            {
                bool busy = _current != null && !_current.Loop;
                _flutterT += (Frozen ? 0f : dt) * (busy ? 1.7f : 1f);
                var fwd = _holder != null ? _holder.forward : transform.forward;
                foreach (var (t, rest, side, chain) in _flutter)
                {
                    float a = chain.Amplitude * (busy ? 1.5f : 1f) * Mathf.Sin(_flutterT * chain.Frequency * Mathf.PI * 2f);
                    t.rotation = Quaternion.AngleAxis(side * a, fwd) * t.rotation;
                }
            }
        }
    }
}
