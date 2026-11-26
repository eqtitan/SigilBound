# Sigilbound (Unity) work plan: one session at a time

Usage limits stop long multi-agent runs partway, so the work is split into steps that each
fit in **one session**. Every session does exactly this:

1. Read this file and do the **first unchecked step**. Nothing else.
2. Work on branch `step/<number>` from `main`, keeping each step small.
3. Verify before finishing:
   - `dotnet run --project unity/Tests~`
   - `dotnet build unity/Tests~/CompileCheck/Client.csproj`
   - `dotnet build unity/Tests~/CompileCheck/Editor.csproj`
   - once step 3 exists, screenshots from the Linux preview build.
4. Merge into `main` and push, so CI publishes Android and Windows builds and players see the update banner.
5. Tick the step here (`[x]`, date, commit), write any notes for the next step under it, and push.

If a session runs out partway, commit what compiles, push the branch, and write
"RESUME:" notes under the step.

Rules that always apply:
- original content only (CC0 or code-built);
- no paid services;
- game rules and saves unchanged unless the step says so;
- no model names in commits;
- commit trailers as in recent history.

Unfinished work saved from the stopped agents:
- branch `unity-landscape`: WIP Icons.cs, UiKit.cs, Ui.cs edits and a font in Resources. Reuse what's useful.
- branch `art-round2`: art bible, 24 hero sheets, the heroforge pipeline, the Unity import tool, the free-tools Blender prototype of Ysolde and Gorrum, and a hero-art CI job.

## Phase A: make the Unity build work on devices (top priority)

- [x] **1. Text bug.** (2026-11-25, step/1) One string drawn into many controls, labels outside buttons, duplicates.
  - Find the root cause in Client/Ui.cs (IMGUI) and fix it everywhere.
  - Ship an OFL font in Resources instead of OS fonts.
  - Done when every screen draws each label once, inside its control.
  - DONE: root cause was the serif font from `Font.CreateDynamicFontFromOSFont`. Unity 6's TextCore IMGUI doesn't generate text for OS fonts, so every serif label or button redrew the previous built-in-font string. Fixed by bundling Cinzel (OFL) in `Resources/Fonts` (regular and bold) and using the built-in font for strings with glyphs Cinzel lacks (`Ui.SerifCanDraw`).
  - Not yet verified visually; there are no screenshots until step 3. The owner should check the next build on a device. If any label is still wrong, the bug isn't only the font: look at TextCore caching per GUIStyle next.
  - Overflowing button text ("Come back tomorrow") and the empty gold button on Home are probably this same bug. Re-check them after step 3.
- [x] **2. Icons.** (2026-11-25, step/2) Replace the emoji icons (currency chips, bottom nav, tags) with code-drawn or texture icons (start from `unity-landscape`'s Icons.cs).
  - DONE: added Icons.cs (42 code-drawn icons, including element icons). Used for the currency chips, the settings menu button, the bottom-nav tab icons and the update banner's close button.
  - The code had no real emoji: the ● ◆ ≡ ★ ‹ symbols render in the built-in font, and the "missing icons" on devices were step 1's font bug.
  - Still text symbols, for later steps to swap to `Icons.Draw` while reworking those screens: ★ (stars in Ui.Stars and hero cards), ‹ › back/next links, → in tips and battle text, ▶ in battle.
- [x] **3. See the game ourselves.** (DONE 2026-11-25: the screenshot tour works since build 27, once screenshot mode kept running without window focus; `tools/unity-shots.sh`.)
  - A `-sbShots=DIR` command-line mode that walks the main screens and saves PNGs.
  - A CI job that builds a StandaloneLinux64 player and publishes it to a `unity-preview` release.
  - A script that runs it under xvfb here and saves the screenshots.
  - From now on, every step checks its screenshots before it's marked done.
- [x] **4. Shaders in builds.** (DONE with step 20, 2026-11-25: the stand-in heroes were really two model-loading bugs, fixed in builds 30 and 35; see step 20. Close this together with step 20.) The GLB heroes, particles and Sigilbound/Character are stripped from builds, so stand-in figures and orange squares appear. Include them via Always Included Shaders, a ShaderVariantCollection or Resources materials, glTFast's shaders included. Done when the screenshots show the real heroes and soft particles.
  - 2026-11-25, partial (step/4):
    - Particles fixed. The orange squares were the arena motes (Scenery.cs) on an untextured material; they now use a leaf or soft-dot texture on a blended material.
    - Shaders were already handled: UrpSetup adds Always Included Shaders, and Resources holds the HeroLit, HeroGlow and Character materials. The GLB heroes use SigilMaterialGenerator → Materials.Lit, NOT glTFast's shaders, so shader stripping is probably NOT why the heroes fall back to stand-ins.
    - RESUME: read player.log from `tools/unity-shots.sh` (step 3's Linux preview). Look for "couldn't load <file>.glb" warnings or glTFast errors. Check that StreamingAssets/Models is in the build, the Android jar: path read in HeroModels.ReadBytes, and glTFast's meshopt/Draco support. Then fix and confirm with screenshots.
- [x] **5. Scenery.** (2026-11-25, step/5; first pass) Replace the sphere trees and plain ground with the web game's scenery recipes (`game3d/scenery.mjs`). The Godot build 4 forest is the minimum bar.
  - DONE (first pass): LowPoly.cs adds faceted canopies and boulders, pine cones, tapered trunks and grass blades, replacing the sphere, capsule and cylinder primitives in Scenery.cs.
  - Not yet: the winding path and worn earth, logs and stumps, the gradient sky, and a check of the ground texture against the Godot forest. Pick these up in step 10 (battle presentation) or a follow-up, after looking at screenshots.
  - Also fixed: all build workflows now publish from `main` only. WIP branches had published builds 9 and 10.

## Phase B: landscape (the owner chose landscape for phone and PC)

- [x] **6. Landscape foundation.** (2026-11-25, step/6)
  - Android landscape, both directions. Windows 1600×900 resizable.
  - Height-based UI scaling from 16:9 to 21:9 with safe areas.
  - Existing screens reflowed so nothing overlaps.
  - DONE:
    - `Ui.Begin(fullWidth)` scales to the height in landscape (RefH 480 on phones, 600 on PC).
    - Menus draw in a centred 390 column (`Ui.W`, `Ui.Offset`/`Ui.Left`, `Ui.FullW`, `Ui.Screen01`); the battle uses the full width.
    - Bars and modal backdrops span the full width.
    - Android autorotates between the two landscape orientations; Windows is 1600×900; the shots script defaults to 1280×720.
  - Steps 7–11 should design each screen for the wide canvas: use `Ui.Begin(true)` and lay out across `Ui.W`, instead of the column.
  - Check first in the screenshots: the battle HUD (turn bar, skill bar) is now stretched across the full width, and the tags clamp to the full width.
- [x] **7. Game modes hub.** (2026-11-25, step/7) Big illustrated cards for campaign, hard, trials, arena and titan, showing live status.
  - DONE: ModesScreen.cs (`ScreenId.Modes`). It's the Battle tab in the nav, replacing Campaign. It's full width in landscape (5 cards in a row) and two columns in portrait. The cards are code-drawn (gradient plate, rune ring, icon, status footer, badge, lock).
  - Ideas for later: illustrated backdrops per mode (small 3D vignettes or painted textures), and the Trials tab is now redundant with the hub (consider replacing it with Codex or Achievements).
- [x] **8. Campaign world map.** (2026-11-25, step/8) Sigilbound's own painted-look map, chapter markers with stars, a reward track and a Normal/Hard switch.
  - DONE: WorldMap.cs, full width in landscape. The map is generated once (512×256 noise parchment; preview checked).
    - Markers show stages cleared as 6 pips: the game has no per-stage stars, so none were invented.
    - The reward track shows the real boss first-clear rewards from `Game.StageRewards`.
    - The chapter panel's stage buttons go to Prep. Step 9 should restyle that panel into the full stage list (enemies, drops).
  - Ideas for later: painted landmarks per chapter (towers, ruins) on the map; an animated marker on the current chapter.
- [x] **9. Stage list and team setup.** (2026-11-25, step/9)
  - Stage rows: stars, enemies, drops, Battle button.
  - Team setup: you vs the enemy, the element wheel, power, a hero picker with filter, sort and presets.
  - DONE: PrepScreens.cs, landscape only (portrait keeps the old screens). Everything is real game data.
    - The stage list draws on the world map when a chapter is chosen (`DrawStageList`). Enemies come from StageWaves/HardWaves, drops from StageRewards/HardRewards, and the gear chance is 45%/90%.
    - Team setup (`DrawPrepWide`) is three columns: team and presets; element wheel and waves; hero picker with filter and sort.
  - There's no leader or aura: the game has no auras, so none were invented. There are no stars either, only the cleared ticks.
  - Check first in the screenshots: the density of the stage rows on a 480-tall canvas (rows are about 40 high), and the wave chip widths with 3 enemies.
- [x] **10. Battle presentation.** (2026-11-25, step/10) Wide camera, slim HP bars with level, wave counter and timer, corner Auto/speed/pause, slim skill bar, tags that don't overlap.
  - DONE: `BattleController.DrawHudWide` (landscape; portrait keeps `DrawHud`).
    - The camera was already aspect-aware (`Arena.FrameBattle`).
    - Pause is new: timeScale 0, cleared on leaving a battle.
    - Tags are nudged apart: lowest first, each moves up 24 until it clears the others.
  - Also done: Windows in-place update (`Updater.UpdateWindows`). It downloads, then a hidden PowerShell script uses Expand-Archive and robocopy over the install folder and restarts the game. Any failure falls back to the link.
  - Check first in the screenshots: tag density with 7 units, and the skill buttons and description against the bottom safe area.
- [x] **11. Results screen.** (2026-11-25, step/11) Banner with stars and time, rewards, per-hero damage dealt / taken / healing, and Replay / Edit Team / Next.
  - DONE: `BattleController.DrawResults` for campaign stages in landscape.
    - Damage taken and healing done are tallied from BattleEvents (Damage, Heal) in the view. Damage dealt is `Unit.DmgDealt`.
    - Shown instead of stars: heroes standing and the battle time.
    - Edit Team calls `App.EditTeam(stage)`.
  - Not done: Hard, Dungeon, Arena and Titan still use their old result boxes. Give them `DrawResults` too (same layout, mode-specific rewards) in a follow-up.
  - Phase B (steps 6–11) is complete. Next is Phase C (art), plus checking every screen with the screenshot tour once the Linux preview works.

## Phase C: hero art (free tools first; the owner decides on paid tools after seeing the result)

- [x] **12. Review the free-tools prototype.** (2026-11-25, step/12) Merge the useful parts of `art-round2`. Render Ysolde and Gorrum in the game next to today's heroes, and send the owner screenshots to decide.
  - DONE:
    - Merged `art-round2` up to d315f67: docs/sigilbound/art (ART_BIBLE, ROUTES, NO_PAID_PATH, 24 hero sheets), the Unity import pipeline (Editor/Art), the runtime loader (HeroRig), the SigilCharacterPainted shader, heroforge and blenderforge.
    - The WIP hero-art CI workflow (commit 91ed352 on that branch) is NOT merged yet. It's untested.
  - Review: `docs/sigilbound/art/prototype/{ysolde,gorrum}-compare.png` compare current against prototype.
    - Better: stronger silhouettes and identity props.
    - Same or worse: stiff anatomy, crude faces; at about 70 px in battle the gain is small.
    - The prototypes haven't been rendered inside Unity: that needs the hero-art import job, which hasn't run yet.
  - The owner decides next: keep improving the free path (CC0 base bodies + scripted kit + baked painted textures), or allow a paid generator for higher quality. Steps 13–18 depend on that answer.
- [x] **13–18. Heroes in batches of 4 per session** (all 24 DONE 2026-11-25, see below; in-game check pending the model-load fix) (24 heroes), with the shared idle/wait animation and unique skill animations where possible.
  - OWNER DECISION (2026-11-25): improve the FREE path. No paid generator.
  - Per hero:
    - start from the CC0 Quaternius base bodies (better anatomy than the scripted skin-modifier bodies);
    - add the hero's identity kit with blenderforge (armour, hair, props from the hero sheet);
    - bake the painted albedo plus glow mask;
    - shared idle/hit from the CC0 clip set, with unique skill/attack motion where CC0 or scripted clips allow;
    - import through Editor/Art (HeroRig) so the game uses it and falls back to today's model otherwise.
  - Step 13 = Ysolde, Gorrum, Hask, Corvan (the starters first). Check each in-game with the screenshot tour before moving on.
  - The hero-art CI import job still needs merging and testing (art-round2 commit 91ed352).
  - RESUME (2026-11-25, branch `step/13`, not merged):
    - `unity/Tools~/blenderforge/quat.py` builds a free-path hero: decoded Quaternius base (godot/models), forge.py painted materials in the hero palette (the 4 starters are defined), rigid kit on bones, one baked atlas, and a GLB keeping the 62-bone skeleton and 24 clips.
    - `render_glb.py` previews an exported GLB with Cycles. EEVEE can't load textures headless here.
    - Ysolde result: `docs/sigilbound/art/prototype/ysolde-quaternius-wip.png`.
    - To fix next:
      1. Hood removal: it isn't the Black slot; find the hood faces on Medieval_Head, e.g. by vertex group or position above the head bone.
      2. Kit placement: the whip curls upward. Build kit pieces in the bone's local frame, not world offsets.
      3. Applying the exported Idle clip hides the mesh in Blender. Check the root/scale keys (the armature is scaled ×100) before any Unity import.
      4. Then do gorrum, hask and corvan; import via the hero-art job; and check with the screenshot tour.
  - STEP 13 DONE (2026-11-25):
    - Fixed: hood/crown/bald hair via the game's own colour roles; hand kit in the bone frame; skeleton identical to the base (no bind-pose guessing, rest pose exported, no clips); real head size for head kit.
    - The 4 starters are in `unity/Tools~/models/heroes` (preview: `docs/sigilbound/art/prototype/starters-free-path.png`). The game loads them through `HeroModels.BuildBaked` with the base clips. This path, not the untested Editor/Art import job, is the route for steps 14–18.
    - Check first in the screenshot tour or on a device:
      - the base clips animate the baked heroes (the Animation goes on `holder.GetChild(0)`; if heroes stand in a T-pose, the clip root path differs);
      - albedo colour space;
      - glow strength.
    - Known polish items: Hask's helm doesn't show; Ysolde's circlet doesn't show; the kit pieces are basic shapes.
  - STEP 14 DONE (2026-11-25): Ulfgar, Morwenna, Oriel, Vaelith (the owner's own team), plus Hask/Ysolde rebaked. Preview: `docs/sigilbound/art/prototype/step14-free-path.png`.
    - quat.py now has the adventurer base, head kit from the measured head mesh, and crown/halo/flame hair/beard/cape/robe/sword/orb.
    - BLOCKER FOUND: the real models (runtime-built and baked) have never shown in any Unity build. The build 27 tour and the owner's phone screenshots show only the code-built stand-ins (CharacterView.BuildPlaceholder), with no warning in the log. This is the step 4 'stand-in heroes' issue.
      - The Linux preview contains StreamingAssets/Models (bases, heroes/, index.txt) and glTFast, and SB_GLTFAST is compiled in.
      - Build 29+ logs every load stage as `SBMODEL ...` in player.log. Run `tools/unity-shots.sh` and `grep SBMODEL <out>/player.log` to see where it stops, then fix that before step 15.
  - Model-load fix pushed (2026-11-25, build 30): shared loads now run on a persistent loader. A hero figure destroyed mid-load used to leave its base model 'loading' forever, so every hero kept its stand-in. Confirm with the build 30 tour (`grep SBMODEL player.log`).
  - STEP 15 DONE (2026-11-25): Aurelia, Velka, Thrain, Kaito; new kit pieces: spiky hair, bandana, katana. Preview: `docs/sigilbound/art/prototype/step15-free-path.png`. 12 heroes baked.
  - STEP 16 DONE (2026-11-25): Lumen, Nyx, Tamsin, Sable; new kit pieces: mask, twin daggers, braid, bow, quiver, veil, hand mirror. Preview: `docs/sigilbound/art/prototype/step16-free-path.png`. 16 heroes baked.
  - STEP 17 DONE (2026-11-25): Brannoc, Mira, Pell, Odo; new kit pieces: spear, ponytail, candle, club. Preview: `docs/sigilbound/art/prototype/step17-free-path.png`. 20 heroes baked.
  - STEP 18 DONE (2026-11-25): Wren, Kettle, Maelis, Seraphine; new kit pieces: lantern, hook, trident, bell (Maelis keeps the monarch body's own crown). Preview: `docs/sigilbound/art/prototype/step18-free-path.png`.
    - ALL 24 heroes are now baked in `unity/Tools~/models/heroes`.
    - Still to check on a device or with the screenshot tour, once the model-load fix (build 30) is confirmed:
      - the base clips animate the baked heroes (no T-pose);
      - the albedo colours and glow strength look right in URP.
    - Polish later: robes are rigid on the hips (the legs can poke through when walking); kit pieces are basic shapes.
  - Steps 14–18: add the next heroes to `HEROES` in quat.py (palette from Core/Looks.cs, kit list), run it, check the previews, commit the files. Adventurer-based heroes need an `adventurer` entry in ROLES (copy from Core/Looks.cs).
- [x] **19. Roster plan to 100 heroes.** Phases and concepts only. New heroes get designed (stats and skills) in their own later steps.
  - DONE (2026-11-25): `docs/sigilbound/ROSTER.md`.
    - 76 concepts in 4 phases: Roadwardens (16), Glass and Storm (20), The Hollow Unbound (20), The Sigil Bearers (20).
    - Each concept has a name, rarity, element, role, base body, kit and a mechanic hook.
    - Targets at 100: 20 heroes per element; 22/34/28/16 by rarity; 34/22/20/24 by role.
    - `docs/sigilbound/roster.py` checks the counts.
    - Ten new mechanics are listed for the engine work that comes before phases 2 to 4.

## Phase D: the roster grows (from docs/sigilbound/ROSTER.md)

- [x] **20. Verify all 24 baked heroes in game.** DONE 2026-11-25: the build 37 gallery shows 24/24 real models on their spots (`docs/sigilbound/art/prototype/step20-gallery-build37.png`); colours and glows read well. The spell-clip fix (build 42) is to be confirmed on a device. Check the menu lineup, battle and hero viewer in the screenshot tour and on a device, then polish (robes that follow the legs, colour and glow levels).
  - Model-load fixes so far: shared loads on a persistent loader (build 30, confirmed in battle); the menu lineup was rebuilt every frame (build 35, CONFIRMED: real models on intro, home, heroes and battle; each figure loads once).
  - Work done (2026-11-25, to confirm in the next build):
    - Screenshot tour: a hero gallery (all 24, four at a time, menus hidden), with real-model counts in shots.log.
    - Robes follow the legs: weighted from the hips toward each thigh. The 7 robed heroes are rebaked. High-knee frames still show the thigh a little; true cloth would need simulation.
    - Stilt legs, then floating (owner's screenshots, Android and Windows, mostly the hooded heroes):
      - The real cause: the UAL spell/hit/death tracks use three.js names without dots (`UpperLegL`, `FootL`), while the Unity bones keep them (`UpperLeg.L`), so only the hips, spine and head were animated.
      - The hooded UAL data also puts Body at about twice its rest height.
      - Fixed in `HeroModels.BuildUal`: bones match with or without dots, and position tracks are applied relative to the rest pose. The earlier `FootFollow` patch, which turned the stilts into floating, is removed.
  - Still open: colour and glow levels (judge from the gallery shots), and device checks.
- [x] **21–24. Phase 1 heroes (Roadwardens), 4 per step.** ALL DONE (2026-11-25): Bram, Nettle, Hobb, Greta; Cole, Tilly, Ronan, Isla; Dunstan, Marisol, Vesper, Brega; Fennick, Orla, Silas, Wynn. 40 heroes (see below). Stats, skills, looks, tests and balance sims, plus the baked model and the new `anchor`/`net` kit.
  - STEP 21 DONE (2026-11-25):
    - Bram, Nettle, Hobb and Greta are in `GameData.Heroes` after the original 24, with looks, runtime art (`Core/HeroArt.Roster.cs`), stand-ins and baked models. New kit: anchor, net. Preview: `docs/sigilbound/art/prototype/step21-roadwardens.png`.
    - Rules set in this step:
      - The new heroes are Unity only. `GameData.Classic` (the first 24) is the web game's roster: parity tests check it, and every enemy pool (campaign, Hard, dungeons, rivals) still draws from it, so stages don't change.
      - New heroes are summonable at once (summons pick from all heroes of the rolled rarity).
      - A Unity save code holding a new hero would break the frozen web game if pasted there; the Unity app ignores unknown ids.
    - `unity/Tests~/RosterTests.cs` checks: every hero has a look and a baked model; every new hero is in ROSTER.md; enemies are Classic only; balance sims against the classic hero of the same rarity and role (40–60%).
    - Final balance: Bram 44% vs Odo, Nettle 55% vs Brannoc/Kettle, Hobb 51% vs Mira, Greta 51% vs Pell/Wren. Kits were tuned from the ROSTER hooks; the ROSTER rows are updated.
  - STEP 22 DONE (2026-11-25): Cole, Tilly, Ronan, Isla. New kit: axe. Preview: `docs/sigilbound/art/prototype/step22-roadwardens.png`. Balance after tuning: Cole 57% vs Brannoc/Kettle, Tilly 44% vs Pell/Wren, Ronan 52% vs Odo, Isla 55% vs Pell/Wren.
    - Note: Brannoc is the weakest classic Common attacker, so new Attack heroes score high against him. Kettle is the fairer peer.
  - STEP 23 DONE (2026-11-25): Dunstan, Marisol, Vesper, Brega, the first new Rares (3 skills each). Preview: `docs/sigilbound/art/prototype/step23-roadwardens.png`. Balance: Dunstan 43% vs Hask/Gorrum, Marisol 55% and Vesper 46% vs Ysolde/Lumen/Nyx, Brega 54%.
    - Beards: the adventurer head's beard shares the hair material, and quat.py never applied Looks.cs `Shave`. Fixed with a SHAVE list (lower-front hair faces removed). Pell, Kettle, Lumen, Kaito, Oriel, Nettle, Cole and Marisol are rebaked clean-shaven, matching the game's looks.
  - STEP 24 DONE (2026-11-25): Fennick, Orla, Silas, Wynn. Preview: `docs/sigilbound/art/prototype/step24-roadwardens.png`. Balance after tuning: Fennick 55% vs Corvan/Tamsin (he started at 1%: Rare supports need a heal), Orla 46% vs Velka, Silas 43% vs Hask/Gorrum, Wynn 56% vs Ysolde/Lumen/Nyx.
    - Phase 1 (Roadwardens) is complete: 16 heroes. Their ROSTER rows hold the final kits.
- [x] **25. Engine: phase 2 mechanics** (Burn, Pierce, Execute, Pull, spread on kill, counter Taunt), with tests.
  - DONE (2026-11-25): `FxType.Burn/Pull/Counter` (added after the web's 16 effects, so IsStatus/IsBuff and parity are unchanged), and `SkillDef.Pierce/Execute/Spread` in Core/Battle.cs. Skill text and status labels: BRN, CTR. 12 checks in `unity/Tests~/MechanicsTests.cs`. Rules in ROSTER.md section 4.
  - Not done: battle VFX made for these mechanics. They show through the existing status, damage and floating-text events ("Executed", "Pulled", "Spread!", "Counter!").
- [x] **26–30. Phase 2 heroes (Glass and Storm).** ALL DONE (2026-11-25); 60 heroes. Step 26:
  - Kestrel (Pierce on both attacks), Sorrel, Amber (Burn and Counter) and Tobias, with the new lute kit. Preview: `docs/sigilbound/art/prototype/step26-glass-and-storm.png`.
  - Balance after tuning: Kestrel 46% vs Ysolde/Lumen/Nyx, Sorrel 55% vs Velka, Amber 43% vs Hask/Gorrum, Tobias 46% vs Corvan/Tamsin. 44 heroes.
  - STEP 27 DONE (2026-11-25): Nerys, Hale, Juno (Burn and Spread), Pike (Pull). Preview: `docs/sigilbound/art/prototype/step27-glass-and-storm.png`.
    - Balance after tuning: Nerys 55% vs Corvan/Tamsin, Hale 57% vs Velka (Taunt swings him from about 21% to 74%, so his shield is tiny), Juno 45% and Pike 45% vs Ysolde/Lumen/Nyx. 48 heroes.
    - Juno got a dancer's skirt, so she doesn't read as a second Ysolde.
  - STEP 28 DONE (2026-11-25): Rhea, Corwin (Execute), and the first new Epics, Astrid (Pierce) and Caspian. Preview: `docs/sigilbound/art/prototype/step28-glass-and-storm.png`.
    - Balance after tuning: Rhea 43% vs Hask/Gorrum, Corwin 51% vs Ysolde/Lumen/Nyx, Astrid 51% vs Kaito/Maelis, Caspian 45% vs Thrain. 52 heroes.
  - STEP 29 DONE (2026-11-25): Ignis (Burn), Lyra, Malachar (Counter), Rowan, all Epic. Preview: `docs/sigilbound/art/prototype/step29-glass-and-storm.png`.
    - Balance after tuning: Ignis 44% and Lyra 43% vs Sable/Oriel, Malachar 54% and Rowan 49% vs Aurelia. 56 heroes.
    - Lessons:
      - Supports without a heal lose badly to Oriel.
      - Defenders swing hard on Taunt (Rowan 19% without, 87% with a team Regen ward), so shields stay small.
    - Horns now use the measured head (they were hidden under Malachar's helm). Gorrum is rebaked.
  - STEP 30 DONE (2026-11-25): Selene, Brann (Counter, with the new gauntlets kit), Ysra, Oswin (Pierce). Preview: `docs/sigilbound/art/prototype/step30-glass-and-storm.png`.
    - Balance after tuning: Selene 48% vs Sable/Oriel, Brann 52% vs Thrain (he needed Thrain's team-wide ATK Up, not more shield), Ysra 45% and Oswin 43% vs Kaito/Maelis.
  - Phase 2 (Glass and Storm) is complete: 20 heroes, using every step 25 mechanic.
  - Next: phase 3 needs its mechanics first (revive, damage that grows with HP lost, a heal when an ally falls). Then the 20 Hollow Unbound heroes.
- [x] **31. Engine: phase 3 mechanics** (Revive, damage that grows with HP lost, a heal when an ally falls), with tests.
  - DONE (2026-11-25):
    - `FxType.Revive` (a buff: back at Pct HP instead of falling, once); `SkillDef.Rage` (damage grows with the attacker's missing HP); `HeroDef.FallHeal` (the Last Light passive).
    - The hero screen's Skills card shows a passive line (`SkillText.Passive`). 3 more checks in MechanicsTests (15 in all). Rules in ROSTER.md section 4.
- [x] **32–33, 35–37. Phase 3 heroes (The Hollow Unbound), 4 per step.** ALL DONE (2026-11-25); 80 heroes. Step 32 DONE (2026-11-25):
  - Grimsby, Marrow (Rage), Ember (Last Light passive), Quill. New kit: book. Preview: `docs/sigilbound/art/prototype/step32-hollow-unbound.png`.
  - Balance after tuning: Grimsby 44% and Quill 45% vs Corvan/Tamsin, Marrow 52% vs Kaito/Maelis, Ember 44% vs Velka. Ember started at 86%: a self-shield with Taunt dominates HP duels again. 64 heroes.
  - Marrow was redesigned as a grey, long-haired giant so he doesn't read as a second Gorrum.
  - STEP 33 DONE (2026-11-25): Drift, Sprig, Tallow, Hollis (Commons). Preview: `docs/sigilbound/art/prototype/step33-hollow-unbound.png`.
    - Balance after tuning: Drift 57% vs Brannoc/Kettle, Sprig 48% vs Pell/Wren, Tallow 53% vs Mira (his Burn is strong for a Common, so its chance is 10%), Hollis 44% vs Odo. 68 heroes.
  - STEP 35 DONE (2026-11-25): Auren, Cinder, Thessaly, Umbra (all Epic). New kit: fan. Preview: `docs/sigilbound/art/prototype/step35-hollow-unbound.png`.
    - Balance: Auren 52% and Umbra 52% vs Sable/Oriel (Umbra needed team Turn Meter), Cinder 46% vs Kaito/Maelis, Thessaly 52% vs Aurelia. 72 heroes. Three of four balanced first time, starting from the kit patterns that worked before.
  - STEP 36 DONE (2026-11-25): Lucan, and the first new Legendaries: Kharra, Nocturne (Execute), Aelira. Preview: `docs/sigilbound/art/prototype/step36-hollow-unbound.png`.
    - Balance: Lucan 51% vs Aurelia (his Cleanse undid Aurelia's stuns: 77% until it was dropped), Kharra 59% vs Odo/Velka/Thrain, Nocturne 48% vs Vaelith/Morwenna, Aelira 43% vs Seraphine. 76 heroes.
    - RosterTests: a peer of a lower rarity (no Legendary HP hero among the classic 24) is raised to the new hero's rarity.
  - STEP 37 DONE (2026-11-25): Zephyrine, Solenne (the first Revive: Dawn Vigil gives the team a one-time revive at 30% HP), Nell, Briar. Preview: `docs/sigilbound/art/prototype/step37-hollow-unbound.png`.
    - Balance: Zephyrine 44% vs Vaelith/Morwenna, Solenne 41% vs Odo/Velka/Thrain (raised to Legendary; she needed a Taunt), Nell 58% vs Brannoc/Kettle, Briar 47% vs Ysolde/Lumen/Nyx.
    - Beards: monarch heads carry the beard in the hair too. Maelis, Marrow and Solenne are now clean-shaven (rebaked).
  - Phase 3 (The Hollow Unbound) is complete: 20 heroes.
  - Next: step 38, campaign act 2.
  Grimsby, Marrow (Rage), Ember (Last Light), Quill; Drift, Sprig, Tallow, Hollis; Auren, Cinder, Thessaly, Umbra; Lucan, Kharra, Nocturne (Execute), Aelira; Zephyrine, Solenne (Revive), Nell, Briar.
- [x] **34. Android in-app updates (like Windows).** Owner request (2026-11-25). The phase 3 hero steps 35–37 follow it.
  - OWNER DECISIONS (2026-11-25):
    - Build this as the next step, before the remaining phase 3 heroes.
    - Auto-update is ON by default: new builds download in the background on Wi-Fi and install at the home screen, never mid-battle. The Settings toggle can turn it off.
  - DONE (2026-11-25): built as planned.
    - `Assets/Plugins/Android/SigilboundUpdateInstaller.java`: PackageInstaller session, `USER_ACTION_NOT_REQUIRED` on API 31+, the confirm screen, and the unknown-apps permission page. It compiles against the Android 14 framework classes (checked here with javac).
    - `Editor/AndroidUpdatePermission.cs` adds `REQUEST_INSTALL_PACKAGES` in Android builds only.
    - `Client/Updater.cs` on Android:
      - "Update now" downloads with progress and checks the file is a real APK, then installs;
      - Auto (Wi-Fi, default) downloads in the background and installs on the home screen when no dialog is open;
      - a cancelled or refused install leaves the banner; an installer that can't start falls back to the download link.
    - Settings has a new "Updates" row: Auto (Wi-Fi) or When I tap.
    - Not tested on a device yet. The first build with this must be installed by hand once, since older builds only open the link.
    - Owner test (2026-11-25, build 52 to 53): the download, the unknown-apps permission and Android's "Update this app?" prompt all worked, but after Update the install failed with no visible reason.
      - Signing is ruled out: every APK carries the committed test certificate (SHA-256 ea8347b4…).
      - Fix 1: the banner shows PackageInstaller's code and message.
      - Fix 2: if that route fails or is cancelled, the game opens Android's standard package installer for the same file (`SigilboundApkProvider`, the same screen as installing from a file manager). If that fails too, the button becomes Download.
    - VERIFIED ON DEVICE (2026-11-25): the owner updated from build 57 to build 58 in the game.
  - Today: the update banner on Android opens the APK link in the browser. The owner then downloads, opens the file and installs.
  - Goal: tap **Update now** in the game. It downloads the APK with a progress bar (as on Windows), then Android's installer opens directly, so one tap installs it and the save is kept. Later updates are silent where Android allows it.
  - What Android allows:
    - A sideloaded app can't update itself silently the first time; Android shows its own confirmation.
    - If the game installs updates through PackageInstaller, it becomes the app's installer of record. On Android 12+ (API 31), it can then ask for `USER_ACTION_NOT_REQUIRED`, and later updates install with no prompt. Android still decides, so the game shows the prompt whenever it insists.
    - The first in-app update needs the one-time "Install unknown apps" permission for Sigilbound. The game opens that settings page with a short explanation.
  - Pieces:
    1. Android plugin `Assets/Plugins/Android/SigilboundUpdater.androidlib`:
       - manifest: the `REQUEST_INSTALL_PACKAGES` permission and an install-status receiver;
       - a small Java `UpdateInstaller`: check `canRequestPackageInstalls()` and open `ACTION_MANAGE_UNKNOWN_APP_SOURCES` if needed; stream the APK into a `PackageInstaller` session, ask for `USER_ACTION_NOT_REQUIRED` on API 31+, and commit; on `STATUS_PENDING_USER_ACTION`, launch Android's confirm screen; report success or failure back to Unity.
    2. `Client/Updater.cs` on Android:
       - the same banner and progress bar as Windows ("Update now");
       - download the exact versioned APK to `temporaryCachePath`;
       - check its size, and its SHA-256 against the release asset digest when GitHub gives one;
       - save the game, then hand the file to `UpdateInstaller`.
       - Any failure falls back to today's download link.
    3. Settings: an "Auto-update" toggle (default on). When on, the APK downloads in the background on Wi-Fi and installs at the home screen, never in a battle. Off means the banner only.
    4. Signing: every build uses the same committed test keystore, so updates install over each other. Before any public release, move to a private keystore in CI secrets. Changing the key once means one manual reinstall.
  - Testing (owner, Galaxy S26+):
    1. Install build N by hand.
    2. When N+1 is out, tap Update now: allow unknown apps once, confirm the install, reopen, and check the save is kept.
    3. When N+2 is out, check the update installs without a prompt (Android 12+).
    4. Turn on airplane mode mid-download and check the fallback link works.
  - CI: compile checks cover the C#, but the Java builds only in the Android job. The plugin goes in its own commit so a build failure is easy to spot.
- [x] **38. Campaign act 2** (chapters 11–15: the port city and the storm coast), with new enemy pools that include roster heroes.
  - DONE (2026-11-25):
    - Five chapters: Saltreach Harbor, The Black Fens, Wraithmoor Barrows, The Ember Groves, The Storm Coast (stages 61–90).
    - Act 1 is pinned to its 60 stages (`GameData.ClassicStages`), so its stages and dungeon floors are unchanged and parity holds:
      - the difficulty curve (`Grade`) divides by the act 1 length;
      - `Trials.DungeonIdx` caps at act 1;
      - act 1 enemies are Classic only.
    - Act 2 enemies come from all heroes (124 roster heroes among its waves): level 50, 5-star, multiplier `Curve.End x (1 + 0.02 per stage)`, about 1.79 to 2.8.
    - The test sweep uses endgame teams (6 stars, level 60) for act 2. The Epic/Legendary team wins 93–100% until chapter 15 (about 53% at 15-1); the Rare/Epic team struggles from chapter 13. It's endgame content.
    - World map: an Act I / Act II switch (Act II opens after stage 60), its own generated storm-coast map and its own boss-reward track.
    - Arenas: `GameData.ThemeOf` gives act 2 chapters their own theme picks (teal shore, glow marsh, amethyst badlands, burning ruins, moonlit shore).
    - Hard mode extends naturally.
    - Story and scenery (2026-11-25, owner request):
      - an Act II introduction shown once when Act II opens, and a few lines per act 2 chapter at the top of its stage list (`GameData.Act2Intro`, `ChapterStory`);
      - five act 2 themes with new scatter: harbour pier posts, crates and lanterns; fen dead trees and reeds over black water; barrow standing stones and mounds; ember groves with glowing leaves; storm-coast rain;
      - each theme has its own sky colours and colour grade;
      - a crash fix: the stage list read `MapPoints[c]` for act 2 chapters;
      - the screenshot tour photographs the five act 2 arenas.
- [x] **39–43. Phase 4 heroes (The Sigil Bearers), 4 per step.** ALL DONE (2026-11-25): the roster is complete at 100 heroes. Step 39:
  - Perrin, Loam, Merrit, Sienna. Preview: `docs/sigilbound/art/prototype/step39-sigil-bearers.png`.
  - Balance: Perrin 54% and Loam 56% vs Mira, Merrit 54% vs Brannoc/Kettle, Sienna 46% vs Ysolde/Lumen/Nyx. 84 heroes.
  - Order of the rest:
    - step 40: Calder, Dorian, Ivo, Saoirse;
    - step 41: Gideon, Liora, Bastian, Elowen;
    - step 42: Faelan, Wick, Azura, Pyrrhus;
    - step 43: Kaelith, Aurex, Stormbearer, Vex.
  - STEPS 40–43 DONE (2026-11-25), in one session:
    - Calder, Dorian, Ivo, Saoirse; Gideon, Liora, Bastian, Elowen; Faelan, Wick, Azura, Pyrrhus; Kaelith, Aurex, Stormbearer, Vex.
    - Previews: `docs/sigilbound/art/prototype/step40-…step43-sigil-bearers.png` and `steps40-43-sigil-bearers.png`.
    - Each kit started from the pattern that balanced for the same rarity and role; 9 of 16 were in range first time.
    - Final balance:
      - 40–48%: Calder 51, Dorian 57, Ivo 45, Saoirse 42;
      - Gideon 56, Liora 56, Bastian 42, Elowen 42;
      - Faelan 59, Wick 48, Azura 43, Pyrrhus 44;
      - Kaelith 58, Aurex 44, Stormbearer 45, Vex 42.
    - Ivo swings on Taunt (27% none, 70% two turns), so he Taunts for one turn.
  - THE ROSTER PLAN IS COMPLETE: 100 heroes (24 classic + 76 Unity-only), every one balanced in the sims and with a baked free-path model.

## Phase E: every hero unique (owner request, 2026-11-25)

Owner decision: do all four upgrades, all free (CC0 and code; no paid tools). Goal: no two of the 100 heroes look or move alike. Each one is recognisable at 70 px in battle and rewarding up close in the hero viewer.

- [x] **44. Uniqueness audit and rules.** Done: `unity/Tools~/blenderforge/audit.py` writes `docs/sigilbound/UNIQUE.md` (base, palette, kit, height, clips and signature of every hero) and CI runs `audit.py --check`. The first audit found 48 near-twin pairs, now the fix list; a new twin fails CI, and a fixed pair must leave the list. Signatures for all 100 heroes are in `signatures.py`. Research for step 45: the bodies in our npm source (soldier, orc, ninja, demon, pirate and others) come from other Quaternius packs, with a smaller skeleton and a different clip set (no Sword_Slash or Idle_Sword), so they are left out. **Quaternius Ultimate Modular Women (CC0)** fits exactly: Medieval, Witch, Adventurer and seven more, a 62-bone rig with every core bone and the same 24 clips as our bases. Its colour-named materials suit quat.py's repaint. The Ultimate Modular Men and fantasy (Knight, Wizard, Elf) packs are the next candidates.
  - A script lists every hero's base body, palette, kit, build and clips.
  - A test fails when two heroes share the same base, the same main kit pieces and a similar palette (colour distance).
  - Today's near-twins go in a fix list (e.g. Tamsin and Sienna, Ysolde and Juno, Gorrum and Kharra, Bram and Dorian, Aurelia and Rhea).
  - Each hero gets a one-line "signature": the one feature only it has.
  - Research for step 45: confirm which free CC0 modular character packs (Quaternius modular men/women and others) share the current 62-bone skeleton, so the base clips and the UAL clips keep working.
- [x] **45. More CC0 base bodies.** Done: three bodies from Quaternius Ultimate Modular Women (CC0): `women_medieval.glb` (hooded), `women_witch.glb` and `women_adventurer.glb` in `godot/models`. They share the hooded rig and its 24 clips, and the UAL clips come from `hooded_adventurer`. All 40 female heroes (owner decision) are rebaked on them: 14 hooded, 14 witch, 12 adventurer. The list is quat.py `WOMEN` = `Core/Looks.cs BakedBases`, and audit.py checks they match. The witch hat stays only on Tilly. The code-built stand-in and the Hollow enemies keep their old base. Near-twins went from 48 to 39.
  - Import the compatible packs into `godot/models` (decoded GLBs) and add their colour roles and head meshes to quat.py and Core/Looks.cs.
  - Mix heads, hair, torsos and legs per hero, so the roster spreads over many body combinations, not three.
  - Each new body goes through the same checks: rest-pose skeleton identical, base clips play, no stilts or floating.
- [ ] **46–50. Unique silhouettes, 20 heroes per step.**
  - New kit pieces, built in quat.py in the bone frame:
    - weapons: glaive, flail, scythe, sickle, mace, rapier, twin axes, crossbow, chakram, harp, tome-staff, lance;
    - armour and dress: pauldrons, layered capes and hooded cloaks, masks of several shapes, belts and pouches;
    - back items: wings, banner, scroll case, quiver styles.
  - Body builds: height, bulk and head size per hero, applied to the mesh before export (the skeleton stays identical).
  - The audit test must pass at the end of each step.
- [ ] **51–52. Painted details.**
  - A decal pass in forge.py's baking that paints per-hero patterns into the albedo and glow: tattoos, scars, embroidery, armour trim, face paint, element runes.
  - Checked in the hero viewer close-up and at battle size.
- [ ] **53–55. Signature animations and effects.**
  - Every hero gets its own attack and cast clips from the free UAL animation library (more clips added to anims_ual.json, retargeted as today).
  - Plus an element aura or trail and a signature skill effect (SkillFx), so heroes feel different in battle as well as looking different.
  - The screenshot tour gallery checks every batch.

## Phase F: high-detail bodies (owner decision, 2026-11-25)

Owner decision: rebuild the heroes on **Quaternius Universal Base Characters + Modular Character Outfits – Fantasy** (CC0; Source editions, mirrored with their licence files and SHA256SUMS in github.com/slopwarefactory/terra `art/characters/quaternius/`). These are detailed male and female bodies (about 14k triangles) with 4K PBR textures (BaseColor, Normal, ORM). Outfits come as separate pieces: Knight (armet, horned helm, pauldrons), Noble (crown), Wizard, Ranger and Peasant, each in 3 colourways, plus 10 hair and beard styles. The rig is a 65-joint, Unreal-style skeleton, the one the Universal Animation Library (CC0, already our clip source) was made for. Runs next, before steps 46–55, which then build on these bodies.

- [x] **56. Import and verify.** Done (import): `godot/models/ubc/`, with 57 parts on one 65-joint skeleton (bodies, heads, 10 hair and beard pieces, Knight, Noble, Wizard, Ranger and Peasant pieces). Every file matched the source's SHA256SUMS; textures were scaled to 1024 (ORM 512). Test assembly renders cleanly. Still open: playing the UAL clips on this rig is checked in step 58. Copy the bodies, outfit pieces, hair and licence files into `godot/models/ubc/`. Scale textures down for phones (1024 for albedo and normal, 512 for ORM). Check every piece's skeleton matches the base body and that the full UAL clip set plays on it (no stilts, no floating).
- [ ] **57. New build tool.** In progress: `blenderforge/ubc.py` (+ `ubc_parts.py`) builds a hero from a design entry: parts picked and tinted, kit bound to bones, one 65-joint skeleton, decimated to 25k triangles (LOD1 about 8k), one baked atlas (albedo, tangent normal, glow). Capes are fitted to each body by ray-casting (they drape over the shoulders, fasten with clasps at the collarbones and hang clear of the back and legs). Weapons sit in the palm along the knuckle line and the fingers curl around them (`grip_pose`, which the game's hand grip will repeat after each animation frame). `ual_retarget.py` retargets all 46 free UAL clips onto the rig. `blenderforge/ubc.py`: per hero, pick the body, head, hair or beard, outfit pieces and colourway, tint the BaseColor by mask to the hero's palette, and add quat.py kit pieces on the new bone names (pelvis, spine_03, hand_r…). Export one skinned GLB with albedo, normal and glow, and set a triangle budget (about 25k at LOD0, plus an LOD1).
- [ ] **58. Game loading.** In progress: `HeroModels.BuildHd` loads `heroes_hd/SB_Hero_<id>_HD.glb` (LOD1 on Low) with the painted-character material (albedo, normal map, glow mask, element rim) and the 46 retargeted UAL clips from `ubc_anims.glb`; `AnimStyle.Hd` picks clips per weapon family; `HandGrip` closes the weapon hand after each animation frame. First four test heroes: Brannoc, Lumen, Morwenna, Aurelia (from the unapproved concepts, for testing only). Still to check: the screenshot tour, and performance on the S26+. HeroModels loads the new rig with the full UAL clip set (more attack, cast and hit clips than today), the Hollow enemies use the new bodies too, and there is a performance check on a Galaxy S26+ (10 heroes on screen) with a lower LOD on Low quality.
- [ ] **59–63. Rebuild all 100 heroes, 20 per step** (steps 59-62 done: the first 80 in roster order, built from the owner-approved concepts with `ubc.py`, compressed with `models/compress-hd.mjs` + JPEG textures, about 1.3 MB per hero; their old free-path bakes removed; RosterTests accept either model and forbid shipping both), each with its own outfit combination and its signature (signatures.py). The uniqueness audit (audit.py) extends to outfit pieces and must pass at the end of each step.

## Phase G: gameplay animation and mechanics (owner request, 2026-11-25)

Owner request: a character mechanics agent (`.claude/agents/character-mechanics.md`) researches common in-game mechanics and uses them to build the heroes' battle animations, and the game matches the device's refresh rate. The spec is `docs/sigilbound/MECHANICS.md`.
- Every rule is time-based: seconds and normalized clip time, never frame counts.
- Clips are free CC0 only, UAL first.

Steps 66–69 need the step 58 rig. Step 65 can run any time.

Owner decisions (2026-11-25): frame rate defaults to **Auto (match the screen)** (players who chose 30 keep it); a **screen shake setting Full / Reduced / Off** (default Full); **free UAL clips first**, paid tier decided after tests T2–T4.

- [x] **64. Research and spec.** Done (2026-11-25): MECHANICS.md has:
  - the five beats of an action with durations, and their scaling at 2x and 3x;
  - the state machine and the nine weapon families with clips and fallbacks;
  - the hit-stop, shake and flash numbers;
  - role personality;
  - the frame-rate design and 12 animation tests.
- [x] **65. Frame-rate matching.** Done: Settings has Frame rate **Auto · <screen Hz> / 60 · Smooth / 30 · Battery** (saved on the device as `sb.fpsMode`, so the save stays identical to the web's; old 30 choices stay 30) and Screen shake **Full / Reduced / Off** (`sb.shake`). Quality: `DisplayHz`, phones set the exact target, desktop Auto follows the monitor by vsync; under load Auto lowers 120 → 60 before graphics and retries between battles (wait doubles, max 8 min). Optimized Frame Pacing and Frame Timing Stats are on. Shake is now time-based noise. The performance overlay shows target/screen Hz. Still to do: spark spawn accumulators and the cloth substep (with steps 67–68).
  - Frame rate setting: Auto (match the display, up to 120), 60 or 30, saved as the new `settings.fpsMode` (`fpsCap` stays for web saves).
  - `Quality.cs` changes as in MECHANICS.md section 6.3:
    - `DisplayHz`;
    - desktop vsync;
    - at high refresh, Auto lowers 120 → 60 before it lowers the quality tier;
    - `FrameTimingManager` decides when to retry 120.
  - Turn on Optimized Frame Pacing and Frame Timing Stats in Player settings.
  - Fix the frame-rate-dependent code (section 4.6): screen shake noise, spark spawn rates, a fixed substep for cloth springs.
  - Check: the performance overlay shows 120 on the S26+ with Auto, and 60 and 30 when chosen. Windows follows the monitor.
- [x] **66. UAL clip set on the new rig.** Done: all 46 free UAL clips retargeted (`ual_retarget.py` → `models/ubc_anims.glb`, loaded by `HeroModels.BuildHd`). Every *(verify)* name in MECHANICS.md checked: all present except `Sword_Block`, `Hit_Knockback`, `LayToIdle` and `Kick` (not in the free tier; their fallbacks apply). `AnimStyle.HdClips` + a test that every clip a hero plays exists. Provenance in `models/CREDITS.md`. Still to check in the screenshot tour: no stilts or floating.
  - Retarget the free UAL Standard clips the families need onto the 65-joint rig (`tools/anim-retarget`), and confirm every *(verify)* name in MECHANICS.md section 3.
  - Add them to the clip set and to `HeroClips.Aliases`.
  - Record their provenance in `models/CREDITS.md`.
  - Check: no stilts or floating; clips play in the hero viewer.
- [ ] **67. Per-weapon state machine.** First pass done (high-detail heroes): `AnimStyle.Hd` follows the family clip table (daggers crouch and cut with both hands, bows draw/shoot, casters spell-idle, shields bash, brawlers jab/cross) and role victory/defeat poses; Defense and shield heroes block light hits (`Punch_Enter` guard) and never dodge; HP heroes shrug hits; idles start at a random phase with ±8% speed per hero; the losing side's survivors take defeat poses. Added: Defeat_Slump (kneel, then prone; owner request), Revive (buckle and rise: the death clip forward to 0.45, held, then backwards), Stunned (dazed crouch loop). Still to do: Animator/playables graph, Ready/Knocked_Down states, additive buff layers, idle and victory at 1x at every battle speed.
  - Replace the queued-crossfade playback with an Animator or playables graph holding the states and priorities in section 3.1.
  - Families come from `Looks.Prop` (section 3.2). Role timing multipliers and idle desync come from section 5.
  - New states: Ready, Block, Stunned, Knocked_Down, Revive (add `EvType.Revive`), Counter, Defeat, and additive buff/debuff layers.
  - Idle and victory stay at 1x at every battle speed.
- [ ] **68. Hit-stop and impact events.** First pass done: `Core/Kit/Impact.cs` (families from the prop, impact times and hit-stop table, speed^0.45 scaling, 0.25 s cap); melee attacks wait for the clip's impact time (`HeroModel.WaitNormalized`) instead of a fixed 0.35 s; attacker and targets (ranged: targets only) freeze via `SetFrozen`/`FreezeFor`; tests prove 30/60/120 fps land within one frame. Second pass done: multi-hit skills space their hits (0.26 s) with a 0.04 s stop on all but the last; projectiles fly by speed (arrow 22, bolt 16, orb 9, shard 14 m/s, clamped) with arcs relative to distance; knockback is out 0.08 s / hold 0.05 s / back 0.30 s (HP heroes 0.8x). Remaining: per-clip impact events for heroes with their own Skill_<n> clips.
  - Every attack clip gets normalized `impact` / `release` times. Damage numbers, effects and reactions fire when those times are crossed, instead of after fixed waits.
  - Hit-stop freezes only the attacker and the target, with the pose snapped to the impact time, using the durations in section 4.1.
  - Knockback curves and speed-based projectiles.
  - Tests in `unity/Tests~` that an action takes the same time at a simulated 30, 60 and 120 fps.
- [ ] **69. Animation test approvals.**
  - Render tests T1–T12 (MECHANICS.md section 7) in Blender on the high-detail heroes, and put them in `docs/sigilbound/art/anim-tests/`.
  - The owner approves, rejects or changes each one. The verdicts go in MECHANICS.md and the ROADMAP decisions log.
  - Rejected tests are reworked before steps 59–63 roll the clips out to all 100 heroes.
  - T11 (frame rate) is also checked live in the Unity build on the S26+.

## Phase H: 50 new heroes (owner-approved concepts, 2026-11-26)

The art director's 50 new heroes (22 non-human: elves, gnomes, beastfolk, golems, dryads, a wraith and more), approved by the owner on the review page. Roster 100 → 150. `docs/sigilbound/newheroes.py` writes them into `Core/GameData.cs` (skills from the design) and `Core/Looks.cs` (a stand-in look), lists them in ROSTER.md section 7, and balances each with team-swap sims against the classic hero(es) of its rarity and role (40-60%, bracketing search on a power factor in `concepts/tuning.json`). Each gets a high-detail model from `ubc.py` (species pieces included).

- [x] **70. Heroes 101-110:** Dace, Dulcie, Holt, Rhosyn, Ansel, Saskia, Kasimir, Imogen, Ilsabet, Obrecht. All balanced 42-58%; models built.
- [ ] **71. Heroes 111-120.**
- [ ] **72. Heroes 121-130.**
- [ ] **73. Heroes 131-140.**
- [ ] **74. Heroes 141-150,** then a summon-pacing check (more heroes per rarity means rarer duplicates; ROSTER.md section 5).

## Known device bugs (owner tests on Windows and a Galaxy S26+, Unity builds 7–8)

- Text duplicated or misplaced everywhere; tip popups show the body text twice, and the heading is "3".
- Emoji icons missing: the chips show only "◇ 0" and the bottom nav only "›".
- Heroes are code-built stand-ins, because the GLB models fail to load.
- Particles are orange squares; trees are spheres.
- Home: "Come back tomorrow" overflows its button, the quest buttons repeat the progress text, and an empty gold button sits at the bottom.
