/* =============================================================================
 * world/level.js — single source of truth for the level layout
 * -----------------------------------------------------------------------------
 * All coordinates are metres. +Y is up, north is -Z (yaw 0 looks north).
 * The bunker floor (y = 2.5) is above sea level so the ocean never intersects
 * the underground rooms. World, physics, nav and AI all read from here.
 * Role: World Builder.
 * ========================================================================== */
(function () {
  'use strict';

  const L = {
    WATER_Y: 0,
    GROUND_Y: 10, // compound plateau
    BUNKER_Y: 2.5, // underground floor
    ROOM_H: 3.6,
    TERRAIN_SIZE: 400, // terrain mesh covers [-200,200]
    TERRAIN_CENTER_Z: -20,

    // Winding forest path from the beach to the compound gate (x, z).
    pathCtrl: [
      [1, 104], [0, 96], [-6, 85], [4, 72], [12, 58], [4, 44], [-9, 32],
      [-10, 16], [2, 3], [9, -10], [3, -22], [0, -30],
    ],

    compound: { minX: -22, maxX: 22, minZ: -62, maxZ: -28 },
    gateHalfWidth: 3.2,
    // Surface entrance building (dug into the hill). Door on its south face.
    entrance: { minX: -6, maxX: 6, minZ: -62, maxZ: -48, doorZ: -48, doorHalf: 1.5, roofY: 14 },
    // Ramp down to the bunker, descending northwards.
    ramp: { minX: -2, maxX: 2, zTop: -50, zBottom: -70, yTop: 10, yBottom: 2.5 },

    rooms: {
      security: { minX: -8, maxX: 8, minZ: -82, maxZ: -70, h: 3.6 },
      corridor: { minX: -2, maxX: 2, minZ: -123.6, maxZ: -82.4, h: 3.4 },
      generator: { minX: -20, maxX: -2.4, minZ: -116, maxZ: -96, h: 6.5 },
      lab: { minX: -10, maxX: 10, minZ: -142, maxZ: -124, h: 4.0 },
    },

    // Doors: hinge/slide data is interpreted by physics/doors.js
    doors: {
      bunker: { x: 0, z: -48, axis: 'x', width: 3.0, height: 3.4, kind: 'slide', y: 10, locked: false, label: 'bunker door' },
      security: { x: 0, z: -82.2, axis: 'x', width: 2.4, height: 2.8, kind: 'slide', y: 2.5, locked: false, label: 'security door', autoOpen: false },
      generator: { x: -2.2, z: -106, axis: 'z', width: 2.4, height: 2.8, kind: 'hinge', y: 2.5, locked: false, label: 'generator room door' },
      lab: { x: 0, z: -123.8, axis: 'x', width: 2.6, height: 2.8, kind: 'slide', y: 2.5, locked: true, label: 'laboratory door', maglock: true },
    },

    dock: { x: 0, zStart: 97, zEnd: 128, halfWidth: 1.4, y: 1.3 },
    boat: { x: 3.7, z: 122.5 },
    boatZone: { x: 0, z: 124, r: 6 },

    generator: { x: -11, z: -106, panel: { x: -6.9, z: -106 } },
    cure: { x: 0, z: -135 },

    // Respawn checkpoints, one per mission phase (yaw 0 = facing north).
    checkpoints: {
      beach: { x: 0, z: 121, yaw: 0 },
      forest: { x: 0, z: 90, yaw: 0 },
      entrance: { x: 0, z: -33, yaw: 0 },
      generator: { x: -4.5, z: -106, yaw: Math.PI / 2 },
      // Escape: checkpoints follow the route backwards (see README decisions)
      escapeEntrance: { x: 0, z: -40, yaw: Math.PI },
      escapeForest: { x: 0, z: -24, yaw: Math.PI },
    },

    healthPickups: [
      { x: 6.5, z: -80.5 },
      { x: 1.2, z: -119 },
      { x: -18.5, z: -114.5 },
    ],

    crate: { x: 0.95, z: -95, size: 1.5 },

    // Agent spawns and routes ------------------------------------------------
    agents: {
      hunters: [
        { x: -24, z: 54, prowl: [[-24, 54], [-30, 30], [-18, 12], [-26, -8], [-14, 70], [-36, 44]] },
        { x: 26, z: 20, prowl: [[26, 20], [30, 44], [20, 64], [32, 2], [18, -14], [36, 30]] },
      ],
      patrols: [
        { x: -14, z: -36, route: [[-14, -36], [14, -36], [16, -46], [10, -55], [-10, -55], [-16, -46]] },
        { x: 3, z: -74, route: [[3, -74], [-4, -74], [-4, -79.5], [3, -79.5]] },
        { x: 0, z: -88, route: [[0, -88], [0, -102], [-0.5, -120], [0, -102]] },
      ],
      guards: [
        { x: -8, z: -100, guard: [-8, -100], name: 'TG-01' },
        { x: 3, z: -129, guard: [2.5, -129], name: 'TG-02' },
      ],
      commander: { x: 6, z: -138, post: [6, -138] },
    },

    // Cover points used by the Tactical Guard's Utility AI.
    coverPoints: [
      [-14.8, -101.2], [-7.2, -101.2], [-14.8, -110.8], [-7.2, -110.8], // generator corners
      [-17.5, -99], [-4.5, -113.5], [-18, -110], [-11, -109.4], [-15.5, -108.6], [-16.8, -106], [-9, -97.5], [-13, -97.5],
      [-6.5, -130.5], [6.5, -130.5], [-6.5, -137], [6.5, -137], // lab benches
      [-1.2, -92.5], [1.2, -110], [-1.2, -117], // corridor pillars
    ],

    // Zones used for lighting blend, fog, footsteps and visibility culling.
    zoneOf(x, y, z) {
      const e = L.entrance;
      if (z < -69.5 && y < 9) return 'bunker';
      if (x > e.minX - 0.5 && x < e.maxX + 0.5 && z < e.maxZ && z > -70.5) return 'entrance';
      return 'outside';
    },
    // 0 outside -> 1 fully underground (smooth along the ramp).
    interiorFactor(x, y, z) {
      const zone = L.zoneOf(x, y, z);
      if (zone === 'bunker') return 1;
      if (zone === 'entrance') return BI.U.smoothstep(-48.5, -62, z);
      return 0;
    },
  };

  BI.Level = L;
})();
