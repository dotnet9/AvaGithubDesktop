// 从宿主资源与实际菜单结构生成原型数据，避免另维护一套菜单和翻译。
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..'),resources={};
for(const culture of ['zh-CN','en-US'])resources[culture]={...JSON.parse(fs.readFileSync(path.join(root,'src/AvaGithubDesktop/I18n',culture+'.json'),'utf8').replace(/^\uFEFF/,'')).AvaGithubDesktop.AvaGithubDesktopL,...JSON.parse(fs.readFileSync(path.join(__dirname,culture+'.json'),'utf8'))};
const source=fs.readFileSync(path.join(root,'src/AvaGithubDesktop/Views/MainWindow.axaml'),'utf8');
const xml=source.slice(source.search(/<Menu(?:\s|>)/),source.indexOf('</Menu>')+7);
const menus=[],stack=[];
for(const match of xml.matchAll(/<(\/?)(MenuItem|Separator)\b([^>]*?)(\/?)>/g)){
 const[,closing,type,attributes,selfClosing]=match;
 if(closing){if(type==='MenuItem')stack.pop();continue;}
 const parent=stack.length?stack.at(-1).children:menus;
 if(type==='Separator'){parent.push({separator:true});continue;}
 const key=attributes.match(/AvaGithubDesktopL\.(\w+)/)?.[1]||({ToggleStashedChangesMenuText:'MenuShowStashedChanges',AccountButtonText:'NotSignedIn',AccountEndpointText:'Remote',UpdateFromDefaultBranchMenuText:'MenuUpdateFromDefaultBranch'}[attributes.match(/Header="\{Binding (\w+)/)?.[1]]);
 if(!key)throw Error('Missing resource mapping for menu: '+attributes);
 const item={key,command:attributes.match(/Command="\{Binding (\w+)/)?.[1]||key,hotkey:attributes.match(/HotKey="([^"]+)/)?.[1]||'',disabled:attributes.includes('IsEnabled="False"'),visibleWhen:attributes.match(/IsVisible="\{Binding (\w+)/)?.[1]||'',children:[]};
 parent.push(item);if(!selfClosing)stack.push(item);
}
if(menus.length!==6)throw Error('Expected the actual six application menus.');
for(const culture of Object.keys(resources))for(const item of menus.flatMap(function flatten(item){return[item,...item.children?.flatMap(flatten)||[]];}))if(item.key&&!resources[culture][item.key])throw Error('Missing '+culture+' resource '+item.key);
const output=path.join(__dirname,'assets/product-data.js'),text='// 根据宿主国际化和主菜单生成，请运行 scripts/prototype/build-resources.cjs。\nwindow.AVA_PRODUCT='+JSON.stringify({resources,menus})+';\n';
if(process.argv.includes('--check')){if(!fs.existsSync(output)||fs.readFileSync(output,'utf8').replace(/\r\n/g,'\n')!==text)throw Error('Prototype resources or menu are stale.');}else fs.writeFileSync(output,text);
