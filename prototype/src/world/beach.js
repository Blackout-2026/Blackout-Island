/* =============================================================================
 * world/beach.js — the landing: dock, boat, lantern, props
 * -----------------------------------------------------------------------------
 * The boat hull is lofted from cross-sections (stern -> bow) into a custom
 * BufferGeometry, then dressed with a cabin, windshield, railings, outboard
 * motor, fenders, a rope coil and a lantern whose flame flickers (it drives a
 * virtual point light, see world/lighting.js). The boat bobs on the waves.
 * Role: World Builder.
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;
  const L = BI.Level;

  function hullGeometry() {
    // stations along local z (stern at -4.2, bow at +4.6)
    const st = [];
    const N = 14;
    for (let i = 0; i <= N; i++) {
      const t = i / N;
      const z = -4.2 + t * 8.8;
      const bowT = U.smoothstep(0.55, 1.0, t);
      const halfW = 1.45 * (1 - Math.pow(bowT, 1.6) * 0.97);
      const depth = 1.0 - bowT * 0.35;
      const sheer = 1.15 + Math.pow(bowT, 2) * 0.55; // top edge rises at the bow
      st.push({ z, halfW, depth, sheer });
    }
    const pos = [], uv = [], idx = [];
    const ring = (s) => [
      [s.halfW, s.sheer], [s.halfW * 0.98, s.sheer * 0.5], [s.halfW * 0.8, -s.depth * 0.45],
      [s.halfW * 0.35, -s.depth * 0.9], [0, -s.depth], [-s.halfW * 0.35, -s.depth * 0.9],
      [-s.halfW * 0.8, -s.depth * 0.45], [-s.halfW * 0.98, s.sheer * 0.5], [-s.halfW, s.sheer],
    ];
    const R = 9;
    st.forEach((s, i) => {
      ring(s).forEach((p, k) => {
        pos.push(p[0], p[1], s.z);
        uv.push(k / (R - 1), i / N * 3);
      });
    });
    for (let i = 0; i < N; i++) {
      for (let k = 0; k < R - 1; k++) {
        const a = i * R + k, b = (i + 1) * R + k;
        idx.push(a, b, a + 1, b, b + 1, a + 1);
      }
    }
    // transom (flat stern)
    const base = pos.length / 3;
    ring(st[0]).forEach((p) => { pos.push(p[0], p[1], st[0].z); uv.push(p[0], p[1]); });
    for (let k = 1; k < R - 1; k++) idx.push(base, base + k + 1, base + k);
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    g.setIndex(idx);
    g.computeVertexNormals();
    return { geo: g, stations: st };
  }

  const Beach = {
    boat: null,
    lantern: null,

    build(scene) {
      const P = BI.Physics;
      const M = (n) => BI.Mat.get(n);
      const d = L.dock;
      const group = new THREE.Group();
      group.name = 'beach';
      scene.add(group);
      const B = new BI.Geo.StaticBatcher('dock');

      // ---- dock ---------------------------------------------------------------
      const deckY = d.y;
      for (let z = d.zStart; z < d.zEnd; z += 0.32) {
        const w = d.halfWidth;
        const jitter = (U.hash2(Math.floor(z * 10), 1, 3) - 0.5) * 0.04;
        B.box(-w, deckY - 0.07 + jitter * 0.3, z, w, deckY + jitter * 0.3, z + 0.29, M('wood'), { worldUV: 0.5 });
      }
      // stringers + posts
      B.box(-d.halfWidth + 0.1, deckY - 0.32, d.zStart, -d.halfWidth + 0.28, deckY - 0.07, d.zEnd, M('woodDark'), { worldUV: 0.5 });
      B.box(d.halfWidth - 0.28, deckY - 0.32, d.zStart, d.halfWidth - 0.1, deckY - 0.07, d.zEnd, M('woodDark'), { worldUV: 0.5 });
      const postGeo = new THREE.CylinderGeometry(0.14, 0.16, 1, 8);
      for (let z = d.zStart + 1; z <= d.zEnd; z += 3) {
        for (const sx of [-1, 1]) {
          const top = deckY + (z > d.zEnd - 4 || (Math.round(z) % 6 === 0) ? 0.9 : 0.25);
          const bottom = -4;
          B.add(postGeo, M('woodDark'), { x: sx * (d.halfWidth + 0.1), y: (top + bottom) / 2, z, sy: top - bottom });
          // railing beam on the left side and partly on the right (gap at the boat)
          const rightGap = sx > 0 && z > L.boat.z - 4 && z < L.boat.z + 4;
          if (!rightGap && z + 3 <= d.zEnd + 0.5) {
            B.box(sx * (d.halfWidth + 0.1) - 0.05, deckY + 0.8, z, sx * (d.halfWidth + 0.1) + 0.05, deckY + 0.9, Math.min(z + 3, d.zEnd), M('woodDark'), { worldUV: 0.5 });
          }
        }
      }
      // cleats + rope coil on the dock
      const cleat = new THREE.BoxGeometry(0.35, 0.08, 0.1);
      B.add(cleat, M('steelOut'), { x: 1.15, y: deckY + 0.05, z: L.boat.z - 2.5 });
      B.add(cleat, M('steelOut'), { x: 1.15, y: deckY + 0.05, z: L.boat.z + 2.5 });
      const coil = new THREE.TorusGeometry(0.28, 0.05, 6, 16);
      coil.rotateX(Math.PI / 2);
      for (let i = 0; i < 4; i++) B.add(coil, M('rope'), { x: -0.7, y: deckY + 0.04 + i * 0.06, z: 112, s: 1 - i * 0.12 });
      // crates and a fuel barrel at the dock root
      B.add(BI.Geo.unitBox, M('crate'), { x: -0.6, y: deckY + 0.3, z: 100, s: 0.6 });
      B.add(BI.Geo.unitBox, M('crate'), { x: -0.55, y: deckY + 0.75, z: 100.1, s: 0.45, ry: 0.4 });
      const barrel = new THREE.CylinderGeometry(0.3, 0.3, 0.9, 12);
      B.add(barrel, M('olivePaintOut'), { x: 0.85, y: deckY + 0.45, z: 101 });
      B.build(group);

      // dock collision: deck + rails (open toward the boat)
      P.addBox(-d.halfWidth, deckY - 0.35, d.zStart, d.halfWidth, deckY, d.zEnd, { surface: 'wood', tag: 'dock' });
      P.addBox(-d.halfWidth - 0.2, deckY, d.zStart + 1, -d.halfWidth, deckY + 1.0, d.zEnd, { surface: 'wood', sight: false, bullets: false, wall: false });
      P.addBox(d.halfWidth, deckY, d.zStart + 1, d.halfWidth + 0.2, deckY + 1.0, L.boat.z - 4, { surface: 'wood', sight: false, bullets: false, wall: false });
      P.addBox(-d.halfWidth - 0.2, deckY, d.zEnd, d.halfWidth + 0.2, deckY + 1.0, d.zEnd + 0.2, { surface: 'wood', sight: false, bullets: false, wall: false });

      // ---- warning sign at the dock root ---------------------------------------
      const signTex = BI.Tex.decal('nolanding', {
        w: 512, h: 320, bg: '#d8d3c4', border: '#111', header: 'WARNING', text: 'PRIVATE ISLAND\nNO LANDING\nTRESPASSERS WILL BE SHOT', icon: null,
      });
      const sign = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 0.88), BI.Mat.standard({ map: signTex, roughness: 0.8 }));
      sign.position.set(-2.6, 2.1, 95.5);
      sign.rotation.y = Math.PI * 0.92;
      group.add(sign);
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 2.4, 6), M('signPost'));
      pole.position.set(-2.6, 0.9 + BI.Terrain.height(-2.6, 95.6) * 0.5, 95.6);
      pole.scale.y = 1;
      pole.position.y = BI.Terrain.height(-2.6, 95.6) + 1.2;
      sign.position.y = pole.position.y + 0.85;
      group.add(pole);
      P.addCyl(-2.6, 95.6, 0.1, 0, 4, { surface: 'metal', wall: false });

      // ---- boat -------------------------------------------------------------------
      const boat = new THREE.Group();
      boat.position.set(L.boat.x, 0.15, L.boat.z);
      boat.rotation.y = 0.05;
      group.add(boat);
      const hull = hullGeometry();
      const hullMesh = new THREE.Mesh(hull.geo, M('boatHull'));
      hullMesh.castShadow = true;
      hullMesh.receiveShadow = true;
      boat.add(hullMesh);
      const BB = new BI.Geo.StaticBatcher('boat');
      // gunwale trim + waterline stripe
      for (const s of [-1, 1]) {
        BB.box(s * 1.4 - 0.06, 1.12, -4.2, s * 1.4 + 0.06, 1.22, 2.0, M('boatTrim'));
      }
      // deck
      BB.box(-1.3, 0.55, -4.1, 1.3, 0.62, 2.6, M('wood'), { worldUV: 0.6 });
      // cabin
      BB.box(-1.0, 0.62, -1.4, 1.0, 2.25, 1.0, M('boatTrim'));
      BB.box(-1.08, 2.25, -1.6, 1.08, 2.35, 1.2, M('metalDarkOut'));
      BB.box(-1.01, 1.55, 0.98, 1.01, 2.15, 1.02, M('glassOut'));
      BB.box(-1.01, 1.55, -1.25, -0.99, 2.1, 0.8, M('glassOut'));
      BB.box(0.99, 1.55, -1.25, 1.01, 2.1, 0.8, M('glassOut'));
      // radar mast + antenna
      BB.add(new THREE.CylinderGeometry(0.04, 0.05, 1.4, 6), M('steelOut'), { x: 0.4, y: 3.0, z: -0.6 });
      BB.add(new THREE.BoxGeometry(0.9, 0.08, 0.16), M('metalDarkOut'), { x: 0.4, y: 3.5, z: -0.6 });
      // railings
      const railGeo = new THREE.CylinderGeometry(0.025, 0.025, 1, 5);
      for (let z = -3.8; z <= 2.2; z += 1.2) {
        for (const s of [-1, 1]) BB.add(railGeo, M('steelOut'), { x: s * 1.3, y: 1.5, z, sy: 0.6 });
      }
      for (const s of [-1, 1]) BB.add(railGeo, M('steelOut'), { x: s * 1.3, y: 1.8, z: -0.8, rx: Math.PI / 2, sy: 6.0 });
      // outboard motor
      BB.add(BI.Geo.roundedBox(0.55, 0.8, 0.5, 0.12), M('plasticDark'), { x: 0, y: 1.0, z: -4.55 });
      BB.add(new THREE.BoxGeometry(0.14, 1.0, 0.2), M('metalDarkOut'), { x: 0, y: 0.25, z: -4.55 });
      // fenders
      const fender = new THREE.CylinderGeometry(0.13, 0.13, 0.55, 10);
      for (const z of [-2.5, -0.5, 1.5]) BB.add(fender, M('boatTrim'), { x: -1.48, y: 0.75, z });
      // jerry cans, rope coil
      BB.add(new THREE.BoxGeometry(0.35, 0.45, 0.18), M('olivePaintOut'), { x: 0.8, y: 0.85, z: -3.2 });
      BB.add(new THREE.BoxGeometry(0.35, 0.45, 0.18), M('olivePaintOut'), { x: 0.8, y: 0.85, z: -2.95 });
      for (let i = 0; i < 3; i++) BB.add(coil, M('rope'), { x: -0.6, y: 0.66 + i * 0.06, z: -3.2, s: 1.1 - i * 0.15 });
      // lantern pole
      BB.add(new THREE.CylinderGeometry(0.03, 0.03, 1.1, 6), M('steelOut'), { x: -1.2, y: 1.75, z: 1.7 });
      BB.build(boat);

      // lantern (emissive glass + cage)
      const lantern = new THREE.Group();
      lantern.position.set(-1.2, 2.45, 1.7);
      const glassMat = M('lanternGlass').clone();
      const glass = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.1, 0.24, 10), glassMat);
      lantern.add(glass);
      const cap = new THREE.Mesh(new THREE.ConeGeometry(0.14, 0.12, 10), M('metalDarkOut'));
      cap.position.y = 0.18;
      lantern.add(cap);
      const baseM = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.05, 10), M('metalDarkOut'));
      baseM.position.y = -0.14;
      lantern.add(baseM);
      boat.add(lantern);
      // rope from boat to dock cleats (catenary)
      const ropePts = (ax, az, bx, bz) => {
        const pts = [];
        for (let i = 0; i <= 10; i++) {
          const t = i / 10;
          pts.push(new THREE.Vector3(U.lerp(ax, bx, t), deckY + 0.05 - Math.sin(t * Math.PI) * 0.35 + (1 - t) * -0.15, U.lerp(az, bz, t)));
        }
        return pts;
      };
      for (const dz of [-2.5, 2.5]) {
        const rope = new THREE.Mesh(BI.Geo.tube(ropePts(1.15, L.boat.z + dz, L.boat.x - 1.35, L.boat.z + dz * 0.8), 0.025, 16, 5), M('rope'));
        group.add(rope);
      }

      // boat collision (deck walkable, hull sides block)
      const bx = L.boat.x, bz = L.boat.z;
      P.addBox(bx - 1.35, -1, bz - 4.2, bx + 1.35, 0.78, bz + 2.6, { surface: 'wood', tag: 'boat' });
      P.addBox(bx - 1.0, 0.78, bz - 1.4, bx + 1.0, 2.4, bz + 1.0, { surface: 'metal', tag: 'boat' });
      P.addBox(bx + 1.35, -1, bz - 4.2, bx + 1.5, 1.6, bz + 2.6, { surface: 'metal', sight: false, wall: false });

      this.boat = boat;
      this.lanternMat = glassMat;
      this.light = BI.Lighting.addPoint({ pos: new THREE.Vector3(bx - 1.2, 2.75, bz + 1.7), color: 0xffa64d, intensity: 1.7, distance: 15, decay: 1.6, zone: 'outside', priority: 3, group: 'lantern' });
      // a dim cold work light at the dock root
      BI.Lighting.addPoint({ pos: new THREE.Vector3(-1.2, 3.0, 98.5), color: 0x9fb8ff, intensity: 0.0, distance: 10, zone: 'outside' });

      BI.Engine.addUpdate((dt, t) => this.update(dt, t), 92);
    },

    update(dt, t) {
      const b = this.boat;
      b.position.y = 0.15 + Math.sin(t * 0.9) * 0.06 + Math.sin(t * 1.7) * 0.03;
      b.rotation.z = Math.sin(t * 0.8) * 0.025;
      b.rotation.x = Math.sin(t * 0.6 + 1) * 0.015;
      const flick = 0.85 + Math.sin(t * 13) * 0.05 + Math.sin(t * 29.7) * 0.05 + (Math.random() - 0.5) * 0.08;
      this.light.mul = flick;
      this.lanternMat.emissiveIntensity = 4.0 * flick;
      this.light.pos.set(L.boat.x - 1.2, 2.75 + (b.position.y - 0.15), L.boat.z + 1.7);
    },
  };

  BI.Beach = Beach;
})();
