/* =============================================================================
 * core/input.js — keyboard, mouse, wheel and pointer lock
 * -----------------------------------------------------------------------------
 * Polled input: systems read `BI.Input.down('KeyW')`, `pressed('KeyE')`
 * (true only on the frame the key went down), mouse deltas and wheel steps.
 * `endFrame()` is called once per frame by the main loop to clear the
 * per-frame state. Uses KeyboardEvent.code so it works on any layout.
 * Role: Core Developer.
 * ========================================================================== */
(function () {
  'use strict';

  const Input = {
    keys: new Set(),
    pressedSet: new Set(),
    releasedSet: new Set(),
    mouseDX: 0,
    mouseDY: 0,
    wheel: 0,
    buttons: [false, false, false],
    buttonsPressed: [false, false, false],
    locked: false,
    enabled: true,
    // When true (debug / automated tests) gameplay runs without pointer lock.
    allowUnlocked: false,
    canvas: null,

    init(canvas) {
      this.canvas = canvas;
      const block = new Set(['Space', 'Tab', 'F1', 'ArrowUp', 'ArrowDown', 'Backquote']);

      window.addEventListener('keydown', (e) => {
        if (block.has(e.code)) e.preventDefault();
        if (!this.keys.has(e.code)) this.pressedSet.add(e.code);
        this.keys.add(e.code);
      });
      window.addEventListener('keyup', (e) => {
        this.keys.delete(e.code);
        this.releasedSet.add(e.code);
      });
      // Chrome opens its help page on F1 unless the "help" event is cancelled.
      window.addEventListener('help', (e) => e.preventDefault());
      window.addEventListener('blur', () => {
        this.keys.clear();
        this.buttons = [false, false, false];
      });

      document.addEventListener('mousemove', (e) => {
        if (!this.locked && !this.allowUnlocked) return;
        // Ignore the occasional huge spike some browsers emit on lock.
        if (Math.abs(e.movementX) > 400 || Math.abs(e.movementY) > 400) return;
        this.mouseDX += e.movementX || 0;
        this.mouseDY += e.movementY || 0;
      });
      document.addEventListener('mousedown', (e) => {
        if (e.button < 3) {
          this.buttons[e.button] = true;
          this.buttonsPressed[e.button] = true;
        }
      });
      document.addEventListener('mouseup', (e) => {
        if (e.button < 3) this.buttons[e.button] = false;
      });
      document.addEventListener('contextmenu', (e) => e.preventDefault());
      window.addEventListener(
        'wheel',
        (e) => {
          this.wheel += Math.sign(e.deltaY);
        },
        { passive: true }
      );

      document.addEventListener('pointerlockchange', () => {
        this.locked = document.pointerLockElement === this.canvas;
        BI.Events.emit('input:lock', { locked: this.locked });
      });
      document.addEventListener('pointerlockerror', () => {
        BI.Events.emit('input:lockerror', {});
      });
    },

    requestLock() {
      if (!this.canvas || this.locked) return;
      try {
        const p = this.canvas.requestPointerLock();
        if (p && p.catch) p.catch(() => {});
      } catch (err) {
        /* lock can fail without a user gesture; the menu stays up */
      }
    },
    exitLock() {
      if (document.pointerLockElement) document.exitPointerLock();
    },

    active() {
      return this.enabled && (this.locked || this.allowUnlocked);
    },
    down(code) {
      return this.active() && this.keys.has(code);
    },
    pressed(code) {
      return this.active() && this.pressedSet.has(code);
    },
    // Raw pressed state even when gameplay input is inactive (menus, debug keys).
    pressedRaw(code) {
      return this.pressedSet.has(code);
    },
    mouseDown(b) {
      return this.active() && this.buttons[b];
    },
    mousePressed(b) {
      return this.active() && this.buttonsPressed[b];
    },

    // Synthetic input used by the automated smoke test.
    simulateKey(code, isDown) {
      if (isDown) {
        if (!this.keys.has(code)) this.pressedSet.add(code);
        this.keys.add(code);
      } else {
        this.keys.delete(code);
        this.releasedSet.add(code);
      }
    },

    endFrame() {
      this.pressedSet.clear();
      this.releasedSet.clear();
      this.mouseDX = 0;
      this.mouseDY = 0;
      this.wheel = 0;
      this.buttonsPressed[0] = this.buttonsPressed[1] = this.buttonsPressed[2] = false;
    },
  };

  BI.Input = Input;
})();
