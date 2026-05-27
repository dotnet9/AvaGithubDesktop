# AvaGithubDesktop需求文档

参考Github Desktop开发一个Git桌面管理客户端：

- https://github.com/desktop/desktop

本地Clone仓库目录：

- D:\github\desktop

目标：

0. 功能基本完全照搬desktop
1. 使用Avalonia开发，支持Windows(包括Win7）、Linux、macOS
2. 项目使用到的Avalonia开源主题及控件：
   - D:\github\Semi.Avalonia
   - D:\github\Ursa.Avalonia
3. 支持国际化，使用Lang.Avalonia.Json实现i18n\l10n
4. 进程内消息通信使用 D:\github\CodeWF.EventBus，达到解耦目的
5. UrsaWindow目前在Win7、Linux支持不好，待修复，可使用CodeWFWindow
做为主窗体基类
   - D:\github\CodeWF.AvaloniaControls
6. 软件关于和更新日志使用CodeWF.Markdown展示
   - D:\github\CodeWF.Markdown
7. 使用Prism 8.X实现IOC依赖注入
8. Prism和ReactiveUI.Avalonia配合使用MVVM模式规范开发Avalonia程序
9. 源码与文档等目录组织可参考 D:\github\Vex
10. 前面提的仓库目录引用时都通过NuGet包的形式安装
11. 完成一个功能，截图分析验证通过，用英文规范化提交及推送，再迭代下一个功能
12. 参考Vex(D:\github\Vex)，语言资源使用T4文件；
13. 注意样式封装，不要直接在App.axaml里写Style，注意封装到文件中去，参考Vex，可再创建
    - AvaGithubDesktop.Controls工程，用于放本项目可能自定义的控件（有的话）
    - AvaGithubDesktop.Controls.Themes工程放对应的自定义控件样式及主题等
    - 如果是对Semi或Ursa的样式个性化调整，也可以创建AvaGithubDesktop.Semi、AvaGithubDesktop.Ursa进行维护，这样方便后续扩展、人工阅读理解维护；
14. 工程不怕多，就怕代码逻辑、业务逻辑划分不清晰；
15. 在./docs/GithubDesktop目录放了原Github Desktop软件部分截图，在d:\github\desktop仓库里实现的软件风格应该也是这样的，你一定要复刻desktop代码里实现的功能哦，截图是让你更明确
16. 添加CodeWF.LogViewer NuGet包，本地仓库目录D:\github\CodeWF.LogViewer可参考用法，用于操作日志展示，默认展示在状态栏上方，高度150~200，便于使用者了解git提交步骤学习、关键操作记录，菜单Menu提供显示或隐藏日志栏，需要记录在App.config
17. 将国际化也放菜单Menu提供切换，需要记录在App.config
18. 添加Semi 6、7种主题切换，也放菜单Menu提供切换，需要记录在App.config，参考D:\github\Vex
19. 一个代码文件不要写太长，特别是6、700行这行，要按逻辑拆分UserControl及ViewModel，尽量不要用partial，拆分要有实际意义，也不能为了拆分耍拆分
20. git diff展示使用CodeWF.Markdown NuGet包(D:\github\CodeWF.Markdown)，如果展示效果不好，你可以修改本地仓库，然后通过本地打包的方式引用，我验证会上传CodeWF.Markdown到NuGet平台
21. 桌面运行了Github Desktop程序，你可运行它，截取我们正在做的功能截图与它对比，但主要功能和样式还是参考它的源码D:\github\desktop
22. 尽量做到使用AvaGithubDesktop软件替换git功能，常用git命令都能在当前软件上实现，且不需要安装git
23. 开发过程中将功能点简单列在本文档中，不列详细，只简单列功能，整个需求文档你按实际开发调整，按正常人可理解的完善

## 功能点

- 仓库打开与 Git 状态概览
- Desktop 风格 Changes 文件选择与提交面板
- Desktop 风格 History 提交列表与提交文件清单
- Desktop 风格选中文件文本 Diff 预览
- 本地分支列表与分支 Checkout
- Desktop 风格 Fetch/Pull/Push 远端同步入口
- Desktop 风格 Changes 文件过滤
- 版本号、更新日志与开发日志
- 版本号集中维护在根目录 Directory.Build.props；第一位用于架构级调整，第二位用于功能新增或修改，第三/四位用于日常开发迭代与修复
- Desktop 风格 Current Branch 分支弹出选择器与分支过滤
- Desktop 风格 Stash all changes、Stashed Changes、Restore stash、Discard stash
- 使用 CodeWF.Markdown 展示更新日志与关于信息
- Desktop 风格 Current repository 仓库选择器、最近仓库和仓库过滤
- Desktop 风格 Repository 菜单当前仓库终端与文件管理器入口
- Desktop 风格仓库选择器列表项右键菜单
- Desktop 风格仓库复制名称/路径与 View on GitHub 操作
- `Directory.Build.props` 使用统一版本变量驱动 `Version`、`FileVersion` 等属性
- Desktop 风格 Changes 文件列表复制相对路径与文件管理器显示操作
- Desktop 风格 Changes 文件列表丢弃更改与确认对话框
- Desktop 风格 Changes 列表空白区域丢弃所有更改与贮藏所有更改菜单
- Desktop 风格 Changes 文件项复制完整文件路径
- Desktop 风格 Changes 文件项在外部编辑器中打开
- Desktop 风格 History 选中提交复制完整 SHA
- Desktop 风格 History 提交文件项右键菜单
- Desktop 风格 Repository 菜单和仓库列表项打开外部编辑器
- Desktop 风格 GitHub 账号登录入口、账户状态展示和退出登录
- 软件专属 Logo 资产与应用图标
- 弹窗按钮交互使用 MVVM 命令绑定，业务逻辑保持在 ViewModel
- GitHub OAuth Client ID 存放在 `App.config`，通过 `CodeWF.Tools.Files` 读取
- GitHub 登录使用浏览器 OAuth Device Flow
- 使用 CodeWF.LogViewer 展示操作日志栏并持久化显隐状态
- 菜单提供语言切换并将当前语言写入 App.config
- Desktop 风格统一 Diff 预览，支持行号、区块、增删行分色
- 操作日志栏级别与菜单文本支持 i18n
- 菜单提供 Semi 主题切换并将当前主题写入 App.config
- Desktop 风格图片与二进制文件 Diff 预览
- Desktop 风格未跟踪文本文件新增 Diff 预览
- Desktop 风格 Branch 菜单与 Current Branch 弹出层创建分支
- Desktop 风格分支右键菜单、重命名分支和删除本地分支
- 左侧仓库列表、文件列表和分支列表文字对比度优化
- Diff 预览 Git 输出统一 UTF-8 解码，支持中文注释和中文内容正常显示
- Desktop 风格分支右键菜单支持打开 GitHub 分支页面
- Desktop 风格未发布分支 Publish branch 同步入口
- Desktop 风格 Branch 主菜单支持打开当前 GitHub 分支页面
- Desktop 风格 Branch 主菜单支持 Compare on GitHub
- Desktop 风格 Branch 主菜单支持 Merge into current branch
