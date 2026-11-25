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

- [ ] **1. Text bug.** One string drawn into many controls, labels outside buttons, duplicates.
  - Find the root cause in Client/Ui.cs (IMGUI) and fix it everywhere.
  - Ship an OFL font in Resources instead of OS fonts.
  - Done when every screen draws each label once, inside its control.
- [ ] **2. Icons.** Replace the emoji icons (currency chips, bottom nav, tags) with code-drawn or texture icons (start from `unity-landscape`'s Icons.cs).
- [ ] **3. See the game ourselves.**
  - A `-sbShots=DIR` command-line mode that walks the main screens and saves PNGs.
  - A CI job that builds a StandaloneLinux64 player and publishes it to a `unity-preview` release.
  - A script that runs it under xvfb here and saves the screenshots.
  - From now on, every step checks its screenshots before it's marked done.
- [ ] **4. Shaders in builds.** The GLB heroes, particles and Sigilbound/Character are stripped from builds, so stand-in figures and orange squares appear. Include them via Always Included Shaders, a ShaderVariantCollection or Resources materials, glTFast's shaders included. Done when the screenshots show the real heroes and soft particles.
- [ ] **5. Scenery.** Replace the sphere trees and plain ground with the web game's scenery recipes (`game3d/scenery.mjs`). The Godot build 4 forest is the minimum bar.

## Phase B: landscape (the owner chose landscape for phone and PC)

- [ ] **6. Landscape foundation.**
  - Android landscape, both directions. Windows 1600×900 resizable.
  - Height-based UI scaling from 16:9 to 21:9 with safe areas.
  - Existing screens reflowed so nothing overlaps.
- [ ] **7. Game modes hub.** Big illustrated cards for campaign, hard, trials, arena and titan, showing live status.
- [ ] **8. Campaign world map.** Sigilbound's own painted-look map, chapter markers with stars, a reward track and a Normal/Hard switch.
- [ ] **9. Stage list and team setup.**
  - Stage rows: stars, enemies, drops, Battle button.
  - Team setup: you vs the enemy, the element wheel, power, a hero picker with filter, sort and presets.
- [ ] **10. Battle presentation.** Wide camera, slim HP bars with level, wave counter and timer, corner Auto/speed/pause, slim skill bar, tags that don't overlap.
- [ ] **11. Results screen.** Banner with stars and time, rewards, per-hero damage dealt / taken / healing, and Replay / Edit Team / Next.

## Phase C: hero art (free tools first; the owner decides on paid tools after seeing the result)

- [ ] **12. Review the free-tools prototype.** Merge the useful parts of `art-round2`. Render Ysolde and Gorrum in the game next to today's heroes, and send the owner screenshots to decide.
- [ ] **13–18. Heroes in batches of 4 per session** (24 heroes), with the shared idle/wait animation and unique skill animations where possible.
- [ ] **19. Roster plan to 100 heroes.** Phases and concepts only. New heroes get designed (stats and skills) in their own later steps.

## Known device bugs (owner tests on Windows and a Galaxy S26+, Unity builds 7–8)

- Text duplicated or misplaced everywhere; tip popups show the body text twice, and the heading is "3".
- Emoji icons missing: the chips show only "◇ 0" and the bottom nav only "›".
- Heroes are code-built stand-ins, because the GLB models fail to load.
- Particles are orange squares; trees are spheres.
- Home: "Come back tomorrow" overflows its button, the quest buttons repeat the progress text, and an empty gold button sits at the bottom.
