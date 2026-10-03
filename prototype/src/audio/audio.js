/* =============================================================================
 * audio/audio.js — all sound, synthesised with WebAudio (no audio files)
 * -----------------------------------------------------------------------------
 * Graph:  sources -> [PannerNode for world sounds] -> sfx / amb buses
 *         sfx -> dry + reverb send (ConvolverNode with a generated impulse,
 *         wet amount follows "how far underground" the listener is)
 *         -> master (volume setting) -> compressor -> speakers
 * Ambience: waves (brown noise, slow swell, louder near the shore), wind
 * (band-passed noise with a drifting centre), forest insects, bunker room
 * tone + generator hum (50/100/150 Hz, positional; spins down at the
 * blackout), two-tone alarm siren after the blackout.
 * One-shots subscribe to game events, so no system calls audio directly.
 * Role: Systems Engineer (audio).
 * ========================================================================== */
(function () {
  'use strict';
  const U = BI.U;

  const Audio = {
    ctx: null,
    ready: false,
    volume: 0.8,
    _v: new THREE.Vector3(),

    start() {
      if (this.ctx) {
        if (this.ctx.state === 'suspended') this.ctx.resume();
        return;
      }
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      const ctx = (this.ctx = new AC());
      this.comp = ctx.createDynamicsCompressor();
      this.comp.threshold.value = -14;
      this.comp.ratio.value = 4;
      this.comp.connect(ctx.destination);
      this.master = ctx.createGain();
      this.master.gain.value = this.volume;
      this.master.connect(this.comp);
      this.sfx = ctx.createGain();
      this.sfx.connect(this.master);
      this.amb = ctx.createGain();
      this.amb.gain.value = 0.9;
      this.amb.connect(this.master);
      // reverb
      this.reverb = ctx.createConvolver();
      this.reverb.buffer = this.impulse(2.4, 2.6);
      this.wet = ctx.createGain();
      this.wet.gain.value = 0;
      this.sfx.connect(this.wet);
      this.wet.connect(this.reverb);
      this.reverb.connect(this.master);
      this.noiseBuf = this.makeNoise(2, false);
      this.brownBuf = this.makeNoise(4, true);
      this.buildAmbience();
      this.subscribe();
      this.ready = true;
      BI.Engine.addAlwaysUpdate(() => this.update(), 95);
    },

    setVolume(v) {
      this.volume = v;
      if (this.master) this.master.gain.setTargetAtTime(v, this.ctx.currentTime, 0.05);
    },

    makeNoise(sec, brown) {
      const ctx = this.ctx;
      const n = Math.floor(ctx.sampleRate * sec);
      const b = ctx.createBuffer(1, n, ctx.sampleRate);
      const d = b.getChannelData(0);
      let last = 0;
      for (let i = 0; i < n; i++) {
        const w = Math.random() * 2 - 1;
        if (brown) {
          last = (last + 0.02 * w) / 1.02;
          d[i] = last * 3.5;
        } else d[i] = w;
      }
      return b;
    },

    impulse(sec, decay) {
      const ctx = this.ctx;
      const n = Math.floor(ctx.sampleRate * sec);
      const b = ctx.createBuffer(2, n, ctx.sampleRate);
      for (let c = 0; c < 2; c++) {
        const d = b.getChannelData(c);
        for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / n, decay) * (i < 400 ? i / 400 : 1);
      }
      return b;
    },

    // Destination for a sound: positional panner (world pos) or the sfx bus.
    out(pos, opts) {
      const ctx = this.ctx;
      if (!pos) return this.sfx;
      const p = ctx.createPanner();
      p.panningModel = 'equalpower';
      p.distanceModel = 'inverse';
      p.refDistance = (opts && opts.ref) || 3;
      p.rolloffFactor = (opts && opts.rolloff) || 1.1;
      p.maxDistance = 120;
      if (p.positionX) {
        p.positionX.value = pos.x; p.positionY.value = pos.y; p.positionZ.value = pos.z;
      } else p.setPosition(pos.x, pos.y, pos.z);
      p.connect(this.sfx);
      return p;
    },

    noise(o) {
      if (!this.ready) return;
      const ctx = this.ctx, t = ctx.currentTime + (o.delay || 0);
      const src = ctx.createBufferSource();
      src.buffer = o.brown ? this.brownBuf : this.noiseBuf;
      src.loop = true;
      const f = ctx.createBiquadFilter();
      f.type = o.type || 'bandpass';
      f.frequency.setValueAtTime(o.freq || 1000, t);
      if (o.freqEnd) f.frequency.exponentialRampToValueAtTime(o.freqEnd, t + o.dur);
      f.Q.value = o.q || 1;
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.0001, t);
      g.gain.exponentialRampToValueAtTime(o.gain || 0.3, t + (o.attack || 0.004));
      g.gain.exponentialRampToValueAtTime(0.0001, t + o.dur);
      src.connect(f);
      f.connect(g);
      g.connect(this.out(o.pos, o));
      src.start(t, Math.random() * 1.5);
      src.stop(t + o.dur + 0.05);
    },

    tone(o) {
      if (!this.ready) return;
      const ctx = this.ctx, t = ctx.currentTime + (o.delay || 0);
      const osc = ctx.createOscillator();
      osc.type = o.wave || 'sine';
      osc.frequency.setValueAtTime(o.freq, t);
      if (o.freqEnd) osc.frequency.exponentialRampToValueAtTime(o.freqEnd, t + o.dur);
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.0001, t);
      g.gain.exponentialRampToValueAtTime(o.gain || 0.2, t + (o.attack || 0.004));
      g.gain.exponentialRampToValueAtTime(0.0001, t + o.dur);
      let node = osc;
      if (o.filter) {
        const f = ctx.createBiquadFilter();
        f.type = o.filter;
        f.frequency.value = o.ffreq || 800;
        f.Q.value = o.q || 1;
        osc.connect(f);
        node = f;
      }
      node.connect(g);
      g.connect(this.out(o.pos, o));
      osc.start(t);
      osc.stop(t + o.dur + 0.05);
    },

    // ---------------------------------------------------------------------------
    // Ambience
    // ---------------------------------------------------------------------------
    loopNoise(brown, type, freq, q, gain) {
      const ctx = this.ctx;
      const src = ctx.createBufferSource();
      src.buffer = brown ? this.brownBuf : this.noiseBuf;
      src.loop = true;
      const f = ctx.createBiquadFilter();
      f.type = type;
      f.frequency.value = freq;
      f.Q.value = q;
      const g = ctx.createGain();
      g.gain.value = gain;
      src.connect(f);
      f.connect(g);
      g.connect(this.amb);
      src.start();
      return { src, f, g };
    },

    buildAmbience() {
      const ctx = this.ctx;
      this.waves = this.loopNoise(true, 'lowpass', 520, 0.5, 0);
      this.wind = this.loopNoise(false, 'bandpass', 600, 0.7, 0);
      this.room = this.loopNoise(true, 'lowpass', 180, 0.7, 0);
      // generator hum: positional, three harmonics through a lowpass
      const pan = ctx.createPanner();
      pan.panningModel = 'equalpower';
      pan.distanceModel = 'inverse';
      pan.refDistance = 4;
      pan.rolloffFactor = 1.3;
      const gp = new THREE.Vector3(BI.Level.generator.x, BI.Level.BUNKER_Y + 1.5, BI.Level.generator.z);
      if (pan.positionX) { pan.positionX.value = gp.x; pan.positionY.value = gp.y; pan.positionZ.value = gp.z; } else pan.setPosition(gp.x, gp.y, gp.z);
      const humGain = ctx.createGain();
      humGain.gain.value = 0;
      const lp = ctx.createBiquadFilter();
      lp.type = 'lowpass';
      lp.frequency.value = 420;
      this.humOsc = [];
      for (const [f, type, g] of [[50, 'sine', 0.5], [100, 'sawtooth', 0.12], [150, 'sine', 0.15], [300, 'triangle', 0.03]]) {
        const o = ctx.createOscillator();
        o.type = type;
        o.frequency.value = f;
        const og = ctx.createGain();
        og.gain.value = g;
        o.connect(og);
        og.connect(lp);
        o.start();
        this.humOsc.push(o);
      }
      // slight wobble
      const lfo = ctx.createOscillator();
      lfo.frequency.value = 0.4;
      const lfoG = ctx.createGain();
      lfoG.gain.value = 0.08;
      lfo.connect(lfoG);
      lfoG.connect(humGain.gain);
      lfo.start();
      lp.connect(humGain);
      humGain.connect(pan);
      pan.connect(this.amb);
      this.humGain = humGain;
      this.humLevel = 0.55;
      // alarm siren (silent until the blackout)
      const siren = ctx.createOscillator();
      siren.type = 'square';
      siren.frequency.value = 660;
      const sf = ctx.createBiquadFilter();
      sf.type = 'bandpass';
      sf.frequency.value = 900;
      sf.Q.value = 2;
      const sg = ctx.createGain();
      sg.gain.value = 0;
      siren.connect(sf);
      sf.connect(sg);
      sg.connect(this.amb);
      siren.start();
      this.siren = siren;
      this.sirenGain = sg;
      this.alarmOn = false;
      this.insectT = 0;
    },

    update() {
      if (!this.ready) return;
      const ctx = this.ctx, t = ctx.currentTime;
      const cam = BI.Engine.camera;
      const l = ctx.listener;
      const p = cam.getWorldPosition(this._v);
      const f = cam.getWorldDirection(new THREE.Vector3());
      if (l.positionX) {
        l.positionX.setTargetAtTime(p.x, t, 0.02); l.positionY.setTargetAtTime(p.y, t, 0.02); l.positionZ.setTargetAtTime(p.z, t, 0.02);
        l.forwardX.setTargetAtTime(f.x, t, 0.02); l.forwardY.setTargetAtTime(f.y, t, 0.02); l.forwardZ.setTargetAtTime(f.z, t, 0.02);
        l.upX.value = 0; l.upY.value = 1; l.upZ.value = 0;
      } else {
        l.setPosition(p.x, p.y, p.z);
        l.setOrientation(f.x, f.y, f.z, 0, 1, 0);
      }
      const pl = BI.player;
      const inter = pl ? pl.interior : 0;
      const shore = U.smoothstep(40, 100, p.z);
      const now = BI.Engine.realTime;
      const swell = 0.6 + 0.4 * Math.sin(now * 0.55) * Math.sin(now * 0.21 + 1);
      this.waves.g.gain.setTargetAtTime((0.05 + 0.32 * shore) * swell * (1 - inter), t, 0.2);
      this.wind.g.gain.setTargetAtTime(0.045 * (1 - inter) * (0.7 + 0.3 * Math.sin(now * 0.3)), t, 0.3);
      this.wind.f.frequency.setTargetAtTime(500 + 300 * Math.sin(now * 0.17), t, 0.5);
      this.room.g.gain.setTargetAtTime(0.06 * inter, t, 0.3);
      this.humGain.gain.setTargetAtTime(this.humLevel * U.smoothstep(0.3, 1, inter), t, 0.3);
      this.wet.gain.setTargetAtTime(0.4 * inter, t, 0.2);
      if (this.alarmOn) {
        const hi = Math.floor(now * 1.6) % 2 === 0;
        this.siren.frequency.setTargetAtTime(hi ? 880 : 620, t, 0.01);
        this.sirenGain.gain.setTargetAtTime(0.05 + 0.07 * inter, t, 0.1);
      }
      // forest insects
      if (inter < 0.2 && pl && pl.pos.z < 85 && BI.Engine.paused === false) {
        this.insectT -= BI.Engine.dt;
        if (this.insectT <= 0) {
          this.insectT = 0.15 + Math.random() * 0.9;
          const pos = p.clone().add(new THREE.Vector3((Math.random() - 0.5) * 30, 1, (Math.random() - 0.5) * 30));
          const fr = 3800 + Math.random() * 1600;
          for (let k = 0; k < 3; k++) this.tone({ freq: fr, dur: 0.035, gain: 0.025, delay: k * 0.07, pos, ref: 6 });
        }
      }
    },

    // ---------------------------------------------------------------------------
    // Event-driven one-shots
    // ---------------------------------------------------------------------------
    footstep(surface, loud) {
      const g = loud;
      switch (surface) {
        case 'sand':
          this.noise({ type: 'lowpass', freq: 900, dur: 0.16, gain: 0.12 * g, attack: 0.02, brown: false });
          break;
        case 'grass':
        case 'foliage':
          this.noise({ type: 'highpass', freq: 2500, dur: 0.09, gain: 0.07 * g, attack: 0.01 });
          this.noise({ type: 'bandpass', freq: 700, q: 0.8, dur: 0.07, gain: 0.06 * g });
          break;
        case 'dirt':
        case 'gravel':
        case 'rock':
          this.noise({ type: 'bandpass', freq: 1600, q: 0.6, dur: 0.09, gain: 0.12 * g });
          this.tone({ freq: 90, freqEnd: 60, dur: 0.06, gain: 0.08 * g });
          break;
        case 'metal':
          this.noise({ type: 'bandpass', freq: 3000, q: 1.5, dur: 0.05, gain: 0.1 * g });
          this.tone({ freq: 880 + Math.random() * 80, dur: 0.22, gain: 0.035 * g });
          this.tone({ freq: 1370, dur: 0.15, gain: 0.02 * g });
          this.tone({ freq: 110, freqEnd: 70, dur: 0.07, gain: 0.08 * g });
          break;
        case 'wood':
          this.tone({ freq: 150, freqEnd: 90, dur: 0.09, gain: 0.14 * g });
          this.noise({ type: 'bandpass', freq: 1200, dur: 0.04, gain: 0.06 * g });
          break;
        default: // concrete
          this.noise({ type: 'bandpass', freq: 2200, q: 1.2, dur: 0.035, gain: 0.12 * g });
          this.tone({ freq: 95, freqEnd: 60, dur: 0.07, gain: 0.1 * g });
      }
    },

    gun(kind, pos) {
      const far = !!pos;
      if (kind === 'smg') {
        this.noise({ type: 'bandpass', freq: 1300, q: 0.7, dur: 0.09, gain: 0.5, pos });
        this.noise({ type: 'highpass', freq: 4000, dur: 0.03, gain: 0.25, pos });
        this.tone({ freq: 140, freqEnd: 55, dur: 0.1, gain: 0.45, pos });
      } else if (kind === 'sniper') {
        this.noise({ type: 'lowpass', freq: 4000, freqEnd: 250, dur: 0.55, gain: 0.8, pos });
        this.noise({ type: 'highpass', freq: 3000, dur: 0.04, gain: 0.5, pos });
        this.tone({ freq: 90, freqEnd: 35, dur: 0.5, gain: 0.7, pos });
        this.noise({ type: 'lowpass', freq: 600, dur: 1.2, gain: 0.12, delay: 0.25, pos }); // tail
      } else if (kind === 'knife') {
        this.noise({ type: 'bandpass', freq: 700, freqEnd: 3200, q: 2, dur: 0.22, gain: 0.18, attack: 0.05 });
      } else if (kind === 'patrol') {
        this.noise({ type: 'bandpass', freq: 950, q: 0.8, dur: 0.11, gain: 0.55, pos, ref: 4 });
        this.tone({ freq: 120, freqEnd: 50, dur: 0.12, gain: 0.4, pos, ref: 4 });
      } else if (kind === 'hunter') {
        this.noise({ type: 'lowpass', freq: 3000, freqEnd: 400, dur: 0.35, gain: 0.6, pos, ref: 5 });
        this.tone({ freq: 100, freqEnd: 40, dur: 0.3, gain: 0.45, pos, ref: 5 });
      } else if (kind === 'guard') {
        this.tone({ wave: 'square', freq: 260, freqEnd: 90, dur: 0.12, gain: 0.25, pos, ref: 4, filter: 'lowpass', ffreq: 1800 });
        this.noise({ type: 'bandpass', freq: 1800, q: 1.2, dur: 0.08, gain: 0.4, pos, ref: 4 });
      } else {
        this.noise({ type: 'bandpass', freq: 1500, dur: 0.07, gain: 0.35, pos, ref: 4 });
        this.tone({ freq: 160, freqEnd: 80, dur: 0.07, gain: 0.25, pos, ref: 4 });
      }
      void far;
    },

    subscribe() {
      const E = BI.Events;
      E.on('player:footstep', (e) => this.footstep(e.surface, e.sprint ? 1.4 : e.crouch ? 0.45 : 1));
      E.on('player:land', (e) => { this.footstep(e.surface, 1.6); this.tone({ freq: 70, freqEnd: 40, dur: 0.15, gain: 0.3 }); });
      E.on('player:jump', () => this.noise({ type: 'bandpass', freq: 500, dur: 0.12, gain: 0.05 }));
      E.on('weapon:fired', (e) => this.gun(e.weapon.sound));
      E.on('weapon:bolt', () => { this.noise({ type: 'bandpass', freq: 2500, q: 3, dur: 0.04, gain: 0.15 }); this.noise({ type: 'bandpass', freq: 1800, q: 3, dur: 0.05, gain: 0.15, delay: 0.22 }); });
      E.on('weapon:switching', () => this.noise({ type: 'bandpass', freq: 1400, q: 2, dur: 0.08, gain: 0.08 }));
      E.on('weapon:switched', () => this.noise({ type: 'bandpass', freq: 2600, q: 4, dur: 0.05, gain: 0.08 }));
      E.on('enemy:fired', (e) => this.gun(e.type, e.pos));
      E.on('hit:marker', (e) => {
        this.tone({ freq: e.head ? 2600 : 1900, dur: 0.05, gain: 0.08 });
        if (e.kill) this.tone({ freq: 520, freqEnd: 380, dur: 0.18, gain: 0.12, delay: 0.04, wave: 'triangle' });
      });
      E.on('enemy:damaged', (e) => {
        const p = e.agent.pos.clone().setY(e.agent.pos.y + 1.5);
        if (e.agent.type === 'guard') {
          this.noise({ type: 'bandpass', freq: 3500, q: 6, dur: 0.18, gain: 0.25, pos: p });
          this.tone({ freq: 640, dur: 0.25, gain: 0.08, pos: p, wave: 'triangle' });
        } else if (e.agent.alive) {
          this.tone({ wave: 'sawtooth', freq: 150 + Math.random() * 40, freqEnd: 110, dur: 0.18, gain: 0.18, pos: p, filter: 'bandpass', ffreq: 750, q: 2 });
        }
      });
      E.on('enemy:killed', (e) => {
        const p = e.agent.pos.clone().setY(e.agent.pos.y + 1.2);
        if (e.agent.type === 'guard') {
          this.tone({ wave: 'sawtooth', freq: 420, freqEnd: 40, dur: 1.4, gain: 0.25, pos: p, filter: 'lowpass', ffreq: 1200 });
          for (let i = 0; i < 6; i++) this.noise({ type: 'highpass', freq: 3000, dur: 0.04, gain: 0.15, delay: Math.random() * 0.8, pos: p });
        } else {
          this.tone({ wave: 'sawtooth', freq: 170, freqEnd: 70, dur: 0.55, gain: 0.2, pos: p, filter: 'bandpass', ffreq: 600, q: 1.5 });
          this.tone({ freq: 60, freqEnd: 40, dur: 0.3, gain: 0.25, pos: p, delay: 0.5 });
        }
      });
      E.on('player:damaged', () => {
        this.tone({ freq: 90, freqEnd: 50, dur: 0.2, gain: 0.35 });
        this.noise({ type: 'lowpass', freq: 600, dur: 0.15, gain: 0.2 });
      });
      E.on('player:died', () => this.tone({ freq: 220, freqEnd: 40, dur: 1.6, gain: 0.3, wave: 'triangle' }));
      E.on('bullet:whiz', (e) => this.noise({ type: 'highpass', freq: 2500, freqEnd: 900, dur: 0.12, gain: 0.12, pos: e.pos, ref: 2 }));
      E.on('pickup:health', () => [660, 880, 1320].forEach((f, i) => this.tone({ freq: f, dur: 0.18, gain: 0.12, delay: i * 0.07 })));
      E.on('door:opened', (e) => this.door(e.door, true));
      E.on('door:closed', (e) => this.door(e.door, false));
      E.on('door:locked', () => this.tone({ wave: 'square', freq: 110, dur: 0.3, gain: 0.08, filter: 'lowpass', ffreq: 600 }));
      E.on('fx:lightpop', (e) => {
        this.noise({ type: 'highpass', freq: 2000, dur: 0.06, gain: 0.3, pos: e.pos });
        this.tone({ freq: 1200, freqEnd: 300, dur: 0.12, gain: 0.08, pos: e.pos });
      });
      E.on('fx:spark', (e) => { for (let i = 0; i < 4; i++) this.noise({ type: 'highpass', freq: 4000, dur: 0.03, gain: 0.12, delay: i * 0.05 * Math.random(), pos: e.pos }); });
      E.on('fx:casing', (e) => this.tone({ freq: 3200 + Math.random() * 1500, dur: 0.06, gain: 0.03, pos: e.pos, ref: 1.5 }));
      E.on('fx:impact', (e) => { if (e.surface === 'metal') this.tone({ freq: 1800 + Math.random() * 600, dur: 0.1, gain: 0.05, pos: e.pos }); });
      E.on('crate:scrape', (e) => this.noise({ type: 'lowpass', freq: 380, dur: 0.5, gain: 0.2, attack: 0.05, pos: e.pos, brown: true }));
      E.on('interact:holdtick', () => this.tone({ freq: 1500, dur: 0.02, gain: 0.03 }));
      E.on('generator:disabled', () => {
        const t = this.ctx.currentTime;
        for (const o of this.humOsc) o.frequency.exponentialRampToValueAtTime(Math.max(8, o.frequency.value * 0.15), t + 3.5);
        this.humGain.gain.cancelScheduledValues(t);
        this.humLevel = 0;
        this.tone({ wave: 'sawtooth', freq: 300, freqEnd: 30, dur: 3.2, gain: 0.3, filter: 'lowpass', ffreq: 900 });
        this.noise({ type: 'lowpass', freq: 2000, freqEnd: 100, dur: 2.5, gain: 0.25 });
      });
      E.on('lights:emergency', () => { this.alarmOn = true; });
      E.on('alert:broadcast', () => {
        this.noise({ type: 'bandpass', freq: 1800, q: 2, dur: 0.35, gain: 0.15 }); // radio squelch
        [880, 660, 880, 660].forEach((f, i) => this.tone({ wave: 'square', freq: f, dur: 0.22, gain: 0.06, delay: 0.3 + i * 0.25, filter: 'lowpass', ffreq: 2000 }));
      });
      E.on('mission:complete', () => [523, 659, 784, 1046].forEach((f, i) => this.tone({ freq: f, dur: 0.6, gain: 0.12, delay: i * 0.15, wave: 'triangle' })));
      E.on('ui:click', () => this.tone({ freq: 1200, dur: 0.03, gain: 0.05 }));
      E.on('player:flashlight', () => this.noise({ type: 'bandpass', freq: 3000, q: 4, dur: 0.03, gain: 0.08 }));
    },

    door(door, open) {
      const pos = door.pos;
      if (door.kind === 'hinge') {
        this.tone({ freq: 260, freqEnd: open ? 340 : 200, dur: 0.6, gain: 0.07, pos, wave: 'sawtooth', filter: 'bandpass', ffreq: 900, q: 6 });
        this.tone({ freq: 90, dur: 0.12, gain: 0.2, pos, delay: 0.5 });
      } else {
        const dur = door.id === 'bunker' ? 2.2 : 1.0;
        this.noise({ type: 'lowpass', freq: 320, dur, gain: door.id === 'bunker' ? 0.45 : 0.25, attack: 0.15, pos, brown: true });
        this.tone({ freq: 70, freqEnd: 50, dur: 0.25, gain: 0.3, pos, delay: dur - 0.15 });
        this.noise({ type: 'bandpass', freq: 2200, q: 3, dur: 0.06, gain: 0.12, pos });
      }
    },
  };

  BI.Audio = Audio;
})();
