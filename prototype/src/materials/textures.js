/* =============================================================================
 * materials/textures.js — every texture in the game, generated on <canvas>
 * -----------------------------------------------------------------------------
 * No image files are loaded. Each generator paints albedo with tileable value
 * noise (BI.U.noise2 with a period), and where it helps also derives a normal
 * map from a height field and a roughness map. Results are cached by name.
 *
 *   BI.Tex.get('concrete')  -> { map, normalMap, roughnessMap }
 *   BI.Tex.decal('DANGER', {...}) -> CanvasTexture with alpha
 *
 * Palette (kept consistent across the whole game, graded on cohesion):
 *   concrete greys  #77766f / #5d5c57      hazard yellow #d9a521
 *   military olive  #4a5136                rust          #7a4128
 *   lab white       #dfe5e6                cyan accent   #47d1d6
 *   uniform navy    #1d2633                emergency red #ff2a1a
 * Role: World Builder / Graphics.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const N = U.noise2, F = U.fbm2, R = U.ridge2;

  const cache = {};
  let maxAniso = 4;

  // ---------------------------------------------------------------------------
  // Canvas helpers
  // ---------------------------------------------------------------------------
  function makeCanvas(w, h) {
    const c = document.createElement('canvas');
    c.width = w;
    c.height = h || w;
    return c;
  }

  // Fill a canvas pixel by pixel. fn(u, v, x, y, out) writes rgba 0..255 into out.
  function paint(w, h, fn) {
    const c = makeCanvas(w, h);
    const ctx = c.getContext('2d');
    const img = ctx.createImageData(w, h);
    const d = img.data;
    const out = [0, 0, 0, 255];
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        out[3] = 255;
        fn(x / w, y / h, x, y, out);
        const i = (y * w + x) * 4;
        d[i] = out[0];
        d[i + 1] = out[1];
        d[i + 2] = out[2];
        d[i + 3] = out[3];
      }
    }
    ctx.putImageData(img, 0, 0);
    return c;
  }

  // Height field -> tangent-space normal map canvas (OpenGL / +Y green).
  function normalFromHeight(hf, w, h, strength) {
    const c = makeCanvas(w, h);
    const ctx = c.getContext('2d');
    const img = ctx.createImageData(w, h);
    const d = img.data;
    const at = (x, y) => hf[((y + h) % h) * w + ((x + w) % w)];
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const dx = (at(x + 1, y) - at(x - 1, y)) * strength;
        const dy = (at(x, y + 1) - at(x, y - 1)) * strength;
        let nx = -dx, ny = dy, nz = 1;
        const l = Math.hypot(nx, ny, nz);
        nx /= l; ny /= l; nz /= l;
        const i = (y * w + x) * 4;
        d[i] = (nx * 0.5 + 0.5) * 255;
        d[i + 1] = (ny * 0.5 + 0.5) * 255;
        d[i + 2] = (nz * 0.5 + 0.5) * 255;
        d[i + 3] = 255;
      }
    }
    ctx.putImageData(img, 0, 0);
    return c;
  }

  function grayCanvas(hf, w, h, fn) {
    return paint(w, h, (u, v, x, y, o) => {
      const g = U.clamp(fn(hf[y * w + x], x, y), 0, 1) * 255;
      o[0] = o[1] = o[2] = g;
    });
  }

  function toTex(canvas, srgb, repeat) {
    const t = new THREE.CanvasTexture(canvas);
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.anisotropy = maxAniso;
    if (srgb) t.encoding = THREE.sRGBEncoding;
    if (repeat) t.repeat.set(repeat, repeat);
    return t;
  }

  const hex = (h) => [(h >> 16) & 255, (h >> 8) & 255, h & 255];
  const mix3 = (a, b, t, o) => {
    o[0] = a[0] + (b[0] - a[0]) * t;
    o[1] = a[1] + (b[1] - a[1]) * t;
    o[2] = a[2] + (b[2] - a[2]) * t;
    return o;
  };
  const tmpA = [0, 0, 0], tmpB = [0, 0, 0];

  // Build a {map, normalMap, roughnessMap} set from a height+color function.
  // colorFn(u,v,height,out) ; heightFn(u,v) -> 0..1 ; roughFn(u,v,height)->0..1
  function surface(size, heightFn, colorFn, opts) {
    opts = opts || {};
    const w = size, h = opts.h || size;
    const hf = new Float32Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) hf[y * w + x] = heightFn(x / w, y / h, x, y);
    const albedo = paint(w, h, (u, v, x, y, o) => colorFn(u, v, hf[y * w + x], o, x, y));
    const res = { map: toTex(albedo, true) };
    // Normal / roughness maps at half resolution: GPU memory is the scarce
    // resource on integrated GPUs (see README, performance notes).
    if (opts.normal !== false) res.normalMap = toTex(half(normalFromHeight(hf, w, h, opts.normalStrength || 2.0)), false);
    if (opts.roughFn) res.roughnessMap = toTex(half(grayCanvas(hf, w, h, (hv, x, y) => opts.roughFn(x / w, y / h, hv))), false);
    res.canvas = albedo;
    return res;
  }

  function half(c) {
    if (c.width <= 128) return c;
    const o = makeCanvas(c.width >> 1, c.height >> 1);
    const ctx = o.getContext('2d');
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(c, 0, 0, o.width, o.height);
    return o;
  }

  // ---------------------------------------------------------------------------
  // Generators
  // ---------------------------------------------------------------------------
  const G = {};

  G.sand = () =>
    surface(
      512,
      (u, v) => {
        const rip = Math.sin((u * 6 + F(u * 4, v * 4, 3, 11, 4) * 1.6 + v * 1.0) * Math.PI * 2) * 0.5 + 0.5;
        return rip * 0.35 + F(u * 32, v * 32, 2, 5, 32) * 0.4 + U.hash2(u * 4096, v * 4096, 3) * 0.25;
      },
      (u, v, hv, o) => {
        const patch = F(u * 3, v * 3, 4, 7, 3);
        mix3(hex(0x6f6450), hex(0x9a8c70), patch, o);
        const g = (U.hash2(u * 9999, v * 9999, 1) - 0.5) * 26 + (hv - 0.5) * 22;
        o[0] += g; o[1] += g; o[2] += g * 0.9;
        if (U.hash2(u * 7777, v * 7777, 9) > 0.995) { o[0] = 40; o[1] = 36; o[2] = 30; }
      },
      { normalStrength: 1.6, roughFn: () => 0.95 }
    );

  G.grass = () => {
    const S = 512;
    const c = paint(S, S, (u, v, x, y, o) => {
      const p = F(u * 4, v * 4, 4, 21, 4);
      mix3(hex(0x1f2a15), hex(0x3a4a24), p, o);
      const d = F(u * 16, v * 16, 2, 22, 16);
      o[0] += (d - 0.5) * 18; o[1] += (d - 0.5) * 24; o[2] += (d - 0.5) * 10;
    });
    const ctx = c.getContext('2d');
    const r = U.rng(42);
    for (let i = 0; i < 5200; i++) {
      const x = r() * S, y = r() * S, len = 4 + r() * 10, ang = -Math.PI / 2 + (r() - 0.5) * 1.2;
      const g = 50 + r() * 70;
      ctx.strokeStyle = 'rgba(' + Math.floor(g * 0.55) + ',' + Math.floor(g) + ',' + Math.floor(g * 0.3) + ',0.5)';
      ctx.lineWidth = 1 + r();
      for (const ox of [0, -S, S]) for (const oy of [0, -S, S]) {
        if (x + ox < -20 || x + ox > S + 20 || y + oy < -20 || y + oy > S + 20) continue;
        ctx.beginPath();
        ctx.moveTo(x + ox, y + oy);
        ctx.lineTo(x + ox + Math.cos(ang) * len, y + oy + Math.sin(ang) * len);
        ctx.stroke();
      }
    }
    return { map: toTex(c, true), canvas: c };
  };

  G.forestFloor = () => {
    const S = 512;
    const c = paint(S, S, (u, v, x, y, o) => {
      const p = F(u * 5, v * 5, 4, 31, 5);
      mix3(hex(0x241c14), hex(0x40331f), p, o);
      const moss = U.smoothstep(0.55, 0.75, F(u * 3 + 3, v * 3, 3, 33, 3));
      mix3(o, hex(0x2c3a1a), moss * 0.7, o);
    });
    const ctx = c.getContext('2d');
    const r = U.rng(77);
    const wrapDraw = (x, y, fn) => {
      for (const ox of [0, -S, S]) for (const oy of [0, -S, S]) {
        if (x + ox < -30 || x + ox > S + 30 || y + oy < -30 || y + oy > S + 30) continue;
        fn(x + ox, y + oy);
      }
    };
    // pine needles
    for (let i = 0; i < 4500; i++) {
      const x = r() * S, y = r() * S, len = 5 + r() * 9, a = r() * Math.PI;
      const t = r();
      ctx.strokeStyle = t < 0.6 ? 'rgba(110,80,45,0.55)' : t < 0.85 ? 'rgba(70,55,35,0.6)' : 'rgba(140,110,60,0.45)';
      ctx.lineWidth = 1;
      wrapDraw(x, y, (px, py) => {
        ctx.beginPath();
        ctx.moveTo(px, py);
        ctx.lineTo(px + Math.cos(a) * len, py + Math.sin(a) * len);
        ctx.stroke();
      });
    }
    // fallen leaves and twigs
    for (let i = 0; i < 260; i++) {
      const x = r() * S, y = r() * S, rad = 2 + r() * 4, a = r() * Math.PI;
      const col = r() < 0.5 ? 'rgba(95,70,35,0.7)' : 'rgba(70,75,35,0.6)';
      wrapDraw(x, y, (px, py) => {
        ctx.fillStyle = col;
        ctx.beginPath();
        ctx.ellipse(px, py, rad, rad * 0.45, a, 0, Math.PI * 2);
        ctx.fill();
      });
    }
    for (let i = 0; i < 60; i++) {
      const x = r() * S, y = r() * S, len = 15 + r() * 30, a = r() * Math.PI;
      wrapDraw(x, y, (px, py) => {
        ctx.strokeStyle = 'rgba(50,38,25,0.8)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(px, py);
        ctx.lineTo(px + Math.cos(a) * len, py + Math.sin(a) * len);
        ctx.stroke();
      });
    }
    return { map: toTex(c, true), canvas: c };
  };

  G.dirt = () =>
    surface(
      512,
      (u, v) => {
        let h = F(u * 8, v * 8, 4, 41, 8) * 0.6;
        // pebbles
        const cx = Math.floor(u * 40), cy = Math.floor(v * 40);
        const px = (cx + U.hash2(cx, cy, 5)) / 40, py = (cy + U.hash2(cx, cy, 6)) / 40;
        const pr = 0.006 + U.hash2(cx, cy, 7) * 0.008;
        const dd = Math.hypot(u - px, v - py);
        if (dd < pr && U.hash2(cx, cy, 8) > 0.45) h += (1 - dd / pr) * 0.6;
        return h;
      },
      (u, v, hv, o) => {
        const p = F(u * 4, v * 4, 3, 43, 4);
        mix3(hex(0x3d2f22), hex(0x5e4a35), p, o);
        if (hv > 0.62) mix3(o, hex(0x7c7466), U.smoothstep(0.62, 0.9, hv), o);
        const g = (U.hash2(u * 5000, v * 5000, 2) - 0.5) * 18;
        o[0] += g; o[1] += g; o[2] += g;
      },
      { normalStrength: 3.0, roughFn: () => 0.95 }
    );

  G.rock = () =>
    surface(
      512,
      (u, v) => F(u * 6, v * 6, 5, 51, 6) * 0.7 + R(u * 4, v * 4, 4, 52, 4) * 0.5,
      (u, v, hv, o) => {
        mix3(hex(0x3f3e3b), hex(0x7a7872), U.clamp01(hv * 1.1 - 0.1), o);
        const crack = U.smoothstep(0.92, 0.98, R(u * 5, v * 5, 3, 53, 5));
        mix3(o, hex(0x1c1b1a), crack * 0.8, o);
        const lichen = U.smoothstep(0.62, 0.78, F(u * 9 + 2, v * 9, 3, 54, 9));
        mix3(o, hex(0x5d6436), lichen * 0.55, o);
      },
      { normalStrength: 4.0, roughFn: (u, v, hv) => 0.75 + hv * 0.2 }
    );

  G.barkPine = () =>
    surface(
      256,
      (u, v) => R(u * 8, v * 2, 4, 61, 8) * 0.8 + F(u * 16, v * 4, 2, 62, 16) * 0.2,
      (u, v, hv, o) => {
        mix3(hex(0x1e1611), hex(0x5b4636), U.smoothstep(0.3, 0.9, hv), o);
        const grey = F(u * 3, v * 3, 2, 63, 3);
        mix3(o, hex(0x4c4842), grey * 0.35, o);
      },
      { normalStrength: 5.0, roughFn: () => 0.95 }
    );

  G.barkPalm = () =>
    surface(
      256,
      (u, v) => {
        const ring = Math.abs(Math.sin(v * Math.PI * 10 + F(u * 4, v * 4, 2, 71, 4) * 1.2));
        return Math.pow(ring, 0.5) * 0.7 + F(u * 16, v * 16, 2, 72, 16) * 0.3;
      },
      (u, v, hv, o) => {
        mix3(hex(0x2c241b), hex(0x6e5d47), hv, o);
      },
      { normalStrength: 4.0, roughFn: () => 0.9 }
    );

  G.concrete = () =>
    surface(
      512,
      (u, v) => {
        const base = F(u * 8, v * 8, 4, 81, 8) * 0.3;
        const pores = U.hash2(Math.floor(u * 512), Math.floor(v * 512), 82) > 0.985 ? -0.4 : 0;
        const crack = U.smoothstep(0.93, 0.985, R(u * 3, v * 3, 4, 83, 3));
        return base + pores - crack * 0.5 + 0.5;
      },
      (u, v, hv, o) => {
        const p = F(u * 3, v * 3, 4, 84, 3);
        mix3(hex(0x5f5e59), hex(0x86857d), p, o);
        // Water stains running down (stretched noise)
        const streak = U.smoothstep(0.55, 0.85, F(u * 14, v * 1.5, 3, 85, 14) * F(u * 3, v * 3, 2, 86, 3) * 1.8);
        mix3(o, hex(0x3d3a33), streak * 0.55, o);
        const rust = U.smoothstep(0.72, 0.9, F(u * 6 + 9, v * 6, 3, 87, 6));
        mix3(o, hex(0x6b4a33), rust * 0.35, o);
        if (hv < 0.3) mix3(o, hex(0x2a2926), 0.7, o);
        const g = (U.hash2(u * 8000, v * 8000, 4) - 0.5) * 14;
        o[0] += g; o[1] += g; o[2] += g;
      },
      { normalStrength: 2.5, roughFn: (u, v, hv) => 0.78 + F(u * 4, v * 4, 2, 88, 4) * 0.2 }
    );

  // Interior wall: concrete panels with seams + lower dark band + grime.
  G.wallPanel = () =>
    surface(
      512,
      (u, v) => {
        const su = u * 2 % 1, sv = v * 2 % 1;
        const seam = Math.min(su, 1 - su, sv, 1 - sv) < 0.008 ? 0 : 1;
        const bolt = (Math.hypot(su - 0.05, sv - 0.05) < 0.012 || Math.hypot(su - 0.95, sv - 0.05) < 0.012 ||
          Math.hypot(su - 0.05, sv - 0.95) < 0.012 || Math.hypot(su - 0.95, sv - 0.95) < 0.012) ? 0.4 : 0;
        return seam * 0.6 + F(u * 10, v * 10, 3, 91, 10) * 0.25 + bolt;
      },
      (u, v, hv, o) => {
        const p = F(u * 4, v * 4, 3, 92, 4);
        mix3(hex(0x6c6e6a), hex(0x8a8c86), p, o);
        const grime = U.smoothstep(0.5, 0.95, F(u * 12, v * 2, 3, 93, 12)) * 0.5;
        mix3(o, hex(0x3b3b36), grime, o);
        if (hv < 0.1) mix3(o, hex(0x2b2b28), 0.8, o);
      },
      { normalStrength: 3.0, roughFn: (u, v, hv) => 0.7 + hv * 0.15 }
    );

  G.metalPlate = () =>
    surface(
      512,
      (u, v) => {
        const su = u * 2 % 1, sv = v * 4 % 1;
        const seam = Math.min(su, 1 - su) < 0.006 || Math.min(sv, 1 - sv) < 0.012 ? 0 : 1;
        let rivet = 0;
        for (const ru of [0.03, 0.97]) {
          for (let k = 0; k < 4; k++) {
            const dv = Math.abs(sv - (0.15 + k * 0.233));
            const d = Math.hypot((su - ru) * 2, dv);
            if (d < 0.03) rivet = Math.max(rivet, 1 - d / 0.03);
          }
        }
        const scratches = U.smoothstep(0.97, 1.0, R(u * 20, v * 3, 2, 101, 20)) * 0.2;
        return seam * 0.55 + rivet * 0.5 + F(u * 20, v * 20, 2, 102, 20) * 0.08 - scratches;
      },
      (u, v, hv, o) => {
        const p = F(u * 5, v * 5, 3, 103, 5);
        mix3(hex(0x4a4d4f), hex(0x6a6e70), p, o);
        const rust = U.smoothstep(0.6, 0.85, F(u * 7, v * 7, 4, 104, 7));
        mix3(o, hex(0x6e3c22), rust * 0.6, o);
        const scratch = U.smoothstep(0.96, 1.0, R(u * 20, v * 3, 2, 101, 20));
        mix3(o, hex(0x9da2a4), scratch * 0.6, o);
      },
      { normalStrength: 3.0, roughFn: (u, v, hv) => 0.45 + F(u * 7, v * 7, 4, 104, 7) * 0.45 }
    );

  G.diamondPlate = () =>
    surface(
      256,
      (u, v) => {
        const a = (u * 16 + v * 16) % 1, b = (u * 16 - v * 16 + 100) % 1;
        const cellU = Math.floor(u * 16), cellV = Math.floor(v * 16);
        const alt = (cellU + cellV) % 2 === 0;
        const t = alt ? a : b;
        const tread = Math.abs(t - 0.5) < 0.12 && Math.abs(((alt ? b : a)) - 0.5) < 0.35 ? 1 : 0;
        return tread * 0.6 + F(u * 8, v * 8, 2, 111, 8) * 0.2;
      },
      (u, v, hv, o) => {
        mix3(hex(0x3c3f41), hex(0x7b7f80), hv, o);
        const dirt = F(u * 3, v * 3, 3, 112, 3);
        mix3(o, hex(0x2b2722), U.smoothstep(0.5, 0.8, dirt) * 0.5, o);
      },
      { normalStrength: 6.0, roughFn: (u, v, hv) => 0.55 - hv * 0.2 }
    );

  G.grate = () =>
    surface(
      256,
      (u, v) => {
        const gu = (u * 12) % 1, gv = (v * 12) % 1;
        const bar = gu < 0.18 || gv < 0.18 ? 1 : 0;
        return bar * 0.8 + F(u * 8, v * 8, 2, 115, 8) * 0.1;
      },
      (u, v, hv, o) => {
        if (hv > 0.5) mix3(hex(0x5a5d5e), hex(0x777a7a), F(u * 6, v * 6, 2, 116, 6), o);
        else { o[0] = 14; o[1] = 15; o[2] = 16; }
      },
      { normalStrength: 5.0, roughFn: (u, v, hv) => (hv > 0.5 ? 0.5 : 0.9) }
    );

  G.rust = () =>
    surface(
      256,
      (u, v) => F(u * 10, v * 10, 4, 121, 10),
      (u, v, hv, o) => {
        mix3(hex(0x3e2214), hex(0x8a4a24), hv, o);
        const pit = U.hash2(Math.floor(u * 256), Math.floor(v * 256), 122) > 0.97;
        if (pit) mix3(o, hex(0x1e120b), 0.7, o);
      },
      { normalStrength: 3.0, roughFn: () => 0.9 }
    );

  G.tiles = () =>
    surface(
      512,
      (u, v) => {
        const tu = (u * 8) % 1, tv = (v * 8) % 1;
        const grout = Math.min(tu, 1 - tu, tv, 1 - tv) < 0.025 ? 0 : 1;
        return grout * 0.7 + F(u * 30, v * 30, 2, 131, 30) * 0.05;
      },
      (u, v, hv, o) => {
        const tile = U.hash2(Math.floor(u * 8), Math.floor(v * 8), 132);
        mix3(hex(0xb9c0c1), hex(0xd8dfe0), tile, o);
        const dirt = U.smoothstep(0.55, 0.85, F(u * 4, v * 4, 3, 133, 4));
        mix3(o, hex(0x8a8f86), dirt * 0.4, o);
        if (hv < 0.3) mix3(o, hex(0x5c605e), 0.85, o);
      },
      {
        normalStrength: 2.0,
        // Puddles -> very low roughness patches (wet decals catch highlights).
        roughFn: (u, v, hv) => {
          const wet = U.smoothstep(0.62, 0.7, F(u * 2 + 5, v * 2, 3, 134, 2));
          return hv < 0.3 ? 0.85 : U.lerp(0.4, 0.16, wet);
        },
      }
    );

  // Polished concrete floor with wet patches (security area / corridor).
  G.floorConcrete = () =>
    surface(
      512,
      (u, v) => {
        const su = (u * 2) % 1, sv = (v * 2) % 1;
        const joint = Math.min(su, 1 - su, sv, 1 - sv) < 0.004 ? 0 : 1;
        return joint * 0.5 + F(u * 12, v * 12, 3, 141, 12) * 0.2;
      },
      (u, v, hv, o) => {
        const p = F(u * 3, v * 3, 4, 142, 3);
        mix3(hex(0x4c4d4a), hex(0x6d6e69), p, o);
        const oil = U.smoothstep(0.6, 0.85, F(u * 5 + 1, v * 5, 3, 143, 5));
        mix3(o, hex(0x2c2b28), oil * 0.5, o);
        if (hv < 0.2) mix3(o, hex(0x222220), 0.8, o);
      },
      {
        normalStrength: 1.5,
        roughFn: (u, v, hv) => {
          const wet = U.smoothstep(0.6, 0.68, F(u * 2.2, v * 2.2 + 3, 3, 144, 2));
          return U.lerp(0.6, 0.14, wet);
        },
      }
    );

  G.ceiling = () =>
    surface(
      256,
      (u, v) => {
        const tu = (u * 4) % 1, tv = (v * 4) % 1;
        const edge = Math.min(tu, 1 - tu, tv, 1 - tv) < 0.03 ? 0 : 1;
        return edge * 0.6 + U.hash2(Math.floor(u * 256), Math.floor(v * 256), 151) * 0.1;
      },
      (u, v, hv, o) => {
        mix3(hex(0x55575a), hex(0x6e7074), F(u * 4, v * 4, 2, 152, 4), o);
        if (hv < 0.3) mix3(o, hex(0x2a2b2d), 0.8, o);
        const stain = U.smoothstep(0.65, 0.85, F(u * 3, v * 3, 3, 153, 3));
        mix3(o, hex(0x5b4a35), stain * 0.4, o);
      },
      { normalStrength: 2.0 }
    );

  G.hazard = () => {
    const S = 256;
    const c = paint(S, S, (u, v, x, y, o) => {
      const s = ((u + v) * 4) % 1;
      if (s < 0.5) { o[0] = 214; o[1] = 160; o[2] = 30; } else { o[0] = 22; o[1] = 22; o[2] = 20; }
      const wear = U.smoothstep(0.6, 0.85, F(u * 8, v * 8, 4, 161, 8));
      mix3(o, hex(0x5a5650), wear * 0.75, o);
      const g = (U.hash2(x, y, 162) - 0.5) * 20;
      o[0] += g; o[1] += g; o[2] += g;
    });
    return { map: toTex(c, true), canvas: c };
  };

  G.fabric = () =>
    surface(
      256,
      (u, v) => {
        const a = Math.sin(u * Math.PI * 2 * 64) * 0.5 + 0.5, b = Math.sin(v * Math.PI * 2 * 64) * 0.5 + 0.5;
        return (Math.floor(u * 64) + Math.floor(v * 64)) % 2 ? a : b;
      },
      (u, v, hv, o) => {
        const g = 150 + hv * 50 + (F(u * 6, v * 6, 3, 171, 6) - 0.5) * 60;
        o[0] = o[1] = o[2] = g;
      },
      { normalStrength: 1.2 }
    );

  G.camo = () => {
    const c = paint(512, 512, (u, v, x, y, o) => {
      const a = F(u * 5, v * 5, 4, 181, 5), b = F(u * 7 + 3, v * 7, 4, 182, 7), d = F(u * 9, v * 9 + 5, 3, 183, 9);
      mix3(hex(0x3d4127), hex(0x3d4127), 0, o);
      if (a > 0.52) mix3(hex(0x5c5a37), hex(0x5c5a37), 0, o);
      if (b > 0.56) mix3(hex(0x2a2a1c), hex(0x2a2a1c), 0, o);
      if (d > 0.6) mix3(hex(0x6b5a3d), hex(0x6b5a3d), 0, o);
      const g = (U.hash2(x, y, 184) - 0.5) * 14;
      o[0] += g; o[1] += g; o[2] += g;
    });
    return { map: toTex(c, true), canvas: c };
  };

  G.wood = () =>
    surface(
      512,
      (u, v) => {
        const plank = Math.floor(u * 6);
        const pu = (u * 6) % 1;
        const gap = pu < 0.03 || pu > 0.97 ? 0 : 1;
        const grain = Math.sin((v * 40 + F(u * 6, v * 6, 3, 191 + plank, 6) * 6) * Math.PI) * 0.5 + 0.5;
        const nail = (Math.hypot((pu - 0.5) * 0.17, ((v * 2) % 1) - 0.08) < 0.008) ? -0.5 : 0;
        return gap * (0.5 + grain * 0.25) + nail;
      },
      (u, v, hv, o, x, y) => {
        const plank = Math.floor(u * 6);
        const tone = U.hash2(plank, 3, 192);
        mix3(hex(0x3b2d20), hex(0x6a5038), tone * 0.6 + hv * 0.4, o);
        const weather = F(u * 4, v * 4, 3, 193, 4);
        mix3(o, hex(0x5a5850), weather * 0.35, o);
        if (hv < 0.1) mix3(o, hex(0x120d09), 0.85, o);
      },
      { normalStrength: 2.5, roughFn: () => 0.85 }
    );

  G.crate = () =>
    surface(
      512,
      (u, v) => {
        const border = u < 0.08 || u > 0.92 || v < 0.08 || v > 0.92;
        const diag = Math.abs(u - v) < 0.05 || Math.abs(u + v - 1) < 0.05;
        const plank = (v * 5) % 1 < 0.02 ? 0 : 1;
        return (border || diag ? 0.9 : 0.5 * plank) + F(u * 10, v * 2, 2, 201, 10) * 0.1;
      },
      (u, v, hv, o) => {
        mix3(hex(0x4a4b2c), hex(0x5d5f39), F(u * 4, v * 4, 3, 202, 4), o);
        if (hv > 0.8) mix3(o, hex(0x6e6f45), 0.5, o);
        const wear = U.smoothstep(0.65, 0.85, F(u * 8, v * 8, 3, 203, 8));
        mix3(o, hex(0x6e5a3e), wear * 0.5, o);
      },
      { normalStrength: 3.0, roughFn: () => 0.8 }
    );

  G.fence = () => {
    const S = 256;
    const c = makeCanvas(S, S);
    const ctx = c.getContext('2d');
    ctx.clearRect(0, 0, S, S);
    ctx.strokeStyle = 'rgba(170,175,175,1)';
    ctx.lineWidth = 3;
    const n = 8, step = S / n;
    for (let i = -n; i <= n * 2; i++) {
      ctx.beginPath();
      ctx.moveTo(i * step, 0);
      ctx.lineTo(i * step + S, S);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(i * step, 0);
      ctx.lineTo(i * step - S, S);
      ctx.stroke();
    }
    const t = toTex(c, true);
    return { map: t, canvas: c };
  };

  G.grassBlades = () => {
    const W = 128, H = 128;
    const c = makeCanvas(W, H);
    const ctx = c.getContext('2d');
    const r = U.rng(301);
    for (let i = 0; i < 26; i++) {
      const x = 10 + r() * (W - 20), lean = (r() - 0.5) * 40, h = H * (0.45 + r() * 0.55);
      const g = 130 + r() * 90;
      const grad = ctx.createLinearGradient(0, H, 0, H - h);
      grad.addColorStop(0, 'rgb(' + Math.floor(g * 0.25) + ',' + Math.floor(g * 0.45) + ',' + Math.floor(g * 0.15) + ')');
      grad.addColorStop(1, 'rgb(' + Math.floor(g * 0.75) + ',' + Math.floor(g * 0.95) + ',' + Math.floor(g * 0.45) + ')');
      ctx.fillStyle = grad;
      ctx.beginPath();
      ctx.moveTo(x - 2.5, H);
      ctx.quadraticCurveTo(x + lean * 0.3, H - h * 0.5, x + lean, H - h);
      ctx.quadraticCurveTo(x + lean * 0.3 + 1.5, H - h * 0.5, x + 2.5, H);
      ctx.closePath();
      ctx.fill();
    }
    const t = toTex(c, true);
    t.wrapS = t.wrapT = THREE.ClampToEdgeWrapping;
    return { map: t, canvas: c };
  };

  G.needles = () =>
    surface(
      256,
      (u, v) => R(u * 24, v * 6, 3, 311, 24) * 0.7 + F(u * 8, v * 8, 2, 312, 8) * 0.3,
      (u, v, hv, o) => {
        mix3(hex(0x1c3320), hex(0x4a6f40), hv, o);
        const tip = U.smoothstep(0.75, 1.0, hv);
        mix3(o, hex(0x6f9150), tip * 0.5, o);
      },
      { normalStrength: 4.0, roughFn: () => 0.9 }
    );

  G.leaf = () =>
    surface(
      256,
      (u, v) => {
        const vein = Math.abs(Math.sin(u * Math.PI * 24)) > 0.92 ? 0.3 : 0;
        return 0.5 + vein + F(u * 8, v * 8, 2, 321, 8) * 0.2;
      },
      (u, v, hv, o) => {
        mix3(hex(0x2c4520), hex(0x5f8640), F(u * 4, v * 4, 3, 322, 4), o);
        const dry = U.smoothstep(0.65, 0.85, F(u * 3 + 3, v * 3, 3, 323, 3));
        mix3(o, hex(0x6b6235), dry * 0.6, o);
      },
      { normalStrength: 1.5 }
    );

  G.glass = () => {
    const c = paint(256, 256, (u, v, x, y, o) => {
      const smudge = F(u * 6, v * 6, 4, 331, 6);
      const g = 200 + smudge * 55;
      o[0] = g; o[1] = g; o[2] = g;
    });
    return { map: toTex(c, false), canvas: c };
  };

  G.robotPaint = () =>
    surface(
      512,
      (u, v) => {
        const su = (u * 3) % 1, sv = (v * 3) % 1;
        const seam = Math.min(su, 1 - su, sv, 1 - sv) < 0.01 ? 0 : 1;
        return seam * 0.5 + F(u * 16, v * 16, 2, 341, 16) * 0.1;
      },
      (u, v, hv, o) => {
        mix3(hex(0x3a3f44), hex(0x4f565c), F(u * 4, v * 4, 3, 342, 4), o);
        const chip = U.smoothstep(0.7, 0.8, F(u * 12, v * 12, 3, 343, 12));
        mix3(o, hex(0x8e9396), chip * 0.7, o);
        const grime = U.smoothstep(0.5, 0.9, F(u * 2, v * 6, 3, 344, 2));
        mix3(o, hex(0x23221f), grime * 0.4, o);
      },
      { normalStrength: 2.5, roughFn: (u, v) => 0.35 + U.smoothstep(0.7, 0.8, F(u * 12, v * 12, 3, 343, 12)) * 0.3 }
    );

  G.skin = () =>
    surface(
      128,
      (u, v) => F(u * 16, v * 16, 3, 351, 16),
      (u, v, hv, o) => mix3(hex(0x9a7058), hex(0xb08368), hv, o),
      { normalStrength: 0.8 }
    );

  // Soft radial blob used for puddles / grime decals (alpha).
  G.puddle = () => {
    const c = paint(256, 256, (u, v, x, y, o) => {
      const d = Math.hypot(u - 0.5, v - 0.5) * 2 + (F(u * 5, v * 5, 3, 361, 0) - 0.5) * 0.6;
      const a = U.smoothstep(1.0, 0.6, d);
      o[0] = 25; o[1] = 26; o[2] = 28; o[3] = a * 200;
    });
    const t = toTex(c, true);
    t.wrapS = t.wrapT = THREE.ClampToEdgeWrapping;
    return { map: t, canvas: c };
  };

  G.scorch = () => {
    const c = paint(128, 128, (u, v, x, y, o) => {
      const d = Math.hypot(u - 0.5, v - 0.5) * 2;
      const a = U.smoothstep(1.0, 0.0, d + (F(u * 6, v * 6, 2, 371, 0) - 0.5) * 0.5);
      o[0] = 10; o[1] = 9; o[2] = 8; o[3] = Math.pow(a, 1.5) * 255;
    });
    const t = toTex(c, true);
    t.wrapS = t.wrapT = THREE.ClampToEdgeWrapping;
    return { map: t, canvas: c };
  };

  // Soft round sprite for particles, glows and light shafts.
  G.glow = () => {
    const c = makeCanvas(128, 128);
    const ctx = c.getContext('2d');
    const g = ctx.createRadialGradient(64, 64, 0, 64, 64, 64);
    g.addColorStop(0, 'rgba(255,255,255,1)');
    g.addColorStop(0.25, 'rgba(255,255,255,0.55)');
    g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 128, 128);
    const t = new THREE.CanvasTexture(c);
    return { map: t, canvas: c };
  };

  // Muzzle flash star.
  G.flash = () => {
    const c = makeCanvas(128, 128);
    const ctx = c.getContext('2d');
    ctx.translate(64, 64);
    for (let i = 0; i < 7; i++) {
      ctx.rotate((Math.PI * 2) / 7 + (i % 2) * 0.2);
      const g = ctx.createLinearGradient(0, 0, 60, 0);
      g.addColorStop(0, 'rgba(255,240,200,1)');
      g.addColorStop(1, 'rgba(255,140,40,0)');
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.moveTo(0, -6);
      ctx.lineTo(58 - (i % 3) * 12, 0);
      ctx.lineTo(0, 6);
      ctx.fill();
    }
    const rg = ctx.createRadialGradient(0, 0, 0, 0, 0, 26);
    rg.addColorStop(0, 'rgba(255,255,240,1)');
    rg.addColorStop(1, 'rgba(255,180,80,0)');
    ctx.fillStyle = rg;
    ctx.fillRect(-30, -30, 60, 60);
    const t = new THREE.CanvasTexture(c);
    return { map: t, canvas: c };
  };

  // Gauge dial face for the generator.
  G.gauge = () => {
    const S = 128;
    const c = makeCanvas(S, S);
    const ctx = c.getContext('2d');
    ctx.fillStyle = '#d9d4c3';
    ctx.beginPath();
    ctx.arc(64, 64, 62, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#222';
    ctx.lineWidth = 3;
    ctx.stroke();
    ctx.lineWidth = 6;
    ctx.strokeStyle = '#2e8b3a';
    ctx.beginPath();
    ctx.arc(64, 64, 48, Math.PI * 0.75, Math.PI * 1.6);
    ctx.stroke();
    ctx.strokeStyle = '#d9a521';
    ctx.beginPath();
    ctx.arc(64, 64, 48, Math.PI * 1.6, Math.PI * 1.95);
    ctx.stroke();
    ctx.strokeStyle = '#b3241b';
    ctx.beginPath();
    ctx.arc(64, 64, 48, Math.PI * 1.95, Math.PI * 2.25);
    ctx.stroke();
    ctx.strokeStyle = '#111';
    ctx.lineWidth = 2;
    for (let i = 0; i <= 10; i++) {
      const a = Math.PI * 0.75 + (i / 10) * Math.PI * 1.5;
      ctx.beginPath();
      ctx.moveTo(64 + Math.cos(a) * 40, 64 + Math.sin(a) * 40);
      ctx.lineTo(64 + Math.cos(a) * 54, 64 + Math.sin(a) * 54);
      ctx.stroke();
    }
    ctx.fillStyle = '#111';
    ctx.font = 'bold 14px Arial, sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('kV', 64, 92);
    const t = new THREE.CanvasTexture(c);
    t.encoding = THREE.sRGBEncoding;
    return { map: t, canvas: c };
  };

  // ---------------------------------------------------------------------------
  // Decals / signage (alpha canvas textures)
  // ---------------------------------------------------------------------------
  function decal(key, opts) {
    if (cache['decal:' + key]) return cache['decal:' + key];
    const w = opts.w || 512, h = opts.h || 256;
    const c = makeCanvas(w, h);
    // (reduced to 256 px wide after drawing, see end of decal())
    const ctx = c.getContext('2d');
    const r = U.rng(key.length * 977 + w);
    if (opts.bg) {
      ctx.fillStyle = opts.bg;
      ctx.fillRect(0, 0, w, h);
    }
    if (opts.border) {
      ctx.strokeStyle = opts.border;
      ctx.lineWidth = Math.max(6, w * 0.025);
      ctx.strokeRect(ctx.lineWidth / 2, ctx.lineWidth / 2, w - ctx.lineWidth, h - ctx.lineWidth);
    }
    if (opts.header) {
      ctx.fillStyle = opts.headerBg || '#b3241b';
      ctx.fillRect(0, 0, w, h * 0.34);
      ctx.fillStyle = opts.headerColor || '#fff';
      ctx.font = 'bold ' + Math.floor(h * 0.22) + 'px Impact, Arial Black, Arial, sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(opts.header, w / 2, h * 0.18);
    }
    if (opts.icon === 'bolt') {
      ctx.fillStyle = '#111';
      const cx = w * 0.16, cy = h * 0.67, s = h * 0.26;
      ctx.beginPath();
      ctx.moveTo(cx - s * 0.9, cy + s);
      ctx.lineTo(cx, cy - s);
      ctx.lineTo(cx + s * 0.9, cy + s);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = '#e8b52a';
      ctx.beginPath();
      ctx.moveTo(cx - s * 0.7, cy + s * 0.82);
      ctx.lineTo(cx, cy - s * 0.72);
      ctx.lineTo(cx + s * 0.7, cy + s * 0.82);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = '#111';
      ctx.beginPath();
      ctx.moveTo(cx + 3, cy - s * 0.45);
      ctx.lineTo(cx - 8, cy + 6);
      ctx.lineTo(cx + 2, cy + 6);
      ctx.lineTo(cx - 4, cy + s * 0.65);
      ctx.lineTo(cx + 9, cy - 4);
      ctx.lineTo(cx - 1, cy - 4);
      ctx.closePath();
      ctx.fill();
    }
    if (opts.icon === 'bio') {
      const cx = w * 0.16, cy = h * 0.67, s = h * 0.22;
      ctx.strokeStyle = '#111';
      ctx.lineWidth = s * 0.22;
      for (let i = 0; i < 3; i++) {
        const a = -Math.PI / 2 + (i * Math.PI * 2) / 3;
        ctx.beginPath();
        ctx.arc(cx + Math.cos(a) * s * 0.55, cy + Math.sin(a) * s * 0.55, s * 0.5, 0, Math.PI * 2);
        ctx.stroke();
      }
      ctx.beginPath();
      ctx.arc(cx, cy, s * 0.62, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (opts.text) {
      ctx.fillStyle = opts.color || '#111';
      const lines = opts.text.split('\n');
      const fs = opts.fontSize || Math.floor(h * (opts.header ? 0.17 : 0.5) / Math.max(1, lines.length * 0.75));
      ctx.font = (opts.fontWeight || 'bold') + ' ' + fs + 'px ' + (opts.font || 'Arial Black, Arial, sans-serif');
      ctx.textAlign = opts.align || 'center';
      ctx.textBaseline = 'middle';
      const x0 = opts.icon ? w * 0.6 : w / 2;
      const top = opts.header ? h * 0.36 : 0;
      const area = h - top;
      lines.forEach((ln, i) => {
        ctx.fillText(ln, x0, top + area * ((i + 0.5) / lines.length));
      });
    }
    if (opts.arrow) {
      ctx.fillStyle = opts.color || '#d9a521';
      ctx.save();
      ctx.translate(w * (opts.arrowX || 0.85), h / 2);
      if (opts.arrow === 'left') ctx.rotate(Math.PI);
      ctx.beginPath();
      ctx.moveTo(-h * 0.3, -h * 0.12);
      ctx.lineTo(0, -h * 0.12);
      ctx.lineTo(0, -h * 0.28);
      ctx.lineTo(h * 0.3, 0);
      ctx.lineTo(0, h * 0.28);
      ctx.lineTo(0, h * 0.12);
      ctx.lineTo(-h * 0.3, h * 0.12);
      ctx.closePath();
      ctx.fill();
      ctx.restore();
    }
    // Wear: erase speckles and add grime so signs look used.
    if (opts.wear !== 0) {
      const amt = opts.wear || 1;
      ctx.globalCompositeOperation = opts.bg ? 'source-atop' : 'destination-out';
      for (let i = 0; i < 420 * amt; i++) {
        const x = r() * w, y = r() * h, rr = 0.5 + r() * 2.2;
        ctx.fillStyle = opts.bg ? 'rgba(40,35,28,' + (0.15 + r() * 0.35) + ')' : 'rgba(0,0,0,' + (0.3 + r() * 0.6) + ')';
        ctx.beginPath();
        ctx.arc(x, y, rr, 0, Math.PI * 2);
        ctx.fill();
      }
      if (opts.bg) {
        const g = ctx.createLinearGradient(0, 0, 0, h);
        g.addColorStop(0, 'rgba(0,0,0,0)');
        g.addColorStop(1, 'rgba(40,30,20,0.35)');
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, w, h);
      }
      ctx.globalCompositeOperation = 'source-over';
    }
    const small = w > 256 && !opts.keepSize ? (() => { const o = makeCanvas(256, Math.round((h * 256) / w)); const x = o.getContext('2d'); x.imageSmoothingQuality = 'high'; x.drawImage(c, 0, 0, o.width, o.height); return o; })() : c;
    const t = new THREE.CanvasTexture(small);
    t.encoding = THREE.sRGBEncoding;
    t.anisotropy = maxAniso;
    cache['decal:' + key] = t;
    return t;
  }

  BI.Tex = {
    init(renderer) {
      maxAniso = Math.min(8, renderer.capabilities.getMaxAnisotropy());
    },
    get(name) {
      if (!cache[name]) {
        if (!G[name]) throw new Error('Unknown texture ' + name);
        const res = G[name]();
        // keep the source canvas reachable but out of {...spread} material params
        if (res.canvas) Object.defineProperty(res, 'canvas', { value: res.canvas, enumerable: false });
        cache[name] = res;
      }
      return cache[name];
    },
    // Clone a texture set with its own repeat (textures share the canvas).
    repeat(name, rx, ry) {
      const base = BI.Tex.get(name);
      const out = {};
      for (const k of ['map', 'normalMap', 'roughnessMap']) {
        if (!base[k]) continue;
        const t = base[k].clone();
        t.needsUpdate = true;
        t.repeat.set(rx, ry === undefined ? rx : ry);
        out[k] = t;
      }
      return out;
    },
    decal,
    names: () => Object.keys(G),
    makeCanvas,
    paint,
  };
})();
