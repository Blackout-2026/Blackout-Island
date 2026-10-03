/* =============================================================================
 * nav/astar.js — A* path finding, path smoothing, request queue, sound flood
 * -----------------------------------------------------------------------------
 * A*: 8-connected grid, octile heuristic, binary heap, typed arrays reused
 * between searches (generation stamps instead of clearing). Moves are only
 * allowed between cells whose floor heights differ by <= 0.6 m (ramps yes,
 * cliffs no) and diagonals may not cut corners. Cells next to walls cost
 * more, so paths keep a little distance from walls and pillars.
 *
 * Smoothing: string pulling — keep a waypoint only when the straight segment
 * to the next candidate leaves walkable space (sampled with side offsets for
 * the agent's radius).
 *
 * Requests are queued and processed within a per-frame time budget so many
 * agents re-planning at once never cause a spike.
 *
 * Sound: floodSound() is a Dijkstra flood limited to the noise radius that
 * walks around sound-blocking walls and closed doors — so a gunshot heard
 * through an open doorway travels the corridor, not through solid concrete.
 * Role: Agent Controller.
 * ========================================================================== */
(function () {
  'use strict';
  const Nav = BI.Nav;
  const SQ2 = Math.SQRT2;

  let gScore, parent, stamp, closed, gen = 1;
  let heapIdx, heapF, heapSize = 0;

  function ensure() {
    const n = Nav.W * Nav.H;
    if (gScore && gScore.length === n) return;
    gScore = new Float32Array(n);
    parent = new Int32Array(n);
    stamp = new Uint32Array(n);
    closed = new Uint32Array(n);
    heapIdx = new Int32Array(n);
    heapF = new Float32Array(n);
  }

  function push(i, f) {
    let k = heapSize++;
    heapIdx[k] = i;
    heapF[k] = f;
    while (k > 0) {
      const p = (k - 1) >> 1;
      if (heapF[p] <= heapF[k]) break;
      const ti = heapIdx[p], tf = heapF[p];
      heapIdx[p] = heapIdx[k]; heapF[p] = heapF[k];
      heapIdx[k] = ti; heapF[k] = tf;
      k = p;
    }
  }
  function pop() {
    const top = heapIdx[0];
    heapSize--;
    if (heapSize > 0) {
      heapIdx[0] = heapIdx[heapSize];
      heapF[0] = heapF[heapSize];
      let k = 0;
      for (;;) {
        const l = k * 2 + 1, r = l + 1;
        let m = k;
        if (l < heapSize && heapF[l] < heapF[m]) m = l;
        if (r < heapSize && heapF[r] < heapF[m]) m = r;
        if (m === k) break;
        const ti = heapIdx[m], tf = heapF[m];
        heapIdx[m] = heapIdx[k]; heapF[m] = heapF[k];
        heapIdx[k] = ti; heapF[k] = tf;
        k = m;
      }
    }
    return top;
  }

  const DX = [1, -1, 0, 0, 1, 1, -1, -1];
  const DZ = [0, 0, 1, -1, 1, -1, 1, -1];
  const DC = [1, 1, 1, 1, SQ2, SQ2, SQ2, SQ2];

  function canStep(a, b) {
    return Math.abs(Nav.height[a] - Nav.height[b]) <= 0.6;
  }

  // Returns an array of THREE.Vector3 (smoothed) or null.
  function findPath(sx, sz, tx, tz, maxNodes) {
    ensure();
    const W = Nav.W;
    const start = Nav.nearestWalkable(sx, sz, 4);
    const goal = Nav.nearestWalkable(tx, tz, 8);
    if (start < 0 || goal < 0) return null;
    gen++;
    heapSize = 0;
    const gx = goal % W, gz = (goal / W) | 0;
    const h = (i) => {
      const dx = Math.abs((i % W) - gx), dz = Math.abs(((i / W) | 0) - gz);
      return Math.max(dx, dz) + (SQ2 - 1) * Math.min(dx, dz);
    };
    stamp[start] = gen;
    gScore[start] = 0;
    parent[start] = -1;
    push(start, h(start));
    let expanded = 0, found = false;
    const limit = maxNodes || 60000;
    while (heapSize > 0) {
      const cur = pop();
      if (closed[cur] === gen) continue;
      closed[cur] = gen;
      if (cur === goal) { found = true; break; }
      if (++expanded > limit) break;
      const cx = cur % W, cz = (cur / W) | 0;
      for (let k = 0; k < 8; k++) {
        const nx = cx + DX[k], nz = cz + DZ[k];
        if (nx < 0 || nz < 0 || nx >= W || nz >= Nav.H) continue;
        const ni = nz * W + nx;
        if (closed[ni] === gen || !Nav.isWalkable(ni) || !canStep(cur, ni)) continue;
        if (k >= 4) {
          // no corner cutting
          const a = cz * W + nx, b = nz * W + cx;
          if (!Nav.isWalkable(a) || !Nav.isWalkable(b)) continue;
        }
        const cost = DC[k] * (Nav.nearWall[ni] ? 1.6 : 1);
        const ng = gScore[cur] + cost;
        if (stamp[ni] !== gen || ng < gScore[ni]) {
          stamp[ni] = gen;
          gScore[ni] = ng;
          parent[ni] = cur;
          push(ni, ng + h(ni) * 1.05);
        }
      }
    }
    AStar.lastExpanded = expanded;
    if (!found) return null;
    const cells = [];
    for (let i = goal; i >= 0; i = parent[i]) cells.push(i);
    cells.reverse();
    return smooth(cells, tx, tz);
  }

  // Walkable straight line between two world points (with radius offsets).
  function clearLine(ax, az, ay, bx, bz) {
    const dx = bx - ax, dz = bz - az;
    const len = Math.hypot(dx, dz);
    if (len < 0.01) return true;
    const px = -dz / len * 0.32, pz = dx / len * 0.32;
    const steps = Math.ceil(len / 0.3);
    let prevH = ay;
    for (let s = 1; s <= steps; s++) {
      const t = s / steps;
      const x = ax + dx * t, z = az + dz * t;
      for (const o of [0, 1, -1]) {
        const [ix, iz] = Nav.cellOf(x + px * o, z + pz * o);
        if (!Nav.inBounds(ix, iz)) return false;
        const i = Nav.index(ix, iz);
        if (!Nav.isWalkable(i)) return false;
        if (o === 0) {
          if (Math.abs(Nav.height[i] - prevH) > 0.6) return false;
          prevH = Nav.height[i];
        }
      }
    }
    return true;
  }

  function smooth(cells, tx, tz) {
    const pts = cells.map((i) => {
      const [x, z] = Nav.center(i % Nav.W, (i / Nav.W) | 0);
      return new THREE.Vector3(x, Nav.height[i], z);
    });
    // use the exact target as the last point when it is walkable
    if (Nav.walkableAt(tx, tz)) pts[pts.length - 1] = new THREE.Vector3(tx, Nav.heightAt(tx, tz), tz);
    if (pts.length <= 2) return pts;
    const out = [pts[0]];
    let i = 0;
    while (i < pts.length - 1) {
      let j = pts.length - 1;
      while (j > i + 1 && !clearLine(pts[i].x, pts[i].z, pts[i].y, pts[j].x, pts[j].z)) j--;
      out.push(pts[j]);
      i = j;
    }
    return out;
  }

  // ---------------------------------------------------------------------------
  // Budgeted request queue
  // ---------------------------------------------------------------------------
  const queue = [];
  function request(owner, from, to, cb) {
    for (let k = queue.length - 1; k >= 0; k--) if (queue[k].owner === owner) queue.splice(k, 1);
    queue.push({ owner, from: from.clone(), to: to.clone(), cb });
  }
  function update() {
    const t0 = performance.now();
    let n = 0;
    while (queue.length && (n < 1 || performance.now() - t0 < 3.5) && n < 4) {
      const r = queue.shift();
      const path = findPath(r.from.x, r.from.z, r.to.x, r.to.z);
      r.cb(path);
      n++;
    }
  }

  // ---------------------------------------------------------------------------
  // Sound propagation flood (Dijkstra within radius, walls and closed doors block)
  // ---------------------------------------------------------------------------
  let sDist, sStamp, sGen = 1;
  function floodSound(x, z, radius) {
    const n = Nav.W * Nav.H;
    if (!sDist) { sDist = new Float32Array(n); sStamp = new Uint32Array(n); }
    ensure();
    sGen++;
    gen++;
    heapSize = 0;
    const W = Nav.W;
    const [cx, cz] = Nav.cellOf(x, z);
    if (!Nav.inBounds(cx, cz)) return;
    const s = cz * W + cx;
    sStamp[s] = sGen;
    sDist[s] = 0;
    push(s, 0);
    const R = radius / Nav.CELL;
    while (heapSize > 0) {
      const cur = pop();
      if (closed[cur] === gen) continue;
      closed[cur] = gen;
      const d0 = sDist[cur];
      const ax = cur % W, az = (cur / W) | 0;
      for (let k = 0; k < 8; k++) {
        const nx = ax + DX[k], nz = az + DZ[k];
        if (nx < 0 || nz < 0 || nx >= W || nz >= Nav.H) continue;
        const ni = nz * W + nx;
        if (closed[ni] === gen) continue;
        if (Nav.sound[ni] || Nav.doorSound[ni]) continue;
        const nd = d0 + DC[k];
        if (nd > R) continue;
        if (sStamp[ni] !== sGen || nd < sDist[ni]) {
          sStamp[ni] = sGen;
          sDist[ni] = nd;
          push(ni, nd);
        }
      }
    }
  }
  // Path distance (metres) from the last flood source, or Infinity.
  function soundDistAt(x, z) {
    const [ix, iz] = Nav.cellOf(x, z);
    if (!Nav.inBounds(ix, iz) || !sStamp) return Infinity;
    const i = iz * Nav.W + ix;
    // the listener may stand in a wall-adjacent cell: check neighbours too
    let best = Infinity;
    for (let dz = -1; dz <= 1; dz++) for (let dx = -1; dx <= 1; dx++) {
      const j = i + dz * Nav.W + dx;
      if (j >= 0 && j < sStamp.length && sStamp[j] === sGen) best = Math.min(best, sDist[j] + Math.hypot(dx, dz));
    }
    return best * Nav.CELL;
  }

  const AStar = { findPath, request, update, floodSound, soundDistAt, clearLine, lastExpanded: 0, queue };
  BI.AStar = AStar;
})();
