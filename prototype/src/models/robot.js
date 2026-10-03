/* =============================================================================
 * models/robot.js — HERO MODEL 2: the Tactical Guard security robot
 * -----------------------------------------------------------------------------
 * ~2.1 m bipedal robot built like a custom Blender asset, as ONE SkinnedMesh
 * (body material + emissive material, so two draw calls):
 *   - digitigrade (reverse-knee) legs with armoured thighs, knee actuators,
 *     hydraulic pistons (housing on the thigh, rod on the shin), clawed feet
 *   - segmented lower torso (stacked rings), armoured chest with hazard
 *     accents, back power pack with glowing cells, vent grille
 *   - shoulder plates, upper-arm pistons, left claw hand, right forearm
 *     replaced by a twin-barrel weapon pod (muzzle point for hitscan/tracers)
 *   - head with a dark visor, an emissive eye bar and a scanner dot that
 *     sweeps side to side (a small non-skinned mesh parented to the head bone)
 * The eye colour reports state: amber = idle/patrol, red = combat.
 * Role: Graphics (hero asset) / Agent Controller (animation hooks).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  function buildRobot() {
    const B = new BI.Models.SkinBuilder();
    const G = BI.Models.prims;
    const P = (bone, g, t, c, e) => B.part(bone, g, t, c, e);
    // skeleton
    B.bone('pelvis', null, 0, 1.05, 0);
    B.bone('spine', 'pelvis', 0, 0.14, 0);
    B.bone('chest', 'spine', 0, 0.3, 0);
    B.bone('head', 'chest', 0, 0.44, 0.02);
    for (const [s, n] of [[1, 'L'], [-1, 'R']]) {
      B.bone('upperArm' + n, 'chest', 0.37 * s, 0.27, 0);
      B.bone('foreArm' + n, 'upperArm' + n, 0, -0.34, 0);
      B.bone('hand' + n, 'foreArm' + n, 0, -0.32, 0);
      B.bone('thigh' + n, 'pelvis', 0.17 * s, -0.06, 0);
      B.bone('shin' + n, 'thigh' + n, 0, -0.42, 0.13);
      B.bone('foot' + n, 'shin' + n, 0, -0.43, -0.15);
    }
    const PAINT = 0xffffff, ACC = 0xc89a24, JOINT = 0x232527, DARK = 0x16181a, STEEL = 0x9aa0a4;
    const RED = new THREE.Color(5.0, 0.35, 0.15), CYAN = new THREE.Color(0.4, 2.6, 3.0);

    // pelvis + hip actuators
    P('pelvis', G.rbox(0.46, 0.2, 0.32, 0.06), {}, JOINT);
    P('pelvis', G.rbox(0.36, 0.08, 0.34, 0.03), { y: 0.1 }, PAINT);
    for (const s of [-1, 1]) P('pelvis', G.cyl(0.085, 0.085, 0.1, 14), { x: 0.2 * s, y: -0.04, rz: Math.PI / 2 }, STEEL);
    // segmented lower torso
    for (let i = 0; i < 3; i++) P('spine', G.rbox(0.34 - i * 0.02, 0.085, 0.26, 0.035), { y: 0.02 + i * 0.1 }, i % 2 ? JOINT : PAINT);
    // chest armour + hazard accents + back pack
    P('chest', G.rbox(0.62, 0.44, 0.4, 0.09), { y: 0.14 }, PAINT);
    P('chest', G.rbox(0.5, 0.22, 0.06, 0.03), { y: 0.2, z: 0.2 }, PAINT);
    for (const s of [-1, 1]) P('chest', G.box(0.06, 0.32, 0.03), { x: 0.24 * s, y: 0.14, z: 0.205 }, ACC);
    P('chest', G.box(0.32, 0.05, 0.03), { y: -0.04, z: 0.205 }, ACC);
    for (let k = 0; k < 5; k++) P('chest', G.box(0.22, 0.015, 0.02), { y: 0.02 + k * 0.035, z: 0.215 }, DARK); // vent grille
    P('chest', G.rbox(0.42, 0.38, 0.18, 0.05), { y: 0.14, z: -0.27 }, JOINT); // power pack
    for (let k = 0; k < 3; k++) P('chest', G.box(0.06, 0.24, 0.02), { x: -0.12 + k * 0.12, y: 0.14, z: -0.365 }, CYAN, true);
    P('chest', G.cyl(0.05, 0.05, 0.18, 8), { y: 0.42, z: -0.22 }, STEEL); // exhaust stub
    // head: helmet, visor, eye, antenna
    P('head', G.rbox(0.27, 0.22, 0.27, 0.07), { y: 0.06 }, PAINT);
    P('head', G.rbox(0.24, 0.07, 0.04, 0.02), { y: 0.07, z: 0.125 }, DARK);
    P('head', G.box(0.17, 0.022, 0.02), { y: 0.07, z: 0.142 }, RED, true);
    P('head', G.box(0.2, 0.035, 0.24), { y: 0.185 }, ACC);
    P('head', G.cyl(0.008, 0.008, 0.26, 4), { x: -0.09, y: 0.3, z: -0.06 }, STEEL);
    P('head', G.sphere(0.018, 6, 4), { x: -0.09, y: 0.43, z: -0.06 }, RED, true);
    P('chest', G.cyl(0.07, 0.09, 0.12, 10), { y: 0.39 }, JOINT); // neck
    for (const [s, n] of [[1, 'L'], [-1, 'R']]) {
      // shoulder plate + joint
      P('upperArm' + n, G.rbox(0.22, 0.14, 0.26, 0.05), { y: 0.03, x: 0.03 * s }, PAINT);
      P('upperArm' + n, G.box(0.05, 0.03, 0.27), { y: 0.1, x: 0.06 * s }, ACC);
      P('upperArm' + n, G.sphere(0.08), {}, JOINT);
      P('upperArm' + n, G.limb(0.065, 0.055, 0.32, 10), { y: -0.02 }, STEEL);
      // upper-arm piston
      P('upperArm' + n, G.cyl(0.02, 0.02, 0.18, 6), { y: -0.12, z: 0.08 }, DARK);
      P('upperArm' + n, G.cyl(0.011, 0.011, 0.16, 6), { y: -0.26, z: 0.08 }, STEEL);
      P('foreArm' + n, G.cyl(0.07, 0.07, 0.12, 12), { rz: Math.PI / 2 }, JOINT);
      if (n === 'L') {
        P('foreArm' + n, G.rbox(0.13, 0.3, 0.13, 0.04), { y: -0.16 }, PAINT);
        P('hand' + n, G.rbox(0.1, 0.06, 0.08, 0.02), { y: -0.02 }, JOINT);
        for (let f = -1; f <= 1; f++) P('hand' + n, G.box(0.025, 0.11, 0.025), { x: f * 0.035, y: -0.09, z: 0.02, rx: 0.3 }, STEEL);
      } else {
        // weapon pod with twin barrels and an ammo drum
        P('foreArm' + n, G.rbox(0.16, 0.36, 0.17, 0.04), { y: -0.18 }, PAINT);
        P('foreArm' + n, G.box(0.17, 0.05, 0.03), { y: -0.1, z: 0.09 }, ACC);
        P('foreArm' + n, G.cyl(0.075, 0.075, 0.1, 12), { y: -0.2, x: -0.11, rz: Math.PI / 2 }, JOINT);
        for (const bx of [-0.035, 0.035]) P('hand' + n, G.cyl(0.022, 0.022, 0.34, 8), { x: bx, y: -0.14 }, DARK);
        P('hand' + n, G.rbox(0.13, 0.08, 0.1, 0.02), { y: 0.0 }, JOINT);
      }
      // legs
      P('thigh' + n, G.sphere(0.09), {}, JOINT);
      P('thigh' + n, G.rbox(0.17, 0.4, 0.2, 0.05), { y: -0.2, z: 0.06, rx: -0.3 }, PAINT);
      P('thigh' + n, G.box(0.04, 0.3, 0.03), { x: 0.06 * s, y: -0.2, z: 0.17, rx: -0.3 }, ACC);
      P('shin' + n, G.cyl(0.08, 0.08, 0.18, 12), { rz: Math.PI / 2 }, STEEL);
      P('shin' + n, G.rbox(0.13, 0.42, 0.13, 0.04), { y: -0.2, z: -0.07, rx: 0.33 }, JOINT);
      P('shin' + n, G.rbox(0.14, 0.2, 0.06, 0.03), { y: -0.14, z: 0.0, rx: 0.33 }, PAINT);
      // knee piston: housing on the thigh, rod on the shin
      P('thigh' + n, G.cyl(0.028, 0.028, 0.24, 6), { y: -0.2, z: -0.1, rx: 0.15 }, DARK);
      P('shin' + n, G.cyl(0.014, 0.014, 0.28, 6), { y: -0.12, z: -0.16, rx: 0.4 }, STEEL);
      P('foot' + n, G.cyl(0.055, 0.055, 0.14, 10), { rz: Math.PI / 2 }, JOINT);
      P('foot' + n, G.rbox(0.18, 0.06, 0.26, 0.02), { y: -0.09, z: 0.06 }, PAINT);
      for (const fx of [-0.05, 0.05]) P('foot' + n, G.box(0.05, 0.04, 0.12), { x: fx, y: -0.1, z: 0.22, rx: 0.15 }, JOINT);
      P('foot' + n, G.box(0.05, 0.04, 0.1), { y: -0.1, z: -0.1, rx: -0.25 }, JOINT);
    }

    const mat = BI.Models.characterMaterial(BI.Tex.get('robotPaint').map);
    mat.metalness = 0.55;
    mat.roughness = 0.6;
    const glow = new THREE.MeshBasicMaterial({ vertexColors: true });
    glow.skinning = true;
    const mesh = B.build(mat, glow);
    const root = new THREE.Group();
    root.add(mesh);
    // scanner dot (non-skinned child of the head bone)
    const dotMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(6, 0.6, 0.3) });
    const dot = new THREE.Mesh(new THREE.SphereGeometry(0.016, 8, 6), dotMat);
    dot.position.set(0, 0.07, 0.15);
    B.byName.head.add(dot);
    const muzzle = new THREE.Object3D();
    muzzle.position.set(0, -0.33, 0);
    B.byName.handR.add(muzzle);
    const rig = {
      kind: 'robot', root, mesh, bones: B.byName, muzzle, material: mat, glow, dot, dotMat,
      phase: 0, hit: 0, hitDirX: 0, death: -1, rest: {}, eyeColor: new THREE.Color(5, 2.4, 0.3),
    };
    for (const b of Object.values(B.byName)) rig.rest[b.name] = b.position.clone();
    rig.pose = (s, dt) => poseRobot(rig, s, dt);
    return rig;
  }

  function poseRobot(rig, s, dt) {
    const b = rig.bones;
    rig.phase += (s.dist || 0) / (s.speed > 3 ? 1.5 : 1.1) * Math.PI;
    const ph = rig.phase;
    const walk = U.clamp01(s.speed / 1.5), run = U.clamp01((s.speed - 2.3) / 2.2);
    const aim = s.aim || 0;
    rig.hit = Math.max(0, rig.hit - dt * 3.5);
    const amp = 0.32 * walk + 0.22 * run;
    for (const [n, off] of [['L', 0], ['R', Math.PI]]) {
      const sp = Math.sin(ph + off);
      b['thigh' + n].rotation.x = -sp * amp - 0.15;
      b['shin' + n].rotation.x = -Math.max(0, Math.sin(ph + off + 0.8)) * 0.45 * (walk + run) + 0.12;
      b['foot' + n].rotation.x = -(b['thigh' + n].rotation.x + b['shin' + n].rotation.x) * 0.7;
    }
    const stomp = Math.abs(Math.sin(ph)) * (0.03 * walk + 0.05 * run);
    b.pelvis.position.y = rig.rest.pelvis.y - 0.04 + stomp - 0.05 * run;
    b.pelvis.rotation.set(0, Math.sin(ph) * 0.06 * walk, Math.sin(ph) * 0.04 * walk);
    b.spine.rotation.set(0.06 * run + rig.hit * 0.35, -b.pelvis.rotation.y, rig.hit * rig.hitDirX * 0.3);
    b.chest.rotation.set(-aim * (s.aimPitch || 0) * 0.4, 0, 0);
    b.head.rotation.set(-aim * (s.aimPitch || 0) * 0.4, U.clamp(s.lookYaw || 0, -1.0, 1.0), 0);
    const swing = 0.25 * walk;
    b.upperArmL.rotation.set(Math.sin(ph + Math.PI) * swing - 0.15 * aim, 0, U.lerp(0.18, 0.35, aim));
    b.foreArmL.rotation.x = U.lerp(-0.3, -1.1, aim);
    b.upperArmR.rotation.set(U.lerp(Math.sin(ph) * swing - 0.2, -1.5 - (s.aimPitch || 0) * 0.6, aim), 0, U.lerp(-0.18, 0.08, aim));
    b.foreArmR.rotation.x = U.lerp(-0.4, -0.08, aim);
    // scanner dot sweeps across the visor
    rig.dot.position.x = Math.sin(BI.Engine.time * (aim > 0.5 ? 9 : 2.5)) * 0.07;
    rig.dotMat.color.copy(rig.eyeColor);

    if (rig.death >= 0) {
      rig.death += dt;
      const t1 = U.clamp01(rig.death / 0.4), t2 = U.smoothstep(0.3, 1.1, rig.death);
      b.thighL.rotation.x = -0.9 * t1; b.thighR.rotation.x = -0.7 * t1;
      b.shinL.rotation.x = -0.9 * t1; b.shinR.rotation.x = -0.8 * t1;
      b.pelvis.position.y = rig.rest.pelvis.y - 0.45 * t1 - 0.35 * t2;
      b.pelvis.rotation.x = 1.2 * t2;
      b.spine.rotation.x = 0.4 * t2;
      b.upperArmL.rotation.x = -0.5 * t2; b.upperArmR.rotation.x = -0.3 * t2;
      b.head.rotation.x = 0.5 * t2;
      rig.dotMat.color.setRGB(0.05, 0.01, 0.01);
    }
  }

  BI.Models = BI.Models || {};
  BI.Models.buildRobot = buildRobot;
})();
