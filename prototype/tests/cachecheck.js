// Runs the transition sequence twice with a persistent profile (warm GPU shader cache on run 2).
const { chromium } = require('playwright');
const path = require('path');
const { URL } = require('./lib');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const dir = path.join(__dirname, 'out', 'profile');
  for (let run = 1; run <= 2; run++) {
    const ctx = await chromium.launchPersistentContext(dir, { headless: true, viewport: { width: 1280, height: 720 }, args: ['--use-angle=d3d11', '--enable-gpu', '--ignore-gpu-blocklist'] });
    const page = ctx.pages()[0] || (await ctx.newPage());
    const t0 = Date.now();
    await page.goto(URL + '?quality=medium');
    await page.waitForFunction(() => window.__debug && window.__debug.ready, null, { timeout: 120000 });
    const load = Date.now() - t0;
    const steps = await page.evaluate(() => __debug.steps());
    await page.evaluate(() => { __debug.start(); __debug.setGod(true); __debug.freezeAI(true); });
    const out = [];
    for (const [a, b] of [['beach', 'forest'], ['entrance', 'beach'], ['security', 'lab'], ['beach', 'security'], ['entrance', 'security'], ['corridor', 'generator']]) {
      await page.evaluate((v) => __debug.go(v), a);
      await sleep(2000);
      const w = await page.evaluate((v) => new Promise((res) => { let last = performance.now(); const t0 = last; let w = 0; __debug.go(v); const f = (now) => { w = Math.max(w, now - last); last = now; if (now - t0 < 2500) requestAnimationFrame(f); else res(Math.round(w)); }; requestAnimationFrame(f); }), b);
      out.push(a + '->' + b + ' ' + w + 'ms');
    }
    console.log('run', run, 'load', load, 'ms (compile', steps.steps.find((s) => s.text === 'Compiling shaders').ms, 'warm-up', (steps.steps.find((s) => s.text === 'Warm-up') || {}).ms, ')');
    console.log('   ', out.join(' | '));
    await ctx.close();
  }
})();
