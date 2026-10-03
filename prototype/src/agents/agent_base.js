/* =============================================================================
 * agents/agent_base.js — what every agent shares (body, health, shooting)
 * -----------------------------------------------------------------------------
 * An Agent = model (rig) + Perception + AgentController + a decision layer.
 * Subclasses implement only think() (the architecture under study) and set
 * this.intent. Update order each frame:
 *   perception (10 Hz) -> think (10 Hz, staggered) -> controller -> combat
 *
 * Fair shooting (shared hitscan in BI.Combat):
 *   - reaction delay after first detecting the player before the first shot
 *   - aim must settle (aim blend) before firing
 *   - angular error grows with distance, player movement and the first second
 *     of an engagement; shots come in bursts with pauses
 * Role: Agent Controller.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  class Agent {
    constructor(o) {
      this.name = o.name;
      this.type = o.type; // patrol | hunter | guard | commander
      this.arch = o.arch; // FSM | BT | Utility | GOAP
      this.rig = o.rig;
      this.pos = new THREE.Vector3(o.x, o.y, o.z);
      this.yaw = o.yaw || 0;
      this.maxHp = o.hp;
      this.hp = o.hp;
      this.alive = true;
      this.eyeHeight = o.eyeHeight || 1.62;
      this.weapon = Object.assign({ damage: 7, burst: 3, interval: 0.11, pause: 0.9, spread: 0.035, range: 30, reaction: 0.55, tracer: true, noise: 40, color: 0xffb060 }, o.weapon || {});
      this.intent = { move: null, speed: 'walk', look: null, aim: false, fire: false, crouch: false };
      this.perception = new BI.Perception(this, o.perception || {});
      this.controller = new BI.AgentController(this, o.move || {});
      this.state = 'Idle'; // label for the debug overlay
      this.heightened = false;
      this.thinkT = Math.random() * 0.1;
      this.burstLeft = 0;
      this.shotT = 0;
      this.engageStart = -1;
      this.hurtTime = -10;
      this.flashT = 0;
      this.deathTime = -1;
      this.kills = 0;
      this.home = this.pos.clone();
      o.parent.add(this.rig.root);
      this.rig.root.position.copy(this.pos);
      this.rig.root.rotation.y = this.yaw;
      this.baseEmissive = this.rig.material.emissive.clone();
    }

    forward() {
      return new THREE.Vector3(Math.sin(this.yaw), 0, Math.cos(this.yaw));
    }
    headYaw() {
      return this.yaw + this.controller.lookYaw;
    }
    inCombat() {
      return this.perception.detected || this.perception.seenRecently(3) || BI.Engine.time - this.hurtTime < 4;
    }
    speedMul() {
      return this.heightened ? 1.25 : 1;
    }
    distToPlayer() {
      return BI.player ? this.pos.distanceTo(BI.player.pos) : 1e9;
    }

    hitbox() {
      if (!this._hb) this._hb = { a: new THREE.Vector3(), b: new THREE.Vector3(), head: new THREE.Vector3(), r: 0.3, headR: 0.14 };
      const hb = this._hb, p = this.pos;
      const big = this.type === 'guard';
      const crouch = this.controller.crouchBlend * 0.4;
      hb.r = big ? 0.4 : 0.3;
      hb.headR = big ? 0.17 : 0.14;
      hb.a.set(p.x, p.y + 0.3, p.z);
      hb.b.set(p.x, p.y + (big ? 1.65 : 1.35) - crouch, p.z);
      hb.head.set(p.x + Math.sin(this.yaw) * 0.03, p.y + (big ? 1.98 : 1.63) - crouch, p.z + Math.cos(this.yaw) * 0.03);
      return hb;
    }

    // Override in subclasses.
    think() {}

    update(dt) {
      if (!this.alive) {
        this.controller.update(dt);
        this.updateDeath(dt);
        return;
      }
      this.thinkT -= dt;
      if (this.thinkT <= 0) {
        const tickDt = 0.1 - this.thinkT;
        this.thinkT = 0.1;
        this.perception.update(tickDt);
        this.think(tickDt);
      }
      this.controller.update(dt);
      this.combat(dt);
      // hit flash
      if (this.flashT > 0) {
        this.flashT -= dt;
        this.rig.material.emissive.setRGB(0.9, 0.05, 0.02).multiplyScalar(Math.max(0, this.flashT / 0.15));
        if (this.flashT <= 0) this.rig.material.emissive.copy(this.baseEmissive);
      }
    }

    // ---------------------------------------------------------------------------
    combat(dt) {
      const it = this.intent, per = this.perception, w = this.weapon;
      const p = BI.player;
      this.shotT -= dt;
      if (!it.fire || !p || !p.alive || !per.canSee) {
        if (!per.canSee) this.engageStart = -1;
        return;
      }
      if (this.engageStart < 0) this.engageStart = BI.Engine.time;
      const engaged = BI.Engine.time - this.engageStart;
      if (engaged < w.reaction || this.controller.aimBlend < 0.8) return;
      if (this.distToPlayer() > w.range) return;
      // facing roughly toward the player?
      const to = p.pos.clone().sub(this.pos);
      if (this.forward().dot(to.setY(0).normalize()) < 0.85) return;
      if (this.shotT > 0) return;
      if (this.burstLeft <= 0) this.burstLeft = w.burst;
      this.burstLeft--;
      this.shotT = this.burstLeft > 0 ? w.interval : w.pause * (0.8 + Math.random() * 0.4);
      this.shoot(engaged);
    }

    shoot(engaged) {
      const p = BI.player, w = this.weapon;
      this.rig.muzzle.updateMatrixWorld(true);
      const muzzle = this.rig.muzzle.getWorldPosition(new THREE.Vector3());
      const target = new THREE.Vector3(p.pos.x, p.pos.y + p.height * 0.62, p.pos.z);
      const dist = muzzle.distanceTo(target);
      const pv = Math.hypot(p.vel.x, p.vel.z);
      let err = w.spread * (1 + dist / 28) * (1 + pv / 4.5 * 0.9) * (engaged < 1.2 ? 1.6 : 1) * (p.crouched ? 1.15 : 1);
      if (this.heightened) err *= 0.9;
      const dir = target.clone().sub(muzzle).normalize();
      const a = Math.random() * Math.PI * 2, r = err * Math.sqrt(-2 * Math.log(Math.max(1e-4, Math.random()))) * 0.7;
      const right = new THREE.Vector3(dir.z, 0, -dir.x).normalize();
      const up = new THREE.Vector3().crossVectors(right, dir).normalize();
      dir.addScaledVector(right, Math.cos(a) * r).addScaledVector(up, Math.sin(a) * r).normalize();
      // start the ray a little in front of the muzzle so it never hits the shooter's own wall corner
      const ro = muzzle.clone().addScaledVector(dir, 0.15);
      const hit = BI.Combat.hitscan(ro, dir, w.range + 10, this);
      const end = hit ? hit.point : ro.clone().addScaledVector(dir, w.range);
      if (w.tracer) BI.FX.tracer(muzzle, end);
      BI.FX.flashBurst(muzzle, [5, 3.5, 1.8], this.type === 'guard' ? 0.5 : 0.35);
      BI.Combat.flashAt(muzzle, w.color, 0.7);
      if (hit && hit.kind === 'player') {
        p.takeDamage(w.damage, this.pos);
        if (!p.alive) this.kills++;
      } else if (hit) {
        BI.Combat.impact(hit, dir);
      } else if (p.eyePosition(new THREE.Vector3()).distanceTo(end) < 6 || ro.distanceTo(p.pos) > 3) {
        BI.Events.emit('bullet:whiz', { pos: end.clone() });
      }
      BI.Events.emit('enemy:fired', { agent: this, pos: muzzle.clone(), type: this.type });
      BI.Events.emit('noise', { pos: this.pos.clone(), radius: w.noise, type: 'gunfire', source: this.name });
    }

    // ---------------------------------------------------------------------------
    takeDamage(amount, fromPos, head, weapon) {
      if (!this.alive) return false;
      this.hp -= amount;
      this.hurtTime = BI.Engine.time;
      this.flashT = 0.15;
      this.rig.hit = 1;
      if (fromPos) {
        const to = fromPos.clone().sub(this.pos);
        this.rig.hitDirX = Math.sign(to.x * Math.cos(this.yaw) - to.z * Math.sin(this.yaw)) || 1;
        // being shot reveals the attacker's position
        const per = this.perception;
        per.awareness = Math.max(per.awareness, 1.0);
        per.detected = true;
        per.lastSeenPos = (per.lastSeenPos || new THREE.Vector3()).copy(fromPos);
        per.lastSeenTime = BI.Engine.time - 0.5;
        BI.Blackboard.reportSighting(this, fromPos);
      }
      BI.Events.emit('enemy:damaged', { agent: this, amount, head, weapon });
      if (this.hp <= 0) {
        this.die();
        return true;
      }
      return false;
    }

    die() {
      this.alive = false;
      this.hp = 0;
      this.deathTime = BI.Engine.time;
      this.rig.death = 0;
      this.intent.move = null;
      this.intent.fire = false;
      this.state = 'Dead';
      BI.Blackboard.data.casualties++;
      BI.Blackboard.data.casualtiesSinceRegroup++;
      BI.Blackboard.clearDirective(this.name);
      BI.Events.emit('enemy:killed', { agent: this, pos: this.pos.clone() });
      if (this.type === 'guard') BI.FX.sparks(this.pos.clone().setY(this.pos.y + 1.4), { x: 0, y: 1, z: 0 }, 30, { speed: 5, floor: this.pos.y });
    }

    // collapse, then fade out and remove from the scene
    updateDeath(dt) {
      const t = BI.Engine.time - this.deathTime;
      const m = this.rig.material;
      if (t > 3.2 && !m.transparent) {
        m.transparent = true;
        m.needsUpdate = true;
        if (this.rig.glow) { this.rig.glow.transparent = true; this.rig.glow.needsUpdate = true; }
      }
      if (t > 3.2) {
        const a = Math.max(0, 1 - (t - 3.2) / 1.6);
        m.opacity = a;
        if (this.rig.glow) this.rig.glow.opacity = a;
        if (a <= 0) this.rig.root.visible = false;
      }
    }

    // Commander-issued directive (read by every decision layer).
    directive() {
      return BI.Blackboard.directiveFor(this.name);
    }
  }

  BI.Agent = Agent;
})();
