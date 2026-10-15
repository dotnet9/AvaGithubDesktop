# 原型与 NativeAOT 复核

2026-10-16，版本 0.118.0.17 补充：修复全局语言切换与第三方主题接线。验证器直接修改唯一 I18nManager，在同一个已打开的 Semi TextBox 上反复展开实际菜单，检查中英文复制、粘贴文案及 Ursa 对话框资源；宿主设置同键文案后继续回切，覆盖保持生效。主题字典保存在主题自身资源中，事件切换在 UI 线程执行。

追加最小窗口内容回归后发现并修复 960×620 下暂存与提交按钮越界：限制日志实际高度，保持用户设置的首选高度；侧栏和历史文件列根据实际空间约束最大宽度，差异区保留可滚动空间。中英 Changes 1440×900、1120×900、960×620 六个布局均检查提交/暂存按钮及至少一行文件内容，History 最小窗口检查两个分栏；关于 560×450 检查更新按钮及滚动后的关闭按钮。验证显式打开隔离 Git 仓库，避开初始 Prism 外壳的异步初始化竞态。

逐图检查又确认了历史操作栏右侧按钮被裁剪，现改为可换行布局并检查全部五个按钮。最小窗口注入 256 字符摘要和 80 行提交描述，历史标题注入长文本，验证按钮与分栏继续处于工作区内；描述框保留固定高度并内部滚动，历史标题最多两行且提示显示全文。原型同步这些约束，并新增最小窗口提交区、历史操作及长标题的浏览器门禁。

0.118.0.17 最终验证使用官方 NuGet 独立缓存完成：正式发布脚本、正式 EXE 隔离资源与偏好自检、PE 无 CLR 目录检查、本机 EXE 安装包构建，以及单独编译运行的 NativeAOT 真实界面验证器全部通过。上述六种工作区布局、最小历史按钮、长描述滚动和最小关于窗口均已在最终原生验证器执行；README 的 PNG 和配色巡览 GIF 由本轮原生离屏图生成，GIF 是视图巡览，不是桌面录像。单文件原型离线审计通过，28 个测量区域的几何对照最低 IoU 为 96.43%，不代表全页像素还原率。

2026-10-15，版本 0.118.0.16。本轮使用固定布局和临时 Git 仓库验证，不读取用户账号、仓库或最近历史，不操作桌面。

- 标题栏高 44px、窗口按钮宽 34px。外观入口为单排八色点、Ursa 三态模式按钮和语言图标下拉，删除重复配色与模式下拉。实际点击内部按钮验证八色各三态、动态提示、语言菜单；中英及 1440/1120 两种宽度检查标题和窗口按钮无重叠。
- 临时目录创建真实 Git 仓库，提交两个文件，修改后校验 Changes、README 的红绿差异和 History。账号服务注入空白测试实现，避免调用用户登录或网络认证。
- 验证八色浅深组合、双语关于、实际程序集版本/编译时间/平台及偏好恢复。更新状态机 16 个受控场景覆盖取消、重试、缺资产、下载和安装分支，不执行真实安装器。
- 执行 `scripts/publish.ps1 -RuntimeIdentifier win-x64` 发布正式程序；将 `scripts/ui-verification/UiVerification.csproj` 单独发布为 NativeAOT 并运行。README 截图来自此原生验证器绘制的真实窗口。
- 单文件离线原型覆盖 Changes / History、仓库与分支工具栏、Diff、提交区和日志，标题栏三入口与实现同步，包含桌面窗框和固定关于样例时间。
- 五平台发布使用 EXE / DEB / DMG 加 SHA256。包装门禁检查 macOS Resources、Linux 桌面入口与图标、失败传播和资产完整性，禁止便携 ZIP。

```powershell
dotnet run --project scripts/ui-verification/UiVerification.csproj -c Release -- ./artifacts/ui-verification
dotnet run --project scripts/update-verification/UpdateVerification.csproj -c Release
pwsh scripts/publish.ps1 -RuntimeIdentifier win-x64
dotnet publish scripts/ui-verification/UiVerification.csproj -c Release -r win-x64 -p:PublishAot=true -p:IlcGenerateCompleteTypeMetadata=true -p:IlcTrimMetadata=false -p:IlcSingleThreaded=true -o ./artifacts/native-ui
./artifacts/native-ui/UiVerification.exe ./artifacts/native-ui-captures
```

正式 EXE 另提供 `--verify-runtime <独立目录>` 自检：逐一检查发布的语言 JSON、全局动态切语与独立偏好持久化，不创建窗口，不读取用户账号或仓库。该自检验证实际发布的可执行文件，UI 验证器负责真实控件与布局；两者都不替代人工桌面验收。

宿主 JSON 输出至 `I18n/`，强类型键由 T4 生成；库资源内嵌并由 Lang 提供同键覆盖。正式工程仅引用 NuGet 包，不提交本地源或跨仓项目引用。

离屏验证不等于人工桌面验收，局部几何和交互覆盖也不等于全页 95% 还原。Prism 默认对话框、运行时 XAML 加载器、Ursa/SVG 等仍报告裁剪与 AOT 警告，本轮仅确认实际调用路径。鼠标键盘、IME、多 DPI、跨屏、真实安装过程及 Linux/macOS UI 仍需对应环境补充。
