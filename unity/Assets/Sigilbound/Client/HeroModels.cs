// Loads the CC0 Quaternius hero models (StreamingAssets/Models/*.glb, prepared by
// unity/Tools~/models) at runtime with glTFast and builds each hero as the web game does
// (three3d.mjs buildRigged): borrowed body parts, shaved/bald heads, recolours from art.js LOOKS,
// height, the code-built kit with equipped-gear visuals (Core.KitBuilder + KitAssembler),
// proportions, and the clips (base clips plus the retargeted CC0 UAL spell/hit/death clips),
// chosen per hero (Core.AnimStyle). If glTFast isn't installed or a model fails to load,
// CharacterView keeps its placeholder figure.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    // A built hero with its animation controller (three3d.mjs makeAnim). Plays either legacy
    // Animation clips (the CC0 GLB heroes, and imported hero prefabs that bring their own clips) or,
    // for imported hero prefabs with a Humanoid avatar, Animator states built by the art import tool
    // (Editor/Art/HeroImportPipeline): one state per battle state in Core.HeroClips, where loops and
    // the return to Idle live in the controller.
    public sealed class HeroModel
    {
        public GameObject Root;
        public Animation Anim;
        public float Height;
        public AnimStyle Style;
        readonly Animator _animator;
        readonly Dictionary<string, string> _names = new Dictionary<string, string>(); // short name -> state name
        readonly Dictionary<string, float> _lengths = new Dictionary<string, float>();
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        int _strikeN;

        public HeroModel(GameObject root, Animation anim, float height, AnimStyle style, IDictionary<string, string> aliases = null)
        {
            Root = root; Anim = anim; Height = height; Style = style;
            // Short names: "Armature|Sword_Idle.001" -> "Sword_Idle" (Blender suffixes renamed clips; lookups use plain names).
            if (anim != null) foreach (AnimationState s in anim) { var shortName = System.Text.RegularExpressions.Regex.Replace(s.name.Split('|').Last(), @"\.\d{3}$", ""); if (!_names.ContainsKey(shortName)) _names[shortName] = s.name; }
            // Canonical battle-state names (Core.HeroClips) pointing at the file's own clip names.
            if (aliases != null) foreach (var kv in aliases) { var clip = kv.Value.Split('|').Last(); if (_names.TryGetValue(clip, out var st)) _names[kv.Key] = st; }
            Idle(0);
        }

        // Animator-driven hero: states[name] = the controller state that plays for battle state `name`.
        public HeroModel(GameObject root, Animator animator, IDictionary<string, string> states, IDictionary<string, float> lengths, float height, AnimStyle style)
        {
            Root = root; _animator = animator; Height = height; Style = style;
            foreach (var kv in states) _names[kv.Key] = kv.Value;
            if (lengths != null) foreach (var kv in lengths) _lengths[kv.Key] = kv.Value;
            Idle(0);
        }

        public bool Has(string name) => name != null && _names.ContainsKey(name);
        string IdleName => Has(Style.Idle) ? Style.Idle : _names.Keys.FirstOrDefault(n => n.StartsWith("Idle", StringComparison.Ordinal));
        public float Length(string name) => !Has(name) ? 0 : _animator != null ? (_lengths.TryGetValue(name, out var l) ? l : 0.6f) : Anim[_names[name]].length;

        // Crossfade to `name`: loops when loop; otherwise plays once and returns to idle (or holds
        // the last frame with clamp, e.g. deaths). With an Animator the controller decides looping.
        public bool Play(string name, bool loop = false, float fade = 0.18f, float speed = 1f, bool clamp = false)
        {
            if (!Has(name)) return false;
            if (_animator != null)
            {
                if (!_animator.isActiveAndEnabled) return false;
                _animator.SetFloat(SpeedParam, speed);
                _animator.CrossFadeInFixedTime(_names[name], fade, 0, 0f);
                return true;
            }
            if (Anim == null) return false;
            _current = name;
            var st = Anim[_names[name]];
            st.wrapMode = loop ? WrapMode.Loop : clamp ? WrapMode.ClampForever : WrapMode.Once;
            st.speed = speed;
            if (loop) _loopSpeed[st.name] = speed;
            st.time = 0;
            Anim.CrossFade(st.name, fade);
            if (!loop && !clamp && IdleName != null && IdleName != name)
            {
                var q = Anim.CrossFadeQueued(_names[IdleName], 0.22f, QueueMode.CompleteOthers);
                q.wrapMode = WrapMode.Loop; q.speed = IdleLoopSpeed; _loopSpeed[_names[IdleName]] = IdleLoopSpeed;
            }
            return true;
        }
        string _current; bool _frozen; readonly Dictionary<AnimationState, float> _frozenSpeeds = new Dictionary<AnimationState, float>();

        // Normalized time of the clip last started with Play (time-based: identical at any frame rate).
        public float CurrentNormalized
        {
            get
            {
                if (_animator != null) return _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                if (Anim == null || _current == null || !Has(_current)) return 1f;
                var st = Anim[_names[_current]];
                return st == null ? 1f : st.length > 0 ? st.time / st.length : 1f;
            }
        }

        // Waits (scaled time) until the current clip reaches normalized time t, or maxSeconds pass.
        public IEnumerator WaitNormalized(float t, float maxSeconds)
        {
            for (float w = 0; w < maxSeconds && CurrentNormalized < t; w += Time.deltaTime) yield return null;
        }

        // Hit-stop: holds the pose (every playing clip at speed 0), then resumes at the speeds it had.
        public void SetFrozen(bool on)
        {
            if (on == _frozen) return;
            _frozen = on;
            if (_animator != null) { _animator.speed = on ? 0f : 1f; return; }
            if (Anim == null) return;
            if (on) { _frozenSpeeds.Clear(); foreach (AnimationState s in Anim) if (s.enabled) { _frozenSpeeds[s] = s.speed; s.speed = 0f; } }
            else { foreach (var kv in _frozenSpeeds) if (kv.Key != null) kv.Key.speed = kv.Value; _frozenSpeeds.Clear(); }
        }

        // Idle, Ready and Stunned loops stay at 1x at every battle speed (MECHANICS.md 3.1): battle speed
        // scales Time.timeScale, so those loops play at their own speed divided by it. Actions still speed up.
        readonly Dictionary<string, float> _loopSpeed = new Dictionary<string, float>();
        float IdleLoopSpeed => Style.Titan ? 0.7f : Style.IdleSpeed;
        bool Ambient(string stateName)
        {
            foreach (var n in new[] { IdleName, Style.Ready, "Crouch_Idle_Loop" })
                if (n != null && Has(n) && stateName.StartsWith(_names[n], StringComparison.Ordinal)) return true;
            return false;
        }
        public void Tick()
        {
            if (_frozen || Anim == null || _animator != null) return;
            float ts = Time.timeScale; if (ts <= 0.01f) return;
            foreach (AnimationState s in Anim)
            {
                if (!s.enabled || s.wrapMode != WrapMode.Loop || !Ambient(s.name)) continue;
                var key = s.name.Replace(" - Queued Clone", "");
                if (_loopSpeed.TryGetValue(key, out var b)) s.speed = b / ts;
            }
        }

        // The unit's turn starts: blend into the family's combat stance (or idle at 1.1x).
        public void Ready()
        {
            var n = Style.Ready != null && Has(Style.Ready) ? Style.Ready : null;
            if (n == null) { var i = IdleName; if (i != null) Play(i, true, 0.15f, IdleLoopSpeed * 1.1f); return; }
            bool pose = n.EndsWith("_Enter", StringComparison.Ordinal);
            Play(n, !pose, 0.15f, 1f, pose);
        }

        bool _idlePhased;
        public void Idle(float fade = 0.25f)
        {
            var n = IdleName; if (n == null) return;
            Play(n, true, fade, IdleLoopSpeed);
            // First idle starts at a random phase (MECHANICS.md 5), so neighbours don't breathe together.
            if (!_idlePhased && _animator == null && Anim != null) { _idlePhased = true; var st = Anim[_names[n]]; if (st != null) st.time = UnityEngine.Random.value * st.length; }
        }
        // Plays `name` from `fromT` (normalized) at `speed` (negative = backwards), holding when done.
        public bool PlayFrom(string name, float fromT, float speed)
        {
            if (Anim == null || !Has(name)) return false;
            _current = name;
            var st = Anim[_names[name]];
            st.wrapMode = WrapMode.ClampForever; st.speed = speed; st.time = Mathf.Clamp01(fromT) * st.length;
            Anim.CrossFade(st.name, 0.08f);
            return true;
        }
        // Stunned: a dazed, slumped loop (crouched idle, slowed) until the next action or Idle.
        public bool Stunned() => Has("Crouch_Idle_Loop") && Play("Crouch_Idle_Loop", true, 0.15f, 0.55f);

        // Guard against a light hit (Defense role, shields).
        public bool Block() => Style.Block != null && Has(Style.Block) && Play(Style.Block, false, 0.06f, 1.4f);
        // The losing side's survivors (held pose or slow loop).
        public void Defeat() { if (Style.Defeat != null && Has(Style.Defeat)) Play(Style.Defeat, Style.Defeat.EndsWith("_Loop"), 0.3f, Style.DefeatSpeed, !Style.Defeat.EndsWith("_Loop")); else Idle(0.3f); }
        public float Strike(float speed = 1f)
        {
            var list = Style.Strike.Where(Has).ToArray();
            var n = list.Length > 0 ? list[_strikeN++ % list.Length] : "Sword_Slash";
            Play(n, false, 0.1f, speed); return Length(n) / Mathf.Max(0.1f, speed);
        }
        // ult: the third skill or an all-enemies skill; plays the hero's ultimate cast if it has one.
        public float Cast(bool support, bool ult = false)
        {
            var n = support && Has(Style.Support) ? Style.Support : ult && Has(Style.Ult) ? Style.Ult : Has(Style.Cast) ? Style.Cast : "Gun_Shoot";
            Play(n, false, 0.12f); return Length(n);
        }
        // The hero's own clip for skill index `skill` (0-based), if it has one (Core.HeroClips Skill_<n>).
        public bool HasSkill(int skill) => skill >= 0 && Has($"Skill_{skill + 1}");
        public float Skill(int skill)
        {
            var n = $"Skill_{skill + 1}";
            if (!Has(n)) return -1;
            Play(n, false, 0.1f); return Length(n);
        }
        public void Hit(bool big)
        {
            if (Style.Titan) { Play(Has("HitReact") ? "HitReact" : "Idle", false, 0.1f); return; }
            var list = big ? Style.BigHit : Style.Hit;
            Play(list.FirstOrDefault(Has) ?? "HitRecieve", false, 0.08f);
        }
        public bool Roll() => Has("Roll") && Play("Roll", false, 0.1f, 1.5f);
        public bool Die() { var n = Style.Death.FirstOrDefault(Has); return n != null && Play(n, false, 0.12f, 1f, true); }
        public void Win() { if (Style.WinHold) Play(Style.Win, false, 0.25f, Style.WinSpeed, true); else Play(Style.Win, true, 0.25f, Style.WinSpeed); }
        public void Run(bool back = false) => Play(back && Has("Run_Back") ? "Run_Back" : "Run", true, 0.15f, back ? 1.5f : 1.4f);
    }

    public static class HeroModels
    {
        public static bool Available
        {
            get
            {
#if SB_GLTFAST
                return true;
#else
                return false;
#endif
            }
        }

#if SB_GLTFAST
        sealed class Library
        {
            public GLTFast.GltfImport Gltf; public Transform Prototype; public float HeadY = 1.55f;
            public List<AnimationClip> Ual; public bool Failed;
        }
        static readonly Dictionary<string, Library> _libs = new Dictionary<string, Library>();
        static Dictionary<string, List<UalClip>> _ualData;
        static GameObject _libRoot;

        // Load tracing ("SBMODEL ..." lines in the player log): the real models have never shown in a
        // build (step 4: every hero is the code-built stand-in) and nothing was logged, so each stage
        // of the load says what it got.
        static void Trace(string msg) => Debug.Log("SBMODEL " + msg);

        // GLBs ship packed and gzipped (prepare-models.mjs writes NAME.glbz; a ".gz" name is un-gzipped by Android's packaging): Unity stores every
        // StreamingAssets file uncompressed in the APK, so this cuts the models' share of the download to about a third.
        // A .glb read tries the .glbz first and unzips it, then falls back to a plain .glb.
        static IEnumerator ReadBytes(string file, Action<byte[]> done)
        {
            if (file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
            {
                byte[] gz = null;
                yield return ReadRaw(file + "z", b => gz = b, false);
                if (gz != null)
                {
                    // Inflate and unpack on a worker thread (plain byte work, about 1.4 MB out per hero): on the
                    // main thread it was one of the steps behind the team-screen hitches (performance review #4).
                    byte[] plain = null;
                    var job = System.Threading.Tasks.Task.Run(() => Inflate(gz));
                    while (!job.IsCompleted) yield return null;
                    if (job.IsFaulted) Debug.LogWarning($"Sigilbound: couldn't unzip {file}z: {job.Exception?.GetBaseException().Message}");
                    else plain = job.Result;
                    if (plain != null) { Trace($"read {file}z: {gz.Length} -> {plain.Length} bytes"); done(plain); yield break; }
                }
            }
            yield return ReadRaw(file, done, true);
        }

        static byte[] Inflate(byte[] gz)
        {
            byte[] plain;
            using (var src = new MemoryStream(gz))
            using (var unzip = new System.IO.Compression.GZipStream(src, System.IO.Compression.CompressionMode.Decompress))
            using (var dst = new MemoryStream(gz.Length * 3))
            { unzip.CopyTo(dst); plain = dst.ToArray(); }
            return ModelPack.Unpack(plain); // "SBZ1" byte-transposed buffers (prepare-models.mjs): back to the exact GLB
        }

        // At most one heavy main-thread step (an image decode or a texture upload) per frame across every
        // loader. A team, a stage's Hollow enemies and the Titan preload together; their decodes used to land
        // in the same frame after the glTF parses finished (performance review #4: 0.1-0.2 s per hero).
        static int _heavyFrame = -1;
        static IEnumerator HeavySlot()
        {
            while (_heavyFrame == Time.frameCount) yield return null;
            _heavyFrame = Time.frameCount;
        }

        static IEnumerator ReadRaw(string file, Action<byte[]> done, bool trace)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Models", file);
            byte[] data = null;
            if (path.Contains("://")) // Android: inside the APK
            {
                using (var req = UnityWebRequest.Get(path))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) data = req.downloadHandler.data;
                }
            }
            else if (File.Exists(path)) data = File.ReadAllBytes(path);
            if (trace) Trace($"read {file}: {(data == null ? "MISSING at " + path : data.Length + " bytes")}");
            done(data);
        }

        // ---------- shared loads ----------
        // Shared, once-only loads (a base model, the UAL clips, the baked index) run on a loader that
        // lives for the whole game, and every hero just waits for them. They used to run inside the
        // first hero's own coroutine: when that hero's figure was destroyed mid-load (any screen
        // change: intro -> home -> battle), Unity silently stopped the coroutine, the model stayed
        // "loading" forever, and every later hero waited on it and kept its stand-in. That is why no
        // real model ever showed in the Unity builds, with nothing in the log.
        sealed class Loader : MonoBehaviour { }
        static Loader _loader;
        static readonly Dictionary<string, bool> _jobs = new Dictionary<string, bool>(); // key -> finished

        static IEnumerator Shared(string key, Func<IEnumerator> work)
        {
            if (!_jobs.ContainsKey(key))
            {
                _jobs[key] = false;
                if (_loader == null)
                {
                    var go = new GameObject("HeroModelLoader"); UnityEngine.Object.DontDestroyOnLoad(go);
                    _loader = go.AddComponent<Loader>();
                }
                _loader.StartCoroutine(RunJob(key, work()));
            }
            while (_jobs.TryGetValue(key, out var finished) && !finished) yield return null;
        }

        // Steps the job (and the steps it waits on) by hand, so an exception anywhere ends it with a
        // warning instead of leaving every waiting hero hanging.
        static IEnumerator RunJob(string key, IEnumerator work)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    current = stack.Peek().Current;
                }
                catch (Exception e) { Debug.LogWarning($"Sigilbound: loading {key} failed: {e}"); break; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            _jobs[key] = true;
        }

        static IEnumerator EnsureLibrary(string baseModel) => Shared("lib:" + baseModel, () => LoadLibrary(baseModel));
        static IEnumerator EnsureUal() => Shared("ual", LoadUal);
        static IEnumerator EnsureBakedIndex() => Shared("baked-index", LoadBakedIndex);
        static Library Lib(string baseModel) => _libs.TryGetValue(baseModel, out var l) ? l : null;

        // Loads a base model once: the parsed glTF (instantiated per unit) and a hidden prototype
        // instance that other heroes borrow body parts from.
        static IEnumerator LoadLibrary(string baseModel)
        {
            var lib = new Library();
            byte[] bytes = null;
            yield return ReadBytes(Looks.Files[baseModel], b => bytes = b);
            if (bytes != null)
            {
                lib.Gltf = new GLTFast.GltfImport(null, null, new SigilMaterialGenerator());
                var load = lib.Gltf.LoadGltfBinary(bytes, null, new GLTFast.ImportSettings { AnimationMethod = GLTFast.AnimationMethod.Legacy, GenerateMipMaps = false });
                float t0 = Time.realtimeSinceStartup;
                while (!load.IsCompleted) yield return null;
                Trace($"lib {baseModel}: parsed={(!load.IsFaulted && load.Result)} in {Time.realtimeSinceStartup - t0:0.0}s {load.Exception?.GetBaseException().Message}");
                if (load.IsFaulted || !load.Result) lib.Failed = true;
                else
                {
                    if (_libRoot == null) { _libRoot = new GameObject("HeroLibrary"); UnityEngine.Object.DontDestroyOnLoad(_libRoot); _libRoot.SetActive(false); }
                    var proto = new GameObject(baseModel).transform; proto.SetParent(_libRoot.transform, false);
                    var inst = lib.Gltf.InstantiateMainSceneAsync(proto);
                    while (!inst.IsCompleted) yield return null;
                    Trace($"lib {baseModel}: prototype={(!inst.IsFaulted && inst.Result)} children={proto.childCount} {inst.Exception?.GetBaseException().Message}");
                    lib.Prototype = proto;
                    var head = BodyShaper.Find(proto, "Head");
                    if (head != null) lib.HeadY = proto.InverseTransformPoint(head.position).y;
                }
            }
            else lib.Failed = true;
            if (lib.Failed) Debug.LogWarning($"Sigilbound: couldn't load {Looks.Files[baseModel]}");
            _libs[baseModel] = lib;
        }

        static IEnumerator LoadUal()
        {
            byte[] bytes = null;
            var path = Path.Combine(Application.streamingAssetsPath, "Models", "anims_ual.json");
            if (path.Contains("://")) { using (var req = UnityWebRequest.Get(path)) { yield return req.SendWebRequest(); if (req.result == UnityWebRequest.Result.Success) bytes = req.downloadHandler.data; } }
            else if (File.Exists(path)) bytes = File.ReadAllBytes(path);
            if (bytes == null) yield break;
            try { _ualData = UalClips.Parse(System.Text.Encoding.UTF8.GetString(bytes)); }
            catch (Exception e) { Debug.LogWarning("Sigilbound: anims_ual.json unreadable: " + e.Message); }
        }

        // Legacy clips from the retargeted UAL data, with paths relative to the Animation component.
        // The UAL tracks use three.js node names, which drop the dots ("UpperLegL", "FootL"); the
        // Unity bones keep them ("UpperLeg.L"). Bones are matched both ways: matching only the exact
        // name bound just the dot-free bones (hips, spine, head), so in spells the arms, legs and the
        // root-parented feet never moved and the legs stretched into stilts.
        // Position tracks are applied as motion relative to the bone's rest position (from the
        // untouched library prototype): the hooded data puts Body at about twice its rest height,
        // which floated hooded heroes about a metre while casting.
        static List<AnimationClip> BuildUal(string baseModel, Transform animRoot)
        {
            var src = UalClips.SourceFor(baseModel);
            if (_ualData == null || src == null || !_ualData.TryGetValue(src, out var clips)) return new List<AnimationClip>();
            var paths = new Dictionary<string, string>();
            void Index(Transform t, string p)
            {
                foreach (Transform c in t)
                {
                    var cp = p.Length == 0 ? c.name : p + "/" + c.name;
                    if (!paths.ContainsKey(c.name)) paths[c.name] = cp;
                    var bare = c.name.Replace(".", "");
                    if (!paths.ContainsKey(bare)) paths[bare] = cp;
                    Index(c, cp);
                }
            }
            Index(animRoot, "");
            var proto = Lib(baseModel)?.Prototype;
            Vector3? Rest(string bone)
            {
                if (proto == null) return null;
                foreach (var t in proto.GetComponentsInChildren<Transform>(true))
                    if (t.name == bone || t.name.Replace(".", "") == bone) return t.localPosition;
                return null;
            }
            var outp = new List<AnimationClip>();
            foreach (var c in clips)
            {
                var clip = new AnimationClip { name = c.Name, legacy = true, frameRate = (float)c.Fps };
                foreach (var tr in c.Rotations)
                {
                    if (!paths.TryGetValue(tr.Key, out var path)) continue;
                    var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                    Quaternion prev = Quaternion.identity;
                    for (int i = 0; i < c.Frames; i++)
                    {
                        float t = i / (float)c.Fps; var v = tr.Value;
                        var q = new Quaternion(v[i * 4], -v[i * 4 + 1], -v[i * 4 + 2], v[i * 4 + 3]); // three.js -> Unity (mirror X)
                        if (i > 0 && Quaternion.Dot(prev, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        prev = q;
                        cx.AddKey(t, q.x); cy.AddKey(t, q.y); cz.AddKey(t, q.z); cw.AddKey(t, q.w);
                    }
                    clip.SetCurve(path, typeof(Transform), "localRotation.x", cx); clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
                    clip.SetCurve(path, typeof(Transform), "localRotation.z", cz); clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
                }
                foreach (var tr in c.Positions)
                {
                    if (!paths.TryGetValue(tr.Key, out var path)) continue;
                    var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
                    var v = tr.Value;
                    // Offset so the clip's first frame sits at the bone's rest position (Unity space: X mirrored).
                    var rest = Rest(tr.Key);
                    var off = rest.HasValue ? rest.Value - new Vector3(-v[0], v[1], v[2]) : Vector3.zero;
                    for (int i = 0; i < c.Frames; i++) { float t = i / (float)c.Fps; px.AddKey(t, -v[i * 3] + off.x); py.AddKey(t, v[i * 3 + 1] + off.y); pz.AddKey(t, v[i * 3 + 2] + off.z); }
                    clip.SetCurve(path, typeof(Transform), "localPosition.x", px); clip.SetCurve(path, typeof(Transform), "localPosition.y", py); clip.SetCurve(path, typeof(Transform), "localPosition.z", pz);
                }
                clip.EnsureQuaternionContinuity();
                outp.Add(clip);
            }
            return outp;
        }

        // Builds a hero under `parent`. Calls done(null) if it can't.
        public static IEnumerator Build(Unit unit, GearLook gear, Transform parent, Action<HeroModel> done)
        {
            bool titan = unit.Titan;
            string id = unit.Template.Id;
            var look = titan ? null : Looks.Of(id);
            string baseModel = titan ? "titan" : look.Base;
            var body = titan ? new BodySpec() : HeroArt.Body(id);
            var kit = titan ? new Dictionary<string, object>() : HeroArt.Kit(id);
            bool hollow = !unit.Ally && !unit.Rival && !titan;
            var parts = new Dictionary<string, string>(body.Parts);
            if (kit.TryGetValue("robe", out var robe) && (robe as string) == "long" && !parts.ContainsKey("legs")) parts["legs"] = "none"; // a long robe replaces the legs

            // A free-path hero baked in Blender (Tools~/blenderforge/quat.py) replaces the runtime build
            // for that hero (not its Hollow enemy copy, nor the Titan); anything missing falls back below.
            Trace($"build {id} base={baseModel} hollow={hollow}");
            if (titan)
            {
                // The high-detail Titan (heroes_hd/SB_Hero_<TitanSpec.Model>_HD.glb, from titan.json), else the old stone yeti below.
                yield return EnsureHdIndex();
                string tid = TitanSpec.Current.Model;
                if (_hd != null && _hd.Contains(tid))
                {
                    HeroModel hd = null;
                    yield return BuildHd(unit, new BodySpec(), parent, m => hd = m, tid, false, true);
                    Trace($"hd titan: {(hd != null ? "ok" : "FAILED, falling back")}");
                    if (hd != null) { done(hd); yield break; }
                }
            }
            if (!titan && hollow)
            {
                // Hollow copies use the hero's high-detail model too (PLAN step 58), darkened and crimson-lit.
                yield return EnsureHdIndex();
                if (_hd != null && _hd.Contains(id))
                {
                    HeroModel hd = null;
                    yield return BuildHd(unit, body, parent, m => hd = m, null, true);
                    Trace($"hd hollow {id}: {(hd != null ? "ok" : "FAILED, falling back")}");
                    if (hd != null) { done(hd); yield break; }
                }
            }
            if (!titan && !hollow)
            {
                yield return EnsureBakedIndex();
                // A high-detail hero (phase F, blenderforge/ubc.py) wins over the free-path bake.
                yield return EnsureHdIndex();
                if (_hd != null && _hd.Contains(id))
                {
                    HeroModel hd = null;
                    yield return BuildHd(unit, body, parent, m => hd = m);
                    Trace($"hd {id}: {(hd != null ? "ok" : "FAILED, falling back")}");
                    if (hd != null) { done(hd); yield break; }
                }
                bool isBaked = _baked != null && _baked.Contains(id);
                Trace($"baked index: {(_baked == null ? "none" : string.Join(",", _baked))}; {id} baked={isBaked}");
                if (isBaked)
                {
                    HeroModel built = null;
                    yield return BuildBaked(unit, look, body, parent, m => built = m);
                    Trace($"baked {id}: {(built != null ? "ok" : "FAILED, falling back")}");
                    if (built != null) { done(built); yield break; }
                }
            }

            yield return EnsureLibrary(baseModel);
            foreach (var src in parts.Values.Where(v => v != "none" && v != baseModel).Distinct()) yield return EnsureLibrary(src);
            yield return EnsureUal();
            var lib = Lib(baseModel);
            if (lib == null || lib.Failed || parent == null) { Trace($"build {id}: no library (failed={lib?.Failed}, parent={(parent != null)})"); done(null); yield break; }

            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);
            holder.localPosition = BuildAway; // out of sight while it instantiates (no bind pose on screen; see Reveal)
            var inst = lib.Gltf.InstantiateMainSceneAsync(holder);
            while (!inst.IsCompleted) yield return null;
            if (inst.IsFaulted || !inst.Result || holder == null) { Trace($"build {id}: instantiate failed {inst.Exception?.GetBaseException().Message}"); if (holder) UnityEngine.Object.Destroy(holder.gameObject); done(null); yield break; }
            holder.localPosition = Vector3.zero;

            var bones = new Dictionary<string, Transform>();
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            if (!titan)
            {
                BodyShaper.TagParts(holder, baseModel);
                var protos = _libs.Where(kv => kv.Value.Prototype != null).ToDictionary(kv => kv.Key, kv => kv.Value.Prototype);
                BodyShaper.SwapParts(holder, baseModel, parts, bones, protos);
                // The code-built sword replaces the hooded model's own; packs go under capes and backpieces.
                if (bones.TryGetValue("Sword", out var sword)) sword.gameObject.SetActive(false);
                bool packOff = kit.ContainsKey("cape") || (kit.TryGetValue("pack", out var pk) && pk is bool pb && !pb) || kit.ContainsKey("back");
                if (packOff && bones.TryGetValue("Backpack", out var pack)) pack.gameObject.SetActive(false);
            }
            // Rest positions for the character shader's detail textures (before anything moves).
            foreach (var smr0 in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true)) CharacterLook.WriteRestPositions(smr0, holder);
            Recolour(holder, baseModel, look, body, hollow, unit.El);

            // Height: head at 1.85 m (3.6 for the Titan) times the hero's build (three3d.mjs).
            float k = (titan ? 3.6f : 1.85f) / Mathf.Max(0.1f, lib.HeadY) * (float)body.H;
            holder.localScale = Vector3.one * k;
            foreach (var r in holder.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            // Fresnel rim in the element colour (red for Hollow enemies), as applyCharacterLook.
            var rim = hollow ? Palette.Hex("#ff3355") : Palette.Element(unit.El);
            if (!titan)
            {
                float hipY = bones.TryGetValue("Hips", out var hips) ? holder.InverseTransformPoint(hips.position).y : 0.9f;
                var sockets = KitBuilder.Build(id, hollow, gear, hipY);
                KitAssembler.Attach(holder, bones, sockets, $"{id}|{(hollow ? 1 : 0)}|{baseModel}|{gear?.Key ?? "-"}|{string.Join(",", parts.Select(p => p.Key + p.Value))}", rim);
                BodyShaper.ShapeBody(holder, bones, body);
            }
            else KitAssembler.Attach(holder, bones, KitBuilder.BuildTitan(unit.El), $"titan|{unit.El}", Color.black); // stone horns, brow, tusks, shards

            var anim = holder.GetComponentInChildren<Animation>();
            if (anim != null && !titan)
            {
                if (lib.Ual == null) lib.Ual = BuildUal(baseModel, anim.transform);
                foreach (var c in lib.Ual) anim.AddClip(c, c.name);
            }
            Trace($"build {id}: done, anim={(anim != null ? anim.GetClipCount() + " clips" : "none")}");
            done(Reveal(holder, new HeroModel(holder.gameObject, anim, (titan ? 4.7f : 2.3f) * (float)body.H, titan ? AnimStyle.TitanStyle : AnimStyle.For(id))));
        }

        // ---------- high-detail heroes (phase F) ----------
        static HashSet<string> _hd, _hdLod1;

        // The parsed high-detail models, by cache key (hero id, "|hollow" for enemy copies, "titan|<element>",
        // and "|lo" on the Low tier, which loads no normal map and the LOD1 mesh where one ships).
        // Bounded (code review H1, performance review #1): an entry counts the figures built from it (HdRef);
        // entries no figure uses are kept up to HdIdleCap, least recently used first out, and an evicted entry
        // frees its glTF meshes, material and textures. Before this every hero ever shown stayed in memory
        // (about 24 MB each), which could get the game killed on 4-6 GB phones.
        sealed class HdEntry
        {
            public GLTFast.GltfImport Gltf; public Material Mat;
            public readonly List<Texture2D> Tex = new List<Texture2D>();
            public int Live; public float Used;
        }
        static readonly Dictionary<string, HdEntry> _hdLibs = new Dictionary<string, HdEntry>();
        public const int HdIdleCap = 8;    // unused models kept for quick reuse (menu lineup, the next battle)
        const int HdIdleHardCap = 16;      // ...or this many when they were all used or preloaded just now
        const float HdRecentSeconds = 30f; // a preloaded model waits this long for its battle before it may go
        public static int HdCached => _hdLibs.Count;
        static float _lastUnload = -100f;

        // On every high-detail figure: tells the cache when the figure is gone.
        sealed class HdRef : MonoBehaviour
        {
            public string Key;
            void OnDestroy() => ReleaseHd(Key);
        }
        static void ReleaseHd(string key)
        {
            if (key != null && _hdLibs.TryGetValue(key, out var e)) { e.Live = Math.Max(0, e.Live - 1); e.Used = Time.unscaledTime; }
        }
        static bool LowTier => Quality.I != null && Quality.I.Tier == 0;
        static string HdKey(string id, Element el, bool titan, bool hollow)
        {
            string k = titan ? $"titan|{el}" : hollow ? id + "|hollow" : id; // the Titan's runes take its current element
            return LowTier ? k + "|lo" : k;
        }

        // Frees models no figure uses beyond the cap, then (at most every few seconds) unused assets
        // (leaked materials and textures included). Called after screen changes and when a battle ends or
        // starts; waits two frames so figures destroyed this frame have let go of their models.
        public static void TrimSoon()
        {
            if (_loader == null) { var go = new GameObject("HeroModelLoader"); UnityEngine.Object.DontDestroyOnLoad(go); _loader = go.AddComponent<Loader>(); }
            _loader.StartCoroutine(TrimLater());
        }
        static IEnumerator TrimLater() { yield return null; yield return null; Trim(true); }
        public static void Trim(bool unloadAssets)
        {
            EvictIdle();
            if (unloadAssets && Time.unscaledTime - _lastUnload > 3f) { _lastUnload = Time.unscaledTime; Resources.UnloadUnusedAssets(); }
        }
        static void EvictIdle()
        {
            var entries = _hdLibs.Select(kv => new CachePolicy.Entry(kv.Key, kv.Value.Live, kv.Value.Used)).ToList();
            foreach (var key in CachePolicy.Evict(entries, Time.unscaledTime, HdIdleCap, HdIdleHardCap, HdRecentSeconds)) EvictHd(key);
        }
        static void EvictHd(string key)
        {
            if (!_hdLibs.TryGetValue(key, out var e)) return;
            _hdLibs.Remove(key);
            ForgetHdJobs(key);
            try { e.Gltf?.Dispose(); } catch (Exception ex) { Debug.LogWarning($"Sigilbound: disposing {key}: {ex.Message}"); }
            if (e.Mat != null) UnityEngine.Object.Destroy(e.Mat);
            foreach (var t in e.Tex) if (t != null) UnityEngine.Object.Destroy(t);
            Trace($"hd cache: evicted {key} ({_hdLibs.Count} left)");
        }
        // Lets the model load again later: after an eviction, or after a failed load (a transient read
        // error used to leave the hero as a block stand-in for the rest of the session; code review L5).
        static void ForgetHdJobs(string key)
        {
            foreach (var j in new[] { "hd:" + key, "preload:" + key })
                if (_jobs.TryGetValue(j, out var finished) && finished) _jobs.Remove(j);
        }

        static IEnumerator EnsureHdIndex() => Shared("hd-index", LoadHdIndex);
        static Dictionary<string, (float h, float b)> _hdBody;
        static IEnumerator EnsureHdBodies() => Shared("hd-body", LoadHdBodies);
        static IEnumerator LoadHdBodies()
        {
            var d = new Dictionary<string, (float, float)>();
            byte[] bytes = null;
            yield return ReadBytes(Path.Combine("heroes_hd", "body.json"), b => bytes = b);
            if (bytes != null)
            {
                try
                {
                    // {"id":[height,bulk],...}
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.UTF8.GetString(bytes), @"""([a-z]+)"":\[([0-9.]+),([0-9.]+)\]"))
                        d[m.Groups[1].Value] = (float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
                }
                catch (Exception e) { Debug.LogWarning("Sigilbound: heroes_hd/body.json unreadable: " + e.Message); }
            }
            _hdBody = d;
        }
        static IEnumerator LoadHdIndex()
        {
            var set = new HashSet<string>();
            byte[] bytes = null;
            yield return ReadBytes(Path.Combine("heroes_hd", "index.txt"), b => bytes = b);
            if (bytes != null)
                foreach (var line in System.Text.Encoding.UTF8.GetString(bytes).Split('\n'))
                    if (line.Trim().Length > 0) set.Add(line.Trim());
            _hd = set;
            // The Titan's model hookup (TitanSpec): which GLB, its size, face, horns, feet, reach and seams.
            bytes = null;
            yield return ReadRaw(Path.Combine("heroes_hd", "titan.json"), b => bytes = b, false);
            if (bytes != null)
            {
                try { TitanSpec.Current = TitanSpec.Parse(System.Text.Encoding.UTF8.GetString(bytes)); }
                catch (Exception e) { Debug.LogWarning("Sigilbound: heroes_hd/titan.json unreadable, keeping the defaults: " + e.Message); }
            }
            bytes = null;
            // Heroes that ship a LOD1 mesh for the Low tier (prepare-models.mjs). None do yet; without the
            // list every Low-tier load asked for a missing file first (166 misses in one tour).
            var lod = new HashSet<string>(); bytes = null;
            yield return ReadRaw(Path.Combine("heroes_hd", "index_lod1.txt"), b => bytes = b, false);
            if (bytes != null)
                foreach (var line in System.Text.Encoding.UTF8.GetString(bytes).Split('\n'))
                    if (line.Trim().Length > 0) lod.Add(line.Trim());
            _hdLod1 = lod;
        }

        // The painted-character material for the baked atlas: albedo, tangent normal, and a mask whose
        // blue channel is the glow (from the _glow PNG) so the eyes, runes and auras light up.
        // The Hollow look on a painted albedo (as Recolour does for the old models): half desaturated and
        // pulled halfway toward deep violet, so enemy copies read as shadows of the hero.
        // Pixel loops only (no Unity calls), so they run on a worker thread: 1M pixels for the tint, 262k for the mask.
        static void HollowTint(Color32[] px)
        {
            var v = new Color32(0x2e, 0x1f, 0x33, 255);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i]; int g = (c.r * 3 + c.g * 6 + c.b) / 10;
                int r = (c.r + g) / 2, gg = (c.g + g) / 2, b = (c.b + g) / 2;
                px[i] = new Color32((byte)((r + v.r) / 2), (byte)((gg + v.g) / 2), (byte)((b + v.b) / 2), c.a);
            }
        }
        // The mask's blue channel is the glow (eyes, runes, auras), from the _glow PNG's brightness.
        static Color32[] MaskFromGlow(Color32[] g)
        {
            var mask = new Color32[g.Length];
            for (int i = 0; i < g.Length; i++) { byte lum = (byte)Math.Min(255, (g[i].r * 3 + g[i].g * 6 + g[i].b) / 10 * 2); mask[i] = new Color32(0, 255, lum, 90); }
            return mask;
        }
        static IEnumerator OnWorker(Action work)
        {
            var job = System.Threading.Tasks.Task.Run(work);
            while (!job.IsCompleted) yield return null;
            if (job.IsFaulted) Debug.LogWarning("Sigilbound: hero texture work failed: " + job.Exception?.GetBaseException().Message);
        }

        // Low quality: the high-detail heroes' normal maps off (on again above Low). Applies to every loaded
        // hero material now and to any loaded later.
        static bool _lowDetail;
        public static void SetLowDetail(bool low)
        {
            _lowDetail = low;
            foreach (var kv in _hdLibs) NormalMapOn(kv.Value.Mat);
        }
        static void NormalMapOn(Material m)
        {
            if (m == null || !m.HasProperty("_BumpMap")) return;
            if (!_lowDetail && m.GetTexture("_BumpMap") != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        }

        static Material HdMaterial(string id, Texture2D alb, Texture2D nrm, Texture2D maskTex, Element el, bool hollow, bool titan = false)
        {
            var sh = Shader.Find("Sigilbound/CharacterPainted");
            var tmpl = Resources.Load<Material>("SigilMaterials/HeroPainted");
            Material mat = tmpl != null ? new Material(tmpl) : sh != null ? new Material(sh) : new Material(Materials.Lit(Color.white, 0, 0.3f));
            mat.name = "HD_" + id;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", alb);
            mat.mainTexture = alb;
            if (nrm != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
            if (mat.HasProperty("_MaskMap") && maskTex != null)
            {
                mat.SetTexture("_MaskMap", maskTex);
                if (mat.HasProperty("_GlowColor")) mat.SetColor("_GlowColor", titan ? TitanSeam(el) * (float)TitanSpec.Current.GlowGain : (hollow ? Palette.Hex("#ff3355") : Palette.Element(el)) * 2.2f);
            }
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", titan ? TitanSeam(el) : hollow ? Palette.Hex("#ff3355") : Palette.Element(el));
            // The Titan's seams and visor glow only on front faces (art review H3): single-sided, whatever the
            // template says, so no glow island can show through the far side of a plate.
            if (titan && mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            if (hollow) mat.name += "_Hollow";
            NormalMapOn(mat);
            return mat;
        }

        // The Titan's seam colour for the day's element: titan.json "seams", else the element colour.
        public static Color TitanSeam(Element el) { var h = TitanSpec.Current.SeamHex(el); return h != null ? Palette.Hex(h) : Palette.Element(el); }

        // Gives the triangles inside TitanSpec.GlowCut boxes a non-glowing texel (copies of the meshes, cached).
        static readonly Dictionary<int, Mesh> _glowCut = new Dictionary<int, Mesh>();
        static void ApplyGlowCut(Transform holder)
        {
            var spec = TitanSpec.Current;
            if (spec.GlowCut.Count == 0) return;
            foreach (var r in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var src = r.sharedMesh;
                if (src == null || !src.isReadable) continue;
                if (!_glowCut.TryGetValue(src.GetInstanceID(), out var cut))
                {
                    var v = src.vertices; var uv = new List<Vector2>(); src.GetUVs(0, uv);
                    if (uv.Count != v.Length) continue;
                    var to = new Vector2((float)spec.GlowCutUv[0], (float)spec.GlowCutUv[1]); int n = 0;
                    for (int sm = 0; sm < src.subMeshCount; sm++)
                    {
                        var t = src.GetTriangles(sm);
                        for (int i = 0; i < t.Length; i += 3)
                        {
                            var c = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                            if (!spec.InGlowCut(c.x, c.y, c.z)) continue;
                            uv[t[i]] = uv[t[i + 1]] = uv[t[i + 2]] = to; n++;
                        }
                    }
                    cut = UnityEngine.Object.Instantiate(src); cut.name = src.name + "_glowcut"; cut.SetUVs(0, uv);
                    _glowCut[src.GetInstanceID()] = cut;
                    Trace($"titan glow cut: {n} triangles");
                }
                r.sharedMesh = cut;
            }
        }

        // Reads and parses one high-detail model and its textures into the cache (a shared job: it runs
        // once per model, survives the view that asked for it, and can be started early by Preload).
        static IEnumerator LoadHd(string id, string cacheKey, Element el, bool hollow)
        {
            if (_hdLibs.ContainsKey(cacheKey)) yield break;
            {
                byte[] glb = null, alb = null, nrm = null, glow = null;
                bool low = cacheKey.EndsWith("|lo"), lod1 = low && _hdLod1 != null && _hdLod1.Contains(id);
                if (lod1) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_LOD1.glb"), b => glb = b);
                if (glb == null) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD.glb"), b => glb = b);
                // Textures ship as JPEG (blenderforge/compress); PNG is still read for older builds.
                yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_albedo.jpg"), b => alb = b);
                if (alb == null) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_albedo.png"), b => alb = b);
                if (!low) // Low draws no normal maps (SetLowDetail), so it doesn't load them
                {
                    yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_normal.jpg"), b => nrm = b);
                    if (nrm == null) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_normal.png"), b => nrm = b);
                }
                yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_glow.png"), b => glow = b);
                if (glb == null || alb == null) { yield break; }
                var gltf = new GLTFast.GltfImport(null, null, new SigilMaterialGenerator());
                var load = gltf.LoadGltfBinary(glb, null, new GLTFast.ImportSettings { AnimationMethod = GLTFast.AnimationMethod.Legacy, GenerateMipMaps = false });
                while (!load.IsCompleted) yield return null;
                if (load.IsFaulted || !load.Result) { Debug.LogWarning($"Sigilbound: couldn't load the high-detail {id}"); gltf.Dispose(); yield break; }
                // GPU copies only: the albedo stays readable just long enough for the Hollow tint, and the glow
                // map only until the mask is built (it used to stay in memory, unused).
                // Spread over frames (performance review #4): each decode or upload takes its own HeavySlot frame
                // and the pixel loops run on a worker, where all of it used to run in the one frame after the parse
                // (estimated 100-200 ms per hero on a mid-range phone). SBPERF lines log each step's main-thread time.
                var sw = System.Diagnostics.Stopwatch.StartNew(); double tAlb, tTint = 0, tNrm, tGlow, tMask = 0;
                yield return HeavySlot(); sw.Restart();
                var albTex = LoadPng(alb, false, hollow);
                tAlb = sw.Elapsed.TotalMilliseconds;
                if (hollow && albTex != null)
                {
                    var px = albTex.GetPixels32();
                    yield return OnWorker(() => HollowTint(px));
                    yield return HeavySlot(); sw.Restart();
                    if (albTex != null) { albTex.SetPixels32(px); albTex.Apply(true, true); } // upload, then drop the CPU copy
                    tTint = sw.Elapsed.TotalMilliseconds;
                }
                yield return HeavySlot(); sw.Restart();
                var nrmTex = LoadPng(nrm, true, false);
                tNrm = sw.Elapsed.TotalMilliseconds;
                yield return HeavySlot(); sw.Restart();
                var glowTex = LoadPng(glow, false, true);
                Color32[] glowPx = null; int gw = 0, gh = 0;
                if (glowTex != null) { glowPx = glowTex.GetPixels32(); gw = glowTex.width; gh = glowTex.height; UnityEngine.Object.Destroy(glowTex); }
                tGlow = sw.Elapsed.TotalMilliseconds;
                Texture2D maskTex = null;
                if (glowPx != null)
                {
                    Color32[] mask = null;
                    yield return OnWorker(() => mask = MaskFromGlow(glowPx));
                    yield return HeavySlot(); sw.Restart();
                    if (mask != null) { maskTex = new Texture2D(gw, gh, TextureFormat.RGBA32, true, true); maskTex.SetPixels32(mask); maskTex.Apply(true, true); }
                    tMask = sw.Elapsed.TotalMilliseconds;
                }
                double longest = Math.Max(Math.Max(tAlb, tTint), Math.Max(tNrm, Math.Max(tGlow, tMask)));
                Debug.Log($"SBPERF hd {cacheKey}: main thread albedo {tAlb:0.0} ms, tint {tTint:0.0}, normal {tNrm:0.0}, glow {tGlow:0.0}, mask {tMask:0.0}; longest frame step {longest:0.0} ms of {tAlb + tTint + tNrm + tGlow + tMask:0.0} ms (inflate and pixel loops on a worker)");
                if (_hdLibs.ContainsKey(cacheKey)) // loaded meanwhile by another job: keep that one
                {
                    gltf.Dispose(); foreach (var t in new[] { albTex, nrmTex, maskTex }) if (t != null) UnityEngine.Object.Destroy(t);
                    yield break;
                }
                var e = new HdEntry { Gltf = gltf, Used = Time.unscaledTime };
                e.Mat = HdMaterial(id, albTex, nrmTex, maskTex, el, hollow, cacheKey.StartsWith("titan|"));
                e.Tex.Add(albTex); e.Tex.Add(nrmTex); e.Tex.Add(maskTex);
                _hdLibs[cacheKey] = e;
                EvictIdle();
            }
        }

        // Starts loading these heroes' high-detail models in the background (menu lineup, the player's
        // team, the Titan before its fight), so their figures are ready when they are first shown.
        public static void Preload(IEnumerable<(string id, Element el, bool titan, bool hollow)> units)
        {
            if (!Available) return;
            foreach (var u in units)
            {
                var (id, el, titan, hollow) = u;
                string key = HdKey(id, el, titan, hollow);
                Kick("preload:" + key, () => PreloadOne(titan ? null : id, key, el, hollow));
            }
        }
        // id null: the Titan, whose model id comes from titan.json (read with the index).
        static IEnumerator PreloadOne(string id, string key, Element el, bool hollow)
        {
            yield return EnsureHdIndex();
            if (id == null) id = TitanSpec.Current.Model;
            if (_hd == null || !_hd.Contains(id)) yield break;
            yield return EnsureLibrary("ubc");
            yield return Shared("hd:" + key, () => LoadHd(id, key, el, hollow));
            if (!_hdLibs.ContainsKey(key) && _jobs.TryGetValue("hd:" + key, out var fin) && fin) _jobs.Remove("hd:" + key); // retry on the next request
        }
        // Starts a shared job without waiting for it.
        static void Kick(string key, Func<IEnumerator> work)
        {
            if (_jobs.ContainsKey(key)) return;
            if (_loader == null) { var go = new GameObject("HeroModelLoader"); UnityEngine.Object.DontDestroyOnLoad(go); _loader = go.AddComponent<Loader>(); }
            _jobs[key] = false;
            _loader.StartCoroutine(RunJob(key, work()));
        }

        // Removes every clip from an Animation (glTFast's auto-added ones) and its default clip.
        public static void ClearClips(Animation anim)
        {
            if (anim == null) return;
            var names = new List<string>();
            foreach (AnimationState st in anim) if (st != null) names.Add(st.name);
            foreach (var n in names) anim.RemoveClip(n);
            anim.clip = null;
        }

        static IEnumerator BuildHd(Unit unit, BodySpec body, Transform parent, Action<HeroModel> done, string hdId = null, bool hollow = false, bool titan = false)
        {
            string id = hdId ?? unit.Template.Id;
            string cacheKey = HdKey(id, unit.El, titan, hollow);
            yield return EnsureLibrary("ubc");
            var lib = Lib("ubc");
            if (lib == null || lib.Failed || parent == null) { done(null); yield break; }
            yield return Shared("hd:" + cacheKey, () => LoadHd(id, cacheKey, unit.El, hollow));
            if (!_hdLibs.TryGetValue(cacheKey, out var bk)) { ForgetHdJobs(cacheKey); done(null); yield break; }
            if (parent == null) { done(null); yield break; }
            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);
            bk.Live++; bk.Used = Time.unscaledTime;
            holder.gameObject.AddComponent<HdRef>().Key = cacheKey; // released when the figure is destroyed
            // Never seen in its bind (T) pose: the instance is built far below the arena, then kept unrendered until
            // its clips are on and the idle is sampled (in-house report 2026-11-28: Home at 2400x1080 caught the
            // heroes with their arms straight out while EnsureHdBodies was still loading). See Reveal.
            holder.localPosition = BuildAway;
            var inst = bk.Gltf.InstantiateMainSceneAsync(holder);
            while (!inst.IsCompleted) yield return null;
            if (inst.IsFaulted || !inst.Result || holder == null || holder.childCount == 0) { if (holder) UnityEngine.Object.Destroy(holder.gameObject); done(null); yield break; }
            holder.localPosition = Vector3.zero;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                r.forceRenderingOff = true;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = bk.Mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            // The clips' curve paths start at the glTF root node ("Armature/pelvis/..."), so the Animation must
            // sit on the object ABOVE that node. glTFast only makes a "Scene" object for files with several
            // root nodes; the high-detail files have one (Armature), which lands straight under the holder.
            // With the Animation on Armature itself no path matched and every hero stood in its bind pose.
            var root = holder.Find("Armature") != null ? holder.gameObject : holder.GetChild(0).gameObject;
            // Creature heroes (Core/Kit/Creatures.cs): their own clips, fixed scale, planted feet (CreatureRig).
            var creature = Creatures.Of(id);
            if (creature != null) { done(Reveal(holder, CreatureRig.Setup(holder.gameObject, root, bk.Gltf.GetAnimationClips(), creature))); yield break; }
            var anim = root.GetComponent<Animation>() ?? root.AddComponent<Animation>();
            // glTFast already put the model's own clips on this Animation (it adds one for any file with clips),
            // under their file names: a digitigrade hero's baked leg clips carry the shared clips' names. Left
            // there, they took the shared clips' place (the GetClip check below skips names that exist), no clip
            // moved the body and RigExtras' rest fix compounded every frame (build 180: Tansy and Corran
            // exploded). Start empty; the model's clips go back where they belong (an embedded Titan's below,
            // DigitigradeLegs' layered "~legs" twins).
            ClearClips(anim);
            var spec = titan ? TitanSpec.Current : null;
            // A separately rigged Titan brings its own clips ("clips": "embedded"); the shared UAL set is added
            // too, and only clips whose names the model doesn't have.
            if (spec != null && spec.Clips == "embedded")
                foreach (var c in bk.Gltf.GetAnimationClips() ?? new AnimationClip[0]) if (c != null && !c.name.Contains("TPose")) anim.AddClip(c, c.name);
            foreach (var c in lib.Gltf.GetAnimationClips() ?? new AnimationClip[0]) if (c != null && !c.name.Contains("TPose") && anim.GetClip(c.name) == null) anim.AddClip(c, c.name);
            anim.playAutomatically = false; // never fall back to a default clip (the T-pose reference)
            // Creature rig extensions (tail, jaw, longer neck, digitigrade legs, wing bones; kit_rig.py):
            // re-apply this hero's rest-pose changes over the humanoid clips and drive the extra bones.
            bool rigExt = RigExtras.Needed(holder);
            if (rigExt) root.AddComponent<RigExtras>().Init(holder, lib.Prototype, anim);
            // Digitigrade legs (animal-folk; blenderforge/i3d_legs.py): the hero's own baked leg clips, layered
            // over the shared clips of the same name on the legs only.
            if (DigitigradeLegs.Needed(holder)) root.AddComponent<DigitigradeLegs>().Init(holder, anim, bk.Gltf.GetAnimationClips());
            // Spring bones (skirts, robes, coat tails; Core/SpringSolver.cs): simulated after the clip, off the legs.
            if (SpringBones.Needed(holder)) root.AddComponent<SpringBones>().Init(holder, id);
            if (titan) { root.AddComponent<HandGrip>(); ApplyGlowCut(holder); }
            else if (AnimStyle.HdGrips(id)) root.AddComponent<HandGrip>().Side = Impact.FamilyOf(id) == WeaponFamily.Bow ? "l" : "r"; // bows: the left hand
            // The rig is in metres with the head bone near 1.6 m: scale to the battle's hero height.
            float headY = 1.6f;
            var head = BodyShaper.Find(holder, spec != null ? spec.HeadBone : "Head");
            if (head != null) headY = Mathf.Max(0.5f, holder.InverseTransformPoint(head.position).y);
            else if (spec != null) { float top = MeshTop(holder); if (top > 0.1f) headY = top / (float)spec.HornTop; } // no head bone: the mesh top is the horn tips
            // a creature's longer neck raises its head (not the Titan: titan.json names its head bone): size it by where a humanoid head would sit
            var neck = rigExt && !titan ? BodyShaper.Find(holder, "neck_01") : null;
            if (neck != null) headY = Mathf.Max(0.5f, holder.InverseTransformPoint(neck.position).y + 0.08f);
            // Height and build from the approved design (heroes_hd/body.json): gnomes stand short, golems tall and broad.
            yield return EnsureHdBodies();
            float hgt = (float)body.H, bulk = 1f;
            if (_hdBody != null && _hdBody.TryGetValue(id, out var hb)) { hgt = hb.h; bulk = hb.b; }
            // The high-detail Titan towers over the team: its head lands at TitanSpec.Head in the battle (6.24 m;
            // heroes 1.85). The model sits inside the view's own Titan scale (ViewScale, 1.3), so it is built to
            // Head / ViewScale here; it used to take the full 6.24 m and the view scaled it again to 8.1 m,
            // which put its horns through its name tag and out of every framed camera (art review H3).
            float k = (titan ? (float)spec.ModelHead : 1.85f * hgt) / headY;
            if (titan) bulk = (float)spec.Bulk; // a broad stone colossus, not a stretched hero
            holder.localScale = new Vector3(k * bulk, k, k * bulk);
            // Feet on the ground: the lowest point of the mesh at y 0, plus the data's offset.
            if (titan)
            {
                float lift = (float)spec.FootOffset;
                if (spec.AutoGround) { float low = MeshBottom(holder); if (!float.IsNaN(low)) lift -= low; }
                holder.localPosition += Vector3.up * lift / Mathf.Max(0.01f, holder.parent != null ? holder.parent.lossyScale.y : 1f);
            }
            done(Reveal(holder, titan ? new HeroModel(holder.gameObject, anim, (float)(spec.HornWorld / spec.ViewScale), AnimStyle.TitanHd, TitanAliases())
                                      : new HeroModel(holder.gameObject, anim, 2.3f * hgt, AnimStyle.Hd(id), AnimStyle.HdAliasesFor(id))));
        }

        // Where a model is instantiated (out of every camera) until its instance is complete.
        static readonly Vector3 BuildAway = new Vector3(0, -1000, 0);
        // Shows a built model: its idle (started by the HeroModel) sampled now, then its renderers on, so the first
        // frame it is drawn is already the idle pose, not the bind pose.
        static HeroModel Reveal(Transform holder, HeroModel m)
        {
            if (holder == null) return m;
            if (m != null && m.Anim != null) m.Anim.Sample();
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = false;
            return m;
        }

        static Dictionary<string, string> TitanAliases()
        {
            var d = new Dictionary<string, string>(AnimStyle.TitanHdAliases);
            foreach (var kv in TitanSpec.Current.Aliases) d[kv.Key] = kv.Value;
            return d;
        }
        // Lowest / highest point of a model's meshes in world metres above the view's root, NaN / 0 if none.
        static float MeshBottom(Transform holder)
        {
            float low = float.PositiveInfinity; var root = holder.parent != null ? holder.parent : holder;
            // From the meshes' bind-pose bounds (a renderer's world bounds aren't refreshed until it is drawn).
            foreach (var r in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var b = r.sharedMesh.bounds;
                for (int i = 0; i < 4; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.min.y, (i & 2) == 0 ? b.min.z : b.max.z);
                    low = Mathf.Min(low, r.transform.TransformPoint(corner).y - root.position.y);
                }
            }
            return float.IsInfinity(low) ? float.NaN : low;
        }
        static float MeshTop(Transform holder)
        {
            float top = 0;
            foreach (var r in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh != null) top = Mathf.Max(top, r.sharedMesh.bounds.max.y);
            return top;
        }

        // ---------- baked free-path heroes ----------
        static HashSet<string> _baked;
        static readonly Dictionary<string, (GLTFast.GltfImport gltf, Material mat)> _bakedLibs = new Dictionary<string, (GLTFast.GltfImport, Material)>();

        static IEnumerator LoadBakedIndex()
        {
            var set = new HashSet<string>();
            byte[] bytes = null;
            yield return ReadBytes(Path.Combine("heroes", "index.txt"), b => bytes = b);
            if (bytes != null)
                foreach (var line in System.Text.Encoding.UTF8.GetString(bytes).Split('\n'))
                    if (line.Trim().Length > 0) set.Add(line.Trim());
            _baked = set;
        }

        // Decodes a JPEG/PNG into a mip-mapped texture. Unless the caller still needs the pixels, the CPU copy
        // is dropped after upload (markNonReadable), which halves its memory.
        static Texture2D LoadPng(byte[] bytes, bool linear, bool readable = false)
        {
            if (bytes == null) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
            if (t.LoadImage(bytes, !readable)) return t;
            UnityEngine.Object.Destroy(t);
            return null;
        }

        // The baked GLB (same skeleton as its base model, no clips, no material) with the painted
        // albedo + glow PNGs, animated by the base model's own clips (identical node names and rest
        // transforms) plus the shared UAL clips.
        static IEnumerator BuildBaked(Unit unit, HeroLook look, BodySpec body, Transform parent, Action<HeroModel> done)
        {
            string id = unit.Template.Id, baseModel = Looks.BakedBaseOf(id);
            yield return EnsureLibrary(baseModel);
            yield return EnsureUal();
            var lib = Lib(baseModel);
            if (lib == null || lib.Failed || parent == null) { done(null); yield break; }
            if (!_bakedLibs.TryGetValue(id, out var bk))
            {
                byte[] glb = null, alb = null, glow = null;
                yield return ReadBytes(Path.Combine("heroes", $"SB_Hero_{id}_LOD0.glb"), b => glb = b);
                yield return ReadBytes(Path.Combine("heroes", $"SB_Hero_{id}_albedo.png"), b => alb = b);
                yield return ReadBytes(Path.Combine("heroes", $"SB_Hero_{id}_glow.png"), b => glow = b);
                if (glb == null || alb == null) { done(null); yield break; }
                var gltf = new GLTFast.GltfImport(null, null, new SigilMaterialGenerator());
                var load = gltf.LoadGltfBinary(glb, null, new GLTFast.ImportSettings { AnimationMethod = GLTFast.AnimationMethod.Legacy, GenerateMipMaps = false });
                while (!load.IsCompleted) yield return null;
                if (load.IsFaulted || !load.Result) { Debug.LogWarning($"Sigilbound: couldn't load the baked {id}"); done(null); yield break; }
                var mat = new Material(Materials.Lit(Color.white, 0, 0.25f)) { name = "Baked_" + id };
                var albTex = LoadPng(alb, false);
                mat.mainTexture = albTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albTex);
                var glowTex = LoadPng(glow, false);
                if (glowTex != null && mat.HasProperty("_EmissionMap"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", glowTex);
                    mat.SetColor("_EmissionColor", Color.white * 1.5f);
                }
                bk = (gltf, mat); _bakedLibs[id] = bk;
            }
            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);
            holder.localPosition = BuildAway; // out of sight while it instantiates (see Reveal)
            var inst = bk.gltf.InstantiateMainSceneAsync(holder);
            while (!inst.IsCompleted) yield return null;
            if (inst.IsFaulted || !inst.Result || holder == null || holder.childCount == 0) { Trace($"baked {id}: instantiate failed {inst.Exception?.GetBaseException().Message}"); if (holder) UnityEngine.Object.Destroy(holder.gameObject); done(null); yield break; }
            holder.localPosition = Vector3.zero;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = bk.mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            // Clips: the base model's own, on the same root the base instance gets its Animation on.
            var root = holder.GetChild(0).gameObject;
            var anim = root.GetComponent<Animation>() ?? root.AddComponent<Animation>();
            foreach (var c in lib.Gltf.GetAnimationClips() ?? new AnimationClip[0]) if (c != null) anim.AddClip(c, c.name);
            if (lib.Ual == null) lib.Ual = BuildUal(baseModel, anim.transform);
            foreach (var c in lib.Ual) anim.AddClip(c, c.name);
            float k = 1.85f / Mathf.Max(0.1f, lib.HeadY) * (float)body.H;
            holder.localScale = Vector3.one * k;
            done(Reveal(holder, new HeroModel(holder.gameObject, anim, 2.3f * (float)body.H, AnimStyle.For(id))));
        }

        // Applies the hero's colour roles (three3d.mjs buildRigged recolour), trims hair for shaved
        // or bald heroes, hides hoods/hair/crowns the hero doesn't wear, and gives the Titan its
        // glowing element maw.
        static void Recolour(Transform root, string baseModel, HeroLook look, BodySpec body, bool hollow, Element el)
        {
            var colors = look == null ? Looks.TitanColors : Looks.RoleColors(look, hollow);
            var hide = new HashSet<string>(body.Hide ?? new string[0]);
            bool showCrown = look != null && look.Gear == "crown" && !hide.Contains("crown");
            var elColor = Palette.Element(el);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Kit", StringComparison.Ordinal)) continue;
                string srcBase = baseModel, kind = "";
                for (var p = r.transform; p != null && p != root; p = p.parent) { var tag = p.GetComponent<BodyShaper.PartTag>(); if (tag != null) { srcBase = tag.SrcBase; kind = tag.Kind; break; } }
                bool isHead = kind == "head";
                var mats = r.sharedMaterials;
                if (look != null && isHead && srcBase != "hooded" && (body.Shave || body.Bald) && r is SkinnedMeshRenderer smr)
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && Looks.RoleOf(srcBase, true, StripName(mats[i].name)) == "hair") BodyShaper.TrimHair(smr, i, body.Bald ? "beard" : "scalp");
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string role = Looks.RoleOf(srcBase, isHead, StripName(mats[i].name));
                    var m = mats[i];
                    if ((role == "crown" && !showCrown) || role == "tongue" || (role != null && (hide.Contains(role) || hide.Contains(kind + ":" + role)))) { mats[i] = Materials.Invisible(); continue; }
                    if (role == "maw") { mats[i] = Materials.Glow(elColor * 0.3f, elColor, 1.8f); continue; }
                    if (role == "eye" && (hollow || (look != null && look.Glow != null))) { var c = Palette.Hex(colors["eye"]); mats[i] = Materials.Glow(c, c, 3f); continue; }
                    Color col = role != null && colors.TryGetValue(role, out var hex) ? Palette.Hex(hex) : hollow ? Color.Lerp(Materials.BaseColor(m), Palette.Hex("#2e1f33"), 0.5f) : Materials.BaseColor(m);
                    bool shiny = (role == "armor" && srcBase == "monarch") || role == "trim" || role == "blade" || role == "crown";
                    if (look == null)
                    {
                        // Titan: dark basalt with glowing seams in the day's element (crackMaterial on Yeti_Main/Secondary).
                        bool seams = role == "stone" || role == "stoneDark";
                        mats[i] = CharacterLook.ShaderAvailable ? CharacterLook.For(col, seams ? "stone" : "default", Color.black, seams, elColor) : Materials.Lit(col, 0f, 0.05f);
                    }
                    else if (CharacterLook.ShaderAvailable)
                        mats[i] = CharacterLook.For(col, CharacterLook.RoleFromGame(role, srcBase, StripName(m.name)), hollow ? Palette.Hex("#ff3355") : elColor);
                    else mats[i] = Materials.Lit(col, shiny ? 0.3f : 0f, shiny ? 0.5f : 0.2f);
                }
                r.sharedMaterials = mats;
            }
        }
        static string StripName(string n) => n.Replace(" (Instance)", "");

        // glTFast material generator that makes our URP materials directly (so glTFast's own shader
        // graphs aren't needed in the build); colours are replaced by Recolour right after.
        sealed class SigilMaterialGenerator : GLTFast.Materials.IMaterialGenerator
        {
            public Material GetDefaultMaterial(bool pointsSupport = false) => Materials.Lit(Color.grey, 0, 0.2f);
            public Material GenerateMaterial(GLTFast.Schema.MaterialBase gltfMaterial, GLTFast.IGltfReadable gltf, bool pointsSupport = false)
            {
                var c = gltfMaterial.PbrMetallicRoughness != null ? gltfMaterial.PbrMetallicRoughness.BaseColor : Color.white;
                var m = new Material(Materials.Lit(c.gamma, 0, 0.2f)) { name = gltfMaterial.name }; // own instance: the name carries the role
                return m;
            }
            public void SetLogger(GLTFast.Logging.ICodeLogger logger) { }
        }
#else
        public static IEnumerator Build(Unit unit, GearLook gear, Transform parent, Action<HeroModel> done) { done(null); yield break; }
        public static void Preload(IEnumerable<(string id, Element el, bool titan, bool hollow)> units) { }
#endif
    }
}
