# Sigilbound roster plan: 24 to 100 heroes

Step 19 of `docs/sigilbound/PLAN.md`. **Phases and concepts only.** Each new hero still gets a
design step of its own (stats, skill numbers, balance sims, tests) and an art step (a baked
free-path model). Names, looks and hooks here are starting points; a design step can change them.

## 1. Targets

The 24 heroes today are lopsided: Tide and Shadow have 4 each, and there are 9 Attack heroes
against 4 HP heroes. At 100, every element has 20, roles are spread for team building, and the
rarity curve leaves room for a deep early bench.

| | Today (24) | Added (76) | Target (100) |
|---|---|---|---|
| Common / Rare / Epic / Legendary | 6 / 8 / 6 / 4 | 16 / 26 / 22 / 12 | **22 / 34 / 28 / 16** |
| Flame / Gale / Tide / Light / Shadow | 5 / 6 / 4 / 5 / 4 | 15 / 14 / 16 / 15 / 16 | **20 each** |
| Attack / Defense / HP / Support | 9 / 5 / 4 / 6 | 25 / 17 / 16 / 18 | **34 / 22 / 20 / 24** |

The tables below meet these targets exactly. `docs/sigilbound/roster.py` checks the counts and
regenerates the tables; run it after any change.

## 2. Rules every new hero follows

- **Free.** The game stays fully free: no shop and no paid summons. New heroes join the existing
  summon pools (`Game.ShardRates`: the shard picks a rarity, then a hero of that rarity).
  More heroes per rarity means rarer duplicates, so phase 2 needs a check on duplicate and
  promotion pacing before it ships (see section 5).
- **Original.** Names, looks and stories are Sigilbound's own (ART_BIBLE section 11). No
  borrowing from other games' characters.
- **Readable at 70 px** (ART_BIBLE section 1): one silhouette idea, one main colour, one accent,
  and a visible role prop. Element colour lives in the rim light and glows.
- **Free-path art.** Every hero is one of the three CC0 Quaternius bodies (`hooded`,
  `adventurer`, `monarch`), repainted, with kit from `unity/Tools~/blenderforge/quat.py`. Kit
  pieces marked `*` don't exist yet and are built in that phase's first art step: anchor, axe,
  book, fan, gauntlets, lute, net.
- **Uses today's combat first.** Most hooks use the existing effects (`FxType`: AtkUp, DefUp,
  SpdUp, Regen, Shield, Taunt, AtkDown, DefDown, Slow, Poison, Stun, Heal, Tm, TmDrain, Cleanse,
  Strip) and HP scaling. New mechanics are listed in section 4 and land before the heroes that
  need them.

## 3. Phases

Each phase ships in batches of 4: one design step (stats and skills in `Core/GameData.cs`, looks
in `Core/Looks.cs`, kit in `HeroArt`, tests in `unity/Tests~`, a balance sim) and one art step
(4 heroes baked with quat.py, checked with the screenshot tour), as in steps 13 to 18.

### Phase 1: Roadwardens (16 heroes, Common and Rare)

Locals of chapters 1 to 5 (Thornwick Road to Bellspire Ruins). Phase 1 deepens the early
collection and fills the thin spots: Tide and Shadow, and the HP and Defense roles. It needs no
new mechanics apart from the `anchor` and `net` kit.

| Id | Name | Rarity | Element | Role | Body | Look and kit (* = new kit piece) | Mechanic hook | Concept |
|---|---|---|---|---|---|---|---|---|
| `bram` | Bram Wetherby | Common | Tide | Hp | monarch | beard, anchor* | Shield, Regen and Taunt on self | Drowned Mill ferryman who hauled the mill wheel back by hand. |
| `nettle` | Nettle Quickfinch | Common | Gale | Attack | adventurer | bandana, daggers | Hits twice with a chance to Slow; a big single hit | Thornwick hedge-runner, fastest courier on the road. |
| `hobb` | Hobb Lanternjaw | Common | Light | Defense | monarch | helm, shield, lantern | DefUp and a small Shield on all allies | Road warden who keeps the waystone lamps lit. |
| `greta` | Greta Brineknot | Common | Tide | Support | hooded | braid, net* | Slow on hit; small Heal and Cleanse on all allies | Saltmarsh net-mender. |
| `cole` | Cole Ashworth | Common | Flame | Attack | adventurer | spiky_hair, axe* | Steady hits; a heavier hit that raises his own ATK | Ashgrave charcoal burner with a woodsman's axe. |
| `tilly` | Tilly Mothwing | Common | Shadow | Support | hooded | veil, candle | DefDown on hit; weak AoE with DefDown and TmDrain | Moth-keeper of the Bellspire ruins. |
| `ronan` | Ronan Stoutbarrow | Common | Gale | Hp | monarch | beard, hammer | Regen on all allies, with a self Shield and Taunt | Thornwick innkeeper, never lets a guest go hungry. |
| `isla` | Isla Fenlight | Common | Light | Support | hooded | long_hair, bell | Small Heal and SpdUp on all allies | Saltmarsh chapel bell-ringer. |
| `dunstan` | Dunstan Oaken | Rare | Gale | Defense | monarch | helm, shield, cape | Taunt; DefUp on allies | Old shield-captain of the Thornwick militia. |
| `marisol` | Marisol Wavecrest | Rare | Tide | Attack | adventurer | ponytail, trident | AoE hit; Slow | Drowned Mill pearl diver turned raider of the Hollow. |
| `vesper` | Vesper Gloam | Rare | Shadow | Attack | hooded | mask, daggers | Poison; DefDown | Thief who works the Hollow Deep's edges. |
| `brega` | Brega Ironmoss | Rare | Tide | Defense | monarch | helm, shield, beard | Shield and Taunt on self; AoE Slow | Mill-lock keeper in barnacled plate. |
| `fennick` | Fennick Sootpaw | Rare | Flame | Support | adventurer | bandana, candle | Poison on hit and on all enemies; AtkUp and Heal on all allies | Ashgrave firework maker. |
| `orla` | Orla Greywater | Rare | Tide | Hp | hooded | long_hair, staff, robe | Heal and Regen on all allies | Marsh healer who brews from river weed. |
| `silas` | Silas Penumbra | Rare | Shadow | Defense | hooded | veil, shield | Shield and Taunt on self; AoE DefDown | Bellspire gatekeeper sworn to the dark. |
| `wynn` | Wynn Brightfeather | Rare | Light | Attack | adventurer | spiky_hair, bow, quiver | Single big hit; Strip | Falconer of Stormcrown's foothills. |

### Phase 2: Glass and Storm (20 heroes, Rare and Epic)

Heroes of chapters 6 to 10 (The Glass Wastes to The Last Pyre). Phase 2 brings the first new
mechanics: Pierce, Execute, Pull, Burn, spread-on-kill and the counter Taunt.

| Id | Name | Rarity | Element | Role | Body | Look and kit (* = new kit piece) | Mechanic hook | Concept |
|---|---|---|---|---|---|---|---|---|
| `kestrel` | Kestrel Vane | Rare | Gale | Attack | adventurer | bandana, spear | Pierce: hits the target and the one behind | Stormcrown lancer. |
| `sorrel` | Sorrel Duskmere | Rare | Shadow | Hp | monarch | cape, club | Poison on hit and on all enemies; team Regen with a self Shield | Hollow Deep miner who survived the collapse. |
| `amber` | Amber Glasswright | Rare | Flame | Defense | hooded | helm, shield | Burn on hit; team Shield; self Shield, Taunt and Counter | Glass Wastes kiln-mistress with a fused-glass shield. |
| `tobias` | Tobias Reed | Rare | Gale | Support | adventurer | long_hair, lute* | SpdUp and Turn Meter for all allies; team Heal and Cleanse | Wandering lute player of the high passes. |
| `nerys` | Nerys Coldspring | Rare | Tide | Support | hooded | circlet, mirror | Cleanse, DefUp and Turn Meter for all allies; team Heal | Sunken Cathedral acolyte. |
| `hale` | Hale Sunforge | Rare | Light | Hp | monarch | beard, hammer, cape | Small self Shield and Taunt; team Regen | Bellspire smith who forged the waystone lamps. |
| `juno` | Juno Ashveil | Rare | Flame | Attack | hooded | veil, whip, robe | Burn on hit; Burn spreads on a kill | Fire dancer of the Last Pyre. |
| `pike` | Pike Saltgrave | Rare | Tide | Attack | adventurer | bandana, hook | Pull: takes the target's turn meter | Smuggler of the Saltmarsh reaches. |
| `rhea` | Rhea Lumen-Kin | Rare | Light | Defense | monarch | helm, shield, cape | Shield, Taunt and Cleanse on self; team DefUp and Shield | Sister of Lumen Varro, sworn to the light-guard. |
| `corwin` | Corwin Blackthorn | Rare | Shadow | Attack | adventurer | spiky_hair, katana | Execute below 25% HP | Duelist exiled from the Stormcrown court. |
| `astrid` | Astrid Windrider | Epic | Gale | Attack | adventurer | ponytail, spear, cape | Piercing basic; a big hit that speeds her up; AoE Slow | Sky-rider of Stormcrown Heights. |
| `caspian` | Caspian Deepwell | Epic | Tide | Hp | monarch | crown, trident, cape | Team Shield while he Taunts; team Regen and DefUp | Drowned king's heir, guardian of the Sunken Cathedral. |
| `ignis` | Ignis Kael | Epic | Flame | Support | adventurer | flame_hair, staff | Burn on hit and on all enemies; team AtkUp, Turn Meter and Heal | Pyre-priest who tends the eternal flame. |
| `lyra` | Lyra Starglass | Epic | Light | Support | hooded | halo, mirror, robe | Heal and Cleanse all allies; team AtkUp and SpdUp | Glass Wastes oracle who reads the shattered Sigil. |
| `malachar` | Malachar Gloomhand | Epic | Shadow | Defense | monarch | helm, horns, shield, cape | Taunt and Counter; DefDown on hit and on all enemies | Hollow knight who broke free of the Hollow. |
| `rowan` | Rowan Thornheart | Epic | Gale | Defense | monarch | long_hair, beard, shield, cape | Small team Shield; Taunt with a turn of DefUp | Warden of the Thornwick old wood. |
| `selene` | Selene Tidewhisper | Epic | Tide | Support | hooded | long_hair, orb, robe | TmDrain on hit; AoE TmDrain that fills allies' Turn Meter; team Heal and Cleanse | Moon-priestess of the tides. |
| `brann` | Brann Emberfist | Epic | Flame | Hp | monarch | beard, gauntlets | Team Shield while he Taunts and Counters; AoE Burn and team AtkUp | Pit fighter of the Ashgrave pass. |
| `ysra` | Ysra Nightbloom | Epic | Shadow | Attack | hooded | veil, daggers | Poison on hit and on all enemies; a big hit that may Stun | Hollow Deep assassin who poisons with flowers. |
| `oswin` | Oswin Dawnspear | Epic | Light | Attack | adventurer | helm, spear | Pierce; AtkUp self | Light-lancer of the Bellspire order. |

### Phase 3: The Hollow Unbound (20 heroes, all rarities)

Hollow who broke free, and people touched by the Titan. Phase 3 gives the story its turn (the
enemy becomes recruitable) and brings the first legendaries since launch. It needs a
revive effect, a heal when an ally falls, and damage that grows with HP lost.

| Id | Name | Rarity | Element | Role | Body | Look and kit (* = new kit piece) | Mechanic hook | Concept |
|---|---|---|---|---|---|---|---|---|
| `grimsby` | Grimsby Boneweaver | Rare | Shadow | Support | adventurer | mask, staff | DefDown on hit; AoE DefDown that may Strip; team Heal | Hollow bone-reader who sees the Sigil's cracks. |
| `marrow` | Marrow Cinderhulk | Epic | Flame | Attack | monarch | long_hair, club, runes, cape | Rage: hits grow with his lost HP; AoE that raises his ATK | A Hollow giant who woke with fire in his chest and remembers his name. |
| `ember` | Ember Tallow | Rare | Flame | Hp | hooded | candle, robe | Last Light passive (heals all when an ally falls); Burn on hit; team Regen; self Shield | Candle-nun of the Last Pyre. |
| `quill` | Quill Inkwell | Rare | Light | Support | adventurer | circlet, book | Strip on hit and a big Strip; team AtkUp and Heal | Royal archivist who writes the Sigil's history. |
| `drift` | Drift Coldfoot | Common | Tide | Attack | adventurer | bandana, daggers | Slow; double hit | Ice skater of the frozen mill pond. |
| `sprig` | Sprig Mosslight | Common | Gale | Support | hooded | braid, bell | Heal; SpdUp | Child druid of the Thornwick wood. |
| `tallow` | Tallow Firth | Common | Flame | Defense | monarch | helm, shield | Burn on hit (small chance); team DefUp | Ashgrave watchman. |
| `hollis` | Hollis Shade | Common | Shadow | Hp | hooded | club (hood kept) | Shield, Regen and Taunt on self | Gravedigger of Hollow Deep. |
| `auren` | Auren Halewind | Epic | Gale | Support | hooded | long_hair, fan*, robe | Cleanse, SpdUp and Heal for all allies; team AtkUp and Turn Meter | Wind-singer of Stormcrown. |
| `cinder` | Cinder Vale | Epic | Flame | Attack | adventurer | spiky_hair, sword, runes | Burn on hit; AoE AtkDown; a big hit that may Burn | Survivor of the Titan's first eruption. |
| `thessaly` | Thessaly Reefward | Epic | Tide | Defense | monarch | helm, shield, trident | Small team Shield while she Taunts; AoE DefDown | Captain of the drowned fleet. |
| `umbra` | Umbra Veilkin | Epic | Shadow | Support | hooded | mask, orb, robe | TmDrain on hit; AoE TmDrain and DefDown; team Heal, Cleanse and Turn Meter | Hollow seer who serves the living now. |
| `lucan` | Lucan Brightshield | Epic | Light | Defense | monarch | helm, halo, shield, cape | Small team Shield while he Taunts; AoE AtkDown | Paladin of the Sunken Cathedral. |
| `kharra` | Kharra Titanborn | Legendary | Flame | Hp | monarch | horns, beard, hammer, cape | Self Shield, Taunt and Regen; AoE Burn with team AtkUp | Born in the Titan's shadow, half stone. |
| `nocturne` | Nocturne | Legendary | Shadow | Attack | hooded | mask, katana, cape | DefDown on hit; AoE Stun; Execute below 30% HP | The Hollow's first champion, now its hunter. |
| `aelira` | Aelira Tidecrowned | Legendary | Tide | Support | hooded | crown, orb, robe, cape | Big team Heal with Shield and Cleanse; team Turn Meter, AtkUp and DefUp | Queen of the Sunken Cathedral. |
| `zephyrine` | Zephyrine | Legendary | Gale | Attack | adventurer | ponytail, katana, cape | AoE; SpdUp all | Wind spirit bound to the Sigil's Gale shard. |
| `solenne` | Solenne Dawnborn | Legendary | Light | Hp | monarch | halo, crown, hammer, cape | Regen all; revive-style Heal | Sun-saint who kept the first Sigil shard. |
| `nell` | Nell Shadowgrass | Common | Shadow | Attack | hooded | mask, hook | Poison; Slow | Reed-cutter of the black fens. |
| `briar` | Briar Wildroot | Rare | Gale | Attack | hooded | braid, whip | Poison; Slow | Thorn-witch of the deep wood. |

### Phase 4: The Sigil Bearers (20 heroes, all rarities)

A new campaign act (chapters 11 to 15, a port city and the storm coast) built around the
five bearers of the Sigil shards, one Legendary per element, with the Hollow king as the
final ally. This phase needs the new chapters first (a separate campaign step).

| Id | Name | Rarity | Element | Role | Body | Look and kit (* = new kit piece) | Mechanic hook | Concept |
|---|---|---|---|---|---|---|---|---|
| `wick` | Saint Wick of the Lamps | Legendary | Flame | Support | adventurer | halo, candle, cape | AtkUp all; Heal all; Cleanse | The urchin who lit the city lamps, grown into the keeper of the Flame shard's hearth. |
| `perrin` | Perrin Gullsby | Common | Tide | Defense | adventurer | bandana, shield | Taunt; DefUp self | Harbour guard of the new act's port city. |
| `loam` | Loam Bracken | Common | Gale | Defense | monarch | beard, shield | Taunt; Shield self | Farmer who held the gate with a plough-shield. |
| `merrit` | Merrit Vellum | Common | Light | Attack | adventurer | spiky_hair, sword | Hit; Strip | Squire to the Sigil Bearers. |
| `calder` | Calder Stormglass | Epic | Light | Support | monarch | beard, lantern | SpdUp all allies; Heal the lowest | Lighthouse keeper whose beam guides ships, and allies, home. |
| `sienna` | Sienna Firethorn | Rare | Flame | Attack | hooded | braid, bow, quiver | Burn-style Poison; AoE | Archer of the ember groves. |
| `dorian` | Dorian Lowe | Rare | Tide | Hp | monarch | beard, anchor* | Regen all; Taunt | Harbourmaster of the port city. |
| `ivo` | Ivo Brightmantle | Rare | Light | Hp | monarch | helm, hammer, cape | Heal self; DefUp | Bell-tower guard. |
| `saoirse` | Saoirse Wraithmoor | Rare | Shadow | Defense | hooded | veil, shield | Shield; TmDrain attacker | Moor-walker who guards the barrows. |
| `gideon` | Gideon Flamecaller | Epic | Flame | Defense | monarch | helm, shield, cape | Shield all; Burn on attackers | Commander of the Pyre guard. |
| `liora` | Liora Tidesinger | Epic | Tide | Attack | hooded | long_hair, trident | AoE; Slow all | Siren of the storm coast. |
| `bastian` | Bastian Grey | Epic | Shadow | Hp | monarch | cape, club | Regen; HP-scaled AoE | Hollow-blooded brawler. |
| `elowen` | Elowen Brightwater | Epic | Light | Attack | adventurer | ponytail, bow, quiver | Big hit; AtkDown | Ranger who carries a Light shard arrow. |
| `faelan` | Faelan Galeheart | Epic | Gale | Hp | monarch | beard, cape, spear | Regen all; SpdUp self | Stormcrown chieftain. |
| `azura` | Azura Deepstar | Legendary | Tide | Attack | adventurer | long_hair, trident, cape | AoE; Stun | Bearer of the Tide shard. |
| `pyrrhus` | Pyrrhus Sunderflame | Legendary | Flame | Defense | monarch | horns, shield, hammer, cape | Shield all; counter-style Taunt | Bearer of the Flame shard. |
| `kaelith` | Kaelith Shadowborne | Legendary | Shadow | Hp | hooded | crown, orb, cape, robe | Regen; TmDrain all | Bearer of the Shadow shard. |
| `aurex` | Aurex Lightsworn | Legendary | Light | Attack | adventurer | halo, sword, cape | AoE; Strip; AtkUp self | Bearer of the Light shard. |
| `stormbearer` | Gale Stormbearer | Legendary | Gale | Defense | monarch | helm, shield, cape | Shield all; Taunt; SpdUp | Bearer of the Gale shard. |
| `vex` | Vex Hollowking | Legendary | Shadow | Support | monarch | crown, orb, cape | DefDown all; Stun; Strip | The Hollow's king, an uneasy ally at the end. |

## 4. New mechanics (engine work, before the heroes that use them)

**Phase 2 mechanics are built (PLAN step 25):**

| Mechanic | In data | Rule |
|---|---|---|
| Burn | `FxType.Burn` | 5% max HP a turn; stacks with Poison; ignores shields; a debuff (Cleanse removes it); capped on the Titan like Poison. |
| Pierce | `SkillDef.Pierce` | Each hit also strikes the next enemy in line for half damage. |
| Execute | `SkillDef.Execute` (e.g. 0.25) | A target left below that HP fraction falls at once; bosses and the Titan are immune. |
| Pull | `FxType.Pull` (Val) | Takes that much turn meter from the target and gives it to the attacker. |
| Spread | `SkillDef.Spread` | If the target dies, its Poison and Burn move to the next enemy. |
| Counter | `FxType.Counter` (buff) | Hit by a single-target attack, the holder strikes back with its basic skill at half power; Strip removes it. |

**Phase 3 mechanics are built (PLAN step 31):**

| Mechanic | In data | Rule |
|---|---|---|
| Revive | `FxType.Revive` (Pct, Turns) | A buff: if the holder would fall, it comes back at that share of max HP instead, once (the buff is used up). Not on the Titan. Strip removes it. |
| Rage | `SkillDef.Rage` | Damage x (1 + Rage x the attacker's missing HP fraction): Rage 1.0 is +50% at half HP. |
| Last Light | `HeroDef.FallHeal` (passive) | When an ally falls, a living hero with the passive heals all living allies that share of their max HP. The hero screen shows passives. |

They are tested in `unity/Tests~/MechanicsTests.cs`. They are Unity only: the web game's 24 heroes use none of them, and the web effect order is unchanged. The table below is the original plan; phase 3's mechanics are built too (above).

| Mechanic | Used by | Notes |
|---|---|---|
| Burn (damage over time that stacks with Poison) | Ignis, Juno, Sienna, Gideon | Today's hooks say "Burn-style Poison": either a new FxType or Poison with a fire look |
| Pierce (also hits the unit behind) | Kestrel, Oswin | Needs a 'behind' rule on the rune circle (BattleLayout) |
| Execute (finish a target below 25% HP) | Corwin, Nocturne | |
| Pull (take turn meter) | Pike | Existing TmDrain may be enough |
| Spread on kill | Juno | Moves the target's Poison to a neighbour |
| Counter Taunt | Brann, Pyrrhus | Hits back when hit while taunting |
| Revive-style Heal | Solenne | Once per battle, the first ally to fall comes back at low HP |
| Grows with HP lost | Marrow | A new Scale (missing HP) |
| Heal on an ally's death | Ember | A battle event hook |

## 5. Before each phase ships

- **Balance:** run the battle sims for the new heroes against today's teams (no new hero may
  win more than about 55% of mirror fights at equal stars and gear).
- **Summon pacing:** with more heroes per rarity, check that the free shard income still lets a
  player promote a chosen hero in a reasonable time; adjust duplicate rewards if not, not the
  prices (there are none).
- **Save codes:** the web-compatible save format must still load (`SaveCodec` tests); new hero
  ids are additive.
- **Art:** the 4 baked heroes pass the screenshot tour (models show, animated, readable).

## 6. Order of work after step 19

1. Step 20: verify all 24 baked heroes in game on device (menu lineup, battle, hero viewer), and
   polish robes and kit.
2. Phase 1 design and art steps (4 batches).
3. Engine work for the phase 2 mechanics, then phase 2 (5 batches).
4. Phase 3 mechanics, then phase 3 (5 batches).
5. Campaign act 2 (chapters 11 to 15), then phase 4 (5 batches).
