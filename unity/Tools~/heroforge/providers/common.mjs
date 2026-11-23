// Shared HTTP helpers for the generator adapters. Keys are never stored: each adapter reads its
// key from the environment, or (with --proxy-auth) sends a placeholder that the session's network
// proxy replaces with the key the owner saved through the secure credential form.
import fs from "node:fs";
import path from "node:path";

export class Http {
  constructor({ base, keyEnv, proxyAuth, dryRun, log = console.log }) {
    this.base = base; this.keyEnv = keyEnv; this.dryRun = dryRun; this.log = log;
    const key = process.env[keyEnv];
    this.auth = key ? `Bearer ${key}` : proxyAuth ? "Bearer proxy-injected-placeholder" : null;
  }
  need() {
    if (!this.auth && !this.dryRun)
      throw new Error(`No API key: set ${this.keyEnv}, or pass --proxy-auth in a session where the key was saved through the credential form for ${new URL(this.base).host}.`);
  }
  async req(method, pathOrUrl, body, { form } = {}) {
    const url = pathOrUrl.startsWith("http") ? pathOrUrl : this.base + pathOrUrl;
    if (this.dryRun) { this.log(`  [dry-run] ${method} ${url}${body ? " " + JSON.stringify(form ? Object.fromEntries(Object.entries(body).map(([k, v]) => [k, typeof v === "string" ? v : "<file>"])) : body).slice(0, 400) : ""}`); return { dryRun: true, result: "<task-id>", uuid: "<task-id>", data: { task_id: "<task-id>" }, jobs: { subscription_key: "<key>" } }; }
    this.need();
    const headers = { Authorization: this.auth };
    let payload;
    if (form) { payload = new FormData(); for (const [k, v] of Object.entries(body)) payload.append(k, v); }
    else if (body) { headers["Content-Type"] = "application/json"; payload = JSON.stringify(body); }
    const res = await fetch(url, { method, headers, body: payload });
    const text = await res.text();
    if (!res.ok) throw new Error(`${method} ${url} -> HTTP ${res.status}: ${text.slice(0, 500)}${res.status === 401 || res.status === 403 ? " (key missing or not yet picked up by the proxy: wait a minute and retry)" : ""}`);
    try { return JSON.parse(text); } catch { return text; }
  }
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Polls fn() until done(result) or failed(result); returns the last result.
export async function poll(fn, { done, failed, everyMs = 8000, timeoutMs = 30 * 60 * 1000, label = "task", log = console.log }) {
  const t0 = Date.now();
  for (;;) {
    const r = await fn();
    if (r?.dryRun) return r;
    if (done(r)) return r;
    if (failed(r)) throw new Error(`${label} failed: ${JSON.stringify(r).slice(0, 500)}`);
    if (Date.now() - t0 > timeoutMs) throw new Error(`${label} timed out`);
    log(`  ${label}: ${r?.status ?? r?.data?.status ?? "running"} ${r?.progress ?? r?.data?.progress ?? ""}`);
    await sleep(everyMs);
  }
}

// Every http(s) URL in a response whose path ends in one of `exts` (field names differ by API
// version, so the adapters don't depend on them): [{ key, url, ext }].
export function findUrls(obj, exts = [".glb", ".fbx"], trail = "") {
  const out = [];
  if (typeof obj === "string" && /^https?:\/\//.test(obj)) {
    const p = new URL(obj).pathname.toLowerCase();
    const ext = exts.find((e) => p.endsWith(e));
    if (ext) out.push({ key: trail, url: obj, ext });
  } else if (obj && typeof obj === "object") for (const [k, v] of Object.entries(obj)) out.push(...findUrls(v, exts, trail ? `${trail}.${k}` : k));
  return out;
}

export async function download(url, dest, { dryRun, log = console.log } = {}) {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  if (dryRun) { log(`  [dry-run] download ${url} -> ${dest}`); return dest; }
  const res = await fetch(url);
  if (!res.ok) throw new Error(`download ${new URL(url).host} -> HTTP ${res.status}${res.status === 403 ? " (the session's network may need this host allowed)" : ""}`);
  fs.writeFileSync(dest, Buffer.from(await res.arrayBuffer()));
  log(`  saved ${path.basename(dest)} (${Math.round(fs.statSync(dest).size / 1024)} KB) from ${new URL(url).host}`);
  return dest;
}
