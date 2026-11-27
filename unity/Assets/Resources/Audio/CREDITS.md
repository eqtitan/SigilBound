# Audio credits

All audio here is **CC0 1.0** (public domain). Credit isn't required; we give it anyway.

- **Sound effects** (`*.ogg`): from **Kenney** sound packs. Each archive's License.txt
  reads "Creative Commons Zero, CC0".
  - Impact Sounds: https://kenney.nl/assets/impact-sounds
  - Interface Sounds: https://kenney.nl/assets/interface-sounds
  - RPG Audio: https://kenney.nl/assets/rpg-audio
  - Sci-fi Sounds: https://kenney.nl/assets/sci-fi-sounds
  - Digital Audio: https://kenney.nl/assets/digital-audio
- **Battle music** (`battle_theme.mp3`): "Battle Theme A" by **cynicmusic**,
  https://opengameart.org/content/battle-theme-a (the page lists CC0). Re-encoded at
  96 kbps to keep the app small.

Taken from the CC0 npm package `@netforge/asset-library-files` 0.2.0, whose NOTICE lists
the sources above.

## Battle sounds and ambience (2026-11-26 pass, `unity/Tools~/audio/make_real.py`)

The battle one-shots (`sfx_swing_*`, `sfx_hit_*`, `sfx_armour_*`, `sfx_cast_*`, `sfx_crit`, `sfx_kill`,
`sfx_downed`, `sfx_arrow`, the statuses and every `sfx_titan_*`, `sfx_earthsplit`, `sfx_meteor`, with
their `_2`.. variants) and the ambience beds (`amb_*.ogg`) are designed by `make_real.py`: CC0 recordings
layered, pitched, filtered and re-mixed with code-made layers (modal plate/bell synthesis, granular
rattles, debris and ember crackle, a formant-filtered growl, fire and bubble synthesis, generated
stereo reverbs). Every source below was checked for a CC0 licence file; `fetch_sources.sh` pins each
one to the commit it was checked at.

| Source | Author | Licence | Used for | Fetched from |
|---|---|---|---|---|
| Impact Sounds 1.0 (impactMetal, impactPlate, impactPunch, impactSoft, impactMining, impactTin, impactWood, impactBell, impactGlass, impactGeneric) | Kenney (kenney.nl) | CC0 1.0 | weapon impacts, armour clangs, Titan stomps, debris | github.com/iree-gd/iree.gd `sample/assets/audio/impact` @ 9db7d70 |
| RPG Audio (knifeSlice, chop, drawKnife, metalClick, metalLatch, metalPot, handleCoins, dropLeather, handleSmallLeather, cloth, beltHandle) | Kenney | CC0 1.0 (License.txt in the folder) | blade cuts, dagger scrapes, mail jingle, chain rattle, plate grind, leather and cloth | github.com/Nazarwadim/School-Hooligan `assets/kenney_rpgaudio` @ 940d8fa |
| Sci-fi Sounds (CrunchBlast, ThrusterFire) | Kenney, via TIB Sound Library | CC0 1.0 (LICENSE) | Earthsplitter blast, the body of Cinder Breath | github.com/ignition-bench/sound-library @ 2573312 |
| sfx-kit (whooshes, reverse whooshes, impacts, booms, bass rumbles, cinematic slams, thunder) | sfx-kit contributors | CC0 1.0 (LICENSE; physically modelled, no recordings) | swings, arrow flight, Titan rumble and slams, distant thunder | github.com/wyzowl/sfx-kit @ 92780e7 |
| Ambient beds `scene_*.ogg`: rain (Ylmir), thunder and water (rubberduck), wind (Luke.RUSTLTD), forest (TinyWorlds), birds (isaiah658), crickets (Wolfgang_), waves (RandomMind), fireplace (PagDev), cicadas (Siobhan Leachman) | as named; mixes by the Ciallo-Reader project | CC0 1.0 (each verified on its own OpenGameArt / Wikimedia page, listed in that folder's CREDITS.md) | the recorded part of every `amb_*` bed except the Titan caldera | github.com/roxycon-dev/Ciallo-Reader `app/src/main/assets/ambient` @ a9c3baa |

The designed results are project work built only from CC0 material.

### Boss-lair set-pieces (`sfx_lair_*`, `unity/Tools~/audio/make_lairs.py`)

The landmark sounds of the boss lairs (bell, mill wheel, pyre, lighthouse beam, lantern snuff, lightning,
glass chimes, cavern drip, the shard pulse and the lair reveal swell) are made the same way from the same pinned CC0 sources: Kenney Impact Sounds
(impactBell, impactMetal, impactWood, impactGlass), Kenney RPG Audio (creak), Kenney Sci-fi (ThrusterFire)
and sfx-kit (whooshes, reverse whooshes, cinematic thunder, soft booms), with modal bell and glass synthesis, fire,
bubble and wind synthesis, a formant-filtered choir pad and generated stereo halls.

## Procedural sounds and music (made for Sigilbound)

The interface and summon sounds (`sfx_tap`, `sfx_confirm`, `sfx_reward`, `sfx_summon_*`) and `music_*.ogg`
are synthesized from scratch by `unity/Tools~/audio/make_sfx.py` and `make_music.py` (numpy, encoded with
ffmpeg/libvorbis): no samples, no third-party material; they belong to the project. Re-run the scripts
to regenerate them.
