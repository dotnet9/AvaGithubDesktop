# design/ — 界面原型

**原型即 UI 设计稿**：`index.html` 是可点击的静态原型（零依赖、不参与构建），
界面实现以原型为准；改动原型后再同步 Avalonia 实现。

打开方式：直接双击 `index.html`，或 `start design/index.html`。

当前包含：

- 主界面静态布局（窗口 chrome、导航、核心内容区）
- 三态模式与八套配色，独立保存，跟随系统明暗变化；色圈与下拉使用常规控件体验。
- 「关于」与「检查更新」入口共用关于窗口，包含版本、开发平台、编译时间、作者、仓库和版权。
- 检查、下载进度、SHA256 校验完成及安装按钮的交互模拟；URL 的 `update=latest/offline/noasset` 用于验收已最新、网络失败与平台资产缺失。

运行 `npm ci --prefix design/tests` 和 `npm test --prefix design/tests` 可在后台检查真实按钮点击、16 套配色、系统变化、设置恢复和四种更新流程。原型不请求 GitHub，也不启动安装器；实际网络、下载校验及安装由应用调用 Toolkit 的 IUpdateService。

应用内更新状态、命令与语言刷新位于 AboutViewModel，视图后置仅负责窗口关闭接线。应用层映射 Studio 颜色令牌，控件主题包保留独立默认资源。原型主要描述本轮新增的主题和关于交互，既有 Git 工作区未宣称逐像素还原。

## 单文件交付

`design/` 中的主 HTML 已内嵌样式、脚本、图标和图片。只复制这一个 HTML 到空目录即可离线查看并操作，无需旁边的资源文件或本地服务。

维护源码放在 `scripts/prototype/`；执行 `pwsh scripts/build_standalone_prototype.ps1` 重新生成，`-Check` 校验生成文件。不要直接修改交付 HTML。交互审计会复制 HTML 到空目录，阻止其他文件和网络资源请求，并检查脚本错误与图片完整性。
