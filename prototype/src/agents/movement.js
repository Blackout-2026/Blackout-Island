/* =============================================================================
 * agents/movement.js — AgentController: shared pathfinding + movement layer
 * -----------------------------------------------------------------------------
 * The decision layers (FSM / BT / Utility / GOAP) only fill agent.intent:
 *   { move: Vector3|null, speed: 'walk'|'run'|'sneak', look: Vector3|null,
 *     aim: bool, fire: bool, crouch: bool }
 * This layer carries the intent out, identically for every agent type:
 *   1. requests A* paths (re-plans when the goal moves, the nav grid changes
 *      or the agent is stuck); short clear hops skip A* entirely
 *   2. path following with arrival slow-down, acceleration limits (no sliding)
 *   3. separation steering from other agents and the player
 *   4. capsule collision against the world + ground snapping (stairs, ramp)
 *   5. opens unlocked doors on its way
 *   6. turn-rate limited facing (body toward motion or look target, head
 *      toward the look target)
 *   7. drives procedural animation (idle/walk/run/aim/crouch blend)
 * Role: Agent Controller.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  class AgentController {
    constructor(agent, cfg) {
      this.agent = agent;
      this.radius = cfg.radius || 0.38;
      this.speeds = { walk: cfg.walk || 1.5, run: cfg.run || 4.0, sneak: cfg.sneak || 1.0 };
      this.accel = cfg.accel || 7;
      this.turnRate = cfg.turnRate || 5;
      this.vx = 0;
      this.vz = 0;
      this.path = null;
      this.pathIdx = 0;
      this.pathGoal = null;
      this.pending = false;
      this.repathT = 0;
      this.navVersion = -1;
      this.stuckT = 0;
      this.stuckRef = new THREE.Vector3();
      this.aimBlend = 0;
      this.crouchBlend = 0;
      this.lookYaw = 0;
      this.failed = 0;
      this.arrived = false;
    }

    speedFor(kind) {
      const mul = this.agent.speedMul ? this.agent.speedMul() : 1;
      return (this.speeds[kind] || this.speeds.walk) * mul;
    }

    requestPath(goal) {
      const ag = this.agent;
      this.pathGoal = goal.clone();
      this.navVersion = BI.Nav.version;
      this.repathT = 2.5 + Math.random();
      // short, clear hop: no A* needed
      if (ag.pos.distanceTo(goal) < 6 && BI.Nav.walkableAt(goal.x, goal.z) && BI.AStar.clearLine(ag.pos.x, ag.pos.z, ag.pos.y, goal.x, goal.z)) {
        this.path = [goal.clone()];
        this.pathIdx = 0;
        return;
      }
      this.pending = true;
      BI.AStar.request(ag, ag.pos, goal, (path) => {
        this.pending = false;
        if (path && path.length) {
          this.path = path;
          this.pathIdx = path.length > 1 ? 1 : 0;
          this.failed = 0;
        } else {
          this.path = null;
          this.failed++;
        }
      });
    }

    update(dt) {
      const ag = this.agent;
      const it = ag.intent;
      const pos = ag.pos;
      const rig = ag.rig;
      if (!ag.alive) {
        rig.pose({ speed: 0, dist: 0, aim: 0 }, dt);
        return;
      }
      // ---- 1. path management ----------------------------------------------------
      this.arrived = false;
      if (it.move) {
        this.repathT -= dt;
        const moved = !this.pathGoal || this.pathGoal.distanceTo(it.move) > (it.speed === 'run' ? 1.6 : 0.9);
        if (!this.pending && (moved || this.repathT <= 0 || this.navVersion !== BI.Nav.version)) this.requestPath(it.move);
      } else {
        this.path = null;
        this.pathGoal = null;
      }
      // ---- 2. desired velocity -------------------------------------------------------
      let dx = 0, dz = 0;
      const speed = this.speedFor(it.speed);
      if (it.move && this.path && this.pathIdx < this.path.length) {
        let wp = this.path[this.pathIdx];
        let tx = wp.x - pos.x, tz = wp.z - pos.z;
        let d = Math.hypot(tx, tz);
        while (d < 0.55 && this.pathIdx < this.path.length - 1) {
          this.pathIdx++;
          wp = this.path[this.pathIdx];
          tx = wp.x - pos.x; tz = wp.z - pos.z;
          d = Math.hypot(tx, tz);
        }
        const last = this.pathIdx === this.path.length - 1;
        if (last && d < 0.4) {
          this.arrived = true;
        } else if (d > 1e-3) {
          const s = last ? speed * Math.min(1, d / 1.4 + 0.15) : speed;
          dx = (tx / d) * s;
          dz = (tz / d) * s;
        }
      } else if (it.move && !this.pending && this.failed > 0 && pos.distanceTo(it.move) < 0.8) {
        this.arrived = true;
      }
      // ---- 3. separation ----------------------------------------------------------------
      for (const o of BI.Agents.list) {
        if (o === ag || !o.alive) continue;
        const ox = pos.x - o.pos.x, oz = pos.z - o.pos.z;
        const d = Math.hypot(ox, oz);
        if (d < 1.3 && d > 1e-4 && Math.abs(pos.y - o.pos.y) < 1.5) {
          const k = (1.3 - d) * 2.4;
          dx += (ox / d) * k;
          dz += (oz / d) * k;
        }
      }
      const p = BI.player;
      if (p && p.alive) {
        const ox = pos.x - p.pos.x, oz = pos.z - p.pos.z;
        const d = Math.hypot(ox, oz);
        if (d < 1.0 && d > 1e-4) { dx += (ox / d) * (1.0 - d) * 3; dz += (oz / d) * (1.0 - d) * 3; }
      }
      // ---- 4. acceleration-limited velocity ------------------------------------------------
      const ddx = dx - this.vx, ddz = dz - this.vz;
      const dl = Math.hypot(ddx, ddz);
      const maxDv = this.accel * dt * (dl > 0 && Math.hypot(dx, dz) < Math.hypot(this.vx, this.vz) ? 1.6 : 1);
      if (dl <= maxDv) { this.vx = dx; this.vz = dz; }
      else { this.vx += (ddx / dl) * maxDv; this.vz += (ddz / dl) * maxDv; }
      // ---- 5. integrate + collide + ground ---------------------------------------------------
      const ox0 = pos.x, oz0 = pos.z;
      pos.x += this.vx * dt;
      pos.z += this.vz * dt;
      const res = BI.Physics.resolveCylinder(pos, this.radius, pos.y, 1.8);
      if (res.hit) {
        const vn = this.vx * res.nx + this.vz * res.nz;
        if (vn < 0) { this.vx -= vn * res.nx; this.vz -= vn * res.nz; }
      }
      const g = BI.Physics.groundAt(pos.x, pos.z, pos.y + 0.5);
      if (g.y > -Infinity) pos.y = U.damp(pos.y, g.y, 18, dt);
      const movedDist = Math.hypot(pos.x - ox0, pos.z - oz0);
      // ---- 6. doors on the way -----------------------------------------------------------------
      if (Math.hypot(this.vx, this.vz) > 0.3) {
        const d = BI.Doors.nearest(pos, 2.4);
        if (d && !d.locked && d.target < 0.5) {
          const to = d.pos.clone().sub(pos);
          if (to.x * this.vx + to.z * this.vz > 0) d.requestOpen(ag.name);
        }
      }
      // ---- 7. facing ----------------------------------------------------------------------------
      const sp = Math.hypot(this.vx, this.vz);
      let targetYaw = ag.yaw;
      if (it.look) targetYaw = Math.atan2(it.look.x - pos.x, it.look.z - pos.z);
      else if (sp > 0.25) targetYaw = Math.atan2(this.vx, this.vz);
      const turn = this.turnRate * (it.aim ? 1.4 : 1) * dt;
      ag.yaw = U.turnToward(ag.yaw, targetYaw, turn);
      // head looks a bit further than the body toward the look target
      const headTarget = it.look ? U.clamp(U.angleDiff(ag.yaw, Math.atan2(it.look.x - pos.x, it.look.z - pos.z)), -0.9, 0.9) : 0;
      this.lookYaw = U.damp(this.lookYaw, headTarget, 6, dt);
      // ---- 8. stuck detection ------------------------------------------------------------------
      if (it.move && !this.arrived && Math.hypot(dx, dz) > 0.4) {
        this.stuckT += dt;
        if (this.stuckT > 1.4) {
          if (this.stuckRef.distanceTo(pos) < 0.35) {
            // nudge sideways and re-plan
            this.vx += (Math.random() - 0.5) * 2.5;
            this.vz += (Math.random() - 0.5) * 2.5;
            this.repathT = 0;
            this.path = null;
            ag.stuckCount = (ag.stuckCount || 0) + 1;
          }
          this.stuckT = 0;
          this.stuckRef.copy(pos);
        }
      } else {
        this.stuckT = 0;
        this.stuckRef.copy(pos);
      }
      // ---- 9. animation -------------------------------------------------------------------------
      this.aimBlend = U.damp(this.aimBlend, it.aim ? 1 : 0, 7, dt);
      this.crouchBlend = U.damp(this.crouchBlend, it.crouch ? 1 : 0, 5, dt);
      let aimPitch = 0;
      if (it.look) {
        const hd = Math.hypot(it.look.x - pos.x, it.look.z - pos.z);
        aimPitch = Math.atan2(it.look.y - (pos.y + 1.4), Math.max(0.5, hd));
      }
      rig.pose({ speed: sp, dist: movedDist, aim: this.aimBlend, aimPitch, crouch: this.crouchBlend, lookYaw: this.lookYaw, sneak: it.speed === 'sneak' }, dt);
      rig.root.position.copy(pos);
      rig.root.rotation.y = ag.yaw;
    }
  }

  BI.AgentController = AgentController;
})();
