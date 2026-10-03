// Repro for the Chrome/ANGLE Direct3D 11 stall described in README (Performance):
// standing outside, re-show all bunker room groups at once and time the next frames.
// Run with BI_ANGLE=d3d11 (default) vs BI_ANGLE=gl to compare backends.
const { open, sleep } = require('./lib');
(async () => {
  const { browser, page } = await open({ query: '?quality=medium' });
  await page.evaluate(() => { __debug.start(); __debug.setGod(true); __debug.freezeAI(true); BI.Doors.byId.bunker.requestOpen('t'); __debug.teleport(0, 12, -46, 0, 0); });
  await sleep(4000);
  const timeIt = (js) => page.evaluate((js) => new Promise((res) => {
    requestAnimationFrame(() => { const t0 = performance.now(); let last = t0, worst = 0, n = 0; eval(js);
      const f = (now) => { worst = Math.max(worst, now - last); last = now; if (++n < 15) requestAnimationFrame(f); else res(Math.round(worst)); }; requestAnimationFrame(f); });
  }), js);
  await page.evaluate(() => { BI.World.update = function () {}; });
  for (let i = 0; i < 3; i++) {
    console.log('show all interior (standing outside):', await timeIt('for (const g of BI.World.interior) g.visible = true'), 'ms');
    await sleep(800);
    console.log('hide all interior:', await timeIt('for (const g of BI.World.interior) g.visible = false'), 'ms');
    await sleep(800);
  }
  console.log('moon shadow off, show all:', await timeIt('BI.Lighting.moon.shadow.autoUpdate = false; BI.Lighting.update = function(){}; for (const g of BI.World.interior) g.visible = true'), 'ms');
  await browser.close();
})();
