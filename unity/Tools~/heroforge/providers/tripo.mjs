// Tripo API adapter: text_to_model, then animate_rig and animate_retarget with preset motions.
// Task types and fields follow community integrations of the v2 task API (checked through search
// results on 2026-11-23); Tripo's newer docs also describe a v3 API at openapi.tripo3d.com/v3
// (pass --tripo-base to switch). Fields marked VERIFY could not be read from Tripo's own docs pages
// from the prep session. Output URLs are found by file extension.
import path from "node:path";
import { Http, poll, findUrls, download } from "./common.mjs";

export const info = {
  name: "tripo", keyEnv: "TRIPO_API_KEY", hosts: ["api.tripo3d.ai", "(VERIFY: model download CDN host)"],
  rigs: true, terms: "https://www.tripo3d.ai/terms", docs: "https://platform.tripo3d.ai/docs",
};

// Preset motions per battle state (biped presets seen in the docs: idle, walk, run, slash, shoot,
// hurt, fall, jump, dive, climb, turn, chop). Missing states come from the shared library in Unity.
export const PRESETS = { Idle: "preset:idle", Run: "preset:run", Attack_1: "preset:slash", Attack_2: "preset:chop", Cast: "preset:shoot", Hit: "preset:hurt", Death: "preset:fall", Dodge: "preset:dive" };

export function create(opts) {
  const http = new Http({ base: opts.tripoBase || "https://api.tripo3d.ai/v2/openapi", keyEnv: info.keyEnv, proxyAuth: opts.proxyAuth, dryRun: opts.dryRun, log: opts.log });
  const ok = (r) => r?.data?.status === "success", bad = (r) => ["failed", "banned", "expired", "cancelled", "unknown"].includes(r?.data?.status);
  const task = async (body, label) => {
    const t = await http.req("POST", "/task", body);
    const id = t?.data?.task_id;
    const done = await poll(() => http.req("GET", `/task/${id}`), { done: ok, failed: bad, label, log: opts.log });
    return { id, done };
  };
  return {
    info,
    async generate(hero, prompts, dir) {
      const { id, done } = await task({
        type: "text_to_model", prompt: prompts.text3d.slice(0, 1024), negative_prompt: prompts.negative3d.slice(0, 255), // VERIFY: negative_prompt length limit
        face_limit: opts.polycount || 30000, texture: true, pbr: false, // VERIFY: texture/pbr flags on the current model version
      }, `${hero} model`);
      const glb = findUrls(done).find((u) => u.ext === ".glb");
      const file = path.join(dir, "raw", "model.glb");
      if (glb || opts.dryRun) await download(glb?.url ?? "https://example.invalid/model.glb", file, opts);
      return { taskId: id, files: [file] };
    },
    async generateProp(hero, prompts, dir) {
      const { id, done } = await task({ type: "text_to_model", prompt: prompts.weapon3d.slice(0, 1024), face_limit: 3000, texture: true }, `${hero} weapon`);
      const glb = findUrls(done).find((u) => u.ext === ".glb");
      const file = path.join(dir, "raw", "weapon.glb");
      if (glb || opts.dryRun) await download(glb?.url ?? "https://example.invalid/weapon.glb", file, opts);
      return { taskId: id, files: [file] };
    },
    async rig(hero, gen, dir) {
      // animate_rig takes the generation task; animate_retarget takes the RIG task.
      const rig = await task({ type: "animate_rig", original_model_task_id: gen.taskId, out_format: "glb" }, `${hero} rig`);
      const files = [];
      const r = findUrls(rig.done).find((u) => u.ext === ".glb");
      if (r || opts.dryRun) files.push(await download(r?.url ?? "https://example.invalid/rigged.glb", path.join(dir, `SB_Hero_${hero}_LOD0.glb`), opts));
      for (const [state, preset] of Object.entries(PRESETS)) {
        const a = await task({ type: "animate_retarget", original_model_task_id: rig.id, animation: preset, out_format: "glb" }, `${hero} ${state}`);
        const u = findUrls(a.done).find((x) => x.ext === ".glb");
        if (u || opts.dryRun) files.push(await download(u?.url ?? "https://example.invalid/anim.glb", path.join(dir, `SB_Anim_${hero}_${state}.glb`), opts));
      }
      return { taskId: rig.id, files };
    },
  };
}
