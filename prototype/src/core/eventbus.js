/* =============================================================================
 * core/eventbus.js — decoupled publish / subscribe
 * -----------------------------------------------------------------------------
 * This mirrors the Unity "ScriptableObject event channel" pattern we will use
 * in the real game: a publisher raises a named channel, and any number of
 * listeners react, without either side holding a reference to the other.
 *
 *   BI.Events.on('generator:disabled', fn)   // subscribe (returns unsubscribe)
 *   BI.Events.emit('generator:disabled', {}) // publish
 *
 * Channels used by the game (payloads in brackets):
 *   noise                 {pos, radius, type, source}       sound propagation
 *   generator:disabled    {pos}                             the event chain
 *   cure:taken            {pos}
 *   alert:broadcast       {by, pos, reason}                 Commander -> all
 *   player:damaged        {amount, from, health}
 *   player:died / player:respawned / player:shot
 *   enemy:damaged / enemy:killed {agent, ...}
 *   door:opened / door:closed / door:locked {door}
 *   nav:changed           {minX,minZ,maxX,maxZ}             grid updated
 *   phase:changed         {phase}
 *   objective:changed     {text}
 *   mission:complete      {time, kills, deaths}
 *   notify                {text, kind}                      HUD toast
 *
 * Every emit is appended to a small ring-buffer log so the debug overlay and
 * the automated smoke test can prove which events fired and in what order.
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';

  class EventBus {
    constructor() {
      this.channels = new Map();
      this.log = [];
      this.maxLog = 200;
      this.time = () => (BI.Engine && BI.Engine.time) || 0;
    }

    on(name, fn) {
      if (!this.channels.has(name)) this.channels.set(name, []);
      this.channels.get(name).push(fn);
      return () => this.off(name, fn);
    }

    once(name, fn) {
      const off = this.on(name, (p) => {
        off();
        fn(p);
      });
      return off;
    }

    off(name, fn) {
      const list = this.channels.get(name);
      if (!list) return;
      const i = list.indexOf(fn);
      if (i >= 0) list.splice(i, 1);
    }

    emit(name, payload) {
      if (name !== 'noise' && name !== 'player:footstep') {
        this.log.push({ name, t: +this.time().toFixed(2) });
        if (this.log.length > this.maxLog) this.log.shift();
      }
      const list = this.channels.get(name);
      if (!list) return;
      // Copy so listeners may unsubscribe while being called.
      const copy = list.slice();
      for (let i = 0; i < copy.length; i++) {
        try {
          copy[i](payload || {});
        } catch (err) {
          console.error('[EventBus] listener for "' + name + '" threw', err);
        }
      }
    }

    count(name) {
      let n = 0;
      for (const e of this.log) if (e.name === name) n++;
      return n;
    }
  }

  BI.EventBus = EventBus;
  BI.Events = new EventBus();
})();
