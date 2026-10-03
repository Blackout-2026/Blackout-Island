/* =============================================================================
 * world/bunker_exterior.js — fenced compound and the bunker entrance
 * -----------------------------------------------------------------------------
 * Compound on the plateau (y = 10) against a concrete retaining wall cut into
 * the cliff. Contents: chain-link fence with razor wire and an open gate,
 * entrance building (lobby + ramp tunnel down to y = 2.5), floodlights with
 * visible light cones, guard booth, steam vents, antenna mast with a blinking
 * beacon, truck, sandbags, crates, barrels and warning signage.
 * Static geometry is merged per material with a StaticBatcher.
 * Role: World Builder.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const L = BI.Level;

  const Ext = {
    group: null,
    beacons: [],

    sign(key, opts, w, h, x, y, z, ry, parent) {
      const tex = BI.Tex.decal(key, opts);
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), BI.Mat.standard({ map: tex, roughness: 0.75, metalness: 0.1, transparent: !opts.bg, alphaTest: opts.bg ? 0 : 0.5 }));
      m.position.set(x, y, z);
      m.rotation.y = ry || 0;
      m.receiveShadow = true;
      (parent || this.group).add(m);
      return m;
    },

    build(scene) {
      const P = BI.Physics;
      const M = (n) => BI.Mat.get(n);
      const G = L.GROUND_Y;
      const group = new THREE.Group();
      group.name = 'exterior';
      scene.add(group);
      this.group = group;
      const B = new BI.Geo.StaticBatcher('exterior');
      const wall = (x0, y0, z0, x1, y1, z1, mat, opts) => {
        B.box(x0, y0, z0, x1, y1, z1, M(mat || 'concrete'), { worldUV: 0.25 });
        if (!opts || opts.collide !== false) P.addBox(x0, y0, z0, x1, y1, z1, Object.assign({ surface: 'concrete' }, opts || {}));
      };

      // ---- retaining wall + pilasters -------------------------------------------
      // split around the ramp tunnel that passes underneath (x -2.6..2.6)
      wall(-24, 7, -63.4, -2.6, 15.2, -62, 'concrete');
      wall(2.6, 7, -63.4, 24, 15.2, -62, 'concrete');
      wall(-2.6, 9.7, -63.4, 2.6, 15.2, -62, 'concrete');
      for (const x of [-21, -15, -9, 9, 15, 21]) wall(x - 0.45, 7, -62, x + 0.45, 15.5, -61.4, 'concreteDark');
      B.box(-24.2, 15.2, -63.6, 24.2, 15.45, -61.8, M('concreteDark'), { worldUV: 0.25 });
      // drainage stains / pipes on the wall
      const pipeV = new THREE.CylinderGeometry(0.09, 0.09, 1, 8);
      for (const x of [-12, 12]) B.add(pipeV, M('pipe'), { x, y: 12.5, z: -61.85, sy: 5 });

      // ---- entrance building --------------------------------------------------
      const e = L.entrance;
      const door = L.doors.bunker;
      const dh = door.width / 2;
      wall(e.minX, 7, e.maxZ - 0.6, -dh - 0.1, e.roofY, e.maxZ); // front left
      wall(dh + 0.1, 7, e.maxZ - 0.6, e.maxX, e.roofY, e.maxZ); // front right
      wall(-dh - 0.1, G + door.height, e.maxZ - 0.6, dh + 0.1, e.roofY, e.maxZ); // lintel
      wall(e.minX, 7, e.minZ, e.minX + 0.6, e.roofY, e.maxZ - 0.6); // west
      wall(e.maxX - 0.6, 7, e.minZ, e.maxX, e.roofY, e.maxZ - 0.6); // east
      // roof + parapet + overhang above door
      wall(e.minX - 0.4, e.roofY, e.minZ - 0.5, e.maxX + 0.4, e.roofY + 0.5, e.maxZ + 0.4, 'concreteDark');
      for (const s of [-1, 1]) B.box(s > 0 ? e.maxX + 0.1 : e.minX - 0.4, e.roofY + 0.5, e.minZ - 0.5, s > 0 ? e.maxX + 0.4 : e.minX - 0.1, e.roofY + 1.0, e.maxZ + 0.4, M('concreteDark'), { worldUV: 0.25 });
      B.box(e.minX - 0.4, e.roofY + 0.5, e.maxZ + 0.1, e.maxX + 0.4, e.roofY + 1.0, e.maxZ + 0.4, M('concreteDark'), { worldUV: 0.25 });
      B.box(-2.6, G + door.height + 0.25, e.maxZ, 2.6, G + door.height + 0.45, e.maxZ + 1.4, M('concreteDark'), { worldUV: 0.25 });
      // hazard stripe door frame
      B.box(-dh - 0.35, G, e.maxZ - 0.05, -dh - 0.1, G + door.height + 0.25, e.maxZ + 0.08, M('hazard'), { worldUV: 1.2 });
      B.box(dh + 0.1, G, e.maxZ - 0.05, dh + 0.35, G + door.height + 0.25, e.maxZ + 0.08, M('hazard'), { worldUV: 1.2 });
      B.box(-dh - 0.35, G + door.height, e.maxZ - 0.05, dh + 0.35, G + door.height + 0.25, e.maxZ + 0.08, M('hazard'), { worldUV: 1.2 });
      // concrete apron in front of the door
      wall(-7, G - 0.5, e.maxZ, 7, G + 0.04, e.maxZ + 8, 'concreteDark', { wall: false });
      // wall vents on the building sides
      for (const s of [-1, 1]) {
        B.box(s * e.maxX - 0.05 * s, 12, -56, s * e.maxX + 0.12 * s, 13, -54.4, M('metalDarkOut'));
        for (let k = 0; k < 6; k++) B.box(s * e.maxX + 0.1 * s - 0.03, 12.1 + k * 0.15, -55.9, s * e.maxX + 0.16 * s, 12.16 + k * 0.15, -54.5, M('steelOut'));
      }

      // ---- entrance (lobby + ramp): own group so it stays visible underground ----
      const entranceGroup = new THREE.Group();
      entranceGroup.name = 'entrance';
      scene.add(entranceGroup);
      this.entranceGroup = entranceGroup;
      const BE = new BI.Geo.StaticBatcher('entrance');
      const wallE = (x0, y0, z0, x1, y1, z1, mat, opts) => {
        BE.box(x0, y0, z0, x1, y1, z1, M(mat || 'concrete'), { worldUV: 0.25 });
        if (!opts || opts.collide !== false) P.addBox(x0, y0, z0, x1, y1, z1, Object.assign({ surface: 'concrete' }, opts || {}));
      };
      // lobby interior (floor at y = 10, ceiling 13.4)
      const lobbyBack = L.ramp.zTop; // -52
      wallE(e.minX + 0.6, G - 0.6, lobbyBack, e.maxX - 0.6, G, e.maxZ - 0.6, 'floorConcrete', { wall: false });
      wallE(e.minX + 0.6, G + door.height, lobbyBack - 0.4, e.maxX - 0.6, e.roofY, e.maxZ - 0.6, 'ceiling', { wall: false });
      wallE(e.minX + 0.6, G, lobbyBack - 0.4, -2, G + door.height, lobbyBack, 'wallPanel');
      wallE(2, G, lobbyBack - 0.4, e.maxX - 0.6, G + door.height, lobbyBack, 'wallPanel');
      // inner wall lining (interior look) — visual only
      BE.box(e.minX + 0.6, G, lobbyBack, e.minX + 0.62, G + door.height, e.maxZ - 0.6, M('wallPanel'), { worldUV: 0.3 });
      BE.box(e.maxX - 0.62, G, lobbyBack, e.maxX - 0.6, G + door.height, e.maxZ - 0.6, M('wallPanel'), { worldUV: 0.3 });
      // lobby props: guard desk, lockers, extinguisher
      wallE(-4.9, G, -51.6, -3.0, G + 1.0, -50.6, 'deskTop', { wall: false });
      BE.add(BI.Geo.roundedBox(0.5, 0.35, 0.05, 0.02), M('plasticDark'), { x: -4.0, y: G + 1.25, z: -51.3, rx: -0.15 });
      for (let i = 0; i < 4; i++) wallE(3.2 + i * 0.52, G, -51.95, 3.68 + i * 0.52, G + 1.9, -51.45, 'locker', { wall: false });
      BE.add(new THREE.CylinderGeometry(0.09, 0.09, 0.5, 10), BI.Mat.get('pipeRed'), { x: 5.1, y: G + 0.25, z: -49.2 });

      // ---- ramp tunnel ----------------------------------------------------------
      const r = L.ramp;
      const rampLen = Math.hypot(r.zTop - r.zBottom, r.yTop - r.yBottom);
      const rampAng = Math.atan2(r.yTop - r.yBottom, r.zTop - r.zBottom);
      const rampMid = { y: (r.yTop + r.yBottom) / 2, z: (r.zTop + r.zBottom) / 2 };
      // floor slab (sloped) — diamond plate
      BE.add(BI.Geo.unitBox, M('diamondPlate'), { x: 0, y: rampMid.y - 0.15, z: rampMid.z, sx: 4, sy: 0.3, sz: rampLen, rx: -rampAng });
      // ceiling (sloped)
      BE.add(BI.Geo.unitBox, M('ceiling'), { x: 0, y: rampMid.y + 3.6 + 0.2, z: rampMid.z, sx: 4.6, sy: 0.4, sz: rampLen + 0.6, rx: -rampAng });
      // walls
      wallE(-2.6, r.yBottom, r.zBottom, -2, G + door.height, r.zTop, 'wallPanel');
      wallE(2, r.yBottom, r.zBottom, 2.6, G + door.height, r.zTop, 'wallPanel');
      P.addRamp(r.minX, r.maxX, r.zBottom, r.zTop, 'z', r.zTop, r.yTop, r.zBottom, r.yBottom, { surface: 'metal' });
      // hazard edge strips + handrails
      for (const s of [-1, 1]) {
        BE.add(BI.Geo.unitBox, M('hazard'), { x: s * 1.85, y: rampMid.y + 0.02, z: rampMid.z, sx: 0.3, sy: 0.04, sz: rampLen, rx: -rampAng });
        BE.add(new THREE.CylinderGeometry(0.035, 0.035, 1, 6), M('steel'), { x: s * 1.92, y: rampMid.y + 0.95, z: rampMid.z, sy: rampLen, rx: Math.PI / 2 - rampAng });
      }
      // ramp ceiling lights (tubes are created by the interior lighting system)
      this.rampLightSpots = [];
      for (let i = 0; i < 4; i++) {
        const z = r.zTop - 3 - i * 4.6;
        const y = r.yTop + (r.yBottom - r.yTop) * ((r.zTop - z) / (r.zTop - r.zBottom)) + 3.45;
        this.rampLightSpots.push(new THREE.Vector3(0, y, z));
      }
      // signage in the lobby
      this.sign('b1level', { w: 512, h: 160, bg: '#e2e0d8', text: 'LEVEL B1  ▼', color: '#1d1d1d', fontSize: 80 }, 1.6, 0.5, 0, G + 3.0, lobbyBack + 0.02, 0, entranceGroup);

      BE.build(entranceGroup);

      // ---- fence ----------------------------------------------------------------
      const c = L.compound;
      const fenceMat = M('fence');
      const H = 3.0;
      const fenceRuns = [
        [c.minX, c.maxZ, c.minX, c.minZ + 1], // west
        [c.maxX, c.maxZ, c.maxX, c.minZ + 1], // east
        [c.minX, c.maxZ, -L.gateHalfWidth, c.maxZ], // south-left
        [L.gateHalfWidth, c.maxZ, c.maxX, c.maxZ], // south-right
      ];
      const postGeo = new THREE.CylinderGeometry(0.05, 0.06, H + 0.6, 6);
      const armGeo = new THREE.BoxGeometry(0.04, 0.5, 0.04);
      const coilPts = [];
      for (const run of fenceRuns) {
        const [x0, z0, x1, z1] = run;
        const len = Math.hypot(x1 - x0, z1 - z0);
        const n = Math.ceil(len / 3);
        const ang = Math.atan2(x1 - x0, z1 - z0);
        for (let i = 0; i < n; i++) {
          const t0 = i / n, t1 = (i + 1) / n;
          const ax = U.lerp(x0, x1, t0), az = U.lerp(z0, z1, t0), bx = U.lerp(x0, x1, t1), bz = U.lerp(z0, z1, t1);
          const segLen = Math.hypot(bx - ax, bz - az);
          const midX = (ax + bx) / 2, midZ = (az + bz) / 2;
          const gy = BI.Terrain.height(midX, midZ);
          const panel = new THREE.PlaneGeometry(segLen, H);
          const uv = panel.attributes.uv;
          for (let k = 0; k < uv.count; k++) uv.setXY(k, uv.getX(k) * segLen * 0.5, uv.getY(k) * H * 0.5);
          B.add(panel, fenceMat, { x: midX, y: gy + H / 2 + 0.05, z: midZ, ry: ang + Math.PI / 2 }, { castShadow: true });
          const py = BI.Terrain.height(ax, az);
          B.add(postGeo, M('steelOut'), { x: ax, y: py + (H + 0.6) / 2 - 0.1, z: az });
          B.add(armGeo, M('steelOut'), { x: ax, y: py + H + 0.6, z: az, rx: 0.5 * Math.cos(ang), rz: -0.5 * Math.sin(ang) });
          for (let k = 0; k < 6; k++) coilPts.push([U.lerp(ax, bx, k / 6), py + H + 0.75, U.lerp(az, bz, k / 6), ang]);
        }
        P.addBox(Math.min(x0, x1) - 0.06, 0, Math.min(z0, z1) - 0.06, Math.max(x0, x1) + 0.06, G + H + 0.8, Math.max(z0, z1) + 0.06, { surface: 'metal', sight: false, bullets: false, wall: false, tag: 'fence' });
      }
      // razor wire coils (instanced torus rings)
      const coil = new THREE.InstancedMesh(new THREE.TorusGeometry(0.28, 0.012, 3, 12), M('steelOut'), coilPts.length);
      const d = new THREE.Object3D();
      coilPts.forEach((p, i) => {
        d.position.set(p[0], p[1], p[2]);
        d.rotation.set(0, p[3], 0.25 * Math.sin(i * 1.3));
        d.updateMatrix();
        coil.setMatrixAt(i, d.matrix);
      });
      coil.frustumCulled = false;
      group.add(coil);
      // gate leaves (swung open) + barrier arm
      for (const s of [-1, 1]) {
        const gx = s * L.gateHalfWidth;
        const leaf = new THREE.Group();
        leaf.position.set(gx, G, c.maxZ);
        leaf.rotation.y = s * 1.9;
        const frame = new THREE.Mesh(new THREE.PlaneGeometry(3.0, 2.6), fenceMat);
        frame.position.set(-s * 1.5, 1.35, 0);
        leaf.add(frame);
        const bar = new THREE.Mesh(new THREE.BoxGeometry(3.0, 0.06, 0.06), M('steelOut'));
        bar.position.set(-s * 1.5, 2.65, 0);
        leaf.add(bar);
        const bar2 = bar.clone();
        bar2.position.y = 0.08;
        leaf.add(bar2);
        group.add(leaf);
      }
      const armStripes = BI.Tex.decal('barrier', { w: 512, h: 32, bg: '#d33', text: '', wear: 0.4 });
      const arm = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.1, 5.6), BI.Mat.standard({ color: 0xd8d8d0, roughness: 0.6 }));
      arm.position.set(L.gateHalfWidth + 0.3, G + 3.3, c.maxZ + 1.2);
      arm.rotation.x = -1.25;
      arm.position.z -= 0;
      group.add(arm);
      void armStripes;
      B.box(L.gateHalfWidth + 0.1, G, c.maxZ + 0.9, L.gateHalfWidth + 0.5, G + 1.1, c.maxZ + 1.4, M('olivePaintOut'));

      // ---- guard booth -------------------------------------------------------------
      const bx = 8.5, bz = -32.5;
      wall(bx - 1.6, G - 0.2, bz - 1.6, bx + 1.6, G + 0.25, bz + 1.6, 'concreteDark', { wall: false }); // step / plinth
      wall(bx - 1.6, G + 0.25, bz - 1.6, bx - 1.4, G + 2.7, bz + 1.6, 'concrete'); // west wall
      wall(bx + 1.4, G + 0.25, bz - 1.6, bx + 1.6, G + 2.7, bz + 1.6, 'concrete'); // east
      wall(bx - 1.6, G + 0.25, bz - 1.6, bx + 1.6, G + 2.7, bz - 1.4, 'concrete'); // north
      wall(bx - 1.6, G + 0.25, bz + 1.4, bx + 0.2, G + 1.1, bz + 1.6, 'concrete'); // south low
      wall(bx - 1.6, G + 2.1, bz + 1.4, bx + 0.2, G + 2.7, bz + 1.6, 'concrete'); // south lintel
      B.box(bx - 1.55, G + 1.1, bz + 1.47, bx + 0.15, G + 2.1, bz + 1.53, M('glassOut'));
      wall(bx - 1.8, G + 2.7, bz - 1.8, bx + 1.8, G + 2.95, bz + 1.8, 'metalDarkOut');
      wall(bx - 1.3, G + 0.25, bz - 1.3, bx + 0.6, G + 1.0, bz - 0.8, 'deskTop', { wall: false });
      const boothGlow = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.06, 0.2), M('amberGlow'));
      boothGlow.position.set(bx, G + 2.6, bz);
      group.add(boothGlow);
      BI.Lighting.addPoint({ pos: new THREE.Vector3(bx, G + 2.4, bz + 0.3), color: 0xffb060, intensity: 1.6, distance: 9, zone: 'outside', priority: 1.2 });
      this.sign('guardpost', { w: 512, h: 128, bg: '#1d2633', text: 'CHECKPOINT 1', color: '#e8e8e0', fontSize: 64 }, 1.4, 0.35, bx - 0.7, G + 2.4, bz + 1.62, 0);

      // ---- floodlights ----------------------------------------------------------------
      const flood = (x, z, tx, ty, tz, h) => {
        const gy = BI.Terrain.height(x, z);
        const top = gy + h;
        B.add(new THREE.CylinderGeometry(0.1, 0.14, h, 8), M('signPost'), { x, y: gy + h / 2, z });
        P.addCyl(x, z, 0.15, gy, top, { surface: 'metal', wall: false });
        const head = new THREE.Group();
        head.position.set(x, top + 0.2, z);
        head.lookAt(tx, ty, tz);
        const housing = new THREE.Mesh(BI.Geo.roundedBox(0.7, 0.5, 0.45, 0.06), M('metalDarkOut'));
        housing.castShadow = true;
        head.add(housing);
        const lens = new THREE.Mesh(new THREE.PlaneGeometry(0.58, 0.38), M('floodLens'));
        lens.position.z = 0.24;
        head.add(lens);
        const cone = BI.FX.makeCone({ radius: 5.5, height: Math.hypot(tx - x, ty - top, tz - z) * 1.05, color: 0xfff1d6, intensity: 0.42 });
        cone.position.z = 0.25;
        head.add(cone);
        group.add(head);
        BI.Lighting.addSpot({
          pos: new THREE.Vector3(x, top + 0.2, z), target: new THREE.Vector3(tx, ty, tz), color: 0xfff0d8,
          intensity: 5.0, distance: 42, angle: 0.48, penumbra: 0.55, decay: 1.2, zone: 'outside', priority: 2,
        });
      };
      flood(-19.5, -30.5, -3, G, -45, 8);
      flood(19.5, -30.5, 4, G, -44, 8);
      flood(17.5, -58, 2, G, -42, 7);

      // wall pack above the bunker door (cool white) + caged lamp in the lobby
      const packGeo = BI.Geo.roundedBox(0.5, 0.22, 0.3, 0.05);
      B.add(packGeo, M('metalDarkOut'), { x: 0, y: G + door.height + 0.75, z: e.maxZ + 0.2 });
      const packLens = new THREE.Mesh(new THREE.PlaneGeometry(0.4, 0.1), M('floodLens'));
      packLens.position.set(0, G + door.height + 0.63, e.maxZ + 0.3);
      packLens.rotation.x = Math.PI / 2;
      group.add(packLens);
      BI.Lighting.addPoint({ pos: new THREE.Vector3(0, G + door.height + 0.5, e.maxZ + 0.8), color: 0xd8e4ff, intensity: 2.2, distance: 12, zone: 'outside', priority: 2 });

      // ---- vents with steam ---------------------------------------------------------
      for (const v of [[-14, -54], [13.5, -51]]) {
        const gy = G;
        B.add(new THREE.CylinderGeometry(0.55, 0.65, 1.6, 14), M('olivePaintOut'), { x: v[0], y: gy + 0.8, z: v[1] });
        B.add(new THREE.CylinderGeometry(0.9, 0.7, 0.3, 14), M('metalDarkOut'), { x: v[0], y: gy + 1.85, z: v[1] });
        B.add(new THREE.CylinderGeometry(0.5, 0.5, 0.25, 14, 1, true), M('blackMatte'), { x: v[0], y: gy + 1.65, z: v[1] });
        P.addCyl(v[0], v[1], 0.75, gy, gy + 2, { surface: 'metal', wall: false });
        BI.FX.addEmitter({ pos: new THREE.Vector3(v[0], gy + 1.75, v[1]), kind: 'steam', rate: 7, range: 55 });
      }

      // ---- antenna mast on the hill with a blinking beacon ---------------------------
      const mx = 11, mz = -67;
      const my = BI.Terrain.height(mx, mz) - 0.3;
      const mh = 16;
      const leg = new THREE.CylinderGeometry(0.05, 0.05, 1, 5);
      for (const [lx, lz] of [[-0.6, -0.6], [0.6, -0.6], [0.6, 0.6], [-0.6, 0.6]]) {
        B.add(leg, M('steelOut'), { x: mx + lx * 0.8, y: my + mh / 2, z: mz + lz * 0.8, sy: mh, rx: -lz * 0.03, rz: lx * 0.03 });
      }
      for (let k = 0; k < 10; k++) {
        const y = my + 1 + k * 1.5;
        const w = 0.96 - k * 0.04;
        for (const rot of [0, Math.PI / 2]) {
          B.add(leg, M('steelOut'), { x: mx, y, z: mz + (rot ? 0 : w), sy: w * 2.0, rz: Math.PI / 2, ry: rot });
          B.add(leg, M('steelOut'), { x: mx, y, z: mz - (rot ? 0 : w), sy: w * 2.0, rz: Math.PI / 2, ry: rot });
        }
      }
      B.add(new THREE.BoxGeometry(0.6, 1.2, 0.15), M('boatTrim'), { x: mx + 0.5, y: my + mh - 2, z: mz });
      const beacon = new THREE.Mesh(new THREE.SphereGeometry(0.16, 10, 8), M('redLamp').clone());
      beacon.position.set(mx, my + mh + 0.3, mz);
      group.add(beacon);
      this.beacons.push(beacon);
      // satellite dish on the roof
      const dish = new THREE.Mesh(new THREE.SphereGeometry(1.0, 18, 8, 0, Math.PI * 2, 0, 0.9), M('boatTrim'));
      dish.material = M('boatTrim');
      dish.position.set(-3.5, e.roofY + 1.6, -57);
      dish.rotation.set(-0.9, 0.6, 0);
      dish.castShadow = true;
      group.add(dish);
      B.add(new THREE.CylinderGeometry(0.08, 0.1, 1.2, 8), M('steelOut'), { x: -3.5, y: e.roofY + 1.0, z: -57 });

      // ---- truck ------------------------------------------------------------------
      const tx = -12, tz = -40;
      const truck = (x0, y0, z0, x1, y1, z1, mat) => B.box(tx + x0, G + y0, tz + z0, tx + x1, G + y1, tz + z1, M(mat), { worldUV: 0.5 });
      truck(-1.2, 0.7, -3.4, 1.2, 1.1, 3.0, 'metalDarkOut'); // chassis
      truck(-1.2, 1.1, 1.0, 1.2, 2.6, 3.0, 'olivePaintOut'); // cab
      truck(-1.15, 1.9, 2.98, 1.15, 2.5, 3.05, 'glassOut'); // windscreen
      truck(-1.25, 1.1, -3.4, 1.25, 1.25, 0.9, 'olivePaintOut'); // bed floor
      truck(-1.25, 1.25, -3.4, -1.15, 2.0, 0.9, 'olivePaintOut');
      truck(1.15, 1.25, -3.4, 1.25, 2.0, 0.9, 'olivePaintOut');
      truck(-1.25, 1.25, -3.5, 1.25, 2.0, -3.4, 'olivePaintOut');
      // canvas cover
      B.add(new THREE.CylinderGeometry(1.3, 1.3, 4.2, 12, 1, true, -Math.PI / 2, Math.PI), M('vest'), { x: tx, y: G + 2.0, z: tz - 1.25, rx: Math.PI / 2, rz: 0 });
      const wheel = new THREE.CylinderGeometry(0.48, 0.48, 0.35, 14);
      for (const wz of [-2.4, -1.3, 2.0]) for (const s of [-1, 1]) B.add(wheel, M('rubber'), { x: tx + s * 1.15, y: G + 0.48, z: tz + wz, rz: Math.PI / 2 });
      P.addBox(tx - 1.3, G, tz - 3.5, tx + 1.3, G + 2.6, tz + 3.1, { surface: 'metal' });
      const headl = new THREE.Mesh(new THREE.PlaneGeometry(0.25, 0.15), M('amberGlow'));
      headl.position.set(tx + 0.85, G + 1.3, tz + 3.06);
      group.add(headl);

      // ---- sandbags, crates, barrels -------------------------------------------------
      const bag = BI.Geo.roundedBox(0.62, 0.2, 0.36, 0.08);
      const sandRow = (cx, cz, ang, n, rows) => {
        for (let rI = 0; rI < rows; rI++) {
          for (let i = 0; i < n; i++) {
            const off = (i - n / 2 + (rI % 2) * 0.5) * 0.6;
            B.add(bag, M('sandbag'), { x: cx + Math.cos(ang) * off, y: G + 0.11 + rI * 0.2, z: cz - Math.sin(ang) * off, ry: ang + (U.hash2(i, rI, 5) - 0.5) * 0.15 });
          }
        }
        const hx = Math.abs(Math.cos(ang)) * n * 0.3 + 0.2, hz = Math.abs(Math.sin(ang)) * n * 0.3 + 0.2;
        P.addBox(cx - hx, G, cz - hz, cx + hx, G + rows * 0.2, cz + hz, { surface: 'fabric', wall: false });
      };
      sandRow(-6.5, -31.5, 0, 6, 4);
      sandRow(6.8, -44, 0.3, 5, 4);
      sandRow(-15, -47, Math.PI / 2, 5, 3);
      const crate = (x, z, s, ry, y) => {
        B.add(BI.Geo.unitBox, M('crate'), { x, y: (y || G) + s / 2, z, s, ry });
        P.addBox(x - s / 2, y || G, z - s / 2, x + s / 2, (y || G) + s, z + s / 2, { surface: 'wood' });
      };
      crate(16, -38, 1.2, 0.1); crate(17.3, -38.2, 1.2, -0.05); crate(16.6, -38.1, 1.0, 0.3, G + 1.2);
      crate(-18, -58, 1.3, 0.2); crate(-16.6, -59, 1.1, 0); crate(-9, -45, 1.0, 0.5);
      const barrel = new THREE.CylinderGeometry(0.3, 0.3, 0.9, 12);
      for (const b of [[18.5, -45], [19.2, -45.6], [18.6, -46.3], [-19, -36], [7.5, -59]]) {
        B.add(barrel, M(b[0] > 0 ? 'olivePaintOut' : 'rust'), { x: b[0], y: G + 0.45, z: b[1] });
        P.addCyl(b[0], b[1], 0.32, G, G + 0.9, { surface: 'metal', wall: false });
      }
      // transformer box against the building
      wall(-8.6, G, -54, -6.1, G + 2.1, -52.4, 'olivePaintOut');
      for (let k = 0; k < 7; k++) B.box(-8.65, G + 0.3 + k * 0.22, -53.8, -8.6, G + 0.42 + k * 0.22, -52.6, M('metalDarkOut'));

      // ---- signage ---------------------------------------------------------------------
      this.sign('restricted', { w: 512, h: 320, bg: '#e6e2d6', border: '#111', header: 'RESTRICTED', text: 'AUTHORISED PERSONNEL\nONLY BEYOND THIS POINT', headerBg: '#b3241b' },
        1.3, 0.8, -6.5, G + 1.7, c.maxZ - 0.05, Math.PI);
      this.sign('restricted', { w: 512, h: 320, bg: '#e6e2d6', border: '#111', header: 'RESTRICTED', text: 'AUTHORISED PERSONNEL\nONLY BEYOND THIS POINT', headerBg: '#b3241b' },
        1.3, 0.8, 9, G + 1.7, c.maxZ + 0.05, 0);
      this.sign('voltage', { w: 512, h: 320, bg: '#e8b52a', border: '#111', header: 'DANGER', text: 'HIGH\nVOLTAGE', icon: 'bolt', headerBg: '#111', headerColor: '#e8b52a' },
        0.9, 0.56, -8.63, G + 1.5, -53.2, -Math.PI / 2);
      this.sign('biohazard', { w: 512, h: 320, bg: '#e6e2d6', border: '#111', header: 'BIOHAZARD', text: 'LEVEL 4\nCONTAINMENT', icon: 'bio', headerBg: '#d9a521', headerColor: '#111' },
        0.95, 0.6, 2.9, G + 1.8, e.maxZ + 0.02, 0);
      this.sign('stencilB1', { w: 512, h: 256, text: 'B-1', color: 'rgba(230,226,214,0.85)', font: 'Impact, Arial Black, sans-serif', fontSize: 210, wear: 1.5 },
        2.4, 1.2, 0, G + door.height + 1.4, e.maxZ + 0.42, 0);
      for (const z of [-40, -52]) {
        for (const s of [-1, 1]) {
          this.sign('restricted', { w: 512, h: 320, bg: '#e6e2d6', border: '#111', header: 'RESTRICTED', text: 'AUTHORISED PERSONNEL\nONLY BEYOND THIS POINT', headerBg: '#b3241b' },
            1.1, 0.68, s * (c.maxX + 0.06), G + 1.6, z, s * Math.PI / 2);
        }
      }

      B.build(group);
      BI.Engine.addUpdate((dt, t) => {
        const on = Math.sin(t * 3.0) > 0.6 ? 3.5 : 0.15;
        for (const b of this.beacons) b.material.emissiveIntensity = on;
      }, 93);
      return group;
    },
  };

  BI.BunkerExterior = Ext;
})();
