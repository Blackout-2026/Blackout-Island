/* =============================================================================
 * core/geometry.js — geometry helpers and the static batcher
 * -----------------------------------------------------------------------------
 * StaticBatcher collects many small static meshes (walls, pipes, props) and
 * merges them into ONE mesh per material, which keeps draw calls low enough
 * for 60 fps. Each room/area gets its own batcher so frustum culling still
 * works per area.
 *
 * `worldUV` re-projects UVs from world position (box mapping by dominant
 * normal axis) so a concrete wall of any size has the same texel density.
 * Role: Core Developer / World Builder.
 * ========================================================================== */
(function () {
  'use strict';

  const _m = new THREE.Matrix4();
  const _q = new THREE.Quaternion();
  const _e = new THREE.Euler();
  const _s = new THREE.Vector3();
  const _p = new THREE.Vector3();
  const _n3 = new THREE.Matrix3();

  function toIndexed(g) {
    if (g.index) return g;
    const n = g.attributes.position.count;
    const idx = new Array(n);
    for (let i = 0; i < n; i++) idx[i] = i;
    g.setIndex(idx);
    return g;
  }

  // Ensure position / normal / uv exist so geometries can be merged.
  function normalize(g) {
    if (!g.attributes.normal) g.computeVertexNormals();
    if (!g.attributes.uv) {
      g.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(g.attributes.position.count * 2), 2));
    }
    for (const k of Object.keys(g.attributes)) {
      if (k !== 'position' && k !== 'normal' && k !== 'uv' && k !== 'color') g.deleteAttribute(k);
    }
    return toIndexed(g);
  }

  function applyWorldUV(g, scale, offset) {
    const pos = g.attributes.position, nor = g.attributes.normal, uv = g.attributes.uv;
    const ox = offset ? offset[0] : 0, oy = offset ? offset[1] : 0;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
      const nx = Math.abs(nor.getX(i)), ny = Math.abs(nor.getY(i)), nz = Math.abs(nor.getZ(i));
      let u, v;
      if (ny >= nx && ny >= nz) { u = x; v = z; }
      else if (nx >= nz) { u = z; v = y; }
      else { u = x; v = y; }
      uv.setXY(i, u * scale + ox, v * scale + oy);
    }
    uv.needsUpdate = true;
  }

  // Merge indexed geometries that share attribute layout.
  function merge(list) {
    let vCount = 0, iCount = 0, hasColor = false;
    for (const g of list) {
      vCount += g.attributes.position.count;
      iCount += g.index.count;
      if (g.attributes.color) hasColor = true;
    }
    const pos = new Float32Array(vCount * 3), nor = new Float32Array(vCount * 3), uv = new Float32Array(vCount * 2);
    const col = hasColor ? new Float32Array(vCount * 3) : null;
    const idx = vCount > 65535 ? new Uint32Array(iCount) : new Uint16Array(iCount);
    let vo = 0, io = 0;
    for (const g of list) {
      const n = g.attributes.position.count;
      pos.set(g.attributes.position.array, vo * 3);
      nor.set(g.attributes.normal.array, vo * 3);
      uv.set(g.attributes.uv.array, vo * 2);
      if (col) {
        if (g.attributes.color) col.set(g.attributes.color.array, vo * 3);
        else col.fill(1, vo * 3, (vo + n) * 3);
      }
      const gi = g.index.array;
      for (let i = 0; i < gi.length; i++) idx[io + i] = gi[i] + vo;
      vo += n;
      io += gi.length;
    }
    const out = new THREE.BufferGeometry();
    out.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    out.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
    out.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
    if (col) out.setAttribute('color', new THREE.BufferAttribute(col, 3));
    out.setIndex(new THREE.BufferAttribute(idx, 1));
    out.computeBoundingSphere();
    out.computeBoundingBox();
    return out;
  }

  // Build a matrix from {x,y,z, rx,ry,rz, sx,sy,sz} or arrays.
  function matrixFrom(t) {
    if (t && t.isMatrix4) return t;
    t = t || {};
    _p.set(t.x || 0, t.y || 0, t.z || 0);
    _e.set(t.rx || 0, t.ry || 0, t.rz || 0, t.order || 'XYZ');
    _q.setFromEuler(_e);
    const s = t.s;
    _s.set(t.sx || s || 1, t.sy || s || 1, t.sz || s || 1);
    return _m.compose(_p, _q, _s);
  }

  class StaticBatcher {
    constructor(name) {
      this.name = name;
      this.groups = new Map(); // key -> {material, geoms:[], cast, receive}
    }

    // geometry is cloned and baked with the transform.
    add(geometry, material, transform, opts) {
      opts = opts || {};
      const g = normalize(geometry.clone());
      const m = matrixFrom(transform);
      g.applyMatrix4(m);
      if (opts.worldUV) applyWorldUV(g, opts.worldUV, opts.uvOffset);
      if (opts.color) {
        const c = new Float32Array(g.attributes.position.count * 3);
        for (let i = 0; i < c.length; i += 3) { c[i] = opts.color.r; c[i + 1] = opts.color.g; c[i + 2] = opts.color.b; }
        g.setAttribute('color', new THREE.BufferAttribute(c, 3));
      }
      const cast = opts.castShadow !== false, receive = opts.receiveShadow !== false;
      const key = material.uuid + (cast ? 'c' : '') + (receive ? 'r' : '');
      if (!this.groups.has(key)) this.groups.set(key, { material, geoms: [], cast, receive });
      this.groups.get(key).geoms.push(g);
      return g;
    }

    // Convenience: axis-aligned box from min/max corners.
    box(minX, minY, minZ, maxX, maxY, maxZ, material, opts) {
      const w = maxX - minX, h = maxY - minY, d = maxZ - minZ;
      if (w <= 0 || h <= 0 || d <= 0) return;
      this.add(BI.Geo.unitBox, material, { x: (minX + maxX) / 2, y: (minY + maxY) / 2, z: (minZ + maxZ) / 2, sx: w, sy: h, sz: d }, opts);
    }

    build(parent) {
      const meshes = [];
      for (const grp of this.groups.values()) {
        // Split huge batches so 16-bit indices stay valid on old GPUs too.
        const chunks = [];
        let cur = [], count = 0;
        for (const g of grp.geoms) {
          const n = g.attributes.position.count;
          if (count + n > 60000 && cur.length) { chunks.push(cur); cur = []; count = 0; }
          cur.push(g);
          count += n;
        }
        if (cur.length) chunks.push(cur);
        for (const ch of chunks) {
          const mesh = new THREE.Mesh(merge(ch), grp.material);
          mesh.castShadow = grp.cast;
          mesh.receiveShadow = grp.receive;
          mesh.matrixAutoUpdate = false;
          mesh.name = this.name;
          mesh.updateMatrix();
          parent.add(mesh);
          meshes.push(mesh);
        }
      }
      this.groups.clear();
      return meshes;
    }
  }

  BI.Geo = {
    StaticBatcher,
    merge: (list) => merge(list.map((g) => normalize(g.clone()))),
    applyWorldUV,
    matrixFrom: (t) => matrixFrom(t).clone(),
    unitBox: new THREE.BoxGeometry(1, 1, 1),
    // Rounded/chamfered box via extruded rounded rectangle (used by hero models).
    roundedBox(w, h, d, r, seg) {
      r = Math.min(r, w / 2 - 0.001, h / 2 - 0.001);
      const shape = new THREE.Shape();
      const x = -w / 2, y = -h / 2;
      shape.moveTo(x + r, y);
      shape.lineTo(x + w - r, y);
      shape.quadraticCurveTo(x + w, y, x + w, y + r);
      shape.lineTo(x + w, y + h - r);
      shape.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
      shape.lineTo(x + r, y + h);
      shape.quadraticCurveTo(x, y + h, x, y + h - r);
      shape.lineTo(x, y + r);
      shape.quadraticCurveTo(x, y, x + r, y);
      const bev = Math.min(r * 0.6, d * 0.2);
      const g = new THREE.ExtrudeGeometry(shape, {
        depth: Math.max(0.001, d - bev * 2), bevelEnabled: true, bevelThickness: bev, bevelSize: bev * 0.8,
        bevelSegments: seg || 2, curveSegments: seg || 3,
      });
      g.translate(0, 0, -(d - bev * 2) / 2);
      // Simple planar UVs scaled to size
      const pos = g.attributes.position, uv = g.attributes.uv;
      for (let i = 0; i < pos.count; i++) uv.setXY(i, (pos.getX(i) + w / 2) / Math.max(w, d) + pos.getZ(i) * 0.5, (pos.getY(i) + h / 2) / h);
      return g;
    },
    // Tube along a list of points (cables / pipes)
    tube(points, radius, segs, radial) {
      const curve = new THREE.CatmullRomCurve3(points.map((p) => (p.isVector3 ? p : new THREE.Vector3(p[0], p[1], p[2]))));
      return new THREE.TubeGeometry(curve, segs || 24, radius, radial || 6, false);
    },
  };
})();
