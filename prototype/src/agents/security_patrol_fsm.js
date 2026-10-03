/* =============================================================================
 * agents/security_patrol_fsm.js — SECURITY PATROL  ::  FINITE STATE MACHINE
 * -----------------------------------------------------------------------------
 * Architecture: classic FSM. Exactly one state is active; each state has
 * enter() and update(); transitions are explicit and logged (debug overlay).
 *
 *            sees player (awareness >= 1)
 *   Patrol ───────────────────────────────▶ Pursue ◀────────┐
 *     │  ▲  noise / glimpse                  │  in range +    │ lost LOS > 1 s
 *     ▼  │                                   ▼  line of sight │
 *   Investigate ── arrived, nothing ─▶ Patrol   Attack ───────┘
 *                                          │ lost sight > 2.5 s
 *   Search ◀───────────────────────────────┘ (search around last known pos)
 *     └── timer expires ─▶ Patrol (or keeps searching while the alarm is on)
 *
 * Perception: vision cone + range + line-of-sight raycast against walls
 * (agents/perception.js). Commander directives (blackboard) are transitions:
 * converge -> Pursue(directive pos), search -> Search(zone), regroup ->
 * Investigate(rally point). After the blackout the patrol is "heightened":
 * 25% faster and sees 40% further.
 * Role: World Builder's IS agent (Student 1).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const ATTACK_RANGE = 22;

  const STATES = {
    Patrol: {
      enter(a) {
        Object.assign(a.intent, { speed: 'walk', aim: false, fire: false, look: null, crouch: false });
        a.waitT = 0;
      },
      update(a, dt) {
        const per = a.perception;
        if (per.detected) return a.go(a.inAttackRange() ? 'Attack' : 'Pursue', null, 'saw player');
        if (a.checkDirective()) return;
        if (per.canSee && per.awareness > 0.35) return a.go('Investigate', { pos: BI.player.pos.clone() }, 'glimpse');
        const n = per.recentNoise(null, 1.5);
        if (n && n.loudness > 0.1 && n.time > a.handledNoise) {
          a.handledNoise = n.time;
          return a.go('Investigate', { pos: n.pos, urgent: n.type === 'gunfire' }, 'heard ' + n.type);
        }
        if (BI.Blackboard.data.alert) return a.go('Search', { pos: BI.Blackboard.data.playerLKP || a.pos.clone(), radius: 10 }, 'alarm');
        // follow the route, pausing at each waypoint
        const wp = a.route[a.routeIdx];
        if (a.pos.distanceTo(wp) < 0.9 || a.controller.arrived) {
          a.intent.move = null;
          a.waitT += dt;
          const t = BI.Engine.time;
          a.intent.look = a.pos.clone().add(new THREE.Vector3(Math.sin(t * 0.7 + a.routeIdx) * 4, 1.5, Math.cos(t * 0.7 + a.routeIdx) * 4));
          if (a.waitT > 2.2) {
            a.routeIdx = (a.routeIdx + 1) % a.route.length;
            a.waitT = 0;
            a.intent.look = null;
          }
        } else {
          a.intent.move = wp;
          a.intent.look = null;
        }
      },
    },

    Investigate: {
      enter(a, d) {
        Object.assign(a.intent, { speed: d.urgent ? 'run' : 'walk', aim: !!d.urgent, fire: false, look: null });
        a.fsm.target = d.pos.clone();
        a.fsm.arriveT = 0;
      },
      update(a, dt) {
        const per = a.perception;
        if (per.detected) return a.go(a.inAttackRange() ? 'Attack' : 'Pursue', null, 'saw player');
        if (a.checkDirective()) return;
        const n = per.recentNoise(['gunfire'], 1.0);
        if (n && n.time > a.handledNoise) {
          a.handledNoise = n.time;
          a.fsm.target = n.pos.clone();
          a.intent.speed = 'run';
        }
        a.intent.move = a.fsm.target;
        if (a.pos.distanceTo(a.fsm.target) < 1.5 || a.controller.arrived || a.controller.failed > 1) {
          a.intent.move = null;
          a.fsm.arriveT += dt;
          const t = a.fsm.arriveT;
          a.intent.look = a.pos.clone().add(new THREE.Vector3(Math.sin(t * 1.5) * 5, 1.4, Math.cos(t * 1.5) * 5));
          if (t > 3.5) a.go(BI.Blackboard.data.alert ? 'Search' : 'Patrol', { pos: a.fsm.target, radius: 8 }, 'nothing found');
        }
      },
    },

    Pursue: {
      enter(a, d) {
        Object.assign(a.intent, { speed: 'run', aim: false, fire: false, look: null, crouch: false });
        a.fsm.target = d && d.pos ? d.pos.clone() : a.perception.lastSeenPos ? a.perception.lastSeenPos.clone() : a.pos.clone();
      },
      update(a) {
        const per = a.perception;
        if (per.canSee && per.detected) {
          a.fsm.target.copy(BI.player.pos);
          if (a.inAttackRange()) return a.go('Attack', null, 'in range');
        } else if (per.lastSeenPos && per.lastSeenTime > (a.fsm.enteredAt || 0)) {
          a.fsm.target.copy(per.lastSeenPos);
        }
        const d = a.directive();
        if (d && d.type === 'converge' && !per.seenRecently(2)) a.fsm.target.copy(d.pos);
        a.intent.move = a.fsm.target;
        a.intent.aim = per.seenRecently(1.5);
        a.intent.look = per.canSee ? a.playerChest() : null;
        const lost = BI.Engine.time - per.lastSeenTime;
        const reached = a.pos.distanceTo(a.fsm.target) < 2 || a.controller.arrived;
        if ((reached && !per.canSee) || (lost > 2.5 && a.fsm.t > 4)) a.go('Search', { pos: a.fsm.target, radius: 9 }, 'lost the player');
      },
    },

    Attack: {
      enter(a) {
        Object.assign(a.intent, { speed: 'walk', aim: true, fire: true, move: null, crouch: false });
        a.fsm.strafeT = 0;
        a.fsm.blindT = 0;
      },
      update(a, dt) {
        const per = a.perception;
        a.intent.look = a.playerChest();
        a.intent.fire = per.canSee;
        if (!per.canSee) a.fsm.blindT += dt;
        else a.fsm.blindT = 0;
        if (a.fsm.blindT > 1.0) return a.go('Pursue', { pos: per.lastSeenPos || BI.player.pos }, 'lost line of sight');
        if (a.distToPlayer() > ATTACK_RANGE * a.perception.rangeMul() * 1.15) return a.go('Pursue', null, 'too far');
        // short strafes so it is not a static target
        a.fsm.strafeT -= dt;
        if (a.fsm.strafeT <= 0) {
          a.fsm.strafeT = 1.6 + Math.random() * 1.4;
          const side = new THREE.Vector3(Math.cos(a.yaw), 0, -Math.sin(a.yaw)).multiplyScalar((Math.random() < 0.5 ? -1 : 1) * (1 + Math.random() * 1.5));
          const pt = a.pos.clone().add(side);
          a.intent.move = BI.Nav.walkableAt(pt.x, pt.z) ? pt : null;
        }
        if (a.intent.move && a.pos.distanceTo(a.intent.move) < 0.4) a.intent.move = null;
      },
    },

    Search: {
      enter(a, d) {
        Object.assign(a.intent, { speed: 'walk', aim: true, fire: false, look: null });
        a.fsm.center = (d && d.pos ? d.pos : a.pos).clone();
        a.fsm.radius = (d && d.radius) || 8;
        a.fsm.duration = BI.Blackboard.data.alert ? 30 : 14;
        a.fsm.point = null;
        a.fsm.lookT = 0;
      },
      update(a, dt) {
        const per = a.perception;
        if (per.detected && per.canSee) return a.go(a.inAttackRange() ? 'Attack' : 'Pursue', null, 'found player');
        if (a.checkDirective()) return;
        if (!a.fsm.point || a.pos.distanceTo(a.fsm.point) < 1.2 || a.controller.arrived || a.controller.failed > 1) {
          a.fsm.lookT += dt;
          a.intent.move = null;
          const t = a.fsm.lookT;
          a.intent.look = a.pos.clone().add(new THREE.Vector3(Math.sin(t * 2) * 5, 1.4, Math.cos(t * 2) * 5));
          if (t > 1.6 || !a.fsm.point) {
            a.fsm.point = BI.Nav.randomPointNear(a.fsm.center.x, a.fsm.center.z, a.fsm.radius) || a.fsm.center.clone();
            a.fsm.lookT = 0;
            a.controller.failed = 0;
          }
        } else {
          a.intent.move = a.fsm.point;
          a.intent.look = null;
        }
        if (a.fsm.t > a.fsm.duration) {
          BI.Blackboard.clearDirective(a.name);
          a.go(BI.Blackboard.data.alert ? 'Search' : 'Patrol', { pos: BI.Blackboard.data.playerLKP || a.home, radius: 12 }, 'search timed out');
        }
      },
    },
  };

  class SecurityPatrol extends BI.Agent {
    constructor(spec, idx, parent) {
      const y = BI.Physics.groundAt(spec.x, spec.z, BI.Nav.baseY(spec.x, spec.z) + 0.5).y;
      super({
        name: 'Patrol-' + (idx + 1), type: 'patrol', arch: 'FSM', rig: BI.Models.buildHumanoid('patrol'), parent,
        x: spec.x, y, z: spec.z, hp: 80,
        weapon: { damage: 7, burst: 3, interval: 0.12, pause: 1.05, spread: 0.034, range: 34, reaction: 0.6 },
        perception: { fov: 1.95, range: 22, hearing: 1.0 },
        move: { walk: 1.55, run: 4.1, turnRate: 5 },
      });
      this.route = spec.route.map(([x, z]) => new THREE.Vector3(x, BI.Nav.baseY(x, z), z));
      this.routeIdx = 0;
      this.handledNoise = -1;
      this.lastDirective = -1;
      this.transitions = [];
      this.fsm = { state: null, t: 0 };
      this.go('Patrol', null, 'spawn');
    }

    go(name, data, reason) {
      this.fsm = { state: name, t: 0, enteredAt: BI.Engine.time };
      this.state = name;
      this.stateReason = reason || '';
      this.transitions.push(name + ' (' + (reason || '') + ')');
      if (this.transitions.length > 4) this.transitions.shift();
      STATES[name].enter(this, data || {});
    }

    think(dt) {
      this.fsm.t += dt;
      STATES[this.fsm.state].update(this, dt);
    }

    inAttackRange() {
      return this.perception.canSee && this.distToPlayer() < ATTACK_RANGE * this.perception.rangeMul();
    }
    playerChest() {
      const p = BI.player;
      return new THREE.Vector3(p.pos.x, p.pos.y + p.height * 0.6, p.pos.z);
    }

    // Commander directives are just more transitions for the FSM.
    checkDirective() {
      const d = this.directive();
      if (!d || d.issuedAt <= this.lastDirective) return false;
      this.lastDirective = d.issuedAt;
      if (d.type === 'converge') this.go('Pursue', { pos: d.pos }, 'order: converge');
      else if (d.type === 'search') this.go('Search', { pos: d.pos, radius: d.radius || 10 }, 'order: search zone');
      else if (d.type === 'regroup') this.go('Investigate', { pos: d.pos }, 'order: regroup');
      return true;
    }

    debugLines() {
      return [this.name + ' [FSM] ' + this.state, '  ' + this.stateReason];
    }
  }

  BI.SecurityPatrol = SecurityPatrol;
})();
