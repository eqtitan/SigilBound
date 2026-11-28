GO WITH NOTES: no crashes, every blocking check passes and the new controls and roster tools work in landscape, but phones held upright (portrait) still get the old small battle buttons with no Pause, and Undo after a bulk release can put saved-team heroes back in the wrong order.

# In-house test: build 175 (a6d2fee), 2026-11-28

- **Build under test:** `a6d2fee` (merge of the roster tools: Release Heroes and Rank Up panels) on top of `9c0745a` (controls and readability patch). CI run **175**: checks, android, windows and linux-preview all green. The APK is build **175**, 163,478,021 bytes (155.9 MiB).
- **Note:** run 175 used the old workflow, so build 175 had already been published to `unity-latest` (live testers) before this gate ran. The same goes for build 173 (`9c0745a`). From `902e814` on, the APK publishes only after a headless smoke tour passes. That build has its own report, `inhouse-2026-11-28-902e814.md`.
- **Baseline:** this is the first in-house report. For comparison I used the desktop tour of build 171 (`3e4c65a`, 1280x720, before the controls patch) and the balance numbers in `MECHANICS.md` 9.7.
- **Preview check:** the `unity-preview` release notes read "commit a6d2fee" when all three tours downloaded it (01:10, 01:15 and 01:22 UTC).

## 1. What's new in this build (changelog `2026-11-27 | Fairer chapters and clearer battles`)

| Change | Status |
|---|---|
| new: Release Heroes (filters, keep best, presets, review, 2-minute undo) | **Verified in the rules** (`ReleaseTests` plus 1,500 random rosters of my own, see 2c). **Not seen on screen:** the tour never opens the panel. **One bug:** undo can reorder saved teams (S3). |
| new: Rank Up fodder slots, Auto-fill, Rank up all with a cost preview | **Verified in the rules** (`RankUpTests`, plus 1,500 random Rank-up-all plans where the preview matched the gold actually spent). The panel itself was not seen on screen. |
| fix: Rank-up no longer offers a new Epic/Legendary before levelled Commons | **Verified** (test "auto-pick takes 3★ Commons before a fresh Lv1 Epic"). |
| change: Heroes list remembers the sort | Verified in the save round trip (test). The Release… and Rank up all buttons show on Heroes at every size. |
| change: bigger tap targets, whole stage rows tappable, Continue Campaign always visible | **Verified in landscape** (stage rows and 48 dp corner buttons in the 1280 and 2400 shots; Continue Campaign pinned at all three sizes). **Not done in portrait:** the battle HUD and campaign list are unchanged there (S3). |
| fix: safe areas, readable locked buttons, Hard and Act switches, element/rarity cues | **Verified in landscape** (LOCKED buttons readable, segmented Normal / Hard and Act I / Act II switches, element icons, C/R letter tabs). In portrait the campaign keeps the old small Normal/Hard chips and the turn chips have no skull or shield glyphs. Real notches can't be checked headless. |
| fix: Round/Enrage banner under the turn bar | **Verified**: 60-combat-ui at 1280 and 2400 shows it under the turn bar (build 171 had it under the chips). In portrait it sits under the bar, on the left. |
| balance items (Light/Shadow, 10-1, Act II, roles, Titan tiers, 38 heroes) | **Verified** by the probes (section 4). These numbers are identical to build 171's, because the battle rules did not change. |
| Break look, counter-Break, Stun/Break chain, auto-battle kill choice, healing column, dodge | Rules **verified** by tests. Healing column **verified** on screen (Pell 850 on 61-battle-result). The Break and dodge visuals were not captured. |

## 2. Bugs (most severe first)

**No S1 or S2 bugs.**

1. **S3: Portrait (phone held upright) didn't get the controls patch.**
   - **Where:** the battle HUD in portrait.
   - **What I saw:** the top buttons are 40-50 x 34 units (under 48 dp) and there is **no Pause button**: only X, Cam, Loop, Auto and 1x. The turn chips have no ally/enemy shield or skull glyph and cut names short ("Ysold", "Kettl"). The portrait campaign list also keeps the old 90 x 32 Normal/Hard chips and the small stage chips.
   - **Expected:** the same 48 dp buttons, Pause and glyphs as landscape.
   - **Why the checks missed it:** the game auto-rotates (`App.cs:53`), but the portrait path in `BattleController.DrawHud` (`if (Ui.Landscape) { DrawHudWide(); return; }`) and `ExtrasScreens.CampaignTabs` were not changed, and the UX tests only read the landscape code.
   - **Workaround:** turn the phone sideways.
   - **Evidence:** `evidence/a6d2fee/portrait-battle-hud-top.jpg`, `evidence/a6d2fee/portrait-campaign-list.jpg`.
   - **Suggested test:** extend the UxTests checks to `DrawHud` and `CampaignTabs`.
2. **S3: Undo after a bulk release puts saved-team heroes back in the wrong order.**
   - **Where:** the Release Heroes panel, then Undo.
   - **Steps:** a saved team holds two heroes that are both released (the "In a saved team" protection is unticked), then the player taps Undo. The heroes come back swapped.
   - **Minimal repro:** saved team `[8,9]`, release both, undo, the team is `[9,8]`. It also showed up in 2 of 1,500 random rosters.
   - **Cause:** `ReleaseMany` records each removal position after the earlier removals, and `UndoRelease` re-inserts by those positions in ascending order.
   - **Impact:** team slot order changes. Nothing is lost.
   - **Workaround:** re-save the team.
   - **Suggested test:** release two heroes from one preset, undo, and check the preset equals the original.
3. **S4: Enemy names and levels are cut off on the stage rows.** At 1280x720 and 2400x1080, "Aurelia Lv4", "Tamsin Lv5", "Corvan Lv6" and "Vaelith Lv6" are clipped mid-word because three enemies don't fit their slots. Evidence: `stage-rows-names-cut-1280.jpg`, `stage-rows-names-cut-2400.jpg`.
4. **S4: Portrait campaign: locked chapter cards are half see-through.** The Home heroes standing behind show through the text of chapters 3-5 ("4. Saltmarsh Reaches" has a hero across it). Evidence: `portrait-campaign-list.jpg`.
5. **S4: Portrait Home: the team isn't visible.** The quest and login cards cover the whole lineup and leave empty sky above. The checklist promises "your team in the middle". Evidence: `portrait-home-no-team.jpg`.
6. **S4 (possible): Home at 2400x1080 caught the three heroes in a T-pose** (arms straight out). The same screen at 1280 and in portrait shows them idling, so it is probably the first frames after the models load. Live testers should confirm on a phone. Evidence: `home-2400-tpose.jpg`.
7. **S4: Release review grid under-warns.**
   - **What:** the red "!" marks a hero only when it is the single copy owned (`ReleasePanel.cs`, `risky = ... CopiesOf(h.id) <= 1`).
   - **When it misses:** with "Allow last copy" ticked and both copies of a hero selected, neither card gets the "!".
   - **Why it's only S4:** the summary still says "includes Epic+ or last copies" and the button still needs the 1-second hold.
   - **Found by:** code review.
8. **S4 (code): compile warning,** an unused variable `G6` in `RankUpPanel.cs(62)`. Harmless.

**Coverage gap (not a bug):** the screenshot tour never opens Release Heroes, the review screen, Rank Up or Rank up all, so none of those panels has been seen running at any size. Suggest adding tour shots of each panel (one with the Undo bar showing) before the next release.

## 2b. Tester checklist results (`docs/sigilbound/qa/TESTER_CHECKLIST.md`)

| Item | Result | Evidence / what to look for |
|---|---|---|
| 1. Home layout (quests left, team middle, Continue right / above the bar in portrait) | **PASS** landscape; **FAIL (S4)** portrait: the team is hidden | 03-home at all three sizes |
| 1. Buttons bigger, thumb-friendly | **PASS** landscape; **FAIL (S3)** portrait battle HUD; feel **NEEDS A PHONE** | UX tests 426-428; bug 1 |
| 1. Stage list: whole row taps; locked stage says what to clear | **PASS** (landscape rows); the toast text is in code (`PrepScreens.cs:105` "Clear 1-1 first"). Portrait uses the old chips. | 10-campaign-stages; UX test 427 |
| 1. Normal / Hard and Act I / Act II switches | **PASS** landscape (top bar, lock icon on Hard and Act II when locked); portrait has old chips and no Act switch (Act II is lower in the list) | 10 and 11 at 2400; 05 and 11 at 1080x2400 |
| 1. Pressed buttons dip and click | Code and test **PASS** (UX test 435); **NEEDS A PHONE** for the feel and sound | |
| 1. Greyed-out buttons readable | **PASS** (LOCKED buttons and disabled plates are clear; test: worst contrast 5.2:1) | 10-campaign-stages |
| 1. Colour-blind: skull/shield chips, element icons, C/R/E/L tabs, WEAK!/RESIST | **PASS** landscape; **FAIL (S3)** portrait turn chips have no glyphs | 60-combat-ui 2400 vs 1080x2400 |
| 1. Notch / rounded corners | **NEEDS A PHONE**: headless runs have no safe-area cut-out. Look at the top bar corners and the left edge in landscape. | |
| 2. Light/Shadow, 10-1, end of Act II beatable | **PASS with a note**: Act II Light/Shadow mid 82.5%, 10-1 65%, 15-6 mid/strong 8/80%. 13-6 is a wall for a mid team (15% mid, 88% strong). | probe `campaign 40` |
| 2. Round counter, red Enrage from round 10 | **PASS** | 60-combat-ui ("Round 12 · Enrage x3 +30% dmg -30% heal") |
| 2. Broken has its own look | Rules **PASS** (tests); look **not captured**, so ask the live testers | |
| 2. Dodge only when no damage | **PASS** (rule in tests and MECHANICS 9.6); visual **NEEDS A PHONE** | |
| 2. Healing column shows real numbers | **PASS** | 61-battle-result (Pell 850) |
| 2. Auto battle finishes weak enemies | **PASS** (test "AI kill estimate … picks the killable foe") | |
| 3. 60 fps default, 30 for auto-replay | **PASS** (fps tests); smoothness and heat **NEEDS A PHONE** | |
| 3. Screen sleeps in menus | **NEEDS A PHONE** | |
| 3. Long sessions don't slow down | **NEEDS A PHONE** (20+ minutes) | |
| 3. Save backed up automatically | **PASS** (SaveSafety tests: backup, corrupt copy, failed load blocks autosave) | |
| 3. Titan/Arena try used at fight start | **PASS** (quit-rule tests) | |
| 4. Place names and changing scenery | **PASS** | story-* shots (mill, storm, glass, crown, bell, light) |
| 4. Boss lair camera reveal, tap skips | Reveal **PASS** (69-72 lair-reveal shots); tap-to-skip **NEEDS A PHONE** | |
| 5. Hits and spells look right | **PASS** in the shots; sound **NEEDS A PHONE** | 58-60, 62-68 |
| 5. Poison/Burn stack x3 badge readable | **PASS** ("x3" badge visible on Hollow Velka in 60-combat-ui; stacking test) | |
| 5. Heroes feel useless or overpowered | Probe **PASS** (roles 48.7 / 47.7 / 50.0 / 53.6); opinions **NEEDS A PHONE** | |
| 6. Titan intro, three moves, final blow; practice is free | **PASS** in the shots (73-79); practice is free by test | |
| 6. Titan tiers 15k / 40k / 80k / 140k / 220k reachable | **PASS**: maxed team 100 / 100 / 81 / 31 / 3% | probe `titandmg` |
| 7. Heroes with wrong models | No floating parts, missing textures or stand-ins in the gallery shots at this size. Ask the live testers about close-ups. | 15-52 gallery |

**The checklist is out of date:** it has nothing on the Release Heroes and Rank Up panels (the headline of this build). Add:

```
## NEW: Release Heroes and Rank Up (Heroes tab)
- [ ] Heroes → Release…: the default "Safe clean-up" should only pick spare plain Commons. Do locked heroes, your team,
      saved teams and heroes wearing gear stay out of the list? Try a preset, release, then tap Undo within 2 minutes:
      are all heroes, their gear and your saved teams exactly as before (team order too)?
- [ ] Ticking "Allow Epic / Legendary" or "Allow last copy" should turn the button into a 1-second hold. Does it?
- [ ] Open a hero → Rank Up: are the fodder slots filled with your cheapest spare heroes (never an Epic/Legendary)?
      Swap one by hand; picking an Epic or a last copy should warn you and need a hold.
- [ ] Rank up all: does the gold in the preview match what you actually spend?
- [ ] Hold the phone upright as well as sideways for all of the above.
```

## 2c. Exploratory findings (my own checks)

1. **Release rules, fuzzed.**
   - **How:** a scratch copy of the core (never committed) made 1,500 random rosters with locks, gear and saved teams, and a random filter and protection set for each.
   - **Checks:** the plan never takes locked or team heroes; the roster never empties; no last copy goes while that protection is on; no Epic+, ranked-up, geared or max-level hero goes while its rule is on; keep-best copies always stay; the essence gained equals the preview; released heroes leave saved teams; Undo inside 2 minutes restores the roster exactly; a second Undo is refused.
   - **Result:** 907 heroes released and undone, all rules held, except **bug 2** (team order after undo, 2 rosters).
2. **Undo edge cases.** Undo at 121 s is refused ("Too late to undo"). After spending the essence it is refused ("You've already spent that essence") and the essence is left untouched. Nothing found.
3. **Last-hero guard.** Releasing everything with every protection off leaves at least one hero. The hero screen's single release won't offer the last hero. A hand-made plan containing the last hero is refused. Nothing found.
4. **Rank up all, fuzzed.**
   - **How:** 1,500 random plans (each target star, team-only or not, with and without "train to cap").
   - **Checks:** fodder is never used twice; a promoted hero is never fodder; no locked, team, saved-team, geared or Epic+ fodder; no last copy used; the preview stays within your gold; the gold actually spent equals the preview.
   - **Result:** 6,093 rank-ups, 0 partial applies, 0 mismatches.
5. **Presets from a damaged or hostile save.**
   - **How:** a save with 9 presets, blank names, masks of -1 and 9999, keep 99, levels 80 to -5, protect 0, an out-of-range active preset, an unknown sort and an unknown extra key.
   - **Result:** it loads with 4 presets, "My filter", levels 60-60, keep 3. Locked, Team and both danger rules are forced on. The unknown key is kept on save. A save with default prefs writes no "roster" key (old builds are unaffected). Nothing found.
6. **Hand-picked rank-up fodder** that wears gear and sits in a saved team: the gear goes back to the bag. The saved team keeps a stale id, which loading already skips (`TeamLoad` filters missing heroes). Nothing found.
7. **Battles at the extremes.** Every hero alone (Lv1 1★ and Lv60 6★) against four random enemies: 450 battles, no exception, no stalemate.
8. **Changelog "What's new" re-show.** The controls lines were added to an entry dated 2026-11-27 that players of build 171 had already seen. The seen-key includes the item count (`Changelog.cs:18`), so the entry shows again. Nothing found.
9. **Portrait code paths.** The landscape-only UX tests let portrait slip, which is **bug 1**. Found by reading why the portrait battle shot still had 34-unit buttons.

**Recommended permanent tests:** preset order after Undo (bug 2); the UX checks applied to the portrait HUD and campaign tabs (bug 1); a "Rank up all preview = actual" property test over random rosters; tour shots of Release, Review, Rank Up and Rank up all.

## 3. Numbers

- **Tests:** `dotnet run -c Release` gives **All checks passed** (480 PASS lines). This includes the new ReleaseTests and RankUpTests, SaveSafetyTests (backup, corrupt copy, failed load blocks autosave), the web-roster save fixture and 11 UX checks. The APK budget check is skipped locally because the models aren't prepared here; CI ran it green.
- **Compile:** Editor and Client CompileCheck have 0 errors. The SB_STORE variant has 0 errors and 1 warning (unused `G6`).
- **Probes** (identical to build 171 and `MECHANICS.md` 9.7, because the battle rules did not change):

| Probe | This build | Baseline |
|---|---|---|
| combatstats median / p90 turns, capped | 65 / 119, 0 capped | 65 / 119 |
| roles (Attack / Defense / HP / Support) | 48.7 / 47.7 / 50.0 / 53.6 | same |
| Act II Light/Shadow mid / strong | 82.5% / 97.1% (40 runs) | 82.6% / 96.5% (60 runs) |
| 10-1 mid / strong | 65% / 100% | 63% / 100% |
| 15-6 mid / strong | 8% / 80% | 8% / 78% |
| Titan average damage | 49,793 | 49,545 |

- **Advisory flags:** **13-6 mid 15%** is a non-finale stage under 25%. It is not a new regression, but nobody had recorded it. Also 5-6 mid 38% and 15-5 mid 65%. No strong-team stage is under 50%. No turn-cap hits.
- **Tours:** 1280x720, 2400x1080 and 1080x2400 all reached `DONE 79` (about 6 minutes each). Smoke check passed at every size. 0 exceptions in `player.log`.
- **Budgets:** `skinned=` is above 0 in every battle, story and Titan shot. Triangles are within 1% of build 171 (battle-mid 133,780 vs 133,864). Renderer counts in the Titan shots are higher at the phone sizes, but that is a different aspect ratio from the 1280 baseline, not a change.
- **APK:** 163.5 MB, under the 175 MB note line. Build 175 is higher than 171.

## 4. What to ask the live testers

- Play some battles holding the phone upright: are the buttons too small, and can you find Pause? (bug 1)
- Try Release Heroes and Undo, and Rank Up / Rank up all, using the checklist lines above.
- Any notch or rounded corner covering the top bar or the Normal/Hard switch.
- Does Home ever show heroes with their arms straight out?

## 5. Not checked

Sound, touch feel and press feedback, real notch and camera-hole shapes, phone heat and battery, frame rate on a real device, 20-minute sessions, screen sleep, tap-to-skip on the lair reveal, and the Release / Rank Up panels on screen (they are not in the tour).
