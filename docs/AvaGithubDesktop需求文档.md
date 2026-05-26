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
16. 开发过程中将功能点简单列在本文档中，不列详细，只简单列功能

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
