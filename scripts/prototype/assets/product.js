// 离线样例复用实际菜单和宿主资源，只在内存演示 Git 与更新状态。
(() => {
  'use strict';
  const $ = selector => document.querySelector(selector);
  const all = selector => [...document.querySelectorAll(selector)];
  const palettes = [['dotnet','ThemeDotNet','#512BD4'],['aurora','ThemeAurora','linear-gradient(135deg,#8968FF,#26D4CC)'],['ember','ThemeEmber','#BD674D'],['default','ThemeDefault','#0064FA'],['aquatic','ThemeAquatic','#008D9E'],['desert','ThemeDesert','#B57532'],['dusk','ThemeDusk','#7A5CC4'],['night-sky','ThemeNightSky','#2B6CB0']];
  const media = matchMedia('(prefers-color-scheme: dark)');
  const state = { culture:'zh-CN', mode:'system', palette:'dotnet', view:'changes', repository:'studio-workspace', branch:'main', branches:['main','develop','feature/studio'], files:[{path:'README.md',selected:true},{path:'settings.json',selected:true}], activeFile:'README.md', history:[{summary:'Create isolated verification workspace',sha:'e9754cd'}], selectedCommit:0, logs:[], stash:[], signedIn:false, logVisible:true, sideBySide:false, whitespace:false, update:'idle' };
  try { const saved=JSON.parse(localStorage.getItem('ava-desktop-appearance')||'{}'); for(const key of ['culture','mode','palette'])if(saved[key])state[key]=saved[key]; } catch {}
  if(!AVA_PRODUCT.resources[state.culture])state.culture='zh-CN';
  if(!['system','light','dark'].includes(state.mode))state.mode='system';
  if(!palettes.some(item=>item[0]===state.palette))state.palette='dotnet';
  const t = (key,...args) => {
    const value=AVA_PRODUCT.resources[state.culture][key];
    if(value===undefined)throw Error('Missing resource: '+key);
    return value.replace(/\{(\d+)\}/g,(_,index)=>args[+index]??'{'+index+'}');
  };
  const node = (tag,text,className) => { const item=document.createElement(tag); if(text!==undefined)item.textContent=text; if(className)item.className=className; return item; };
  const keyNode = (tag,key) => {const item=node(tag,t(key));item.dataset.key=key;return item;};
  const version = '0.118.0.19';
  const about = $('#about-dialog'), popup = $('#popup');
  let updateTimer, toastTimer, dialogAction, dialogKey;
  const scenario = new URLSearchParams(location.search).get('update')||'new';
  if(new URLSearchParams(location.search).has('capture'))document.body.classList.add('capture-window');
  const metadata = [['AboutVersionLabel',version],['AboutPlatformLabel','.NET 10.0.13 · Avalonia 12.1.4.0'],['AboutCompileTimeLabel','2026-10-15 00:00:00'],['AboutAuthorLabel','@AboutAuthorName'],['AboutRepositoryLabel','https://github.com/dotnet9/AvaGithubDesktop'],['AboutWebsiteLabel','https://codewf.com'],['AboutBrandLabel','@AboutBrandName'],['AboutCopyrightLabel','@PrototypeCopyright']];
  for(const [key,,color] of palettes){const button=node('button');button.dataset.palette=key;button.style.setProperty('--swatch',color);button.onclick=()=>setAppearance({palette:key});$('#swatches').append(button);}
  AVA_PRODUCT.menus.forEach(menu=>{const button=keyNode('button',menu.key);button.dataset.menu=menu.key;button.setAttribute('aria-haspopup','menu');button.onclick=event=>{event.stopPropagation();showMenu(button,menu.children);};$('#main-menu').append(button);});
  function setAppearance(values) {
    Object.assign(state,values);
    document.documentElement.lang=state.culture;
    document.documentElement.dataset.theme=state.mode==='system'?(media.matches?'dark':'light'):state.mode;
    document.documentElement.dataset.themeMode=state.mode;
    document.documentElement.dataset.palette=state.palette;
    try{localStorage.setItem('ava-desktop-appearance',JSON.stringify({culture:state.culture,mode:state.mode,palette:state.palette}));}catch{}
    all('[data-key]').forEach(item=>item.textContent=t(item.dataset.key));
    all('[data-placeholder]').forEach(item=>item.placeholder=t(item.dataset.placeholder));
    $('#theme-mode').textContent=({system:'▣',light:'☀',dark:'☾'})[state.mode];$('#theme-mode').title=t('Theme')+': '+t('Theme'+state.mode[0].toUpperCase()+state.mode.slice(1));$('#theme-mode').setAttribute('aria-label',$('#theme-mode').title);
    $('#language-button').title=t('Language');$('#language-button').setAttribute('aria-label',t('Language'));
    const captionLabels=state.culture==='zh-CN'?['最小化','最大化或还原','关闭']:['Minimize','Maximize or restore','Close'];all('.captions > button').forEach((button,index)=>{button.title=captionLabels[index];button.setAttribute('aria-label',button.title);});
    all('#swatches button').forEach(button=>{button.title=t(palettes.find(item=>item[0]===button.dataset.palette)[1]);button.setAttribute('aria-label',button.title);button.setAttribute('aria-pressed',String(button.dataset.palette===state.palette));});
    renderWorkspace();renderAbout();renderUpdate();renderLogs();status(state.logs.length?t(state.logs.at(-1).key,...state.logs.at(-1).args):t('StatusReady'));
  }
  $('#theme-mode').onclick=()=>{const modes=['system','light','dark'];setAppearance({mode:modes[(modes.indexOf(state.mode)+1)%3]});};
  $('#language-button').onclick=event=>{event.stopPropagation();showMenu(event.currentTarget,[{key:'SimplifiedChinese'},{key:'English'}]);};
  media.addEventListener('change',()=>{if(state.mode==='system')setAppearance({});});
  function status(text){$('#status').textContent=text;}
  function log(key,...args){state.logs.push({key,args,time:new Date().toLocaleTimeString('en-GB')});renderLogs();status(t(key,...args));}
  function renderLogs(){
    $('#log-lines').replaceChildren(...state.logs.map(item=>{const line=node('div',undefined,'log-line');line.append(node('time','2026-10-15 '+item.time+'  '),node('em','[Information]  '),node('span',t(item.key,...item.args)));return line;}));
    if(!state.logs.length)$('#log-lines').append(node('div','[Information]  [git] $ git status --short','log-line'));
    $('#log-lines').scrollTop=$('#log-lines').scrollHeight;
  }
  function toast(text){$('#toast').textContent=text;$('#toast').classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('#toast').classList.remove('show'),1600);}
  function renderWorkspace(){
    $('#repository-name').textContent=state.repository;
    $('#branch-name').textContent=state.branch;
    $('#sync-name').textContent=t('SyncNoRemote');$('#sync-detail').textContent=t('SyncNoRemoteDescription');
    $('#changes-count').textContent=state.files.length;
    $('#changes-count').hidden=!state.files.length;
    $('#commit-form').hidden=state.view!=='changes';$('#files').hidden=state.view!=='changes';$('#history').hidden=state.view!=='history';$('#filter').hidden=state.view!=='changes';
    $('#selection-header').classList.toggle('history-header',state.view==='history');$('#select-all').hidden=state.view!=='changes';
    all('[data-view]').forEach(button=>button.classList.toggle('selected',button.dataset.view===state.view));
    const included=state.files.filter(file=>file.selected).length;
    $('#selection-title').textContent=state.view==='changes'?t('ChangedFilesCountFormat',state.files.length):t('History');
    $('#selected-count').textContent=state.view==='changes'?t('SelectedFilesCountFormat',included,state.files.length):t(state.history.length===1?'HistoryCommitCountFormat':'HistoryCommitsCountFormat',state.history.length);
    $('#select-all').checked=!!included&&included===state.files.length;$('#select-all').indeterminate=included>0&&included<state.files.length;
    const filter=$('#filter').value.toLowerCase();
    $('#files').replaceChildren(...state.files.filter(file=>file.path.toLowerCase().includes(filter)).map(file=>{
      const row=node('div',undefined,'file-row'+(file.path===state.activeFile?' selected':''));row.dataset.file=file.path;row.tabIndex=0;
      const checkbox=node('input');checkbox.type='checkbox';checkbox.checked=file.selected;checkbox.setAttribute('aria-label',file.path);checkbox.onclick=event=>{event.stopPropagation();file.selected=checkbox.checked;renderWorkspace();};
      row.append(checkbox,node('span',file.path),node('small',t('PrototypeModified')));row.onclick=()=>{state.activeFile=file.path;renderWorkspace();};row.onkeydown=event=>{if(event.key==='Enter')row.click();};row.oncontextmenu=event=>{event.preventDefault();showMenu(row,[{key:'MenuCopyFilePath'},{key:'MenuCopyRelativePath'},{separator:true},{key:'MenuDiscardChanges'}]);};return row;
    }));
    if(!state.files.length)$('#files').append(node('div',t('NoChangesTitle'),'empty'));
    $('#history').replaceChildren(...state.history.map((entry,index)=>{const button=node('button',undefined,'history-item'+(state.selectedCommit===index?' selected':''));button.append(node('b',entry.summary),node('small',t('PrototypeAuthor')+' · '+entry.sha+' · '+t('PrototypeJustNow')));button.onclick=()=>{state.selectedCommit=index;renderDiff();renderWorkspace();};return button;}));
    $('#commit').textContent=$('#amend').checked?t('AmendLastCommitButton'):t(included===1?'CommitFileToBranchFormat':'CommitFilesToBranchFormat',included,state.branch);
    $('#commit').title=$('#commit').textContent;
    $('#commit').disabled=!included||!$('#summary').value.trim();$('#stash').disabled=!state.files.length;
    renderDiff();
  }
  function renderDiff(){
    const container=$('.diff');container.replaceChildren();container.classList.toggle('history-diff',state.view==='history');
    let host=container;
    if(state.view==='history'){
      const entry=state.history[state.selectedCommit]||state.history[0];
      const head=node('div',undefined,'history-commit-header');head.append(node('b',entry?.summary||t('NoHistoryTitle')));
      const meta=node('div',undefined,'history-commit-meta');meta.append(node('span',t('PrototypeAuthor')+' <verification@example.invalid>'),node('span',t('PrototypeJustNow')));
      const actions=node('div',undefined,'history-commit-actions');actions.append(node('span',entry?.sha||''));
      for(const key of ['CopyCommitSha','ViewCommitOnGitHub','CreateTag','CherryPickCommit','RevertCommit']){const button=keyNode('button',key);button.onclick=()=>run({key});actions.append(button);}head.append(meta,actions);container.append(head);
      const body=node('div',undefined,'history-diff-body');const files=node('div',undefined,'history-files');files.append(node('b',t('CommitChangedFilesCountFormat',2),'history-files-title'));
      for(const path of ['README.md','settings.json']){const button=node('button',undefined,'history-file'+(state.activeFile===path?' selected':''));button.append(node('small',t('PrototypeAdded')),node('span',path));button.onclick=()=>{state.activeFile=path;renderDiff();};files.append(button);}
      host=node('div',undefined,'history-code');body.append(files,node('div',undefined,'history-divider'),host);container.append(body);
    }
    const header=node('div',undefined,'diff-header');header.append(node('b',t('Diff')),node('span',state.activeFile||t('NoFileSelected')));host.append(header);
    const body=node('div');body.id='diff-body';body.classList.toggle('side-by-side',state.sideBySide);host.append(body);
    if(state.view==='changes'&&!state.files.length){body.append(node('div',t('NoChangesDetail'),'empty'));return;}
    const lines=state.activeFile==='settings.json'?[['header','','','diff --git a/settings.json b/settings.json'],['','1','1','  {'],['remove','2','','-   "theme": "system"'],['add','','2','+   "theme": "dark",'],['add','','3','+   "palette": "dotnet"'],['','3','4','  }']]:state.view==='history'?[['header','','','diff --git a/README.md b/README.md'],['header','','','new file mode 100644'],['header','','','index 0000000..badf3e5'],['header','','','--- /dev/null'],['header','','','+++ b/README.md'],['hunk','','','@@ -0,0 +1,3 @@'],['add','','1','+ # Verification'],['add','','2','+'],['add','','3','+ A local Git workspace.']]:[['header','','','diff --git a/README.md b/README.md'],['header','','','index badf3e5..b072bea 100644'],['header','','','--- a/README.md'],['header','','','+++ b/README.md'],['hunk','','','@@ -1,3 +1,6 @@'],['','1','1','  # Verification'],['','2','2','  '],['remove','3','','- A local Git workspace.'],['add','','3','+ A local Git workspace with a visible diff.'],['add','','4','+'],['add','','5','+ - Eight palettes'],['add','','6','+ - Three appearance modes']];
    for(const [type,oldNumber,newNumber,text]of lines){const row=node('div',undefined,'code-line '+type);row.append(node('span',oldNumber),node('span',newNumber),node('span',text));body.append(row);}
  }
  function selectView(view){state.view=view;popup.hidden=true;renderWorkspace();}
  all('[data-view]').forEach(button=>button.onclick=()=>selectView(button.dataset.view));
  $('#filter').oninput=renderWorkspace;$('#summary').oninput=renderWorkspace;$('#amend').onchange=renderWorkspace;
  $('#select-all').onchange=event=>{state.files.forEach(file=>file.selected=event.target.checked);renderWorkspace();};
  function commit(){
    if($('#commit').disabled){toast(t('PrototypeReadyToCommit'));return;}
    const summary=$('#summary').value.trim();if($('#amend').checked)state.history[0].summary=summary;else state.history.unshift({summary,sha:Math.random().toString(16).slice(2,9)});
    state.files=state.files.filter(file=>!file.selected);$('#summary').value='';$('#description').value='';state.selectedCommit=0;log('StatusCommittedFormat',summary);renderWorkspace();
  }
  $('#commit-form').onsubmit=event=>{event.preventDefault();commit();};
  $('#stash').onclick=()=>run({key:'MenuStashAllChanges'});
  function showMenu(anchor,items){
    popup.replaceChildren();
    for(const item of items){
      if(item.visibleWhen==='IsSignedIn'&&!state.signedIn||item.visibleWhen==='IsSignedOut'&&state.signedIn)continue;
      if(item.separator){popup.append(node('hr'));continue;}
      const button=node('button',undefined);button.dataset.command=item.key;button.append(node('span',t(item.key)),node('small',item.children?.length?'›':item.hotkey||''));
      button.disabled=!!item.disabled||/Menu(?:Continue|Abort|Skip)(?:Merge|Rebase|Revert|CherryPick)/.test(item.key);
      button.onclick=event=>{event.stopPropagation();if(item.children?.length)showMenu(anchor,item.children);else{popup.hidden=true;run(item);}};popup.append(button);
    }
    popup.hidden=false;const rect=anchor.getBoundingClientRect();popup.style.left=Math.min(rect.left,innerWidth-280)+'px';popup.style.top=Math.min(rect.bottom,innerHeight-popup.offsetHeight-30)+'px';
  }
  document.addEventListener('click',event=>{if(!event.target.closest('#popup')&&!event.target.closest('#main-menu'))popup.hidden=true;});
  function ask(key,value,action,content){dialogKey=key;dialogAction=action;$('#action-title').textContent=t(key);$('#action-content').textContent=content||'';$('#action-value').hidden=value===null;$('#action-value').value=value||'';if(!$('#action-dialog').open)$('#action-dialog').showModal();if(value!==null)$('#action-value').focus();}
  $('#action-form').onsubmit=event=>{event.preventDefault();const value=$('#action-value').value.trim();if(!$('#action-value').hidden&&!value){$('#action-value').focus();return;}$('#action-dialog').close();dialogAction?.(value);};
  $('#action-cancel').onclick=()=>$('#action-dialog').close();
  function picker(anchor,values,action){showMenu(anchor,values.map(value=>({key:value[0]})));[...popup.querySelectorAll('button')].forEach((button,index)=>{button.onclick=()=>{popup.hidden=true;action(values[index][1]);};});}
  $('#repository').onclick=event=>{event.stopPropagation();popup.replaceChildren();for(const name of ['studio-workspace','CodeWF.Avalonia','CodeWF.Toolkit']){const button=node('button',name);button.onclick=()=>{state.repository=name;popup.hidden=true;log('StatusLoadedRepositoryFormat',name,state.files.length);renderWorkspace();};popup.append(button);}for(const key of ['MenuNewRepository','MenuAddLocalRepository','MenuCloneRepository']){const button=keyNode('button',key);button.onclick=()=>{popup.hidden=true;run({key});};popup.append(button);}const rect=$('#repository').getBoundingClientRect();popup.style.left=rect.left+'px';popup.style.top=rect.bottom+'px';popup.hidden=false;};
  $('#branch').onclick=event=>{event.stopPropagation();popup.replaceChildren();for(const name of state.branches){const button=node('button',name);button.onclick=()=>{state.branch=name;popup.hidden=true;renderWorkspace();};popup.append(button);}const button=keyNode('button','MenuNewBranch');button.onclick=()=>{popup.hidden=true;run({key:'MenuNewBranch'});};popup.append(button);const rect=$('#branch').getBoundingClientRect();popup.style.left=rect.left+'px';popup.style.top=rect.bottom+'px';popup.hidden=false;};
  $('#remote').onclick=()=>run({key:'MenuManageRemote'});
  function run(item){
    const key=item.key;
    if(key==='SimplifiedChinese'||key==='English'){setAppearance({culture:key==='English'?'en-US':'zh-CN'});return;}
    if(['ThemeSystem','ThemeLight','ThemeDark'].includes(key)){setAppearance({mode:key.replace('Theme','').toLowerCase()});return;}
    const palette=palettes.find(entry=>entry[1]===key);if(palette){setAppearance({palette:palette[0]});return;}
    if(key==='Changes'||key==='History'){selectView(key.toLowerCase());return;}
    if(['MenuAbout','CheckUpdate'].includes(key)){showAbout();return;}
    if(key==='MenuShowRepositoryList'){ $('#repository').click();return; }
    if(key==='MenuShowBranchesList'){ $('#branch').click();return; }
    if(key==='MenuFocusCommitSummary'){selectView('changes');$('#summary').focus();return;}
    if(['MenuFind','MenuFocusChangesFilter'].includes(key)){selectView('changes');$('#filter').focus();return;}
    if(key==='ShowOperationLog'){state.logVisible=!state.logVisible;$('.operation-log').hidden=!state.logVisible;$('.horizontal-splitter').hidden=!state.logVisible;log(state.logVisible?'StatusOperationLogShown':'StatusOperationLogHidden');return;}
    if(key==='MenuShowSideBySideDiff'){state.sideBySide=!state.sideBySide;renderDiff();log(state.sideBySide?'StatusSideBySideDiffShown':'StatusUnifiedDiffShown');return;}
    if(key==='MenuHideWhitespaceChanges'){state.whitespace=!state.whitespace;log(state.whitespace?'StatusWhitespaceChangesHidden':'StatusWhitespaceChangesShown');return;}
    if(['MenuNewRepository','MenuCloneRepository','MenuAddLocalRepository','Open'].includes(key)){ask(key,key==='MenuCloneRepository'?'https://github.com/dotnet9/sample.git':'sample-workspace',value=>{state.repository=value.split(/[\\/]/).filter(Boolean).at(-1).replace(/\.git$/,'');state.files=[{path:'README.md',selected:true}];log('StatusLoadedRepositoryFormat',state.repository,state.files.length);renderWorkspace();});return;}
    if(['MenuNewBranch','MenuRenameBranch'].includes(key)){ask(key,key==='MenuNewBranch'?'feature/studio-preview':state.branch,value=>{if(!state.branches.includes(value))state.branches.push(value);state.branch=value;renderWorkspace();});return;}
    if(key==='MenuDeleteBranch'){ask('DeleteBranchTitle',null,()=>{state.branches=state.branches.filter(branch=>branch!==state.branch);state.branch=state.branches[0]||'main';renderWorkspace();},t('DeleteBranchMessageFormat',state.branch));return;}
    if(['MenuMergeBranch','MenuSquashMergeBranch','MenuRebaseBranch','MenuSetUpstream','MenuManageRemote','CreateTag'].includes(key)){ask(key,key==='MenuManageRemote'?'https://github.com/dotnet9/'+state.repository+'.git':key==='CreateTag'?'v1.0.0':'develop',()=>log('PrototypeOperationCompleted',t(key)));return;}
    if(['MenuStashAllChanges','MenuStashChanges'].includes(key)){state.stash=state.files.map(file=>({...file}));state.files=[];log('StatusStashedChangesFormat',state.branch);renderWorkspace();return;}
    if(['MenuRestoreStash','MenuShowStashedChanges'].includes(key)){if(state.stash.length){state.files=state.stash.map(file=>({...file}));state.stash=[];selectView('changes');log('StatusRestoredStash');}else toast(t('PrototypeNoStash'));return;}
    if(key==='MenuDiscardStash'){state.stash=[];log('StatusDiscardedStash');return;}
    if(['MenuDiscardChanges','MenuDiscardAllChanges'].includes(key)){ask('DiscardChangesConfirmTitle',null,()=>{state.files=key==='MenuDiscardAllChanges'?[]:state.files.filter(file=>file.path!==state.activeFile);renderWorkspace();},t('DiscardChangesConfirmWarning'));return;}
    if(key==='KeyboardShortcuts'){ask('PrototypeShortcutTitle',null,null,t('PrototypeShortcutBody'));return;}
    if(key==='SignInToGitHub'){ask('SignInTitle','https://api.github.com',()=>{state.signedIn=true;log('PrototypeOperationCompleted',t('SignIn'));},t('SignInDescription')+'\n'+t('PrototypeSignInCode'));return;}
    if(key==='SignOut'){state.signedIn=false;log('StatusSignedOutGitHub');return;}
    if(key==='MenuSelectAll'){const input=document.activeElement;if(input?.select)input.select();else{state.files.forEach(file=>file.selected=true);renderWorkspace();}return;}
    if(['MenuUndo','MenuRedo','MenuCut','MenuCopy','MenuPaste'].includes(key)){document.execCommand(key.replace('Menu','').toLowerCase());toast(t(key));return;}
    if(/Copy/.test(key)){const text=/Sha/.test(key)?state.history[state.selectedCommit]?.sha:/Branch/.test(key)?state.branch:/RepositoryName/.test(key)?state.repository:/Path/.test(key)?state.activeFile:'https://github.com/dotnet9/'+state.repository;navigator.clipboard?.writeText(text).catch(()=>{});toast(t('PrototypeCopied',text));return;}
    if(['MenuExit','quit'].includes(key)){$('.win').hidden=true;$('.win').dataset.windowState='closed';$('#relaunch').hidden=false;return;}
    if(key==='MenuToggleFullScreen'||key==='maximize'){$('.win').classList.toggle('maximized');toast(t('MenuToggleFullScreen'));return;}
    if(key==='MenuExpandActiveResizable'||key==='MenuContractActiveResizable'){$('.workspace').style.gridTemplateColumns=(key==='MenuExpandActiveResizable'?'440':'300')+'px 4px minmax(0,1fr)';return;}
    if(/(?:Push|Pull|Fetch|Refresh|UpdateSubmodules|UpdateFromDefault|UnsetUpstream|SetCurrentBranchAsDefault)/.test(key)){log('PrototypeOperationCompleted',t(key));return;}
    ask(key,null,null,t('PrototypeExternalAction',/GitHub|Issue|Support|UserGuides/.test(key)?'https://github.com/dotnet9/'+state.repository:t(key)));
  }
  all('[data-action]').forEach(button=>button.onclick=()=>{if(button.dataset.action==='minimize'){$('.win').hidden=true;$('.win').dataset.windowState='minimized';$('#relaunch').hidden=false;}else run({key:button.dataset.action});});
  $('#relaunch button').onclick=()=>{$('#relaunch').hidden=true;$('.win').hidden=false;delete $('.win').dataset.windowState;};
  function renderAbout(){
    $('#about-info').className='about-info';$('#about-info').replaceChildren(...metadata.flatMap(([key,value])=>{const dd=node('dd');if(value.startsWith('https:')){const link=node('button',value,'link');link.onclick=()=>ask(key,null,null,t('PrototypeExternalAction',value));dd.append(link);}else dd.textContent=value.startsWith('@')?t(value.slice(1)):value;return[node('dt',t(key)),dd];}));
    $('#release-notes p').textContent=t('PrototypeReleaseNotes');
  }
  function showAbout(){popup.hidden=true;if(!about.open)about.showModal();}
  function renderUpdate(){
    const busy=['checking','downloading'].includes(state.update);const action=$('#update-action');action.disabled=busy;$('#update-cancel').hidden=!busy;$('#update-progress').hidden=state.update!=='downloading';$('#update-recheck').hidden=!['available','downloaded','installed'].includes(state.update);$('#release-notes').hidden=!['available','downloading','downloaded','installed'].includes(state.update);
    const labels={idle:'AboutCheckUpdate',checking:'AboutCheckingUpdate',available:scenario==='noasset'?'AboutOpenRelease':'AboutDownload',downloading:'AboutDownloading',downloaded:'AboutInstall',installed:'AboutCheckUpdate',latest:'AboutCheckUpdate',offline:'AboutCheckUpdate',canceled:'AboutCheckUpdate'};
    const messages={idle:'',checking:t('AboutCheckingUpdate'),available:scenario==='noasset'?t('PrototypeReleaseUnavailable'):t('AboutUpdateAvailable','0.119.0'),downloading:t('AboutDownloadProgress',$('#update-progress').value+'%'),downloaded:t('PrototypeDownloadVerified'),installed:t('PrototypeInstallerPreview'),latest:t('AboutUpToDate'),offline:t('AboutUpdateFailed',t('PrototypeOffline')),canceled:t('AboutCanceled')};
    action.textContent=t(labels[state.update]);$('#update-status').textContent=messages[state.update];
  }
  $('#update-action').onclick=()=>{
    clearInterval(updateTimer);
    if(state.update==='available'){
      if(scenario==='noasset'){toast(t('AboutOpenRelease'));return;}
      state.update='downloading';$('#update-progress').value=0;renderUpdate();updateTimer=setInterval(()=>{$('#update-progress').value+=20;if($('#update-progress').value>=100){clearInterval(updateTimer);state.update='downloaded';}renderUpdate();},90);
    }else if(state.update==='downloaded'){state.update='installed';renderUpdate();}
    else{state.update='checking';renderUpdate();updateTimer=setTimeout(()=>{state.update=['latest','offline'].includes(scenario)?scenario:'available';renderUpdate();},180);}
  };
  $('#update-cancel').onclick=()=>{clearInterval(updateTimer);state.update='canceled';renderUpdate();};
  $('#update-recheck').onclick=()=>{state.update='idle';$('#update-action').click();};
  $('#about-minimize').onclick=()=>about.close();$('#about-maximize').onclick=()=>about.classList.toggle('about-maximized');
  for(const selector of ['#about-close','#about-close-caption'])$(selector).onclick=()=>about.close();
  about.addEventListener('close',()=>{clearInterval(updateTimer);if(['checking','downloading'].includes(state.update)){state.update='canceled';renderUpdate();}});
  for(const [selector,direction]of [['.vertical-splitter','x'],['.horizontal-splitter','y']]){
    $(selector).onpointerdown=event=>{event.preventDefault();const start=direction==='x'?event.clientX:event.clientY;const size=direction==='x'?$('.left').offsetWidth:$('.operation-log').offsetHeight;const move=next=>{if(direction==='x')$('.workspace').style.gridTemplateColumns=Math.min(520,Math.max(260,size+next.clientX-start))+'px 4px minmax(0,1fr)';else $('.operation-log').style.height=Math.min(320,Math.max(80,size+start-next.clientY))+'px';};const done=()=>{document.removeEventListener('pointermove',move);document.removeEventListener('pointerup',done);};document.addEventListener('pointermove',move);document.addEventListener('pointerup',done);};
  }
  document.addEventListener('keydown',event=>{if(event.key==='Escape'){popup.hidden=true;return;}if(!event.ctrlKey)return;const key=event.key.toLowerCase();const command={1:'Changes',2:'History',t:'MenuShowRepositoryList',b:'MenuShowBranchesList',g:'MenuFocusCommitSummary',l:'MenuFocusChangesFilter'}[key];if(command){event.preventDefault();run({key:command});}if(key==='enter'){event.preventDefault();commit();}});
  window.AvaPrototype={setAppearance,selectView,run:key=>run({key}),getState:()=>JSON.parse(JSON.stringify(state)),showAbout,geometry:()=>Object.fromEntries(all('[data-region]').map(item=>{const rect=item.getBoundingClientRect();return[item.dataset.region,{x:rect.x,y:rect.y,width:rect.width,height:rect.height}];}))};
  setAppearance({});status(t('StatusReady'));
})();
