// Interactive backgrounds for boss stages and the Titan (docs/sigilbound/art/SCENERY.md, part 4).
// BattleController reports what happened through ArenaEvents (presentation only: no battle logic
// reads or changes anything here); the ArenaReactor on a boss arena answers with the scenery:
// trees and banners sway with shockwaves, braziers flare on kills, the theme's vents erupt on big
// hits (lava, sea spray, crystal sparks, spores or dust), debris falls on crits and boss blows,
// lightning splits the sky in storm themes, and the ground cracks under a warlord's slam.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class ArenaEvents
    {
        public static event System.Action<Vector3, float, bool, bool> Hit;   // where, share of the target's max HP, crit, heavy (boss or ultimate)
        public static event System.Action<Vector3> Kill;                     // where a unit fell
        public static event System.Action<Vector3, float, bool> Shock;       // where, strength (1 = an ultimate), slam (a boss or Titan blow)
        public static void RaiseHit(Vector3 p, float frac, bool crit, bool heavy) => Hit?.Invoke(p, frac, crit, heavy);
        public static void RaiseKill(Vector3 p) => Kill?.Invoke(p);
        public static void RaiseShock(Vector3 p, float strength, bool slam) => Shock?.Invoke(p, strength, slam);
        // A story lair's set-piece answered (bell toll, wheel spin-up, pyre flare ...): the sound cue
        // (StoryScenes.CueOf) and where. ArenaReactor raises it and plays the cue; listeners may add more.
        public static event System.Action<string, Vector3> LairCue;
        public static void RaiseLairCue(string cue, Vector3 p) { LairCue?.Invoke(cue, p); Sound.I?.PlayAt(cue, p, 0.8f); }
    }

    public sealed class ArenaReactor : MonoBehaviour
    {
        public enum Hazard { Dust, Water, Lava, Crystal, Spores }

        // Filled in by Scenery.Build.
        public readonly List<Transform> Sway = new List<Transform>();      // trees, posts: tilt from the base
        public readonly List<Transform> Cloths = new List<Transform>();    // banner cloths: swing about the crossbar
        public readonly List<Transform> Braziers = new List<Transform>();
        public readonly List<Vector3> Vents = new List<Vector3>();
        // The lair's moving set-pieces (Core.StoryScenes LairFx): wheel, bell, lamp, pyre, lanterns, crag ...
        public sealed class LairPart { public Transform T; public LairFx Fx; public Vector3 Anchor; public Material Glow; public Quaternion Rest; public float A, W, Spin, Pulse; public bool Out; }
        public readonly List<LairPart> Lair = new List<LairPart>();
        public Hazard Kind; public bool Storm, NoCracks; public Color Tint = Color.white; public Material Ramp; public Light Sun; public int Tier = 2;

        Quaternion[] _rest, _clothRest; Vector2[] _a, _w; float[] _ca, _cw, _phase;
        readonly List<ParticleSystem> _vent = new List<ParticleSystem>();
        readonly List<ParticleSystem> _flames = new List<ParticleSystem>();
        readonly List<Light> _lights = new List<Light>();
        ParticleSystem _dust;
        float _sunBase, _flash, _nextStrike, _cdGeyser, _cdDebris, _cdBolt, _brazierFlare; bool _lit; // _lit: the sun/ambient are flashed right now
        Color _ambBase;

        sealed class Rock { public Transform T; public Vector3 V, Spin; public float Age, Rest; public bool Landed; }
        readonly List<Rock> _rocks = new List<Rock>();
        readonly List<(Transform t, Material m, float age)> _cracks = new List<(Transform, Material, float)>();
        LineRenderer _bolt; float _boltT;

        void OnEnable() { ArenaEvents.Hit += OnHit; ArenaEvents.Kill += OnKill; ArenaEvents.Shock += OnShock; }
        void OnDisable() { ArenaEvents.Hit -= OnHit; ArenaEvents.Kill -= OnKill; ArenaEvents.Shock -= OnShock; RestoreLight(); }
        void OnDestroy() => RestoreLight();

        void Start()
        {
            _rest = new Quaternion[Sway.Count]; _a = new Vector2[Sway.Count]; _w = new Vector2[Sway.Count]; _phase = new float[Sway.Count];
            for (int i = 0; i < Sway.Count; i++) { _rest[i] = Sway[i].localRotation; _phase[i] = i * 1.37f; }
            _clothRest = new Quaternion[Cloths.Count]; _ca = new float[Cloths.Count]; _cw = new float[Cloths.Count];
            for (int i = 0; i < Cloths.Count; i++) _clothRest[i] = Cloths[i].localRotation;
            foreach (var v in Vents) _vent.Add(MakeVent(v));
            foreach (var b in Braziers)
            {
                _flames.Add(MakeFlame(b));
                if (Tier >= 2) // a warm light per brazier on High and Ultra
                {
                    var l = new GameObject("BrazierLight").AddComponent<Light>();
                    l.transform.SetParent(b, false); l.transform.localPosition = new Vector3(0, 1.4f, 0);
                    l.type = LightType.Point; l.range = 5f; l.intensity = 1.2f; l.color = new Color(1f, 0.62f, 0.3f); l.shadows = LightShadows.None;
                    _lights.Add(l);
                }
            }
            _dust = MakeBurst("DebrisDust", new Color(0.55f, 0.48f, 0.4f, 0.7f), false, 0.4f, 1.2f, 1.5f, 0.2f);
            if (Sun != null) _sunBase = Sun.intensity;
            _ambBase = RenderSettings.ambientLight;
            _nextStrike = Time.time + Random.Range(5f, 9f);
            foreach (var l in Lair)
            {
                l.Rest = l.T.localRotation;
                if (l.Glow != null && l.Glow.HasProperty("_EmissionColor")) _glowBase[l] = l.Glow.GetColor("_EmissionColor");
                if (l.Fx == LairFx.Flare) _lairFlames[l] = MakeBurst("LairFlame", new Color(1f, 0.55f, 0.18f, 1), true, 0.6f, 1.1f, 3.5f, -0.4f);
            }
        }
        readonly Dictionary<LairPart, Color> _glowBase = new Dictionary<LairPart, Color>();
        readonly Dictionary<LairPart, ParticleSystem> _lairFlames = new Dictionary<LairPart, ParticleSystem>();

        // ---------- the lair answers ----------
        void LairEvent(string what, Vector3 p, bool crit, bool heavy)
        {
            foreach (var l in Lair)
            {
                if (l.T == null) continue;
                switch (l.Fx)
                {
                    case LairFx.Spin when crit || heavy || what == "shock":    // the mill wheel spins up
                        if (l.Spin < 60) ArenaEvents.RaiseLairCue("lairWheel", l.Anchor);
                        l.Spin = Mathf.Min(l.Spin + 220, 420); break;
                    case LairFx.Swing when what == "kill":                     // the bell swings and tolls
                        l.W += 120; ArenaEvents.RaiseLairCue("lairBell", l.Anchor); break;
                    case LairFx.Toll when what == "kill":                      // a bell on a post: a toll and a shiver
                        Push(l.T.position + Vector3.back, 0.3f); ArenaEvents.RaiseLairCue("lairBell", l.Anchor); break;
                    case LairFx.Beam when crit:                                // the lighthouse beam flashes
                        l.Pulse = 1; ArenaEvents.RaiseLairCue("lairBeam", l.Anchor); break;
                    case LairFx.Flare when what == "kill" || what == "shock":  // the pyre, torches and lanterns flare
                        if (_lairFlames.TryGetValue(l, out var f) && f) { f.transform.position = l.Anchor; f.Emit(Tier == 0 ? 14 : 34); }
                        ArenaEvents.RaiseLairCue("lairPyre", l.Anchor); break;
                    case LairFx.Strike when crit || what == "shock":           // lightning finds the crag or the throne
                        Lightning(true, l.Anchor); ArenaEvents.RaiseLairCue("lairThunder", l.Anchor); break;
                    case LairFx.Chime when crit:                               // the glass spires flash and sing
                        l.Pulse = 1; ArenaEvents.RaiseLairCue("lairChime", l.Anchor); break;
                    case LairFx.Drip when what == "shock":                     // the rose window / cavern glow swells
                        l.Pulse = 1; ArenaEvents.RaiseLairCue("lairDrip", l.Anchor); break;
                    case LairFx.Pulse when what == "shock" || (what == "kill" && heavy): // a Sigil shard pulses
                        l.Pulse = 1; ArenaEvents.RaiseLairCue("lairShard", l.Anchor); break;
                }
            }
            // the fen lanterns go out, one per kill or boss crit
            if (what == "kill" || (crit && heavy))
                foreach (var l in Lair)
                    if (l.Fx == LairFx.Snuff && !l.Out && l.T != null)
                    {
                        l.Out = true;
                        if (l.Glow != null && l.Glow.HasProperty("_EmissionColor")) l.Glow.SetColor("_EmissionColor", Color.black);
                        if (l.Glow != null) Materials.SetBase(l.Glow, new Color(0.05f, 0.05f, 0.06f));
                        ArenaEvents.RaiseLairCue("lairSnuff", l.Anchor);
                        break;
                    }
        }

        void UpdateLair(float dt, float t)
        {
            foreach (var l in Lair)
            {
                if (l.T == null) continue;
                switch (l.Fx)
                {
                    case LairFx.Spin:   // a slow turn, spun up by crits and easing back
                        l.Spin = Mathf.MoveTowards(l.Spin, 0, dt * 90);
                        l.A += (25 + l.Spin) * dt;
                        l.T.localRotation = l.Rest * Quaternion.Euler(0, 0, -l.A); break;
                    case LairFx.Swing:  // a damped pendulum about the yoke
                        l.W += (-l.A * 9f - l.W * 0.9f) * dt; l.A = Mathf.Clamp(l.A + l.W * dt, -40, 40);
                        l.T.localRotation = l.Rest * Quaternion.Euler(l.A, 0, 0); break;
                    case LairFx.Beam:   // the lamp turns its beam steadily
                        l.T.localRotation = Quaternion.Euler(0, t * 40f, 0) * l.Rest; break;
                }
                if (l.Glow != null && !l.Out && _glowBase.TryGetValue(l, out var baseC))
                {
                    l.Pulse = Mathf.MoveTowards(l.Pulse, 0, dt * 1.2f);
                    float k = 1 + 2.2f * l.Pulse + (l.Fx == LairFx.Beam ? 0.25f * Mathf.Sin(t * 3f) : 0);
                    l.Glow.SetColor("_EmissionColor", baseC * k);
                }
            }
        }

        // ---------- events ----------
        void OnHit(Vector3 p, float frac, bool crit, bool heavy)
        {
            LairEvent("hit", p, crit, heavy);
            if (crit || heavy) Push(p, heavy ? 0.8f : 0.45f);
            if (frac > 0.18f || heavy) Geyser(p, false);
            if (crit || heavy) Debris(p, heavy ? 3 : 2);
            if (Storm && (crit || heavy)) Lightning();
        }
        void OnKill(Vector3 p)
        {
            LairEvent("kill", p, false, false);
            _brazierFlare = 1f;
            foreach (var f in _flames) if (f) f.Emit(Tier == 0 ? 12 : 28);
            Push(p, 0.35f);
            Geyser(p, false);
        }
        void OnShock(Vector3 p, float strength, bool slam)
        {
            LairEvent("shock", p, false, slam);
            Push(p, Mathf.Clamp(strength, 0.3f, 1.6f));
            Geyser(p, true);
            Debris(p, slam ? 4 : 3, true);
            if (Storm) Lightning(true);
            if (slam && !NoCracks) Crack(p);
        }

        // ---------- sway: a damped spring per tree, pushed away from the blow ----------
        void Push(Vector3 p, float s)
        {
            // Events can arrive between OnEnable (subscribed) and Start (arrays built): a shock from an ultimate or a
            // boss blow in that gap threw a NullReferenceException and killed the battle's action loop (in-house report,
            // build 190). Until Start has run there is nothing to push.
            if (_w == null || _cw == null || _w.Length != Sway.Count || _cw.Length != Cloths.Count) return;
            for (int i = 0; i < Sway.Count; i++)
            {
                if (!Sway[i]) continue;
                var d = Sway[i].position - p; d.y = 0; float dist = d.magnitude;
                var dir = dist > 0.01f ? d / dist : Vector3.forward;
                float imp = s * 55f / (1f + dist * 0.12f);
                _w[i] += new Vector2(dir.z, -dir.x) * imp;
            }
            for (int i = 0; i < Cloths.Count; i++)
            {
                if (!Cloths[i]) continue;
                float dist = Vector3.Distance(Cloths[i].position, p);
                _cw[i] += s * 160f / (1f + dist * 0.1f) * (i % 2 == 0 ? 1 : -1);
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;
            for (int i = 0; i < Sway.Count; i++)
            {
                if (!Sway[i]) continue;
                _w[i] += (-_a[i] * 38f - _w[i] * 4.2f) * dt;
                _a[i] += _w[i] * dt;
                _a[i] = Vector2.ClampMagnitude(_a[i], 9f);
                float wind = Mathf.Sin(t * 0.9f + _phase[i]) * 0.8f + Mathf.Sin(t * 2.3f + _phase[i] * 2) * 0.25f; // a gentle idle breeze
                Sway[i].localRotation = Quaternion.Euler(_a[i].x + wind, 0, _a[i].y + wind * 0.4f) * _rest[i];
            }
            for (int i = 0; i < Cloths.Count; i++)
            {
                if (!Cloths[i]) continue;
                _cw[i] += (-_ca[i] * 22f - _cw[i] * 2.6f) * dt;
                _ca[i] = Mathf.Clamp(_ca[i] + _cw[i] * dt, -50f, 50f);
                float flutter = Mathf.Sin(t * 1.7f + i) * 4f + Mathf.Sin(t * 4.1f + i * 2) * 1.5f;
                Cloths[i].localRotation = _clothRest[i] * Quaternion.Euler(_ca[i] + flutter, 0, 0);
            }
            // brazier flare: the lights jump and settle
            _brazierFlare = Mathf.MoveTowards(_brazierFlare, 0, dt * 1.2f);
            for (int i = 0; i < _lights.Count; i++) if (_lights[i]) _lights[i].intensity = 1.2f + 3.2f * _brazierFlare + Mathf.PerlinNoise(t * 6f, i) * 0.4f;
            _cdGeyser -= dt; _cdDebris -= dt; _cdBolt -= dt;
            UpdateRocks(dt);
            UpdateLair(dt, t);
            UpdateCracks(dt);
            // lightning flash and the bolt
            if (_flash > 0 || _boltT > 0)
            {
                _flash = Mathf.Max(0, _flash - dt * 5f);
                float f = _flash * Access.FlashScale; // Settings > Accessibility > Flash effects
                if (Sun != null) Sun.intensity = _sunBase * (1 + 1.6f * f);
                RenderSettings.ambientLight = _ambBase * (1 + 1.2f * f);
                _lit = f > 0;
                if (_bolt != null)
                {
                    _boltT -= dt;
                    // the bolt flickers as it fades; with flashes limited it shows steady (Reduced) or not at all (Off)
                    _bolt.enabled = _boltT > 0 && Access.Flash < 2 && (_boltT > 0.12f || Access.Flash == 1 || _boltT % 0.06f > 0.03f);
                }
            }
            if (Storm && t > _nextStrike) { Lightning(); _nextStrike = t + Random.Range(7f, 14f); }
        }

        // Only undo a flash in progress: a newly built arena has already set its own sun and ambient.
        void RestoreLight()
        {
            if (!_lit) return;
            _lit = false;
            if (Sun != null && _sunBase > 0) Sun.intensity = _sunBase;
            RenderSettings.ambientLight = _ambBase;
        }

        // ---------- vents ----------
        Vector3 NearestVent(Vector3 p)
        {
            Vector3 best = Vents.Count > 0 ? Vents[0] : p; float bd = 1e9f;
            foreach (var v in Vents) { float d = (v - p).sqrMagnitude; if (d < bd) { bd = d; best = v; } }
            return best;
        }
        void Geyser(Vector3 p, bool all)
        {
            if (_vent.Count == 0 || (!all && _cdGeyser > 0)) return;
            _cdGeyser = 0.6f;
            int n = Kind == Hazard.Lava ? 46 : Kind == Hazard.Water ? 40 : 30;
            if (Tier == 0) n /= 2;
            if (all) { foreach (var v in _vent) if (v) v.Emit(n); return; }
            var at = NearestVent(p);
            for (int i = 0; i < _vent.Count; i++) if (_vent[i] && (Vents[i] - at).sqrMagnitude < 0.01f) _vent[i].Emit(n);
        }

        ParticleSystem MakeVent(Vector3 at)
        {
            bool add = Kind == Hazard.Lava || Kind == Hazard.Crystal || Kind == Hazard.Spores;
            Color c = Kind switch
            {
                Hazard.Lava => new Color(1f, 0.5f, 0.15f, 1),
                Hazard.Water => new Color(0.85f, 0.95f, 1f, 0.85f),
                Hazard.Crystal => new Color(Tint.r, Tint.g, Tint.b, 1),
                Hazard.Spores => new Color(Tint.r, Tint.g, Tint.b, 0.9f),
                _ => new Color(0.6f, 0.52f, 0.42f, 0.75f),
            };
            float speed = Kind == Hazard.Lava ? 8.5f : Kind == Hazard.Water ? 7f : Kind == Hazard.Spores ? 2.2f : 4.5f;
            float size = Kind == Hazard.Dust ? 0.7f : Kind == Hazard.Water ? 0.32f : 0.24f;
            float grav = Kind == Hazard.Spores ? -0.05f : Kind == Hazard.Dust ? 0.3f : 1.1f;
            var ps = MakeBurst("Vent", c, add, size, Kind == Hazard.Spores ? 3f : 1.6f, speed, grav);
            ps.transform.position = at;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = Kind == Hazard.Dust ? 30 : 12; sh.radius = 0.35f;
            ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
            return ps;
        }

        ParticleSystem MakeFlame(Transform brazier)
        {
            var ps = MakeBurst("Flame", new Color(1f, 0.6f, 0.2f, 1), true, 0.35f, 0.8f, 1.4f, -0.3f);
            ps.transform.SetParent(brazier, false); ps.transform.localPosition = new Vector3(0, 1.05f, 0); ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8; sh.radius = 0.18f;
            var em = ps.emission; em.rateOverTime = Tier == 0 ? 6 : 14;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeBurst(string name, Color c, bool additive, float size, float life, float speed, float gravity)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.maxParticles = 160; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life); main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size); main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.35f));
            var em = ps.emission; em.rateOverTime = 0;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            var add = Shader.Find("Legacy Shaders/Particles/Additive"); var blend = Shader.Find("Sprites/Default");
            var mat = additive && add != null ? new Material(add) : blend != null ? new Material(blend) : Materials.Unlit(Color.white);
            mat.mainTexture = Scenery.MoteTex(false);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mat.mainTexture);
            r.sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        // ---------- falling debris ----------
        void Debris(Vector3 near, int count, bool force = false)
        {
            if (!force && _cdDebris > 0) return;
            _cdDebris = 0.8f;
            if (Ramp == null) return;
            int max = Tier == 0 ? 3 : Tier == 1 ? 6 : 9;
            for (int k = 0; k < count; k++)
            {
                Rock r = null;
                foreach (var x in _rocks) if (!x.T.gameObject.activeSelf) { r = x; break; }
                if (r == null)
                {
                    if (_rocks.Count >= max) return;
                    var go = SceneryKit.Place(transform, "debris" + (_rocks.Count % 2), Vector3.zero, 1, 0, Ramp, Ramp);
                    if (go == null) return;
                    go.name = "Debris";
                    r = new Rock { T = go.transform }; _rocks.Add(r);
                }
                // somewhere off the ring, on the far half or the flanks (never between camera and heroes)
                float a = Random.Range(10f, 170f) * Mathf.Deg2Rad, rad = Random.Range(6.8f, 11f);
                var land = new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad * 0.9f + 2f);
                float s = Random.Range(0.8f, 1.8f);
                r.T.gameObject.SetActive(true);
                r.T.localScale = Vector3.one * s;
                r.T.position = land + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(9f, 13f), Random.Range(-1f, 1f));
                r.T.rotation = Random.rotation;
                r.V = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-3f, -1f), Random.Range(-0.6f, 0.6f));
                r.Spin = Random.insideUnitSphere * 360f; r.Age = 0; r.Landed = false; r.Rest = 0.12f * s;
            }
        }
        void UpdateRocks(float dt)
        {
            foreach (var r in _rocks)
            {
                if (!r.T.gameObject.activeSelf) continue;
                r.Age += dt;
                if (!r.Landed)
                {
                    r.V += Vector3.down * 16f * dt;
                    r.T.position += r.V * dt; r.T.Rotate(r.Spin * dt, Space.World);
                    if (r.T.position.y <= r.Rest)
                    {
                        var p = r.T.position; p.y = r.Rest; r.T.position = p;
                        if (r.V.y < -6f) { r.V = new Vector3(r.V.x * 0.4f, -r.V.y * 0.25f, r.V.z * 0.4f); r.Spin *= 0.3f; } // one small bounce
                        else { r.Landed = true; r.Age = 0; }
                        if (_dust) { _dust.transform.position = p; _dust.Emit(Tier == 0 ? 5 : 12); }
                    }
                }
                else if (r.Age > 2.6f)
                {
                    r.T.position += Vector3.down * dt * 0.5f; // settle into the ground, then recycle
                    if (r.Age > 4f) r.T.gameObject.SetActive(false);
                }
            }
        }

        // ---------- lightning ----------
        void Lightning(bool force = false, Vector3? target = null)
        {
            if (!force && _cdBolt > 0) return;
            _cdBolt = 1.6f;
            if (_bolt == null)
            {
                var go = new GameObject("SkyBolt"); go.transform.SetParent(transform, false);
                _bolt = go.AddComponent<LineRenderer>();
                _bolt.positionCount = 16; _bolt.useWorldSpace = true; _bolt.widthCurve = AnimationCurve.Linear(0, 0.5f, 1, 0.12f);
                var add = Shader.Find("Legacy Shaders/Particles/Additive");
                _bolt.sharedMaterial = add != null ? new Material(add) { color = new Color(0.75f, 0.85f, 1f) } : Materials.Unlit(new Color(0.85f, 0.9f, 1f));
                _bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var end = target ?? new Vector3(Random.Range(-26f, 26f), 0, Random.Range(26f, 36f)); // a lair crag or throne, else the far ridges
            var start = end + new Vector3(Random.Range(-6f, 6f), 36f, Random.Range(2f, 6f));
            for (int i = 0; i < 16; i++)
            {
                float k = i / 15f;
                var p = Vector3.Lerp(start, end, k);
                if (i > 0 && i < 15) p += new Vector3(Random.Range(-1.6f, 1.6f), 0, Random.Range(-0.8f, 0.8f));
                _bolt.SetPosition(i, p);
            }
            _boltT = 0.22f; _bolt.enabled = true; _flash = 1f;
        }

        // ---------- ground cracks under a boss slam ----------
        void Crack(Vector3 p)
        {
            Transform t = null; Material m = null;
            for (int i = 0; i < _cracks.Count; i++) if (!_cracks[i].t.gameObject.activeSelf) { t = _cracks[i].t; m = _cracks[i].m; _cracks.RemoveAt(i); break; }
            if (t == null)
            {
                if (_cracks.Count >= 4) { t = _cracks[0].t; m = _cracks[0].m; _cracks.RemoveAt(0); }
                else
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                    q.name = "Circle_Crack"; q.transform.SetParent(transform, false);
                    bool lava = Kind == Hazard.Lava; var add = Shader.Find("Legacy Shaders/Particles/Additive");
                    m = lava && add != null ? new Material(add) : Materials.Unlit(Color.white);
                    m.mainTexture = SceneryPaint.Crack();
                    var rr = q.GetComponent<Renderer>(); rr.sharedMaterial = m; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
                    t = q.transform;
                }
            }
            t.gameObject.SetActive(true);
            t.position = new Vector3(p.x, 0.015f, p.z); t.rotation = Quaternion.Euler(90, Random.Range(0, 360f), 0);
            t.localScale = Vector3.one * Random.Range(2.6f, 3.6f);
            _cracks.Add((t, m, 0));
        }
        void UpdateCracks(float dt)
        {
            for (int i = 0; i < _cracks.Count; i++)
            {
                var (t, m, age) = _cracks[i];
                if (!t.gameObject.activeSelf) continue;
                age += dt; _cracks[i] = (t, m, age);
                float a = age < 0.15f ? age / 0.15f : Mathf.Clamp01(1 - (age - 2.5f) / 1.5f);
                bool lava = Kind == Hazard.Lava;
                var c = lava ? new Color(1f, 0.45f, 0.12f, a) * new Color(a, a, a, 1) : new Color(0.08f, 0.06f, 0.05f, 0.85f * a);
                m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (age > 4f) t.gameObject.SetActive(false);
            }
        }
    }
}
