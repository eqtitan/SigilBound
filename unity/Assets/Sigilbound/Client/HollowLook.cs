// The Hollow enemies' look (art review M2, PMO P2-9; ART_DIRECTION 1.5): a clean, continuous red outline and two
// glowing red eyes on a dark violet body, instead of the old scattered pink speckles (a normal-mapped fresnel rim
// and the hero's own glow mask, both recoloured red, on a near-black albedo).
//   - outline: Shaders/SigilHollowOutline.shader, an inverted hull added as an extra material on each renderer
//     (Unity draws the mesh again with it). Constant pixel width by tier (Core BattleFx.OutlineWidth); one unlit
//     pass per enemy, so Low keeps it. HDR red on High/Ultra so bloom catches it.
//   - eyes: two soft additive dots on the face, parented to the head bone so they follow every clip. Steady (no
//     flicker or pulse, so Settings > Flash effects has nothing to dim); their HDR gain stays under the tier's bloom.
// The HD model's own speckle sources are switched off where its material is made (HeroModels.HdMaterial).
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class HollowLook
    {
        static Material _outline, _eyes; static int _outlineFor = -1; static Texture2D _dot;
        static int TierNow => Quality.I != null ? Quality.I.Tier : 2;

        static Color Red(float gain) { var r = BattleFx.HollowRed; return new Color((float)r[0] * gain, (float)r[1] * gain, (float)r[2] * gain, 1); }

        // The shared outline material, re-sized when the tier or the screen height changes.
        public static Material Outline()
        {
            int tier = TierNow, key = tier * 100000 + Screen.height;
            if (_outline == null)
            {
                var sh = Shader.Find("Sigilbound/HollowOutline");
                if (sh == null || !sh.isSupported) return null;   // no outline rather than a magenta hull
                _outline = new Material(sh) { name = "HollowOutline" };
            }
            if (_outlineFor != key)
            {
                _outlineFor = key;
                _outline.SetFloat("_Width", (float)BattleFx.OutlineWidth(tier, Screen.height));
                _outline.SetColor("_OutlineColor", Red((float)BattleFx.OutlineGain(tier)));
            }
            return _outline;
        }

        // Dresses a loaded enemy model: the outline on every mesh, and eyes when it is a high-detail hero (the older
        // models and the stand-ins already have glowing eye materials). headTop: the view's head-top marker.
        public static void Apply(Transform holder, Transform view, bool hd)
        {
            if (holder == null) return;
            var mat = Outline();
            if (mat != null)
                foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;   // not particles, lines or trails
                    var mats = r.sharedMaterials;
                    if (mats.Length == 0 || System.Array.IndexOf(mats, mat) >= 0) continue;
                    var more = new Material[mats.Length + 1];
                    mats.CopyTo(more, 0); more[mats.Length] = mat;
                    r.sharedMaterials = more;
                }
            if (hd) Eyes(holder, view);
        }

        static void Eyes(Transform holder, Transform view)
        {
            var head = BodyShaper.Find(holder, "Head") ?? BodyShaper.Find(holder, "head");
            if (head == null || view == null) return;
            // The head's span: the rig puts its head bone at 1.6 m on a ~1.85 m figure (HeroModels.BuildHd); about a
            // quarter of a metre from the joint to the crown, scaled with the figure.
            float bodyScale = Mathf.Max(0.3f, (head.position.y - view.position.y) / 1.6f);
            float span = 0.24f * bodyScale;
            Vector3 up = Vector3.up, fwd = view.forward, right = Vector3.Cross(up, fwd).normalized;
            var mid = head.position + up * ((float)BattleFx.EyeUp * span) + fwd * ((float)BattleFx.EyeForward * span);
            var root = new GameObject("HollowEyes").transform;
            root.SetParent(head, true);
            root.position = mid;
            var eyes = root.gameObject.AddComponent<FaceCamera>();
            for (int s = -1; s <= 1; s += 2)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>());
                q.name = "Eye"; q.transform.SetParent(root, true);
                q.transform.position = mid + right * (s * (float)BattleFx.EyeApart * span);
                q.transform.localScale = Vector3.one * ((float)BattleFx.EyeSize * span) / Mathf.Max(1e-3f, root.lossyScale.x);
                var r = q.GetComponent<Renderer>(); r.sharedMaterial = EyeMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                eyes.Quads.Add(q.transform);
            }
        }

        static Material EyeMaterial()
        {
            if (_eyes != null) return _eyes;
            var add = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            _eyes = new Material(add) { name = "HollowEyes" };
            if (_dot == null)
            {
                const int n = 32; _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "EyeDot" };
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                    float a = Mathf.Clamp01(1 - d); a = a * a * (0.6f + 0.4f * Mathf.Clamp01(1 - d * 2.2f)); // bright core, soft halo
                    _dot.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                _dot.Apply(false, true);
            }
            _eyes.mainTexture = _dot;
            var c = Red((float)BattleFx.EyeGain(TierNow) * 0.5f); c.g += 0.12f; c.b += 0.08f; // legacy additive doubles; a hot core
            if (_eyes.HasProperty("_TintColor")) _eyes.SetColor("_TintColor", c); else _eyes.color = c;
            return _eyes;
        }

        // Keeps the eye dots turned to the camera.
        sealed class FaceCamera : MonoBehaviour
        {
            public readonly System.Collections.Generic.List<Transform> Quads = new System.Collections.Generic.List<Transform>();
            void LateUpdate()
            {
                var cam = Camera.main; if (cam == null) return;
                var rot = cam.transform.rotation;
                foreach (var q in Quads) if (q != null) q.rotation = rot;
            }
        }
    }
}
