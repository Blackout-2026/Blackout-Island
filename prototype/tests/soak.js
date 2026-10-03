// Soak test: AI fully active, player mortal. Walks the mission (teleport hops between
// phases, real input inside areas), then the escape with every agent converging.
// Reports frame times, deaths, agent states, stuck counters and console errors.
const { open, sleep } = require('./lib');
(async () => {
  const { browser, page, errors } = await open({ query: '?quality=medium' });
  const E = (js, a) => page.evaluate(js, a);
  await E(() => { __debug.start(); });
  const frames = async (sec) => E((sec) => __debug.measure(sec), sec);
  const report = [];
  const hold = async (key, ms) => { await E((k) => __debug.key(k, true), key); await sleep(ms); await E((k) => __debug.key(k, false), key); };

  // beach -> forest walk (real input) for a while
  await E(() => __debug.look(0, 0));
  await E(() => __debug.key('KeyW', true));
  const f1 = await frames(6);
  await E(() => __debug.key('KeyW', false));
  report.push(['walk on beach/forest', f1.fps, f1.worstMs]);

  // forest: stand near the path for the hunters
  await E(() => __debug.teleport(2, 30, 40, 0));
  const f2 = await frames(8);
  report.push(['forest (hunters active)', f2.fps, f2.worstMs]);

  // compound -> door -> down the ramp
  await E(() => { __debug.teleport(0, 30, -36, 0); BI.Doors.byId.bunker.requestOpen('test'); });
  await sleep(3500);
  await E(() => { __debug.teleport(0, 12, -50.5, 0); __debug.key('KeyW', true); });
  const f3 = await frames(4);
  await E(() => __debug.key('KeyW', false));
  report.push(['ramp descent', f3.fps, f3.worstMs]);

  // generator: disable it
  await E(() => { __debug.go('generator'); __debug.lookAt(-6.9, 3.65, -106); });
  await hold('KeyE', 2300);
  const f4 = await frames(6);
  report.push(['blackout cascade', f4.fps, f4.worstMs]);

  // cure -> escape with everyone converging
  await E(() => __debug.go('lab'));
  await sleep(800);
  await E(() => __debug.go('cure'));
  await sleep(500);
  const f5 = await frames(10);
  report.push(['alert + convergence (lab)', f5.fps, f5.worstMs]);
  await E(() => __debug.teleport(0, 12, -44, Math.PI));
  const f6 = await frames(10);
  report.push(['escape: compound', f6.fps, f6.worstMs]);
  await E(() => __debug.teleport(3, 30, 20, Math.PI));
  const f7 = await frames(10);
  report.push(['escape: forest', f7.fps, f7.worstMs]);

  const st = await E(() => __debug.state());
  for (const r of report) console.log(r[0].padEnd(28), 'fps', String(r[1]).padStart(5), ' worst', r[2], 'ms');
  console.log('deaths', st.deaths, 'kills', st.kills, 'phase', st.phase, 'checkpoint', st.checkpoint);
  console.log('agents:', st.agents.map((a) => a.name + ':' + a.state + (a.alive ? '' : '(dead)') + (a.stuck ? ' stuck' + a.stuck : '')).join(' | '));
  console.log('A* last expanded', await E(() => BI.AStar.lastExpanded), 'queue', await E(() => BI.AStar.queue.length));
  console.log('errors:', errors.length ? errors.slice(0, 5) : 'none');
  await browser.close();
})();
