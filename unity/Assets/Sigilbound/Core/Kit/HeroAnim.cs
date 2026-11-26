// Animation choices per hero (three3d.mjs styleOf / TITAN_STYLE) and the retargeted CC0 UAL
// clips (models/anims_ual.json), plus the three.js -> Unity conversion glTFast uses.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilbound.Core
{
    public sealed class AnimStyle
    {
        public string Idle; public string[] Strike; public string Cast, Support, Ult; public string[] Hit, BigHit, Death, Fidget; public string Win; public bool Titan;
        // Phase G (high-detail heroes): guard, defeat pose, victory hold, per-hero idle speed (desync).
        public string Block, Defeat; public float WinSpeed = 1f, DefeatSpeed = 1f, IdleSpeed = 1f, HitScale = 1f; public bool WinHold;

        static readonly HashSet<string> Brawlers = new HashSet<string> { "odo", "kettle", "gorrum", "thrain" };
        static readonly HashSet<string> SmallFocus = new HashSet<string> { "candle", "mirror", "bell", "orb", "lantern" };

        // High-detail heroes (phase F): the free CC0 UAL clips retargeted onto the 65-joint rig
        // (models/ubc_anims.glb). Clip choice per weapon family, as docs/sigilbound/MECHANICS.md section 3.
        // The free UAL clips retargeted onto the high-detail rig (models/ubc_anims.glb, MECHANICS.md 3.0).
        public static readonly string[] HdClips = "Crouch_Fwd_Loop Crouch_Idle_Loop Dance_Loop Death01 Driving_Loop Fixing_Kneeling Hit_Chest Hit_Head Idle_Loop Idle_Talking_Loop Idle_Torch_Loop Interact Jog_Fwd_Loop Jump_Land Jump_Loop Jump_Start PickUp_Table Pistol_Aim_Down Pistol_Aim_Neutral Pistol_Aim_Up Pistol_Idle_Loop Pistol_Reload Pistol_Shoot Punch_Cross Punch_Enter Punch_Jab Push_Loop Roll Roll_RM Sitting_Enter Sitting_Exit Sitting_Idle_Loop Sitting_Talking_Loop Spell_Simple_Enter Spell_Simple_Exit Spell_Simple_Idle_Loop Spell_Simple_Shoot Sprint_Loop Swim_Fwd_Loop Swim_Idle_Loop Sword_Attack Sword_Attack_RM Sword_Idle Walk_Formal_Loop Walk_Loop Defeat_Slump Spear_Thrust Hit_Kneel".Split(' ');

        public static readonly Dictionary<string, string> HdAliases = new Dictionary<string, string>
        {
            ["Run"] = "Jog_Fwd_Loop", ["Run_Back"] = "Jog_Fwd_Loop", ["HitRecieve"] = "Hit_Chest", ["HitRecieve_2"] = "Hit_Head",
            ["Death"] = "Death01", ["Wave"] = "Interact", ["Idle"] = "Idle_Loop",
        };
        public static bool HdGrips(string heroId)
        {
            string p = Looks.Of(heroId).Prop;
            return !SmallFocus.Contains(p) && !Brawlers.Contains(heroId);
        }
        // Per weapon family (MECHANICS.md 3.2, free clips only) and per role (section 5).
        public static AnimStyle Hd(string heroId)
        {
            var fam = Impact.FamilyOf(heroId);
            var role = GameData.Heroes.FirstOrDefault(h => h.Id == heroId)?.Role ?? Role.Attack;
            var st = new AnimStyle
            {
                Cast = "Spell_Simple_Shoot", Support = "Spell_Simple_Enter", Ult = "Spell_Simple_Shoot",
                // Heavy hits drop the hero to one knee, then they rise (owner review of test T8).
                Hit = new[] { "Hit_Chest" }, BigHit = new[] { "Hit_Kneel", "Hit_Head" }, Death = new[] { "Death01" },
                Fidget = new[] { "Idle_Loop", "Idle_Talking_Loop" },
            };
            switch (fam)
            {
                case WeaponFamily.Daggers: st.Idle = "Crouch_Idle_Loop"; st.Strike = new[] { "Punch_Jab", "Punch_Cross", "Sword_Attack" }; break;
                case WeaponFamily.Bow: st.Idle = "Pistol_Idle_Loop"; st.Strike = new[] { "Pistol_Shoot" }; st.Cast = "Pistol_Shoot"; break;
                case WeaponFamily.Caster: st.Idle = "Spell_Simple_Idle_Loop"; st.Strike = new[] { "Spell_Simple_Shoot" }; break;
                case WeaponFamily.Support: st.Idle = "Idle_Loop"; st.Strike = new[] { "Spell_Simple_Shoot" }; break;
                case WeaponFamily.Shield: st.Idle = "Sword_Idle"; st.Strike = new[] { "Punch_Cross", "Sword_Attack" }; st.Block = "Punch_Enter"; break;
                case WeaponFamily.Unarmed: st.Idle = "Idle_Loop"; st.Strike = new[] { "Punch_Jab", "Punch_Cross" }; break;
                case WeaponFamily.Polearm: st.Idle = "Sword_Idle"; st.Strike = new[] { "Spear_Thrust" }; break; // owner review of test T3: a real thrust
                default: st.Idle = "Sword_Idle"; st.Strike = new[] { "Sword_Attack" }; break; // blade, heavy
            }
            switch (role)
            {
                case Role.Attack: st.Win = "Sword_Attack"; st.WinSpeed = 0.5f; st.WinHold = true; st.Defeat = "Crouch_Idle_Loop"; st.DefeatSpeed = 0.6f; break;
                case Role.Defense: st.Win = "Interact"; st.Block ??= "Punch_Enter"; st.Defeat = "Idle_Loop"; st.DefeatSpeed = 0.5f; break;
                case Role.Hp: st.Win = "Idle_Talking_Loop"; st.WinSpeed = 0.8f; st.Defeat = "Sitting_Enter"; st.HitScale = 0.8f; break;
                default: st.Win = "Spell_Simple_Enter"; st.WinHold = true; st.Defeat = "Crouch_Idle_Loop"; st.DefeatSpeed = 0.6f; break;
            }
            // Defeat (owner request; PvP/Arena losses only): fall to the knees and slump forward onto the stomach, then hold
            // (Defeat_Slump, hand-authored by blenderforge/make_clips.py).
            st.Defeat = "Defeat_Slump"; st.DefeatSpeed = 1f;
            if (fam == WeaponFamily.Caster || fam == WeaponFamily.Support) st.Win = role == Role.Support ? "Spell_Simple_Enter" : st.Win;
            // Desync: breathing speed varies +/-8% by hero id, so a lineup never breathes in sync.
            int hsh = 17; foreach (char ch in heroId) hsh = hsh * 31 + ch;
            st.IdleSpeed = 0.92f + (System.Math.Abs(hsh) % 1000) / 1000f * 0.16f;
            return st;
        }

        public static AnimStyle For(string heroId)
        {
            string p = Looks.Of(heroId).Prop;
            return new AnimStyle
            {
                Idle = SmallFocus.Contains(p) ? "Idle_Neutral" : p == "bow" ? "Idle_Gun" : "Idle_Sword",
                Strike = Brawlers.Contains(heroId) ? new[] { "Punch_Right", "Punch_Left", "Kick_Right" } : p == "daggers" ? new[] { "Sword_Slash", "Punch_Left", "Kick_Left" }
                    : p == "spear" || p == "trident" ? new[] { "Sword_Slash", "Kick_Right" } : new[] { "Sword_Slash", "Sword_Slash", "Kick_Right" },
                Cast = p == "bow" ? "Gun_Shoot" : "Spell_Simple_Shoot", Support = "Spell_Simple_Enter",
                Hit = new[] { "Hit_Chest", "HitRecieve" }, BigHit = new[] { "Hit_Head", "HitRecieve_2" }, Death = new[] { "Death01", "Death" },
                Fidget = new[] { "Idle", "Idle_Neutral", "Interact" }, Win = "Wave",
            };
        }
        // Imported hero prefabs (docs/sigilbound/art): the canonical battle states of Core.HeroClips.
        // The import tool maps every state to a clip or its fallback, so these names always exist
        // when the clip (or its fallback) does. "Roll" is the dodge, kept for HeroModel.Roll().
        public static AnimStyle Revamp() => new AnimStyle
        {
            Idle = "Idle", Strike = new[] { "Attack_1", "Attack_2", "Attack_3" }, Cast = "Cast", Ult = "Cast_Ult", Support = "Cast_Support",
            Hit = new[] { "Hit" }, BigHit = new[] { "Hit_Big", "Hit" }, Death = new[] { "Death" }, Fidget = new[] { "Idle_Fidget" }, Win = "Victory",
        };
        // The high-detail Titan (owner-approved 2026-11-26): the shared UAL clips, played heavy.
        public static readonly AnimStyle TitanHd = new AnimStyle
        {
            Idle = "Idle_Loop", Strike = new[] { "Punch_Cross", "Sword_Attack", "Punch_Jab" }, Cast = "Punch_Cross", Support = "Interact",
            Hit = new[] { "Hit_Chest" }, BigHit = new[] { "Hit_Head" }, Death = new[] { "Death01" }, Fidget = new[] { "Idle_Talking_Loop" }, Win = "Interact", Titan = true,
        };
        public static readonly Dictionary<string, string> TitanHdAliases = new Dictionary<string, string>
        {
            ["Jump"] = "Jump_Start", ["HitReact"] = "Hit_Chest", ["Yes"] = "Interact", ["No"] = "Idle_Talking_Loop", ["Wave"] = "Interact", ["Idle"] = "Idle_Loop",
        };

        public static readonly AnimStyle TitanStyle = new AnimStyle
        {
            Idle = "Idle", Strike = new[] { "Punch", "Weapon", "Punch" }, Cast = "Punch", Support = "Yes", Hit = new[] { "HitReact" }, BigHit = new[] { "HitReact" },
            Death = new[] { "Death" }, Fidget = new[] { "No" }, Win = "Yes", Titan = true,
        };
    }

    public sealed class UalClip
    {
        public string Name; public double Fps; public int Frames;
        public readonly Dictionary<string, float[]> Rotations = new Dictionary<string, float[]>(); // bone -> xyzw per frame (three.js)
        public readonly Dictionary<string, float[]> Positions = new Dictionary<string, float[]>(); // bone -> xyz per frame (three.js)
        public double Length => (Frames - 1) / Fps;
    }

    public static class UalClips
    {
        // anims_ual.json: { "monarch": { clip: { fps, frames, tracks: { Bone: [xyzw...], BonePos: [xyz...] } } }, "hooded_adventurer": {...} }
        public static Dictionary<string, List<UalClip>> Parse(string json)
        {
            var root = (Dictionary<string, object>)Json.Parse(json);
            var outp = new Dictionary<string, List<UalClip>>();
            foreach (var src in root)
            {
                var list = new List<UalClip>();
                foreach (var kv in (Dictionary<string, object>)src.Value)
                {
                    var c = new UalClip { Name = kv.Key, Fps = Json.Num(kv.Value, "fps", 24), Frames = Json.Int(kv.Value, "frames") };
                    foreach (var t in Json.Obj(kv.Value, "tracks"))
                    {
                        var vals = ((List<object>)t.Value).Select(x => (float)(double)x).ToArray();
                        if (t.Key.EndsWith("Pos", StringComparison.Ordinal)) c.Positions[t.Key.Substring(0, t.Key.Length - 3)] = vals;
                        else c.Rotations[t.Key] = vals;
                    }
                    list.Add(c);
                }
                outp[src.Key] = list;
            }
            return outp;
        }

        // Which UAL source rig each base model uses (three3d.mjs preload): adventurer shares the monarch's skeleton.
        public static string SourceFor(string baseModel) => baseModel == "hooded" || baseModel == "hooded_f" || baseModel == "witch" || baseModel == "adventurer_f" ? "hooded_adventurer" : baseModel == "titan" ? null : "monarch";
    }

    // glTF/three.js (right-handed) -> Unity (left-handed), as glTFast imports: mirror the X axis.
    public static class ThreeToUnity
    {
        public static V3 Vec(V3 v) => new V3(-v.X, v.Y, v.Z);
        public static Quat Rot(Quat q) => new Quat(q.X, -q.Y, -q.Z, q.W);
    }
}
