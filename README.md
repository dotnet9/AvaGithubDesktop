# AvaGithubDesktop

AvaGithubDesktop 是一个受 GitHub Desktop 启发的 Avalonia 桌面 Git 客户端。

## 仓库规范

- 当前版本：`0.118.0.12`，版本号统一维护在根目录 `Directory.Build.props` 的 `<Version>` 节点。
- NuGet 包项目统一支持 `net8.0;net10.0`；Demo、App、测试与内部应用项目统一使用 `net10.0` / `net10.0-windows`。
- 根目录 `logo.svg`、`logo.png`、`logo.ico` 是唯一图标源，子工程只通过 MSBuild `Link` 引用，不维护图标副本。
- 运行时帮助、Markdown 示例、内置备忘录、设计说明等业务文档按功能保留；仓库级入口文档使用根目录 `README.md` 和 `UpdateLog.md`。

## 当前功能

- 打开本地 Git 仓库，显示分支、上游、远程仓库、最近提交和工作区变更。
- 在类似 GitHub Desktop 的 Changes 面板中选择变更文件并创建提交，支持撤销更改。
- 在 History 视图中浏览最近提交，并查看所选提交的文件详情。
- 预览工作区和历史提交中选中文件的差异（统一/并排、图片与二进制 diff、隐藏空白）。
- 分支全家桶：新建/重命名/删除/切换/合并/压缩合并/变基/设置上游。
- 同步：fetch / pull / push、领先落后状态徽章、stash 贮藏/恢复/丢弃。
- 标签创建与推送、远程仓库管理（添加/设置/移除）。
- GitHub 联动：账号登录（OAuth）、在 GitHub 查看仓库/比较分支/创建 Issue 与 PR。
- 冲突解决、Revert/Cherry-pick、LFS 与子模块支持。
- 7 套主题、中英双语、完整快捷键体系与操作日志。
## 包版本维护约定

XML 文件统一使用两个空格缩进。`Directory.Packages.props` 统一承载 NuGet 中央包管理开关和包版本变量，包括 `AvaloniaVersion` 等共享版本属性；`Directory.Build.props` 仅保留项目构建、编译选项和 NuGet 元数据。仓库如引用 `VC-LTL`、`YY-Thunks`，这两个兼容旧版操作系统的特殊包必须使用最新预览版。

## 发布

标准发布流程与发布说明规范见 [docs/RELEASE.md](docs/RELEASE.md)。
