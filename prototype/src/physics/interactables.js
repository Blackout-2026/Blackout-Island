/* =============================================================================
 * physics/interactables.js — E interactions, pickups and triggers
 * -----------------------------------------------------------------------------
 *   doors            press E (open / close, or "locked")
 *   generator        hold E for 2 s at the control console (progress ring)
 *   cure             walk into the containment chamber to take it
 *   health pickups   walk over them (only when hurt), +35 HP
 * The system picks the best interactable in front of the player each frame
 * and exposes `BI.Interact.prompt` for the HUD. Results are published as
 * events (generator:disabled, cure:taken, pickup:health) — the systems that
 * react to them (lighting, audio, AI, mission) never get called directly.
 * Role: Systems Engineer.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const L = BI.Level;

  const Interact = {
    items: [],
    prompt: null, // {text, key, hold, progress}
    holdT: 0,
    holdTarget: null,
    pickups: [],
    cure: null,

    add(item) {
      this.items.push(item);
      return item;
    },

    build(scene) {
      const BF = L.BUNKER_Y;
      // ---- doors ----------------------------------------------------------------
      for (const d of BI.Doors.list) {
        this.add({
          kind: 'door',
          pos: () => d.pos,
          radius: 2.6,
          enabled: () => true,
          text: () => (d.locked ? (d.def.maglock ? 'Locked — magnetic lock powered' : 'Locked') : d.target > 0.5 ? 'Close ' + d.label : 'Open ' + d.label),
          key: () => (d.locked ? '' : 'E'),
          use: () => d.toggle('player'),
        });
      }
      // ---- generator console (hold E) ----------------------------------------------
      const pc = L.generator.panel;
      const genPos = new THREE.Vector3(pc.x, BF + 1.15, pc.z);
      this.add({
        kind: 'generator',
        pos: () => genPos,
        radius: 2.4,
        hold: 2.0,
        enabled: () => BI.BunkerInterior.generator && BI.BunkerInterior.generator.running,
        text: () => 'Disable generator',
        key: () => 'Hold E',
        use: () => {
          const gen = BI.BunkerInterior.generator;
          gen.disable();
          BI.Events.emit('noise', { pos: genPos.clone(), radius: 60, type: 'generator', source: 'player' });
          BI.Events.emit('generator:disabled', { pos: new THREE.Vector3(L.generator.x, BF + 2, L.generator.z) });
        },
      });

      // ---- the cure: glowing vial case on the pedestal --------------------------------
      const cure = new THREE.Group();
      const M = (n) => BI.Mat.get(n);
      const base = new THREE.Mesh(BI.Geo.roundedBox(0.42, 0.08, 0.24, 0.03), M('steel'));
      cure.add(base);
      const lid = new THREE.Mesh(BI.Geo.roundedBox(0.42, 0.04, 0.24, 0.02), M('steel'));
      lid.position.y = 0.26;
      cure.add(lid);
      for (const s of [-1, 1]) {
        const post = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 0.22, 6), M('chrome'));
        post.position.set(s * 0.19, 0.13, 0);
        cure.add(post);
      }
      const vialMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(0.4, 2.6, 2.8) });
      for (let i = 0; i < 3; i++) {
        const v = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.17, 10), vialMat);
        v.position.set(-0.1 + i * 0.1, 0.13, 0);
        cure.add(v);
        const cap = new THREE.Mesh(new THREE.CylinderGeometry(0.034, 0.034, 0.03, 10), M('plasticDark'));
        cap.position.set(-0.1 + i * 0.1, 0.235, 0);
        cure.add(cap);
      }
      const glass = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.2, 0.22), M('glass'));
      glass.position.y = 0.14;
      cure.add(glass);
      cure.position.set(L.cure.x, BF + 1.45, L.cure.z);
      scene.add(cure);
      this.cure = { group: cure, taken: false, vialMat };

      // ---- health pickups ---------------------------------------------------------------
      const crossTex = BI.Tex.decal('medcross', { w: 128, h: 128, bg: '#e8e8e2', text: '+', color: '#c41e1e', font: 'Arial Black, Arial, sans-serif', fontSize: 150, wear: 0.3 });
      const kitMat = BI.Mat.standard({ map: crossTex, roughness: 0.5 });
      for (const hp of L.healthPickups) {
        const g = new THREE.Group();
        const box = new THREE.Mesh(BI.Geo.roundedBox(0.42, 0.26, 0.28, 0.05), kitMat);
        box.castShadow = true;
        g.add(box);
        const handle = new THREE.Mesh(new THREE.TorusGeometry(0.07, 0.015, 6, 12, Math.PI), M('plasticDark'));
        handle.position.y = 0.13;
        g.add(handle);
        const y = BI.Physics.groundAt(hp.x, hp.z, BF + 3.2).y;
        g.position.set(hp.x, y + 0.35, hp.z);
        scene.add(g);
        const light = BI.Lighting.addPoint({ pos: new THREE.Vector3(hp.x, y + 0.6, hp.z), color: 0x60ff90, intensity: 0.5, distance: 3, zone: 'bunker', priority: 0.4 });
        this.pickups.push({ group: g, x: hp.x, z: hp.z, y, taken: false, light });
      }

      BI.Engine.addUpdate((dt, t) => this.update(dt, t), 15);
    },

    update(dt, t) {
      const p = BI.player;
      const In = BI.Input;
      if (!p) return;
      const eye = p.eyePosition(new THREE.Vector3());
      const fwd = new THREE.Vector3();
      BI.Engine.camera.getWorldDirection(fwd);

      // ---- best interactable in front of the player ------------------------------------
      let best = null, bestScore = -1;
      if (p.alive) {
        for (const it of this.items) {
          if (!it.enabled()) continue;
          const ip = it.pos();
          const to = ip.clone().sub(eye);
          const d = to.length();
          if (d > it.radius) continue;
          const facing = to.normalize().dot(fwd);
          if (facing < 0.35) continue;
          const score = facing * 2 - d * 0.3;
          if (score > bestScore) { bestScore = score; best = it; }
        }
      }
      if (best) {
        const holding = best.hold && In.down('KeyE');
        if (best.hold) {
          if (holding && this.holdTarget === best) this.holdT += dt;
          else if (holding) { this.holdTarget = best; this.holdT = dt; }
          else { this.holdT = 0; this.holdTarget = null; }
          if (this.holdT >= best.hold) {
            this.holdT = 0;
            this.holdTarget = null;
            best.use();
          }
        } else if (In.pressed('KeyE') && best.key()) {
          best.use();
        }
        this.prompt = { text: best.text(), key: best.key(), hold: !!best.hold, progress: best.hold ? U.clamp01(this.holdT / best.hold) : 0 };
        if (best.hold && this.holdT > 0 && Math.random() < dt * 6) BI.Events.emit('interact:holdtick', {});
      } else {
        this.prompt = null;
        this.holdT = 0;
        this.holdTarget = null;
      }

      // ---- cure (walk-in trigger) -----------------------------------------------------
      const c = this.cure;
      if (c && !c.taken) {
        c.group.rotation.y += dt * 0.6;
        c.group.position.y = L.BUNKER_Y + 1.45 + Math.sin(t * 1.6) * 0.02;
        c.vialMat.color.setRGB(0.4, 2.2 + Math.sin(t * 3) * 0.5, 2.6 + Math.sin(t * 3) * 0.4);
        const dx = p.pos.x - L.cure.x, dz = p.pos.z - L.cure.z;
        if (p.alive && Math.hypot(dx, dz) < 1.05 && Math.abs(p.pos.y - L.BUNKER_Y) < 1.5) this.takeCure();
      }

      // ---- health pickups -------------------------------------------------------------------
      for (const hp of this.pickups) {
        if (hp.taken) continue;
        hp.group.rotation.y += dt * 1.2;
        hp.group.position.y = hp.y + 0.35 + Math.sin(t * 2 + hp.x) * 0.05;
        hp.light.mul = 0.7 + Math.sin(t * 3) * 0.3;
        if (!p.alive) continue;
        const d = Math.hypot(p.pos.x - hp.x, p.pos.z - hp.z);
        if (d < 1.1 && Math.abs(p.pos.y - hp.y) < 1.6) {
          if (p.health < p.maxHealth) {
            const got = p.heal(35);
            hp.taken = true;
            hp.group.visible = false;
            hp.light.intensity = 0;
            BI.Events.emit('pickup:health', { amount: got });
            BI.Events.emit('notify', { text: '+' + Math.round(got) + ' HEALTH', kind: 'good' });
          } else if (!hp.warned) {
            hp.warned = true;
            BI.Events.emit('notify', { text: 'HEALTH FULL — medkit left for later', kind: '' });
          }
        } else hp.warned = false;
      }
    },

    takeCure() {
      const c = this.cure;
      if (c.taken) return;
      c.taken = true;
      c.group.visible = false;
      BI.player.hasCure = true;
      BI.BunkerInterior.cureLight.intensity = 0;
      BI.BunkerInterior.chamberShaft.visible = false;
      BI.Events.emit('cure:taken', { pos: new THREE.Vector3(L.cure.x, L.BUNKER_Y + 1.2, L.cure.z) });
      BI.Events.emit('noise', { pos: new THREE.Vector3(L.cure.x, L.BUNKER_Y + 1, L.cure.z), radius: 18, type: 'alarm', source: 'player' });
    },
  };

  BI.Interact = Interact;
})();
