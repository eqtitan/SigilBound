// Worker for prepare-models.mjs: encodes one high-detail hero texture to KTX2 (Basis Universal) per message.
// The encoder is the Basis Universal WASM build bundled by the ktx2-encoder package (MIT; Basis Universal is
// Apache-2.0), so no native tool is needed on CI. Unity loads the files with KTX for Unity (com.unity.cloud.ktx,
// Apache-2.0), which transcodes them on the phone to ETC2/ASTC (Android) or BC1/BC7 (Windows): GPU-compressed
// in memory instead of the RGBA32 a JPEG decodes to.
//   albedo: ETC1S (supercompressed, about JPEG size), sRGB
//   normal: ETC1S in normal-map mode, linear
//   mask:   built here from the _glow and _mat PNGs (what HeroModels.MaskFromGlow did on the phone), UASTC + zstd, linear
// Every image is stored bottom row first (Y flip), as Texture2D.LoadImage leaves a JPEG, so the UVs stay as they are.
import { parentPort } from "node:worker_threads";
import jpeg from "jpeg-js";
import { PNG } from "pngjs";
import { encodeToKTX2 } from "ktx2-encoder";

function decode(buf) {
  const b = Buffer.from(buf);
  if (b[0] === 0xff && b[1] === 0xd8) { const r = jpeg.decode(b, { useTArray: true, formatAsRGBA: true, maxMemoryUsageInMB: 1024 }); return { width: r.width, height: r.height, data: r.data }; }
  const p = PNG.sync.read(b); return { width: p.width, height: p.height, data: p.data };
}

// Same rule as HeroModels.MaskFromGlow: R metal (or the Titan's ridges, 255 - glow alpha), G 255 (AO), B glow
// brightness ((r*3 + g*6 + b) / 10 * 2), A smoothness (at least 90). The mat map is sampled at the mask's size.
export function buildMask(glow, mat, ridges) {
  const w = glow ? glow.width : mat.width, h = glow ? glow.height : mat.height, out = new Uint8Array(w * h * 4);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const i = (y * w + x) * 4;
    let lum = 0, metal = 0, smooth = 90;
    if (glow) lum = Math.min(255, Math.floor((glow.data[i] * 3 + glow.data[i + 1] * 6 + glow.data[i + 2]) / 10) * 2);
    if (mat) { const j = (Math.floor(y * mat.height / h) * mat.width + Math.floor(x * mat.width / w)) * 4; metal = mat.data[j]; smooth = Math.max(90, mat.data[j + 1]); }
    if (ridges && glow) metal = 255 - glow.data[i + 3];
    out[i] = metal; out[i + 1] = 255; out[i + 2] = lum; out[i + 3] = smooth;
  }
  return { width: w, height: h, data: out };
}

export const SETTINGS = {
  albedo: { isUASTC: false, qualityLevel: 160, compressionLevel: 1, isPerceptual: true, isSetKTX2SRGBTransferFunc: true },
  normal: { isUASTC: false, qualityLevel: 192, compressionLevel: 1, isNormalMap: true, isPerceptual: false, isSetKTX2SRGBTransferFunc: false },
  mask: { isUASTC: true, needSupercompression: true, enableRDO: true, rdoQualityLevel: 1, uastcLDRQualityLevel: 1, isPerceptual: false, isSetKTX2SRGBTransferFunc: false },
};

export async function encodeJob(job) {
  const img = job.kind === "mask"
    ? buildMask(job.glow ? decode(job.glow) : null, job.mat ? decode(job.mat) : null, job.ridges)
    : decode(job.src);
  return encodeToKTX2(new Uint8Array(1), { ...SETTINGS[job.kind], generateMipmap: true, isKTX2File: true, isYFlip: true, imageDecoder: async () => img });
}

if (parentPort) {
  // The WASM encoder prints its progress on stdout; keep the prepare log readable.
  const log = console.log; console.log = () => {}; void log;
  parentPort.on("message", async (job) => {
    try { const out = await encodeJob(job); parentPort.postMessage({ id: job.id, out }, [out.buffer]); }
    catch (e) { parentPort.postMessage({ id: job.id, error: String(e && e.message || e) }); }
  });
}
