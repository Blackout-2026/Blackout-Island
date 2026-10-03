/* =============================================================================
 * game/mission.js — mission flow, objectives, checkpoints, death and victory
 * -----------------------------------------------------------------------------
 * Phases (objective text shown in the HUD):
 *   beach      start on the dock            "Reach the bunker"
 *   forest     left the beach               "Get into the bunker"
 *   entrance   inside the compound          "Open the bunker door" / "Go down"
 *   bunker     underground                  "Find the generator room"
 *   generator  entered the generator room   "Disable the generator"
 *              (after the blackout)          "Take the cure from the laboratory"
 *   escape     cure taken                    "Escape to the boat"
 * Death: no game-over screen. After 2.2 s the player respawns at the start of
 * the current phase with full health and 1.5 s invulnerability; generator and
 * cure state persist. During the escape, checkpoints follow the route back out
 * (generator room -> bunker entrance -> forest edge).
 * Victory: reaching the boat with the cure -> Mission Complete (time, kills,
 * deaths, Restart).
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';
  const L = BI.Level;

  const OBJ = {
    beach: 'Reach the bunker in the north of the island',
    forest: 'Follow the trail and get into the bunker',
    entranceDoor: 'Open the bunker door [E]',
    entranceGo: 'Go down the ramp to level B1',
    bunker: 'Find the generator room',
    generator: 'Disable the generator — hold [E] at the control console',
    cure: 'Take the cure from the laboratory',
    escape: 'Escape — get back to the boat!',
  };
  const PHASE_LABEL = { beach: 'BEACH', forest: 'FOREST', entrance: 'BUNKER ENTRANCE', bunker: 'LEVEL B1', generator: 'GENERATOR ROOM', escape: 'ESCAPE' };

  const Mission = {
    phase: 'beach',
    checkpoint: 'beach',
    objective: '',
    startTime: 0,
    kills: 0,
    deaths: 0,
    complete: false,
    generatorDown: false,
    cureTaken: false,
    respawnAt: -1,

    init() {
      this.setPhase('beach', true);
      BI.Events.on('enemy:killed', () => this.kills++);
      BI.Events.on('player:died', () => {
        this.deaths++;
        this.respawnAt = BI.Engine.time + 2.2;
        BI.Events.emit('notify', { text: 'YOU DIED — RESPAWNING AT ' + PHASE_LABEL[this.checkpointPhase()] || '', kind: 'warn' });
      });
      BI.Events.on('generator:disabled', () => {
        this.generatorDown = true;
        this.setObjective(OBJ.cure);
        BI.Events.emit('notify', { text: 'POWER FAILURE — EMERGENCY LIGHTING ACTIVE', kind: 'warn' });
      });
      BI.Events.on('cure:taken', () => {
        this.cureTaken = true;
        this.setPhase('escape');
        BI.Events.emit('notify', { text: 'CURE ACQUIRED', kind: 'good' });
      });
      BI.Events.on('door:opened', (e) => {
        if (e.door.id === 'bunker' && this.phase === 'entrance') this.setObjective(OBJ.entranceGo);
      });
      BI.Engine.addUpdate((dt) => this.update(dt), 20);
    },

    checkpointPhase() {
      return { beach: 'beach', forest: 'forest', entrance: 'entrance', generator: 'generator', escapeEntrance: 'entrance', escapeForest: 'forest' }[this.checkpoint] || 'beach';
    },

    setObjective(text) {
      if (text === this.objective) return;
      this.objective = text;
      BI.Events.emit('objective:changed', { text, phase: this.phase, label: PHASE_LABEL[this.phase] });
    },

    setPhase(phase, silent) {
      this.phase = phase;
      if (phase === 'beach') { this.checkpoint = 'beach'; this.setObjective(OBJ.beach); }
      if (phase === 'forest') { this.checkpoint = 'forest'; this.setObjective(OBJ.forest); }
      if (phase === 'entrance') { this.checkpoint = 'entrance'; this.setObjective(BI.Doors.byId.bunker.isOpen() ? OBJ.entranceGo : OBJ.entranceDoor); }
      if (phase === 'bunker') this.setObjective(OBJ.bunker);
      if (phase === 'generator') { this.checkpoint = 'generator'; this.setObjective(this.generatorDown ? OBJ.cure : OBJ.generator); }
      if (phase === 'escape') { this.setObjective(OBJ.escape); }
      if (!silent) {
        BI.Events.emit('phase:changed', { phase, label: PHASE_LABEL[phase] });
        BI.Events.emit('checkpoint', { name: this.checkpoint });
      }
    },

    update() {
      const p = BI.player;
      if (!p || this.complete) return;
      if (!p.alive) {
        if (this.respawnAt > 0 && BI.Engine.time >= this.respawnAt) {
          this.respawnAt = -1;
          const cp = L.checkpoints[this.checkpoint];
          const y = this.checkpoint === 'beach' ? 3 : this.checkpoint === 'generator' ? L.BUNKER_Y + 0.5 : this.checkpoint.indexOf('ntrance') > 0 ? L.GROUND_Y + 0.5 : 40;
          p.respawn({ x: cp.x, y, z: cp.z, yaw: cp.yaw });
        }
        return;
      }
      const x = p.pos.x, z = p.pos.z;
      const c = L.compound;
      const inCompound = x > c.minX && x < c.maxX && z < c.maxZ && z > c.minZ;
      const g = L.rooms.generator;
      const inGen = x > g.minX && x < g.maxX && z > g.minZ && z < g.maxZ && p.pos.y < 6;
      if (!this.cureTaken) {
        if (this.phase === 'beach' && z < 92) this.setPhase('forest');
        if ((this.phase === 'beach' || this.phase === 'forest') && inCompound) this.setPhase('entrance');
        if (this.phase === 'entrance' && p.zone === 'bunker') this.setPhase('bunker');
        if ((this.phase === 'bunker' || this.phase === 'entrance') && inGen) this.setPhase('generator');
      } else {
        // escape: checkpoints move outward as the player retraces the route
        if (this.checkpoint === 'generator' && p.zone === 'outside' && inCompound) {
          this.checkpoint = 'escapeEntrance';
          BI.Events.emit('checkpoint', { name: this.checkpoint });
        }
        if (this.checkpoint === 'escapeEntrance' && !inCompound && z > -26) {
          this.checkpoint = 'escapeForest';
          BI.Events.emit('checkpoint', { name: this.checkpoint });
        }
        const b = L.boatZone;
        if (Math.hypot(x - b.x, z - b.z) < b.r && p.pos.y > 0.5) this.win();
      }
    },

    win() {
      this.complete = true;
      const time = BI.Engine.time - this.startTime;
      BI.Events.emit('mission:complete', { time, kills: this.kills, deaths: this.deaths });
    },
  };

  BI.Mission = Mission;
})();
