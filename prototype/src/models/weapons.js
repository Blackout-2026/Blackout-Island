/* =============================================================================
 * models/weapons.js — first-person viewmodels (SMG, sniper, knife)
 * -----------------------------------------------------------------------------
 * Rendered in their own scene/camera (core/engine.js ViewmodelPass) so they
 * never clip into walls. Camera space: +X right, +Y up, -Z forward.
 * Each model returns { group, muzzle (Object3D at the barrel tip), parts } —
 * the weapon system animates sway, bob, recoil, bolt and swing on top.
 * Hands and sleeves are part of every viewmodel (tactical gloves, dark sleeves).
 * Role: Graphics (models) / Core Developer (weapon system).
 * ========================================================================== */
(function () {
  'use strict';

  function mats() {
    if (mats.cache) return mats.cache;
    const track = (m) => BI.Mat.track(m, 'view');
    const metal = BI.Mat.standard({ color: 0x2e3135, roughness: 0.38, metalness: 0.85 });
    const polymer = BI.Mat.standard({ color: 0x1b1d1f, roughness: 0.6, metalness: 0.1 });
    const tan = BI.Mat.standard({ color: 0x6b604a, roughness: 0.7 });
    const steel = BI.Mat.standard({ color: 0xb8bec2, roughness: 0.18, metalness: 1.0 });
    const glove = BI.Mat.standard({ ...BI.Tex.get('fabric'), color: 0x2b2c2a, roughness: 0.9 });
    const sleeve = BI.Mat.standard({ ...BI.Tex.get('fabric'), color: 0x3a4030, roughness: 1 });
    const lens = new THREE.MeshBasicMaterial({ color: new THREE.Color(0.05, 0.12, 0.16) });
    const dot = new THREE.MeshBasicMaterial({ color: new THREE.Color(8, 0.4, 0.2) });
    mats.cache = { metal, polymer, tan, steel, glove, sleeve, lens, dot };
    for (const m of [metal, polymer, tan, steel, glove, sleeve]) track(m);
    return mats.cache;
  }

  const box = (w, h, d) => new THREE.BoxGeometry(w, h, d);
  const cyl = (r0, r1, h, s) => new THREE.CylinderGeometry(r0, r1, h, s || 12);
  function add(g, geo, mat, x, y, z, rx, ry, rz) {
    const m = new THREE.Mesh(geo, mat);
    m.position.set(x || 0, y || 0, z || 0);
    m.rotation.set(rx || 0, ry || 0, rz || 0);
    g.add(m);
    return m;
  }
  const alongZ = (g) => (g.rotateX(Math.PI / 2), g);

  // Gloved hand + sleeve reaching back/down out of view.
  function arm(g, M, x, y, z, rx, ry, rz, len) {
    const a = new THREE.Group();
    a.position.set(x, y, z);
    a.rotation.set(rx, ry, rz);
    add(a, BI.Geo.roundedBox(0.055, 0.035, 0.1, 0.012), M.glove, 0, 0, 0);
    add(a, box(0.05, 0.025, 0.035), M.glove, 0.0, -0.012, -0.06, 0.6, 0, 0); // fingers wrapped
    add(a, alongZ(cyl(0.032, 0.036, 0.12, 10)), M.glove, 0, 0, 0.1);
    add(a, alongZ(cyl(0.042, 0.05, len || 0.4, 10)), M.sleeve, 0, 0, 0.16 + (len || 0.4) / 2);
    g.add(a);
    return a;
  }

  function smg() {
    const M = mats();
    const g = new THREE.Group();
    const gun = new THREE.Group();
    g.add(gun);
    add(gun, BI.Geo.roundedBox(0.05, 0.068, 0.27, 0.012), M.metal, 0, 0, 0);
    add(gun, alongZ(cyl(0.02, 0.02, 0.13)), M.polymer, 0, 0.004, -0.19);
    add(gun, alongZ(cyl(0.012, 0.012, 0.06)), M.metal, 0, 0.004, -0.28);
    add(gun, alongZ(cyl(0.016, 0.016, 0.03)), M.metal, 0, 0.004, -0.31);
    add(gun, box(0.032, 0.16, 0.048), M.polymer, 0, -0.1, -0.03, 0.12);
    add(gun, box(0.034, 0.095, 0.044), M.polymer, 0, -0.07, 0.085, -0.3);
    add(gun, box(0.028, 0.05, 0.03), M.polymer, 0, -0.045, -0.14, 0.2); // foregrip
    add(gun, box(0.03, 0.012, 0.22), M.metal, 0, 0.04, -0.01); // rail
    add(gun, box(0.04, 0.035, 0.05), M.metal, 0, 0.065, 0.02); // red dot body
    add(gun, box(0.032, 0.028, 0.003), M.lens, 0, 0.068, -0.004);
    add(gun, new THREE.SphereGeometry(0.0035, 6, 4), M.dot, 0, 0.068, 0.0);
    add(gun, box(0.012, 0.012, 0.03), M.steel, 0.03, 0.02, 0.06); // charging handle
    // folded wire stock along the right side
    add(gun, box(0.008, 0.008, 0.18), M.metal, 0.03, 0.012, -0.02);
    add(gun, box(0.008, 0.008, 0.18), M.metal, 0.03, -0.02, -0.02);
    add(gun, box(0.006, 0.04, 0.01), M.polymer, 0.03, -0.004, -0.11);
    arm(g, M, 0.0, -0.075, 0.075, 0.8, 0.5, 0.0, 0.35); // right hand on grip, sleeve down-right-back
    arm(g, M, -0.012, -0.06, -0.14, 0.7, -0.6, 0.0, 0.4); // left hand on foregrip, sleeve down-left-back
    const muzzle = new THREE.Object3D();
    muzzle.position.set(0, 0.004, -0.33);
    gun.add(muzzle);
    g.position.set(0.13, -0.125, -0.45);
    g.rotation.y = -0.04;
    return { group: g, gun, muzzle, hip: new THREE.Vector3(0.13, -0.125, -0.45) };
  }

  function sniper() {
    const M = mats();
    const g = new THREE.Group();
    const gun = new THREE.Group();
    g.add(gun);
    add(gun, BI.Geo.roundedBox(0.05, 0.06, 0.3, 0.012), M.metal, 0, 0, 0);
    add(gun, alongZ(cyl(0.013, 0.016, 0.62)), M.metal, 0, 0.008, -0.45);
    add(gun, alongZ(cyl(0.02, 0.02, 0.06)), M.metal, 0, 0.008, -0.77);
    add(gun, BI.Geo.roundedBox(0.055, 0.075, 0.48, 0.02), M.tan, 0, -0.03, 0.05); // stock body
    add(gun, BI.Geo.roundedBox(0.05, 0.11, 0.18, 0.02), M.tan, 0, -0.04, 0.33); // butt
    add(gun, box(0.04, 0.03, 0.12), M.polymer, 0, 0.035, 0.27); // cheek rest
    add(gun, box(0.03, 0.08, 0.06), M.polymer, 0, -0.09, -0.02, 0.1); // magazine
    // scope
    add(gun, alongZ(cyl(0.024, 0.024, 0.24, 16)), M.metal, 0, 0.075, -0.02);
    add(gun, alongZ(cyl(0.034, 0.024, 0.06, 16)), M.metal, 0, 0.075, -0.17);
    add(gun, alongZ(cyl(0.024, 0.03, 0.05, 16)), M.metal, 0, 0.075, 0.12);
    add(gun, alongZ(cyl(0.031, 0.031, 0.004, 16)), M.lens, 0, 0.075, -0.2);
    for (const z of [-0.08, 0.05]) add(gun, box(0.03, 0.035, 0.02), M.metal, 0, 0.045, z);
    add(gun, cyl(0.012, 0.012, 0.04), M.metal, 0.03, 0.075, -0.01, 0, 0, Math.PI / 2); // turret
    // bolt handle (animated)
    const bolt = new THREE.Group();
    bolt.position.set(0.03, 0.01, 0.08);
    add(bolt, cyl(0.006, 0.006, 0.06, 6), M.steel, 0.03, 0, 0, 0, 0, Math.PI / 2);
    add(bolt, new THREE.SphereGeometry(0.012, 8, 6), M.steel, 0.06, 0, 0);
    gun.add(bolt);
    // folded bipod
    for (const s of [-1, 1]) add(gun, box(0.008, 0.008, 0.2), M.metal, s * 0.018, -0.03, -0.3);
    arm(g, M, 0.0, -0.07, 0.12, 0.8, 0.5, 0.0, 0.35);
    arm(g, M, -0.01, -0.06, -0.22, 0.65, -0.55, 0.0, 0.45);
    const muzzle = new THREE.Object3D();
    muzzle.position.set(0, 0.008, -0.81);
    gun.add(muzzle);
    g.position.set(0.12, -0.12, -0.46);
    g.rotation.y = -0.04;
    return { group: g, gun, muzzle, bolt, hip: new THREE.Vector3(0.12, -0.12, -0.46) };
  }

  function knife() {
    const M = mats();
    const g = new THREE.Group();
    const hand = new THREE.Group();
    g.add(hand);
    const blade = new THREE.Group();
    hand.add(blade);
    add(blade, alongZ(cyl(0.016, 0.018, 0.12, 10)), M.polymer, 0, 0, 0.0);
    for (let i = 0; i < 4; i++) add(blade, alongZ(cyl(0.019, 0.019, 0.006, 10)), M.polymer, 0, 0, -0.04 + i * 0.025);
    add(blade, box(0.07, 0.012, 0.014), M.metal, 0, 0, -0.065);
    // tapered blade with a darker spine
    const shape = new THREE.Shape();
    shape.moveTo(0, 0);
    shape.lineTo(0.18, 0.004);
    shape.lineTo(0.205, 0.022);
    shape.lineTo(0.0, 0.032);
    shape.lineTo(0, 0);
    const bg = new THREE.ExtrudeGeometry(shape, { depth: 0.004, bevelEnabled: true, bevelThickness: 0.002, bevelSize: 0.002, bevelSegments: 1 });
    bg.translate(0, -0.016, -0.002);
    bg.rotateY(Math.PI / 2);
    add(blade, bg, M.steel, 0, 0, -0.07);
    add(blade, box(0.005, 0.006, 0.17), M.metal, 0, 0.014, -0.15);
    blade.rotation.set(0.0, 0, 0);
    arm(hand, M, 0, -0.008, 0.02, 0.6, 0.35, 0, 0.42);
    hand.position.set(0.17, -0.18, -0.33);
    hand.rotation.set(0.25, 0.25, -0.4);
    return { group: g, hand, blade, muzzle: new THREE.Object3D(), hip: new THREE.Vector3(0, 0, 0) };
  }

  // Muzzle flash: crossed additive planes + a point light in the view scene.
  function muzzleFlash() {
    const tex = BI.Tex.get('flash').map;
    const m = new THREE.MeshBasicMaterial({ map: tex, color: new THREE.Color(3.5, 2.6, 1.6), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const g = new THREE.Group();
    const a = new THREE.Mesh(new THREE.PlaneGeometry(0.22, 0.22), m);
    const b = a.clone();
    b.rotation.y = Math.PI / 2;
    const c = a.clone();
    c.rotation.x = Math.PI / 2;
    c.scale.set(0.7, 0.7, 0.7);
    a.rotation.y = 0;
    g.add(a, b, c);
    g.visible = false;
    return { group: g, mat: m };
  }

  BI.Models = BI.Models || {};
  BI.Models.viewmodels = { smg, sniper, knife, muzzleFlash };
})();
