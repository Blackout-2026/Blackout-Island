/* =============================================================================
 * world/ocean.js — animated ocean (custom shader)
 * -----------------------------------------------------------------------------
 * Vertex: sum of four Gerstner waves (amplitude damped in shallow water using
 * the baked terrain height texture). Fragment: Schlick fresnel between deep
 * water colour and a sky reflection colour, moon glint (Blinn), shoreline
 * foam from water depth + scrolling noise, three.js fog. Fragments over dry
 * land are discarded (saves fill rate and keeps the ramp area clean).
 * Role: World Builder / Graphics.
 * ========================================================================== */
(function () {
  'use strict';

  const vert = [
    'uniform float time; uniform sampler2D tHeight; uniform vec4 hRect;',
    'varying vec3 vWPos; varying vec3 vNrm; varying float vDepth;',
    '#include <fog_pars_vertex>',
    'vec3 gerstner(vec2 p, vec2 dir, float steep, float wl, float speed, inout vec3 tan, inout vec3 bin){',
    '  float k = 6.28318 / wl; float c = sqrt(9.8 / k) * speed; vec2 d = normalize(dir);',
    '  float f = k * (dot(d, p) - c * time); float a = steep / k;',
    '  tan += vec3(-d.x*d.x*(steep*sin(f)), d.x*(steep*cos(f)), -d.x*d.y*(steep*sin(f)));',
    '  bin += vec3(-d.x*d.y*(steep*sin(f)), d.y*(steep*cos(f)), -d.y*d.y*(steep*sin(f)));',
    '  return vec3(d.x*(a*cos(f)), a*sin(f), d.y*(a*cos(f)));',
    '}',
    'void main(){',
    '  vec4 wp = modelMatrix * vec4(position, 1.0);',
    '  vec2 huv = (wp.xz - hRect.xy) / hRect.zw;',
    '  float th = texture2D(tHeight, huv).r * 32.0 - 8.0;',
    '  if (huv.x < 0.0 || huv.y < 0.0 || huv.x > 1.0 || huv.y > 1.0) th = -12.0;',
    '  float depth = -th; vDepth = depth;',
    '  float damp = clamp(depth / 5.0, 0.25, 1.0);',
    '  vec3 tan = vec3(1,0,0), bin = vec3(0,0,1); vec3 o = vec3(0.0);',
    '  o += gerstner(wp.xz, vec2(1.0, 0.6), 0.16, 24.0, 1.0, tan, bin);',
    '  o += gerstner(wp.xz, vec2(-0.4, 1.0), 0.12, 13.0, 1.0, tan, bin);',
    '  o += gerstner(wp.xz, vec2(0.3, -1.0), 0.10, 7.0, 1.0, tan, bin);',
    '  o += gerstner(wp.xz, vec2(-1.0, -0.2), 0.08, 3.6, 1.0, tan, bin);',
    '  wp.xyz += o * damp;',
    '  vNrm = normalize(cross(bin, tan));',
    '  vWPos = wp.xyz;',
    '  vec4 mvPosition = viewMatrix * wp;',
    '  gl_Position = projectionMatrix * mvPosition;',
    '  #include <fog_vertex>',
    '}',
  ].join('\n');

  const frag = [
    'uniform float time; uniform vec3 moonDir; uniform vec3 deepCol; uniform vec3 skyCol; uniform vec3 moonCol;',
    'uniform vec3 lanternPos; uniform vec3 lanternCol;',
    'varying vec3 vWPos; varying vec3 vNrm; varying float vDepth;',
    '#include <fog_pars_fragment>',
    'float hash2(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }',
    'float noise(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);',
    '  return mix(mix(hash2(i), hash2(i+vec2(1,0)), f.x), mix(hash2(i+vec2(0,1)), hash2(i+vec2(1,1)), f.x), f.y); }',
    'void main(){',
    '  if (vDepth < -0.6) discard;',
    '  vec3 V = normalize(cameraPosition - vWPos);',
    // ripple normal detail
    '  vec2 q = vWPos.xz * 0.9;',
    '  float n1 = noise(q + time * 0.6), n2 = noise(q * 2.3 - time * 0.8);',
    '  vec3 N = normalize(vNrm + vec3(n1 - 0.5, 0.0, n2 - 0.5) * 0.25);',
    '  float fres = 0.02 + 0.98 * pow(1.0 - max(dot(N, V), 0.0), 5.0);',
    '  vec3 col = mix(deepCol, skyCol, clamp(fres, 0.0, 1.0));',
    '  vec3 H = normalize(V + normalize(moonDir));',
    '  float spec = pow(max(dot(N, H), 0.0), 380.0) * 6.0 + pow(max(dot(N, H), 0.0), 40.0) * 0.12;',
    '  col += moonCol * spec;',
    // lantern reflection near the boat
    '  vec3 Lp = lanternPos - vWPos; float ld = length(Lp); vec3 Hl = normalize(V + Lp / ld);',
    '  col += lanternCol * (pow(max(dot(N, Hl), 0.0), 120.0) * 3.0 + 0.04) / (1.0 + ld * ld * 0.02);',
    // shoreline foam
    '  float wave = 0.5 + 0.5 * sin(vDepth * 7.0 - time * 1.4);',
    '  float foamBand = smoothstep(0.55, 0.0, vDepth) * (0.35 + 0.65 * wave);',
    '  float fn = noise(vWPos.xz * 2.2 + vec2(time * 0.3, 0.0)) * 0.6 + noise(vWPos.xz * 6.0 - time * 0.2) * 0.4;',
    '  float foam = clamp(foamBand * smoothstep(0.45, 0.8, fn + foamBand * 0.35), 0.0, 1.0);',
    '  col = mix(col, vec3(0.16, 0.19, 0.22), foam * 0.7);',
    '  float edge = smoothstep(-0.5, 0.15, vDepth);',
    '  float clarity = mix(0.55, 0.95, smoothstep(0.0, 2.5, vDepth));',
    '  gl_FragColor = vec4(col, edge * clarity);',
    '  #include <tonemapping_fragment>',
    '  #include <encodings_fragment>',
    '  #include <fog_fragment>',
    '}',
  ].join('\n');

  const Ocean = {
    mesh: null,
    build(scene) {
      const T = BI.Terrain;
      const uniforms = THREE.UniformsUtils.merge([
        THREE.UniformsLib.fog,
        {
          time: { value: 0 },
          tHeight: { value: null },
          hRect: { value: new THREE.Vector4(T.X0, T.Z0, T.SIZE, T.SIZE) },
          moonDir: { value: BI.Sky.moonDir },
          deepCol: { value: new THREE.Color(0x02070b) },
          skyCol: { value: new THREE.Color(0x1a2840) },
          moonCol: { value: new THREE.Color(0xd8e2ff) },
          lanternPos: { value: new THREE.Vector3(BI.Level.boat.x, 2.6, BI.Level.boat.z) },
          lanternCol: { value: new THREE.Color(0xffa040) },
        },
      ]);
      uniforms.tHeight.value = T.heightTexture();
      const mat = new THREE.ShaderMaterial({
        uniforms,
        vertexShader: vert,
        fragmentShader: frag,
        fog: true,
        transparent: true,
        depthWrite: true,
      });
      // Dense grid around the island, coarse ring beyond (fog hides the edge).
      const geo = new THREE.PlaneGeometry(520, 520, 150, 150);
      geo.rotateX(-Math.PI / 2);
      const mesh = new THREE.Mesh(geo, mat);
      mesh.position.set(0, BI.Level.WATER_Y, BI.Level.TERRAIN_CENTER_Z);
      mesh.frustumCulled = false;
      mesh.name = 'ocean';
      mesh.renderOrder = 1;
      scene.add(mesh);
      const far = new THREE.Mesh(
        new THREE.RingGeometry(250, 1200, 48, 1).rotateX(-Math.PI / 2),
        new THREE.MeshBasicMaterial({ color: 0x02050a, fog: true })
      );
      far.position.set(0, -0.3, BI.Level.TERRAIN_CENTER_Z);
      far.name = 'ocean-far';
      scene.add(far);
      this.mesh = mesh;
      this.far = far;
      BI.Engine.addUpdate((dt, t) => {
        mat.uniforms.time.value = t;
      }, 90);
      return mesh;
    },
  };

  BI.Ocean = Ocean;
})();
