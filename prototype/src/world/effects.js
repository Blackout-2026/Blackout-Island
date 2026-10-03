/* =============================================================================
 * world/effects.js — particles, emitters, tracers, decals, shell casings
 * -----------------------------------------------------------------------------
 * Two pooled THREE.Points systems with a small custom shader (per-particle
 * size, colour, alpha, fog):
 *   glow  additive   sparks, muzzle embers, generator spark showers
 *   soft  alpha      smoke, dust, steam
 * Particles are simulated on the CPU (gravity, drag, bounce on the ground)
 * and compacted every frame so only live ones are drawn (one draw call each).
 * Also: pooled bullet tracers, impact decals and physical shell casings.
 * Role: World Builder / Graphics (shared by weapons and the generator).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const vert = [
    'attribute float size; attribute float alpha; attribute vec3 pcolor;',
    'uniform float uScale;',
    'varying vec3 vColor; varying float vAlpha;',
    '#include <fog_pars_vertex>',
    'void main(){',
    '  vColor = pcolor; vAlpha = alpha;',
    '  vec4 mvPosition = modelViewMatrix * vec4(position, 1.0);',
    '  gl_PointSize = size * uScale / max(-mvPosition.z, 0.05);',
    '  gl_Position = projectionMatrix * mvPosition;',
    '  #include <fog_vertex>',
    '}',
  ].join('\n');
  const frag = [
    'uniform sampler2D map;',
    'varying vec3 vColor; varying float vAlpha;',
    '#include <fog_pars_fragment>',
    'void main(){',
    '  vec4 t = texture2D(map, gl_PointCoord);',
    '  gl_FragColor = vec4(vColor * t.rgb, t.a * vAlpha);',
    '  #include <tonemapping_fragment>',
    '  #include <encodings_fragment>',
    '  #include <fog_fragment>',
    '}',
  ].join('\n');

  class ParticlePool {
    constructor(scene, cap, additive, tex) {
      this.cap = cap;
      this.n = 0;
      this.p = []; // particle objects
      const g = new THREE.BufferGeometry();
      this.pos = new Float32Array(cap * 3);
      this.col = new Float32Array(cap * 3);
      this.size = new Float32Array(cap);
      this.alpha = new Float32Array(cap);
      g.setAttribute('position', new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage));
      g.setAttribute('pcolor', new THREE.BufferAttribute(this.col, 3).setUsage(THREE.DynamicDrawUsage));
      g.setAttribute('size', new THREE.BufferAttribute(this.size, 1).setUsage(THREE.DynamicDrawUsage));
      g.setAttribute('alpha', new THREE.BufferAttribute(this.alpha, 1).setUsage(THREE.DynamicDrawUsage));
      g.setDrawRange(0, 0);
      const uniforms = THREE.UniformsUtils.merge([THREE.UniformsLib.fog, { map: { value: tex }, uScale: { value: 400 } }]);
      uniforms.map.value = tex;
      const mat = new THREE.ShaderMaterial({
        uniforms,
        vertexShader: vert,
        fragmentShader: frag,
        transparent: true,
        depthWrite: false,
        fog: true,
        blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending,
      });
      this.points = new THREE.Points(g, mat);
      this.points.frustumCulled = false;
      this.points.renderOrder = additive ? 5 : 4;
      this.geo = g;
      this.mat = mat;
      scene.add(this.points);
    }

    spawn(o) {
      if (this.p.length >= this.cap) this.p.shift();
      this.p.push(o);
      return o;
    }

    update(dt) {
      const live = [];
      for (const q of this.p) {
        q.age += dt;
        if (q.age >= q.life) continue;
        q.vy -= q.g * dt;
        const drag = Math.exp(-q.drag * dt);
        q.vx *= drag; q.vy *= drag; q.vz *= drag;
        q.x += q.vx * dt; q.y += q.vy * dt; q.z += q.vz * dt;
        if (q.bounce && q.y < q.floor) {
          q.y = q.floor;
          q.vy = -q.vy * q.bounce;
          q.vx *= 0.6; q.vz *= 0.6;
        }
        live.push(q);
      }
      this.p = live;
      const n = live.length;
      for (let i = 0; i < n; i++) {
        const q = live[i];
        const t = q.age / q.life;
        this.pos[i * 3] = q.x; this.pos[i * 3 + 1] = q.y; this.pos[i * 3 + 2] = q.z;
        const fade = q.fadeIn ? Math.min(1, t / q.fadeIn) : 1;
        const a = q.a0 * fade * Math.pow(1 - t, q.fadePow || 1);
        const c = q.c0;
        const k = q.c1 ? t : 0;
        this.col[i * 3] = c[0] + (q.c1 ? (q.c1[0] - c[0]) * k : 0);
        this.col[i * 3 + 1] = c[1] + (q.c1 ? (q.c1[1] - c[1]) * k : 0);
        this.col[i * 3 + 2] = c[2] + (q.c1 ? (q.c1[2] - c[2]) * k : 0);
        this.size[i] = q.s0 + (q.s1 - q.s0) * t;
        this.alpha[i] = a;
      }
      this.geo.setDrawRange(0, n);
      for (const k of ['position', 'pcolor', 'size', 'alpha']) {
        const att = this.geo.attributes[k];
        att.updateRange.offset = 0;
        att.updateRange.count = n * att.itemSize;
        att.needsUpdate = true;
      }
    }
  }

  function part(x, y, z, o) {
    return {
      x, y, z,
      vx: o.vx || 0, vy: o.vy || 0, vz: o.vz || 0,
      g: o.g === undefined ? 0 : o.g,
      drag: o.drag || 0,
      life: o.life || 1,
      age: 0,
      s0: o.s0 || 0.1, s1: o.s1 === undefined ? o.s0 || 0.1 : o.s1,
      c0: o.c0 || [1, 1, 1], c1: o.c1 || null,
      a0: o.a0 === undefined ? 1 : o.a0,
      fadeIn: o.fadeIn || 0, fadePow: o.fadePow || 1,
      bounce: o.bounce || 0, floor: o.floor === undefined ? -1e9 : o.floor,
    };
  }

  const FX = {
    glow: null,
    soft: null,
    emitters: [],
    tracers: [],
    decals: [],
    casings: [],

    init(scene) {
      this.scene = scene;
      const glowTex = BI.Tex.get('glow').map;
      this.glow = new ParticlePool(scene, 1600, true, glowTex);
      this.soft = new ParticlePool(scene, 1100, false, glowTex);

      // tracers: stretched additive boxes
      const trMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(4.0, 3.2, 1.8), transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false, fog: true });
      const trGeo = new THREE.BoxGeometry(1, 1, 1);
      trGeo.translate(0, 0, -0.5);
      for (let i = 0; i < 24; i++) {
        const m = new THREE.Mesh(trGeo, trMat.clone());
        m.visible = false;
        m.frustumCulled = false;
        scene.add(m);
        this.tracers.push({ mesh: m, life: 0 });
      }
      // impact decals
      const decMat = BI.Mat.standard({ map: BI.Tex.get('scorch').map, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2, roughness: 1 });
      const decGeo = new THREE.PlaneGeometry(1, 1);
      for (let i = 0; i < 48; i++) {
        const m = new THREE.Mesh(decGeo, decMat);
        m.visible = false;
        scene.add(m);
        this.decals.push(m);
      }
      this.decalIdx = 0;
      // shell casings
      const casGeo = new THREE.CylinderGeometry(0.006, 0.006, 0.024, 6);
      casGeo.rotateZ(Math.PI / 2);
      const casMat = BI.Mat.get('copper');
      for (let i = 0; i < 30; i++) {
        const m = new THREE.Mesh(casGeo, casMat);
        m.visible = false;
        scene.add(m);
        this.casings.push({ mesh: m, v: new THREE.Vector3(), spin: new THREE.Vector3(), life: 0, rested: false });
      }
      this.casIdx = 0;

      BI.Engine.addUpdate((dt) => this.update(dt), 85);
      BI.Events.on('quality:changed', () => this.updateScale());
      window.addEventListener('resize', () => this.updateScale());
      this.updateScale();
    },

    updateScale() {
      const r = BI.Engine.renderer;
      const h = r.getDrawingBufferSize(new THREE.Vector2()).y;
      const s = h / (2 * Math.tan(THREE.MathUtils.degToRad(BI.Engine.camera.fov) / 2));
      this.glow.mat.uniforms.uScale.value = s;
      this.soft.mat.uniforms.uScale.value = s;
    },

    // ---- presets ------------------------------------------------------------------
    sparks(pos, normal, count, opts) {
      opts = opts || {};
      const n = normal || { x: 0, y: 1, z: 0 };
      const floor = opts.floor === undefined ? pos.y - 3 : opts.floor;
      for (let i = 0; i < count; i++) {
        const sp = (opts.speed || 6) * (0.4 + Math.random());
        const vx = n.x * sp * 0.6 + (Math.random() - 0.5) * sp;
        const vy = n.y * sp * 0.6 + (Math.random() - 0.2) * sp;
        const vz = n.z * sp * 0.6 + (Math.random() - 0.5) * sp;
        this.glow.spawn(part(pos.x, pos.y, pos.z, {
          vx, vy, vz, g: 14, drag: 1.5, life: (opts.life || 0.45) * (0.5 + Math.random()),
          s0: opts.size || 0.05, s1: 0.01, c0: opts.color || [3.5, 2.2, 0.9], c1: [1.2, 0.2, 0.05],
          bounce: 0.35, floor,
        }));
      }
    },
    dust(pos, normal, count, color) {
      const n = normal || { x: 0, y: 1, z: 0 };
      const c = color || [0.38, 0.35, 0.3];
      for (let i = 0; i < count; i++) {
        this.soft.spawn(part(pos.x + n.x * 0.05, pos.y + n.y * 0.05, pos.z + n.z * 0.05, {
          vx: n.x * 1.2 + (Math.random() - 0.5) * 1.2, vy: n.y * 1.2 + Math.random() * 0.6, vz: n.z * 1.2 + (Math.random() - 0.5) * 1.2,
          g: -0.3, drag: 3, life: 0.6 + Math.random() * 0.6, s0: 0.12, s1: 0.6, c0: c, a0: 0.55, fadePow: 1.5,
        }));
      }
    },
    smoke(pos, count, opts) {
      opts = opts || {};
      for (let i = 0; i < count; i++) {
        this.soft.spawn(part(pos.x + (Math.random() - 0.5) * (opts.spread || 0.3), pos.y, pos.z + (Math.random() - 0.5) * (opts.spread || 0.3), {
          vx: (Math.random() - 0.5) * 0.4 + (opts.wind || 0), vy: (opts.rise || 1.0) * (0.6 + Math.random() * 0.6), vz: (Math.random() - 0.5) * 0.4,
          g: 0, drag: 0.4, life: (opts.life || 2.5) * (0.7 + Math.random() * 0.6), s0: opts.s0 || 0.3, s1: opts.s1 || 1.6,
          c0: opts.color || [0.12, 0.12, 0.12], a0: opts.alpha || 0.4, fadeIn: 0.15, fadePow: 1.2,
        }));
      }
    },
    flashBurst(pos, color, size) {
      this.glow.spawn(part(pos.x, pos.y, pos.z, { life: 0.07, s0: size || 0.6, s1: (size || 0.6) * 1.4, c0: color || [6, 4, 2], a0: 1 }));
    },

    // Continuous emitter: {pos, kind:'steam'|'sparks'|'smoke', rate, active}
    addEmitter(e) {
      e.acc = 0;
      e.active = e.active !== false;
      this.emitters.push(e);
      return e;
    },

    tracer(from, to) {
      const t = this.tracers.find((q) => q.life <= 0) || this.tracers[0];
      const m = t.mesh;
      const len = from.distanceTo(to);
      m.position.copy(from);
      m.lookAt(to);
      m.scale.set(0.018, 0.018, len);
      m.visible = true;
      t.life = 0.06;
      t.max = 0.06;
    },

    decal(point, normal, size) {
      const m = this.decals[this.decalIdx++ % this.decals.length];
      m.position.set(point.x + normal.x * 0.01, point.y + normal.y * 0.01, point.z + normal.z * 0.01);
      m.lookAt(point.x + normal.x, point.y + normal.y, point.z + normal.z);
      m.rotateZ(Math.random() * 6.28);
      const s = size || 0.12;
      m.scale.set(s, s, s);
      m.visible = true;
    },

    casing(pos, vel) {
      const c = this.casings[this.casIdx++ % this.casings.length];
      c.mesh.position.copy(pos);
      c.v.copy(vel);
      c.spin.set(Math.random() * 20, Math.random() * 20, Math.random() * 20);
      c.life = 3;
      c.rested = false;
      c.bounces = 0;
      c.mesh.visible = true;
    },

    update(dt) {
      // emitters (only near the camera)
      const cam = BI.Engine.camera.position;
      for (const e of this.emitters) {
        if (!e.active || e.pos.distanceTo(cam) > (e.range || 60)) continue;
        e.acc += dt * e.rate;
        while (e.acc >= 1) {
          e.acc -= 1;
          if (e.kind === 'steam') {
            this.soft.spawn(part(e.pos.x + (Math.random() - 0.5) * 0.3, e.pos.y, e.pos.z + (Math.random() - 0.5) * 0.3, {
              vx: (Math.random() - 0.5) * 0.3 + 0.25, vy: 1.4 + Math.random() * 0.8, vz: (Math.random() - 0.5) * 0.3,
              drag: 0.5, life: 2.6 + Math.random(), s0: 0.35, s1: 2.4, c0: e.color || [0.5, 0.52, 0.55], a0: 0.22, fadeIn: 0.2, fadePow: 1.3,
            }));
          } else if (e.kind === 'sparks') {
            this.sparks(e.pos, e.dir || { x: 0, y: -0.5, z: 0 }, e.burst || 6, { speed: 4, floor: e.floor });
          } else if (e.kind === 'smoke') {
            this.smoke(e.pos, 1, { color: e.color, rise: 0.8, life: 3, s1: 2.2, alpha: 0.35 });
          } else if (e.kind === 'drip') {
            this.glow.spawn(part(e.pos.x, e.pos.y, e.pos.z, { g: 9.8, life: 0.4, s0: 0.03, c0: [0.3, 0.35, 0.4], a0: 0.6 }));
          }
        }
      }
      this.glow.update(dt);
      this.soft.update(dt);
      FX.updateCones(BI.Engine.time);

      for (const t of this.tracers) {
        if (t.life > 0) {
          t.life -= dt;
          t.mesh.material.opacity = Math.max(0, t.life / t.max);
          if (t.life <= 0) t.mesh.visible = false;
        }
      }
      for (const c of this.casings) {
        if (c.life <= 0) continue;
        c.life -= dt;
        if (c.life <= 0) { c.mesh.visible = false; continue; }
        if (c.rested) continue;
        c.v.y -= 9.8 * dt;
        c.mesh.position.addScaledVector(c.v, dt);
        c.mesh.rotation.x += c.spin.x * dt;
        c.mesh.rotation.y += c.spin.y * dt;
        const p = c.mesh.position;
        const g = BI.Physics.groundAt(p.x, p.z, p.y + 0.1, 0.1);
        if (p.y < g.y + 0.006) {
          p.y = g.y + 0.006;
          c.v.y = -c.v.y * 0.35;
          c.v.x *= 0.5; c.v.z *= 0.5;
          c.spin.multiplyScalar(0.5);
          if (c.bounces++ === 0) BI.Events.emit('fx:casing', { pos: p.clone(), surface: g.surface });
          if (Math.abs(c.v.y) < 0.4) c.rested = true;
        }
      }
    },
  };

  // ---------------------------------------------------------------------------
  // Faux volumetric light cone / shaft (additive, soft edges, fades in fog).
  // Apex at the origin, extends along +Z: call mesh.lookAt(target).
  // ---------------------------------------------------------------------------
  const coneVert = [
    'varying float vH; varying vec3 vN; varying vec3 vV; varying vec3 vW; varying float vFogDepth;',
    'void main(){',
    '  vH = uv.y;',
    '  vec4 wp = modelMatrix * vec4(position, 1.0);',
    '  vW = wp.xyz;',
    '  vN = normalize(mat3(modelMatrix) * normal);',
    '  vV = normalize(cameraPosition - wp.xyz);',
    '  vec4 mv = viewMatrix * wp; vFogDepth = -mv.z;',
    '  gl_Position = projectionMatrix * mv;',
    '}',
  ].join('\n');
  const coneFrag = [
    'uniform vec3 color; uniform float intensity; uniform float time; uniform float fogDensity;',
    'varying float vH; varying vec3 vN; varying vec3 vV; varying vec3 vW; varying float vFogDepth;',
    'float h2(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }',
    'float nz(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f); return mix(mix(h2(i), h2(i+vec2(1,0)), f.x), mix(h2(i+vec2(0,1)), h2(i+vec2(1,1)), f.x), f.y); }',
    'void main(){',
    '  float edge = pow(abs(dot(normalize(vN), normalize(vV))), 1.6);',
    '  float along = pow(vH, 1.3) * smoothstep(0.0, 0.08, 1.0 - vH);',
    '  float dust = 0.65 + 0.55 * nz(vW.xz * 0.9 + vW.y * 0.7 + vec2(time * 0.15, -time * 0.1));',
    '  float fogF = exp(-fogDensity * fogDensity * vFogDepth * vFogDepth * 0.5);',
    '  gl_FragColor = vec4(color * intensity * edge * along * dust * fogF, 1.0);',
    '}',
  ].join('\n');
  const cones = [];
  FX.makeCone = function (o) {
    const g = new THREE.ConeGeometry(o.radius, o.height, 20, 1, true);
    g.translate(0, -o.height / 2, 0);
    g.rotateX(-Math.PI / 2);
    const mat = new THREE.ShaderMaterial({
      uniforms: {
        color: { value: new THREE.Color(o.color === undefined ? 0xfff0d0 : o.color) },
        intensity: { value: o.intensity === undefined ? 0.25 : o.intensity },
        time: { value: 0 },
        fogDensity: { value: 0.01 },
      },
      vertexShader: coneVert,
      fragmentShader: coneFrag,
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const m = new THREE.Mesh(g, mat);
    m.renderOrder = 6;
    m.userData.baseIntensity = mat.uniforms.intensity.value;
    cones.push(m);
    return m;
  };
  FX.updateCones = function (t) {
    const fd = BI.Engine.scene.fog.density;
    for (const c of cones) {
      c.material.uniforms.time.value = t;
      c.material.uniforms.fogDensity.value = fd;
    }
  };

  BI.FX = FX;
})();
