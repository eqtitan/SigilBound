// Builds every generation prompt from docs/sigilbound/art/heroes/hero-prompts.json and the web
// game's own data (public/game/data.js: name, element, rarity, role, skills). Shared by
// make-sheets.mjs (the hero sheets) and heroforge.mjs (the API batch), so the sheets show exactly
// what the batch sends.
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
export const PROMPTS_FILE = path.join(ROOT, "docs/sigilbound/art/heroes/hero-prompts.json");

// The web game's data.js and art.js are browser scripts: run them in a sandbox and pick the tables.
export function loadGame() {
  const ctx = { console, Object, Array, Math, JSON, window: {} };
  vm.createContext(ctx);
  const src = ["data.js", "art.js"].map((f) => fs.readFileSync(path.join(ROOT, "public/game", f), "utf8")).join("\n;\n");
  vm.runInContext(src + "\n;this.__out = { HEROES, ELEMENTS, RARITY, ROLES, LOOKS };", ctx);
  return ctx.__out;
}

export function loadArt() { return JSON.parse(fs.readFileSync(PROMPTS_FILE, "utf8")); }

const fill = (t, vars) => t.replace(/\{(\w+)\}/g, (_, k) => (vars[k] ?? ""));
const words = (c) => (c || "").replace(/^#[0-9a-f]{6}\s*/i, "").trim();

// The battle state a skill plays, as BattleController.DoAct decides it: melee heroes (no ranged
// focus) run in and strike for single-target damage; otherwise no damage = support cast, third skill
// or all-enemies = ultimate cast, else a plain cast.
export function skillKind(s, i, caster) {
  if (s.mult > 0 && !caster && s.target === "enemy") return "Attack";
  if (s.mult === 0) return "Cast_Support";
  if (i === 2 || s.target === "allEnemies") return "Cast_Ult";
  return "Cast";
}

export function heroPrompts(art, game, id, opts = {}) {
  const h = art.heroes.find((x) => x.id === id);
  const g = game.HEROES.find((x) => x.id === id);
  if (!h || !g) throw new Error(`unknown hero ${id}`);
  const p = h.palette;
  const vars = {
    subject: h.subject, weapon: h.weapon, main: words(p.main), accent: words(p.accent),
    glowText: p.glow ? `, glowing accents in ${words(p.glow)}` : "",
    elementWords: art.elementWords[g.el], textureNotes: h.textureNotes,
    pose: opts.pose === "A" ? "A-pose" : "T-pose",
  };
  const t = art.templates;
  return {
    concept2d: fill(t.concept2d, vars),
    negative2d: t.negative2d,
    text3d: fill(t.text3d, vars),
    weapon3d: fill(t.weapon3d, vars),
    negative3d: t.negative3d,
    texture: fill(t.texture, vars),
  };
}

// Same check as the core tests: no other game's, studio's or artist's name in what we send.
export function bannedHits(art, text) {
  const low = text.toLowerCase();
  return art.bannedTerms.filter((b) => low.includes(b.toLowerCase()));
}
