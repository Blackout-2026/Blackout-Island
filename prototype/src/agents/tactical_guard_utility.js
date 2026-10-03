/* =============================================================================
 * agents/tactical_guard_utility.js — TACTICAL GUARD  ::  UTILITY AI
 * -----------------------------------------------------------------------------
 * Architecture: utility-based action selection. Every think tick (10 Hz) each
 * candidate action is SCORED from weighted, normalised considerations and the
 * highest score wins (with a small inertia bonus for the current action so it
 * does not flicker). Scores are logged to the F1 debug overlay.
 *
 * Considerations (all 0..1):
 *   dist       distance to the player (response curve, good inside 18 m)
 *   visible    player detected and in line of sight
 *   knows      player position known recently (own sighting or blackboard)
 *   threat     being shot at / seen / aimed at
 *   health     current HP fraction
 *   cover      quality of the best cover point within 14 m that breaks the
 *              player's line of sight (level cover points)
 *   post       distance from its guarded point (normalised by 12 m)
 *
 * Actions:  Attack | MoveToCover | Reposition | HoldPost
 * WEIGHTS below are data, not code: tune them without touching the logic.
 * Commander directives move the guarded point (converge / search location),
 * so the same scoring naturally pulls the robot toward the order.
 * Body: the bipedal security robot hero model (models/robot.js).
 * Role: Core Developer's IS agent (Student 3).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const WEIGHTS = {
    attack: { base: 0.35, range: 0.65, healthKeep: 0.4, underFire: 0.6 },
    cover: { threat: 1.0, lowHealth: 1.0, inCoverPenalty: 0.7 },
    reposition: { knows: 0.45, nearPost: 0.3, longRange: 0.4 },
    hold: { base: 0.25, farFromPost: 0.4, knowsPenalty: 0.15 },
    inertia: 0.1,
  };

  class TacticalGuard extends BI.Agent {
    constructor(spec, idx, parent) {
      const y = BI.Physics.groundAt(spec.x, spec.z, BI.Level.BUNKER_Y + 0.5).y;
      super({
        name: spec.name || 'TG-0' + (idx + 1), type: 'guard', arch: 'Utility', rig: BI.Models.buildRobot(), parent,
        x: spec.x, y, z: spec.z, hp: 100, eyeHeight: 1.95,
        weapon: { damage: 8, burst: 4, interval: 0.1, pause: 1.15, spread: 0.032, range: 34, reaction: 0.5, color: 0xff7040 },
        perception: { fov: 2.1, range: 22, hearing: 0.9 },
        move: { walk: 1.4, run: 3.5, turnRate: 4, radius: 0.45 },
      });
      this.post = new THREE.Vector3(spec.guard[0], y, spec.guard[1]);
      this.homePost = this.post.clone();
      this.action = 'HoldPost';
      this.scores = { Attack: 0, MoveToCover: 0, Reposition: 0, HoldPost: 0 };
      this.factors = {};
      this.coverPoint = null;
      this.repositionPoint = null;
      this.lastDirective = -1;
      this.scanT = Math.random() * 6;
    }

    playerChest() {
      const p = BI.player;
      return new THREE.Vector3(p.pos.x, p.pos.y + p.height * 0.6, p.pos.z);
    }

    // Best cover point: close, and the player cannot see it.
    evaluateCover() {
      const p = BI.player;
      const eye = p.eyePosition(new THREE.Vector3());
      let best = null, bestQ = 0;
      for (const [x, z] of BI.Level.coverPoints) {
        const d = Math.hypot(x - this.pos.x, z - this.pos.z);
        if (d > 14) continue;
        const y = BI.Level.BUNKER_Y;
        if (BI.Physics.lineOfSight(eye.x, eye.y, eye.z, x, y + 1.0, z)) continue; // exposed
        const q = 1 - d / 14;
        if (q > bestQ) { bestQ = q; best = new THREE.Vector3(x, y, z); }
      }
      return { q: bestQ, point: best };
    }

    think() {
      const per = this.perception, BB = BI.Blackboard.data, p = BI.player;
      // directives move the guarded point
      const dir = this.directive();
      if (dir && dir.issuedAt > this.lastDirective) {
        this.lastDirective = dir.issuedAt;
        this.post.copy(dir.pos);
        this.post.y = BI.Nav.heightAt(dir.pos.x, dir.pos.z);
        if (this.post.y < -50) this.post.y = this.pos.y;
      }
      // ---- considerations ------------------------------------------------------------
      const d = this.distToPlayer();
      const visible = per.detected && per.canSee ? 1 : 0;
      const lkp = per.seenRecently(10) ? per.lastSeenPos : BI.Blackboard.sightingAge() < 10 ? BB.playerLKP : null;
      const knows = lkp ? 1 : 0;
      const hurt = BI.Engine.time - this.hurtTime < 3 ? 1 : 0;
      let aimedAt = 0;
      if (visible && p) {
        const f = BI.Engine.camera.getWorldDirection(new THREE.Vector3());
        const to = this.pos.clone().setY(this.pos.y + 1.4).sub(p.eyePosition(new THREE.Vector3())).normalize();
        aimedAt = f.dot(to) > 0.97 ? 1 : 0;
      }
      const threat = U.clamp01(visible * 0.4 + hurt * 0.5 + aimedAt * 0.25);
      const health = this.hp / this.maxHp;
      const cov = visible || hurt ? this.evaluateCover() : { q: 0, point: null };
      const postD = U.clamp01(this.pos.distanceTo(this.post) / 12);
      const inCover = this.coverPoint && this.pos.distanceTo(this.coverPoint) < 1.0 ? 1 : 0;
      const rangeCurve = 1 - U.smoothstep(18, 36, d);
      this.factors = { dist: +d.toFixed(1), visible, knows, threat: +threat.toFixed(2), health: +health.toFixed(2), cover: +cov.q.toFixed(2), post: +postD.toFixed(2) };

      // ---- scores ------------------------------------------------------------------------
      const W = WEIGHTS;
      const s = {
        Attack: visible ? (W.attack.base + W.attack.range * rangeCurve) * (1 - W.attack.healthKeep + W.attack.healthKeep * health) * (1 - W.attack.underFire * threat * (1 - health)) : 0,
        MoveToCover: Math.sqrt(cov.q) * U.clamp01(0.2 + threat * W.cover.threat) * (1.6 - health * W.cover.lowHealth) * (inCover ? 1 - W.cover.inCoverPenalty : 1),
        Reposition: knows && !visible ? W.reposition.knows + W.reposition.nearPost * (1 - postD) : visible && d > 30 ? W.reposition.longRange : 0,
        HoldPost: W.hold.base + W.hold.farFromPost * postD - (knows ? W.hold.knowsPenalty : 0),
      };
      s[this.action] += W.inertia;
      let best = 'HoldPost';
      for (const k of Object.keys(s)) if (s[k] > s[best]) best = k;
      this.scores = s;
      if (best !== this.action) {
        this.action = best;
        if (best === 'MoveToCover') this.coverPoint = cov.point;
        if (best === 'Reposition') this.repositionPoint = null;
      }
      this.state = this.action;
      this.execute(lkp, cov);
      // eye colour: red in combat, amber otherwise
      this.rig.eyeColor.setRGB(this.inCombat() || BB.alert ? 6 : 5, this.inCombat() || BB.alert ? 0.4 : 2.4, 0.2);
    }

    execute(lkp, cov) {
      const it = this.intent, per = this.perception;
      if (this.action === 'Attack') {
        Object.assign(it, { move: null, speed: 'walk', aim: true, fire: true, look: this.playerChest(), crouch: false });
      } else if (this.action === 'MoveToCover') {
        if (!this.coverPoint && cov.point) this.coverPoint = cov.point;
        const atCover = this.coverPoint && this.pos.distanceTo(this.coverPoint) < 1.0;
        Object.assign(it, { move: atCover ? null : this.coverPoint, speed: 'run', aim: true, fire: per.canSee, look: per.canSee ? this.playerChest() : null, crouch: atCover });
      } else if (this.action === 'Reposition') {
        // a point near the last known position, but within reach of the post
        if (!this.repositionPoint || this.pos.distanceTo(this.repositionPoint) < 1.2 || this.controller.failed > 1) {
          const c = lkp || this.post;
          const toward = c.clone().sub(this.post);
          const lim = BI.Blackboard.data.alert ? 30 : 15;
          if (toward.length() > lim) toward.setLength(lim);
          const base = this.post.clone().add(toward);
          this.repositionPoint = BI.Nav.randomPointNear(base.x, base.z, 3) || base;
          this.controller.failed = 0;
        }
        Object.assign(it, { move: this.repositionPoint, speed: 'run', aim: true, fire: false, look: null, crouch: false });
      } else {
        const atPost = this.pos.distanceTo(this.post) < 1.2;
        this.scanT += 0.1;
        Object.assign(it, {
          move: atPost ? null : this.post, speed: 'walk', aim: false, fire: false, crouch: false,
          look: atPost ? this.pos.clone().add(new THREE.Vector3(Math.sin(this.scanT * 0.5) * 6, 1.6, Math.cos(this.scanT * 0.5) * 6)) : null,
        });
      }
    }

    debugLines() {
      const s = this.scores;
      return [
        this.name + ' [Utility] ' + this.action,
        '  A ' + s.Attack.toFixed(2) + '  C ' + s.MoveToCover.toFixed(2) + '  R ' + s.Reposition.toFixed(2) + '  H ' + s.HoldPost.toFixed(2),
      ];
    }
  }

  TacticalGuard.WEIGHTS = WEIGHTS;
  BI.TacticalGuard = TacticalGuard;
})();
