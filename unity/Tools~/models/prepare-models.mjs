// Copies the web game's CC0 Quaternius hero models (public/game/models) into the Unity
// project's StreamingAssets, decoding their EXT_meshopt_compression so glTFast can load them
// without the optional meshopt package. KHR_mesh_quantization stays (glTFast supports it).
// Also copies the retargeted UAL clips and the credits file. Run before a Unity build
// (CI does this):  cd unity/Tools~/models && npm ci && node prepare-models.mjs
// Last step: the generated .glb files (hero folders, clip file, Unity-only bodies) are packed and gzipped to
// NAME.glbz. Packing ("SBZ1", Core/ModelPack.cs) byte-transposes each vertex and index buffer so gzip
// compresses it far better; it is lossless and HeroModels.ReadBytes undoes it after unzipping. Before
// that the high-detail heroes' positions are snapped to 1/16384 of the model's size (about 0.1 mm) and
// skin weights to 1/256 steps (summing to exactly 1). Both stay float32 (see compress-hd.mjs for why
// they are never quantized), the change is far below a pixel, and gzip shrinks the result about a
// quarter more than the plain floats (APK size, performance review #6).
// Not a .gz name: Android's packaging silently un-gzips assets named *.gz and ships them full size (build 102).
// Unity stores all StreamingAssets uncompressed in the APK, so this cuts the models to under half their size.
// High-detail hero textures ship as KTX2 (Basis Universal; ktx-worker.mjs), which the phone transcodes to a
// GPU-compressed format, and each hero gets a LOD1 ("SBL1", Core/HdLod.cs: the kept vertices and simplified
// triangles of the shipped mesh) for the Low and Medium tiers and Hollow enemies (PLAN step 121).
import fs from "node:fs";
import os from "node:os";
import zlib from "node:zlib";
import path from "node:path";
import crypto from "node:crypto";
import { Worker } from "node:worker_threads";
import { fileURLToPath } from "node:url";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { MeshoptDecoder, MeshoptSimplifier } from "meshoptimizer";
import { SETTINGS as KTX_SETTINGS } from "./ktx-worker.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../../../public/game/models");
const out = path.resolve(here, "../../Assets/StreamingAssets/Models");
fs.mkdirSync(out, { recursive: true });

await MeshoptDecoder.ready;
await MeshoptSimplifier.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ "meshopt.decoder": MeshoptDecoder });

// High-detail hero: positions snapped to a power-of-two step of about 1/16384 of the model's extent,
// skin weights to multiples of 1/256 with the largest taking the remainder (sum exactly 1; vertices
// with no weights are left alone). Types stay float32; gltf-transform rewrites the accessor min/max.
async function snapHd(file) {
  const doc = await io.read(file);
  return snapDoc(doc);
}
async function snapDoc(doc) {
  for (const mesh of doc.getRoot().listMeshes()) for (const prim of mesh.listPrimitives()) {
    const pos = prim.getAttribute("POSITION");
    if (pos && pos.getComponentType() === 5126) {
      const a = pos.getArray().slice(); let ext = 0;
      for (const v of a) ext = Math.max(ext, Math.abs(v));
      if (ext > 0) {
        const step = 2 ** Math.floor(Math.log2(ext / 16384));
        for (let i = 0; i < a.length; i++) a[i] = Math.round(a[i] / step) * step;
        pos.setArray(a);
      }
    }
    for (const name of prim.listSemantics().filter((n) => n.startsWith("WEIGHTS_"))) {
      const w = prim.getAttribute(name);
      if (w.getComponentType() !== 5126 || w.getElementSize() !== 4) continue;
      const a = w.getArray().slice();
      for (let i = 0; i < a.length; i += 4) {
        const src = [a[i], a[i + 1], a[i + 2], a[i + 3]], sum = src[0] + src[1] + src[2] + src[3];
        if (!(sum > 0.5)) continue;
        const q = src.map((x) => Math.round(x * 256) / 256), big = src.indexOf(Math.max(...src));
        q[big] += 1 - (q[0] + q[1] + q[2] + q[3]);
        for (let k = 0; k < 4; k++) a[i + k] = q[k];
      }
      w.setArray(a);
    }
  }
  return Buffer.from(await io.writeBinary(doc));
}

// LOD1 (performance review #3, PLAN P2-2): meshoptimizer's simplifier on the shipped (snapped) mesh, aiming at
// LOD_RATIO of the triangles within LOD_ERROR of the model's size. It only drops vertices, so every kept vertex
// keeps its exact position, normal, UV and skin weights, and attribute seams (UV islands, hard edges) are kept
// (meshopt's default, no Permissive flag). Shipped as a list of kept vertices plus the new triangles over them
// ("SBL1", read by Core/HdLod.cs), not a second GLB: a full LOD1 GLB per hero would add about 45 MB to the APK,
// this adds about 8. Meshes it can't cut by a fifth (flat-shaded triangle soups, where every edge is a seam) get none.
const LOD_RATIO = 0.42, LOD_ERROR = 0.01, LOD_MAX = 0.8;
function makeLod(doc) {
  const meshes = doc.getRoot().listMeshes();
  if (meshes.length !== 1 || meshes[0].listPrimitives().length !== 1) return null;
  const prim = meshes[0].listPrimitives()[0], pos = prim.getAttribute("POSITION"), ind = prim.getIndices();
  if (prim.getMode() !== 4 || !pos || !ind || !prim.getAttribute("JOINTS_0") || pos.getComponentType() !== 5126) return null;
  const p = pos.getArray(), idx = new Uint32Array(ind.getArray()), nv = pos.getCount();
  const target = Math.floor(idx.length / 3 * LOD_RATIO) * 3;
  const [out] = MeshoptSimplifier.simplify(idx, p, 3, target, LOD_ERROR, []);
  if (out.length > idx.length * LOD_MAX || out.length < 3) return null;
  const used = new Uint8Array(nv); for (const i of out) used[i] = 1;
  const keep = []; for (let i = 0; i < nv; i++) if (used[i]) keep.push(i);
  const remap = new Int32Array(nv).fill(-1); keep.forEach((v, k) => { remap[v] = k; });
  const wide = keep.length > 65535, head = 4 + 4 * 5 + 12 + 36;
  const b = Buffer.alloc(head + keep.length * 4 + out.length * (wide ? 4 : 2));
  b.write("SBL1", 0, "ascii"); b.writeInt32LE(1, 4); b.writeInt32LE(nv, 8); b.writeInt32LE(idx.length, 12);
  b.writeInt32LE(keep.length, 16); b.writeInt32LE(out.length, 20);
  for (let k = 0; k < 3; k++) { b.writeInt32LE(idx[k], 24 + 4 * k); for (let c = 0; c < 3; c++) b.writeFloatLE(p[idx[k] * 3 + c], 36 + 12 * k + 4 * c); }
  let o = head;
  keep.forEach((v, k) => { b.writeInt32LE(k ? v - keep[k - 1] : v, o); o += 4; }); // ascending: deltas gzip to almost nothing
  for (const v of out) { if (wide) { b.writeUInt32LE(remap[v], o); o += 4; } else { b.writeUInt16LE(remap[v], o); o += 2; } }
  return { bytes: zlib.gzipSync(b, { level: 9 }), tris: out.length / 3, hdTris: idx.length / 3, verts: keep.length, hdVerts: nv };
}

// KTX2 textures (ktx-worker.mjs). Encoding all of them takes minutes, so results are cached by a hash of the
// inputs and settings in .ktx-cache (CI keeps it with actions/cache); only new or changed textures are encoded.
const ktxCache = path.resolve(here, ".ktx-cache");
function ktxKey(kind, parts) {
  const h = crypto.createHash("sha1").update(`sb-ktx-1|${kind}|${JSON.stringify(KTX_SETTINGS[kind])}`);
  for (const x of parts) h.update(x == null ? "-" : x);
  return h.digest("hex");
}
async function encodeAll(jobs) {
  fs.mkdirSync(ktxCache, { recursive: true });
  const todo = jobs.filter((j) => !fs.existsSync(path.join(ktxCache, j.key + ".ktx2")));
  if (todo.length) {
    const n = Math.max(1, Math.min(todo.length, os.availableParallelism ? os.availableParallelism() : os.cpus().length));
    console.log(`KTX2: encoding ${todo.length} of ${jobs.length} hero textures on ${n} threads (cached in ${path.relative(process.cwd(), ktxCache)})...`);
    const t0 = Date.now(); let next = 0, done = 0;
    await Promise.all(Array.from({ length: n }, () => new Promise((resolve, reject) => {
      const w = new Worker(new URL("./ktx-worker.mjs", import.meta.url));
      const feed = () => {
        if (next >= todo.length) { w.terminate(); resolve(); return; }
        const j = todo[next++];
        w.once("message", (m) => {
          if (m.error) { w.terminate(); reject(new Error(`KTX2 ${j.name}: ${m.error}`)); return; }
          fs.writeFileSync(path.join(ktxCache, j.key + ".ktx2"), Buffer.from(m.out));
          if (++done % 50 === 0) console.log(`  ${done}/${todo.length}`);
          feed();
        });
        w.postMessage({ id: next, kind: j.kind, src: j.src, glow: j.glow, mat: j.mat, ridges: j.ridges });
      };
      w.on("error", reject);
      feed();
    })));
    console.log(`KTX2: encoded in ${((Date.now() - t0) / 1000).toFixed(0)} s`);
  }
  const keep = new Set(jobs.map((j) => j.key + ".ktx2"));
  for (const f of fs.readdirSync(ktxCache)) if (!keep.has(f)) fs.rmSync(path.join(ktxCache, f)); // stale entries
}

// "SBZ1" container (Core/ModelPack.cs, same region rule as ModelPack.Regions): magic, version 1, region
// count, then per region (offset in the GLB, length, stride) as little-endian int32, then the GLB with each
// region byte-transposed (byte k of element i moves to k * count + i).
function pack(glb) {
  const regions = [];
  if (glb.toString("ascii", 0, 4) === "glTF" && glb.readUInt32LE(16) === 0x4e4f534a) {
    const jsonLen = glb.readUInt32LE(12), js = JSON.parse(glb.toString("utf8", 20, 20 + jsonLen));
    const binStart = 20 + jsonLen + 8, binLen = glb.readUInt32LE(20 + jsonLen);
    const strideOf = new Map(), comp = { 5120: 1, 5121: 1, 5122: 2, 5123: 2 }, num = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT2: 4, MAT3: 9, MAT4: 16 };
    for (const a of js.accessors || []) if (a.bufferView != null && !strideOf.has(a.bufferView)) strideOf.set(a.bufferView, (comp[a.componentType] || 4) * (num[a.type] || 1));
    (js.bufferViews || []).forEach((v, i) => {
      if (!strideOf.has(i) || (v.buffer || 0) !== 0) return;
      const stride = v.byteStride || strideOf.get(i), off = v.byteOffset || 0, count = stride > 0 ? Math.floor(v.byteLength / stride) : 0;
      if (stride < 2 || count < 16 || off + v.byteLength > binLen) return;
      regions.push([binStart + off, count * stride, stride]);
    });
    regions.sort((x, y) => x[0] - y[0]);
  }
  const clean = []; let end = 0;
  for (const r of regions) if (r[0] >= end) { clean.push(r); end = r[0] + r[1]; }
  const head = 12 + clean.length * 12, outp = Buffer.alloc(head + glb.length);
  outp.write("SBZ1", 0, "ascii"); outp.writeInt32LE(1, 4); outp.writeInt32LE(clean.length, 8);
  glb.copy(outp, head);
  clean.forEach(([off, len, stride], r) => {
    outp.writeInt32LE(off, 12 + r * 12); outp.writeInt32LE(len, 16 + r * 12); outp.writeInt32LE(stride, 20 + r * 12);
    const count = len / stride;
    for (let k = 0; k < stride; k++) for (let i = 0, src = off + k, dst = head + off + k * count; i < count; i++, src += stride) outp[dst + i] = glb[src];
  });
  return outp;
}

for (const f of fs.readdirSync(src).filter((n) => n.endsWith(".glb"))) {
  const doc = await io.read(path.join(src, f));
  for (const ext of doc.getRoot().listExtensionsUsed()) {
    if (ext.extensionName === "EXT_meshopt_compression") ext.dispose(); // write plain buffers
  }
  const bytes = await io.writeBinary(doc);
  fs.writeFileSync(path.join(out, f), bytes);
  const anims = doc.getRoot().listAnimations().length, meshes = doc.getRoot().listMeshes().length;
  console.log(`${f}: ${(fs.statSync(path.join(src, f)).size / 1024).toFixed(0)} KB -> ${(bytes.length / 1024).toFixed(0)} KB, ${meshes} meshes, ${anims} clips`);
}
for (const f of ["anims_ual.json", "CREDITS.md"]) fs.copyFileSync(path.join(src, f), path.join(out, f));
// Unity-only CC0 bodies (phase E step 45: Quaternius Ultimate Modular Women), plain GLBs in godot/models.
const extra = path.resolve(here, "../../../godot/models");
for (const f of fs.readdirSync(extra).filter((n) => /^women_.*\.glb$/.test(n))) {
  fs.copyFileSync(path.join(extra, f), path.join(out, f));
  console.log(`${f}: copied (Unity-only body)`);
}
// Free-path heroes baked by unity/Tools~/blenderforge/quat.py (SB_Hero_<id>_LOD0.glb + _albedo/_glow
// PNGs): copied as they are, with an index the game reads (HeroModels) to know which exist.
const heroesSrc = path.resolve(here, "heroes");
if (fs.existsSync(heroesSrc)) {
  const heroesOut = path.join(out, "heroes");
  fs.mkdirSync(heroesOut, { recursive: true });
  const ids = [];
  for (const f of fs.readdirSync(heroesSrc)) {
    fs.copyFileSync(path.join(heroesSrc, f), path.join(heroesOut, f));
    const m = /^SB_Hero_(.+)_LOD0\.glb$/.exec(f);
    if (m) ids.push(m[1]);
  }
  fs.writeFileSync(path.join(heroesOut, "index.txt"), ids.join("\n") + "\n");
  console.log(`Baked heroes: ${ids.join(", ") || "none"}`);
}
// High-detail heroes (phase F, blenderforge/ubc.py) and the retargeted UAL clips for their rig.
const hdSrc = path.resolve(here, "heroes_hd");
if (fs.existsSync(hdSrc)) {
  const hdOut = path.join(out, "heroes_hd");
  fs.mkdirSync(hdOut, { recursive: true });
  let titanId = "titan";
  try { titanId = JSON.parse(fs.readFileSync(path.join(hdSrc, "titan.json"), "utf8")).model || titanId; } catch { /* default */ }
  const ids = [], lod1 = [], texOf = new Map(), lodStat = { tris: 0, hdTris: 0, bytes: 0, none: [] };
  const TEX = /^SB_Hero_(.+)_HD_(albedo|normal|glow|mat)\.(jpg|png)$/;
  for (const f of fs.readdirSync(hdSrc).sort()) {
    const t = TEX.exec(f);
    if (t) { // textures: encoded to KTX2 below, not copied
      const e = texOf.get(t[1]) || {}; if (!e[t[2]] || t[3] === "jpg") e[t[2]] = path.join(hdSrc, f); texOf.set(t[1], e);
      continue;
    }
    const m = /^SB_Hero_(.+)_HD\.glb$/.exec(f);
    if (m) {
      const doc = await io.read(path.join(hdSrc, f));
      fs.writeFileSync(path.join(hdOut, f), await snapDoc(doc));
      ids.push(m[1]);
      if (m[1] !== titanId) { // the Titan is one big boss on screen: always the full mesh
        const lod = makeLod(doc);
        if (lod) {
          fs.writeFileSync(path.join(hdOut, `SB_Hero_${m[1]}_HD_LOD1.sbl`), lod.bytes); lod1.push(m[1]);
          lodStat.tris += lod.tris; lodStat.hdTris += lod.hdTris; lodStat.bytes += lod.bytes.length;
        } else lodStat.none.push(m[1]);
      }
    } else fs.copyFileSync(path.join(hdSrc, f), path.join(hdOut, f));
  }
  fs.writeFileSync(path.join(hdOut, "index.txt"), ids.join("\n") + "\n");
  // Heroes with a LOD1 (SB_Hero_<id>_HD_LOD1.sbl): the game only asks for the files listed here (HeroModels.LoadHdIndex).
  fs.writeFileSync(path.join(hdOut, "index_lod1.txt"), lod1.join("\n") + (lod1.length ? "\n" : ""));
  console.log(`High-detail heroes: ${ids.join(", ") || "none"}`);
  console.log(`LOD1: ${lod1.length} heroes, ${(100 * lodStat.tris / Math.max(1, lodStat.hdTris)).toFixed(0)}% of their triangles, ${(lodStat.bytes / 1048576).toFixed(1)} MB; full mesh only: ${lodStat.none.join(", ") || "none"}`);
  // Textures: one KTX2 per map. index_ktx.txt lists "<id> <maps>" (a albedo, n normal, m mask) so the game asks for no missing file.
  const jobs = [], lines = [];
  const read = (p) => (p ? fs.readFileSync(p) : null);
  for (const id of ids) {
    const e = texOf.get(id); if (!e || !e.albedo) continue;
    const mine = [];
    const add = (kind, map, parts, job) => { const key = ktxKey(kind, parts); jobs.push({ key, kind, name: `${id} ${map}`, ...job }); mine.push({ key, map }); };
    const alb = read(e.albedo); add("albedo", "albedo", [alb], { src: alb });
    if (e.normal) { const n = read(e.normal); add("normal", "normal", [n], { src: n }); }
    if (e.glow || e.mat) { const g = read(e.glow), mt = read(e.mat), ridges = id === titanId; add("mask", "mask", [g, mt, ridges ? "ridges" : ""], { glow: g, mat: mt, ridges }); }
    lines.push({ id, mine });
  }
  await encodeAll(jobs);
  let ktxBytes = 0;
  for (const { id, mine } of lines) {
    for (const { key, map } of mine) { const dst = path.join(hdOut, `SB_Hero_${id}_HD_${map}.ktx2`); fs.copyFileSync(path.join(ktxCache, key + ".ktx2"), dst); ktxBytes += fs.statSync(dst).size; }
  }
  fs.writeFileSync(path.join(hdOut, "index_ktx.txt"), lines.map(({ id, mine }) => `${id} ${mine.map((x) => x.map[0]).join("")}`).join("\n") + "\n");
  let srcBytes = 0; for (const e of texOf.values()) for (const p of Object.values(e)) srcBytes += fs.statSync(p).size;
  console.log(`KTX2 hero textures: ${jobs.length} files, ${(ktxBytes / 1048576).toFixed(1)} MB (the JPEG/PNG sources: ${(srcBytes / 1048576).toFixed(1)} MB)`);
}
if (fs.existsSync(path.resolve(here, "ubc_anims.glb"))) fs.copyFileSync(path.resolve(here, "ubc_anims.glb"), path.join(out, "ubc_anims.glb"));
// Gzip the generated GLBs (see the header): the hero folders and the clip file, almost all of the size.
// The four small base models committed in StreamingAssets/Models stay plain (no tracked files change).
// Plain copies are removed so only one form ships.
let before = 0, after = 0;
function gz(p) {
  if (!fs.existsSync(p)) return;
  if (fs.statSync(p).isDirectory()) { for (const n of fs.readdirSync(p)) gz(path.join(p, n)); return; }
  if (!p.endsWith(".glb")) return;
  // Packed unless the plain GLB gzips smaller (image-to-3D meshes do: their duplicated seam vertices are long
  // repeats that the byte transpose breaks up); ModelPack.Unpack passes a plain GLB through.
  const raw = fs.readFileSync(p), pk = zlib.gzipSync(pack(raw), { level: 9 }), pl = zlib.gzipSync(raw, { level: 9 });
  const packed = pk.length <= pl.length ? pk : pl;
  fs.writeFileSync(p + "z", packed); fs.rmSync(p);
  before += raw.length; after += packed.length;
}
for (const n of ["heroes_hd", "heroes", "ubc_anims.glb", ...fs.readdirSync(out).filter((f) => /^women_.*\.glb$/.test(f))]) gz(path.join(out, n));
console.log(`GLBs gzipped: ${(before / 1048576).toFixed(1)} MB -> ${(after / 1048576).toFixed(1)} MB`);
console.log(`Models ready in ${path.relative(process.cwd(), out)}`);
