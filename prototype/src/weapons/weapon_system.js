/* =============================================================================
 * weapons/weapon_system.js — data-driven weapons + shared hitscan combat
 * -----------------------------------------------------------------------------
 * WEAPONS is the single data table (mirrors a Unity ScriptableObject per
 * weapon). One WeaponSystem runs all three; behaviour differs only by data:
 *   type 'hitscan' | 'melee', damage, fire rate, auto, spread, damage falloff,
 *   head multiplier, recoil, noise radius, tracer cadence, zoom, crosshair.
 *
 * BI.Combat is shared by the player AND the AI (agents call it to shoot):
 *   hitscan(origin, dir, range, shooter) -> nearest of world / agents / player
 *   Capsule + head-sphere hitboxes; impacts spawn sparks/dust/decals by
 *   surface; every shot publishes `noise` and `weapon:fired`.
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const WEAPONS = [
    {
      id: 'smg', name: 'MP-9 SMG', slot: 1, type: 'hitscan', damage: 15, rpm: 720, auto: true,
      spread: 0.024, moveSpread: 0.02, bloomPerShot: 0.004, bloomMax: 0.022,
      falloffStart: 10, falloffEnd: 32, falloffMin: 0.45, headMul: 1.6, range: 90,
      recoil: { pitch: 0.0085, yaw: 0.005, kick: 0.035, rot: 0.04 }, noise: 42, tracerEvery: 2,
      crosshair: 'cross', sound: 'smg', casing: true, flash: 0.75,
    },
    {
      id: 'sniper', name: 'R-700 RIFLE', slot: 2, type: 'hitscan', damage: 60, rpm: 52, auto: false,
      spread: 0, moveSpread: 0, bloomPerShot: 0, bloomMax: 0,
      falloffStart: 999, falloffEnd: 1000, falloffMin: 1, headMul: 2.5, range: 320,
      recoil: { pitch: 0.055, yaw: 0.01, kick: 0.11, rot: 0.16 }, noise: 65, tracerEvery: 1,
      crosshair: 'dot', sound: 'sniper', casing: true, flash: 1.2, zoomFov: 22, zoomSens: 0.35, bolt: 0.95,
    },
    {
      id: 'knife', name: 'COMBAT KNIFE', slot: 3, type: 'melee', damage: 40, rpm: 125, auto: true,
      range: 2.3, coneCos: 0.62, backstabMul: 2.5, noise: 3,
      recoil: { pitch: 0.0, yaw: 0.0, kick: 0, rot: 0 }, crosshair: 'ring', sound: 'knife', swing: 0.34,
    },
  ];

  // ---------------------------------------------------------------------------
  // Shared combat helpers
  // ---------------------------------------------------------------------------
  const _v1 = new THREE.Vector3(), _v2 = new THREE.Vector3();
  function raySphere(ro, rd, c, r) {
    const ox = ro.x - c.x, oy = ro.y - c.y, oz = ro.z - c.z;
    const b = ox * rd.x + oy * rd.y + oz * rd.z;
    const cc = ox * ox + oy * oy + oz * oz - r * r;
    const h = b * b - cc;
    if (h < 0) return -1;
    const t = -b - Math.sqrt(h);
    return t >= 0 ? t : -1;
  }
  // Ray vs capsule (segment a-b, radius r), after Inigo Quilez.
  function rayCapsule(ro, rd, a, b, r) {
    const bax = b.x - a.x, bay = b.y - a.y, baz = b.z - a.z;
    const oax = ro.x - a.x, oay = ro.y - a.y, oaz = ro.z - a.z;
    const baba = bax * bax + bay * bay + baz * baz;
    const bard = bax * rd.x + bay * rd.y + baz * rd.z;
    const baoa = bax * oax + bay * oay + baz * oaz;
    const rdoa = rd.x * oax + rd.y * oay + rd.z * oaz;
    const oaoa = oax * oax + oay * oay + oaz * oaz;
    const A = baba - bard * bard;
    const B = baba * rdoa - baoa * bard;
    const Cc = baba * oaoa - baoa * baoa - r * r * baba;
    const h = B * B - A * Cc;
    if (h >= 0) {
      const t = (-B - Math.sqrt(h)) / A;
      const y = baoa + t * bard;
      if (y > 0 && y < baba && t >= 0) return t;
      const ocx = y <= 0 ? oax : ro.x - b.x, ocy = y <= 0 ? oay : ro.y - b.y, ocz = y <= 0 ? oaz : ro.z - b.z;
      const B2 = rd.x * ocx + rd.y * ocy + rd.z * ocz;
      const C2 = ocx * ocx + ocy * ocy + ocz * ocz - r * r;
      const h2 = B2 * B2 - C2;
      if (h2 > 0) {
        const t2 = -B2 - Math.sqrt(h2);
        if (t2 >= 0) return t2;
      }
    }
    return -1;
  }

  const Combat = {
    raySphere,
    rayCapsule,
    // Closest living agent hit by the ray. Returns {agent, t, head}.
    raycastAgents(ro, rd, maxT, ignore) {
      let best = null;
      const agents = BI.Agents ? BI.Agents.list : [];
      for (const ag of agents) {
        if (!ag.alive || ag === ignore) continue;
        const hb = ag.hitbox();
        // quick reject
        _v1.copy(hb.a).add(hb.b).multiplyScalar(0.5);
        const tc = (_v1.x - ro.x) * rd.x + (_v1.y - ro.y) * rd.y + (_v1.z - ro.z) * rd.z;
        if (tc < -1 || tc > maxT + 1) continue;
        const th = raySphere(ro, rd, hb.head, hb.headR);
        const tb = rayCapsule(ro, rd, hb.a, hb.b, hb.r);
        let t = -1, head = false;
        if (th >= 0 && (tb < 0 || th <= tb + 0.05)) { t = th; head = true; }
        else if (tb >= 0) t = tb;
        if (t >= 0 && t < maxT && (!best || t < best.t)) best = { agent: ag, t, head };
      }
      return best;
    },
    playerHitbox() {
      const p = BI.player;
      return { a: _v1.set(p.pos.x, p.pos.y + 0.25, p.pos.z), b: _v2.set(p.pos.x, p.pos.y + p.height - 0.2, p.pos.z), r: 0.34 };
    },
    // Unified hitscan. shooter: 'player' or an agent. Returns hit info or null.
    hitscan(ro, rd, range, shooter) {
      const world = BI.Physics.raycast(ro.x, ro.y, ro.z, rd.x, rd.y, rd.z, range, { mode: 'bullet' });
      let maxT = world ? world.t : range;
      let hit = world ? { kind: 'world', t: world.t, point: new THREE.Vector3(world.x, world.y, world.z), normal: new THREE.Vector3(world.nx, world.ny, world.nz), surface: world.surface } : null;
      if (shooter === 'player') {
        const a = this.raycastAgents(ro, rd, maxT);
        if (a) hit = { kind: 'agent', t: a.t, agent: a.agent, head: a.head, point: ro.clone().addScaledVector(rd, a.t) };
      } else if (BI.player && BI.player.alive) {
        const hb = this.playerHitbox();
        const t = rayCapsule(ro, rd, hb.a, hb.b, hb.r);
        if (t >= 0 && t < maxT) hit = { kind: 'player', t, point: ro.clone().addScaledVector(rd, t) };
      }
      return hit;
    },
    impact(hit, dir) {
      const p = hit.point, n = hit.normal || new THREE.Vector3(-dir.x, -dir.y, -dir.z);
      const s = hit.surface || 'concrete';
      if (s === 'metal' || s === 'glass') {
        BI.FX.sparks(p, n, 9, { speed: 5, life: 0.35, floor: p.y - 2 });
        BI.FX.flashBurst(p, [4, 3, 1.5], 0.25);
      } else if (s === 'wood' || s === 'crate') {
        BI.FX.dust(p, n, 4, [0.32, 0.24, 0.16]);
      } else if (s === 'grass' || s === 'foliage') {
        BI.FX.dust(p, n, 4, [0.18, 0.22, 0.12]);
      } else if (s === 'sand' || s === 'dirt' || s === 'gravel' || s === 'terrain') {
        BI.FX.dust(p, n, 6, [0.33, 0.29, 0.23]);
      } else {
        BI.FX.dust(p, n, 5, [0.38, 0.37, 0.35]);
        BI.FX.sparks(p, n, 3, { speed: 4, life: 0.25 });
      }
      if (s !== 'foliage' && s !== 'glass') BI.FX.decal(p, n, 0.09 + Math.random() * 0.05);
      BI.Events.emit('fx:impact', { pos: p.clone(), surface: s });
    },
    // One world-space flash light shared by every shooter (keeps light count fixed).
    flashAt(pos, color, strength) {
      const l = this.flashLight;
      if (!l) return;
      l.position.copy(pos);
      l.color.setHex(color || 0xffb060);
      l.intensity = 3.2 * (strength || 1);
      this.flashT = 0.06;
    },
  };

  // ---------------------------------------------------------------------------
  // Player weapon system
  // ---------------------------------------------------------------------------
  class WeaponSystem {
    constructor() {
      this.defs = WEAPONS;
      this.index = 0;
      this.state = 'ready'; // ready | lowering | raising
      this.switchT = 0;
      this.pending = -1;
      this.cooldown = 0;
      this.bloom = 0;
      this.shots = 0;
      this.zoom = 0;
      this.swingT = -1;
      this.swingSide = 1;
      this.boltT = -1;
      this.recoil = { z: 0, vz: 0, rx: 0, vrx: 0 };
      this.sway = { x: 0, y: 0, rx: 0, ry: 0 };
      this.flashT = 0;
      this.hitAgents = 0;

      const E = BI.Engine;
      const vm = BI.Models.viewmodels;
      this.models = { smg: vm.smg(), sniper: vm.sniper(), knife: vm.knife() };
      this.holder = new THREE.Group();
      this.holder.scale.setScalar(0.85);
      E.viewCamera.add(this.holder);
      for (const k of Object.keys(this.models)) {
        this.holder.add(this.models[k].group);
        this.models[k].group.visible = false;
      }
      this.flash = vm.muzzleFlash();
      E.viewCamera.add(this.flash.group);
      // view scene lighting (follows the world light at the player)
      this.vHemi = new THREE.HemisphereLight(0x8090b0, 0x202020, 0.6);
      this.vKey = new THREE.DirectionalLight(0xffffff, 0.6);
      this.vKey.position.set(-1, 2, 1.5);
      this.vFlash = new THREE.PointLight(0xffb060, 0, 2.5, 2);
      E.viewScene.add(this.vHemi, this.vKey);
      E.viewCamera.add(this.vFlash);
      // shared world flash light
      Combat.flashLight = new THREE.PointLight(0xffb060, 0, 14, 2);
      E.scene.add(Combat.flashLight);
      this.setWeapon(0, true);
    }

    get def() {
      return this.defs[this.index];
    }
    get model() {
      return this.models[this.def.id];
    }

    setWeapon(i, instant) {
      if (i === this.index && !instant) return;
      if (instant) {
        this.index = i;
        for (const k of Object.keys(this.models)) this.models[k].group.visible = k === this.def.id;
        this.state = 'ready';
        BI.Events.emit('weapon:switched', { weapon: this.def });
        return;
      }
      if (this.state === 'lowering') { this.pending = i; return; }
      this.pending = i;
      this.state = 'lowering';
      this.switchT = 0;
      BI.Events.emit('weapon:switching', { to: this.defs[i] });
    }

    currentSpread() {
      const d = this.def;
      if (d.type !== 'hitscan') return 0;
      const p = BI.player;
      const move = U.clamp01(Math.hypot(p.vel.x, p.vel.z) / 4.3);
      return d.spread + d.moveSpread * move + this.bloom + (p.onGround ? 0 : 0.02) - (p.crouched ? d.spread * 0.35 : 0);
    }

    // Muzzle position in WORLD space matching where it appears on screen.
    muzzleWorld(out) {
      const E = BI.Engine;
      const m = this.model.muzzle;
      m.updateMatrixWorld(true);
      const p = m.getWorldPosition(new THREE.Vector3());
      E.viewCamera.worldToLocal(p);
      const tv = Math.tan(THREE.MathUtils.degToRad(E.viewCamera.fov / 2)), tm = Math.tan(THREE.MathUtils.degToRad(E.camera.fov / 2));
      const z = p.z;
      p.x = (p.x / tv) * tm;
      p.y = (p.y / tv) * tm;
      p.z = z;
      return (out || p).copy(E.camera.localToWorld(p));
    }

    fire() {
      const d = this.def;
      const E = BI.Engine, p = BI.player;
      this.cooldown = 60 / d.rpm;
      this.shots++;
      const cam = E.camera;
      const origin = cam.getWorldPosition(new THREE.Vector3());
      const fwd = cam.getWorldDirection(new THREE.Vector3());
      if (d.type === 'melee') {
        this.swingT = 0;
        this.swingSide = -this.swingSide;
        BI.Events.emit('weapon:fired', { weapon: d, pos: p.pos.clone() });
        // cone check against agents
        let best = null, bestD = 1e9;
        for (const ag of BI.Agents ? BI.Agents.list : []) {
          if (!ag.alive) continue;
          const c = ag.pos.clone();
          c.y += 1.1;
          const to = c.sub(origin);
          const dist = to.length();
          if (dist > d.range + 0.4) continue;
          if (to.normalize().dot(fwd) < d.coneCos) continue;
          if (!BI.Physics.lineOfSight(origin.x, origin.y, origin.z, ag.pos.x, ag.pos.y + 1.1, ag.pos.z)) continue;
          if (dist < bestD) { bestD = dist; best = ag; }
        }
        if (best) {
          // backstab: hitting an agent from behind that is not in combat
          const behind = best.forward().dot(fwd) > 0.4;
          const dmg = d.damage * (behind && !best.inCombat() ? d.backstabMul : 1);
          setTimeout(() => {
            if (!best.alive) return;
            const killed = best.takeDamage(dmg, p.pos, false, 'knife');
            BI.Events.emit('hit:marker', { kill: killed, head: false, melee: true });
            BI.FX.sparks(best.pos.clone().setY(best.pos.y + 1.2), { x: -fwd.x, y: 0.3, z: -fwd.z }, 4, { speed: 2, color: [1.2, 1.0, 0.8] });
          }, 110);
        } else {
          const hit = BI.Physics.raycast(origin.x, origin.y, origin.z, fwd.x, fwd.y, fwd.z, d.range, { mode: 'bullet' });
          if (hit) setTimeout(() => {
            BI.FX.sparks(new THREE.Vector3(hit.x, hit.y, hit.z), { x: hit.nx, y: hit.ny, z: hit.nz }, 5, { speed: 3 });
            BI.Events.emit('fx:melee', { surface: hit.surface, pos: new THREE.Vector3(hit.x, hit.y, hit.z) });
          }, 110);
        }
        BI.Events.emit('noise', { pos: p.pos.clone(), radius: d.noise, type: 'melee', source: 'player' });
        return;
      }

      // hitscan with spread (cone around the view direction)
      const spread = this.currentSpread() * (this.zoom > 0.8 ? 0 : 1);
      const dir = fwd.clone();
      if (spread > 0) {
        const a = Math.random() * Math.PI * 2, r = Math.sqrt(Math.random()) * spread;
        const right = new THREE.Vector3().crossVectors(dir, cam.up).normalize();
        const up = new THREE.Vector3().crossVectors(right, dir).normalize();
        dir.addScaledVector(right, Math.cos(a) * r).addScaledVector(up, Math.sin(a) * r).normalize();
      }
      const hit = Combat.hitscan(origin, dir, d.range, 'player');
      const muzzle = this.muzzleWorld();
      const end = hit ? hit.point : origin.clone().addScaledVector(dir, d.range);
      if (this.shots % d.tracerEvery === 0) BI.FX.tracer(muzzle, end);
      if (hit && hit.kind === 'agent') {
        const dist = hit.t;
        const fall = U.lerp(1, d.falloffMin, U.smoothstep(d.falloffStart, d.falloffEnd, dist));
        const dmg = d.damage * fall * (hit.head ? d.headMul : 1);
        const killed = hit.agent.takeDamage(dmg, p.pos, hit.head, d.id);
        this.hitAgents++;
        BI.Events.emit('hit:marker', { kill: killed, head: hit.head });
        BI.FX.sparks(hit.point, { x: -dir.x, y: -dir.y + 0.3, z: -dir.z }, hit.agent.type === 'guard' ? 8 : 3, { speed: 3, life: 0.25, color: hit.agent.type === 'guard' ? [3.5, 2.2, 0.9] : [1.0, 0.9, 0.8] });
      } else if (hit) {
        Combat.impact(hit, dir);
      }
      // muzzle flash (view + world light), casing, recoil, bloom, noise
      this.flashT = 0.05;
      this.flash.group.rotation.z = Math.random() * Math.PI;
      Combat.flashAt(muzzle, 0xffb060, d.flash);
      if (d.casing) {
        const right = new THREE.Vector3(1, 0, 0).applyQuaternion(cam.quaternion);
        const vel = right.multiplyScalar(1.8 + Math.random()).add(new THREE.Vector3(0, 1.6 + Math.random(), 0)).addScaledVector(fwd, -0.4);
        BI.FX.casing(muzzle.clone().addScaledVector(fwd, -0.25).add(new THREE.Vector3(0, -0.05, 0)), vel);
      }
      this.recoil.vz += d.recoil.kick * 30;
      this.recoil.vrx += d.recoil.rot * 30;
      p.kick.pitch += d.recoil.pitch * (0.8 + Math.random() * 0.4);
      p.kick.yaw += (Math.random() - 0.5) * 2 * d.recoil.yaw;
      this.bloom = Math.min(d.bloomMax, this.bloom + d.bloomPerShot);
      if (d.bolt) this.boltT = 0;
      BI.Events.emit('weapon:fired', { weapon: d, pos: p.pos.clone() });
      BI.Events.emit('noise', { pos: p.pos.clone(), radius: d.noise, type: 'gunfire', source: 'player' });
    }

    update(dt) {
      const In = BI.Input, p = BI.player, E = BI.Engine;
      const d = this.def;
      this.cooldown -= dt;
      this.bloom = Math.max(0, this.bloom - dt * 0.06);

      // ---- input: switching -------------------------------------------------------
      if (p.alive) {
        if (In.pressed('Digit1')) this.setWeapon(0);
        if (In.pressed('Digit2')) this.setWeapon(1);
        if (In.pressed('Digit3')) this.setWeapon(2);
        if (In.wheel !== 0 && this.state === 'ready') this.setWeapon((this.index + (In.wheel > 0 ? 1 : 2)) % 3);
      }
      if (this.state === 'lowering') {
        this.switchT += dt / 0.18;
        if (this.switchT >= 1) {
          this.index = this.pending;
          this.pending = -1;
          for (const k of Object.keys(this.models)) this.models[k].group.visible = k === this.def.id;
          this.state = 'raising';
          this.switchT = 1;
          this.boltT = -1;
          this.swingT = -1;
          BI.Events.emit('weapon:switched', { weapon: this.def });
        }
      } else if (this.state === 'raising') {
        this.switchT -= dt / 0.22;
        if (this.switchT <= 0) { this.switchT = 0; this.state = 'ready'; }
      }

      // ---- zoom (sniper) -------------------------------------------------------------
      const wantZoom = p.alive && d.zoomFov && In.mouseDown(2) && this.state === 'ready' && !p.sprinting;
      this.zoom = U.damp(this.zoom, wantZoom ? 1 : 0, 14, dt);
      if (this.zoom < 0.01) this.zoom = 0;
      E.camera.fov = U.lerp(72, d.zoomFov || 72, this.zoom);
      E.camera.updateProjectionMatrix();
      p.zoomSensMul = U.lerp(1, d.zoomSens || 1, this.zoom);
      p.speedMul = this.zoom > 0.5 ? 0.6 : 1;

      // ---- firing ---------------------------------------------------------------------
      const trigger = d.auto ? In.mouseDown(0) : In.mousePressed(0);
      if (p.alive && trigger && this.cooldown <= 0 && this.state === 'ready' && !(p.sprinting && d.type !== 'melee')) this.fire();
      // recoil recovery
      p.kick.pitch = U.damp(p.kick.pitch, 0, 7, dt);
      p.kick.yaw = U.damp(p.kick.yaw, 0, 7, dt);

      // ---- viewmodel animation ---------------------------------------------------------
      const r = this.recoil;
      r.vz += (-r.z * 180 - r.vz * 18) * dt;
      r.z += r.vz * dt;
      r.vrx += (-r.rx * 160 - r.vrx * 16) * dt;
      r.rx += r.vrx * dt;
      const sw = this.sway;
      sw.x = U.damp(sw.x, U.clamp(-In.mouseDX * 0.0005, -0.03, 0.03), 10, dt);
      sw.y = U.damp(sw.y, U.clamp(In.mouseDY * 0.0005, -0.03, 0.03), 10, dt);
      const bobX = Math.cos(p.bob) * 0.009 * p.bobAmp, bobY = -Math.abs(Math.sin(p.bob)) * 0.012 * p.bobAmp;
      const sprint = U.damp(this._sprint || 0, p.sprinting ? 1 : 0, 8, dt);
      this._sprint = sprint;
      const sw01 = U.smoothstep(0, 1, this.switchT);
      const m = this.model;
      const breathe = Math.sin(E.time * 1.4) * 0.002;
      this.holder.position.set(sw.x + bobX - sprint * 0.04 - this.zoom * 0.15, sw.y + bobY + breathe - sw01 * 0.3 - sprint * 0.03 - (p.alive ? 0 : 0.5), r.z * 0.6);
      this.holder.rotation.set(r.rx * 0.6 + sw.y * 2 - sw01 * 0.7 - sprint * 0.25, sw.x * 2 + sprint * 0.5, sw.x * 1.5 + sprint * 0.15);
      this.holder.visible = this.zoom < 0.85;

      if (d.id === 'knife') {
        const k = m;
        if (this.swingT >= 0) {
          this.swingT += dt / d.swing;
          const t = Math.min(1, this.swingT);
          const arc = Math.sin(t * Math.PI);
          const s = this.swingSide;
          k.hand.position.set(0.17 - arc * 0.2 * s - (s < 0 ? 0.0 : 0), -0.18 + arc * 0.06, -0.33 - arc * 0.12);
          k.hand.rotation.set(0.25 - arc * 0.3, 0.25 + arc * 0.9 * s, -0.4 - arc * 0.8 * s);
          if (this.swingT >= 1) this.swingT = -1;
        } else {
          k.hand.position.set(0.17, -0.18, -0.33);
          k.hand.rotation.set(0.25, 0.25, -0.4);
        }
      }
      if (d.id === 'sniper' && this.boltT >= 0) {
        this.boltT += dt / d.bolt;
        const t = this.boltT, b = m.bolt;
        const up = U.smoothstep(0.15, 0.3, t) * (1 - U.smoothstep(0.7, 0.85, t));
        const back = U.smoothstep(0.3, 0.45, t) * (1 - U.smoothstep(0.55, 0.7, t));
        b.rotation.z = up * 1.2;
        b.position.z = 0.08 + back * 0.09;
        m.gun.rotation.z = up * 0.25;
        if (t > 0.35 && !this._boltSound) { this._boltSound = true; BI.Events.emit('weapon:bolt', {}); }
        if (t >= 1) { this.boltT = -1; this._boltSound = false; m.gun.rotation.z = 0; }
      }

      // ---- muzzle flash -------------------------------------------------------------------
      if (this.flashT > 0) {
        this.flashT -= dt;
        const mp = m.muzzle.getWorldPosition(new THREE.Vector3());
        E.viewCamera.worldToLocal(mp);
        this.flash.group.position.copy(mp);
        const s = d.flash * (0.8 + Math.random() * 0.4);
        this.flash.group.scale.set(s, s, s);
        this.flash.group.visible = this.zoom < 0.85;
        this.vFlash.position.copy(mp);
        this.vFlash.intensity = 4;
      } else {
        this.flash.group.visible = false;
        this.vFlash.intensity = 0;
      }
      if (Combat.flashT > 0) {
        Combat.flashT -= dt;
        if (Combat.flashT <= 0) Combat.flashLight.intensity = 0;
      }

      // ---- view lighting follows the world ------------------------------------------------
      const L = BI.Lighting;
      const inter = p.interior, bo = L.blackout;
      const amb = new THREE.Color(0x6f7fa6).lerp(new THREE.Color(0x707478), inter).lerp(new THREE.Color(0x5a1410), bo * inter);
      this.vHemi.color.copy(amb);
      this.vHemi.groundColor.setHex(0x1a1a1c);
      this.vHemi.intensity = U.lerp(0.55, 0.45, inter) * (1 - 0.35 * bo * inter);
      const probe = L.probe;
      const k = Math.min(1.2, (probe.r + probe.g + probe.b) / 3);
      this.vKey.color.setRGB(0.5 + probe.r * 0.6, 0.5 + probe.g * 0.6, 0.55 + probe.b * 0.6).multiplyScalar(0.6);
      this.vKey.intensity = U.lerp(0.45, 0.25, inter) + k * 0.6 + (p.flashOn ? 0.5 : 0);
    }
  }

  BI.WEAPONS = WEAPONS;
  BI.Combat = Combat;
  BI.WeaponSystem = WeaponSystem;
})();
