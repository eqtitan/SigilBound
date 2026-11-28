# Sigilbound roadmap

Owned by the **pmo** agent (`.claude/agents/pmo.md`). Status of the project, what's next,
what's blocked, and decisions made. Update it whenever work lands or plans change.

_Last updated: 2026-11-28, about 07:00 UTC (main `d911552`)._

**How we know (sources for this page):** `git log origin/main`, `git ls-remote --heads origin`, `PLAN.md` steps 99–121,
`unity/Assets/Resources/Changelog.txt`, the in-house tester reports on branch `qa/reports`
(`docs/sigilbound/qa/inhouse-2026-11-28-{a6d2fee,902e814,f7ddd6b,4a2723d}.md`), `store/READINESS.md`,
`qa/PHONE_CHECK.md` and the GitHub Actions run list. "Not verified" means nobody has looked at it in a real build yet.

## Where things stand

> **Real-device check (2026-11-22):** the web/Capacitor app v0.15.0 ran very smoothly on the owner's Galaxy S26+.
> **The Unity build has never been measured on a real phone.** The phone check (step 117) exists; nobody has run it yet.

> **Builds right now:** the live testers have **build 190** (`4a2723d`), which the in-house tester rated **NO-GO**
> (mangled Titan, an arena crash in portrait). Both are fixed on main (`108bacd`, `58ae756`), but every main build
> from 191 to 208 was cancelled by the CI queue (L005). **Build 209** (`2b75de4`, all of this morning's merges) and
> **210** (`d911552`) are running now. Nothing merged after build 190 has been seen in a real build of main yet.

| Area | Status | Notes and how we know |
|---|---|---|
| Unity Android app (tester APK, `unity-latest`) | 🟡 Build 190 live, NO-GO; fix build running | 165 MB, targetSdk 36, 64-bit, 16 KB pages (READINESS items 1–3, read from the build-188 manifest). APKs publish only after the smoke tour passes (`ci/gate-publish`, merged). |
| Gameplay (Unity) | ✅ Feature-complete for a first test | 150+ heroes, campaign Acts I–II with Hard, Titan, Trials, arena, gear, skill tomes, Auto Climb (step 112), roster tools (107), accessibility (115). 326+ tests pass on main. |
| Balance | ✅ Patched (step 99), 🟡 owner check pending | Probes: Act II Light/Shadow mid team 87% (build 180 report), 10-1 at 63%, role spread 5.9 points, every hero in 40–60%. 15-6 finale still 8% for a mid team (by design, to confirm). |
| Hero models | 🟡 Usable; quality work in flight | 28+ heroes remodelled from their portraits (portrait canon). Known defects: some faces warp (fix on hold), head accessories float (fix in progress), long weapons pass through heads (deferred to the rebuild). |
| Character system rebuild | 🟡 Pilot built, not merged | `feat/skeleton-library`: skeleton library + Unity Humanoid retargeting + per-hero profiles, pilot on 5 heroes, physics standard on library rigs. Pilot build (run 46118984547) dispatched 06:57. |
| Campaign scenes | ✅ Merged (step 118); 🟡 owner look pending | All 90 stops and 15 lairs, real screenshots of build 182 checked. |
| UI/UX | ✅ Merged (steps 106, 109, 116, 119, 120) | Shared components and design rules as tests (UxRulesTests). Owner phone checks pending; most screenshots were mock-ups, not real builds. |
| Google Play release | ⛔ Waiting on the owner | Code ready (store AAB task, updater left out). Needs the developer account, upload key, privacy contact (see "Waiting on the owner"). Package name decided: `com.sigilbound.game`, store build only (`feat/store-config`, step 123). |
| iOS | ⏸ Parked | Needs an Apple Developer account ($99/year). |
| Friends & chat | ⛔ Built, not live | D1 database missing. Hidden in the store build for v1 (owner decision 2026-11-28, `feat/store-config`); tester builds keep it. |
| Web game (`public/game/`) | ⏸ Maintenance only | v0.21.1. New work goes into Unity. |
| Hero card art | ✅ Done | 150 painted portraits (step 92); 3D busts as fallback. |

### Shipped today (merged to main, 2026-11-28)

| What | PLAN step / commit | Seen in a real build? |
|---|---|---|
| Save safety, model memory cache, smaller APK, store build without the updater (the four P0s) | 100–103 | Yes (builds 169–190); store AAB never built |
| 60 fps default, 30 in auto-replay; quit rule for Titan and arena | 104, 105 | Tests; phone not measured |
| Phone-sized screens and controls; polish pass | 106, 109 | Landscape yes (build 175); portrait fixed in 111 (build 180) |
| Roster tools (bulk release, rank-up fodder, Rank up all) | 107 | Rules yes (build 175); panel not in the tour |
| Titan scene; Titan's own moves for its new body; Titan runtime fix | 108, 114, `58ae756` | Moves broke in build 190 (L003); fix proven in branch run 203, main build 209 running |
| Load hitches, garbage, speed rules, original 24 rebalanced | 110 | Tests (build 180) |
| In-house tester fixes (builds 175/176) | 111 | Yes (build 180) |
| Auto Climb | 112 | Yes (build 180) |
| Digitigrade legs for Tansy and Corran | 113 | Exploded in build 180 (L002), rolled back, fixed `411e789`; plain rigs in build 190 |
| Accessibility settings | 115 | Yes (build 190, two layout nits) |
| Gameplay and UI gaps; release readiness (phone check, Play review, Android CI pin) | 116, 117 | Not yet (build 209 running) |
| Procedural campaign scenes | 118 | Branch build 182 |
| UI/UX consistency; battle visuals | 119, 120 | Not yet (build 209 running) |
| Heroes without a glow map get a neutral mask; Obrecht's pick, Nell's idle | `658af9c` (L010, L023) | Not yet (build 210 running) |
| Six new heroes, Vessa, new Titan model, portrait-canon remodels, CI publish gate, in-house tester agent, character TD agent, art pipeline, project brain | `release/models-1`, `feat/portrait-canon`, `ci/gate-publish`, `8b1d831` | Yes (builds 176–190) |

### In progress (branches on origin, not merged)

| Branch | What | State (how we know) |
|---|---|---|
| `feat/skeleton-library` | Character system rebuild: skeleton library, Humanoid retargeting, per-hero profiles; physics standard (body colliders, chains, sockets) on library rigs | Pilot (Titan, Ysolde, Augustin, Corran, Nyssa) + `RigLibraryTests`; design in `art/SKELETON_LIBRARY.md`; pilot build dispatched 06:57. Still to come per the owner's brief: sockets/grips/two-hand IK, separate accessory meshes, twist bones and facial basics, life layers and partial ragdoll, per-move CI checks, then hero batches (~2 weeks). |
| physics standard for all heroes (owner's `feat/physics-standard`) | Colliders and physics for every hero, remediation in batches | **Not on origin yet.** The first part is commit `75423fa` on `feat/skeleton-library`. |
| `perf/textures-lod` | KTX2 hero textures, LOD1 meshes (P2-1, P2-2) | LOD1 skinning bug found and fixed on the branch (`8e4b8b1`). **Merge note:** its PLAN step "119" and lessons L023–L025 collide with main; renumber to step 122 and to the next free lesson ids on main at merge time (L024). |
| `fix/ctd-faces` (step 121) | Face-only lock for 85 heroes | **On hold** until the headgear fix lands (owner decision). Sheets in scratchpad `ctd_faces/`. Its lesson L023 also collides. |
| owner's `fix/ctd-headgear` | Floating head accessories + a character-TD skill | **Not on origin yet.** |
| Lazlo (option A), Neve (option B: full braid with physics), Holt (option A) remodels | Owner-picked remodel options | **Not on origin yet.** |
| `fix/ctd-roster-3` `0f3ed27` | Long weapons out of the head (Tallow, Saoirse, Hobb, Hask) | **Deferred**: waits for the rebuild's grips and two-hand IK. The rest of this branch is on main. |
| `feat/new-heroes-6` | One `i3d_rig.py` tweak left | The heroes themselves shipped via `release/models-1`. |
| `qa/reports` | In-house reports for builds 175, 176, 180, 190 | Docs only; merge any time. |

### Blocked

| What | Blocked on | Since |
|---|---|---|
| Play Store closed test | Owner: developer account, upload key, package name, privacy contact | 2026-11-28 (READINESS) |
| Real performance numbers (memory, heat, frame time) | Owner/testers: run the phone check on a real phone | 2026-11-27 |
| Faces pass (`fix/ctd-faces`) | Engineering: headgear fix first | 2026-11-28 |
| Weapon-through-head fix | Engineering: skeleton rebuild (grips, two-hand IK) | 2026-11-28 |
| Owner checks of steps 99–121 (see each step's "Owner check") | Engineering: a real build of main (build 209/210) for screenshots | 2026-11-28 |
| Friends & chat | Owner: D1 database ID + the v1 decision | 2026-11-22 |
| iOS / TestFlight | Owner: Apple Developer account | 2026-11-22 |

## Review findings (2026-11-27): what's done and what's left

Six reviews (art, UX, QA, performance, code, mechanics) of build 169 (`3430f88`, v0.21.1). Full plan with P2/P3 items:
scratchpad `reviews/PMO_PLAN.md` (outside the repo). Status below is from PLAN.md and the in-house reports.

**P0 (all four done)**
1. ✅ Failed save no longer overwrites the real save; backup and corrupt copy (step 100).
2. ✅ Hero model memory: reference-counted cache, frees beyond 8 (step 101). GPU-compressed textures are the follow-up on `perf/textures-lod`.
3. ✅ APK size: 195 → 165 MB, CI budget 190 MB (step 102). Play Asset Delivery planned for when it passes 185 MB (READINESS).
4. ✅ Store build compiles without the updater (step 103). **Not verified on an actual AAB** (never built).

**P1**
- ✅ Balance patch as one change: Light/Shadow ×0.92, role speed shift, chapter-10 factor, gentler Act II, Titan tiers 15k/40k/80k/140k/220k, 38 heroes retuned (step 99; Act II pool freeze 111; 13-6 ease 116).
- ✅ Combat bugs: counter-Break, Stun→Break chain, AI kill estimate, healing on results, dodge only on absorbed hits (step 99, `MechanicsTests`).
- ✅ Readability: enrage counter, Broken icon and banner (step 99). **Owner check pending** (no screenshots of the Broken look yet).
- ✅ Quit rule: Titan and arena tries count at the start (step 105).
- ✅ Screens and controls: 48 dp taps, Continue Campaign visible, safe areas, switches, colour-blind cues (steps 106, 111, 119).
- ✅ Heat: 60 fps default, 30 in auto-replay (step 104). **Not measured on a phone.**
- ✅ Art identity: portrait is canon; 28+ heroes remodelled from portraits; Vex/Aurex redesigned and crosses replaced by sigil medallions (`release/models-1`, `feat/portrait-canon`).
- ✅ Process: APK publishes only after the smoke tour (`ci/gate-publish`); combat rules have tests (step 99); pose guard SBBOUNDS (L016).
- ⬜ Hartwin trim: approved 2026-11-27, **no commit found**; he tests at 42–57%. Confirm whether it's still wanted.

**P2 (done)** P2-3/4/20/21 (110), P2-9/10 (120), P2-14/15 (116), P2-15/16/18/19/23/24 (109), P2-17 (115), P2-26 (117).
**P2 (in flight)** P2-1/P2-2 textures and LOD (`perf/textures-lod`).
**Left from the review:** Light/Shadow counter hint on the prep screen, Hit_Big × Pace, defeat reasons (step 99 "Not in this patch"); Play Asset Delivery (when needed); every owner device check listed in steps 99–121.

## Waiting on the owner

Each item is something only you can do. Most urgent first.

1. **Start the Google Play developer account now** (longest wait).
   - Go to play.google.com/console, sign up as a personal developer, pay US$25, complete the identity check (takes days).
   - New personal accounts must run a **closed test with at least 12 testers for 14 days in a row** before going public.
     Line up 12+ people with Android phones and Google accounts (a Google Group is easiest).
   - Once the account works: Testing > Closed testing > Create track, add the group, share the opt-in link.
2. **Make the upload key and add four GitHub secrets**, then run the first store build.
   - On your computer: `keytool -genkeypair -v -keystore sigilbound-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000`.
     Back up the file and both passwords somewhere safe (never in the repo or chat).
   - GitHub repo > Settings > Secrets and variables > Actions > New repository secret, four times:
     `ANDROID_UPLOAD_KEYSTORE_BASE64` (output of `base64 -w0 sigilbound-upload.keystore`), `ANDROID_UPLOAD_KEYSTORE_PASS`,
     `ANDROID_UPLOAD_KEY_ALIAS` (`upload`), `ANDROID_UPLOAD_KEY_PASS`.
   - Actions > Sigilbound Unity > Run workflow > task **build-store-aab** on `main`. Download the `sigilbound-store-aab` artifact.
     The first run may need an engineering fix.
   - On the first upload, accept **Play App Signing**.
3. ✅ **Package name** decided 2026-11-28: `com.sigilbound.game` for the store build; the tester app keeps `com.sigilbound.unity`
   (`feat/store-config`). The old Capacitor test app used the same id: uninstall it before installing from Play.
4. **Privacy policy:** send the contact email for the policy and pick where it's hosted. *Recommended: the GitHub page of
   `docs/sigilbound/store/PRIVACY.md` for now* (the `sigilbound.pages.dev/privacy` address is 404). Then paste the URL in
   Play Console > App content > Privacy policy.
5. ✅ **Friends and chat in v1** decided 2026-11-28: hidden in the store build (`feat/store-config`); turn them on in an update once
   the server is live and someone reads reports weekly.
6. **Run the phone check on a real phone** (about 30 minutes, `docs/sigilbound/qa/PHONE_CHECK.md`) once build 209/210 is out:
   Settings > Graphics quality > Auto > Run phone check, paste the report; again at Low; play 20 minutes; a third check while warm.
   This gives the first real memory, heat and frame-time numbers.
7. **Look at today's work and say yes or no** (once build 209/210 screenshots are posted): campaign scene sheets and the
   boss-stage camera tilt; the balance/enrage/Broken look; the skeleton-library pilot frames; the face sheets (after headgear).
8. **Older items, still open:** Cloudflare D1 database ID and merging PR #1 (only if Friends goes on); the paid UAL clip
   tier (start free); screen-shake setting; animation tests T1–T12 (step 69); Apple account (only for iPhone).
9. **Later:** a Discord server for testers and players (not needed for the closed test).

## Next up (priority order)

1. **Get build 209/210 to the testers and run the in-house tester on it.** Live testers have a NO-GO build; the fixes are merged but unproven. *Engineering + in-house tester. Don't push to main or dispatch while it's pending (L005).*
2. **Headgear fix (`fix/ctd-headgear`), then the faces pass (`fix/ctd-faces`).** Floating hats and warped faces are the most visible model flaws; faces wait on headgear. *Character TD.*
3. **Skeleton library: finish the rig features and the pilot verdict.** Sockets/grips/two-hand IK, accessory meshes, twist bones and facial basics, life layers and partial ragdoll, per-move CI checks; owner approval of the pilot frames, then hero batches. It removes the patch layer behind L002/L003 and unblocks the weapon-through-head fix. *Engineering + character TD.*
4. **Physics standard for all heroes, in batches.** Builds on the library rigs; every hero gets colliders so cloth, hair and props stop passing through bodies. *Engineering + character TD.*
5. **Merge `perf/textures-lod`** after renumbering its step and lesson ids. Cuts memory and loading on phones; needs one real build. *Engineering.*
6. **Store release prep that doesn't wait on the owner:** ~~hide Friends in `SB_STORE` builds~~ (`feat/store-config`), in-game privacy link, fresh store screenshots from a real build (READINESS items 7, 9, 14). Short tasks that remove days from the release path. *Engineering.*
7. **Lazlo A, Neve B (braid with physics), Holt A remodels.** Owner-picked; Neve's braid should use the physics standard. *Art pipeline + character TD.*
8. **Review leftovers:** Light/Shadow counter hint, defeat reasons, Hit_Big × Pace. Small player-facing clarity wins. *Engineering.*
9. **Play Asset Delivery** before the APK passes 185 MB. Not needed for the first upload. *Engineering.*

## Backlog
- Arena against friends' teams using the friends server.
- Hero voice lines.
- On-demand hero downloads (Play Asset Delivery, to cut the install to ~60 MB).
- iOS / TestFlight once there's an Apple account.

## Release checklist (each tester build)
- [ ] Version/changelog: a dated "## date | title" entry in `unity/Assets/Resources/Changelog.txt` with new:/fix:/change:/balance: lines (L013).
- [ ] CI green: checks, android, linux-preview smoke tour (SBBOUNDS pose lines all ok), publish-android, windows.
- [ ] In-house tester report on the exact build (`docs/sigilbound/qa/inhouse-<date>-<sha>.md`), GO or GO WITH NOTES.
- [ ] Visual changes judged from real smoke frames, not previews (L004).
- [ ] Install over the previous APK on a real phone and confirm progress is kept.
- [ ] Phone check report from a real phone at Auto and Low (`docs/sigilbound/qa/PHONE_CHECK.md`).
- [ ] For a store build: walk `docs/sigilbound/store/READINESS.md`.

## Decisions log
- **2026-11-28:** **Project brain** (`docs/sigilbound/brain/`): every session reads the lessons before work and adds lessons when done; BrainTests check the format.
- **2026-11-28:** **In-house tester on demand**: an agent that tours each build at three screen sizes and writes a GO / NO-GO report before the live testers rely on it; APKs publish only after the smoke tour passes (`ci/gate-publish`).
- **2026-11-28:** **Character system rebuild** (L022): a skeleton library (biped, digitigrade biped, brute, multi-leg, add-on chains) with Unity Humanoid retargeting and per-hero profiles, CC0 and own clips only (no Mixamo), switched on per hero with fallback. Pilot: Titan, Ysolde, Augustin, Corran, Nyssa.
- **2026-11-28:** **Rig features** in the rebuild: sockets, grips and two-hand IK; accessories as separate meshes; twist bones and facial basics; life layers and partial ragdoll; CI checks per move.
- **2026-11-28:** **Colliders and physics standard for every hero**, remediated in batches; cloth, hair and braids simulate against body colliders.
- **2026-11-28:** **Faces pass on hold** until floating headgear is fixed; the **weapon-through-head fix waits** for the rebuild's grips and IK.
- **2026-11-28:** **Remodels:** Lazlo option A, Neve option B (full braid with physics), Holt option A.
- **2026-11-28:** **Titan option C** (hunched basalt colossus) with its own moves: Bedrock Hammer, Faultline Leap, Heartfire Torrent, same damage (step 114).
- **2026-11-28:** **Character TD role**: every frame of every clip judged visually; ship only if no frame got worse (L008).
- **2026-11-28:** Roster tools (bulk release, rank-up fodder, Rank up all) and Auto Climb approved and built (steps 107, 112); animal-folk get digitigrade legs (step 113).
- **2026-11-28:** All campaign scenes built procedurally around the battle cameras; image-to-3D landmarks within a $5 default budget (`art/CAMPAIGN_SCENES_PLAN.md`).
- **2026-11-28:** Agents push their own branches only; the lead merges after owner approval and green gates (L014).
- **2026-11-27:** **Portrait is canon** for the ~30 heroes whose model differed; models remodelled from portraits. Vex and Aurex regenerated; real-world crosses replaced by sigil medallions.
- **2026-11-27:** **60 fps by default**, the screen's 90/120 Hz as an opt-in, 30 in auto-replay (replaces the 2026-11-25 "Auto" recommendation; step 104).
- **2026-11-27:** **Quit rule:** leaving a Titan or arena fight doesn't give a free retry; the try counts at the start (step 105).
- **2026-11-27:** **Balance patch as one change:** Light/Shadow ×0.92, role speed shift (Attack +8, Defense −2, HP −4, Support 0), smaller chapter-10 jump, gentler Act II, Titan tiers 15k/40k/80k/140k/220k (step 99). Hartwin trim approved (not yet done).
- **2026-11-27:** **Play Asset Delivery** for the store build: yes, when the APK nears 185 MB (READINESS plan).
- **2026-11-25:** The owner asked the pmo to set up a character mechanics agent: research common mechanics, build the battle animations, match the device's refresh rate, and send animation tests to the owner. Done: `.claude/agents/character-mechanics.md`, `MECHANICS.md`, PLAN phase G (steps 64–69). Rules: time-based animation, CC0 clips only, UAL first.
- **2026-11-22:** Focus is the Android app. Fully free to play, with no shop or paid currency.
- **2026-11-22:** Unity port resumed in parallel (app id `com.sigilbound.unity`, release `unity-latest`); it is now where new work goes.
- **2026-11-22:** Every asset must be CC0 or owned, with provenance in `models/CREDITS.md`.
- **2026-11-22:** Hero card art from the owner's AI-generated images or commissions (done: 150 painted portraits).
- 2026-11-28: Old Capacitor test app (`android-latest`, id com.sigilbound.game) retired so the id belongs to the Play Store build; its publish step is disabled (owner).
