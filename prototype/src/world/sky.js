/* =============================================================================
 * world/sky.js — night sky dome: gradient, stars, moon, drifting clouds
 * -----------------------------------------------------------------------------
 * A single inverted sphere with a custom shader. Stars are hashed cells with
 * twinkle, the moon is an analytic disc + halo in the moon direction, and
 * clouds are scrolling fbm lit from the moon side. The horizon colour equals
 * the fog colour so terrain melts into the sky.
 * Role: World Builder / Graphics.
 * ========================================================================== */
(function () {
  'use strict';

  const vert = [
    'varying vec3 vDir;',
    'void main(){',
    '  vDir = normalize(position);',
    '  vec4 p = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
    '  gl_Position = p.xyww;', // keep at far plane
    '}',
  ].join('\n');

  const frag = [
    'uniform float time; uniform vec3 moonDir; uniform vec3 horizon; uniform vec3 zenith; uniform float cloudCover;',
    'varying vec3 vDir;',
    'float hash(vec3 p){ p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }',
    'float hash2(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }',
    'float noise(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);',
    '  return mix(mix(hash2(i), hash2(i+vec2(1,0)), f.x), mix(hash2(i+vec2(0,1)), hash2(i+vec2(1,1)), f.x), f.y); }',
    'float fbm(vec2 p){ float s = 0.0, a = 0.5; for(int i=0;i<5;i++){ s += a*noise(p); p *= 2.03; a *= 0.5; } return s; }',
    'void main(){',
    '  vec3 d = normalize(vDir);',
    '  float h = clamp(d.y, 0.0, 1.0);',
    '  vec3 col = mix(horizon, zenith, pow(h, 0.55));',
    // stars
    '  vec3 sp = d * 220.0; vec3 cell = floor(sp);',
    '  float s = hash(cell);',
    '  if (s > 0.9965) {',
    '    vec3 c = cell + 0.5 + (vec3(hash(cell+1.3), hash(cell+2.7), hash(cell+5.1)) - 0.5) * 0.4;',
    '    float dist = length(sp - c);',
    '    float tw = 0.65 + 0.35 * sin(time * (1.5 + s * 9.0) + s * 100.0);',
    '    float star = smoothstep(0.26, 0.0, dist) * tw * smoothstep(0.02, 0.25, h);',
    '    col += vec3(0.85, 0.9, 1.0) * star * min(1.0 + (s - 0.9965) * 500.0, 2.2);',
    '  }',
    // moon
    '  float md = dot(d, normalize(moonDir));',
    '  float disc = smoothstep(0.99955, 0.99975, md);',
    '  vec2 mp = vec2(dot(d, vec3(1,0,0)), d.y) * 900.0;',
    '  float craters = 0.82 + 0.18 * noise(mp * 0.05) ;',
    '  col = mix(col, vec3(1.0, 0.97, 0.9) * 3.2 * craters, disc);',
    '  col += vec3(0.45, 0.55, 0.75) * pow(max(md, 0.0), 220.0) * 0.6;',
    '  col += vec3(0.2, 0.26, 0.38) * pow(max(md, 0.0), 12.0) * 0.18;',
    // clouds
    '  if (d.y > 0.0) {',
    '    vec2 cp = d.xz / (d.y + 0.12) * 1.4 + vec2(time * 0.012, time * 0.004);',
    '    float c = fbm(cp);',
    '    float cl = smoothstep(0.52 - cloudCover * 0.2, 0.85, c) * smoothstep(0.0, 0.3, d.y);',
    '    vec3 cloudCol = mix(vec3(0.03, 0.045, 0.07), vec3(0.16, 0.19, 0.26), pow(max(md, 0.0), 6.0) + 0.15);',
    '    col = mix(col, cloudCol, cl * 0.88);',
    '  }',
    '  gl_FragColor = vec4(col, 1.0);',
    '  #include <tonemapping_fragment>',
    '  #include <encodings_fragment>',
    '}',
  ].join('\n');

  const Sky = {
    mesh: null,
    moonDir: new THREE.Vector3(-0.35, 0.42, -0.84).normalize(),
    build(scene) {
      const mat = new THREE.ShaderMaterial({
        uniforms: {
          time: { value: 0 },
          moonDir: { value: this.moonDir },
          horizon: { value: new THREE.Color(0x0e1626) },
          zenith: { value: new THREE.Color(0x02040a) },
          cloudCover: { value: 0.5 },
        },
        vertexShader: vert,
        fragmentShader: frag,
        side: THREE.BackSide,
        depthWrite: false,
        fog: false,
      });
      const mesh = new THREE.Mesh(new THREE.SphereGeometry(500, 32, 16), mat);
      mesh.frustumCulled = false;
      mesh.renderOrder = -10;
      mesh.name = 'sky';
      scene.add(mesh);
      this.mesh = mesh;
      BI.Engine.addUpdate((dt, t) => {
        mat.uniforms.time.value = t;
        mesh.position.copy(BI.Engine.camera.position);
      }, 90);
      return mesh;
    },
    setHorizon(color) {
      this.mesh.material.uniforms.horizon.value.copy(color);
    },
  };

  BI.Sky = Sky;
})();
