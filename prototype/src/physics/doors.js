/* =============================================================================
 * physics/doors.js — sliding and hinged doors
 * -----------------------------------------------------------------------------
 * Built from BI.Level.doors. Each door owns a collider that follows the panel
 * while it slides; a hinged door drops its collider once it has swung past
 * ~35 degrees. Opening/closing plays a sound and publishes a `noise` event
 * (so the Hunter and patrols can hear doors) plus door:opened / door:closed.
 *
 * Agents may open unlocked doors (security clearance): the movement layer
 * calls door.requestOpen() when its path crosses a closed door. Locked doors
 * (the lab maglock) block the nav grid until `generator:disabled` releases
 * the magnetic lock (fail-safe design) — part of the event chain.
 * Role: Systems Engineer.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  class Door {
    constructor(id, def, parent) {
      this.id = id;
      this.def = def;
      this.label = def.label;
      this.kind = def.kind;
      this.locked = !!def.locked;
      this.open = 0;
      this.target = 0;
      this.autoCloseTimer = 0;
      this.pos = new THREE.Vector3(def.x, def.y + def.height / 2, def.z);
      this.group = new THREE.Group();
      this.group.position.set(def.x, def.y, def.z);
      if (def.axis === 'z') this.group.rotation.y = Math.PI / 2; // panel spans z
      parent.add(this.group);
      this.build();
      // nav/wall false: open-or-closed is tracked dynamically by the nav grid
      this.collider = BI.Physics.addBox(0, 0, 0, 0, 0, 0, { surface: 'metal', nav: false, wall: false, tag: 'door', owner: this });
      this.updateCollider();
    }

    build() {
      const d = this.def;
      const M = (n) => BI.Mat.get(n);
      const w = d.width, h = d.height;
      this.panels = [];
      const mkPanel = (pw, mat, x) => {
        const g = new THREE.Group();
        const body = new THREE.Mesh(BI.Geo.roundedBox(pw, h - 0.02, 0.16, 0.03), mat);
        body.position.y = h / 2;
        body.castShadow = true;
        body.receiveShadow = true;
        g.add(body);
        g.position.x = x;
        this.group.add(g);
        return g;
      };
      if (this.id === 'bunker') {
        // heavy two-leaf blast door with hazard stripes and a locking wheel
        const mat = M('olivePaintOut');
        for (const s of [-1, 1]) {
          const leaf = mkPanel(w / 2, mat, s * w / 4);
          const stripe = new THREE.Mesh(new THREE.BoxGeometry(w / 2 - 0.1, 0.35, 0.02), M('hazard'));
          stripe.position.set(0, 0.4, 0.09);
          leaf.add(stripe);
          for (let k = 0; k < 4; k++) {
            const rib = new THREE.Mesh(new THREE.BoxGeometry(w / 2 - 0.2, 0.06, 0.04), M('metalDarkOut'));
            rib.position.set(0, 1.0 + k * 0.6, 0.1);
            leaf.add(rib);
          }
          leaf.userData.side = s;
          this.panels.push(leaf);
        }
        const wheel = new THREE.Mesh(new THREE.TorusGeometry(0.28, 0.04, 8, 20), M('pipeRed'));
        wheel.position.set(-w / 4 + 0.5, 1.6, 0.14);
        this.panels[1].add(wheel);
        this.wheel = wheel;
      } else if (this.kind === 'hinge') {
        const pivot = new THREE.Group();
        pivot.position.x = -w / 2;
        this.group.add(pivot);
        const mat = M('locker');
        const body = new THREE.Mesh(BI.Geo.roundedBox(w - 0.04, h - 0.02, 0.08, 0.02), mat);
        body.position.set(w / 2, h / 2, 0);
        body.castShadow = true;
        pivot.add(body);
        const handle = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.04, 0.06), M('steel'));
        handle.position.set(w - 0.25, 1.05, 0.08);
        pivot.add(handle);
        const sign = new THREE.Mesh(new THREE.PlaneGeometry(0.8, 0.3), BI.Mat.standard({ map: BI.Tex.decal('gendoor', { w: 512, h: 192, bg: '#d9a521', text: 'GENERATOR\nROOM', color: '#111' }), roughness: 0.6 }));
        sign.position.set(w / 2, 1.75, 0.05);
        pivot.add(sign);
        this.pivot = pivot;
      } else {
        // sliding single panel with a small window (and maglock lamp for the lab)
        const mat = this.id === 'lab' ? M('labWhite') : M('locker');
        const p = mkPanel(w, mat, 0);
        const win = new THREE.Mesh(new THREE.PlaneGeometry(0.5, 0.35), M('glassOut'));
        win.position.set(0, 1.65, 0.085);
        p.add(win);
        const stripe = new THREE.Mesh(new THREE.BoxGeometry(w - 0.1, 0.12, 0.02), M('hazard'));
        stripe.position.set(0, 0.25, 0.085);
        p.add(stripe);
        this.panels.push(p);
        if (this.def.maglock) {
          this.lockLamp = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.08, 0.05), new THREE.MeshBasicMaterial({ color: new THREE.Color(4, 0.2, 0.1) }));
          this.lockLamp.position.set(w / 2 + 0.35, h + 0.2, 0.22);
          this.group.add(this.lockLamp);
          const box = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.2, 0.08), M('metalDark'));
          box.position.set(w / 2 + 0.35, h + 0.2, 0.17);
          this.group.add(box);
        }
      }
    }

    // World-space AABB of the closed/sliding panel.
    updateCollider() {
      const d = this.def, w = d.width, h = d.height;
      const c = this.collider;
      if (this.kind === 'hinge') {
        c.enabled = this.open < 0.35;
      } else if (this.id === 'bunker') {
        // two leaves: approximate by the remaining gap-blocking width
        c.enabled = this.open < 0.8;
      } else {
        c.enabled = this.open < 0.8;
      }
      const half = (w / 2) * (this.kind === 'hinge' || this.id === 'bunker' ? 1 : 1 - this.open);
      const off = this.kind === 'slide' && this.id !== 'bunker' ? (w * this.open) : 0;
      if (d.axis === 'x') BI.Physics.moveBox(c, d.x - half + off, d.y, d.z - 0.12, d.x + half + off, d.y + h, d.z + 0.12);
      else BI.Physics.moveBox(c, d.x - 0.12, d.y, d.z - half + off, d.x + 0.12, d.y + h, d.z + half + off);
    }

    isOpen() {
      return this.open > 0.8;
    }

    setLocked(v) {
      this.locked = v;
      BI.Events.emit('door:lockchanged', { door: this });
    }

    toggle(by) {
      if (this.locked) {
        BI.Events.emit('door:locked', { door: this, by });
        return false;
      }
      this.target = this.target > 0.5 ? 0 : 1;
      this.onMove();
      return true;
    }

    requestOpen(by) {
      if (this.locked || this.target > 0.5) return false;
      this.target = 1;
      this.autoCloseTimer = 0;
      this.onMove(by);
      return true;
    }

    onMove(by) {
      BI.Events.emit(this.target > 0.5 ? 'door:opened' : 'door:closed', { door: this, by: by || 'player' });
      BI.Events.emit('noise', { pos: this.pos.clone(), radius: this.id === 'bunker' ? 22 : 14, type: 'door', source: by || 'player' });
    }

    update(dt) {
      const speed = this.id === 'bunker' ? 0.45 : this.kind === 'hinge' ? 1.6 : 1.1;
      const prev = this.open;
      if (this.open !== this.target) {
        const step = speed * dt;
        this.open = Math.abs(this.target - this.open) <= step ? this.target : this.open + Math.sign(this.target - this.open) * step;
      }
      if (this.open !== prev || !this._init) {
        this._init = true;
        const e = U.smoothstep(0, 1, this.open);
        const w = this.def.width;
        if (this.id === 'bunker') {
          for (const leaf of this.panels) leaf.position.x = leaf.userData.side * (w / 4 + e * (w / 2 + 0.1));
          if (this.wheel) this.wheel.rotation.z = -e * 6;
        } else if (this.kind === 'hinge') {
          this.pivot.rotation.y = e * 1.75; // swings into the generator room
        } else {
          this.panels[0].position.x = e * (w + 0.05);
        }
        this.updateCollider();
      }
      if (this.lockLamp) this.lockLamp.material.color.setRGB(this.locked ? 4 : 0.2, this.locked ? 0.2 : 3.5, 0.15);
    }
  }

  const Doors = {
    list: [],
    byId: {},
    build(scene) {
      const group = new THREE.Group();
      group.name = 'doors';
      scene.add(group);
      for (const id of Object.keys(BI.Level.doors)) {
        const d = new Door(id, BI.Level.doors[id], group);
        this.list.push(d);
        this.byId[id] = d;
      }
      // Fail-safe maglock: power loss releases the laboratory door.
      BI.Events.on('generator:disabled', () => {
        setTimeout(() => {
          const lab = this.byId.lab;
          lab.setLocked(false);
          lab.requestOpen('system');
          BI.Events.emit('notify', { text: 'MAGLOCK RELEASED — LABORATORY DOOR OPEN', kind: 'good' });
        }, 2600);
      });
      BI.Engine.addUpdate((dt) => {
        for (const d of this.list) d.update(dt);
      }, 40);
      this.group = group;
    },
    nearest(pos, maxDist) {
      let best = null, bd = maxDist;
      for (const d of this.list) {
        const dd = d.pos.distanceTo(pos);
        if (dd < bd) { bd = dd; best = d; }
      }
      return best;
    },
  };

  BI.Door = Door;
  BI.Doors = Doors;
})();
