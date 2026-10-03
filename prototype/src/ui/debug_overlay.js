/* =============================================================================
 * ui/debug_overlay.js — F1 (or `) AI / performance debug overlay
 * -----------------------------------------------------------------------------
 * In the world: each agent's vision cone on the floor (colour = state: green
 * calm, amber suspicious/searching, red combat), its current A* path, a
 * label with name + architecture + state, and expanding rings for every
 * noise event (radius = propagation radius).
 * Panel: FPS / frame time / draw calls / triangles, the shared blackboard,
 * the Commander's goal + plan + plan log, Tactical Guard utility scores with
 * bars and their input factors, each agent's state / BT branch, event log.
 * Role: Agent Controller (debug) / Core Developer.
 * ========================================================================== */
(function () {
  'use strict';

  const COL = { calm: 0x40ff80, sus: 0xffb030, combat: 0xff3020, dead: 0x555555 };
  function stateColor(a) {
    if (!a.alive) return COL.dead;
    if (a.inCombat() || /Attack|Pursue|Engage|Converge/.test(a.state)) return COL.combat;
    if (/Investigate|Search|Gunfire|Footsteps|Reposition|Cover|HuntLastSeen|Alert|Order/.test(a.state)) return COL.sus;
    return COL.calm;
  }

  const Debug = {
    on: false,
    built: false,

    init() {
      const el = document.createElement('div');
      el.id = 'debug';
      el.innerHTML = '<div class="fps"></div><div class="panel"></div><div class="labels"></div><div class="dbg-hint">DEBUG OVERLAY — F1 / ` TO CLOSE</div>';
      document.getElementById('ui').appendChild(el);
      this.el = el;
      this.fpsEl = el.querySelector('.fps');
      this.panel = el.querySelector('.panel');
      this.labels = el.querySelector('.labels');
      BI.Engine.addAlwaysUpdate((dt) => {
        if (BI.Input.pressedRaw('F1') || BI.Input.pressedRaw('Backquote')) this.toggle();
        if (this.on) this.update(dt);
      }, 97);
    },

    toggle(v) {
      this.on = v === undefined ? !this.on : v;
      this.el.classList.toggle('on', this.on);
      if (this.on && !this.built) this.build();
      if (this.group) this.group.visible = this.on;
    },

    build() {
      this.built = true;
      const scene = BI.Engine.scene;
      const g = new THREE.Group();
      g.name = 'debug';
      scene.add(g);
      this.group = g;
      this.cones = [];
      this.paths = [];
      this.tags = [];
      for (const a of BI.Agents.list) {
        const fov = a.perception.fov;
        const geo = new THREE.CircleGeometry(1, 24, -fov / 2, fov);
        geo.rotateX(-Math.PI / 2);
        // CircleGeometry starts at +x; rotate so the fan is centred on +z
        geo.rotateY(-Math.PI / 2);
        const m = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ color: COL.calm, transparent: true, opacity: 0.07, depthWrite: false, side: THREE.DoubleSide, fog: false }));
        m.renderOrder = 8;
        g.add(m);
        this.cones.push(m);
        const lg = new THREE.BufferGeometry();
        lg.setAttribute('position', new THREE.BufferAttribute(new Float32Array(64 * 3), 3));
        const line = new THREE.Line(lg, new THREE.LineBasicMaterial({ color: 0x47d1d6, fog: false }));
        line.frustumCulled = false;
        g.add(line);
        this.paths.push(line);
        const tag = document.createElement('div');
        tag.className = 'label';
        this.labels.appendChild(tag);
        this.tags.push(tag);
      }
      this.rings = [];
      for (let i = 0; i < 20; i++) {
        const r = new THREE.Mesh(new THREE.RingGeometry(0.96, 1, 48).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0xffd040, transparent: true, opacity: 0.6, depthWrite: false, side: THREE.DoubleSide, fog: false }));
        r.visible = false;
        r.renderOrder = 9;
        g.add(r);
        this.rings.push(r);
      }
    },

    update() {
      const E = BI.Engine, st = E.stats();
      this.fpsEl.textContent = st.fps.toFixed(0) + ' FPS  ' + st.frameMs.toFixed(1) + ' ms  |  ' + st.calls + ' draws  ' + (st.triangles / 1000).toFixed(0) + 'k tris  ' + BI.Engine.settings.name.toUpperCase();
      const cam = E.camera;
      const w = window.innerWidth, h = window.innerHeight;
      const v = new THREE.Vector3();
      BI.Agents.list.forEach((a, i) => {
        const cone = this.cones[i];
        const col = stateColor(a);
        cone.material.color.setHex(col);
        cone.visible = a.alive;
        cone.position.set(a.pos.x, a.pos.y + 0.06, a.pos.z);
        cone.rotation.y = a.headYaw();
        const r = a.perception.range * a.perception.rangeMul();
        cone.scale.set(r, 1, r);
        // path
        const line = this.paths[i];
        const path = a.controller.path;
        const arr = line.geometry.attributes.position.array;
        let n = 0;
        if (path && a.alive && a.intent.move) {
          arr[0] = a.pos.x; arr[1] = a.pos.y + 0.15; arr[2] = a.pos.z;
          n = 1;
          for (let k = a.controller.pathIdx; k < path.length && n < 64; k++, n++) {
            arr[n * 3] = path[k].x; arr[n * 3 + 1] = path[k].y + 0.15; arr[n * 3 + 2] = path[k].z;
          }
        }
        line.geometry.setDrawRange(0, n);
        line.geometry.attributes.position.needsUpdate = true;
        // label
        const tag = this.tags[i];
        v.set(a.pos.x, a.pos.y + (a.type === 'guard' ? 2.5 : 2.15), a.pos.z).project(cam);
        const vis = v.z < 1 && Math.abs(v.x) < 1.1 && Math.abs(v.y) < 1.1 && a.pos.distanceTo(cam.position) < 70;
        tag.style.display = vis ? 'block' : 'none';
        if (vis) {
          tag.style.left = ((v.x * 0.5 + 0.5) * w).toFixed(0) + 'px';
          tag.style.top = ((-v.y * 0.5 + 0.5) * h).toFixed(0) + 'px';
          tag.style.color = '#' + col.toString(16).padStart(6, '0');
          tag.textContent = a.name + ' [' + a.arch + '] ' + a.state + (a.alive ? '  ' + Math.ceil(a.hp) + 'hp  aw ' + a.perception.awareness.toFixed(2) : '');
        }
      });
      // noise rings
      const now = E.time;
      const recent = BI.Noise.recent.filter((n) => now - n.time < 2.0);
      this.rings.forEach((r, k) => {
        const n = recent[recent.length - 1 - k];
        if (!n) { r.visible = false; return; }
        const t = (now - n.time) / 2.0;
        r.visible = true;
        r.position.set(n.pos.x, n.pos.y + 0.08, n.pos.z);
        const s = n.radius * Math.min(1, t * 3);
        r.scale.set(s, 1, s);
        r.material.opacity = 0.7 * (1 - t);
        r.material.color.setHex(n.type === 'gunfire' ? 0xff5030 : n.type === 'generator' || n.type === 'alarm' ? 0xff30ff : 0xffd040);
      });
      // panel text
      const lines = [];
      lines.push('BLACKBOARD');
      lines.push(BI.Blackboard.dump());
      lines.push('');
      const c = BI.Agents.commander;
      lines.push('COMMANDER (GOAP)  goal: ' + c.goal);
      lines.push('  world: ' + Object.keys(c.worldState).map((k) => k + '=' + (c.worldState[k] ? 1 : 0)).join(' '));
      lines.push('  plan:  ' + (c.plan.map((nm, i) => (i === c.step ? '[' + nm + ']' : nm)).join(' → ') || '-'));
      for (const l of c.planLog) lines.push('  · ' + l);
      lines.push('');
      lines.push('TACTICAL GUARDS (UTILITY)');
      for (const a of BI.Agents.list) {
        if (a.type !== 'guard') continue;
        lines.push('  ' + a.name + (a.alive ? '  → ' + a.action : '  (destroyed)'));
        if (!a.alive) continue;
        for (const k of ['Attack', 'MoveToCover', 'Reposition', 'HoldPost']) {
          const s = Math.max(0, a.scores[k] || 0);
          lines.push('    ' + k.padEnd(12) + s.toFixed(2) + ' ' + '█'.repeat(Math.round(s * 20)));
        }
        lines.push('    ' + Object.keys(a.factors).map((k) => k + ' ' + a.factors[k]).join('  '));
      }
      lines.push('');
      lines.push('AGENTS');
      for (const a of BI.Agents.list) {
        if (a.type === 'guard' || a.type === 'commander') continue;
        lines.push('  ' + a.name.padEnd(10) + '[' + a.arch + '] ' + a.state + (a.type === 'hunter' ? '  ' + a.activeBranch : a.stateReason ? '  (' + a.stateReason + ')' : ''));
      }
      lines.push('');
      lines.push('EVENTS  ' + BI.Events.log.slice(-6).map((e) => e.name).join(', '));
      lines.push('A* last expanded: ' + BI.AStar.lastExpanded + '   queue: ' + BI.AStar.queue.length);
      this.panel.textContent = lines.join('\n');
    },
  };

  BI.Debug = Debug;
})();
