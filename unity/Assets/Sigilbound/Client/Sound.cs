// Sound effects and music. Effects: the CC0 Kenney files from the web version (Assets/Resources/Audio,
// CREDITS.md) plus procedurally made ones (Tools~/audio/make_sfx.py, sfx_*.ogg): each weapon family has
// its own swing and hit, each element its own cast, statuses and the Titan their own sounds. Music: the
// composed CC0 battle theme, and generated menu and Titan loops and a victory sting (make_music.py),
// crossfading between screens. Volume sliders stay in the save (settings.sfxVol / musicVol, shared with
// the web save); music plays at 0.55 of its slider.
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
            ["hit"] = "hit", ["crit"] = "impact", ["kill"] = "kill", ["swing"] = "sfx_swing_blade", ["cast"] = "sfx_hit_magic",
            ["heal"] = "sfx_heal", ["buff"] = "sfx_buff", ["victory"] = "level_up", ["coin"] = "coin",
            ["tap"] = "sfx_tap", ["confirm"] = "sfx_confirm", ["bigKill"] = "big_kill", ["hurt"] = "hurt", ["downed"] = "sfx_downed",
            ["legendary"] = "sfx_summon_legendary", ["epic"] = "sfx_summon_epic", ["rare"] = "sfx_summon_rare", ["reward"] = "sfx_reward",
            ["debuff"] = "sfx_debuff", ["stun"] = "sfx_stun", ["poison"] = "sfx_poison", ["shield"] = "sfx_shield", ["arrow"] = "sfx_arrow",
            ["titanRoar"] = "sfx_titan_roar", ["titanSlam"] = "sfx_titan_slam", ["earthsplit"] = "sfx_earthsplit", ["meteor"] = "sfx_meteor_whoosh",
            ["swing_blade"] = "sfx_swing_blade", ["swing_heavy"] = "sfx_swing_heavy", ["swing_polearm"] = "sfx_swing_polearm", ["swing_daggers"] = "sfx_swing_daggers",
            ["swing_bow"] = "sfx_swing_bow", ["swing_shield"] = "sfx_swing_shield", ["swing_unarmed"] = "sfx_swing_unarmed",
            ["hit_blade"] = "sfx_hit_blade", ["hit_heavy"] = "sfx_hit_heavy", ["hit_polearm"] = "sfx_hit_polearm", ["hit_daggers"] = "sfx_hit_daggers",
            ["hit_arrow"] = "sfx_hit_arrow", ["hit_shield"] = "sfx_hit_shield", ["hit_unarmed"] = "sfx_hit_unarmed", ["hit_magic"] = "sfx_hit_magic",
            ["cast_flame"] = "sfx_cast_flame", ["cast_gale"] = "sfx_cast_gale", ["cast_tide"] = "sfx_cast_tide", ["cast_light"] = "sfx_cast_light", ["cast_shadow"] = "sfx_cast_shadow",
        };
        public static readonly Dictionary<string, string> Tracks = new Dictionary<string, string>
        {
            ["menu"] = "music_menu", ["battle"] = "battle_theme", ["titan"] = "music_titan", ["victory"] = "music_victory",
        };
        const float MusicMax = 0.55f, Crossfade = 1.2f;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioClip> _tracks = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _sfx = new List<AudioSource>();
        AudioSource _a, _b, _sting; string _track; Coroutine _fade;
        App _app;
        float SfxVol => (float)(_app?.G?.S.settings.sfxVol ?? 0.8);
        float MusicVol => (float)(_app?.G?.S.settings.musicVol ?? 0.6);
        float MusicLevel => Mathf.Min(1, MusicVol * MusicMax);

        public static Sound Create(App app)
        {
            var s = app.gameObject.AddComponent<Sound>();
            s._app = app; I = s;
            foreach (var kv in Files) { var c = Resources.Load<AudioClip>("Audio/" + kv.Value); if (c != null) s._clips[kv.Key] = c; }
            foreach (var kv in Tracks) { var c = Resources.Load<AudioClip>("Audio/" + kv.Value); if (c != null) s._tracks[kv.Key] = c; }
            AudioSource Src(bool loop) { var a = app.gameObject.AddComponent<AudioSource>(); a.loop = loop; a.playOnAwake = false; return a; }
            s._a = Src(true); s._b = Src(true); s._sting = Src(false);
            for (int i = 0; i < 8; i++) s._sfx.Add(Src(false));
            s.Music("menu");
            return s;
        }

        // play(name, { volume, rate, jitter }) from audio.js.
        public void Play(string name, float volume = 1, float rate = 1, float jitter = 0.06f)
        {
            if (SfxVol <= 0 || !_clips.TryGetValue(name, out var clip)) return;
            AudioSource src = null;
            foreach (var a in _sfx) if (!a.isPlaying) { src = a; break; }
            if (src == null) src = _sfx[Random.Range(0, _sfx.Count)];
            src.pitch = rate * (1 + Random.Range(-1f, 1f) * jitter);
            src.PlayOneShot(clip, volume * SfxVol);
        }

        // ---- per weapon / element / status (each hero sounds like its weapon) ----
        static string Fam(WeaponFamily f) => f switch
        {
            WeaponFamily.Heavy => "heavy", WeaponFamily.Polearm => "polearm", WeaponFamily.Daggers => "daggers", WeaponFamily.Bow => "bow",
            WeaponFamily.Shield => "shield", WeaponFamily.Unarmed => "unarmed", WeaponFamily.Caster or WeaponFamily.Support => "magic", _ => "blade",
        };
        public void Swing(WeaponFamily f, float volume = 0.6f) => Play(f == WeaponFamily.Caster || f == WeaponFamily.Support ? "hit_magic" : "swing_" + Fam(f), volume);
        public void Hit(WeaponFamily f, bool crit) { Play(f == WeaponFamily.Bow ? "hit_arrow" : "hit_" + Fam(f), crit ? 1f : 0.8f); if (crit) Play("crit", 0.6f); }
        public void Cast(Element el, float volume = 0.6f) => Play("cast_" + el.ToString().ToLowerInvariant(), volume);
        public void Status(FxType fx)
        {
            string n = fx == FxType.Stun ? "stun" : fx == FxType.Poison || fx == FxType.Burn ? "poison" : fx == FxType.Shield ? "shield" : FxDef.IsBuff(fx) ? "buff" : "debuff";
            Play(n, 0.4f);
        }

        // ---- music: crossfade to a named track (menu, battle, titan); a sting over it (victory) ----
        public void Music(string key)
        {
            if (key == _track || !_tracks.TryGetValue(key, out var clip)) return;
            _track = key;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossTo(clip));
        }
        IEnumerator CrossTo(AudioClip clip)
        {
            var from = _a; var to = _b; _a = to; _b = from;
            to.clip = clip; to.volume = 0; to.time = 0;
            if (MusicVol > 0) to.Play();
            float v0 = from.volume;
            for (float t = 0; t < Crossfade; t += Time.unscaledDeltaTime)
            {
                float k = t / Crossfade; to.volume = MusicLevel * k; from.volume = v0 * (1 - k);
                yield return null;
            }
            to.volume = MusicLevel; from.Stop(); from.volume = 0; _fade = null;
        }
        public void Sting(string key)
        {
            if (MusicVol <= 0 || !_tracks.TryGetValue(key, out var clip)) return;
            _sting.PlayOneShot(clip, Mathf.Min(1, MusicVol * 0.9f));
        }
        // Battles: their track; back to the menu theme afterwards.
        public void StartMusic(bool titan = false) => Music(titan ? "titan" : "battle");
        public void StopMusic() => Music("menu");

        // Volume sliders apply instantly.
        public void ApplyVolumes()
        {
            if (_fade != null) return;
            _a.volume = MusicLevel;
            if (MusicVol <= 0) _a.Stop(); else if (!_a.isPlaying && _a.clip != null) _a.Play();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { _a.Pause(); _b.Pause(); }
            else if (MusicVol > 0 && _a.clip != null) _a.UnPause();
        }
    }
}
