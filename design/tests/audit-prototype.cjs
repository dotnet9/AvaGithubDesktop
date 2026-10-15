const standaloneFixture = require('./standalone-fixture.cjs');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { chromium } = require('playwright');

// 验证交付 HTML 的实际菜单、布局与样例交互，不请求外部服务。
(async () => {
  execFileSync(process.execPath,[path.resolve(__dirname,'../../scripts/prototype/build-resources.cjs'),'--check']);
  const standalone = standaloneFixture(path.resolve(__dirname, '../index.html'));
  const output=path.resolve(process.env.AVA_AUDIT_OUTPUT||path.join(__dirname,'../../artifacts/prototype-audit'));
  fs.mkdirSync(output,{recursive:true});
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion:'reduce' });
    await standalone.attach(page);
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(standalone.url);
    assert.ok(await page.locator('.win').evaluate(el=>el.getBoundingClientRect().x===18&&parseFloat(getComputedStyle(el).borderTopWidth)>0));
    assert.equal(await page.locator('.captions button').count(),3);
    assert.deepEqual(await page.locator('.captions > button').evaluateAll(items=>items.map(item=>item.dataset.action)),['minimize','maximize','quit']);
    await page.locator('.captions [data-action="maximize"]').click();assert.ok(await page.locator('.win').evaluate(el=>el.classList.contains('maximized')&&el.getBoundingClientRect().x===0));
    await page.locator('.captions [data-action="maximize"]').click();assert.ok(await page.locator('.win').evaluate(el=>!el.classList.contains('maximized')&&el.getBoundingClientRect().x===18));
    for(const action of ['minimize','quit']){await page.locator('.captions [data-action="'+action+'"]').click();assert.equal(await page.locator('.win').isVisible(),false);assert.equal(await page.locator('#relaunch button').isVisible(),true);await page.locator('#relaunch button').click();assert.equal(await page.locator('.win').isVisible(),true);}
    await page.goto(standalone.url+'?capture=window');
    assert.equal(await page.locator('#main-menu>button').count(),6);
    async function menu(key,command){await page.locator(`[data-menu="${key}"]`).click();await page.locator(`#popup [data-command="${command}"]`).click();}
    async function capture(name){await page.screenshot({path:path.join(output,name+'.png')});const geometry=await page.evaluate(()=>AvaPrototype.geometry());fs.writeFileSync(path.join(output,name+'.geometry.json'),JSON.stringify(geometry,null,2));}
    const palettes = await page.locator('.swatches button').evaluateAll(items => items.map(item => item.dataset.palette));
    assert.equal(palettes.length, 8);
    assert.deepEqual(palettes,['dotnet','aurora','ember','default','aquatic','desert','dusk','night-sky']);
    const nativeSwatches=await page.locator('.swatches button').evaluateAll(items=>items.map(item=>getComputedStyle(item,':before').background));
    assert.match(nativeSwatches[1],/linear-gradient.*137, 104, 255.*38, 212, 204/);
    for(const culture of ['zh-CN','en-US']){
      await page.locator('#language-button').click();assert.equal(await page.locator('#popup').isVisible(),true);await page.locator(`#popup [data-command="${culture==='zh-CN'?'SimplifiedChinese':'English'}"]`).click();
      assert.equal(await page.locator('html').getAttribute('lang'),culture);
      for (const palette of palettes) {
        await page.locator(`.swatches button[data-palette="${palette}"]`).click();
        for (const mode of ['light', 'dark']) {
          while(await page.locator('html').getAttribute('data-theme-mode')!==mode)await page.locator('#theme-mode').click();
          assert.equal(await page.locator('html').getAttribute('data-theme'), mode);
          assert.equal(await page.locator('html').getAttribute('data-palette'), palette);
          assert.deepEqual(await page.locator('.swatches button').evaluateAll(items=>items.map(item=>getComputedStyle(item,':before').background)),nativeSwatches);
        }
      }
      for(const width of [1440,1120]){
        await page.setViewportSize({width,height:900});
        const geometry=await page.evaluate(()=>AvaPrototype.geometry());
        assert.equal(geometry.toolbar.height,50);assert.equal(geometry.left.width,360);assert.equal(geometry.log.height,180);
        assert.equal(geometry.diff.x,386);assert.equal(geometry.diff.y,123);assert.equal(geometry.diff.width,width-405);
        assert.ok(await page.locator('.captions').evaluate(el=>el.getBoundingClientRect().right<=innerWidth));
        assert.equal(await page.locator('.top select').count(),0);
        await capture(`workspace-${culture}-${width}`);
      }
      await page.setViewportSize({width:960,height:620});
      assert.ok(await page.locator('#stash, #commit').evaluateAll(buttons=>buttons.length===2&&buttons.every(button=>{
        const bounds=button.getBoundingClientRect(), host=button.closest('.workspace').getBoundingClientRect();
        return bounds.width>0&&bounds.height>0&&bounds.top>=host.top&&bounds.bottom<=host.bottom+1;
      })), 'Commit and stash actions stay inside the minimum workspace');
      await menu('MenuBranch','MenuNewBranch');
      await page.locator('#action-value').fill('feature/'+culture+'-'+'f'.repeat(120));
      await page.locator('#action-confirm').click();
      assert.ok(await page.locator('#commit').evaluate(button=>{
        const bounds=button.getBoundingClientRect(),host=button.closest('#commit-form').getBoundingClientRect();
        return getComputedStyle(button).textOverflow==='ellipsis'&&button.scrollWidth>button.clientWidth
          &&button.title===button.textContent&&bounds.left>=host.left&&bounds.right<=host.right;
      }), 'A long branch is constrained with an ellipsis and complete action tooltip');
      await capture('workspace-long-branch-'+culture);
      await page.locator('#branch').click();
      await page.locator('#popup').getByRole('button',{name:'main',exact:true}).click();
      assert.ok(await page.locator('.file-area').evaluate(element=>element.clientHeight>=100), 'Minimum workspace retains file navigation');
      await capture(`workspace-minimum-${culture}`);
      await page.setViewportSize({width:1440,height:900});
      await menu('MenuHelp','MenuAbout');assert.ok(await page.locator('#about-dialog').evaluate(dialog=>dialog.open));
      assert.equal(await page.locator('#about-info dt').count(),8);assert.match(await page.locator('#about-info').innerText(),/2026-10-15 00:00:00/);assert.equal(await page.locator('.about-captions button').count(),3);
      await page.locator('#about-dialog').screenshot({path:path.join(output,`about-${culture}.png`)});
      await page.locator('#about-close').click();
    }
    while(await page.locator('html').getAttribute('data-theme-mode')!=='system')await page.locator('#theme-mode').click();
    for(const mode of ['dark','light']){await page.emulateMedia({colorScheme:mode});await page.waitForFunction(value=>document.documentElement.dataset.theme===value,mode);}
    await page.reload();assert.equal(await page.locator('html').getAttribute('data-palette'),palettes.at(-1));
    assert.equal(await page.locator('html').getAttribute('lang'),'en-US');
    await page.locator('#filter').fill('settings');assert.equal(await page.locator('.file-row').count(),1);
    await page.locator('#filter').fill('');await page.locator('[data-file="settings.json"]').click();assert.match(await page.locator('#diff-body').innerText(),/"palette"/);
    await page.locator('[data-file="README.md"]').click();assert.match(await page.locator('#diff-body').innerText(),/visible diff/);
    await page.locator('[data-view="history"]').click();assert.equal(await page.locator('.history-files').count(),1);await capture('workspace-history');
    await page.setViewportSize({width:960,height:620});
    await page.locator('.history-commit-header>b').evaluate(element=>element.textContent='Long commit summary '.repeat(40));
    assert.ok(await page.locator('.history-commit-actions button').evaluateAll(buttons=>buttons.length===5&&buttons.every(button=>{
      const bounds=button.getBoundingClientRect(), host=button.closest('.workspace').getBoundingClientRect();
      return bounds.left>=host.left&&bounds.right<=host.right+1&&bounds.bottom<=host.bottom+1;
    })), 'Every history action remains visible at minimum size');
    assert.ok(await page.locator('.history-commit-header').evaluate(header=>header.getBoundingClientRect().bottom<=document.querySelector('.history-diff-body').getBoundingClientRect().top+1), 'Long history summary leaves the diff below the toolbar');
    await capture('workspace-history-minimum');
    await page.setViewportSize({width:1440,height:900});
    await page.keyboard.press('Control+1');await page.locator('#summary').fill('Improve Studio workspace');await page.locator('#commit').click();
    assert.equal(await page.evaluate(()=>AvaPrototype.getState().files.length),0);
    assert.equal(await page.evaluate(()=>AvaPrototype.getState().history[0].summary),'Improve Studio workspace');
    await menu('MenuFile','MenuNewRepository');await page.locator('#action-value').fill('sample');await page.locator('#action-confirm').click();
    await page.locator('#stash').click();assert.equal(await page.evaluate(()=>AvaPrototype.getState().stash.length),1);
    await menu('MenuBranch','MenuRestoreStash');assert.equal(await page.evaluate(()=>AvaPrototype.getState().files.length),1);
    await menu('MenuBranch','MenuNewBranch');await page.locator('#action-value').fill('feature/prototype');await page.locator('#action-confirm').click();assert.equal(await page.locator('#branch-name').innerText(),'feature/prototype');
    await menu('MenuView','ShowOperationLog');assert.equal(await page.locator('.operation-log').isVisible(),false);await menu('MenuView','ShowOperationLog');
    const splitter=await page.locator('.vertical-splitter').boundingBox();await page.mouse.move(splitter.x+2,splitter.y+20);await page.mouse.down();await page.mouse.move(splitter.x+42,splitter.y+20);await page.mouse.up();assert.equal(await page.locator('.left').evaluate(el=>el.offsetWidth),400);
    for(const scenario of ['new','latest','offline','noasset']){
      await page.goto(standalone.url+'?capture=window&update='+scenario);await menu('MenuHelp','CheckUpdate');await page.locator('#update-action').click();await page.waitForFunction(()=>AvaPrototype.getState().update!=='checking');
      if(scenario==='new'){assert.equal(await page.evaluate(()=>AvaPrototype.getState().update),'available');await page.locator('#update-action').click();await page.waitForFunction(()=>AvaPrototype.getState().update==='downloaded');assert.match(await page.locator('#update-status').innerText(),/SHA256/);await page.locator('#update-action').click();assert.equal(await page.evaluate(()=>AvaPrototype.getState().update),'installed');}
      else if(scenario==='latest'||scenario==='offline')assert.equal(await page.evaluate(()=>AvaPrototype.getState().update),scenario);
      else assert.equal(await page.locator('#update-action').innerText(),'Open release page');
      await page.locator('#about-close').click();
    }
    await page.goto(standalone.url+'?capture=window');await menu('MenuHelp','CheckUpdate');await page.locator('#update-action').click();await page.locator('#update-cancel').click();assert.equal(await page.evaluate(()=>AvaPrototype.getState().update),'canceled');
    assert.deepEqual(errors,[]);await standalone.verify(page);
    console.log('PASS six source-derived menus, 32 appearance/language combinations, 1120/1440 layouts, Changes/History/commit/stash/branch/splitters, metadata and update flows.');
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
