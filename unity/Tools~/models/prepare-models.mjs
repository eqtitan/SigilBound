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
import fs from "node:fs";
import zlib from "node:zlib";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { MeshoptDecoder } from "meshoptimizer";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../../../public/game/models");
const out = path.resolve(here, "../../Assets/StreamingAssets/Models");
fs.mkdirSync(out, { recursive: true });

await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ "meshopt.decoder": MeshoptDecoder });

// High-detail hero: positions snapped to a power-of-two step of about 1/16384 of the model's extent,
// skin weights to multiples of 1/256 with the largest taking the remainder (sum exactly 1; vertices
// with no weights are left alone). Types stay float32; gltf-transform rewrites the accessor min/max.
async function snapHd(file) {
  const doc = await io.read(file);
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
  const ids = [], lod1 = [];
  for (const f of fs.readdirSync(hdSrc)) {
    if (/_HD(_LOD1)?\.glb$/.test(f)) fs.writeFileSync(path.join(hdOut, f), await snapHd(path.join(hdSrc, f)));
    else fs.copyFileSync(path.join(hdSrc, f), path.join(hdOut, f));
    const m = /^SB_Hero_(.+)_HD\.glb$/.exec(f);
    if (m) ids.push(m[1]);
    const l = /^SB_Hero_(.+)_HD_LOD1\.glb$/.exec(f);
    if (l) lod1.push(l[1]);
  }
  fs.writeFileSync(path.join(hdOut, "index.txt"), ids.join("\n") + "\n");
  // Heroes with a Low-tier LOD1 mesh: the game only asks for LOD1 files listed here (HeroModels.LoadHdIndex).
  fs.writeFileSync(path.join(hdOut, "index_lod1.txt"), lod1.join("\n") + (lod1.length ? "\n" : ""));
  console.log(`High-detail heroes: ${ids.join(", ") || "none"}`);
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
