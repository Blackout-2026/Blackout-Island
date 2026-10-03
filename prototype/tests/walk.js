// Realistic transition test: walk (simulated keys) from the forest edge into the bunker.
const { open, sleep } = require('./lib');
(async () => {
  const { browser, page } = await open({ query: '?quality=medium' });
  await page.evaluate(() => { __debug.start(); __debug.setGod(true); __debug.freezeAI(true); });
  // open the bunker door first
  await page.evaluate(() => { BI.Doors.byId.bunker.requestOpen('test'); BI.Doors.byId.security.requestOpen('test'); });
  await page.evaluate(() => __debug.teleport(0, 12, -40, 0, -0.1));
  await sleep(4000);
  const r = await page.evaluate(() => new Promise((res) => {
    const frames = []; let last = performance.now(); const t0 = last;
    __debug.key('KeyW', true); __debug.key('ShiftLeft', true);
    const f = (now) => {
      frames.push({ t: Math.round(now - t0), dt: Math.round(now - last), z: +BI.player.pos.z.toFixed(1), zone: BI.player.zone });
      last = now;
      if (BI.player.pos.z > -76 && now - t0 < 15000) requestAnimationFrame(f);
      else { __debug.key('KeyW', false); __debug.key('ShiftLeft', false); res(frames); }
    };
    requestAnimationFrame(f);
  }));
  const slow = r.filter((f) => f.dt > 25);
  console.log('frames', r.length, 'end z', r[r.length - 1].z, 'zone', r[r.length - 1].zone);
  console.log('slow frames (>25ms):', slow.length, 'worst', Math.max(...r.map((f) => f.dt)), 'ms', slow.filter((f) => f.dt > 60).map((f) => f.t + 'ms dt=' + f.dt + ' z=' + f.z + ' ' + f.zone).join(' | '));
  const avg = r.reduce((a, f) => a + f.dt, 0) / r.length;
  console.log('avg frame', avg.toFixed(1), 'ms');
  await browser.close();
})();
