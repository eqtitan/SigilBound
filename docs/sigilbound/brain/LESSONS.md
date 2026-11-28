# Lessons

Newest at the bottom. Format and rules: README.md. Systems: MAP.md.

## L001 - Stale prepared models fake test results
- Status: solved
- Systems: build-pipeline, tests
- Symptom: model-pack test failed locally ("snapping moves vertices 1780/16384") right after a merge; a script pushed anyway.
- Cause: StreamingAssets/Models held models prepared before the merge; tests compare prepared files with sources. The push was chained on a grep's exit code, not on the test result.
- Fix: run `cd unity/Tools~/models && node prepare-models.mjs` before tests; push only when the LAST test line is exactly "All checks passed." and every CompileCheck says 0 errors.; in a fresh checkout run `npm ci --no-audit --no-fund` in unity/Tools~/models first (prepare-models fails with ERR_MODULE_NOT_FOUND otherwise), and check the APK-budget test no longer says "not prepared here (skipped)"
- Prevention: every agent brief and the in-house tester require prepare-models first and a last-line check.
- Related: L007
- Evidence: commit b5a8401 (merge), re-run showed all checks passed

## L002 - Digitigrade heroes exploded in game (build 180)
- Status: solved
- Systems: hero-models, animation, rig-runtime
- Symptom: Tansy smeared into a sheet, Corran flung into the sky - only in the real game; Blender previews looked perfect.
- Cause: glTFast pre-adds a GLB's own clips to the Animation; BuildHd skipped shared clips whose names were already taken; only leg-only clips played, so RigExtras re-applied its rest fix to bones no clip rewrote, compounding every frame.
- Fix: HeroModels.ClearClips before adding shared clips; RigExtras skips bones that still hold what it wrote last frame.
- Prevention: SBBOUNDS pose guard in ShotsMode + tools/unity-smoke-check.sh fails CI on a mangled pose; DigitigradeTests check clip-clearing order.
- Related: L003, L004, L016, L022
- Evidence: commits 411e789, 4fd5a8a; docs/sigilbound/qa/inhouse-2026-11-28-f7ddd6b.md; RIGGING.md "Runtime pitfall (build 180)"

## L003 - Titan mangled by its own clips (build 190)
- Status: solved
- Systems: titan, animation, rig-runtime
- Symptom: limbs stretched into sheets/spikes in every Titan shot; 42 Titan tests and the smoke gate passed.
- Cause: the L002 family. The Titan carries the `rest_fit` marker, so it got RigExtras, whose rest fix re-expresses shared clips for moved joints. Its own clips (blenderforge/titan_moves.py) are authored on its own rest pose and need no fix; run in the frames before its first clip (BuildHd yields between RigExtras.Init and the first Play) and on bones its clips don't key, the fix compounded every frame. glTFast pre-adding the GLB's clips (L002) was in play too. Not isolated: main's 108bacd build (ClearClips + the LastR guard alone) was cancelled by the queue, so it's unproven whether that alone was enough.
- Fix: fix/titan-runtime (58ae756, on top of 108bacd): HeroModels gives a Titan with its own clips no RigExtras at all (`ownTitanClips`). Run 203 (dispatch of the branch): all 7 Titan shots compact and hunched, SBBOUNDS titan-* all ok (head 0.97-1.05, top <= 1.82, wide <= 2.02).
- Prevention: SBBOUNDS titan-<shot> in ShotsMode (intro + 6 shots; BAD past head 0.4-1.3 / 0.8 sideways, top 2.3 or wide 2.6: the baked clips stay under 1.85 wide, the bind pose is 2.65) and unity-smoke-check.sh fails on a BAD line or a missing Titan check; TitanMechanicsTests asserts the RigExtras skip and the guard. The skeleton-library rebuild (L022) removes the patch layer.
- Related: L002, L004, L016, L022
- Evidence: docs/sigilbound/qa/inhouse-2026-11-28-4a2723d.md; run 46114432755 (#203) sigilbound-unity-smoke 78-titan-intro..84-titan-6; build 207 (8610b2b) tours at 1280x720, 2400x1080, 1080x2400, 1024x768 show a coherent Titan in every shot (docs/sigilbound/qa/evidence/8610b2b/titan-now-2400-1024.jpg) - likely fixed by the L002 clip-clearing; keep open until confirmed on a phone and the Titan is in the SBBOUNDS guard
## L004 - Offline previews are not the game
- Status: solved
- Systems: ci, qa, hero-models, scenes
- Symptom: Blender/PIL previews approved, real build broken (L002, L003); scene previews darker than the game.
- Cause: previews don't run the game's import (glTFast), animation system, shaders or lighting.
- Fix: prove visual changes with a real build: workflow_dispatch the branch (task build-apk; publishing is locked to main), download the `sigilbound-unity-smoke` artifact, judge real frames.
- Prevention: owner approvals for visuals use real screenshots when possible; label mock-ups as mock-ups.
- Related: L002, L003, L005
- Evidence: runs 182, 186, 191

## L005 - The CI queue keeps one pending run
- Status: solved
- Systems: ci
- Symptom: branch test builds cancelled before running; agents waited hours.
- Cause: license-seat concurrency groups keep only the newest pending job, so each dispatch (or main push) cancels the waiting one.
- Fix: dispatch once, wait for it to finish; the lead spaces dispatches between agents.
- Prevention: agent briefs say "dispatch once and wait"; avoid pushing to main while a needed branch build is pending.
- Related: L004
- Evidence: runs 185, 187, 193, 194, 197 cancelled

## L006 - New heroes reshuffled every Act II stage
- Status: solved
- Systems: balance, campaign
- Symptom: after adding seven heroes, Act II mid-team wins fell (12-6 70% -> 3%).
- Cause: Game.StageWaves drew Act II enemies from the whole hero list.
- Fix: frozen GameData.Act2EnemyIds; golden fixture unity/Tests~/fixtures/stage-enemies.json.
- Prevention: test that adding a hero changes no stage; re-run `-- campaign 60` after any roster/pool/speed change.
- Related: L020
- Evidence: commit fe80ca1

## L007 - prepare-models overwrites the Unity credits file
- Status: solved
- Systems: build-pipeline, licensing
- Symptom: image-to-3D credits vanished from StreamingAssets/Models/CREDITS.md in built APKs.
- Cause: prepare-models.mjs copies public/game/models/CREDITS.md over it.
- Fix: edit the source file (public/game/models/CREDITS.md); both copies carry every section.
- Prevention: art-pipeline skill and character-td rules say "credit in both copies".
- Related: L001
- Evidence: commits ceed8fb, 04f41e5

## L008 - Better averages, worse frames
- Status: solved
- Systems: hero-models, character-td
- Symptom: clipping/face pass improved every metric; owner: "good in some frames, horrible in others".
- Cause: verdicts came from numeric scores, not from looking at every frame; rules fixed some poses and broke others.
- Fix: character-td role: sweep every frame of every clip, judge visually, ship only if no frame got worse.
- Prevention: .claude/agents/character-td.md; per-hero owner approval sheets.
- Related: L018, L019
- Evidence: fix/model-clipping (not shipped), fix/ctd-pilot

## L009 - Image-to-3D input rules
- Status: solved
- Systems: art-pipeline
- Symptom: unusable TRELLIS meshes (Odo, Marrow: torn; Poppy: backdrop card, fused legs; Maelis: odd weapon).
- Cause: props resting on shoulders/folded arms, legs together, flat backgrounds kept as geometry.
- Fix: props held down at the side or upright and clear of the body, legs apart, background cut out (unity/Tools~/artgen/cutout.py).
- Prevention: .claude/skills/art-pipeline/SKILL.md prompt rules.
- Related: L018
- Evidence: odo/marrow/poppy redos

## L010 - In-place mesh fixes didn't reach the game
- Status: solved
- Systems: hero-models, build-pipeline, materials
- Symptom: specks around Fennick, Brega, Holt and Nell still visible in build 190 after the "removed" fix shipped; a "stray prop" in front of Tansy.
- Cause: the mesh fix DID ship (the shipped GLBs carry 117/299/204/175 degenerate triangles where the islands were), but the islands were not what the tester saw. (1) The specks were material: heroes with no _glow PNG and no _mat PNG got no mask texture, so the shader's default white _MaskMap made them metal 1 / smoothness 1 everywhere - dark, metallic, sparkling on noisy fur (30 image-to-3D heroes). (2) Nell's "loose piece" is her own scythe blade resting on the ground in the crouching idle. (3) The prop in front of Tansy is Obrecht's 1.7 m pick, held mid-haft and pointing forward from the next gallery slot.
- Fix: HeroModels always builds a mask (neutral when there is no glow/metal map); Obrecht's pick turned in the hand (blenderforge/prop_turn.py); Nell idles with Idle_Loop. fix/ctd-roster-3.
- Prevention: before fixing a tester's visual report, reproduce it in a real build's gallery shot (CI smoke artifact) and identify the exact pixels; offline previews must use the game's material inputs (mask defaults included). See L004.
- Related: L001, L004, L023
- Evidence: docs/sigilbound/qa/inhouse-2026-11-28-4a2723d.md; CI smoke artifact of run 46107643548 (gallery-10); scratchpad ctd/smoke190/maskproof.png; still visible in build 207 (8610b2b): Nell piece, Holt speck, Fennick specks (docs/sigilbound/qa/evidence/8610b2b/fragments-nell-fennick-holt-brega.jpg)

## L011 - GUI.depth across scripts is unreliable
- Status: solved
- Systems: ui
- Symptom: the Windows update banner slipped behind screens after a bottom-nav tap.
- Cause: IMGUI ordering via GUI.depth between two MonoBehaviours isn't dependable.
- Fix: App draws overlays last in its own OnGUI (finally block) - Updater.Overlay().
- Prevention: one OnGUI owner for overlays.
- Related: L017
- Evidence: commit eaa4766

## L012 - Credentials through the proxy
- Status: solved
- Systems: art-pipeline, tooling
- Symptom: Gemini key couldn't be saved via the chat card; Hugging Face ZeroGPU quota exhausted.
- Cause: the card only injects Authorization headers; Gemini's native API wants x-goog-api-key; free ZeroGPU quota is tiny.
- Fix: Gemini via its OpenAI-compatible endpoint (Bearer), prepaid AI Studio credit; fal.ai `fal-ai/trellis` with Authorization: Key, saved as a network secret for queue.fal.run.
- Prevention: unity/Tools~/artgen scripts send placeholders; keys live only in environment secrets.
- Related: L009
- Evidence: .claude/skills/art-pipeline/SKILL.md

## L013 - Merge hygiene: changelog tags, step numbers, headings
- Status: solved
- Systems: docs, tests
- Symptom: changelog test failures after merges ("art:" tag, empty duplicate heading); PLAN steps duplicated.
- Cause: parallel branches each add lines/steps; the changelog test allows only new:/fix:/change:/balance: and non-empty dated entries.
- Fix: keep both sides, renumber duplicate PLAN steps, drop empty headings, merge contradicting lines.
- Prevention: lead checks duplicates after every merge.
- Related: L001
- Evidence: commits 53f4bb2, 39bf346

## L014 - Agents don't push to main
- Status: decision
- Systems: process
- Symptom: an agent brief that pushed straight to main was blocked by the safety classifier.
- Cause: unreviewed autonomous pushes to the default branch.
- Fix: agents push branches; the lead merges after owner approval and green gates.
- Prevention: every brief says "push your branch only".
- Related: L001
- Evidence: session 2026-11-28

## L015 - Events between OnEnable and Start
- Status: solved
- Systems: arena, rig-runtime
- Symptom: NullReferenceException in ArenaReactor.Push killed a battle's action loop (portrait tour, build 190).
- Cause: subscribed in OnEnable, arrays built in Start; a shock arrived in between.
- Fix: guard in Push (commit 108bacd).
- Prevention: build state in Awake or guard event handlers.
- Related: L016
- Evidence: docs/sigilbound/qa/evidence/4a2723d/portrait-nullreference.txt

## L016 - "skinned>0" is not a health check
- Status: solved
- Systems: ci, qa
- Symptom: smoke gate passed builds with exploded heroes and a mangled Titan.
- Cause: it only checked that animated models loaded.
- Fix: SBBOUNDS pose lines per gallery hero (head height, offset, top, width) fail on BAD.
- Prevention: extend guards whenever a new failure slips through.
- Related: L002, L003, L004
- Evidence: commit 4fd5a8a

## L017 - Per-screen UI fixes drift
- Status: solved
- Systems: ui
- Symptom: consistency review found ~45 small buttons, five "selected" styles, results differing by mode/orientation.
- Cause: fixes went into individual screens instead of shared components.
- Fix: fix in Ui.cs/App.cs/Skin/Palette; design rules as tests (UxRulesTests).
- Prevention: new screens must use shared components; rule tests in CI.
- Related: L011
- Evidence: scratchpad reviews/ux_consistency.md, fix/ux-consistency

## L018 - Spring cloth works only where cloth hangs free
- Status: solved
- Systems: hero-models, character-td
- Symptom: spring coat fixed Jasper; made Lazlo, Neve, Holt and Vorgrim worse.
- Cause: cloth that hugs the legs or has overlapping inner layers or arm weights can't be simulated cleanly.
- Fix: spring chains only for cloth hanging off the legs; otherwise re-skin first or regenerate the image with shorter cloth.
- Prevention: character-td notes; RIGGING.md.
- Related: L008, L009, L022
- Evidence: fix/ctd-pilot, fix/ctd-roster-1

## L019 - Face locks must be tight
- Status: open
- Systems: hero-models, character-td
- Symptom: a head lock fixed face drift but dragged manes, hoods and shells into the shoulders.
- Cause: the lock covered the whole head region, not just the face.
- Fix: in progress - tight face mask with soft falloff (fix/ctd-faces).
- Prevention: per-frame visual verdict.
- Related: L008
- Evidence: scratchpad reviews/clipping.md

## L020 - Balance ripples
- Status: solved
- Systems: balance
- Symptom: stage 13-6 swung (strong 88% -> 60%) after an unrelated pool change.
- Cause: stage eases are tuned against a specific enemy pool and roster.
- Fix: StageEase[77] 0.88 (commit in fix/gameplay-ui-gaps).
- Prevention: run `-- campaign 60` and `-- roles` after any roster, pool, speed or curve change.
- Related: L006
- Evidence: campaign_before.txt / campaign_after.txt

## L021 - Windows froze when unfocused
- Status: solved
- Systems: platform
- Symptom: battles froze when the Windows window lost focus.
- Cause: PlayerSettings.runInBackground false.
- Fix: Background.cs device preference (default keep running), optional mute.
- Prevention: desktop settings group.
- Related: L011
- Evidence: commit b6a0b0e

## L022 - One skeleton plus patch rules doesn't scale
- Status: decision
- Systems: animation, rig-runtime, hero-models, titan
- Symptom: stretching, exploded heroes (L002), mangled Titan (L003), reduced move sets.
- Cause: every hero forced onto one humanoid skeleton with legacy Animation and correction rules.
- Fix: owner decision 2026-11-28: skeleton library + Unity Humanoid retargeting + per-hero profiles (feat/skeleton-library).
- Prevention: docs/sigilbound/art/SKELETON_LIBRARY.md.
- Related: L002, L003, L018
- Evidence: owner message 2026-11-28

## L023 - Missing texture inputs fall back to shader defaults
- Status: solved
- Systems: materials, hero-models
- Symptom: some heroes looked dark, metallic and speckled; others with the same shader looked fine.
- Cause: optional per-hero maps (_glow, _mat) were only turned into a mask when present; with neither, the material kept the shader's default "white" _MaskMap = full metal and smoothness. Every new optional input needs a neutral fallback.
- Fix: neutral mask always built (metal 0, AO 1, glow 0, smoothness 90/255).
- Prevention: when adding a map or shader property, check what the default texture means for heroes that lack the file.
- Related: L010
- Evidence: fix/ctd-roster-3 commit c52e108

## L024 - A green main run can publish no APK
- Status: solved
- Systems: ci, qa
- Symptom: run 207 (8610b2b) showed linux-preview and windows green and unity-preview updated, but the android job was cancelled after 3 minutes and publish-android skipped; testers stayed on build 190.
- Cause: queue concurrency (L005) cancelled the waiting android job when newer pushes and dispatches arrived; the preview job still ran.
- Fix: the tester checks publish-android's result and the unity-latest build number, not only "the preview names the commit".
- Prevention: in-house reports state which APK build testers actually have; the lead re-runs or waits for the next main build before announcing.
- Related: L005, L016
- Evidence: docs/sigilbound/qa/inhouse-2026-11-28-8610b2b.md, run 207 jobs

## L025 - Headless tours run at 96 dpi
- Status: solved
- Systems: qa, ui
- Symptom: a 56 dp "Larger tap targets" overflow in the portrait battle row (build 190) never showed in any tour, even with the option seeded.
- Cause: Ui.Dp scales by Screen.dpi/160; the xvfb player reports 96 dpi, so every dp size sits at its unit floor and density bugs are invisible.
- Fix: check dp layouts by arithmetic at real densities (1080 px wide, 400-480 dpi, 390-unit portrait canvas) when reading the code; the portrait row now wraps (8610b2b).
- Prevention: tester definition 5c; a rule test that lays out the HUD rows at 56 dp and 480 dpi would catch it for good.
- Related: L016, L017
- Evidence: docs/sigilbound/qa/inhouse-2026-11-28-4a2723d.md (bug 4)

## L026 - Tours share one PlayerPrefs file
- Status: solved
- Systems: qa, tests
- Symptom: tours were not independent (changelog-seen and Act II intro flags carried over); accessibility options had no tour switch.
- Cause: the Linux player keeps PlayerPrefs in ~/.config/unity3d/unknown/unknown/prefs, shared by every run on the machine; the tour only resets the save.
- Fix: for a settings tour, back up the file, add sb.textSize, sb.palette (base64 string), sb.flash, sb.reduceMotion, sb.bigTaps, run tools/unity-shots.sh, restore the file.
- Prevention: a -sbPrefs flag or a seeding option in tools/unity-shots.sh (suggested); until then the tester restores the file after every seeded run.
- Related: L004, L025
- Evidence: docs/sigilbound/qa/inhouse-2026-11-28-4a2723d.md, docs/sigilbound/qa/inhouse-2026-11-28-8610b2b.md (a11y-1080x2400 tours)

## L027 - The preview can change between tour sizes
- Status: solved
- Systems: qa, ci
- Symptom: the f7ddd6b portrait tour would have downloaded a newer commit's preview; three sizes take about 20 minutes and main moves faster.
- Cause: tools/unity-shots.sh downloads unity-preview at the start of each run and every main build replaces it.
- Fix: re-read the preview notes before each size and stop if they changed; a newer commit that only touches unity/Tools~ (not built) is the same game and may stand in, said so in the report.
- Prevention: the tester's tour loop checks the notes per size (tours/<sha>/run-*.txt record it).
- Related: L005, L024
- Evidence: docs/sigilbound/qa/inhouse-2026-11-28-f7ddd6b.md (portrait run on 35ea51d)

## L028 - Painted plates hide card text after shared-skin changes
- Status: solved
- Systems: ui, qa
- Symptom: build 207 team-setup hero cards (prep and arena prep, every landscape size, 1024x768 and portrait) have the painted chip frame drawn over the name, level and power; the stage row button reads "TEA / M".
- Cause: (1) the UI consistency pass gave in-team cards the ChipOn look via `Ui.Plate`, which paints the `button_chipon` art as a 9-slice scaled to the card's height; that art's crown ornaments sit in the stretched middle column (outside the declared 33/36 px borders), so on a 48-unit card they smear into gold bands across the top and bottom thirds, exactly where the two text lines were placed at fixed offsets (r.y+2, r.y+24) with no reference to the frame. The preset chips had the same two-line problem. (2) "TEAM ▶": the 88-unit button left a 28-unit label room after the plate inset and the arrow, `ButtonPx` never shrinks at the Default text size (top == floor), and the button style had word wrap on, so Unity broke inside the word.
- Fix: branch fix/ui-regressions (PLAN 122). Cards use the small card frame (`Ui.CardFrame`, `UiLayout.CardFrameK`) plus the shared selected mark, and lay out from `UiLayout.HeroCard` inside `UiLayout.FrameInner` (skin.txt 9-slice borders x `UiLayout.FrameScale`, which `Skin.DrawFrame` now uses too). One-word and arrow labels are drawn without wrap and shrink to fit (`Ui.OneLinePx`: to 11 points, then sans); the stage Team button is 108 wide. Same pass: compact Home (camera shift, full-height column) and gear rows (name on its own line).
- Prevention: lay text out against the frame's content rect from Core arithmetic, never at fixed offsets from the outer rect; chip plates only for one-line labels; UxRulesTests "L028" checks card text inside the inner rect at 640/1067/1422 and portrait, one-line labels and the Team button's label room. Keep a tour shot of prep and the stage list in every gate and compare with the last report (UxRulesTests check sizes, not paint).
- Related: L017, L011, L025
- Evidence: docs/sigilbound/qa/evidence/8610b2b/prep-card-frames-cover-text.jpg, docs/sigilbound/qa/inhouse-2026-11-28-8610b2b.md (bugs 1-4), branch fix/ui-regressions

## L029 - Parallel branches take the same lesson and PLAN step ids
- Status: solved
- Systems: docs, process, tests
- Symptom: by 07:00 on 2026-11-28 three different lessons were called L023 (main, perf/textures-lod, fix/ctd-faces), perf/textures-lod also added L029-L025, and its PLAN step "119" duplicates main's 119 (UI/UX consistency).
- Cause: each branch takes the next id from its own base; BrainTests require ids sequential from L001, so a branch cannot reserve a higher id. Collisions are guaranteed whenever two branches add lessons or steps.
- Fix: the lead renumbers the incoming branch's lessons and steps at merge (next free id on main, update Related links, MAP.md rows and any "Lxxx" mentions in its docs); ROADMAP lists the pending renumbers.
- Prevention: merge checklist (L013) now includes lesson ids: after merging, run BrainTests ("lesson ids are unique/sequential") and grep the branch's docs for the old ids. PMO flags colliding branches in ROADMAP "In progress".
- Related: L013, L014
- Evidence: git diff origin/main...origin/perf/textures-lod and ...origin/fix/ctd-faces (LESSONS.md, PLAN.md); ROADMAP.md 2026-11-28

## L030 - Status can only be as good as what is pushed
- Status: solved
- Systems: process, docs, ci
- Symptom: the PMO brief listed feat/physics-standard, fix/ctd-headgear and the Lazlo/Neve/Holt remodels as in flight, but none was on origin; and the testers kept NO-GO build 190 for hours while builds 191-208 of main were all cancelled by the queue (L005), so nothing merged after 190 had been seen in a real build.
- Cause: agents work locally until done; the CI queue keeps one pending run, and branch dispatches plus frequent main merges keep cancelling main's build.
- Fix: ROADMAP marks such work "Not on origin yet" and says which main build last completed instead of assuming merged means built.
- Prevention: agents push a WIP branch as soon as they start (push their own branch only, L014); PMO checks `gh run list -w "Sigilbound Unity" -b main` for the last completed main build before reporting what testers have; after a hotfix for a NO-GO build, hold other merges and dispatches until main's build publishes.
- Related: L005, L014
- Evidence: git ls-remote --heads origin (2026-11-28 07:00); Actions runs 191-208 cancelled, 209/210 running

## L031 - The recommended store package name was already one of our app ids
- Status: open
- Systems: platform, process
- Symptom: READINESS and ROADMAP recommended `com.sigilbound.game` for the Play app "so it sits beside the tester app", and the owner chose it; only while wiring it did the comment in ProjectSetup.cs show it is also the old Capacitor web-wrapper app's id (`mobile/`, release `android-latest`, signed with the committed test key).
- Cause: the review compared the store id only with the Unity tester id (`com.sigilbound.unity`) and never grepped the repo for other apps (`mobile/android/app/build.gradle`, `capacitor.config.json`). Android refuses to install an app over one with the same id and a different signing key.
- Fix: kept the owner's permanent choice (feat/store-config, PLAN step 123); READINESS item 5, PLAY_CONSOLE and ROADMAP now say to uninstall the old Capacitor app before installing from Play, and suggest retiring or re-iding `android-latest` before the closed test.
- Prevention: before recommending an app id, grep the whole repo (`grep -rn "com\.sigilbound\." --include=*.gradle --include=*.json --include=*.cs`) and list every app that already uses it.
- Related: L030
- Evidence: mobile/android/app/build.gradle (applicationId "com.sigilbound.game"); unity/Assets/Sigilbound/Editor/ProjectSetup.cs; docs/sigilbound/store/READINESS.md item 5

## L032 - The battle cameras never see the sky
- Status: open
- Systems: sky, scenes
- Symptom: the owner asked for a sun disc "in view for most of the day"; placed at its true elevation it is off screen in the menu and every default battle view, at any hour.
- Cause: by design (Scenery.cs ridge comment, art review M1) the menu camera looks 30 degrees down with a 42 degree lens and the Three-quarter battle view 30 degrees down with 50 degrees, so the top of the frame is 5-10 degrees below the horizon (the boss tilt adds 6, reaching about +1); painted ridges fill the top. Only low views (Cinematic, portrait's wider lens, the lair reveal, the Titan's views) show sky. Also: the sky arc and ridges are Sprites/Default (no depth write, no fog), so their order is purely the render queue, and the arena's linear fog would erase anything ~42 m out drawn with a fogged shader.
- Fix: branch feat/real-sun keeps the disc exactly on the light's line (owner rule) and draws it at 42 m from the camera with its own unfogged additive shader (Shaders/SigilSkyGlow.shader, queue 2996 between the sky 2995 and the ridges 2997-2999); the shadows show the sun's direction in every view. ShotsMode's SkyTour adds a "-up" frame per case, the same eye turned toward the disc.
- Prevention: before promising something "visible" in the arena, project it through BattleLayout.View for the default preset at 16:9 and portrait (SkyTests does this for left/right); owner decision needed if the disc should also show in the default view (a raised "visual" elevation, or a higher boss-style tilt), which would break the light/disc alignment rule.
- Related: L004, L034
- Evidence: Core/BattleLayout.cs Cameras (angle: pitch 30, FovFor 50), Arena.FrameMenu (pitch ~30.5, fov 42), Client/Scenery.cs backdrop comment; branch feat/real-sun SkyTour

## L033 - Unity adds precise location when a script uses Input.location
- Status: open
- Systems: sky, platform
- Symptom: none yet (caught while wiring the real-sun location); the owner asked for coarse location only.
- Cause: Unity's Android build adds ACCESS_FINE_LOCATION to the unityLibrary manifest on its own whenever any script references Input.location; the project has no Plugins/Android manifest to override it, and the permission would show on the Play listing as precise location.
- Fix: Editor/AndroidLocationPermission.cs (IPostGenerateGradleAndroidProject, both tester and store builds) removes ACCESS_FINE_LOCATION from the generated manifest and adds ACCESS_COARSE_LOCATION; SkySun asks only for coarse and starts the service at 5 km accuracy.
- Prevention: SkyTests checks the post-processor is not store-gated and removes the fine permission; after the first AAB/APK with it, run `bundletool dump manifest` / `aapt dump permissions` and confirm COARSE without FINE (READINESS item 11). Not yet verified that Input.location starts with only coarse granted on every Android version: check on a phone.
- Related: L031
- Evidence: Editor/AndroidLocationPermission.cs, Client/SkySun.cs, docs/sigilbound/store/READINESS.md items 8 and 11

## L034 - The authored sun stood behind the camera
- Status: open
- Systems: sky, scenes
- Symptom: drawing a disc "exactly along the light" with the old light (Euler 48, -30) would have put it behind the default camera, invisible; facing the camera toward the equator flips it in front and backlights the heroes.
- Cause: every authored mood's light yaw (-20 to -62) travels away from the camera: the sun sat behind its right shoulder so faces were front-lit (art review M2 "hero exposure stays constant" relies on it). The owner's "face the equator" rule puts the real sun in front of the camera for most of the day.
- Fix: under the real sky the Fill light moves behind the camera (camera heading + 12, pitch 26, 0.5; 0.42 by moonlight) instead of the old far-side rim fill; ambient keeps its floor; the light's pitch keeps a 12 degree floor. "Real sun" off keeps the authored light exactly as before.
- Prevention: judge hero exposure in the SkyTour frames of a real build (L004) before approval; if heroes read too dark, raise the fill or ambient under the real sun rather than turning the sun back behind the camera.
- Related: L032, L004
- Evidence: Core/SceneGen.cs LightOf (authored yaws), Client/Arena.cs Build (Fill), branch feat/real-sun
