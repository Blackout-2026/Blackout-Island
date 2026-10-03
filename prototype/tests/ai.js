// AI + mechanics behaviour test. Prints PASS/FAIL per behaviour.
// usage: node tests/ai.js
const { open, sleep } = require('./lib');
const results = [];
const check = (n, ok, d) => { results.push(!!ok); console.log((ok ? 'PASS ' : 'FAIL ') + n + (d ? '  — ' + d : '')); };

(async () => {
  const { browser, page, errors } = await open({ query: '?quality=medium' });
  const E = (js, arg) => page.evaluate(js, arg);
  const agent = (n) => E((n) => {
    const a = BI.Agents.byName(n);
    return { state: a.state, x: a.pos.x, z: a.pos.z, hp: a.hp, alive: a.alive, aw: a.perception.awareness, det: a.perception.detected, branch: a.activeBranch || '', action: a.action, scores: a.scores };
  }, n);
  await E(() => { __debug.start(); __debug.setGod(true); __debug.teleport(0, 3, 121, 0); });
  await sleep(1500);

  // 1. Patrol follows its route while the player is far away
  const p0 = await agent('Patrol-2');
  await sleep(8000);
  const p1 = await agent('Patrol-2');
  const moved = Math.hypot(p1.x - p0.x, p1.z - p0.z);
  check('Security Patrol walks its route (FSM Patrol)', p1.state === 'Patrol' && moved > 2, 'moved ' + moved.toFixed(1) + ' m, state ' + p1.state);

  // 2. Detection -> Pursue/Attack, shooting at the player
  await E(() => { __debug.setGod(false); BI.player.invuln = 0; BI.player.health = 100; __debug.agentTo('Patrol-3', 0, -96, 0); __debug.teleport(0, 4, -86, Math.PI); });
  let seen = null;
  for (let i = 0; i < 30 && !seen; i++) {
    await sleep(200);
    const a = await agent('Patrol-3');
    if (/Attack|Pursue/.test(a.state)) seen = a;
  }
  check('Patrol detects the player in its view cone (Pursue/Attack)', !!seen, seen ? seen.state : 'never');
  await sleep(3500);
  const hp = await E(() => BI.player.health);
  check('Patrol shoots back (player took damage)', hp < 100, 'health ' + hp.toFixed(0));

  // 3. Lose the player -> Search
  await E(() => { __debug.setGod(true); __debug.teleport(0, 3, 121, 0); });
  let searched = false;
  for (let i = 0; i < 30 && !searched; i++) {
    await sleep(300);
    const a = await agent('Patrol-3');
    if (a.state === 'Search') searched = true;
  }
  check('Patrol searches the last known position after losing sight', searched);
  await E(() => __debug.teleport(0, 3, 121, 0));

  // 4. Hunter: footsteps -> sneak; gunfire -> flank / cover
  const h = await agent('Hunter-1');
  await E((h) => { BI.Events.emit('noise', { pos: new THREE.Vector3(h.x + 7, BI.Terrain.height(h.x + 7, h.z + 7), h.z + 7), radius: 13, type: 'sprint', source: 'player' }); }, h);
  await sleep(700);
  const h1 = await agent('Hunter-1');
  check('Hunter reacts to footsteps by sneaking toward them (BT)', /Footsteps|Sneak|Ambush/.test(h1.branch), h1.branch);
  const h2 = await agent('Hunter-2');
  await E((h) => { BI.Events.emit('noise', { pos: new THREE.Vector3(h.x + 25, BI.Terrain.height(h.x + 25, h.z + 10), h.z + 10), radius: 45, type: 'gunfire', source: 'player' }); }, h2);
  await sleep(700);
  const h3 = await agent('Hunter-2');
  check('Hunter reacts to distant gunfire with a flank (different tactic)', /Flank/.test(h3.branch), h3.branch);
  await E((h) => { BI.Events.emit('noise', { pos: new THREE.Vector3(h.x + 8, BI.Terrain.height(h.x + 8, h.z), h.z), radius: 45, type: 'gunfire', source: 'player' }); }, h3);
  await sleep(700);
  const h4 = await agent('Hunter-2');
  check('Hunter takes cover from close gunfire', /TakeCover/.test(h4.branch), h4.branch);

  // 5. Sound propagation: closed doors block, open doors let sound through, walls muffle
  const snd = await E(() => {
    const d = BI.Doors.byId.generator;
    d.target = 0; d.open = 0; d._init = false; d.update(0);
    BI.Nav.updateDoors();
    BI.AStar.floodSound(-11, -101, 30);
    const closedDoor = BI.AStar.soundDistAt(0, -106);
    d.open = 1; d.target = 1; d._init = false; d.update(0);
    BI.Nav.updateDoors();
    BI.AStar.floodSound(-11, -101, 30);
    const openDoor = BI.AStar.soundDistAt(0, -106);
    const walls = BI.Physics.wallsBetween(-11, 4, -101, 0, 4, -101, 4);
    return { closedDoor, openDoor: +openDoor.toFixed(1), walls };
  });
  check('Sound flood: a closed door blocks, an open door lets sound through', snd.closedDoor === null || snd.closedDoor > 1e6 ? snd.openDoor < 30 : false, JSON.stringify(snd));
  check('Sound transmission counts walls between rooms', snd.walls >= 1, 'walls ' + snd.walls);

  // 6. Tactical Guard utility: visible -> Attack; hurt -> cover
  await E(() => { __debug.agentTo('TG-01', -8, -100, Math.PI / 2); __debug.teleport(-3.5, 4, -104, Math.PI / 2); __debug.lookAt(-8, 4.5, -100); });
  let g = null;
  for (let i = 0; i < 25; i++) {
    await sleep(200);
    g = await agent('TG-01');
    if (g.action === 'Attack') break;
  }
  check('Tactical Guard picks Attack when the player is visible (Utility)', g.action === 'Attack', JSON.stringify(g.scores));
  await E(() => { BI.Agents.byName('TG-01').takeDamage(55, BI.player.pos, false, 'test'); });
  for (let i = 0; i < 15; i++) {
    await sleep(200);
    g = await agent('TG-01');
    if (g.action === 'MoveToCover') break;
  }
  check('Guard switches to MoveToCover when hurt and threatened', g.action === 'MoveToCover', JSON.stringify(g.scores));
  await E(() => __debug.teleport(0, 3, 121, 0));

  // 7. Crate push updates the nav grid
  const crate = await E(async () => {
    const c = BI.crate;
    const z0 = c.z, v0 = BI.Nav.version;
    __debug.teleport(c.x, 4, c.z + 2.2, 0);
    __debug.key('KeyW', true);
    await new Promise((r) => setTimeout(r, 1800));
    __debug.key('KeyW', false);
    await new Promise((r) => setTimeout(r, 1500));
    return { moved: z0 - c.z, navVersionChanged: BI.Nav.version !== v0 };
  });
  check('Barricade crate is pushed by walking into it', crate.moved > 0.5, 'moved ' + crate.moved.toFixed(2) + ' m');
  check('Crate push updates the A* grid (nav:changed)', crate.navVersionChanged);

  // 8. Death and respawn at the checkpoint
  const death = await E(async () => {
    __debug.setGod(false);
    BI.player.invuln = 0;
    BI.player.takeDamage(500, null);
    const dead = !BI.player.alive;
    await new Promise((r) => setTimeout(r, 2800));
    return { dead, alive: BI.player.alive, health: BI.player.health, invuln: +BI.player.invuln.toFixed(2), cp: BI.Mission.checkpoint, deaths: BI.Mission.deaths };
  });
  check('Death respawns at the current checkpoint with full health', death.dead && death.alive && death.health === 100 && death.deaths === 1, JSON.stringify(death));

  // 9. Weapons: switching, sniper zoom
  const wpn = await E(async () => {
    const W = BI.weapons, out = {};
    __debug.setGod(true);
    __debug.key('Digit2', true); await new Promise((r) => setTimeout(r, 60)); __debug.key('Digit2', false);
    await new Promise((r) => setTimeout(r, 600));
    out.afterKey2 = W.def.id;
    BI.Input.buttons[2] = true; await new Promise((r) => setTimeout(r, 700));
    out.fovZoomed = +BI.Engine.camera.fov.toFixed(1);
    BI.Input.buttons[2] = false;
    __debug.key('Digit3', true); await new Promise((r) => setTimeout(r, 60)); __debug.key('Digit3', false);
    await new Promise((r) => setTimeout(r, 600));
    out.afterKey3 = W.def.id;
    return out;
  });
  check('Weapon switching with 1/2/3 and sniper RMB zoom', wpn.afterKey2 === 'sniper' && wpn.afterKey3 === 'knife' && wpn.fovZoomed < 40, JSON.stringify(wpn));

  // 10. Knife backstab
  const knife = await E(async () => {
    __debug.freezeAI(true);
    const a = BI.Agents.byName('Patrol-1');
    a.perception.forget();
    const p = a.pos, f = a.forward();
    __debug.teleport(p.x - f.x * 1.4, p.y + 2, p.z - f.z * 1.4);
    __debug.lookAt(p.x, p.y + 1.2, p.z);
    await new Promise((r) => setTimeout(r, 300));
    BI.weapons.cooldown = 0;
    BI.weapons.fire();
    await new Promise((r) => setTimeout(r, 400));
    __debug.freezeAI(false);
    return { alive: a.alive, hp: a.hp };
  });
  check('Knife backstab on an unaware enemy kills it (melee cone)', !knife.alive, JSON.stringify(knife));

  // 11. Commander regroups when the squad is depleted after the alarm
  const goap = await E(async () => {
    BI.Blackboard.set('cureStolen', true);
    for (const n of ['Patrol-2', 'Patrol-3', 'TG-01', 'TG-02', 'Hunter-1']) __debug.killAgent(n);
    BI.Blackboard.reportSighting(BI.Agents.commander, BI.player.pos);
    await new Promise((r) => setTimeout(r, 1500));
    const c = BI.Agents.commander;
    return { goal: c.goal, plan: c.plan.slice(), log: c.planLog.slice() };
  });
  check('Commander GOAP plans OrderRegroup when the squad is not ready', goap.log.some((l) => /OrderRegroup/.test(l)), goap.log.join(' | '));

  check('no console errors', errors.length === 0, errors.slice(0, 2).join(' | '));
  await browser.close();
  console.log(results.filter(Boolean).length + '/' + results.length + ' passed');
})();
