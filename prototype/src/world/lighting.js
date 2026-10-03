/* =============================================================================
 * world/lighting.js — moonlight, ambient, light pool, zones, blackout state
 * -----------------------------------------------------------------------------
 * three.js shades every light for every pixel, so hundreds of real lights
 * would never hold 60 fps on a laptop GPU. Instead every lamp in the world is a
 * VIRTUAL light (position, colour, intensity, range). Each frame the N most
 * important virtual lights near the camera are assigned to a fixed pool of
 * real PointLights / SpotLights (N depends on the quality setting). Lights
 * fade out before being reassigned, so there is no popping. The visible lamp
 * meshes are emissive (bloom does the glow), so distant lamps still read as
 * lit even when they have no real light.
 *
 * Zones blend fog, ambient and moonlight between outside, the ramp and the
 * bunker. The generator blackout switches the bunker to the emergency set:
 * thicker red-black fog, dim red ambient, red emergency lamps and rotating
 * alarm beacons (see world/bunker_interior.js for the cascade itself).
 * Role: World Builder / Graphics.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const FOG = {
    outside: { color: new THREE.Color(0x0b1220), density: 0.0135 },
    inside: { color: new THREE.Color(0x0b0d11), density: 0.022 },
    blackout: { color: new THREE.Color(0x0b0203), density: 0.05 },
  };
  const HEMI = {
    outside: { sky: new THREE.Color(0x52668f), ground: new THREE.Color(0x141820), i: 0.78 },
    inside: { sky: new THREE.Color(0x3c4048), ground: new THREE.Color(0x18191c), i: 0.42 },
    blackout: { sky: new THREE.Color(0x3a0907), ground: new THREE.Color(0x060202), i: 0.2 },
  };

  const Lighting = {
    hemi: null,
    moon: null,
    points: [], // real light slots {light, v, w}
    spots: [],
    vPoints: [], // virtual lights
    vSpots: [],
    blackout: 0, // 0 -> 1 blend for the bunker
    blackoutTarget: 0,
    frame: 0,
    probe: new THREE.Color(), // light colour at the player (for the viewmodel)

    init(scene) {
      this.scene = scene;
      this.hemi = new THREE.HemisphereLight(0x4a5d85, 0x10131a, 0.6);
      scene.add(this.hemi);

      const moon = new THREE.DirectionalLight(0xa9bde6, 0.75);
      moon.position.copy(BI.Sky.moonDir).multiplyScalar(80);
      moon.castShadow = true;
      const sc = moon.shadow.camera;
      sc.left = -40; sc.right = 40; sc.top = 40; sc.bottom = -40; sc.near = 1; sc.far = 220;
      moon.shadow.bias = -0.0006;
      moon.shadow.normalBias = 0.04;
      scene.add(moon);
      scene.add(moon.target);
      this.moon = moon;

      this.applyQuality(BI.Engine.settings);
      BI.Events.on('quality:changed', (e) => this.applyQuality(e.settings));
      // Event chain: the power cut thickens the fog and turns the bunker's
      // ambient + reflections red (the fixtures' cascade is in bunker_interior).
      BI.Events.on('generator:disabled', () => this.setBlackout(true));
      BI.Events.on('lights:emergency', () => BI.Mat.setInteriorEnv('emergency'));
    },

    applyQuality(s) {
      const scene = this.scene;
      this.moon.shadow.mapSize.set(s.moonShadow, s.moonShadow);
      if (this.moon.shadow.map) {
        this.moon.shadow.map.dispose();
        this.moon.shadow.map = null;
      }
      for (const p of this.points) scene.remove(p.light);
      for (const p of this.spots) {
        scene.remove(p.light);
        scene.remove(p.light.target);
      }
      this.points = [];
      this.spots = [];
      for (let i = 0; i < s.pointLights; i++) {
        const l = new THREE.PointLight(0xffffff, 0, 10, 2);
        l.castShadow = false;
        scene.add(l);
        this.points.push({ light: l, v: null, w: 0 });
      }
      for (let i = 0; i < s.spotLights; i++) {
        const l = new THREE.SpotLight(0xffffff, 0, 30, 0.6, 0.5, 1.5);
        l.castShadow = false;
        scene.add(l);
        scene.add(l.target);
        this.spots.push({ light: l, v: null, w: 0 });
      }
      this.shadowEvery = s.shadowEvery || 1;
    },

    // -------------------------------------------------------------------------
    // Virtual lights
    // -------------------------------------------------------------------------
    addPoint(o) {
      const v = {
        type: 'point',
        pos: o.pos.clone ? o.pos.clone() : new THREE.Vector3(o.pos[0], o.pos[1], o.pos[2]),
        color: new THREE.Color(o.color === undefined ? 0xffffff : o.color),
        intensity: o.intensity === undefined ? 1 : o.intensity,
        distance: o.distance || 10,
        decay: o.decay === undefined ? 2 : o.decay,
        mul: 1, // flicker / on-off multiplier, animated by owners
        zone: o.zone || 'any', // 'outside' | 'bunker' | 'any'
        group: o.group || '',
        priority: o.priority || 1,
      };
      this.vPoints.push(v);
      return v;
    },
    addSpot(o) {
      const v = this.addPoint(o);
      this.vPoints.pop();
      v.type = 'spot';
      v.target = o.target.clone ? o.target.clone() : new THREE.Vector3(o.target[0], o.target[1], o.target[2]);
      v.angle = o.angle || 0.6;
      v.penumbra = o.penumbra === undefined ? 0.5 : o.penumbra;
      this.vSpots.push(v);
      return v;
    },

    assign(slots, virtuals, cam, dt, playerZone) {
      // score
      const scored = [];
      for (let i = 0; i < virtuals.length; i++) {
        const v = virtuals[i];
        const I = v.intensity * v.mul;
        if (I <= 0.01) continue;
        if (v.zone === 'bunker' && playerZone === 'outside') continue;
        if (v.zone === 'outside' && playerZone === 'bunker') continue;
        const d = v.pos.distanceTo(cam);
        if (d > v.distance * 2.2 + 6) continue;
        let score = (I * v.priority * v.distance) / (1 + d * d * 0.35);
        for (const s of slots) if (s.v === v) score *= 1.35; // hysteresis
        scored.push({ v, score });
      }
      scored.sort((a, b) => b.score - a.score);
      const desired = scored.slice(0, slots.length).map((s) => s.v);
      const fade = dt * 6;
      for (const s of slots) {
        if (s.v && desired.indexOf(s.v) >= 0) s.w = Math.min(1, s.w + fade);
        else if (s.v) {
          s.w -= fade * 1.5;
          if (s.w <= 0) { s.w = 0; s.v = null; }
        }
      }
      for (const v of desired) {
        if (slots.some((s) => s.v === v)) continue;
        const free = slots.find((s) => !s.v);
        if (free) { free.v = v; free.w = 0; }
      }
      for (const s of slots) {
        const l = s.light;
        if (!s.v) { l.intensity = 0; continue; }
        const v = s.v;
        l.position.copy(v.pos);
        l.color.copy(v.color);
        l.distance = v.distance;
        l.decay = v.decay;
        l.intensity = v.intensity * v.mul * s.w;
        if (v.type === 'spot') {
          l.target.position.copy(v.target);
          l.angle = v.angle;
          l.penumbra = v.penumbra;
        }
      }
    },

    // Approximate light reaching a point (used to light the first-person gun).
    sampleAt(p, out) {
      out.setRGB(0, 0, 0);
      for (const v of this.vPoints) {
        const I = v.intensity * v.mul;
        if (I <= 0.01) continue;
        const d = v.pos.distanceTo(p);
        if (d > v.distance) continue;
        const att = Math.pow(1 - d / v.distance, 2);
        out.r += v.color.r * I * att;
        out.g += v.color.g * I * att;
        out.b += v.color.b * I * att;
      }
      return out;
    },

    setBlackout(on) {
      this.blackoutTarget = on ? 1 : 0;
    },

    // -------------------------------------------------------------------------
    update(dt) {
      const cam = BI.Engine.camera.position;
      const player = BI.player;
      const interior = player ? player.interior : 0;
      const zone = player ? player.zone : 'outside';
      this.blackout = U.damp(this.blackout, this.blackoutTarget, 1.6, dt);
      const bo = this.blackout;

      // fog + ambient blend
      const scene = BI.Engine.scene;
      const fogIn = { color: FOG.inside.color.clone().lerp(FOG.blackout.color, bo), density: U.lerp(FOG.inside.density, FOG.blackout.density, bo) };
      scene.fog.color.copy(FOG.outside.color).lerp(fogIn.color, interior);
      scene.fog.density = U.lerp(FOG.outside.density, fogIn.density, interior);
      if (BI.Engine.renderer) BI.Engine.renderer.setClearColor(scene.fog.color);
      const hIn = {
        sky: HEMI.inside.sky.clone().lerp(HEMI.blackout.sky, bo),
        ground: HEMI.inside.ground.clone().lerp(HEMI.blackout.ground, bo),
        i: U.lerp(HEMI.inside.i, HEMI.blackout.i, bo),
      };
      this.hemi.color.copy(HEMI.outside.sky).lerp(hIn.sky, interior);
      this.hemi.groundColor.copy(HEMI.outside.ground).lerp(hIn.ground, interior);
      this.hemi.intensity = U.lerp(HEMI.outside.i, hIn.i, interior);
      this.moon.intensity = 0.9 * (1 - U.smoothstep(0.2, 0.7, interior));

      // moon shadow follows the camera, snapped to texels to avoid shimmer
      const m = this.moon;
      const outsideish = interior < 0.9;
      m.shadow.autoUpdate = outsideish && this.frame % this.shadowEvery === 0;
      if (outsideish) {
        const texel = 80 / m.shadow.mapSize.x;
        const fx = Math.round(cam.x / texel) * texel, fz = Math.round(cam.z / texel) * texel;
        m.target.position.set(fx, 8, fz);
        m.position.set(fx, 8, fz).addScaledVector(BI.Sky.moonDir, 100);
        m.target.updateMatrixWorld();
      }
      this.frame++;

      this.assign(this.points, this.vPoints, cam, dt, zone);
      this.assign(this.spots, this.vSpots, cam, dt, zone);

      // light probe for the viewmodel
      this.sampleAt(cam, this.probe);
    },
  };

  BI.Lighting = Lighting;
})();
