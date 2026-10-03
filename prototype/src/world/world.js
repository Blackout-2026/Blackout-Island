/* =============================================================================
 * world/world.js — builds the world in order and culls it by zone
 * -----------------------------------------------------------------------------
 * Zone culling (cheap portal-style visibility):
 *   outside   island, sky, ocean, compound, entrance        (bunker hidden)
 *   entrance  everything (the ramp sees both worlds)
 *   bunker    entrance + bunker rooms                        (island hidden)
 * Hiding the island while underground removes the forest, terrain and ocean
 * from both the main pass and the shadow pass.
 * Role: World Builder.
 * ========================================================================== */
(function () {
  'use strict';

  const World = {
    exterior: [],
    interior: [],
    lastZone: null,

    build(scene) {
      BI.FX.init(scene);
      BI.Forest.build(scene);
      BI.Beach.build(scene);
      BI.BunkerExterior.build(scene);
      BI.BunkerInterior.build(scene);
      this.exterior = [BI.Terrain.mesh, BI.Ocean.mesh, BI.Ocean.far, BI.Sky.mesh, BI.BunkerExterior.group, scene.getObjectByName('beach')];
      for (const k of Object.keys(BI.Forest.meshes)) this.exterior.push(BI.Forest.meshes[k]);
      // The instanced light fixtures are never hidden: on Chrome's Direct3D 11
      // backend, re-showing these per-instance-coloured InstancedMeshes stalled
      // the GPU process for 1-3 s (measured; OpenGL/Vulkan backends do not).
      // They cost 5 draw calls and are depth-occluded from outside.
      this.interior = Object.keys(BI.BunkerInterior.groups).filter((k) => k !== 'fixtures').map((k) => BI.BunkerInterior.groups[k]);
      BI.Engine.addUpdate(() => this.update(), 5);
      BI.Engine.preRender.push((r) => BI.BunkerInterior.renderFeed(r));
    },

    setAllVisible(v) {
      for (const o of this.exterior) if (o) o.visible = v;
      for (const o of this.interior) if (o) o.visible = v;
      this.lastZone = null;
    },

    update() {
      const p = BI.player;
      if (!p) return;
      const zone = p.zone;
      if (zone === this.lastZone) return;
      this.lastZone = zone;
      const showExt = zone !== 'bunker';
      const showInt = zone !== 'outside';
      for (const o of this.exterior) if (o) o.visible = showExt;
      for (const o of this.interior) if (o) o.visible = showInt;
      BI.Events.emit('zone:changed', { zone });
    },
  };

  BI.World = World;
})();
