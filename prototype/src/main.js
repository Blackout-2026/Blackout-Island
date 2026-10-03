/* =============================================================================
 * main.js — bootstrap: build the world step by step, then start the loop
 * -----------------------------------------------------------------------------
 * Build order matters: the nav grid samples the physics layer, so it is built
 * after every collider (world, doors, crate) exists; agents need the nav grid.
 * Steps are async so the loading bar repaints between the heavy ones.
 * Update order (BI.Engine.addUpdate priorities):
 *   5 zone culling · 10 player · 12 weapons · 15 interactions · 20 mission
 *   30 agents · 40 doors · 41 crate · 80 lighting · 84 bunker lights · 85 FX
 * window.__debug exposes hooks for the automated Playwright tests.
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';

  const nextFrame = () => new Promise((r) => requestAnimationFrame(() => r()));

  const Game = {
    started: false,
    steps: [],

    async init() {
      this.loadStart = performance.now();
      const ui = document.getElementById('ui');
      ui.innerHTML =
        '<div id="loading" class="screen"><div class="title">BLACKOUT ISLAND</div><div class="subtitle">THE CURE</div>' +
        '<div class="bar"><div id="loadfill"></div></div><div id="loadtext">Starting…</div></div>';
      const setProgress = (p, text) => {
        const f = document.getElementById('loadfill'), t = document.getElementById('loadtext');
        if (f) f.style.width = Math.round(p * 100) + '%';
        if (t) t.textContent = text;
      };
      const step = async (p, text, fn) => {
        setProgress(p, text);
        await nextFrame();
        const t0 = performance.now();
        fn();
        this.steps.push({ text, ms: Math.round(performance.now() - t0) });
      };

      const canvas = document.getElementById('game');
      const E = BI.Engine;
      await step(0.02, 'Initialising renderer', () => {
        E.init(canvas);
        BI.Input.init(canvas);
        BI.Tex.init(E.renderer);
      });
      await step(0.06, 'Generating textures', () => {
        for (const n of ['sand', 'grass', 'forestFloor', 'dirt', 'rock', 'concrete']) BI.Tex.get(n);
      });
      await step(0.14, 'Environment maps', () => BI.Mat.init(E.renderer));
      await step(0.18, 'Shaping the island', () => {
        BI.Terrain.bake();
        BI.Terrain.buildInfo(BI.Forest.place());
        BI.Terrain.buildMesh(E.scene);
      });
      await step(0.3, 'Sky and ocean', () => {
        BI.Sky.build(E.scene);
        BI.Ocean.build(E.scene);
        BI.Lighting.init(E.scene);
      });
      await step(0.36, 'Building the forest, beach and bunker', () => BI.World.build(E.scene));
      await step(0.55, 'Doors, crate and pickups', () => {
        BI.Doors.build(E.scene);
        BI.crate = new BI.Crate(E.scene);
        BI.Interact.build(E.scene);
      });
      await step(0.6, 'Navigation grid', () => BI.Nav.build());
      await step(0.66, 'Player and weapons', () => {
        BI.player = new BI.Player();
        const cp = BI.Level.checkpoints.beach;
        BI.player.teleport(cp.x, 3, cp.z, cp.yaw);
        E.addUpdate((dt) => BI.player.update(dt), 10);
        BI.weapons = new BI.WeaponSystem();
        E.addUpdate((dt) => BI.weapons.update(dt), 12);
        E.addUpdate((dt) => BI.Lighting.update(dt), 80);
      });
      await step(0.72, 'Spawning agents', () => {
        BI.Noise.init();
        BI.Agents.spawn(E.scene);
      });
      await step(0.8, 'Interface', () => {
        BI.HUD.build();
        BI.Debug.init();
        BI.Mission.init();
        BI.Menus.init();
      });
      await this.compileChunked(setProgress, 0.82, 0.95);
      await this.warmup(setProgress, 0.95, 1.0);
      E.start();
      this.loadMs = Math.round(performance.now() - this.loadStart);
      document.getElementById('ui').querySelector('#loading').remove();
      BI.Menus.showStart();
      window.__debug.ready = true;
    },

    // Compile shader programs a few materials at a time so the loading bar
    // keeps moving. three r128's compile() visits every object with a material,
    // so materials outside the current chunk are detached temporarily.
    async compileChunked(setProgress, p0, p1) {
      const E = BI.Engine;
      const t0 = performance.now();
      BI.World.setAllVisible(true);
      for (const scene of [E.scene, E.viewScene]) {
        const holders = [];
        const seen = new Set();
        scene.traverse((o) => {
          if (!o.material) return;
          holders.push({ o, m: o.material });
          const key = (Array.isArray(o.material) ? o.material.map((m) => m.uuid).join() : o.material.uuid) + (o.isInstancedMesh ? 'i' : '') + (o.isSkinnedMesh ? 's' : '');
          if (!seen.has(key)) {
            seen.add(key);
            holders[holders.length - 1].rep = true;
          }
        });
        const reps = holders.filter((h) => h.rep);
        const CH = /[?&]profile/.test(location.search) ? 1 : 8;
        this.compileProfile = this.compileProfile || [];
        for (const h of holders) h.o.material = null;
        for (let i = 0; i < reps.length; i += CH) {
          for (let j = i; j < Math.min(i + CH, reps.length); j++) reps[j].o.material = reps[j].m;
          const tc = performance.now();
          E.renderer.compile(scene, scene === E.scene ? E.camera : E.viewCamera);
          this.compileProfile.push({ m: reps[i].m.name || reps[i].m.type, o: reps[i].o.name || reps[i].o.type, ms: Math.round(performance.now() - tc) });
          for (let j = i; j < Math.min(i + CH, reps.length); j++) reps[j].o.material = null;
          setProgress(p0 + (p1 - p0) * Math.min(1, (i + CH) / reps.length) * (scene === E.scene ? 1 : 0.1), 'Compiling shaders');
          await nextFrame();
        }
        for (const h of holders) h.o.material = h.m;
      }
      this.steps.push({ text: 'Compiling shaders', ms: Math.round(performance.now() - t0) });
    },

    // Render the real pipeline from several places (outside, inside, flashlight
    // on, hidden effects shown) while the loading screen covers the canvas, so
    // every shader program, shadow-depth variant and texture upload happens
    // now instead of as a hitch the first time the player sees something.
    async warmup(setProgress, p0, p1) {
      const E = BI.Engine, p = BI.player;
      const t0 = performance.now();
      const shown = [];
      for (const sc of [E.scene, E.viewScene]) {
        sc.traverse((o) => {
          if ((o.isMesh || o.isPoints || o.isLine) && !o.visible) { o.visible = true; shown.push(o); }
        });
      }
      for (const k of Object.keys(BI.weapons.models)) BI.weapons.models[k].group.visible = true;
      BI.weapons.flash.group.visible = true;
      BI.World.setAllVisible(true);
      // ANGLE (D3D11) compiles shader variants lazily per program, vertex
      // layout and render target on the FIRST DRAW. Drawing every object once
      // (culling off) moves those stalls into the loading screen.
      const culled = [];
      for (const sc of [E.scene, E.viewScene]) sc.traverse((o) => { if (o.frustumCulled) { o.frustumCulled = false; culled.push(o); } });
      p.flashOn = true;
      const views = [['beach', 0], ['compound', 0], ['security', 0], ['generator', 0], ['lab', 0]];
      for (let i = 0; i < views.length; i++) {
        window.__debug.go(views[i][0], views[i][1]);
        p.update(1 / 60);
        p.flashlight.intensity = 2;
        p.flashlight.shadow.autoUpdate = true;
        BI.Lighting.update(1 / 60);
        BI.Lighting.moon.shadow.autoUpdate = true;
        E.renderFrame();
        setProgress(p0 + (p1 - p0) * ((i + 1) / views.length), 'Warming up');
        await nextFrame();
      }
      for (const o of culled) o.frustumCulled = true;
      for (const o of shown) o.visible = false;
      for (const k of Object.keys(BI.weapons.models)) BI.weapons.models[k].group.visible = k === BI.weapons.def.id;
      BI.weapons.flash.group.visible = false;
      p.flashOn = false;
      p.flashlight.intensity = 0;
      window.__debug.go('beach');
      p.update(1 / 60);
      BI.World.lastZone = null;
      BI.World.update();
      this.steps.push({ text: 'Warm-up', ms: Math.round(performance.now() - t0) });
    },

    start(unlocked) {
      if (this.started) return;
      this.started = true;
      BI.Menus.clear();
      if (unlocked) BI.Input.allowUnlocked = true;
      else BI.Input.requestLock();
      BI.Audio.volume = BI.Menus.settings.volume;
      BI.Audio.start();
      BI.HUD.show(true);
      BI.Mission.startTime = BI.Engine.time;
      BI.Events.emit('objective:changed', { text: BI.Mission.objective, label: 'BEACH' });
      BI.Events.emit('checkpoint', { name: 'beach' });
      BI.Engine.paused = false;
    },
  };

  BI.Game = Game;

  // ---------------------------------------------------------------------------
  // Debug / test hooks (used by tests/*.js; harmless in normal play)
  // ---------------------------------------------------------------------------
  const SPOTS = {
    beach: [0, 3, 121, 0], forest: [2, 30, 60, 0.3], compound: [0, 30, -24, 0], entrance: [0, 12, -44, 0],
    security: [0, 4, -72, 0], corridor: [0, 4, -84, 0], generatorDoor: [-1, 4, -106, Math.PI / 2],
    generator: [-5.6, 4, -106, Math.PI / 2], lab: [0, 4, -126, 0], cure: [0, 4, -134.25, 0], boat: [0, 3, 124, Math.PI],
  };
  window.__debug = {
    ready: false,
    start: () => Game.start(true),
    steps: () => ({ total: Game.loadMs, steps: Game.steps }),
    stats: () => BI.Engine.stats(),
    player: () => {
      const p = BI.player;
      return { x: p.pos.x, y: p.pos.y, z: p.pos.z, yaw: p.yaw, pitch: p.pitch, health: p.health, alive: p.alive, zone: p.zone, onGround: p.onGround, hasCure: p.hasCure };
    },
    teleport: (x, y, z, yaw, pitch) => {
      BI.player.teleport(x, y, z, yaw);
      if (pitch !== undefined) BI.player.pitch = pitch;
    },
    go: (name, pitch) => {
      const s = SPOTS[name];
      BI.player.teleport(s[0], s[1], s[2], s[3]);
      BI.player.pitch = pitch || 0;
    },
    key: (code, down) => BI.Input.simulateKey(code, down),
    look: (yaw, pitch) => {
      BI.player.yaw = yaw;
      BI.player.pitch = pitch || 0;
    },
    lookAt: (x, y, z) => {
      const p = BI.player, e = p.eyePosition(new THREE.Vector3());
      p.yaw = Math.atan2(-(x - e.x), -(z - e.z));
      p.pitch = Math.atan2(y - e.y, Math.hypot(x - e.x, z - e.z));
    },
    events: () => BI.Events.log.slice(),
    count: (name) => BI.Events.count(name),
    state: () => ({
      phase: BI.Mission.phase,
      objective: BI.Mission.objective,
      checkpoint: BI.Mission.checkpoint,
      generatorDown: BI.Mission.generatorDown,
      cureTaken: BI.Mission.cureTaken,
      complete: BI.Mission.complete,
      kills: BI.Mission.kills,
      deaths: BI.Mission.deaths,
      alert: BI.Blackboard.data.alert,
      blackout: BI.Lighting.blackout,
      labLocked: BI.Doors.byId.lab.locked,
      fogDensity: BI.Engine.scene.fog.density,
      commander: { goal: BI.Agents.commander.goal, plan: BI.Agents.commander.plan.slice(), step: BI.Agents.commander.step },
      directives: Object.keys(BI.Blackboard.directives).map((k) => k + ':' + BI.Blackboard.directives[k].type),
      agents: BI.Agents.list.map((a) => ({ name: a.name, type: a.type, arch: a.arch, state: a.state, alive: a.alive, hp: Math.round(a.hp), heightened: a.heightened, x: +a.pos.x.toFixed(1), z: +a.pos.z.toFixed(1), stuck: a.stuckCount || 0 })),
    }),
    setGod: (v) => { BI.player.godMode = v; },
    freezeAI: (v) => { BI.Agents.frozen = v; },
    debugOverlay: (v) => BI.Debug.toggle(v),
    flash: (v) => { BI.player.flashOn = v; },
    weapon: (i) => BI.weapons.setWeapon(i, true),
    quality: (q) => BI.Engine.applyQuality(q),
    agentTo: (name, x, z, yaw) => {
      const a = BI.Agents.byName(name);
      a.pos.set(x, BI.Physics.groundAt(x, z, BI.Nav.baseY(x, z) + 0.5).y, z);
      if (yaw !== undefined) a.yaw = yaw;
      a.controller.path = null;
    },
    profile: (reset) => { const p = Object.assign({}, BI.Engine.profile); if (reset) BI.Engine.profile = {}; return p; },
    killAgent: (name) => { const a = BI.Agents.byName(name); if (a && a.alive) a.takeDamage(999, BI.player.pos, false, 'debug'); },
    // FPS over a window (frame times recorded by the engine loop)
    measure: (sec) => new Promise((res) => {
      const times = [];
      let last = performance.now();
      const t0 = last;
      const f = (now) => {
        times.push(now - last);
        last = now;
        if (now - t0 < sec * 1000) requestAnimationFrame(f);
        else {
          times.shift();
          times.sort((a, b) => a - b);
          const avg = times.reduce((a, b) => a + b, 0) / times.length;
          res({ frames: times.length, avgMs: +avg.toFixed(2), fps: +(1000 / avg).toFixed(1), p95Ms: +times[Math.floor(times.length * 0.95)].toFixed(2), worstMs: +times[times.length - 1].toFixed(2), stats: BI.Engine.stats() });
        }
      };
      requestAnimationFrame(f);
    }),
  };

  window.addEventListener('load', () => {
    Game.init().catch((err) => {
      console.error(err);
      window.__loadError = String((err && err.stack) || err);
      const t = document.getElementById('loadtext');
      if (t) t.textContent = 'Error: ' + err.message;
    });
  });
})();
