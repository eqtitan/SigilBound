// Meshy API adapter: text-to-3D (preview, then refine for textures), auto-rigging and animation
// library. Endpoints from docs.meshy.ai (API reference: text-to-3d, rigging, animation, pricing),
// checked through search results on 2026-11-23; field names marked VERIFY could not be read from
// the docs pages themselves (the docs host was not reachable from the prep session). The adapter
// finds output URLs by file extension, so a renamed response field doesn't break downloads.
import fs from "node:fs";
import path from "node:path";
import { Http, poll, findUrls, download } from "./common.mjs";

export const info = {
  name: "meshy", keyEnv: "MESHY_API_KEY", hosts: ["api.meshy.ai", "assets.meshy.ai (VERIFY: download CDN host)"],
  rigs: true, terms: "https://www.meshy.ai/terms-of-use", docs: "https://docs.meshy.ai/en/api/text-to-3d",
};

// Animation library action ids per battle state. VERIFY every id against Meshy's animation library
// list before use; null = not requested (the shared CC0 library covers it in Unity).
export const ACTIONS = JSON.parse(fs.readFileSync(new URL("./meshy-actions.json", import.meta.url), "utf8"));

export function create(opts) {
  const http = new Http({ base: "https://api.meshy.ai", keyEnv: info.keyEnv, proxyAuth: opts.proxyAuth, dryRun: opts.dryRun, log: opts.log });
  const ok = (r) => r?.status === "SUCCEEDED", bad = (r) => r?.status === "FAILED" || r?.status === "CANCELED" || r?.status === "EXPIRED";

  return {
    info,
    // Untextured preview mesh, then the textured refine. Returns { taskId, files }.
    async generate(hero, prompts, dir) {
      const preview = await http.req("POST", "/openapi/v2/text-to-3d", {
        mode: "preview", prompt: prompts.text3d.slice(0, 600), negative_prompt: prompts.negative3d, // VERIFY: negative_prompt support on current models
        ai_model: opts.model || "latest", topology: "triangle", target_polycount: opts.polycount || 30000, should_remesh: true,
        pose_mode: opts.pose === "A" ? "a-pose" : "t-pose",
      });
      const pid = preview.result;
      await poll(() => http.req("GET", `/openapi/v2/text-to-3d/${pid}`), { done: ok, failed: bad, label: `${hero} preview`, log: opts.log });
      const refine = await http.req("POST", "/openapi/v2/text-to-3d", {
        mode: "refine", preview_task_id: pid, texture_prompt: prompts.texture.slice(0, 600), enable_pbr: false, // hand-painted albedo, our shader does the rest
      });
      const rid = refine.result;
      const done = await poll(() => http.req("GET", `/openapi/v2/text-to-3d/${rid}`), { done: ok, failed: bad, label: `${hero} refine`, log: opts.log });
      const glb = findUrls(done).find((u) => u.ext === ".glb");
      const file = path.join(dir, "raw", "model.glb");
      if (glb || opts.dryRun) await download(glb?.url ?? "https://example.invalid/model.glb", file, opts);
      return { taskId: rid, previewTaskId: pid, files: [file] };
    },
    async generateProp(hero, prompts, dir) {
      const t = await http.req("POST", "/openapi/v2/text-to-3d", { mode: "preview", prompt: prompts.weapon3d.slice(0, 600), ai_model: opts.model || "latest", topology: "triangle", target_polycount: 3000, should_remesh: true });
      await poll(() => http.req("GET", `/openapi/v2/text-to-3d/${t.result}`), { done: ok, failed: bad, label: `${hero} weapon preview`, log: opts.log });
      const r = await http.req("POST", "/openapi/v2/text-to-3d", { mode: "refine", preview_task_id: t.result, texture_prompt: prompts.texture.slice(0, 600) });
      const done = await poll(() => http.req("GET", `/openapi/v2/text-to-3d/${r.result}`), { done: ok, failed: bad, label: `${hero} weapon refine`, log: opts.log });
      const glb = findUrls(done).find((u) => u.ext === ".glb");
      const file = path.join(dir, "raw", "weapon.glb");
      if (glb || opts.dryRun) await download(glb?.url ?? "https://example.invalid/weapon.glb", file, opts);
      return { taskId: r.result, files: [file] };
    },
    // Auto-rig (humanoid), then one animation task per mapped battle state.
    async rig(hero, gen, dir, heightM) {
      const rig = await http.req("POST", "/openapi/v1/rigging", { input_task_id: gen.taskId, height_meters: heightM }); // VERIFY: height_meters
      const rigId = rig.result;
      const done = await poll(() => http.req("GET", `/openapi/v1/rigging/${rigId}`), { done: ok, failed: bad, label: `${hero} rig`, log: opts.log });
      const urls = findUrls(done);
      const rigged = urls.find((u) => u.ext === ".fbx" && /rig|character/i.test(u.key)) ?? urls.find((u) => u.ext === ".glb");
      const files = [];
      const lod0 = path.join(dir, `SB_Hero_${hero}_LOD0${rigged?.ext ?? ".fbx"}`);
      if (rigged || opts.dryRun) files.push(await download(rigged?.url ?? "https://example.invalid/rigged.fbx", lod0, opts));
      for (const [state, id] of Object.entries(ACTIONS.actions)) {
        if (id == null) continue;
        const a = await http.req("POST", "/openapi/v1/animations", { rig_task_id: rigId, action_id: id }); // VERIFY: endpoint and fields
        const d = await poll(() => http.req("GET", `/openapi/v1/animations/${a.result}`), { done: ok, failed: bad, label: `${hero} ${state}`, log: opts.log });
        const u = findUrls(d).find((x) => x.ext === ".fbx") ?? findUrls(d)[0];
        if (u || opts.dryRun) files.push(await download(u?.url ?? "https://example.invalid/anim.fbx", path.join(dir, `SB_Anim_${hero}_${state}${u?.ext ?? ".fbx"}`), opts));
      }
      return { taskId: rigId, files };
    },
  };
}
