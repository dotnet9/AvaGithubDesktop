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

开发过程中将功能点简单列在本文档中，不列详细，只简单列功能

## 功能点

- 仓库打开与 Git 状态概览
- Desktop 风格 Changes 文件选择与提交面板
- Desktop 风格 History 提交列表与提交文件清单
