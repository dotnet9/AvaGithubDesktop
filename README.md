# AvaGithubDesktop

AvaGithubDesktop 是一个受 GitHub Desktop 启发的 Avalonia 桌面 Git 客户端。

## 仓库规范

- 当前版本：`0.118.0.2`，版本号统一维护在根目录 `Directory.Build.props` 的 `<Version>` 节点。
- NuGet 包项目统一支持 `net8.0;net10.0`；Demo、App、测试与内部应用项目统一使用 `net11.0` / `net11.0-windows`。
- 根目录 `logo.svg`、`logo.png`、`logo.ico` 是唯一图标源，子工程只通过 MSBuild `Link` 引用，不维护图标副本。
- 运行时帮助、Markdown 示例、内置备忘录、设计说明等业务文档按功能保留；仓库级入口文档使用根目录 `README.md` 和 `UpdateLog.md`。

## 当前功能

- 打开本地 Git 仓库，显示分支、上游、远程仓库、最近提交和工作区变更。
- 在类似 GitHub Desktop 的 Changes 面板中选择变更文件并创建提交。
- 在 History 视图中浏览最近提交，并查看所选提交的文件详情。
- 预览工作区和历史提交中选中文件的文本差异。
- 在工具栏中列出本地分支，并切换到选中的分支。
