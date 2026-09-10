# CDBox 5.1.1：设置工作台与主题系统

日期：2026-09-07。

后续：2026-09-08 已按要求移除外观页示例预览块并重构公共业务界面，见 [公共业务工作台重构](common-workbench-2026-09-08.md)。以下保留本次原始构建记录。

## 本轮范围

已采纳的宗地调查数据编辑器正式编译到基础组件与 5.1.1 安装器，包括整高导航、自动保存、表格按宽度隐藏次要列、自动/默认字段阅读态、分组折叠和房屋主从编辑。CDBox 设置页按同一工作台风格重构。通用、污水等其他界面的结构本轮不改。

设置页使用左侧四类导航：外观、悬浮球与浮窗、绘图与交互、安装与更新。旧主页面的设置入口也指向新版独立设置窗口。设置行统一右侧操作，组间分隔，不再嵌套大卡片；主题编辑器保留与参考截图对应的一层分组。

## 主题能力

- 浅色、深色分别存储。可选择 Codex、GitHub、Solarized、Nord、One 五组预设，共十个浅深配色。
- 参考截图的 GitHub 深色采用强调色 `#1F6FEB`、背景 `#0D1117`、前景 `#E6EDF3`；Codex 保留已采纳编辑器的中性默认底色。
- 支持强调色自定义/跟随前景、原生颜色选择器和 HEX 输入、UI 字体、内容字体、各自字重、0—100 对比度。对比度控制分隔线和控件边界强度，背景和前景仍以输入色值为准。
- 内容字体有独立预览；原生标题栏跟随颜色。字体使用本机常见字体及回退字体，不下载额外字体。
- 导入/复制使用 `cdbox-theme-v1` JSON，包括当前模式和完整主题配置。导入应用于当前编辑模式，另一模式保持不变；这是 CDBox 主题格式，不承诺兼容 Codex 的分享字符串。
- 改动即时预览、自动保存；连续修改串行合并，失败显示重试。HEX、字体、字重、对比度有前后端校验。设置 XML 通过临时文件原子替换，写入失败保留原有效文件。
- 新令牌系统当前接入设置页和宗地编辑器。设置保存后，已打开的这两个工作台只更新颜色/字体，不刷新宗地数据；其余旧页面继续沿用原有主题实现，后续逐页接入公共系统。

## 代码与兼容性

全部界面和主题仍在基础组件 `CDBox.dll` 中：

- `CDBoxThemeProfile.cs`：浅深配置、预设、规范化、字体白名单和颜色派生。
- `CDBoxWorkbenchAppearance.cs` / `CDBoxWorkbenchThemeScript.cs`：共享 CSS 令牌与不刷新页面的主题更新。
- `CDBoxStudioSettingsPage.cs`：新版设置布局和交互。
- `CDBoxStudioSettingsStore.cs`：XML 版本 7，新增 `LightTheme` / `DarkTheme`，旧配置自动补默认值，保留旧业务开关。
- `CDBoxStudioCommandRouter.cs` / `CDBoxStudioWebPageForm.cs`：主题保存回执、复制主题和已打开工作台的同步。

安装器仍提供原有组件组合与安装/卸载流程，不自动结束用户的 CAD 进程。打包脚本补充 UTF-8 BOM，以兼容 Windows PowerShell 的中文解析。

## 构建与验收

- `CDBox.csproj` 以 `Release511` 配置、优化开启进行独立正式构建，输出 `bin/Release511/net48`，0 警告、0 错误，避免覆盖正在被 CAD 使用的旧 DLL。
- 核心测试：61 通过、0 失败，包含主题 XML 迁移、浅深配置往返、非法输入防护、写入失败保持配置。
- 从同次正式 DLL 生成浏览器页面：设置页 133 项通过；宗地编辑器 421 项通过。
- 所有 8 种模块安装组合检查通过，UI 归属检查通过。
- 安装器内嵌文件自检通过。再次读取其 41 个内嵌文件，核对五个核心/业务 DLL 和三份宗地模板 SHA-256，全部与验证过的正式构建一致。安装器、包清单版本均为 5.1.1。
- 浏览器使用示例数据和模拟宿主回执；真实 CAD 中的跨窗口同步、原生交互和安装后运行还未在本轮执行。本轮没有上传发布站点。

## 交付

- 安装器：`artifacts/installer/5.1.1/CDBoxInstaller-5.1.1.exe`
- 大小：9,125,888 字节（约 8.70 MiB）
- SHA-256：`F0A1FFF685A85E78CB8117A8DD0A07167CD626EDB5624A90B1096F41C36B2E8E`
- 校验文件：`artifacts/installer/5.1.1/CDBoxInstaller-5.1.1.exe.sha256`
- 截图、编译、浏览器及安装器校验记录：`artifacts/settings-workbench/`
- 设置交互预览：`http://127.0.0.1:8766/`

复现：先编译 `dotnet build CDBox.csproj -c Release511 --no-restore -m:1 -p:BuildInParallel=false -p:Optimize=true -p:DebugType=pdbonly`。打包使用 Windows PowerShell，在同一进程中预加载当前 AutoCAD 的 AcDbMgd、AcMgd、AcCoreMgd 程序集，再执行 `scripts/New-CDBoxRelease.ps1 -Configuration Release511 -SkipBuild -ReleaseVersion 5.1.1 -InstallerVersion 5.1.1`。版本目录中的安装器不可覆盖为不同内容；后续改动须使用新版本或新的产物目录。
