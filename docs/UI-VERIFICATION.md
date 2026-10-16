# 原型与 NativeAOT 复核

2026-10-16，版本 0.118.0.19：修复提交按钮和图片对比模式按钮的配色。实际模板检查发现，固定白字遇到深色配色的浅色强调背景、选中模式遇到悬停背景时对比不足；提交操作改用 Semi SolidButton，图片模式分别定义普通选中、悬停与按下的成对资源。八配色的浅深组合、四种启用状态共 128 项实际文字与背景检查通过，并检查禁用及恢复。单文件原型同步主操作配色和版本，浏览器实际悬停、按下、恢复的 128 项对比通过；28 项布局区域最小 IoU 为 96.43%，这只表示区域几何，不能换算为整页还原率。

验证器在应用初始化前隔离配置，配色入口只创建真实按钮和图片对比模板，不访问用户账号或仓库。0.118.0.19 已执行正式 Windows x64 NativeAOT 发布脚本；实际发布 EXE 的双语资源、全局切语和隔离偏好自检通过，PE 的 CLR 目录 RVA 与大小均为 0。独立 NativeAOT UI 验证器已实际运行，真实 Git 工作区、中英各三种窗口尺寸、最小历史及关于窗口、后台切语与活跃 Markdown 帮助窗均通过；原生按钮的 128 项 normal / hover / pressed / reenabled 对比全部通过，最低为 4.590:1，另检查禁用和恢复。

收尾时重新逐文件核对发布前冻结清单：191 个生产及验证源文件 SHA256 均保持一致，正式 EXE SHA256 为 `0800a430b3c60efe3bde01e15351488fc805e9c1662fa667f3fcf02a6c6255e4`。README 四张 PNG 已替换为本轮原生离屏结果；配色巡览 GIF 保留 0.118.0.18 历史版本并明确标注。此前已生成 0.118.0.19 安装器，但安装器仍在独立收尾，本记录不把它视作最终交付或真实安装验收。

本轮还原和发布报告 `Microsoft.Build.Tasks.Git 10.0.301` 的 NU1902 警告（[GHSA-23fw-v26w-5fgq](https://github.com/advisories/GHSA-23fw-v26w-5fgq)）；当前未消除此项依赖告警。Prism 对话框反射绑定、Markdown、Ursa 与 SVG 等仍有裁剪或 AOT 分析警告，已通过的实际路径不能代表所有反射路径安全。Windows 人工键鼠、多 DPI、跨屏及真实安装，Linux/macOS 实机 UI 仍未验证。

```powershell
dotnet run --project scripts/ui-verification/UiVerification.csproj -- --palette-audit E:/github/temp/ava-palette
node design/tests/audit-palette.cjs
```

2026-10-16，版本 0.118.0.18：补查发现全局语言与标题栏下拉不同步，以及合法长分支名撑出提交按钮，均已修复。后台语言回归同时保持真实 Markdown 帮助窗打开，使用同一个实际下拉反向切回；升级 CodeWF.Avalonia 12.1.4.24 修复活跃查看器的跨线程更新。长分支来自临时真实 Git 仓库，检查按钮内部文本的几何边界、省略和完整提示；原型同步该约束并在最小窗口下审计。

旧验证器在 `SetupWithoutStarting` 后才替换历史与账号服务，Prism 可能已创建持有真实服务的外壳。本轮改为在 `RegisterTypes` 中提前注入，避免触达真实历史；不能沿用旧文档中“仅因启动后注册替身即可保证隔离”的假设。回归还要求提交与暂存按钮恰好两项、历史操作恰好五项，避免空集合产生假阳性。

0.118.0.18 已使用官方 NuGet 源及独立包缓存重新通过正式 Windows NativeAOT 发布、正式 EXE 隔离自检、PE 空 CLR 目录检查、EXE 安装器生成及独立 NativeAOT UI 完整验证。新增后台切语与同一下拉反向同步、活跃 Markdown 帮助窗、真实长分支内部文字边界，以及全部提交和历史操作数量断言，均已在最终原生验证器执行。README 的四张 PNG 与配色巡览 GIF 已替换为本轮原生结果并逐图查看；GIF 为四个实际视图的轮播，不是桌面交互录像。验证前后真实仓库历史文件的 SHA256 保持一致，本轮没有执行真实安装。

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
