/* =============================================================================
 * models/generator.js — HERO MODEL 1: the bunker generator
 * -----------------------------------------------------------------------------
 * Modelled in code with the detail of a custom Blender asset. Local origin is
 * the top of its plinth; it is ~6.4 m long (x), 3 m wide (z), 3.9 m tall.
 *
 *   diesel engine block   painted body, cooling fins, 6 cylinder heads,
 *                         intake manifold, spinning flywheel + belt guard
 *   alternator / core     ribbed glass housing showing an animated energy core
 *                         (custom shader, intensity = coreLevel)
 *   control face (+x)     three gauges with needles that jitter with load,
 *                         indicator LEDs, a big valve wheel, warning labels
 *   plumbing              coolant pipes, valves, power cables (TubeGeometry)
 *   beacon                amber rotating lamp on top while running
 *
 * disable(): core flickers out, flywheel spins down, needles fall to zero,
 * a shower of sparks + smoke; afterwards it keeps sparking intermittently.
 * Role: World Builder / Graphics (hero asset).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const coreVert = 'varying vec3 vP; varying vec3 vN; varying vec3 vV; void main(){ vP = position; vec4 wp = modelMatrix * vec4(position,1.0); vN = normalize(mat3(modelMatrix)*normal); vV = normalize(cameraPosition - wp.xyz); gl_Position = projectionMatrix * viewMatrix * wp; }';
  const coreFrag = [
    'uniform float time; uniform float level;',
    'varying vec3 vP; varying vec3 vN; varying vec3 vV;',
    'float h2(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }',
    'float nz(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f); return mix(mix(h2(i), h2(i+vec2(1,0)), f.x), mix(h2(i+vec2(0,1)), h2(i+vec2(1,1)), f.x), f.y); }',
    'void main(){',
    '  float a = atan(vP.z, vP.x);',
    '  vec2 q = vec2(a * 2.0 + time * 3.0, vP.y * 3.0 - time * 1.2);',
    '  float n = nz(q) * 0.6 + nz(q * 2.7 + 3.1) * 0.4;',
    '  float bands = 0.5 + 0.5 * sin(vP.y * 18.0 - time * 9.0);',
    '  float rim = pow(1.0 - abs(dot(normalize(vN), normalize(vV))), 1.5);',
    '  vec3 c = mix(vec3(0.05, 0.35, 0.55), vec3(0.5, 1.4, 1.8), n) * (0.6 + bands * 0.6) + vec3(0.6, 1.2, 1.4) * rim;',
    '  gl_FragColor = vec4(c * level * 2.2 + vec3(0.01, 0.015, 0.02), 1.0);',
    '}',
  ].join('\n');

  function buildGenerator() {
    const M = (n) => BI.Mat.get(n);
    const root = new THREE.Group();
    root.name = 'generator';
    const B = new BI.Geo.StaticBatcher('generatorModel');
    const paint = BI.Mat.cache.genPaint || (BI.Mat.cache.genPaint = (() => {
      const m = BI.Mat.get('olivePaint').clone();
      m.color.setHex(0x5f6a4a);
      BI.Mat.track(m, 'interior');
      return m;
    })());

    // ---- skid / frame ----------------------------------------------------------
    B.box(-3.2, 0, -1.45, 3.2, 0.12, -1.25, M('metalDark'));
    B.box(-3.2, 0, 1.25, 3.2, 0.12, 1.45, M('metalDark'));
    B.box(-3.2, 0.12, -1.4, 3.2, 0.22, 1.4, M('metalDark'));
    for (let x = -2.8; x <= 2.8; x += 1.4) B.box(x - 0.08, 0, -1.45, x + 0.08, 0.22, 1.45, M('metalDark'));

    // ---- diesel engine block -----------------------------------------------------
    B.add(BI.Geo.roundedBox(2.7, 1.5, 1.9, 0.18, 2), paint, { x: -1.7, y: 1.0, z: 0 }, {});
    // cooling fins on both sides
    for (let i = 0; i < 14; i++) {
      for (const s of [-1, 1]) B.box(-3.0 + i * 0.19, 0.45, s * 0.96 - 0.04, -2.96 + i * 0.19, 1.55, s * 0.96 + 0.04 + s * 0.08, M('metalDark'));
    }
    // cylinder heads + rocker covers
    for (let i = 0; i < 6; i++) {
      const x = -2.75 + i * 0.42;
      B.add(BI.Geo.roundedBox(0.36, 0.28, 0.9, 0.06), M('metalDark'), { x, y: 1.9, z: 0 });
      B.add(new THREE.CylinderGeometry(0.05, 0.05, 0.12, 8), M('chrome'), { x, y: 2.08, z: 0.25 });
      B.add(new THREE.CylinderGeometry(0.05, 0.05, 0.12, 8), M('chrome'), { x, y: 2.08, z: -0.25 });
    }
    // intake manifold (pipe along the heads) and exhaust runners
    B.add(new THREE.CylinderGeometry(0.11, 0.11, 2.6, 10), M('steel'), { x: -1.7, y: 1.8, z: 0.62, rz: Math.PI / 2 });
    for (let i = 0; i < 6; i++) {
      const x = -2.75 + i * 0.42;
      B.add(new THREE.CylinderGeometry(0.06, 0.06, 0.4, 8), M('copper'), { x, y: 1.75, z: -0.65, rx: 0.9 });
    }
    B.add(new THREE.CylinderGeometry(0.14, 0.14, 2.6, 10), M('rust'), { x: -1.7, y: 1.6, z: -0.88, rz: Math.PI / 2 });
    // air filter canister
    B.add(new THREE.CylinderGeometry(0.32, 0.32, 0.6, 16), M('metalDark'), { x: -0.6, y: 2.05, z: 0.55 });
    B.add(new THREE.CylinderGeometry(0.34, 0.34, 0.06, 16), M('chrome'), { x: -0.6, y: 2.37, z: 0.55 });
    // belt guard
    B.add(BI.Geo.roundedBox(0.3, 1.4, 1.2, 0.15), M('hazard'), { x: -3.25, y: 1.1, z: 0.0 });

    // ---- coupling + alternator housing ---------------------------------------------
    B.add(new THREE.CylinderGeometry(0.6, 0.6, 0.6, 20), M('metalDark'), { x: -0.1, y: 1.15, z: 0, rz: Math.PI / 2 });
    const hx0 = 0.2, hx1 = 3.0, hR = 1.05;
    // end sections (solid)
    for (const [a, b] of [[hx0, hx0 + 0.55], [hx1 - 0.55, hx1]]) {
      B.add(new THREE.CylinderGeometry(hR, hR, b - a, 28), paint, { x: (a + b) / 2, y: 1.25, z: 0, rz: Math.PI / 2 });
    }
    // ribs over the glass section
    for (let i = 0; i <= 5; i++) {
      const x = hx0 + 0.55 + i * ((hx1 - hx0 - 1.1) / 5);
      B.add(new THREE.TorusGeometry(hR, 0.05, 6, 28), M('metalDark'), { x, y: 1.25, z: 0, ry: Math.PI / 2 });
    }
    for (let k = 0; k < 6; k++) {
      const a = (k / 6) * Math.PI * 2;
      B.add(new THREE.BoxGeometry(hx1 - hx0 - 1.1, 0.06, 0.06), M('metalDark'), { x: (hx0 + hx1) / 2, y: 1.25 + Math.sin(a) * hR, z: Math.cos(a) * hR });
    }
    // support saddles
    for (const x of [0.6, 2.6]) B.box(x - 0.15, 0.22, -0.9, x + 0.15, 0.6, 0.9, M('metalDark'));
    // exhaust / cooling stack (connects to the ceiling pipe in the room)
    B.add(new THREE.CylinderGeometry(0.24, 0.3, 1.8, 14), M('rust'), { x: 1.5, y: 3.2, z: 0 });
    B.add(new THREE.CylinderGeometry(0.34, 0.34, 0.14, 14), M('metalDark'), { x: 1.5, y: 2.35, z: 0 });

    // glass section + glowing core
    const glass = new THREE.Mesh(new THREE.CylinderGeometry(hR - 0.03, hR - 0.03, hx1 - hx0 - 1.1, 28, 1, true), M('glass'));
    glass.rotation.z = Math.PI / 2;
    glass.position.set((hx0 + hx1) / 2, 1.25, 0);
    glass.renderOrder = 3;
    root.add(glass);
    const coreMat = new THREE.ShaderMaterial({ uniforms: { time: { value: 0 }, level: { value: 1 } }, vertexShader: coreVert, fragmentShader: coreFrag });
    const core = new THREE.Mesh(new THREE.CylinderGeometry(0.62, 0.62, hx1 - hx0 - 1.2, 24, 6), coreMat);
    core.rotation.z = Math.PI / 2;
    core.position.copy(glass.position);
    root.add(core);
    const coreRod = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.16, hx1 - hx0 - 0.9, 12), M('chrome'));
    coreRod.rotation.z = Math.PI / 2;
    coreRod.position.copy(glass.position);
    root.add(coreRod);

    // ---- control face (+x end) -----------------------------------------------------
    const fx = 3.05;
    B.add(BI.Geo.roundedBox(1.6, 1.5, 0.3, 0.08), M('metalDark'), { x: fx + 0.05, y: 1.35, z: 0, ry: Math.PI / 2 });
    const gauges = [];
    const gaugeTex = BI.Tex.get('gauge').map;
    const gaugeMat = BI.Mat.standard({ map: gaugeTex, roughness: 0.4 });
    const needleMat = new THREE.MeshBasicMaterial({ color: 0xd02010 });
    [[-0.45, 1.65, 0.28], [0.0, 1.65, 0.28], [0.45, 1.65, 0.28]].forEach((p, i) => {
      const face = new THREE.Mesh(new THREE.CircleGeometry(p[2] * 0.62, 24), gaugeMat);
      face.position.set(fx + 0.21, p[1], p[0]);
      face.rotation.y = Math.PI / 2;
      root.add(face);
      B.add(new THREE.TorusGeometry(p[2] * 0.64, 0.025, 6, 24), M('chrome'), { x: fx + 0.21, y: p[1], z: p[0], ry: Math.PI / 2 });
      const pivot = new THREE.Group();
      pivot.position.set(fx + 0.225, p[1], p[0]);
      pivot.rotation.y = Math.PI / 2;
      const needle = new THREE.Mesh(new THREE.BoxGeometry(0.014, p[2] * 0.5, 0.004), needleMat);
      needle.position.y = p[2] * 0.22;
      const nGroup = new THREE.Group();
      nGroup.add(needle);
      pivot.add(nGroup);
      root.add(pivot);
      gauges.push({ needle: nGroup, base: 0.35 + i * 0.12, value: 0.6, jitter: 0.04 + i * 0.03 });
    });
    // indicator LEDs (shared materials so they can blink together)
    const ledOn = new THREE.MeshBasicMaterial({ color: new THREE.Color(0.3, 3.0, 0.5) });
    const ledWarn = new THREE.MeshBasicMaterial({ color: new THREE.Color(3.0, 1.6, 0.2) });
    for (let i = 0; i < 5; i++) {
      const led = new THREE.Mesh(new THREE.SphereGeometry(0.03, 8, 6), i === 3 ? ledWarn : ledOn);
      led.position.set(fx + 0.22, 1.05, -0.5 + i * 0.25);
      root.add(led);
    }
    // valve wheel
    const wheel = new THREE.Group();
    wheel.position.set(fx + 0.35, 0.75, 0.35);
    wheel.rotation.y = Math.PI / 2;
    wheel.add(new THREE.Mesh(new THREE.TorusGeometry(0.22, 0.03, 8, 20), M('pipeRed')));
    for (let k = 0; k < 3; k++) {
      const sp = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.03, 0.03), M('pipeRed'));
      sp.rotation.z = (k / 3) * Math.PI;
      wheel.add(sp);
    }
    root.add(wheel);
    B.add(new THREE.CylinderGeometry(0.05, 0.05, 0.3, 8), M('steel'), { x: fx + 0.2, y: 0.75, z: 0.35, rz: Math.PI / 2 });

    // ---- coolant plumbing + side valves ------------------------------------------------
    const pipeBetween = (a, b, r, mat) => {
      const A = new THREE.Vector3(...a), Bv = new THREE.Vector3(...b);
      const g = new THREE.CylinderGeometry(r, r, A.distanceTo(Bv), 10);
      const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), Bv.clone().sub(A).normalize());
      B.add(g, M(mat), new THREE.Matrix4().compose(A.clone().add(Bv).multiplyScalar(0.5), q, new THREE.Vector3(1, 1, 1)));
    };
    pipeBetween([-2.9, 0.5, 1.15], [2.7, 0.5, 1.15], 0.07, 'pipe');
    pipeBetween([-2.9, 0.75, 1.2], [2.7, 0.75, 1.2], 0.05, 'pipeYellow');
    pipeBetween([2.7, 0.5, 1.15], [2.7, 1.0, 1.15], 0.07, 'pipe');
    pipeBetween([-2.9, 0.5, 1.15], [-2.9, 1.1, 0.95], 0.07, 'pipe');
    for (const x of [-1.6, 0.9, 2.2]) {
      B.add(new THREE.CylinderGeometry(0.1, 0.1, 0.16, 10), M('steel'), { x, y: 0.5, z: 1.25, rx: Math.PI / 2 });
      B.add(new THREE.TorusGeometry(0.12, 0.02, 6, 14), M('pipeRed'), { x, y: 0.5, z: 1.36 });
    }

    // ---- power cables (lying on the grating, running toward the cabinets) ----------
    const cablePaths = [
      [[2.6, 0.7, -0.9], [3.1, 0.1, -1.6], [3.4, -0.28, -2.6], [3.6, -0.28, -4.6], [4.0, -0.28, -7.0], [4.2, -0.28, 9.0]],
      [[2.4, 0.7, -0.95], [2.9, 0.1, -1.7], [3.1, -0.28, -2.8], [3.0, -0.28, 5.5]],
      [[0.8, 0.5, -1.0], [1.0, -0.1, -1.8], [0.4, -0.28, -3.2], [-3.0, -0.28, -3.4]],
    ];
    for (const pts of cablePaths) {
      const clean = pts.filter((p) => Math.abs(p[2]) < 5);
      root.add(new THREE.Mesh(BI.Geo.tube(clean, 0.05, 30, 6), M('cable')));
    }

    // ---- labels -------------------------------------------------------------------
    const label = (key, opts, w, h, x, y, z, ry) => {
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), BI.Mat.standard({ map: BI.Tex.decal(key, opts), roughness: 0.6, polygonOffset: true, polygonOffsetFactor: -1 }));
      m.position.set(x, y, z);
      m.rotation.y = ry;
      root.add(m);
    };
    label('gen11kv', { w: 512, h: 200, bg: '#e8b52a', border: '#111', text: 'DANGER 11 kV', color: '#111', fontSize: 80 }, 0.9, 0.35, 1.6, 1.25, hR + 0.02, 0);
    label('genid', { w: 512, h: 200, bg: '#1d2633', text: 'G-1  //  3.2 MW', color: '#e8e8e0', fontSize: 70 }, 0.9, 0.35, -1.7, 1.2, 0.97, 0);
    label('genid2', { w: 512, h: 200, bg: '#1d2633', text: 'G-1  //  3.2 MW', color: '#e8e8e0', fontSize: 70 }, 0.9, 0.35, -1.7, 1.2, -0.97, Math.PI);
    label('genctrl', { w: 512, h: 128, bg: '#111', text: 'MAIN CONTROL', color: '#d9a521', fontSize: 60 }, 0.7, 0.18, fx + 0.21, 2.0, 0, Math.PI / 2);

    // ---- flywheel (spins) ---------------------------------------------------------
    const fly = new THREE.Group();
    fly.position.set(-3.45, 1.0, 0);
    const flyDisc = new THREE.Mesh(new THREE.CylinderGeometry(0.62, 0.62, 0.14, 28), M('steel'));
    flyDisc.rotation.z = Math.PI / 2;
    fly.add(flyDisc);
    for (let k = 0; k < 6; k++) {
      const hole = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 0.16, 10), M('metalDark'));
      hole.rotation.z = Math.PI / 2;
      hole.position.set(0, Math.sin((k / 6) * Math.PI * 2) * 0.38, Math.cos((k / 6) * Math.PI * 2) * 0.38);
      fly.add(hole);
    }
    root.add(fly);

    // ---- amber beacon ---------------------------------------------------------------
    const beacon = new THREE.Group();
    beacon.position.set(-0.6, 2.55, -0.5);
    const beaconMat = BI.Mat.standard({ color: 0x402000, emissive: 0xffa020, emissiveIntensity: 2.5, transparent: true, opacity: 0.85 });
    beacon.add(new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.1, 0.18, 12), beaconMat));
    const refl = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.12, 0.15), M('chrome'));
    beacon.add(refl);
    root.add(beacon);
    B.add(new THREE.CylinderGeometry(0.12, 0.12, 0.06, 12), M('metalDark'), { x: -0.6, y: 2.43, z: -0.5 });

    B.build(root);
    root.traverse((o) => {
      if (o.isMesh && o !== glass && o !== core) {
        o.castShadow = true;
        o.receiveShadow = true;
      }
    });

    // ---- behaviour -------------------------------------------------------------------
    const api = {
      group: root,
      coreLevel: 1,
      running: true,
      disabledAt: -1,
      flySpeed: 9,
      sparkTimer: 0,
      panelWorld: null,
      disable() {
        if (!this.running) return;
        this.running = false;
        this.disabledAt = BI.Engine.time;
        const wp = new THREE.Vector3();
        root.localToWorld(wp.set(fx + 0.3, 1.6, 0));
        BI.FX.sparks(wp, { x: 1, y: 0.5, z: 0 }, 60, { speed: 7, life: 0.8, floor: root.position.y - 0.3 });
        root.localToWorld(wp.set(1.5, 2.8, 0));
        BI.FX.sparks(wp, { x: 0, y: 1, z: 0 }, 50, { speed: 6, life: 0.9, floor: root.position.y - 0.3 });
        BI.FX.smoke(wp, 14, { color: [0.1, 0.1, 0.1], rise: 1.2, life: 4, s1: 3, alpha: 0.5, spread: 1.2 });
        // lingering faults
        root.localToWorld(wp.set(2.9, 0.6, -1.2));
        this.emSparks = BI.FX.addEmitter({ pos: wp.clone(), kind: 'sparks', rate: 0.0, burst: 8, floor: root.position.y - 0.3, dir: { x: 0.3, y: 0.6, z: -0.5 } });
        root.localToWorld(wp.set(-1.7, 2.0, 0));
        this.emSmoke = BI.FX.addEmitter({ pos: wp.clone(), kind: 'smoke', rate: 2.5, range: 40, color: [0.09, 0.09, 0.09] });
      },
      update(dt, t) {
        coreMat.uniforms.time.value = t;
        if (this.running) {
          this.coreLevel = 0.92 + Math.sin(t * 7.0) * 0.05 + Math.sin(t * 23.0) * 0.03;
        } else {
          const k = BI.Engine.time - this.disabledAt;
          const fl = k < 1.6 ? (Math.random() < 0.4 ? 1.1 : 0.15) * (1 - k / 1.6) : 0;
          this.coreLevel = Math.max(0, fl);
          // intermittent spark bursts from the damaged cabling
          this.sparkTimer -= dt;
          if (this.sparkTimer <= 0 && this.emSparks) {
            this.sparkTimer = 0.4 + Math.random() * 1.6;
            BI.FX.sparks(this.emSparks.pos, this.emSparks.dir, 6 + Math.floor(Math.random() * 10), { speed: 4, floor: this.emSparks.floor });
            BI.Events.emit('fx:spark', { pos: this.emSparks.pos.clone() });
          }
        }
        coreMat.uniforms.level.value = this.coreLevel;
        this.flySpeed = U.damp(this.flySpeed, this.running ? 9 : 0, this.running ? 4 : 0.5, dt);
        fly.rotation.x += this.flySpeed * dt;
        for (const g of gauges) {
          const target = this.running ? g.base + Math.sin(t * (2 + g.jitter * 30)) * g.jitter + (Math.random() - 0.5) * g.jitter * 0.5 : 0;
          g.value = U.damp(g.value, target, this.running ? 8 : 2.5, dt);
          g.needle.rotation.z = Math.PI * 0.75 - g.value * Math.PI * 1.5;
        }
        ledOn.color.setRGB(this.running ? 0.3 : 0.02, this.running ? 3.0 : 0.05, this.running ? 0.5 : 0.02);
        const warnBlink = this.running ? (Math.sin(t * 4) > 0 ? 3 : 0.3) : Math.sin(t * 10) > 0 ? 3.5 : 0.1;
        ledWarn.color.setRGB(warnBlink, warnBlink * 0.4, 0.05);
        beaconMat.emissiveIntensity = this.running ? 2.5 : 0.05;
        if (this.running) beacon.rotation.y += dt * 5;
      },
    };
    return api;
  }

  BI.Models = BI.Models || {};
  BI.Models.buildGenerator = buildGenerator;
})();
