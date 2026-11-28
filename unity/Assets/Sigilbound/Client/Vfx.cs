// Battle effects built in code: particle bursts, projectiles with trails,
// floating damage numbers, heal sparkles and camera shake.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed class Vfx : MonoBehaviour
    {
        public static Vfx I;
        Material _particleMat; Font _font; Camera _cam; Vector3 _camHome;

        public static Vfx Create(Camera cam)
        {
            var v = new GameObject("Vfx").AddComponent<Vfx>();
            v._cam = cam; v._camHome = cam.transform.position;
            v._particleMat = new Material(Shader.Find("Sprites/Default"));
            v._particleMat.mainTexture = SoftDot();
            v._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            I = v;
            return v;
        }

        static Texture2D SoftDot()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d * d)));
                }
            t.Apply();
            return t;
        }

        ParticleSystem MakeSystem(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity, bool sphere = true)
        {
            var go = new GameObject("fx");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed); main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var sh = ps.shape; sh.shapeType = sphere ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Circle; sh.radius = 0.15f;
            if (!sphere) sh.rotation = new Vector3(-90, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = _particleMat;
            ps.Play();
            return ps;
        }

        public void Burst(Vector3 pos, Color color, bool big)
        {
            MakeSystem(pos, color, big ? 40 : 20, big ? 5f : 3.2f, big ? 0.35f : 0.25f, 0.55f, 0.6f);
            MakeSystem(pos, Color.white, big ? 10 : 5, 1.5f, big ? 0.9f : 0.6f, 0.18f, 0f); // flash core
        }
        public void Heal(Vector3 pos) => StartCoroutine(Rise(pos, new Color(0.45f, 1f, 0.5f), 18));
        public void Buff(Vector3 pos, bool good) => StartCoroutine(Rise(pos, good ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.45f, 1f), 10));
        public void Dissolve(Vector3 pos) => MakeSystem(pos, new Color(0.5f, 0.5f, 0.55f), 40, 1.6f, 0.3f, 1.2f, -0.3f);
        public void Cast(Vector3 pos, Color color) => MakeSystem(pos, color, 24, 1.2f, 0.3f, 0.7f, -0.6f, false);

        IEnumerator Rise(Vector3 pos, Color color, int n)
        {
            var ps = MakeSystem(pos + Vector3.down * 0.5f, color, n, 0.5f, 0.22f, 1.0f, -0.8f, false);
            yield return null;
        }

        public IEnumerator Projectile(Vector3 from, Vector3 to, Color color, float dur = 0.3f)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.transform.localScale = Vector3.one * 0.28f;
            orb.GetComponent<Renderer>().sharedMaterial = CharacterView.Unlit(Color.Lerp(color, Color.white, 0.4f));
            var trail = orb.AddComponent<TrailRenderer>();
            trail.time = 0.25f; trail.startWidth = 0.25f; trail.endWidth = 0f;
            trail.sharedMaterial = _particleMat;
            trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0);
            for (float k = 0; k < 1; k += Time.deltaTime / dur)
            {
                orb.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                yield return null;
            }
            Destroy(orb);
        }

        public void Slash(Vector3 pos, Color color)
        {
            var go = new GameObject("slash");
            go.transform.position = pos;
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 12; lr.useWorldSpace = false; lr.sharedMaterial = _particleMat;
            lr.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 0.18f), new Keyframe(1, 0));
            lr.startColor = Color.white; lr.endColor = color;
            float rot = Random.Range(-30f, 30f);
            for (int i = 0; i < 12; i++)
            {
                float a = Mathf.Lerp(-1.1f, 1.1f, i / 11f);
                lr.SetPosition(i, Quaternion.Euler(0, 0, rot) * new Vector3(Mathf.Sin(a) * 0.7f, Mathf.Cos(a) * 0.7f - 0.4f, 0));
            }
            go.transform.rotation = _cam.transform.rotation;
            StartCoroutine(FadeLine(lr, 0.22f));
        }
        IEnumerator FadeLine(LineRenderer lr, float dur)
        {
            Color s = lr.startColor, e = lr.endColor;
            for (float k = 0; k < 1; k += Time.deltaTime / dur)
            {
                lr.startColor = new Color(s.r, s.g, s.b, 1 - k); lr.endColor = new Color(e.r, e.g, e.b, 1 - k);
                yield return null;
            }
            Destroy(lr.gameObject);
        }

        // Floating numbers (art review M3): placed in screen space above the unit's head tag (Core NumberLayout),
        // with a little random spread, stacked upward when several pop at once, and kept on screen for their whole
        // rise. Drawn on top of everything (no depth test) with a dark drop shadow; a WEAK!/RESIST tag is its own
        // smaller line under the number, in its own colour, so it stays readable over any background.
        // anchor: the head-tag point in the world (BattleController.NumberAnchor). Reduce motion: no rise, a fade only.
        sealed class Num { public Transform T; public TextMesh Main, Shadow, Tag, TagShadow; public NumBox Box; public float Depth, Rise, Age, Life, Up; public Color C, TagC; }
        readonly List<Num> _nums = new List<Num>();
        Material _numMat;
        const float NumPx = 34f;           // a normal number's height at Ui.Scale 1 (pixels)

        public void Number(Vector3 anchor, string text, Color color, float size = 1f, string tag = null, Color? tagColor = null)
        {
            if (_cam == null) return;
            var sp = _cam.WorldToScreenPoint(anchor);
            if (sp.z <= 0.1f) return;
            if (_numMat == null)
            {
                _numMat = new Material(_font.material) { name = "NumberOnTop" };
                _numMat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always); // never hidden behind a body
            }
            float h = NumPx * Ui.Scale * size;
            float rise = Access.ReduceMotion ? 0f : (float)Core.NumberLayout.Rise * h;
            float th = string.IsNullOrEmpty(tag) ? 0 : Mathf.Max(h * 0.62f, 20f * Ui.Scale); // the tag line
            float boxH = h + th;
            var live = new List<NumBox>(_nums.Count);
            foreach (var n in _nums) live.Add(new NumBox(n.Box.X, n.Box.Y + n.Up, n.Box.W, n.Box.H)); // where each is now (they all rise at one speed)
            double w = System.Math.Max(Core.NumberLayout.Width(text, h), string.IsNullOrEmpty(tag) ? 0 : Core.NumberLayout.Width(tag, th));
            float top = Screen.height * 0.12f; // the header, turn-order bar and round pill
            var box = Core.NumberLayout.Place(sp.x, sp.y, w, boxH, live, Screen.width, Screen.height, top, rise, Random.Range(-1f, 1f));

            var go = new GameObject("num");
            var num = new Num { T = go.transform, Box = box, Depth = sp.z, Rise = rise, Life = 0.95f, C = color, TagC = tagColor ?? color };
            num.Shadow = Line(go.transform, text, 100); num.Main = Line(go.transform, text, 101);
            if (th > 0) { num.TagShadow = Line(go.transform, tag, 100); num.Tag = Line(go.transform, tag, 101); }
            _nums.Add(num);
            Place(num, h, th);
            StartCoroutine(Float(num, h, th));
        }

        TextMesh Line(Transform parent, string text, int order)
        {
            var go = new GameObject(order == 100 ? "shadow" : "text"); go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font; tm.text = text; tm.fontSize = 64; tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = _numMat; r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return tm;
        }

        // Puts the number's lines at its box (plus the rise so far), sized to stay the same on screen.
        void Place(Num n, float h, float th)
        {
            float worldPerPx = 2f * n.Depth * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            float up = n.Up = n.Rise * Mathf.Clamp01(n.Age / n.Life); // a steady rise: numbers stacked together keep their gaps
            float cx = (float)n.Box.X, cy = (float)n.Box.Y + up, top = cy + (float)n.Box.H / 2;
            var rot = _cam.transform.rotation;
            void Set(TextMesh tm, float px, float x, float y, bool shadow)
            {
                if (tm == null) return;
                float off = shadow ? Mathf.Max(1.5f, px * 0.07f) : 0;
                tm.transform.position = _cam.ScreenToWorldPoint(new Vector3(x + off, y - off, n.Depth));
                tm.transform.rotation = rot;
                tm.characterSize = px * worldPerPx * 10f / tm.fontSize / 0.92f; // TextMesh line height ~ fontSize x characterSize / 10
            }
            Set(n.Shadow, h, cx, top - h / 2, true); Set(n.Main, h, cx, top - h / 2, false);
            Set(n.TagShadow, th, cx, top - h - th / 2, true); Set(n.Tag, th, cx, top - h - th / 2, false);
        }

        IEnumerator Float(Num n, float h, float th)
        {
            for (n.Age = 0; n.Age < n.Life && n.T != null; n.Age += Time.deltaTime)
            {
                float k = n.Age / n.Life, a = k < 0.65f ? 1 : 1 - (k - 0.65f) / 0.35f;
                Place(n, h, th);
                Color C(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
                n.Main.color = C(n.C, a); n.Shadow.color = new Color(0.04f, 0.02f, 0.06f, a * 0.85f);
                if (n.Tag != null) { n.Tag.color = C(n.TagC, a); n.TagShadow.color = new Color(0.04f, 0.02f, 0.06f, a * 0.9f); }
                yield return null;
            }
            _nums.Remove(n);
            if (n.T != null) Destroy(n.T.gameObject);
        }

        // Settings > Screen shake: full | reduced | off.
        public static System.Func<string> ShakeSetting;
        public void Shake(float intensity = 0.12f)
        {
            string s = ShakeSetting?.Invoke() ?? "full";
            if (s == "off") return;
            StartCoroutine(ShakeRoutine(s == "reduced" ? intensity * 0.4f : intensity));
        }
        // Time-based, so it looks the same at 30, 60 and 120 fps: a smooth noise offset sampled at a fixed
        // 28 Hz of shake (not a new random jump every rendered frame) that fades over `dur`. It is an
        // OFFSET (ShakeOffset) the Arena's camera rig adds, so shakes, the breathing tilt and cinematic
        // cameras never fight over the camera transform.
        public static Vector3 ShakeOffset;
        static readonly List<Vector3> _shakes = new List<Vector3>();
        public void ShakeLong(float intensity, float dur)
        {
            string s = ShakeSetting?.Invoke() ?? "full";
            if (s == "off") return;
            StartCoroutine(ShakeRoutine(s == "reduced" ? intensity * 0.4f : intensity, dur));
        }
        IEnumerator ShakeRoutine(float intensity, float dur = 0.22f)
        {
            float seed = Random.value * 100f; int slot = _shakes.Count; _shakes.Add(Vector3.zero);
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur, f = (seed + t) * 28f;
                var n = new Vector3(Mathf.PerlinNoise(f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, f) - 0.5f, Mathf.PerlinNoise(f, f * 0.5f) - 0.5f) * 2f;
                if (slot < _shakes.Count) _shakes[slot] = n * intensity * (1 - k);
                Sum();
                yield return null;
            }
            if (slot < _shakes.Count) _shakes[slot] = Vector3.zero;
            if (_shakes.TrueForAll(v => v == Vector3.zero)) _shakes.Clear();
            Sum();
        }
        static void Sum() { var o = Vector3.zero; foreach (var v in _shakes) o += v; ShakeOffset = o; }
    }
}
