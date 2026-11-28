// A soft stand-in where a menu hero is still loading (in-house report S4, after the T-pose fix: Home showed
// empty spots until each model was revealed). Draws the hero's portrait card, or a hero silhouette when there
// is no portrait, faded, with a slow light sweep, at the hero's spot on screen, until HeroModels has revealed
// the model (CharacterView.AwaitingModel). The block stand-in takes over if a load is slow or fails.
using System.Collections.Generic;
using UnityEngine;

namespace Sigilbound.Client
{
    public static class LoadingPlaceholder
    {
        public const float SweepSeconds = 1.6f;   // one pass of the light band
        public const float Alpha = 0.72f;         // soft: it reads as "coming", not as the hero
        public const float HeroMetres = 2.3f;     // the lineup's model height (HeroModels' hero scale)

        static Texture2D _silhouette, _band;

        // Every lineup view still waiting for its model. cam: the menu camera.
        public static void DrawLineup(Camera cam, IList<CharacterView> views)
        {
            if (cam == null || views == null || Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (v == null || !v.AwaitingModel || v.Unit?.Template == null) continue;
                float s = v.Model != null ? v.Model.lossyScale.y : 1f;
                Vector3 feet = cam.WorldToScreenPoint(v.transform.position), head = cam.WorldToScreenPoint(v.transform.position + Vector3.up * HeroMetres * s);
                if (feet.z < 0 || head.z < 0) continue;
                float yTop = (Screen.height - head.y) / Ui.Scale, yBot = (Screen.height - feet.y) / Ui.Scale;
                float h = Mathf.Max(40, yBot - yTop), w = h * 0.62f;
                float cx = feet.x / Ui.Scale - Ui.Offset;
                Draw(new Rect(cx - w / 2, yTop, w, h), v.Unit.Template.Id, Palette.Element(v.Unit.El), i * 0.23f);
            }
        }

        // One placeholder: the portrait (3:4, framed in the element colour) or the silhouette, plus the sweep.
        public static void Draw(Rect r, string heroId, Color element, float phase = 0)
        {
            var keep = GUI.color;
            GUI.color = new Color(1, 1, 1, Alpha);
            var tex = Portraits.Get(heroId);
            if (tex != null)
            {
                float ch = Mathf.Min(r.height, r.width * 4f / 3f), cw = ch * 0.75f;
                var card = new Rect(r.center.x - cw / 2, r.yMax - ch, cw, ch);
                Portraits.Card(card, heroId, element * new Color(1, 1, 1, 0.8f));
                r = card;
            }
            else
            {
                GUI.color = new Color(element.r * 0.35f + 0.2f, element.g * 0.35f + 0.2f, element.b * 0.35f + 0.22f, Alpha);
                GUI.DrawTexture(r, Silhouette, ScaleMode.ScaleToFit);
            }
            // The sweep: a soft light band crossing the card, clipped to it.
            float t = Mathf.Repeat(Time.unscaledTime / SweepSeconds + phase, 1f);
            GUI.BeginGroup(r);
            float bw = r.width * 0.55f;
            GUI.color = new Color(1, 1, 1, 0.22f);
            GUI.DrawTexture(new Rect(-bw + (r.width + bw) * t, 0, bw, r.height), Band);
            GUI.EndGroup();
            GUI.color = keep;
        }

        // A standing hero: head, shoulders, tapering body, legs; soft edges (built once, 64 x 128).
        static Texture2D Silhouette
        {
            get
            {
                if (_silhouette != null) return _silhouette;
                const int W = 64, H = 128;
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f) / W - 0.5f, v = 1 - (y + 0.5f) / H; // v: 0 feet .. 1 top
                        float head = 1 - Mathf.Clamp01((Mathf.Sqrt(u * u + (v - 0.87f) * (v - 0.87f) / 0.9f) - 0.1f) / 0.02f);
                        float halfW = v > 0.76f ? 0 : v > 0.68f ? 0.30f : v > 0.42f ? Mathf.Lerp(0.2f, 0.3f, (v - 0.42f) / 0.26f) : v > 0.02f ? 0.17f : 0;
                        float body = 1 - Mathf.Clamp01((Mathf.Abs(u) - halfW) / 0.02f);
                        if (v < 0.42f && Mathf.Abs(u) < 0.025f) body = 0; // the gap between the legs
                        float a = Mathf.Max(head, body);
                        px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _silhouette = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "loading_silhouette" };
                _silhouette.SetPixels32(px); _silhouette.Apply(false, true);
                return _silhouette;
            }
        }

        // A horizontal light band, transparent at both ends (16 x 1).
        static Texture2D Band
        {
            get
            {
                if (_band != null) return _band;
                const int W = 16;
                var px = new Color32[W];
                for (int x = 0; x < W; x++) { float k = Mathf.Sin((x + 0.5f) / W * Mathf.PI); px[x] = new Color32(255, 255, 255, (byte)(k * k * 255)); }
                _band = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "loading_band" };
                _band.SetPixels32(px); _band.Apply(false, true);
                return _band;
            }
        }
    }
}
