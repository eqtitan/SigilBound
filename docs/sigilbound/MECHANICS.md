# Sigilbound character mechanics and gameplay animation

Owned by the **character-mechanics** agent (`.claude/agents/character-mechanics.md`). This is the
spec for how heroes move and react in battle: beats, states, clips, timing, hit-stop, frame
rate and the animation tests the owner approves. PLAN.md phase G implements it.

_Last updated: 2026-11-27 (combat rules section 9 added and the step 97-99 rules brought into sections 1, 3.1 and 4.5; first version 2026-11-25, written from genre knowledge)._

**Ground rules (apply to everything below):**
- **Time, not frames.** All times are in seconds at **1x battle speed**, and all event times are
  **normalized clip time** (0 = clip start, 1 = clip end). Nothing counts frames, so a move takes
  the same time at 30, 60 and 120 fps.
- **Free CC0 clips only.** UAL first, then the CC0 base clips already in the game, then procedural layers.
- **Original.** Mechanics are described generically. No other game's characters or named moves are used.

---

## 1. The battle loop today (read from `Client/BattleController.cs`)

- **Turn order.** `Battle.NextActor()` picks the unit whose turn meter fills first.
  `BeginTurn` runs start-of-turn effects: the enrage text when a new enrage stack starts, one tick per
  Poison/Burn stack (up to 3 tick numbers per DoT, each a 0.12 s damage beat), Regen, then the Stun or
  Broken skip ("Stunned!" / "Broken!"). The rules are in section 9. The actor then picks a skill
  (the player in manual, `AiChoose` in auto), `DoAct` presents it and `EndTurn` closes it. A new
  wave spawns after 0.6 s plus 0.5 s.
- **Battle speed.** 1x, 2x or 3x is applied as `Time.timeScale` (clamped 1–3). Pause sets it to 0.
  Everything that uses `WaitForSeconds` and `Time.deltaTime` scales with it. The auto-replay
  countdown uses `WaitForSecondsRealtime` (a fixed 3 s).
- **Presentation of one action, at 1x today:**

  | Part | Melee | Caster |
  |---|---|---|
  | Skill banner | 0.20 | 0.20 |
  | Run-in / step forward | 0.26 (run-in) | 0.20 (step), plus 0.15 hold |
  | Strike / cast wait | 0.35 | |
  | Projectile | | 0.22 (arrow), 0.32 (bolt, orb or shard) |
  | Each damage event | 0.12 | 0.12 |
  | Events tail | 0.20 | 0.20 |
  | Recover (run home) | 0.25 | 0.25 |
  | Turn tail | 0.25 | 0.25 |
  | **Typical total** | **≈1.6 s** | **≈1.5 s** |

- **What's missing:**
  - No real hit-stop.
  - Impacts are not tied to the moment the weapon connects: damage numbers fire after a fixed 0.35 s wait, whatever the clip.
  - No block, stun, knock-down or revive states.
  - Idle also plays 3x faster at 3x speed.
  - Shake and some effects depend on frame rate (section 4.6).

## 2. The beats of one action

Every action is the same five beats. Durations are in seconds at 1x, using the **default** column;
section 3.2 gives each weapon family its own values.

| Beat | What happens | Default | Range | Notes |
|---|---|---|---|---|
| 0. Ready | The actor's ring lights; it blends from relaxed idle to combat stance | 0.15 | 0.10–0.20 | Overlaps the skill banner (0.20) |
| 1. Anticipation | Wind-up: weight shifts back, weapon draws back, the caster gathers energy | 0.22 | 0.12–0.40 | Heavier weapons wind up longer. Ultimates add a camera push-in |
| 2. Travel | Melee: run or dash to the strike point. Ranged: projectile flight | 0.26 | 0.15–0.35 | Melee: distance ÷ 7 m/s, clamped 0.18–0.32. Projectiles: section 4.4 |
| 3. Impact | The clip's `impact` time is crossed: damage, effects, hit-stop, the target's reaction | 0.00 + hit-stop | hit-stop 0.04–0.14 | The single most important moment. Section 4 |
| 4. Recovery | Follow-through and settle back to stance in place | 0.30 | 0.20–0.45 | The damage numbers float during this beat |
| 5. Return | Run or backstep home (melee), step back (casters), turn to face forward | 0.25 | 0.20–0.30 | Melee uses `Run_Back` or a backstep, never a full turn-around |
| Tail | Breath before the next turn | 0.20 | 0.15–0.25 | Replaces today's 0.25 |

A melee basic attack is ≈1.4 s at 1x, and a caster basic is ≈1.3 s. Ultimates are 2.0–2.6 s.

**Overlap rule.** The next actor's **Ready** beat may start during the current actor's Return,
0.10 s before it ends. That keeps 3x speed snappy without the two actions overlapping on screen.

### 2.1 Scaling with battle speed

Battle speed stays `Time.timeScale` (so pause keeps working). Every beat scales with it, with
these exceptions:

| Thing | 1x | 2x | 3x | Rule |
|---|---|---|---|---|
| Beats 0–5 and tail | 1.0 | 0.5 | 0.33 | Scaled time (automatic) |
| Hit-stop (real seconds) | 1.00 × base | 0.70 × base | 0.55 × base | Scales with `speed^-0.55`, so impacts still read at 3x. Section 4.1 |
| Screen shake amplitude | 1.0 | 1.0 | 0.85 | Duration scales with speed like the beats |
| Idle, fidget, victory loops | 1x | 1x | 1x | Animator speed is multiplied by `1 / Time.timeScale` while in these states. ArtSpec already marks Idle and Victory `SpeedParam = false` |
| Damage number float | 0.9 s | 0.9 s | 0.9 s | Unscaled, so numbers stay readable |
| Skill banner | 0.20 | 0.10 | 0.07 | Scaled, but the banner text stays up for at least 0.5 s real time |
| Auto-replay countdown | 3 s real | 3 s real | 3 s real | Unchanged |

**Worked example.** A heavy melee basic (section 3.2):
- At 1x: 0.15 + 0.32 + 0.26 + 0.12 hit-stop + 0.40 + 0.25 + 0.20 ≈ **1.70 s**.
- At 3x: (1.70 − 0.12) / 3 + 0.12 × 0.55 ≈ **0.59 s**.

## 3. The state machine every hero needs

### 3.0 Verified UAL clips on the high-detail rig (step 66)

The free CC0 Universal Animation Library (Standard tier, 46 clips), retargeted onto the 65-joint rig by `unity/Tools~/blenderforge/ual_retarget.py` into `unity/Tools~/models/ubc_anims.glb`. `Core/Kit/HeroAnim.cs` `UalClips.HdClips` holds the same list, and a test checks that every clip the game asks for is in it.

`A_TPose`, `Crouch_Fwd_Loop`, `Crouch_Idle_Loop`, `Dance_Loop`, `Death01`, `Driving_Loop`, `Fixing_Kneeling`, `Hit_Chest`, `Hit_Head`, `Idle_Loop`, `Idle_Talking_Loop`, `Idle_Torch_Loop`, `Interact`, `Jog_Fwd_Loop`, `Jump_Land`, `Jump_Loop`, `Jump_Start`, `PickUp_Table`, `Pistol_Aim_Down`, `Pistol_Aim_Neutral`, `Pistol_Aim_Up`, `Pistol_Idle_Loop`, `Pistol_Reload`, `Pistol_Shoot`, `Punch_Cross`, `Punch_Enter`, `Punch_Jab`, `Push_Loop`, `Roll`, `Roll_RM`, `Sitting_Enter`, `Sitting_Exit`, `Sitting_Idle_Loop`, `Sitting_Talking_Loop`, `Spell_Simple_Enter`, `Spell_Simple_Exit`, `Spell_Simple_Idle_Loop`, `Spell_Simple_Shoot`, `Sprint_Loop`, `Swim_Fwd_Loop`, `Swim_Idle_Loop`, `Sword_Attack`, `Sword_Attack_RM`, `Sword_Idle`, `Walk_Formal_Loop`, `Walk_Loop`.

### 3.1 States

Canonical state names follow `Core/Art/ArtSpec.cs HeroClips`. New states are marked **new**.

Event times are normalized: `impact` is the moment of contact, `release` is when a projectile leaves the hand.

Blend times are in seconds. UAL clip names were checked against the free Standard pack in step 66 (✓ = present). Four named clips are not in the free tier (`Sword_Block`, `Hit_Knockback`, `LayToIdle`, `Kick`); their fallbacks apply. The full verified list is in section 3.0. The clips already in the game (`anims_ual.json`) are certain:
- `Spell_Simple_Enter`, `Spell_Simple_Shoot`, `Spell_Simple_Exit`, `Spell_Simple_Idle_Loop`;
- `Hit_Chest`, `Hit_Head`, `Death01`.

Fallbacks are the current base-rig clips (`Idle_Sword`, `Sword_Slash`, `Punch_Right`, `Kick_Right`, `Gun_Shoot`, `Roll`, `Wave`, `Death`, `HitRecieve`, `Interact`, `Yes`, `No`) or procedural layers.

| State | Loop | Enter blend | Trigger (rules event) | UAL clip (first choice) | Fallback | Events |
|---|---|---|---|---|---|---|
| Idle | yes | 0.25 | default | `Idle_Loop` ✓; per family, see 3.2 | `Idle_Sword`, `Idle_Neutral` | |
| Idle_Fidget | no | 0.30 | random, every 8–20 s, never two neighbours within 3 s | `Idle_Talking_Loop` (one cycle) *(verify)*, `Interact` | procedural: look around and shrug | |
| Ready **new** | no, holds | 0.15 | the actor's turn starts | `Sword_Idle` ✓ / family stance | Idle at 1.1x with a 3° forward lean | |
| Run | yes | 0.12 | melee travel | `Jog_Fwd_Loop` ✓ | `Run` | footfalls at 0.0 and 0.5 |
| Run_Back | yes | 0.10 | melee return | `Walk_Loop` at −1x speed *(verify)* | `Run` at 1.5x facing home | |
| Attack_1..3 | no | 0.08 | basic attack (skill 1), rotating | per family, see 3.2 | `Sword_Slash`, `Punch_Right`, `Kick_Right` | `impact` per family |
| Skill_2 / Cast | no | 0.10 | skill 2 | `Spell_Simple_Shoot` (ranged), `Sword_Attack` ✓ (melee) | `Gun_Shoot` | `release` 0.45 / `impact` 0.42 |
| Cast_Ult | no | 0.12 | skill 3 or all-enemies skill | `Spell_Simple_Enter` → `Spell_Simple_Idle_Loop` (channel) → `Spell_Simple_Shoot` | `Spell_Simple_Shoot` at 0.8x | `release` 0.45 of the Shoot |
| Cast_Support | no | 0.12 | no-damage skill | `Spell_Simple_Enter` + `Spell_Simple_Exit` | `Interact`, `Yes` | `release` 0.60 |
| Channel **new** | yes | 0.10 | multi-turn or long casts (ultimates over 0.6 s) | `Spell_Simple_Idle_Loop` | Idle plus a hand-glow effect | |
| Hit | no | 0.06 | damage under 14% max HP | `Hit_Chest` | `HitRecieve` | |
| Hit_Big | no | 0.05 | crit, or damage of 14% max HP or more. The 14% predates `Battle.Pace` 1.6: it now fires on about 34% of hits instead of 22% (a 0.14 × Pace threshold would restore it; not changed yet) | `Hit_Head` | `HitRecieve_2` | pushes back 0.34 m |
| Block **new** | no | 0.05 | Defense role or shield prop and the hit is fully or mostly absorbed (`EvType.Absorb` ≥ 60% of the hit), or the unit has DefUp | `Sword_Block` *(not in the free tier: use the procedural block)* | procedural: shield arm forward, 0.12 m slide back, crouch 6° | |
| Dodge | no | 0.06 | a hit that costs **no HP** (fully absorbed by a shield), 35% chance; never for Defense. A hit that lowers HP never dodges (PLAN step 99: the old glancing-blow rule rolled away about 10% of all hits while HP dropped) | `Roll` (short) *(verify)* | `Roll` at 1.5x | sidestep 0.45 m sideways, back home by 0.9 |
| Broken **new** | no, then Idle | 0.05 | `EvType.Status` Broken (weakness break, section 9.4) | the Hit_Big clip, then a 0.22 s freeze, then the shoulders-down debuff pulse | same | cracked ground in the breaking element, orange "BREAK!" banner, cracked-sigil status icon; the lost turn shows "Broken!" |
| Stunned **new** | yes | 0.20 | Stun applied; holds until the skipped turn ends | `Crouch_Idle_Loop` at 0.55x | Idle at 0.6x, slumped 8° | the status icon's orbiting stars |
| Knocked_Down **new** | clamp, then get up | 0.05 | a heavy-family or ultimate hit that also applies Stun, or Pull | `Hit_Knockback` *(not in the free tier: use the fallback)* → `LayToIdle` *(not in the free tier: use the fallback)* | `Death01` up to 0.55, then reversed to stand | down at 0.35, rise starts after 0.6 s held |
| Death | clamp | 0.10 | `EvType.Death` | `Death01` | `Death` | sinks after 1.2 s |
| Revive **new** | no | 0.10 | `Revived!` from Revive (`Battle.ApplyDamage`). Add `EvType.Revive` | `Death01` to 0.50, then `LayToIdle` *(not in the free tier: use the fallback)* | `Death01` played backwards from 0.6 to 0 at 1.4x, plus a light pillar | stands at 0.85 |
| Counter **new** | no | 0.05 | `Counter!` text event | the hero's Attack_1 at 1.3x, turning in place in 0.10 s | same | `impact` as Attack_1 |
| Buffed / Debuffed **new**, additive | no | 0.10 | `EvType.Status` | additive layer: chest-up 4° (buff), shoulders down 4° (debuff), 0.4 s | the effect alone | |
| Victory | yes | 0.25 | winners after the last wave, staggered 0.10–0.12 s | per role, see section 5 | `Wave`, `Yes` | |
| Defeat **new** | clamp | 0.30 | the losing side's survivors when the battle is lost | `Defeat_Slump` (owner decisions 2026-11-25/26: Arena (PvP) losses only; fall to the knees, then slump forward onto the stomach pivoting on the planted knees, and hold; hand-authored by `blenderforge/make_clips.py`, 2.0 s) | Idle at 0.5x, slumped 12° | |

**Transition rules:**
- **Priority order:** Death > Revive > Knocked_Down > Broken > Stunned > Hit_Big > Block or Dodge > Hit > Attack or Cast > Ready > Fidget > Idle.
- A hit on a unit that is attacking (counter windows) plays as an additive flinch, not a full state change.
- Every non-looping state returns to **Ready** while it is the unit's turn, otherwise to **Idle**.
  Today `HeroModel.Play` queues Idle with `CrossFadeQueued`. The new machine must use the Animator
  (or a playables graph) so this follows the priority order.

### 3.2 Weapon families

The families are mapped from `Looks.Prop` and the `Brawlers` set in `Core/Kit/HeroAnim.cs`. New
kit pieces from steps 46–50 are listed so they land in a family from day one.

| Family | Props (today + planned) | Heroes, e.g. | Stance | Anticipation | `impact` | Recovery | Hit-stop |
|---|---|---|---|---|---|---|---|
| **Blade** | sword, katana, rapier* | Lumen, Kaito, Vaelith | blade low, side-on | 0.18 | 0.38 | 0.28 | 0.07 |
| **Heavy** | hammer, club, mace*, axe*, flail*, twin axes* | Cole, Ronan, Solenne | two-handed, weight back | 0.32 | 0.52 | 0.40 | 0.12 |
| **Polearm** | spear, trident, glaive*, scythe*, lance*, whip (lash variant) | Maelis, Brannoc, Ysolde | long guard, front foot forward | 0.24 | 0.45 | 0.32 | 0.08 |
| **Dual daggers** | daggers, sickle*, hook | Nyx, Vesper, Nettle, Kettle | low crouch, blades reversed | 0.12 | 0.30 (multi: 0.22 / 0.48 / 0.74) | 0.22 | 0.05 per hit, 0.09 on the last |
| **Bow / crossbow** | bow, crossbow*, chakram* (thrown) | Tamsin, Wynn | bow side, string hand at hip | 0.26 (draw) | `release` 0.55 | 0.25 | 0.05 (on arrival) |
| **Staff / orb caster** | staff, orb, mirror, tome-staff* | Morwenna, Velka, Sable | staff planted, free hand open | 0.28 (gather) | `release` 0.45 | 0.30 | 0.06 (on arrival); ultimate 0.10 |
| **Instrument / lantern support** | bell, lantern, candle, harp*, lute* | Seraphine, Wren, Pell | prop held to the chest | 0.30 | `release` 0.60 | 0.35 | none (heals); 0.05 on a damage basic |
| **Shield** | shield (20 heroes) | Ulfgar, Aurelia, Hask, Mira | shield forward, weapon tucked | 0.20 | 0.40 (bash) | 0.30 | 0.09 |
| **Unarmed brawler** | `Brawlers`: odo, kettle, gorrum, thrain; gauntlets* | Odo, Gorrum, Thrain | fists up, bouncing | 0.14 | 0.33 / 0.36 / 0.42 (jab, cross, kick) | 0.24 | 0.06, 0.10 on the finisher |

Clips per family. The first choice is UAL (checked in step 66: ✓ = in the free pack); the second is the base rig:

| Family | Idle / stance | Attack_1 | Attack_2 | Attack_3 | Skill / Cast | Ultimate |
|---|---|---|---|---|---|---|
| Blade | `Sword_Idle` / `Idle_Sword` | `Sword_Attack` / `Sword_Slash` | `Sword_Attack` mirrored at 1.1x / `Sword_Slash` | `Punch_Cross` (pommel) / `Kick_Right` | `Sword_Attack` at 0.85x plus a dash 0.8 m | `Spell_Simple_Enter` + `Sword_Attack` (charged slash) |
| Heavy | `Sword_Idle` lowered 10° / `Idle_Sword` | `Sword_Attack` at 0.8x, procedural overhead arc / `Sword_Slash` | `Punch_Cross` (shoulder check) | `Jump_Start` + `Jump_Land` (leaping slam) | slam: `Jump_Land` / `Sword_Slash` at 0.7x | leaping slam plus a shockwave |
| Polearm | `Sword_Idle` / `Idle_Sword` | `Sword_Attack` (thrust, procedural forward reach 0.3 m) | `Sword_Attack` sweep | `Kick_Right` | sweep at 0.9x | spin: `Sword_Attack` ×2 at 1.2x |
| Dual daggers | `Crouch_Idle_Loop` / `Idle_Sword` | `Punch_Jab` + `Punch_Cross` (one cut per hand) / `Punch_Right` + `Punch_Left` | `Sword_Attack` at 1.3x | `Kick_Left` / `Roll` → strike | `Roll` + `Sword_Attack` (vanish strike) | flurry: Jab/Cross ×3 at 1.4x |
| Bow / crossbow | `Pistol_Idle_Loop` / `Idle_Gun` | `Pistol_Shoot` / `Gun_Shoot` | same | same, backstep 0.3 m | `Pistol_Aim_Up` + `Pistol_Shoot` (arc volley) | volley: Shoot ×3 at 1.3x |
| Staff / orb | `Spell_Simple_Idle_Loop` (subtle) / `Idle_Neutral` | `Spell_Simple_Shoot` | `Spell_Simple_Shoot` | `Spell_Simple_Shoot` | `Spell_Simple_Enter` + `Shoot` | Enter → Channel 0.5 s → Shoot |
| Instrument / lantern | `Idle_Loop` / `Idle_Neutral` | `Spell_Simple_Shoot` (prop raised) | same | same | `Spell_Simple_Enter` + `Exit` (Cast_Support) | Enter → Channel 0.6 s → Exit with a ring pulse |
| Shield | `Sword_Idle`, shield up / `Idle_Sword` | `Punch_Cross` (shield bash) / `Punch_Right` | `Sword_Attack` | `Kick_Right` | taunt: `Interact` + a chest beat / `Yes` | `Jump_Land` slam, shield first |
| Unarmed | `Idle_Loop` + bounce / `Idle_Sword` | `Punch_Jab` / `Punch_Right` | `Punch_Cross` / `Punch_Left` | `Kick` *(not in the free tier: use the fallback)* / `Kick_Right` | `Punch_Cross` at 0.8x (haymaker) | `Jump_Land` ground pound |

`Skill_<n>` clips from a hero's own model always win over this table (ArtSpec). The table is the
default each hero starts from, so no two families feel alike even before Phase E's signature clips.

**Multi-hit skills** (Nyx's Hollow Cut ×3, Kaito's Thousand Cuts ×4, Ysolde's Lash ×2):
- One clip has one `impact` per hit, from the table or evenly spaced from 0.22 to 0.78.
- Each hit fires its own damage event, so the current fixed 0.12 s between numbers is replaced by the clip's timing.

## 4. Impact rules: hit-stop, shake, impact flash, knockback

### 4.1 Hit-stop

Hit-stop is a short freeze of the **attacker and the target only**. It never changes
`Time.timeScale`, because that is battle speed and pause.

- **Start:** the frame on which the attacker's clip crosses `impact`. Both poses are **snapped to the exact
  `impact` time** (set the normalized time, then set the animator speed to 0), so the frozen pose is
  identical at 30, 60 and 120 fps.
- **Duration:** in scaled seconds, `base × speed^0.45`. Measured with `Time.deltaTime`, so pause
  holds it and the real-time length is `base × speed^-0.55`.
- **End:** resume at the speed the clip had. The time left over past the end of the stop is added
  to the clip, so total action time is exact and doesn't drift by a frame.
- **Base durations (1x):**

  | Hit | Base | Extra |
  |---|---|---|
  | Light (per family table) | 0.05–0.08 | |
  | Heavy family | 0.12 | |
  | Crit | +0.04 | |
  | Killing blow | +0.03 | |
  | Ultimate's final hit | 0.14 | |
  | Multi-hit, not the last | 0.03–0.05 | the last hit takes the family value |
  | DoT ticks, heals, buffs | 0 | |

- **Cap:** 0.25 s total hit-stop per action. At 30 fps the shortest stop (0.05 s) still shows on
  1–2 frames, and at 120 fps on 6.
- **Ranged:** the projectile's arrival is the impact. Only the target freezes, plus the projectile
  head for 0.03 s.

### 4.2 Screen shake

Replace `Vfx.ShakeRoutine`. Today it picks a new random offset every frame, so at 120 fps it
buzzes twice as fast as at 60.

- **Offset:** `amp × env(t) × noise(t × 22 Hz)` on the camera's local X and Y, plus roll of
  `amp × 6°/m`. `noise` is Perlin with a per-shake seed, sampled by **time**, never per frame.
  `env(t) = (1 − t/dur)²`.
- **Values:** amplitude in metres at the default camera distance; duration at 1x, scaled by speed.

  | Event | Amplitude | Duration |
  |---|---|---|
  | Normal hit | none | |
  | Heavy-family hit | 0.06 | 0.18 |
  | Crit | 0.10 | 0.22 |
  | Ultimate impact | 0.16 | 0.35 |
  | Titan slam | 0.28 | 0.40 |

- **Stacking:** take the maximum, never the sum.
- **Settings:** a **Screen shake** setting with Full, Reduced (×0.4) or Off.

### 4.3 Impact flash

- **Target:** an emissive/rim boost in the attacker's element colour, 0.06 s, fading out over the next 0.10 s.
- **Crits:** add a full-screen element tint at 12% alpha for 0.08 s (today's `CritFlash`).
- **Minimum one frame:** a flash shorter than one frame is still drawn for one rendered frame,
  without adding time to the action.
- **Starts with hit-stop.** The flash plays on unscaled time, so it shows during the stop.

### 4.4 Knockback, projectiles, travel

- **Knockback** starts when hit-stop ends:
  - push out 0.20 m (Hit) or 0.34 m (Hit_Big) in 0.08 s with an ease-out;
  - hold 0.05 s;
  - return in 0.30 s with an ease-in-out.
  - Knocked_Down pushes 0.6 m.
  - All of it uses evaluated curves over time, as `HitRoutine` does now.
- **Projectiles** move by **speed**, not by a fixed duration:

  | Kind | Speed | Flight time clamp |
  |---|---|---|
  | Arrow | 22 m/s | 0.15–0.35 s |
  | Bolt | 16 m/s | 0.18–0.40 s |
  | Orb | 9 m/s | 0.25–0.55 s |
  | Shard | 14 m/s | 0.18–0.45 s |

  An arc's height is relative to the distance. Trail and spark emission is a rate per second
  (section 4.6).
- **Melee travel:** stop 1.15 m short (today's rule), at 7 m/s. Run cycle footfalls sync to
  distance (stride 1.4 m), so feet don't slide at any speed.

### 4.5 Feedback for buffs, debuffs, DoTs and deaths

| Event | Character | Effect / UI | Time |
|---|---|---|---|
| Buff (AtkUp, DefUp, SpdUp, Regen, Shield, Counter) | additive chest-up | rising motes, icon pop | 0.4 s |
| Debuff (AtkDown, DefDown, Slow, TmDrain) | additive shoulders-down | sinking motes, icon pop | 0.4 s |
| Poison, Burn tick | small flinch (additive, 0.15 s) | tick number, no hit-stop | 0.15 s |
| Stun | Stunned loop | Stun icon (orbiting stars) | until the turn is skipped |
| Break! (meter full) | Broken state | orange "BREAK!" number (1.35x), small cracks in the breaking element, light shake, cracked-sigil Broken icon | 0.75 s |
| Broken (the lost turn) | none | orange "Broken!" | 0.35 s turn tail |
| Break meter | none | 4 px orange bar with a bright leading edge under the HP bar, only while above 0 | live |
| Poison/Burn stacks | none | "×2" / "×3" beside the status icon; one tick number per stack | per tick 0.12 s |
| Enrage onset | none | "Enrage! Damage up, healing down" (then "Enrage xN") on the actor; the round counter turns red; one-time Enrage tip | 0.7 s |
| Taunt | Ready plus a chest beat (`Interact`) | red ring on the ground | 0.5 s |
| Cleanse / Strip | brief upward glance | burst of the icon colour | 0.3 s |
| Pull (turn meter) | 0.15 m lurch toward the attacker | a tether streak | 0.25 s |
| Death | Death01, 1.2 s, then sink 0.6 s | DeathFade | 1.8 s, does not block the next action after 0.5 s |
| Revive | Revive state | light pillar 0.8 s | 1.1 s |

### 4.6 Frame-rate bugs found in the current code

Fix these in step 65 (PLAN.md phase G):
- **`Vfx.ShakeRoutine`:** per-frame `Random.insideUnitSphere`. Use time-sampled noise (4.2).
- **`SkillFx.Projectile`:** `if (Random.value < 0.6f) Spark(...)` per frame. That is twice the sparks
  (and twice the GPU cost) at 120 fps. Use a rate: `sparksPerSecond × dt`, carrying the remainder.
- **`SkillFx` beam:** the same per-frame `Random.value < 0.7f` spawn.
- **`KitLive.Spring`:** variable-`dt` semi-implicit Euler. It is stable but differs slightly between
  30 and 120 fps. Use a fixed 1/120 s substep with an accumulator (at most 8 substeps), and skip
  the integration while hit-stopped or paused.
- **Lerp loops** (`for (k = 0; k < 1; k += Time.deltaTime / dur)`) are time-based and fine. They must
  always set the exact end state after the loop, as most already do.
- **`HeroModel.Play`:** uses `CrossFadeInFixedTime` (seconds), which is good. Keep every fade in
  seconds.

## 5. Role personality

The timing multipliers apply to anticipation and recovery, on top of the family values.

| Role | Idle | Stance | Fidgets (pick 1 of 3) | Timing | Victory | Defeat |
|---|---|---|---|---|---|---|
| **Attack** | light, restless weight shifts, 3.2 s breathing cycle | forward lean 4°, weapon ready | weapon twirl, shoulder roll, impatient foot tap | anticipation ×0.85, recovery ×0.9 (snappy) | weapon flourish then point at the camera (`Sword_Attack` at 0.5x into a hold) | drops to one knee, weapon planted |
| **Defense** | planted, slow breathing (4.0 s), minimal sway | wide base, shield or guard up | shield tap, look left and right, plant weapon | anticipation ×1.1, recovery ×1.0, never dodges, prefers Block | shield raised, chest-out hold (`Yes`) | stands, shield lowered, head bowed |
| **HP** | heavy, rolling shoulders, 4.4 s breathing | upright, chest out, arms loose | crack neck, slap chest, stretch | anticipation ×1.15, recovery ×1.15 (weighty); Hit reactions at 0.8 amplitude (shrugs hits off) | arms wide, laugh (`Wave` at 0.8x) | sits down heavily |
| **Support** | gentle sway, prop glow pulses with breathing (3.6 s) | prop held close, half-turned toward allies | check the prop, glance at allies, small hum (head tilt) | anticipation ×1.0, recovery ×1.1, cast release later (0.60) | prop raised overhead, element ring (`Spell_Simple_Enter` hold) | prop hugged, kneel |

**Desync:** each unit's idle starts at a random phase (0–1). Breathing-cycle speed varies ±8% per
hero, set by hero id, so a lineup of five never breathes in sync.

## 6. Frame rate

### 6.1 Rules

Owner decision 2026-11-27 (replaces "Auto matches the display" as the default; 120 fps costs roughly
1.6-2x the power, a heat and battery risk in long auto-replay sessions). Rules in `Core/FrameRate.cs`.

- **60 (default):** the cap is 60 (or the display rate, if lower). The old saved "Auto" reads as 60.
- **90/120 · Smoothest:** a Settings choice on screens faster than 60 Hz: the display's refresh rate, up to 120 Hz.
  - Read it from `Screen.currentResolution.refreshRateRatio.value` (a double, Unity 6).
  - Round it, and treat 0 or NaN as 60.
  - Steps down to 60 when the phone can't hold it, and retries between battles.
- **30 · Battery:** the cap is 30.
- **Forced 30:** Low tier, thermal limits and **auto-replay** force 30.
- **Menus:** the screen may sleep (`SleepTimeout.SystemSetting`); it is kept awake only in battle. Behind a
  menu with an opaque full-screen painting (title screen, Summon) the arena camera draws nothing and skips
  post-processing.
- **Mobile (Android/iOS):**
  - `QualitySettings.vSyncCount = 0`, and `Application.targetFrameRate` = the target.
  - Turn on **Optimized Frame Pacing** (Swappy) in Player settings, so 120, 60 and 30 pace evenly
    on a 120 Hz panel.
  - On Android, Unity picks a display mode matching `targetFrameRate`.
  - Galaxy S26+ notes for the owner:
    - Settings → Display → **Motion smoothness: Adaptive** must be on for 120 Hz.
    - Game Booster / Game Optimizing Service can cap games at 60. If Auto shows 60 in the
      performance overlay, check Game Booster's frame-rate option.
- **Desktop (Windows):**
  - 90/120 = `vSyncCount = 1` and `targetFrameRate = -1`, which matches any monitor (60, 120,
    144, 165 Hz or VRR).
  - 60 or 30 = `vSyncCount = DisplayHz / cap` when that divides evenly (1–4). Otherwise
    `vSyncCount = 0` with `targetFrameRate = cap`.
- **Pick targets that divide the display rate** (120 → 120, 60, 40 or 30), so frames never judder.
  On a 90 Hz panel the high choice is 90.

### 6.2 Time rules for code

- **Battle presentation** (beats, travel, knockback, clips) uses scaled time: `Time.deltaTime` and
  `WaitForSeconds`. Battle speed and pause apply automatically.
- **UI, damage numbers, flashes, the auto-replay countdown, quality and thermal polling** use
  `Time.unscaledDeltaTime` and `WaitForSecondsRealtime`.
- **Hit-stop** uses scaled time but freezes only the two animators (4.1).
- **Smoothing:** `x = Lerp(x, target, 1 - Mathf.Exp(-rate * dt))`, never a constant factor.
  **Random spawning:** an accumulator of `rate × dt`.
- **Fixed timestep:** the battle uses no physics forces. Leave `Time.fixedDeltaTime` at 0.02 and
  keep gameplay out of `FixedUpdate`. If physics is added later, interpolate rigidbodies.
  Cosmetic springs use their own fixed substep (4.6).
- **Animation sampling:**
  - Animator update mode is **Normal** for heroes (scaled time). Culling is **Cull Update
    Transforms** for off-screen units.
  - Clips are authored at 30 fps (UAL source) and sampled every rendered frame, interpolated,
    so 120 fps shows smoother motion at the same timing.
  - Do not use "Animate Physics". Do not round clip time to source frames.
- **Events are crossings, not frame hits:** fire every event whose normalized time lies in
  `(previousTime, currentTime]` this frame, in order, also across loop wraps. A 33 ms frame at
  30 fps may cross two multi-hit impacts at once; both fire, and the second hit-stop starts after
  the first.

### 6.3 Exact changes to `Client/Quality.cs` (and its callers)

1. **Settings value.** Keep `settings.fpsCap` (shared save format with the web, and SaveCodec clamps
   it to 30 or 60). Add a new field `settings.fpsMode` = `"auto" | "60" | "30"`:
   - `SaveData.cs`: `public string fpsMode = "auto";`
   - `SaveCodec.cs`: read `fpsMode` (default: `"30"` if `fpsCap == 30`, else `"auto"`); write it next to `fpsCap`.
   - Keep `fpsCap` written as 30 or 60 for web compatibility.
2. **Quality fields.**
   ```csharp
   public string FpsMode = "auto";           // auto | 60 | 30
   int _autoHz;                              // Auto's current target; may drop 120 -> 60 under load
   public static int DisplayHz { get { double r = Screen.currentResolution.refreshRateRatio.value;
       return r > 1 && !double.IsNaN(r) ? Mathf.Clamp((int)System.Math.Round(r), 30, 240) : 60; } }
   int AutoTarget => _autoHz > 0 ? _autoHz : Mathf.Min(DisplayHz, 120);
   public int TargetFps => Tier == 0 || ThermalLimited || FpsMode == "30" ? 30
       : FpsMode == "60" ? Mathf.Min(60, DisplayHz) : AutoTarget;
   ```
   `Create(host, choice, fpsMode)` and `Set(choice, fpsMode)` take the mode string instead of `fpsCap`.
   Update `App.cs` lines 59 and 109 and `SettingsScreen.cs` 81–85 to match.
3. **`Apply()` frame-rate part.**
   ```csharp
   #if UNITY_ANDROID || UNITY_IOS
   QualitySettings.vSyncCount = 0; Application.targetFrameRate = TargetFps;
   #else
   if (FpsMode == "auto" && TargetFps == AutoTarget && _autoHz == 0) { QualitySettings.vSyncCount = 1; Application.targetFrameRate = -1; }
   else { int div = DisplayHz % TargetFps == 0 ? DisplayHz / TargetFps : 0;
          QualitySettings.vSyncCount = div >= 1 && div <= 4 ? div : 0; Application.targetFrameRate = QualitySettings.vSyncCount > 0 ? -1 : TargetFps; }
   #endif
   ```
4. **`Update()` auto-tier.** Compare against `TargetFps`, not `Application.targetFrameRate` (which is −1
   under desktop vsync). At a high refresh rate, **lower the frame target before the quality tier**,
   so a phone that holds 90 at 120 Hz doesn't lose its graphics:
   ```csharp
   int target = TargetFps;
   if (fps < target * 0.85f && target > 60 && FpsMode == "auto") { _autoHz = 60; _retryHzAt = Time.unscaledTime + 120f; Apply(); _acc = 0; _frames = 0; return; } // not gated by Busy: the look doesn't change
   ```
   The tier step-down and step-up then run against 60 as today. The rule stays `fps < target × 0.85`;
   the step-up stays `> upTarget × 0.97` with `upTarget = from == 0 ? (FpsMode == "30" ? 30 : 60) : target`.
5. **Retry 120.** At 60 the measured fps can't show headroom, so use `FrameTimingManager` (Unity 6;
   turn on *Frame Timing Stats* in Player settings):
   - every 4 s window, call `FrameTimingManager.CaptureFrameTimings()` and `GetLatestTimings`;
   - take `max(cpuFrameTime, gpuFrameTime)` in ms;
   - after `_retryHzAt`, if the 90th percentile is under `1000 / DisplayHz × 0.8` ms for two
     windows in a row, set `_autoHz = 0` and Apply;
   - a failed retry doubles the wait, up to 8 minutes;
   - where timings aren't supported (`cpuFrameTime == 0`), retry once per battle start instead.
6. **Thermals:** unchanged (forces 30). On cooling, restore `_autoHz = 0`.
7. **Settings row:** "Frame rate", with chips **Auto · 120** (showing the live `DisplayHz`),
   **60 · Smooth** and **30 · Battery**. Toasts:
   - "Matches your screen (120 Hz). Uses more battery."
   - "60 fps."
   - "30 fps: cooler and longer battery."
8. **Perf overlay:** show `TargetFps / DisplayHz`, plus "(lowered)" when `_autoHz` is in effect,
   so the owner can see what Auto chose.
9. **Battery note:** Auto at 120 uses noticeably more power. The thermal guard already handles heat.
   Starting Auto at 120 on Medium tier or above only (Low stays 30) is the default proposed here.

## 7. Animation tests for owner approval

Rendered in Blender on the new high-detail heroes (phase F bodies) with the retargeted UAL clips:
- every hero against a neutral training dummy (a Hollow enemy on the new body) on the default arena floor;
- 1080p video at 60 fps unless stated, with a small on-screen timer (seconds) and the beat name;
- a 0.5x slow version of each, to judge the details.

Timings are the 1x values from this document, so the render previews the game. The frame-rate
test (T11) is also repeated in the Unity build on the S26+ (an "Animation lab" debug screen).

Status column: **pending** until the owner approves, rejects or asks for changes.

| # | Test | Hero (role) | Family | Sequence and clips | Length | Camera | Owner judges | Status |
|---|---|---|---|---|---|---|---|---|
| T1 | **Idle lineup** | Lumen (Attack), Ulfgar (Defense), Thrain (HP), Seraphine (Support), Nyx (Attack) | blade, shield, brawler, instrument, daggers | Idle loops with random phase. Each hero plays one fidget at 3, 6, 9, 11 and 13 s. Then Ready on Lumen | 15 s | the battle camera, ally side, fixed | Do they feel alive and different by role? Any two breathing in sync? Fidgets natural, not distracting? | pending |
| T2 | **Blade basic, full beat** | Lumen | blade | Ready → anticipation 0.18 → Run 0.26 → `Sword_Attack` (impact 0.38) → hit-stop 0.07 → dummy Hit → recovery 0.28 → Run_Back 0.25 | 2.0 s | side three-quarter, both in frame | Does the hit land on contact? Weight shift readable? Feet sliding? | pending |
| T3 | **Hit-stop on vs off** | Cole (Attack) | heavy | The same overhead hammer strike twice, side by side: left with no hit-stop, right with 0.12 s hit-stop, Hit_Big knockback 0.34 m and a 0.06 m shake | 2 × 2.2 s | side, close | Does hit-stop make it feel heavier without feeling laggy? Is 0.12 s right, or should it be 0.08 or 0.16? | pending |
| T4 | **Multi-hit flurry** | Nyx (Attack) | dual daggers | Hollow Cut: Jab/Cross/Sword_Attack with impacts at 0.22, 0.48 and 0.74; 0.05 / 0.05 / 0.09 stops; three numbers | 1.8 s | low three-quarter | Can you count three distinct hits? Too fast or too slow? | pending |
| T5 | **Bow shot and projectile** | Tamsin (Support) | bow | `Pistol_Shoot` draw 0.26 → release 0.55 → arrow at 22 m/s over 6 m (0.27 s) → dummy Hit with a 0.05 s target-only stop → Slow debuff motes | 1.8 s | wide side, archer and target | Does the release feel like a bow? Arrow speed believable? Does the debuff read? | pending |
| T6 | **Caster ultimate with camera beat** | Morwenna (Attack) | staff/orb | Crown of Night: Enter → Channel 0.5 s (camera pushes in 0.4 s) → Shoot release 0.45 → AoE on 5 dummies → 0.14 s stop, 0.16 m shake → 1–2 dummies Knocked_Down (Stun) → Stunned loops | 3.2 s | starts on the caster, pulls out to the whole enemy row | Is it epic enough for a Legendary's ultimate? Camera move comfortable? Stuns readable? | pending |
| T7 | **Support heal** | Seraphine (Support) | instrument | Seventh Bell: Enter → Channel 0.6 s → Exit release 0.60 → heal motes on 5 allies, additive chest-up buff on each, numbers | 2.4 s | the ally row, from slightly above | Calm and graceful, clearly "good news"? Heal timing vs the bell motion? | pending |
| T8 | **Reactions: hit, big hit, block, dodge** | Ulfgar (Defense) taking hits, then Nettle (Attack) dodging | shield, daggers | Ulfgar: Hit → Hit_Big → Block (shield absorbs) → Block. Nettle: a glancing hit → Dodge sidestep 0.45 m | 5 s | front three-quarter of the defender | Can you tell all four reactions apart? Does the Defense hero feel sturdy (no dodge)? | pending |
| T9 | **Death and revive** | Solenne (HP) casts Dawn Vigil; Lumen dies and revives | heavy, blade | Solenne Cast_Support → Revive buff on allies → Lumen takes a killing Hit_Big → Death01 to 0.50 → light pillar → Revive stands at 0.85 → Ready | 4.0 s | Lumen centred, Solenne at the edge | Is the death dramatic but short? Does the revive read as "saved", not as a glitch? | pending |
| T10 | **Victory and defeat** | Team: Lumen, Ulfgar, Thrain, Seraphine, Tamsin | mixed | Last enemy falls → staggered victories (0.10–0.12 s apart, role poses); then the same team in Defeat poses | 6 s + 4 s | the battle camera, then a slow 10° orbit | Do the poses fit each role? Stagger natural? Defeat sad but not grim? | pending |
| T11 | **Frame-rate comparison** | Maelis (Attack) | polearm | The Trident basic (anticipation 0.24, impact 0.45, 0.08 stop, recovery 0.32, return 0.25), rendered at **30, 60 and 120 fps** in three columns of one **120 fps** video. A shared timer, and a flash marker on each column's impact frame | 3 × 1.6 s, synced | the same side camera in all three | All three must start, hit and finish at the same moment (marker within one frame of 30 fps). Only smoothness may differ. Also checked live in the Unity build at Auto/60/30 on the S26+ | pending |
| T12 | **Battle speed 1x / 2x / 3x** | Kaito (Attack) | blade | Thousand Cuts (4 hits) at 1x, 2x and 3x one after another, with hit-stop scaled by `speed^-0.55` and idle kept at 1x | 2.4 + 1.3 + 0.95 s | the battle camera | Is 3x still readable (hits countable)? Does idle stay calm at 3x? | pending |

**How the owner reviews:**
- The MP4s go in `docs/sigilbound/art/anim-tests/` with a contact sheet.
- The owner replies per test: approve, reject, or a change ("hit-stop longer", "less shake").
- The verdicts are recorded in the Status column and in ROADMAP.md's decisions log.

## 8. Change log

- **2026-11-25:** First version. Agent created at the owner's request ("set up a character
  mechanics agent… mechanics must adjust the frame to match the device's refresh rate"; "come up
  with some animation tests for approval").
- **2026-11-27:** Section 9 (combat rules after PLAN steps 97-99). Broken state, honest dodge rule,
  Hit_Big note, enrage/Break/stack feedback rows, section 1 turn start; the owner review moved to
  section 10 (it was a second "8.").


## 9. Combat rules (2026-11-27)

The rules engine is `Core/Battle.cs` (curves and data in `Core/GameData.cs` and `Core/Game.cs`). It
mirrors the web game's `battle.js` except where a rule is marked **Unity-only**; the web parity
tests add those back or divide them out. Measured with `Tests~ -- combatstats` (2,000 random 4v4,
level 40, 4 stars) after PLAN step 99: median 65 turns (about 8 rounds), p90 119, no stalls; 44%
of battles reach round 10.

### 9.1 Rules table

| Rule | Value | Where | Presentation |
|---|---|---|---|
| Turn meter | full at 1000; each tick adds speed × 0.7; the highest full meter acts, ties to the faster unit | `NextActor`, `PredictTurns` | turn-order bar (next 7) |
| Starting meter | speed × 3 + random 0-40 (the fastest unit acts first about 70% of the time) | `StartTm` | |
| New wave | allies' meters are capped at 60% | `CheckEnd` | "Wave N!" banner |
| Speed | 95 + 2 × rarity tier + role + `RoleSpdShift` + (stars − 1); SpdUp +30%, Slow −30% | `Game.BaseStats`, `Speed` | |
| Role speed (**Unity-only** shift) | Attack +8, Defense −2, HP −4, Support 0 (Support already has +6 in `Mods`, so its total is +6). Was ±8 until step 99, which gave Support +14 | `GameData.RoleSpdShift` | |
| Pace | every hit, heal, regen, shield and DoT tick × 1.6; HP is not scaled. 1.0 in the Titan fight | `Battle.Pace` | |
| Hit damage | stat × skill multiplier × Skill Tome boost (+8%/level to 3) × element × 650 / (650 + target DEF) × enrage × Pace, × (1 + Rage × missing HP share) for Rage skills, × crit, × 0.95-1.05; at least 1. The stat is ATK, DEF or max HP by the skill's `Scale` | `HitBase`, `Hit` | damage number, Hit or Hit_Big |
| Buffs on stats | AtkUp/AtkDown ±40% ATK, DefUp/DefDown ±50% DEF | `AtkOf`, `DefOf` | status icons |
| Element | ×1.3 on the element you beat (Flame > Gale > Tide > Flame; Light and Shadow beat each other), ×0.8 against the one that beats you | `ElementMultiplier` | orange number (strong), grey (weak) |
| Crits | chance = Crit (15 base); **half** on element disadvantage, none on disadvantage in the Titan fight; damage × (1 + Cdmg/100), Cdmg 50 base | `Hit` | "CRIT", flash, shake |
| Shields | caster's max HP × skill % × 0.8 × heal-enrage × Pace; 2 turns; a re-cast keeps the larger value and resets to 2 turns; absorb hits, not DoTs; a zero shield (late enrage) is not added | `ApplyFx` | "Shield N" number; dodge/block may play when a hit is fully absorbed |
| Heals | target's max HP × skill % × boost × heal-enrage × Pace; Regen 8% of max HP a turn | `HealUnit` | heal number; the healer is the event's `Source` (results screen's Healing column) |
| Poison / Burn | 4% / 5% of max HP × Pace a turn, once per stack, up to `MaxDotStacks` 3; ignore shields, DEF and enrage. The Titan never stacks (a re-apply only refreshes) | `BeginTurn` | one tick number per stack; "×2"/"×3" beside the icon |
| DotCap | on bosses and the Titan a tick (per stack) is capped at 3 × the strongest opponent's ATK × Pace. It never binds in the campaign (raw ticks are a third of the cap or less); it only acts on the Titan | `DotCap` | |
| Durations | effects lose a turn at the end of the holder's turn; an effect a unit puts on itself (or a Broken it takes on its own turn) is `Fresh` and skips its first countdown | `EndTurn` | turns left on the icon |
| Stun | the next turn is lost; the Titan is immune to Stun and TmDrain | `BeginTurn` | Stunned state, "Stunned!" |
| Execute, Pierce, Spread, Counter, Revive, Rage, Last Light | Unity-only roster mechanics (ROSTER.md section 4; `Tests~/MechanicsTests.cs`). Counter: half-power basic back at a single-target attacker; counter hits never counter | `Act`, `ApplyDamage` | see 3.1 |

### 9.2 Enrage

- A **round** is one turn for each unit alive when the wave started (`Round = wave turns / that count + 1`). It restarts with each wave.
- From round `EnrageFrom` (10) every round adds a stack: hits deal +10% per stack and heals and shields lose 10% per stack. At 10 stacks (round 19) healing and shields are 0. Not in the Titan fight (it has a 20-turn limit).
- The first time a stack starts, the actor shows "Enrage! Damage up, healing down" (later "Enrage xN").
- **HUD (step 99):** a round pill under the stage info ("Round N"); from round 10 it turns red with "Enrage xN" (landscape adds "+N0% dmg −N0% heal"). The one-time `Tips` "enrage" tip explains it the first time a wave enrages.
- **AI:** when healing and shields are down to 15% or less (`Battle.SustainFloor`, from round 18), pure heal, regen and shield skills count as useless.

### 9.3 Weakness Break

- Every hit with element advantage (×1.3) adds `BreakPerHit` 34 to the target's meter (17 on bosses). At 100 the target is **Broken**: it loses its next turn, the meter empties and `BreakGuard` 2 starts.
- Heroes can be Broken too (about 4% of all turns are lost to Break, on both sides). The Titan can't be.
- While Broken or guarded, hits add nothing. The guard counts down only on turns the unit **acts** after the lost one (step 99: a turn lost to Stun no longer counts, which removed Stun → Break → Stun chains).
- A Break that lands during the unit's own turn (a Counter hit on the attacker) is `Fresh`, so it still costs the next turn (step 99: it used to be used up at once).
- The meter is cleared on death.

### 9.4 Broken presentation

- Status icon `Resources/StatusIcons/Broken.png`: the orange rim with a **cracked diamond sigil** (`Tools~/skin/broken_icon.py`), not Stun's orbiting stars.
- On "Break!": the Broken state (3.1), an orange "BREAK!" number, small ground cracks in the element that broke it, a light shake. On the lost turn: an orange "Broken!".
- Tip and legend wording: "Hits from the element that beats a unit fill its orange bar (your heroes have one too). When it's full, that unit is Broken and loses its next turn."

### 9.5 AI (also auto battle)

- **Skill:** the ready skill with the highest `cooldown × 10 + index`, minus 100 for a self-Taunt already up and minus 100 for a damage-free skill that would change nothing (`Useful`): Heal/Regen need someone under 65% HP (and enrage above the sustain floor), Shield someone without a shield of 5% max HP, Cleanse a debuff, Strip a buff, Poison/Burn a target under 3 stacks, other effects someone who lacks them.
- **Enemy target:** Taunt forces the taunters. Then: a target it can kill (HP + shield ≤ 95% of the expected hit × hits, from the same maths as `Hit` without crits and spread: scaling stat, Skill Tome boost, element, DEF, enrage, Pace, Rage), then for pure debuff skills the target missing most of them, then the best element, then the lowest HP share.
- **Ally target:** buffs go to whoever lacks most of them (AtkUp prefers the Attack role), then the lowest HP share; heals to the lowest HP share.

### 9.6 Animation rules tied to the combat rules

- **Dodge only when no HP is lost** (a fully absorbed hit); never on a hit that lowers HP.
- Hit_Big still fires at 14% of max HP (now about a third of hits at Pace 1.6; see 3.1).

### 9.7 Campaign curve and rewards (step 99)

- Enemy multiplier: Act I follows the web curve (`Curve.Start`..`End`, `Shape`, chapter factors `Curve.Chapter`); Act II is `End × (1 + Act2Ramp × (stage − 59))` with `Act2Ramp` 0.015 (was 0.02).
- **Unity-only `Game.CampaignEase`** on top (campaign and Hard stages, not dungeons): Light/Shadow chapters × `LSEase` 0.92 (a Flame/Gale/Tide team has no advantage there), chapter 10's factor 0.98 in place of 1.04 (`ChapterFix`), and `StageEase` per stage in both acts: 14-4 × 0.926, 15-6 × 0.90.
- Gold and XP per clear grow 2% per stage past the level cap (stage index 61): 15-6 pays 5,928 gold and 18,065 XP (was 3,800 and 11,580, flat for the last 29 stages).
- Titan reward tiers (**Unity-only** thresholds): 0 / 15k / 40k / 80k / 140k / 220k (was up to 400k and 1M, never reached). A maxed team (6 stars, level 60, Mythic +10) has median 112k and p90 186k damage: 29% reach 140k, 3% reach 220k.

| Measure (60 runs a stage) | Before step 99 | After |
|---|---|---|
| Act I Light/Shadow, mid / strong team | 86.1% / 98.6% | 92.6% / 99.7% |
| Act II Light/Shadow, mid / strong team | 39.6% / 80.0% | 82.6% / 96.5% |
| Act II triangle, mid / strong team | 86.8% / 98.4% | 90.0% / 99.2% |
| 10-1, mid / strong | 5% / 100% | 63% / 100% |
| 15-6, mid / strong | 0% / 2% | 8% / 78% |
| Role vs role (3 shared fillers, Epic, 1,500 a pair): Attack / Defense / HP / Support | 49.3 / 42.7 / 46.9 / 61.1 (spread 18.3) | 48.7 / 47.7 / 50.0 / 53.6 (spread 5.9) |
| Median battle (combatstats) | 67 turns | 65 turns |

Mid team: Ysolde, Corvan, Hask, Kaito. Strong team: Maelis, Oriel, Aurelia, Vaelith (the suite's lineups).

## 10. Owner review of the animation tests (2026-11-25)

Approved: T1 idle, T2 blade attack, T6 spell cast, T9 death, and all 150 concepts. Changes requested and done: T3 spear (now `Spear_Thrust`: guard, draw, lunge, recover), T8 heavy hit (now `Hit_Kneel`: stagger, drop to one knee, rise), T10 defeat (fall pivots on the planted knees; PvP losses only, owner 2026-11-26). Deaths always use the death clip. Hand-made clips are authored in `unity/Tools~/blenderforge/make_clips.py`.
