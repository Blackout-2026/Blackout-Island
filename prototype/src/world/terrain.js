/* =============================================================================
 * world/terrain.js — island heightmap, terrain mesh and splat shader
 * -----------------------------------------------------------------------------
 * Height comes from an analytic function (island ellipse + fbm noise + route
 * profile + compound plateau + cliff/hill over the bunker), baked once into a
 * 2 m grid. `height(x,z)` interpolates the grid with exactly the same triangle
 * split that THREE.PlaneGeometry uses, so feet sit on the rendered surface.
 *
 * The terrain material is MeshStandardMaterial patched with onBeforeCompile:
 *   - 5 layers (sand, grass, forest litter, dirt path, rock) blended by
 *     height, slope and a painted "info" mask (path / gravel / litter)
 *   - per-pixel detail normal, wet sand near the waterline (low roughness)
 *   - a rectangular hole where the entrance building's ramp goes underground
 * Role: World Builder.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const L = BI.Level;

  const SIZE = L.TERRAIN_SIZE; // 400 m
  const SEG = 200; // 2 m cells
  const STEP = SIZE / SEG;
  const N = SEG + 1;
  const X0 = -SIZE / 2;
  const Z0 = L.TERRAIN_CENTER_Z - SIZE / 2;

  const T = {
    SIZE, SEG, STEP, X0, Z0,
    heights: null,
    path: null, // polyline [[x,z],...]
    hole: { minX: L.entrance.minX + 0.05, maxX: L.entrance.maxX - 0.05, minZ: L.entrance.minZ + 0.05, maxZ: L.entrance.maxZ - 0.05 },
    infoCanvas: null,
    infoData: null,
    INFO: 1024,
    mesh: null,
    material: null,
  };

  // ---------------------------------------------------------------------------
  // Path distance (coarse acceleration: segments bucketed by z band)
  // ---------------------------------------------------------------------------
  let segs = [];
  function buildPath() {
    T.path = U.catmullRom2D(L.pathCtrl, 10);
    segs = [];
    for (let i = 0; i < T.path.length - 1; i++) segs.push([T.path[i][0], T.path[i][1], T.path[i + 1][0], T.path[i + 1][1]]);
    buildPathField();
  }
  // Distance to the path, from a precomputed 2 m field (fast) when available.
  let pdField = null;
  const PD_MAX = 95;
  function buildPathField() {
    pdField = new Float32Array(N * N).fill(PD_MAX);
    for (const s of segs) {
      const x0 = Math.max(0, Math.floor((Math.min(s[0], s[2]) - PD_MAX - X0) / STEP));
      const x1 = Math.min(SEG, Math.ceil((Math.max(s[0], s[2]) + PD_MAX - X0) / STEP));
      const z0 = Math.max(0, Math.floor((Math.min(s[1], s[3]) - PD_MAX - Z0) / STEP));
      const z1 = Math.min(SEG, Math.ceil((Math.max(s[1], s[3]) + PD_MAX - Z0) / STEP));
      for (let iz = z0; iz <= z1; iz++) {
        const z = Z0 + iz * STEP;
        for (let ix = x0; ix <= x1; ix++) {
          const d = U.distToSegment2D(X0 + ix * STEP, z, s[0], s[1], s[2], s[3]);
          const k = iz * N + ix;
          if (d < pdField[k]) pdField[k] = d;
        }
      }
    }
  }
  T.pathDistance = function (x, z) {
    if (pdField) {
      const gx = (x - X0) / STEP, gz = (z - Z0) / STEP;
      const ix = Math.floor(gx), iz = Math.floor(gz);
      if (ix >= 0 && iz >= 0 && ix < SEG && iz < SEG) {
        const fx = gx - ix, fz = gz - iz;
        const a = pdField[iz * N + ix], b = pdField[iz * N + ix + 1], c = pdField[(iz + 1) * N + ix], d = pdField[(iz + 1) * N + ix + 1];
        const v = (a + (b - a) * fx) * (1 - fz) + (c + (d - c) * fx) * fz;
        // near the path use the exact distance for crisp edges
        if (v > 6) return v;
      }
    }
    return exactPathDistance(x, z);
  };
  function exactPathDistance(x, z) {
    let best = 1e9;
    for (let i = 0; i < segs.length; i++) {
      const s = segs[i];
      // quick reject by bounding box
      if (z < Math.min(s[1], s[3]) - best || z > Math.max(s[1], s[3]) + best) continue;
      const d = U.distToSegment2D(x, z, s[0], s[1], s[2], s[3]);
      if (d < best) best = d;
    }
    return best;
  };

  // ---------------------------------------------------------------------------
  // Analytic height
  // ---------------------------------------------------------------------------
  // Wavy cliff line north of the compound; straight behind the compound wall.
  T.cliffZ = function (x) {
    const ax = Math.abs(x);
    if (ax <= 24) return -62;
    const w = U.smoothstep(24, 40, ax);
    return -62 + w * (Math.sin(x * 0.09 + 1.3) * 5 + (U.noise2(x * 0.05, 3.3, 41) - 0.5) * 8 + 3);
  };
  // Curved shoreline of the beach bay (z where sand meets water).
  T.shoreZ = function (x) {
    return 105 - Math.pow(Math.abs(x) / 55, 2) * 12 + Math.sin(x * 0.11) * 1.2;
  };

  function rawHeight(x, z) {
    // Island ellipse distance with a noisy coastline (calmer at the beach).
    const ex = x / 118, ez = (z + 28) / 142;
    let d = Math.sqrt(ex * ex + ez * ez);
    const southness = U.smoothstep(40, 90, z);
    d += (U.fbm2(x * 0.012 + 11.3, z * 0.012 - 4.1, 3, 5) - 0.5) * 0.24 * (1 - southness * 0.85);
    const inland = 1 - d;

    // Route profile: rises from the beach to the plateau.
    const t = U.smoothstep(92, -30, z);
    let h = 2.0 + 8.0 * t;

    // Rolling forest relief, calmer near the path.
    const pd = T.pathDistance(x, z);
    const n1 = U.fbm2(x * 0.022 + 3.7, z * 0.022 + 1.3, 4, 21);
    const n2 = U.fbm2(x * 0.08, z * 0.08, 2, 23);
    const relief = ((n1 - 0.5) * 9 + (n2 - 0.5) * 1.4) * U.smoothstep(3, 14, pd) * U.smoothstep(92, 74, z);
    const valley = U.smoothstep(5, 35, pd) * 2.2 * U.smoothstep(92, 76, z);
    h += relief + valley;

    // Cliff + hill north of the compound (hides the underground bunker).
    const cz = T.cliffZ(x);
    if (z < cz) {
      const k = U.smoothstep(cz, cz - 22, z);
      h = 14.6 + 6 * k + (U.fbm2(x * 0.04, z * 0.04, 3, 29) - 0.5) * 3 * k;
    } else if (z < cz + 22) {
      // ground rises toward the cliff foot east/west of the compound
      h = Math.max(h, U.lerp(h, 10, U.smoothstep(cz + 22, cz + 4, z)));
    }

    // Compound plateau, flattened to exactly GROUND_Y.
    const c = L.compound;
    if (z >= -62.01) {
      const cdx = Math.max(c.minX - x, 0, x - c.maxX), cdz = Math.max(0, z - c.maxZ);
      const cw = 1 - U.smoothstep(0, 16, Math.hypot(cdx, cdz));
      h = U.lerp(h, L.GROUND_Y, cw);
    }

    // Path: smooth toward the profile so it is walkable.
    const pw = 1 - U.smoothstep(1.5, 6, pd);
    if (z > -40) h = U.lerp(h, 2.0 + 8.0 * t + (n2 - 0.5) * 0.3, pw * 0.85);

    // Beach: a curved bay with a gentle slope into the sea on the south side.
    const shore = T.shoreZ(x);
    const beachH = 2.0 - (z - (shore - 17)) * 0.115;
    const ax = Math.abs(x);
    const beachW = U.smoothstep(shore - 26, shore - 15, z) * (1 - U.smoothstep(42, 72, ax)) * (1 - U.smoothstep(shore + 22, shore + 40, z));
    h = U.lerp(h, Math.min(h, beachH), beachW);

    // Coastline falloff to the sea bed elsewhere.
    const coast = U.smoothstep(0.0, 0.11, inland);
    const seabed = -5 + (U.fbm2(x * 0.03, z * 0.03, 2, 31) - 0.5) * 2;
    h = U.lerp(seabed, h, U.lerp(coast, 1, beachW));
    return Math.max(h, -7);
  }
  T.rawHeight = rawHeight;

  T.bake = function () {
    buildPath();
    const H = new Float32Array(N * N);
    for (let iz = 0; iz < N; iz++) {
      for (let ix = 0; ix < N; ix++) H[iz * N + ix] = rawHeight(X0 + ix * STEP, Z0 + iz * STEP);
    }
    T.heights = H;
  };

  // Exact triangle interpolation matching PlaneGeometry (rotated -90° on X):
  // cell corners a=(ix,iz) b=(ix,iz+1) c=(ix+1,iz+1) d=(ix+1,iz); split b-d.
  T.height = function (x, z) {
    const gx = (x - X0) / STEP, gz = (z - Z0) / STEP;
    let ix = Math.floor(gx), iz = Math.floor(gz);
    if (ix < 0 || iz < 0 || ix >= SEG || iz >= SEG) return -7;
    const fx = gx - ix, fz = gz - iz;
    const H = T.heights;
    const ha = H[iz * N + ix], hb = H[(iz + 1) * N + ix], hc = H[(iz + 1) * N + ix + 1], hd = H[iz * N + ix + 1];
    if (fx + fz <= 1) return ha + (hd - ha) * fx + (hb - ha) * fz;
    return hc + (hb - hc) * (1 - fx) + (hd - hc) * (1 - fz);
  };

  T.inHole = function (x, z) {
    const h = T.hole;
    return x > h.minX && x < h.maxX && z > h.minZ && z < h.maxZ;
  };

  T.normal = function (x, z, out) {
    const e = 0.75;
    const hx = T.height(x + e, z) - T.height(x - e, z);
    const hz = T.height(x, z + e) - T.height(x, z - e);
    out = out || new THREE.Vector3();
    return out.set(-hx, 2 * e, -hz).normalize();
  };

  // ---------------------------------------------------------------------------
  // Info mask canvas: R = dirt path, G = compound gravel, B = forest litter
  // ---------------------------------------------------------------------------
  function worldToInfo(x, z) {
    return [((x - X0) / SIZE) * T.INFO, ((z - Z0) / SIZE) * T.INFO];
  }
  T.worldToInfo = worldToInfo;

  T.buildInfo = function (treePositions) {
    const S = T.INFO;
    const c = BI.Tex.makeCanvas(S, S);
    const ctx = c.getContext('2d');
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, S, S);
    const px = S / SIZE; // pixels per metre
    ctx.globalCompositeOperation = 'lighter';

    // B: forest litter under trees
    ctx.filter = 'blur(' + Math.round(px * 2) + 'px)';
    ctx.fillStyle = 'rgba(0,0,255,0.55)';
    for (const t of treePositions || []) {
      const p = worldToInfo(t[0], t[1]);
      ctx.beginPath();
      ctx.arc(p[0], p[1], px * (t[2] || 3.5), 0, Math.PI * 2);
      ctx.fill();
    }
    // R: winding dirt path with soft, noisy edges
    ctx.filter = 'blur(' + Math.max(1, Math.round(px * 0.8)) + 'px)';
    ctx.strokeStyle = 'rgb(255,0,0)';
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.lineWidth = px * 3.4;
    ctx.beginPath();
    T.path.forEach((p, i) => {
      const q = worldToInfo(p[0], p[1]);
      if (i === 0) ctx.moveTo(q[0], q[1]);
      else ctx.lineTo(q[0], q[1]);
    });
    ctx.stroke();
    // tire tracks / side ruts
    ctx.lineWidth = px * 0.6;
    for (const off of [-1.6, 1.6]) {
      ctx.beginPath();
      T.path.forEach((p, i) => {
        const nx = i < T.path.length - 1 ? T.path[i + 1][1] - p[1] : 0, nz = i < T.path.length - 1 ? -(T.path[i + 1][0] - p[0]) : 1;
        const l = Math.hypot(nx, nz) || 1;
        const q = worldToInfo(p[0] + (nx / l) * off, p[1] + (nz / l) * off);
        if (i === 0) ctx.moveTo(q[0], q[1]);
        else ctx.lineTo(q[0], q[1]);
      });
      ctx.stroke();
    }
    // G: compound gravel
    ctx.filter = 'blur(' + Math.round(px * 1.5) + 'px)';
    ctx.fillStyle = 'rgb(0,255,0)';
    const cmp = L.compound;
    const a = worldToInfo(cmp.minX + 1, cmp.minZ), b = worldToInfo(cmp.maxX - 1, cmp.maxZ - 1);
    ctx.fillRect(a[0], a[1], b[0] - a[0], b[1] - a[1]);
    // the path continues as gravel through the gate
    const g1 = worldToInfo(-3, -32), g2 = worldToInfo(3, -24);
    ctx.fillRect(g1[0], g1[1], g2[0] - g1[0], g2[1] - g1[1]);
    ctx.filter = 'none';
    ctx.globalCompositeOperation = 'source-over';
    T.infoCanvas = c;
    T.infoData = ctx.getImageData(0, 0, S, S).data;
  };

  T.info = function (x, z) {
    if (!T.infoData) return [0, 0, 0];
    const p = worldToInfo(x, z);
    const ix = U.clamp(Math.floor(p[0]), 0, T.INFO - 1), iz = U.clamp(Math.floor(p[1]), 0, T.INFO - 1);
    const i = (iz * T.INFO + ix) * 4;
    return [T.infoData[i] / 255, T.infoData[i + 1] / 255, T.infoData[i + 2] / 255];
  };

  // Footstep / impact surface for a terrain point.
  T.surfaceAt = function (x, z) {
    const h = T.height(x, z);
    const inf = T.info(x, z);
    if (inf[1] > 0.5) return 'gravel';
    if (h < 2.3 && z > 70) return 'sand';
    if (inf[0] > 0.45) return 'dirt';
    return 'grass';
  };

  // ---------------------------------------------------------------------------
  // Mesh + material
  // ---------------------------------------------------------------------------
  T.buildMesh = function (scene) {
    const geo = new THREE.PlaneGeometry(SIZE, SIZE, SEG, SEG);
    geo.rotateX(-Math.PI / 2);
    geo.translate(0, 0, L.TERRAIN_CENTER_Z);
    const pos = geo.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const ix = i % N, iz = Math.floor(i / N);
      pos.setY(i, T.heights[iz * N + ix]);
    }
    geo.computeVertexNormals();
    geo.computeBoundingSphere();

    const tex = {
      sand: BI.Tex.get('sand'),
      grass: BI.Tex.get('grass'),
      litter: BI.Tex.get('forestFloor'),
      dirt: BI.Tex.get('dirt'),
      rock: BI.Tex.get('rock'),
      gravel: BI.Tex.get('concrete'),
    };
    const info = new THREE.CanvasTexture(T.infoCanvas);
    info.wrapS = info.wrapT = THREE.ClampToEdgeWrapping;

    const mat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.92, metalness: 0 });
    mat.name = 'terrain';
    const uniforms = {
      tSand: { value: tex.sand.map },
      tGrass: { value: tex.grass.map },
      tLitter: { value: tex.litter.map },
      tDirt: { value: tex.dirt.map },
      tRock: { value: tex.rock.map },
      tGravel: { value: tex.gravel.map },
      tRockN: { value: tex.rock.normalMap },
      tDirtN: { value: tex.dirt.normalMap },
      tSandN: { value: tex.sand.normalMap },
      tInfo: { value: info },
      infoRect: { value: new THREE.Vector4(X0, Z0, SIZE, SIZE) },
      holeRect: { value: new THREE.Vector4(T.hole.minX, T.hole.minZ, T.hole.maxX, T.hole.maxZ) },
    };
    mat.onBeforeCompile = (shader) => {
      Object.assign(shader.uniforms, uniforms);
      shader.vertexShader = shader.vertexShader
        .replace('#include <common>', '#include <common>\nvarying vec3 vWPos;\nvarying vec3 vWNrm;')
        .replace(
          '#include <worldpos_vertex>',
          '#include <worldpos_vertex>\nvWPos = (modelMatrix * vec4(transformed, 1.0)).xyz;\nvWNrm = normalize(mat3(modelMatrix) * objectNormal);'
        );
      shader.fragmentShader = shader.fragmentShader
        .replace(
          '#include <common>',
          [
            '#include <common>',
            'varying vec3 vWPos; varying vec3 vWNrm;',
            'uniform sampler2D tSand, tGrass, tLitter, tDirt, tRock, tGravel, tRockN, tDirtN, tSandN, tInfo;',
            'uniform vec4 infoRect; uniform vec4 holeRect;',
            'vec3 lin(vec3 c){ return c * c * (c * 0.3 + 0.7); }', // cheap sRGB->linear
            'float tW_rock; float tW_sand; float tWet;',
            'vec3 tWorldN;',
          ].join('\n')
        )
        .replace(
          '#include <map_fragment>',
          [
            'if (vWPos.x > holeRect.x && vWPos.x < holeRect.z && vWPos.z > holeRect.y && vWPos.z < holeRect.w) discard;',
            'vec2 wuv = vWPos.xz;',
            'vec4 inf = texture2D(tInfo, (vWPos.xz - infoRect.xy) / infoRect.zw);',
            'float slope = 1.0 - clamp(vWNrm.y, 0.0, 1.0);',
            'vec3 cSand = lin(texture2D(tSand, wuv * 0.22).rgb);',
            'vec3 cGrass = lin(texture2D(tGrass, wuv * 0.30).rgb);',
            'vec3 cLitter = lin(texture2D(tLitter, wuv * 0.28).rgb);',
            'vec3 cDirt = lin(texture2D(tDirt, wuv * 0.30).rgb);',
            'vec3 cRock = lin(texture2D(tRock, vec2(wuv.x + vWPos.y, wuv.y - vWPos.y) * 0.12).rgb);',
            'vec3 cGravel = lin(texture2D(tGravel, wuv * 0.2).rgb) * vec3(0.75, 0.74, 0.70);',
            // large-scale variation to kill tiling
            'float macro = texture2D(tLitter, wuv * 0.013).g;',
            'float litterW = clamp(inf.b * 1.6 + (macro - 0.35) * 1.2, 0.0, 1.0);',
            'vec3 col = mix(cGrass, cLitter, litterW);',
            'col *= 0.85 + macro * 0.4;',
            'float sandW = (1.0 - smoothstep(1.9, 2.8, vWPos.y)) * step(60.0, vWPos.z);',
            'sandW = max(sandW, 1.0 - smoothstep(0.4, 1.4, vWPos.y));',
            'col = mix(col, cSand, sandW);',
            'col = mix(col, cDirt, smoothstep(0.25, 0.75, inf.r) * (1.0 - sandW * 0.7));',
            'col = mix(col, cGravel, smoothstep(0.3, 0.8, inf.g));',
            'float rockW = smoothstep(0.30, 0.48, slope);',
            'col = mix(col, cRock, rockW);',
            'tWet = (1.0 - smoothstep(-0.2, 0.55, vWPos.y)) * sandW;',
            'col *= 1.0 - tWet * 0.45;',
            'tW_rock = rockW; tW_sand = sandW;',
            'diffuseColor.rgb *= col;',
          ].join('\n')
        )
        .replace(
          '#include <roughnessmap_fragment>',
          'float roughnessFactor = mix(roughness, 0.25, tWet); roughnessFactor = mix(roughnessFactor, 0.8, tW_rock);'
        )
        .replace(
          '#include <normal_fragment_maps>',
          [
            'vec3 nR = texture2D(tRockN, vec2(wuv.x + vWPos.y, wuv.y - vWPos.y) * 0.12).xyz * 2.0 - 1.0;',
            'vec3 nD = texture2D(tDirtN, wuv * 0.30).xyz * 2.0 - 1.0;',
            'vec3 nS = texture2D(tSandN, wuv * 0.22).xyz * 2.0 - 1.0;',
            'vec3 nT = mix(mix(nD, nS, tW_sand), nR, tW_rock);',
            'vec3 wn = normalize(vWNrm + vec3(nT.x, 0.0, -nT.y) * 0.55);',
            'normal = normalize((viewMatrix * vec4(wn, 0.0)).xyz);',
          ].join('\n')
        );
    };
    mat.customProgramCacheKey = () => 'terrain-v1';

    const mesh = new THREE.Mesh(geo, mat);
    mesh.receiveShadow = true;
    mesh.castShadow = false;
    mesh.name = 'terrain';
    mesh.matrixAutoUpdate = false;
    mesh.updateMatrix();
    scene.add(mesh);
    T.mesh = mesh;
    T.material = mat;
    return mesh;
  };

  // Height texture for the ocean shader (shoreline foam / discard over land).
  T.heightTexture = function () {
    const data = new Float32Array(N * N);
    for (let i = 0; i < N * N; i++) data[i] = T.heights[i];
    // Pack into RGBA8: height range -8..24 -> 0..1
    const bytes = new Uint8Array(N * N * 4);
    for (let i = 0; i < N * N; i++) {
      const v = U.clamp01((data[i] + 8) / 32);
      bytes[i * 4] = Math.floor(v * 255);
      bytes[i * 4 + 1] = Math.floor((v * 255 - Math.floor(v * 255)) * 255);
      bytes[i * 4 + 2] = 0;
      bytes[i * 4 + 3] = 255;
    }
    const t = new THREE.DataTexture(bytes, N, N, THREE.RGBAFormat);
    t.magFilter = THREE.LinearFilter;
    t.minFilter = THREE.LinearFilter;
    t.needsUpdate = true;
    return t;
  };

  BI.Terrain = T;
})();
