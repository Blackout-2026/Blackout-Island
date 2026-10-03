// Performance: average / p95 frame time at fixed views (AI frozen so the numbers are comparable).
// usage: node tests/perf.js [quality]
const { open, sleep } = require('./lib');
(async () => {
  const q = process.argv[2] || 'medium';
  const noCull = process.argv[3] === 'nocull';
  const { browser, page, errors } = await open({ query: '?quality=' + q, width: 1280, height: 720 });
  page.on('framenavigated', (f) => console.log('NAVIGATED', f.url()));
  page.on('crash', () => console.log('PAGE CRASH'));
  page.on('console', (m) => { if (/context|lost|WebGL/i.test(m.text())) console.log('CONSOLE', m.type(), m.text()); });
  await page.evaluate(() => { BI.Engine.renderer.domElement.addEventListener('webglcontextlost', () => console.log('WebGL CONTEXT LOST')); __debug.start(); __debug.setGod(true); __debug.freezeAI(true); });
  if (noCull) await page.evaluate(() => { BI.World.update = function () {}; BI.World.setAllVisible(true); });
  const views = ['beach', 'forest', 'compound', 'entrance', 'security', 'corridor', 'generator', 'lab'];
  const rows = [];
  for (const v of views) {
    await page.evaluate((v) => __debug.go(v), v);
    await page.evaluate(() => __debug.profile(true));
    await sleep(1200);
    const m = await page.evaluate(() => __debug.measure(3));
    const prof = await page.evaluate(() => __debug.profile(true));
    console.log('measured', v, m.fps, 'worst per system:', JSON.stringify(Object.fromEntries(Object.entries(prof).filter(([k, ms]) => ms > 8).map(([k, ms]) => [k, Math.round(ms)]))));
    rows.push(v.padEnd(10) + ' fps ' + String(m.fps).padStart(5) + '  avg ' + m.avgMs + 'ms  p95 ' + m.p95Ms + 'ms  worst ' + m.worstMs + 'ms  draws ' + m.stats.calls + '  tris ' + Math.round(m.stats.triangles / 1000) + 'k');
  }
  console.log('quality', q, '\n' + rows.join('\n'));
  console.log('errors', errors.length ? errors : 'none');
  await browser.close();
})();
