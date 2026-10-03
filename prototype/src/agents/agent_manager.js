/* =============================================================================
 * agents/agent_manager.js — spawns and updates every agent
 * -----------------------------------------------------------------------------
 * Spawn table (BI.Level.agents): Hunter x2 (forest), Security Patrol x3
 * (compound, security office, corridor), Tactical Guard x2 (generator room,
 * lab), Commander x1 (lab).
 * Event-chain hooks (subscribers only — nothing calls the AI directly):
 *   generator:disabled  -> Patrols and Guards become "heightened"
 *                          (+25% speed, +40% vision range)
 *   alert:broadcast     -> every living agent gets heightened as well
 *   cure:taken          -> if the Commander is already dead, the lab's
 *                          automatic system raises the alert instead
 *   player:respawned    -> agents lose track of the player (fair restart)
 * Role: Agent Controller.
 * ========================================================================== */
(function () {
  'use strict';

  const Agents = {
    list: [],
    group: null,

    spawn(scene) {
      const A = BI.Level.agents;
      const g = new THREE.Group();
      g.name = 'agents';
      scene.add(g);
      this.group = g;
      A.hunters.forEach((s, i) => this.list.push(new BI.Hunter(s, i, g)));
      A.patrols.forEach((s, i) => this.list.push(new BI.SecurityPatrol(s, i, g)));
      A.guards.forEach((s, i) => this.list.push(new BI.TacticalGuard(s, i, g)));
      this.commander = new BI.Commander(A.commander, g);
      this.list.push(this.commander);

      BI.Events.on('generator:disabled', () => {
        BI.Blackboard.set('generatorDown', true);
        for (const a of this.list) if (a.type === 'patrol' || a.type === 'guard') a.heightened = true;
      });
      BI.Events.on('alert:broadcast', () => {
        for (const a of this.list) if (a.alive) a.heightened = true;
      });
      BI.Events.on('cure:taken', () => {
        setTimeout(() => {
          if (this.commander.alive || BI.Blackboard.data.alert) return;
          // no commander left: automated lab security raises the alarm
          const BB = BI.Blackboard;
          BB.set('alert', true);
          BB.set('alertReason', 'lab sensors');
          BB.reportSighting(this.commander, BI.player.pos);
          for (const u of this.list) if (u.alive) BB.issue(u.name, { type: 'converge', pos: BI.player.pos.clone(), by: 'system' });
          BI.Events.emit('alert:broadcast', { by: 'Automated security', pos: BI.player.pos.clone(), reason: 'lab sensors' });
        }, 1500);
      });
      BI.Events.on('player:respawned', () => {
        for (const a of this.list) {
          if (!a.alive) continue;
          a.perception.forget();
          a.engageStart = -1;
        }
        BI.Blackboard.data.playerLastSeen = -1e9;
      });
      BI.Engine.addUpdate((dt) => this.update(dt), 30);
    },

    update(dt) {
      BI.AStar.update();
      if (this.frozen) return; // test hook: pose agents for screenshots
      for (const a of this.list) a.update(dt);
      this.pushPlayer();
    },

    // The player cannot walk through living agents.
    pushPlayer() {
      const p = BI.player;
      if (!p) return;
      for (const a of this.list) {
        if (!a.alive) continue;
        const dx = p.pos.x - a.pos.x, dz = p.pos.z - a.pos.z;
        const d = Math.hypot(dx, dz);
        const min = p.radius + a.controller.radius;
        if (d < min && d > 1e-4 && Math.abs(p.pos.y - a.pos.y) < 1.6) {
          p.pos.x += (dx / d) * (min - d);
          p.pos.z += (dz / d) * (min - d);
        }
      }
    },

    living() {
      return this.list.filter((a) => a.alive);
    },
    byName(n) {
      return this.list.find((a) => a.name === n);
    },
  };

  BI.Agents = Agents;
})();
