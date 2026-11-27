// Sound effects, ambience and music. Effects are true-stereo variant sets (Tools~/audio/make_real.py):
// CC0 recordings (Kenney Impact Sounds / RPG Audio / Sci-fi, sfx-kit; CREDITS.md) layered with modal
// metal synthesis, granular rattles and generated stereo reverbs. sfx_x.ogg, sfx_x_2.ogg ... are loaded
// as variants of one name and rotated without immediate repeats. A hit is two layers: the attacker's
// weapon (hit_<family>) and the defender's armour (armour_<class>, from Core.HeroArmour): plate clangs
// and rattles, mail jingles, leather slaps, cloth thuds. Each sound pans to where it happens on screen
// (PlayAt / PanOf). Each arena family has a quiet ambience bed under the music (amb_*.ogg, streamed).
// Music: the composed CC0 battle theme, generated menu and Titan loops and a victory sting (make_music.py),
// crossfading between screens. Volume sliders stay in the save (settings.sfxVol / musicVol, shared with
// the web save); music plays at 0.55 of its slider, ambience at 0.6 of the effects slider.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed class Sound : MonoBehaviour
    {
        public static Sound I;
        // Sound name the code plays -> file in Resources/Audio. Tests~ checks every name exists.
        public static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            ["hit"] = "hit", ["crit"] = "sfx_crit", ["kill"] = "sfx_kill", ["swing"] = "sfx_swing_blade", ["cast"] = "sfx_hit_magic",
            ["heal"] = "sfx_heal", ["buff"] = "sfx_buff", ["victory"] = "level_up", ["coin"] = "coin",
            ["tap"] = "sfx_tap", ["confirm"] = "sfx_confirm", ["bigKill"] = "big_kill", ["hurt"] = "hurt", ["downed"] = "sfx_downed",
            ["legendary"] = "sfx_summon_legendary", ["epic"] = "sfx_summon_epic", ["rare"] = "sfx_summon_rare", ["reward"] = "sfx_reward",
            ["debuff"] = "sfx_debuff", ["stun"] = "sfx_stun", ["poison"] = "sfx_poison", ["shield"] = "sfx_shield", ["arrow"] = "sfx_arrow",
            ["titanRoar"] = "sfx_titan_roar", ["titanSlam"] = "sfx_titan_slam", ["earthsplit"] = "sfx_earthsplit", ["meteor"] = "sfx_meteor",
            ["titanStep"] = "sfx_titan_step", ["titanSwing"] = "sfx_titan_swing", ["titanBreath"] = "sfx_titan_breath",
            ["swing_blade"] = "sfx_swing_blade", ["swing_heavy"] = "sfx_swing_heavy", ["swing_polearm"] = "sfx_swing_polearm", ["swing_daggers"] = "sfx_swing_daggers",
            ["swing_bow"] = "sfx_swing_bow", ["swing_shield"] = "sfx_swing_shield", ["swing_unarmed"] = "sfx_swing_unarmed",
            ["hit_blade"] = "sfx_hit_blade", ["hit_heavy"] = "sfx_hit_heavy", ["hit_polearm"] = "sfx_hit_polearm", ["hit_daggers"] = "sfx_hit_daggers",
            ["hit_arrow"] = "sfx_hit_arrow", ["hit_shield"] = "sfx_hit_shield", ["hit_unarmed"] = "sfx_hit_unarmed", ["hit_magic"] = "sfx_hit_magic",
            ["armour_plate"] = "sfx_armour_plate", ["armour_mail"] = "sfx_armour_mail", ["armour_leather"] = "sfx_armour_leather", ["armour_cloth"] = "sfx_armour_cloth",
            ["cast_flame"] = "sfx_cast_flame", ["cast_gale"] = "sfx_cast_gale", ["cast_tide"] = "sfx_cast_tide", ["cast_light"] = "sfx_cast_light", ["cast_shadow"] = "sfx_cast_shadow",
            // boss lairs: the landmark set-pieces reacting to the fight (story scenes; Tools~/audio/make_lairs.py)
            ["lairBell"] = "sfx_lair_bell", ["lairWheel"] = "sfx_lair_wheel", ["lairPyre"] = "sfx_lair_pyre", ["lairBeam"] = "sfx_lair_beam",
            ["lairSnuff"] = "sfx_lair_snuff", ["lairThunder"] = "sfx_lair_thunder", ["lairChime"] = "sfx_lair_chime", ["lairDrip"] = "sfx_lair_drip", ["lairShard"] = "sfx_lair_shard",
            ["lairReveal"] = "sfx_lair_reveal",   // the boss-lair camera reveal: sub swell, choir/wind pad, a low boom at ~2.2 s
        };
        // Ambience beds (amb_*.ogg, loaded when a battle starts) and the bed each arena theme uses
        // (index = GameData.ThemeOf(chapter), the Scenery theme order); the Titan's caldera has its own.
        public static readonly Dictionary<string, string> Beds = new Dictionary<string, string>
        {
            ["forest"] = "amb_forest", ["meadow"] = "amb_meadow", ["shore"] = "amb_shore", ["wind"] = "amb_wind",
            ["marsh"] = "amb_marsh", ["fire"] = "amb_fire", ["storm"] = "amb_storm", ["caldera"] = "amb_caldera",
        };
        public static readonly string[] ThemeBeds =
        {
            "forest", "shore", "wind", "marsh", "meadow", "meadow", "shore", "wind", "marsh", "fire",   // act 1: dusk forest .. burning ruins
            "shore", "marsh", "wind", "fire", "storm",                                                 // act 2: harbor, fens, barrows, ember groves, storm coast
            "wind", "storm",                                                                           // the Glass Wastes' glass desert, Stormcrown Heights' crag
        };
        // Music. menu/titan/victory: make_music.py; boss/act2/map: make_music2.py (stereo, streamed); battle: the CC0 theme.
        public static readonly Dictionary<string, string> Tracks = new Dictionary<string, string>
        {
            ["menu"] = "music_menu", ["battle"] = "battle_theme", ["titan"] = "music_titan", ["victory"] = "music_victory",
            ["boss"] = "music_boss", ["act2"] = "music_act2", ["map"] = "music_map",
        };
        const float MusicMax = 0.55f, Crossfade = 1.2f, AmbMax = 0.6f, AmbFade = 1.5f;
        const int MaxVariants = 8;
        readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> _last = new Dictionary<string, int>();
        readonly Dictionary<string, AudioClip> _tracks = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _sfx = new List<AudioSource>();
        AudioSource _a, _b, _sting, _amb, _ambOld; string _track, _bed; Coroutine _fade, _ambFade;
        App _app;
        float SfxVol => (float)(_app?.G?.S.settings.sfxVol ?? 0.8);
        float MusicVol => (float)(_app?.G?.S.settings.musicVol ?? 0.6);
        float MusicLevel => Mathf.Min(1, MusicVol * MusicMax);
        float AmbLevel => Mathf.Min(1, SfxVol * AmbMax);

        public static Sound Create(App app)
        {
            var s = app.gameObject.AddComponent<Sound>();
            s._app = app; I = s;
            foreach (var kv in Files)
            {
                var set = new List<AudioClip>();
                var c0 = Resources.Load<AudioClip>("Audio/" + kv.Value); if (c0 != null) set.Add(c0);
                for (int v = 2; v <= MaxVariants && c0 != null; v++) { var c = Resources.Load<AudioClip>("Audio/" + kv.Value + "_" + v); if (c == null) break; set.Add(c); }
                if (set.Count > 0) s._clips[kv.Key] = set.ToArray();
            }
            foreach (var kv in Tracks) { var c = Resources.Load<AudioClip>("Audio/" + kv.Value); if (c != null) s._tracks[kv.Key] = c; }
            AudioSource Src(bool loop) { var a = app.gameObject.AddComponent<AudioSource>(); a.loop = loop; a.playOnAwake = false; return a; }
            s._a = Src(true); s._b = Src(true); s._sting = Src(false); s._amb = Src(true); s._ambOld = Src(true);
            for (int i = 0; i < 16; i++) s._sfx.Add(Src(false));   // hits are two layers (weapon + armour)
            s.Music("menu");
            return s;
        }

        // play(name, { volume, rate, jitter }) from audio.js; pan -1 (left) .. 1 (right). A random variant,
        // never the same one twice running.
        public void Play(string name, float volume = 1, float rate = 1, float jitter = 0.06f, float pan = 0)
        {
            if (SfxVol <= 0 || name == null || !_clips.TryGetValue(name, out var set)) return;   // unknown cue: silence
            int i = 0;
            if (set.Length > 1)
            {
                int last = _last.TryGetValue(name, out var l) ? l : -1;
                i = Random.Range(0, set.Length - (last >= 0 ? 1 : 0));
                if (last >= 0 && i >= last) i++;
            }
            _last[name] = i;
            AudioSource src = null;
            foreach (var a in _sfx) if (!a.isPlaying) { src = a; break; }
            if (src == null) src = _sfx[Random.Range(0, _sfx.Count)];
            src.pitch = rate * (1 + Random.Range(-1f, 1f) * jitter);
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.PlayOneShot(set[i], volume * SfxVol);
        }
        // Where a sound happens on screen: the camera's view of the point, mapped to a stereo position
        // (kept inside +-0.85 so nothing vanishes from one ear).
        public static float PanOf(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return 0;
            var v = cam.WorldToViewportPoint(world);
            return v.z <= 0 ? 0 : Mathf.Clamp((v.x - 0.5f) * 1.7f, -0.85f, 0.85f);
        }
        // Unknown names (or a missing file) play nothing: Play looks the name up and returns quietly.
        public void PlayAt(string name, Vector3 world, float volume = 1, float rate = 1, float jitter = 0.06f)
        {
            if (string.IsNullOrEmpty(name) || !_clips.ContainsKey(name)) return;
            Play(name, volume, rate, jitter, PanOf(world));
        }

        // ---- per weapon / armour / element / status (each hero sounds like its weapon) ----
        static string Fam(WeaponFamily f) => f switch
        {
            WeaponFamily.Heavy => "heavy", WeaponFamily.Polearm => "polearm", WeaponFamily.Daggers => "daggers", WeaponFamily.Bow => "bow",
            WeaponFamily.Shield => "shield", WeaponFamily.Unarmed => "unarmed", WeaponFamily.Caster or WeaponFamily.Support => "magic", _ => "blade",
        };
        static string Arm(ArmourClass a) => a switch { ArmourClass.Plate => "plate", ArmourClass.Mail => "mail", ArmourClass.Leather => "leather", _ => "cloth" };
        // How much of the armour's own sound a weapon brings out: a hammer rings plate fully, an arrow ticks it, a spell barely.
        static float ArmourWeight(WeaponFamily f) => f switch
        {
            WeaponFamily.Heavy => 1f, WeaponFamily.Shield => 0.95f, WeaponFamily.Blade => 0.9f, WeaponFamily.Polearm => 0.85f,
            WeaponFamily.Unarmed => 0.75f, WeaponFamily.Daggers => 0.7f, WeaponFamily.Bow => 0.6f, _ => 0.35f,
        };
        public static ArmourClass ArmourOf(Unit u) => u == null ? ArmourClass.Leather : u.Titan ? ArmourClass.Plate : HeroArmour.Of(u.Template?.Id, u.Template?.Role ?? Role.Attack);
        public void Swing(WeaponFamily f, float volume = 0.6f, float pan = 0) => Play(f == WeaponFamily.Caster || f == WeaponFamily.Support ? "hit_magic" : "swing_" + Fam(f), volume, 1, 0.06f, pan);
        // A blow landing: the weapon's impact plus the defender's armour answering it. rate < 1 for huge bodies.
        public void Hit(WeaponFamily f, bool crit, ArmourClass armour = ArmourClass.Leather, float pan = 0, float rate = 1)
        {
            Play(f == WeaponFamily.Bow ? "hit_arrow" : "hit_" + Fam(f), crit ? 1f : 0.8f, rate, 0.06f, pan);
            Play("armour_" + Arm(armour), (crit ? 1f : 0.85f) * ArmourWeight(f), rate, 0.05f, pan);
            if (crit) Play("crit", 0.6f, 1, 0.05f, pan);
        }
        public void Cast(Element el, float volume = 0.6f, float pan = 0) => Play("cast_" + el.ToString().ToLowerInvariant(), volume, 1, 0.06f, pan);
        public void Status(FxType fx, float pan = 0)
        {
            string n = fx == FxType.Stun ? "stun" : fx == FxType.Poison || fx == FxType.Burn ? "poison" : fx == FxType.Shield ? "shield" : FxDef.IsBuff(fx) ? "buff" : "debuff";
            Play(n, 0.4f, 1, 0.06f, pan);
        }

        // ---- ambience: a quiet looping bed per arena, crossfaded; loaded on demand, unloaded after ----
        public void Ambience(int theme, bool titan)
        {
            string bed = titan ? "caldera" : ThemeBeds[Mathf.Clamp(theme, 0, ThemeBeds.Length - 1)];
            if (bed == _bed && _amb.isPlaying) return;
            _bed = bed;
            var clip = Beds.TryGetValue(bed, out var file) ? Resources.Load<AudioClip>("Audio/" + file) : null;
            if (_ambFade != null) StopCoroutine(_ambFade);
            _ambFade = StartCoroutine(AmbTo(clip));
        }
        public void StopAmbience() { if (_bed == null) return; _bed = null; if (_ambFade != null) StopCoroutine(_ambFade); _ambFade = StartCoroutine(AmbTo(null)); }
        IEnumerator AmbTo(AudioClip clip)
        {
            var from = _amb; var to = _ambOld; _amb = to; _ambOld = from;
            to.clip = clip; to.volume = 0; to.panStereo = 0;
            if (clip != null && SfxVol > 0) { to.time = Random.Range(0f, clip.length * 0.9f); to.Play(); }   // start anywhere in the loop
            float v0 = from.volume;
            for (float t = 0; t < AmbFade; t += Time.unscaledDeltaTime)
            {
                float k = t / AmbFade; to.volume = clip != null ? AmbLevel * k : 0; from.volume = v0 * (1 - k);
                yield return null;
            }
            to.volume = clip != null ? AmbLevel : 0;
            var old = from.clip; from.Stop(); from.clip = null; from.volume = 0;
            if (old != null && old != clip) Resources.UnloadAsset(old);
            _ambFade = null;
        }

        // ---- music: crossfade to a named track (menu, battle, titan); a sting over it (victory) ----
        // key null: fade the music out (a boss stage waits in silence for its lair reveal).
        public void Music(string key)
        {
            if (key == _track) return;
            AudioClip clip = null;
            if (key != null && !_tracks.TryGetValue(key, out clip)) return;
            _track = key;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossTo(clip));
        }
        IEnumerator CrossTo(AudioClip clip)
        {
            var from = _a; var to = _b; _a = to; _b = from;
            to.clip = clip; to.volume = 0; to.time = 0;
            if (MusicVol > 0 && clip != null) to.Play();
            float v0 = from.volume;
            for (float t = 0; t < Crossfade; t += Time.unscaledDeltaTime)
            {
                float k = t / Crossfade; to.volume = clip != null ? MusicLevel * k : 0; from.volume = v0 * (1 - k);
                yield return null;
            }
            to.volume = clip != null ? MusicLevel : 0; from.Stop(); from.volume = 0; _fade = null;
        }
        public void Sting(string key)
        {
            if (MusicVol <= 0 || !_tracks.TryGetValue(key, out var clip)) return;
            _sting.PlayOneShot(clip, Mathf.Min(1, MusicVol * 0.9f));
        }
        // Battles: their track (battle, act2, titan; boss after its lair reveal); back to the menu theme
        // afterwards. Out of battle the campaign map and team select have their own calm theme.
        bool _inBattle;
        public void StartMusic(bool titan = false) => StartMusic(titan ? "titan" : "battle");
        public void StartMusic(string key) { _inBattle = true; Music(key); }
        public void StopMusic() { _inBattle = false; Music(MenuTrack); StopAmbience(); }
        string MenuTrack => _app != null && (_app.Screen == ScreenId.Campaign || _app.Screen == ScreenId.Prep) ? "map" : "menu";
        void Update()
        {
            if (!_inBattle && _app != null && _app.Screen != ScreenId.Battle && _track != MenuTrack && _track != null) Music(MenuTrack);
        }

        // Volume sliders apply instantly.
        public void ApplyVolumes()
        {
            if (_ambFade == null && _amb.clip != null)
            {
                _amb.volume = AmbLevel;
                if (SfxVol <= 0) _amb.Stop(); else if (!_amb.isPlaying) _amb.Play();
            }
            if (_fade != null) return;
            _a.volume = MusicLevel;
            if (MusicVol <= 0) _a.Stop(); else if (!_a.isPlaying && _a.clip != null) _a.Play();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { _a.Pause(); _b.Pause(); _amb.Pause(); }
            else
            {
                if (MusicVol > 0 && _a.clip != null) _a.UnPause();
                if (SfxVol > 0 && _amb.clip != null) _amb.UnPause();
            }
        }
    }
}
