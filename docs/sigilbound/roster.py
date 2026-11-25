# Roster concepts for docs/sigilbound/ROSTER.md (step 19). Checks the tallies and prints the tables:
#   python3 docs/sigilbound/roster.py            (counts)
#   python3 docs/sigilbound/roster.py --md out.md (tables to paste into ROSTER.md)
import collections, sys
C,R,E,L = "Common","Rare","Epic","Legendary"
F,G,T,Li,S = "Flame","Gale","Tide","Light","Shadow"
A,D,H,Su = "Attack","Defense","Hp","Support"
h,a,m = "hooded","adventurer","monarch"
# (phase, id, name, rarity, element, role, base, kit, hook, concept)
X = [
# Phase 1: Roadwardens (chapters 1-5 locals; fill Tide/Shadow and the Hp/Defense roles; common/rare)
(1,"bram","Bram Wetherby",C,T,H,m,"beard, anchor*","Shield, Regen and Taunt on self","Drowned Mill ferryman who hauled the mill wheel back by hand."),
(1,"nettle","Nettle Quickfinch",C,G,A,a,"bandana, daggers","Hits twice with a chance to Slow; a big single hit","Thornwick hedge-runner, fastest courier on the road."),
(1,"hobb","Hobb Lanternjaw",C,Li,D,m,"helm, shield, lantern","DefUp and a small Shield on all allies","Road warden who keeps the waystone lamps lit."),
(1,"greta","Greta Brineknot",C,T,Su,h,"braid, net*","Slow on hit; small Heal and Cleanse on all allies","Saltmarsh net-mender."),
(1,"cole","Cole Ashworth",C,F,A,a,"spiky_hair, axe*","Steady hits; a heavier hit that raises his own ATK","Ashgrave charcoal burner with a woodsman's axe."),
(1,"tilly","Tilly Mothwing",C,S,Su,h,"veil, candle","DefDown on hit; weak AoE with DefDown and TmDrain","Moth-keeper of the Bellspire ruins."),
(1,"ronan","Ronan Stoutbarrow",C,G,H,m,"beard, hammer","Regen on all allies, with a self Shield and Taunt","Thornwick innkeeper, never lets a guest go hungry."),
(1,"isla","Isla Fenlight",C,Li,Su,h,"long_hair, bell","Small Heal and SpdUp on all allies","Saltmarsh chapel bell-ringer."),
(1,"dunstan","Dunstan Oaken",R,G,D,m,"helm, shield, cape","Taunt; DefUp on allies","Old shield-captain of the Thornwick militia."),
(1,"marisol","Marisol Wavecrest",R,T,A,a,"ponytail, trident","AoE hit; Slow","Drowned Mill pearl diver turned raider of the Hollow."),
(1,"vesper","Vesper Gloam",R,S,A,h,"mask, daggers","Poison; DefDown","Thief who works the Hollow Deep's edges."),
(1,"brega","Brega Ironmoss",R,T,D,m,"helm, shield, beard","Shield and Taunt on self; AoE Slow","Mill-lock keeper in barnacled plate."),
(1,"fennick","Fennick Sootpaw",R,F,Su,a,"bandana, candle","Poison on hit and on all enemies; AtkUp and Heal on all allies","Ashgrave firework maker."),
(1,"orla","Orla Greywater",R,T,H,h,"long_hair, staff, robe","Heal and Regen on all allies","Marsh healer who brews from river weed."),
(1,"silas","Silas Penumbra",R,S,D,h,"veil, shield","Shield and Taunt on self; AoE DefDown","Bellspire gatekeeper sworn to the dark."),
(1,"wynn","Wynn Brightfeather",R,Li,A,a,"spiky_hair, bow, quiver","Single big hit; Strip","Falconer of Stormcrown's foothills."),
# Phase 2: Glass and Storm (chapters 6-10 regions; rare/epic)
(2,"kestrel","Kestrel Vane",R,G,A,a,"bandana, spear","Pierce: hits the target and the one behind","Stormcrown lancer."),
(2,"sorrel","Sorrel Duskmere",R,S,H,m,"cape, club","Poison on hit and on all enemies; team Regen with a self Shield","Hollow Deep miner who survived the collapse."),
(2,"amber","Amber Glasswright",R,F,D,h,"helm, shield","Burn on hit; team Shield; self Shield, Taunt and Counter","Glass Wastes kiln-mistress with a fused-glass shield."),
(2,"tobias","Tobias Reed",R,G,Su,a,"long_hair, lute*","SpdUp and Turn Meter for all allies; team Heal and Cleanse","Wandering lute player of the high passes."),
(2,"nerys","Nerys Coldspring",R,T,Su,h,"circlet, mirror","Cleanse, DefUp and Turn Meter for all allies; team Heal","Sunken Cathedral acolyte."),
(2,"hale","Hale Sunforge",R,Li,H,m,"beard, hammer, cape","Small self Shield and Taunt; team Regen","Bellspire smith who forged the waystone lamps."),
(2,"juno","Juno Ashveil",R,F,A,h,"veil, whip, robe","Burn on hit; Burn spreads on a kill","Fire dancer of the Last Pyre."),
(2,"pike","Pike Saltgrave",R,T,A,a,"bandana, hook","Pull: takes the target's turn meter","Smuggler of the Saltmarsh reaches."),
(2,"rhea","Rhea Lumen-Kin",R,Li,D,m,"helm, shield, cape","Shield on self; Cleanse self","Sister of Lumen Varro, sworn to the light-guard."),
(2,"corwin","Corwin Blackthorn",R,S,A,a,"spiky_hair, katana","Execute below 25% HP","Duelist exiled from the Stormcrown court."),
(2,"astrid","Astrid Windrider",E,G,A,a,"ponytail, spear, cape","AoE hit; SpdUp self","Sky-rider of Stormcrown Heights."),
(2,"caspian","Caspian Deepwell",E,T,H,m,"crown, trident, cape","Regen all allies; Shield self","Drowned king's heir, guardian of the Sunken Cathedral."),
(2,"ignis","Ignis Kael",E,F,Su,a,"flame_hair, staff","AtkUp all; Burn-style Poison on enemies","Pyre-priest who tends the eternal flame."),
(2,"lyra","Lyra Starglass",E,Li,Su,h,"halo, mirror, robe","Heal all allies; Cleanse","Glass Wastes oracle who reads the shattered Sigil."),
(2,"malachar","Malachar Gloomhand",E,S,D,m,"helm, shield, cape","Taunt; DefDown on attackers","Hollow knight who broke free of the Hollow."),
(2,"rowan","Rowan Thornheart",E,G,D,m,"beard, shield, cape","Shield all allies; Regen","Warden of the Thornwick old wood."),
(2,"selene","Selene Tidewhisper",E,T,Su,h,"long_hair, orb, robe","TmDrain enemies; Tm boost allies","Moon-priestess of the tides."),
(2,"brann","Brann Emberfist",E,F,H,m,"beard, gauntlets*","Regen on self; counter-style Taunt","Pit fighter of the Ashgrave pass."),
(2,"ysra","Ysra Nightbloom",E,S,A,h,"veil, daggers","Poison all; Stun on crit","Hollow Deep assassin who poisons with flowers."),
(2,"oswin","Oswin Dawnspear",E,Li,A,a,"helm, spear","Pierce; AtkUp self","Light-lancer of the Bellspire order."),
# Phase 3: The Hollow Unbound (redeemed Hollow and Titan-touched; epic/legendary)
(3,"grimsby","Grimsby Boneweaver",R,S,Su,a,"mask, staff","DefDown all; Strip","Hollow bone-reader who sees the Sigil's cracks."),
(3,"marrow","Marrow Cinderhulk",E,F,A,m,"horns, club","Hit that grows with HP lost; AtkUp self","A Hollow giant who woke with fire in his chest and remembers his name."),
(3,"ember","Ember Tallow",R,F,H,h,"candle, robe","Regen all; Heal on death of an ally","Candle-nun of the Last Pyre."),
(3,"quill","Quill Inkwell",R,Li,Su,a,"book*, circlet","Strip enemy buffs; AtkUp ally","Royal archivist who writes the Sigil's history."),
(3,"drift","Drift Coldfoot",C,T,A,a,"bandana, daggers","Slow; double hit","Ice skater of the frozen mill pond."),
(3,"sprig","Sprig Mosslight",C,G,Su,h,"braid, bell","Heal; SpdUp","Child druid of the Thornwick wood."),
(3,"tallow","Tallow Firth",C,F,D,m,"helm, shield","Shield self; Taunt","Ashgrave watchman."),
(3,"hollis","Hollis Shade",C,S,H,h,"veil, club","Regen; Taunt","Gravedigger of Hollow Deep."),
(3,"auren","Auren Halewind",E,G,Su,h,"long_hair, fan*, robe","Cleanse all; SpdUp all","Wind-singer of Stormcrown."),
(3,"cinder","Cinder Vale",E,F,A,a,"flame_hair, sword","Burst AoE; AtkDown","Survivor of the Titan's first eruption."),
(3,"thessaly","Thessaly Reefward",E,T,D,m,"helm, shield, trident","Shield all; Taunt","Captain of the drowned fleet."),
(3,"umbra","Umbra Veilkin",E,S,Su,h,"mask, orb, robe","TmDrain all; DefDown","Hollow seer who serves the living now."),
(3,"lucan","Lucan Brightshield",E,Li,D,m,"helm, shield, cape","Shield all; Cleanse","Paladin of the Sunken Cathedral."),
(3,"kharra","Kharra Titanborn",L,F,H,m,"horns, hammer, cape","Huge HP; Regen; Taunt","Born in the Titan's shadow, half stone."),
(3,"nocturne","Nocturne",L,S,A,h,"mask, katana, cape","Execute; Stun","The Hollow's first champion, now its hunter."),
(3,"aelira","Aelira Tidecrowned",L,T,Su,h,"crown, orb, robe, cape","Heal all; Tm boost all","Queen of the Sunken Cathedral."),
(3,"zephyrine","Zephyrine",L,G,A,a,"ponytail, katana, cape","AoE; SpdUp all","Wind spirit bound to the Sigil's Gale shard."),
(3,"solenne","Solenne Dawnborn",L,Li,H,m,"halo, crown, hammer, cape","Regen all; revive-style Heal","Sun-saint who kept the first Sigil shard."),
# Phase 4: The Sigil Bearers (a new act, chapters 11-15; legendaries and a deep bench)
(4,"wick","Saint Wick of the Lamps",L,F,Su,a,"halo, candle, cape","AtkUp all; Heal all; Cleanse","The urchin who lit the city lamps, grown into the keeper of the Flame shard's hearth."),
(4,"perrin","Perrin Gullsby",C,T,D,a,"bandana, shield","Taunt; DefUp self","Harbour guard of the new act's port city."),
(4,"loam","Loam Bracken",C,G,D,m,"beard, shield","Taunt; Shield self","Farmer who held the gate with a plough-shield."),
(4,"merrit","Merrit Vellum",C,Li,A,a,"spiky_hair, sword","Hit; Strip","Squire to the Sigil Bearers."),
(3,"nell","Nell Shadowgrass",C,S,A,h,"mask, hook","Poison; Slow","Reed-cutter of the black fens."),
(4,"calder","Calder Stormglass",E,Li,Su,m,"beard, lantern","SpdUp all allies; Heal the lowest","Lighthouse keeper whose beam guides ships, and allies, home."),
(4,"sienna","Sienna Firethorn",R,F,A,h,"braid, bow, quiver","Burn-style Poison; AoE","Archer of the ember groves."),
(4,"dorian","Dorian Lowe",R,T,H,m,"beard, anchor*","Regen all; Taunt","Harbourmaster of the port city."),
(4,"ivo","Ivo Brightmantle",R,Li,H,m,"helm, hammer, cape","Heal self; DefUp","Bell-tower guard."),
(4,"saoirse","Saoirse Wraithmoor",R,S,D,h,"veil, shield","Shield; TmDrain attacker","Moor-walker who guards the barrows."),
(4,"gideon","Gideon Flamecaller",E,F,D,m,"helm, shield, cape","Shield all; Burn on attackers","Commander of the Pyre guard."),
(4,"liora","Liora Tidesinger",E,T,A,h,"long_hair, trident","AoE; Slow all","Siren of the storm coast."),
(4,"bastian","Bastian Grey",E,S,H,m,"cape, club","Regen; HP-scaled AoE","Hollow-blooded brawler."),
(4,"elowen","Elowen Brightwater",E,Li,A,a,"ponytail, bow, quiver","Big hit; AtkDown","Ranger who carries a Light shard arrow."),
(4,"faelan","Faelan Galeheart",E,G,H,m,"beard, cape, spear","Regen all; SpdUp self","Stormcrown chieftain."),
(4,"azura","Azura Deepstar",L,T,A,a,"long_hair, trident, cape","AoE; Stun","Bearer of the Tide shard."),
(4,"pyrrhus","Pyrrhus Sunderflame",L,F,D,m,"horns, shield, hammer, cape","Shield all; counter-style Taunt","Bearer of the Flame shard."),
(4,"kaelith","Kaelith Shadowborne",L,S,H,h,"crown, orb, cape, robe","Regen; TmDrain all","Bearer of the Shadow shard."),
(4,"aurex","Aurex Lightsworn",L,Li,A,a,"halo, sword, cape","AoE; Strip; AtkUp self","Bearer of the Light shard."),
(4,"stormbearer","Gale Stormbearer",L,G,D,m,"helm, shield, cape","Shield all; Taunt; SpdUp","Bearer of the Gale shard."),
(4,"vex","Vex Hollowking",L,S,Su,m,"crown, orb, cape","DefDown all; Stun; Strip","The Hollow's king, an uneasy ally at the end."),
(3,"briar","Briar Wildroot",R,G,A,h,"braid, whip","Poison; Slow","Thorn-witch of the deep wood."),
]
cur = dict(rar=collections.Counter({C:6,R:8,E:6,L:4}), el=collections.Counter({F:5,G:6,T:4,Li:5,S:4}), role=collections.Counter({A:9,D:5,H:4,Su:6}))
ids=[x[1] for x in X]; assert len(ids)==len(set(ids)), [i for i in ids if ids.count(i)>1]
tot = lambda k,i: cur[k]+collections.Counter(x[i] for x in X)
print(len(X), "new;", 24+len(X), "total")
for k,i in (("rar",3),("el",4),("role",5)): print(k, dict(tot(k,i)))
for p in (1,2,3,4): print("phase",p, sum(1 for x in X if x[0]==p), dict(collections.Counter(x[3] for x in X if x[0]==p)))
if "--md" in sys.argv:
    out=[]
    for p in (1,2,3,4):
        out.append(f"\n<!--phase{p}-->\n| Id | Name | Rarity | Element | Role | Body | Look and kit (* = new kit piece) | Mechanic hook | Concept |\n|---|---|---|---|---|---|---|---|---|")
        for x in X:
            if x[0]==p: out.append("| " + " | ".join([f"`{x[1]}`", x[2], x[3], x[4], x[5], x[6], x[7], x[8], x[9]]) + " |")
    open(sys.argv[-1],"w").write("\n".join(out)+"\n")
