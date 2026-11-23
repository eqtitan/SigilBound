// Generates parity.json from the WEB game's own code (public/game/data.js, state.js, battle.js),
// so the Godot demo's tests can check that the GDScript port computes the same numbers:
// hero data, hero stats (with gear and set bonuses), XP, stage enemies and rewards, and
// whole auto battles turn by turn.
// Run from the repo root after changing game rules on the web side:
//   node godot/tests/make-fixtures.cjs
// The clock is frozen and Math.random is replaced by a seeded Park-Miller generator
// (seed = seed * 16807 % 2147483647, exact in doubles), which the GDScript tests replay.
"use strict";
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "../..");
const game = (f) => fs.readFileSync(path.join(root, "public/game", f), "utf8");

const FIXED_NOW = Date.parse("2026-11-23T12:00:00Z");
class FixedDate extends Date {
  constructor(...a) { if (a.length) super(...a); else super(FIXED_NOW); }
  static now() { return FIXED_NOW; }
}
let seed = 1;
const rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647; };
const reseed = (s) => { seed = s; };
const math = Object.create(Math); math.random = rnd;

const store = {};
const ctx = {
  console, Date: FixedDate, Math: math, JSON, Object, Array, String, Number, Set, Map, Promise, Infinity, NaN, isFinite, parseInt, parseFloat,
  encodeURIComponent, decodeURIComponent, escape, unescape,
  btoa: (s) => Buffer.from(s, "binary").toString("base64"), atob: (s) => Buffer.from(s, "base64").toString("binary"),
  localStorage: { getItem: (k) => store[k] ?? null, setItem: (k, v) => { store[k] = v; } },
  setTimeout: () => 0, clearTimeout: () => {}, document: { getElementById: () => null },
};
ctx.window = ctx;
ctx.window.SB_VERSION = { version: "fixture", build: "fixture", commit: "", date: "" };
vm.createContext(ctx);
const src = [game("data.js"), game("state.js"), game("battle.js"),
  `globalThis.__sb = { get G() { return G; }, newState, HEROES, HERO, STARTERS, ELEMENTS, RARITY, ROLES, EFFECTS, ENEMY_CURVE, CHAPTERS,
     GEAR_SLOTS, GEAR_SETS, SUBSTATS, addHero, makeGear, equip, heroStats, power, giveXp, xpToNext, stageWaves, enemyGrade,
     stageRewards, stageLevel, unitFromHero, unitFromEnemy, Battle, TOTAL_STAGES };`].join("\n;\n");
vm.runInContext(src, ctx, { filename: "web-game.js" });
const W = ctx.__sb;
const G = W.G;
const clone = (o) => JSON.parse(JSON.stringify(o));

// ---------- hero stats: every hero at a few levels/stars, some with gear ----------
reseed(20261123);
const heroStats = [];
W.HEROES.forEach((t, i) => {
  const h = W.addHero(t.id);
  h.level = 1 + (i * 7) % 30;
  h.stars = Math.min(6, W.RARITY[t.rarity].stars + (i % 3));
  // Gear on two thirds of the heroes: up to four pieces of mixed rarity and level, so set
  // bonuses (2-piece) and percentage/flat substats are all exercised.
  const pieces = i % 3 === 2 ? 0 : 1 + (i % 4);
  ["weapon", "helm", "chest", "boots"].slice(0, pieces).forEach((slot, k) => {
    const g = W.makeGear(1 + ((i + k) % 5), slot);
    g.level = (i * 3 + k) % 11;
    if (k === 1 && i % 2 === 0) g.set = G.gear.find((x) => x.uid === h.gear.weapon).set; // force a 2-piece set
    W.equip(h, g);
  });
  const gear = Object.values(h.gear).map((u) => clone(G.gear.find((g) => g.uid === u)));
  const st = W.heroStats(h);
  heroStats.push({ id: t.id, level: h.level, stars: h.stars, gear, stats: st, power: W.power(st) });
});

// ---------- XP ----------
const xp = [];
for (const [lvl, stars, amount] of [[1, 1, 100], [1, 2, 5000], [5, 1, 99999], [9, 1, 10], [3, 3, 1234], [19, 2, 800]]) {
  const h = { level: lvl, xp: 0, stars };
  const ups = W.giveXp(h, amount);
  xp.push({ level: lvl, stars, amount, ups, after: { level: h.level, xp: h.xp } });
}

// ---------- stages ----------
const stages = [0, 1, 2, 3, 4, 5, 6, 17, 23, 41, 59].map((idx) => ({
  idx, level: W.stageLevel(idx), grade: W.enemyGrade(idx), rewardsFirst: W.stageRewards(idx, true), rewards: W.stageRewards(idx, false),
  waves: W.stageWaves(idx).map((w) => w.map((e) => {
    const u = W.unitFromEnemy(e);
    return { id: e.template.id, el: e.el, boss: !!e.boss, shownLevel: e.shownLevel, name: u.name, hp: u.maxHp, atk: u.atk, def: u.def, spd: u.spd, crit: u.crit, cdmg: u.cdmg };
  })),
}));

// ---------- deterministic auto battles (the same engine calls ui.js loop() makes) ----------
function starterTeam(starter, level) {
  G.heroes = []; G.gear = []; G.nextUid = 1;
  const team = [W.addHero(starter), W.addHero("pell"), W.addHero("odo")];
  const gear = [];
  team.forEach((h, k) => {
    h.level = level;
    const g = W.makeGear(1, "weapon"); g.set = ["fury", "vigor", "keen"][k];
    W.equip(h, g); gear.push(clone(g));
  });
  return { team, gear };
}
function runBattle(starter, level, idx, battleSeed) {
  const { team, gear } = starterTeam(starter, level);
  const heroes = team.map((h) => ({ uid: h.uid, id: h.id, level: h.level, stars: h.stars, gear: clone(h.gear) }));
  reseed(battleSeed);
  W.Battle._n = 0;
  const allies = team.map(W.unitFromHero);
  const b = new W.Battle(allies, W.stageWaves(idx).map((w) => w.map(W.unitFromEnemy)));
  const start = b.allies.concat(...b.waves).map((u) => ({ key: u.key, tm: u.tm, hp: u.maxHp }));
  const turns = [];
  for (let guard = 0; guard < 400 && !b.over; guard++) {
    const u = b.nextActor();
    if (!u) break;
    const t = { actor: u.key };
    const begin = b.beginTurn(u);
    t.begin = begin.events;
    if (!begin.skip) {
      const choice = b.aiChoose(u);
      t.skill = choice.skill; t.target = choice.target;
      t.events = b.act(u, choice.skill, choice.target);
    } else t.skip = true;
    t.end = b.endTurn(u);
    t.hp = b.units.map((x) => x.hp);
    turns.push(t);
  }
  return { starter, level, idx, seed: battleSeed, heroes, gear, start, turns, result: b.result, wave: b.wave };
}
// Required as a module (e.g. a balance simulation): expose the battle runner, write nothing.
if (require.main !== module) { module.exports = { runBattle }; return; }
const battles = [
  runBattle("ysolde", 1, 0, 42), runBattle("hask", 1, 1, 7), runBattle("corvan", 6, 2, 99),
  runBattle("ysolde", 9, 3, 1234), runBattle("hask", 10, 4, 2026), runBattle("corvan", 10, 5, 31337),
  runBattle("ysolde", 2, 5, 5), // a likely defeat
];

const out = {
  note: "Generated by godot/tests/make-fixtures.cjs from public/game/{data,state,battle}.js. Do not edit by hand.",
  data: {
    heroes: W.HEROES.map((h) => ({ id: h.id, name: h.name, rarity: h.rarity, el: h.el, role: h.role, face: h.face, skills: h.skills })),
    starters: W.STARTERS, curve: W.ENEMY_CURVE, chapters: W.CHAPTERS, roles: W.ROLES, rarity: W.RARITY,
    elements: Object.fromEntries(Object.entries(W.ELEMENTS).map(([k, e]) => [k, { name: e.name, color: e.color, strong: e.strong }])),
    effects: Object.fromEntries(Object.entries(W.EFFECTS).map(([k, e]) => [k, { name: e.name, buff: e.buff }])),
    gearSlots: Object.fromEntries(Object.entries(W.GEAR_SLOTS).map(([k, s]) => [k, { stat: s.stat, base: s.base }])),
    gearSets: Object.fromEntries(Object.entries(W.GEAR_SETS).map(([k, s]) => [k, { stat: s.stat, val: s.val }])),
  },
  heroStats, xp, stages, battles,
};
const dest = path.join(__dirname, "fixtures", "parity.json");
fs.mkdirSync(path.dirname(dest), { recursive: true });
fs.writeFileSync(dest, JSON.stringify(out) + "\n");
console.log(`parity.json: ${heroStats.length} hero stats, ${stages.length} stages, ${battles.length} battles (${battles.map((b) => `${b.result} in ${b.turns.length}`).join(", ")})`);
