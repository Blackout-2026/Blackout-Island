// Quick development check: load the game, start it, take screenshots from a list
// of named views, print load timings, renderer stats and any console errors.
// usage: node tests/quick.js [viewName ...]
const { open, sleep } = require('./lib');
const path = require('path');

const VIEWS = {
  beach: { tp: [0, 3, 121, 0, -0.05] },
  beachBack: { tp: [6, 3, 100, Math.PI * 0.85, 0.02] },
  forest: { tp: [2, 30, 60, 0.3, 0.0] },
  forest2: { tp: [-6, 30, 30, -0.2, 0.05] },
  compound: { tp: [0, 30, -22, 0, 0.05] },
  entrance: { tp: [3, 12, -40, 0.15, 0.08] },
  ramp: { tp: [0, 12, -49, 0, -0.2] },
  security: { tp: [0, 4, -72, 0, -0.05] },
  corridor: { tp: [0, 4, -84, 0, 0.0] },
  generator: { tp: [-3.5, 4, -106, Math.PI / 2, 0.05] },
  lab: { tp: [0, 4, -126, 0, -0.05] },
};

(async () => {
  const names = process.argv.slice(2);
  const { browser, page, errors, failed, loadMs } = await open();
  console.log('loaded in', loadMs, 'ms');
  console.log('steps', JSON.stringify(await page.evaluate(() => __debug.steps())));
  await page.evaluate(() => __debug.start());
  await sleep(1500);
  for (const n of names.length ? names : ['beach']) {
    const v = VIEWS[n];
    if (!v) { console.log('unknown view', n); continue; }
    await page.evaluate((tp) => __debug.teleport(...tp), v.tp);
    if (v.flash) await page.evaluate(() => __debug.flash && __debug.flash(true));
    await sleep(v.wait || 1800);
    const file = path.join(__dirname, 'out', 'q_' + n + '.png');
    await page.screenshot({ path: file });
    console.log(n, JSON.stringify(await page.evaluate(() => ({ p: __debug.player(), s: __debug.stats() }))));
  }
  console.log('errors:', errors.length ? errors : 'none');
  console.log('failed requests:', failed.length ? failed : 'none');
  await browser.close();
})();
