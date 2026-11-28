// Skill VFX in the spirit of game3d/vfx.mjs: element palettes (linear HDR, same values), pooled
// effects and no downloaded assets (every texture is drawn in code, see FxTextures).
//   over    - world-space ParticleSystem, additive (glows, motes)
//   flame   - additive flipbook of licking fire tongues (FxTextures.FlameSheet)
//   smoke   - alpha flipbook of billowing puffs (smoke, dust, ink, heat haze)
//   streaks - additive stretched-billboard sparks that arc under light gravity (embers, impact sparks)
//   debris  - alpha rock chunks under full gravity, bouncing on the arena floor plane
//   leaves  - alpha leaves fluttering on a little noise (gale)
//   under / fall - alpha soft dots and additive falling drops (mud, petals, droplets)
//   decals  - pooled ground/billboard quads, additive and alpha (rune circles, shock rings, lava, crust)
//   walls / domes - pooled open cylinders (pillars, splash crowns) and spheres (shields)
//   trails / lines - pooled TrailRenderers (projectiles, wind ribbons) and LineRenderers (beams, bolts)
// Every effect is time-based (same look at 30, 60 or 120 fps) and scales its particle counts by tier.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed class SkillFx : MonoBehaviour
    {
        public static SkillFx I;
        public sealed class Pal { public Color Core, Main, Deep, Dark; }

        // vfx.mjs PAL (linear HDR).
        static Color L(float r, float g, float b) => new Color(r, g, b, 1);
        public static readonly Dictionary<string, Pal> Palettes = new Dictionary<string, Pal>
        {
            ["flame"] = new Pal { Core = L(4.2f, 2.3f, 0.8f), Main = L(3.4f, 0.85f, 0.16f), Deep = L(1.5f, 0.22f, 0.04f), Dark = L(0.09f, 0.035f, 0.02f) },
            ["gale"] = new Pal { Core = L(1.9f, 3.6f, 2.2f), Main = L(0.45f, 2.6f, 1.1f), Deep = L(0.12f, 0.8f, 0.32f), Dark = L(0.04f, 0.12f, 0.05f) },
            ["tide"] = new Pal { Core = L(1.9f, 3.0f, 4.0f), Main = L(0.3f, 1.3f, 3.6f), Deep = L(0.06f, 0.35f, 1.3f), Dark = L(0.02f, 0.06f, 0.14f) },
            ["light"] = new Pal { Core = L(4.2f, 3.8f, 2.6f), Main = L(3.6f, 2.5f, 0.65f), Deep = L(1.4f, 0.8f, 0.14f), Dark = L(0.12f, 0.09f, 0.03f) },
            ["shadow"] = new Pal { Core = L(2.6f, 1.1f, 4.0f), Main = L(1.4f, 0.35f, 3.0f), Deep = L(0.4f, 0.06f, 0.9f), Dark = L(0.018f, 0.004f, 0.035f) },
            ["physical"] = new Pal { Core = L(3.4f, 3.2f, 2.9f), Main = L(2.4f, 2.1f, 1.75f), Deep = L(0.8f, 0.65f, 0.5f), Dark = L(0.06f, 0.05f, 0.045f) },
        };
        static readonly Pal HEAL = new Pal { Core = L(2.2f, 4.0f, 2.0f), Main = L(0.55f, 3.0f, 0.8f), Deep = L(0.15f, 1.0f, 0.25f) };
        static readonly Pal GOLD = new Pal { Core = L(4.0f, 3.4f, 1.8f), Main = L(3.4f, 2.2f, 0.5f), Deep = L(1.2f, 0.6f, 0.1f) };
        static readonly Pal CURSE = new Pal { Core = L(2.6f, 1.2f, 4.0f), Main = L(1.5f, 0.4f, 3.0f), Deep = L(0.4f, 0.08f, 0.9f) };
        static readonly Pal AQUA = new Pal { Core = L(1.8f, 3.4f, 4.2f), Main = L(0.5f, 1.9f, 3.8f), Deep = L(0.1f, 0.5f, 1.4f) };
        static readonly Color DUST = new Color(0.46f, 0.4f, 0.33f, 1), STONE = new Color(0.62f, 0.56f, 0.5f, 1), INK = new Color(0.07f, 0.02f, 0.12f, 1);
        public static Pal Of(Element e) => Palettes[e.ToString().ToLowerInvariant()];

        Camera _cam; Material _add, _alpha;
        ParticleSystem _over, _under, _fall, _flame, _smoke, _streaks, _debris, _leaves;
        Texture2D _dot, _ring, _rune, _streak, _hex, _burst, _crack, _glow, _shock, _rays, _flare, _caustic, _foam, _lava, _crust;
        float _mult = 1f; // particle count scale by quality tier (Low 0.35 .. Ultra 1.2)
        public int Tier { get; private set; } = 2;   // Low 0 .. Ultra 3 (SetTier)
        Renderer _flash; float _flashT = -1, _flashDur;

        sealed class Decal { public Transform T; public Renderer R; public MaterialPropertyBlock B; public bool Busy; }
        readonly List<Decal> _decals = new List<Decal>(), _adecals = new List<Decal>(), _walls = new List<Decal>(), _domes = new List<Decal>();
        readonly List<TrailRenderer> _trails = new List<TrailRenderer>();
        readonly List<LineRenderer> _lines = new List<LineRenderer>();

        public static SkillFx Create(Camera cam)
        {
            var go = new GameObject("SkillFx");
            var f = go.AddComponent<SkillFx>();
            f._cam = cam; I = f;
            f.Init();
            return f;
        }

        // Per tier: particle count scale and pool sizes. Low keeps the look (flipbooks, decals) with far
        // fewer particles; the heavy systems (flame, smoke) are the ones cut hardest.
        public void SetTier(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 3); Tier = tier;
            _mult = new[] { 0.35f, 0.6f, 1f, 1.2f }[tier];
            void Max(ParticleSystem ps, int[] m) { if (ps != null) { var mm = ps.main; mm.maxParticles = m[tier]; } }
            Max(_over, new[] { 500, 900, 1500, 2000 });
            Max(_under, new[] { 150, 300, 500, 700 });
            Max(_fall, new[] { 200, 350, 600, 800 });
            Max(_flame, new[] { 120, 250, 400, 550 });
            Max(_smoke, new[] { 80, 160, 260, 360 });
            Max(_streaks, new[] { 150, 300, 500, 650 });
            Max(_debris, new[] { 40, 90, 160, 220 });
            Max(_leaves, new[] { 30, 60, 110, 150 });
        }

        void Init()
        {
            var addShader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
            _add = new Material(addShader); _alpha = new Material(Shader.Find("Sprites/Default"));
            _dot = T(64, () => FxTextures.Dot(64)); _ring = T(128, () => FxTextures.Ring(128)); _crack = T(256, () => FxTextures.Crack(256));
            _burst = T(128, () => FxTextures.Burst(128)); _rune = T(256, () => FxTextures.Rune(256)); _streak = T(64, () => FxTextures.Streak(64));
            _hex = T(128, () => FxTextures.Hex(128)); _glow = T(64, () => FxTextures.Glow(64)); _shock = T(128, () => FxTextures.Shock(128));
            _rays = T(128, () => FxTextures.Rays(128)); _flare = T(128, () => FxTextures.Flare(128)); _caustic = T(128, () => FxTextures.Caustic(128));
            _foam = T(128, () => FxTextures.Foam(128)); _lava = T(128, () => FxTextures.Lava(128)); _crust = T(128, () => FxTextures.Crust(128));

            _over = MakeSystem("Over", _add, _dot, 1500, 0);
            _under = MakeSystem("Under", _alpha, _dot, 500, 0);
            _fall = MakeSystem("Fall", _add, _dot, 600, 0.6f); // signature motifs that arc and drop (petals, drops, bones)

            _flame = MakeSystem("Flame", _add, T(256, () => FxTextures.FlameSheet(64)), 400, -0.05f, 0.12f);
            Sheet(_flame); Size(_flame, new Keyframe(0, 0.55f), new Keyframe(0.35f, 1f), new Keyframe(1, 0.75f));
            Fade(_flame, 0.08f, 1f, 0.5f, 0.85f, new Color(0.75f, 0.68f, 0.62f));

            _smoke = MakeSystem("Smoke", _alpha, T(256, () => FxTextures.SmokeSheet(64)), 260, -0.02f, 0.1f);
            Sheet(_smoke); Size(_smoke, new Keyframe(0, 0.5f), new Keyframe(1, 1.6f));
            Fade(_smoke, 0.15f, 1f, 0.55f, 0.7f, Color.white);
            var rol = _smoke.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            _streaks = MakeSystem("Streaks", _add, T(32, () => FxTextures.Spark(32)), 500, 0.45f, 0.05f);
            var sr = _streaks.GetComponent<ParticleSystemRenderer>(); sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.045f; sr.lengthScale = 1.4f;
            Size(_streaks, new Keyframe(0, 1), new Keyframe(1, 0.2f));
            Fade(_streaks, 0.02f, 1f, 0.6f, 0.9f, Color.white);

            _debris = MakeSystem("Debris", _alpha, T(64, () => FxTextures.Chunk(64)), 160, 1.0f, 0.02f);
            Size(_debris, new Keyframe(0, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0));
            Fade(_debris, 0.01f, 1f, 0.8f, 1f, Color.white);
            var floor = new GameObject("FxFloor").transform; floor.SetParent(transform, false); floor.position = new Vector3(0, 0.02f, 0);
            var col = _debris.collision; col.enabled = true; col.type = ParticleSystemCollisionType.Planes; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.SetPlane(0, floor); col.bounce = 0.3f; col.dampen = 0.45f; col.lifetimeLoss = 0; col.radiusScale = 0.4f;

            _leaves = MakeSystem("Leaves", _alpha, T(64, () => FxTextures.Leaf(64)), 110, 0.12f, 0.1f);
            Size(_leaves, new Keyframe(0, 1), new Keyframe(1, 0.8f));
            Fade(_leaves, 0.1f, 1f, 0.7f, 1f, Color.white);
            var nz = _leaves.noise; nz.enabled = true; nz.strength = 1.1f; nz.frequency = 0.8f; nz.scrollSpeed = 0.4f; nz.quality = ParticleSystemNoiseQuality.Low;

            // Crit flash: a quad in front of the camera.
            var fq = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(fq.GetComponent<Collider>());
            fq.transform.SetParent(_cam.transform, false); fq.transform.localPosition = new Vector3(0, 0, _cam.nearClipPlane + 0.05f); fq.transform.localScale = new Vector3(3, 3, 1);
            _flash = fq.GetComponent<Renderer>(); _flash.sharedMaterial = new Material(_add) { mainTexture = _burst }; fq.SetActive(false);
            StartCoroutine(PaintTextures());
        }

        // The procedural textures (about 0.45 Mpx of noise math, 120-150 ms on a desktop core and several
        // times that on a phone) used to be painted on the main thread while the game started (performance
        // review #4). Now T() hands out a blank texture of the final size, the painting runs on a worker
        // thread (FxTextures is plain math), and each result is uploaded on its own frame. Effects only play in
        // battle, seconds later; until then the blank textures draw nothing.
        readonly List<(Texture2D tex, System.Func<float[]> paint)> _toPaint = new List<(Texture2D, System.Func<float[]>)>();
        public bool TexturesReady { get; private set; }
        Texture2D T(int n, System.Func<float[]> paint)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(new Color32[n * n]); t.Apply(false); // transparent until painted
            _toPaint.Add((t, paint));
            return t;
        }
        IEnumerator PaintTextures()
        {
            var jobs = _toPaint.ToArray(); _toPaint.Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var work = System.Threading.Tasks.Task.Run(() => { var o = new float[jobs.Length][]; for (int i = 0; i < jobs.Length; i++) o[i] = jobs[i].paint(); return o; });
            while (!work.IsCompleted) yield return null;
            double painted = sw.Elapsed.TotalMilliseconds, mainMs = 0, worst = 0;
            if (work.IsFaulted) { Debug.LogWarning("Sigilbound: effect textures failed to paint: " + work.Exception?.GetBaseException().Message); yield break; }
            var px = work.Result;
            for (int i = 0; i < jobs.Length; i++)
            {
                var t0 = sw.Elapsed.TotalMilliseconds;
                var t = jobs[i].tex; int n = t.width; var p = px[i];
                var c = new Color32[n * n];
                for (int k = 0; k < c.Length; k++) c[k] = new Color(p[k * 4], p[k * 4 + 1], p[k * 4 + 2], p[k * 4 + 3]);
                t.SetPixels32(c); t.Apply(false, true); // drop the CPU copy: saves memory on phones
                double step = sw.Elapsed.TotalMilliseconds - t0; mainMs += step; worst = System.Math.Max(worst, step);
                yield return null;
            }
            TexturesReady = true;
            Debug.Log($"SBPERF fx textures: {jobs.Length} painted on a worker thread in {painted:0} ms; main thread {mainMs:0.0} ms over {jobs.Length} frames (longest {worst:0.0} ms)");
        }

        ParticleSystem MakeSystem(string name, Material mat, Texture2D tex, int max, float gravity, float drag = 0.08f)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = max; main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            Fade(ps, 0.12f, 1f, 0.6f, 0.8f, Color.white);
            Size(ps, new Keyframe(0, 1), new Keyframe(1, 0.25f));
            var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.dampen = drag; vel.limit = 100;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = new Material(mat) { mainTexture = tex };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }
        // Alpha: fade in to `peak` by tIn, hold to `holdA` at tHold, out to 0; colour drifts to `end`.
        static void Fade(ParticleSystem ps, float tIn, float peak, float tHold, float holdA, Color end)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, tHold * 0.5f), new GradientColorKey(end, 1) },
                      new[] { new GradientAlphaKey(tIn <= 0.011f ? peak : 0, 0), new GradientAlphaKey(peak, tIn), new GradientAlphaKey(holdA, tHold), new GradientAlphaKey(0, 1) });
            col.color = g;
        }
        static void Size(ParticleSystem ps, params Keyframe[] k) { var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(k)); }
        static void Sheet(ParticleSystem ps)
        {
            var ts = ps.textureSheetAnimation; ts.enabled = true; ts.mode = ParticleSystemAnimationMode.Grid;
            ts.numTilesX = 4; ts.numTilesY = 4; ts.animation = ParticleSystemAnimationType.WholeSheet; ts.cycleCount = 1;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 0.999f));
        }

        // ---------- primitives ----------
        static Color Hdr(Color c, float k) { var o = c * k * 0.5f; o.a = 1; return o; } // legacy additive doubles the tint
        // An HDR palette colour squashed to a displayable one (for alpha-blended leaves, ink, splashes).
        static Color Ldr(Color c, float a = 1, float bright = 0.9f) { float m = Mathf.Max(c.r, Mathf.Max(c.g, Mathf.Max(c.b, 0.001f))); var o = c * (bright / m); o.a = a; return o; }
        static Vector3 Ground(Vector3 p, float y = 0.03f) => new Vector3(p.x, y, p.z);
        int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * _mult));
        // Time-based emission: how many to emit this frame at `perSec` (scaled by tier), carrying the remainder.
        int Rate(ref float acc, float perSec) { acc += perSec * _mult * Time.deltaTime; int k = (int)acc; acc -= k; return k; }
        // Life long enough to survive one frame: per-frame "head" particles never flicker at low fps.
        static float Hold => Mathf.Max(0.06f, Time.deltaTime * 1.6f);

        static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, Color c, float size, float life, float rot = 0, float spin = 0)
        {
            var e = new ParticleSystem.EmitParams { position = p, velocity = v, startColor = c, startSize = size, startLifetime = life, rotation = rot, angularVelocity = spin, applyShapeToPosition = false };
            ps.Emit(e, 1);
        }
        void Spark(ParticleSystem ps, Vector3 p, Vector3 v, Color c, float size, float life) => Emit(ps, p, v, c, size, life);
        void FlameAt(Vector3 p, Vector3 v, Color c, float size, float life) => Emit(_flame, p, v, c, size, life, Random.Range(-12f, 12f));
        void Puff(Vector3 p, Vector3 v, Color c, float size, float life) => Emit(_smoke, p, v, c, size, life, Random.Range(0, 360f), Random.Range(-25f, 25f));
        void Streak(Vector3 p, Vector3 v, Color c, float size, float life) => Emit(_streaks, p, v, c, size, life);
        void Leaf(Vector3 p, Vector3 v, Color c, float size, float life) => Emit(_leaves, p, v, c, size, life, Random.Range(0, 360f), Random.Range(-260f, 260f));

        // Burst sparks: stretched streaks flying out and arcing down.
        void Sparks(Vector3 p, int n, Color c, float spd, float size = 0.12f, float life = 0.5f, float up = 0)
        {
            for (int i = 0; i < n; i++) Streak(p, Random.onUnitSphere * spd * Random.Range(0.4f, 1f) + Vector3.up * (up + spd * 0.25f), Hdr(c, 1.2f), size * Random.Range(0.45f, 0.8f), life * Random.Range(0.6f, 1f));
        }
        // Billowing smoke puffs (flipbook). The puff fills about half its cell, hence the size boost.
        void Smoke(Vector3 p, int n, Color c, float r = 0.4f, float size = 0.8f, float life = 1.1f, float alpha = 0.5f)
        {
            for (int i = 0; i < n; i++)
            {
                var off = Random.insideUnitSphere * r; off.y = Mathf.Abs(off.y);
                var col = c; col.a = alpha;
                Puff(p + off, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.3f, 0.8f), Random.Range(-0.2f, 0.2f)), col, size * 1.7f * Random.Range(0.75f, 1.25f), life * Random.Range(0.85f, 1.15f));
            }
        }
        // Rock chunks thrown up from p, spread horizontally, bouncing on the floor.
        void Debris(Vector3 p, int n, float spread, float up = 4f, float size = 0.2f, Color? tint = null)
        {
            for (int i = 0; i < n; i++)
            {
                var d = Random.insideUnitCircle.normalized * spread * Random.Range(0.5f, 1.4f);
                var c = (tint ?? STONE) * Random.Range(0.75f, 1.1f); c.a = 1;
                Emit(_debris, p + Vector3.up * 0.15f, new Vector3(d.x, up * Random.Range(0.6f, 1.3f), d.y), c, size * Random.Range(0.5f, 1.4f), Random.Range(1.3f, 1.9f), Random.Range(0, 360f), Random.Range(-420f, 420f));
            }
        }
        // Flames licking up from p (radius r), each tongue `size` tall.
        void Flames(Vector3 p, int n, Pal P, float r, float size, float life = 0.55f)
        {
            var F = P;
            for (int i = 0; i < n; i++)
            {
                var off = Random.insideUnitCircle * r;
                FlameAt(p + new Vector3(off.x, 0, off.y), new Vector3(off.x * 0.4f, Random.Range(0.9f, 1.7f), off.y * 0.4f), Hdr(i % 3 == 0 ? F.Core : F.Main, 0.9f), size * Random.Range(0.7f, 1.3f), life * Random.Range(0.8f, 1.2f));
            }
        }
        // Embers: tiny bright streaks drifting up and out.
        void Embers(Vector3 p, int n, Pal P, float spd)
        {
            for (int i = 0; i < n; i++) Streak(p + Random.insideUnitSphere * 0.2f, new Vector3(Random.Range(-1f, 1f) * spd * 0.5f, Random.Range(0.6f, 1.4f) * spd, Random.Range(-1f, 1f) * spd * 0.5f), Hdr(i % 2 == 0 ? P.Core : P.Main, 1.3f), Random.Range(0.035f, 0.07f), Random.Range(0.6f, 1.1f));
        }
        // Droplets: water drops arcing out and falling.
        void Droplets(Vector3 p, int n, float spd)
        {
            for (int i = 0; i < n; i++)
            {
                var d = Random.insideUnitCircle * spd;
                Spark(_fall, p, new Vector3(d.x, Random.Range(1.6f, 3.2f) * spd * 0.6f, d.y), Hdr(i % 3 == 0 ? AQUA.Core : AQUA.Main, 0.9f), Random.Range(0.05f, 0.11f), Random.Range(0.55f, 0.9f));
            }
        }

        Decal Take(List<Decal> pool, PrimitiveType type, string name, Material mat)
        {
            foreach (var d in pool) if (!d.Busy) { d.Busy = true; d.T.gameObject.SetActive(true); return d; }
            var go = GameObject.CreatePrimitive(type); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sharedMaterial = mat;
            var nd = new Decal { T = go.transform, R = r, B = new MaterialPropertyBlock(), Busy = true };
            pool.Add(nd);
            return nd;
        }
        void Release(Decal d) { d.Busy = false; d.T.gameObject.SetActive(false); }

        // A flat quad on the ground (or a camera-facing one): scales s0 -> s1, spins rv rad/s, fades.
        // `upright` keeps a billboard level (anamorphic flares) instead of a random roll.
        void GroundFx(Texture2D tex, Vector3 p, float s0, float s1, Color c, float k, float life, float rv = 0, float delay = 0, bool billboard = false, bool upright = false)
            => StartCoroutine(GroundRoutine(tex, p, s0, s1, c, k, life, rv, delay, billboard, upright, false));
        // The same with an alpha-blended quad: `c` is a real colour whose alpha is the opacity (crust, ink).
        void GroundFxA(Texture2D tex, Vector3 p, float s0, float s1, Color c, float life, float rv = 0, float delay = 0)
            => StartCoroutine(GroundRoutine(tex, p, s0, s1, c, 1, life, rv, delay, false, false, true));
        IEnumerator GroundRoutine(Texture2D tex, Vector3 p, float s0, float s1, Color c, float k, float life, float rv, float delay, bool billboard, bool upright, bool alpha)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var d = alpha ? Take(_adecals, PrimitiveType.Quad, "DecalA", _alpha) : Take(_decals, PrimitiveType.Quad, "Decal", _add);
            float rot = upright ? 0 : Random.Range(0, 360f);
            d.B.SetTexture("_MainTex", tex);
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                float u = t / life, ease = 1 - Mathf.Pow(1 - u, 3), a = Mathf.Clamp01(u / 0.12f) * Mathf.Clamp01((1 - u) / 0.45f);
                float s = Mathf.Lerp(s0, s1, ease);
                d.T.position = p + Vector3.up * 0.04f;
                d.T.rotation = billboard ? _cam.transform.rotation * Quaternion.Euler(0, 0, rot + rv * t * Mathf.Rad2Deg) : Quaternion.Euler(90, rot + rv * t * Mathf.Rad2Deg, 0);
                d.T.localScale = new Vector3(s, s, 1);
                Color col; if (alpha) { col = c; col.a = c.a * a; } else col = Hdr(c, k * a);
                d.B.SetVector("_TintColor", col); d.B.SetVector("_Color", col);
                d.R.SetPropertyBlock(d.B);
                yield return null;
            }
            Release(d);
        }

        // A glowing open cylinder (pillar / shock wall / splash crown): radius and height animate, fades.
        void Wall(Vector3 p, float r0, float r1, float h0, float h1, Color c, float k, float life, float delay = 0)
            => StartCoroutine(WallRoutine(p, r0, r1, h0, h1, c, k, life, delay));
        IEnumerator WallRoutine(Vector3 p, float r0, float r1, float h0, float h1, Color c, float k, float life, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var d = Take(_walls, PrimitiveType.Cylinder, "Wall", _add);
            d.B.SetTexture("_MainTex", _streak);
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                float u = t / life, ease = 1 - Mathf.Pow(1 - u, 2), a = Mathf.Clamp01(u / 0.15f) * Mathf.Clamp01((1 - u) / 0.5f);
                float r = Mathf.Lerp(r0, r1, ease), h = Mathf.Lerp(h0, h1, ease);
                d.T.position = p + Vector3.up * (h / 2); d.T.rotation = Quaternion.Euler(0, t * 90, 0); d.T.localScale = new Vector3(r * 2, h / 2, r * 2);
                var col = Hdr(c, k * a * 0.6f); d.B.SetVector("_TintColor", col); d.B.SetVector("_Color", col); d.R.SetPropertyBlock(d.B);
                yield return null;
            }
            Release(d);
        }

        void Dome(Vector3 p, float r0, float r1, Color c, float life) => StartCoroutine(DomeRoutine(p, r0, r1, c, life));
        IEnumerator DomeRoutine(Vector3 p, float r0, float r1, Color c, float life)
        {
            var d = Take(_domes, PrimitiveType.Sphere, "Dome", _add);
            d.B.SetTexture("_MainTex", _hex);
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                float u = t / life, ease = 1 - Mathf.Pow(1 - u, 3), a = Mathf.Clamp01(u / 0.08f) * Mathf.Clamp01((1 - u) / 0.6f);
                float r = Mathf.Lerp(r0, r1, ease);
                d.T.position = p; d.T.localScale = Vector3.one * r * 2; d.T.rotation = Quaternion.Euler(0, t * 40, 0);
                var col = Hdr(c, a * 0.45f); d.B.SetVector("_TintColor", col); d.B.SetVector("_Color", col); d.R.SetPropertyBlock(d.B);
                yield return null;
            }
            Release(d);
        }

        // Pooled trail: placed at `at` with its history cleared, then emitting.
        TrailRenderer TakeTrail(Vector3 at, float time, float width, Gradient g)
        {
            TrailRenderer tr = null;
            foreach (var x in _trails) if (!x.gameObject.activeSelf) { tr = x; break; }
            if (tr == null)
            {
                var go = new GameObject("Trail"); go.transform.SetParent(transform, false); go.SetActive(false);
                tr = go.AddComponent<TrailRenderer>(); tr.sharedMaterial = _add; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
                tr.widthCurve = AnimationCurve.Linear(0, 1, 1, 0); tr.minVertexDistance = 0.05f;
                _trails.Add(tr);
            }
            tr.transform.position = at; tr.Clear(); tr.time = time; tr.widthMultiplier = width; tr.colorGradient = g;
            tr.gameObject.SetActive(true); tr.emitting = true;
            return tr;
        }
        IEnumerator ReleaseTrail(TrailRenderer tr) { tr.emitting = false; yield return new WaitForSeconds(tr.time + 0.05f); tr.Clear(); tr.gameObject.SetActive(false); }

        LineRenderer TakeLine(int points, float width, Gradient g)
        {
            LineRenderer lr = null;
            foreach (var x in _lines) if (!x.gameObject.activeSelf) { lr = x; break; }
            if (lr == null)
            {
                var go = new GameObject("Line"); go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = _over.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                _lines.Add(lr);
            }
            lr.gameObject.SetActive(true); lr.positionCount = points; lr.widthMultiplier = width; lr.widthCurve = AnimationCurve.Constant(0, 1, 1); lr.colorGradient = g;
            return lr;
        }
        void ReleaseLine(LineRenderer lr) { if (lr != null) lr.gameObject.SetActive(false); }

        // A wind ribbon: a trail spiralling up around p (gale). dir = +1 / -1 for the spin.
        void Ribbon(Vector3 p, float radius, float height, float dur, Pal P, float dir) => StartCoroutine(RibbonRoutine(p, radius, height, dur, P, dir));
        IEnumerator RibbonRoutine(Vector3 p, float radius, float height, float dur, Pal P, float dir)
        {
            float a0 = Random.Range(0, Mathf.PI * 2);
            Vector3 At(float u) { float a = a0 + dir * u * Mathf.PI * 3.2f, r = radius * (0.6f + 0.6f * u); return p + new Vector3(Mathf.Cos(a) * r, height * u, Mathf.Sin(a) * r); }
            var tr = TakeTrail(At(0), dur * 0.45f, 0.13f * Mathf.Max(0.6f, radius), Grad(Hdr(P.Core, 1.1f), Hdr(P.Main, 0.6f)));
            for (float t = 0; t < dur; t += Time.deltaTime) { tr.transform.position = At(t / dur); yield return null; }
            yield return ReleaseTrail(tr);
        }

        // ---------- element detail ----------
        // The layer that makes each element read as itself, added to casts, impacts, AoEs and signatures:
        //   flame - licking fire, embers and heat smoke      gale   - wind ribbons, leaves and dust
        //   tide  - splash crown, droplets and a foam ring    light  - rays, motes and a halo flare
        //   shadow - inky smoke, curling tendrils and void sparks
        void Detail(Vector3 p, Element el, float s)
        {
            var P = Of(el); var g = Ground(p);
            switch (el)
            {
                case Element.Flame:
                    Flames(p, N(5 * s), P, 0.3f * s, 0.75f * s);
                    Embers(p, N(9 * s), P, 2.2f * s);
                    Smoke(p + Vector3.up * 0.5f * s, N(2 * s), new Color(0.16f, 0.11f, 0.09f), 0.3f * s, 0.8f * s, 1.3f, 0.4f);
                    break;
                case Element.Gale:
                    Ribbon(p - Vector3.up * 0.4f * s, 0.5f * s, 1.3f * s, 0.5f, P, 1);
                    if (s >= 0.9f) Ribbon(p - Vector3.up * 0.4f * s, 0.7f * s, 1.7f * s, 0.6f, P, -1);
                    for (int i = 0; i < N(5 * s); i++) Leaf(p + Random.insideUnitSphere * 0.3f * s, Random.insideUnitSphere * 2.2f * s + Vector3.up * 1.2f, Ldr(Color.Lerp(P.Main, P.Core, Random.value), 1, Random.Range(0.6f, 0.95f)), Random.Range(0.1f, 0.17f) * Mathf.Sqrt(s), Random.Range(0.9f, 1.4f));
                    Smoke(g, N(2 * s), DUST, 0.4f * s, 0.55f * s, 0.8f, 0.28f);
                    break;
                case Element.Tide:
                    Wall(g, 0.2f * s, 0.65f * s, 0.15f, 0.9f * s, P.Core, 0.8f, 0.45f); // splash crown
                    Droplets(p, N(14 * s), 1.6f * s);
                    GroundFx(_foam, g, 0.4f * s, 1.7f * s, AQUA.Core, 0.45f, 0.8f, 0.3f);
                    break;
                case Element.Light:
                    GroundFx(_rays, p, 0.6f * s, 2.0f * s, P.Core, 0.75f, 0.42f, 1.2f, 0, true);
                    GroundFx(_flare, p, 1.4f * s, 3.2f * s, P.Core, 0.7f, 0.26f, 0, 0, true, true);
                    GroundFx(_ring, p, 0.3f * s, 1.3f * s, P.Core, 0.55f, 0.32f, 0, 0, true); // halo
                    for (int i = 0; i < N(10 * s); i++) Spark(_over, p + Random.insideUnitSphere * 0.4f * s, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1.1f), Random.Range(-0.3f, 0.3f)), Hdr(i % 2 == 0 ? P.Core : GOLD.Core, 1f), Random.Range(0.05f, 0.11f), Random.Range(0.7f, 1.2f));
                    break;
                case Element.Shadow:
                    Smoke(p, N(3 * s), INK, 0.25f * s, 0.65f * s, 0.9f, 0.75f);
                    // tendrils: violet streaks whipping out then curling back toward the centre
                    for (int i = 0; i < N(8 * s); i++)
                    {
                        float a = Random.Range(0, Mathf.PI * 2); var d = new Vector3(Mathf.Cos(a), Random.Range(-0.2f, 0.6f), Mathf.Sin(a));
                        Streak(p + d * 0.1f, d * Random.Range(2.5f, 4f) * s + Vector3.Cross(Vector3.up, d) * 1.5f, Hdr(i % 2 == 0 ? P.Main : P.Deep, 1.4f), Random.Range(0.06f, 0.1f), Random.Range(0.3f, 0.5f));
                    }
                    for (int i = 0; i < N(6 * s); i++) Spark(_over, p + Random.insideUnitSphere * 0.5f * s, Random.insideUnitSphere * 0.4f, Hdr(P.Core, 0.9f), Random.Range(0.04f, 0.08f), Random.Range(0.25f, 0.45f)); // void sparks
                    for (int i = 0; i < N(3 * s); i++) Spark(_over, p + Random.insideUnitSphere * 0.4f * s, new Vector3(0, Random.Range(0.4f, 0.9f), 0), Hdr(CURSE.Main, 0.6f), Random.Range(0.18f, 0.3f), Random.Range(0.8f, 1.2f)); // wisps
                    break;
                default:
                    Smoke(g, N(2 * s), DUST, 0.3f * s, 0.6f * s, 0.8f, 0.35f);
                    Sparks(p, N(8 * s), P.Core, 3.5f * s, 0.1f, 0.35f);
                    break;
            }
        }
        // One trail particle behind a moving head (projectiles, beams): `back` is the head's velocity.
        void TrailBit(Vector3 p, Vector3 back, Element el, float s)
        {
            var P = Of(el); var drift = -back * 0.08f;
            switch (el)
            {
                case Element.Flame:
                    if (Random.value < 0.6f) FlameAt(p + Random.insideUnitSphere * 0.06f, drift + Vector3.up * 0.9f, Hdr(Random.value < 0.4f ? P.Core : P.Main, 0.9f), Random.Range(0.25f, 0.4f) * s, Random.Range(0.25f, 0.4f));
                    else Streak(p, drift + Random.insideUnitSphere * 1.2f + Vector3.up * 0.8f, Hdr(P.Core, 1.3f), 0.04f, 0.5f);
                    break;
                case Element.Gale:
                    if (Random.value < 0.25f) Leaf(p, drift + Random.insideUnitSphere * 1.2f, Ldr(P.Main, 1, Random.Range(0.6f, 0.95f)), 0.1f * s, 0.8f);
                    else Spark(_over, p + Random.insideUnitSphere * 0.08f, drift + Random.insideUnitSphere * 0.8f, Hdr(P.Core, 0.6f), 0.12f * s, 0.25f);
                    break;
                case Element.Tide:
                    Spark(_fall, p + Random.insideUnitSphere * 0.06f, drift + Random.insideUnitSphere * 0.7f + Vector3.up * 0.5f, Hdr(Random.value < 0.4f ? AQUA.Core : P.Main, 0.9f), Random.Range(0.05f, 0.09f) * s, 0.45f);
                    break;
                case Element.Light:
                    Spark(_over, p + Random.insideUnitSphere * 0.1f, drift + Random.insideUnitSphere * 0.3f + Vector3.up * 0.3f, Hdr(Random.value < 0.5f ? P.Core : GOLD.Core, 1f), Random.Range(0.05f, 0.1f) * s, Random.Range(0.4f, 0.7f));
                    break;
                case Element.Shadow:
                    if (Random.value < 0.35f) Puff(p, drift + Vector3.up * 0.2f, new Color(INK.r, INK.g, INK.b, 0.6f), 0.5f * s, 0.55f);
                    else Spark(_over, p + Random.insideUnitSphere * 0.08f, drift + Random.insideUnitSphere * 0.5f, Hdr(Random.value < 0.5f ? P.Main : CURSE.Main, 0.8f), Random.Range(0.1f, 0.18f) * s, 0.4f);
                    break;
                default:
                    Streak(p, drift + Random.insideUnitSphere, Hdr(P.Core, 1f), 0.04f, 0.3f);
                    break;
            }
        }

        // ---------- effects ----------
        public void CastCircle(Vector3 pos, Element el)
        {
            var P = Of(el);
            GroundFx(_rune, pos, 0.8f, 2.0f, P.Main, 1.05f, 1.05f, 1.4f);
            GroundFx(_rune, pos, 0.4f, 1.15f, P.Core, 0.55f, 0.95f, -2.6f);
            GroundFx(_glow, pos, 1.8f, 2.6f, P.Main, 0.4f, 1.0f);
            Wall(pos, 0.7f, 0.92f, 0.15f, 0.8f, P.Main, 0.75f, 0.85f);
            int n = N(14);
            for (int i = 0; i < n; i++)
            {
                float th = i / (float)n * Mathf.PI * 2, r = 0.85f;
                Spark(_over, pos + new Vector3(Mathf.Cos(th) * r, 0.12f, Mathf.Sin(th) * r), new Vector3(0, Random.Range(0.5f, 1.6f), 0), Hdr(i % 2 == 0 ? P.Core : P.Main, 1), Random.Range(0.1f, 0.24f), Random.Range(0.5f, 0.9f));
            }
            StartCoroutine(After(0.15f, () => Detail(pos + Vector3.up * 0.25f, el, 0.6f)));
        }

        // Energy gathering into a unit (motes pulled toward its chest).
        public void Charge(Transform unit, Element el, float dur = 0.5f) => StartCoroutine(ChargeRoutine(unit, el, dur));
        IEnumerator ChargeRoutine(Transform unit, Element el, float dur)
        {
            var P = Of(el); float acc = 0;
            for (float t = 0; t < dur && unit != null; t += Time.deltaTime)
            {
                var at = unit.position + Vector3.up * 1.1f + unit.forward * 0.4f;
                for (int i = Rate(ref acc, 60); i > 0; i--) { var from = at + Random.onUnitSphere * 1.0f; Spark(_over, from, (at - from) * 2.4f, Hdr(P.Core, 1), 0.09f, 0.4f); }
                Spark(_over, at, Vector3.zero, Hdr(P.Main, 0.35f + 0.5f * t / dur), 0.3f + 0.5f * t / dur, Hold); // the gathering glow
                yield return null;
            }
            if (unit != null) GroundFx(_glow, unit.position + Vector3.up * 1.1f + unit.forward * 0.4f, 0.4f, 1.3f, P.Core, 0.7f, 0.25f, 0, 0, true);
        }

        // kind: "orb" (casters' foci), "bolt" (staffs, mirrors), "arrow" (bows), "shard" (others).
        // Each kind has its own head (glow orb / stretched lance / slim streak / spinning crystal) and a
        // ribbon trail; the element sheds its own trail bits (fire tongues, leaves, drops, motes, ink).
        public IEnumerator Projectile(Vector3 from, Vector3 to, Element el, string kind = "orb")
        {
            var P = Of(el);
            bool orb = kind == "orb", bolt = kind == "bolt", arrow = kind == "arrow";
            var trail = TakeTrail(from, arrow ? 0.12f : bolt ? 0.2f : 0.25f, orb ? 0.3f : arrow ? 0.05f : bolt ? 0.12f : 0.16f, Grad(Hdr(P.Core, 1.2f), Hdr(P.Main, 0.9f)));
            float dist = Vector3.Distance(from, to);
            float dur = (float)Sigilbound.Core.Impact.FlightTime(kind, dist), arc = (arrow ? 0.05f : bolt ? 0.03f : 0.14f) * dist; // speed-based; arc relative to distance
            float acc = 0, rate = arrow ? 14 : bolt ? 35 : 45; var prev = from;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                var pos = Vector3.Lerp(from, to, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * arc;
                var vel = Time.deltaTime > 0 ? (pos - prev) / Time.deltaTime : Vector3.zero; prev = pos;
                trail.transform.position = pos;
                // head
                if (orb) { Spark(_over, pos, Vector3.zero, Hdr(P.Main, 0.9f), 0.55f, Hold); Spark(_over, pos, Vector3.zero, Hdr(P.Core, 1.3f), 0.24f, Hold); }
                else if (bolt) { Streak(pos, vel * 0.7f, Hdr(P.Core, 1.6f), 0.2f, Hold); Spark(_over, pos, Vector3.zero, Hdr(P.Main, 0.7f), 0.4f, Hold); }
                else if (arrow) Streak(pos, vel * 0.5f, Hdr(P.Core, 1.3f), 0.08f, Hold);
                else { Emit(_over, pos, Vector3.zero, Hdr(P.Core, 1.2f), 0.26f, Hold); Streak(pos, vel * 0.35f, Hdr(P.Main, 1.2f), 0.14f, Hold); }
                for (int i = Rate(ref acc, rate); i > 0; i--)
                {
                    TrailBit(pos, vel, el, orb ? 1.1f : 0.8f);
                    if (!orb && !arrow && Random.value < 0.4f) Streak(pos, vel * 0.1f + Random.insideUnitSphere * 1.6f, Hdr(P.Core, 1.2f), 0.035f, 0.25f); // shard / bolt glints
                }
                yield return null;
            }
            StartCoroutine(ReleaseTrail(trail));
        }
        static Gradient Grad(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            return g;
        }

        public void Slash(Vector3 pos, Element el, Vector3 dir)
        {
            var P = Of(el);
            StartCoroutine(SlashRoutine(pos, P, dir));
            Sparks(pos, N(12), P.Core, 3.4f, 0.1f, 0.35f);
            Detail(pos, el, 0.4f);
        }
        IEnumerator SlashRoutine(Vector3 pos, Pal P, Vector3 dir)
        {
            // A blade arc: a bright thin edge over a wider soft glow, sweeping across the target.
            var glow = TakeLine(16, 0.55f, Grad(Hdr(P.Main, 0.9f), Hdr(P.Deep, 0.6f)));
            var edge = TakeLine(16, 0.16f, Grad(Hdr(P.Core, 2.0f), Hdr(P.Main, 1.4f)));
            var w = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0)); glow.widthCurve = w; edge.widthCurve = w;
            var side = Vector3.Cross(Vector3.up, dir.sqrMagnitude > 0.01f ? dir.normalized : _cam.transform.forward).normalized;
            float tilt = Random.Range(-0.5f, 0.5f);
            for (float t = 0; t < 0.3f; t += Time.deltaTime)
            {
                float u = t / 0.3f;
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.Lerp(-1.1f, 1.1f, i / 15f) * Mathf.Clamp01(u * 2.5f);
                    var p = pos + (side * Mathf.Sin(a) + Vector3.up * (Mathf.Cos(a) - 0.6f) * 0.8f + Vector3.up * tilt * Mathf.Sin(a)) * 0.8f;
                    glow.SetPosition(i, p); edge.SetPosition(i, p);
                }
                glow.widthMultiplier = 0.55f * (1 - u); edge.widthMultiplier = 0.16f * (1 - u * u);
                yield return null;
            }
            ReleaseLine(glow); ReleaseLine(edge);
        }

        // Hit: a flash, a shock ring (in the air and on the ground), streak sparks, the element's detail
        // and a glow that lingers on the ground. Crits are bigger, with rays, a flare and flying debris.
        public void Impact(Vector3 pos, Element el, bool crit)
        {
            var P = Of(el); float s = crit ? 1.55f : 1f; var g = Ground(pos);
            GroundFx(_glow, pos, 0.6f * s, 1.7f * s, P.Core, 1.1f, 0.15f, 0, 0, true);
            GroundFx(_shock, pos, 0.3f, 1.5f * s, P.Main, 0.7f, 0.26f, 0, 0, true);
            GroundFx(_shock, g, 0.5f * s, 1.6f * s, P.Main, 0.5f, 0.5f);
            GroundFx(_glow, g, 1.0f * s, 1.2f * s, P.Main, 0.32f, 1.2f); // lingering glow
            Sparks(pos, N(crit ? 24 : 14), P.Core, crit ? 5.5f : 3.6f, 0.11f, 0.45f);
            Detail(pos, el, 0.7f * s);
            if (crit)
            {
                GroundFx(_shock, pos, 0.4f, 2.6f, P.Core, 0.9f, 0.3f, 0, 0, true);
                GroundFx(_burst, pos, 1, 2.4f, P.Core, 0.6f, 0.24f, 0, 0, true);
                GroundFx(_flare, pos, 2f, 4.2f, P.Core, 0.8f, 0.24f, 0, 0, true, true);
                Debris(pos, N(5), 2.4f, 3.5f, 0.14f);
            }
        }

        public void Aoe(Vector3 centre, float radius, Element el)
        {
            var P = Of(el); var g = Ground(centre);
            GroundFx(_rune, centre, radius * 1.4f, radius * 2.2f, P.Main, 0.7f, 1.4f, 0.8f);
            GroundFx(_rune, centre, radius * 0.8f, radius * 1.3f, P.Core, 0.35f, 1.2f, -1.6f);
            // element ground layer
            switch (el)
            {
                case Element.Tide: GroundFx(_caustic, g, radius * 1.6f, radius * 2.1f, P.Core, 0.6f, 1.4f, 0.3f); GroundFx(_foam, g, radius * 0.8f, radius * 2.2f, AQUA.Core, 0.4f, 1.0f, 0.2f); break;
                case Element.Flame: GroundFx(_lava, g, radius * 1.2f, radius * 2.0f, P.Main, 0.7f, 1.3f, 0.1f); break;
                case Element.Light: GroundFx(_rays, g, radius * 1.2f, radius * 2.4f, P.Core, 0.55f, 1.0f, 0.6f); break;
                case Element.Shadow: GroundFxA(_glow, g, radius * 1.4f, radius * 2.2f, new Color(INK.r, INK.g, INK.b, 0.7f), 1.4f); break;
                default: GroundFx(_shock, g, radius * 0.6f, radius * 2.3f, P.Core, 0.5f, 0.9f, 2.5f); break;
            }
            Wall(centre, radius * 0.7f, radius * 0.95f, 0.3f, 2.0f, P.Main, 1f, 1.1f);
            for (int i = 0; i < N(24); i++) { var off = Random.insideUnitCircle * radius; Spark(_over, centre + new Vector3(off.x, 0.1f, off.y), new Vector3(0, Random.Range(1f, 3f), 0), Hdr(P.Core, 1), Random.Range(0.1f, 0.22f), Random.Range(0.5f, 1f)); }
            Smoke(centre + Vector3.up * 0.3f, N(6), P.Dark.gamma, radius * 0.7f, 1.0f, 1.3f, 0.4f);
            // the element erupting at a few spots across the area, staggered
            int spots = _mult >= 1 ? 4 : 2;
            StartCoroutine(Stream(0.5f, spots, k => { var off = Random.insideUnitCircle * radius * 0.75f; Detail(centre + new Vector3(off.x, 0.3f, off.y), el, 0.75f); }));
        }

        public IEnumerator Beam(Vector3 from, Vector3 to, Element el)
        {
            var P = Of(el);
            var glow = TakeLine(2, 0.7f, Grad(Hdr(P.Main, 1.0f), Hdr(P.Deep, 0.8f)));
            var core = TakeLine(2, 0.2f, Grad(Hdr(P.Core, 2.2f), Hdr(P.Core, 1.6f)));
            glow.SetPosition(0, from); glow.SetPosition(1, to); core.SetPosition(0, from); core.SetPosition(1, to);
            var dir = (to - from).normalized; float acc = 0, accHit = 0;
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                float env = Mathf.Sin(t / 0.45f * Mathf.PI), jit = Random.Range(0.85f, 1.15f);
                glow.widthMultiplier = 0.7f * env * jit; core.widthMultiplier = 0.2f * env;
                Spark(_over, from, Vector3.zero, Hdr(P.Core, 1.2f), 0.7f * env, Hold);   // muzzle
                Spark(_over, to, Vector3.zero, Hdr(P.Main, 1.0f), 1.0f * env, Hold);     // contact
                for (int i = Rate(ref acc, 70); i > 0; i--) TrailBit(Vector3.Lerp(from, to, Random.value), dir * 6, el, 1f);
                for (int i = Rate(ref accHit, 40); i > 0; i--) Streak(to, (Random.onUnitSphere - dir) * Random.Range(2f, 4.5f), Hdr(P.Core, 1.3f), 0.06f, 0.3f);
                yield return null;
            }
            ReleaseLine(glow); ReleaseLine(core);
            Impact(to, el, true);
        }

        public void Shockwave(Vector3 pos, Element el, float radius = 3)
        {
            var P = Of(el); var g = Ground(pos);
            GroundFx(_shock, g, 0.4f, radius * 2.1f, P.Main, 1.0f, 0.5f);
            GroundFx(_shock, g, 0.3f, radius * 1.6f, P.Core, 0.55f, 0.65f);
            GroundFx(_glow, g, radius, radius * 1.4f, P.Main, 0.3f, 0.45f);
            Wall(pos, 0.4f, radius, 0.7f, 0.12f, P.Main, 0.75f, 0.55f);
            // a rolling ring of dust puffs pushed out with the wave
            int n = N(14);
            for (int i = 0; i < n; i++)
            {
                float th = i / (float)n * Mathf.PI * 2 + Random.Range(-0.15f, 0.15f); var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                Puff(new Vector3(pos.x, 0.3f, pos.z) + d * 0.4f, d * radius * Random.Range(1.2f, 1.7f) + Vector3.up * 0.3f, new Color(DUST.r, DUST.g, DUST.b, 0.5f), Random.Range(1.1f, 1.7f), Random.Range(0.7f, 1.0f));
            }
            Debris(pos, N(6), radius * 0.9f, 3f, 0.16f);
        }
        public void Heal(Vector3 pos)
        {
            var P = HEAL;
            GroundFx(_dot, pos, 1.4f, 1.9f, P.Main, 0.5f, 1.1f);
            GroundFx(_ring, pos, 0.6f, 1.8f, P.Core, 0.7f, 0.8f);
            Wall(pos, 0.55f, 0.45f, 1.4f, 2.3f, P.Main, 0.9f, 1.1f);
            for (int i = 0; i < N(28); i++) { float th = Random.Range(0, Mathf.PI * 2), r = Random.Range(0.15f, 0.6f); Spark(_over, pos + new Vector3(Mathf.Cos(th) * r, Random.Range(0.1f, 1.2f), Mathf.Sin(th) * r), new Vector3(0, Random.Range(0.6f, 1.6f), 0), Hdr(i % 3 == 0 ? P.Core : P.Main, 1), Random.Range(0.08f, 0.2f), Random.Range(0.6f, 1.1f)); }
        }

        public void Buff(Vector3 pos, bool good)
        {
            var P = good ? GOLD : CURSE;
            GroundFx(_dot, pos, 1.2f, 1.6f, P.Main, 0.4f, 0.9f);
            for (int i = 0; i < N(22); i++)
            {
                float th = Random.Range(0, Mathf.PI * 2), r = Random.Range(0.2f, 0.6f);
                var p = pos + new Vector3(Mathf.Cos(th) * r, good ? Random.Range(0.1f, 0.8f) : Random.Range(1.3f, 2f), Mathf.Sin(th) * r);
                Spark(_over, p, new Vector3(0, good ? Random.Range(1.8f, 2.8f) : -Random.Range(1.5f, 2.4f), 0), Hdr(P.Core, 1), Random.Range(0.08f, 0.18f), 0.6f);
            }
            if (!good) Smoke(pos + Vector3.up * 1.2f, N(5), new Color(0.15f, 0.05f, 0.25f), 0.4f, 0.5f, 0.7f, 0.5f);
        }

        public void Shield(Vector3 pos)
        {
            var P = AQUA;
            Dome(pos + Vector3.up * 0.85f, 0.55f, 1.05f, P.Main, 1.2f);
            GroundFx(_dot, pos + Vector3.up * 0.9f, 1, 1.5f, P.Main, 0.6f, 0.2f, 0, 0, true);
            GroundFx(_rune, pos, 1.3f, 2.0f, P.Main, 0.7f, 1.0f, 1);
        }

        // Damage-over-time tick: poison bubbles (and a coloured pulse) on the unit.
        public void DebuffTick(Vector3 pos, string kind = "poison")
        {
            Color c = kind == "burn" ? Palettes["flame"].Main : kind == "bleed" ? L(2.6f, 0.12f, 0.1f) : kind == "freeze" ? AQUA.Main : L(0.6f, 2.6f, 0.35f);
            var cy = pos + Vector3.up * 1.0f;
            GroundFx(_dot, cy, 0.5f, 0.9f, c, 0.45f, 0.35f, 0, 0, true);
            GroundFx(_ring, cy, 0.35f, 1.0f, c, 0.6f, 0.4f, 0, 0, true);
            for (int i = 0; i < N(10); i++) Spark(_over, pos + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.3f, 1.2f), Random.Range(-0.3f, 0.3f)), new Vector3(0, Random.Range(0.5f, 1f), 0), Hdr(c, 0.9f), Random.Range(0.1f, 0.2f), 0.6f);
            if (kind == "poison") Smoke(pos + Vector3.up * 0.8f, N(4), new Color(0.06f, 0.16f, 0.03f), 0.3f, 0.6f, 0.7f, 0.55f);
        }

        public void DeathFade(Vector3 pos, Element el)
        {
            var P = Of(el);
            GroundFx(_dot, pos, 1.4f, 1.8f, P.Main, 0.35f, 1.6f);
            GroundFx(_ring, pos, 0.5f, 1.9f, P.Core, 0.5f, 1.2f);
            Wall(pos, 0.45f, 0.15f, 1.6f, 2.6f, P.Main, 0.35f, 1.5f);
            StartCoroutine(Stream(1.2f, N(60), k => Spark(_over, pos + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.1f, 1.8f) * (1 - k * 0.6f), Random.Range(-0.35f, 0.35f)), new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.6f, 1.4f), Random.Range(-0.15f, 0.15f)), Hdr(P.Core, 1), Random.Range(0.06f, 0.14f), Random.Range(0.6f, 1.1f))));
            Smoke(pos + Vector3.up * 0.8f, N(16), new Color(0.12f, 0.11f, 0.12f), 0.3f, 0.9f, 1.2f, 0.6f);
        }

        // ---------- signatures (phase E steps 53-55) ----------
        // Each hero's motif (Core HeroSignatures, from its design's fx line): a small burst on each of its
        // hits (scale ~0.6), a big one on its ultimates (~1.4). Coloured from the hero's element palette.
        public void Signature(Vector3 pos, Element el, Motif m, float scale = 1f)
        {
            var P = Of(el); float s = scale; int n = N(14 * s);
            Vector3 Out(float spd) { var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f; return d * spd * Random.Range(0.5f, 1f); }
            Color Pale(Color c, float w) => Color.Lerp(c, new Color(3, 3, 3, 1), w);
            switch (m)
            {
                case Motif.Wind:
                    for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; var r = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); Spark(_over, pos + r * 0.45f * s, Vector3.Cross(Vector3.up, r) * 2.6f * s + Vector3.up * Random.Range(0.3f, 1f), Hdr(P.Core, 1), 0.07f * s, 0.45f); }
                    GroundFx(_ring, pos, 0.3f * s, 1.3f * s, P.Main, 0.5f, 0.3f, 6f, 0, true);
                    Ribbon(pos - Vector3.up * 0.3f * s, 0.45f * s, 1.1f * s, 0.45f, P, 1); if (s > 1) Ribbon(pos - Vector3.up * 0.3f * s, 0.6f * s, 1.4f * s, 0.55f, P, -1); break;
                case Motif.Embers:
                    Flames(pos - Vector3.up * 0.2f * s, N(4 * s), Of(Element.Flame), 0.25f * s, 0.55f * s, 0.5f);
                    Embers(pos, n, P, 1.8f * s); break;
                case Motif.Water:
                    for (int i = 0; i < n; i++) Spark(_fall, pos, Out(2.2f * s) + Vector3.up * 2.2f * s, Hdr(i % 2 == 0 ? AQUA.Core : P.Main, 0.9f), Random.Range(0.05f, 0.11f) * s, 0.7f);
                    GroundFx(_foam, new Vector3(pos.x, 0.02f, pos.z), 0.3f * s, 1.5f * s, AQUA.Core, 0.5f, 0.6f, 0.3f);
                    Wall(Ground(pos), 0.15f * s, 0.5f * s, 0.1f, 0.7f * s, AQUA.Core, 0.7f, 0.4f); break;
                case Motif.Bubbles:
                    for (int i = 0; i < n; i++) Spark(_over, pos + Random.insideUnitSphere * 0.4f * s, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 0.9f), Random.Range(-0.2f, 0.2f)), Hdr(Pale(AQUA.Main, 0.3f), 0.6f), Random.Range(0.1f, 0.2f) * s, 1.2f); break;
                case Motif.Frost:
                    for (int i = 0; i < n; i++) Streak(pos, Out(3.6f * s), Hdr(Pale(AQUA.Core, 0.5f), 1.2f), 0.06f * s, 0.4f);
                    Smoke(pos, N(4 * s), new Color(0.85f, 0.92f, 1f), 0.3f * s, 0.6f * s, 0.9f, 0.3f); break;
                case Motif.Leaves:
                    for (int i = 0; i < n; i++) Leaf(pos + Random.insideUnitSphere * 0.3f * s, Out(1.6f * s) + Vector3.up * 1.4f * s, Ldr(Color.Lerp(P.Main, HEAL.Main, Random.value), 1, Random.Range(0.55f, 0.95f)), Random.Range(0.1f, 0.16f) * s, Random.Range(1.0f, 1.5f));
                    break;
                case Motif.Petals: case Motif.Feathers:
                    var pc = m == Motif.Petals ? Pale(P.Main, 0.35f) : Pale(P.Core, 0.6f);
                    for (int i = 0; i < n; i++) Spark(_fall, pos + Random.insideUnitSphere * 0.3f * s, Out(1.4f * s) + Vector3.up * 1.6f * s, Hdr(pc, 0.8f), Random.Range(0.08f, m == Motif.Feathers ? 0.18f : 0.13f) * s, Random.Range(0.9f, 1.4f)); break;
                case Motif.Stars:
                    for (int i = 0; i < n; i++) Spark(_over, pos, Out(2.6f * s), Hdr(i % 2 == 0 ? GOLD.Core : P.Core, 1.2f), Random.Range(0.05f, 0.14f) * s, Random.Range(0.4f, 0.8f));
                    GroundFx(_burst, pos, 0.4f * s, 1.1f * s, GOLD.Core, 0.6f, 0.22f, 2f, 0, true);
                    GroundFx(_flare, pos, 0.8f * s, 2.0f * s, GOLD.Core, 0.5f, 0.2f, 0, 0, true, true); break;
                case Motif.Lightning:
                    StartCoroutine(BoltRoutine(pos, P, s)); Sparks(pos, N(10 * s), P.Core, 3.5f * s, 0.06f, 0.25f); break;
                case Motif.Smoke:
                    Smoke(pos, N(5 * s), Color.Lerp(new Color(0.2f, 0.19f, 0.2f), P.Dark, 0.3f), 0.35f * s, 0.7f * s, 1.1f, 0.55f); break;
                case Motif.Wisps:
                    for (int i = 0; i < n; i++) Spark(_over, pos + Random.insideUnitSphere * 0.5f * s, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 1.1f), Random.Range(-0.3f, 0.3f)), Hdr(i % 2 == 0 ? CURSE.Main : P.Main, 0.8f), Random.Range(0.12f, 0.22f) * s, Random.Range(0.9f, 1.5f));
                    Smoke(pos, N(2 * s), INK, 0.3f * s, 0.5f * s, 0.9f, 0.55f); break;
                case Motif.Bones:
                    for (int i = 0; i < n; i++) Spark(_fall, pos, Out(2.4f * s) + Vector3.up * 1.4f * s, Hdr(new Color(1.1f, 1.05f, 0.9f), 0.7f), 0.07f * s, 0.6f);
                    GroundFx(_ring, pos, 0.3f * s, 1.2f * s, CURSE.Main, 0.4f, 0.3f, 0, 0, true); break;
                case Motif.Mud:
                    Smoke(pos, N(5 * s), new Color(0.32f, 0.24f, 0.15f), 0.3f * s, 0.5f * s, 0.7f, 0.7f);
                    Debris(pos, N(5 * s), 1.4f * s, 2.6f * s, 0.1f * s, new Color(0.4f, 0.3f, 0.2f)); break;
                case Motif.Runes:
                    GroundFx(_rune, pos, 0.4f * s, 1.0f * s, P.Main, 0.8f, 0.5f, 2.2f, 0, true); Sparks(pos, N(8 * s), P.Core, 1.6f * s, 0.06f, 0.4f); break;
                case Motif.Notes:
                    for (int i = 0; i < n; i++) { float a = Random.Range(0, Mathf.PI * 2); Spark(_over, pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.35f * s, new Vector3(Mathf.Cos(a) * 0.4f, Random.Range(0.8f, 1.5f), Mathf.Sin(a) * 0.4f), Hdr(GOLD.Core, 0.9f), Random.Range(0.06f, 0.11f) * s, 0.9f); }
                    GroundFx(_ring, pos, 0.2f * s, 1.0f * s, GOLD.Main, 0.5f, 0.35f, 0, 0, true); break;
                case Motif.Poison:
                    Smoke(pos, N(4 * s), new Color(0.35f, 0.55f, 0.2f), 0.3f * s, 0.5f * s, 0.9f, 0.45f);
                    for (int i = 0; i < n; i++) Spark(_over, pos + Random.insideUnitSphere * 0.3f, new Vector3(0, Random.Range(0.4f, 1f), 0), Hdr(new Color(0.7f, 2.4f, 0.4f), 0.8f), 0.07f * s, 0.8f); break;
                case Motif.Shards:
                    for (int i = 0; i < n; i++) Streak(pos, Out(5f * s), Hdr(Pale(P.Core, 0.3f), 1.4f), Random.Range(0.06f, 0.11f) * s, 0.35f);
                    GroundFx(_burst, pos, 0.3f * s, 1.0f * s, P.Core, 0.6f, 0.18f, 0, 0, true); break;
                default: // Motes
                    for (int i = 0; i < n; i++) Spark(_over, pos + Random.insideUnitSphere * 0.4f * s, new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.5f, 1.2f), Random.Range(-0.25f, 0.25f)), Hdr(i % 2 == 0 ? P.Core : GOLD.Core, 0.9f), Random.Range(0.05f, 0.1f) * s, 1.0f); break;
            }
        }
        // Branching lightning: a jagged main bolt with two forks, re-struck three times, and a flash.
        IEnumerator BoltRoutine(Vector3 pos, Pal P, float s)
        {
            var g = Grad(Hdr(P.Core, 2.2f), Hdr(P.Main, 1.6f));
            var main = TakeLine(10, 0.07f * s, g); var f1 = TakeLine(5, 0.04f * s, g); var f2 = TakeLine(5, 0.035f * s, g);
            var glow = TakeLine(10, 0.3f * s, Grad(Hdr(P.Main, 0.7f), Hdr(P.Deep, 0.5f)));
            GroundFx(_glow, pos, 0.6f * s, 1.8f * s, P.Core, 1.0f, 0.2f, 0, 0, true);
            for (int flick = 0; flick < 3; flick++)
            {
                var top = pos + new Vector3(Random.Range(-0.5f, 0.5f), 2.6f * s, Random.Range(-0.5f, 0.5f));
                var pts = new Vector3[10];
                for (int i = 0; i < 10; i++) { float u = i / 9f; pts[i] = Vector3.Lerp(top, pos, u) + (i > 0 && i < 9 ? Random.insideUnitSphere * 0.22f * s : Vector3.zero); main.SetPosition(i, pts[i]); glow.SetPosition(i, pts[i]); }
                void Fork(LineRenderer f, int from)
                {
                    var o = pts[from]; var d = (Random.onUnitSphere + Vector3.down * 0.6f).normalized * 0.9f * s;
                    for (int i = 0; i < 5; i++) f.SetPosition(i, o + d * (i / 4f) + (i > 0 ? Random.insideUnitSphere * 0.1f * s : Vector3.zero));
                }
                Fork(f1, Random.Range(2, 5)); Fork(f2, Random.Range(4, 8));
                main.widthMultiplier = 0.07f * s * (1 - flick * 0.2f);
                float wait = 0.05f; while ((wait -= Time.deltaTime) > 0) yield return null;
            }
            ReleaseLine(main); ReleaseLine(f1); ReleaseLine(f2); ReleaseLine(glow);
        }

        // A short glowing trail on the weapon hand for one melee strike (element colour).
        public void WeaponTrail(Transform hand, Element el, float dur = 0.45f) { if (hand != null) StartCoroutine(TrailRoutine(hand, Of(el), dur)); }
        IEnumerator TrailRoutine(Transform hand, Pal P, float dur)
        {
            var tr = TakeTrail(hand.position, 0.18f, 0.14f, Grad(Hdr(P.Core, 1.3f), Hdr(P.Main, 0.6f)));
            for (float t = 0; t < dur && hand != null; t += Time.deltaTime) { tr.transform.position = hand.position; yield return null; }
            yield return ReleaseTrail(tr);
        }

        // Legendary heroes' ambient aura: one or two motif motes drifting up around them.
        public void Aura(Vector3 pos, Element el, Motif m)
        {
            var P = Of(el);
            for (int i = 0; i < N(1.5f); i++)
            {
                var off = Random.insideUnitCircle * 0.55f;
                Spark(m == Motif.Petals || m == Motif.Leaves || m == Motif.Feathers ? _fall : _over, pos + new Vector3(off.x, Random.Range(0.1f, 1.2f), off.y), new Vector3(0, Random.Range(0.25f, 0.6f), 0), Hdr(P.Core, 0.5f), Random.Range(0.04f, 0.08f), 1.4f);
            }
        }

        // Heartfire Torrent: a stream of molten fire from the Titan's open chest core (`source`, read every frame so
        // it follows the socket as the body turns) toward a point that sweeps from `a` to `b` over `dur` seconds, in
        // the day's element. Layers: big licking flame tongues along the stream, ember streaks, a pale heat-haze
        // shimmer above it, dark smoke and molten splashes where it lands, and pools left behind. On Low only the
        // embers, the glowing core, the splashes and the pools (TitanSpec.HeavyBreath). All rates are per second.
        public IEnumerator Breath(System.Func<Vector3> source, Vector3 a, Vector3 b, Element el, float dur)
        {
            var P = Of(el); var F = P; bool heavy = Sigilbound.Core.TitanSpec.HeavyBreath(Tier);
            float aEmber = 0, aFlame = 0, aHaze = 0, aSmoke = 0, aSplash = 0, nextPool = 0;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                var mouth = source(); var tgt = Vector3.Lerp(a, b, t / dur);
                float ramp = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((dur - t) / 0.25f + 0.3f);
                Spark(_over, mouth, Vector3.zero, Hdr(F.Core, 1.2f), 0.9f * ramp, Hold); // the glowing maw
                for (int i = Rate(ref aEmber, 150 * ramp); i > 0; i--)
                {
                    var to = tgt + Random.insideUnitSphere * 0.9f; to.y = Mathf.Max(0.2f, to.y);
                    var v = to - mouth; float spd = Random.Range(11f, 16f);
                    Streak(mouth + Random.insideUnitSphere * 0.25f, v.normalized * spd + Random.insideUnitSphere * 1.5f, Hdr(i % 3 == 0 ? P.Core : F.Core, 1.3f), Random.Range(0.05f, 0.1f), v.magnitude / spd * Random.Range(0.95f, 1.2f));
                }
                for (int i = heavy ? Rate(ref aFlame, 70 * ramp) : 0; i > 0; i--)
                {
                    var to = tgt + Random.insideUnitSphere * 0.7f; var v = to - mouth; float spd = Random.Range(8.5f, 11.5f);
                    FlameAt(mouth + Random.insideUnitSphere * 0.3f, v.normalized * spd + Random.insideUnitSphere * 0.8f + Vector3.up * 0.8f, Hdr(i % 3 == 0 ? F.Core : (i % 3 == 1 ? F.Main : P.Main), 1.0f), Random.Range(1.2f, 2.1f), v.magnitude / spd * Random.Range(0.9f, 1.15f));
                }
                for (int i = heavy ? Rate(ref aHaze, 8 * ramp) : 0; i > 0; i--) // heat haze: faint pale puffs rising over the stream
                {
                    var p = Vector3.Lerp(mouth, tgt, Random.Range(0.3f, 1f)) + Vector3.up * 0.6f;
                    Puff(p, Vector3.up * 1.2f, new Color(1f, 0.92f, 0.82f, 0.07f), Random.Range(1.6f, 2.4f), 0.8f);
                }
                for (int i = heavy ? Rate(ref aSmoke, 7) : 0; i > 0; i--) Puff(tgt + Random.insideUnitSphere * 0.5f + Vector3.up * 0.4f, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.4f), Random.Range(-0.3f, 0.3f)), new Color(0.2f, 0.14f, 0.11f, 0.45f), Random.Range(1.4f, 2.2f), Random.Range(1.0f, 1.4f));
                for (int i = Rate(ref aSplash, 26 * ramp); i > 0; i--) // molten splashes where it lands
                {
                    var d = Random.insideUnitCircle * 2.2f; var g = Ground(tgt, 0.15f) + new Vector3(d.x, 0, d.y) * 0.25f;
                    if (i % 2 == 0) Spark(_fall, g, new Vector3(d.x, Random.Range(2.5f, 4.5f), d.y), Hdr(i % 4 == 0 ? F.Core : F.Main, 1.1f), Random.Range(0.07f, 0.13f), Random.Range(0.55f, 0.85f));
                    else Streak(g, new Vector3(d.x * 1.5f, Random.Range(3f, 5f), d.y * 1.5f), Hdr(F.Core, 1.3f), 0.07f, 0.5f);
                }
                if (t >= nextPool) { nextPool = t + 0.32f; LavaPool(tgt, el); }
                yield return null;
            }
        }
        // A pool of lava where the breath lands: molten veins under a crust that forms over them, a red
        // underglow, bubbles that pop (with a flicker of flame and a spit of droplets) and heat smoke, ~3.5 s.
        public void LavaPool(Vector3 at, Element el = Element.Flame)
        {
            var F = Of(el); var g = Ground(at, 0.035f);
            GroundFx(_glow, g, 1.4f, 2.4f, F.Deep, 0.8f, 3.3f);
            GroundFx(_lava, g, 0.5f, 1.8f, F.Main, 1.25f, 3.5f, 0.06f);
            GroundFx(_lava, g, 0.3f, 1.2f, F.Core, 0.6f, 2.2f, -0.1f, 0.1f);
            GroundFxA(_crust, Ground(at, 0.05f), 0.7f, 2.0f, new Color(1, 1, 1, 0.95f), 3.2f, 0, 0.3f);
            if (Tier > 0) StartCoroutine(Bubbles(g, 3f, el));
            Smoke(g, N(2), new Color(0.18f, 0.12f, 0.1f), 0.5f, 0.8f, 1.6f, 0.35f);
        }
        IEnumerator Bubbles(Vector3 g, float dur, Element el = Element.Flame)
        {
            var F = Of(el);
            float next = Random.Range(0.15f, 0.4f);
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (t < next) { yield return null; continue; }
                next = t + Random.Range(0.25f, 0.5f) / Mathf.Max(0.5f, _mult);
                var off = Random.insideUnitCircle * 0.55f; var p = g + new Vector3(off.x, 0.01f, off.y);
                GroundFx(_ring, p, 0.08f, 0.4f, F.Core, 0.9f, 0.22f);
                for (int i = 0; i < 3; i++) Spark(_fall, p, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(1.6f, 2.6f), Random.Range(-0.6f, 0.6f)), Hdr(F.Core, 1.1f), 0.06f, 0.5f);
                if (Random.value < 0.5f) FlameAt(p, Vector3.up * 0.8f, Hdr(F.Main, 0.9f), Random.Range(0.4f, 0.7f), 0.45f);
                if (Random.value < 0.3f) Puff(p + Vector3.up * 0.2f, Vector3.up * 0.7f, new Color(0.2f, 0.15f, 0.12f, 0.3f), 0.9f, 1.2f);
                yield return null;
            }
        }
        // Fire flaring up around a hero caught in the breath: licking tongues, embers, a little smoke.
        public void FireFlare(Vector3 feet, Element el = Element.Flame)
        {
            var F = Of(el);
            Flames(feet + Vector3.up * 0.1f, N(7), F, 0.4f, 0.9f, 0.6f);
            Embers(feet + Vector3.up * 0.5f, N(6), F, 2f);
            Smoke(feet + Vector3.up * 1.0f, N(1), new Color(0.18f, 0.12f, 0.1f), 0.2f, 0.6f, 1.0f, 0.35f);
        }

        // Burning cracks spreading across the ground from pos (Titan finishers): a dark fissure under the
        // glowing seams, rock chunks thrown up, embers breathing out of the cracks, and a dust ring.
        public void Cracks(Vector3 pos, Element el, float radius = 4)
        {
            var P = Of(el); var g = Ground(pos);
            GroundFxA(_crack, Ground(pos, 0.025f), radius * 0.35f, radius * 1.02f, new Color(0.04f, 0.025f, 0.02f, 0.9f), 2.4f);
            GroundFx(_crack, g, radius * 0.3f, radius, P.Main, 1.2f, 1.8f);
            GroundFx(_crack, g, radius * 0.2f, radius * 0.85f, P.Core, 0.7f, 1.0f, 0, 0.05f);
            GroundFx(_glow, g, radius * 0.6f, radius * 1.1f, P.Deep, 0.5f, 1.6f);
            Debris(pos, N(8 * Mathf.Clamp(radius / 4, 0.6f, 1.5f)), radius * 0.9f, 4.5f, 0.22f);
            StartCoroutine(Stream(1.0f, N(18), k =>
            {
                float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(0.3f, 0.9f) * radius * 0.5f;
                Streak(g + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r), new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1.5f, 3f), Random.Range(-0.4f, 0.4f)), Hdr(P.Core, 1.3f), 0.05f, Random.Range(0.6f, 1.0f));
            }));
            Dust(pos, radius * 0.6f, el);
        }
        // A rolling ring of dust and grit (the Titan landing, roaring, slamming): billowing puffs pushed
        // outward, a few pebbles and sparks.
        public void Dust(Vector3 pos, float radius, Element el)
        {
            float k = Mathf.Clamp(radius / 2, 0.5f, 2);
            int n = N(14 * k);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2 + Random.Range(-0.2f, 0.2f); var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var col = DUST * Random.Range(0.8f, 1.1f); col.a = 0.55f;
                Puff(new Vector3(pos.x, 0.3f, pos.z) + d * radius * 0.3f, d * Random.Range(1.4f, 2.6f) * radius / 2 + Vector3.up * Random.Range(0.3f, 0.8f), col, Random.Range(0.9f, 1.5f) * Mathf.Clamp(radius / 2, 0.7f, 1.8f), Random.Range(1.0f, 1.5f));
            }
            Debris(pos, N(3 * k), radius * 0.8f, 2.5f, 0.12f);
            Sparks(pos + Vector3.up * 0.2f, N(10), Of(el).Main, 3f, 0.08f, 0.5f, 1.5f);
        }

        // Titan slam (Bedrock Hammer, Faultline Leap, the Earthsplitter): a flash, a double shock ring, a burst, rock chunks hurled up,
        // a dust wave, a spray of sparks and a ring of secondary impacts.
        public void TitanSlam(Vector3 pos, Element el)
        {
            var P = Of(el); const float R = 4.2f;
            GroundFx(_glow, pos + Vector3.up * 0.5f, 2f, 5f, P.Core, 1.1f, 0.2f, 0, 0, true);
            GroundFx(_flare, pos + Vector3.up * 0.5f, 4f, 9f, P.Core, 0.7f, 0.3f, 0, 0, true, true);
            Shockwave(pos, el, R);
            GroundFx(_shock, Ground(pos), 1.5f, R * 2.4f, P.Core, 1.1f, 0.7f);
            GroundFx(_burst, Ground(pos), R, R * 1.6f, P.Main, 0.8f, 0.9f);
            Debris(pos, N(16), 3.5f, 6f, 0.28f);
            Sparks(pos + Vector3.up * 0.4f, N(30), P.Core, 8, 0.12f, 0.6f, 2);
            int m = _mult >= 1 ? 6 : 3;
            for (int i = 0; i < m; i++) { float th = i / (float)m * Mathf.PI * 2, r = Random.Range(1.6f, 2.6f); var p = pos + new Vector3(Mathf.Cos(th) * r, 0.4f, Mathf.Sin(th) * r); StartCoroutine(After(0.08f + i * 0.05f, () => Impact(p, el, false))); }
        }

        // Energy gathering at a point (the Titan's chest core in the Heartfire Torrent's wind-up).
        public void ChargeAt(Vector3 at, Element el, float dur, float radius = 1f) => StartCoroutine(ChargeAtRoutine(at, el, dur, radius));
        IEnumerator ChargeAtRoutine(Vector3 at, Element el, float dur, float radius)
        {
            var P = Of(el); float acc = 0;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                for (int i = Rate(ref acc, 50); i > 0; i--) { var from = at + Random.onUnitSphere * radius; Spark(_over, from, (at - from) * 2.6f, Hdr(P.Core, 1), 0.14f, 0.38f); }
                yield return null;
            }
        }
        // The Titan's open chest core: one soft glow particle on it this frame (TitanBody; ~1 live particle).
        public void CoreGlow(Vector3 at, Color seam, float strength, float size) => Spark(_over, at, Vector3.zero, Hdr(seam, Mathf.Clamp(strength, 0, 2.5f)), size, Hold);
        // A crystal ridge breaking off the Titan: a flash in the seam colour, `n` crystal shards (none on Low) and a
        // few sparks, thrown along `dir`.
        public void RidgeShatter(Vector3 at, Vector3 dir, Element el, Color seam, int n)
        {
            GroundFx(_glow, at, 0.6f, 2.2f, seam, 1.2f, 0.25f, 0, 0, true);
            var crystal = new Color(0.62f, 0.64f, 0.7f, 1);
            for (int i = 0; i < n; i++)
            {
                var v = (dir.normalized + Random.insideUnitSphere * 0.45f) * Random.Range(4f, 7f);
                Emit(_debris, at, v, crystal * Random.Range(0.85f, 1.15f), Random.Range(0.25f, 0.45f), Random.Range(1.1f, 1.6f), Random.Range(0, 360f), Random.Range(-500f, 500f));
            }
            if (n > 0) Sparks(at, N(10), Of(el).Core, 5f, 0.1f, 0.5f, 1f);
        }

        // Short full-screen flash on crits.
        public void CritFlash(Element el)
        {
            _flashT = 0; _flashDur = 0.22f;
            _flash.gameObject.SetActive(true);
            _flash.sharedMaterial.SetColor("_TintColor", Hdr(Of(el).Core, 0.25f));
            _flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
        }

        void Update()
        {
            if (_flashT < 0) return;
            _flashT += Time.unscaledDeltaTime;
            float a = 1 - _flashT / _flashDur;
            if (a <= 0) { _flashT = -1; _flash.gameObject.SetActive(false); return; }
            _flash.transform.localScale = new Vector3(3, 3, 1) * (1 + _flashT * 3);
        }

        // Emits n times spread over dur seconds, by elapsed time (catches up within a frame at low fps).
        IEnumerator Stream(float dur, int n, System.Action<float> emit)
        {
            int done = 0;
            for (float t = 0; done < n; t += Time.deltaTime)
            {
                int want = Mathf.Min(n, Mathf.FloorToInt(t / dur * n) + 1);
                while (done < want) { emit(done / (float)n); done++; }
                yield return null;
            }
        }
        IEnumerator After(float s, System.Action a) { yield return new WaitForSeconds(s); a(); }
    }
}
