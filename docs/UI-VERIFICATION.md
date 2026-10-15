# 2026-10-15 原型与实现复核

本轮使用固定布局与真实 Git 数据验证，不读取用户仓库，不写用户最近历史，不操作桌面。原型保留 Changes / History、仓库与分支工具栏、Diff、提交区和日志的功能层次，主题模式与配色独立。

- 主窗口标题栏 44px、窗口按钮 34px，八色圆点与配色下拉共用状态；中英文 1440×900、1120×900 两种宽度均渲染检查。
- 临时目录创建真实 Git 仓库：提交两个文件，修改后校验变更、README 差异和 History；配置和仓库历史均隔离。
- 16 套主题、中英关于窗口、真实程序集版本/编译时间/平台、主题配置保存恢复均通过。
- 更新状态机 16 个场景通过，包括取消、重试、缺资产、下载与安装分支；不执行真实安装器。
- `scripts/publish.ps1 -RuntimeIdentifier win-x64` 已通过；`scripts/ui-verification/UiVerification.csproj` 以 NativeAOT 发布并运行退出 0。README 截图取自此原生验证器绘制的实际窗口。

```powershell
dotnet run --project scripts/ui-verification/UiVerification.csproj -c Release -- ./artifacts/ui-verification
dotnet run --project scripts/update-verification/UpdateVerification.csproj -c Release
pwsh scripts/publish.ps1 -RuntimeIdentifier win-x64
dotnet publish scripts/ui-verification/UiVerification.csproj -c Release -r win-x64 -p:PublishAot=true -p:IlcGenerateCompleteTypeMetadata=true -p:IlcTrimMetadata=false -p:IlcSingleThreaded=true -o ./artifacts/native-ui
./artifacts/native-ui/UiVerification.exe ./artifacts/native-ui-captures
```

验证器与 App 发布产物分别记录，不把离屏验证描述为人工桌面验收。Prism 默认对话框、旧运行时 XAML 加载器、第三方 Ursa/SVG 等仍报告裁剪与 AOT 分析警告，本轮只确认上述实际调用路径。真实鼠标键盘、IME、多 DPI、跨屏及 Linux/macOS 运行仍需对应环境补充验证。

宿主 JSON 输出至 `I18n/`，资源键由 T4 生成；库资源内嵌并由 Lang 提供同键覆盖。正式工程只引用公开 NuGet 版本，临时源及缓存不进入仓库。
