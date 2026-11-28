// The remodelled Titan's body effects (owner request 2026-11-28), on the high-detail Titan only:
// - its molten seams and open chest core breathe with the idle and FLARE in the wind-up of every big move
//   (BattleController ramps Charge before the impact and releases it after);
// - the crystal ridges on its back and shoulders glow while it attacks (the glow mask's red channel, _RidgeColor);
// - the crystals crack and shatter off as its health falls (TitanSpec.RidgeStages: 66% and 33% of the top damage
//   tier still to deal), shards flying (none on Low), broken crystals left as stubs;
// - sockets for the effects: the chest core (the breath pours from it), the fists (impacts land where they are).
// Everything is tinted by the day's element (titan.json "seams"). Time-based smoothing (1 - exp(-dt / tc)), so it
// looks the same at 30, 60 and 120 fps. Uses a MaterialPropertyBlock: the shared Titan material is never changed.
using System.Collections.Generic;
using Sigilbound.Core;
using UnityEngine;

namespace Sigilbound.Client
{
    public sealed class TitanBody : MonoBehaviour
    {
        Renderer[] _renderers = new Renderer[0];
        MaterialPropertyBlock _mpb;
        Color _seam; Element _el;
        Animation _anim;
        public Transform Chest, FistL, FistR;
        readonly List<(Transform t, int stage)> _ridges = new List<(Transform, int)>();
        readonly HashSet<Transform> _broken = new HashSet<Transform>();
        float _charge, _chargeTo, _chargeTc = 0.4f;   // 0 = calm, 1 = full wind-up flare
        float _dim = 1, _dimTo = 1, _dimTc = 0.5f;    // the collapse fades the glow out
        float _pulse;                                 // a short spike at an impact (decays by itself)
        int _stage;

        public static TitanBody Of(CharacterView v) => v != null && v.Hero != null && v.Hero.Root != null ? v.Hero.Root.GetComponentInChildren<TitanBody>() : null;
        public int Stage => _stage;
        public int Broken => _broken.Count;

        public void Init(Transform model, Element el, Animation anim)
        {
            var spec = TitanSpec.Current;
            _el = el; _seam = HeroModels.TitanSeam(el); _anim = anim;
            _renderers = model.GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            Chest = BodyShaper.Find(model, spec.ChestBone); FistL = BodyShaper.Find(model, spec.FistL); FistR = BodyShaper.Find(model, spec.FistR);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("ridge_") && t.name.Length > 3 && t.name[t.name.Length - 3] == '_' && t.name[t.name.Length - 2] == 's')
                    _ridges.Add((t, t.name[t.name.Length - 1] - '0'));
        }

        // Where the breath leaves the body: the chest-core socket, else the chest height on the model's front.
        public Vector3 ChestPoint(CharacterView v)
        {
            if (Chest != null) return Chest.position;
            var spec = TitanSpec.Current; return v.transform.position + Vector3.up * (float)(spec.Chest * spec.Head) + v.transform.forward * 0.8f;
        }
        // Where the fists are (their centre), for impacts that land where they actually hit.
        public Vector3? Fists => FistL != null && FistR != null ? (FistL.position + FistR.position) * 0.5f : (Vector3?)null;
        // The lower fist (the one raking the ground in a one-arm sweep).
        public Vector3? LowFist => FistL != null && FistR != null ? (FistL.position.y < FistR.position.y ? FistL.position : FistR.position) : (Vector3?)null;

        // Wind-up: ramp the seam, core and ridge glow toward `level` (0-1) with time constant `tc` seconds.
        public void Charge(float level, float tc) { _chargeTo = Mathf.Clamp01(level); _chargeTc = Mathf.Max(0.02f, tc); }
        // The impact: a bright spike on top of the charge.
        public void Pulse(float amount = 1f) => _pulse = Mathf.Max(_pulse, amount);
        // The collapse: the glow dies down to `level` over `tc`.
        public void Dim(float level, float tc) { _dimTo = Mathf.Clamp01(level); _dimTc = Mathf.Max(0.02f, tc); }

        // Health 0-1 (TitanSpec.Health of the damage dealt): crystals break off when a stage is passed.
        public void SetHealth(double health)
        {
            int s = TitanSpec.Current.RidgeStage(health);
            while (_stage < s) { _stage++; Break(_stage); }
        }

        void Break(int stage)
        {
            var fx = SkillFx.I; int tier = fx != null ? fx.Tier : 1;
            int shards = TitanSpec.ShardsFor(tier); bool any = false;
            foreach (var (t, st) in _ridges)
            {
                if (st != stage || t == null || !_broken.Add(t)) continue;
                any = true;
                var outward = t.position - (transform.position + Vector3.up * t.position.y); outward.y = 0;
                outward = (outward.sqrMagnitude > 1e-4f ? outward.normalized : -transform.forward) + Vector3.up * 0.9f;
                if (fx != null) fx.RidgeShatter(t.position, outward, _el, _seam, shards);
            }
            if (!any) return;
            _pulse = Mathf.Max(_pulse, 0.6f);
            Vfx.I?.Shake(0.12f);
            Sound.I?.PlayAt("crit", transform.position + Vector3.up * 5f, 0.7f, 0.7f, 0.05f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime; if (dt <= 0) dt = Time.unscaledDeltaTime;
            var spec = TitanSpec.Current;
            _charge += (_chargeTo - _charge) * (1 - Mathf.Exp(-dt / _chargeTc));
            _dim += (_dimTo - _dim) * (1 - Mathf.Exp(-dt / _dimTc));
            _pulse *= Mathf.Exp(-dt / 0.18f);
            // Breathing: the core swells on the idle's inhale (its own clip's event), calm otherwise.
            float breath = 0;
            if (_anim != null)
            {
                string idle = spec.ClipOf("idle", null);
                var st = idle != null ? _anim[idle] : null;
                if (st != null && st.enabled && st.weight > 0.3f && st.length > 0)
                {
                    float ph = st.time / st.length - (float)spec.Ev("idle", "inhale", 0.25);
                    breath = (0.5f + 0.5f * Mathf.Cos(ph * Mathf.PI * 2)) * st.weight;
                }
            }
            float gain = (float)spec.GlowGain;
            float mul = (1 + ((float)spec.FlareGain - 1) * _charge + 0.6f * _pulse + 0.14f * breath * (1 - _charge)) * _dim;
            var glow = _seam * gain * mul; glow.a = 1;
            var ridge = _seam * (float)spec.RidgeGlow * Mathf.Clamp01(_charge + 0.7f * _pulse) * _dim; ridge.a = 1;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_GlowColor", glow); _mpb.SetColor("_RidgeColor", ridge);
                r.SetPropertyBlock(_mpb);
            }
            // The open chest core: one soft glow sprite on it (Medium and up), brighter in a wind-up.
            var fx = SkillFx.I;
            float core = (0.25f + 0.25f * breath + 1.1f * _charge + 0.8f * _pulse) * _dim;
            if (fx != null && Chest != null && fx.Tier > 0 && core > 0.05f) fx.CoreGlow(Chest.position, _seam, core, 1.4f + 1.8f * _charge);
            // Broken crystals stay stubs whatever the clip does.
            foreach (var t in _broken) if (t != null) t.localScale = Vector3.one * 0.3f;
        }
    }
}
