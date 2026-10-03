// Measures first-load time (fresh browser profile = no shader cache) per quality.
const { open } = require('./lib');
(async () => {
  for (const q of process.argv.slice(2).length ? process.argv.slice(2) : ['low', 'medium']) {
    const { browser, page, errors } = await open({ query: '?quality=' + q });
    const s = await page.evaluate(() => __debug.steps());
    const progs = await page.evaluate(() => BI.Engine.renderer.info.programs.length);
    console.log(q, 'total', s.total, 'ms; programs', progs, '; compile', (s.steps.find((x) => x.text === 'Compiling shaders') || {}).ms, 'ms; errors', errors.length);
    console.log('   ', s.steps.map((x) => x.text.split(' ')[0] + '=' + x.ms).join(' '));
    await browser.close();
  }
})();
