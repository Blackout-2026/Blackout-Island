/* =============================================================================
 * physics/collision.js — static world collision, ground queries, raycasts
 * -----------------------------------------------------------------------------
 * Collider types (all stored in a 2D spatial hash, 4 m cells):
 *   box   axis-aligned box   (walls, floors, crates, doors, steps)
 *   cyl   vertical cylinder  (tree trunks, posts, rocks)
 *   ramp  sloped floor quad  (the bunker ramp)
 * Terrain is handled analytically through BI.Terrain.height().
 *
 * Character collision is a vertical capsule approximated as a cylinder:
 *   - boxes whose top is below feet + STEP_UP are steps (walk up onto them)
 *   - everything else overlapping the capsule's height pushes it out along the
 *     shortest separating direction -> natural sliding along walls
 *   - groundAt() returns the highest walkable surface under a point (terrain,
 *     box tops, ramps), ceilingAt() the lowest overhead box bottom
 * raycast() walks the hash with a 2D DDA (Amanatides & Woo), so bullets and
 * AI line-of-sight checks only test colliders near the ray.
 * Role: Systems Engineer.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const CELL = 4;
  const P = {
    CELL,
    STEP_UP: 0.45,
    grid: new Map(),
    all: [],
    stamp: 1,
    nextId: 1,
  };

  const key = (ix, iz) => (ix + 2000) * 8192 + (iz + 2000);

  function bounds(c) {
    if (c.type === 'cyl') return [c.x - c.r, c.z - c.r, c.x + c.r, c.z + c.r];
    if (c.type === 'ramp') return [c.minX, c.minZ, c.maxX, c.maxZ];
    return [c.min.x, c.min.z, c.max.x, c.max.z];
  }

  function insert(c) {
    const b = bounds(c);
    const x0 = Math.floor(b[0] / CELL), z0 = Math.floor(b[1] / CELL), x1 = Math.floor(b[2] / CELL), z1 = Math.floor(b[3] / CELL);
    c._cells = [];
    for (let ix = x0; ix <= x1; ix++) {
      for (let iz = z0; iz <= z1; iz++) {
        const k = key(ix, iz);
        let list = P.grid.get(k);
        if (!list) P.grid.set(k, (list = []));
        list.push(c);
        c._cells.push(k);
      }
    }
  }

  function removeFromGrid(c) {
    if (!c._cells) return;
    for (const k of c._cells) {
      const list = P.grid.get(k);
      if (!list) continue;
      const i = list.indexOf(c);
      if (i >= 0) list.splice(i, 1);
    }
    c._cells = null;
  }

  function base(opts) {
    opts = opts || {};
    return {
      id: P.nextId++,
      surface: opts.surface || 'concrete',
      solid: opts.solid !== false, // blocks characters
      sight: opts.sight !== false, // blocks line of sight
      bullets: opts.bullets !== false, // stops bullets
      wall: opts.wall !== undefined ? opts.wall : true, // attenuates sound
      nav: opts.nav !== false, // marks nav grid blocked
      tag: opts.tag || '',
      owner: opts.owner || null,
      enabled: true,
    };
  }

  P.addBox = function (minX, minY, minZ, maxX, maxY, maxZ, opts) {
    const c = base(opts);
    c.type = 'box';
    c.min = { x: Math.min(minX, maxX), y: Math.min(minY, maxY), z: Math.min(minZ, maxZ) };
    c.max = { x: Math.max(minX, maxX), y: Math.max(minY, maxY), z: Math.max(minZ, maxZ) };
    insert(c);
    P.all.push(c);
    return c;
  };

  P.addCyl = function (x, z, r, y0, y1, opts) {
    const c = base(opts);
    c.type = 'cyl';
    c.x = x; c.z = z; c.r = r; c.y0 = y0; c.y1 = y1;
    insert(c);
    P.all.push(c);
    return c;
  };

  // Ramp: y changes linearly along `axis` ('z' or 'x') from y0 at a0 to y1 at a1.
  P.addRamp = function (minX, maxX, minZ, maxZ, axis, a0, y0, a1, y1, opts) {
    const c = base(Object.assign({ nav: false }, opts));
    c.type = 'ramp';
    Object.assign(c, { minX, maxX, minZ, maxZ, axis, a0, y0, a1, y1 });
    insert(c);
    P.all.push(c);
    return c;
  };

  P.rampY = function (c, x, z) {
    const a = c.axis === 'z' ? z : x;
    const t = U.clamp01((a - c.a0) / (c.a1 - c.a0));
    return c.y0 + (c.y1 - c.y0) * t;
  };

  P.moveBox = function (c, minX, minY, minZ, maxX, maxY, maxZ) {
    removeFromGrid(c);
    c.min.x = minX; c.min.y = minY; c.min.z = minZ;
    c.max.x = maxX; c.max.y = maxY; c.max.z = maxZ;
    insert(c);
  };

  P.remove = function (c) {
    removeFromGrid(c);
    const i = P.all.indexOf(c);
    if (i >= 0) P.all.splice(i, 1);
  };

  // Gather unique colliders overlapping an XZ rectangle.
  const _cand = [];
  P.candidates = function (minX, minZ, maxX, maxZ) {
    const s = ++P.stamp;
    _cand.length = 0;
    const x0 = Math.floor(minX / CELL), z0 = Math.floor(minZ / CELL), x1 = Math.floor(maxX / CELL), z1 = Math.floor(maxZ / CELL);
    for (let ix = x0; ix <= x1; ix++) {
      for (let iz = z0; iz <= z1; iz++) {
        const list = P.grid.get(key(ix, iz));
        if (!list) continue;
        for (let i = 0; i < list.length; i++) {
          const c = list[i];
          if (c._stamp === s || !c.enabled) continue;
          c._stamp = s;
          _cand.push(c);
        }
      }
    }
    return _cand;
  };

  // ---------------------------------------------------------------------------
  // Ground / ceiling
  // ---------------------------------------------------------------------------
  const _ground = { y: -Infinity, surface: 'none', collider: null, terrain: false };
  P.groundAt = function (x, z, feetY, stepUp) {
    const lim = feetY + (stepUp === undefined ? P.STEP_UP : stepUp);
    _ground.y = -Infinity;
    _ground.surface = 'none';
    _ground.collider = null;
    _ground.terrain = false;
    const T = BI.Terrain;
    if (T && T.heights && !T.inHole(x, z)) {
      const th = T.height(x, z);
      if (th <= lim) {
        _ground.y = th;
        _ground.terrain = true;
        _ground.surface = 'terrain';
      }
    }
    const cands = P.candidates(x, z, x, z);
    for (let i = 0; i < cands.length; i++) {
      const c = cands[i];
      if (!c.solid) continue;
      let top;
      if (c.type === 'box') {
        if (x < c.min.x || x > c.max.x || z < c.min.z || z > c.max.z) continue;
        top = c.max.y;
      } else if (c.type === 'ramp') {
        if (x < c.minX || x > c.maxX || z < c.minZ || z > c.maxZ) continue;
        top = P.rampY(c, x, z);
      } else {
        if ((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z) > c.r * c.r) continue;
        top = c.y1;
      }
      if (top <= lim && top > _ground.y) {
        _ground.y = top;
        _ground.surface = c.surface;
        _ground.collider = c;
        _ground.terrain = false;
      }
    }
    if (_ground.terrain) _ground.surface = T.surfaceAt(x, z);
    return _ground;
  };

  P.ceilingAt = function (x, z, fromY) {
    let best = Infinity;
    const cands = P.candidates(x, z, x, z);
    for (let i = 0; i < cands.length; i++) {
      const c = cands[i];
      if (c.type !== 'box' || !c.solid) continue;
      if (x < c.min.x || x > c.max.x || z < c.min.z || z > c.max.z) continue;
      if (c.min.y >= fromY - 0.01 && c.min.y < best) best = c.min.y;
    }
    return best;
  };

  // ---------------------------------------------------------------------------
  // Capsule (as cylinder) vs world. Mutates pos (x,z). Returns contact info.
  // ---------------------------------------------------------------------------
  const _res = { hit: false, nx: 0, nz: 0, colliders: [] };
  P.resolveCylinder = function (pos, radius, feetY, height, opts) {
    const stepUp = opts && opts.stepUp !== undefined ? opts.stepUp : P.STEP_UP;
    const ignore = opts && opts.ignore;
    _res.hit = false;
    _res.nx = 0;
    _res.nz = 0;
    _res.colliders.length = 0;
    const headY = feetY + height;
    for (let iter = 0; iter < 3; iter++) {
      let moved = false;
      const cands = P.candidates(pos.x - radius, pos.z - radius, pos.x + radius, pos.z + radius);
      for (let i = 0; i < cands.length; i++) {
        const c = cands[i];
        if (!c.solid || c.type === 'ramp' || c === ignore) continue;
        if (c.type === 'box') {
          if (c.max.y <= feetY + stepUp || c.min.y >= headY) continue;
          const cx = U.clamp(pos.x, c.min.x, c.max.x), cz = U.clamp(pos.z, c.min.z, c.max.z);
          let dx = pos.x - cx, dz = pos.z - cz;
          const d2 = dx * dx + dz * dz;
          if (d2 >= radius * radius) continue;
          let nx, nz, pen;
          if (d2 > 1e-10) {
            const d = Math.sqrt(d2);
            nx = dx / d; nz = dz / d; pen = radius - d;
          } else {
            // centre inside the box: push out along the smallest axis
            const l = pos.x - c.min.x, r = c.max.x - pos.x, b = pos.z - c.min.z, f = c.max.z - pos.z;
            const m = Math.min(l, r, b, f);
            if (m === l) { nx = -1; nz = 0; pen = l + radius; }
            else if (m === r) { nx = 1; nz = 0; pen = r + radius; }
            else if (m === b) { nx = 0; nz = -1; pen = b + radius; }
            else { nx = 0; nz = 1; pen = f + radius; }
          }
          pos.x += nx * pen;
          pos.z += nz * pen;
          _res.hit = true;
          _res.nx += nx;
          _res.nz += nz;
          if (_res.colliders.indexOf(c) < 0) _res.colliders.push(c);
          moved = true;
        } else {
          if (c.y1 <= feetY + stepUp || c.y0 >= headY) continue;
          const dx = pos.x - c.x, dz = pos.z - c.z;
          const rr = radius + c.r;
          const d2 = dx * dx + dz * dz;
          if (d2 >= rr * rr) continue;
          const d = Math.sqrt(d2) || 1e-5;
          const pen = rr - d;
          pos.x += (dx / d) * pen;
          pos.z += (dz / d) * pen;
          _res.hit = true;
          _res.nx += dx / d;
          _res.nz += dz / d;
          if (_res.colliders.indexOf(c) < 0) _res.colliders.push(c);
          moved = true;
        }
      }
      if (!moved) break;
    }
    if (_res.hit) {
      const l = Math.hypot(_res.nx, _res.nz) || 1;
      _res.nx /= l;
      _res.nz /= l;
    }
    return _res;
  };

  // ---------------------------------------------------------------------------
  // Raycast
  // ---------------------------------------------------------------------------
  function rayBox(ox, oy, oz, dx, dy, dz, c, maxT, out) {
    let tmin = 0, tmax = maxT, nAxis = -1, nSign = 0;
    const o = [ox, oy, oz], d = [dx, dy, dz], mn = [c.min.x, c.min.y, c.min.z], mx = [c.max.x, c.max.y, c.max.z];
    for (let a = 0; a < 3; a++) {
      if (Math.abs(d[a]) < 1e-9) {
        if (o[a] < mn[a] || o[a] > mx[a]) return false;
      } else {
        const inv = 1 / d[a];
        let t1 = (mn[a] - o[a]) * inv, t2 = (mx[a] - o[a]) * inv;
        let s = -1;
        if (t1 > t2) { const tt = t1; t1 = t2; t2 = tt; s = 1; }
        if (t1 > tmin) { tmin = t1; nAxis = a; nSign = s; }
        if (t2 < tmax) tmax = t2;
        if (tmin > tmax) return false;
      }
    }
    if (nAxis < 0) return false; // origin inside box: ignore
    out.t = tmin;
    out.nx = nAxis === 0 ? nSign : 0;
    out.ny = nAxis === 1 ? nSign : 0;
    out.nz = nAxis === 2 ? nSign : 0;
    return true;
  }

  function rayCyl(ox, oy, oz, dx, dy, dz, c, maxT, out) {
    const px = ox - c.x, pz = oz - c.z;
    const a = dx * dx + dz * dz;
    if (a < 1e-10) return false;
    const b = 2 * (px * dx + pz * dz);
    const cc = px * px + pz * pz - c.r * c.r;
    const disc = b * b - 4 * a * cc;
    if (disc < 0) return false;
    const t = (-b - Math.sqrt(disc)) / (2 * a);
    if (t < 0 || t > maxT) return false;
    const y = oy + dy * t;
    if (y < c.y0 || y > c.y1) return false;
    out.t = t;
    const hx = px + dx * t, hz = pz + dz * t;
    const l = Math.hypot(hx, hz) || 1;
    out.nx = hx / l; out.ny = 0; out.nz = hz / l;
    return true;
  }

  function rayRamp(ox, oy, oz, dx, dy, dz, c, maxT, out) {
    // plane through ramp: y = y0 + (a - a0) * k
    const k = (c.y1 - c.y0) / (c.a1 - c.a0);
    // f(t) = oy + dy t - (y0 + (oa + da t - a0) k) = 0
    const oa = c.axis === 'z' ? oz : ox, da = c.axis === 'z' ? dz : dx;
    const denom = dy - da * k;
    if (Math.abs(denom) < 1e-9) return false;
    const t = (c.y0 + (oa - c.a0) * k - oy) / denom;
    if (t < 0 || t > maxT) return false;
    const hx = ox + dx * t, hz = oz + dz * t;
    if (hx < c.minX || hx > c.maxX || hz < c.minZ || hz > c.maxZ) return false;
    out.t = t;
    const n = c.axis === 'z' ? [0, 1, -k] : [-k, 1, 0];
    const l = Math.hypot(n[0], n[1], n[2]);
    out.nx = n[0] / l; out.ny = n[1] / l; out.nz = n[2] / l;
    return true;
  }

  const _tmpHit = { t: 0, nx: 0, ny: 0, nz: 0 };

  // opts: { maxDist, mode: 'bullet' | 'sight', ignore: collider, terrain: bool }
  P.raycast = function (ox, oy, oz, dx, dy, dz, maxDist, opts) {
    const mode = (opts && opts.mode) || 'bullet';
    const ignore = opts && opts.ignore;
    let best = null;
    let bestT = maxDist;

    // --- colliders via 2D DDA over hash cells ---
    const s = ++P.stamp;
    let cx = Math.floor(ox / CELL), cz = Math.floor(oz / CELL);
    const len2 = Math.hypot(dx, dz);
    const stepX = dx > 0 ? 1 : -1, stepZ = dz > 0 ? 1 : -1;
    const tDeltaX = Math.abs(dx) > 1e-9 ? CELL / Math.abs(dx) : Infinity;
    const tDeltaZ = Math.abs(dz) > 1e-9 ? CELL / Math.abs(dz) : Infinity;
    let tMaxX = Math.abs(dx) > 1e-9 ? ((dx > 0 ? (cx + 1) * CELL : cx * CELL) - ox) / dx : Infinity;
    let tMaxZ = Math.abs(dz) > 1e-9 ? ((dz > 0 ? (cz + 1) * CELL : cz * CELL) - oz) / dz : Infinity;
    let tCell = 0;
    for (let guard = 0; guard < 400; guard++) {
      const list = P.grid.get(key(cx, cz));
      if (list) {
        for (let i = 0; i < list.length; i++) {
          const c = list[i];
          if (c._stamp === s || !c.enabled || c === ignore) continue;
          c._stamp = s;
          if (mode === 'sight' && !c.sight) continue;
          if (mode === 'bullet' && !c.bullets) continue;
          if (mode === 'sound' && !c.wall) continue;
          let ok = false;
          if (c.type === 'box') ok = rayBox(ox, oy, oz, dx, dy, dz, c, bestT, _tmpHit);
          else if (c.type === 'cyl') ok = rayCyl(ox, oy, oz, dx, dy, dz, c, bestT, _tmpHit);
          else ok = rayRamp(ox, oy, oz, dx, dy, dz, c, bestT, _tmpHit);
          if (ok && _tmpHit.t < bestT) {
            bestT = _tmpHit.t;
            best = best || {};
            best.t = _tmpHit.t;
            best.nx = _tmpHit.nx; best.ny = _tmpHit.ny; best.nz = _tmpHit.nz;
            best.collider = c;
            best.surface = c.surface;
          }
        }
      }
      if (len2 < 1e-9) break; // vertical ray: only one cell
      tCell = Math.min(tMaxX, tMaxZ);
      if (tCell > bestT) break;
      if (tMaxX < tMaxZ) { cx += stepX; tMaxX += tDeltaX; }
      else { cz += stepZ; tMaxZ += tDeltaZ; }
    }

    // --- terrain march (only when starting above the surface) ---
    const T = BI.Terrain;
    if ((!opts || opts.terrain !== false) && T && T.heights) {
      const startH = T.inHole(ox, oz) ? -100 : T.height(ox, oz);
      if (oy > startH - 0.05) {
        const stepLen = 0.6;
        let prevT = 0;
        for (let t = stepLen; t <= bestT; t += stepLen) {
          const x = ox + dx * t, y = oy + dy * t, z = oz + dz * t;
          if (T.inHole(x, z)) { prevT = t; continue; }
          if (y < T.height(x, z)) {
            let a = prevT, b = t;
            for (let k = 0; k < 8; k++) {
              const m = (a + b) / 2;
              const mx = ox + dx * m, my = oy + dy * m, mz = oz + dz * m;
              if (my < T.height(mx, mz)) b = m;
              else a = m;
            }
            const hx = ox + dx * b, hz = oz + dz * b;
            const n = T.normal(hx, hz, new THREE.Vector3());
            best = { t: b, nx: n.x, ny: n.y, nz: n.z, collider: null, surface: T.surfaceAt(hx, hz), terrain: true };
            bestT = b;
            break;
          }
          prevT = t;
        }
      }
    }
    if (best) {
      best.x = ox + dx * best.t;
      best.y = oy + dy * best.t;
      best.z = oz + dz * best.t;
    }
    return best;
  };

  // True when nothing blocks sight between two points.
  P.lineOfSight = function (ax, ay, az, bx, by, bz, ignore) {
    const dx = bx - ax, dy = by - ay, dz = bz - az;
    const d = Math.hypot(dx, dy, dz);
    if (d < 1e-4) return true;
    const hit = P.raycast(ax, ay, az, dx / d, dy / d, dz / d, d - 0.05, { mode: 'sight', ignore });
    return !hit;
  };

  // Number of sound-blocking walls between two points (sound transmission).
  P.wallsBetween = function (ax, ay, az, bx, by, bz, maxCount) {
    let count = 0;
    let ox = ax, oy = ay, oz = az;
    const dx0 = bx - ax, dy0 = by - ay, dz0 = bz - az;
    const total = Math.hypot(dx0, dy0, dz0);
    if (total < 1e-4) return 0;
    const dx = dx0 / total, dy = dy0 / total, dz = dz0 / total;
    let travelled = 0;
    let ignore = null;
    while (count < (maxCount || 4)) {
      const hit = P.raycast(ox, oy, oz, dx, dy, dz, total - travelled, { mode: 'sound', ignore, terrain: false });
      if (!hit) break;
      count++;
      // continue just past the far side of this collider
      ignore = hit.collider;
      const skip = hit.t + 0.3;
      ox += dx * skip; oy += dy * skip; oz += dz * skip;
      travelled += skip;
      if (travelled >= total) break;
    }
    return count;
  };

  BI.Physics = P;
})();
