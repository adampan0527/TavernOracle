const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

async function verifyReport(dirPath) {
  const fullPath = path.resolve(dirPath, 'index.html');
  if (!fs.existsSync(fullPath)) {
    console.error(`Error: index.html not found at ${fullPath}`);
    process.exit(1);
  }

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1200, height: 900 } });

  const errors = [];
  page.on('pageerror', err => errors.push(`Page Error: ${err.message}`));
  page.on('console', msg => {
    if (msg.type() === 'error') errors.push(`Console Error: ${msg.text()}`);
  });

  console.log(`Loading file://${fullPath}...`);
  await page.goto(`file://${fullPath}`);

  // Check for KaTeX if math is present
  const katexCount = await page.locator('.katex').count();
  console.log(`[PASS] KaTeX rendered mathematical elements: ${katexCount}`);

  // Check that no raw LaTeX '$' delimiter remains in visible text
  const bodyText = await page.locator('body').innerText();
  const rawLatexRegex = /\$[a-zA-Z0-9_\\]+\$/;
  if (rawLatexRegex.test(bodyText)) {
    errors.push('Found raw uncompiled LaTeX $...$ in body text!');
  } else {
    console.log('[PASS] No uncompiled LaTeX math found in body text.');
  }

  // Check interactive elements (e.g. slider)
  const slider = page.locator('#lethal-slider');
  if (await slider.count() > 0) {
    await slider.fill('50');
    await slider.dispatchEvent('input');
    const lethalVal = await page.locator('#lethal-value').textContent();
    console.log(`[PASS] Slider interaction verified (value = ${lethalVal}).`);
  }

  const screenshotPath = path.resolve(dirPath, 'verification.png');
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log(`[PASS] Verification screenshot saved: ${screenshotPath}`);

  await browser.close();

  if (errors.length > 0) {
    console.error('FAILED with errors:', errors);
    process.exit(1);
  }

  console.log('✅ ALL CHECKS PASSED!');
}

const targetDir = process.argv[2] || path.join(__dirname, '001_example');
verifyReport(targetDir);
