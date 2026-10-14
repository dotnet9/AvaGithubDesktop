// 原型只模拟更新状态；实际应用通过 Toolkit 服务检查、校验和安装。
(() => {
  const palettes = [
    ['dotnet', '.NET 紫'], ['aurora', 'AI 极光'], ['ember', 'AI 赤陶'], ['default', '默认蓝'],
    ['aquatic', '水生'], ['desert', '沙漠'], ['dusk', '黄昏'], ['night-sky', '夜空']
  ];
  const system = matchMedia('(prefers-color-scheme: dark)');
  const mode = document.querySelector('#theme-mode');
  let palette = 'dotnet';
  try {
    const saved = JSON.parse(localStorage.getItem('ava-desktop-appearance') || '{}');
    if (['system', 'light', 'dark'].includes(saved.mode)) mode.value = saved.mode;
    if (palettes.some(item => item[0] === saved.palette)) palette = saved.palette;
  } catch (_) {}
  const swatches = document.querySelector('.swatches');
  for (const [key, title] of palettes) {
    const button = document.createElement('button');
    button.dataset.palette = key;
    button.title = title;
    button.setAttribute('aria-label', title);
    button.onclick = () => { palette = key; apply(); };
    // 每个色圈取自身主题资源，避免按 RGB 值判断当前主题。
    button.dataset.theme = 'light';
    swatches.append(button);
  }
  function apply() {
    const effective = mode.value === 'system' ? (system.matches ? 'dark' : 'light') : mode.value;
    document.documentElement.dataset.theme = effective;
    document.documentElement.dataset.themeMode = mode.value;
    document.documentElement.dataset.palette = palette;
    for (const button of swatches.children) button.setAttribute('aria-pressed', String(button.dataset.palette === palette));
    try { localStorage.setItem('ava-desktop-appearance', JSON.stringify({ mode: mode.value, palette })); } catch (_) {}
  }
  mode.onchange = apply;
  system.addEventListener('change', () => { if (mode.value === 'system') apply(); });
  apply();
  const menu = document.querySelector('#moreMenu');
  const dialog = document.querySelector('#about-dialog');
  const action = document.querySelector('#update-action');
  const status = document.querySelector('#update-status');
  const progress = document.querySelector('#update-progress');
  const scenario = new URLSearchParams(location.search).get('update') || 'new';
  let state = 'idle', timer;
  function showAbout() { menu.classList.remove('open'); if (!dialog.open) dialog.showModal(); }
  function setState(next, text, label = '检查更新') {
    state = next; status.textContent = text; action.textContent = label;
    action.disabled = ['checking', 'downloading'].includes(state);
    progress.hidden = state !== 'downloading';
  }
  document.querySelector('#moreBtn').onclick = event => { event.stopPropagation(); menu.classList.toggle('open'); };
  document.body.addEventListener('click', () => menu.classList.remove('open'));
  document.querySelector('#aboutItem').onclick = showAbout;
  for (const id of ['checkUpdateItem', 'checkUpdateFoot']) document.getElementById(id).onclick = showAbout;
  document.querySelector('#about-close').onclick = () => dialog.close();
  dialog.addEventListener('close', () => { clearInterval(timer); if (['checking', 'downloading'].includes(state)) setState('idle', ''); });
  action.onclick = () => {
    if (state === 'available') {
      if (scenario === 'noasset') { setState('idle', '当前平台暂无可用安装包，请访问发布页。', '打开发布页'); return; }
      setState('downloading', '正在下载并校验…', '正在下载…'); progress.value = 0;
      timer = setInterval(() => {
        progress.value += 20;
        if (progress.value >= 100) { clearInterval(timer); setState('downloaded', '下载及 SHA256 校验完成。', '安装更新'); }
      }, 100);
    } else if (state === 'downloaded') setState('installed', '已演示启动安装程序。');
    else {
      setState('checking', '正在检查更新…', '正在检查…');
      timer = setTimeout(() => {
        if (scenario === 'latest') setState('current', '当前已是最新版本。');
        else if (scenario === 'offline') setState('failed', '检查更新失败：网络不可用。');
        else setState('available', '发现新版本 0.119.0。', scenario === 'noasset' ? '打开发布页' : '下载更新');
      }, 200);
    }
  };
})();
