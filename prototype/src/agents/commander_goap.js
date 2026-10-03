/* =============================================================================
 * agents/commander_goap.js — COMMANDER  ::  GOAP PLANNER + SHARED BLACKBOARD
 * -----------------------------------------------------------------------------
 * Architecture: Goal-Oriented Action Planning. The Commander does not script
 * a reaction; it builds a WORLD STATE from the blackboard, picks a GOAL, and an
 * A* planner searches for the cheapest sequence of ACTIONS whose effects reach
 * the goal from the current state. The plan is executed step by step and
 * re-planned whenever an action fails or the world state invalidates it.
 *
 * World state (booleans read from the blackboard each tick):
 *   cureStolen, alertActive, playerLocated (sighting < 6 s old),
 *   squadReady (>= 2 units alive and < 2 losses since the last regroup),
 *   playerNeutralized (player dead)
 * Goals:   PlayerLocated      (chosen while the player's position is unknown)
 *          PlayerNeutralized  (chosen once the player is located)
 * Actions (preconditions -> effects, cost):
 *   BroadcastAlert   cureStolen, !alertActive        -> alertActive        1
 *   OrderRegroup     alertActive, !squadReady        -> squadReady         2
 *   OrderSearchZone  alertActive, !playerLocated     -> playerLocated      3
 *   OrderConverge    alertActive, playerLocated,
 *                    squadReady                      -> playerNeutralized  2
 * Orders are DIRECTIVES written to the blackboard; the FSM, BT and Utility AI
 * each read them in their own way. The Commander "detects" the cure being
 * taken through the cure:taken event (lab sensors) and broadcasts the alert.
 * Body: humanoid in a lab coat with a headset; weak sidearm, keeps distance.
 * Role: Agent Controller's IS agent (Student 4).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const ACTIONS = [
    { name: 'BroadcastAlert', pre: { cureStolen: true, alertActive: false }, eff: { alertActive: true }, cost: 1 },
    { name: 'OrderRegroup', pre: { alertActive: true, squadReady: false }, eff: { squadReady: true }, cost: 2 },
    { name: 'OrderSearchZone', pre: { alertActive: true, playerLocated: false }, eff: { playerLocated: true }, cost: 3 },
    { name: 'OrderConverge', pre: { alertActive: true, playerLocated: true, squadReady: true }, eff: { playerNeutralized: true }, cost: 2 },
  ];
  const GOALS = {
    PlayerLocated: { playerLocated: true },
    PlayerNeutralized: { playerNeutralized: true },
  };

  // A* over boolean world states. Returns [actionName, ...] or null.
  function plan(start, goal) {
    const sat = (s, cond) => Object.keys(cond).every((k) => !!s[k] === cond[k]);
    const key = (s) => Object.keys(s).sort().map((k) => k + (s[k] ? 1 : 0)).join('');
    const h = (s) => Object.keys(goal).filter((k) => !!s[k] !== goal[k]).length;
    const open = [{ s: start, g: 0, f: h(start), plan: [] }];
    const seen = new Set();
    let it = 0;
    while (open.length && it++ < 200) {
      open.sort((a, b) => a.f - b.f);
      const n = open.shift();
      if (sat(n.s, goal)) return n.plan;
      const k = key(n.s);
      if (seen.has(k)) continue;
      seen.add(k);
      if (n.plan.length >= 5) continue;
      for (const a of ACTIONS) {
        if (!sat(n.s, a.pre)) continue;
        const s2 = Object.assign({}, n.s, a.eff);
        open.push({ s: s2, g: n.g + a.cost, f: n.g + a.cost + h(s2), plan: n.plan.concat(a.name) });
      }
    }
    return null;
  }

  class Commander extends BI.Agent {
    constructor(spec, parent) {
      const y = BI.Physics.groundAt(spec.x, spec.z, BI.Level.BUNKER_Y + 0.5).y;
      super({
        name: 'Commander', type: 'commander', arch: 'GOAP', rig: BI.Models.buildHumanoid('commander'), parent,
        x: spec.x, y, z: spec.z, hp: 70, yaw: Math.PI,
        weapon: { damage: 5, burst: 2, interval: 0.25, pause: 1.2, spread: 0.05, range: 22, reaction: 0.8, color: 0xffd090 },
        perception: { fov: 2.0, range: 18, hearing: 0.9 },
        move: { walk: 1.3, run: 3.4 },
      });
      this.postPos = new THREE.Vector3(spec.post[0], y, spec.post[1]);
      this.goal = 'none';
      this.plan = [];
      this.step = 0;
      this.actionT = 0;
      this.lastIssue = 0;
      this.planLog = [];
      this.worldState = {};
      BI.Events.on('cure:taken', () => {
        BI.Blackboard.set('cureStolen', true);
        if (this.alive) BI.Events.emit('notify', { text: 'COMMANDER: "The cure! Lock the facility down!"', kind: 'warn' });
      });
    }

    units() {
      return BI.Agents.list.filter((a) => a.alive && a !== this);
    }

    readWorld() {
      const BB = BI.Blackboard.data;
      const units = this.units();
      return {
        cureStolen: !!BB.cureStolen,
        alertActive: !!BB.alert,
        playerLocated: !!BB.playerLKP && BI.Blackboard.sightingAge() < 6,
        squadReady: units.length >= 2 && BB.casualtiesSinceRegroup < 2,
        playerNeutralized: !BI.player.alive,
      };
    }

    think(dt) {
      const ws = (this.worldState = this.readWorld());
      // ---- own body: stay at the console, defend if seen ---------------------------
      this.body();
      if (!ws.cureStolen && !ws.alertActive) {
        this.goal = 'none (monitoring)';
        this.state = 'Monitor';
        return;
      }
      // ---- goal selection ----------------------------------------------------------------
      const goal = ws.playerNeutralized ? 'PlayerNeutralized' : ws.playerLocated ? 'PlayerNeutralized' : 'PlayerLocated';
      const needPlan = goal !== this.goal || this.step >= this.plan.length || !this.preOk(ws);
      if (needPlan) {
        this.goal = goal;
        this.plan = plan(ws, GOALS[goal]) || [];
        this.step = 0;
        this.actionT = 0;
        this.planLog.unshift(BI.Engine.time.toFixed(1) + 's ' + goal + ': ' + (this.plan.join(' → ') || '(no plan)'));
        if (this.planLog.length > 4) this.planLog.pop();
      }
      if (!this.plan.length) { this.state = 'Idle'; return; }
      const name = this.plan[this.step];
      this.state = name;
      this.actionT += dt;
      const r = this[name](ws, dt);
      if (r === 'done') { this.step++; this.actionT = 0; }
      else if (r === 'failed') { this.plan = []; this.goal = 'replan'; }
    }

    preOk(ws) {
      const name = this.plan[this.step];
      const a = ACTIONS.find((x) => x.name === name);
      if (!a) return false;
      return Object.keys(a.pre).every((k) => !!ws[k] === a.pre[k]);
    }

    lkp() {
      const BB = BI.Blackboard.data;
      return BB.playerLKP ? BB.playerLKP.clone() : new THREE.Vector3(BI.Level.cure.x, BI.Level.BUNKER_Y, BI.Level.cure.z);
    }

    // ---- actions -------------------------------------------------------------------------
    BroadcastAlert() {
      if (this.actionT < 0.6) return 'running'; // keys the headset
      const BB = BI.Blackboard;
      BB.set('alert', true);
      BB.set('alertReason', 'cure stolen');
      BB.set('alertTime', BI.Engine.time);
      if (!BB.data.playerLKP) BB.reportSighting(this, BI.player.pos);
      const pos = this.lkp();
      for (const u of this.units()) BB.issue(u.name, { type: 'converge', pos, by: this.name });
      BI.Events.emit('alert:broadcast', { by: this.name, pos, reason: 'cure stolen' });
      return 'done';
    }

    OrderConverge(ws) {
      if (!ws.playerLocated) return 'failed';
      if (BI.Engine.time - this.lastIssue > 1.0) {
        this.lastIssue = BI.Engine.time;
        const pos = this.lkp();
        for (const u of this.units()) BI.Blackboard.issue(u.name, { type: 'converge', pos, by: this.name });
      }
      return ws.playerNeutralized ? 'done' : 'running';
    }

    OrderSearchZone(ws) {
      if (ws.playerLocated) return 'done';
      if (this.actionT < 0.15 || BI.Engine.time - this.lastIssue > 8) {
        this.lastIssue = BI.Engine.time;
        const c = this.lkp();
        const units = this.units();
        units.forEach((u, i) => {
          const a = (i / Math.max(1, units.length)) * Math.PI * 2;
          const p = c.clone().add(new THREE.Vector3(Math.cos(a) * 7, 0, Math.sin(a) * 7));
          BI.Blackboard.issue(u.name, { type: 'search', pos: p, radius: 9, by: this.name });
        });
      }
      return this.actionT > 24 ? 'failed' : 'running';
    }

    OrderRegroup() {
      const rally = this.lkp().lerp(this.pos, 0.5);
      if (this.actionT < 0.15) {
        BI.Blackboard.set('rally', rally);
        for (const u of this.units()) BI.Blackboard.issue(u.name, { type: 'regroup', pos: rally, by: this.name });
      }
      const near = this.units().filter((u) => u.pos.distanceTo(rally) < 10).length;
      if (near >= 2 || this.actionT > 12) {
        BI.Blackboard.data.casualtiesSinceRegroup = 0;
        return 'done';
      }
      return 'running';
    }

    // The Commander's own body: hold the console, shoot back weakly, back off when hurt.
    body() {
      const per = this.perception, it = this.intent;
      const hurtRecently = BI.Engine.time - this.hurtTime < 4;
      const retreat = new THREE.Vector3(-6, this.pos.y, -140.5);
      if (per.detected && per.canSee) {
        Object.assign(it, { aim: true, fire: true, look: this.playerChest(), move: hurtRecently ? retreat : null, speed: 'run' });
      } else {
        const target = hurtRecently ? retreat : this.postPos;
        const at = this.pos.distanceTo(target) < 1.0;
        Object.assign(it, { aim: false, fire: false, move: at ? null : target, speed: 'walk', look: at ? new THREE.Vector3(6.4, this.pos.y + 1.4, -142) : null });
      }
    }

    playerChest() {
      const p = BI.player;
      return new THREE.Vector3(p.pos.x, p.pos.y + p.height * 0.6, p.pos.z);
    }

    debugLines() {
      return [
        'Commander [GOAP] goal: ' + this.goal,
        '  plan: ' + (this.plan.map((n, i) => (i === this.step ? '[' + n + ']' : n)).join(' → ') || '-'),
      ];
    }
  }

  Commander.ACTIONS = ACTIONS;
  Commander.plan = plan;
  BI.Commander = Commander;
})();
