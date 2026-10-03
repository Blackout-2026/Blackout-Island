/* =============================================================================
 * world/forest.js — instanced forest: pines, palms, bushes, grass, rocks, logs
 * -----------------------------------------------------------------------------
 * Everything here is an InstancedMesh (one draw call per type). Placement is
 * deterministic (seeded RNG): denser close to the path, noise-driven
 * clearings, palms on the low ground near the beach, sparse pines on the hill
 * above the bunker. Tree trunks and big rocks register cylinder colliders.
 *
 * Wind: `windify()` patches the material's vertex shader so foliage sways with
 * height^2, phase-shifted by the instance position. The same patch is applied
 * to a custom depth material so shadows sway with the leaves.
 * Role: World Builder.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const wind = { value: 0 };

  // amp / heightRef are per-material uniforms, so every foliage type shares one
  // shader program (fewer programs = faster first load).
  function windify(mat, amp, heightRef, key) {
    const uAmp = { value: amp }, uRef = { value: heightRef };
    mat.onBeforeCompile = (sh) => {
      sh.uniforms.uWind = wind;
      sh.uniforms.uWindAmp = uAmp;
      sh.uniforms.uWindRef = uRef;
      sh.vertexShader = sh.vertexShader
        .replace('#include <common>', '#include <common>\nuniform float uWind; uniform float uWindAmp; uniform float uWindRef;')
        .replace(
          '#include <begin_vertex>',
          [
            '#include <begin_vertex>',
            '#ifdef USE_INSTANCING',
            '  vec2 ip = vec2(instanceMatrix[3][0], instanceMatrix[3][2]);',
            '#else',
            '  vec2 ip = vec2(0.0);',
            '#endif',
            '  float hg = max(position.y, 0.0) / uWindRef;',
            '  float ph = uWind * 1.25 + ip.x * 0.13 + ip.y * 0.09;',
            '  float sw = (sin(ph) * 0.65 + sin(ph * 2.37 + 1.7) * 0.25 + sin(ph * 5.1 + position.x) * 0.1) * hg * hg * uWindAmp;',
            '  transformed.x += sw;',
            '  transformed.z += sw * 0.55 + cos(ph * 1.7) * hg * hg * uWindAmp * 0.3;',
          ].join('\n')
        );
    };
    mat.customProgramCacheKey = () => (key.indexOf('depth') >= 0 ? 'wind-depth' : 'wind');
    return mat;
  }

  function depthFor(mat, amp, heightRef, key, alphaMap) {
    const d = new THREE.MeshDepthMaterial({ depthPacking: THREE.RGBADepthPacking });
    if (alphaMap) {
      d.map = alphaMap;
      d.alphaTest = 0.5;
    }
    return windify(d, amp, heightRef, key + '-depth');
  }

  // ---------------------------------------------------------------------------
  // Geometry builders
  // ---------------------------------------------------------------------------
  function pineFoliage(seed) {
    const r = U.rng(seed);
    const parts = [];
    const tiers = 6;
    for (let i = 0; i < tiers; i++) {
      const t = i / (tiers - 1);
      const h = 2.6 - t * 1.2;
      const rad = 2.4 - t * 1.85 + r() * 0.2;
      const y0 = 2.2 + t * 6.2;
      const g = new THREE.ConeGeometry(rad, h, 11, 1, true);
      const pos = g.attributes.position;
      const nrm = g.attributes.normal;
      for (let k = 0; k < pos.count; k++) {
        let x = pos.getX(k), y = pos.getY(k), z = pos.getZ(k);
        const rr = Math.hypot(x, z);
        if (rr > 0.01) {
          const a = Math.atan2(z, x);
          const jag = 1 + (U.noise2(Math.cos(a) * 2.2 + i * 7, Math.sin(a) * 2.2, seed) - 0.5) * 0.7;
          x *= jag;
          z *= jag;
          y -= Math.pow(rr / rad, 2) * 0.55;
        }
        pos.setXYZ(k, x, y, z);
        // soft "volumetric" normals: outward from the trunk and up
        const nx = x, ny = rad * 0.55 + (y + h / 2) * 0.25, nz = z;
        const l = Math.hypot(nx, ny, nz) || 1;
        nrm.setXYZ(k, nx / l, ny / l, nz / l);
      }
      g.rotateY(r() * Math.PI * 2);
      g.translate((r() - 0.5) * 0.2, y0 + h / 2, (r() - 0.5) * 0.2);
      parts.push(g);
    }
    return BI.Geo.merge(parts);
  }

  function palmGeometry(seed) {
    const r = U.rng(seed);
    const trunkParts = [];
    const frondParts = [];
    const H = 7.5, segs = 11;
    const bend = 1.6 + r() * 0.8;
    const curve = (t) => new THREE.Vector3(Math.pow(t, 1.8) * bend, t * H, 0);
    for (let i = 0; i < segs; i++) {
      const t0 = i / segs, t1 = (i + 1) / segs;
      const a = curve(t0), b = curve(t1);
      const len = a.distanceTo(b);
      const rad = 0.26 - t0 * 0.09;
      const g = new THREE.CylinderGeometry(rad * 0.86, rad, len * 1.02, 8, 1, true);
      const dir = b.clone().sub(a).normalize();
      const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
      const m = new THREE.Matrix4().compose(a.clone().add(b).multiplyScalar(0.5), q, new THREE.Vector3(1, 1, 1));
      // ring UVs so each segment shows one bark ring
      const uv = g.attributes.uv;
      for (let k = 0; k < uv.count; k++) uv.setY(k, uv.getY(k) * 0.1 + i * 0.1);
      g.applyMatrix4(m);
      trunkParts.push(g);
    }
    const top = curve(1);
    const fronds = 9;
    for (let f = 0; f < fronds; f++) {
      const ang = (f / fronds) * Math.PI * 2 + r() * 0.3;
      const len = 3.4 + r() * 1.0;
      const lift = 0.5 + r() * 0.5;
      const N = 14;
      const verts = [], uvs = [], idx = [];
      for (let s = 0; s <= N; s++) {
        const t = s / N;
        // rib: rises then droops
        const along = t * len;
        const y = Math.sin(t * Math.PI * 0.8) * lift - t * t * 1.9;
        const w = 0.62 * Math.pow(Math.sin(Math.PI * Math.min(1, t * 1.05)), 0.7) * (s % 2 ? 0.75 : 1.0);
        const fold = w * 0.35;
        verts.push(along, y, 0); // rib
        verts.push(along, y - fold, -w);
        verts.push(along, y - fold, w);
        uvs.push(0.5, t, 0, t, 1, t);
        if (s < N) {
          const b0 = s * 3, b1 = (s + 1) * 3;
          idx.push(b0, b1, b0 + 1, b1, b1 + 1, b0 + 1); // left half
          idx.push(b0, b0 + 2, b1, b1, b0 + 2, b1 + 2); // right half
        }
      }
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.Float32BufferAttribute(verts, 3));
      g.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
      g.setIndex(idx);
      g.computeVertexNormals();
      g.rotateZ(-0.15 + r() * 0.2);
      g.rotateY(ang);
      g.translate(top.x, top.y - 0.1, top.z);
      frondParts.push(g);
    }
    // coconuts
    for (let c = 0; c < 4; c++) {
      const g = new THREE.SphereGeometry(0.16, 6, 5);
      const a = (c / 4) * Math.PI * 2;
      g.translate(top.x + Math.cos(a) * 0.25, top.y - 0.35, top.z + Math.sin(a) * 0.25);
      trunkParts.push(g);
    }
    return { trunk: BI.Geo.merge(trunkParts), fronds: BI.Geo.merge(frondParts) };
  }

  function lumpGeometry(seed, detail, rough, squash) {
    const g = new THREE.IcosahedronGeometry(1, detail);
    const pos = g.attributes.position;
    // weld-consistent displacement: depends on the vertex direction only
    for (let k = 0; k < pos.count; k++) {
      const x = pos.getX(k), y = pos.getY(k), z = pos.getZ(k);
      const n = U.noise2(x * 1.7 + z * 0.9 + seed, y * 1.9 - z * 0.7, seed) * 0.7 + U.noise2(x * 4.1 + seed, y * 3.7 + z * 2.3, seed + 1) * 0.3;
      const s = 1 + (n - 0.5) * rough;
      pos.setXYZ(k, x * s, y * s * squash, z * s);
    }
    g.computeVertexNormals();
    return g;
  }

  function bushGeometry(seed) {
    const r = U.rng(seed);
    const parts = [];
    for (let i = 0; i < 2; i++) {
      const g = lumpGeometry(seed + i * 3, 1, 0.6, 0.75);
      const s = 0.55 + r() * 0.35;
      g.scale(s, s, s);
      g.translate((r() - 0.5) * 0.9, 0.35 + s * 0.4, (r() - 0.5) * 0.9);
      parts.push(g);
    }
    // soft normals pointing away from the bush centre
    const m = BI.Geo.merge(parts);
    const p = m.attributes.position, n = m.attributes.normal;
    for (let k = 0; k < p.count; k++) {
      const x = p.getX(k), y = p.getY(k) - 0.2, z = p.getZ(k);
      const l = Math.hypot(x, y, z) || 1;
      n.setXYZ(k, (n.getX(k) + x / l) * 0.5, (n.getY(k) + y / l) * 0.5 + 0.2, (n.getZ(k) + z / l) * 0.5);
    }
    m.computeBoundingSphere();
    return m;
  }

  function grassTuft() {
    const parts = [];
    for (let i = 0; i < 3; i++) {
      const g = new THREE.PlaneGeometry(1.0, 0.75);
      g.translate(0, 0.37, 0);
      g.rotateY((i * Math.PI) / 3);
      parts.push(g);
    }
    const m = BI.Geo.merge(parts);
    // normals up so tufts shade like the ground under them
    const n = m.attributes.normal;
    for (let k = 0; k < n.count; k++) n.setXYZ(k, 0, 1, 0);
    return m;
  }

  // ---------------------------------------------------------------------------
  const Forest = {
    trees: [],
    bushes: [],
    rocks: [],
    grass: [],
    logs: [],
    meshes: {},
    wind,

    // Deterministic placement (needs only the baked terrain).
    place() {
      const T = BI.Terrain;
      const r = U.rng(1234);
      const L = BI.Level;
      const trees = [];
      const blocked = (x, z) => {
        if (x > -27 && x < 27 && z > -67 && z < -23) return true; // compound + margin
        const cz = T.cliffZ(x);
        if (z < cz + 3 && z > cz - 5) return true; // cliff face
        if (Math.abs(x - L.dock.x) < 4 && z > 88) return true; // dock approach
        return false;
      };
      const STEP = 4.6;
      for (let gx = -118; gx < 118; gx += STEP) {
        for (let gz = -175; gz < 100; gz += STEP) {
          const x = gx + r() * STEP * 0.9, z = gz + r() * STEP * 0.9;
          const h = T.height(x, z);
          const pd = T.pathDistance(x, z);
          const rnd = r(), rnd2 = r(), rnd3 = r();
          if (h < 1.0 || pd < 3.4 || blocked(x, z)) continue;
          const n = T.normal(x, z);
          if (n.y < 0.8) continue;
          let density = 0.2 + 0.8 * U.smoothstep(85, 18, pd);
          if (z < T.cliffZ(x)) density *= 0.5;
          if (U.fbm2(x * 0.035, z * 0.035, 2, 77) < 0.4) density *= 0.3; // clearings
          if (rnd > density * 0.85) continue;
          const lowBeach = h < 3.2 && z > 60;
          if (lowBeach && rnd2 > 0.55) continue;
          const palm = lowBeach || (z > 35 && h < 5.5 && rnd3 < 0.18);
          trees.push({ x, z, y: h, type: palm ? 'palm' : 'pine', s: 0.78 + r() * 0.5, rot: r() * Math.PI * 2, v: r() < 0.5 ? 0 : 1 });
        }
      }
      this.trees = trees;

      // bushes, grass, rocks
      const rb = U.rng(4321);
      for (let i = 0; i < 2600 && this.bushes.length < 420; i++) {
        const x = rb.range(-90, 90), z = rb.range(-60, 95);
        const pd = T.pathDistance(x, z);
        if (pd < 3.8 || pd > 45 || blocked(x, z)) continue;
        const h = T.height(x, z);
        if (h < 1.6) continue;
        this.bushes.push({ x, z, y: h, s: rb.range(0.6, 1.4), rot: rb() * 6.28, v: rb() < 0.5 ? 0 : 1 });
      }
      const rg = U.rng(999);
      for (let i = 0; i < 30000 && this.grass.length < 6000; i++) {
        const x = rg.range(-80, 80), z = rg.range(-62, 98);
        const pd = T.pathDistance(x, z);
        if (pd < 1.4 || pd > 38 || blocked(x, z)) continue;
        const h = T.height(x, z);
        if (h < 1.3) continue;
        // grass clumps where the noise says so (and lining the path)
        const clump = U.fbm2(x * 0.08, z * 0.08, 2, 55);
        if (clump < 0.45 && pd > 4) continue;
        this.grass.push({ x, z, y: h, s: rg.range(0.6, 1.25), rot: rg() * 6.28 });
      }
      // shuffle grass so lowering the instance count thins uniformly
      for (let i = this.grass.length - 1; i > 0; i--) {
        const j = Math.floor(rg() * (i + 1));
        const t = this.grass[i];
        this.grass[i] = this.grass[j];
        this.grass[j] = t;
      }
      const rr = U.rng(777);
      for (let i = 0; i < 1500 && this.rocks.length < 170; i++) {
        const x = rr.range(-110, 110), z = rr.range(-120, 104);
        const h = T.height(x, z);
        if (h < -0.5) continue;
        const cz = T.cliffZ(x);
        const nearCliff = Math.abs(z - cz) < 6;
        const pd = T.pathDistance(x, z);
        if (pd < 3 || (x > -24 && x < 24 && z > -63 && z < -26)) continue;
        if (!nearCliff && rr() < 0.55) continue;
        const s = nearCliff ? rr.range(1.6, 4.2) : rr.range(0.3, 1.5);
        this.rocks.push({ x, z, y: h - s * 0.25, s, rot: rr() * 6.28, v: rr.int(0, 2) });
      }
      // fallen logs
      const rl = U.rng(55);
      for (let i = 0; i < 300 && this.logs.length < 12; i++) {
        const x = rl.range(-50, 50), z = rl.range(-20, 85);
        const pd = T.pathDistance(x, z);
        if (pd < 4 || pd > 25 || blocked(x, z)) continue;
        this.logs.push({ x, z, y: T.height(x, z) + 0.28, rot: rl() * 6.28, len: rl.range(3, 6) });
      }
      // litter circles for the terrain info mask
      return trees.map((t) => [t.x, t.z, 3.2 * t.s]).concat(this.bushes.map((b) => [b.x, b.z, 1.6 * b.s]));
    },

    build(scene) {
      const P = BI.Physics;
      const dummy = new THREE.Object3D();
      const col = new THREE.Color();
      const make = (geo, mat, list, setup, opts) => {
        const mesh = new THREE.InstancedMesh(geo, mat, Math.max(1, list.length));
        list.forEach((it, i) => {
          setup(dummy, it, i);
          dummy.updateMatrix();
          mesh.setMatrixAt(i, dummy.matrix);
          if (opts && opts.color) {
            opts.color(col, it);
            mesh.setColorAt(i, col);
          }
        });
        mesh.count = list.length;
        mesh.instanceMatrix.needsUpdate = true;
        if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
        mesh.castShadow = !opts || opts.cast !== false;
        mesh.receiveShadow = true;
        mesh.frustumCulled = false; // instances span the island
        scene.add(mesh);
        return mesh;
      };

      // ---- pines ----
      const pines = this.trees.filter((t) => t.type === 'pine');
      const palms = this.trees.filter((t) => t.type === 'palm');
      const trunkGeo = new THREE.CylinderGeometry(0.17, 0.34, 9.6, 7, 3);
      trunkGeo.translate(0, 4.8, 0);
      const barkMat = BI.Mat.get('barkPine');
      const needleBase = BI.Mat.get('needles');
      const needleMat = windify(needleBase.clone(), 0.32, 9.5, 'pine');
      needleMat.side = THREE.DoubleSide;
      needleMat.vertexColors = false;
      const needleDepth = depthFor(needleMat, 0.32, 9.5, 'pine');
      const setTree = (o, t) => {
        o.position.set(t.x, t.y - 0.15, t.z);
        o.rotation.set(0, t.rot, 0);
        o.scale.setScalar(t.s);
      };
      this.meshes.pineTrunk = make(trunkGeo, barkMat, pines, setTree);
      for (let v = 0; v < 2; v++) {
        const list = pines.filter((t) => t.v === v);
        const m = make(pineFoliage(100 + v * 31), needleMat, list, setTree, {
          color: (c, t) => c.setHSL(0.26 + (t.rot % 0.06), 0.3 + (t.s % 0.1), 0.6 + ((t.x * 7.3) % 0.1)),
        });
        m.customDepthMaterial = needleDepth;
        this.meshes['pine' + v] = m;
      }

      // ---- palms ----
      const palmG = palmGeometry(9);
      const palmBark = BI.Mat.get('barkPalm');
      const frondMat = windify(BI.Mat.get('leaf').clone(), 0.45, 8.0, 'palm');
      const frondDepth = depthFor(frondMat, 0.45, 8.0, 'palm');
      this.meshes.palmTrunk = make(palmG.trunk, palmBark, palms, setTree);
      const fr = make(palmG.fronds, frondMat, palms, setTree, { color: (c, t) => c.setHSL(0.21 + (t.rot % 0.05), 0.4, 0.64) });
      fr.customDepthMaterial = frondDepth;
      this.meshes.palmFronds = fr;

      // tree colliders
      for (const t of pines) P.addCyl(t.x, t.z, 0.3 * t.s, t.y - 1, t.y + 9, { surface: 'wood', wall: false, tag: 'tree' });
      for (const t of palms) P.addCyl(t.x, t.z, 0.25 * t.s, t.y - 1, t.y + 7, { surface: 'wood', wall: false, tag: 'tree' });

      // ---- bushes ----
      const bushMat = windify(BI.Mat.get('leaf').clone(), 0.08, 1.4, 'bush');
      bushMat.color.setHex(0x9fae86);
      for (let v = 0; v < 2; v++) {
        const list = this.bushes.filter((b) => b.v === v);
        this.meshes['bush' + v] = make(bushGeometry(200 + v * 17), bushMat, list, (o, b) => {
          o.position.set(b.x, b.y - 0.1, b.z);
          o.rotation.set(0, b.rot, 0);
          o.scale.set(b.s, b.s * 0.85, b.s);
        }, { cast: false, color: (c, b) => c.setHSL(0.24 + (b.rot % 0.08), 0.35, 0.55 + (b.s % 0.1)) });
      }

      for (const b of this.bushes) P.addCyl(b.x, b.z, 0.75 * b.s, b.y - 0.5, b.y + 1.1 * b.s, { surface: 'foliage', wall: false, sight: false, bullets: false, tag: 'bush' });

      // ---- grass (instance count follows quality) ----
      const grassMat = windify(BI.Mat.get('grassBlades').clone(), 0.14, 0.75, 'grass');
      this.meshes.grass = make(grassTuft(), grassMat, this.grass, (o, g) => {
        o.position.set(g.x, g.y - 0.05, g.z);
        o.rotation.set(0, g.rot, 0);
        o.scale.setScalar(g.s);
      }, { cast: false, color: (c, g) => c.setHSL(0.22 + (g.rot % 0.05), 0.35, 0.5 + (g.s % 0.15)) });

      // ---- rocks ----
      const rockMat = BI.Mat.get('rock');
      for (let v = 0; v < 3; v++) {
        const list = this.rocks.filter((rk) => rk.v === v);
        this.meshes['rock' + v] = make(lumpGeometry(300 + v * 13, 2, 0.75, 0.62), rockMat, list, (o, rk) => {
          o.position.set(rk.x, rk.y, rk.z);
          o.rotation.set(0, rk.rot, 0);
          o.scale.set(rk.s, rk.s, rk.s * 0.85);
        });
      }
      for (const rk of this.rocks) {
        if (rk.s > 0.7) P.addCyl(rk.x, rk.z, rk.s * 0.8, rk.y - 1, rk.y + rk.s * 0.6, { surface: 'rock', wall: false, tag: 'rock' });
      }

      // ---- logs ----
      const logGeo = new THREE.CylinderGeometry(0.28, 0.34, 1, 8);
      logGeo.rotateZ(Math.PI / 2);
      this.meshes.logs = make(logGeo, barkMat, this.logs, (o, l) => {
        o.position.set(l.x, l.y, l.z);
        o.rotation.set(0, l.rot, 0);
        o.scale.set(l.len, 1, 1);
      });
      for (const l of this.logs) {
        for (let k = -1; k <= 1; k++) {
          const ox = Math.cos(l.rot) * k * l.len * 0.33, oz = -Math.sin(l.rot) * k * l.len * 0.33;
          P.addCyl(l.x + ox, l.z + oz, 0.32, l.y - 0.4, l.y + 0.3, { surface: 'wood', wall: false, nav: true, tag: 'log' });
        }
      }

      this.applyQuality(BI.Engine.settings);
      BI.Events.on('quality:changed', (e) => this.applyQuality(e.settings));
      BI.Engine.addUpdate((dt, t) => { wind.value = t; }, 91);
    },

    applyQuality(s) {
      if (this.meshes.grass) this.meshes.grass.count = Math.floor(this.grass.length * s.grass);
    },

    setVisible(v) {
      for (const k of Object.keys(this.meshes)) this.meshes[k].visible = v;
    },
  };

  BI.Forest = Forest;
})();
