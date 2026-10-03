/* =============================================================================
 * player/player.js — first-person controller
 * -----------------------------------------------------------------------------
 * WASD + mouse look (pointer lock), Space jump, Shift sprint, C crouch toggle,
 * F flashlight (shadow-casting spotlight). 100 HP, no regeneration.
 *
 * Movement is integrated in small sub-steps. Each sub-step:
 *   1. horizontal move, then terrain limits (max slope, deep water, cliff)
 *   2. capsule vs world resolution (BI.Physics.resolveCylinder) -> sliding
 *      (velocity into the wall normal is removed); pushable objects that were
 *      touched receive an impulse (the barricade crate)
 *   3. vertical move + ground snap (steps up to 0.45 m, snap down on stairs
 *      and ramps), ceiling clamp
 * Footsteps are distance driven, sound different per surface and publish
 * `noise` events (sprinting is loud, crouching silent) for the Hunter AI.
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const L = BI.Level;

  const WALK = 4.3, SPRINT = 7.0, CROUCH = 2.1;
  const GRAVITY = 17, JUMP_V = 5.6;
  const STAND_H = 1.75, CROUCH_H = 1.15;

  class Player {
    constructor() {
      this.pos = new THREE.Vector3(0, 2, 120);
      this.vel = new THREE.Vector3();
      this.yaw = 0;
      this.pitch = 0;
      this.radius = 0.35;
      this.height = STAND_H;
      this.eyeY = STAND_H - 0.12;
      this.crouched = false;
      this.sprinting = false;
      this.onGround = false;
      this.wasOnGround = false;
      this.surface = 'sand';
      this.health = 100;
      this.maxHealth = 100;
      this.alive = true;
      this.invuln = 0;
      this.deathTimer = 0;
      this.hasCure = false;
      this.stepDist = 0;
      this.stepIndex = 0;
      this.bob = 0;
      this.bobAmp = 0;
      this.landOffset = 0;
      this.landVel = 0;
      this.shake = 0;
      this.kick = { pitch: 0, yaw: 0 }; // recoil offsets (weapon system)
      this.sensitivity = 1.0;
      this.zoomSensMul = 1.0;
      this.speedMul = 1.0;
      this.fallStartY = 0;
      this.flashOn = false;
      this.lastNoiseTime = 0;
      this.lastHurtTime = -10;
      this.zone = 'outside';
      this.interior = 0;
      this.frozen = false;
      this.camera = BI.Engine.camera;
      this.buildFlashlight();
    }

    buildFlashlight() {
      const cam = this.camera;
      const light = new THREE.SpotLight(0xfff1dc, 0, 34, 0.5, 0.5, 1.4);
      light.position.set(0.22, -0.2, 0.1);
      light.target.position.set(0.05, -0.1, -6);
      cam.add(light);
      cam.add(light.target);
      light.shadow.camera.near = 0.3;
      light.shadow.camera.far = 30;
      light.shadow.bias = -0.0008;
      light.shadow.normalBias = 0.02;
      this.flashlight = light;
      this.applyQuality(BI.Engine.settings);
      BI.Events.on('quality:changed', (e) => this.applyQuality(e.settings));
    }

    applyQuality(s) {
      const l = this.flashlight;
      l.castShadow = !!s.flashShadow;
      l.shadow.mapSize.set(s.flashShadowSize, s.flashShadowSize);
      if (l.shadow.map) {
        l.shadow.map.dispose();
        l.shadow.map = null;
      }
    }

    toggleFlashlight() {
      this.flashOn = !this.flashOn;
      BI.Events.emit('player:flashlight', { on: this.flashOn });
    }

    forward(out) {
      return out.set(-Math.sin(this.yaw), 0, -Math.cos(this.yaw));
    }

    eyePosition(out) {
      return out.set(this.pos.x, this.pos.y + this.eyeY, this.pos.z);
    }

    // -------------------------------------------------------------------------
    teleport(x, yHint, z, yaw) {
      const g = BI.Physics.groundAt(x, z, yHint, 0.5);
      this.pos.set(x, g.y > -Infinity ? g.y : yHint, z);
      this.vel.set(0, 0, 0);
      if (yaw !== undefined) this.yaw = yaw;
      this.pitch = 0;
      this.onGround = true;
      this.fallStartY = this.pos.y;
    }

    respawn(cp) {
      this.health = this.maxHealth;
      this.alive = true;
      this.invuln = 1.5;
      this.deathTimer = 0;
      this.crouched = false;
      this.height = STAND_H;
      this.teleport(cp.x, cp.y, cp.z, cp.yaw);
      BI.Events.emit('player:respawned', { pos: this.pos.clone() });
    }

    takeDamage(amount, fromPos) {
      if (!this.alive || this.invuln > 0 || this.godMode) return;
      this.health = Math.max(0, this.health - amount);
      this.lastHurtTime = BI.Engine.time;
      this.shake = Math.min(1, this.shake + amount * 0.03);
      BI.Events.emit('player:damaged', { amount, from: fromPos ? fromPos.clone() : null, health: this.health });
      if (this.health <= 0) this.die();
    }

    heal(amount) {
      const before = this.health;
      this.health = Math.min(this.maxHealth, this.health + amount);
      return this.health - before;
    }

    die() {
      if (!this.alive) return;
      this.alive = false;
      this.deathTimer = 0;
      this.flashOn = false;
      BI.Events.emit('player:died', { pos: this.pos.clone() });
    }

    // -------------------------------------------------------------------------
    // Terrain limits: steep slopes, deep water, the northern cliff.
    terrainBlocked(x, z, fromX, fromZ) {
      const T = BI.Terrain;
      if (T.inHole(x, z) || T.inHole(fromX, fromZ)) return false;
      const tNew = T.height(x, z);
      const tOld = T.height(fromX, fromZ);
      // underground (below the terrain surface): terrain does not apply
      if (this.pos.y < tOld - 1.5) return false;
      // standing on a structure well above the terrain (dock, floors)
      if (this.pos.y > tNew + 0.6 && !this.onTerrain) {
        return false;
      }
      const horiz = Math.hypot(x - fromX, z - fromZ) || 1e-4;
      const rise = tNew - Math.max(this.pos.y, tOld);
      if (rise > 0.05 && rise / horiz > 1.15) return true; // ~49 deg
      if (tNew < -1.15 && tNew < tOld - 0.001) return true; // too deep to wade (can always head back)
      if (z < T.cliffZ(x) + 0.6 && this.zone === 'outside') return true;
      if (Math.abs(x) > 160 || z > 150 || z < -200) return true;
      return false;
    }

    // -------------------------------------------------------------------------
    update(dt) {
      const In = BI.Input;
      const cam = this.camera;
      this.zone = L.zoneOf(this.pos.x, this.pos.y, this.pos.z);
      this.interior = L.interiorFactor(this.pos.x, this.pos.y, this.pos.z);
      if (this.invuln > 0) this.invuln = Math.max(0, this.invuln - dt);

      if (!this.alive) {
        this.deathTimer += dt;
        // fall over
        const t = U.clamp01(this.deathTimer / 0.8);
        this.eyeY = U.lerp(this.eyeY, 0.25, t * 0.2);
        cam.position.set(this.pos.x, this.pos.y + this.eyeY, this.pos.z);
        cam.rotation.set(this.pitch * (1 - t) - 0.2 * t, this.yaw, t * 1.1);
        this.flashlight.intensity = 0;
        return;
      }

      // ---- look ----------------------------------------------------------------
      if (!this.frozen) {
        const sens = 0.0021 * this.sensitivity * this.zoomSensMul;
        this.yaw -= In.mouseDX * sens;
        this.pitch -= In.mouseDY * sens;
        this.pitch = U.clamp(this.pitch, -1.52, 1.52);
      }

      // ---- toggles --------------------------------------------------------------
      if (In.pressed('KeyF')) this.toggleFlashlight();
      if (In.pressed('KeyC') || In.pressed('ControlLeft')) {
        if (this.crouched) {
          const ceil = BI.Physics.ceilingAt(this.pos.x, this.pos.z, this.pos.y + CROUCH_H);
          if (ceil > this.pos.y + STAND_H + 0.05) this.crouched = false;
        } else this.crouched = true;
      }

      // ---- wish direction ---------------------------------------------------------
      let f = 0, r = 0;
      if (!this.frozen) {
        if (In.down('KeyW')) f += 1;
        if (In.down('KeyS')) f -= 1;
        if (In.down('KeyD')) r += 1;
        if (In.down('KeyA')) r -= 1;
      }
      const sy = Math.sin(this.yaw), cy = Math.cos(this.yaw);
      let wx = -sy * f + cy * r, wz = -cy * f - sy * r;
      const wl = Math.hypot(wx, wz);
      if (wl > 0) { wx /= wl; wz /= wl; }

      this.sprinting = In.down('ShiftLeft') && f > 0 && !this.crouched && this.onGround;
      if (this.sprinting && this.crouched) this.crouched = false;
      let speed = this.crouched ? CROUCH : this.sprinting ? SPRINT : WALK;
      speed *= this.speedMul;

      // ---- horizontal velocity (acceleration / friction) --------------------------
      const accel = this.onGround ? (wl > 0 ? 38 : 30) : 7;
      const tx = wx * speed, tz = wz * speed;
      const dvx = tx - this.vel.x, dvz = tz - this.vel.z;
      const dvl = Math.hypot(dvx, dvz);
      const maxDv = accel * dt;
      if (dvl <= maxDv) { this.vel.x = tx; this.vel.z = tz; }
      else { this.vel.x += (dvx / dvl) * maxDv; this.vel.z += (dvz / dvl) * maxDv; }

      // ---- jump ---------------------------------------------------------------------
      if (In.pressed('Space') && this.onGround && !this.frozen) {
        if (this.crouched) {
          this.crouched = false;
        } else {
          this.vel.y = JUMP_V;
          this.onGround = false;
          BI.Events.emit('noise', { pos: this.pos.clone(), radius: 7, type: 'jump', source: 'player' });
          BI.Events.emit('player:jump', {});
        }
      }
      this.vel.y -= GRAVITY * dt;

      // ---- crouch height ----------------------------------------------------------------
      const targetH = this.crouched ? CROUCH_H : STAND_H;
      this.height = U.damp(this.height, targetH, 14, dt);

      // ---- integrate in sub-steps -------------------------------------------------
      this.wasOnGround = this.onGround;
      const travel = Math.hypot(this.vel.x, this.vel.y, this.vel.z) * dt;
      const steps = Math.min(6, Math.max(1, Math.ceil(travel / 0.2)));
      const sdt = dt / steps;
      const startX = this.pos.x, startZ = this.pos.z;
      let landedSpeed = 0;
      for (let s = 0; s < steps; s++) {
        const ox = this.pos.x, oz = this.pos.z;
        let nx = ox + this.vel.x * sdt, nz = oz + this.vel.z * sdt;
        // terrain limits with axis-separated sliding
        if (this.terrainBlocked(nx, nz, ox, oz)) {
          if (!this.terrainBlocked(nx, oz, ox, oz)) { nz = oz; this.vel.z *= 0.5; }
          else if (!this.terrainBlocked(ox, nz, ox, oz)) { nx = ox; this.vel.x *= 0.5; }
          else { nx = ox; nz = oz; this.vel.x = 0; this.vel.z = 0; }
        }
        this.pos.x = nx;
        this.pos.z = nz;
        const res = BI.Physics.resolveCylinder(this.pos, this.radius, this.pos.y, this.height);
        if (res.hit) {
          const vn = this.vel.x * res.nx + this.vel.z * res.nz;
          if (vn < 0) {
            this.vel.x -= vn * res.nx;
            this.vel.z -= vn * res.nz;
          }
          for (const c of res.colliders) {
            if (c.owner && c.owner.onPush) c.owner.onPush(-res.nx, -res.nz, Math.max(0.5, Math.hypot(tx, tz)), this);
          }
        }

        // vertical
        const prevY = this.pos.y;
        this.pos.y += this.vel.y * sdt;
        const g = BI.Physics.groundAt(this.pos.x, this.pos.z, Math.max(prevY, this.pos.y));
        const snap = this.wasOnGround && this.vel.y <= 0 ? 0.35 : 0.0;
        if (this.vel.y <= 0 && this.pos.y <= g.y + snap) {
          if (!this.onGround && this.vel.y < -1) landedSpeed = -this.vel.y;
          this.pos.y = g.y;
          this.vel.y = 0;
          this.onGround = true;
          this.onTerrain = g.terrain;
          this.surface = g.surface === 'terrain' ? 'grass' : g.surface;
        } else {
          this.onGround = false;
        }
        // ceiling
        if (this.vel.y > 0) {
          const ceil = BI.Physics.ceilingAt(this.pos.x, this.pos.z, prevY + this.height - 0.05);
          if (this.pos.y + this.height > ceil) {
            this.pos.y = ceil - this.height;
            this.vel.y = 0;
          }
        }
      }
      if (this.pos.y < -20) this.die();

      // ---- landing ----------------------------------------------------------------
      if (!this.wasOnGround && this.onGround && landedSpeed > 2.5) {
        this.landVel -= Math.min(2.8, landedSpeed * 0.32);
        BI.Events.emit('player:land', { speed: landedSpeed, surface: this.surface });
        if (landedSpeed > 6) BI.Events.emit('noise', { pos: this.pos.clone(), radius: 9, type: 'land', source: 'player' });
        if (landedSpeed > 13) this.takeDamage((landedSpeed - 13) * 8, null);
      }
      // landing spring
      this.landVel += (-this.landOffset * 120 - this.landVel * 14) * dt;
      this.landOffset += this.landVel * dt;

      // ---- footsteps --------------------------------------------------------------
      const moved = Math.hypot(this.pos.x - startX, this.pos.z - startZ);
      const hSpeed = moved / Math.max(dt, 1e-4);
      if (this.onGround && hSpeed > 0.6) {
        const stride = this.sprinting ? 1.05 : this.crouched ? 0.62 : 0.78;
        this.stepDist += moved;
        if (this.stepDist >= stride) {
          this.stepDist -= stride;
          this.stepIndex++;
          const loud = this.sprinting ? 13 : this.crouched ? 0 : 4.5;
          BI.Events.emit('player:footstep', { surface: this.surface, sprint: this.sprinting, crouch: this.crouched, index: this.stepIndex });
          if (loud > 0) BI.Events.emit('noise', { pos: this.pos.clone(), radius: loud, type: this.sprinting ? 'sprint' : 'footstep', source: 'player' });
        }
        this.bob += (moved / stride) * Math.PI;
      }
      const targetAmp = this.onGround ? U.clamp(hSpeed / WALK, 0, 1.6) * (this.crouched ? 0.6 : 1) : 0;
      this.bobAmp = U.damp(this.bobAmp, targetAmp, 8, dt);

      // ---- camera -----------------------------------------------------------------
      const targetEye = this.height - 0.12;
      // smooth step-ups so stairs do not jerk the view
      this.eyeY = U.damp(this.eyeY, targetEye, 16, dt);
      this.smoothY = this.smoothY === undefined ? this.pos.y : U.damp(this.smoothY, this.pos.y, 18, dt);
      if (Math.abs(this.smoothY - this.pos.y) > 1.0) this.smoothY = this.pos.y;
      const bobY = Math.abs(Math.sin(this.bob)) * 0.05 * this.bobAmp - 0.025 * this.bobAmp;
      const bobX = Math.cos(this.bob) * 0.025 * this.bobAmp;
      this.shake = Math.max(0, this.shake - dt * 2.5);
      const sh = this.shake * this.shake * 0.04;
      const t = BI.Engine.time;
      cam.position.set(
        this.pos.x + cy * bobX,
        this.smoothY + this.eyeY + bobY + this.landOffset * 0.12,
        this.pos.z - sy * bobX
      );
      const strafeRoll = -U.clamp((this.vel.x * cy - this.vel.z * sy) / SPRINT, -1, 1) * 0.012;
      this.roll = U.damp(this.roll || 0, strafeRoll, 6, dt);
      cam.rotation.set(
        this.pitch + this.kick.pitch + Math.sin(t * 47) * sh + this.landOffset * 0.02,
        this.yaw + this.kick.yaw + Math.cos(t * 39) * sh,
        this.roll
      );

      // ---- flashlight ---------------------------------------------------------------
      this.flashlight.intensity = U.damp(this.flashlight.intensity, this.flashOn ? 2.6 : 0, 30, dt);
      this.flashlight.shadow.autoUpdate = this.flashOn;

      BI.Engine.fx.invuln = this.invuln > 0 ? Math.min(1, this.invuln) : 0;
    }
  }

  BI.Player = Player;
})();
