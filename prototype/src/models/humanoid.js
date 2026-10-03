/* =============================================================================
 * models/humanoid.js — skinned humanoids (Security Patrol, Hunter, Commander)
 * -----------------------------------------------------------------------------
 * SkinBuilder: build a bone hierarchy, then attach primitives (boxes,
 * cylinders, spheres — r128 has no CapsuleGeometry) to bones in bind pose.
 * Every primitive is rigidly bound to one bone (weight 1) and coloured with
 * vertex colours, and everything is merged into ONE THREE.SkinnedMesh — a
 * whole character is a single draw call, and animation is just bone rotation.
 *
 * Procedural animation (pose()): gait phase drives hip/knee/ankle swing and
 * arm counter-swing, blended idle -> walk -> run by speed; an aim layer
 * raises the weapon bone toward the target pitch; plus crouch, head look,
 * hit-react impulse and a death collapse (knees buckle, then the body falls).
 * Proportions: ~1.79 m tall, model faces +Z.
 * Role: Graphics (characters) / Agent Controller (animation hooks).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  // ---------------------------------------------------------------------------
  // SkinBuilder (shared with models/robot.js)
  // ---------------------------------------------------------------------------
  class SkinBuilder {
    constructor() {
      this.bones = [];
      this.byName = {};
      this.parts = [];
    }
    bone(name, parentName, x, y, z) {
      const b = new THREE.Bone();
      b.name = name;
      b.position.set(x, y, z);
      if (parentName) this.byName[parentName].add(b);
      this.bones.push(b);
      this.byName[name] = b;
      return b;
    }
    // geometry in the bone's local space; t = {x,y,z,rx,ry,rz,sx,sy,sz,s}
    part(boneName, geometry, t, color, emissive) {
      this.parts.push({ bone: boneName, geometry, t: t || {}, color: new THREE.Color(color === undefined ? 0xffffff : color), emissive: !!emissive });
    }
    build(material, emissiveMaterial) {
      const root = this.bones[0];
      root.updateMatrixWorld(true);
      const body = [], glow = [];
      for (const p of this.parts) {
        const b = this.byName[p.bone];
        const bi = this.bones.indexOf(b);
        let g = p.geometry.clone();
        if (g.index) g = g.toNonIndexed();
        for (const k of Object.keys(g.attributes)) if (k !== 'position' && k !== 'normal' && k !== 'uv') g.deleteAttribute(k);
        if (!g.attributes.uv) g.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(g.attributes.position.count * 2), 2));
        g.applyMatrix4(BI.Geo.matrixFrom(p.t));
        g.applyMatrix4(b.matrixWorld);
        const n = g.attributes.position.count;
        const col = new Float32Array(n * 3), si = new Float32Array(n * 4), sw = new Float32Array(n * 4);
        for (let i = 0; i < n; i++) {
          col[i * 3] = p.color.r; col[i * 3 + 1] = p.color.g; col[i * 3 + 2] = p.color.b;
          si[i * 4] = bi;
          sw[i * 4] = 1;
        }
        g.setAttribute('color', new THREE.BufferAttribute(col, 3));
        g.setAttribute('skinIndex', new THREE.BufferAttribute(si, 4));
        g.setAttribute('skinWeight', new THREE.BufferAttribute(sw, 4));
        (p.emissive ? glow : body).push(g);
      }
      const merged = mergeNonIndexed(body.concat(glow));
      let mat = material;
      if (glow.length && emissiveMaterial) {
        let cBody = 0;
        for (const g of body) cBody += g.attributes.position.count;
        merged.addGroup(0, cBody, 0);
        merged.addGroup(cBody, merged.attributes.position.count - cBody, 1);
        mat = [material, emissiveMaterial];
      }
      const mesh = new THREE.SkinnedMesh(merged, mat);
      mesh.add(root);
      mesh.bind(new THREE.Skeleton(this.bones));
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      return mesh;
    }
  }

  function mergeNonIndexed(list) {
    let n = 0;
    for (const g of list) n += g.attributes.position.count;
    const out = new THREE.BufferGeometry();
    for (const [k, size] of [['position', 3], ['normal', 3], ['uv', 2], ['color', 3], ['skinIndex', 4], ['skinWeight', 4]]) {
      const arr = new Float32Array(n * size);
      let o = 0;
      for (const g of list) {
        arr.set(g.attributes[k].array, o);
        o += g.attributes[k].array.length;
      }
      out.setAttribute(k, new THREE.BufferAttribute(arr, size));
    }
    out.computeBoundingSphere();
    out.boundingSphere.radius = Math.max(out.boundingSphere.radius, 1.2);
    return out;
  }

  // Character material: vertex colours * detail map, skinning on (r128 flag).
  function characterMaterial(map) {
    const m = BI.Mat.standard({ map: map || BI.Tex.get('fabric').map, vertexColors: true, roughness: 0.85, metalness: 0.05 });
    m.skinning = true;
    return m;
  }

  // Primitive helpers (all centred, extend along -Y when a length is given)
  const G = {
    box: (w, h, d) => new THREE.BoxGeometry(w, h, d),
    rbox: (w, h, d, r) => BI.Geo.roundedBox(w, h, d, r || Math.min(w, h, d) * 0.25, 1),
    limb: (r0, r1, len, seg) => {
      const g = new THREE.CylinderGeometry(r0, r1, len, seg || 8, 1);
      g.translate(0, -len / 2, 0);
      return g;
    },
    sphere: (r, w, h) => new THREE.SphereGeometry(r, w || 10, h || 8),
    cyl: (r0, r1, h, seg) => new THREE.CylinderGeometry(r0, r1, h, seg || 8),
  };

  // ---------------------------------------------------------------------------
  // Humanoid skeleton (shared by all three human agents)
  // ---------------------------------------------------------------------------
  function skeleton(B) {
    B.bone('hips', null, 0, 0.98, 0);
    B.bone('spine', 'hips', 0, 0.1, 0);
    B.bone('chest', 'spine', 0, 0.2, 0);
    B.bone('neck', 'chest', 0, 0.27, 0);
    B.bone('head', 'neck', 0, 0.1, 0);
    for (const [s, n] of [[1, 'L'], [-1, 'R']]) {
      B.bone('upperArm' + n, 'chest', 0.2 * s, 0.2, 0);
      B.bone('foreArm' + n, 'upperArm' + n, 0, -0.29, 0);
      B.bone('hand' + n, 'foreArm' + n, 0, -0.26, 0);
      B.bone('thigh' + n, 'hips', 0.1 * s, -0.06, 0);
      B.bone('shin' + n, 'thigh' + n, 0, -0.44, 0);
      B.bone('foot' + n, 'shin' + n, 0, -0.42, 0);
    }
    B.bone('gun', 'chest', -0.04, 0.12, 0.2);
  }

  const C = {
    skin: 0xb08368, skinDark: 0x6e5040, navy: 0x2b3546, black: 0x1a1b1d, vest: 0x2a2d28, olive: 0x4a5136,
    helmet: 0x262b31, visor: 0x0b0d10, boot: 0x141210, glove: 0x1c1d1e, white: 0xe9ebe7, trouser: 0x24272c,
    gun: 0x2a2c2f, gunDark: 0x161718, hair: 0x2a2018, red: 0x7a1d1d, camo1: 0x6a6a52, camo2: 0x4c5536,
    leaf1: 0x55643a, leaf2: 0x6b6644, leaf3: 0x3b4a2a,
  };

  function body(B, o) {
    // o: { torso, sleeve, leg, glove, boot, skin, coat }
    const P = (bone, g, t, c) => B.part(bone, g, t, c);
    P('hips', G.rbox(0.34, 0.2, 0.22, 0.06), { y: 0.0 }, o.leg);
    P('spine', G.rbox(0.31, 0.24, 0.2, 0.07), { y: 0.1 }, o.torso);
    P('chest', G.rbox(0.38, 0.32, 0.24, 0.08), { y: 0.12 }, o.torso);
    P('chest', G.sphere(0.075), { x: 0.19, y: 0.2 }, o.torso);
    P('chest', G.sphere(0.075), { x: -0.19, y: 0.2 }, o.torso);
    P('neck', G.cyl(0.05, 0.055, 0.12), { y: 0.04 }, o.skin);
    P('head', G.sphere(0.105, 14, 12), { y: 0.07, z: 0.005, sx: 0.92, sy: 1.12, sz: 1.0 }, o.skin);
    P('head', G.rbox(0.13, 0.07, 0.12, 0.03), { y: 0.0, z: 0.03 }, o.skin); // jaw
    P('head', G.box(0.03, 0.04, 0.04), { y: 0.06, z: 0.11 }, o.skin); // nose
    for (const [s, n] of [[1, 'L'], [-1, 'R']]) {
      P('head', G.box(0.02, 0.045, 0.03), { x: 0.1 * s, y: 0.06 }, o.skin);
      P('upperArm' + n, G.limb(0.058, 0.048, 0.3), {}, o.sleeve);
      P('foreArm' + n, G.sphere(0.05), {}, o.sleeve);
      P('foreArm' + n, G.limb(0.048, 0.036, 0.26), {}, o.sleeve);
      P('hand' + n, G.rbox(0.075, 0.1, 0.04, 0.015), { y: -0.05 }, o.glove);
      P('hand' + n, G.box(0.03, 0.06, 0.03), { x: 0.035 * s, y: -0.03, z: 0.02, rz: 0.4 * s }, o.glove);
      P('thigh' + n, G.limb(0.088, 0.066, 0.45, 9), {}, o.leg);
      P('shin' + n, G.sphere(0.065), {}, o.leg);
      P('shin' + n, G.limb(0.064, 0.048, 0.42, 9), {}, o.leg);
      P('foot' + n, G.rbox(0.11, 0.09, 0.27, 0.03), { y: -0.03, z: 0.05 }, o.boot);
      P('shin' + n, G.cyl(0.058, 0.062, 0.14), { y: -0.36 }, o.boot); // boot shaft
    }
  }

  function rifle(B, c1, c2, opts) {
    const P = (g, t, c) => B.part('gun', g, t, c);
    P(G.box(0.06, 0.09, 0.42), { z: 0.12 }, c1); // receiver
    P(G.cyl(0.016, 0.016, 0.4, 8), { z: 0.52, rx: Math.PI / 2 }, c2); // barrel
    P(G.box(0.05, 0.05, 0.22), { z: 0.38, y: -0.01 }, c1); // handguard
    P(G.box(0.045, 0.16, 0.07), { z: 0.08, y: -0.11, rx: 0.25 }, c2); // magazine
    P(G.box(0.045, 0.1, 0.05), { z: -0.04, y: -0.08, rx: -0.3 }, c2); // grip
    P(G.box(0.05, 0.08, 0.22), { z: -0.2, y: -0.02 }, c1); // stock
    if (opts && opts.scope) P(G.cyl(0.024, 0.024, 0.2, 10), { z: 0.15, y: 0.075, rx: Math.PI / 2 }, c2);
    else P(G.box(0.02, 0.03, 0.05), { z: 0.18, y: 0.06 }, c2);
  }

  function pistol(B, c) {
    B.part('gun', G.box(0.035, 0.05, 0.17), { z: 0.12, y: 0.0 }, c);
    B.part('gun', G.box(0.032, 0.09, 0.045), { z: 0.07, y: -0.06, rx: -0.2 }, c);
  }

  // ---------------------------------------------------------------------------
  function buildHumanoid(kind) {
    const B = new SkinBuilder();
    skeleton(B);
    const P = (bone, g, t, c) => B.part(bone, g, t, c);
    let map = null;
    if (kind === 'patrol') {
      body(B, { torso: C.navy, sleeve: C.navy, leg: C.navy, glove: C.glove, boot: C.boot, skin: C.skin });
      // tactical vest + pouches + belt
      P('chest', G.rbox(0.41, 0.3, 0.27, 0.05), { y: 0.1 }, C.vest);
      for (let i = -1; i <= 1; i++) P('chest', G.rbox(0.09, 0.11, 0.06, 0.02), { x: i * 0.11, y: 0.0, z: 0.15 }, C.black);
      P('chest', G.box(0.06, 0.1, 0.04), { x: 0.16, y: 0.26, z: 0.08 }, C.black); // radio
      P('hips', G.cyl(0.18, 0.18, 0.06, 12), { y: 0.06 }, C.black);
      // helmet with brim + dark visor
      P('head', G.sphere(0.128, 14, 8), { y: 0.1, sy: 0.9 }, C.helmet);
      P('head', G.cyl(0.135, 0.14, 0.03, 14), { y: 0.06 }, C.helmet);
      P('head', G.rbox(0.19, 0.06, 0.06, 0.02), { y: 0.07, z: 0.1 }, C.visor);
      for (const s of [-1, 1]) P('shin' + (s > 0 ? 'L' : 'R'), G.rbox(0.1, 0.1, 0.05, 0.02), { y: -0.02, z: 0.06 }, C.black); // knee pads
      rifle(B, C.gun, C.gunDark);
    } else if (kind === 'hunter') {
      map = BI.Tex.get('camo').map;
      body(B, { torso: 0xffffff, sleeve: 0xffffff, leg: 0xffffff, glove: C.camo2, boot: 0x2a2620, skin: C.skinDark });
      // hood + face wrap
      P('head', G.sphere(0.13, 12, 10), { y: 0.08, z: -0.015, sx: 1.0, sy: 1.05 }, 0xd9d9c8);
      P('head', G.rbox(0.15, 0.08, 0.12, 0.03), { y: 0.0, z: 0.04 }, C.camo2);
      // ghillie strips hanging from head, shoulders and back
      const r = U.rng(kind.length * 31);
      const strips = [['head', 0.08, 0.2], ['chest', 0.22, 0.3], ['spine', 0.12, 0.15], ['upperArmL', 0.0, 0.1], ['upperArmR', 0.0, 0.1], ['hips', 0.0, 0.05]];
      for (const [bone, y0, y1] of strips) {
        for (let k = 0; k < 9; k++) {
          const a = r() * Math.PI * 2;
          const rad = bone === 'head' ? 0.12 : bone.indexOf('Arm') > 0 ? 0.07 : 0.2;
          const len = 0.12 + r() * 0.2;
          const g = G.box(0.05 + r() * 0.04, len, 0.008);
          g.translate(0, -len / 2, 0);
          P(bone, g, { x: Math.cos(a) * rad, y: y0 + r() * (y1 - y0), z: Math.sin(a) * rad * 0.8, ry: -a + Math.PI / 2, rx: (r() - 0.5) * 0.6, rz: (r() - 0.5) * 0.4 }, [C.leaf1, C.leaf2, C.leaf3][k % 3]);
        }
      }
      rifle(B, C.camo2, C.gunDark, { scope: true });
    } else {
      // commander: lab coat, trousers, headset, glasses, sidearm
      body(B, { torso: C.white, sleeve: C.white, leg: C.trouser, glove: C.skin, boot: C.black, skin: C.skin });
      P('chest', G.box(0.1, 0.22, 0.02), { y: 0.14, z: 0.125 }, 0x3c4b5c); // shirt
      P('chest', G.box(0.035, 0.18, 0.01), { y: 0.12, z: 0.137 }, C.red); // tie
      P('chest', G.box(0.06, 0.07, 0.01), { x: 0.11, y: 0.2, z: 0.125 }, 0x1d2633); // badge
      // coat tails (front panels + back) bound to the hips
      const tail = new THREE.CylinderGeometry(0.2, 0.25, 0.5, 14, 1, true, Math.PI * 0.12, Math.PI * 1.76);
      tail.translate(0, -0.25, 0);
      P('hips', tail, { y: 0.06 }, C.white);
      // hair, glasses, headset
      P('head', G.sphere(0.11, 12, 8), { y: 0.11, z: -0.01, sy: 0.75 }, C.hair);
      P('head', G.box(0.13, 0.025, 0.01), { y: 0.075, z: 0.105 }, 0x111111);
      P('head', new THREE.TorusGeometry(0.115, 0.012, 6, 16, Math.PI), { y: 0.08 }, C.black);
      for (const s of [-1, 1]) P('head', G.cyl(0.035, 0.035, 0.03, 10), { x: 0.112 * s, y: 0.06, rz: Math.PI / 2 }, C.black);
      P('head', G.cyl(0.005, 0.005, 0.12, 4), { x: 0.09, y: 0.0, z: 0.07, rx: Math.PI / 2, ry: -0.6 }, C.black);
      pistol(B, C.black);
    }
    const mat = characterMaterial(map);
    const mesh = B.build(mat);
    const root = new THREE.Group();
    root.add(mesh);
    const muzzle = new THREE.Object3D();
    muzzle.position.set(0, 0.0, kind === 'commander' ? 0.22 : 0.74);
    B.byName.gun.add(muzzle);
    const rig = {
      kind, root, mesh, bones: B.byName, muzzle, material: mat,
      phase: 0, hit: 0, hitDirX: 0, death: -1,
      rest: {},
    };
    for (const b of B.bones) rig.rest[b.name] = b.position.clone();
    rig.pose = (s, dt) => poseHumanoid(rig, s, dt);
    return rig;
  }

  // ---------------------------------------------------------------------------
  // Procedural animation
  // s: { speed, runSpeed, aim (0..1), aimPitch, crouch (0..1), lookYaw, dist, sneak }
  // ---------------------------------------------------------------------------
  function poseHumanoid(rig, s, dt) {
    const b = rig.bones;
    rig.phase += (s.dist || 0) / (s.speed > 3 ? 1.35 : 0.95) * Math.PI;
    const ph = rig.phase;
    const walk = U.clamp01(s.speed / 1.6);
    const run = U.clamp01((s.speed - 2.2) / 2.5);
    const crouch = s.crouch || 0;
    const aim = s.aim || 0;
    rig.hit = Math.max(0, rig.hit - dt * 4);

    const legAmp = 0.42 * walk + 0.38 * run;
    const kneeAmp = 0.55 * walk + 0.75 * run;
    const sL = Math.sin(ph), sR = Math.sin(ph + Math.PI);
    const thighBase = -0.55 * crouch;
    const kneeBase = 1.0 * crouch;
    b.thighL.rotation.set(thighBase - sL * legAmp, 0, 0.02);
    b.thighR.rotation.set(thighBase - sR * legAmp, 0, -0.02);
    b.shinL.rotation.x = kneeBase + Math.max(0, Math.sin(ph + 0.9)) * kneeAmp + 0.05;
    b.shinR.rotation.x = kneeBase + Math.max(0, Math.sin(ph + Math.PI + 0.9)) * kneeAmp + 0.05;
    b.footL.rotation.x = -(b.thighL.rotation.x + b.shinL.rotation.x) * 0.6;
    b.footR.rotation.x = -(b.thighR.rotation.x + b.shinR.rotation.x) * 0.6;

    const bob = Math.abs(Math.sin(ph)) * (0.025 * walk + 0.05 * run);
    b.hips.position.y = rig.rest.hips.y - 0.28 * crouch + bob - 0.02 * walk;
    b.hips.rotation.set(0.0, Math.sin(ph) * 0.08 * walk, Math.sin(ph) * 0.03 * walk);
    const lean = 0.12 * run + 0.18 * crouch + (s.sneak ? 0.2 : 0);
    b.spine.rotation.set(lean + rig.hit * 0.45, -b.hips.rotation.y * 0.8, rig.hit * rig.hitDirX * 0.3);
    b.chest.rotation.set(-aim * U.clamp(s.aimPitch || 0, -0.6, 0.6) * 0.5, 0, 0);
    b.neck.rotation.set(-lean * 0.5 - aim * (s.aimPitch || 0) * 0.3, U.clamp(s.lookYaw || 0, -0.9, 0.9), 0);

    // arms: counter swing (relaxed) blended with the aiming pose
    const swing = 0.35 * walk + 0.4 * run;
    const relaxL = { x: sR * swing - 0.1, z: 0.12 }, relaxR = { x: sL * swing * 0.4 - 0.45, z: -0.12 };
    const aimL = { x: -1.25, z: -0.55 }, aimR = { x: -1.05, z: 0.35 };
    if (rig.kind === 'commander') { aimL.x = -0.2; aimL.z = 0.1; aimR.x = -1.45; aimR.z = 0.15; }
    b.upperArmL.rotation.set(U.lerp(relaxL.x, aimL.x, aim), 0, U.lerp(relaxL.z, aimL.z, aim));
    b.upperArmR.rotation.set(U.lerp(relaxR.x, aimR.x, aim), 0, U.lerp(relaxR.z, aimR.z, aim));
    b.foreArmL.rotation.set(U.lerp(-0.25 - run * 0.9, -0.55, aim), 0, 0);
    b.foreArmR.rotation.set(U.lerp(-0.7 - run * 0.6, -0.75, aim), 0, 0);
    // weapon bone: low ready -> shouldered toward the target
    const gp = rig.rest.gun;
    if (rig.kind === 'commander') {
      b.gun.position.set(U.lerp(gp.x - 0.18, -0.1, aim), U.lerp(gp.y - 0.45, 0.2, aim), U.lerp(gp.z - 0.05, 0.45, aim));
    } else {
      b.gun.position.set(gp.x, U.lerp(gp.y - 0.12, gp.y + 0.12, aim), U.lerp(gp.z - 0.02, gp.z + 0.05, aim));
    }
    b.gun.rotation.set(U.lerp(0.65, -(s.aimPitch || 0) * 0.5, aim), U.lerp(0.35, 0, aim), 0);

    // death collapse (overrides): knees buckle, then fall backwards
    if (rig.death >= 0) {
      rig.death += dt;
      const t1 = U.clamp01(rig.death / 0.35), t2 = U.smoothstep(0.25, 1.0, rig.death);
      b.thighL.rotation.x = -1.2 * t1; b.thighR.rotation.x = -1.0 * t1;
      b.shinL.rotation.x = 1.9 * t1; b.shinR.rotation.x = 1.7 * t1;
      b.hips.position.y = rig.rest.hips.y - 0.5 * t1 - 0.32 * t2;
      b.hips.rotation.x = -1.35 * t2;
      b.spine.rotation.x = -0.2 * t2;
      b.upperArmL.rotation.set(-0.6 * t1 + 1.9 * t2, 0, 0.6 * t2);
      b.upperArmR.rotation.set(-0.4 * t1 + 1.7 * t2, 0, -0.7 * t2);
      b.foreArmL.rotation.x = -0.3;
      b.foreArmR.rotation.x = -0.4;
      b.neck.rotation.x = -0.4 * t2;
      b.gun.rotation.x = 1.4 * t2;
    }
  }

  BI.Models = BI.Models || {};
  BI.Models.SkinBuilder = SkinBuilder;
  BI.Models.characterMaterial = characterMaterial;
  BI.Models.prims = G;
  BI.Models.buildHumanoid = buildHumanoid;
})();
