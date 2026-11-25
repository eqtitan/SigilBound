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
- [ ] **3. See the game ourselves.**
  - A `-sbShots=DIR` command-line mode that walks the main screens and saves PNGs.
  - A CI job that builds a StandaloneLinux64 player and publishes it to a `unity-preview` release.
  - A script that runs it under xvfb here and saves the screenshots.
  - From now on, every step checks its screenshots before it's marked done.
- [ ] **4. Shaders in builds.** The GLB heroes, particles and Sigilbound/Character are stripped from builds, so stand-in figures and orange squares appear. Include them via Always Included Shaders, a ShaderVariantCollection or Resources materials, glTFast's shaders included. Done when the screenshots show the real heroes and soft particles.
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
- [ ] **13–18. Heroes in batches of 4 per session** (24 heroes), with the shared idle/wait animation and unique skill animations where possible.
- [ ] **19. Roster plan to 100 heroes.** Phases and concepts only. New heroes get designed (stats and skills) in their own later steps.

## Known device bugs (owner tests on Windows and a Galaxy S26+, Unity builds 7–8)

- Text duplicated or misplaced everywhere; tip popups show the body text twice, and the heading is "3".
- Emoji icons missing: the chips show only "◇ 0" and the bottom nav only "›".
- Heroes are code-built stand-ins, because the GLB models fail to load.
- Particles are orange squares; trees are spheres.
- Home: "Come back tomorrow" overflows its button, the quest buttons repeat the progress text, and an empty gold button sits at the bottom.
