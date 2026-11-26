# Sigilbound: 50 new heroes (roster 100 to 150)

Concept pack for the owner's review. Every hero is original to Sigilbound and built from the CC0 Quaternius Universal Base Characters and the Fantasy outfit kit, plus custom pieces (marked **custom** in the data). The full machine-readable data, including outfit part names and skill numbers, is in `new_heroes.json` next to this file. Skill numbers are starting points; designers will tune them.

## Overview

**50 heroes: 26 female, 24 male. 22 are non-human (44%), and 28 are human.**

### Element by rarity

| | Common | Rare | Epic | Legendary | Total |
|---|---|---|---|---|---|
| Flame | 2 | 3 | 3 | 2 | 10 |
| Gale | 3 | 3 | 3 | 1 | 10 |
| Tide | 2 | 4 | 2 | 2 | 10 |
| Light | 3 | 3 | 3 | 1 | 10 |
| Shadow | 2 | 3 | 3 | 2 | 10 |
| **Total** | **12** | **16** | **14** | **8** | **50** |

### Element by role

| | Attack | Defense | HP | Support | Total |
|---|---|---|---|---|---|
| Flame | 3 | 3 | 2 | 2 | 10 |
| Gale | 3 | 2 | 2 | 3 | 10 |
| Tide | 3 | 3 | 2 | 2 | 10 |
| Light | 3 | 2 | 3 | 2 | 10 |
| Shadow | 3 | 2 | 2 | 3 | 10 |
| **Total** | **15** | **12** | **11** | **12** | **50** |

### Peoples

| Element | Non-human heroes |
|---|---|
| Flame | 4: Dace (gnome), Holt (bear-folk), Saskia (ember dryad), Obrecht (hearth golem) |
| Gale | 4: Tansy (hare-folk), Corran (goat-folk), Linnet (elf), Aveline (sylph) |
| Tide | 4: Wenna (turtle-folk), Lazlo (shark-folk), Neve (tidekin), Morvoren (barrow wraith) |
| Light | 5: Poppy (petal sprite), Benedict (sandstone golem), Ottavia (elf), Fabian (gnome), Augustin (lion-folk) |
| Shadow | 5: Rook (raven-folk), Perpetua (cat-folk), Jasper (fox-folk), Lune (owl-folk), Ondessa (fen dryad) |


Species count: human 28, elf 2, gnome 2, barrow wraith 1, bear-folk 1, cat-folk 1, ember dryad 1, fen dryad 1, fox-folk 1, goat-folk 1, hare-folk 1, hearth golem 1, lion-folk 1, owl-folk 1, petal sprite 1, raven-folk 1, sandstone golem 1, shark-folk 1, sylph 1, tidekin 1, turtle-folk 1.

### Style rules for this batch

- **One idea per hero.** Each hero has one signature object that no other hero among the 150 has (an oven door, a whale-tail shield, an hourglass backpack, a floating loom), and the silhouette is built around it so it reads at 70 px in battle.
- **Element lives in the light.** The palette's `glow` colour (ember orange, wind green, sea blue, dawn gold, violet) drives the rim light, eyes, runes and effects. The painted clothes stay in that element's family of hues, but the main colour is the hero's own.
- **Rarity is finish, not palette.** Commons wear practical cloth, leather and wood; Rares add one metal or signature piece; Epics add a cape or large back piece and 2-3 glowing details; Legendaries get a signature material (molten iron, sea-steel, ribbed glass, woven wind) and a feature above the head (an orbiting comet, a floating loom, a curling wave, a light beam, a lily crown, a shadow twin, a smoke-ring crown, a crest of frozen spray), plus an aura.
- **Non-humans share one skeleton.** Every hero, beastfolk and golems included, is a Regular male or female body with custom heads, ears, tails, horns, shells, plates or foot covers, so they all use the shared animation library. Spirits that float or fade (Aveline, Morvoren, Ondessa) still have legs inside the mesh; nothing needs a custom skeleton.
- **Warm, not grim.** Undead and fen spirits read as sad or eerie through colour and posture, never as horror: no skulls, gore or spikes. Villain-adjacent heroes (Aldric's shadow, Ondessa) avoid the horned, spiked dark-lord look.


## Flame

### Dace Sootworth, the Chimney Gnome

**Flame · Defense · Common** · male gnome · act 2 · id `dace`

A pocket-sized, soot-black gnome with long pointed ears, a stovepipe cap and an orange scarf. He hides almost entirely behind a square iron oven door whose slotted vent glows like a furnace, and a round chimney brush sticks up behind his head. *Silhouette:* a tiny figure hidden behind a square black door, a round brush head poking up over his shoulder like a dark sun.

- **Palette:** main `#2b2725`, secondary `#5a4636`, accent `#ff8a3a`, metal `#4a4a50`, glow `#ff6a2a`
- **Outfit:** custom: short stovepipe sweep's cap with a brass band; Male_Peasant/Body; Male_Peasant/Arms; Male_Peasant/Legs; Male_Ranger/Feet_Boots; Male_Knight/Acc_Scarf; custom: long pointed gnome ears; custom: bulbous soot-smudged nose
- **Kit:** custom: square cast-iron oven-door shield with a glowing vent grille; custom: round chimney brush on a jointed pole, worn on his back (weapon: oven-door shield (left) and brush pole used as a cudgel (right))
- **Signature:** an oven-door shield whose vent grille glows ember-orange
- **Pose and effects:** crouched behind the door, peering over the top edge, brush cocked; soot puffs on every hit; the grille brightens when he shields the team; flame-orange rim
- **Story:** The gnomes of Saltreach's rooftops sweep every chimney in the harbour. When Hollow came down the flues, Dace tore the oven door off the bakehouse and held the alley alone.
- **3D build:** Regular male at 0.86 height with a 1.1 head and short limbs. Custom ears and nose are rigid on the head bone. Peasant set recoloured near-black with orange cuffs. The door is a bevelled box 0.6 x 0.5 m with a 3 x 4 slot grille on the glow mask; the brush head is a disc of alpha-card bristles.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Grille Bash | one enemy | 0.85 x DEF | basic | Burn (2t, 10% chance) |
| Soot Screen | all allies | - | 4 | DefUp (2t), Shield (4%) |

### Dulcie Oventhorne, the Bakehouse Matron

**Flame · HP · Common** · female human · act 2 · id `dulcie`

A broad, rosy baker in a brick-red dress and a huge white apron, with a kerchief and an auburn bun. She carries a long wooden bread peel whose scorched blade still glows orange from the oven. *Silhouette:* a round bell of white apron with a long flat paddle held diagonally like a spear.

- **Palette:** main `#f2e6d0`, secondary `#a8452a`, accent `#ffb347`, metal `#6a5a4a`, glow `#ff8a3a`
- **Outfit:** custom: flour-dusted kerchief over a bun; Female_Peasant/Body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Peasant/Feet; custom: long white apron with an ember-orange hem; custom: loaf-shaped satchel
- **Kit:** custom: bread peel taller than she is, blade scorched black at the tip (weapon: bread peel (two-handed wooden paddle))
- **Signature:** a bread peel whose blade still glows from the oven
- **Pose and effects:** peel planted beside her, other fist on her hip; heat shimmer and drifting ember crumbs; orange rim
- **Story:** Her Saltreach bakehouse oven has not gone cold in forty years. The harbour's defenders eat first, and she makes sure they come back to eat again.
- **3D build:** Regular female, bulk 1.22. Peasant set in brick red under a custom apron mesh with secondary cloth bones. The peel is about 1.9 m with a 0.35 m blade, glow mask on the scorched tip.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Peel Smack | one enemy | 0.08 x HP | basic | - |
| Fresh from the Oven | all allies | - | 4 | Heal (8%), Taunt (2t, on self), Shield (15%, on self) |

### Holt Ironpot, the Cauldron Wall

**Flame · Defense · Rare** · male bear-folk · act 1 · id `holt`

A big brown bear-folk cook in a rust leather jerkin with a round iron pauldron, wearing an upturned cooking pot as a helmet. On his arm sits a cast-iron cauldron with glowing coals in its rim, and he swings a heavy iron ladle. *Silhouette:* a squat, furry bulk behind a black dome of cauldron, topped by a matching pot helmet: two domes.

- **Palette:** main `#3a3634`, secondary `#7a3a22`, accent `#e8a040`, metal `#4c4a48`, glow `#ff6a1a`
- **Outfit:** custom: upturned iron pot helmet with a riveted rim; Male_Knight/Body_Cloth; Male_Knight/Arms; Male_Knight/Legs; custom: digitigrade paw boot covers; Male_Knight/Acc_Pauldron_Round; custom: bear head (muzzle, round ears); custom: stubby tail
- **Kit:** custom: half-cauldron shield, a cast-iron bowl on the left arm with coals glowing in its lip; custom: iron ladle mace (weapon: iron ladle mace)
- **Signature:** an upturned cauldron shield that still holds glowing coals in its lip
- **Pose and effects:** cauldron forward, ladle raised behind it; coals spill as embers on Spill the Coals; heat shimmer over the shield
- **Story:** A bear-folk cook who fed the Ashgrave Pass forge crews. When the Hollow came he flipped the stew pot over and has carried it into every fight since.
- **3D build:** Regular male at bulk 1.25 with a custom bear head mesh replacing the human head (same head bone), a fur ruff on alpha cards and paw boot covers over the feet. Keep the muzzle short so the pot helmet sits right. The cauldron is a half sphere of 0.55 m with a glowing coal ring on the glow mask.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Pot Slam | one enemy | 0.9 x DEF | basic | Burn (2t, 25% chance) |
| Cast-Iron | self | - | 4 | Shield (30%), Taunt (2t) |
| Spill the Coals | all enemies | 0.6 x DEF | 5 | Burn (2t, 40% chance) |

### Rhosyn Slingcoal, the Ember Grove Slinger

**Flame · Attack · Rare** · female human · act 2 · id `rhosyn`

A wiry, dark-skinned slinger in charcoal leathers with red-tipped cropped hair and a bandolier of smoking pouches. A glowing coal spins in her sling and draws a ring of ember light over her head. *Silhouette:* a lean figure with one arm raised, whirling a sling that draws a glowing ring above her head.

- **Palette:** main `#4a3a2a`, secondary `#7a2a1a`, accent `#ffb030`, metal `#8a6a40`, glow `#ff7a2a`
- **Outfit:** Female_Ranger/Body; Female_Ranger/Arms; Female_Ranger/Legs; Female_Ranger/Feet; custom: cross-chest bandolier of smoking coal pouches; custom: single left leather pauldron
- **Kit:** custom: leather sling with a glowing coal in the pouch (weapon: sling)
- **Signature:** a whirling sling that leaves a ring of ember light over her head
- **Pose and effects:** mid wind-up, weight on her back foot, sling circling overhead; ember-trail circle; a burning coal projectile; orange rim
- **Story:** Raised among the Ember Groves' charcoal kilns, she learned to hit a crow at sixty paces with a hot coal. Now she hits Hollow.
- **3D build:** Ranger set in charcoal leather, hood removed. The sling is two rope strands on 3 helper bones so it can spin in the idle loop. The bandolier holds 5 pouches with glowing coal tops.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Hot Coal | one enemy | 1.0 x ATK | basic | Burn (2t, 30% chance) |
| Double Shot | one enemy | 0.6 x ATK x2 hits | 3 | Burn (2t, 40% chance) |
| Coal Hail | all enemies | 0.85 x ATK | 4 | Burn (2t, 40% chance) |

### Brother Ansel Smokewright, the Censer Monk

**Flame · Support · Rare** · male human · act 1 · id `ansel`

A tall, tonsured monk in a deep madder-red robe with a rope belt of red-glass beads. He swings a brass censer on a long chain, and it puffs rings of glowing smoke. *Silhouette:* a tall robed monk; a long chain arcs from his hand to a swinging brass censer under a column of smoke.

- **Palette:** main `#8a2a1e`, secondary `#d8c8a8`, accent `#e8b040`, metal `#b08a3a`, glow `#ff9a40`
- **Outfit:** Male_Wizard/Body; Male_Wizard/Arms; Male_Wizard/Legs; Male_Wizard/Feet; custom: rope belt with red-glass prayer beads
- **Kit:** custom: brass censer on a long chain, trailing smoke rings (weapon: chained censer)
- **Signature:** a brass censer that breathes perfect rings of ember-lit smoke
- **Pose and effects:** censer swinging low, free hand raised in blessing; smoke rings with glowing orange cores; warm motes on heals
- **Story:** A brother of the Last Pyre who swung the censer at the eternal flame's vigil. He carried one coal of it west to Saltreach to rekindle the bearers' courage.
- **3D build:** Wizard robe recoloured madder red. The censer chain is 4 helper bones off the right hand; smoke rings are a VFX emitter at Socket_Cast.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Censer Swing | one enemy | 1.0 x ATK | basic | AtkDown (2t, 30% chance) |
| Sweet Smoke | all allies | - | 4 | Heal (15%), Cleanse |
| Kindled Prayer | all allies | - | 4 | AtkUp (2t), Turn Meter + (15% meter) |

### Saskia Emberbark, Dryad of the Burning Grove

**Flame · Defense · Epic** · female ember dryad (living tree) · act 2 · id `saskia`

A tall dryad whose skin is dark bark split by glowing ember veins, with charred twigs and smouldering leaves for hair. She braces a woven ember-wood shield and wears a cape of burning leaves. *Silhouette:* a tall upright oval shield and a flared leaf cape: the outline of a burning bush.

- **Palette:** main `#4a2e1e`, secondary `#7a4a2a`, accent `#ff9a30`, metal `#8a5a30`, glow `#ff6a2a`
- **Outfit:** custom: layered bark-plate body with glowing ember veins; custom: bark-plated forearms; Female_Ranger/Legs; custom: root-like foot covers; Female_Ranger/Acc_Pauldrons; custom: cape of overlapping charred leaves with glowing edges
- **Kit:** custom: tall oval shield woven from ember-wood branches, leaves still smouldering; custom: short bronze billhook (weapon: bronze billhook)
- **Signature:** a woven ember-wood shield whose leaves never stop smouldering
- **Pose and effects:** shield braced, leaf cape blown back; drifting glowing leaves, ember-lit cape edge, flame rim
- **Story:** The tree-spirit of the oldest ember tree. When a Light shard fell among the groves and set them alight, she stepped out of her trunk to keep the fire from reaching the villages.
- **3D build:** Regular female with skin painted as dark bark. Custom bark plates are rigid per bone, with ember veins on the glow mask. The twig crown is alpha cards. The leaf cape uses 4 secondary bones.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Bark Bash | one enemy | 0.95 x DEF | basic | Burn (2t, 25% chance) |
| Grove Wall | all allies | - | 4 | Shield (6%), Taunt (2t, on self) |
| Kindling Thorns | self | - | 5 | Shield (20%), Counter (2t) |

### Kasimir Brand, the Sigil-Brander

**Flame · Attack · Epic** · male human · act 1 · id `kasimir`

A broad, moustached soldier in oxblood leather and soot-dark plate, with one spiked pauldron and a smith's apron-skirt. He shoulders a huge branding iron whose Sigil-shaped head glows white-hot. *Silhouette:* a long diagonal pole ending in a bright, glowing square stamp.

- **Palette:** main `#5a2018`, secondary `#2a2422`, accent `#ffb040`, metal `#5a5450`, glow `#ff6a1a`
- **Outfit:** Male_Knight/Body_Armor; Male_Knight/Arms; Male_Knight/Legs; Male_Ranger/Feet_Boots; Male_Knight/Acc_Pauldron_Spike (right only); custom: thick leather smith's apron-skirt; custom: short scorched half-cape
- **Kit:** custom: two-handed branding iron, 2 m long, its head a glowing stamp shaped like a broken Sigil (weapon: branding iron (polearm))
- **Signature:** a branding iron whose Sigil-shaped head glows white-hot
- **Pose and effects:** pole over his shoulder, stamp glowing behind his head; white-orange stamp glow; a scorched Sigil mark appears on targets he weakens
- **Story:** The Pyre guard's brander once marked supply crates. Now he marks the Hollow, so every ally knows where to strike.
- **3D build:** Knight armour recoloured oxblood and soot iron. Polearm archetype clips. The stamp head is 0.3 x 0.3 m with the glowing Sigil on the glow mask.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Brand | one enemy | 1.1 x ATK | basic | DefDown (2t, 40% chance) |
| Searing Mark | one enemy | 2.0 x ATK | 3 | Burn (2t), DefDown (2t, 60% chance) |
| Iron Sweep | all enemies | 0.9 x ATK | 4 | Burn (2t, 40% chance) |

### Imogen Drumfire, the War-Drummer

**Flame · Support · Epic** · female human · act 2 · id `imogen`

A bright redhead in scarlet and cream with her hair in two high puffs and a short flame-red cape. A war drum hangs at her hip, its skin glowing like a forge, and she raises two torch-tipped drumsticks. *Silhouette:* a big drum circle at her hip and two sticks raised in a V.

- **Palette:** main `#c8402a`, secondary `#f0e0c0`, accent `#e8b040`, metal `#b08a3a`, glow `#ff8a3a`
- **Outfit:** Female_Peasant/Body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Ranger/Feet; custom: crossed scarlet sash with brass studs; custom: short flame-red cape
- **Kit:** custom: war drum slung at the hip, skin glowing; custom: two torch-headed drumsticks (weapon: war drum and drumsticks)
- **Signature:** a war drum whose skin flares like a forge on every beat
- **Pose and effects:** sticks raised in a V, mid-beat; orange shockwave rings on each beat; sparks from the stick tips
- **Story:** Saltreach's harbour guard marched to her drum before the Hollow came. Now every beat carries the heat of the old lighthouse fire.
- **3D build:** Peasant set recoloured scarlet and cream. The drum hangs on Socket_Hip; drumsticks are paired props for both hands. The cape uses 3 secondary bones.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Drumbeat | one enemy | 1.0 x ATK | basic | - |
| Rally Beat | all allies | - | 4 | AtkUp (2t), SpdUp (2t) |
| Ember Cadence | all allies | - | 5 | Turn Meter + (20% meter), Heal (15%) |

### Ilsabet Cometheart, the Falling Star of Ashgrave

**Flame · Attack · Legendary** · female human · act 1 · id `ilsabet`

A dark-skinned warrior in soot-black plate split by molten seams, with a long split cape and a single heavy braid tipped with a glowing iron ring. She spins a meteor hammer around herself while a small comet orbits above her head. *Silhouette:* a slim figure inside a big glowing orbit of chain and ball, with a tiny comet circling above her head.

- **Palette:** main `#1e1614`, secondary `#5a1a10`, accent `#ffa020`, metal `#2a2422`, glow `#ff5a1a`
- **Outfit:** custom: fitted soot-plate corset over the Female Ranger body; custom: blackened plate bracers with molten seams; Female_Ranger/Legs; custom: armoured greaves; custom: asymmetric molten pauldron (left); custom: long split cape of soot-black silk with a glowing hem
- **Kit:** custom: meteor hammer: a molten iron ball on a long chain; custom: small comet orbiting above her head, trailing embers (breaker) (weapon: meteor hammer (chain weapon))
- **Signature:** a comet that orbits her head on an ellipse while her meteor hammer draws a second, larger orbit
- **Pose and effects:** chain spinning around her body, ball blazing; animated molten seams in her plate, an ember aura, a comet trail overhead
- **Story:** When the Titan first erupted, a burning stone fell into Ashgrave Pass and a girl caught it on a chain. She has never let it cool.
- **3D build:** Legendary finish: molten metal is the signature material. The chain is 6 helper bones; the comet orbit is a VFX ring on Socket_Head. Animate the glow on her plate seams and braid ring.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Comet Lash | one enemy | 1.2 x ATK | basic | Burn (2t, 50% chance) |
| Meteor Ring | all enemies | 0.7 x ATK x2 hits | 3 | Burn (2t, 50% chance) |
| Starfall | one enemy | 3.0 x ATK | 4 | Burn (2t), AtkUp (2t, on self), Spread |

### Obrecht, the Last Hearth

**Flame · HP · Legendary** · male hearth golem (stone construct) · act 2 · id `obrecht`

A giant of mortared fieldstone with a glowing fire-mouth in his chest, iron bands on his arms and soot-stone beard plates. A stone chimney rises over his shoulders, breathing flames and rings of smoke that float above his head like a crown. *Silhouette:* a huge round stone giant with a tall chimney rising behind his shoulders and smoke rings above his head.

- **Palette:** main `#6a5a4a`, secondary `#8a3a1e`, accent `#ffb050`, metal `#3a3430`, glow `#ff7a2a`
- **Outfit:** custom: floating crown of smoke rings (breaker); custom: hearthstone chest with a glowing fire-mouth at the centre; custom: fieldstone arm plates bound with iron bands; custom: stone leg plates; custom: iron-shod stone feet; custom: chimney stack of fused stones rising over both shoulders, flames curling out; Male_Peasant/Body as a scorched leather apron underlayer
- **Kit:** custom: hearth fire-mouth in the chest; custom: chimney back-stack; custom: long hooked fire-iron poker (weapon: fire-iron poker)
- **Signature:** a stone chimney on his back that breathes flame and smoke rings
- **Pose and effects:** poker planted, chest out, chimney fuming; animated glowing cracks in the chimney and chest, a warm aura, the smoke-ring crown
- **Story:** The last living hearth of the Ember Groves' charcoal village stood up when the Hollow came, built itself a body from its own fieldstones, and has guarded the groves' people ever since.
- **Passive:** Banked Embers: when an ally falls, heal all living allies 12% of their max HP (HeroDef.FallHeal 0.12).
- **3D build:** Regular male at 1.15 height and 1.25 bulk, fully skinned in rigid stone plates (one plate per bone) over a hidden body. Custom stone head. The chimney is a rigid prop on Socket_Back; the fire-mouth and cracks go on the glow mask. Uses the shared Heavy clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Poker Slam | one enemy | 0.09 x HP | basic | Burn (2t, 30% chance) |
| Hearthguard | all allies | - | 4 | Shield (12%), Taunt (2t, on self), Regen (2t) |
| Flue Roar | all enemies | 0.06 x HP | 5 | Burn (2t, 50% chance) |


## Gale

### Tansy Burrfoot, the Hare-Folk Runner

**Gale · Attack · Common** · female hare-folk · act 1 · id `tansy`

A small hare-folk girl with tall floppy ears (one bent), a moss-green tunic and a green scarf. She holds a wooden boomerang nearly her own width, carved with wind lines that glow in flight. *Silhouette:* a small figure topped by two tall ears, a wide V-shaped boomerang held back.

- **Palette:** main `#6a8a3a`, secondary `#c8a860`, accent `#e8d070`, metal `#7a5a3a`, glow `#5ad08a`
- **Outfit:** custom: tall hare ears (one bent); Female_Peasant/Body; Female_Peasant/Arms; Female_Peasant/Legs; custom: digitigrade hare-foot covers; custom: green neck scarf; custom: cotton tail
- **Kit:** custom: large carved wooden boomerang with wind-line grooves (weapon: boomerang)
- **Signature:** a boomerang almost as wide as she is tall, whose carved wind lines glow green in flight
- **Pose and effects:** boomerang cocked behind her ear, crouched to spring; green arc trail looping back to her hand
- **Story:** The fastest runner of the Thornwick warrens, she can outpace a fox and catch her boomerang before it lands. The Hollow are slower than foxes.
- **3D build:** Regular female at 0.9 height. Custom hare ears with 2 secondary bones each (they flop); digitigrade foot covers over the feet; the tail is rigid on the hips. Peasant set in moss and straw.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Boomerang | one enemy | 0.5 x ATK x2 hits | basic | - |
| Return Throw | one enemy | 1.4 x ATK | 3 | Slow (2t, 40% chance) |

### Barley Fleecewell, the Shepherd of Stormcrown

**Gale · HP · Common** · male human · act 1 · id `barley`

A broad, white-bearded shepherd in a moss-green smock under a huge, cloud-like woollen mantle. He rests a pair of giant shears open on his shoulder. *Silhouette:* a round white cloud of fleece over broad shoulders and the X of open shears.

- **Palette:** main `#e8e2d2`, secondary `#5a7a4a`, accent `#c89a3a`, metal `#8a8a8a`, glow `#5ad08a`
- **Outfit:** Male_Peasant/Body; Male_Peasant/Arms; Male_Peasant/Legs; Male_Peasant/Feet; custom: huge woolly fleece mantle over the shoulders; custom: shears holster
- **Kit:** custom: giant two-handed sheep shears (1 m); custom: cloud-like fleece mantle (weapon: sheep shears)
- **Signature:** a fleece mantle so thick it puffs out wider than his shoulders
- **Pose and effects:** shears resting open on one shoulder; wool tufts and green wind on hit
- **Story:** Stormcrown's high pastures taught him to stand in a gale. He stands in front of his friends the same way.
- **3D build:** Peasant set in moss green. The fleece mantle is a lumpy, sculpted mesh (no alpha). Shears are a two-handed prop on Socket_HandR.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Shear Snip | one enemy | 0.08 x HP | basic | Slow (2t, 20% chance) |
| Woolwrap | all allies | - | 4 | Shield (5%), Taunt (2t, on self) |

### Clover Kitewright, the Kite-Flyer

**Gale · Support · Common** · female human · act 2 · id `clover`

A small girl in a green smock with ribboned pigtails and a string spool held high. A green-and-cream diamond kite with a long coral ribbon tail hovers above her head. *Silhouette:* a child-sized figure with a diamond shape floating high above her.

- **Palette:** main `#3fa070`, secondary `#e8d8a0`, accent `#f08a4a`, metal `#7a5a3a`, glow `#7ae0a0`
- **Outfit:** Female_Peasant/Body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Peasant/Feet; custom: hand spool of kite string
- **Kit:** custom: diamond kite with a long ribbon tail, tethered to her spool and hovering above her (weapon: kite on a string)
- **Signature:** a tethered kite that hovers over her head and dives at enemies
- **Pose and effects:** spool held high, kite overhead; the kite swoops at targets; green wind streaks on buffs
- **Story:** Saltreach's harbour children fly kites to read the storm. Clover's kite read the Hollow coming before the lookouts did.
- **3D build:** Peasant set in green and cream. The kite is a rigid prop on a 1.2 m string of 3 helper bones from the left hand; the ribbon tail is 3 more bones.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Kite Dive | one enemy | 1.0 x ATK | basic | Slow (2t, 30% chance) |
| Updraft | all allies | - | 4 | SpdUp (2t), Turn Meter + (10% meter) |

### Edric Millsail, Miller of the Windward Hill

**Gale · Defense · Rare** · male human · act 1 · id `edric`

A sturdy, moustached miller in sailcloth and green, with a flour-dusted cap and a leather pauldron. He holds a whole windmill sail, lattice and canvas, as a tall slanted shield. *Silhouette:* a tall slanted lattice rectangle beside a sturdy man.

- **Palette:** main `#d8cfb8`, secondary `#4a6a3a`, accent `#c8a050`, metal `#6a5a48`, glow `#5ad08a`
- **Outfit:** custom: flat miller's cap dusted with flour; Male_Knight/Body_Cloth; Male_Peasant/Arms; Male_Peasant/Legs; Male_Ranger/Feet_Boots; Male_Ranger/Acc_Pauldron
- **Kit:** custom: shield cut from a windmill sail: a tall lattice frame with canvas, 1.5 m long; custom: flour-sack mallet (weapon: flour-sack mallet)
- **Signature:** a windmill-sail shield whose canvas snaps in a wind only he feels
- **Pose and effects:** sail shield planted diagonally, mallet low; green wind gusting through the lattice; flour puffs
- **Story:** When the Hollow burned his mill on the Thornwick hills he walked away with one sail on his back. Nothing has got past it since.
- **3D build:** Knight cloth body in sailcloth cream over peasant arms and legs. The sail is a bevelled lattice frame with a canvas panel that flutters through a vertex-colour wind mask.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Sail Bash | one enemy | 0.9 x DEF | basic | Slow (2t, 30% chance) |
| Millwall | self | - | 4 | Shield (30%), Taunt (2t) |
| Turning Sails | all allies | - | 5 | DefUp (2t), Turn Meter + (5% meter) |

### Corran Highstep, the Goat-Folk Ridge-Runner

**Gale · Attack · Rare** · male goat-folk · act 1 · id `corran`

A lean goat-folk runner with thick curled horns, a white mohawk and a beard tuft, in pine-green leathers with a climbing rope across his chest. He carries a long iron-shod quarterstaff wrapped in streaming cloth strips. *Silhouette:* a tall lean figure with curled horns and a long horizontal staff trailing strips: a cross shape.

- **Palette:** main `#3a5a3a`, secondary `#a87a4a`, accent `#e8e0c8`, metal `#5a5a5a`, glow `#5ad08a`
- **Outfit:** custom: thick back-curled ram horns; Male_Ranger/Body; Male_Ranger/Arms; Male_Ranger/Legs; custom: digitigrade cloven-hoof covers; Male_Knight/Acc_Scarf (long, green); custom: climbing rope coiled across the chest; custom: goat ears
- **Kit:** custom: 2.2 m quarterstaff, iron-shod at both ends, wrapped in streaming cloth strips (weapon: quarterstaff)
- **Signature:** a quarterstaff wrapped in streamers that crack like flags when he spins it
- **Pose and effects:** staff held level across his hips, knees bent on hooves; green wind spirals when he spins
- **Story:** Goat-folk messenger of the Stormcrown passes who crosses ridges no human would try. He vaults over Hollow lines the same way.
- **3D build:** Regular male with custom horns and ears on the head bone, hoof covers over the feet (keep the human foot bones). Ranger set in pine green. The staff streamers are alpha cards on 2 helper bones per end.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Staff Sweep | one enemy | 0.5 x ATK x2 hits | basic | - |
| Ridge Vault | one enemy | 1.8 x ATK | 3 | SpdUp (2t, on self) |
| Gale Spin | all enemies | 0.8 x ATK | 4 | Slow (2t, 40% chance) |

### Linnet Reedpipe, the Elven Piper

**Gale · Support · Rare** · female elf · act 1 · id `linnet`

A slim elf in straw-gold and slate with long pointed ears and an auburn side braid, her hood pushed back. She plays wide reed panpipes bound in red cord, with red ribbons streaming from her wrists. *Silhouette:* a slender figure with panpipes at her lips, a stepped fan shape at her face, ribbons trailing.

- **Palette:** main `#c8a860`, secondary `#5a6a70`, accent `#e86a6a`, metal `#8a7a5a`, glow `#5ad08a`
- **Outfit:** Female_Ranger/Head_Hood (straw gold, pushed back); Female_Ranger/Body; Female_Ranger/Arms; Female_Ranger/Legs; Female_Ranger/Feet; custom: long pointed elf ears; custom: red wind ribbons trailing from her wrists
- **Kit:** custom: wide panpipes of eight reeds bound in red cord (weapon: panpipes)
- **Signature:** eight-reed panpipes whose notes become visible green ribbons
- **Pose and effects:** pipes at her lips, one knee lifted mid-dance; ribbon-shaped notes float to her allies
- **Story:** An elf of the Saltmarsh reed beds, where the wind plays the reeds by itself. She learned her tunes there, and they carry tired feet a little farther.
- **3D build:** Regular female, slim (bulk 0.86), with custom elf ears. The Ranger hood is pushed back and recoloured straw gold (not green, and she carries no bow). The ribbons are alpha strips on 2 bones per wrist.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Piping Note | one enemy | 1.0 x ATK | basic | TM drain (10% meter, 40% chance) |
| Wayfarer's Air | all allies | - | 4 | Heal (15%), SpdUp (2t) |
| Swallow Song | all allies | - | 5 | Cleanse, Turn Meter + (15% meter) |

### Ysabeau Galecutter, Sky-Raider of the Storm Coast

**Gale · Attack · Epic** · female human · act 2 · id `ysabeau`

A lean sky-raider in a sea-green oilskin coat, flight cap and brass goggles, with a hooked cutlass. On her back is a bamboo-and-sailcloth glider that snaps open into a wing twice her width. *Silhouette:* a wide triangular glider wing behind a lean figure, a curved cutlass in front.

- **Palette:** main `#2f6a4a`, secondary `#6a4a30`, accent `#e8c060`, metal `#b08a4a`, glow `#5ae08a`
- **Outfit:** custom: leather flight cap with brass goggles; custom: fitted oilskin flying coat with buckles; Female_Ranger/Arms; Female_Ranger/Legs; Female_Ranger/Feet; custom: back-mounted glider of bamboo ribs and sea-green sailcloth; custom: sash
- **Kit:** custom: folding glider frame on her back that opens into a wing twice her width; custom: hooked cutlass (weapon: hooked cutlass)
- **Signature:** a folding sailcloth glider that snaps open into a wing twice her width
- **Pose and effects:** glider half-open, cutlass forward, leaning into the wind; green streamers from the glider ribs; her dive leaves a tunnel of wind
- **Story:** She rode the storm from the coast cliffs into Saltreach's rigging and cut a Hollow ship loose before it docked, by the light of the Gale shard's lightning.
- **3D build:** The glider is a rigid frame on Socket_Back with 2 hinge bones (folded in idle, open on skills). The sailcloth is single-sided with an alpha edge. Blade archetype clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Cutlass Gust | one enemy | 1.1 x ATK | basic | Slow (2t, 30% chance) |
| Dive from Above | one enemy | 2.2 x ATK | 3 | Pierce |
| Squall Raid | all enemies | 1.0 x ATK | 4 | DefDown (2t, 40% chance) |

### Wendel Hivewarden, the Beekeeper of Bellspire

**Gale · HP · Epic** · male human · act 1 · id `wendel`

A round, bearded beekeeper in olive and honey-gold, with a wide hat and a pale green veil. A woven straw beehive rides on his back, circled by glowing green bees, and he holds up a brass smoker. *Silhouette:* a round body with a dome hive behind his head and a wide hat brim: stacked circles.

- **Palette:** main `#d8b060`, secondary `#4a5a3a`, accent `#f0f0e0`, metal `#b08a3a`, glow `#7ae08a`
- **Outfit:** custom: wide-brimmed beekeeper hat with a pale green mesh veil; Male_Peasant/Body; Male_Peasant/Arms; Male_Peasant/Legs; Male_Ranger/Feet_Boots; custom: woven straw skep hive on his back; custom: brass bee smoker on his belt; custom: short honey-gold mantle
- **Kit:** custom: skep hive on his back, circled by glowing green bees; custom: bee smoker (weapon: bee smoker)
- **Signature:** a living straw hive on his back, ringed by glowing green bees
- **Pose and effects:** smoker raised, veil down; bee-swarm particles orbit him; the swarm thickens into a shield when he taunts
- **Story:** Bellspire's monks kept bees in the ruins long before the Hollow. Wendel kept the hives alive through the dark, and now the bees guard him.
- **3D build:** Peasant set in olive and honey. The veil is an alpha-clip cylinder under the hat brim (keep the face readable through it at LOD0). The hive is rigid on Socket_Back; bees are a VFX particle orbit.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Smoker Puff | one enemy | 0.085 x HP | basic | AtkDown (2t, 30% chance) |
| Royal Jelly | all allies | - | 4 | Regen (2t), Heal (10%) |
| Swarm Wall | self | - | 5 | Shield (25%), Taunt (2t), Counter (2t) |

### Hilde Windrose, Keeper of the Coast Road

**Gale · Defense · Epic** · female human · act 2 · id `hilde`

A broad, copper-braided guard in a slate quilted gambeson and steel cap under a grey storm cape. She plants a tall pavise painted with an ivory wind-rose whose needle spins toward danger. *Silhouette:* a tall rectangular shield with a compass star and a heavy cape.

- **Palette:** main `#5a6a70`, secondary `#3a5a40`, accent `#e8e0c8`, metal `#9aa4ac`, glow `#5ad08a`
- **Outfit:** custom: open steel cap with a nasal guard; custom: quilted gambeson with steel plates; custom: quilted leather sleeves with steel bands; Female_Ranger/Legs; Female_Ranger/Feet; Female_Ranger/Acc_Pauldrons; custom: hooded slate-grey oilskin storm cape
- **Kit:** custom: tall pavise shield painted with an ivory wind-rose, spiked foot for planting; custom: short broad sword (weapon: short broad sword)
- **Signature:** a planted pavise painted with a wind-rose whose needle swings toward danger
- **Pose and effects:** pavise planted, cape blowing, sword held low; the wind-rose glows green and spins when she taunts
- **Story:** She guards the coast road where the storms land first. The needle on her shield has never pointed the wrong way.
- **3D build:** Custom quilted body pieces over the Female Ranger legs. The pavise is 1.3 x 0.6 m; the wind-rose needle is a separate rigid part that spins (1 helper bone). ShieldBlade clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Rose Bash | one enemy | 0.95 x DEF | basic | Slow (2t, 30% chance) |
| Plant the Pavise | self | - | 4 | Shield (30%), Taunt (2t), DefUp (2t) |
| True North | all allies | - | 5 | Shield (10%), Cleanse |

### Aveline Skyloom, Sylph of the Four Winds

**Gale · Support · Legendary** · female sylph (wind fae) · act 2 · id `aveline`

A tall, pale-mint wind-spirit in layered sky-silk, with fae ears and silver-white hair that floats upward in braided strands. She hovers just off the ground, a circular loom strung with glowing green threads floating behind her, and weaves with a silver shuttle. *Silhouette:* a tall, slender vertical figure inside a large ring, threads and hair rising upward.

- **Palette:** main `#e8eee0`, secondary `#3a7a5a`, accent `#d8b860`, metal `#c8b070`, glow `#5aff9a`
- **Outfit:** custom: silver thread circlet; custom: layered sky-silk gown woven with wind patterns; custom: long gauzy silk sleeves; custom: soft pointed slippers, toes off the ground; custom: circular loom ring floating behind her head and shoulders, strung with glowing green threads (breaker); custom: woven shawl tails that float like banners; custom: delicate swept-back fae ears
- **Kit:** custom: floating loom ring; custom: silver weaving shuttle held like a wand (weapon: weaving shuttle (focus))
- **Signature:** a ring-shaped loom floating behind her, weaving the wind into green threads
- **Pose and effects:** hovering a hand's width off the ground, shuttle raised, threads streaming from the ring to her fingers; animated glowing threads and hair, a wind aura, a swirl of wind where her feet should touch the ground
- **Story:** A wind-spirit of the Storm Coast whose sisters once wove the weather. She alone still holds a loom, and with it she can stitch a broken line of heroes back together.
- **3D build:** Regular female with a pale mint skin tone and fae ears; humanoid skeleton kept, hips offset up 0.1 m so she hovers. The gown hides her lower legs and ends in an alpha wind swirl. The loom ring is rigid on Socket_Back with animated thread UVs. Legendary: animated hair glow and a wind aura.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Shuttle Strike | one enemy | 1.0 x ATK | basic | Slow (2t, 40% chance) |
| Woven Gale | all allies | - | 4 | Heal (30%), SpdUp (2t), Cleanse |
| Four Winds | all allies | - | 5 | Turn Meter + (30% meter), AtkUp (2t), DefUp (2t) |


## Tide

### Jory Saltline, Harpooner of Saltreach

**Tide · Attack · Common** · male human · act 2 · id `jory`

A bearded whaler in a navy jersey and knit cap with a coil of rope at his hip. He raises a long barbed harpoon tied to the rope. *Silhouette:* a long diagonal harpoon with a loop of rope at his hip.

- **Palette:** main `#2a5a7a`, secondary `#d8d0b8`, accent `#c8642a`, metal `#6a7a80`, glow `#5ab0ff`
- **Outfit:** custom: navy knit fisherman's cap; Male_Peasant/Body; Male_Peasant/Arms; Male_Peasant/Legs; Male_Ranger/Feet_Boots; custom: coiled rope at his hip
- **Kit:** custom: long whaling harpoon with a barbed head and rope line (weapon: harpoon)
- **Signature:** a barbed harpoon tied to a coil of rope he reels back after every throw
- **Pose and effects:** harpoon raised overhead, rope taut; water splash and a rope-line trail
- **Story:** Saltreach's best whaler stayed ashore when the Hollow filled the docks. He throws at them now, and reels them in.
- **3D build:** Peasant set as a navy jersey and cream oilskin trousers. Polearm clips. The rope coil is rigid on Socket_Hip; on Reel In a VFX line connects the target to the hand.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Harpoon Jab | one enemy | 1.0 x ATK | basic | - |
| Reel In | one enemy | 1.3 x ATK | 3 | Pull (15% meter) |

### Morag Kelpwright, the Tide-Gatherer

**Tide · Support · Common** · female human · act 2 · id `morag`

A freckled, red-haired gatherer in teal with a sea-green headscarf. She carries a long kelp rake and a wicker basket on her back overflowing with glowing kelp. *Silhouette:* a basket hump with drooping kelp strands and a long rake.

- **Palette:** main `#3a7a70`, secondary `#c8b080`, accent `#f0e8d8`, metal `#7a6a5a`, glow `#5ab0ff`
- **Outfit:** custom: knotted sea-green headscarf; Female_Peasant/Body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Peasant/Feet; custom: big wicker basket on her back spilling glowing kelp
- **Kit:** custom: wicker back-basket spilling glowing kelp; custom: long-tined kelp rake (weapon: kelp rake)
- **Signature:** a basket of glowing kelp that she wraps around wounded friends
- **Pose and effects:** rake on her shoulder, kelp dangling; glowing kelp ribbons wrap allies when she heals
- **Story:** She works the Black Fens' tidal edges, where the kelp grows thick and glows at night, and brews it into poultices that close any cut.
- **3D build:** Peasant set in teal. The basket is rigid on Socket_Back; kelp strands are alpha cards on 2 bones with a glow mask on their tips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Rake | one enemy | 1.0 x ATK | basic | Slow (2t, 20% chance) |
| Kelp Poultice | all allies | - | 4 | Heal (10%), Regen (2t) |

### Wenna Shellback, the Turtle-Folk Guard

**Tide · Defense · Rare** · female turtle-folk · act 2 · id `wenna`

A short, stout turtle-folk with sea-green scaled skin, a beaked face and a cream belly-plate under a scale vest. A huge sea-turtle carapace covers her back and curls over her shoulders, and she carries a coral-tipped club. *Silhouette:* a low, wide dome on her back: she reads as a turtle at a glance.

- **Palette:** main `#3a6a5a`, secondary `#c8b890`, accent `#e8805a`, metal `#6a7a7a`, glow `#5ab0ff`
- **Outfit:** custom: turtle head (rounded, beaked, small shell cap); custom: scale-mail vest over a cream plastron (belly shell); custom: scaled forearms with shell bracers; Female_Ranger/Legs; custom: stumpy scaled foot covers; custom: huge sea-turtle carapace on her back, curving over the shoulders
- **Kit:** custom: her own great carapace (back shield); custom: coral-tipped short club (weapon: coral-tipped club)
- **Signature:** a sea-turtle carapace she can crouch into like a fortress
- **Pose and effects:** half-crouched, carapace rolled forward over her shoulders; a water ripple across the shell when she shields
- **Story:** The turtle-folk keeper of Saltreach's tide pools, more than a hundred years old. Children have hidden behind her shell for three generations.
- **3D build:** Regular female, short and wide, with a custom turtle head on the head bone and a rigid carapace on the spine (bound to Spine2 so it follows the hunch). Plastron and scales are painted on a custom vest. Coral club with coral-pink accents.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Shell Butt | one enemy | 0.9 x DEF | basic | Slow (2t, 25% chance) |
| Tuck In | self | - | 4 | Shield (32%), Taunt (2t) |
| Tidepool Guard | all allies | - | 5 | DefUp (2t), Shield (6%) |

### Lazlo Swordbill, the Shark-Folk Duellist

**Tide · Attack · Rare** · male shark-folk · act 2 · id `lazlo`

A sleek, blue-grey shark-folk duellist with a tall dorsal fin, a toothy grin and a high teal fin-collar on his deep-blue coat. He fences with a needle-thin rapier made from a swordfish bill. *Silhouette:* a thin elegant figure with a fin crest and a very long needle blade held forward.

- **Palette:** main `#1e5a7a`, secondary `#e8e0d0`, accent `#4ae0d0`, metal `#a8b8c0`, glow `#5ab0ff`
- **Outfit:** custom: shark head with a dorsal fin crest; Male_Noble/Body; Male_Noble/Arms; Male_Noble/Legs; Male_Noble/Feet; custom: high fin-shaped collar in teal; custom: short tail fin at the back of the belt
- **Kit:** custom: rapier made from a swordfish bill with a scallop-shell guard (weapon: swordfish-bill rapier)
- **Signature:** a rapier made from a swordfish's bill with a scallop-shell guard
- **Pose and effects:** en garde, free hand behind his back; a streak of water jet on each thrust
- **Story:** A shark-folk dandy of the Saltreach docks who once duelled a swordfish for three days and kept its bill. He fights the Hollow with the same patience.
- **3D build:** Regular male with a custom shark head (rigid jaw, no extra bones) and a fin crest. Noble set recoloured as a deep-blue duelling coat. The tail fin is rigid on the hips. Blade clips with a thrust override.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Bill Thrust | one enemy | 1.0 x ATK | basic | Pierce |
| Lunge | one enemy | 1.9 x ATK | 3 | DefDown (2t, 50% chance) |
| Shoal Rush | all enemies | 0.85 x ATK | 4 | Slow (2t, 40% chance) |

### Mungo Barrelhand, the Dock Giant

**Tide · HP · Rare** · male human · act 2 · id `mungo`

A huge, dark-skinned docker with a shaved head, beard and gold earring, in navy trousers and a rope harness. He carries a sloshing iron-hooped barrel on one shoulder and swings it like a hammer. *Silhouette:* a huge round man with a barrel on his shoulder: a block on a mountain.

- **Palette:** main `#6a4a30`, secondary `#2a4a6a`, accent `#e8c060`, metal `#5a5a5a`, glow `#5ab0ff`
- **Outfit:** Male_Peasant/Body; Male_Peasant/Arms; Male_Peasant/Legs; Male_Peasant/Feet; custom: rope harness across the chest; custom: gold hoop earring; custom: iron cargo hook on his belt
- **Kit:** custom: iron-hooped brine barrel carried on one shoulder, sloshing (weapon: brine barrel)
- **Signature:** a sloshing brine barrel he swings like a hammer
- **Pose and effects:** barrel on his shoulder, other fist on his hip; brine splashes; a ring of water when he shields
- **Story:** Saltreach's strongest docker can unload a ship alone. When the Hollow took the docks he simply kept unloading, onto them.
- **3D build:** Peasant set as navy trousers and an open oak-brown vest. The barrel (0.5 m tall) sits on the right shoulder in idle and swings as a two-handed hammer in attacks (Heavy clips).

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Barrel Roll | one enemy | 0.08 x HP | basic | Stun (1t, 15% chance) |
| Salted Rations | all allies | - | 4 | Regen (2t) |
| Bulkhead | self | - | 5 | Shield (25%), Taunt (2t) |

### Neve Conchcaller, the Tidekin Singer

**Tide · Support · Rare** · female tidekin (merfolk) · act 2 · id `neve`

A pale-aqua tidekin with fin-shaped ears, sea-foam hair and a teal shawl fringed with shells. She holds a great spiral conch to her lips with webbed hands. *Silhouette:* a slim figure with a big spiral cone at her mouth and fin ears.

- **Palette:** main `#2a7a8a`, secondary `#f0e8dc`, accent `#f08a7a`, metal `#c8b8a0`, glow `#6ab8ff`
- **Outfit:** Female_Ranger/Body; custom: webbed hands and scaled forearms; Female_Ranger/Legs; custom: finned ankle covers; custom: teal shawl with a shell-bead fringe; custom: fin ears
- **Kit:** custom: great spiral conch horn held to her lips with both hands (weapon: conch horn)
- **Signature:** a spiral conch horn whose call makes the water rise
- **Pose and effects:** conch raised, blowing; blue rings of sound rippling outward
- **Story:** A tidekin girl from the flooded halls beneath Wraithmoor. When the Tide shard began to sing under the stones she answered with her grandmother's conch, and the restless dead fell quiet to listen.
- **3D build:** Regular female with pale aqua skin, custom fin ears and fin-shaped hair cards. She walks on legs, so the skeleton and clips are shared; ankle fins are rigid on the feet. The conch is a two-handed prop.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Conch Blast | one enemy | 1.0 x ATK | basic | Slow (2t, 30% chance) |
| Calling Tide | all allies | - | 4 | Heal (18%) |
| Low Tide | all allies | - | 5 | Cleanse, Turn Meter + (15% meter) |

### Captain Hendrick Prowe, Captain of the Last Galley

**Tide · Defense · Epic** · male human · act 2 · id `hendrick`

A grey-whiskered sea captain in navy plate and a long brass-buttoned coat, with a plumed bicorne. His shield is his ship's carved wooden figurehead, banded with brass, its painted eyes glowing sea-blue, and he carries a boarding axe. *Silhouette:* a tall, irregular carved shield, a bicorne hat and long coat tails.

- **Palette:** main `#1e3a5a`, secondary `#8a6a40`, accent `#e8c050`, metal `#b0904a`, glow `#5ab0ff`
- **Outfit:** custom: bicorne hat with a teal plume; Male_Knight/Body_Armor; Male_Knight/Arms; Male_Knight/Legs; Male_Ranger/Feet_Boots; Male_Noble/Acc_Pauldron; custom: long navy captain's coat tails with brass buttons
- **Kit:** custom: shield carved from a ship's figurehead (a carved sea-maiden), brass-banded; custom: boarding axe (weapon: boarding axe)
- **Signature:** his ship's carved figurehead, now his shield, whose painted eyes glow sea-blue
- **Pose and effects:** figurehead shield forward, axe held low; spray along the shield edge on blocks; a ghostly ship's wake when he taunts
- **Story:** His galley sank off the Storm Coast with the Hollow aboard. He swam ashore with the figurehead and swears she still steers him true.
- **3D build:** Knight armour recoloured navy with brass trim. The coat tails use 4 secondary bones. The figurehead shield is 1.2 m of carved wood (keep the carved face simple and stylised); its eyes are on the glow mask.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Prow Ram | one enemy | 0.95 x DEF | basic | Slow (2t, 30% chance) |
| Hold Fast | all allies | - | 4 | Shield (6%), Taunt (2t, on self) |
| Broadside | all enemies | 0.8 x DEF | 5 | DefDown (2t, 50% chance) |

### Kaija Floebreaker, Ice-Cutter of the Drowned Mill

**Tide · Attack · Epic** · female human · act 1 · id `kaija`

A sturdy ice-cutter in a white fur-trimmed parka and frost-blue cape, her hood ringed with fur. She hauls a jagged two-handed ice saw longer than she is tall. *Silhouette:* a fur halo of parka hood and a long jagged horizontal saw.

- **Palette:** main `#e8eef4`, secondary `#2a4a6a`, accent `#7ad8ff`, metal `#9ab0c0`, glow `#6ac0ff`
- **Outfit:** Female_Ranger/Head_Hood with a custom fur trim; custom: fur-trimmed white parka; Female_Ranger/Arms; Female_Ranger/Legs; Female_Ranger/Feet; Female_Ranger/Acc_Pauldrons; custom: short frost-blue cape
- **Kit:** custom: two-handed ice saw, 1.8 m, jagged teeth rimmed with frost (weapon: ice saw)
- **Signature:** a two-handed ice saw longer than she is tall
- **Pose and effects:** saw held low and level, ready to rip upward; frost shards spray; ice-crack decals on targets
- **Story:** When the Drowned Mill's pond froze over the Hollow beneath it, she cut them out one block at a time.
- **3D build:** Custom parka over Ranger arms and legs; the fur hood trim is alpha cards. The saw is a rigid two-handed prop with frost teeth on the glow mask. Polearm clips work for the sweeps.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Saw Bite | one enemy | 0.55 x ATK x2 hits | basic | Slow (2t, 30% chance) |
| Floe Split | one enemy | 2.3 x ATK | 3 | TM drain (25% meter) |
| Shatter | all enemies | 1.1 x ATK | 4 | Stun (1t, 20% chance) |

### Morvoren, the Singing Deep

**Tide · HP · Legendary** · female barrow wraith (drowned spirit) · act 2 · id `morvoren`

A tall, pale sea-blue spirit in layered deep-teal silk, her dark hair drifting as if underwater. A living wave curls over her head like a hood, a ring of water carrying seven glowing barrow stones circles her waist, and below the knees she fades into a column of water. *Silhouette:* a tall bell-shaped gown fading into water, a curling wave over her head and a ring of water at her waist.

- **Palette:** main `#1a4a6a`, secondary `#2a8a8a`, accent `#f0e8d8`, metal `#c8c0b0`, glow `#5ac8ff`
- **Outfit:** custom: standing wave that curls over her head like a hood (breaker); custom: flowing gown of layered deep-teal silk ending in wave scallops; custom: shell-plated gauntlets; custom: ghostly lower body that fades into a column of water; custom: ring of water circling her waist carrying seven small barrow stones; custom: kelp-veil train
- **Kit:** custom: standing-stone staff carved with tide runes; custom: water ring with seven floating barrow stones; custom: curling wave hood (weapon: standing-stone staff)
- **Signature:** a living wave that curls over her head, and a slow ring of water carrying barrow stones around her waist
- **Pose and effects:** staff planted, free hand palm-down, calming the water; animated water ring and wave hood, glowing runes on the stones, a blue aura; translucent fade below the knees
- **Story:** She drowned in the Barrows' flooded halls a hundred years ago, and the Tide shard's song brought her back. She guards the living now as she once guarded the dead kings.
- **3D build:** Regular female skeleton with pale sea-blue skin. Below the knees the mesh fades out with alpha clip and a water-column VFX; the leg bones still exist so clips play, but foot contact is hidden. The wave hood and water ring are animated meshes on Socket_Head and the hips. This is a humanoid, not a custom body.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Undertow Grip | one enemy | 0.09 x HP | basic | TM drain (15% meter) |
| Barrow Hymn | all allies | - | 4 | Shield (12%), Regen (2t), Taunt (2t, on self) |
| Grave Tide | all enemies | 0.07 x HP | 5 | Slow (2t, 50% chance), DefDown (2t, 40% chance) |

### Leofric Tidewarden, the Breakwater of Saltreach

**Tide · Defense · Legendary** · male human · act 2 · id `leofric`

A towering knight in sea-steel plate with teal enamel, a visored helm crowned by a crest of frozen spray, and a cape that ends in a breaking wave. He carries a huge whale-tail shield of whalebone and steel. *Silhouette:* a wide twin-lobed shield like a whale's tail, a crest of frozen spray over the helm and a wave-hemmed cape.

- **Palette:** main `#2a4a6a`, secondary `#e8e8e0`, accent `#4ae0e0`, metal `#7a90a0`, glow `#5ab8ff`
- **Outfit:** Male_Knight/Head_Armet with a custom crest of frozen spray rising from it (breaker); Male_Knight/Body_Armor; Male_Knight/Arms; Male_Knight/Legs; Male_Knight/Feet; Male_Knight/Acc_Pauldron_Round; custom: long cape whose hem is a breaking wave
- **Kit:** custom: whale-fluke shield, twin-lobed, of carved whalebone and sea-steel; custom: short boarding spear (weapon: boarding spear)
- **Signature:** a shield shaped like a whale's tail that slams the ground like a breaching whale
- **Pose and effects:** fluke shield raised high, about to slam down; animated foam along the cape, a glowing spray crest, a water aura
- **Story:** Saltreach's sea-wall bears his family's name. When it cracked he took its place, and the Hollow tide has broken on him ever since.
- **3D build:** Knight armour recoloured sea-steel with teal enamel. Legendary signature material: sea-steel with animated foam. The fluke shield is 1.1 m wide; the spray crest is a translucent alpha-clip mesh with a glow. The cape uses 4 secondary bones.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Fluke Slam | one enemy | 1.0 x DEF | basic | Slow (2t, 40% chance) |
| Sea Wall | all allies | - | 4 | Shield (15%), DefUp (2t), Taunt (2t, on self) |
| Breach | all enemies | 0.9 x DEF | 5 | Stun (1t, 30% chance) |


## Light

### Hettie Sunfield, the Harvest Girl

**Light · Attack · Common** · female human · act 1 · id `hettie`

A sun-browned farm girl in a wheat-gold dress with a brown bodice and a gold kerchief. She charges with a three-tined pitchfork tied with a glowing gold ribbon. *Silhouette:* a small figure with a long upright pitchfork, its tines like a tiny crown above her.

- **Palette:** main `#e8d090`, secondary `#8a5a3a`, accent `#5a8ac8`, metal `#8a8a7a`, glow `#ffe070`
- **Outfit:** custom: gold kerchief tied over her hair; Female_Peasant/Body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Peasant/Feet
- **Kit:** custom: three-tined pitchfork with a gold ribbon tied below the tines (weapon: pitchfork)
- **Signature:** a pitchfork with a gold harvest ribbon that glows like sunrise
- **Pose and effects:** fork levelled forward, mid-charge; a gold glint along the tines; ribbon trail
- **Story:** Thornwick's harvest girl chased the Hollow out of the wheat with a pitchfork, and she hasn't stopped chasing.
- **3D build:** Peasant set in wheat gold and brown with a cornflower-blue accent. Polearm clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Fork Jab | one enemy | 1.0 x ATK | basic | - |
| Harvest Thrust | one enemy | 1.5 x ATK | 3 | AtkDown (2t, 30% chance) |

### Abel Millstone, the Grain-Bearer

**Light · HP · Common** · male human · act 1 · id `abel`

A broad, flour-dusted miller in a white apron with a gold sash. A carved millstone with a glowing sun groove rides on his back, and he carries a short wooden grain shovel. *Silhouette:* a round man with a big stone disc behind him like a shell.

- **Palette:** main `#f0e8d0`, secondary `#8a6a4a`, accent `#e8c060`, metal `#8a8478`, glow `#ffe070`
- **Outfit:** Male_Peasant/Body; Male_Peasant/Arms; Male_Peasant/Legs; Male_Peasant/Feet; custom: white flour apron; custom: gold sash
- **Kit:** custom: carved millstone slung on his back by a leather strap; custom: short wooden grain shovel (weapon: grain shovel)
- **Signature:** a carved millstone on his back whose sun-shaped groove glows gold
- **Pose and effects:** thumbs hooked in his apron straps, millstone behind; flour puffs and golden grain motes
- **Story:** The Bellspire miller ground flour for the ruined town's last bakery. The old sun is carved into his millstone, and he won't leave it behind.
- **3D build:** Peasant set with a custom apron. The millstone (0.8 m disc) is rigid on Socket_Back with the sun groove on the glow mask.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Grindstone | one enemy | 0.08 x HP | basic | - |
| Daily Bread | all allies | - | 4 | Regen (2t), Taunt (2t, on self) |

### Poppy Dawnseed, the Petal Sprite

**Light · Support · Common** · female petal sprite (fae) · act 2 · id `poppy`

A tiny golden-skinned sprite with hair like sunflower petals, a leaf-green tunic and two small petal wings. She holds up a sunflower staff far taller than she is, its face glowing and turning toward her friends. *Silhouette:* a tiny figure with a petal halo of hair and an enormous flower disc above her.

- **Palette:** main `#e0b030`, secondary `#4a6a3a`, accent `#6a3a1a`, metal `#8a7a5a`, glow `#ffe070`
- **Outfit:** custom: leaf-green tunic with petal skirt; Female_Peasant/Arms; Female_Peasant/Legs; custom: seed-pod slippers; custom: two small translucent petal wings; custom: short antennae-like stamen curls
- **Kit:** custom: giant sunflower staff taller than her, its face glowing (weapon: sunflower staff)
- **Signature:** a sunflower staff whose glowing face turns to follow her allies
- **Pose and effects:** staff held up, flower facing an ally; petal motes and a golden glow on heals
- **Story:** A sprite who woke inside a sunflower on the Ember Groves' ashes when a spark of the falling Light shard landed in it. She has been planting hope ever since.
- **3D build:** Regular female at 0.85 height with a 1.1 head. Petal hair as rigid cards; small petal wings are alpha cards on Socket_Back with 1 flutter bone each. The sunflower head is 0.45 m and turns on a helper bone.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Petal Toss | one enemy | 1.0 x ATK | basic | - |
| Sunbloom | all allies | - | 4 | Heal (12%), DefUp (2t) |

### Benedict, the Sundial Golem

**Light · Defense · Rare** · male sandstone golem (construct) · act 1 · id `benedict`

A blocky sandstone golem carved with hour lines, with bronze bands, a bronze fin on his head and glowing eye slits. He holds a half-disc sundial shield whose bronze fin casts a moving line of gold light. *Silhouette:* a blocky stone figure behind a half-moon shield with its flat side up and a triangular fin standing on it.

- **Palette:** main `#e8e0c8`, secondary `#6a5a40`, accent `#d8a83a`, metal `#b89a50`, glow `#ffe070`
- **Outfit:** custom: bronze gnomon fin on the crown of his head; custom: sandstone torso carved with hour lines; custom: sandstone arm blocks with bronze bands; custom: stone leg blocks; custom: square stone feet; Male_Knight/Acc_Pauldron_Round (bronze); custom: ivory cloth tabard
- **Kit:** custom: half-disc sundial shield with a raised bronze gnomon fin; custom: short bronze mace (weapon: bronze mace)
- **Signature:** a sundial shield whose gnomon casts a glowing line of light that marks the hour
- **Pose and effects:** shield level, gnomon up, mace resting on the rim; a gold line sweeps across the shield face; hour lines glow on his body when he shields
- **Story:** Bellspire's old sundial, carved by the bell-makers, stood up when the bells fell silent. It still keeps the hours, and it keeps its friends alive until noon.
- **3D build:** Regular male skeleton under rigid sandstone blocks, one per bone. No face beyond carved brows and two glowing eye slits. The bronze gnomon on his head echoes the shield. ShieldBlade clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Dial Bash | one enemy | 0.9 x DEF | basic | AtkDown (2t, 30% chance) |
| Noon Ward | self | - | 4 | Shield (30%), Taunt (2t), Cleanse |
| High Sun | all allies | - | 5 | DefUp (2t) |

### Emeric Larkspur, the Javelineer

**Light · Attack · Rare** · male human · act 2 · id `emeric`

A lean, dark-skinned thrower in ivory and sky-blue leathers. Five gilded javelins fan out behind his shoulders like sunrays, and he cocks a sixth to throw. *Silhouette:* a fan of shafts spreading behind his shoulders like sunrays, one javelin cocked.

- **Palette:** main `#f0e8d0`, secondary `#4a6a9a`, accent `#e8b83a`, metal `#c8a050`, glow `#ffe070`
- **Outfit:** Male_Ranger/Body; Male_Ranger/Arms; Male_Ranger/Legs; Male_Ranger/Feet_Boots; Male_Ranger/Acc_Pauldron; custom: back quiver fanning five gilded javelins
- **Kit:** custom: fan of five gilded javelins on his back; custom: one javelin in hand (weapon: javelins)
- **Signature:** a sunray fan of five gold javelins on his back
- **Pose and effects:** javelin cocked at his shoulder, other arm aimed forward; gold streaks; spent javelins re-form on his back as light
- **Story:** A runner of the Ember Groves' fire-watch, he lights signal javelins and throws them at whatever the fire reveals.
- **3D build:** Ranger set recoloured ivory and sky blue (not green). The javelin fan is rigid on Socket_Back; the thrown javelin is a prop on Socket_HandR that hides on release.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Javelin | one enemy | 1.0 x ATK | basic | Pierce |
| Volley | all enemies | 0.8 x ATK | 3 | AtkDown (2t, 40% chance) |
| Sunlance Throw | one enemy | 2.0 x ATK | 4 | Strip |

### Mother Agathe, Bearer of the Reliquary

**Light · HP · Rare** · female human · act 1 · id `agathe`

A stout, kindly abbess in an ivory habit and gold-banded wimple. A small golden shrine rides on her back like a pack, and light pours from its open doors. *Silhouette:* a round habit with a tall, boxy spired shrine rising behind her head.

- **Palette:** main `#f0e8d8`, secondary `#5a6a8a`, accent `#d8a83a`, metal `#c8a050`, glow `#ffe070`
- **Outfit:** custom: ivory wimple with a gold band; custom: ivory habit over the Female Peasant body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Peasant/Feet; custom: golden reliquary shrine on her back with little doors that shine; custom: prayer cord
- **Kit:** custom: reliquary shrine worn like a backpack; custom: short gold-capped walking staff (weapon: walking staff)
- **Signature:** a little golden shrine on her back whose doors open to pour out light
- **Pose and effects:** staff planted, shrine doors open; beams of light from the shrine doors
- **Story:** The Sunken Cathedral's sisters carried their relics out before the water rose. Agathe carries them still, and the light inside has not dimmed.
- **3D build:** Custom habit over the peasant body. The shrine is rigid on Socket_Back with 2 hinge bones for the doors and a glow mask inside.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Reliquary Bash | one enemy | 0.08 x HP | basic | Stun (1t, 15% chance) |
| Relic Glow | all allies | - | 4 | Regen (2t) |
| Sanctified | self | - | 5 | Shield (25%), Taunt (2t), Cleanse |

### Ottavia Sunhalberd, Elven Dawnguard

**Light · Attack · Epic** · female elf · act 2 · id `ottavia`

A tall, dark-skinned elf in ivory and gold armour with a short sky-blue cape and a bascinet crowned by a white horsehair plume. She wields a halberd whose blade is shaped like a rising sun. *Silhouette:* a tall plume and a tall halberd: two verticals with a crescent blade at the top.

- **Palette:** main `#f0e8d0`, secondary `#3a5a9a`, accent `#e8b83a`, metal `#d8b860`, glow `#ffe070`
- **Outfit:** custom: open bascinet with a tall white horsehair plume; custom: gold-trimmed brigandine over the Female Ranger body; custom: gilded plate vambraces; Female_Ranger/Legs; custom: plate sabatons; custom: long pointed elf ears through slots in the helm; custom: asymmetric short sky-blue cape
- **Kit:** custom: halberd whose axe blade is a gold half-sunrise (weapon: halberd)
- **Signature:** a halberd with a sunrise-shaped blade that leaves a dawn-coloured arc
- **Pose and effects:** halberd held diagonally, blade high, plume streaming; a gold arc on each sweep; a sunburst flash when Sunrise Verdict finishes a foe
- **Story:** An elf of the Ember Groves' dawn watch who has stood guard over the trees for three hundred years. Since the Light shard fell among them, her halberd has caught the sunrise.
- **3D build:** Regular female, tall and slim, with custom elf ears. Custom gilded armour pieces over the Ranger legs. The plume is alpha cards on 2 bones. Polearm clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Halberd Cleave | one enemy | 1.1 x ATK | basic | AtkDown (2t, 30% chance) |
| Dawn Reaping | all enemies | 1.0 x ATK | 4 | Strip (30% chance) |
| Sunrise Verdict | one enemy | 2.4 x ATK | 4 | Execute below 25% |

### Magister Fabian Orrery, the Gnome Star-Clockmaker

**Light · Support · Epic** · male gnome · act 1 · id `fabian`

A small gnome scholar with wild white hair, a moustache, long ears and stacked brass spectacles, in a twilight-blue robe and star-dotted mantle. A brass orrery with turning rings floats above his open palm. *Silhouette:* a small robed figure, one hand out, a ringed sphere floating above it.

- **Palette:** main `#3a5a9a`, secondary `#f0e8d0`, accent `#e8b83a`, metal `#c89a40`, glow `#ffe070`
- **Outfit:** custom: round brass spectacles with extra flip-down lenses; Male_Wizard/Body; Male_Wizard/Arms; Male_Wizard/Legs; Male_Wizard/Feet; Male_Noble/Acc_Gorget; custom: sky-blue mantle with gold stars; custom: long pointed gnome ears
- **Kit:** custom: clockwork brass orrery floating above his open palm, rings turning (weapon: floating orrery (focus))
- **Signature:** a floating brass orrery whose rings align to speed his allies
- **Pose and effects:** palm up, other hand adjusting a ring; gold orbit rings expand outward on buffs
- **Story:** A gnome astronomer of the Glass Wastes who mapped the shattered Sigil's shards across the night sky. His orrery has pointed west, toward Saltreach, ever since.
- **3D build:** Regular male at 0.86 height with a 1.1 head, gnome ears and big spectacles. Wizard robe recoloured twilight blue. The orrery hovers over Socket_Cast with 3 independently spinning ring meshes.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Star Ping | one enemy | 1.0 x ATK | basic | AtkDown (2t, 30% chance) |
| Alignment | all allies | - | 4 | Turn Meter + (20% meter), SpdUp (2t) |
| Clear Skies | all allies | - | 5 | Heal (25%), Cleanse |

### Augustin Goldmane, the Lion of Bellspire

**Light · HP · Epic** · male lion-folk · act 1 · id `augustin`

A massive lion-folk wrestler with a great tawny mane, bare chest, gold arm wraps and a crimson sash. A huge golden lion-head pauldron sits on one shoulder, its eyes glowing when he roars. *Silhouette:* a huge triangular torso with a vast mane and a second gold lion head on one shoulder.

- **Palette:** main `#c8902a`, secondary `#f0e8d0`, accent `#a83a2a`, metal `#d8b050`, glow `#ffe070`
- **Outfit:** custom: lion head with round ears; custom: bare-chested wrestler's harness with a gold belt; Male_Noble/Arms; Male_Noble/Legs; custom: digitigrade paw boot covers; Male_Noble/Acc_Pauldron_Lion (enlarged, gold); custom: gold arm wraps; custom: tufted lion tail; custom: crimson sash
- **Kit:** custom: gold-wrapped fists (no weapon); custom: enlarged lion-head pauldron (weapon: bare fists (Brawler))
- **Signature:** a golden lion-head pauldron whose eyes and mouth glow when he roars
- **Pose and effects:** arms wide, chest out, mane flared; a golden roar shockwave
- **Story:** Bellspire's festival wrestler, a lion-folk who never lost a bout. He wrestles the Hollow now, and teaches the young ones to get back up.
- **3D build:** Regular male at bulk 1.25 with a custom lion head, a mane on alpha cards plus a sculpted base, paw boot covers and a tail with 2 bones. The Noble lion pauldron is scaled 1.5x. Brawler clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Lion's Grip | one enemy | 0.085 x HP | basic | Stun (1t, 20% chance) |
| Pride's Stand | all allies | - | 4 | Shield (8%), Taunt (2t, on self) |
| Roar of Dawn | all allies | - | 5 | AtkUp (2t), Regen (2t) |

### Clemence Brightlens, the Lens-Wright

**Light · Defense · Legendary** · female human · act 2 · id `clemence`

A tall warden in white enamel armour edged in gold, with a sky-blue cape and a golden high tail. She bears a huge ribbed glass lens as a shield, and a slow beam of lighthouse light sweeps in a circle above her helm. *Silhouette:* a big round glass disc and a horizontal beam sweeping over her head.

- **Palette:** main `#f4f0e4`, secondary `#3a6aa8`, accent `#e8b83a`, metal `#d8c070`, glow `#fff0a0`
- **Outfit:** custom: open helm with a slowly turning beam of light above it (breaker); custom: white enamel plate with gold edging; custom: white enamel vambraces with gold edging; custom: white enamel greaves; custom: gold sabatons; custom: long sky-blue cape lined with gold
- **Kit:** custom: huge ribbed glass lens shield; custom: short gold sword; custom: rotating light-beam ring above the helm (weapon: short gold sword)
- **Signature:** a lighthouse-lens shield that gathers light and throws it as a beam
- **Pose and effects:** lens shield angled up, beam sweeping overhead; animated glowing lens ribs, a sweeping beam overhead, a gold aura
- **Story:** She ground the great lens for Saltreach's lighthouse in the furnaces of the Glass Wastes. When the Hollow came to shatter it, she took it down and carried it into battle.
- **3D build:** Custom enamel armour on the Regular female body. Signature material: ribbed glass (a refraction-look painted albedo plus glow-mask rings, opaque, no transparency). The beam ring is an additive VFX quad on Socket_Head, not a mesh. ShieldBlade clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Lens Flash | one enemy | 1.0 x DEF | basic | AtkDown (2t, 40% chance) |
| Prism Bulwark | all allies | - | 4 | Shield (15%), DefUp (2t), Taunt (2t, on self) |
| Focused Beam | all enemies | 0.9 x DEF | 5 | Stun (1t, 30% chance) |


## Shadow

### Rook Fenwhistle, the Raven-Folk Trapper

**Shadow · Attack · Common** · male raven-folk · act 2 · id `rook`

A lean raven-folk with a glossy black beak and feathered crest, in violet-grey leathers and patched waders hung with small iron traps. He carries a long pole that ends in a snapping iron jaw-trap. *Silhouette:* a hunched, beaked figure with a long pole ending in open jaws.

- **Palette:** main `#3a3448`, secondary `#5a5a3a`, accent `#c8c0b0`, metal `#5a5a60`, glow `#b080ff`
- **Outfit:** custom: raven head (beak, feathered crest); Male_Ranger/Body; Male_Ranger/Arms; Male_Ranger/Legs; custom: clawed bird-foot covers; custom: belt of small iron traps; custom: short tail-feather fan; custom: patched waders
- **Kit:** custom: long pole with a snapping iron jaw-trap at the end (weapon: trap-pole)
- **Signature:** a jaw-trap on a pole that snaps shut with a violet spark
- **Pose and effects:** pole held low like a fishing rod, trap open; a violet spark when the trap snaps
- **Story:** A raven-folk trapper of the Black Fens who knows every sinkhole and every safe tussock. He catches whatever the fen spits out.
- **3D build:** Regular male with a custom raven head (rigid beak) and feather crest cards, clawed foot covers and a rigid tail-feather fan on the hips. Ranger set recoloured fen violet-grey (no hood). Polearm clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Snap | one enemy | 1.0 x ATK | basic | Poison (2t, 30% chance) |
| Spring the Trap | one enemy | 1.4 x ATK | 3 | Slow (2t) |

### Pernille Strings, the Puppeteer

**Shadow · Support · Common** · female human · act 2 · id `pernille`

A slim young woman in a plum-and-silver striped coat and a tilted velvet cap, with plum hair. A little puppet knight dances on glowing violet strings from the control bar in her raised hand. *Silhouette:* a slim figure with one arm raised and a small figure dangling below the hand.

- **Palette:** main `#4a3a6a`, secondary `#c8c8d8`, accent `#e8d8a0`, metal `#a8a8b8`, glow `#b080ff`
- **Outfit:** custom: small tilted velvet cap; custom: striped plum-and-silver coat over the Female Peasant body; Female_Peasant/Arms; Female_Peasant/Legs; Female_Peasant/Feet
- **Kit:** custom: marionette crossbar with a small jointed puppet knight dancing on violet strings (weapon: marionette)
- **Signature:** a dancing marionette that mimics her enemies to mock them
- **Pose and effects:** control bar raised, puppet mid-dance; violet glowing strings
- **Story:** Saltreach's street puppeteer, whose shows made Hollow-frightened children laugh. Now her puppet trips up the real thing.
- **3D build:** Peasant set under a custom striped coat. The puppet is a 0.4 m jointed prop with 6 helper bones driven from the left-hand bar; the strings are a line VFX.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| String Pull | one enemy | 1.0 x ATK | basic | AtkDown (2t, 30% chance) |
| Curtain Call | all enemies | 0.3 x ATK | 4 | AtkDown (2t, 70% chance), TM drain (10% meter) |

### Lady Perpetua Nightshade, the Cat-Folk Umbrella Duellist

**Shadow · Defense · Rare** · female cat-folk · act 2 · id `perpetua`

An elegant black-furred cat-folk lady in a high-collared midnight-blue coat with silver buttons, her hair in a sleek chignon between her ears and a long tail curling behind her. She holds a huge starry black-silk umbrella open like a shield. *Silhouette:* an open dome of umbrella like a black moon beside a slim upright lady with cat ears and a tail.

- **Palette:** main `#1e1e3a`, secondary `#2a2430`, accent `#c8c8e0`, metal `#b8b8c8`, glow `#a070ff`
- **Outfit:** custom: cat ears with a jet hairpin between them; custom: high-collared midnight-blue dress coat; custom: long silver-buttoned gloves; Female_Ranger/Legs; custom: heeled boots; custom: long slim cat tail; custom: silver chain at the waist
- **Kit:** custom: huge black-silk umbrella with silver ribs and star flecks; opens as a shield, the ferrule is a blade (weapon: umbrella (shield and blade))
- **Signature:** an umbrella of starry black silk that opens into a shield
- **Pose and effects:** umbrella open and angled forward, tail curled, ferrule pointed; violet raindrops roll off the canopy when she blocks
- **Story:** A black-furred cat-folk shipping heiress of Saltreach who refuses to run from anything, the Hollow included. She has never once got wet.
- **3D build:** Regular female with black-furred skin paint, custom cat ears and a tail (3 bones). The umbrella has 2 states: closed in idle (a cane) and open on skills (canopy swap or a scale bone). ShieldBlade clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Ferrule Jab | one enemy | 0.9 x DEF | basic | DefDown (2t, 30% chance) |
| Open Canopy | self | - | 4 | Shield (30%), Taunt (2t) |
| Starless Rain | all enemies | 0.6 x DEF | 5 | DefDown (2t, 50% chance) |

### Jasper Blackcard, the Fox-Folk Gambler

**Shadow · Attack · Rare** · male fox-folk · act 2 · id `jasper`

A sly, smoke-grey fox-folk in a long violet waistcoat and cravat, with a wide-brimmed hat cut for his ears and a big silver-tipped tail. He fans a hand of razor-edged steel cards at his chin. *Silhouette:* a wide-brimmed hat with ears through it, a fan of cards like a small shield, and a big bushy tail.

- **Palette:** main `#2a1e3a`, secondary `#3a3a4a`, accent `#e8e0c8`, metal `#c8c8d0`, glow `#b07aff`
- **Outfit:** custom: flat-crowned wide-brimmed hat with a card in the band, cut for fox ears; Male_Noble/Body; Male_Noble/Arms; Male_Noble/Legs; Male_Noble/Feet; custom: long violet waistcoat and cravat; custom: big bushy smoke-grey tail with a silver tip
- **Kit:** custom: fan of razor-edged steel playing cards with violet backs (weapon: throwing cards)
- **Signature:** a fan of razor-edged steel cards that return to his hand
- **Pose and effects:** cards fanned at his chin, hat tipped, tail flicking; violet card trails; cards flip in the air
- **Story:** A dusk-furred fox-folk who ran card tables on the fen barges until the Hollow sank them. He has dealt himself into this war.
- **3D build:** Regular male with a custom fox head, ears through the hat brim and a tail with 3 bones. Noble set recoloured deep violet. The card fan is a rigid prop; thrown cards are VFX. Daggers clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Card Flick | one enemy | 0.5 x ATK x2 hits | basic | - |
| Stacked Deck | one enemy | 1.8 x ATK | 3 | Strip |
| Fifty-Two Pickup | all enemies | 0.85 x ATK | 4 | DefDown (2t, 40% chance) |

### Mabon Sandglass, Keeper of Borrowed Hours

**Shadow · Support · Rare** · male human · act 1 · id `mabon`

A grey-bearded old man in a night-blue robe with a brass pocket-watch chain and cane. A tall hourglass full of glowing lilac sand is strapped to his back. *Silhouette:* a robed old man with a tall hourglass shape rising behind his head.

- **Palette:** main `#24284a`, secondary `#7a6a50`, accent `#c8a8ff`, metal `#b09a60`, glow `#b080ff`
- **Outfit:** Male_Wizard/Body; Male_Wizard/Arms; Male_Wizard/Legs; Male_Wizard/Feet; custom: pocket-watch chain
- **Kit:** custom: large hourglass strapped to his back, full of lilac sand; custom: short brass-tipped cane (weapon: cane)
- **Signature:** a giant hourglass on his back whose violet sand flows upward when he gives his allies time
- **Pose and effects:** cane forward, turning toward an ally; streams of violet sand
- **Story:** Hollow Deep's clock-keeper counted the hours for miners who never saw the sun. He learned to steal a few minutes from the dark and give them away.
- **3D build:** Wizard robe recoloured night blue. The hourglass (0.7 m) is rigid on Socket_Back; sand is a glow-mask gradient with a UV-scroll that reverses on Turn the Glass.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Grain of Sand | one enemy | 1.0 x ATK | basic | TM drain (15% meter, 50% chance) |
| Turn the Glass | all allies | - | 4 | Turn Meter + (20% meter) |
| Borrowed Time | all allies | - | 5 | Heal (15%), Cleanse |

### Tindra Mirefang, the Blowpipe Huntress

**Shadow · Attack · Epic** · female human · act 2 · id `tindra`

A lithe, dark-skinned huntress in mottled leathers under a flared mantle of grey-violet nightjar feathers, her locs tied back with reed cord. She kneels with a long fen-reed blowpipe raised to her lips. *Silhouette:* a crouched figure with a long thin tube at her lips and a flared feather mantle.

- **Palette:** main `#4a4058`, secondary `#3a4a3a`, accent `#e0c8a0`, metal `#8a8a90`, glow `#b070ff`
- **Outfit:** custom: mottled leather wrap; Female_Ranger/Arms; Female_Ranger/Legs; Female_Ranger/Feet; custom: nightjar-feather mantle (mottled grey-violet, flares at the shoulders); custom: dart pouch
- **Kit:** custom: fen-reed blowpipe, 1.6 m (weapon: blowpipe)
- **Signature:** a blowpipe longer than she is tall, whose darts glow violet
- **Pose and effects:** down on one knee, pipe raised to her lips; violet dart streaks and poison clouds
- **Story:** Something walks in the Black Fens, and she has hunted it for ten years. The Hollow make good practice.
- **3D build:** Ranger arms and legs with a custom wrap. The feather mantle is sculpted with alpha-card edges on 2 bones. The blowpipe is held two-handed at the mouth for Cast; it needs a custom Cast clip (Archer clips as the fallback).

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Fen Dart | one enemy | 1.0 x ATK | basic | Poison (2t, 40% chance) |
| Mire Toxin | one enemy | 1.9 x ATK | 3 | Poison (2t), Spread |
| Cloud of Darts | all enemies | 0.9 x ATK | 4 | Poison (2t, 50% chance) |

### Ambrose Keywarden, the Night Porter

**Shadow · HP · Epic** · male human · act 2 · id `ambrose`

A huge bearded porter in an indigo greatcoat and peaked cap, a padlock at his belt. He swings a ring of giant iron keys on a chain like a flail. *Silhouette:* a massive greatcoat block with a big ring of keys swinging at his side.

- **Palette:** main `#2a2a40`, secondary `#5a4a3a`, accent `#c8b070`, metal `#6a6a70`, glow `#a070ff`
- **Outfit:** custom: night porter's peaked cap; Male_Knight/Body_Cloth; Male_Peasant/Arms; Male_Knight/Legs; Male_Knight/Feet; Male_Noble/Acc_Gorget; custom: heavy greatcoat with a lamp-black lining; custom: giant padlock at his belt
- **Kit:** custom: iron ring of oversized keys on a chain, swung like a flail; custom: padlock (weapon: key ring (flail))
- **Signature:** a ring of giant iron keys, each said to open a door in the dark
- **Pose and effects:** key ring dangling from one fist, other hand on the padlock; violet glints off the keys; a lock-shaped shield glyph
- **Story:** He kept the gates of the Wraithmoor barrows for forty years. The dead kings gave him their keys, and he locks the Hollow out.
- **3D build:** Knight cloth body under a custom greatcoat with 4 tail bones. The key ring is 3 chain helper bones and a rigid ring holding 7 keys. Heavy clips.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Key Ring | one enemy | 0.085 x HP | basic | DefDown (2t, 30% chance) |
| Locksmith's Ward | all allies | - | 4 | Shield (8%), Taunt (2t, on self) |
| Night Watch | all allies | - | 5 | Regen (2t), DefUp (2t) |

### Lune Duskfeather, the Owl-Folk Night Nurse

**Shadow · Support · Epic** · female owl-folk · act 1 · id `lune`

A gentle owl-folk with a heart-shaped white face, large eyes and silver-grey ear tufts, in a long night-blue coat under a broad feathered capelet. She carries a staff topped by a small silver owl whose eyes glow violet. *Silhouette:* a wide triangle of feather capelet and a tall staff with an owl on top.

- **Palette:** main `#2a3050`, secondary `#e8e4dc`, accent `#c8a060`, metal `#b8b8c8`, glow `#b490ff`
- **Outfit:** custom: owl head with ear tufts; custom: long night-blue nurse's coat; custom: feathered forearms; Female_Peasant/Legs; custom: taloned foot covers; custom: broad owl-feather capelet that flares like folded wings; custom: herb satchel
- **Kit:** custom: perch staff topped by a small silver owl whose eyes glow violet (weapon: perch staff)
- **Signature:** a heart-shaped white owl face and a silver owl on her staff that keeps watch over the wounded
- **Pose and effects:** staff raised, capelet spread like half-open wings; silent violet feathers drift down when she heals
- **Story:** An owl-folk night nurse of Hollow Deep's infirmary who moves without a sound. The wounded wake to find her already there.
- **3D build:** Regular female with a custom owl head (a flat face disc, not a long beak, so it stays clear of the beak-mask look) and ear-tuft cards. The capelet is sculpted feathers on 4 bones. Taloned foot covers. The staff owl is rigid with glowing eyes.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Night Call | one enemy | 1.0 x ATK | basic | TM drain (15% meter, 40% chance) |
| Night Vigil | all allies | - | 4 | Heal (20%), DefUp (2t) |
| Silent Wings | all enemies | 0.5 x ATK | 5 | AtkDown (2t, 60% chance), TM drain (15% meter) |

### Sir Aldric Twinshade, Knight of the Second Shadow

**Shadow · Defense · Legendary** · male human · act 2 · id `aldric`

A tall knight in black-violet plate with silver trim and a visored helm, his tattered night-blue cape flecked with silver stars. He bears a hexagonal black-glass shield traced with constellations, and his own shadow, twice his size and glowing violet, stands up behind him and copies his guard. *Silhouette:* a knight with a tall hexagonal shield, and a much larger translucent violet double looming behind him.

- **Palette:** main `#1e1a30`, secondary `#2a3050`, accent `#d0d4e8`, metal `#a8acc0`, glow `#a070ff`
- **Outfit:** Male_Knight/Head_Armet; Male_Knight/Body_Armor; Male_Knight/Arms; Male_Knight/Legs; Male_Knight/Feet; Male_Noble/Acc_Pauldron; custom: tattered night-blue cape with silver star flecks; custom: translucent violet shadow-twin, twice his size, standing behind him (breaker)
- **Kit:** custom: tall hexagonal shield of black glass with silver constellation lines; custom: silver longsword; custom: shadow-twin aura (weapon: silver longsword)
- **Signature:** his own shadow stands up behind him at twice his size and blocks blows too
- **Pose and effects:** shield forward; the shadow-twin mirrors him a beat late; the animated twin, drifting star flecks, a violet aura (the twin turns red on Hollow copies)
- **Story:** In Wraithmoor he fought a Hollow knight for a night and a day, until his shadow rose to help him. It has never lain down again.
- **3D build:** Knight armour recoloured black-violet with silver; no horns and no spikes, to stay clear of the dark-lord archetype. The twin is a second instance of his LOD2 mesh, scaled 1.9x, with an additive violet shader and a 0.15 s animation delay (one extra skinned mesh; check the draw-call budget). The shield's constellation lines go on the glow mask.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Twin Strike | one enemy | 1.0 x DEF | basic | DefDown (2t, 40% chance) |
| Second Shadow | all allies | - | 4 | Shield (15%), Taunt (2t, on self), Counter (2t, on self) |
| Eclipse Wall | all enemies | 0.9 x DEF | 5 | Stun (1t, 30% chance), TM drain (15% meter) |

### Ondessa Mirebloom, the Fen Dryad

**Shadow · HP · Legendary** · female fen dryad (living plant) · act 2 · id `ondessa`

A tall dryad of the fens with moss-dark skin, trailing hair of black reeds and lily stems, and a gown of black water and leaves that pools into lily pads at her feet. A crown of huge violet night-lilies blooms on her head, and pale wisps circle her slowly. *Silhouette:* a tall gown pooling wide at the base, a big flower crown and floating lights.

- **Palette:** main `#1a1428`, secondary `#3a4a3a`, accent `#e8d8f8`, metal `#9a9ab0`, glow `#a070ff`
- **Outfit:** custom: crown of huge night-lilies blooming violet (breaker); custom: gown of black water and leaves that drips and pools at her feet; custom: long gloves of lily leaves; custom: root-tangle lower legs; custom: roots spreading into a pool of lily pads; custom: slow ring of floating violet will-o'-wisps (aura); custom: train of lily pads
- **Kit:** custom: lily crown; custom: wisp ring; custom: gnarled bog-oak staff tipped with a closed, glowing lily bud (weapon: bog-oak staff)
- **Signature:** a crown of giant night-lilies that bloom wider every time she is struck
- **Pose and effects:** staff held out, wisps circling her; animated wisps, a dripping-water shader on the gown, a violet aura; the lilies open under Taunt
- **Story:** The fen-folk whisper of a drowned thing that learned to walk. It was the Black Fens themselves, grown into a woman of reeds and lilies, and she chose to fight the Hollow rather than feed them.
- **3D build:** Regular female with dark moss-green skin paint. The gown and root skirt hide the legs (alpha-clip root cards at the hem; the legs are still skinned so clips play). The lily crown petals have 1 bone each so they open. Wisps are VFX.

| Skill | Target | Power | Cooldown | Effects |
|---|---|---|---|---|
| Mire Grasp | one enemy | 0.09 x HP | basic | Slow (2t, 40% chance) |
| Wisp Ring | all allies | - | 4 | Shield (12%), Regen (2t), Taunt (2t, on self) |
| Drown the Light | all enemies | 0.07 x HP | 5 | TM drain (20% meter), DefDown (2t, 50% chance) |


## Reading the skill tables

Power is a multiplier of the stat named (ATK, DEF or HP; HP-scaled hits use small numbers such as 0.08 x HP, as the existing HP heroes do). Effects use the existing `FxType` and `SkillDef` options: `t` is turns, a percentage chance is the chance to apply, Heal and Shield percentages are of max HP, and Turn Meter values are a share of the bar. Pierce, Spread and Execute are the phase 2 mechanics; Counter and the Fall Heal passive (Obrecht) are already in the engine.
