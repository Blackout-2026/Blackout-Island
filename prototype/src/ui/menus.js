/* =============================================================================
 * ui/menus.js — start screen, pause menu (settings), Mission Complete
 * -----------------------------------------------------------------------------
 * Settings: mouse sensitivity, quality (low / medium / high), master volume.
 * They persist in localStorage (wrapped in try/catch: file:// may block it).
 * Pause = pointer lock lost (Esc) while playing. Restart reloads the page,
 * which is the simplest way to reset every system deterministically.
 * Role: Core Developer (UI).
 * ========================================================================== */
(function () {
  'use strict';

  const store = {
    get(k, d) {
      try {
        const v = localStorage.getItem('bi_' + k);
        return v === null ? d : JSON.parse(v);
      } catch (e) {
        return d;
      }
    },
    set(k, v) {
      try { localStorage.setItem('bi_' + k, JSON.stringify(v)); } catch (e) { /* ignore */ }
    },
  };

  const CONTROLS = [
    ['W A S D', 'Move'], ['Mouse', 'Look'], ['Shift', 'Sprint'], ['Space', 'Jump'],
    ['C', 'Crouch'], ['F', 'Flashlight'], ['E', 'Interact (hold for generator)'], ['1 2 3 / Wheel', 'Weapons'],
    ['LMB', 'Fire / slash'], ['RMB', 'Sniper scope'], ['Esc', 'Pause / settings'], ['F1 or `', 'AI debug overlay'],
  ];

  const Menus = {
    screen: null,
    settings: { sensitivity: 1.0, quality: 'medium', volume: 0.8 },

    init() {
      this.settings.sensitivity = store.get('sens', 1.0);
      this.settings.volume = store.get('vol', 0.8);
      const urlQ = (location.search.match(/quality=(low|medium|high)/) || [])[1];
      this.settings.quality = urlQ || store.get('quality', 'medium');
      if (this.settings.quality !== BI.Engine.settings.name) BI.Engine.applyQuality(this.settings.quality);
      BI.player.sensitivity = this.settings.sensitivity;
      BI.Events.on('input:lock', (e) => {
        if (!e.locked && BI.Game.started && !BI.Mission.complete && !BI.Input.allowUnlocked) this.showPause();
      });
      BI.Events.on('mission:complete', (e) => this.showComplete(e));
      BI.Engine.addAlwaysUpdate(() => {
        if (BI.Input.allowUnlocked && BI.Input.pressedRaw('Escape') && BI.Game.started && !BI.Mission.complete) {
          if (this.screen) this.resume();
          else this.showPause();
        }
      }, 1);
    },

    clear() {
      if (this.screen) this.screen.remove();
      this.screen = null;
    },

    mount(html) {
      this.clear();
      const d = document.createElement('div');
      d.className = 'screen';
      d.innerHTML = html;
      document.getElementById('ui').appendChild(d);
      this.screen = d;
      return d;
    },

    settingsHTML() {
      const s = this.settings;
      const opt = (v, l) => '<option value="' + v + '"' + (s.quality === v ? ' selected' : '') + '>' + l + '</option>';
      return (
        '<div class="row"><label>Mouse sensitivity</label><input id="m_sens" type="range" min="0.2" max="3" step="0.05" value="' + s.sensitivity + '"><span id="m_sens_v">' + s.sensitivity.toFixed(2) + '</span></div>' +
        '<div class="row"><label>Quality</label><select id="m_q">' + opt('low', 'Low') + opt('medium', 'Medium') + opt('high', 'High') + '</select></div>' +
        '<div class="row"><label>Volume</label><input id="m_vol" type="range" min="0" max="1" step="0.05" value="' + s.volume + '"><span id="m_vol_v">' + Math.round(s.volume * 100) + '%</span></div>'
      );
    },

    wireSettings(root) {
      const sens = root.querySelector('#m_sens'), q = root.querySelector('#m_q'), vol = root.querySelector('#m_vol');
      sens.oninput = () => {
        this.settings.sensitivity = +sens.value;
        root.querySelector('#m_sens_v').textContent = (+sens.value).toFixed(2);
        BI.player.sensitivity = +sens.value;
        store.set('sens', +sens.value);
      };
      q.onchange = () => {
        this.settings.quality = q.value;
        store.set('quality', q.value);
        BI.Engine.applyQuality(q.value);
      };
      vol.oninput = () => {
        this.settings.volume = +vol.value;
        root.querySelector('#m_vol_v').textContent = Math.round(+vol.value * 100) + '%';
        BI.Audio.setVolume(+vol.value);
        store.set('vol', +vol.value);
      };
    },

    showStart() {
      const d = this.mount(
        '<div class="title">BLACKOUT ISLAND</div><div class="subtitle">THE CURE</div>' +
          '<div class="story">A virus is spreading. The only supply of the cure is locked in an underground research bunker on a remote island, guarded by a private security force. ' +
          'You land alone at night. Get through the forest, kill the bunker\'s power, steal the cure from the lab — and make it back to the boat.</div>' +
          '<div class="controls">' + CONTROLS.map((c) => '<b>' + c[0] + '</b><span>' + c[1] + '</span>').join('') + '</div>' +
          '<div class="menu-panel">' + this.settingsHTML() + '<button id="startbtn">CLICK TO START</button></div>' +
          '<div class="hint">Best with headphones. The first launch compiles shaders; later launches are faster.</div>'
      );
      this.wireSettings(d);
      d.querySelector('#startbtn').onclick = () => {
        BI.Events.emit('ui:click', {});
        BI.Game.start(false);
      };
    },

    showPause() {
      BI.Engine.paused = true;
      const d = this.mount(
        '<div class="title" style="font-size:44px">PAUSED</div><div class="subtitle">' + (BI.Mission.objective || '') + '</div>' +
          '<div class="menu-panel">' + this.settingsHTML() +
          '<div><button id="resume">RESUME</button><button id="restart" class="ghost">RESTART MISSION</button></div></div>' +
          '<div class="controls" style="margin-top:20px">' + CONTROLS.map((c) => '<b>' + c[0] + '</b><span>' + c[1] + '</span>').join('') + '</div>'
      );
      this.wireSettings(d);
      d.querySelector('#resume').onclick = () => this.resume();
      d.querySelector('#restart').onclick = () => location.reload();
    },

    resume() {
      this.clear();
      BI.Engine.paused = false;
      if (!BI.Input.allowUnlocked) BI.Input.requestLock();
    },

    showComplete(e) {
      BI.Engine.paused = true;
      BI.Input.exitLock();
      const t = Math.round(e.time);
      const mm = Math.floor(t / 60), ss = String(t % 60).padStart(2, '0');
      const d = this.mount(
        '<div class="title">MISSION COMPLETE</div><div class="subtitle">THE CURE IS OFF THE ISLAND</div>' +
          '<div class="menu-panel"><div class="stats">' +
          '<span>TIME</span><span>' + mm + ':' + ss + '</span>' +
          '<span>KILLS</span><span>' + e.kills + ' / ' + BI.Agents.list.length + '</span>' +
          '<span>DEATHS</span><span>' + e.deaths + '</span>' +
          '</div><button id="again">RESTART</button></div>'
      );
      d.id = 'complete';
      d.querySelector('#again').onclick = () => location.reload();
    },
  };

  BI.Menus = Menus;
})();
