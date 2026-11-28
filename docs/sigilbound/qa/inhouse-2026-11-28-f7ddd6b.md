GO WITH NOTES: no crashes and every blocking check passes, and all of my earlier findings are fixed (Act II is back to 87% for a mid team) and Auto Climb behaves correctly in every area, but the two heroes given digitigrade legs, Tansy and Corran, render as exploded, stretched geometry at every screen size.

# In-house test: build 180 (f7ddd6b), 2026-11-28

- **Build under test:** `f7ddd6b`, the newest main commit that changes the game. Since build 176 (`902e814`) it adds:
  - the polish batch: named skills, WEAK/RESIST, richer results, dungeon prep, one pop-up at a time, Reset save moved into Settings;
  - the performance and balance batch: model loading spread over frames, lazy sounds, Slow/SpdUp ±20%, a gear speed cap of +50, 54 heroes retuned, and 13-6 / 15-6 eased;
  - Windows keeps running when unfocused, and the update banner is drawn last;
  - the fixes for my earlier reports;
  - Auto Climb;
  - the portrait batch: 28 heroes remodelled from their portraits, plus digitigrade legs for Tansy and Corran.
- **Commits after it:** main has since moved to `b5a8401`. `35ea51d` only adds art-pipeline tools under `unity/Tools~`, which is not built into the game. `7b06d47` (spring bones) is **not** covered by this report.
- **CI run 180:** every job green: checks, android, linux-preview (smoke tour passed), publish-android and windows. The APK is build **180**, 165,225,551 bytes (165.2 MB).
- **Already live:** build 181 (`35ea51d`, the same game) is on `unity-latest` now. The live testers already have everything below, including the Tansy/Corran bug.
- **Preview check:** the 1280x720 and 2400x1080 tours downloaded `unity-preview` while it read "commit f7ddd6b (smoke tour passed)". Before the portrait tour started, the preview moved on to `35ea51d`. That commit only changes `unity/Tools~`, so the game is identical, and I ran 1080x2400 on it.
- **Previous report:** `inhouse-2026-11-28-902e814.md`. I did not tour `648aa4b` separately: its game code is identical to this build except for the portrait remodels and the digitigrade legs (the battle rules are byte-identical), so its tests and probes stand for both.

## 1. What's new and whether it works

| Change | Status |
|---|---|
| Act II enemies frozen to the pre-new-heroes pool | **Verified.** Tests show the golden stage-enemy list matches, "0 of 90 stages moved" when a hero is added, and none of the seven newest heroes are in Act II. The probe has Act II Light/Shadow mid back at **87.1%** (was 62.3%). |
| Undo keeps saved-team and roster order | **Verified.** There is a test, and my repro now gives `[8,9]` → release → undo → `[8,9]`. 0 failures over 1,500 random rosters. |
| Portrait battle HUD: big round buttons, Pause, ally/enemy marks | **Verified** in 62-combat-ui at 1080x2400. It has Leave, View, Climb, Replay, Auto, 1x and Pause, and the chips carry shield and skull marks. Evidence: `portrait-hud-fixed.jpg`. |
| Portrait campaign: Normal/Hard and Act I/II switches, solid locked chapters | **Verified** (05 and 11 at 1080x2400). Evidence: `portrait-campaign-fixed.jpg`. |
| Stage-list names no longer cut off | **Verified.** The level moves under long names ("Aurelia / Lv4"). Evidence: `stage-rows-fixed-1280.jpg`. |
| Release review marks every copy with "!" | **Verified in code** (`ReleasePanel.cs:301` uses `plan.LastCopyIds`). The panel itself is not in the tour. |
| Titan arrival filmed between the heroes; name tag clear of the banner | **Verified.** The face is visible between Ysolde and Pell, and in titan-5 the name tag sits below "SEARING COIL". Evidence: `titan-intro-fixed-2400.jpg`. |
| Wide heroes get more room in line-ups | **Verified**: gallery pages 38 and 39 no longer overlap. |
| No T-pose on Home while models load | **Verified**: no T-pose anywhere. **But** at 2400x1080 Home was captured with Pell's and Odo's spots **empty**, models not yet loaded (S4, bug 5). |
| Auto Climb (Climb in the HUD and on results, Climb Summary) | **Verified.** Climb shows in the landscape and portrait HUD, and "Replay: Off / Climb: Off" shows on results. The rules are covered by 41 climb checks in the tests plus my simulation (2c). The countdown, Stop button and summary are not in the tour. |
| Windows keeps running unfocused; update banner on top | The setting row shows on desktop only (`Background.Supported`), as it should. Behaviour on Windows **needs a Windows PC**. |
| Polish batch | Named skill buttons, the Status button and the richer results (Hard/Titan/dungeon) **verified** in shots and tests. Reset save is gone from Home and sits in Settings. The pop-up queue is covered by PolishTests. |
| Performance batch | Covered by HotPathTests and FrameRate tests. Smoothness **needs a phone**. |
| Balance: Slow/SpdUp ±20%, gear speed cap +50, 54 heroes retuned, 13-6 and 15-6 easier | Probes in section 3. 13-6 is easier for the mid team (15% → 43%) but **harder for the strong team (88% → 60%)**. Advisory. |
| 31 heroes with new portrait-matched models | **Mostly verified.** All load real models. **Tansy and Corran are broken** (S2, bug 1). Several others show small speckled fragments (S4, bug 4). |
| Tansy and Corran bend their legs like a hare and a goat | **Broken** (S2, bug 1). The tests on the leg clips pass, but in the running game both models explode. |

## 2. Bugs (most severe first)

1. **S2: Tansy and Corran render as exploded, stretched geometry.** This is the digitigrade-legs change.
   - **Where:** the gallery at all three sizes: page 28 has Tansy, page 29 has Corran. The same model path is used on Home, in battle and on the summon reveal.
   - **Tansy:** her body is smeared into a huge teal sheet across the arena, with her head on the ground and a foot floating apart.
   - **Corran:** his slot is **empty**. His body has been flung up into the sky as a long upside-down spike, off the top of the frame at 2400x1080.
   - **Expected:** an upright hare and goat with backward-folding hocks.
   - **Why nothing caught it:** the tests check the bone chains and the baked clips (all pass). shots.log says "4/4 real models" and the smoke check only looks for `skinned>0`.
   - **Likely cause:** the runtime layering in `Client/DigitigradeLegs.cs` (the "~legs" twin states on layer 1). Both heroes looked normal in build 176.
   - **Impact:** two summonable heroes are unusable to look at, and anyone who owns them sees it in battle and on Home. Neither is in any campaign enemy list (checked against `fixtures/stage-enemies.json`).
   - **Workaround:** don't use them.
   - **Not confirmed on a phone:** all my tours run the Linux build with software rendering. Ask a live tester to open Tansy or Corran.
   - **Evidence:** `evidence/f7ddd6b/tansy-broken-2400.jpg`, `corran-broken-2400.jpg`, `tansy-broken-portrait.jpg`, `corran-broken-portrait.jpg`.
   - **Suggested permanent check:** in the tour, compare each gallery hero's screen-space bounds with its slot, so an empty slot or a body 3× its normal size fails the smoke.
2. **S4: Changelog contradiction.** The 2026-11-28 entry has two Titan lines that disagree. Line 12 says "filmed between your heroes", line 23 says "filmed from behind your heroes looking up at its face". Players see both in What's New. Remove line 23.
3. **S4: A dungeon climb that clears the top floor with the last run of the day says "Out of dungeon runs for today", not "Reached the end".** `AutoClimb.NextTarget` checks runs before the top floor. Found by my simulation (2c). Wording only.
4. **S4: Speckled fragments on some of the new models.** Small dark specks and shards float just off the silhouette of Fennick (gallery 10), Brega (gallery 9) and Holt (gallery 26). A loose curved piece lies by Nell's feet (gallery 20). They are visible at 2400x1080 and blend in at normal battle zoom. Evidence: `remodels-a.jpg` to `remodels-d.jpg`. Ask the live testers to look close up.
5. **S4: Home at 2400x1080 was captured with two empty spots.** Pell and Odo had not loaded yet; the 1280 tour shows all three. Spreading model loads over frames swapped the old T-pose for blank spots. Live testers should say how long Home looks empty on a phone. Evidence: `home-2400-models-not-loaded.jpg`.
6. **S4 (carried over): portrait Home still hides the team** behind the cards. This fix wasn't part of the batch.

**Coverage gaps (not bugs):**
- The tour still never opens Release Heroes, Rank Up, the Climb countdown or the Climb Summary.
- No hero is ever shown being summoned.
- Tansy and Corran are never shown in battle.

## 2b. Tester checklist results

The checklist (`docs/sigilbound/qa/TESTER_CHECKLIST.md`) is unchanged since my first report.

| Item | Result | Evidence |
|---|---|---|
| 1. Home layout | **PASS** landscape (1280: Continue Campaign pinned, Reset save gone from Home). Portrait team hidden (S4). Empty spots while loading at 2400 (S4). | 03-home at all three sizes |
| 1. Buttons bigger | **PASS**, now portrait too (round 48 dp HUD buttons). Feel **NEEDS A PHONE**. | portrait-hud-fixed.jpg |
| 1. Stage rows, locked stage message | **PASS** | stage-rows-fixed-1280.jpg |
| 1. Normal/Hard and Act I/II switches | **PASS** in both orientations | 10 and 11 landscape; 05 and 11 portrait |
| 1. Press dip and click | Test **PASS**; **NEEDS A PHONE** | |
| 1. Greyed-out buttons readable | **PASS** | LOCKED rows, the Hard switch |
| 1. Colour-blind cues | **PASS** in both orientations (shield/skull on chips, element icons, C/R/E/L tabs) | 62-combat-ui |
| 1. Notch and rounded corners | **NEEDS A PHONE** | |
| 2. Light/Shadow, 10-1, end of Act II beatable | **PASS**: Act II Light/Shadow mid 87.1%, 10-1 88%, 12-6 80%, 14-6 100%, 15-5 65%. Note: 13-6 is the hardest non-finale stage (mid 43%, strong 60%). 15-6 is mid 35%, strong 68%. | campaign probe |
| 2. Round/Enrage counter | **PASS** | 62-combat-ui |
| 2. Broken look | Rules **PASS**; look not captured | |
| 2. Dodge only on no damage | Rule **PASS**; visual **NEEDS A PHONE** | |
| 2. Healing column | **PASS** (Pell 843) | results-replay-climb-2400.jpg |
| 2. Auto battle finishes weak enemies | **PASS** (test) | |
| 3. 60 fps and auto-replay 30 | **PASS** (tests; climbing also runs at 30); smoothness **NEEDS A PHONE** | |
| 3. Sleep, long sessions, heat | **NEEDS A PHONE** | |
| 3. Save backup | **PASS** (SaveSafety tests) | |
| 3. Titan/Arena try at start | **PASS** (quit-rule tests; the arena climb uses a try per fight, confirmed in my simulation) | |
| 4. Place names, scenery, lair reveal | **PASS**; tap-to-skip **NEEDS A PHONE** | story and lair shots |
| 5. Effects and stacks | **PASS** | 60-62 |
| 5. Hero balance | **PASS** (roles 49.4 / 50.0 / 47.2 / 53.4) | roles probe |
| 6. Titan intro, moves, practice | **PASS** (the face is now visible) | 78-84 |
| 6. Titan tiers | **PASS** (100 / 100 / 81 / 39 / 13%) | titandmg |
| 7. Heroes with wrong models | **FAIL (S2)**: Tansy and Corran. Fragments on some remodels (S4). | gallery 9-37 |

**Corrected checklist:** keep the Release / Rank Up and new-heroes blocks from my earlier reports, and add:

```
## NEW: Auto Climb
- [ ] In a campaign battle tap Climb (top right). After each win the next stage should start by itself after ~3 s.
      Does it stop on the first loss and show a Climb Summary with what you earned?
- [ ] Try it in a dungeon (stops when today's runs are used) and the arena (stops when your fights run out).
- [ ] Tap Stop on the results screen during the countdown: does it stop cleanly?
- [ ] If you're near the end of Act I: does the Act II intro show once and the climb carry on?

## NEW: 31 remodelled heroes
- [ ] Look at Odo, Fennick, Holt, Kharra, Marrow, Obrecht, Saskia, Barley, Corran, Nettle, Tansy, Brega, Lazlo, Wenna,
      Neve, Maelis, Morvoren, Caspian, Augustin, Benedict, Poppy, Jasper, Perpetua, Lune, Rook, Nell, Aveline, Astrid,
      Brannoc, Vex, Aurex close up (Heroes → tap → the 3D view). Any holes, floating bits or stretched parts?
- [ ] Tansy and Corran especially: we expect them to look broken in this build. Tell us if they look fine on your phone.
```

## 2c. Exploratory findings

1. **Auto Climb simulated in the core** (a scratch project, never committed). It runs real battles with `Battle.ForStage/ForHard/ForDungeon/ForArena` and books each result with `CompleteStage/CompleteHard/CompleteDungeon/StartArena + CompleteArena`, driven by `AutoClimb.AfterFight` and logged with `ClimbLog`.
   - **Normal, fresh saves:** 60 climbs, mid and strong teams at levels 10-50. Each stops on the first loss (102 stops) or at the end (8). The campaign progress always equals the wins, the climb never steps into a locked stage, and the furthest climb reached 15-6.
   - **Act II boundary:** 30 climbs from 10-5 with a maxed team. All 30 crossed into Act II, the Act II intro triggered exactly once each, and 8 climbed to the end.
   - **Replaying cleared stages:** 20 climbs starting at 1-6 with 30 cleared. They replay through and continue at the first uncleared stage.
   - **Hard:** 30 climbs. Hard never passes Normal, and every "locked" stop came exactly at the Normal frontier (30/30).
   - **Dungeons:** all three dungeons, 20 climbs each. A win always uses a run and a loss never does; no climb won more fights than the runs available. All 60 stopped with out-of-runs (3 winning runs a day).
   - **Arena:** 30 climbs. Each fight uses a try when it starts, the slot (Easy/Even/Hard) is kept, and the climb stops exactly when tries reach 0 (25) or on a loss (5).
   - **Summary totals:** in all 260 climbs the summary's gold, worn/mystic/radiant shards, essence and gear count equal the save's actual change. Fights always equal cleared plus lost, with at most one loss. "Stopped by you" after a loss keeps "Lost at …".
   - **Result:** the rules hold. One wording quirk, **bug 3**.
2. **Act II numbers against my last report**, mid / strong (full table in `evidence/f7ddd6b/campaign-vs-902e814.txt`):

   | Stage | Build 176 | This build | Build 175 (before the new heroes) |
   |---|---|---|---|
   | 12-6 | 3% / 100% | **80%** / 100% | 70% / 100% |
   | 14-6 | 0% / 93% | **100%** / 100% | 95% / 100% |
   | 15-5 | 23% / 83% | **65%** / 88% | 65% / 88% |
   | 13-6 | 43% / 75% | 43% / **60%** | 15% / 88% |
   | 15-6 | 3% / 98% | 35% / 68% | 8% / 80% |

   Act II Light/Shadow mid is **87.1%** (62.3% before; 82.5% before the new heroes). Act II triangle mid is 93.3%. **Fixed**, and slightly better than build 175 thanks to the retune. Watch 13-6: the strong team's 88% → 60% drop (40 runs a stage, so more than noise) is the opposite of what the changelog promises ("a little easier"), probably from the strong line-up's Oriel being toned down.
3. **Roster tools re-fuzzed on this core.** 1,500 random rosters for Release and 1,500 Rank-up-all plans (6,060 rank-ups), plus the hostile preset save: **0 failures**. The undo order is fixed.
4. **Battles at the extremes.** Every hero alone (Lv1 1★ and Lv60 6★) against four random enemies: 471 battles, no exception, no stalemate.
5. **Consistency hunt.**
   - The changelog contradiction is **bug 2**.
   - The Windows "keep running" setting is correctly hidden on phones (`Background.Supported`).
   - Odo's results thumbnail with a red skull is correct: he died in that fight.
   - Codex 3/157 is still right.
6. **Visual hunt on the 31 remodels.** I looked at every gallery page holding a remodelled hero at 2400x1080, plus Tansy and Corran at all three sizes. That found **bug 1** and **bug 4**.

**Recommended permanent tests:** the gallery bounds check (bug 1); a climb test for the top floor on the last run (bug 3); a changelog lint for two lines in one entry about the same feature (bug 2).

## 3. Numbers

- **Tests:** `dotnet run -c Release` gives **All checks passed** (648 PASS lines). That includes AutoClimbTests, DigitigradeTests, PolishTests, HotPathTests, the stage-enemy golden list and SaveSafetyTests.
- **Compile:** Editor and SB_STORE CompileCheck have **0 errors and 0 warnings** (the old `G6` warning is gone).
- **Probes** (run on `648aa4b`; the battle code is identical to `f7ddd6b`):

| Probe | f7ddd6b | 902e814 (last report) | Flag? |
|---|---|---|---|
| combatstats median / p90 turns, capped | 61 / 113, 0 capped | 63 / 112 | -3% |
| roles (Attack / Defense / HP / Support) | 49.4 / 50.0 / 47.2 / 53.4 (spread 6.3) | 51.3 / 47.4 / 49.0 / 52.3 | all inside 44-56% |
| Act I Light/Shadow mid / strong | 96.6% / 100% | 93.1% / 99.8% | |
| Act II Light/Shadow mid / strong | **87.1%** / 94.8% | 62.3% / 97.5% | fixed |
| Act II triangle mid / strong | 93.3% / 96.7% | 88.2% / 98.6% | |
| 10-1 mid / strong | 88% / 100% | 65% / 100% | |
| Titan average damage | 52,714 | 52,557 | |

- **Stages flagged:** no non-finale stage has the mid team under 25% (the lowest is 13-6 at 43%). No strong-team stage is under 50% (the lowest is 13-6 at 60%). No turn-cap hits.
- **Tours:** 1280x720, 2400x1080 and 1080x2400 all reached `DONE 84`. The smoke check passed at every size, with 0 exceptions in any `player.log`.
- **Budgets:** `skinned=` is above 0 in every battle, story, creature and Titan shot. The remodelled heroes are lighter: battle-mid is 123,831 triangles vs 133,008 (-7%), the creature line-up 165,629 vs 177,217, and the Titan about -9%. Particle counts jump in some story shots (story-storm-6 +133% at 2400), but that depends on which effect is mid-air at capture, not a regression.
- **APK:** 165.2 MB (build 180), **down 3.2 MB** from build 176 and under the 175 MB note line. Build 181 is the same size. Build numbers keep rising.

## 4. What to ask the live testers

- Open Tansy and Corran (Heroes → Codex if you don't own them). Do they look broken on your phone too?
- Look closely at the remodelled heroes for floating bits (the list is in the corrected checklist).
- Try Auto Climb in the campaign, a dungeon and the arena, and tell us how it stopped.
- How long does Home look empty after launch before your heroes appear?
- Is 13-6 harder than before with your team?

## 5. Not checked

Sound, touch feel, real notch shapes, heat, battery, on-device frame rate, Windows "keep running" behaviour, the Climb countdown and Summary on screen, Release / Rank Up on screen, the summon reveal for a new or remodelled hero, Tansy and Corran in battle, and spring bones (`7b06d47`, after this build).
