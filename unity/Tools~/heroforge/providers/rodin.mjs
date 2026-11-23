// Hyper3D Rodin API adapter: generation only (Rodin has no rigging endpoint), so heroes from Rodin
// are rigged by another provider's rig API (--rig-with meshy|tripo, needs that provider's key too)
// before they reach Unity. Endpoints (/api/v2/rodin, /status, /download) and fields from third-party
// integrations, checked through search results on 2026-11-23; VERIFY against Hyper3D's API docs.
import path from "node:path";
import { Http, poll, findUrls, download } from "./common.mjs";

export const info = {
  name: "rodin", keyEnv: "RODIN_API_KEY", hosts: ["api.hyper3d.com", "(VERIFY: download host)"],
  rigs: false, terms: "https://hyper3d.ai/legal/copyright-license", docs: "https://developer.hyper3d.ai",
};

export function create(opts) {
  const http = new Http({ base: "https://api.hyper3d.com/api/v2", keyEnv: info.keyEnv, proxyAuth: opts.proxyAuth, dryRun: opts.dryRun, log: opts.log });
  const run = async (prompt, label, quality) => {
    const sub = await http.req("POST", "/rodin", {
      prompt, tier: "Regular", mesh_mode: "Raw", quality, geometry_file_format: "glb", material: "Shaded", TAPose: "true", // VERIFY: TAPose, material values
    }, { form: true });
    const key = sub?.jobs?.subscription_key, uuid = sub?.uuid;
    await poll(() => http.req("POST", "/status", { subscription_key: key }), {
      done: (r) => Array.isArray(r?.jobs) && r.jobs.every((j) => j.status === "Done"),
      failed: (r) => Array.isArray(r?.jobs) && r.jobs.some((j) => j.status === "Failed"), label, log: opts.log,
    });
    const list = await http.req("POST", "/download", { task_uuid: uuid });
    return { uuid, list };
  };
  return {
    info,
    async generate(hero, prompts, dir) {
      const { uuid, list } = await run(prompts.text3d, `${hero} model`, "medium");
      const glb = findUrls(list).find((u) => u.ext === ".glb");
      const file = path.join(dir, "raw", "model.glb");
      if (glb || opts.dryRun) await download(glb?.url ?? "https://example.invalid/model.glb", file, opts);
      return { taskId: uuid, files: [file], modelUrl: glb?.url };
    },
    async generateProp(hero, prompts, dir) {
      const { uuid, list } = await run(prompts.weapon3d, `${hero} weapon`, "low");
      const glb = findUrls(list).find((u) => u.ext === ".glb");
      const file = path.join(dir, "raw", "weapon.glb");
      if (glb || opts.dryRun) await download(glb?.url ?? "https://example.invalid/weapon.glb", file, opts);
      return { taskId: uuid, files: [file] };
    },
    rig: null,
  };
}
