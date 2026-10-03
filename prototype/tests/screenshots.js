// Captures the key moments into prototype/screenshots/ (1280x720, Medium quality).
// usage: node tests/screenshots.js [name ...]   (no names = all)
const { open, sleep } = require('./lib');
const path = require('path');
const OUT = path.join(__dirname, '..', 'screenshots');

const BLACKOUT = "BI.BunkerInterior.generator.disable(); BI.Events.emit('generator:disabled', { pos: new THREE.Vector3(-11, 4.5, -106) });";
const SHOTS = [
  { name: '01_beach', setup: "__debug.teleport(0.6, 3, 119); __debug.lookAt(-2, 7, 70);" },
  { name: '02_forest', setup: "__debug.teleport(5, 30, 66); __debug.lookAt(-4, 6, 40);" },
  { name: '03_bunker_entrance', setup: "__debug.teleport(4.5, 30, -33.5); __debug.lookAt(-0.5, 12.2, -50);" },
  { name: '04_security_office', setup: "__debug.teleport(-6, 4, -71.5); __debug.lookAt(4, 3.6, -79);" },
  { name: '05_corridor', setup: "BI.Doors.byId.security.requestOpen('t'); __debug.teleport(0.6, 4, -84); __debug.lookAt(-0.3, 3.9, -110);", wait: 2500 },
  { name: '06_generator_before_blackout', setup: "__debug.teleport(-3.2, 4, -101.5); __debug.lookAt(-11, 4.2, -106.5);" },
  { name: '07_hero_generator', setup: "__debug.teleport(-4.4, 4, -103.0); __debug.lookAt(-10, 3.9, -106.7); __debug.flash(true);", wait: 2000 },
  { name: '08_hero_robot', robot: true },
  { name: '09_enemy_fight', fight: true },
  { name: '10_debug_overlay', debug: true },
  { name: '11_generator_after_blackout', setup: "__debug.teleport(-3.2, 4, -101.5); __debug.lookAt(-11, 4.2, -106.5); " + BLACKOUT, wait: 7000 },
  { name: '12_corridor_after_blackout', setup: "__debug.teleport(0.6, 4, -86); __debug.lookAt(-0.3, 3.8, -112);", wait: 2500 },
  { name: '13_lab', setup: "__debug.teleport(6.5, 4, -125.5); __debug.lookAt(0, 3.6, -135);", wait: 3000 },
];

(async () => {
  const want = process.argv.slice(2);
  const { browser, page, errors } = await open({ query: '?quality=medium' });
  await page.evaluate(() => { __debug.start(); __debug.setGod(true); __debug.freezeAI(true); });
  await sleep(1000);
  for (const s of SHOTS) {
    if (want.length && !want.some((w) => s.name.includes(w))) continue;
    if (s.fight) {
      // a Security Patrol at close range in Attack; the player returns fire mid-capture
      await page.evaluate(() => {
        __debug.freezeAI(false);
        __debug.agentTo('Patrol-1', -1.2, -39.5);
        __debug.teleport(1.0, 30, -33.5);
        const a = BI.Agents.byName('Patrol-1');
        a.yaw = Math.atan2(BI.player.pos.x - a.pos.x, BI.player.pos.z - a.pos.z);
        a.perception.awareness = 1.5; a.perception.detected = true; a.perception.lastSeenTime = BI.Engine.time;
        a.go('Attack', null, 'staged');
        __debug.lookAt(-1.2, 11.2, -39.5); __debug.weapon(0);
      });
      await sleep(1800);
      // fire a short burst, and capture right after a shot so the flash/tracer is on screen
      for (let i = 0; i < 3; i++) { await page.evaluate(() => { __debug.lookAt(-1.2, 11.2, -40.5); BI.weapons.cooldown = 0; BI.weapons.fire(); }); await sleep(90); }
      // staging for the still: hold this shot's flash and tracers on screen long enough to capture
      await page.evaluate(() => { const a = BI.Agents.byName('Patrol-1'); a.shoot(2); __debug.lookAt(-1.2, 11.2, -39.5); BI.weapons.cooldown = 0; BI.weapons.fire(); BI.weapons.flashT = 0.5; BI.Combat.flashT = 0.5; BI.FX.tracers.forEach((t) => { if (t.life > 0) { t.life = 0.5; t.max = 0.5; } }); });
      await sleep(60);
      await page.screenshot({ path: path.join(OUT, s.name + '.png') });
      await page.evaluate(() => { __debug.freezeAI(true); });
    } else if (s.robot) {
      await page.evaluate(() => {
        __debug.agentTo('TG-02', 2.5, -130.5, Math.atan2(0.2 - 2.5, -127.2 + 130.5));
        const a = BI.Agents.byName('TG-02');
        a.intent.aim = true; a.controller.aimBlend = 1; a.rig.eyeColor.setRGB(6, 0.4, 0.2);
        a.rig.pose({ speed: 0, dist: 0, aim: 1, aimPitch: 0, lookYaw: 0 }, 0.016);
        a.rig.root.position.copy(a.pos); a.rig.root.rotation.y = a.yaw;
        __debug.teleport(0.2, 4, -127.2); __debug.lookAt(2.5, 3.6, -130.5); __debug.flash(true);
      });
      await sleep(1500);
      await page.screenshot({ path: path.join(OUT, s.name + '.png') });
      await page.evaluate(() => __debug.flash(false));
    } else if (s.debug) {
      await page.evaluate(() => { __debug.freezeAI(false); __debug.teleport(-2.6, 4, -110.5); __debug.lookAt(-12, 2.6, -103); __debug.debugOverlay(true); });
      await sleep(3000);
      await page.screenshot({ path: path.join(OUT, s.name + '.png') });
      await page.evaluate(() => { __debug.debugOverlay(false); __debug.freezeAI(true); });
    } else {
      await page.evaluate(s.setup);
      await sleep(s.wait || 1800);
      await page.screenshot({ path: path.join(OUT, s.name + '.png') });
      await page.evaluate(() => __debug.flash(false));
    }
    console.log('saved', s.name);
  }
  console.log('errors:', errors.length ? errors : 'none');
  await browser.close();
})();
