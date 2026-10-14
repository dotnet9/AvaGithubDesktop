const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

// 验证真实菜单、主题事件和关于状态，不请求外部发布站点。
(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const url = pathToFileURL(path.join(__dirname, '..', 'index.html')).href;
    await page.goto(url);
    const palettes = await page.locator('.swatches button').evaluateAll(items => items.map(item => item.dataset.palette));
    assert.equal(palettes.length, 8);
    for (const palette of palettes) {
      await page.locator(`.swatches button[data-palette="${palette}"]`).click();
      for (const mode of ['light', 'dark']) {
        await page.locator('#theme-mode').selectOption(mode);
        assert.equal(await page.locator('html').getAttribute('data-theme'), mode);
        assert.equal(await page.locator('html').getAttribute('data-palette'), palette);
      }
    }
    await page.locator('#theme-mode').selectOption('system');
    for (const mode of ['dark', 'light']) {
      await page.emulateMedia({ colorScheme: mode });
      await page.waitForFunction(value => document.documentElement.dataset.theme === value, mode);
    }
    await page.reload();
    assert.equal(await page.locator('html').getAttribute('data-palette'), palettes.at(-1));
    await page.locator('#moreBtn').click();
    await page.locator('#aboutItem').click();
    assert.equal(await page.locator('#about-dialog').evaluate(dialog => dialog.open), true);
    await page.locator('#about-close').click();
    for (const scenario of ['new', 'latest', 'offline', 'noasset']) {
      await page.goto(url + '?update=' + scenario);
      await page.locator('#checkUpdateFoot').click();
      await page.locator('#update-action').click();
      await page.waitForFunction(() => !document.querySelector('#update-action').disabled);
      if (scenario === 'new') {
        assert.equal(await page.locator('#update-action').textContent(), '下载更新');
        await page.locator('#update-action').click();
        await page.waitForFunction(() => document.querySelector('#update-action').textContent === '安装更新');
        assert.match(await page.locator('#update-status').textContent(), /SHA256/);
      } else if (scenario === 'latest') assert.match(await page.locator('#update-status').textContent(), /最新/);
      else if (scenario === 'offline') assert.match(await page.locator('#update-status').textContent(), /失败/);
      else assert.equal(await page.locator('#update-action').textContent(), '打开发布页');
      await page.locator('#about-close').click();
    }
    assert.deepEqual(errors, []);
    console.log('PASS: sixteen palette combinations, system changes, persistence, real about menu and four update scenarios.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
