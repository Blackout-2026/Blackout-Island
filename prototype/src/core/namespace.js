/* =============================================================================
 * Blackout Island: The Cure — core/namespace.js
 * -----------------------------------------------------------------------------
 * Creates the single global namespace `BI` that every other file attaches to
 * (classic <script> tags, no modules, so the game runs from file://).
 * Also holds small math / noise helpers shared by every system.
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';
  const BI = (window.BI = window.BI || {});

  BI.VERSION = '1.0.0';

  // ---------------------------------------------------------------------------
  // Scalar helpers
  // ---------------------------------------------------------------------------
  const U = {};
  U.clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
  U.clamp01 = (v) => (v < 0 ? 0 : v > 1 ? 1 : v);
  U.lerp = (a, b, t) => a + (b - a) * t;
  U.invLerp = (a, b, v) => U.clamp01((v - a) / (b - a));
  U.smoothstep = (e0, e1, x) => {
    const t = U.clamp01((x - e0) / (e1 - e0));
    return t * t * (3 - 2 * t);
  };
  // Frame-rate independent exponential smoothing toward a target.
  U.damp = (a, b, lambda, dt) => a + (b - a) * (1 - Math.exp(-lambda * dt));
  U.angleWrap = (a) => {
    while (a > Math.PI) a -= Math.PI * 2;
    while (a < -Math.PI) a += Math.PI * 2;
    return a;
  };
  U.angleDiff = (from, to) => U.angleWrap(to - from);
  // Rotate angle `a` toward `b` by at most `maxStep` radians.
  U.turnToward = (a, b, maxStep) => {
    const d = U.angleDiff(a, b);
    if (Math.abs(d) <= maxStep) return b;
    return U.angleWrap(a + Math.sign(d) * maxStep);
  };
  U.dist2D = (ax, az, bx, bz) => Math.hypot(ax - bx, az - bz);

  // ---------------------------------------------------------------------------
  // Deterministic RNG (mulberry32) so the world is identical every run.
  // ---------------------------------------------------------------------------
  U.rng = function (seed) {
    let s = seed >>> 0;
    const f = function () {
      s = (s + 0x6d2b79f5) >>> 0;
      let t = s;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
    f.range = (a, b) => a + (b - a) * f();
    f.int = (a, b) => Math.floor(a + (b - a + 1) * f());
    f.pick = (arr) => arr[Math.floor(f() * arr.length)];
    return f;
  };
  // Non-deterministic helpers for gameplay jitter.
  U.rand = (a, b) => a + (b - a) * Math.random();
  U.pick = (arr) => arr[Math.floor(Math.random() * arr.length)];

  // ---------------------------------------------------------------------------
  // Value noise. hash2 -> [0,1). `period` (optional) makes it tile seamlessly,
  // which every canvas texture relies on.
  // ---------------------------------------------------------------------------
  function hash2(ix, iy, seed) {
    let h = Math.imul(ix | 0, 374761393) + Math.imul(iy | 0, 668265263) + Math.imul(seed | 0, 1442695041);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    h ^= h >>> 16;
    return (h >>> 0) / 4294967296;
  }
  U.hash2 = hash2;

  function fade(t) {
    return t * t * t * (t * (t * 6 - 15) + 10);
  }

  U.noise2 = function (x, y, seed, period) {
    const x0 = Math.floor(x), y0 = Math.floor(y);
    const fx = fade(x - x0), fy = fade(y - y0);
    let ax = x0, ay = y0, bx = x0 + 1, by = y0 + 1;
    if (period) {
      ax = ((ax % period) + period) % period;
      bx = ((bx % period) + period) % period;
      ay = ((ay % period) + period) % period;
      by = ((by % period) + period) % period;
    }
    const s = seed || 0;
    const v00 = hash2(ax, ay, s), v10 = hash2(bx, ay, s);
    const v01 = hash2(ax, by, s), v11 = hash2(bx, by, s);
    const a = v00 + (v10 - v00) * fx;
    const b = v01 + (v11 - v01) * fx;
    return a + (b - a) * fy;
  };

  // Fractal Brownian motion, returns roughly [0,1].
  U.fbm2 = function (x, y, octaves, seed, period) {
    let amp = 0.5, sum = 0, norm = 0, f = 1;
    for (let i = 0; i < octaves; i++) {
      sum += amp * U.noise2(x * f, y * f, (seed || 0) + i * 17, period ? period * f : 0);
      norm += amp;
      amp *= 0.5;
      f *= 2;
    }
    return sum / norm;
  };

  // Ridged noise for cracks / rock veins, roughly [0,1].
  U.ridge2 = function (x, y, octaves, seed, period) {
    let amp = 0.5, sum = 0, norm = 0, f = 1;
    for (let i = 0; i < octaves; i++) {
      const n = U.noise2(x * f, y * f, (seed || 0) + i * 31, period ? period * f : 0);
      sum += amp * (1 - Math.abs(n * 2 - 1));
      norm += amp;
      amp *= 0.5;
      f *= 2;
    }
    return sum / norm;
  };

  // Distance from point to segment in XZ.
  U.distToSegment2D = function (px, pz, ax, az, bx, bz) {
    const abx = bx - ax, abz = bz - az;
    const l2 = abx * abx + abz * abz;
    let t = l2 > 0 ? ((px - ax) * abx + (pz - az) * abz) / l2 : 0;
    t = t < 0 ? 0 : t > 1 ? 1 : t;
    const cx = ax + abx * t, cz = az + abz * t;
    return Math.hypot(px - cx, pz - cz);
  };

  // Catmull-Rom spline sampled into a polyline of [x,z] points.
  U.catmullRom2D = function (pts, samplesPerSeg) {
    const out = [];
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[Math.max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.min(pts.length - 1, i + 2)];
      for (let s = 0; s < samplesPerSeg; s++) {
        const t = s / samplesPerSeg, t2 = t * t, t3 = t2 * t;
        const f = (a, b, c, d) => 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        out.push([f(p0[0], p1[0], p2[0], p3[0]), f(p0[1], p1[1], p2[1], p3[1])]);
      }
    }
    out.push(pts[pts.length - 1].slice());
    return out;
  };

  BI.U = U;

  // Shared scratch objects (avoid per-frame allocation). Only use locally.
  BI.tmp = {};
})();
