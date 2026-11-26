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
            st.time = 0;
            Anim.CrossFade(st.name, fade);
            if (!loop && !clamp && IdleName != null && IdleName != name) Anim.CrossFadeQueued(_names[IdleName], 0.22f, QueueMode.CompleteOthers).wrapMode = WrapMode.Loop;
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

        bool _idlePhased;
        public void Idle(float fade = 0.25f)
        {
            var n = IdleName; if (n == null) return;
            Play(n, true, fade, Style.Titan ? 0.7f : Style.IdleSpeed);
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
        public float Strike()
        {
            var list = Style.Strike.Where(Has).ToArray();
            var n = list.Length > 0 ? list[_strikeN++ % list.Length] : "Sword_Slash";
            Play(n, false, 0.1f); return Length(n);
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

        static IEnumerator ReadBytes(string file, Action<byte[]> done)
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
            Trace($"read {file}: {(data == null ? "MISSING at " + path : data.Length + " bytes")}");
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
            while (!_jobs[key]) yield return null;
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
                // The high-detail Titan (heroes_hd/SB_Hero_titan_HD.glb), else the old stone yeti below.
                yield return EnsureHdIndex();
                if (_hd != null && _hd.Contains("titan"))
                {
                    HeroModel hd = null;
                    yield return BuildHd(unit, new BodySpec(), parent, m => hd = m, "titan");
                    Trace($"hd titan: {(hd != null ? "ok" : "FAILED, falling back")}");
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
            var inst = lib.Gltf.InstantiateMainSceneAsync(holder);
            while (!inst.IsCompleted) yield return null;
            if (inst.IsFaulted || !inst.Result || holder == null) { Trace($"build {id}: instantiate failed {inst.Exception?.GetBaseException().Message}"); if (holder) UnityEngine.Object.Destroy(holder.gameObject); done(null); yield break; }

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
            done(new HeroModel(holder.gameObject, anim, (titan ? 4.7f : 2.3f) * (float)body.H, titan ? AnimStyle.TitanStyle : AnimStyle.For(id)));
        }

        // ---------- high-detail heroes (phase F) ----------
        static HashSet<string> _hd;
        static readonly Dictionary<string, (GLTFast.GltfImport gltf, Material mat)> _hdLibs = new Dictionary<string, (GLTFast.GltfImport, Material)>();
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
        }

        // The painted-character material for the baked atlas: albedo, tangent normal, and a mask whose
        // blue channel is the glow (from the _glow PNG) so the eyes, runes and auras light up.
        static Material HdMaterial(string id, Texture2D alb, Texture2D nrm, Texture2D glow, Element el)
        {
            var sh = Shader.Find("Sigilbound/CharacterPainted");
            var tmpl = Resources.Load<Material>("SigilMaterials/HeroPainted");
            Material mat = tmpl != null ? new Material(tmpl) : sh != null ? new Material(sh) : new Material(Materials.Lit(Color.white, 0, 0.3f));
            mat.name = "HD_" + id;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", alb);
            mat.mainTexture = alb;
            if (nrm != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
            if (mat.HasProperty("_MaskMap") && glow != null)
            {
                var g = glow.GetPixels32(); var mask = new Color32[g.Length];
                for (int i = 0; i < g.Length; i++) { byte lum = (byte)Mathf.Min(255, (g[i].r * 3 + g[i].g * 6 + g[i].b) / 10 * 2); mask[i] = new Color32(0, 255, lum, 90); }
                var mt = new Texture2D(glow.width, glow.height, TextureFormat.RGBA32, true, true); mt.SetPixels32(mask); mt.Apply(true, true);
                mat.SetTexture("_MaskMap", mt);
                if (mat.HasProperty("_GlowColor")) mat.SetColor("_GlowColor", Palette.Element(el) * 2.2f);
            }
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Palette.Element(el));
            return mat;
        }

        static IEnumerator BuildHd(Unit unit, BodySpec body, Transform parent, Action<HeroModel> done, string hdId = null)
        {
            string id = hdId ?? unit.Template.Id;
            bool titan = id == "titan";
            string cacheKey = titan ? $"titan|{unit.El}" : id; // the Titan's runes take its current element
            yield return EnsureLibrary("ubc");
            var lib = Lib("ubc");
            if (lib == null || lib.Failed || parent == null) { done(null); yield break; }
            if (!_hdLibs.TryGetValue(cacheKey, out var bk))
            {
                byte[] glb = null, alb = null, nrm = null, glow = null;
                bool low = Quality.I != null && Quality.I.Tier == 0;
                yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD{(low ? "_LOD1" : "")}.glb"), b => glb = b);
                if (glb == null && low) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD.glb"), b => glb = b);
                // Textures ship as JPEG (blenderforge/compress); PNG is still read for older builds.
                yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_albedo.jpg"), b => alb = b);
                if (alb == null) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_albedo.png"), b => alb = b);
                yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_normal.jpg"), b => nrm = b);
                if (nrm == null) yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_normal.png"), b => nrm = b);
                yield return ReadBytes(Path.Combine("heroes_hd", $"SB_Hero_{id}_HD_glow.png"), b => glow = b);
                if (glb == null || alb == null) { done(null); yield break; }
                var gltf = new GLTFast.GltfImport(null, null, new SigilMaterialGenerator());
                var load = gltf.LoadGltfBinary(glb, null, new GLTFast.ImportSettings { AnimationMethod = GLTFast.AnimationMethod.Legacy, GenerateMipMaps = false });
                while (!load.IsCompleted) yield return null;
                if (load.IsFaulted || !load.Result) { Debug.LogWarning($"Sigilbound: couldn't load the high-detail {id}"); done(null); yield break; }
                bk = (gltf, HdMaterial(id, LoadPng(alb, false), LoadPng(nrm, true), LoadPng(glow, false), unit.El)); _hdLibs[cacheKey] = bk;
            }
            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);
            var inst = bk.gltf.InstantiateMainSceneAsync(holder);
            while (!inst.IsCompleted) yield return null;
            if (inst.IsFaulted || !inst.Result || holder == null || holder.childCount == 0) { if (holder) UnityEngine.Object.Destroy(holder.gameObject); done(null); yield break; }
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = bk.mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            var root = holder.GetChild(0).gameObject;
            var anim = root.GetComponent<Animation>() ?? root.AddComponent<Animation>();
            foreach (var c in lib.Gltf.GetAnimationClips() ?? new AnimationClip[0]) if (c != null && !c.name.Contains("TPose")) anim.AddClip(c, c.name);
            anim.playAutomatically = false; // never fall back to a default clip (the T-pose reference)
            if (titan) root.AddComponent<HandGrip>();
            else if (AnimStyle.HdGrips(id)) root.AddComponent<HandGrip>().Side = Impact.FamilyOf(id) == WeaponFamily.Bow ? "l" : "r"; // bows: the left hand
            // The rig is in metres with the head bone near 1.6 m: scale to the battle's hero height.
            float headY = 1.6f;
            var head = BodyShaper.Find(holder, "Head");
            if (head != null) headY = Mathf.Max(0.5f, holder.InverseTransformPoint(head.position).y);
            // Height and build from the approved design (heroes_hd/body.json): gnomes stand short, golems tall and broad.
            yield return EnsureHdBodies();
            float hgt = (float)body.H, bulk = 1f;
            if (_hdBody != null && _hdBody.TryGetValue(id, out var hb)) { hgt = hb.h; bulk = hb.b; }
            float k = (titan ? 3.6f : 1.85f) / headY * hgt;
            holder.localScale = new Vector3(k * bulk, k, k * bulk);
            done(titan ? new HeroModel(holder.gameObject, anim, 4.7f, AnimStyle.TitanHd, AnimStyle.TitanHdAliases)
                       : new HeroModel(holder.gameObject, anim, 2.3f * hgt, AnimStyle.Hd(id), AnimStyle.HdAliases));
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

        static Texture2D LoadPng(byte[] bytes, bool linear)
        {
            if (bytes == null) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
            return t.LoadImage(bytes) ? t : null;
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
            var inst = bk.gltf.InstantiateMainSceneAsync(holder);
            while (!inst.IsCompleted) yield return null;
            if (inst.IsFaulted || !inst.Result || holder == null || holder.childCount == 0) { Trace($"baked {id}: instantiate failed {inst.Exception?.GetBaseException().Message}"); if (holder) UnityEngine.Object.Destroy(holder.gameObject); done(null); yield break; }
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
            done(new HeroModel(holder.gameObject, anim, 2.3f * (float)body.H, AnimStyle.For(id)));
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
#endif
    }
}
