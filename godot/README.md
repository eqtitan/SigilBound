# Sigilbound — Godot demo

A playable Godot 4 demo of Sigilbound for Android phones (portrait, arm64), built with no
accounts or licenses: pick a starter, then fight through **Chapter 1 (stages 1-1 to 1-6)** in
3D battles with the web game's own rules.

**Download:** https://github.com/eqtitan/SigilBound/releases/tag/godot-latest
(`Sigilbound-Godot.apk`; installs as a separate app, "Sigilbound (Godot)",
`com.sigilbound.godot`, next to the regular one. Newer builds install over older ones.)

## What's in the demo

- Title ("Sigilbound — Godot demo" with the version and build) → starter pick (Ysolde, Hask
  or Corvan, joined by Pell and Odo, each with a Worn weapon) → home → battle → victory/defeat
  with gold, shards, XP, level-ups and gear drops → home. Saved locally (`user://`).
- Home: team (tap a hero for stats, skills, gold training and team changes, up to 4), Chapter 1
  stages, summoning with the starting Worn/Mystic shards (web rates and pity), auto-equip gear,
  a 30 fps "Cooler" option, reset.
- Rules ported from `public/game/{data,state,battle}.js`: all 24 heroes and skills, stats with
  gear and set bonuses, the turn-meter battle engine with every effect, elements, the stage
  waves and enemy curve, rewards and XP. Checked against the web code (see Tests).
- 3D battle: the CC0 Quaternius heroes (decoded GLBs in `models/`), recoloured per hero from
  art.js `LOOKS` (enemies as violet "Hollow" copies with red eyes), playing idle, attack, cast,
  hit, death and victory clips; units on the rune-circle rim (centre (0, -0.6), rx 4.2,
  rz 3.95, 2.2 m apart, facing the centre, turning to their target only while acting); the
  "Three-quarter" camera (yaw 38°, pitch 30°) fitted to the portrait frame; arena ground with
  the rune circle, cast circles, projectiles, slashes, hit/heal/buff bursts and floating numbers;
  HP/turn-meter tags with names; manual skill buttons (tap an enemy tag to pick a target),
  Auto and 1x/2x/3x. 60 fps cap (30 with Cooler), 4x MSAA, soft directional shadows; Mobile
  renderer (Vulkan) with the OpenGL ES 3 fallback.

Not in the demo: chapters 2-10, the Titan, Trials/arena/dungeons, gear upgrading and selling,
rank-ups, save codes, audio, the web's code-built signature kit (capes, helmets, robes; simple
props stand in for weapons), body proportions and borrowed body parts, and post-processing.

## Layout

| Path | What it is |
|---|---|
| `scripts/core/` | Rules (`rules.gd`), battle engine (`sb_battle.gd`, `sb_unit.gd`), RNG (`sb_rng.gd`), layout/camera maths, hero colours. No scene code. |
| `scripts/generated/` | Data tables generated from the web files by `tools/make-generated.cjs`. Don't edit. |
| `scripts/battle/` | Arena, hero models, battle screen and HUD. |
| `scripts/ui/`, `scripts/main.gd`, `scripts/game.gd` | The gilded UI kit, screens, and the save/actions autoload. |
| `models/` | Plain GLBs from `public/game/models` (`tools/models/prepare-models.mjs`), with credits. |
| `fonts/` | Cinzel (SIL Open Font License 1.1, `fonts/OFL.txt`; from the `@fontsource/cinzel` package). |
| `tests/` | Parity tests (`run_tests.gd`), fixtures generated from the web code, and `run.sh`. |

## Tests

```
node godot/tests/make-fixtures.cjs                       # fixtures from public/game/*.js (seeded Math.random)
(cd godot/tools/models && npm ci && node make-role-colors.mjs)  # hero-colour fixture via three.js
godot/tests/run.sh /path/to/Godot_v4.7.2-stable_linux.x86_64   # parity tests + headless smoke battle
```

The fixture generator runs the web code in Node with `Math.random` replaced by a Park-Miller
generator; the GDScript tests replay the same seed and compare hero data, hero stats with gear,
XP, stage enemies and rewards, and seven whole auto battles turn by turn (actor, skill, target,
every event, HP after every turn). The smoke run (`-- --smoke`) clicks through title → starter →
battle 1-1 on auto → result → home and prints `SMOKE OK`.

Screenshots: `godot --rendering-driver opengl3 --resolution 412x915 --path godot -- --shots=DIR`
(under `xvfb-run` on a server) saves title, starter, home, battle and result PNGs.

## Build (CI)

`.github/workflows/sigilbound-godot.yml` runs on pushes touching `godot/` (or by hand): it
downloads the pinned Godot 4.7.2 editor and export templates from GitHub releases (SHA-512
checked), runs the tests, sets up JDK 17 and the Android SDK build-tools, exports a debug APK
signed with the permanent test keystore (`mobile/android/app/sigilbound-test.keystore`), and
uploads it to the `godot-latest` pre-release.
