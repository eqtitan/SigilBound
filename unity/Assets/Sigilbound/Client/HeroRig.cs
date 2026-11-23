// Runtime side of the hero art pipeline. The import tool (Editor/Art/HeroImportPipeline) puts this
// component on the root of every generated hero prefab (Resources/Heroes/<heroId>), already filled
// in: the animation backend, the battle-state -> clip/state table, the sockets and the gear
// variants. CharacterView asks it to set the prefab up; if anything is missing it says no and the
// game keeps today's GLB hero (and placeholder) path, so a half-finished import never breaks a battle.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    [DisallowMultipleComponent]
    public sealed class HeroRig : MonoBehaviour
    {
        [Tooltip("Hero id from the game data (public/game/data.js), e.g. ysolde.")]
        public string HeroId;
        [Tooltip("Model height in metres as imported (renderer bounds); the runtime rescales to the hero's battle height.")]
        public float NativeHeight = 2f;
        [Tooltip("Humanoid or generic Animator built by the import tool (null when legacy clips are used).")]
        public Animator Animator;
        [Tooltip("Legacy Animation with the file's own clips (null when the Animator is used).")]
        public Animation Legacy;
        // Battle state (Core.HeroClips) -> Animator state name or legacy clip name, and clip length.
        public string[] StateKeys = new string[0];
        public string[] StateNames = new string[0];
        public float[] StateLengths = new float[0];
        public Transform SocketHandR, SocketHandL, SocketBack, SocketHead, SocketHip, SocketCast;
        [Tooltip("Optional rarity variants under the sockets, named Gear_<Slot>_r<1-5> (shown up to the equipped rarity).")]
        public GameObject[] GearVariants = new GameObject[0];
        public Renderer[] Renderers = new Renderer[0];
        [TextArea] public string ImportSummary;

        public static bool Enabled = true; // a switch for side-by-side comparisons

        public IDictionary<string, string> States()
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i < Math.Min(StateKeys.Length, StateNames.Length); i++) if (!string.IsNullOrEmpty(StateNames[i])) d[StateKeys[i]] = StateNames[i];
            if (d.TryGetValue("Dodge", out var dodge)) d["Roll"] = dodge; // HeroModel.Roll() plays "Roll"
            return d;
        }

        bool Playable()
        {
            var s = States();
            bool clipsOk = HeroClips.All.Where(c => c.Required).All(c => s.ContainsKey(c.Name));
            return clipsOk && ((Animator != null && Animator.runtimeAnimatorController != null) || (Legacy != null && Legacy.GetClipCount() > 0));
        }

        // Scales and colours the prefab for this unit and wraps it in a HeroModel the battle
        // choreography drives. Returns null (caller falls back) if the prefab can't animate.
        public HeroModel Setup(Unit unit, GearLook gear)
        {
            if (!Enabled || !Playable()) return null;
            float target = (float)ArtSpec.TargetHeight(HeroId ?? unit.Template.Id);
            transform.localScale = Vector3.one * (target / Mathf.Max(0.05f, NativeHeight));
            bool hollow = !unit.Ally && !unit.Rival && !unit.Titan;
            ApplyLook(unit.Template.Id, unit.El, hollow);
            ApplyGear(gear);
            foreach (var r in Renderers) if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            var style = AnimStyle.Revamp();
            // Height drives the HUD tag anchor (0.87 x Height), matching the GLB heroes' 2.3 m x build.
            float tagHeight = target * 1.12f;
            if (Animator != null)
            {
                var lengths = new Dictionary<string, float>();
                for (int i = 0; i < Math.Min(StateKeys.Length, StateLengths.Length); i++) lengths[StateKeys[i]] = StateLengths[i];
                if (lengths.TryGetValue("Dodge", out var dl)) lengths["Roll"] = dl;
                Animator.applyRootMotion = false;
                return new HeroModel(gameObject, Animator, States(), lengths, tagHeight, style);
            }
            return new HeroModel(gameObject, Legacy, tagHeight, style, States());
        }

        // Element rim and glow (the hero's own glow colour, or red for Hollow enemies) and the Hollow
        // tint, on per-variant material copies so the SRP batcher still batches them.
        static readonly Dictionary<(int, bool, Element), Material> _variants = new Dictionary<(int, bool, Element), Material>();
        void ApplyLook(string heroId, Element el, bool hollow)
        {
            var look = Looks.Of(heroId);
            Color rim = hollow ? Palette.Hex("#ff3355") : Palette.Element(el);
            Color glow = hollow ? Palette.Hex("#ff2a40") : look.Glow != null ? Palette.Hex(look.Glow) : Palette.Element(el);
            foreach (var r in Renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var key = (m.GetInstanceID(), hollow, el);
                    if (!_variants.TryGetValue(key, out var v) || v == null)
                    {
                        v = new Material(m) { name = m.name + (hollow ? "_Hollow" : "_" + el) };
                        if (v.HasProperty("_RimColor")) v.SetColor("_RimColor", rim);
                        if (v.HasProperty("_GlowColor")) v.SetColor("_GlowColor", glow * (hollow ? 2.2f : 1.6f));
                        if (hollow)
                        {
                            var tint = Color.Lerp(Color.white, Palette.Hex("#3a2a44"), 0.55f);
                            if (v.HasProperty("_Tint")) v.SetColor("_Tint", tint);
                            else if (v.HasProperty("_BaseColor")) v.SetColor("_BaseColor", v.GetColor("_BaseColor") * tint);
                        }
                        _variants[key] = v;
                    }
                    mats[i] = v;
                }
                r.sharedMaterials = mats;
            }
        }

        // Equipped gear: the signature weapon grows with rarity (as the code-built kit does) and any
        // rarity variants the art provides (Gear_<Slot>_r<N>) show up to the equipped rarity.
        void ApplyGear(GearLook gear)
        {
            int Rar(GearLook.Item it) => it == null ? 0 : Mathf.Clamp(it.R, 0, 5);
            int weapon = gear == null ? 0 : Rar(gear.Weapon);
            if (SocketHandR != null) SocketHandR.localScale = Vector3.one * (float)ArtBudget.WeaponScale[weapon];
            var bySlot = new Dictionary<string, int>
            {
                ["Weapon"] = weapon, ["Offhand"] = weapon,
                ["Helm"] = gear == null ? 0 : Rar(gear.Helm), ["Chest"] = gear == null ? 0 : Rar(gear.Chest), ["Back"] = gear == null ? 0 : Rar(gear.Chest), ["Boots"] = gear == null ? 0 : Rar(gear.Boots),
            };
            // Per slot, show the best variant at or below the equipped rarity.
            var best = new Dictionary<string, (int r, GameObject go)>();
            foreach (var g in GearVariants)
            {
                if (g == null) continue;
                var parts = g.name.Split('_'); // Gear_<Slot>_r<N>
                if (parts.Length < 3 || !int.TryParse(parts[2].TrimStart('r'), out var r)) continue;
                g.SetActive(false);
                if (!bySlot.TryGetValue(parts[1], out var want) || r > want) continue;
                if (!best.TryGetValue(parts[1], out var cur) || r > cur.r) best[parts[1]] = (r, g);
            }
            foreach (var b in best.Values) b.go.SetActive(true);
        }
    }
}
