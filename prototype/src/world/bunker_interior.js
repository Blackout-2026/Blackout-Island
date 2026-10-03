/* =============================================================================
 * world/bunker_interior.js — the underground rooms and their lighting
 * -----------------------------------------------------------------------------
 * Rooms (floor y = 2.5), each with its own palette so they read differently:
 *   Security office  blue-grey panels, desks, live camera monitors, lockers
 *   Corridor         long, pillars, ceiling pipes + cable tray, puddles, crate
 *   Generator room   tall, warm sodium high-bays, grating, catwalk + stairs,
 *                    fuel tanks, cabinets, the hero generator
 *   Laboratory       white tiles, cool panels, glass containment chamber
 *
 * Lighting: every fixture is a virtual light (world/lighting.js) plus an
 * instanced emissive tube and an instanced faux-volumetric shaft. Some tubes
 * are faulty and flicker. On `generator:disabled` the fixtures die in a
 * cascade outward from the generator (spark + pop each), then red emergency
 * lamps fade in and two rotating alarm beacons start sweeping red beams
 * through the thickened fog.
 * Role: World Builder (rooms) / Graphics (lighting, screens).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const L = BI.Level;
  const BF = L.BUNKER_Y;

  // --- monitor screen shader: animated static, scanlines, optional live feed ---
  const screenVert = 'varying vec2 vUv; varying vec3 vW; void main(){ vUv = uv; vec4 wp = modelMatrix * vec4(position,1.0); vW = wp.xyz; gl_Position = projectionMatrix * viewMatrix * wp; }';
  const screenFrag = [
    'uniform float time; uniform float power; uniform sampler2D tFeed; uniform float useFeed; uniform vec3 tint;',
    'varying vec2 vUv; varying vec3 vW;',
    'float h(vec2 p){ return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }',
    'void main(){',
    '  vec2 uv = vUv;',
    '  float seed = floor(vW.x * 3.1) + floor(vW.z * 1.7) * 7.0;',
    '  vec3 col;',
    '  if (useFeed > 0.5) {',
    '    vec3 f = texture2D(tFeed, uv + vec2(h(vec2(floor(uv.y * 90.0), floor(time * 20.0))) * 0.003, 0.0)).rgb;',
    '    float l = dot(f, vec3(0.3, 0.59, 0.11)) * 2.6 + 0.04;',
    '    col = vec3(l) * tint;',
    '    col += (h(floor(uv * vec2(256.0, 144.0)) + floor(time * 30.0)) - 0.5) * 0.06;',
    '  } else {',
    '    float mode = fract(seed * 0.37);',
    '    float n = h(floor(uv * vec2(140.0, 100.0)) + floor(time * 24.0) + seed);',
    '    if (mode < 0.45) { col = vec3(n * 0.4 + 0.05) * mix(vec3(1.0), tint, 0.4); }',
    '    else {',
    // fake data screen: scrolling bars and lines
    '      float row = floor((uv.y + time * 0.05 * (0.5 + mode)) * 18.0);',
    '      float len = h(vec2(row, seed));',
    '      float bar = step(uv.x, 0.08 + len * 0.8) * step(0.25, fract((uv.y + time * 0.05 * (0.5 + mode)) * 18.0));',
    '      col = tint * (0.08 + bar * 0.55) + n * 0.04;',
    '    }',
    '  }',
    '  float scan = 0.82 + 0.18 * sin(uv.y * 520.0);',
    '  float roll = 0.9 + 0.1 * smoothstep(0.0, 0.05, abs(fract(uv.y - time * 0.12) - 0.5));',
    '  vec2 d = uv - 0.5; float vig = 1.0 - dot(d, d) * 1.6;',
    '  gl_FragColor = vec4(col * scan * roll * vig * power * 0.95, 1.0);',
    '}',
  ].join('\n');

  // --- instanced light shaft shader (per-instance intensity in instanceColor.r)
  const shaftVert = [
    'varying float vH; varying vec3 vN; varying vec3 vV; varying vec3 vW; varying vec3 vI; varying float vFog;',
    'void main(){',
    '  vH = uv.y;',
    '  mat4 m = modelMatrix * instanceMatrix;',
    '  vec4 wp = m * vec4(position, 1.0); vW = wp.xyz;',
    '  vN = normalize(mat3(m) * normal); vV = normalize(cameraPosition - wp.xyz);',
    '  #ifdef USE_INSTANCING_COLOR',
    '  vI = instanceColor;',
    '  #else',
    '  vI = vec3(1.0);',
    '  #endif',
    '  vec4 mv = viewMatrix * wp; vFog = -mv.z;',
    '  gl_Position = projectionMatrix * mv;',
    '}',
  ].join('\n');
  const shaftFrag = [
    'uniform vec3 color; uniform float time; uniform float fogDensity;',
    'varying float vH; varying vec3 vN; varying vec3 vV; varying vec3 vW; varying vec3 vI; varying float vFog;',
    'float h2(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }',
    'float nz(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f); return mix(mix(h2(i), h2(i+vec2(1,0)), f.x), mix(h2(i+vec2(0,1)), h2(i+vec2(1,1)), f.x), f.y); }',
    'void main(){',
    '  if (max(vI.r, max(vI.g, vI.b)) < 0.002) discard;',
    '  float edge = pow(abs(dot(normalize(vN), normalize(vV))), 2.0);',
    '  float along = pow(vH, 1.6) * smoothstep(0.0, 0.1, 1.0 - vH);',
    '  float dust = 0.6 + 0.6 * nz(vW.xz * 1.3 + vW.y * 0.8 + vec2(time * 0.12, time * -0.07));',
    '  float fogF = exp(-fogDensity * fogDensity * vFog * vFog * 0.4);',
    '  gl_FragColor = vec4(color * vI * edge * along * dust * fogF * 0.11, 1.0);',
    '}',
  ].join('\n');

  const Interior = {
    groups: {},
    fixtures: [],
    emergency: [],
    alarms: [],
    fans: [],
    leds: [],
    blackoutTime: -1,
    blackoutDone: false,

    build(scene) {
      const P = BI.Physics;
      const M = (n) => BI.Mat.get(n);
      this.scene = scene;
      const mkGroup = (name) => {
        const g = new THREE.Group();
        g.name = name;
        scene.add(g);
        this.groups[name] = g;
        return g;
      };

      // -------------------------------------------------------------------------
      // instanced fixtures shared by all rooms
      // -------------------------------------------------------------------------
      const tubeGeo = new THREE.BoxGeometry(1.25, 0.05, 0.08);
      const panelGeo = new THREE.BoxGeometry(1.1, 0.03, 0.55);
      const bulbGeo = new THREE.SphereGeometry(0.16, 10, 6);
      const tubeMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
      this.tubes = new THREE.InstancedMesh(tubeGeo, tubeMat, 40);
      this.panels = new THREE.InstancedMesh(panelGeo, tubeMat, 12);
      this.bulbs = new THREE.InstancedMesh(bulbGeo, tubeMat, 12);
      this.emLamps = new THREE.InstancedMesh(new THREE.BoxGeometry(0.28, 0.14, 0.12), new THREE.MeshBasicMaterial({ color: 0xffffff }), 24);
      const shaftGeo = new THREE.ConeGeometry(1, 1, 18, 1, true);
      shaftGeo.translate(0, -0.5, 0);
      this.shaftMat = new THREE.ShaderMaterial({
        uniforms: { color: { value: new THREE.Color(1, 1, 1) }, time: { value: 0 }, fogDensity: { value: 0.02 } },
        vertexShader: shaftVert, fragmentShader: shaftFrag, transparent: true, depthWrite: false,
        blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
      });
      this.shafts = new THREE.InstancedMesh(shaftGeo, this.shaftMat, 40);
      for (const im of [this.tubes, this.panels, this.bulbs, this.emLamps, this.shafts]) {
        // Allocate the per-instance colour buffer at full capacity BEFORE
        // resetting count (setColorAt sizes the buffer from the current count).
        im.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(im.instanceMatrix.count * 3), 3);
        im.count = 0;
        im.frustumCulled = false;
        im.castShadow = false;
        im.receiveShadow = false;
      }
      this.shafts.renderOrder = 6;
      const fixGroup = mkGroup('fixtures');
      fixGroup.add(this.tubes, this.panels, this.bulbs, this.emLamps, this.shafts);

      // -------------------------------------------------------------------------
      // helpers
      // -------------------------------------------------------------------------
      const dummy = new THREE.Object3D();
      const addInst = (im, pos, rotY, scale) => {
        const i = im.count++;
        dummy.position.copy(pos);
        dummy.rotation.set(0, rotY || 0, 0);
        dummy.scale.set(scale ? scale.x : 1, scale ? scale.y : 1, scale ? scale.z : 1);
        dummy.updateMatrix();
        im.setMatrixAt(i, dummy.matrix);
        im.setColorAt(i, new THREE.Color(0, 0, 0));
        return i;
      };
      // Fixture = housing (batched) + emissive instance + shaft + virtual light
      const fixture = (B, kind, x, y, z, rotY, color, intensity, dist, shaftH, shaftR) => {
        const pos = new THREE.Vector3(x, y, z);
        let im, idx;
        if (kind === 'tube') {
          B.add(new THREE.BoxGeometry(1.45, 0.1, 0.32), M('plasticLight'), { x, y: y + 0.07, z, ry: rotY || 0 }, { castShadow: false });
          im = this.tubes;
          idx = [addInst(im, new THREE.Vector3(x + Math.sin(rotY || 0) * 0.08, y, z + Math.cos(rotY || 0) * 0.08), rotY), addInst(im, new THREE.Vector3(x - Math.sin(rotY || 0) * 0.08, y, z - Math.cos(rotY || 0) * 0.08), rotY)];
        } else if (kind === 'panel') {
          B.add(new THREE.BoxGeometry(1.25, 0.08, 0.7), M('plasticLight'), { x, y: y + 0.05, z, ry: rotY || 0 }, { castShadow: false });
          im = this.panels;
          idx = [addInst(im, pos, rotY)];
        } else {
          // sodium high-bay: reflector dome + bulb
          const dome = new THREE.CylinderGeometry(0.25, 0.75, 0.55, 16, 1, true);
          B.add(dome, M('metalDark'), { x, y: y + 0.2, z }, { castShadow: false });
          B.add(new THREE.CylinderGeometry(0.03, 0.03, 9.0 - y, 5), M('steel'), { x, y: (9.0 + y) / 2 + 0.3, z }, { castShadow: false });
          im = this.bulbs;
          idx = [addInst(im, new THREE.Vector3(x, y + 0.05, z))];
        }
        const light = BI.Lighting.addPoint({ pos: new THREE.Vector3(x, y - 0.25, z), color, intensity, distance: dist, decay: 1.6, zone: 'bunker', group: 'fluoro', priority: 1 });
        const shaftIdx = shaftH ? addInst(this.shafts, new THREE.Vector3(x, y - 0.02, z), rotY, { x: shaftR, y: shaftH, z: shaftR * (kind === 'tube' ? 0.55 : 1) }) : -1;
        const f = {
          kind, pos, im, idx, light, shaftIdx, color: new THREE.Color(color), base: kind === 'sodium' ? 6 : kind === 'panel' ? 3.2 : 4.2,
          out: 1, state: 'on', dieAt: -1, faulty: false, flick: 1, seed: Math.random() * 100,
        };
        this.fixtures.push(f);
        return f;
      };
      const emergencyLamp = (B, x, y, z, rotY) => {
        B.add(new THREE.BoxGeometry(0.34, 0.2, 0.08), M('metalDark'), { x: x - Math.sin(rotY) * 0.04, y, z: z - Math.cos(rotY) * 0.04, ry: rotY }, { castShadow: false });
        const idx = addInst(this.emLamps, new THREE.Vector3(x, y - 0.02, z), rotY);
        const light = BI.Lighting.addPoint({ pos: new THREE.Vector3(x + Math.sin(rotY) * 0.4, y - 0.2, z + Math.cos(rotY) * 0.4), color: 0xff2614, intensity: 0, distance: 9, decay: 1.5, zone: 'bunker', group: 'emergency', priority: 1.1 });
        this.emergency.push({ idx, light });
      };
      const alarm = (parent, B, x, y, z, mountUp) => {
        const g = new THREE.Group();
        g.position.set(x, y, z);
        parent.add(g);
        B.add(new THREE.CylinderGeometry(0.16, 0.18, 0.12, 12), M('metalDark'), { x, y: y + (mountUp ? 0.12 : -0.12), z });
        const domeMat = BI.Mat.standard({ color: 0x400404, emissive: 0xff1a0a, emissiveIntensity: 0.2, transparent: true, opacity: 0.85, roughness: 0.2 });
        const dome = new THREE.Mesh(new THREE.SphereGeometry(0.15, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), domeMat);
        if (!mountUp) dome.rotation.x = Math.PI;
        g.add(dome);
        const spinner = new THREE.Group();
        g.add(spinner);
        const cone = BI.FX.makeCone({ radius: 2.6, height: 9, color: 0xff2010, intensity: 0 });
        cone.position.set(0, 0, 0);
        cone.rotation.set(0, 0, 0);
        const holder = new THREE.Group();
        holder.rotation.x = 0.25; // beam slightly downward
        holder.add(cone);
        spinner.add(holder);
        const light = BI.Lighting.addSpot({ pos: new THREE.Vector3(x, y, z), target: new THREE.Vector3(x, y - 1, z + 5), color: 0xff1808, intensity: 0, distance: 22, angle: 0.42, penumbra: 0.4, decay: 1.3, zone: 'bunker', group: 'alarm', priority: 2 });
        this.alarms.push({ g, spinner, cone, domeMat, light, x, y, z, phase: Math.random() * 6 });
      };
      const signs = [];
      const sign = (parent, key, opts, w, h, x, y, z, ry, rx) => {
        const tex = BI.Tex.decal(key, opts);
        const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), BI.Mat.standard({ map: tex, roughness: 0.7, transparent: !opts.bg, alphaTest: opts.bg ? 0 : 0.5, polygonOffset: true, polygonOffsetFactor: -1 }));
        m.position.set(x, y, z);
        m.rotation.set(rx || 0, ry || 0, 0);
        m.receiveShadow = true;
        parent.add(m);
        signs.push(m);
        return m;
      };
      // Room shell: floor, ceiling, walls with openings.
      // openings: { side: 'xMin'|'xMax'|'zMin'|'zMax', a0, a1, top } (a = along-wall coordinate)
      const shell = (B, r, mats, openings, skip, wallTop) => {
        const T = 0.4;
        const y0 = BF, y1 = BF + r.h;
        const top = wallTop || {};
        B.box(r.minX - T, y0 - 0.4, r.minZ - T, r.maxX + T, y0, r.maxZ + T, M(mats.floor), { worldUV: mats.floorUV || 0.25 });
        P.addBox(r.minX - T, y0 - 0.4, r.minZ - T, r.maxX + T, y0, r.maxZ + T, { surface: mats.floorSurface || 'concrete', wall: false });
        B.box(r.minX - T, y1, r.minZ - T, r.maxX + T, y1 + 0.4, r.maxZ + T, M(mats.ceiling || 'ceiling'), { worldUV: 0.4, castShadow: false, receiveShadow: false });
        P.addBox(r.minX - T, y1, r.minZ - T, r.maxX + T, y1 + 0.4, r.maxZ + T, { surface: 'concrete', nav: false });
        const sides = {
          xMin: { fixed: r.minX - T, fixed2: r.minX, a0: r.minZ - T, a1: r.maxZ + T, axis: 'z' },
          xMax: { fixed: r.maxX, fixed2: r.maxX + T, a0: r.minZ - T, a1: r.maxZ + T, axis: 'z' },
          zMin: { fixed: r.minZ - T, fixed2: r.minZ, a0: r.minX, a1: r.maxX, axis: 'x' },
          zMax: { fixed: r.maxZ, fixed2: r.maxZ + T, a0: r.minX, a1: r.maxX, axis: 'x' },
        };
        for (const name of Object.keys(sides)) {
          if (skip && skip.indexOf(name) >= 0) continue;
          const s = sides[name];
          const wy1 = top[name] || y1;
          const ops = (openings || []).filter((o) => o.side === name).sort((a, b) => a.a0 - b.a0);
          let cur = s.a0;
          const seg = (a0, a1, b0, b1) => {
            if (a1 - a0 < 0.01 || b1 - b0 < 0.01) return;
            if (s.axis === 'z') {
              B.box(s.fixed, b0, a0, s.fixed2, b1, a1, M(mats.wall), { worldUV: 0.3 });
              P.addBox(s.fixed, b0, a0, s.fixed2, b1, a1, { surface: 'concrete' });
            } else {
              B.box(a0, b0, s.fixed, a1, b1, s.fixed2, M(mats.wall), { worldUV: 0.3 });
              P.addBox(a0, b0, s.fixed, a1, b1, s.fixed2, { surface: 'concrete' });
            }
          };
          for (const o of ops) {
            seg(cur, o.a0, y0, wy1);
            seg(o.a0, o.a1, y0 + (o.top || 2.8), wy1);
            cur = o.a1;
          }
          seg(cur, s.a1, y0, wy1);
        }
      };
      // hazard frame around a doorway
      const doorFrame = (B, axis, cx, cz, half, h) => {
        const t = 0.12;
        if (axis === 'x') {
          for (const s of [-1, 1]) B.box(cx + s * half - (s > 0 ? 0 : t), BF, cz - 0.28, cx + s * half + (s > 0 ? t : 0), BF + h, cz + 0.28, M('hazard'), { worldUV: 1.5 });
          B.box(cx - half - t, BF + h, cz - 0.28, cx + half + t, BF + h + t, cz + 0.28, M('hazard'), { worldUV: 1.5 });
        } else {
          for (const s of [-1, 1]) B.box(cx - 0.28, BF, cz + s * half - (s > 0 ? 0 : t), cx + 0.28, BF + h, cz + s * half + (s > 0 ? t : 0), M('hazard'), { worldUV: 1.5 });
          B.box(cx - 0.28, BF + h, cz - half - t, cx + 0.28, BF + h + t, cz + half + t, M('hazard'), { worldUV: 1.5 });
        }
      };
      const pipeRun = (B, mat, r, pts) => {
        for (let i = 0; i < pts.length - 1; i++) {
          const a = new THREE.Vector3(...pts[i]), b = new THREE.Vector3(...pts[i + 1]);
          const len = a.distanceTo(b);
          const g = new THREE.CylinderGeometry(r, r, len, 10);
          const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), b.clone().sub(a).normalize());
          const m4 = new THREE.Matrix4().compose(a.clone().add(b).multiplyScalar(0.5), q, new THREE.Vector3(1, 1, 1));
          B.add(g, M(mat), m4, { castShadow: false });
          if (i > 0) B.add(new THREE.SphereGeometry(r * 1.08, 10, 6), M(mat), { x: a.x, y: a.y, z: a.z }, { castShadow: false });
        }
      };
      const box = (B, mat, x0, y0, z0, x1, y1, z1, collide, uv) => {
        B.box(x0, y0, z0, x1, y1, z1, M(mat), uv ? { worldUV: uv } : {});
        if (collide) P.addBox(x0, y0, z0, x1, y1, z1, Object.assign({ surface: 'metal', wall: false }, collide === true ? {} : collide));
      };
      const puddle = (parent, x, z, s, rot) => {
        const mat = BI.Mat.standard({ map: BI.Tex.get('puddle').map, color: 0x6a7076, roughness: 0.08, metalness: 0.1, envMapIntensity: 0.5, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2 });
        BI.Mat.track(mat, 'interior');
        const m = new THREE.Mesh(new THREE.PlaneGeometry(s, s * 0.7), mat);
        m.rotation.set(-Math.PI / 2, 0, rot || 0);
        m.position.set(x, BF + 0.005, z);
        m.receiveShadow = true;
        parent.add(m);
      };
      // screens
      this.screenMat = new THREE.ShaderMaterial({
        uniforms: { time: { value: 0 }, power: { value: 1 }, tFeed: { value: null }, useFeed: { value: 0 }, tint: { value: new THREE.Color(0.55, 1.0, 0.7) } },
        vertexShader: screenVert, fragmentShader: screenFrag,
      });
      this.feedMat = this.screenMat.clone();
      this.feedMat.uniforms.useFeed.value = 1;
      this.feedMat.uniforms.tint.value = new THREE.Color(0.7, 1.0, 0.85);
      const monitor = (parent, B, x, y, z, ry, feed, w, h) => {
        w = w || 0.62; h = h || 0.42;
        B.add(BI.Geo.roundedBox(w + 0.08, h + 0.08, 0.12, 0.03), M('plasticDark'), { x: x - Math.sin(ry) * 0.06, y, z: z - Math.cos(ry) * 0.06, ry }, { castShadow: true });
        B.add(new THREE.BoxGeometry(0.06, 0.25, 0.06), M('plasticDark'), { x: x - Math.sin(ry) * 0.08, y: y - h / 2 - 0.12, z: z - Math.cos(ry) * 0.08, ry });
        const scr = new THREE.Mesh(new THREE.PlaneGeometry(w, h), feed ? this.feedMat : this.screenMat);
        scr.position.set(x + Math.sin(ry) * 0.005, y, z + Math.cos(ry) * 0.005);
        scr.rotation.y = ry;
        parent.add(scr);
        BI.Lighting.addPoint({ pos: new THREE.Vector3(x + Math.sin(ry) * 0.5, y, z + Math.cos(ry) * 0.5), color: 0x7fd8a0, intensity: 0.35, distance: 3, zone: 'bunker', group: 'screen', priority: 0.3 });
        return scr;
      };
      const desk = (B, x, z, w, d, ry) => {
        // ry 0: long along x
        const hw = ry ? d / 2 : w / 2, hd = ry ? w / 2 : d / 2;
        box(B, 'deskTop', x - hw, BF + 0.74, z - hd, x + hw, BF + 0.78, z + hd, false);
        for (const sx of [-1, 1]) for (const sz of [-1, 1]) box(B, 'metalDark', x + sx * (hw - 0.06) - 0.03, BF, z + sz * (hd - 0.06) - 0.03, x + sx * (hw - 0.06) + 0.03, BF + 0.74, z + sz * (hd - 0.06) + 0.03, false);
        box(B, 'locker', x + (ry ? -hw + 0.05 : hw - 0.5), BF, z - hd + 0.05, x + (ry ? hw - 0.05 : hw - 0.05), BF + 0.7, z + (ry ? -hd + 0.5 : hd - 0.05), false);
        P.addBox(x - hw, BF, z - hd, x + hw, BF + 0.78, z + hd, { surface: 'metal', wall: false });
      };
      const chair = (B, x, z, ry) => {
        B.add(new THREE.BoxGeometry(0.46, 0.08, 0.46), M('plasticDark'), { x, y: BF + 0.47, z, ry });
        B.add(new THREE.BoxGeometry(0.44, 0.5, 0.06), M('plasticDark'), { x: x + Math.sin(ry) * 0.22, y: BF + 0.78, z: z + Math.cos(ry) * 0.22, ry });
        B.add(new THREE.CylinderGeometry(0.03, 0.03, 0.42, 6), M('steel'), { x, y: BF + 0.22, z });
        B.add(new THREE.CylinderGeometry(0.25, 0.25, 0.04, 8), M('metalDark'), { x, y: BF + 0.03, z });
      };

      // =========================================================================
      // SECURITY OFFICE
      // =========================================================================
      {
        const r = L.rooms.security;
        const g = mkGroup('security');
        const B = new BI.Geo.StaticBatcher('security');
        BI.Mat.cache.wallSec = BI.Mat.cache.wallSec || (() => { const m = BI.Mat.get('wallPanel').clone(); m.color.setHex(0xaeb9c4); m.name = 'wallSec'; return m; })();
        shell(B, r, { floor: 'floorConcrete', wall: 'wallSec', ceiling: 'ceiling' }, [
          { side: 'zMax', a0: -2, a1: 2, top: 3.6 },
          { side: 'zMin', a0: -1.2, a1: 1.2, top: 2.8 },
        ]);
        doorFrame(B, 'x', 0, -82.2, 1.2, 2.8);
        // dark wainscot band
        const band = BI.Mat.cache.bandSec || (BI.Mat.cache.bandSec = BI.Mat.standard({ color: 0x1e2833, roughness: 0.6 }));
        for (const [x0, z0, x1, z1] of [[-8, -82, -7.97, -70], [7.97, -82, 8, -70], [-8, -82, -1.3, -81.97], [1.3, -82, 8, -81.97], [-8, -70.03, -2, -70], [2, -70.03, 8, -70]]) B.box(x0, BF, z0, x1, BF + 1.0, z1, band);
        // desks with monitors along the east wall (live feed on two of them)
        desk(B, 6.6, -76.6, 3.0, 1.0, 1);
        desk(B, 6.6, -73.2, 3.0, 1.0, 1);
        const feedScreens = [];
        feedScreens.push(monitor(g, B, 6.85, BF + 1.3, -77.4, -Math.PI / 2, true));
        monitor(g, B, 6.85, BF + 1.3, -76.1, -Math.PI / 2 - 0.15, false);
        feedScreens.push(monitor(g, B, 6.85, BF + 1.3, -73.9, -Math.PI / 2 + 0.1, true));
        monitor(g, B, 6.85, BF + 1.3, -72.6, -Math.PI / 2, false);
        monitor(g, B, 7.75, BF + 2.25, -75, -Math.PI / 2, false, 1.1, 0.62);
        chair(B, 5.4, -76.8, Math.PI / 2 + 0.3);
        chair(B, 5.3, -73.0, Math.PI / 2 - 0.2);
        // glass partition
        box(B, 'plasticDark', 4.35, BF, -78, 4.5, BF + 0.95, -72, true);
        B.box(4.39, BF + 0.95, -78, 4.46, BF + 2.3, -72, M('glass'), { castShadow: false });
        box(B, 'steel', 4.33, BF + 2.3, -78.02, 4.52, BF + 2.36, -71.98, false);
        for (const z of [-78, -75, -72]) box(B, 'steel', 4.36, BF + 0.95, z - 0.03, 4.49, BF + 2.3, z + 0.03, false);
        // lockers along the west wall
        for (let i = 0; i < 12; i++) {
          const z = -80.5 + i * 0.55;
          box(B, 'locker', -8, BF, z, -7.45, BF + 2.0, z + 0.52, false);
          B.box(-7.46, BF + 1.6, z + 0.1, -7.44, BF + 1.75, z + 0.42, M('metalDark'));
        }
        P.addBox(-8, BF, -80.5, -7.45, BF + 2.0, -73.9, { surface: 'metal' });
        // checkpoint counter and metal detector by the door
        box(B, 'deskTop', -4.2, BF, -81.6, -1.8, BF + 1.1, -80.9, { surface: 'metal' });
        box(B, 'metalDark', -4.2, BF + 1.1, -81.6, -1.8, BF + 1.14, -80.75, false);
        monitor(g, B, -3.0, BF + 1.42, -81.3, 0, false, 0.45, 0.3);
        for (const s of [-1, 1]) box(B, 'plasticLight', s * 1.05 - 0.12, BF, -81.0, s * 1.05 + 0.12, BF + 2.35, -80.6, true);
        box(B, 'plasticLight', -1.17, BF + 2.35, -81.0, 1.17, BF + 2.55, -80.6, false);
        // table + chairs in the middle
        box(B, 'deskTop', -4.2, BF + 0.72, -77.2, -2.2, BF + 0.76, -76.2, false);
        for (const sx of [-1, 1]) for (const sz of [-1, 1]) box(B, 'steel', -3.2 + sx * 0.9 - 0.025, BF, -76.7 + sz * 0.4 - 0.025, -3.2 + sx * 0.9 + 0.025, BF + 0.72, -76.7 + sz * 0.4 + 0.025, false);
        P.addBox(-4.2, BF, -77.2, -2.2, BF + 0.76, -76.2, { surface: 'metal', wall: false });
        chair(B, -3.8, -75.7, Math.PI);
        chair(B, -2.6, -77.7, 0.2);
        B.add(new THREE.CylinderGeometry(0.04, 0.035, 0.1, 10), M('plasticLight'), { x: -3.5, y: BF + 0.81, z: -76.8 });
        // weapon rack + notice board + facility map
        box(B, 'woodDark', -7.97, BF + 1.0, -73.2, -7.9, BF + 2.2, -71.2, false);
        for (let k = 0; k < 3; k++) B.add(new THREE.BoxGeometry(0.06, 1.0, 0.1), M('gunMetal'), { x: -7.86, y: BF + 1.6, z: -72.8 + k * 0.6, rz: 0.05 });
        sign(g, 'facilitymap', { w: 512, h: 384, bg: '#cfd3cc', border: '#223', text: '', wear: 0.6, keepSize: true }, 1.6, 1.2, -1.0, BF + 1.9, -69.98 - 0.0, Math.PI, 0);
        this.drawFacilityMap();
        sign(g, 'secstencil', { w: 1024, h: 160, text: 'SECURITY CHECKPOINT  B1-S', color: 'rgba(225,225,215,0.8)', font: 'Impact, Arial Black, sans-serif', fontSize: 100, wear: 1.2 }, 4.6, 0.72, 0, BF + 3.05, -81.97, 0);
        sign(g, 'idcheck', { w: 512, h: 256, bg: '#1d2633', text: 'ALL PERSONNEL\nPRESENT ID', color: '#e8e8e0' }, 0.9, 0.45, -3.0, BF + 2.0, -81.97, 0);
        // fixtures
        for (const x of [-4.5, 0, 4.5]) for (const z of [-73, -79]) fixture(B, 'tube', x, BF + 3.5, z, Math.PI / 2, 0xe8f0ff, 1.5, 9, 3.2, 1.4);
        emergencyLamp(B, -7.85, BF + 3.1, -76, Math.PI / 2);
        emergencyLamp(B, 7.85, BF + 3.1, -79.5, -Math.PI / 2);
        // ceiling cable tray + vent
        box(B, 'metalDark', -7, BF + 3.25, -76.15, 7, BF + 3.3, -75.85, false);
        for (let i = 0; i < 4; i++) B.add(new THREE.CylinderGeometry(0.025, 0.025, 14, 6), M('cable'), { x: -0.0, y: BF + 3.33, z: -76.1 + i * 0.07, rz: Math.PI / 2 }, { castShadow: false });
        box(B, 'metalDark', -6, BF + 3.55, -71.2, -4.8, BF + 3.6, -70.6, false);
        // floor hazard strip at the ramp mouth + security camera
        B.box(-2, BF, -70.6, 2, BF + 0.01, -70.2, M('hazard'), { worldUV: 1.5 });
        B.add(BI.Geo.roundedBox(0.3, 0.18, 0.4, 0.05), M('plasticLight'), { x: -7.6, y: BF + 3.3, z: -81.6, ry: 0.7, rx: 0.4 });
        B.build(g);
        this.feedScreens = feedScreens;
      }

      // =========================================================================
      // CORRIDOR
      // =========================================================================
      {
        const r = L.rooms.corridor;
        const g = mkGroup('corridor');
        const B = new BI.Geo.StaticBatcher('corridor');
        const gr = L.rooms.generator;
        shell(B, r, { floor: 'floorConcrete', wall: 'wallPanel', ceiling: 'ceiling' }, [
          { side: 'xMin', a0: -107.2, a1: -104.8, top: 2.8 },
        ], ['zMin', 'zMax']);
        // raise the shared wall where the tall generator room is
        const T = 0.4;
        box(B, 'wallGen', r.minX - T, BF + r.h, gr.minZ - T, r.minX, BF + gr.h + 0.4, gr.maxZ + T, true, 0.3);
        doorFrame(B, 'z', -2.2, -106, 1.2, 2.8);
        doorFrame(B, 'x', 0, -123.8, 1.3, 2.8);
        // floor edge lines
        const lineMat = BI.Mat.cache.lineYellow || (BI.Mat.cache.lineYellow = BI.Mat.standard({ color: 0xb08a22, roughness: 0.6 }));
        for (const s of [-1, 1]) B.box(s * 1.62 - 0.05, BF, r.minZ, s * 1.62 + 0.05, BF + 0.006, r.maxZ, lineMat);
        // pillars (also cover)
        for (const z of [-88, -94, -100, -112, -118]) {
          for (const s of [-1, 1]) {
            box(B, 'concreteDark', s > 0 ? 1.62 : -2, BF, z - 0.32, s > 0 ? 2 : -1.62, BF + r.h, z + 0.32, { surface: 'concrete', wall: false }, 0.3);
            B.box(s > 0 ? 1.6 : -2, BF, z - 0.34, s > 0 ? 2 : -1.6, BF + 0.5, z + 0.34, M('hazard'), { worldUV: 1.5 });
          }
        }
        // ceiling pipes with brackets
        pipeRun(B, 'pipe', 0.1, [[1.35, BF + 3.05, r.maxZ], [1.35, BF + 3.05, r.minZ]]);
        pipeRun(B, 'pipeRed', 0.07, [[1.0, BF + 3.15, r.maxZ], [1.0, BF + 3.15, -103], [1.0, BF + 2.6, -103.5], [1.0, BF + 2.6, -108.5], [1.0, BF + 3.15, -109], [1.0, BF + 3.15, r.minZ]]);
        pipeRun(B, 'pipeYellow', 0.06, [[-1.55, BF + 3.2, r.maxZ], [-1.55, BF + 3.2, -104], [-2.1, BF + 3.2, -104]]);
        for (let z = r.maxZ - 1.5; z > r.minZ; z -= 3) {
          box(B, 'metalDark', 0.75, BF + 3.25, z - 0.03, 1.6, BF + 3.4, z + 0.03, false);
          box(B, 'metalDark', 1.57, BF + 2.9, z - 0.03, 1.62, BF + 3.4, z + 0.03, false);
        }
        // cable tray with cables
        box(B, 'metalDark', -1.2, BF + 3.2, r.minZ, -0.5, BF + 3.23, r.maxZ, false);
        for (let i = 0; i < 5; i++) B.add(new THREE.CylinderGeometry(0.022, 0.022, r.maxZ - r.minZ, 5), M('cable'), { x: -1.12 + i * 0.13, y: BF + 3.26, z: (r.maxZ + r.minZ) / 2, rx: Math.PI / 2 }, { castShadow: false });
        // fire hose cabinet, fuse boxes, vents
        box(B, 'pipeRed', 1.85, BF + 1.0, -91.6, 2.0, BF + 1.9, -90.6, false);
        sign(g, 'firehose', { w: 256, h: 128, bg: '#b3241b', text: 'FIRE HOSE', color: '#fff' }, 0.6, 0.3, 1.84, BF + 2.1, -91.1, -Math.PI / 2);
        for (const z of [-97, -115]) box(B, 'olivePaint', -2.0, BF + 1.4, z - 0.35, -1.85, BF + 2.2, z + 0.35, false);
        for (const z of [-90, -110]) {
          B.box(1.97, BF + 0.2, z - 0.5, 2.0, BF + 0.7, z + 0.5, M('metalDark'));
          for (let k = 0; k < 5; k++) B.box(1.94, BF + 0.25 + k * 0.1, z - 0.48, 1.97, BF + 0.29 + k * 0.1, z + 0.48, M('steel'));
        }
        // signage
        sign(g, 'genarrow', { w: 512, h: 128, bg: '#d9a521', text: 'GENERATOR', color: '#111', fontSize: 64, arrow: 'left', arrowX: 0.12, align: 'center' }, 1.1, 0.28, 1.98, BF + 2.2, -101, -Math.PI / 2);
        sign(g, 'labarrow', { w: 512, h: 128, bg: '#e6e2d6', text: 'LABORATORY  ▼', color: '#111', fontSize: 58 }, 1.2, 0.3, -1.98, BF + 2.2, -112, Math.PI / 2);
        sign(g, 'stencilC', { w: 512, h: 256, text: 'B1-C', color: 'rgba(220,220,210,0.7)', font: 'Impact, Arial Black, sans-serif', fontSize: 190, wear: 1.5 }, 1.4, 0.7, -1.98, BF + 2.0, -86, Math.PI / 2);
        sign(g, 'stencilGEN', { w: 512, h: 256, text: 'GEN', color: 'rgba(217,165,33,0.85)', font: 'Impact, Arial Black, sans-serif', fontSize: 200, wear: 1.5 }, 1.0, 0.5, -2.42, BF + 3.25, -106, -Math.PI / 2);
        sign(g, 'labdoor', { w: 512, h: 320, bg: '#e6e2d6', border: '#111', header: 'BIOHAZARD', text: 'LAB-01  CONTAINMENT\nMAGNETIC LOCK', icon: 'bio', headerBg: '#d9a521', headerColor: '#111' }, 0.95, 0.6, 1.9, BF + 1.8, -123.58, 0);
        // puddles + leaking pipe
        puddle(g, 0.4, -92, 2.2, 0.3);
        puddle(g, -0.8, -103.5, 1.6, 1.2);
        puddle(g, 0.6, -115, 2.6, 2.0);
        BI.FX.addEmitter({ pos: new THREE.Vector3(0.95, BF + 3.05, -115.2), kind: 'drip', rate: 1.6, range: 25 });
        // fixtures every 4 m
        for (let z = -85; z > -122; z -= 4) fixture(B, 'tube', 0, BF + 3.3, z, Math.PI / 2, 0xe2ecff, 1.35, 8.5, 3.0, 1.25);
        for (const [x, z, ry] of [[1.9, -86, -Math.PI / 2], [-1.9, -95, Math.PI / 2], [1.9, -104, -Math.PI / 2], [-1.9, -114, Math.PI / 2], [1.9, -121, -Math.PI / 2]]) emergencyLamp(B, x, BF + 3.0, z, ry);
        alarm(g, B, 0, BF + 3.32, -98, false);
        alarm(g, B, 0, BF + 3.32, -117, false);
        B.build(g);
      }

      // =========================================================================
      // GENERATOR ROOM
      // =========================================================================
      {
        const r = L.rooms.generator;
        const g = mkGroup('generator');
        const B = new BI.Geo.StaticBatcher('generatorRoom');
        shell(B, r, { floor: 'concreteDark', wall: 'wallGen', ceiling: 'ceiling', floorUV: 0.25 }, [], ['xMax']);
        // grating around the generator + plinth
        const gx = L.generator.x, gz = L.generator.z;
        B.box(gx - 5.2, BF, gz - 3.6, gx + 5.2, BF + 0.02, gz + 3.6, M('grate'), { worldUV: 0.6 });
        P.addBox(gx - 5.2, BF - 0.2, gz - 3.6, gx + 5.2, BF + 0.02, gz + 3.6, { surface: 'metal', wall: false });
        box(B, 'concreteDark', gx - 3.6, BF, gz - 2.0, gx + 3.6, BF + 0.3, gz + 2.0, { surface: 'concrete' }, 0.3);
        for (const s of [-1, 1]) B.box(gx - 3.65, BF + 0.02, gz + s * 2.02 - 0.06, gx + 3.65, BF + 0.31, gz + s * 2.02 + 0.06, M('hazard'), { worldUV: 1.5 });
        // the hero generator model
        this.generator = BI.Models.buildGenerator();
        this.generator.group.position.set(gx, BF + 0.3, gz);
        g.add(this.generator.group);
        P.addBox(gx - 3.2, BF, gz - 1.6, gx + 3.2, BF + 3.6, gz + 1.6, { surface: 'metal', tag: 'generator' });
        // control console in front of it (interaction point)
        const pc = L.generator.panel;
        box(B, 'olivePaint', pc.x - 0.45, BF, pc.z - 0.9, pc.x + 0.3, BF + 1.05, pc.z + 0.9, { surface: 'metal' });
        B.add(new THREE.BoxGeometry(0.6, 0.06, 1.8), M('metalDark'), { x: pc.x - 0.05, y: BF + 1.12, z: pc.z, rz: 0.35 });
        this.consoleLeds = [];
        // fuel tanks along the west wall
        for (const tz of [-102, -110]) {
          B.add(new THREE.CylinderGeometry(1.0, 1.0, 4.2, 20), M('olivePaint'), { x: -18.6, y: BF + 1.35, z: tz, rx: Math.PI / 2 });
          for (const dz of [-1.4, 1.4]) box(B, 'metalDark', -19.5, BF, tz + dz - 0.12, -17.7, BF + 0.6, tz + dz + 0.12, false);
          P.addBox(-19.7, BF, tz - 2.15, -17.5, BF + 2.4, tz + 2.15, { surface: 'metal', wall: false });
          pipeRun(B, 'pipeYellow', 0.08, [[-17.7, BF + 1.4, tz], [-15.5, BF + 1.4, tz], [-15.5, BF + 0.9, gz + (tz < gz ? -1.0 : 1.0)], [gx - 3.0, BF + 0.9, gz + (tz < gz ? -1.0 : 1.0)]]);
        }
        sign(g, 'diesel', { w: 512, h: 160, bg: '#d9a521', text: 'DIESEL  FLAMMABLE', color: '#111', fontSize: 64 }, 1.6, 0.5, -17.58, BF + 1.4, -102, Math.PI / 2);
        // electrical cabinets with LEDs along the north wall (zMax)
        for (let i = 0; i < 6; i++) {
          const x = -18 + i * 1.3;
          box(B, 'locker', x, BF, r.maxZ - 0.7, x + 1.2, BF + 2.2, r.maxZ, { surface: 'metal' });
          box(B, 'metalDark', x + 0.15, BF + 1.4, r.maxZ - 0.72, x + 1.05, BF + 2.0, r.maxZ - 0.7, false);
          for (let k = 0; k < 3; k++) this.leds.push(new THREE.Vector3(x + 0.3 + k * 0.3, BF + 1.85, r.maxZ - 0.73));
        }
        // exhaust/ceiling pipes from generator to the walls
        pipeRun(B, 'pipe', 0.22, [[gx + 1.5, BF + 4.2, gz], [gx + 1.5, BF + 5.6, gz], [gx + 1.5, BF + 5.6, r.minZ + 0.6], [gx + 1.5, BF + r.h, r.minZ + 0.6]]);
        pipeRun(B, 'pipe', 0.16, [[r.minX, BF + 5.0, -99], [-6, BF + 5.0, -99], [-6, BF + 5.0, -97], [r.maxX, BF + 5.0, -97]]);
        pipeRun(B, 'pipeRed', 0.1, [[r.minX, BF + 4.6, -113], [r.maxX, BF + 4.6, -113]]);
        // catwalk along the south wall (zMin) + stairs (step height 0.3)
        const cwY = BF + 3.0;
        box(B, 'grate', -19.6, cwY - 0.15, r.minZ, -6.0, cwY, r.minZ + 2.4, { surface: 'metal' }, 0.6);
        for (let x = -19; x <= -6.5; x += 3) box(B, 'metalDark', x - 0.06, BF, r.minZ + 2.3, x + 0.06, cwY - 0.15, r.minZ + 2.42, { surface: 'metal' });
        box(B, 'steel', -19.6, cwY + 0.95, r.minZ + 2.36, -6.0, cwY + 1.0, r.minZ + 2.42, false);
        P.addBox(-19.6, cwY, r.minZ + 2.35, -6.0, cwY + 1.0, r.minZ + 2.45, { surface: 'metal', sight: false, bullets: false, wall: false });
        for (let x = -19.4; x <= -6; x += 1.5) box(B, 'steel', x - 0.02, cwY, r.minZ + 2.37, x + 0.02, cwY + 0.95, r.minZ + 2.41, false);
        for (let i = 0; i < 10; i++) {
          const x1 = -2.9 - 0.31 * i, x0 = x1 - 0.31;
          box(B, 'diamondPlate', x0, BF, r.minZ, x1, BF + 0.3 * (i + 1), r.minZ + 1.4, { surface: 'metal' }, 0.6);
        }
        box(B, 'steel', -6.0, BF + 0.95, r.minZ + 1.38, -2.9, cwY + 1.0, r.minZ + 1.44, false);
        P.addBox(-6.0, BF, r.minZ + 1.4, -2.9, cwY + 1.0, r.minZ + 1.45, { surface: 'metal', sight: false, bullets: false, wall: false, nav: true });
        // wall fan (rotates until the blackout)
        const fanG = new THREE.Group();
        fanG.position.set(r.minX + 0.25, BF + 5.0, -106);
        fanG.rotation.y = Math.PI / 2;
        const ring = new THREE.Mesh(new THREE.TorusGeometry(1.0, 0.08, 8, 24), M('metalDark'));
        fanG.add(ring);
        const blades = new THREE.Group();
        for (let k = 0; k < 5; k++) {
          const bl = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.9, 0.03), M('steel'));
          bl.position.y = 0.45;
          const piv = new THREE.Group();
          piv.rotation.z = (k / 5) * Math.PI * 2;
          bl.rotation.y = 0.4;
          piv.add(bl);
          blades.add(piv);
        }
        fanG.add(blades);
        const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.15, 0.15, 0.2, 10), M('metalDark'));
        hub.rotation.x = Math.PI / 2;
        fanG.add(hub);
        g.add(fanG);
        this.fans.push({ blades, speed: 6 });
        BI.Mat.cache.fanBack = BI.Mat.cache.fanBack || new THREE.MeshBasicMaterial({ color: 0x050505 });
        const back = new THREE.Mesh(new THREE.CircleGeometry(1.0, 20), BI.Mat.cache.fanBack);
        back.position.set(r.minX + 0.02, BF + 5.0, -106);
        back.rotation.y = Math.PI / 2;
        g.add(back);
        // signs
        sign(g, 'voltage2', { w: 512, h: 320, bg: '#e8b52a', border: '#111', header: 'DANGER', text: 'HIGH\nVOLTAGE', icon: 'bolt', headerBg: '#111', headerColor: '#e8b52a' }, 1.0, 0.62, -11, BF + 2.8, r.maxZ - 0.73, Math.PI);
        sign(g, 'gen1', { w: 1024, h: 200, text: 'GENERATOR G-1', color: 'rgba(217,165,33,0.9)', font: 'Impact, Arial Black, sans-serif', fontSize: 140, wear: 1.2 }, 5.0, 1.0, r.minX + 0.02, BF + 3.6, -106, Math.PI / 2);
        sign(g, 'engonly', { w: 512, h: 256, bg: '#1d2633', text: 'AUTHORISED\nENGINEERS ONLY', color: '#e8e8e0' }, 0.9, 0.45, -2.62, BF + 2.0, -103.3, -Math.PI / 2);
        sign(g, 'keepclear', { w: 1024, h: 256, text: 'KEEP CLEAR', color: 'rgba(230,200,60,0.85)', font: 'Impact, Arial Black, sans-serif', fontSize: 200, wear: 1.6 }, 3.2, 0.8, gx, BF + 0.03, gz + 3.0, 0, -Math.PI / 2);
        // high-bay sodium lamps
        for (const x of [-16, -11, -6]) for (const z of [-100, -112]) fixture(B, 'sodium', x, BF + 5.8, z, 0, 0xffa555, 3.0, 14, 5.5, 2.6);
        emergencyLamp(B, -19.85, BF + 4.0, -99, Math.PI / 2);
        emergencyLamp(B, -19.85, BF + 4.0, -112, Math.PI / 2);
        emergencyLamp(B, -9, BF + 4.0, r.maxZ - 0.05, Math.PI);
        alarm(g, B, -11, BF + 6.3, -106, false);
        this.genLight = BI.Lighting.addPoint({ pos: new THREE.Vector3(gx, BF + 2.6, gz + 1.9), color: 0x46d6ff, intensity: 1.1, distance: 8, decay: 1.5, zone: 'bunker', group: 'core', priority: 2.5 });
        B.build(g);
      }

      // =========================================================================
      // LABORATORY
      // =========================================================================
      {
        const r = L.rooms.lab;
        const g = mkGroup('lab');
        const B = new BI.Geo.StaticBatcher('lab');
        shell(B, r, { floor: 'tiles', wall: 'wallLab', ceiling: 'ceiling', floorUV: 0.25 }, [{ side: 'zMax', a0: -1.3, a1: 1.3, top: 2.8 }]);
        // decontamination stripes at the door
        B.box(-1.6, BF, -125.0, 1.6, BF + 0.008, -124.2, M('hazard'), { worldUV: 1.5 });
        // glass containment chamber
        const cx = L.cure.x, cz = L.cure.z, R = 1.7;
        const gap = 1.25; // opening (radians) facing the door (+z)
        // three's cylinder angle 0 points at +z, so start after half the gap
        const glassGeo = new THREE.CylinderGeometry(R, R, 2.9, 40, 1, true, gap / 2, Math.PI * 2 - gap);
        const glass = new THREE.Mesh(glassGeo, M('glass'));
        glass.position.set(cx, BF + 0.35 + 1.45, cz);
        glass.renderOrder = 3;
        g.add(glass);
        box(B, 'steel', cx - R - 0.2, BF, cz - R - 0.2, cx + R + 0.2, BF + 0.35, cz + R + 0.2, false);
        P.addBox(cx - R - 0.2, BF, cz - R - 0.2, cx + R + 0.2, BF + 0.35, cz + R + 0.2, { surface: 'metal', wall: false });
        B.add(new THREE.CylinderGeometry(R + 0.15, R + 0.15, 0.3, 40), M('steel'), { x: cx, y: BF + 3.4, z: cz });
        B.add(new THREE.CylinderGeometry(0.12, 0.12, r.h - 3.5, 8), M('steel'), { x: cx, y: BF + 3.55 + (r.h - 3.5) / 2, z: cz });
        const ringGlow = new THREE.Mesh(new THREE.TorusGeometry(R - 0.05, 0.04, 6, 48), M('cyanGlow'));
        ringGlow.rotation.x = Math.PI / 2;
        ringGlow.position.set(cx, BF + 3.22, cz);
        g.add(ringGlow);
        // glass colliders around the arc except the opening
        for (let k = 0; k < 16; k++) {
          const a = (k / 16) * Math.PI * 2;
          const off = Math.atan2(Math.sin(a), Math.cos(a)); // angle from +z measured via (sin, cos)
          if (Math.abs(off) < gap / 2 + 0.12) continue;
          P.addCyl(cx + Math.sin(a) * R, cz + Math.cos(a) * R, 0.2, BF + 0.35, BF + 3.3, { surface: 'glass', wall: false, sight: false });
        }
        // pedestal (the cure model is created by the interaction system)
        B.add(new THREE.CylinderGeometry(0.28, 0.36, 1.0, 16), M('labWhite'), { x: cx, y: BF + 0.85, z: cz });
        B.add(new THREE.CylinderGeometry(0.34, 0.34, 0.05, 16), M('steel'), { x: cx, y: BF + 1.37, z: cz });
        P.addCyl(cx, cz, 0.3, BF, BF + 1.35, { surface: 'metal', wall: false, nav: false });
        this.chamberLight = BI.Lighting.addSpot({ pos: new THREE.Vector3(cx, BF + 3.3, cz), target: new THREE.Vector3(cx, BF, cz), color: 0x9ff4ff, intensity: 1.5, distance: 6, angle: 0.55, penumbra: 0.6, decay: 1.2, zone: 'bunker', group: 'chamber', priority: 3 });
        this.cureLight = BI.Lighting.addPoint({ pos: new THREE.Vector3(cx, BF + 1.7, cz), color: 0x47d1d6, intensity: 1.0, distance: 6, decay: 1.5, zone: 'bunker', group: 'cure', priority: 2.5 });
        this.chamberShaft = BI.FX.makeCone({ radius: 1.4, height: 2.9, color: 0x7fe9ff, intensity: 0.1 });
        this.chamberShaft.position.set(cx, BF + 3.25, cz);
        this.chamberShaft.lookAt(cx, BF, cz);
        g.add(this.chamberShaft);
        // lab benches (also cover)
        const bench = (x, z) => {
          box(B, 'labWhite', x - 1.7, BF, z - 0.45, x + 1.7, BF + 0.88, z + 0.45, { surface: 'metal' });
          box(B, 'plasticDark', x - 1.75, BF + 0.88, z - 0.5, x + 1.75, BF + 0.93, z + 0.5, false);
          // equipment
          B.add(new THREE.CylinderGeometry(0.05, 0.05, 0.22, 10), M('glass'), { x: x - 1.2, y: BF + 1.05, z: z + 0.1 }, { castShadow: false });
          B.add(new THREE.CylinderGeometry(0.07, 0.04, 0.18, 10), M('glass'), { x: x - 1.0, y: BF + 1.02, z: z - 0.15 }, { castShadow: false });
          B.add(BI.Geo.roundedBox(0.4, 0.3, 0.4, 0.06), M('labWhite'), { x: x + 0.9, y: BF + 1.08, z });
          B.add(new THREE.CylinderGeometry(0.12, 0.14, 0.12, 14), M('plasticDark'), { x: x + 0.9, y: BF + 1.29, z });
          // microscope
          B.add(new THREE.BoxGeometry(0.18, 0.04, 0.24), M('plasticDark'), { x: x - 0.1, y: BF + 0.95, z });
          B.add(new THREE.BoxGeometry(0.05, 0.3, 0.05), M('plasticDark'), { x: x - 0.1, y: BF + 1.1, z: z - 0.08 });
          B.add(new THREE.CylinderGeometry(0.03, 0.03, 0.2, 8), M('steel'), { x: x - 0.1, y: BF + 1.22, z: z - 0.02, rx: 0.6 });
        };
        bench(-6.5, -129.5); bench(6.5, -129.5); bench(-6.5, -136.5); bench(6.5, -136.5);
        // specimen fridges along the east wall
        for (let i = 0; i < 3; i++) {
          const z = -127.5 - i * 1.4;
          box(B, 'labWhite', r.maxX - 0.8, BF, z - 0.65, r.maxX, BF + 2.1, z + 0.65, { surface: 'metal' });
          B.box(r.maxX - 0.82, BF + 0.15, z - 0.55, r.maxX - 0.8, BF + 1.95, z + 0.55, M('cyanGlow'));
        }
        // shelves with jars (west wall)
        const jarMats = [M('cyanGlow'), M('amberGlow'), M('greenLed')];
        for (let s = 0; s < 3; s++) {
          box(B, 'steel', r.minX, BF + 0.9 + s * 0.6, -133, r.minX + 0.45, BF + 0.93 + s * 0.6, -128, false);
          for (let k = 0; k < 9; k++) {
            B.add(new THREE.CylinderGeometry(0.07, 0.07, 0.22, 8), jarMats[(k + s) % 3], { x: r.minX + 0.22, y: BF + 1.05 + s * 0.6, z: -132.6 + k * 0.52 }, { castShadow: false });
          }
        }
        // fume hood (west wall, south part)
        box(B, 'labWhite', r.minX, BF, -140.5, r.minX + 1.0, BF + 0.9, -137.5, { surface: 'metal' });
        box(B, 'labWhite', r.minX, BF + 2.0, -140.5, r.minX + 1.0, BF + 2.6, -137.5, false);
        B.box(r.minX + 0.95, BF + 0.9, -140.5, r.minX + 1.0, BF + 2.0, -137.5, M('glass'), { castShadow: false });
        // commander's console + wall screen
        box(B, 'deskTop', 4.2, BF, -141.6, 8.6, BF + 0.85, -140.6, { surface: 'metal' });
        monitor(g, B, 5.0, BF + 1.25, -141.0, 0, false);
        monitor(g, B, 6.4, BF + 1.25, -141.0, 0, false);
        monitor(g, B, 7.8, BF + 1.25, -141.0, -0.2, false);
        const map = new THREE.Mesh(new THREE.PlaneGeometry(3.4, 1.9), new THREE.MeshBasicMaterial({ map: this.drawOutbreakMap(), color: new THREE.Color(1.6, 1.6, 1.6) }));
        map.position.set(6.4, BF + 2.6, -141.96);
        g.add(map);
        this.outbreakMap = map;
        sign(g, 'labstencil', { w: 1024, h: 200, text: 'LAB-01  //  PROJECT LAZARUS', color: 'rgba(40,60,70,0.85)', font: 'Impact, Arial Black, sans-serif', fontSize: 110, wear: 0.6 }, 5.0, 1.0, -4.0, BF + 3.1, -141.98, 0);
        sign(g, 'bio2', { w: 512, h: 320, bg: '#e6e2d6', border: '#111', header: 'BIOHAZARD', text: 'LEVEL 4\nCONTAINMENT', icon: 'bio', headerBg: '#d9a521', headerColor: '#111' }, 0.95, 0.6, 1.8, BF + 1.8, -124.02, Math.PI);
        // panel lights
        for (const x of [-5, 0, 5]) for (const z of [-128, -138]) {
          if (x === 0 && z === -138) continue;
          fixture(B, 'panel', x, BF + 3.95, z, 0, 0xe4f6ff, 1.3, 9, 3.6, 1.2);
        }
        emergencyLamp(B, -9.85, BF + 3.4, -130, Math.PI / 2);
        emergencyLamp(B, 9.85, BF + 3.4, -137, -Math.PI / 2);
        emergencyLamp(B, 0, BF + 3.4, -141.85, 0);
        B.build(g);
      }

      // ramp fixtures (built by the exterior file, lit and switched here)
      {
        const g = mkGroup('rampfx');
        const B = new BI.Geo.StaticBatcher('rampfx');
        for (const p of BI.BunkerExterior.rampLightSpots || []) fixture(B, 'tube', p.x, p.y, p.z, 0, 0xe2ecff, 1.2, 8, 0, 0);
        emergencyLamp(B, 1.95, 7.0, -60, -Math.PI / 2);
        B.build(g);
      }

      // finalise instancing
      for (const im of [this.tubes, this.panels, this.bulbs, this.emLamps, this.shafts]) {
        im.instanceMatrix.needsUpdate = true;
        if (im.instanceColor) im.instanceColor.needsUpdate = true;
      }
      // a couple of faulty tubes that flicker on their own
      for (const f of this.fixtures) {
        if (f.kind === 'tube' && Math.abs(f.pos.z - -93) < 1.5) f.faulty = true;
        if (f.kind === 'tube' && Math.abs(f.pos.x - 4.5) < 0.1 && Math.abs(f.pos.z + 79) < 0.1) f.faulty = true;
      }
      this.signs = signs;

      // live camera feed for two security monitors
      this.feedRT = new THREE.WebGLRenderTarget(256, 144, { minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter });
      this.feedCam = new THREE.PerspectiveCamera(70, 256 / 144, 0.1, 60);
      this.feedCam.position.set(1.6, BF + 3.1, -84);
      this.feedCam.lookAt(-0.5, BF + 0.6, -100);
      this.feedMat.uniforms.tFeed.value = this.feedRT.texture;
      this.feedTimer = 0;

      BI.Events.on('generator:disabled', (e) => this.startBlackout(e));
      BI.Engine.addUpdate((dt, t) => this.update(dt, t), 84);
      return this.groups;
    },

    drawFacilityMap() {
      const tex = BI.Tex.decal('facilitymap', {});
      const c = tex.image;
      const ctx = c.getContext('2d');
      ctx.fillStyle = '#cfd3cc';
      ctx.fillRect(0, 0, c.width, c.height);
      ctx.strokeStyle = '#223';
      ctx.lineWidth = 8;
      ctx.strokeRect(4, 4, c.width - 8, c.height - 8);
      ctx.fillStyle = '#223';
      ctx.font = 'bold 30px Arial, sans-serif';
      ctx.fillText('B1 FACILITY PLAN', 24, 44);
      const map = (x, z) => [256 + x * 10, 60 + (-70 - z) * 4.2];
      ctx.lineWidth = 3;
      const rect = (r, label, color) => {
        const a = map(r.minX, r.maxZ), b = map(r.maxX, r.minZ);
        ctx.fillStyle = color;
        ctx.fillRect(a[0], a[1], b[0] - a[0], b[1] - a[1]);
        ctx.strokeRect(a[0], a[1], b[0] - a[0], b[1] - a[1]);
        ctx.fillStyle = '#111';
        ctx.font = 'bold 15px Arial, sans-serif';
        ctx.fillText(label, a[0] + 4, a[1] + 18);
      };
      const R = L.rooms;
      rect(R.security, 'SECURITY', '#b9c4cf');
      rect(R.corridor, '', '#d9d9d0');
      rect(R.generator, 'GENERATOR', '#d9c08a');
      rect(R.lab, 'LAB-01', '#a9d8da');
      ctx.fillStyle = '#b3241b';
      ctx.beginPath();
      ctx.arc(map(0, -76)[0], map(0, -76)[1], 7, 0, Math.PI * 2);
      ctx.fill();
      ctx.font = 'bold 13px Arial, sans-serif';
      ctx.fillText('YOU ARE HERE', map(0, -76)[0] + 10, map(0, -76)[1] + 5);
      tex.needsUpdate = true;
    },

    drawOutbreakMap() {
      const c = BI.Tex.makeCanvas(512, 288);
      const ctx = c.getContext('2d');
      ctx.fillStyle = '#04121a';
      ctx.fillRect(0, 0, 512, 288);
      ctx.strokeStyle = 'rgba(71,209,214,0.18)';
      for (let x = 0; x < 512; x += 32) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, 288); ctx.stroke(); }
      for (let y = 0; y < 288; y += 32) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(512, y); ctx.stroke(); }
      // abstract continents from noise blobs
      const r = U.rng(5);
      ctx.fillStyle = 'rgba(71,209,214,0.35)';
      for (let i = 0; i < 260; i++) {
        const x = r() * 512, y = 40 + r() * 210;
        if (U.fbm2(x * 0.012, y * 0.02, 3, 9) < 0.5) continue;
        ctx.beginPath();
        ctx.arc(x, y, 6 + r() * 10, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.fillStyle = 'rgba(255,50,30,0.75)';
      for (let i = 0; i < 40; i++) {
        const x = r() * 512, y = 50 + r() * 190;
        ctx.beginPath();
        ctx.arc(x, y, 2 + r() * 7, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.fillStyle = '#47d1d6';
      ctx.font = 'bold 18px Consolas, monospace';
      ctx.fillText('OUTBREAK MONITOR  —  STRAIN K-7', 14, 24);
      ctx.fillStyle = '#ff5a40';
      ctx.fillText('INFECTED: 41,882,109', 14, 276);
      ctx.fillStyle = '#47d1d6';
      ctx.fillText('CURE STOCK: 1 (LAB-01)', 290, 276);
      const t = new THREE.CanvasTexture(c);
      t.encoding = THREE.sRGBEncoding;
      return t;
    },

    // -------------------------------------------------------------------------
    // Blackout cascade
    // -------------------------------------------------------------------------
    startBlackout(e) {
      if (this.blackoutTime >= 0) return;
      const origin = e && e.pos ? e.pos : new THREE.Vector3(L.generator.x, BF + 2, L.generator.z);
      this.blackoutTime = BI.Engine.time;
      for (const f of this.fixtures) {
        const d = f.pos.distanceTo(origin);
        f.dieAt = this.blackoutTime + 0.35 + d * 0.028 + Math.random() * 0.12;
        f.state = 'dying';
      }
      this.cascadeEnd = Math.max(...this.fixtures.map((f) => f.dieAt)) + 0.5;
      this.genLight.mul = 1;
      for (const fn of this.fans) fn.target = 0;
      BI.Events.emit('lights:cascade', { duration: this.cascadeEnd - this.blackoutTime });
    },

    update(dt, t) {
      const setC = (im, i, r, g, b) => im.setColorAt(i, Interior._c.setRGB(r, g, b));
      let tubesDirty = false;
      const now = BI.Engine.time;
      const blackout = this.blackoutTime >= 0;

      for (const f of this.fixtures) {
        let out = 1;
        if (f.state === 'dying') {
          const dt2 = now - f.dieAt;
          if (dt2 > 0) {
            f.state = 'off';
            BI.FX.sparks(f.pos, { x: 0, y: -1, z: 0 }, 10, { speed: 3, floor: BF });
            BI.Events.emit('fx:lightpop', { pos: f.pos.clone() });
            out = 0;
          } else if (dt2 > -0.3) {
            out = Math.random() < 0.5 ? 1.4 : 0.05; // strobe before dying
          }
        } else if (f.state === 'off') {
          out = 0;
        } else if (f.faulty) {
          const ph = (t + f.seed) % 7;
          if (ph < 0.6) out = Math.random() < 0.55 ? 1 : 0.04;
          else if (ph < 0.75) out = 0.05;
        } else {
          out = 0.97 + Math.sin(t * 50 + f.seed) * 0.02;
        }
        if (out !== f.out || f.state === 'on' || f.state === 'dying') {
          f.out = out;
          const k = f.base * out;
          const c = f.color;
          for (const i of f.idx) setC(f.im, i, c.r * k, c.g * k, c.b * k);
          if (f.shaftIdx >= 0) { const sk = out * (f.kind === 'sodium' ? 1.5 : 1); setC(this.shafts, f.shaftIdx, c.r * sk, c.g * sk, c.b * sk); }
          f.light.mul = out;
          tubesDirty = true;
        }
      }
      // emergency lamps + alarms after the cascade
      const em = blackout ? U.clamp01((now - (this.cascadeEnd || 0)) / 1.2) : 0;
      if (this._emLast !== em || blackout) {
        const pulse = 0.75 + 0.25 * Math.sin(t * 3.2);
        for (const e of this.emergency) {
          const k = em * pulse;
          setC(this.emLamps, e.idx, 5 * k + 0.05, 0.35 * k + 0.02, 0.2 * k + 0.02);
          e.light.intensity = 2.2 * em;
          e.light.mul = pulse;
        }
        this._emLast = em;
        tubesDirty = true;
      }
      if (!this._emInit) {
        for (const e of this.emergency) setC(this.emLamps, e.idx, 0.12, 0.02, 0.02);
        this._emInit = true;
      }
      for (const a of this.alarms) {
        if (em > 0) {
          a.spinner.rotation.y += dt * 3.6;
          a.cone.material.uniforms.intensity.value = 0.55 * em;
          a.domeMat.emissiveIntensity = 2.5 + Math.sin(t * 7.2) * 0.8;
          a.light.intensity = 9 * em;
          const ang = a.spinner.rotation.y;
          a.light.target.set(a.x + Math.sin(ang) * 6, a.y - 1.6, a.z + Math.cos(ang) * 6);
        }
      }
      if (tubesDirty) {
        for (const im of [this.tubes, this.panels, this.bulbs, this.emLamps, this.shafts]) if (im.instanceColor) im.instanceColor.needsUpdate = true;
      }
      if (blackout && now > (this.cascadeEnd || 0) && !this.blackoutDone) {
        this.blackoutDone = true;
        BI.Events.emit('lights:emergency', {});
      }
      // generator core light dims with the generator
      if (this.generator) {
        this.genLight.mul = this.generator.coreLevel;
        this.generator.update(dt, t);
      }
      // fans spin down
      for (const fn of this.fans) {
        if (fn.target !== undefined) fn.speed = U.damp(fn.speed, fn.target, 0.6, dt);
        fn.blades.rotation.z += fn.speed * dt;
      }
      // screens
      const power = blackout ? Math.max(0, 1 - (now - this.blackoutTime) * 2.5) : 1;
      this.screenMat.uniforms.time.value = t;
      this.screenMat.uniforms.power.value = power;
      this.feedMat.uniforms.time.value = t;
      this.feedMat.uniforms.power.value = power;
      this.outbreakMap.material.color.setScalar(1.6 * Math.max(power, 0.0));
      this.shaftMat.uniforms.time.value = t;
      this.shaftMat.uniforms.fogDensity.value = BI.Engine.scene.fog.density;
      this.shaftMat.uniforms.color.value.setRGB(1, 1, 1);
    },

    // Called by the engine before the main render (medium/high quality only).
    renderFeed(renderer) {
      if (!BI.Engine.settings.feed || !BI.player || BI.player.zone === 'outside') return;
      if (this.blackoutTime >= 0) return;
      this.feedTimer -= BI.Engine.dt;
      if (this.feedTimer > 0) return;
      this.feedTimer = 0.18;
      // only when the security monitors are near the camera
      if (BI.Engine.camera.position.distanceTo(new THREE.Vector3(6.5, BF + 1.3, -75)) > 22) return;
      const cur = renderer.getRenderTarget();
      renderer.setRenderTarget(this.feedRT);
      renderer.clear();
      renderer.render(BI.Engine.scene, this.feedCam);
      renderer.setRenderTarget(cur);
    },

    setVisible(name, v) {
      if (this.groups[name]) this.groups[name].visible = v;
    },
    _c: new THREE.Color(),
  };

  BI.BunkerInterior = Interior;
})();
