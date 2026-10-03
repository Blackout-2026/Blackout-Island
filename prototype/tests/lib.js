// Shared Playwright helpers for the prototype tests.
// Opens index.html over file:// (no server), on the real GPU via ANGLE/D3D11,
// and records every console error, page error and failed network request.
const { chromium } = require('playwright');
const path = require('path');
const { pathToFileURL } = require('url');

const ROOT = path.join(__dirname, '..');
const URL = pathToFileURL(path.join(ROOT, 'index.html')).href;

async function open(opts) {
  opts = opts || {};
  const browser = await chromium.launch({
    headless: opts.headless !== false,
    args: ['--use-angle=' + (process.env.BI_ANGLE || 'd3d11'), '--enable-gpu', '--ignore-gpu-blocklist', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required'],
  });
  const page = await browser.newPage({ viewport: { width: opts.width || 1280, height: opts.height || 720 } });
  const errors = [];
  const failed = [];
  const logs = [];
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(m.text());
    else if (m.type() === 'warning') logs.push('[warn] ' + m.text());
  });
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('requestfailed', (r) => failed.push(r.url() + ' ' + (r.failure() && r.failure().errorText)));
  page.on('response', (r) => {
    if (r.status() >= 400) failed.push(r.url() + ' HTTP ' + r.status());
  });
  const t0 = Date.now();
  await page.goto(URL + (opts.query || ''));
  try {
    await page.waitForFunction(() => (window.__debug && window.__debug.ready) || window.__loadError, null, { timeout: opts.loadTimeout || 120000 });
  } catch (e) {
    console.log('LOAD TIMEOUT. errors so far:', errors);
    console.log('loading text:', await page.evaluate(() => (document.getElementById('loadtext') || {}).textContent));
    await browser.close();
    process.exit(1);
  }
  const le = await page.evaluate(() => window.__loadError);
  if (le) {
    console.log('LOAD ERROR:', le, errors);
    await browser.close();
    process.exit(1);
  }
  const loadMs = Date.now() - t0;
  return { browser, page, errors, failed, logs, loadMs };
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

module.exports = { open, sleep, ROOT, URL };
