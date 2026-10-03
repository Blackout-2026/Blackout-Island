/* =============================================================================
 * physics/crate.js — the pushable barricade crate in the corridor
 * -----------------------------------------------------------------------------
 * Simple rigid-body-style response (translation only, axis-aligned box):
 *   - the player controller calls onPush(dir, speed) while walking into it
 *   - push force accelerates the crate toward a fraction of the push speed
 *     (it is heavy), Coulomb-style friction decelerates it when released
 *   - box-vs-box collision with walls/pillars removes the velocity component
 *     into the contact and separates along the smallest overlap axis
 *   - a little tilt from acceleration sells the weight visually
 * Whenever it has moved enough, it publishes `nav:changed` so the A* grid
 * re-marks the old and new footprint and agents re-plan around it.
 * Role: Systems Engineer.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  class Crate {
    constructor(scene) {
      const L = BI.Level;
      this.size = L.crate.size;
      this.half = this.size / 2;
      this.x = L.crate.x;
      this.z = L.crate.z;
      this.y = L.BUNKER_Y;
      this.vx = 0;
      this.vz = 0;
      this.pushX = 0;
      this.pushZ = 0;
      this.pushTime = 0;
      this.mass = 1;
      this.navX = this.x;
      this.navZ = this.z;
      this.scrapeTimer = 0;
      this.tiltX = 0;
      this.tiltZ = 0;
      this.moving = false;

      const g = new THREE.Group();
      const box = new THREE.Mesh(BI.Geo.roundedBox(this.size, this.size, this.size, 0.05), BI.Mat.get('crate'));
      box.castShadow = true;
      box.receiveShadow = true;
      box.position.y = this.half;
      g.add(box);
      // stencil + straps
      const label = new THREE.Mesh(new THREE.PlaneGeometry(0.9, 0.45), BI.Mat.standard({ map: BI.Tex.decal('crate', { w: 512, h: 256, text: 'MED-SUPPLY\n04-117', color: 'rgba(230,226,210,0.85)', font: 'Impact, Arial Black, sans-serif', wear: 1.2 }), transparent: true, alphaTest: 0.5, roughness: 0.9 }));
      label.position.set(0, this.half + 0.1, this.half + 0.006);
      g.add(label);
      const label2 = label.clone();
      label2.position.set(0, this.half + 0.1, -this.half - 0.006);
      label2.rotation.y = Math.PI;
      g.add(label2);
      for (const s of [-0.45, 0.45]) {
        const strap = new THREE.Mesh(new THREE.BoxGeometry(this.size + 0.02, 0.06, this.size + 0.02), BI.Mat.get('metalDark'));
        strap.position.y = this.half + s;
        g.add(strap);
      }
      g.position.set(this.x, this.y, this.z);
      scene.add(g);
      this.mesh = g;
      this.box = box;
      this.collider = BI.Physics.addBox(this.x - this.half, this.y, this.z - this.half, this.x + this.half, this.y + this.size, this.z + this.half, { surface: 'wood', tag: 'crate', owner: this, wall: false });
      BI.Engine.addUpdate((dt) => this.update(dt), 41);
    }

    // Called by the player controller with the push direction.
    onPush(dx, dz, speed) {
      // push along the dominant axis only (box slides along corridor walls)
      if (Math.abs(dx) > Math.abs(dz)) { dz = 0; dx = Math.sign(dx); } else { dx = 0; dz = Math.sign(dz); }
      this.pushX = dx * Math.min(speed, 4.5) * 0.42;
      this.pushZ = dz * Math.min(speed, 4.5) * 0.42;
      this.pushTime = 0.12;
    }

    update(dt) {
      const ax0 = this.vx, az0 = this.vz;
      if (this.pushTime > 0) {
        this.pushTime -= dt;
        this.vx = U.damp(this.vx, this.pushX, 6, dt);
        this.vz = U.damp(this.vz, this.pushZ, 6, dt);
      } else {
        // kinetic friction: constant deceleration
        const sp = Math.hypot(this.vx, this.vz);
        const dec = 5.5 * dt;
        if (sp <= dec) { this.vx = 0; this.vz = 0; }
        else { this.vx -= (this.vx / sp) * dec; this.vz -= (this.vz / sp) * dec; }
      }
      const sp = Math.hypot(this.vx, this.vz);
      this.moving = sp > 0.05;
      if (this.moving) {
        let nx = this.x + this.vx * dt, nz = this.z + this.vz * dt;
        // collide against static boxes (walls, pillars, door frames)
        const h = this.half;
        const cands = BI.Physics.candidates(nx - h - 0.1, nz - h - 0.1, nx + h + 0.1, nz + h + 0.1);
        for (const c of cands) {
          if (c === this.collider || c.type !== 'box' || !c.solid) continue;
          if (c.max.y <= this.y + 0.05 || c.min.y >= this.y + this.size) continue;
          const ox = Math.min(nx + h, c.max.x) - Math.max(nx - h, c.min.x);
          const oz = Math.min(nz + h, c.max.z) - Math.max(nz - h, c.min.z);
          if (ox <= 0 || oz <= 0) continue;
          if (ox < oz) {
            nx += nx < (c.min.x + c.max.x) / 2 ? -ox : ox;
            this.vx = 0;
          } else {
            nz += nz < (c.min.z + c.max.z) / 2 ? -oz : oz;
            this.vz = 0;
          }
          BI.Events.emit('crate:bump', { pos: new THREE.Vector3(nx, this.y + h, nz) });
        }
        this.x = nx;
        this.z = nz;
        BI.Physics.moveBox(this.collider, this.x - h, this.y, this.z - h, this.x + h, this.y + this.size, this.z + h);
        this.scrapeTimer -= dt;
        if (this.scrapeTimer <= 0) {
          this.scrapeTimer = 0.45;
          BI.Events.emit('crate:scrape', { pos: new THREE.Vector3(this.x, this.y, this.z), speed: sp });
          BI.Events.emit('noise', { pos: new THREE.Vector3(this.x, this.y + 0.5, this.z), radius: 11, type: 'crate', source: 'player' });
          BI.FX.dust(new THREE.Vector3(this.x - Math.sign(this.vx) * h, this.y + 0.05, this.z - Math.sign(this.vz) * h), { x: 0, y: 1, z: 0 }, 2, [0.3, 0.29, 0.27]);
        }
      }
      // visual tilt from acceleration
      const accX = (this.vx - ax0) / Math.max(dt, 1e-3), accZ = (this.vz - az0) / Math.max(dt, 1e-3);
      this.tiltX = U.damp(this.tiltX, U.clamp(accZ * 0.01, -0.05, 0.05), 10, dt);
      this.tiltZ = U.damp(this.tiltZ, U.clamp(-accX * 0.01, -0.05, 0.05), 10, dt);
      this.mesh.position.set(this.x, this.y, this.z);
      this.mesh.rotation.set(this.tiltX, 0, this.tiltZ);

      // notify the nav grid once it has moved or come to rest somewhere new
      const moved = Math.hypot(this.x - this.navX, this.z - this.navZ);
      if (moved > 0.6 || (!this.moving && moved > 0.05)) {
        const h = this.half + 0.6;
        BI.Events.emit('nav:changed', {
          minX: Math.min(this.x, this.navX) - h, maxX: Math.max(this.x, this.navX) + h,
          minZ: Math.min(this.z, this.navZ) - h, maxZ: Math.max(this.z, this.navZ) + h, by: 'crate',
        });
        this.navX = this.x;
        this.navZ = this.z;
      }
    }
  }

  BI.Crate = Crate;
})();
