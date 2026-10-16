const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const standaloneFixture = require('./standalone-fixture.cjs');

// 在交付的单文件上实际悬停、按下主操作，避免只核对颜色变量。
(async () => {
  const output = process.env.AVA_AUDIT_OUTPUT || 'E:/github/temp/ava-action-palette';
  fs.mkdirSync(output, { recursive: true });
  const fixture = standaloneFixture(path.resolve(__dirname, '../index.html'));
  const browser = await chromium.launch({ headless: true });
  const rows = [];
  const errors = [];
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' });
    page.on('pageerror', error => errors.push(error.message));
    await fixture.attach(page);
    await page.goto(fixture.url);
    await page.locator('#summary').fill('Palette verification');
    const palettes = await page.locator('.swatches button').evaluateAll(items => items.map(item => item.dataset.palette));
    for (const culture of ['zh-CN', 'en-US']) {
      await page.locator('#language-button').click();
      await page.locator(`#popup [data-command="${culture === 'zh-CN' ? 'SimplifiedChinese' : 'English'}"]`).click();
      for (const palette of palettes) {
        await page.locator(`.swatches button[data-palette="${palette}"]`).click();
        for (const mode of ['light', 'dark']) {
          while (await page.locator('html').getAttribute('data-theme-mode') !== mode) await page.locator('#theme-mode').click();
          const button = page.locator('#commit');
          await page.mouse.move(1, 1);
          await check('normal');
          await button.hover();
          await check('hover');
          await page.mouse.down();
          await check('pressed');
          await page.mouse.move(1, 1);
          await page.mouse.up();
          await page.locator('#summary').fill('');
          assert.equal(await button.isDisabled(), true);
          await page.locator('#summary').fill('Palette verification');
          await check('reenabled');
          async function check(state) {
            const reading = await button.evaluate(element => {
              const style = getComputedStyle(element);
              const rgb = value => value.match(/[\d.]+/g).slice(0, 3).map(Number);
              const luminance = value => rgb(value).map(channel => channel / 255)
                .map(channel => channel <= .04045 ? channel / 12.92 : ((channel + .055) / 1.055) ** 2.4)
                .reduce((sum, channel, index) => sum + channel * [.2126, .7152, .0722][index], 0);
              const first = luminance(style.color), second = luminance(style.backgroundColor);
              return { foreground: style.color, background: style.backgroundColor, ratio: (Math.max(first, second) + .05) / (Math.min(first, second) + .05) };
            });
            rows.push({ culture, palette, mode, state, ...reading });
            assert.ok(reading.ratio >= 4.5, `${culture}/${palette}/${mode}/${state}: ${reading.ratio}`);
          }
        }
      }
    }
    await fixture.verify(page);
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(output, 'action-palette.json'), JSON.stringify(rows, null, 2));
    console.log(`PASS: ${rows.length} actual action state comparisons, zero script errors.`);
  } finally { await browser.close(); fixture.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
