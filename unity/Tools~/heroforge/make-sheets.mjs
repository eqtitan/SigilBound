// Writes the 24 hero sheets (docs/sigilbound/art/heroes/<id>.md) and their index from
// hero-prompts.json and the web game's data (skills, element, rarity, role, colours), so every
// sheet's animation list matches the skills the battle code actually plays.
//   node unity/Tools~/heroforge/make-sheets.mjs
import fs from "node:fs";
import path from "node:path";
import { ROOT, loadArt, loadGame, heroPrompts, skillKind, bannedHits } from "./prompts.mjs";

const OUT = path.join(ROOT, "docs/sigilbound/art/heroes");
const art = loadArt();
const game = loadGame();
const RANGED = ["staff", "orb", "bell", "lantern", "candle", "mirror", "bow"]; // Core.Looks.Ranged

const SHAPE = {
  attack: "Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.",
  defense: "Squares and rectangles: wide shoulders, low centre of gravity, symmetrical front, the shield or armour reads first.",
  hp: "Circles and mass: round, heavy forms, thick limbs, soft big shapes that look hard to knock down.",
  support: "Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.",
};
const RARITY = {
  common: "Common: practical matte materials (linen, leather, iron), one accent colour, no glow, no cape.",
  rare: "Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.",
  epic: "Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.",
  legendary: "Legendary: a silhouette-breaker above the head (crown, halo, flame, wings), animated glow on large areas, one signature material (molten, starlit glass, sunmetal).",
};
const ATTACKS = {
  Blade: ["diagonal slash, step in", "backhand slash", "short thrust or front kick"],
  Polearm: ["two-handed thrust at full reach", "sweeping haft strike", "spinning overhead chop"],
  Brawler: ["right hook or club swing", "left jab or backhand", "heavy kick or shoulder barge"],
  Daggers: ["crossing double slash", "reverse-grip stab", "spinning kick"],
  ShieldBlade: ["sword slash from behind the shield", "shield bash", "shield-up thrust"],
  Heavy: ["overhead two-handed slam", "sideways sweep", "punch with the free hand"],
  Caster: ["prop swing (melee fallback, rarely used)", "-", "-"],
  Archer: ["bow strike (melee fallback, rarely used)", "-", "-"],
};
const rel = (f) => path.relative(OUT, path.join(ROOT, f)).replace(/\\/g, "/");

function sheet(h) {
  const g = game.HEROES.find((x) => x.id === h.id);
  const L = game.LOOKS[h.id];
  const caster = RANGED.includes(L.prop);
  const P = heroPrompts(art, game, h.id);
  const PA = heroPrompts(art, game, h.id, { pose: "A" });
  const hits = bannedHits(art, Object.values(P).join(" "));
  if (hits.length) throw new Error(`${h.id}: banned terms in prompts: ${hits}`);
  const sw = (c) => (c ? `\`${c.split(" ")[0]}\` ${c.split(" ").slice(1).join(" ")}` : "none");
  const states = new Set(["Idle", "Run", "Attack_1", "Cast", "Hit", "Death", "Victory"]);
  const byState = {};
  const skillRows = g.skills.map((s, i) => {
    let k = skillKind(s, i, caster);
    if (k === "Attack") k = "Attack_1-3 (run-in, strike, run back)";
    else { states.add(k); (byState[k] = byState[k] || []).push(s.name); }
    const tgt = { enemy: "one enemy", allEnemies: "all enemies", ally: "one ally", allAllies: "all allies", self: "self" }[s.target];
    return `| ${i + 1}. ${s.name} | ${tgt}${s.hits > 1 ? `, ${s.hits} hits` : ""}${s.mult === 0 ? ", no damage" : ""} | ${k} |`;
  });
  const usesSupport = states.has("Cast_Support");
  const a = ATTACKS[h.archetype];
  const anim = [
    ["Idle", "loop", "always", `${caster ? "prop held up near the chest, light sway" : "weapon ready, weight shifting"}; matches the personality: ${h.personality.split(";")[0]}`],
    ["Idle_Fidget", "once", "optional", "short look-around or prop twirl (web plays one every 8-20 s)"],
    ["Run", "loop", caster ? "rarely (casters cast in place)" : "melee run-in, 0.26 s", "in place, root locked; the game moves the unit"],
    ["Run_Back", "loop", caster ? "rarely" : "backpedal home, 0.25 s", "falls back to Run"],
    ["Attack_1", "once", caster ? "melee fallback only" : skillRows.filter((r) => r.includes("Attack_1")).map((r) => r.split("|")[1].replace(/^ \d\. /, "").trim()).join(", ") || "-", a[0]],
    ["Attack_2", "once", caster ? "-" : "strike rotation", a[1]],
    ["Attack_3", "once", caster ? "-" : "strike rotation", a[2]],
    ["Cast", "once", byState.Cast ? byState.Cast.join(", ") : "fallback for the casts below", caster ? `${L.prop} thrust forward, release at about 0.3 s` : "weapon raised, a short charge, release"],
    ["Cast_Ult", "once", byState.Cast_Ult ? `${byState.Cast_Ult.join(", ")} (camera zooms in)` : "not used by this hero's skills (falls back to Cast)", byState.Cast_Ult ? `the big moment: ${h.skillsLook.split(";").find((x) => byState.Cast_Ult.some((n) => x.includes(n)))?.trim() ?? h.skillsLook.split(";").slice(-1)[0]}`.trim() : "-"],
    ["Cast_Support", "once", usesSupport ? byState.Cast_Support.join(", ") : "not used by this hero's skills (falls back to Cast)", usesSupport ? "prop or weapon raised high, open chest, upward motion" : "-"],
    ["Hit", "once", "taking damage", "small flinch, back to idle in about 0.4 s"],
    ["Hit_Big", "once", "crit or a hit over 14% of max HP", "bigger stagger, one step back"],
    ["Dodge", "once", "glancing blow, 35% chance", "short side-step or roll; skipped if missing"],
    ["Death", "hold last frame", "HP reaches 0", "falls backward or kneels then collapses; the unit then sinks"],
    ["Victory", "loop", "after the last wave", `a short pose in character (${h.personality.split(";")[0]}) then a loopable hold`],
  ];
  const rarity = g.rarity, role = g.role;
  const norm = (x) => x.toLowerCase().replace(/[^a-z]/g, "");
  return `# ${g.name}${norm(g.name).includes(norm(h.epithet.replace(/^(the|of the)\s+/i, ""))) ? "" : `, ${h.epithet}`}

| | |
|---|---|
| Element | ${game.ELEMENTS[g.el].name} (${art.elementWords[g.el]}) |
| Rarity | ${game.RARITY[rarity].name} |
| Role | ${game.ROLES[role].name} |
| Animation archetype | ${h.archetype} (\`SB_Anim_${h.archetype}_<state>\`) |
| Build | ${h.build} |
| Files | \`SB_Hero_${h.id}_LOD0.glb\` (LOD1, LOD2), \`T_Hero_${h.id}_Albedo/Normal/Mask.png\`, \`SB_Gear_${h.id}_Weapon.glb\` |

## Concept

- **Silhouette:** ${h.silhouette}.
- **Outfit:** ${h.outfit}.
- **Weapon / focus:** ${h.weapon}.
- **Colours:** main ${sw(h.palette.main)}, accent ${sw(h.palette.accent)}, glow ${sw(h.palette.glow)}, skin ${h.palette.skin}, hair ${sw(h.palette.hair)}. Element rim: ${game.ELEMENTS[g.el].color} (added by the shader, don't paint it in).
- **Personality cues:** ${h.personality}.
- **Skills on screen:** ${h.skillsLook}.
- **Shape language (${game.ROLES[role].name}):** ${SHAPE[role]}
- **Rarity finish:** ${RARITY[rarity]}
- **Phone check:** at 70 px tall the read is ${h.silhouette.split(":")[0]}; one main colour, one accent, the ${caster ? "focus" : "weapon"} visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, ${caster ? "focus" : "weapon"}, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (${h.silhouette.split(":")[1]?.split(",")[0]?.trim() ?? "headgear, weapon"}); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: ${h.secondary} |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored \`Socket_*\` nodes win):
${h.attachments.map((x) => `- ${x}`).join("\n")}
- Gear rarity variants (optional): \`SB_Gear_${h.id}_Helm_r3.glb\` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from \`public/game/data.js\`; ${caster ? "a ranged hero: every attack is cast in place" : "a melee hero: single-target damage runs in and strikes"}):

| Skill | Target | Plays |
|---|---|---|
${skillRows.join("\n")}

| State | Loop | When the game plays it for ${g.name.split(" ")[0].replace(",", "")} | Direction |
|---|---|---|---|
${anim.map((r) => `| ${r[0]} | ${r[1]} | ${r[2]} | ${r[3]} |`).join("\n")}

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see \`Core/Art/ArtSpec.cs\`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

\`\`\`text
${P.concept2d}
\`\`\`

Negative: \`${P.negative2d}\`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

\`\`\`text
${P.text3d}
\`\`\`

Negative:

\`\`\`text
${P.negative3d}
\`\`\`

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

\`\`\`text
${P.weapon3d}
\`\`\`

**c) Texture style** (refine / texturing step):

\`\`\`text
${P.texture}
\`\`\`
`;
}

const index = [];
for (const h of art.heroes) {
  fs.writeFileSync(path.join(OUT, `${h.id}.md`), sheet(h));
  const g = game.HEROES.find((x) => x.id === h.id);
  index.push(`| [${g.name}](${h.id}.md) | ${game.ELEMENTS[g.el].name} | ${game.RARITY[g.rarity].name} | ${game.ROLES[g.role].name} | ${h.archetype} | ${h.silhouette.split(":")[0]} |`);
}
fs.writeFileSync(path.join(OUT, "README.md"), `# Hero sheets

One sheet per hero: concept, model and LOD notes, attachments, the animations the battle code plays
for that hero, and ready-to-paste prompts. Generated from [hero-prompts.json](hero-prompts.json) and
the web game's data by \`node unity/Tools~/heroforge/make-sheets.mjs\`; edit the JSON, then regenerate.
The visual rules are in [../ART_BIBLE.md](../ART_BIBLE.md); enemies in [enemies.md](enemies.md).

| Hero | Element | Rarity | Role | Archetype | Silhouette read |
|---|---|---|---|---|---|
${index.join("\n")}
`);
console.log(`wrote ${art.heroes.length} sheets and README.md to ${path.relative(ROOT, OUT)}`);
