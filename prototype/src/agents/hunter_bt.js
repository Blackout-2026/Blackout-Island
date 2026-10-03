/* =============================================================================
 * agents/hunter_bt.js — HUNTER  ::  BEHAVIOR TREE + SOUND PROPAGATION
 * -----------------------------------------------------------------------------
 * Architecture: behaviour tree, ticked at 10 Hz from the root. The root is a
 * reactive Selector (re-evaluated every tick, so a higher-priority branch
 * interrupts a running lower one); Sequences keep their place while RUNNING.
 *
 * Root ?
 * ├─ → Engage            [SeesPlayer] ? ( → [InRange] AimAndFire | CloseIn )
 * ├─ → HuntLastSeen      [SawRecently] StalkToLastSeen
 * ├─ → ReactToGunfire    [HeardGunfire] ? ( → [GunfireClose] TakeCover
 * │                                         | FlankGunfire )
 * ├─ → ReactToFootsteps  [HeardFootsteps] SneakToNoise  Ambush(4 s)
 * ├─ → FollowOrders      [HasDirective] MoveToDirective
 * └─ → Prowl             PickProwlPoint  WalkTo  Wait(2-4 s)
 *
 * Hearing is the Hunter's speciality (hearing x1.7). Noises come from the
 * propagation model in agents/perception.js (radius, path distance around
 * walls, wall muffling). Tactics differ by what it heard:
 *   gunfire   -> loud and dangerous: run to a FLANK point 12 m to the side of
 *                the shot (or dive behind a tree if it is close)
 *   footsteps -> prey nearby: crouch, SNEAK silently to the sound, then wait
 *                in AMBUSH facing it
 * Camouflage: woodland-camo ghillie model, prowls the forest.
 * Role: Systems Engineer's IS agent (Student 2).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const SUCCESS = 'SUCCESS', FAILURE = 'FAILURE', RUNNING = 'RUNNING';

  // ---------------------------------------------------------------------------
  // Minimal behaviour tree library
  // ---------------------------------------------------------------------------
  class Node {
    constructor(name) { this.name = name; }
    reset() {}
    run(a) {
      const s = this.tick(a);
      a.btTrace.push(this.name + ':' + s[0]);
      return s;
    }
  }
  class Selector extends Node {
    constructor(name, children) { super(name); this.children = children; this.last = -1; }
    tick(a) {
      for (let i = 0; i < this.children.length; i++) {
        const s = this.children[i].run(a);
        if (s !== FAILURE) {
          if (this.last >= 0 && this.last !== i) this.children[this.last].reset();
          this.last = s === RUNNING ? i : -1;
          return s;
        }
      }
      if (this.last >= 0) this.children[this.last].reset();
      this.last = -1;
      return FAILURE;
    }
    reset() { for (const c of this.children) c.reset(); this.last = -1; }
  }
  class Sequence extends Node {
    constructor(name, children) { super(name); this.children = children; this.i = 0; }
    tick(a) {
      // Reactive guards: conditions before the running child are re-checked
      // every tick, so a running branch aborts as soon as its premise fails.
      for (let k = 0; k < this.i; k++) {
        const c = this.children[k];
        if (c instanceof Condition && c.run(a) === FAILURE) { this.reset(); return FAILURE; }
      }
      while (this.i < this.children.length) {
        const s = this.children[this.i].run(a);
        if (s === RUNNING) return RUNNING;
        if (s === FAILURE) { this.reset(); return FAILURE; }
        this.i++;
      }
      this.reset();
      return SUCCESS;
    }
    reset() { this.i = 0; for (const c of this.children) c.reset(); }
  }
  class Condition extends Node {
    constructor(name, fn) { super(name); this.fn = fn; }
    tick(a) { return this.fn(a) ? SUCCESS : FAILURE; }
  }
  class Action extends Node {
    constructor(name, fn, onReset) { super(name); this.fn = fn; this.mem = {}; this.onReset = onReset; }
    tick(a) { return this.fn(a, this.mem); }
    reset() { this.mem = {}; if (this.onReset) this.onReset(); }
  }
  const Wait = (name, min, max) => new Action(name, (a, m) => {
    if (m.until === undefined) m.until = BI.Engine.time + min + Math.random() * (max - min);
    return BI.Engine.time >= m.until ? SUCCESS : RUNNING;
  });
  BI.BT = { Selector, Sequence, Condition, Action, Wait, SUCCESS, FAILURE, RUNNING };

  // ---------------------------------------------------------------------------
  // Leaf behaviours
  // ---------------------------------------------------------------------------
  const FOOTSTEP_TYPES = ['footstep', 'sprint', 'jump', 'land', 'door', 'crate', 'melee'];

  function moveTo(a, m, target, speed, arriveDist) {
    a.intent.move = target;
    a.intent.speed = speed;
    if (a.pos.distanceTo(target) < (arriveDist || 1.4) || a.controller.arrived) return SUCCESS;
    if (a.controller.failed > 2) return FAILURE;
    if (m.t0 === undefined) m.t0 = BI.Engine.time;
    if (BI.Engine.time - m.t0 > 25) return FAILURE;
    return RUNNING;
  }

  function buildTree() {
    const engage = new Sequence('Engage', [
      new Condition('SeesPlayer', (a) => a.perception.detected && a.perception.seenRecently(0.6)),
      new Selector('EngageMode', [
        new Sequence('Shoot', [
          new Condition('InRange', (a) => a.distToPlayer() < 32 * a.perception.rangeMul() && a.perception.canSee),
          new Action('AimAndFire', (a) => {
            Object.assign(a.intent, { move: null, aim: true, fire: true, crouch: a.distToPlayer() > 14, look: a.playerChest() });
            return a.perception.canSee ? RUNNING : FAILURE;
          }),
        ]),
        new Action('CloseIn', (a, m) => {
          Object.assign(a.intent, { aim: true, fire: false, crouch: false, look: null });
          return moveTo(a, m, BI.player.pos.clone(), 'run', 6);
        }),
      ]),
    ]);

    const huntLastSeen = new Sequence('HuntLastSeen', [
      new Condition('SawRecently', (a) => a.perception.seenRecently(10) && !a.perception.canSee && a.perception.lastSeenPos),
      new Action('StalkToLastSeen', (a, m) => {
        Object.assign(a.intent, { aim: true, fire: false, crouch: false, look: null });
        if (!m.target) m.target = a.perception.lastSeenPos.clone();
        const s = moveTo(a, m, m.target, 'run', 1.8);
        if (s === SUCCESS) a.perception.lastSeenTime = -1e9; // searched it, forget
        return s;
      }),
    ]);

    const gunfire = new Sequence('ReactToGunfire', [
      new Condition('HeardGunfire', (a) => {
        const n = a.perception.recentNoise(['gunfire', 'generator', 'alarm'], 6);
        if (n && n.loudness > 0.06) { a.bb.noise = n; return true; }
        return false;
      }),
      new Selector('GunfireTactic', [
        new Sequence('CloseFire', [
          new Condition('GunfireClose', (a) => a.pos.distanceTo(a.bb.noise.pos) < 16),
          new Action('TakeCover', (a, m) => {
            if (!m.cover) m.cover = a.findTreeCover(a.bb.noise.pos) || a.pos.clone();
            Object.assign(a.intent, { aim: true, fire: false, crouch: true, look: a.bb.noise.pos.clone().setY(a.bb.noise.pos.y + 1.4) });
            const s = moveTo(a, m, m.cover, 'run', 1.0);
            if (s !== RUNNING) {
              m.hold = m.hold || BI.Engine.time + 2.5;
              a.intent.move = null;
              return BI.Engine.time > m.hold ? SUCCESS : RUNNING;
            }
            return s;
          }),
        ]),
        new Action('FlankGunfire', (a, m) => {
          const n = a.bb.noise;
          if (!m.flank) {
            m.flank = a.flankPoint(n.pos);
            m.phase = 0;
          }
          Object.assign(a.intent, { aim: true, fire: false, crouch: false, look: null });
          if (m.phase === 0) {
            const s = moveTo(a, m, m.flank, 'run', 2.0);
            if (s === RUNNING) return RUNNING;
            m.phase = 1;
            m.t0 = undefined;
          }
          // close in on the shot from the flank, slower
          a.intent.look = n.pos.clone().setY(n.pos.y + 1.4);
          return moveTo(a, m, n.pos, 'walk', 4);
        }),
      ]),
    ]);

    const footsteps = new Sequence('ReactToFootsteps', [
      new Condition('HeardFootsteps', (a) => {
        const n = a.perception.recentNoise(FOOTSTEP_TYPES, 5);
        if (n && n.loudness > 0.04) { a.bb.step = n; return true; }
        return false;
      }),
      new Action('SneakToNoise', (a, m) => {
        if (!m.target) m.target = a.bb.step.pos.clone();
        // keep updating while new steps are heard
        if (a.bb.step.time > (m.stamp || 0)) { m.target.copy(a.bb.step.pos); m.stamp = a.bb.step.time; }
        Object.assign(a.intent, { aim: true, fire: false, crouch: true, look: null });
        return moveTo(a, m, m.target, 'sneak', 3.0);
      }),
      new Action('Ambush', (a, m) => {
        if (!m.until) m.until = BI.Engine.time + 4;
        Object.assign(a.intent, { move: null, aim: true, fire: false, crouch: true, look: a.bb.step.pos.clone().setY(a.bb.step.pos.y + 1.2) });
        if (BI.Engine.time > m.until) { a.perception.noises.length = 0; return SUCCESS; }
        return RUNNING;
      }),
    ]);

    const orders = new Sequence('FollowOrders', [
      new Condition('HasDirective', (a) => !!a.directive()),
      new Action('MoveToDirective', (a, m) => {
        const d = a.directive();
        Object.assign(a.intent, { aim: true, fire: false, crouch: false, look: null });
        const target = m.point || (d.type === 'search' ? BI.Nav.randomPointNear(d.pos.x, d.pos.z, d.radius || 10) || d.pos : d.pos);
        m.point = target;
        const s = moveTo(a, m, target, d.type === 'regroup' ? 'walk' : 'run', 2.5);
        if (s === SUCCESS && d.type !== 'converge') m.point = null; // keep searching
        if (s === SUCCESS && d.type === 'converge') { BI.Blackboard.clearDirective(a.name); }
        return s === FAILURE ? FAILURE : RUNNING;
      }),
    ]);

    const prowl = new Sequence('Prowl', [
      new Action('PickProwlPoint', (a) => {
        a.bb.prowl = a.prowlPoints[(a.prowlIdx = (a.prowlIdx + 1 + Math.floor(Math.random() * 2)) % a.prowlPoints.length)];
        return SUCCESS;
      }),
      new Action('WalkTo', (a, m) => {
        Object.assign(a.intent, { aim: false, fire: false, crouch: false, look: null });
        return moveTo(a, m, a.bb.prowl, 'walk', 2);
      }),
      new Action('LookAround', (a, m) => {
        a.intent.move = null;
        if (!m.until) m.until = BI.Engine.time + 2 + Math.random() * 2;
        const t = BI.Engine.time;
        a.intent.look = a.pos.clone().add(new THREE.Vector3(Math.sin(t * 0.8) * 5, 1.4, Math.cos(t * 0.8) * 5));
        return t > m.until ? SUCCESS : RUNNING;
      }),
    ]);

    return new Selector('Root', [engage, huntLastSeen, gunfire, footsteps, orders, prowl]);
  }

  class Hunter extends BI.Agent {
    constructor(spec, idx, parent) {
      const y = BI.Terrain.height(spec.x, spec.z);
      super({
        name: 'Hunter-' + (idx + 1), type: 'hunter', arch: 'BT', rig: BI.Models.buildHumanoid('hunter'), parent,
        x: spec.x, y, z: spec.z, hp: 90, eyeHeight: 1.6,
        weapon: { damage: 12, burst: 1, interval: 0.1, pause: 1.35, spread: 0.024, range: 45, reaction: 0.7, color: 0xffc070 },
        perception: { fov: 2.0, range: 25, hearing: 1.7 },
        move: { walk: 1.6, run: 4.5, sneak: 1.15, turnRate: 6 },
      });
      this.prowlPoints = spec.prowl.map(([x, z]) => new THREE.Vector3(x, BI.Terrain.height(x, z), z));
      this.prowlIdx = 0;
      this.tree = buildTree();
      this.bb = {}; // the tree's private scratch memory
      this.btTrace = [];
      this.activeBranch = '';
    }

    think() {
      this.btTrace = [];
      this.tree.run(this);
      // summarise the running path for the debug overlay
      const running = this.btTrace.filter((s) => s.endsWith(':R')).map((s) => s.split(':')[0]).reverse();
      const success = this.btTrace.filter((s) => s.endsWith(':S')).map((s) => s.split(':')[0]);
      this.activeBranch = running.length ? running.join(' > ') : success.slice(-2).join(' > ');
      this.state = running[1] || running[0] || 'Prowl';
    }

    playerChest() {
      const p = BI.player;
      return new THREE.Vector3(p.pos.x, p.pos.y + p.height * 0.6, p.pos.z);
    }

    // Point 12 m to the side of the noise, on the hunter's side, walkable.
    flankPoint(noisePos) {
      const to = noisePos.clone().sub(this.pos);
      to.y = 0;
      const d = to.length() || 1;
      to.divideScalar(d);
      const side = new THREE.Vector3(-to.z, 0, to.x);
      const sign = Math.random() < 0.5 ? 1 : -1;
      for (const s of [sign, -sign]) {
        const p = noisePos.clone().addScaledVector(side, 12 * s).addScaledVector(to, -4);
        const q = BI.Nav.randomPointNear(p.x, p.z, 3);
        if (q) return q;
      }
      return BI.Nav.randomPointNear(this.pos.x, this.pos.z, 6) || this.pos.clone();
    }

    // Nearest tree trunk that is between us and the threat (cover).
    findTreeCover(threat) {
      let best = null, bd = 1e9;
      for (const t of BI.Forest.trees) {
        const d = Math.hypot(t.x - this.pos.x, t.z - this.pos.z);
        if (d > 12) continue;
        const tx = t.x - threat.x, tz = t.z - threat.z;
        const l = Math.hypot(tx, tz) || 1;
        const cx = t.x + (tx / l) * 0.9, cz = t.z + (tz / l) * 0.9; // the far side of the trunk
        if (!BI.Nav.walkableAt(cx, cz)) continue;
        if (d < bd) { bd = d; best = new THREE.Vector3(cx, BI.Terrain.height(cx, cz), cz); }
      }
      return best;
    }

    debugLines() {
      return [this.name + ' [BT] ' + this.state, '  ' + this.activeBranch];
    }
  }

  BI.Hunter = Hunter;
})();
