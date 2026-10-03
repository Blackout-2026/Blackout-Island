/* =============================================================================
 * materials/materials.js — shared MeshStandardMaterial library
 * -----------------------------------------------------------------------------
 * One place defines every material so the whole game shares one palette and
 * one roughness/metalness language (visual cohesion). Materials are cached by
 * name: BI.Mat.get('concrete').
 *
 * Environment reflections: metals look black without something to reflect, so
 * we bake three tiny procedural environments with PMREM:
 *   night      outdoor moonlit sky gradient
 *   interior   neutral bunker room with bright fluorescent strips
 *   emergency  the same room in red after the blackout
 * Materials tagged `env: 'interior'` swap to the emergency env on blackout.
 * Role: World Builder / Graphics.
 * ========================================================================== */
(function () {
  'use strict';
  const cache = {};
  const envTargets = {};
  const envUsers = { night: [], interior: [], view: [] };

  // Every MeshStandardMaterial gets the same texture slots (1x1 defaults where
  // a material has none) so they compile to a handful of shader programs
  // instead of one per combination. This cut first-load compile time a lot.
  let defaults = null;
  function getDefaults() {
    if (!defaults) {
      const tex = (r, g, b, srgb) => {
        const t = new THREE.DataTexture(new Uint8Array([r, g, b, 255]), 1, 1, THREE.RGBAFormat);
        if (srgb) t.encoding = THREE.sRGBEncoding;
        t.needsUpdate = true;
        return t;
      };
      defaults = { white: tex(255, 255, 255, false), whiteS: tex(255, 255, 255, true), flat: tex(128, 128, 255, false) };
    }
    return defaults;
  }
  function std(params) {
    const m = new THREE.MeshStandardMaterial(params);
    const d = getDefaults();
    if (!m.map) m.map = d.whiteS;
    if (!m.normalMap) m.normalMap = d.flat;
    if (!m.roughnessMap) m.roughnessMap = d.white;
    if (!m.envMap && BI.Mat.env.neutral) m.envMap = BI.Mat.env.neutral;
    return m;
  }

  // ---------------------------------------------------------------------------
  // Procedural environment maps
  // ---------------------------------------------------------------------------
  // Small canvas cube maps (64 px per face). A plain CubeTexture compiles far
  // faster than a PMREM env on ANGLE/D3D11 (measured ~230 ms less per shader
  // program); roughness picks a blurrier mip level.
  function buildEnv(renderer, kind) {
    const S = 64;
    const top = kind === 'night' ? '#0b1428' : kind === 'emergency' ? '#170303' : '#1b1d20';
    const mid = kind === 'night' ? '#05080f' : kind === 'emergency' ? '#090101' : '#0e0f11';
    const bot = kind === 'night' ? '#010203' : kind === 'emergency' ? '#030000' : '#050506';
    const faces = [];
    for (let f = 0; f < 6; f++) {
      const c = document.createElement('canvas');
      c.width = c.height = S;
      const ctx = c.getContext('2d');
      if (f === 2) ctx.fillStyle = top;
      else if (f === 3) ctx.fillStyle = bot;
      else {
        const g = ctx.createLinearGradient(0, 0, 0, S);
        g.addColorStop(0, top);
        g.addColorStop(0.5, mid);
        g.addColorStop(1, bot);
        ctx.fillStyle = g;
      }
      ctx.fillRect(0, 0, S, S);
      if (kind === 'interior' && f === 2) {
        ctx.fillStyle = 'rgba(225,235,255,0.55)';
        for (let i = 0; i < 4; i++) ctx.fillRect(6 + i * 15, 8, 4, 48);
      }
      if (kind === 'interior' && f !== 2 && f !== 3) {
        ctx.fillStyle = 'rgba(200,210,230,0.25)';
        ctx.fillRect(0, 4, S, 3);
      }
      if (kind === 'emergency' && (f === 0 || f === 4)) {
        const g = ctx.createRadialGradient(S * 0.5, S * 0.25, 0, S * 0.5, S * 0.25, S * 0.3);
        g.addColorStop(0, 'rgba(255,40,20,0.6)');
        g.addColorStop(1, 'rgba(255,40,20,0)');
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, S, S);
      }
      if (kind === 'night' && f === 5) {
        const g = ctx.createRadialGradient(S * 0.6, S * 0.3, 0, S * 0.6, S * 0.3, S * 0.25);
        g.addColorStop(0, 'rgba(200,215,255,0.8)');
        g.addColorStop(1, 'rgba(200,215,255,0)');
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, S, S);
      }
      faces.push(c);
    }
    const t = new THREE.CubeTexture(faces);
    t.encoding = THREE.sRGBEncoding;
    t.needsUpdate = true;
    return t;
  }

  function reg(mat, env) {
    if (env && envUsers[env]) envUsers[env].push(mat);
    return mat;
  }

  // ---------------------------------------------------------------------------
  // Definitions
  // ---------------------------------------------------------------------------
  const DEF = {
    // --- structure -----------------------------------------------------------
    concrete: () => std({ ...BI.Tex.get('concrete'), color: 0xffffff, roughness: 1, metalness: 0 }),
    concreteDark: () => std({ ...BI.Tex.get('concrete'), color: 0x8a8a86, roughness: 1, metalness: 0 }),
    wallPanel: () => std({ ...BI.Tex.get('wallPanel'), color: 0xffffff, roughness: 1 }),
    wallLab: () => std({ ...BI.Tex.get('wallPanel'), color: 0xe9eeef, roughness: 0.8 }),
    wallGen: () => std({ ...BI.Tex.get('wallPanel'), color: 0x9a968a, roughness: 1 }),
    floorConcrete: () => reg(std({ ...BI.Tex.get('floorConcrete'), color: 0xffffff, roughness: 1, envMapIntensity: 0.35 }), 'interior'),
    tiles: () => reg(std({ ...BI.Tex.get('tiles'), color: 0xd8dcdc, roughness: 1, envMapIntensity: 0.3 }), 'interior'),
    ceiling: () => std({ ...BI.Tex.get('ceiling'), color: 0xffffff, roughness: 0.95 }),
    hazard: () => std({ ...BI.Tex.get('hazard'), roughness: 0.75 }),

    // --- metals --------------------------------------------------------------
    metal: () => reg(std({ ...BI.Tex.get('metalPlate'), color: 0xffffff, roughness: 1, metalness: 0.65, envMapIntensity: 0.9 }), 'interior'),
    metalOut: () => reg(std({ ...BI.Tex.get('metalPlate'), color: 0xcfcfcf, roughness: 1, metalness: 0.6, envMapIntensity: 1.0 }), 'night'),
    metalDark: () => reg(std({ color: 0x2b2e31, roughness: 0.45, metalness: 0.7, envMapIntensity: 0.8 }), 'interior'),
    metalDarkOut: () => reg(std({ color: 0x2b2e31, roughness: 0.5, metalness: 0.7, envMapIntensity: 1.0 }), 'night'),
    steel: () => reg(std({ color: 0x9aa0a4, roughness: 0.3, metalness: 0.9, envMapIntensity: 1.0 }), 'interior'),
    steelOut: () => reg(std({ color: 0x8a9094, roughness: 0.35, metalness: 0.9 }), 'night'),
    copper: () => reg(std({ color: 0xb06a3c, roughness: 0.35, metalness: 0.9 }), 'interior'),
    diamondPlate: () => reg(std({ ...BI.Tex.get('diamondPlate'), color: 0xffffff, roughness: 1, metalness: 0.7 }), 'interior'),
    grate: () => reg(std({ ...BI.Tex.get('grate'), color: 0xffffff, roughness: 1, metalness: 0.6 }), 'interior'),
    rust: () => std({ ...BI.Tex.get('rust'), color: 0xffffff, roughness: 1, metalness: 0.3 }),
    pipe: () => reg(std({ color: 0x5d6a62, roughness: 0.5, metalness: 0.6 }), 'interior'),
    pipeRed: () => reg(std({ color: 0x7a2a20, roughness: 0.55, metalness: 0.4 }), 'interior'),
    pipeYellow: () => reg(std({ color: 0xa27b1d, roughness: 0.5, metalness: 0.4 }), 'interior'),
    cable: () => std({ color: 0x121212, roughness: 0.6, metalness: 0.1 }),
    rubber: () => std({ color: 0x1a1a1a, roughness: 0.9 }),
    plasticDark: () => std({ color: 0x23272b, roughness: 0.55 }),
    plasticLight: () => std({ color: 0xc9ccc8, roughness: 0.5 }),
    deskTop: () => std({ color: 0x3c4146, roughness: 0.6, metalness: 0.1 }),
    locker: () => reg(std({ ...BI.Tex.get('metalPlate'), color: 0x51606b, roughness: 1, metalness: 0.5 }), 'interior'),
    olivePaint: () => reg(std({ ...BI.Tex.get('metalPlate'), color: 0x6a7350, roughness: 1, metalness: 0.4 }), 'interior'),
    olivePaintOut: () => reg(std({ ...BI.Tex.get('metalPlate'), color: 0x5f6748, roughness: 1, metalness: 0.4 }), 'night'),
    labWhite: () => reg(std({ color: 0xdfe5e6, roughness: 0.35, metalness: 0.05 }), 'interior'),

    // --- outdoor natural -----------------------------------------------------
    rock: () => std({ ...BI.Tex.get('rock'), color: 0xb8b8b4, roughness: 1 }),
    barkPine: () => std({ ...BI.Tex.get('barkPine'), color: 0xffffff, roughness: 1 }),
    barkPalm: () => std({ ...BI.Tex.get('barkPalm'), color: 0xffffff, roughness: 1 }),
    needles: () => std({ ...BI.Tex.get('needles'), color: 0xffffff, roughness: 1 }),
    leaf: () => std({ ...BI.Tex.get('leaf'), color: 0xffffff, roughness: 0.85, side: THREE.DoubleSide }),
    wood: () => std({ ...BI.Tex.get('wood'), color: 0xffffff, roughness: 1 }),
    woodDark: () => std({ ...BI.Tex.get('wood'), color: 0x7a6a5a, roughness: 1 }),
    crate: () => std({ ...BI.Tex.get('crate'), color: 0xffffff, roughness: 1 }),
    rope: () => std({ ...BI.Tex.get('fabric'), color: 0x8a7652, roughness: 1 }),
    sandbag: () => std({ ...BI.Tex.get('fabric'), color: 0x7d7158, roughness: 1 }),
    boatHull: () => reg(std({ ...BI.Tex.get('metalPlate'), color: 0x3b4a52, roughness: 1, metalness: 0.35 }), 'night'),
    boatTrim: () => std({ color: 0x8a867c, roughness: 0.6 }),
    fence: () => {
      const t = BI.Tex.get('fence').map.clone();
      t.needsUpdate = true;
      return std({ map: t, color: 0xb0b4b4, transparent: false, alphaTest: 0.5, side: THREE.DoubleSide, roughness: 0.5, metalness: 0.6 });
    },
    grassBlades: () => std({ map: BI.Tex.get('grassBlades').map, alphaTest: 0.5, side: THREE.DoubleSide, roughness: 1, color: 0xd2e0c0 }),

    // --- characters ----------------------------------------------------------
    uniform: () => std({ ...BI.Tex.get('fabric'), color: 0x262f3d, roughness: 1 }),
    vest: () => std({ ...BI.Tex.get('fabric'), color: 0x2a2e26, roughness: 1 }),
    helmet: () => std({ color: 0x1f2328, roughness: 0.55, metalness: 0.2 }),
    visorDark: () => reg(std({ color: 0x0b0d10, roughness: 0.08, metalness: 0.8 }), 'interior'),
    camo: () => std({ map: BI.Tex.get('camo').map, color: 0xffffff, roughness: 1 }),
    ghillie: () => std({ ...BI.Tex.get('leaf'), color: 0x8a9466, roughness: 1, side: THREE.DoubleSide }),
    labCoat: () => std({ ...BI.Tex.get('fabric'), color: 0xe6e8e4, roughness: 0.95 }),
    trousers: () => std({ ...BI.Tex.get('fabric'), color: 0x22252a, roughness: 1 }),
    boots: () => std({ color: 0x15130f, roughness: 0.7 }),
    glove: () => std({ color: 0x1b1c1d, roughness: 0.8 }),
    skin: () => std({ ...BI.Tex.get('skin'), color: 0xffffff, roughness: 0.75 }),
    hair: () => std({ color: 0x2a2018, roughness: 0.9 }),
    gunMetal: () => reg(std({ color: 0x2c2f33, roughness: 0.4, metalness: 0.8 }), 'interior'),
    gunPolymer: () => std({ color: 0x1d1f21, roughness: 0.65 }),
    robotPaint: () => reg(std({ ...BI.Tex.get('robotPaint'), color: 0xffffff, roughness: 1, metalness: 0.55 }), 'interior'),
    robotAccent: () => reg(std({ color: 0xc4921d, roughness: 0.45, metalness: 0.4 }), 'interior'),
    robotJoint: () => reg(std({ color: 0x1a1c1e, roughness: 0.35, metalness: 0.85 }), 'interior'),
    chrome: () => reg(std({ color: 0xd9dde0, roughness: 0.12, metalness: 1.0 }), 'interior'),

    // --- glass & emissives (emissive > 1 feeds the bloom) ---------------------
    glass: () =>
      reg(
        std({
          color: 0x9fc9d0, roughness: 0.06, metalness: 0.2, transparent: true, opacity: 0.12,
          envMapIntensity: 0.6, depthWrite: false, side: THREE.DoubleSide,
        }),
        'interior'
      ),
    glassOut: () => reg(std({ color: 0x2a3a40, roughness: 0.1, metalness: 0.4, envMapIntensity: 1.2 }), 'night'),
    tube: () => std({ color: 0xffffff, emissive: 0xe6f0ff, emissiveIntensity: 3.0, roughness: 0.4 }),
    emergency: () => std({ color: 0x330000, emissive: 0xff2a1a, emissiveIntensity: 0.0, roughness: 0.4 }),
    redLamp: () => std({ color: 0x400000, emissive: 0xff1a0a, emissiveIntensity: 3.0, roughness: 0.4 }),
    cyanGlow: () => std({ color: 0x0a2a2c, emissive: 0x47d1d6, emissiveIntensity: 2.5, roughness: 0.3 }),
    amberGlow: () => std({ color: 0x2a1a05, emissive: 0xffa040, emissiveIntensity: 2.5, roughness: 0.4 }),
    greenLed: () => std({ color: 0x052a05, emissive: 0x40ff60, emissiveIntensity: 2.0 }),
    lanternGlass: () => std({ color: 0xffd9a0, emissive: 0xffb050, emissiveIntensity: 4.0, roughness: 0.2, transparent: true, opacity: 0.9 }),
    floodLens: () => std({ color: 0xffffff, emissive: 0xfff2d8, emissiveIntensity: 6.0 }),
    blackMatte: () => std({ color: 0x0c0c0d, roughness: 0.9 }),
    signPost: () => std({ color: 0x45484a, roughness: 0.6, metalness: 0.5 }),
  };

  BI.Mat = {
    env: {},
    init(renderer) {
      this.env.night = buildEnv(renderer, 'night');
      this.env.interior = buildEnv(renderer, 'interior');
      this.env.emergency = buildEnv(renderer, 'emergency');
      this.env.neutral = buildEnv(renderer, 'interior'); // never swapped
    },
    get(name) {
      if (!cache[name]) {
        if (!DEF[name]) throw new Error('Unknown material ' + name);
        const m = DEF[name]();
        m.name = name;
        // apply env maps to registered metals
        m.envMap = this.env.neutral;
        for (const k of Object.keys(envUsers)) {
          if (envUsers[k].includes(m)) m.envMap = k === 'night' ? this.env.night : k === 'interior' ? this.env.interior : this.env.neutral;
        }
        cache[name] = m;
      }
      return cache[name];
    },
    // Standard material with the shared texture-slot layout (see std()).
    standard(params) {
      return std(params);
    },
    // Fresh copy (for per-object emissive animation such as flickering tubes).
    clone(name) {
      const m = this.get(name).clone();
      m.envMap = this.get(name).envMap;
      return m;
    },
    // Register a material created elsewhere (viewmodels) for env switching.
    track(mat, group) {
      reg(mat, group);
      if (group === 'interior') mat.envMap = this.env.interior;
      if (group === 'night') mat.envMap = this.env.night;
      return mat;
    },
    // Blackout: interior reflections turn red.
    setInteriorEnv(kind) {
      const tex = this.env[kind];
      for (const m of envUsers.interior) {
        m.envMap = tex;
      }
    },
    cache,
  };
})();
