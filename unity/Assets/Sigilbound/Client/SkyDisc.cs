// The visible sun (or moon) on the sky (owner decision 2026-11-28, real-sun sky): a small disc and a wide soft halo,
// two camera-facing quads of additive light (Shaders/SigilSkyGlow.shader), placed along the direction the stage's
// light comes from (SceneLight.DiscYaw / DiscElevation), so the shadows on the ground point away from it.
//   - Distance: 42 m from the camera, every frame (Arena.LateUpdate calls Follow after composing the camera), so the
//     body sits "at infinity" and never moves against the hills when the camera swings. The painted sky arc is
//     50+ m out and the nearest ridge card 28 m, all drawn without depth: the order is the render queue
//     (sky 2995, halo and disc 2996, ridges 2997-2999), so the ridges hide a low sun and the disc sits on the sky.
//     Its depth test still lets scenery, heroes and the ground (all written to depth) cover it.
//   - Cost: two quads, one material each, no post-processing, no extra light; on every tier.
//   - Edge glow (owner decision 2026-11-28): the disc stays exactly true, so when it is above or beside the frame (always
//     in the default battle and menu views, which end below the horizon, L032) a warm radial glow and 3-5 soft, slowly
//     breathing beams come in from the top edge on its side (Core/SkyEdge: how far outside, how strong). A faint cool
//     glow by moonlight, no beams; storms dim it; no beams on Low. Quads 1 m in front of the camera, drawn after the 3D
//     effects (queue 3100) and before the IMGUI HUD, which always paints over them; the glow's centre sits above the
//     top edge so only its tail reaches the turn bar.
//   - The disc may sit lower than the light: the light's pitch keeps a 12 degree floor (SolarClock.MinShadowPitch),
//     the disc keeps the true elevation and fades out a degree or two below the horizon.
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed class SkyDisc : MonoBehaviour
    {
        public const float Distance = 42f;
        public const int Queue = 2996;          // after the painted sky (2995), before the ridges (2997-2999)
        public const int EdgeQueue = 3100;      // the edge glow: over the 3D scene and its effects, under the HUD
        Transform _disc, _halo; Material _discMat, _haloMat;
        Transform _edge, _edgeGlow; readonly System.Collections.Generic.List<Transform> _beams = new System.Collections.Generic.List<Transform>();
        Material _edgeGlowMat; readonly System.Collections.Generic.List<Material> _beamMats = new System.Collections.Generic.List<Material>();
        Color _edgeTint; float _el; bool _storm;
        public float EdgeGlow { get; private set; }   // last frame's glow strength (0 = none): ShotsMode logs it
        Vector3 _dir = Vector3.forward;
        float _discDeg, _haloDeg;
        public bool Moon { get; private set; }
        public Vector3 Direction => _dir;       // toward the body (world, unit)
        static Texture2D _discTex, _haloTex, _moonTex, _beamTex;
        static Shader _shader; static bool _looked;

        // Builds the disc for this stage under the scenery root. tint: the sky's horizon colour (warms or cools the halo).
        public static SkyDisc Build(Transform root, SceneLight li, Color horizon, bool storm)
        {
            var go = new GameObject("SkyDisc");
            go.transform.SetParent(root, false);
            var d = go.AddComponent<SkyDisc>();
            d.Set(li, horizon, storm);
            return d;
        }

        void Set(SceneLight li, Color horizon, bool storm)
        {
            Moon = li.DiscMoon;
            float el = li.DiscElevation * Mathf.Deg2Rad, yaw = li.DiscYaw * Mathf.Deg2Rad;
            _dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yaw) * Mathf.Cos(el)).normalized;
            var (disc, halo, discDeg, haloDeg) = Look(li, horizon);
            // Weather on top: storm clouds swallow most of the disc and halo.
            float wx = storm ? 0.45f : 1f;
            // Below the horizon it fades out (the ridges usually hide it first).
            float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 1.5f, li.DiscElevation));
            disc *= wx * up; halo *= wx * up;
            _discDeg = discDeg; _haloDeg = haloDeg;
            EnsureTextures();
            _halo = Quad("SkyHalo", _haloTex, halo, out _haloMat);
            _disc = Quad(Moon ? "SkyMoon" : "SkySun", Moon ? _moonTex : _discTex, disc, out _discMat);
            // The edge glow: the halo's colour, normalised so the strength alone sets how bright it gets.
            _el = li.DiscElevation; _storm = storm;
            var hc = Look(li, horizon).halo; float mx = Mathf.Max(0.01f, Mathf.Max(hc.r, Mathf.Max(hc.g, hc.b)));
            _edgeTint = new Color(hc.r / mx, hc.g / mx, hc.b / mx, 1f);
            _edge = new GameObject("SkyEdge").transform; _edge.SetParent(transform, false);
            _edgeGlow = Quad("SkyEdgeGlow", _haloTex, Color.black, out _edgeGlowMat, _edge, EdgeQueue);
            int tier = Quality.I != null ? Quality.I.Tier : 2;
            int beams = Moon ? 0 : tier >= 2 ? 5 : tier == 1 ? 3 : 0;   // no beams on Low, none by moonlight
            for (int i = 0; i < beams; i++) { _beams.Add(Quad("SkyEdgeBeam", _beamTex, Color.black, out var bm, _edge, EdgeQueue)); _beamMats.Add(bm); }
            _edge.gameObject.SetActive(false);
            bool show = up > 0.01f;
            _halo.gameObject.SetActive(show); _disc.gameObject.SetActive(show);
        }

        // Colour and size per band: a white noon sun with a pale halo; warmer and wider toward golden hour and dusk;
        // a small cool moon with a faint glow. Sizes are angular diameters in degrees (stylised: the real sun is 0.5).
        static (Color disc, Color halo, float discDeg, float haloDeg) Look(SceneLight li, Color horizon)
        {
            if (li.DiscMoon) return (new Color(0.86f, 0.9f, 1f) * 0.85f, Color.Lerp(new Color(0.55f, 0.65f, 1f), horizon, 0.3f) * 0.2f, 3.2f, 16f);
            switch (li.Mood)
            {
                case Mood.Dusk: return (new Color(1f, 0.58f, 0.32f), Color.Lerp(new Color(1f, 0.45f, 0.22f), horizon, 0.3f) * 0.6f, 4.6f, 40f);
                case Mood.Golden: return (new Color(1f, 0.8f, 0.52f), Color.Lerp(new Color(1f, 0.62f, 0.32f), horizon, 0.25f) * 0.5f, 4.2f, 36f);
                case Mood.Late: return (new Color(1f, 0.93f, 0.78f), Color.Lerp(new Color(1f, 0.85f, 0.6f), horizon, 0.2f) * 0.4f, 3.9f, 32f);
                default: return (new Color(1f, 0.97f, 0.9f), Color.Lerp(new Color(1f, 0.92f, 0.75f), horizon, 0.2f) * 0.35f, 3.8f, 30f);
            }
        }

        Transform Quad(string name, Texture2D tex, Color tint, out Material mat, Transform parent = null, int queue = Queue)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(parent != null ? parent : transform, false);
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (!_looked) { _shader = Shader.Find("Sigilbound/SkyGlow"); _looked = true; }
            if (_shader != null)
            {
                mat = new Material(_shader) { name = name };
                mat.SetTexture("_MainTex", tex);
                tint.a = 1f; mat.SetColor("_TintColor", tint);
            }
            else
            {
                // Fallback: alpha-blended, the profile as alpha and the tint's brightness as its strength.
                mat = new Material(Shader.Find("Sprites/Default")) { name = name + "Fallback", mainTexture = tex };
                float k = Mathf.Clamp01(Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
                mat.color = new Color(Mathf.Clamp01(tint.r / Mathf.Max(0.01f, k)), Mathf.Clamp01(tint.g / Mathf.Max(0.01f, k)), Mathf.Clamp01(tint.b / Mathf.Max(0.01f, k)), k);
            }
            mat.renderQueue = queue;
            r.sharedMaterial = mat;
            return q.transform;
        }

        // Keeps the quads along the light's direction from wherever the camera is (Arena.LateUpdate).
        public void Follow(Camera cam)
        {
            if (cam == null || _disc == null) return;
            var eye = cam.transform.position;
            var rot = Quaternion.LookRotation(_dir, Vector3.up);
            // The halo a touch farther, so it sorts behind the disc (both additive, but it keeps the order stable).
            Place(_halo, eye + _dir * (Distance + 1f), rot, _haloDeg, Distance + 1f);
            Place(_disc, eye + _dir * Distance, rot, _discDeg, Distance);
            Edge(cam);
        }

        static readonly float[] BeamSpread = { -24f, 12f, -6f, 26f, 2f };   // degrees around the beams' lean; the first 3 on Medium
        void Edge(Camera cam)
        {
            if (_edge == null) return;
            var ct = cam.transform;
            var (outDeg, sx, _) = SkyEdge.Frame(V(_dir), V(ct.right), V(ct.up), V(ct.forward), cam.fieldOfView, cam.aspect);
            var (glow, beams) = SkyEdge.Strength(outDeg, _el, Moon, _storm);
            EdgeGlow = (float)glow;
            bool on = glow > 0.004 && _disc.gameObject.activeSelf;
            if (_edge.gameObject.activeSelf != on) _edge.gameObject.SetActive(on);
            if (!on) return;
            const float d = 1f;                                           // metres in front of the camera (near clip 0.1)
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var src = cam.ViewportToWorldPoint(new Vector3((float)sx, 1.12f, d));   // just above the top edge, on the body's side
            _edgeGlow.position = src; _edgeGlow.rotation = ct.rotation;
            _edgeGlow.localScale = new Vector3(h * 1.7f, h * 1.7f, 1f);
            Tint(_edgeGlowMat, _edgeTint * (float)glow * (Moon ? 0.55f : 0.34f));
            // Beams lean in toward the middle of the screen and fan out; each breathes slowly (still with Reduce motion).
            float lean = Mathf.Atan2(((float)sx - 0.5f) * -1.1f, 1f) * Mathf.Rad2Deg;
            float t = Access.ReduceMotion ? 0f : Time.time;
            for (int i = 0; i < _beams.Count; i++)
            {
                float a = (lean + BeamSpread[i]) * Mathf.Deg2Rad;
                var dir = (ct.right * Mathf.Sin(a) - ct.up * Mathf.Cos(a)).normalized;   // downward, leaning
                float len = h * (1.25f + 0.2f * (i % 3)), wid = h * (0.07f + 0.035f * (i % 2));
                _beams[i].position = src + dir * (len * 0.5f) + ct.up * (h * 0.06f);
                _beams[i].rotation = Quaternion.LookRotation(ct.forward, -dir);
                _beams[i].localScale = new Vector3(wid, len, 1f);
                float breath = 0.7f + 0.3f * Mathf.Sin(t * (0.25f + 0.07f * i) + i * 1.9f);
                Tint(_beamMats[i], _edgeTint * (float)beams * 0.12f * breath);
            }
        }

        static V3 V(Vector3 v) => new V3(v.x, v.y, v.z);

        // Additive SkyGlow takes the tint as light; the Sprites/Default fallback as colour and alpha.
        static void Tint(Material m, Color c)
        {
            if (m.HasProperty("_TintColor")) { c.a = 1f; m.SetColor("_TintColor", c); return; }
            float k = Mathf.Clamp01(Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            m.color = k <= 0f ? Color.clear : new Color(c.r / k, c.g / k, c.b / k, k);
        }

        static void Place(Transform t, Vector3 pos, Quaternion rot, float deg, float dist)
        {
            t.position = pos; t.rotation = rot;
            float s = 2f * dist * Mathf.Tan(deg * 0.5f * Mathf.Deg2Rad);
            t.localScale = new Vector3(s, s, 1f);
        }

        void OnDestroy()
        {
            if (_discMat) Destroy(_discMat);
            if (_haloMat) Destroy(_haloMat);
            if (_edgeGlowMat) Destroy(_edgeGlowMat);
            foreach (var m in _beamMats) if (m) Destroy(m);
        }

        // Radial profiles, made once: the sun a bright core with a soft rim, the halo a long Gaussian tail that dies at
        // the quad's edge, the moon a cooler disc with a faint darker limb and a couple of soft "seas".
        static void EnsureTextures()
        {
            if (_discTex != null) return;
            const int N = 64;
            _discTex = Radial(N, "SkySunTex", r => Mathf.Clamp01(1f - Mathf.SmoothStep(0.30f, 0.42f, r)) + 0.35f * Mathf.Exp(-r * r * 18f) * (1f - Mathf.SmoothStep(0.8f, 1f, r)));
            _haloTex = Radial(N, "SkyHaloTex", r => Mathf.Exp(-r * r * 5.5f) * (1f - Mathf.SmoothStep(0.7f, 1f, r)));
            _moonTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "SkyMoonTex", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2 - 1, v = (y + 0.5f) / N * 2 - 1, r = Mathf.Sqrt(u * u + v * v);
                    float disc = 1f - Mathf.SmoothStep(0.36f, 0.44f, r);
                    float limb = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(r / 0.44f));
                    float seas = 1f - 0.18f * Mathf.Exp(-((u + 0.12f) * (u + 0.12f) + (v - 0.1f) * (v - 0.1f)) * 60f) - 0.14f * Mathf.Exp(-((u - 0.14f) * (u - 0.14f) + (v + 0.08f) * (v + 0.08f)) * 80f);
                    float glow = 0.18f * Mathf.Exp(-r * r * 10f) * (1f - Mathf.SmoothStep(0.8f, 1f, r));
                    _moonTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(disc * limb * seas + glow)));
                }
            _moonTex.Apply(false, true);
            // A beam: soft across, brightest at the top (where it enters) and fading out down its length.
            _beamTex = new Texture2D(16, 64, TextureFormat.RGBA32, false) { name = "SkyBeamTex", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 16; x++)
                {
                    float u = (x + 0.5f) / 16 * 2 - 1, v = (y + 0.5f) / 64;   // v = 1 at the top
                    float a = Mathf.Pow(1 - Mathf.Abs(u), 2f) * Mathf.SmoothStep(0f, 1f, v) * Mathf.SmoothStep(0f, 1f, (1 - v) / 0.08f);
                    _beamTex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            _beamTex.Apply(false, true);
        }

        static Texture2D Radial(int n, string name, System.Func<float, float> a)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a(Mathf.Sqrt(u * u + v * v)))));
                }
            t.Apply(false, true);
            return t;
        }
    }
}
