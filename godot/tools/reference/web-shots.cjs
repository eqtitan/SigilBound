// Renders the web game (public/game) at 412x915 for side-by-side reference with the Godot demo:
// intro, Home, a 3D auto battle at stage 1-1 and the hero page.
//   cd godot/tools/reference && npm ci && node web-shots.cjs OUTDIR [CHROME_PATH]
// Uses the system Chrome/Chromium (SwiftShader WebGL works headless).
const { chromium } = require("playwright-core");
const http = require("http");
const fs = require("fs");
const path = require("path");
const root = path.resolve(__dirname, "../../../public");
const out = path.resolve(process.argv[2] || "web-shots");
fs.mkdirSync(out, { recursive: true });
const types = { ".html": "text/html", ".js": "text/javascript", ".mjs": "text/javascript", ".css": "text/css", ".svg": "image/svg+xml", ".glb": "model/gltf-binary", ".json": "application/json", ".webmanifest": "application/json", ".mp3": "audio/mpeg", ".ogg": "audio/ogg", ".png": "image/png" };
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split("?")[0]);
  if (p.endsWith("/")) p += "index.html";
  const f = path.join(root, p);
  fs.readFile(f, (e, d) => { if (e) { res.writeHead(404); res.end(); return; } res.writeHead(200, { "content-type": types[path.extname(f)] || "application/octet-stream" }); res.end(d); });
}).listen(8765);

(async () => {
  const browser = await chromium.launch({ executablePath: process.argv[3] || "/usr/bin/google-chrome", args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", "--no-sandbox"] });
  const ctx = await browser.newContext({ viewport: { width: 412, height: 915 }, deviceScaleFactor: 1, isMobile: true, hasTouch: true });
  const page = await ctx.newPage();
  page.on("console", (m) => { if (m.type() === "error" || m.type() === "warning") console.log("console:", m.text().slice(0, 200)); });
  const wait = (ms) => page.waitForTimeout(ms);
  const quiet = () => page.evaluate(() => { try { const M = ensureModes(); for (const t of TIPS) M.tips[t.id] = 1; save(); closeModal(); } catch (_) {} });
  const shot = async (n) => { await quiet(); await wait(300); await page.screenshot({ path: `${out}/${n}.png` }); console.log("shot", n); };
  await page.goto("http://localhost:8765/game/index.html");
  await wait(2500);
  await shot("web-1-intro");
  await page.evaluate(() => { ACTIONS.pickStarter({ id: "hask" }); const M = ensureModes(); for (const t of TIPS) M.tips[t.id] = 1; save(); closeModal(); render(); });
  await wait(1500);
  await shot("web-2-home");
  await page.evaluate(() => { window.scrollTo(0, 700); });
  await wait(500);
  await shot("web-2b-home-scrolled");
  await page.evaluate(() => { G.settings.auto = true; save(); });
  await page.evaluate(() => startBattle({ mode: "stage", idx: 0 }));
  await wait(9000);
  await shot("web-3-battle");
  await wait(3500);
  await shot("web-4-battle");
  await wait(3500);
  await shot("web-5-battle");
  await page.evaluate(() => { if (B) { B.token = {}; B = null; } UI.screen = "heroes"; render(); });
  await wait(2500);
  await shot("web-6-heroes");
  await page.evaluate(() => { UI.heroUid = G.heroes[0].uid; UI.screen = "hero"; render(); });
  await wait(4000);
  await shot("web-7-hero");
  await browser.close();
  server.close();
})().catch((e) => { console.error(e); process.exit(1); });
