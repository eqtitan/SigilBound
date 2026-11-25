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
- [ ] **26–30. Phase 2 heroes (Glass and Storm).** Step 26 DONE (2026-11-25):
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
  - Next (step 30): Selene, Brann (Counter), Ysra, Oswin (Pierce). That finishes phase 2.
- [ ] Later: phase 3 mechanics and heroes, campaign act 2 (chapters 11–15), then phase 4.

## Known device bugs (owner tests on Windows and a Galaxy S26+, Unity builds 7–8)

- Text duplicated or misplaced everywhere; tip popups show the body text twice, and the heading is "3".
- Emoji icons missing: the chips show only "◇ 0" and the bottom nav only "›".
- Heroes are code-built stand-ins, because the GLB models fail to load.
- Particles are orange squares; trees are spheres.
- Home: "Come back tomorrow" overflows its button, the quest buttons repeat the progress text, and an empty gold button sits at the bottom.
