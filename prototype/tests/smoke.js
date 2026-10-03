// Scripted smoke test: drives the whole mission through window.__debug and
// asserts the event chain. Run: node tests/smoke.js   (exit code 0 = pass)
const { open, sleep } = require('./lib');

const results = [];
function check(name, ok, detail) {
  results.push({ name, ok: !!ok, detail });
  console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? '  — ' + detail : ''));
}

(async () => {
  const { browser, page, errors, failed, loadMs } = await open({ query: '?quality=medium' });
  console.log('loaded in', loadMs, 'ms');
  const S = () => page.evaluate(() => __debug.state());
  const ev = (n) => page.evaluate((n) => __debug.count(n), n);
  try {
    await page.evaluate(() => { __debug.start(); __debug.setGod(true); });
    await sleep(1500);
    let s = await S();
    check('starts on the beach phase', s.phase === 'beach', s.objective);
    check('all 8 agents spawned (2 hunters, 3 patrols, 2 guards, 1 commander)',
      s.agents.length === 8 && s.agents.filter((a) => a.type === 'hunter').length === 2 && s.agents.filter((a) => a.type === 'patrol').length === 3 && s.agents.filter((a) => a.type === 'guard').length === 2 && s.agents.filter((a) => a.type === 'commander').length === 1,
      s.agents.map((a) => a.name + '[' + a.arch + ']').join(', '));
    check('four architectures present', ['FSM', 'BT', 'Utility', 'GOAP'].every((k) => s.agents.some((a) => a.arch === k)));

    // ---- generator: hold E for 2 s at the console ----------------------------------
    await page.evaluate(() => { __debug.go('generator'); __debug.lookAt(-6.9, 3.65, -106); });
    await sleep(800);
    const prompt = await page.evaluate(() => BI.Interact.prompt && BI.Interact.prompt.text);
    check('generator prompt shown at the console', prompt === 'Disable generator', 'prompt: ' + prompt);
    await page.evaluate(() => __debug.key('KeyE', true));
    await sleep(1000);
    s = await S();
    check('holding E for 1 s does not yet disable it', !s.generatorDown);
    await sleep(1400);
    await page.evaluate(() => __debug.key('KeyE', false));
    await sleep(300);
    s = await S();
    check('generator disabled after a 2 s hold', s.generatorDown);
    check('generator:disabled event published once', (await ev('generator:disabled')) === 1);
    await sleep(4500);
    s = await S();
    check('light cascade + emergency lighting events fired', (await ev('lights:cascade')) === 1 && (await ev('lights:emergency')) === 1);
    check('blackout lighting active (blend > 0.8)', s.blackout > 0.8, 'blend ' + s.blackout.toFixed(2));
    check('fog thickened in the bunker', s.fogDensity > 0.045, 'density ' + s.fogDensity.toFixed(3));
    check('patrols and guards heightened (faster, see further)', s.agents.filter((a) => a.type === 'patrol' || a.type === 'guard').every((a) => a.heightened));
    check('lab maglock released by the power cut', s.labLocked === false);
    const objAfterGen = s.objective;
    check('objective now: take the cure', /cure/i.test(objAfterGen), objAfterGen);

    // ---- cure ---------------------------------------------------------------------
    await page.evaluate(() => __debug.go('lab'));
    await sleep(1500);
    await page.evaluate(() => __debug.go('cure'));
    await sleep(500);
    s = await S();
    check('cure taken by walking into the chamber', s.cureTaken && (await page.evaluate(() => __debug.player().hasCure)));
    await sleep(2500);
    s = await S();
    check('Commander broadcast the alert (alert:broadcast event)', (await ev('alert:broadcast')) >= 1 && s.alert);
    check('HUD shows the SECURITY ALERT banner', await page.evaluate(() => document.querySelector('.hud-alert').classList.contains('on')));
    check('Commander GOAP plan targets the player', s.commander.goal === 'PlayerNeutralized' || s.commander.goal === 'PlayerLocated', s.commander.goal + ': ' + s.commander.plan.join(' -> '));
    const living = s.agents.filter((a) => a.alive && a.type !== 'commander');
    const hasOrder = living.filter((a) => s.directives.some((d) => d.startsWith(a.name + ':')));
    check('every living agent received a directive', hasOrder.length === living.length, s.directives.join(' '));
    await sleep(2500);
    s = await S();
    const pursuitStates = /Pursue|Attack|Search|Investigate|Engage|FollowOrders|HuntLastSeen|Reposition|MoveToCover|Converge/;
    const notPursuing = s.agents.filter((a) => a.alive && a.type !== 'commander' && !pursuitStates.test(a.state));
    check('all living agents switched to search / pursue', notPursuing.length === 0, s.agents.filter((a) => a.alive).map((a) => a.name + ':' + a.state).join(', '));
    check('phase is escape', s.phase === 'escape', s.objective);

    // ---- escape: reach the boat ----------------------------------------------------
    await page.evaluate(() => __debug.go('boat'));
    await sleep(1500);
    s = await S();
    check('reaching the boat with the cure completes the mission', s.complete);
    const complete = await page.evaluate(() => { const el = document.getElementById('complete'); return el ? el.innerText.replace(/\s+/g, ' ') : null; });
    check('Mission Complete screen with time / kills / deaths and Restart', !!complete && /TIME/.test(complete) && /KILLS/.test(complete) && /DEATHS/.test(complete) && /RESTART/.test(complete), complete);

    check('zero console errors', errors.length === 0, errors.slice(0, 3).join(' | '));
    check('zero failed network requests', failed.length === 0, failed.slice(0, 3).join(' | '));
  } catch (e) {
    check('test script ran without exceptions', false, e.message);
  }
  await browser.close();
  const bad = results.filter((r) => !r.ok);
  console.log('\n' + (results.length - bad.length) + '/' + results.length + ' checks passed');
  process.exit(bad.length ? 1 : 0);
})();
