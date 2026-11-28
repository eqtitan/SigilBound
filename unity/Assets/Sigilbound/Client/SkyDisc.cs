// The visible sun (or moon) on the sky (owner decision 2026-11-28, real-sun sky): a small disc and a wide soft halo,
// two camera-facing quads of additive light (Shaders/SigilSkyGlow.shader), placed along the direction the stage's
// light comes from (SceneLight.DiscYaw / DiscElevation), so the shadows on the ground point away from it.
//   - Distance: 42 m from the camera, every frame (Arena.LateUpdate calls Follow after composing the camera), so the
//     body sits "at infinity" and never moves against the hills when the camera swings. The painted sky arc is
//     50+ m out and the nearest ridge card 28 m, all drawn without depth: the order is the render queue
//     (sky 2995, halo and disc 2996, ridges 2997-2999), so the ridges hide a low sun and the disc sits on the sky.
//     Its depth test still lets scenery, heroes and the ground (all written to depth) cover it.
//   - Cost: two quads, one material each, no post-processing, no extra light; on every tier.
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
        Transform _disc, _halo; Material _discMat, _haloMat;
        Vector3 _dir = Vector3.forward;
        float _discDeg, _haloDeg;
        public bool Moon { get; private set; }
        public Vector3 Direction => _dir;       // toward the body (world, unit)
        static Texture2D _discTex, _haloTex, _moonTex;
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

        Transform Quad(string name, Texture2D tex, Color tint, out Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(transform, false);
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
            mat.renderQueue = Queue;
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
