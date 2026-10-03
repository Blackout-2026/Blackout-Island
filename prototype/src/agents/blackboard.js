/* =============================================================================
 * agents/blackboard.js — shared blackboard (squad knowledge + directives)
 * -----------------------------------------------------------------------------
 * The single place agents share knowledge. Any agent that sees the player
 * writes a sighting; the Commander (GOAP) reads the blackboard to build its
 * world state, and writes DIRECTIVES back for the other three decision
 * layers to read:
 *   { type: 'converge' | 'search' | 'regroup', pos, radius, issuedAt, by }
 * Agents never call each other; they only read/write here (and use events).
 * Role: Agent Controller (Commander owner: GOAP).
 * ========================================================================== */
(function () {
  'use strict';

  const BB = {
    data: {
      alert: false,
      alertReason: '',
      alertTime: -1,
      cureStolen: false,
      generatorDown: false,
      playerLKP: null, // last known position (THREE.Vector3)
      playerLastSeen: -1e9,
      lastSeenBy: '',
      casualties: 0,
      casualtiesSinceRegroup: 0,
      rally: null,
    },
    directives: {},
    history: [],

    set(k, v) {
      this.data[k] = v;
    },
    get(k) {
      return this.data[k];
    },

    reportSighting(agent, pos) {
      const d = this.data;
      if (!d.playerLKP) d.playerLKP = new THREE.Vector3();
      d.playerLKP.copy(pos);
      d.playerLastSeen = BI.Engine.time;
      d.lastSeenBy = agent.name;
    },
    reportNoise(agent, pos) {
      const d = this.data;
      // a heard gunshot is weaker evidence than a sighting: only fills a gap
      if (BI.Engine.time - d.playerLastSeen > 4) {
        if (!d.playerLKP) d.playerLKP = new THREE.Vector3();
        d.playerLKP.copy(pos);
        d.playerLastSeen = BI.Engine.time - 2;
        d.lastSeenBy = agent.name + ' (heard)';
      }
    },
    sightingAge() {
      return BI.Engine.time - this.data.playerLastSeen;
    },

    issue(agentName, directive) {
      directive.issuedAt = BI.Engine.time;
      this.directives[agentName] = directive;
      this.history.push({ t: +BI.Engine.time.toFixed(1), to: agentName, type: directive.type });
      if (this.history.length > 20) this.history.shift();
    },
    directiveFor(agentName) {
      return this.directives[agentName] || null;
    },
    clearDirective(agentName) {
      delete this.directives[agentName];
    },

    dump() {
      const d = this.data;
      const v = (p) => (p ? p.x.toFixed(1) + ',' + p.z.toFixed(1) : '-');
      return [
        'alert          ' + d.alert + (d.alert ? ' (' + d.alertReason + ')' : ''),
        'cureStolen     ' + d.cureStolen,
        'generatorDown  ' + d.generatorDown,
        'playerLKP      ' + v(d.playerLKP) + '  age ' + (d.playerLastSeen > -1e8 ? this.sightingAge().toFixed(1) + 's' : '-') + '  by ' + (d.lastSeenBy || '-'),
        'casualties     ' + d.casualties,
        'directives     ' + Object.keys(this.directives).map((k) => k + ':' + this.directives[k].type).join(' ') || '-',
      ].join('\n');
    },
  };

  BI.Blackboard = BB;
})();
