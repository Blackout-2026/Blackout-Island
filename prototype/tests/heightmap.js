// Dev tool: renders the analytic terrain to a PNG so the island shape can be inspected.
const fs = require('fs'), zlib = require('zlib'), vm = require('vm'), path = require('path');
const ctx = { window: {}, console, Math, Float32Array, Uint8Array, Map, Set };
ctx.window = ctx; ctx.THREE = { Vector3: class { set(x,y,z){this.x=x;this.y=y;this.z=z;return this;} normalize(){return this;} } };
vm.createContext(ctx);
for (const f of ['src/core/namespace.js', 'src/world/level.js', 'src/world/terrain.js'])
  vm.runInContext(fs.readFileSync(path.join(__dirname, '..', f), 'utf8'), ctx);
const T = ctx.BI.Terrain; T.bake();
const W = 400, H = 400, img = Buffer.alloc((W * 3 + 1) * H);
for (let y = 0; y < H; y++) { img[y * (W * 3 + 1)] = 0; for (let x = 0; x < W; x++) {
  const wx = -200 + x, wz = -220 + y; const h = T.height(wx, wz);
  let r, g, b; if (h < 0) { const k = Math.max(0, 1 + h / 7); r = 10; g = 40 * k + 20; b = 90 * k + 40; }
  else { const k = Math.min(1, h / 22); r = 60 + 160 * k; g = 110 + 110 * k; b = 50 + 150 * k; if (h < 2.3 && wz > 60) { r = 200; g = 180; b = 130; } }
  if (T.pathDistance(wx, wz) < 1.7) { r = 150; g = 90; b = 40; }
  if (Math.abs(h - 10) < 0.05 && wz < -25 && wz > -63) { r = 120; g = 120; b = 120; }
  const i = y * (W * 3 + 1) + 1 + x * 3; img[i] = r; img[i + 1] = g; img[i + 2] = b; } }
const crc = (b) => { let c, t = []; for (let n = 0; n < 256; n++) { c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } let x = 0xffffffff; for (const v of b) x = t[(x ^ v) & 255] ^ (x >>> 8); return (x ^ 0xffffffff) >>> 0; };
const chunk = (type, data) => { const l = Buffer.alloc(4); l.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4); ihdr[8] = 8; ihdr[9] = 2;
fs.writeFileSync(path.join(__dirname, 'out', 'heightmap.png'), Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(img)), chunk('IEND', Buffer.alloc(0))]));
const col = (x) => [120,110,100,96,90,80,60,40,20,0,-20,-30,-40,-48,-55,-61,-63,-70,-90,-110,-130,-142,-150,-160].map(z => z + ':' + T.height(x, z).toFixed(1)).join(' ');
console.log('x=0  ', col(0)); console.log('x=-15', col(-15)); console.log('x=40 ', col(40));
console.log('path pts', T.path.length);
