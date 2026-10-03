/* =============================================================================
 * nav/navgrid.js — grid navigation mesh over the level (Agent Controller)
 * -----------------------------------------------------------------------------
 * 1 m cells covering the forest, compound, ramp and bunker. Every cell stores:
 *   walk      1 if an agent (radius ~0.35 m) can stand there
 *   height    floor height (terrain, dock, ramp or bunker floor)
 *   nearWall  1 next to an obstacle (adds path cost so agents keep off walls)
 *   sound     1 if a sound-blocking wall fills the cell (sound propagation)
 * Built by sampling the physics layer (groundAt + collider clearance), so it
 * always matches what the player collides with. Runtime updates:
 *   nav:changed (crate pushed)  -> re-evaluate that rectangle
 *   door:lockchanged / opened   -> lab maglock cells, door sound blocking
 * Role: Agent Controller.
 * ========================================================================== */
(function () {
  'use strict';
  const L = BI.Level;

  const Nav = {
    X0: -62,
    Z0: -146,
    W: 124,
    H: 252,
    CELL: 1,
    walk: null,
    height: null,
    nearWall: null,
    sound: null,
    built: false,
    version: 0,

    inBounds(ix, iz) {
      return ix >= 0 && iz >= 0 && ix < this.W && iz < this.H;
    },
    cellOf(x, z) {
      return [Math.floor((x - this.X0) / this.CELL), Math.floor((z - this.Z0) / this.CELL)];
    },
    index(ix, iz) {
      return iz * this.W + ix;
    },
    center(ix, iz) {
      return [this.X0 + (ix + 0.5) * this.CELL, this.Z0 + (iz + 0.5) * this.CELL];
    },

    // Height the floor is expected at, used to probe the physics layer.
    baseY(x, z) {
      const r = L.ramp;
      if (z < -69.5 && x > -21 && x < 11) return L.BUNKER_Y;
      if (x > r.minX - 0.6 && x < r.maxX + 0.6 && z <= r.zTop && z >= r.zBottom) return r.yTop + (r.yBottom - r.yTop) * ((r.zTop - z) / (r.zTop - r.zBottom));
      const e = L.entrance;
      if (x > e.minX && x < e.maxX && z > e.minZ && z < e.maxZ) return L.GROUND_Y;
      return BI.Terrain.height(x, z);
    },

    evaluate(ix, iz) {
      const P = BI.Physics, T = BI.Terrain;
      const [x, z] = this.center(ix, iz);
      const i = this.index(ix, iz);
      const by = this.baseY(x, z);
      const g = P.groundAt(x, z, by + 0.3, 0.45);
      let ok = g.y > -Infinity;
      let y = g.y;
      if (ok && g.terrain) {
        if (y < -0.45) ok = false; // water
        else if (T.normal(x, z).y < 0.72) ok = false; // too steep
        else if (z < T.cliffZ(x) + 1.0 && L.zoneOf(x, y, z) === 'outside') ok = false; // the hill
      }
      // clearance against obstacles (agent radius + margin)
      const r = 0.42;
      let near = 0, snd = g.y > -Infinity ? 0 : 1; // no floor = solid rock: blocks sound
      if (!(g.y > -Infinity)) y = by;
      {
        const cands = P.candidates(x - 1.2, z - 1.2, x + 1.2, z + 1.2);
        for (let k = 0; k < cands.length; k++) {
          const c = cands[k];
          if (!c.solid || c.type === 'ramp') continue;
          let lo, hi, dist;
          if (c.type === 'box') {
            lo = c.min.y; hi = c.max.y;
            const dx = Math.max(c.min.x - x, 0, x - c.max.x), dz = Math.max(c.min.z - z, 0, z - c.max.z);
            dist = Math.hypot(dx, dz);
          } else {
            lo = c.y0; hi = c.y1;
            dist = Math.max(0, Math.hypot(x - c.x, z - c.z) - c.r);
          }
          if (hi <= y + 0.46 || lo >= y + 1.9) continue; // step or overhead
          if (c.wall && dist < 0.55) snd = 1;
          if (!c.nav) continue;
          if (dist < r) ok = false;
          else if (dist < r + 0.7) near = 1;
        }
      }
      this.walk[i] = ok ? 1 : 0;
      this.height[i] = ok ? y : -100;
      this.nearWall[i] = near;
      this.sound[i] = snd;
    },

    build() {
      const n = this.W * this.H;
      this.walk = new Uint8Array(n);
      this.height = new Float32Array(n);
      this.nearWall = new Uint8Array(n);
      this.sound = new Uint8Array(n);
      this.lockBlock = new Uint8Array(n);
      this.doorSound = new Uint8Array(n);
      for (let iz = 0; iz < this.H; iz++) for (let ix = 0; ix < this.W; ix++) this.evaluate(ix, iz);
      this.built = true;
      this.updateDoors();
      BI.Events.on('nav:changed', (e) => this.rebuildRect(e.minX, e.minZ, e.maxX, e.maxZ));
      BI.Events.on('door:lockchanged', () => this.updateDoors());
      BI.Events.on('door:opened', () => this.updateDoors());
      BI.Events.on('door:closed', () => this.updateDoors());
    },

    rebuildRect(minX, minZ, maxX, maxZ) {
      const [x0, z0] = this.cellOf(minX, minZ), [x1, z1] = this.cellOf(maxX, maxZ);
      for (let iz = Math.max(0, z0); iz <= Math.min(this.H - 1, z1); iz++) {
        for (let ix = Math.max(0, x0); ix <= Math.min(this.W - 1, x1); ix++) this.evaluate(ix, iz);
      }
      this.version++;
      BI.Events.emit('nav:updated', { minX, minZ, maxX, maxZ, version: this.version });
    },

    // Locked doors block walking; closed doors block sound.
    updateDoors() {
      this.lockBlock.fill(0);
      this.doorSound.fill(0);
      for (const d of BI.Doors.list) {
        const def = d.def;
        const hw = def.width / 2 + 0.2;
        const minX = def.axis === 'x' ? def.x - hw : def.x - 0.5, maxX = def.axis === 'x' ? def.x + hw : def.x + 0.5;
        const minZ = def.axis === 'x' ? def.z - 0.5 : def.z - hw, maxZ = def.axis === 'x' ? def.z + 0.5 : def.z + hw;
        const [x0, z0] = this.cellOf(minX, minZ), [x1, z1] = this.cellOf(maxX, maxZ);
        for (let iz = z0; iz <= z1; iz++) for (let ix = x0; ix <= x1; ix++) {
          if (!this.inBounds(ix, iz)) continue;
          const i = this.index(ix, iz);
          if (d.locked) this.lockBlock[i] = 1;
          if (d.open < 0.5) this.doorSound[i] = 1;
        }
      }
      this.version++;
    },

    isWalkable(i) {
      return this.walk[i] === 1 && this.lockBlock[i] === 0;
    },
    walkableAt(x, z) {
      const [ix, iz] = this.cellOf(x, z);
      return this.inBounds(ix, iz) && this.isWalkable(this.index(ix, iz));
    },
    heightAt(x, z) {
      const [ix, iz] = this.cellOf(x, z);
      if (!this.inBounds(ix, iz)) return -100;
      return this.height[this.index(ix, iz)];
    },

    // Nearest walkable cell to (x,z) within maxR cells (spiral search).
    nearestWalkable(x, z, maxR) {
      const [cx, cz] = this.cellOf(x, z);
      if (this.inBounds(cx, cz) && this.isWalkable(this.index(cx, cz))) return this.index(cx, cz);
      for (let r = 1; r <= (maxR || 8); r++) {
        let best = -1, bd = 1e9;
        for (let dz = -r; dz <= r; dz++) for (let dx = -r; dx <= r; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dz)) !== r) continue;
          const ix = cx + dx, iz = cz + dz;
          if (!this.inBounds(ix, iz)) continue;
          const i = this.index(ix, iz);
          if (!this.isWalkable(i)) continue;
          const d = dx * dx + dz * dz;
          if (d < bd) { bd = d; best = i; }
        }
        if (best >= 0) return best;
      }
      return -1;
    },

    // Random walkable point within radius of (x,z) (used by searches/prowling).
    randomPointNear(x, z, radius, tries) {
      for (let k = 0; k < (tries || 20); k++) {
        const a = Math.random() * Math.PI * 2, r = Math.sqrt(Math.random()) * radius;
        const px = x + Math.cos(a) * r, pz = z + Math.sin(a) * r;
        const [ix, iz] = this.cellOf(px, pz);
        if (!this.inBounds(ix, iz)) continue;
        const i = this.index(ix, iz);
        if (this.isWalkable(i) && !this.nearWall[i]) return new THREE.Vector3(px, this.height[i], pz);
      }
      return null;
    },
  };

  BI.Nav = Nav;
})();
