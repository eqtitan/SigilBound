// The 3D battlefield: camera, lights, sky backdrop, ground, the rune circle and chapter scenery.
// Battle positions and camera framing come from Core.BattleLayout (ported from three3d.mjs).
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed class Arena : MonoBehaviour
    {
        public Camera Cam;
        public Light Sun;
        GameObject _scenery;
        string _camKey = BattleLayout.DefaultCamera; bool _framedBattle, _framedTitan; float _framedAspect;

        public static Vector3 ToUnity(V3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        public static Arena Create()
        {
            var a = new GameObject("Arena").AddComponent<Arena>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            a.Cam = camGo.AddComponent<Camera>();
            a.Cam.fieldOfView = 42; a.Cam.nearClipPlane = 0.1f; a.Cam.farClipPlane = 120;
            a.Cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0, 7.2f, -9.6f);
            camGo.transform.LookAt(new Vector3(0, 0.9f, 1.1f));
            var sun = a.Sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.35f; fill.color = new Color(0.6f, 0.7f, 1f);
            fill.transform.rotation = Quaternion.Euler(20, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            a.SetPose(new Vector3(0, 7.2f, -9.6f), new Vector3(0, 0.9f, 1.1f), 42); // the rig's starting pose
            return a;
        }

        // ---------- camera rig ----------
        // The camera's pose = the framed view (_pose*), then the Titan's breathing tilt, then any cinematic
        // override (Titan intro, finishers), then the screen-shake offset (Vfx.ShakeOffset). LateUpdate
        // composes them, so nothing else writes the camera transform.
        Vector3 _posePos; Quaternion _poseRot = Quaternion.identity; float _poseFov = 42;
        float _breath, _ovW; public float BreathTarget;   // 0..1: the Titan winding up (BattleController)
        Vector3 _ovPos; Quaternion _ovRot = Quaternion.identity; float _ovFov = 42;
        public Vector3 BreathFocus;                        // where the breathing tilt looks (the Titan's head)
        public static float MotionScale => (Vfx.ShakeSetting?.Invoke() ?? "full") switch { "off" => 0f, "reduced" => 0.4f, _ => 1f };

        void SetPose(Vector3 pos, Vector3 target, float fov)
        {
            _posePos = pos; _poseRot = Quaternion.LookRotation(target - pos); _poseFov = fov;
            Cam.transform.position = pos; Cam.transform.rotation = _poseRot; Cam.fieldOfView = fov;
        }
        // A cinematic camera, blended in with weight w (0 = the battle view).
        // roll: a tilted (Dutch) angle in degrees, for the Titan's cinematics.
        public void Override(Vector3 pos, Vector3 lookAt, float fov, float w, float roll = 0f)
        {
            var d = lookAt - pos;
            _ovPos = pos; if (d.sqrMagnitude > 1e-4f) _ovRot = Quaternion.LookRotation(d, Quaternion.AngleAxis(roll, d.normalized) * Vector3.up); _ovFov = fov; _ovW = Mathf.Clamp01(w);
        }
        public void ClearOverride() => _ovW = 0;
        public Vector3 PosePosition => _posePos;
        public Quaternion PoseRotation => _poseRot;

        void ComposeCamera()
        {
            // Breathing tilt: eases toward the target in ~0.35 s (time-based, frame-rate independent).
            _breath = Mathf.MoveTowards(_breath, _framedTitan ? Mathf.Clamp01(BreathTarget) : 0, Time.deltaTime / 0.35f);
            float b = Mathf.SmoothStep(0, 1, _breath) * MotionScale;
            var pos = _posePos + Vector3.down * 0.7f * b - (_poseRot * Vector3.forward) * 0.6f * b;
            if (pos.y < 0.4f) pos.y = 0.4f;
            var rot = b > 0 && BreathFocus != Vector3.zero ? Quaternion.Slerp(_poseRot, Quaternion.LookRotation(BreathFocus - pos), 0.3f * b) : _poseRot;
            float fov = _poseFov;
            if (_ovW > 0) { pos = Vector3.Lerp(pos, _ovPos, _ovW); rot = Quaternion.Slerp(rot, _ovRot, _ovW); fov = Mathf.Lerp(fov, _ovFov, _ovW); }
            Cam.transform.position = pos + Vfx.ShakeOffset; Cam.transform.rotation = rot; Cam.fieldOfView = fov;
        }

        // Menu view: the team lineup stands in front of a fixed camera.
        public void FrameMenu()
        {
            _framedBattle = false; _framedTitan = false; _ovW = 0; BreathTarget = 0; _breath = 0;
            SetPose(new Vector3(0, 7.2f, -9.6f), new Vector3(0, 0.9f, 1.1f), 42);
        }

        // Battle view: a camera preset (Three-quarter by default) at the closest distance that fits
        // the whole rune circle and the HUD tags on this screen's aspect ratio.
        public void FrameBattle(string camKey, bool titan)
        {
            _camKey = camKey; _framedBattle = true; _framedTitan = titan;
            _framedAspect = Cam.aspect;
            double aspect = Mathf.Max(0.2f, Cam.aspect);
            var v = BattleLayout.View(camKey, aspect, titan); // the usual presets, or a Titan-only view
            SetPose(ToUnity(v.Eye), ToUnity(v.Target), (float)v.Fov);
            _viewTarget = ToUnity(v.Target);
            HideOccluders();
        }

        // Scenery that would block the battle from this camera (low presets such as Cinematic can sit
        // inside a rock or behind a tree): hide every prop the camera is inside or near, or that cuts a
        // sight line from the camera to the battle circle (its centre, its left/right/front edges and
        // head height). Restored when the camera changes. Ground, sky, water and the rune circle stay.
        readonly List<Renderer> _hidden = new List<Renderer>(); Vector3 _viewTarget;
        void HideOccluders()
        {
            foreach (var r in _hidden) if (r) r.enabled = true;
            _hidden.Clear();
            if (!_scenery || !_framedBattle) return;
            Vector3 eye = Cam.transform.position;
            var c = _viewTarget;
            Vector3 right = Vector3.Cross(Vector3.up, (c - eye).normalized).normalized;
            // Sight lines to the whole battle ring (where every hero stands, plus a margin for the
            // name tags above them): centre, two rings of points, at foot, chest and tag height.
            var targets = new List<Vector3> { c };
            var ctr = ToUnity(BattleLayout.Centre);
            foreach (float k in new[] { 0.55f, 1.12f })
                for (int a = 0; a < 16; a++)
                {
                    float ang = a * Mathf.PI / 8f;
                    var p = ctr + new Vector3(Mathf.Cos(ang) * (float)BattleLayout.RimRx * k, 0, Mathf.Sin(ang) * (float)BattleLayout.RimRz * k);
                    foreach (float h in new[] { 0.3f, 1.2f, 2.4f }) targets.Add(p + Vector3.up * h);
                }
            HideBetween(new[] { eye }, targets, null);
        }

        // Hides scenery that sits within 1.2 m of an eye or cuts a sight line from any eye to any target
        // (ground, sky, water, the ring and far backdrop stay). keep: the subject's base (the lair landmark):
        // renderers centred within 3.5 m of it (the landmark, its bell, lamp or wheel) are never hidden.
        void HideBetween(Vector3[] eyes, List<Vector3> targets, Vector3? keep)
        {
            foreach (var r in _scenery.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                string n = r.gameObject.name;
                if (n == "Ground" || n.StartsWith("Sky") || n.StartsWith("Water") || n.StartsWith("Circle") || n.StartsWith("Rune") || n.StartsWith("Shaft")) continue;
                var b = r.bounds;
                if (b.extents.magnitude > 18f) continue; // sky domes, backdrop hills far away
                if (b.max.y < 0.15f) continue;            // flat ground decals never block
                if (keep.HasValue && new Vector2(b.center.x - keep.Value.x, b.center.z - keep.Value.z).sqrMagnitude < 3.5f * 3.5f) continue; // the landmark and its parts
                bool block = false;
                foreach (var eye in eyes)
                {
                    if (b.SqrDistance(eye) < 1.2f * 1.2f) { block = true; break; }
                    var bb = b; bb.Expand(0.25f);
                    foreach (var t in targets)
                    {
                        var d = t - eye; float len = d.magnitude;
                        if (bb.IntersectRay(new Ray(eye, d / len), out float hit) && hit < len - 0.45f) { block = true; break; }
                    }
                    if (block) break;
                }
                if (block) { r.enabled = false; _hidden.Add(r); }
            }
        }

        // The lair reveal's low camera path: hide scatter between its eyes and the landmark (base, middle, top).
        // FrameBattle (called when the reveal ends) puts them back and hides the battle view's own occluders.
        public void ClearSightForReveal(Vector3[] eyes, Vector3 landmark, float top)
        {
            foreach (var r in _hidden) if (r) r.enabled = true;
            _hidden.Clear();
            if (!_scenery) return;
            var targets = new List<Vector3>();
            foreach (float k in new[] { 0.1f, 0.5f, 0.9f })
                foreach (float dx in new[] { -1.5f, 0f, 1.5f }) targets.Add(landmark + new Vector3(dx, top * k, 0));
            HideBetween(eyes, targets, landmark + Vector3.up * 0.5f);
        }

        void LateUpdate()
        {
            // Light shafts: Ultra only, slowly breathing.
            if (_shafts != null)
            {
                bool on = Quality.I != null && Quality.I.Tier == 3;
                if (_shafts.activeSelf != on) _shafts.SetActive(on);
                if (on) for (int i = 0; i < _shaftMats.Count; i++) { var c = _shaftMats[i].color; c.a = 0.07f + 0.035f * Mathf.Sin(Time.time * (0.4f + i * 0.13f) + i * 1.7f); _shaftMats[i].color = c; }
            }
            // Re-fit when the screen changes shape (rotation, split screen, foldables).
            if (_framedBattle && Mathf.Abs(Cam.aspect - _framedAspect) > 0.01f) FrameBattle(_camKey, _framedTitan);
            ComposeCamera();
        }

        public void Show(bool on)
        {
            Cam.gameObject.SetActive(true);
            if (_scenery) _scenery.SetActive(on);
        }

        // interactive: a campaign boss stage or the Titan; the backdrop answers the fight (ArenaReactor).
        // stage: the stage within the chapter (0-5) for a campaign battle, -1 otherwise: picks the route stop
        // (Core.StoryScenes) and its time of day. hard: Hard mode, the same stops by night.
        public void Build(int chapter, bool titan, bool interactive = false, int stage = -1, bool hard = false)
        {
            var set = titan || stage < 0 ? null : StoryScenes.Of(chapter, stage);
            var mood = titan ? Mood.Day : hard ? Mood.Night : set?.Mood ?? Mood.Day;
            var (moodLight, dusk, fogK) = StoryScenes.MoodOf(mood);
            bool night = mood == Mood.Night;
            PostFx.SetArena(chapter, titan, true, night); // per-arena colour grade (postfx2 presets), cooler by night
            if (_scenery) Destroy(_scenery);
            _scenery = new GameObject("Scenery");
            var (top, bottom, ground, prop) = titan ? (Palette.Hex("#1a0a0a"), Palette.Hex("#c0401a"), Palette.Hex("#24100c"), Palette.Hex("#100606")) : Palette.Scene(chapter);
            if (!titan && dusk > 0)
            {
                // Later stops drift toward dusk inside the chapter palette: the horizon toward the theme's shadow colour.
                var sil = Scenery.ShadowColor(chapter, false);
                bottom = Color.Lerp(bottom, sil * 1.6f, dusk);
                top = Color.Lerp(top, top * 0.55f, night ? 0.7f : dusk);
            }
            Cam.backgroundColor = top;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = bottom;
            // Fog per theme: thicker over marsh, fen, barrow and storm, clearer over the badlands (the battle camera sits ~19 m out).
            var (fogNear, fogFar) = Scenery.FogRange(chapter, titan);
            RenderSettings.fogStartDistance = fogNear * fogK; RenderSettings.fogEndDistance = fogFar * fogK;
            bool lair = set != null && stage == GameData.StagesPerChapter - 1;
            // Night lairs keep more ambient (the throne and crag must read in the dark); other night stops dim it.
            RenderSettings.ambientLight = Color.Lerp(bottom, Color.white, 0.35f) * 0.55f * (night ? (lair ? 1.0f : 0.85f) : 1f - dusk * 0.25f);
            // The sun takes a little of the horizon's colour (warm dusks, cold storms); kept close to white so the
            // heroes' painted look holds. Storms are dimmer.
            if (Sun != null)
            {
                Sun.color = Color.Lerp(Color.white, Color.Lerp(bottom, Color.white, 0.4f), 0.35f);
                Sun.intensity = (Scenery.Stormy(chapter, titan) && !titan ? 0.9f : 1.15f) * (night && lair ? Mathf.Max(moodLight, 0.78f) : moodLight);
                if (night) Sun.color = Color.Lerp(Sun.color, new Color(0.7f, 0.8f, 1f), 0.5f); // moonlight
            }
            // The sky (painted clouds) and the distant ridges are painted arcs built with the scenery.

            // The rune circle: units stand on its rim (BattleLayout.Rim*).
            var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder); circle.name = "Circle";
            Destroy(circle.GetComponent<Collider>());
            circle.transform.SetParent(_scenery.transform);
            circle.transform.position = new Vector3((float)BattleLayout.RimCx, 0.005f, (float)BattleLayout.RimCz);
            circle.transform.localScale = new Vector3((float)BattleLayout.RimRx * 2, 0.002f, (float)BattleLayout.RimRz * 2);
            circle.GetComponent<Renderer>().sharedMaterial = CharacterView.Unlit(new Color(1, 1, 1, 0.05f));
            RuneRing(1.0f, 0.06f, new Color(1f, 0.81f, 0.42f, 0.55f));
            RuneRing(0.82f, 0.03f, new Color(1f, 0.85f, 0.5f, 0.3f));

            // Ground, water or lava, scatter, horizon silhouettes and motes (scenery.mjs themes).
            // Scatter density by tier; Ultra stays at High's count (budget pass after build 138: ~25k scenery triangles).
            float density = Quality.I == null ? 1f : new[] { 0.4f, 0.7f, 1f, 1f }[Quality.I.Tier];
            Scenery.Build(_scenery.transform, chapter, titan, density, bottom, top, interactive, Sun, set, hard);
            if (lair && night) LairRim(set, Scenery.ShadowColor(chapter, false));
            StaticBatch(_scenery);
            HideOccluders(); // a re-built arena under an already framed camera
            Shafts(titan ? Palette.Hex("#ff9a5a") : Color.Lerp(bottom, new Color(1f, 0.95f, 0.8f), 0.7f));
        }

        // A night lair's rim light: a cool moonlit point light in front of and above the landmark, so its
        // silhouette and upper faces read against the dark sky (owner check, build 138). One light, every tier.
        void LairRim(StageSet set, Color sil)
        {
            Piece main = null;
            foreach (var p in set.Pieces) if (!p.Far && (main == null || p.R * p.Scale > main.R * main.Scale)) main = p;
            if (main == null) return;
            var l = new GameObject("LairRim").AddComponent<Light>();
            l.transform.SetParent(_scenery.transform, false);
            l.transform.position = new Vector3(main.X * 0.8f, 5.5f * main.Scale + 1.5f, main.Z - main.R - 2.5f);
            l.type = LightType.Point; l.range = 18f; l.intensity = 1.8f; l.shadows = LightShadows.None;
            l.color = Color.Lerp(Color.Lerp(sil, Color.white, 0.5f), new Color(0.72f, 0.82f, 1f), 0.6f);
        }

        // Ultra: a few soft light shafts slanting down from the sun's side onto the arena
        // (alpha-blended gradient quads; no extra passes). Toggled with the tier in LateUpdate.
        GameObject _shafts; readonly List<Material> _shaftMats = new List<Material>();
        static Texture2D _shaftTex;
        void Shafts(Color tint)
        {
            _shaftMats.Clear();
            _shafts = new GameObject("LightShafts");
            _shafts.transform.SetParent(_scenery.transform, false);
            if (_shaftTex == null)
            {
                _shaftTex = new Texture2D(32, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Shaft" };
                for (int y = 0; y < 64; y++) for (int x = 0; x < 32; x++)
                {
                    float u = x / 31f * 2 - 1, v = y / 63f; // v = 0 on the ground, 1 towards the sun
                    float a = Mathf.Pow(1 - Mathf.Abs(u), 2.2f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(v / 0.6f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - v) / 0.2f));
                    _shaftTex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                _shaftTex.Apply();
            }
            var sunDir = Sun != null ? Sun.transform.forward : new Vector3(0.4f, -0.7f, 0.5f).normalized;
            var rng = new System.Random(7);
            for (int i = 0; i < 4; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                q.name = "Shaft"; q.transform.SetParent(_shafts.transform, false);
                float x = -4.5f + i * 3f + (float)rng.NextDouble(), z = 2.5f + (float)rng.NextDouble() * 4f, len = 14f;
                // Lay the quad along the light direction, centred above its landing spot, turned to the camera.
                var foot = new Vector3(x, 0, z);
                q.transform.position = foot - sunDir * (len * 0.5f);
                var toCam = Cam.transform.position - q.transform.position; toCam -= Vector3.Dot(toCam, sunDir) * sunDir;
                q.transform.rotation = Quaternion.LookRotation(-toCam.normalized, -sunDir);
                q.transform.localScale = new Vector3(1.4f + (float)rng.NextDouble() * 1.4f, len, 1);
                var m = Materials.Unlit(new Color(tint.r, tint.g, tint.b, 0.08f)); m.mainTexture = _shaftTex;
                _shaftMats.Add(m);
                var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            _shafts.SetActive(Quality.I != null && Quality.I.Tier == 3);
        }

        // Static batching: everything in the arena that never moves (ground, circle, scatter) is
        // combined per material; animated water/lava, boss-stage props that sway or flare, the
        // backdrop arcs (drawn in a fixed far-to-near order) and particles are left out.
        static void StaticBatch(GameObject root)
        {
            var still = new List<GameObject>();
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable || mr.GetComponent<SceneryWave>() != null) continue;
                if (mr.GetComponentInParent<SceneryMoves>() != null || mr.gameObject.name.StartsWith("Sky")) continue; // swaying props; the sorted backdrop
                still.Add(mr.gameObject);
            }
            if (still.Count > 1) StaticBatchingUtility.Combine(still.ToArray(), root);
        }

        // A glowing ellipse on the ground at `k` times the rim size.
        void RuneRing(float k, float width, Color c)
        {
            var go = new GameObject("RuneRing");
            go.transform.SetParent(_scenery.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            const int n = 96;
            lr.positionCount = n; lr.loop = true; lr.useWorldSpace = false;
            lr.widthMultiplier = width; lr.sharedMaterial = CharacterView.Unlit(c);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                lr.SetPosition(i, new Vector3((float)BattleLayout.RimCx + Mathf.Sin(a) * (float)BattleLayout.RimRx * k, 0.02f, (float)BattleLayout.RimCz + Mathf.Cos(a) * (float)BattleLayout.RimRz * k));
            }
        }

        void Prop(PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(t);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_scenery.transform);
            go.transform.position = pos; go.transform.localScale = scale;
            if (euler.HasValue) go.transform.eulerAngles = euler.Value;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        // Battle positions: on the rune circle's rim, allies along the near arc and enemies along
        // the far arc, 2.2 m apart; everyone idles facing the centre.
        public static Vector3 Slot(bool ally, int index, int count) => ToUnity(BattleLayout.Slot(ally, index, count));
        public static float FacingYaw(Vector3 p) => (float)BattleLayout.FacingCentreYaw(new V3(p.x, p.y, p.z));
    }
}
