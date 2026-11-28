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
    - The test sweep uses endgame teams (6 stars, level 60) for act 2. The Epic/Legendary team wins 93–100% until chapter 15 (about 53% at 15-1); the Rare/Epic team struggles from chapter 13. It's endgame content. _(Out of date after steps 97–98: by 2026-11-27 15-1 was 97% and 15-6 2% for the strong team. Step 99 retuned Act II; current numbers are in MECHANICS.md section 9.7.)_
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
- [x] **46–50. Unique silhouettes, 20 heroes per step.** Done 2026-11-26 on the high-detail bodies (all 151 rebuilt at once, pending owner review): owner audit of every placeholder, then modelled replacements in `blenderforge/kit_props.py` (about 70 weapon and focus types, 13 shield shapes; every design mapped deliberately), `kit_cloth.py` (cloth-simulated gowns weighted to the legs, varied hems and trains, matching bodices, capes over the gown, feathered/moth/petal/membrane/glider/helm wings), `kit_head.py` + `kit_acc.py` + `kit_geo.py` (crowns, circlets, halos, curved horns and antlers, species ears/beaks/tails/shells/fins/golem stone/wraith, hats, hoods, helms, masks, goggles, draped veils, pauldrons, necklaces, sashes, belts, bracers, hip and back items).
  - New kit pieces, built in quat.py in the bone frame:
    - weapons: glaive, flail, scythe, sickle, mace, rapier, twin axes, crossbow, chakram, harp, tome-staff, lance;
    - armour and dress: pauldrons, layered capes and hooded cloaks, masks of several shapes, belts and pouches;
    - back items: wings, banner, scroll case, quiver styles.
  - Body builds: height, bulk and head size per hero, applied to the mesh before export (the skeleton stays identical).
  - The audit test must pass at the end of each step.
- [x] **51–52. Painted details.** Done 2026-11-26: `blenderforge/kit_painted` pass (`kit_paint.py`, run by `ubc.bake()` after the albedo): baked ambient occlusion darkens crevices, Cycles pointiness lightens worn edges, a soft top light. The designs name few per-hero marks (one tattoo, a few sigils), so no invented decals were added.
  - A decal pass in forge.py's baking that paints per-hero patterns into the albedo and glow: tattoos, scars, embroidery, armour trim, face paint, element runes.
  - Checked in the hero viewer close-up and at battle size.
- [x] **53–55. Signature animations and effects.** Done 2026-11-26 (game side): every hero has a signature motif read from its approved design's fx line (`docs/sigilbound/sigfx.py` → `Core/Kit/HeroSignatures.cs`, 19 motifs: wind swirls, rising embers, splashes, bubbles, frost, petals, leaves, feathers, stars, lightning, smoke, wisps, bones, mud, runes, notes, poison, shards, motes). It plays on each of the hero's hits, big on ultimates (caster and every target), melee strikes leave an element trail on the weapon hand, and Legendary heroes carry a faint ambient aura (off at Low quality). Per-hero clips: the free UAL tier is used up (48 clips, assigned per weapon family, role and hero desync); more distinct clips would be hand-authored in `make_clips.py` (as Spear_Thrust, Hit_Kneel), not bought. SignatureTests guard full coverage and the spread.
  - Every hero gets its own attack and cast clips from the free UAL animation library (more clips added to anims_ual.json, retargeted as today).
  - Plus an element aura or trail and a signature skill effect (SkillFx), so heroes feel different in battle as well as looking different.
  - The screenshot tour gallery checks every batch.

## Phase F: high-detail bodies (owner decision, 2026-11-25)

Owner decision: rebuild the heroes on **Quaternius Universal Base Characters + Modular Character Outfits – Fantasy** (CC0; Source editions, mirrored with their licence files and SHA256SUMS in github.com/slopwarefactory/terra `art/characters/quaternius/`). These are detailed male and female bodies (about 14k triangles) with 4K PBR textures (BaseColor, Normal, ORM). Outfits come as separate pieces: Knight (armet, horned helm, pauldrons), Noble (crown), Wizard, Ranger and Peasant, each in 3 colourways, plus 10 hair and beard styles. The rig is a 65-joint, Unreal-style skeleton, the one the Universal Animation Library (CC0, already our clip source) was made for. Runs next, before steps 46–55, which then build on these bodies.

- [x] **56. Import and verify.** Done (import): `godot/models/ubc/`, with 57 parts on one 65-joint skeleton (bodies, heads, 10 hair and beard pieces, Knight, Noble, Wizard, Ranger and Peasant pieces). Every file matched the source's SHA256SUMS; textures were scaled to 1024 (ORM 512). Test assembly renders cleanly. Still open: playing the UAL clips on this rig is checked in step 58. Copy the bodies, outfit pieces, hair and licence files into `godot/models/ubc/`. Scale textures down for phones (1024 for albedo and normal, 512 for ORM). Check every piece's skeleton matches the base body and that the full UAL clip set plays on it (no stilts, no floating).
- [x] **57. New build tool.** Done 2026-11-26 (all 151 heroes + the Titan build cleanly with the full kit). In progress: `blenderforge/ubc.py` (+ `ubc_parts.py`) builds a hero from a design entry: parts picked and tinted, kit bound to bones, one 65-joint skeleton, decimated to 25k triangles (LOD1 about 8k), one baked atlas (albedo, tangent normal, glow). Capes are fitted to each body by ray-casting (they drape over the shoulders, fasten with clasps at the collarbones and hang clear of the back and legs). Weapons sit in the palm along the knuckle line and the fingers curl around them (`grip_pose`, which the game's hand grip will repeat after each animation frame). `ual_retarget.py` retargets all 46 free UAL clips onto the rig. `blenderforge/ubc.py`: per hero, pick the body, head, hair or beard, outfit pieces and colourway, tint the BaseColor by mask to the hero's palette, and add quat.py kit pieces on the new bone names (pelvis, spine_03, hand_r…). Export one skinned GLB with albedo, normal and glow, and set a triangle budget (about 25k at LOD0, plus an LOD1).
- [ ] **58. Game loading.** Download size (2026-11-26): the APK had reached 295 MB, almost all of it the high-detail heroes, because Unity stores StreamingAssets uncompressed. Now every build goes `ubc.py` → `node models/compress-hd.mjs DIR` → `python3 models/compress-textures.py DIR` (albedo JPEG q82, normal maps 768 px q88, each marked so it is never re-compressed), and `prepare-models.mjs` gzips the hero GLBs and the clip file into StreamingAssets (lossless, as `.glbz`: Android's packaging un-gzips files named `.gz`, so build 102 shipped them full size; `HeroModels.ReadBytes` unzips `.glbz`). Positions and skin weights stay float. `ApkSizeTests` guards the budget. In progress: `HeroModels.BuildHd` loads `heroes_hd/SB_Hero_<id>_HD.glb` (LOD1 on Low) with the painted-character material (albedo, normal map, glow mask, element rim) and the 46 retargeted UAL clips from `ubc_anims.glb`; `AnimStyle.Hd` picks clips per weapon family; `HandGrip` closes the weapon hand after each animation frame. First four test heroes: Brannoc, Lumen, Morwenna, Aurelia (from the unapproved concepts, for testing only). Screenshot tour checked (heroes and Hollow copies animate, packed models load; APK 163 MB). Low quality now skins with 2 bones per vertex and drops the hero normal maps. Still to check: performance on the S26+ (owner device). HeroModels loads the new rig with the full UAL clip set (more attack, cast and hit clips than today), the Hollow enemies use the new bodies too, and there is a performance check on a Galaxy S26+ (10 heroes on screen) with a lower LOD on Low quality.
- [x] **59–63. Rebuild all 100 heroes, 20 per step** (done 2026-11-26: all 100, in roster order, built from the owner-approved concepts with `ubc.py`, compressed with `models/compress-hd.mjs` + JPEG textures, about 1.3 MB per hero; their old free-path bakes removed; RosterTests accept either model and forbid shipping both), each with its own outfit combination and its signature (signatures.py). The uniqueness audit (audit.py) extends to outfit pieces and must pass at the end of each step.

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
- [x] **67. Per-weapon state machine.** First pass done (high-detail heroes): `AnimStyle.Hd` follows the family clip table (daggers crouch and cut with both hands, bows draw/shoot, casters spell-idle, shields bash, brawlers jab/cross) and role victory/defeat poses; Defense and shield heroes block light hits (`Punch_Enter` guard) and never dodge; HP heroes shrug hits; idles start at a random phase with ±8% speed per hero; the losing side's survivors take defeat poses. Added: Defeat_Slump (kneel, then prone; owner request), Revive (buckle and rise: the death clip forward to 0.45, held, then backwards), Stunned (dazed crouch loop). Second pass done: Ready (the family stance when the turn starts: bows aim, brawlers raise fists, supports gather, others lean in at 1.1x), Knocked_Down (stuns from heavy weapons, the Titan or ultimates: thrown back 0.6 m, down, held 0.6 s, up into the dazed loop), idle/Ready/Stunned loops at 1x at every battle speed (victory already plays at 1x). Third pass done (2026-11-26): Counter (the core's `Counter!` event: the holder turns to the attacker in 0.10 s and strikes in place at 1.3x; a test pins the event order the client relies on) and additive buff/debuff posture (`Client/PostureLayer.cs`: chest up 4° on buffs, shoulders down 4° on debuffs as 0.4 s pulses when they land, a 40% hold while they last, a faint tremor under poison or burn; LateUpdate on spine and clavicles only, so HandGrip, hit-stop and the 1x idle loops are untouched). **Animator/playables graph: not done, by decision.** Every state in section 3.1 now runs on the legacy Animation path (crossfades, clamp holds, reversed playback for Revive and Knocked_Down, speed control for hit-stop and battle speed) plus procedural layers, and the priority order is enforced by CharacterView's busy flag. A Mecanim or playables rewrite would need the 150 high-detail heroes re-imported as Humanoid or a hand-built graph, risks the HandGrip and hit-stop timing that now works, and adds nothing the players would see. Revisit only if blended locomotion or many simultaneous layers are needed.
  - Replace the queued-crossfade playback with an Animator or playables graph holding the states and priorities in section 3.1.
  - Families come from `Looks.Prop` (section 3.2). Role timing multipliers and idle desync come from section 5.
  - New states: Ready, Block, Stunned, Knocked_Down, Revive (add `EvType.Revive`), Counter, Defeat, and additive buff/debuff layers.
  - Idle and victory stay at 1x at every battle speed.
- [x] **68. Hit-stop and impact events.** Closed 2026-11-26: the last item (per-clip impact events for heroes with their own Skill_<n> clips) no longer applies: no high-detail hero has Skill clips, and the family impact times cover every hero. First pass done: `Core/Kit/Impact.cs` (families from the prop, impact times and hit-stop table, speed^0.45 scaling, 0.25 s cap); melee attacks wait for the clip's impact time (`HeroModel.WaitNormalized`) instead of a fixed 0.35 s; attacker and targets (ranged: targets only) freeze via `SetFrozen`/`FreezeFor`; tests prove 30/60/120 fps land within one frame. Second pass done: multi-hit skills space their hits (0.26 s) with a 0.04 s stop on all but the last; projectiles fly by speed (arrow 22, bolt 16, orb 9, shard 14 m/s, clamped) with arcs relative to distance; knockback is out 0.08 s / hold 0.05 s / back 0.30 s (HP heroes 0.8x). Remaining: per-clip impact events for heroes with their own Skill_<n> clips.
  - Every attack clip gets normalized `impact` / `release` times. Damage numbers, effects and reactions fire when those times are crossed, instead of after fixed waits.
  - Hit-stop freezes only the attacker and the target, with the pose snapped to the impact time, using the durations in section 4.1.
  - Knockback curves and speed-based projectiles.
  - Tests in `unity/Tests~` that an action takes the same time at a simulated 30, 60 and 120 fps.
- [x] **69. Animation test approvals.** Done 2026-11-26: the owner approved every test; T3 (spear thrust), T8 (heavy hit to one knee) and T10 (defeat slump) were reworked per their notes first.
  - Render tests T1–T12 (MECHANICS.md section 7) in Blender on the high-detail heroes, and put them in `docs/sigilbound/art/anim-tests/`.
  - The owner approves, rejects or changes each one. The verdicts go in MECHANICS.md and the ROADMAP decisions log.
  - Rejected tests are reworked before steps 59–63 roll the clips out to all 100 heroes.
  - T11 (frame rate) is also checked live in the Unity build on the S26+.

- [x] **Titan cinematics (owner request 2026-11-26).** The HD Titan stands at `BattleLayout.TitanSlot()` (1.2 m behind the far rim) with its head at `TitanHead` 4.8 m (heroes 1.85). Titan-only views (`BattleLayout.TitanCameras`: Worm's-eye default, Side duel, Titan's shoulder) are solved per aspect (`TitanView`: a rail and the closest distance that keeps the team in frame and the Titan's head and tag under `TitanCeiling`); the View cycle in Titan fights is those three then the usual five, remembered in PlayerPrefs `sb.titanCam` (not the save). The camera is a rig in `Arena` (framed pose + breathing tilt + cinematic override + `Vfx.ShakeOffset`). Arrival intro, Earthsplitter (fight-ending blow) and Meteor Leap (Titan ultimate) live in `BattleController`; presentation only. TitanCameraTests: every view and aspect, Titan at least 2x a hero on screen in its own views.

## Phase H: 50 new heroes (owner-approved concepts, 2026-11-26)

The art director's 50 new heroes (22 non-human: elves, gnomes, beastfolk, golems, dryads, a wraith and more), approved by the owner on the review page. Roster 100 → 150. `docs/sigilbound/newheroes.py` writes them into `Core/GameData.cs` (skills from the design) and `Core/Looks.cs` (a stand-in look), lists them in ROSTER.md section 7, and balances each with team-swap sims against the classic hero(es) of its rarity and role (40-60%, bracketing search on a power factor in `concepts/tuning.json`). Each gets a high-detail model from `ubc.py` (species pieces included).

- [x] **70. Heroes 101-110:** Dace, Dulcie, Holt, Rhosyn, Ansel, Saskia, Kasimir, Imogen, Ilsabet, Obrecht. All balanced 42-58%; models built.
- [x] **71. Heroes 111-120:** balanced 43-58% (Hilde's Plant the Pavise trimmed: no DefUp, 5-turn cooldown, 1-turn taunt; True North 6 turns).
- [x] **72. Heroes 121-130:** balanced (Morvoren's self-taunt trimmed to 1 turn).
- [x] **73. Heroes 131-140:** balanced 43-58%.
- [x] **74. Heroes 141-150:** balanced 41-57%. Summon pacing checked: promotion uses any heroes of the same star level as fodder (`Game.RankUpFodder`), not duplicates, so a bigger roster does not slow promotion; only collecting one specific hero gets slower (e.g. 1 in 24 legendaries instead of 1 in 16), which needs no reward change. Models for heroes 131-150 follow the current rebuild.

## Known device bugs (owner tests on Windows and a Galaxy S26+, Unity builds 7–8)

- Text duplicated or misplaced everywhere; tip popups show the body text twice, and the heading is "3".
- Emoji icons missing: the chips show only "◇ 0" and the bottom nav only "›".
- Heroes are code-built stand-ins, because the GLB models fail to load.
- Particles are orange squares; trees are spheres.
- Home: "Come back tomorrow" overflows its button, the quest buttons repeat the progress text, and an empty gold button sits at the bottom.

## Phase I: polish, feel and release (owner choices, 2026-11-26)

The owner picked all four next-phase tracks after phases A-H; each ran as its own branch and merged after checks.

- [x] **75. Model polish.** Done 2026-11-26: design-matched bodices for the 31 gowned heroes (not the shared corset), cloth-draped sleeves, masks fitted to the face, animal muzzles and beaks that blend into the face, thicker small props, softer veils; then all 151 rebuilt for owner review.
- [x] **76. Signature ultimates.** 11 hand-made clips in `make_clips.py` (LeapSlam, Whirlwind, Overhead, DashStrike, Uppercut, GroundPound, Channel, Volley, Twirl, Blessing, TwinFlurry), each with an impact time; assigned to all 66 Legendary and Epic heroes by `docs/sigilbound/ultfx.py` (`Core/Kit/HeroUlts.cs`).
- [x] **77. Release prep.** First-time tutorial (PlayerPrefs, skippable, replayable), "Send feedback" report (device, build, recent errors; clipboard + file), app icon and store art, `docs/sigilbound/store/` (privacy policy draft, listing text, Play Console guide). Store build: `BuildScript.AndroidStore` (AAB, private upload key, `SB_STORE` leaves the in-app updater out) and the manual CI task `build-store-aab`. Waiting on the owner: package name, contact email, Play developer account, upload key secrets.
- [x] **78. Sound and music.** 38 procedural sounds (per weapon family, per element, statuses, Titan, UI, summon stings) and menu / Titan / victory music (`unity/Tools~/audio/`), wired by family and element; normal battles keep the CC0 battle theme. Owner listening check pending.
- [x] **79. Titan: scale, cameras and moves.** HD Titan at its real 6.24 m head height; Titan-only views (worm's-eye = mock-up option C with the tag above the horns, side duel, over its shoulder) plus the breathing tilt; arrival intro; Titan practice (no rewards, no try used); its three moves each with their own animation and camera (Crushing Palm: stride, over-the-victim's-shoulder slow-motion windup, slam; Quake: the Meteor leap with lens-only zoom and aftershocks; Cinder Breath: fire stream, lava pools, burning heroes, over-the-shoulder and side-on shots); the Earthsplitter on any fight-ending blow. Owner-approved via animated mock-ups.
- [x] **80. Interface art and readability.** Painted buttons from the owner's Grok Imagine art (`Skin.cs`, `Tools~/skin/button.py`, Settings toggle), hero portrait support (`Portraits.cs`, `Tools~/skin/portrait.py`), owner-approved status icons with a tap-for-legend (`StatusIcons.cs`). Remaining Grok pieces (blue/chip buttons, panel, card, banner, icon sheet, backgrounds, portraits) arrive as the owner makes them.
- [ ] **81. Owner device checks.** Performance on the S26+ (10 heroes, Titan), the sounds and music by ear, the Titan moves at 1x and 3x, and the first-time tutorial on a fresh install. The screenshot tour logs an `SBBUDGET` line (visible renderers, triangles, skinned meshes, live particles, lights, playing sounds) at battle-mid and each Titan shot, to compare against what the S26+ handles.
- [x] **82. Painted battlefields.** Art-director pass (`docs/sigilbound/art/SCENERY.md`): 42 baked-shaded props in the heroes' painted style (`Resources/Scenery/props.bytes`, `SceneryKit.cs`), one colour ramp per theme, painted sky, three ridge layers, painted ground and water (`SceneryPaint.cs`), per-theme fog. Boss stages and the Titan react (`ArenaReact.cs`): swaying foliage, braziers that flare on kills, theme vents, falling rocks, lightning, warlord cracks. Follow-up fix after the in-game check: backdrop cards close off the high cameras, low-gloss painted water, crusted Titan lava.
- [x] **83. Spell effects overhaul.** Procedural flipbook fire and smoke, rune circle, shock ring, rays, flare, caustics, foam, lava, crust, cracks, rock chunks and leaves (`FxTextures.cs`); per-element looks on casts, impacts, area spells, beams and trails; four projectile heads; crits 1.55x with rays and debris; rate-based emission with per-tier particle caps; Titan breath, lava pools and cracks rebuilt.
- [x] **84. Real battle sound.** Recorded CC0 sources (Kenney packs, field recordings): every hit is the weapon layer plus the defender's armour layer (`HeroArmour`: cloth, leather, mail, plate); the Titan's grinding steps, cave roar, debris slams and a stereo-sweeping breath; stereo panned to screen position (`Sound.PlayAt`); arena ambience beds per theme, including a caldera bed for the Titan. Owner listening check pending (step 81).
- [x] **85. Story scenes.** Each chapter is a journey (owner direction and approval 2026-11-26; `docs/sigilbound/art/STORY_SCENES.md`): its six stages are stops on a route through the chapter's world, the landmark ahead (drawn darker on the horizon), and the boss's lair at stage 6. Data in `Core/StoryScenes.cs` (`StageSet[chapter][stage]`: stop name, mood, pieces), read by `Scenery.Build` from the stage index; set-pieces built in Blender (`Tools~/scenery/story_pieces.py`, `make_landmarks.py`) into per-act packs loaded on demand (`story_act1.bytes`, `story_act2.bytes`, 784 KB); lair parts animated by `ArenaReactor` with lair sound cues (`ArenaEvents.LairCue`). Own themes for The Glass Wastes (glass desert) and Stormcrown Heights (crag); act 1 story lines; stop names in the stage list; Hard mode by night. Core tests in `Tests~/StoryScenesTests.cs`.
- [x] **86. Lair reveal.** Owner chose option C of the lair-size mock-ups: full-size lair landmarks and a 2.5 s skippable arrival camera on campaign boss stages (low behind the team, up at the landmark against the sky, then into the battle view), with a gold title card and the `lairReveal` swell. Once per lair per session. `StoryScenes.RevealOf`, `LairRevealTests`.
- [x] **87. Menu fixes (owner screenshots).** Covered buttons ignore taps (`Ui.Cover`/`OnTop`; the open stage list no longer leaks taps to the map); `Ui.Round` draws rounded rects 9-sliced so frames stay crisp (44 call sites); readable stop names, whole enemy names and no text under the Replay button in stage rows; redesigned victory/defeat cards (crest or portrait, level up, XP bar, MVP crown, labelled stats).
- [x] **88. Stand-ins and preloading.** The block stand-in stays hidden while a hero's HD model loads (shown only if the load is slow or fails); the team, starters and today's Titan preload at launch and a stage's enemies when its prep screen opens; the Titan intro waits for its model.
- [x] **89. Hero portraits.** 150 bust renders of the shipped HD models on element backdrops (`Resources/Portraits`, `Tools~/portraits/`), owner-approved; Grok portraits replace them per hero. The portrait check found floating magic crowns and halos cutting through hats: fixed in `kit_head.py` for 14 heroes; Morwenna and Lucan (and a roster-wide placeholder-block check) in a follow-up.
- [x] **90. Scene budget probe.** The screenshot tour logs `SBBUDGET`, `SBBUDGET-SPLIT` (heroes / scenery / other) and `SBBUDGET-TOP` at battle and Titan shots, counting batched meshes once. Typical battle on Ultra: about 155k triangles (heroes ~25k each, scenery 3-10k), Titan about 100k.
- [x] **91. Owner UI art.** Grok/Google plates processed by `Tools~/skin/plate.py` and `sheet2.py`: blue buttons, chips, ornate panel/card frames (`Skin.DrawFrame`, at most half size and drawn around the content after the in-game check), bronze small cards, ribbon banners (`Ui.Banner`), painted icons in the currency bar, bottom nav and mode cards (`Icons.Painted`), the painted Act I world map with its own chapter points, the summon altar backdrop and the citadel title screen. All behind the Settings skin switch. Act II map art requested from the owner.
- [x] **92. Painted portraits.** 150 hand-painted portraits from the owner's 17 Google AI batch sheets (3x3 grids from `portrait_batches.txt`), sliced and matched by batch order; the 3D bust renders remain the fallback for heroes without one.
- [x] **93. Music and sound polish.** Boss, Act II and campaign-map themes (generated, `make_music.py`), the battle theme cut to a seamless loop, boss-lair cues (bell, wheel, pyre, beam, snuff, thunder, chime, drip, shard) and the lair-reveal swell.
- [ ] **94. Six new heroes.** Vorgrim Ashscale, Talvo Brassfang, Wystan Quillmere, Crusk Brineclaw, Hartwin Sunhorn, Nyssa Duskwing from the owner's concept sheets: kits and balance approved (normal summon pool); models in a second pass (beast heads and bodies, white-gold plate, tattered moth wings) for owner approval. All six are now image-to-3D (TRELLIS) meshes rigged by `blenderforge/i3d_rig.py`; Crusk regenerated and Nyssa added from fal.ai `fal-ai/trellis` (Nyssa's moth wings rigid on `wing_l`/`wing_r` bones that RigExtras flutters). Branch `feat/new-heroes-6`.
- [ ] **95. Phase H outfit rebuild.** The 50 phase H heroes named outfit parts with a slash, so their slots fell back to peasant parts; rebuild with the intended parts, before/after for owner approval.
- [x] **97. Combat rules 1-3** (owner request after a combat review). Enrage from round 10 (+10% damage, -10% healing and shields per round; not in the Titan fight), shields x0.8, auto-battle skips utility skills that would do nothing and finishes killable enemies (healer-first focus and an area-attack preference were tried and dropped: they reshuffled hero balance), Poison/Burn on campaign bosses capped at 3x the strongest opponent's attack. 22 heroes retuned (`newheroes.py --tune`, new `retune.py`). `Tests~ -- combatstats` measures battles. Owner-approved and shipped 2026-11-27.
- [x] **98. Combat rules 4-7** (owner request after the combat review). Stacking Poison/Burn (up to 3, each stack ticks), half-rate crits on element disadvantage, role speed spread (`RoleSpdShift`: Attack/Support +8, Defense/Hp -8, so Support really ran at +14 with its +6 from `Mods`; changed in step 99; Unity-only, web parity tests add it back) and a speed-based starting turn meter, Weakness Break (3 advantaged hits = Broken, a lost turn; bosses half, Titan immune; break bar and Broken icon), global `Battle.Pace` 1.6 on hits, heals, regen, shields and DoTs (not in the Titan fight). AI: the blanket "no heal if no one is hurt" rule removed (Useful() judges the whole skill). 39 heroes retuned; kit trims for Vex, Obrecht, Ondessa, Morvoren, Solenne. Owner-approved with three fixes: stage 14-4 eased (`Curve.StageEase`, 0.926), no Poison/Burn stacking and no disadvantage crits on the Titan (average Titan damage 48,654 vs 49,110 on main), a one-time Weakness Break tip (`Tips` "break"). `Tests~ -- stagerate <idx> [runs]` and `-- titandmg` probe a stage and Titan damage.
- [ ] **96. Vessa Silkmourne (first creature hero).** Owner concept and prototype approved; built on the shared creature path (`docs/sigilbound/art/CREATURES.md`): UBC upper body on a spider rig, her own baked clips, runtime foot planting (`CreatureLegs`), wider lineup slot and melee reach, knock-down holds the hit pose. Legendary Shadow Attack, balanced 49% (vs Vaelith 49%, Morwenna 50%). Model replaced by the owner's image-to-3D mesh (TRELLIS via fal.ai) on the same spider rig and clips (`creature_i3d.py`). Waiting on owner review of the renders. Branch `feat/vessa`.
- [ ] **99. Balance patch and combat fixes** (owner-approved as one change after the mechanics and code reviews, 2026-11-27; branch `fix/balance-combat`). Rules and measurements: MECHANICS.md section 9.
  - [x] Light/Shadow chapters: enemies x0.92 (`Curve.LSEase`), applied by the Unity-only `Game.CampaignEase` on top of the web curve (campaign and Hard; dungeons unchanged). Act II Light/Shadow mid team 39.6% -> 82.6%, strong 80.0% -> 96.5%.
  - [x] Role speed shift Attack +8, Defense -2, HP -4, Support 0 (Support keeps +6 from `Mods`). Role-vs-role spread 18.3 -> 5.9 points (`Tests~ -- roles`).
  - [x] Stage 10-1: chapter 10 factor 1.04 -> 0.98 (`Curve.ChapterFix`, Unity-only); `StageEase` now read in both acts. 10-1 mid team 5% -> 63%.
  - [x] Act II: `Act2Ramp` 0.02 -> 0.015, `StageEase[89]` 0.90, gold and XP per clear +2% a stage past the level cap. 15-6 mid/strong 0/2% -> 8/78%.
  - [x] Titan tiers 0/15k/40k/80k/140k/220k (Unity-only thresholds, web rewards). Maxed team: 29% reach 140k, 3% reach 220k.
  - [x] Fixes: a Counter-caused Break costs the next turn (code M1); BreakGuard counts only acted turns, not Stun-skipped ones (mechanics #9); the AI kill estimate uses `Hit`'s maths (M2); heal events carry `Source` (M3); the AI drops heals and shields at the late-enrage floor and zero shields aren't added (L9). Tests in `MechanicsTests` "Combat fixes".
  - [x] Readability: round counter that turns red with "Enrage xN" from round 10 and a one-time Enrage tip; Broken gets its own icon (cracked sigil), reaction and BREAK! banner; Break tip/legend say heroes can be Broken; the dodge only plays on a fully absorbed hit.
  - [x] 38 heroes retuned for the speed shift (`retune.py`, `newheroes.py --tune`; Rowan and Aveline by hand), every hero back in 40-60%. New probes: `Tests~ -- campaign [runs]`, `-- roles [battles]`, role slot win rates in `combatstats`, Titan tier reach in `titandmg`.
  - [ ] Owner check in game: the round/enrage counter, the Broken look and icon (no Unity build of this branch was available for screenshots), and the 15-6 finale (mid team still 8%).
  - Not in this patch (from the review): the Light/Shadow counter hint on the prep screen, Hit_Big × Pace, defeat reasons, Slow/SpdUp ±20%, the original-24 outliers (P2-21).

## Phase J: review fixes (the P0 items and owner decisions from the six reviews of 2026-11-27)

- [x] **100. Save safety (P0-1).** `Core/SaveStore.cs`: a save that fails to load is copied to `sigilbound.save.v1.corrupt-<time>` and left in place, the backup (`.bak`, the previous good save, written before each save) is tried, and while both fail every write is refused (autosave on app switch included) until the player loads a save code or starts over on purpose (a modal explains, with "Copy damaged save"). Unknown hero ids (newer or branch builds) and the gear they wear are kept as raw JSON and written back; campaign progress past this build's last stage is kept. `Tests~/SaveSafetyTests.cs`.
- [x] **101. Hero model memory (P0-2).** `HeroModels`: the high-detail cache counts the figures using each model (`HdRef`) and frees unused ones beyond 8, least recently used first (`Core/CachePolicy.cs`; just-preloaded models get 30 s, up to 16), disposing the glTF, material and textures; `TrimSoon` after every screen change and battle start runs the eviction and `Resources.UnloadUnusedAssets` (at most every 3 s). Textures are uploaded non-readable (`LoadImage(..., markNonReadable)`, `Apply(true, true)` after the Hollow tint), the decoded glow map is destroyed once the mask is built, Low loads no normal map. The cache key carries the tier (`|lo`); LOD1 is only requested for heroes listed in `index_lod1.txt` (written by prepare-models; none yet, which removes the 166 missing-file reads). A failed load is retried on the next request. `CharacterView` destroys the unlit materials it made (code review L10). Estimate: about 12 MB per cached hero instead of about 24 MB, and at most 8 idle heroes (about 100 MB) instead of every hero seen. `Tests~/CachePolicyTests.cs`.
- [x] **102. APK size (P0-3).** `prepare-models.mjs` snaps the high-detail heroes' positions (about 1/16384 of the model) and skin weights (1/256, sum exactly 1), both still float32, and packs every generated GLB into an "SBZ1" container (`Core/ModelPack.cs`: buffers byte-transposed, lossless; `HeroModels.ReadBytes` unpacks) before gzip; the Unity-only bodies are gzipped too. Generated GLBs 96 MB to 66 MB, so the APK goes from 195 MB to about 164 MB (estimate). CI: the `checks` job prepares the models first and on CI a skipped size check fails; `ModelPackTests` checks the round trip, the shipped files and an APK estimate within 190 MB; the `android` job fails an APK over 190 MB. Hero textures stay JPEG (ASTC would grow the download; see `docs/sigilbound/APK_SIZE.md`, which also notes what Play Asset Delivery would take; not implemented, owner decision D8).
- [x] **103. Store build without the updater (P0-4).** Confirmed: `BuildScript.AndroidStore` sets `SB_STORE` and `AndroidUpdatePermission` adds no install permission or provider to the store manifest. Tightened: `Updater.cs` now compiles only without `SB_STORE` (the store build gets `UpdaterStore.cs`, a stand-in with the build number and no download or install code; Settings hides the update options), and the store build also switches the updater's Java plugins off for Android during the build. The updater's decisions moved to `Core/UpdateCheck.cs` (newest versioned asset from the release JSON, strictly newer build only, package must be a zip of the size the release lists, so a cut-off download is never installed; Windows zips are now checked too) with `Tests~/UpdaterTests.cs` (real release fixture, error pages, truncation, store-build source checks). CI compile-checks the `SB_STORE` variant (`-p:ExtraDefines=SB_STORE`).
- [x] **104. Frame rate and battery (owner decision D2, P1-20 part).** 60 fps by default (the old saved "Auto" reads as 60); Settings offers the screen's 90/120 Hz ("high", steps down to 60 when it can't hold) and 30; auto-replay runs at 30 (`Quality.ReplayCapped`, `BattleController.Replaying`); rules in `Core/FrameRate.cs` (`Tests~/FrameRateTests.cs`). The screen may sleep in menus (kept awake only in battle). Behind the title screen and Summon (opaque full-screen paintings) the arena camera culls everything and post-processing is off (`Arena.SetCovered`, `PostFx.SetCovered`). Not done here: dropping HDR and the fill light on Low/Medium, a menu idle frame interval, the world map (its painting doesn't cover the whole screen). MECHANICS.md 6.1 updated.
- [x] **105. Quit rule (owner decision D3, P1-11) and Titan cinematic retreat (code review M4).** `Game.StartTitan` uses the try and `Game.StartArena` uses the fight and books it as a loss (points, weekly record, new rivals) when the battle starts, saved at once; `CompleteTitan`/`CompleteArena` then count the real result once (without a start they keep the web game's order). Titan practice and the campaign are unchanged. `BattleController.ResetCinema` (on retreat and at every battle start) clears the camera override and breathing tilt, hides "Tap to skip" and the lair title, and puts the Titan back at its spot, idle and unfrozen; `Arena.FrameBattle` also clears any override. `Tests~/QuitRuleTests.cs`.
- [x] **106. Screens and controls on phones (P1-12 to P1-19, UX review #1-#4, #6-#8, art review H5).** Branch `fix/ux-p1`. Shared pieces in `Ui.cs`: `Ui.Dp`/`Ui.Tap` (48 dp from `Screen.dpi`, never under 44 units), `Ui.SafeContent`, `Ui.Segmented`, `Ui.TextOnArt`, `Ui.ElementBadge`, `Ui.RarityTab`, `Ui.FitSize`, `Ui.Contrast`. `Tests~/UxTests.cs` guards them.
  - Touch targets: stage list rows at least `Tap + 6` tall, the whole row is the tap target (the button stays as its label), the list scrolls with an edge fade and covers the reward track while open, close button 48 dp, row text 12-13; locked rows and medallions toast what to clear. Battle corner buttons 48 dp with 8-unit gaps and 11-point captions, turn chips 88×28 with 11-point names, tags with 11-point names, a 10-point level badge, HP 8 / break 4 / turn meter 4, status icons 18 with a 12-unit turns badge (10-point digit) and a 48 dp hit box, skill description 12 in a box that grows to fit. Prep (wide): every button and chip at tap height, presets side by side. Settings: 48 dp chips and buttons, a drawn slider (gold track, filled part, 28-unit thumb). Top bar menu 48 dp, tutorial buttons 42, Skip is a framed chip.
  - Home (landscape lays out full width): quests and help chips on the left, the team in the middle, login, achievements and a pinned "Continue Campaign" on the right; portrait pins the button above the nav. The tutorial's "To battle" step points at the same button as the welcome tip.
  - Safe area: top bar contents, nav cells (`App.NavCell`, a centred band of at most 560), modals and full-width screen content stay inside `Screen.safeArea`; battle tags clamp to it (the HUD corners already did).
  - Readability: disabled buttons draw a dark greyed plate with `#d2ccc0` text (5.2:1 at worst; was about 1.4:1), and quest Claim shows "2/5" while not ready, Summon ×10 shows "×10 · 5/10"; nav labels `#c4b89f` (9:1); map chapter names, the stage count, the Home subtitle, the Summon line and "Enemy turn" sit on dark pills; mode card titles shrink to fit and locked hints use the disabled text colour.
  - Campaign switches: "Normal | Hard" and "Act I | Act II" are segmented controls in a taller top bar (outside the painting), 48 dp, locked options readable with a lock and a toast.
  - Press feedback: painted plates shrink 3% with their pressed art; drawn (ghost) buttons get a light overlay while held and on mouse hover, and play the tap sound. The painted-plate padding that was set and then overwritten is now one value.
  - Colour-blind cues: enemy chips and turn chips use element icons (no initials or coloured dots), turn chips mark allies with a shield and enemies with a skull, damage numbers add "WEAK!"/"RESIST", the prep matchup line has ▲/▼, rarity frames carry a C/R/E/L tab.
  - [ ] Owner check on a phone (no build of this branch was available, so the screenshots are PIL mock-ups in the scratchpad `ux_mock/`): stage list, Home, battle HUD, campaign switches, a camera-hole device.
  - Not in this patch: element/rarity palette changes (Tide/Shadow, Rare/Tide hues), a colour-blind palette setting, enemy intent, skill names on the buttons, modal width cap, a global text-size setting.
- [x] **107. Roster tools: bulk release, rank-up fodder, Rank up all (owner decisions 2026-11-28; design: research `BULK_RELEASE_AUTO_RANKUP.md`).** Branch `feat/roster-tools`.
  - Fodder order fix first (its own commit): `SuggestFodder` sorted by level before rarity, so a fresh Lv1 Epic/Legendary was offered before levelled Commons. Now lowest rarity, then spare copies, then lowest level; the confirm showed names only and now shows stars, level and rarity.
  - `Core/Roster.cs`: one `IsProtected(hero, Protect)` for release and rank-up. Locked (the Lock is the favourite, no new flag) and the current team always; saved teams, geared, ranked-up on by default; Epic/Legendary and last copy only via red danger toggles, never saved off. Rank-up auto-picks use `Protect.Fodder` = the release defaults minus RankedUp (fodder must share the target's stars, so every 3★+ Common or Rare is ranked up; with RankedUp on, Auto-fill could never fill a 3★+ rank-up). Hand picks are allowed for anything but locked/team heroes, with a warning (Epic+/last copy: 1-second hold).
  - Release: `PlanRelease` (masks, level range, dupes only, keep-best-N over the whole roster, taps on the review grid keep heroes, never every copy with LastCopy on, never every hero), `ReleaseMany` (re-checks each hero, gear back to the bag, removed from saved presets), `UndoRelease` (in memory, 120 s, refused once the essence was spent; gear back on only if still free). The hero screen's single Release uses the same path and gets the Undo too.
  - Rank up: `AutoFill`, `PlanRankAll(target, teamOnly, trainToCap)` (strongest first, fodder never reused, promoted heroes never fodder, stops when gold runs out), `ApplyRankAll`. No auto rank-up after battles (owner: not in v1). No fodder chains.
  - Save: optional root key `"roster"` (`presets`, `active`, `sort`, `rankTarget`, `rankTeamOnly`), written only when not the defaults or when the source save had it; values clamped on load, Locked/Team and the danger bits forced on; unknown keys inside it kept. Save version stays 1; old saves load unchanged. The Heroes list sort (`roster.sort`) now persists (team prep shares it).
  - UI: panels drawn inside the Heroes screen (`Client/ReleasePanel.cs`, `RankUpPanel.cs`, `RosterUi.cs`; no new ScreenId), hooks only in `Screens.cs` (DrawHeroes, the rank-up card, AskRelease) and `App.cs` (Go, Back, save import). Controls use `Ui.Tap`. Layout is for the 390-wide menu column (the research mock-ups were landscape full-width).
  - Tests: `Tests~/ReleaseTests.cs`, `Tests~/RankUpTests.cs`, fixture `fixtures/save-web-roster.json`.
  - [ ] Owner check on a device (no Unity player could be built here; the screenshots are PIL mock-ups from the layout constants in the scratchpad `roster_shots/`).
  - Not in this patch: auto rank-up after battles, "Prepare fodder" chains, long-press to delete presets (× on the active preset instead), the web game (keeps the `"roster"` key, has no presets; its rank-up rule is unchanged).
- [x] **108. Titan scene (art review H3).** Findings: the camera was already on the heroes' side and the Titan already faced it. The "white U on its back" is a glow island on the front of the knight's chest (boxes at z 0.17, glTF +Z = front), drawn at 2.2x the element colour, which clips to white. The model was also scaled twice (built to the 6.24 m head inside the view's 1.3 scale, so 8.1 m), which put its horns through its name tag. Fixes: model built to `head / viewScale`; arrival filmed from behind the team (`BattleLayout.TitanIntroKeys`), and Cinder Breath gathers seen from the heroes' side; every Titan cinematic tilts down until the ground is in frame (`MaxPitchForGround`, `Arena.ComposeCamera`; Meteor Leap's peak goes from 55 to 35 degrees up); the material is single-sided, seam gain is 1.35 with a per-element seam colour, and the chest letter is cut from the glow (`glowCut`); the name tag follows the head bone above the horn tips; the caldera has a 14-chunk crater ridge with a lava line at its foot and two lava-vein strips (`Scenery.Caldera`). The model hookup is data (`Core/TitanSpec.cs`, `heroes_hd/titan.json`: model, head, face, horns, feet, reach, clips, aliases, seams, glow cut), ready for the basalt colossus (`docs/sigilbound/art/TITAN_MODEL.md`). `Tests~/TitanCameraTests.cs`. Branch `fix/titan-scene`.
  - [ ] Owner check in game: no Unity build of this branch was available for screenshots (the camera placement was checked in `Tests~` and on before/after diagrams).
- [x] **109. Polish pass (PMO P2-15, P2-16, P2-18, P2-19, P2-23, P2-24; UX review #5, #9, #10, #14, #17; QA review #8; code review L1, L2, L8).** Branch `fix/polish-p2`. Keeps step 106's painted skin, `Ui.Tap` sizes and colour-blind cues.
  - Wording (P2-18): `GameData.ShardName` (Worn/Mystic/Radiant Shard on Summon, the Essence Exchange, the results strip and the auto-replay summary); `GameData.RoleName` prints the HP role as "HP"; "View" everywhere (portrait HUD, Settings "Battle view", the toast); the auto-replay toggle is "Replay" with a toast that names it, and the one-off rerun on results is "Again"; the stage-list button reads "Team ▶" (it opens team setup); the hero list says "In team #1" (Team 1-3 are presets).
  - Feedback (P2-19, the parts step 106 left): dungeon floors open team setup (`OpenDungeonPrep`: waves, title, runs left, "Fight floor N"; results "Edit Team" comes back to it), locked floors toast why; preset Save asks before replacing a different saved team (`AskSavePreset`, both layouts); Reset save leaves Home for Settings > Danger zone, behind a confirm with "Copy my save code" (the write goes through `SaveStore`, so the replaced save stays in the rolling `.bak` until the next save).
  - Battle info (P2-15): skill buttons carry a short skill name (`SkillText.ShortNames`: whole words, at most 11 letters, distinct per hero; every hero fits without cutting) and "N turns" on cooldown; `Battle.Matchup` puts "▲ WEAK" / "▼ RESIST" over each highlighted target for the chosen skill (both layouts); a "Status" corner button (landscape) and a "?" by the skill text (portrait) open the status legend on any turn. Enemy intent: not done (needs Core to expose the planned action; effort L).
  - Modals (P2-16): `App.Modal` takes a max width, 560 by default and 900 (`WideModalW`) for results, centred in the safe area. Hard, Titan (and practice) and Dungeon results use the campaign results layout in landscape (`ResultsSpec`: where, rewards incl. essence/tomes/drops, the Titan's damage as a headline, a note such as runs left, the mode's own buttons; MVP, damage/taken/healing columns). Portrait keeps the narrow modals.
  - Pop-ups (P2-24): `Core/PopupQueue.cs` decides what opens: a save problem, then nothing while the tutorial's coach panel is up (`Tutorial.Blocking`), What's New (Home only, never on the very first launch: that marks the notes seen), the Act II intro (campaign screen), tips (`Tips.Peek`/`Next`), and the update banner last, outside battle (`App.UpdateBannerAllowed`, read by `Updater.OnGUI`), with a 0.8 s gap after a modal closes. Tips the tutorial covers ("welcome", "battle") are marked seen once it reaches them.
  - Code safety (P2-23): `Battle` drops empty waves and ends at once with no foes (no result, nothing granted) or no heroes (defeat); `RunAuto` stops on a null actor; the client shows a neutral "Battle over" for a result of None (L1). `DrawWorldMap` resets `Ui.Cover`/`OnTop` in a finally (L2). `PredictTurns` steps the meters like `NextActor` (L8). L5 and L9 were already done (steps 101 and 99).
  - Tests: `Tests~/PolishTests.cs` (queue order and first launch, empty sides, a near-tie where the old step disagreed plus 60 random fights, captions, names, matchup, client source checks).
  - [ ] Owner check on a device (no Unity player could be built here; the before/after screenshots are PIL mock-ups in the scratchpad `polish_mock/`).
- [x] **110. Load hitches, per-frame garbage, speed and the original 24 in the balance band (P2-3, P2-4, P2-20, P2-21; performance review #4/#5, code review M5, mechanics review #6/#7).** Branch `fix/perf-balance-p2`.
  - Team-screen hitches (P2-3): `HeroModels.LoadHd` no longer does all of a hero's image work in the frame after its glTF parse. Gzip inflate and the SBZ1 unpack run on a worker (`Inflate`), as do the Hollow tint and the glow-to-mask loop (`HollowTint`, `MaskFromGlow`); each decode or upload waits for `HeavySlot`, at most one per frame across every loader, so a stage's six Hollow enemies preloading on the team screen no longer decode in the same frame. Measured on a desktop core with the shipped files: inflate about 6 ms, albedo JPEG 15 ms, normal 7 ms, glow PNG 3 ms, mask loop 3 ms, Hollow loop 5-9 ms, all in one frame before (about 35-45 ms, so 100-200 ms on a mid-range phone, times every hero finishing together); now the longest single main-thread step is the albedo decode. `SBPERF hd <key>` log lines give each step's main-thread time on a device.
  - First launch (P2-3): `Sound.Create` no longer loads all 206 clips (135 short ones are decoded to PCM, 20.7 MB and 122 s of stereo; about 0.6 s of Vorbis decode on a desktop core); cues load on first use and `Sound.Warm` loads one cue per frame after start-up, taps first; `AudioImportRules` (version 4) sets `loadInBackground` on every clip. `SkillFx` paints its 20 procedural textures (0.45 Mpx, 120-150 ms on a desktop core) on a worker thread and uploads one per frame (`PaintTextures`); until then they are blank, and effects only play in battle. `SBPERF sound` and `SBPERF fx textures` log lines.
  - Per-frame garbage (P2-4): `Core/UiCache.cs` (`UiKey` struct cache keys, `NineSlice`, `NumText`). `Ui` caches textures and styles by `UiKey` with no string keys or closures (the old `Ui.Label` key was 136 B per call), button plate names per `Kind` are built once, `Card` draws its studs without an array; `Skin.On` reads PlayerPrefs once, `Draw9`/`DrawFrame` use `NineSlice` instead of four `float[]`, plate variant names and fallback borders are cached; `Palette.Hex` parses each literal once; the landscape battle HUD reuses its tag lists, checks overlaps with a loop and takes level text from `NumText`; status icon stacks and turns too; `Unit.Has` and `Unit.Shield` are loops. `Tests~/HotPathTests.cs` measures that the helpers allocate nothing (`GC.GetAllocatedBytesForCurrentThread`) and checks the client sources.
  - Speed (P2-20): Slow and SpdUp ±20% (`Battle.SpeedFx`, was ±30%); gear adds at most +50 SPD (`Game.GearSpdCap`, Unity-only). New probe `Tests~ -- speed` (`SpeedProbe.cs`): a speed-stacked Haste set (+110) beat the same heroes in ordinary +40 gear 99.6%; at +50 it wins 68.5%, about what +10% of another stat gives (+10% HP 73.7%, +10% ATK 64.8%); 81% of random Mythic +10 sets stay under the cap. MECHANICS.md 9.1 and 9.8.
  - Balance band (P2-21): `RosterTests` checks all 157 heroes, the original 24 included (`RosterTests.Peers`, shared with the `balance` probe). Retuned: 9 classic heroes (Kettle, Oriel and Wren down; Corvan, Ysolde, Brannoc, Nyx, Tamsin, Sable up) with `retune.py`, then the 25 hand-written and 20 phase H heroes (Crusk included) the speed trim and the new classic numbers pushed out of band (`retune.py`, `newheroes.py --tune`; Crusk by its tuning factor alone, so the generated block's hand edits stay). 7 heroes outside 40-60% before, about 31 after the speed trim, 0 after. Retuned classic heroes differ from the web game's skill numbers (`GameData.UnityTuned`; the parity and Skill Tome tests skip their power numbers only). Stage 13-6 eased × 0.92 and 15-6 × 0.85 (was 0.90): without it the strong suite lineup dropped to 38% and 75% there.
  - Probes before (origin/main) → after: combatstats median 63 → 61 turns, p90 112 → 113, ally wins 988 → 1010 of 2,000; roles spread 4.9 → 6.3 points; Titan average 52.6k → 52.7k, maxed median 122k → 127k; campaign Act II Light/Shadow mid/strong 63.2/97.8% → 71.5/94.4%, Act II triangle 88.1/98.5% → 91.9/97.3%, 13-6 33/75% → 85/72%, 15-6 5/98% → 18/95%; Act I unchanged or easier (Light/Shadow mid 92.6 → 96.5%).
  - [ ] Owner check on a phone: the team screen with a stage's enemies preloading (look for the SBPERF lines in the log), first launch time, and that the first tap after start-up plays its sound.
  - Not in this patch: JPEG decode off the main thread (needs Unity 6's `DownloadedTextureParams` or KTX2), `App.UpdateLineup`'s per-frame key, `SceneryPaint` per arena, the web game's `data.js` numbers.
- [x] **111. In-house tester findings (builds 175 and 176, `docs/sigilbound/qa/inhouse-2026-11-28-*.md`).** Branch `fix/qa-176`.
  - S2 Act II walls: `Game.StageWaves` drew Act II enemies from every hero, so the seven new heroes reshuffled every Act II line-up. Act II now picks from `GameData.Act2EnemyIds`, a frozen list of the 150 heroes Act II was tuned with (step 99); a new hero joins only by appending it there on purpose (which re-rolls Act II and needs a re-tune). Act I keeps `Classic`. Unity-only rule (the web game has no Act II), so web parity is unaffected. Tests: `Game.StageWaves(idx, roster)` with a hero appended or inserted moves no stage; every stage matches `Tests~/fixtures/stage-enemies.json` (`dotnet run -- stageenemies` regenerates it, only for a deliberate change).
  - Campaign probe (`campaign 60`, mid / strong), origin/main → this branch: Act II Light/Shadow 71.5/94.4% → 86.3/95.6% (mid under 50%: 3 → 1); Act II triangle 91.9/97.3% → 93.1/96.4%; 12-6 18/95% → 83/100%; 13-6 85/72% → 37/62%; 14-6 8/85% → 100/100%; 15-5 35/70% → 70/88%; 15-6 18/95% → 35/73%. No retune: no non-finale stage has the mid team under 25% or the strong team under 50%; 13-6 (a boss, already eased x0.92) is the hardest non-finale stage and is worth watching.
  - S3 Release undo: `ReleaseMany` recorded positions after the earlier removals, so undo swapped heroes in saved teams (`[8,9]` → `[9,8]`) and in the roster; it now records the original positions. Test in `ReleaseTests`.
  - S3 portrait: the battle HUD's portrait path uses the step-106 rules (48 dp `RoundButton`s with Leave, View, Replay, Auto, speed and a Pause button; the Status button on any turn; a 48 dp hit box on the status icons; turn chips with element icon and shield/skull glyph, names to 7 letters). The portrait campaign uses the same segmented Normal/Hard and Act I/II switches as the landscape top bar (`DrawCampaignSwitches`), lists the chosen act's chapters, and has 48 dp stage chips. `UxTests` now read the portrait code too.
  - S4: stage-row enemy chips put the level under a name that doesn't fit, then step the font down and cut with an ellipsis; locked portrait chapter cards are dimmed but opaque; the release review marks every card of a hero whose copies all go (`ReleasePlan.LastCopyIds`); the unused `G6` in `RankUpPanel` is gone; the Titan arrival keys stand in the gap between heroes nearest the middle (`BattleLayout.IntroGapX`: with three heroes the camera sat right behind the middle one, Pell) and the Titan's tag keeps 18 more units under the skill banner; menu/gallery lineups use `Creatures.LineupXs` (1.4 m gaps scaled by `Creatures.LineupWidth`: creatures' `SlotWidth`, and `WideHeroes` for the six new humanoids, capped at 5.2 m); models are instantiated out of sight and kept unrendered until their clips are on and the idle is sampled (`HeroModels.Reveal`): the 2400x1080 T-pose was the bind pose drawn while `BuildHd` waited on `EnsureHdBodies` before the `HeroModel` (and its idle) existed.
  - [ ] Owner check on a phone: portrait battle HUD and campaign, the Titan arrival with a three-hero team, the gallery and Home line-ups with the new heroes, Home at first launch (no T-pose).
  - Not in this patch: the portrait Home hiding the team behind the quest and login cards (S4, carried over).
- [x] **112. Auto Climb (owner request 2026-11-28).** Branch `feat/auto-climb`. A Climb switch beside Auto/Replay on the battle HUD (landscape round button; portrait now has a second 48 dp row: Replay, Climb, Auto, speed) and on every results screen (Replay/Climb chips: icon, "On/Off" and a check badge, painted chip plates). After a win the next fight starts after the same 3 s real-time pause as auto-replay; the results screen shows "Climbing · next in N" and a Stop button instead of its own buttons.
  - Rules in `Core/AutoClimb.cs` (`NextTarget`, `AfterFight`, `SetClimb`/`SetReplay`, `AutoBattle`, `SkipLairReveal`, `PlaysActIntro`, `ClimbLog`); MECHANICS.md 12. Normal: next stage across chapters and into Act II (the one-time Act II intro plays once, in battle, then the climb goes on); Hard: the next unlocked Hard stage; Dungeons: the next floor of the same dungeon while runs remain; Arena: the same slot (Easy/Even/Hard) of the new rivals while fights remain (tries are still used at fight start). Stops on the first loss, at the end, at a locked stage, out of runs/tries, when another modal opens during the pause, or when the player stops it (Stop, Back, Leave, switching Climb off or Replay on).
  - Climb Summary (`Client/AutoClimbUi.cs`): stages/floors/fights cleared, fights in all, the reason (icon + words), gold, XP, worn/mystic/radiant shards, gear, essence, tomes and level ups; Edit team, Keep climbing (the lost fight again, or the fight that was next), Close.
  - Climb and Replay are exclusive; Climb forces Auto battle on (the Auto button says so instead of switching off). Lair reveals: once per session as before, and while climbing also skipped for a boss stage cleared before. 30 fps cap while climbing (`Replaying` feeds `Quality.ReplayCapped`); on desktop the climb carries on unfocused when "keep running" is on (real-time waits only).
  - Save: `settings.autoClimb`, written only when on (or when the source save had the key); a save with both Replay and Climb loads with Replay only. `App.ModalSerial` lets the climb notice a modal that isn't its results screen.
  - Tests: `Tests~/AutoClimbTests.cs` (next target per area incl. chapter/act boundaries, locked stages, runs/tries used up, stop on loss, a real engine climb whose summary equals the sum of battle rewards, settings round-trip and SaveStore write, exclusivity, forced Auto, reveal skipping).
  - [ ] Owner check on a device (no Unity player could be built here; the layouts are PIL mock-ups in the scratchpad `climb_mock/`).

- [x] **113. Digitigrade legs for animal-folk (owner report 2026-11-28: "some legs had knees for humans but not the animal they represent").** Branch `feat/animal-legs`. Rigging notes: `docs/sigilbound/art/RIGGING.md`.
  - Survey of all 150 high-detail heroes (portraits, concept images, TRELLIS meshes, roster species, side views in walk/run/crouch/attack/hit): Tansy (hare) and Corran (goat) stand on digitigrade legs; Nettle, Fennick, Rook, Augustin, Lune, Holt, Jasper, Perpetua and the rest have plantigrade legs (boots, armour, robes, bear paws) and keep the humanoid rig.
  - `blenderforge/i3d_legs.py` (opt-in `"legs": "digitigrade"` with knee/hock/ball/toe landmarks, `_digitigrade()` in `i3d_landmarks_portrait.py`): a metatarsal bone per leg (`thigh > calf > meta > foot > ball`), and every shared UBC clip solved offline for the reverse-knee leg (foot plants and hip motion from a virtual humanoid leg of UBC proportions, hock folding with the humanoid ankle, two-bone IK for thigh and calf) and baked to FK keys in the hero's GLB under the shared clip names. `i3d_rig.py` only gains hooks behind the flag (copy it with `i3d_legs.py` to `feat/new-heroes-6`, `feat/titan-model` and `main`).
  - `Client/DigitigradeLegs.cs`: layers each baked leg clip over its shared clip (layer 1, leg chains only, time and weight synced every frame); `RigExtras` skips those leg bones. No runtime IK; clip names and battle code unchanged.
  - Tests: `Tests~/DigitigradeTests.cs` (metatarsal chain, a baked leg clip per shared clip with the same length, keys on all ten leg bones only, wiring). Size: +0.3 MB gzipped per hero; APK estimate 162 MB.
  - [ ] Owner check on a device (no Unity player here; the proof is Blender renders of the shipped GLB data: scratchpad `reviews/legs/`).

- [x] **114. Campaign scenes, procedural and camera-first (owner request 2026-11-28; plan `docs/sigilbound/art/CAMPAIGN_SCENES_PLAN.md`, rules `art/SCENERY.md` section 8).** Branch `feat/campaign-scenes`.
  - Cameras in Core (`SceneCameras`): the five battle views fitted at portrait, 16:9 and 19.5:9, the lair reveal's keys (moved from BattleController into `SceneCameras.RevealKeys`), the hero lane, ground footprints. Battle start and victory use the framed view.
  - Generator (`SceneGen`): stage id -> seed -> light (constant hero light, mood on the world's albedo, lane lift), the landmark framed for the default view (or its copy ahead, nearer each stop), the road, story set-pieces moved only when illegal, boss dressing, night lanterns, skyline, mid-ground, edge framing, approach props, scatter with falloff near the lane, edge tufts, terrain relief (flat in the lane, on the road, at the shore and under the landmark), per-tier triangle budgets. `SceneRules.Check` refuses anything in the lane, blocking it from Classic/Overhead/Cinematic/Three-quarter, in the foreground outside the screen's edge bands, in the reveal's sight lines, or seen by no camera. Hero view fades its blockers at run time as before.
  - Data: the theme table moved to `Core/Biomes.cs` with the generator's tables (Palette.Scene reads it); mesh footprints in `Core/SceneMeshes.Generated.cs` (`Tools~/scenery/mesh_bounds.py`).
  - Client: `Scenery.Build` places the layout (`PlaceItem`), a terrain grid with the Plane's mapping, the ground painter adds the road, mood tint, lane lift and stage blotches (`paint.py` mirrors it); `Arena.Build` uses `SceneGen.LightOf` (sun height and side per mood) and keeps the layout for the reveal and the night lair rim light.
  - Image-to-3D: 16 landmarks (Gemini concept -> fal.ai TRELLIS -> `Tools~/scenery/i3d_landmark.py` -> `Resources/Scenery/Landmarks/*.bytes`, gradient-mapped and painted from the chapter ramp at build time, glow texels with an emission mask, moving parts on sockets). Spend $1.00 of $5.
  - Tests: `Tests~/SceneGenTests.cs` (every stage x tier, Normal and Hard; non-campaign arenas; mesh table; landmark size budget). Previews: `Tools~/scenery/scene_preview.py` (Blender, game camera parameters, contact sheet per chapter).
  - Boss-stage tilt (owner decision 2026-11-28): `BattleLayout.View(key, aspect, titan, boss)` / `BossView` raise the look 6 degrees (`BossTilt`) on campaign stage 6 (Normal and Hard) for Classic, Overhead, Cinematic and Three-quarter and refit the distance (`FrameFits`); `Arena.Build` sets `BossTilt`, `FrameBattle` uses it (the lair reveal blends into the tilted view). `SceneCameras.Battle(fog, boss)` feeds the scene rules the tilted cameras; on a boss stage `FrameError` also wants the lair's base on screen and the landmark up to 6 m (`LairShown`) under the turn bar. Tests: tilt angle, every hero/enemy/tag in frame at 8 screen shapes, Hero view and Titan untouched, each lair framed and shown more than in the level view, the client wiring.
  - Screenshot tour: `-sbShotsScenes` (`ShotsMode.SceneTour`) shoots every chapter's stops 1, 3 and 6 in battle plus the lair reveal and two Hard lairs, logging camera pitch and SBBUDGET; opt-in (workflow_dispatch input `scenes`, `SB_SHOTS_SCENES=1 tools/unity-shots.sh`) so the per-commit smoke stays about 5 minutes; `unity-smoke-check.sh` then requires all 47 scene stops with skinned heroes.
  - [ ] Owner check of the real screenshots (contact sheets per chapter, CAMPAIGN_SCENES_PLAN.md section 7).
  - Not in this patch: the Titan caldera (unchanged), glow-heavy lairs (glass spires, Hollow cavern, last pyre, drowned shrine, burning tree) still procedural.
