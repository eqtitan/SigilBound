// Current-look reference: a battle with Gorrum, Ysolde and Hask at 390x844 (web build, QA harness).
// node battle-shot.cjs <repo root> <out dir>   (needs playwright resolvable, see tools/qa/README.md)
process.env.QA_NO_BUILD = "1";
const Q = require(process.argv[2] + "/tools/qa/lib.cjs");
const OUT = process.argv[3];
(async () => {
  const stop = await Q.ensureServer();
  const { browser, page } = await Q.launch({ width: 390, height: 844, dpr: 2 });
  try {
    await Q.bootGame(page);
    for (let i = 0; i < 5; i++) { const b = await page.$("[data-act=closeModal]"); if (!b) break; await b.click().catch(() => {}); await Q.sleep(300); }
    await page.evaluate(() => {
      const ids = ["gorrum", "ysolde", "hask"];
      const heroes = ids.map((id) => G.heroes.find((h) => h.id === id) || addHero(id));
      G.team = heroes.map((h) => h.uid); save();
      startBattle({ mode: "stage", idx: 0 });
    });
    await page.waitForSelector(".w3d-canvas", { timeout: 60000 });
    await Q.sleep(2500);
    for (let i = 0; i < 3; i++) { const b = await page.$("text=Got it"); if (!b) break; await b.click().catch(() => {}); await Q.sleep(500); }
    await Q.sleep(4000);
    await page.evaluate(() => { try { B.paused = true; } catch (e) {} });
    await page.screenshot({ path: OUT + "/current-battle.png" });
    console.log("saved");
  } finally { await browser.close(); if (stop) stop(); }
})().catch((e) => { console.error(e); process.exit(1); });
