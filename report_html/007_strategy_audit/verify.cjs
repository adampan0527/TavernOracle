const { chromium } = require('playwright');
const { pathToFileURL } = require('url');
const path = require('path');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1080 } });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  await page.goto(pathToFileURL(path.join(__dirname, 'index.html')).href);
  if (await page.locator('.katex').count() !== 1) throw Error('Missing rendered formula');
  if ((await page.locator('body').innerText()).includes('$')) throw Error('Uncompiled formula');
  for (const scene of ['gold', 'position', 'magnetic', 'hold', 'spell']) {
    await page.selectOption('#scene', scene);
    await page.click('#fixed');
    const fixed = await page.locator('#advice').innerText();
    await page.click('#old');
    if (fixed === await page.locator('#advice').innerText()) throw Error('Mode did not change: ' + scene);
  }
  await page.selectOption('#scene', 'gold');
  await page.click('#fixed');
  await page.locator('#gold').fill('0');
  if (!(await page.locator('#advice').innerText()).includes('金币已用完')) throw Error('Gold input failed');
  await page.selectOption('#scene', 'magnetic');
  await page.screenshot({ path: path.join(__dirname, 'verification.png'), fullPage: true });
  if (errors.length) throw Error(errors.join('\n'));
  console.log('PASS: offline formula, five scenarios, mode switches, gold input, zero script/console errors');
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
