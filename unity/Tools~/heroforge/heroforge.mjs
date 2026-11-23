#!/usr/bin/env node
// heroforge: the agent-run batch flow for hero art (unity/ART_PIPELINE.md).
//   prompt -> generate (Meshy / Tripo / Rodin API) -> download GLB -> rig + animate (provider API)
//   -> LOD1/LOD2 (meshoptimizer) -> stage with convention names + provenance -> offline validation
//   (Core/Art rules via Tools~/artcheck) -> Unity import tool (batch mode) -> validation report.
//
// Usage (from the repo root, after `npm ci` in unity/Tools~/heroforge):
//   node unity/Tools~/heroforge/heroforge.mjs <plan|generate|rig|lods|stage|validate|import|all> [options]
// Options:
//   --provider meshy|tripo|rodin|local   generator (local = copy the CC0 models, for testing; default local)
//   --heroes id,id,...                   default: all 24
//   --out <dir>                          work folder (default .heroforge-out, git-ignored)
//   --dry-run                            print every API request instead of sending it (no key needed)
//   --proxy-auth                         send a placeholder key; the session proxy injects the saved one
//   --plan-tier free|paid                the owner's plan on that service (recorded; free tiers are refused
//                                        unless --allow-free-tier, see docs/sigilbound/art/ROUTES.md)
//   --rig-with meshy|tripo               rig provider for Rodin models
//   --pose T|A   --polycount N   --unity <Unity editor binary>   --tripo-base <url>
// Keys: MESHY_API_KEY / TRIPO_API_KEY / RODIN_API_KEY in the environment, or --proxy-auth. Never in the repo.
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { ROOT, loadArt, loadGame, heroPrompts, bannedHits } from "./prompts.mjs";

// Node's fetch ignores HTTPS_PROXY unless asked; re-run with it on in proxied sessions.
if (process.env.HTTPS_PROXY && !process.env.NODE_USE_ENV_PROXY) {
  const r = spawnSync(process.execPath, ["--disable-warning=UNDICI-EHPA", ...process.argv.slice(1)], { stdio: "inherit", env: { ...process.env, NODE_USE_ENV_PROXY: "1" } });
  process.exit(r.status ?? 1);
}

const argv = process.argv.slice(2);
const cmd = argv[0];
const opt = (k, d) => { const i = argv.indexOf(`--${k}`); return i > 0 && argv[i + 1] && !argv[i + 1].startsWith("--") ? argv[i + 1] : d; };
const flag = (k) => argv.includes(`--${k}`);
const log = (...a) => console.log(...a);

const art = loadArt(), game = loadGame();
const heroes = (opt("heroes") ? opt("heroes").split(",") : art.heroes.map((h) => h.id)).map((s) => s.trim()).filter(Boolean);
for (const h of heroes) if (!art.heroes.some((x) => x.id === h)) { console.error(`unknown hero: ${h}`); process.exit(2); }
const OUT = path.resolve(ROOT, opt("out", ".heroforge-out"));
const providerName = opt("provider", "local");
const o = { dryRun: flag("dry-run"), proxyAuth: flag("proxy-auth"), pose: opt("pose", "T"), polycount: opt("polycount") ? Number(opt("polycount")) : undefined, tripoBase: opt("tripo-base"), log };
const STATE = path.join(OUT, "state.json");
const state = fs.existsSync(STATE) ? JSON.parse(fs.readFileSync(STATE, "utf8")) : { heroes: {} };
const save = () => { if (!o.dryRun) { fs.mkdirSync(OUT, { recursive: true }); fs.writeFileSync(STATE, JSON.stringify(state, null, 2)); } };
const heroDir = (id) => path.join(OUT, id);

async function provider(name) {
  const mod = await import(`./providers/${name}.mjs`);
  return mod.create(o);
}

function guardTier(p) {
  if (p.info.name === "local") return;
  const tier = opt("plan-tier");
  if (!tier) throw new Error(`--plan-tier free|paid is required for ${p.info.name}: output ownership depends on it (ROUTES.md).`);
  if (tier === "free" && !flag("allow-free-tier"))
    throw new Error(`${p.info.name} free-tier outputs are not owned by us (or need attribution); see ROUTES.md. Use a paid plan, or pass --allow-free-tier for throwaway tests only.`);
}

const steps = {
  async plan() {
    log(`Plan: ${heroes.length} hero(es) with provider ${providerName} into ${path.relative(ROOT, OUT) || "."}`);
    for (const id of heroes) {
      const P = heroPrompts(art, game, id, { pose: o.pose });
      const hits = bannedHits(art, Object.values(P).join(" "));
      log(`\n## ${id}${hits.length ? `  BANNED TERMS: ${hits.join(", ")}` : ""}\n  text3d: ${P.text3d.slice(0, 160)}...\n  files: SB_Hero_${id}_LOD0/1/2.glb, SB_Gear_${id}_Weapon.glb, SB_Anim_${id}_<state>`);
      if (hits.length) process.exitCode = 1;
    }
    if (providerName === "meshy") log(`\nCost estimate (Meshy API pricing seen 2026-11-23, VERIFY): about 20 preview + 10 refine + 5 rig + 3 per clip credits per hero, plus a weapon (about 30): ~${heroes.length * (20 + 10 + 5 + 30)} credits + 3 x clips x heroes.`);
  },
  async generate() {
    const p = await provider(providerName); guardTier(p);
    for (const id of heroes) {
      const s = (state.heroes[id] ||= {});
      if (s.generate && !flag("force")) { log(`${id}: generated already (task ${s.generate.taskId}); --force to redo`); continue; }
      log(`${id}: generating with ${providerName}`);
      const P = heroPrompts(art, game, id, { pose: o.pose });
      s.generate = await p.generate(id, P, heroDir(id));
      if (p.generateProp) s.prop = await p.generateProp(id, P, heroDir(id));
      s.prompt = P.text3d; s.provider = providerName; s.date = new Date().toISOString();
      save();
    }
  },
  async rig() {
    const p = await provider(providerName);
    const rigger = p.rig ? p : opt("rig-with") ? await provider(opt("rig-with")) : null;
    if (!rigger) throw new Error(`${providerName} has no rigging API: pass --rig-with meshy|tripo (needs that key too).`);
    guardTier(rigger);
    for (const id of heroes) {
      const s = state.heroes[id];
      if (!s?.generate && !o.dryRun) { log(`${id}: not generated yet`); continue; }
      if (s?.rig && !flag("force")) { log(`${id}: rigged already`); continue; }
      log(`${id}: rigging with ${rigger.info.name}`);
      const heightM = 2.05; // ArtSpec.BaseHeight; the Unity tool rescales to each hero's build
      const r = await rigger.rig(id, s?.generate ?? { taskId: "dry-run" }, heroDir(id), heightM);
      if (s) { s.rig = r; save(); }
    }
  },
  async lods() {
    const { makeLods } = await import("./lods.mjs");
    for (const id of heroes) {
      const lod0 = path.join(heroDir(id), `SB_Hero_${id}_LOD0.glb`);
      if (!fs.existsSync(lod0)) { log(`${id}: no LOD0 .glb (FBX LODs: let Unity flag them, or convert first)`); continue; }
      const r = await makeLods(lod0);
      for (const [lod, x] of Object.entries(r)) log(`${id}: LOD${lod} ${x.before} -> ${x.after} tris (target ${x.target})`);
    }
  },
  async stage() {
    const inc = path.join(OUT, "incoming");
    fs.mkdirSync(inc, { recursive: true });
    const prov = [];
    for (const id of heroes) {
      const d = heroDir(id);
      if (!fs.existsSync(d)) continue;
      for (const f of fs.readdirSync(d)) if (/^(SB_Hero|SB_Anim|SB_Gear|T_Hero)_/.test(f)) fs.copyFileSync(path.join(d, f), path.join(inc, f));
      const weapon = path.join(d, "raw", "weapon.glb");
      if (fs.existsSync(weapon)) fs.copyFileSync(weapon, path.join(inc, `SB_Gear_${id}_Weapon.glb`));
      const s = state.heroes[id] || {};
      prov.push({ hero: id, provider: s.provider ?? providerName, planTier: opt("plan-tier") ?? (providerName === "local" ? "CC0" : "unknown"), generateTask: s.generate?.taskId, rigTask: s.rig?.taskId, date: s.date, prompt: s.prompt });
    }
    fs.writeFileSync(path.join(inc, "PROVENANCE.json"), JSON.stringify({ note: "Where each hero file came from. Copy into public/game/models/CREDITS.md / the Unity CREDITS when shipping.", files: prov }, null, 2));
    log(`staged ${fs.readdirSync(inc).length - 1} files in ${path.relative(ROOT, inc)}`);
  },
  async validate() {
    const inc = path.join(OUT, "incoming");
    fs.mkdirSync(path.join(OUT, "reports"), { recursive: true });
    const report = path.join(OUT, "reports", "artcheck.md");
    const r = spawnSync("dotnet", ["run", "--project", path.join(ROOT, "unity/Tools~/artcheck"), "--", inc, report], { stdio: "inherit" });
    log(`offline validation: ${r.status === 0 ? "all heroes pass" : r.status === 1 ? "some heroes fail (see report)" : "could not run"} -> ${path.relative(ROOT, report)}`);
    if (r.status !== 0) process.exitCode = r.status ?? 2;
  },
  async import() {
    const unity = opt("unity", process.env.UNITY_PATH);
    const inc = path.join(OUT, "incoming");
    const args = ["-batchmode", "-nographics", "-projectPath", path.join(ROOT, "unity"), "-executeMethod", "Sigilbound.EditorTools.HeroImportPipeline.ImportFromCommandLine", "-sbArtIn", inc, "-logFile", "-"];
    if (!unity || !fs.existsSync(unity)) {
      log(`Unity import: no Unity editor here (set UNITY_PATH or --unity). Run this where Unity 6000.0.58f1 is installed and licensed:\n  <Unity> ${args.join(" ")}\nThe tool writes unity/Assets/Sigilbound/Art/Reports/HeroImport_latest.md and Resources/Heroes/<id>.prefab for heroes that pass.`);
      return;
    }
    const r = spawnSync(unity, args, { stdio: "inherit" });
    log(`Unity import exit ${r.status} (0 = all passed, 1 = some failed validation, 2 = error); report: unity/Assets/Sigilbound/Art/Reports/HeroImport_latest.md`);
    if (r.status !== 0) process.exitCode = r.status ?? 2;
  },
};

const order = ["plan", "generate", "rig", "lods", "stage", "validate", "import"];
try {
  if (cmd === "all") for (const s of order) { log(`\n=== ${s} ===`); await steps[s](); }
  else if (steps[cmd]) await steps[cmd]();
  else { console.log(fs.readFileSync(fileURLToPath(import.meta.url), "utf8").split("\n").filter((l) => l.startsWith("//")).map((l) => l.slice(3)).join("\n")); process.exit(cmd ? 2 : 0); }
} catch (e) { console.error(`heroforge ${cmd}: ${e.message}`); process.exit(2); }
