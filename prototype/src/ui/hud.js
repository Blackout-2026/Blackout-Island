/* =============================================================================
 * ui/hud.js — in-game HUD (HTML/CSS overlay)
 * -----------------------------------------------------------------------------
 * Health bar (+ trailing damage ghost), weapon slots 1-3 with icons, crosshair
 * that changes per weapon and opens with spread, hit-marker (red on kill),
 * damage direction arcs, interaction prompt with hold-progress ring, phase +
 * objective text, compass with objective marker and distance, checkpoint
 * label, SECURITY ALERT banner, toasts, sniper scope overlay.
 * Purely a subscriber: it reads state and listens to events.
 * Role: Core Developer (UI).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const ICONS = {
    smg: '<svg viewBox="0 0 80 32"><path fill="currentColor" d="M4 12h44l4-3h12v5h10v4H64v3H52l-3 9h-7l2-9h-6l-2 7h-7l2-7H18v4h-4v-4H4z"/></svg>',
    sniper: '<svg viewBox="0 0 80 32"><path fill="currentColor" d="M2 15h10l6-3h22V8h16v4h4v3h18v3H60l-4 3H44l-3 8h-6l2-8H22l-8 5H4z"/></svg>',
    knife: '<svg viewBox="0 0 80 32"><path fill="currentColor" d="M8 13h22v6H8zM30 11h4v10h-4zM34 13h30l10 3-10 3H34z"/></svg>',
  };

  const HUD = {
    el: {},
    dmgArcs: [],
    hitT: 0,
    alertT: 0,
    toastT: 0,
    ghost: 100,

    build() {
      const ui = document.getElementById('ui');
      const hud = document.createElement('div');
      hud.id = 'hud';
      hud.innerHTML = [
        '<div class="dmg-css"></div>',
        '<div class="scope"><div class="mask"></div><div class="ring"></div><div class="h"></div><div class="v"></div><div class="mil"></div></div>',
        '<div class="hud-objective"><div class="phase"></div><div class="text"></div></div>',
        '<div class="hud-compass"><div class="strip"></div><div class="center"></div><div class="obj"></div></div><div class="hud-dist"></div>',
        '<div class="hud-checkpoint">CHECKPOINT<b></b></div>',
        '<div class="hud-health"><div class="lbl">HEALTH</div><div class="val">100</div><div class="track"><div class="ghost"></div><div class="fill"></div></div></div>',
        '<div class="hud-weapons"></div>',
        '<div class="hud-flash"></div>',
        '<div class="xhair"></div>',
        '<div class="hitmark"><i></i><i></i><i></i><i></i></div>',
        '<div class="dmgdir"></div>',
        '<div class="hud-prompt"><span class="k"></span><span class="t"></span><svg viewBox="0 0 40 40"><circle class="ring-bg" cx="20" cy="20" r="16"/><circle class="ring-fg" cx="20" cy="20" r="16" stroke-dasharray="100.5" stroke-dashoffset="100.5"/></svg></div>',
        '<div class="hud-alert">SECURITY ALERT<small>THE CURE HAS BEEN STOLEN — ALL UNITS CONVERGING</small></div>',
        '<div class="hud-toast"></div>',
      ].join('');
      ui.appendChild(hud);
      const q = (s) => hud.querySelector(s);
      this.el = {
        hud, dmgCss: q('.dmg-css'), scope: q('.scope'), phase: q('.hud-objective .phase'), obj: q('.hud-objective .text'),
        strip: q('.hud-compass .strip'), compObj: q('.hud-compass .obj'), dist: q('.hud-dist'), cp: q('.hud-checkpoint b'),
        health: q('.hud-health'), hval: q('.hud-health .val'), hfill: q('.hud-health .fill'), hghost: q('.hud-health .ghost'),
        weapons: q('.hud-weapons'), flash: q('.hud-flash'), xhair: q('.xhair'), hit: q('.hitmark'), dmgdir: q('.dmgdir'),
        prompt: q('.hud-prompt'), pk: q('.hud-prompt .k'), pt: q('.hud-prompt .t'), pring: q('.hud-prompt svg'), pfg: q('.ring-fg'),
        alert: q('.hud-alert'), toast: q('.hud-toast'),
      };
      // weapon slots
      this.el.weapons.innerHTML = BI.WEAPONS.map((w) => '<div class="wslot" data-id="' + w.id + '"><span class="key">' + w.slot + '</span>' + ICONS[w.id] + '<span class="nm">' + w.name + '</span></div>').join('');
      // compass ticks (two full turns so it can scroll seamlessly)
      let ticks = '';
      const card = { 0: 'N', 45: 'NE', 90: 'E', 135: 'SE', 180: 'S', 225: 'SW', 270: 'W', 315: 'NW' };
      for (let d = -360; d <= 720; d += 15) {
        const n = ((d % 360) + 360) % 360;
        ticks += '<span class="tick' + (card[n] ? ' card' : '') + '" style="left:' + d * 2 + 'px">' + (card[n] || '|') + '</span>';
      }
      this.el.strip.innerHTML = ticks;
      this.setCrosshair('cross');
      this.subscribe();
      BI.Engine.addAlwaysUpdate((dt) => this.update(dt), 96);
      hud.style.display = 'none';
    },

    show(v) {
      this.el.hud.style.display = v ? 'block' : 'none';
    },

    setCrosshair(kind) {
      this.xkind = kind;
      this.el.xhair.innerHTML = kind === 'cross' ? '<i class="t"></i><i class="b"></i><i class="l"></i><i class="r"></i><i class="dot"></i>' : kind === 'dot' ? '<i class="dot"></i>' : '<i class="ring"></i><i class="dot"></i>';
    },

    subscribe() {
      const E = BI.Events;
      E.on('objective:changed', (e) => {
        this.el.obj.textContent = e.text;
        this.el.phase.textContent = e.label || '';
        this.el.obj.classList.remove('flash');
        void this.el.obj.offsetWidth;
        this.el.obj.classList.add('flash');
      });
      E.on('phase:changed', (e) => (this.el.phase.textContent = e.label));
      E.on('checkpoint', (e) => {
        const names = { beach: 'BEACH', forest: 'FOREST', entrance: 'BUNKER ENTRANCE', generator: 'GENERATOR ROOM', escapeEntrance: 'BUNKER ENTRANCE', escapeForest: 'FOREST EDGE' };
        this.el.cp.textContent = names[e.name] || e.name;
        this.toast('CHECKPOINT — ' + (names[e.name] || e.name), '');
      });
      E.on('weapon:switched', (e) => this.setCrosshair(e.weapon.crosshair));
      E.on('hit:marker', (e) => {
        this.hitT = e.kill ? 0.35 : 0.16;
        this.el.hit.classList.toggle('kill', !!e.kill);
      });
      E.on('player:damaged', (e) => {
        BI.Engine.fx.damage = Math.min(1, BI.Engine.fx.damage + e.amount / 25);
        if (e.from) this.damageArc(e.from);
      });
      E.on('alert:broadcast', () => {
        this.alertT = 7;
        this.el.alert.classList.add('on');
      });
      E.on('notify', (e) => this.toast(e.text, e.kind));
    },

    toast(text, kind) {
      const t = this.el.toast;
      t.textContent = text;
      t.className = 'hud-toast on ' + (kind || '');
      this.toastT = 3.2;
    },

    damageArc(from) {
      const p = BI.player;
      const ang = Math.atan2(from.x - p.pos.x, from.z - p.pos.z); // world bearing (atan2(x,z))
      const fwd = Math.atan2(-Math.sin(p.yaw), -Math.cos(p.yaw));
      const rel = U.angleDiff(fwd, ang);
      const arc = document.createElement('div');
      arc.className = 'arc';
      arc.style.transform = 'rotate(' + (-rel) + 'rad)';
      arc.style.opacity = '1';
      this.el.dmgdir.appendChild(arc);
      this.dmgArcs.push({ el: arc, t: 1.1 });
    },

    objectiveTarget() {
      const M = BI.Mission, L = BI.Level;
      if (!M) return null;
      const BF = L.BUNKER_Y;
      if (M.cureTaken) {
        const p = BI.player;
        if (p.zone === 'bunker') return new THREE.Vector3(0, BF, -70);
        if (p.zone === 'entrance') return new THREE.Vector3(0, 10, -46);
        return new THREE.Vector3(L.boatZone.x, 1, L.boatZone.z);
      }
      if (M.phase === 'beach' || M.phase === 'forest') return new THREE.Vector3(0, 10, -46);
      if (M.phase === 'entrance') return new THREE.Vector3(0, 10, -60);
      if (M.phase === 'bunker') return new THREE.Vector3(-2.2, BF, -106);
      if (M.phase === 'generator') return M.generatorDown ? new THREE.Vector3(0, BF, -130) : new THREE.Vector3(L.generator.panel.x, BF, L.generator.panel.z);
      return null;
    },

    update(dt) {
      if (this.el.hud.style.display === 'none') return;
      const p = BI.player, ws = BI.weapons;
      if (!p) return;
      // health
      const h = Math.max(0, p.health);
      this.el.hval.textContent = Math.ceil(h);
      this.el.hfill.style.width = h + '%';
      this.ghost = Math.max(h, U.damp(this.ghost, h, 2.5, dt));
      this.el.hghost.style.width = this.ghost + '%';
      this.el.health.classList.toggle('low', h < 35);
      const E = BI.Engine;
      E.fx.damage = Math.max(0, E.fx.damage - dt * 1.4);
      E.fx.lowHealth = p.alive ? U.clamp01((40 - h) / 40) * (0.75 + 0.25 * Math.sin(E.realTime * 6)) : 1;
      this.el.dmgCss.style.opacity = E.composer ? 0 : Math.min(0.9, E.fx.damage + E.fx.lowHealth * 0.5);
      // weapons
      if (ws) {
        for (const s of this.el.weapons.children) s.classList.toggle('active', s.dataset.id === ws.def.id);
        // crosshair spread
        const zoomed = ws.zoom > 0.85;
        this.el.scope.classList.toggle('on', zoomed);
        this.el.xhair.style.display = zoomed || !p.alive ? 'none' : 'block';
        if (this.xkind === 'cross') {
          const gap = 6 + ws.currentSpread() * 520;
          const c = this.el.xhair.children;
          c[0].style.top = -gap - 8 + 'px';
          c[1].style.top = gap + 'px';
          c[2].style.left = -gap - 8 + 'px';
          c[3].style.left = gap + 'px';
        }
      }
      this.el.flash.textContent = 'F — FLASHLIGHT ' + (p.flashOn ? 'ON' : 'OFF');
      // hit marker
      this.hitT -= dt;
      this.el.hit.style.opacity = this.hitT > 0 ? 1 : 0;
      // damage arcs
      for (let i = this.dmgArcs.length - 1; i >= 0; i--) {
        const a = this.dmgArcs[i];
        a.t -= dt;
        a.el.style.opacity = Math.max(0, a.t).toFixed(2);
        if (a.t <= 0) { a.el.remove(); this.dmgArcs.splice(i, 1); }
      }
      // prompt
      const pr = BI.Interact && BI.Interact.prompt;
      if (pr && p.alive) {
        this.el.prompt.style.display = 'flex';
        this.el.pk.textContent = pr.key;
        this.el.pk.style.display = pr.key ? 'inline-block' : 'none';
        this.el.pt.textContent = pr.text;
        this.el.pring.style.display = pr.hold ? 'block' : 'none';
        this.el.pfg.setAttribute('stroke-dashoffset', (100.5 * (1 - pr.progress)).toFixed(1));
      } else this.el.prompt.style.display = 'none';
      // compass
      const bearing = -p.yaw * 180 / Math.PI;
      const b = ((bearing % 360) + 360) % 360;
      this.el.strip.style.left = 230 - b * 2 + 'px';
      const tgt = this.objectiveTarget();
      if (tgt) {
        const tb = Math.atan2(tgt.x - p.pos.x, -(tgt.z - p.pos.z)) * 180 / Math.PI;
        let rel = ((tb - b + 540) % 360) - 180;
        rel = U.clamp(rel, -112, 112);
        this.el.compObj.style.left = 230 + rel * 2 + 'px';
        this.el.compObj.style.display = 'block';
        this.el.dist.textContent = Math.round(Math.hypot(tgt.x - p.pos.x, tgt.z - p.pos.z)) + ' m';
      } else {
        this.el.compObj.style.display = 'none';
        this.el.dist.textContent = '';
      }
      // alert + toast timers
      if (this.alertT > 0) {
        this.alertT -= dt;
        if (this.alertT <= 0) {
          this.el.alert.classList.remove('on');
          this.el.alert.style.display = 'block';
          this.el.alert.style.fontSize = '13px';
          this.el.alert.style.top = '58px';
          this.el.alert.style.padding = '3px 24px';
          this.el.alert.innerHTML = 'SECURITY ALERT';
        }
      }
      if (this.toastT > 0) {
        this.toastT -= dt;
        if (this.toastT <= 0) this.el.toast.classList.remove('on');
      }
    },
  };

  BI.HUD = HUD;
})();
