/* =============================================================================
 * core/engine.js — renderer, cameras, post-processing, quality, main loop
 * -----------------------------------------------------------------------------
 * Rendering pipeline (Medium / High quality):
 *
 *   RenderPass(world scene)        -> HDR half-float target, linear, no tonemap
 *   ViewmodelPass(weapon scene)    -> same target, depth cleared (no wall clip)
 *   UnrealBloomPass                -> bloom on HDR emissives / lights / flashes
 *   CompositePass (our shader)     -> exposure + ACES filmic + sRGB + vignette
 *                                     + film grain + damage overlay
 *   FXAA                           -> anti-aliasing on the final sRGB image
 *
 * three.js r128 applies `renderer.toneMapping` inside every material even when
 * rendering into a render target, so on this path the renderer's tone mapping
 * is set to None and ACES is done once, after bloom, in the composite shader.
 * Low quality skips the composer and lets three apply ACES + sRGB directly.
 * Role: Core Developer (pipeline) / Graphics.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  // ---------------------------------------------------------------------------
  // Quality presets. Every system reads BI.Engine.settings and listens to the
  // 'quality:changed' event to rebuild what it owns.
  // ---------------------------------------------------------------------------
  const QUALITY = {
    low: {
      name: 'low', maxPixelRatio: 0.8, post: false, bloom: false,
      moonShadow: 1024, flashShadow: false, flashShadowSize: 256,
      pointLights: 3, spotLights: 1, grass: 0.35, trees: 0.7, feed: false, shadowEvery: 3,
    },
    medium: {
      name: 'medium', maxPixelRatio: 0.85, post: true, bloom: true, bloomScale: 0.35,
      moonShadow: 2048, flashShadow: true, flashShadowSize: 512,
      pointLights: 4, spotLights: 2, grass: 0.7, trees: 1.0, feed: false, shadowEvery: 2,
    },
    high: {
      name: 'high', maxPixelRatio: 1.25, post: true, bloom: true, bloomScale: 0.5,
      moonShadow: 2048, flashShadow: true, flashShadowSize: 1024,
      pointLights: 8, spotLights: 3, grass: 1.0, trees: 1.0, feed: true, shadowEvery: 1,
    },
  };

  // ---------------------------------------------------------------------------
  // Final composite: exposure, ACES filmic (same fit three.js uses), sRGB,
  // vignette, film grain, damage overlay, low-health desaturation.
  // ---------------------------------------------------------------------------
  const CompositeShader = {
    uniforms: {
      tDiffuse: { value: null },
      exposure: { value: 1.0 },
      vignette: { value: 0.55 },
      grain: { value: 0.035 },
      time: { value: 0 },
      damage: { value: 0 },
      lowHealth: { value: 0 },
      invuln: { value: 0 },
      saturation: { value: 1.0 },
      resolution: { value: new THREE.Vector2(1, 1) },
    },
    vertexShader: [
      'varying vec2 vUv;',
      'void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
    ].join('\n'),
    fragmentShader: [
      'uniform sampler2D tDiffuse;',
      'uniform float exposure, vignette, grain, time, damage, lowHealth, invuln, saturation;',
      'uniform vec2 resolution;',
      'varying vec2 vUv;',
      'vec3 RRTAndODTFit(vec3 v){ vec3 a = v*(v+0.0245786)-0.000090537; vec3 b = v*(0.983729*v+0.4329510)+0.238081; return a/b; }',
      'vec3 acesFilmic(vec3 c){',
      '  const mat3 inM = mat3(vec3(0.59719,0.07600,0.02840), vec3(0.35458,0.90834,0.13383), vec3(0.04823,0.01566,0.83777));',
      '  const mat3 outM = mat3(vec3(1.60475,-0.10208,-0.00327), vec3(-0.53108,1.10813,-0.07276), vec3(-0.07367,-0.00605,1.07602));',
      '  c *= exposure / 0.6; c = inM * c; c = RRTAndODTFit(c); c = outM * c; return clamp(c, 0.0, 1.0);',
      '}',
      'vec3 toSRGB(vec3 c){ return mix(pow(c, vec3(0.41666)) * 1.055 - 0.055, c * 12.92, vec3(lessThanEqual(c, vec3(0.0031308)))); }',
      'float hash(vec2 p){ return fract(sin(dot(p, vec2(12.9898,78.233))) * 43758.5453); }',
      'void main(){',
      '  vec2 d = vUv - 0.5;',
      '  float r = length(d * vec2(resolution.x/resolution.y, 1.0));',
      // slight chromatic split when hurt
      '  float ca = damage * 0.004;',
      '  vec3 c;',
      '  c.r = texture2D(tDiffuse, vUv + d * ca).r;',
      '  c.g = texture2D(tDiffuse, vUv).g;',
      '  c.b = texture2D(tDiffuse, vUv - d * ca).b;',
      '  c = acesFilmic(c);',
      '  float l = dot(c, vec3(0.2126,0.7152,0.0722));',
      '  float sat = saturation * (1.0 - lowHealth * 0.6);',
      '  c = mix(vec3(l), c, sat);',
      '  c = toSRGB(c);',
      '  c *= mix(1.0, smoothstep(1.05, 0.25, r), vignette);',
      '  float edge = smoothstep(0.35, 0.95, r);',
      '  c = mix(c, vec3(0.55, 0.02, 0.02), clamp(edge * (damage * 0.9 + lowHealth * 0.35), 0.0, 0.85));',
      '  c += vec3(0.25, 0.35, 0.45) * invuln * edge * 0.5;',
      '  float n = hash(vUv * resolution + fract(time) * 917.0);',
      '  c += (n - 0.5) * grain;',
      '  gl_FragColor = vec4(c, 1.0);',
      '}',
    ].join('\n'),
  };

  // Renders the first-person weapon scene on top of the world with a cleared
  // depth buffer, so the gun never clips into walls.
  class ViewmodelPass extends THREE.Pass {
    constructor(scene, camera) {
      super();
      this.scene = scene;
      this.camera = camera;
      this.needsSwap = false;
    }
    render(renderer, writeBuffer, readBuffer) {
      const ac = renderer.autoClear;
      renderer.autoClear = false;
      renderer.setRenderTarget(this.renderToScreen ? null : readBuffer);
      renderer.clearDepth();
      renderer.render(this.scene, this.camera);
      renderer.autoClear = ac;
    }
  }

  const Engine = {
    QUALITY,
    renderer: null,
    scene: null,
    camera: null,
    viewScene: null,
    viewCamera: null,
    composer: null,
    passes: {},
    settings: QUALITY.medium,
    time: 0,
    realTime: 0,
    dt: 0,
    frame: 0,
    paused: true,
    updaters: [],
    alwaysUpdaters: [],
    preRender: [], // fn(renderer) run before the main render (e.g. camera feeds)
    fps: 60,
    frameMs: 16,
    exposure: 1.0,
    fx: { damage: 0, lowHealth: 0, invuln: 0, saturation: 1 },
    halfFloatOK: false,

    init(canvas) {
      const renderer = new THREE.WebGLRenderer({
        canvas,
        antialias: false,
        powerPreference: 'high-performance',
        stencil: false,
      });
      renderer.shadowMap.enabled = true;
      renderer.shadowMap.type = THREE.PCFSoftShadowMap;
      // Shadow maps are refreshed once per frame by renderFrame(), not by every
      // render() call, so secondary renders (camera feed) do not redo them.
      renderer.shadowMap.autoUpdate = false;
      renderer.outputEncoding = THREE.sRGBEncoding;
      renderer.toneMappingExposure = 1.0;
      renderer.setClearColor(0x05070c, 1);
      renderer.info.autoReset = false; // reset once per frame (composer renders many times)
      // Synchronous per-program link checks stall Chrome for every shader;
      // GL errors still reach the console without them. ?debug re-enables.
      renderer.debug.checkShaderErrors = /[?&]debug/.test(location.search);
      this.renderer = renderer;

      const ext = renderer.extensions;
      this.halfFloatOK = renderer.capabilities.isWebGL2
        ? ext.has('EXT_color_buffer_float') || ext.has('EXT_color_buffer_half_float')
        : ext.has('OES_texture_half_float') && ext.has('EXT_color_buffer_half_float');

      this.scene = new THREE.Scene();
      this.scene.fog = new THREE.FogExp2(0x0b1220, 0.014);
      this.camera = new THREE.PerspectiveCamera(72, window.innerWidth / window.innerHeight, 0.08, 700);
      this.camera.rotation.order = 'YXZ';
      this.scene.add(this.camera);

      // Separate scene/camera for the first-person weapon (viewmodel).
      this.viewScene = new THREE.Scene();
      this.viewCamera = new THREE.PerspectiveCamera(58, window.innerWidth / window.innerHeight, 0.01, 10);
      this.viewScene.add(this.viewCamera);

      window.addEventListener('resize', () => this.resize());
      const q = (location.search.match(/quality=(low|medium|high)/) || [])[1];
      this.applyQuality(q || 'medium', true);
    },

    // -------------------------------------------------------------------------
    // Post-processing chain
    // -------------------------------------------------------------------------
    buildComposer() {
      if (this.composer) {
        this.composer.renderTarget1.dispose();
        this.composer.renderTarget2.dispose();
        this.composer = null;
      }
      const s = this.settings;
      if (!s.post) return;
      const size = this.renderer.getDrawingBufferSize(new THREE.Vector2());
      const rt = new THREE.WebGLRenderTarget(size.x, size.y, {
        minFilter: THREE.LinearFilter,
        magFilter: THREE.LinearFilter,
        format: THREE.RGBAFormat,
        type: this.halfFloatOK ? THREE.HalfFloatType : THREE.UnsignedByteType,
        stencilBuffer: false,
      });
      const composer = new THREE.EffectComposer(this.renderer, rt);
      const renderPass = new THREE.RenderPass(this.scene, this.camera);
      composer.addPass(renderPass);
      composer.addPass(new ViewmodelPass(this.viewScene, this.viewCamera));

      let bloom = null;
      if (s.bloom) {
        bloom = new THREE.UnrealBloomPass(new THREE.Vector2(size.x, size.y), 0.75, 0.5, 0.85);
        // Run bloom at reduced resolution to keep it cheap.
        const origSetSize = bloom.setSize.bind(bloom);
        const scale = s.bloomScale || 0.5;
        bloom.setSize = (w, h) => origSetSize(Math.max(64, Math.floor(w * scale)), Math.max(64, Math.floor(h * scale)));
        bloom.setSize(size.x, size.y);
        composer.addPass(bloom);
      }
      const composite = new THREE.ShaderPass(CompositeShader);
      composer.addPass(composite);
      const fxaa = new THREE.ShaderPass(THREE.FXAAShader);
      composer.addPass(fxaa);

      this.composer = composer;
      this.passes = { renderPass, bloom, composite, fxaa };
      this.resize();
    },

    applyQuality(name, initial) {
      this.settings = QUALITY[name] || QUALITY.medium;
      const s = this.settings;
      const r = this.renderer;
      r.toneMapping = s.post ? THREE.NoToneMapping : THREE.ACESFilmicToneMapping;
      this.resize();
      this.buildComposer();
      // Tone mapping is baked into programs: force recompilation.
      if (!initial) {
        const touch = (o) => {
          if (!o.material) return;
          const mats = Array.isArray(o.material) ? o.material : [o.material];
          for (const m of mats) m.needsUpdate = true;
        };
        this.scene.traverse(touch);
        this.viewScene.traverse(touch);
      }
      BI.Events.emit('quality:changed', { settings: s });
    },

    resize() {
      const w = window.innerWidth, h = window.innerHeight;
      const pr = Math.min(window.devicePixelRatio || 1, this.settings.maxPixelRatio);
      this.renderer.setPixelRatio(pr);
      this.renderer.setSize(w, h, false);
      this.renderer.domElement.style.width = w + 'px';
      this.renderer.domElement.style.height = h + 'px';
      this.camera.aspect = w / h;
      this.camera.updateProjectionMatrix();
      this.viewCamera.aspect = w / h;
      this.viewCamera.updateProjectionMatrix();
      if (this.composer) {
        // The composer was given an explicit render target (pixel ratio 1),
        // so pass drawing-buffer pixels directly.
        const size = this.renderer.getDrawingBufferSize(new THREE.Vector2());
        this.composer.setSize(size.x, size.y);
        this.passes.fxaa.uniforms.resolution.value.set(1 / size.x, 1 / size.y);
        this.passes.composite.uniforms.resolution.value.set(size.x, size.y);
      }
    },

    // -------------------------------------------------------------------------
    // Loop
    // -------------------------------------------------------------------------
    // Worst-case timing per system (debug overlay / perf tests).
    profile: {},
    addUpdate(fn, order) {
      this.updaters.push({ fn, order: order || 0, name: 'u' + (order || 0) });
      this.updaters.sort((a, b) => a.order - b.order);
    },
    addAlwaysUpdate(fn, order) {
      this.alwaysUpdaters.push({ fn, order: order || 0 });
      this.alwaysUpdaters.sort((a, b) => a.order - b.order);
    },

    start() {
      let last = performance.now();
      let fpsAcc = 0, fpsFrames = 0;
      const loop = (now) => {
        requestAnimationFrame(loop);
        let dt = (now - last) / 1000;
        last = now;
        if (!(dt > 0)) dt = 1 / 60;
        fpsAcc += dt;
        fpsFrames++;
        if (fpsAcc >= 0.5) {
          this.fps = fpsFrames / fpsAcc;
          this.frameMs = (fpsAcc / fpsFrames) * 1000;
          fpsAcc = 0;
          fpsFrames = 0;
        }
        dt = Math.min(dt, 0.05);
        this.dt = dt;
        this.realTime += dt;
        this.frame++;
        try {
          if (!this.paused) {
            this.time += dt;
            for (let i = 0; i < this.updaters.length; i++) {
              const u = this.updaters[i];
              const t0 = performance.now();
              u.fn(dt, this.time);
              const ms = performance.now() - t0;
              if (!(this.profile[u.name] >= ms)) this.profile[u.name] = ms;
            }
          }
          for (let i = 0; i < this.alwaysUpdaters.length; i++) this.alwaysUpdaters[i].fn(dt, this.realTime);
          const tr = performance.now();
          this.renderFrame(dt);
          const rms = performance.now() - tr;
          if (!(this.profile.render >= rms)) this.profile.render = rms;
        } catch (err) {
          console.error(err);
        }
        BI.Input.endFrame();
      };
      requestAnimationFrame(loop);
    },

    renderFrame() {
      const r = this.renderer;
      r.info.reset();
      for (let i = 0; i < this.preRender.length; i++) this.preRender[i](r);
      r.shadowMap.needsUpdate = true;
      if (this.composer) {
        const u = this.passes.composite.uniforms;
        u.time.value = this.realTime;
        u.exposure.value = this.exposure;
        u.damage.value = this.fx.damage;
        u.lowHealth.value = this.fx.lowHealth;
        u.invuln.value = this.fx.invuln;
        u.saturation.value = this.fx.saturation;
        this.composer.render(this.dt);
      } else {
        r.toneMappingExposure = this.exposure;
        r.autoClear = true;
        r.setRenderTarget(null);
        r.render(this.scene, this.camera);
        r.autoClear = false;
        r.clearDepth();
        r.render(this.viewScene, this.viewCamera);
        r.autoClear = true;
      }
    },

    stats() {
      const info = this.renderer.info;
      return {
        fps: this.fps,
        frameMs: this.frameMs,
        calls: info.render.calls,
        triangles: info.render.triangles,
        programs: info.programs ? info.programs.length : 0,
        textures: info.memory.textures,
        geometries: info.memory.geometries,
      };
    },
  };

  BI.Engine = Engine;
})();
