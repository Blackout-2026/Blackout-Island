/* =============================================================================
 * agents/perception.js — vision and hearing (sound propagation)
 * -----------------------------------------------------------------------------
 * VISION (per agent, every think tick):
 *   in range? -> inside the horizontal view cone (or very close)? -> line of
 *   sight raycast from the eyes to the player's head and chest (walls, trunks,
 *   doors block; chain-link fences do not). Seeing fills an awareness meter
 *   instead of detecting instantly; the fill rate scales with distance and
 *   with how visible the player is (crouch 0.55x, sprint 1.25x, flashlight on
 *   1.6x, dark outdoors 0.8x). awareness >= 1 means "detected".
 *
 * HEARING (BI.Noise): every `noise` event has a radius. For each listener:
 *   path  = 1 - pathDistance / radius   (Dijkstra flood on the nav grid that
 *           goes around walls and through open doors only)
 *   wall  = (1 - straightDistance / radius) * 0.35^wallsCrossed
 *   loudness = max(path, wall) * listener.hearing
 * so sound bends around corners, is muffled by concrete, and is stopped by
 * closed doors. Recent noises are kept for the F1 debug circles.
 * Role: Agent Controller (shared by all four AI architectures).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const _eye = new THREE.Vector3(), _tgt = new THREE.Vector3();

  class Perception {
    constructor(agent, cfg) {
      this.agent = agent;
      this.fov = cfg.fov || 1.9; // radians, full cone
      this.range = cfg.range || 24;
      this.hearing = cfg.hearing || 1.0;
      this.awareness = 0;
      this.canSee = false;
      this.detected = false;
      this.lastSeenPos = null;
      this.lastSeenTime = -1e9;
      this.firstSeenTime = -1e9;
      this.noises = []; // {pos, loudness, type, time}
      this.lastNoise = null;
    }

    visibilityFactor() {
      const p = BI.player;
      let f = 1;
      if (p.crouched) f *= 0.55;
      if (p.sprinting) f *= 1.25;
      if (p.flashOn) f *= 1.6;
      if (p.zone === 'outside' && !p.flashOn) f *= 0.8;
      return f;
    }

    rangeMul() {
      return this.agent.heightened ? 1.4 : 1;
    }

    update(dt) {
      const ag = this.agent, p = BI.player;
      this.canSee = false;
      if (!p || !p.alive) {
        this.awareness = Math.max(0, this.awareness - dt);
        this.detected = false;
        return;
      }
      _eye.set(ag.pos.x, ag.pos.y + ag.eyeHeight, ag.pos.z);
      p.eyePosition(_tgt);
      const dx = _tgt.x - _eye.x, dz = _tgt.z - _eye.z;
      const dist = Math.hypot(dx, dz, _tgt.y - _eye.y);
      const range = this.range * this.rangeMul();
      let inCone = false;
      if (dist < range) {
        const fx = Math.sin(ag.headYaw()), fz = Math.cos(ag.headYaw());
        const cos = (dx * fx + dz * fz) / (Math.hypot(dx, dz) || 1);
        inCone = cos > Math.cos(this.fov / 2) || dist < 2.2;
      }
      if (inCone) {
        const P = BI.Physics;
        const seeHead = P.lineOfSight(_eye.x, _eye.y, _eye.z, _tgt.x, _tgt.y, _tgt.z);
        const seeChest = seeHead || P.lineOfSight(_eye.x, _eye.y, _eye.z, p.pos.x, p.pos.y + p.height * 0.55, p.pos.z);
        this.canSee = seeHead || seeChest;
      }
      if (this.canSee) {
        const near = 1.25 - dist / range;
        const rate = 2.2 * Math.max(0.15, near) * this.visibilityFactor() * (this.detected ? 3 : 1);
        this.awareness = Math.min(1.5, this.awareness + rate * dt);
        if (this.awareness >= 1) {
          if (!this.detected) this.firstSeenTime = BI.Engine.time;
          this.detected = true;
          this.lastSeenPos = (this.lastSeenPos || new THREE.Vector3()).copy(p.pos);
          this.lastSeenTime = BI.Engine.time;
          BI.Blackboard.reportSighting(ag, p.pos);
        }
      } else {
        this.awareness = Math.max(0, this.awareness - dt * (this.detected ? 0.25 : 0.35));
        if (this.detected && BI.Engine.time - this.lastSeenTime > 6) this.detected = false;
      }
      // forget old noises
      const now = BI.Engine.time;
      while (this.noises.length && now - this.noises[0].time > 8) this.noises.shift();
    }

    seenRecently(sec) {
      return BI.Engine.time - this.lastSeenTime < sec;
    }

    // Most important recent noise matching a type filter.
    recentNoise(types, maxAge) {
      const now = BI.Engine.time;
      let best = null;
      for (let i = this.noises.length - 1; i >= 0; i--) {
        const n = this.noises[i];
        if (now - n.time > (maxAge || 5)) break;
        if (types && types.indexOf(n.type) < 0) continue;
        if (!best || n.loudness > best.loudness) best = n;
      }
      return best;
    }

    forget() {
      this.awareness = 0;
      this.detected = false;
      this.canSee = false;
      this.noises.length = 0;
    }

    onNoise(n) {
      this.noises.push(n);
      this.lastNoise = n;
    }
  }

  // ---------------------------------------------------------------------------
  // Noise system
  // ---------------------------------------------------------------------------
  const Noise = {
    recent: [], // for the debug overlay: {pos, radius, type, time}

    init() {
      BI.Events.on('noise', (e) => this.emit(e));
    },

    emit(e) {
      const now = BI.Engine.time;
      this.recent.push({ pos: e.pos.clone(), radius: e.radius, type: e.type, time: now });
      if (this.recent.length > 30) this.recent.shift();
      if (!BI.Agents || !BI.Nav.built) return;
      // only flood if someone could possibly hear it
      const listeners = BI.Agents.list.filter((a) => a.alive && a.pos.distanceTo(e.pos) < e.radius && e.source !== a.name);
      if (!listeners.length) return;
      BI.AStar.floodSound(e.pos.x, e.pos.z, e.radius);
      for (const a of listeners) {
        const straight = a.pos.distanceTo(e.pos);
        const pathD = BI.AStar.soundDistAt(a.pos.x, a.pos.z);
        const viaPath = pathD < Infinity ? 1 - pathD / e.radius : 0;
        let viaWall = 0;
        if (viaPath <= 0.05) {
          const walls = BI.Physics.wallsBetween(e.pos.x, e.pos.y + 1.2, e.pos.z, a.pos.x, a.pos.y + 1.6, a.pos.z, 3);
          viaWall = (1 - straight / e.radius) * Math.pow(0.35, walls);
        }
        const loud = Math.max(viaPath, viaWall) * a.perception.hearing;
        if (loud > 0.04) a.perception.onNoise({ pos: e.pos.clone(), loudness: loud, type: e.type, time: now, source: e.source });
      }
    },
  };

  BI.Perception = Perception;
  BI.Noise = Noise;
})();
