// Generates godot/tests/fixtures/role-colors.json: each hero's model colours as the WEB game
// computes them (game3d/three3d.mjs roleColors() with three.js r149 colour management), for
// the Godot LookColors parity test.
// Run:  cd godot/tools/models && npm ci && node make-role-colors.mjs
import * as T from "three";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

T.ColorManagement.legacyMode = false; // as three3d.mjs
const here = path.dirname(fileURLToPath(import.meta.url));
const read = (p) => fs.readFileSync(path.resolve(here, "../../..", p), "utf8");
const grab = (src, name) => {
  const i = src.indexOf(`const ${name} = {`);
  let depth = 0;
  const j = src.indexOf("{", i);
  for (let k = j; k < src.length; k++) { if (src[k] === "{") depth++; else if (src[k] === "}") { depth--; if (!depth) return eval("(" + src.slice(j, k + 1) + ")"); } }
  return null;
};
const art = read("public/game/art.js");
const LOOKS = grab(art, "LOOKS"), SKIN = grab(art, "SKIN");
// three3d.mjs helpers and roleColors(), copied verbatim.
const mix = (a, b, k) => "#" + new T.Color(a).lerp(new T.Color(b), k).getHexString();
const mul = (a, k) => "#" + new T.Color(a).multiplyScalar(k).getHexString();
function roleColors(L, hollow) {
  const tint = (c) => hollow ? mix(c, "#2e1f33", 0.5) : c;
  const skin = SKIN[L.skin];
  return {
    armor: tint(L.armor), cloth: tint(L.cloth || mul(L.armor, 0.62)), pants: tint(L.pants || mix(L.cloth || L.armor, "#2a2522", L.cloth ? 0.35 : 0.6)), accent: tint(L.accent),
    trim: tint(L.trim || mix(L.accent, "#c8ccd4", 0.4)), blade: tint("#c8d0dc"), wood: tint("#6b4a29"), boot: tint(L.boot || "#2a1f1a"),
    hair: tint(L.hair), brow: tint(mul(L.hair, 0.8)), hood: tint(L.hood || L.armor), skin: tint(skin), crown: tint(L.accent), eye: hollow ? "#ff2a2a" : L.glow || "#1a141f",
  };
}
// Guard against drift: the copy above must match the web source.
if (!read("game3d/three3d.mjs").includes("pants: tint(L.pants || mix(L.cloth || L.armor, \"#2a2522\", L.cloth ? 0.35 : 0.6))"))
  throw new Error("roleColors() changed in game3d/three3d.mjs: update make-role-colors.mjs and LookColors");
const out = {};
for (const id of Object.keys(LOOKS)) out[id] = { hero: roleColors(LOOKS[id], false), hollow: roleColors(LOOKS[id], true) };
const dest = path.resolve(here, "../../tests/fixtures/role-colors.json");
fs.writeFileSync(dest, JSON.stringify(out, null, 1) + "\n");
console.log("role-colors.json:", Object.keys(out).length, "heroes");
