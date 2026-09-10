# CDBox Studio Preview

CDBox 是面向 AutoCAD / CASS 的 x64 .NET Framework 4.8 插件。主程序集提供经典工具箱、停靠侧栏和 WebView2 Studio；统一安装器负责首次安装、版本更新、业务模块增减和完整卸载。

UI 页面、浮窗、主题和控件实现统一归属必装基础组件；可选业务模块只保留业务处理与展示请求。后续 UI 开发边界及验证方式见 [UI 基础组件归属](docs/ui-base-migration.md)。

插件更新现已改为查询公开版本服务并引导官网，不再读取版本清单或下载安装器。真实服务地址与返回契约仍待确认，配置及对接约定见 [插件版本服务接入说明](docs/plugin-release-service.md)。

本轮图层管理器、属性识别表和建筑边长注记设置重构见 [5.1.9 交付说明](docs/release-5.1.9.md)。房屋计算与导出说明见 [房屋面积使用说明](docs/building-area-5.1.8.md)。

## 开发环境

- Windows x64
- AutoCAD 2023；默认安装目录为 `C:\Program Files\Autodesk\AutoCAD 2023`
- Visual Studio 或可构建 `net48` 的 .NET SDK/MSBuild
- Microsoft Edge WebView2 Runtime（运行 Studio 时需要）
- CASS11（验证表面积标注正式流程时需要）

AutoCAD 不在默认目录时，不要修改项目文件，改用：

```powershell
dotnet build CDBox.sln -c Debug -p:AutoCADManagedDir="D:\Autodesk\AutoCAD 2023"
```

## 构建

首次构建允许 NuGet 还原依赖：

```powershell
dotnet restore CDBox.sln
dotnet build CDBox.sln -c Debug --no-restore
```

成功后主要产物位于：

- `bin\Debug\net48\CDBox.dll`
- `CDBoxInstaller\bin\Debug\net48\CDBox安装器.exe`（开发构建不含正式内嵌载荷）
- `bin\Debug\net48\Templates\工程量计算表模板.xls`

不要把 AutoCAD 的 `AcMgd.dll`、`AcDbMgd.dll`、`AcCoreMgd.dll` 复制到发布目录；项目引用已设置为 `Private=false`，运行时由 AutoCAD 宿主提供。

## 核心逻辑测试

核心测试项目不依赖 AutoCAD 进程，也不需要下载第三方测试框架。它通过链接正式源码验证工程量属性规则、Studio 路由消息和 Studio 状态行为：

```powershell
dotnet run --project tests\CDBox.CoreTests\CDBox.CoreTests.csproj -c Debug
```

任一断言失败时进程返回非零退出码，适合放入后续 CI。涉及 AutoCAD Database、Editor、WebView2 或 CASS 的流程仍需宿主内集成测试和人工回归。

仓库已配置 `.github/workflows/core-verification.yml`：每次 push、Pull Request 或手动触发时，使用 Windows runner 构建统一安装器并运行 Release 核心测试。该流程不需要安装 AutoCAD；通过 Autodesk 发布的 `AutoCAD.NET 24.2.0` NuGet 包获取编译引用，且不会把这些引用打包进 CDBox。宿主相关验证仍按下述脚本和人工回归清单执行。

AutoCAD 2023 Core Console 宿主冒烟测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-CDBoxCoreSelfTest.ps1 -Configuration Release
```

脚本只读取复制后的 Autodesk 示例图纸，并把日志保存在 `artifacts\host-selftest`。成功标志为 `CDBOX_SELFTEST_RESULT=PASS`。

启动可交互的 AutoCAD Studio 冒烟测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-CDBoxInteractiveSmoke.ps1 -Configuration Release
```

该脚本会打开复制后的示例图纸、加载插件并执行 `CDSTUDIO`，AutoCAD 保持打开以供人工检查。

## 生成发行安装器

后续发布统一以 [`CDBox 发布渠道与 update.json 调整方案`](开发文档/CDBox%20发布渠道与%20update.json%20调整方案.md) 为准；旧发布说明如有冲突，以该方案和本节 V2 流程为准。

发布脚本会构建 Release、把完整 `CDBox.bundle` 作为安装器内部载荷，并根据最终 EXE 自动写入大小和 SHA-256，生成 Release Manifest V2：

```powershell
.\scripts\New-CDBoxRelease.ps1 -Channel preview -Summary "CDBox 5.1.0 预发行版本。"
```

版本身份、版本码、标题、安装器文件名、AutoCAD AppVersion 和默认更新通道统一定义在 `CDBox.csproj`。发布渠道域名配置位于 `scripts\release-config.json`，正式数据契约位于 `scripts\update.schema.json`。生成器使用强类型模型，自动读取安装器文件名和大小、计算 SHA-256，并校验 Schema V2、网站字段契约、HTTPS 官方域名和统一 URL 规则；任何校验失败都会中止发布。

默认产物结构如下：

- `artifacts\installer\5.1.0\CDBoxInstaller-5.1.0.exe`：客户端更新使用的不可变版本安装器；
- `artifacts\installer\latest\CDBoxInstaller.exe`：官网“立即下载”使用的 Latest 安装器；
- `artifacts\releases\preview\update.json`：Preview 唯一发布清单；
- `artifacts\schemas\update.schema.json`：随发布保存的清单结构定义。

腾讯云静态托管/CDN 是正式发行源。发布时必须先上传并验证版本安装器，再更新 Latest 安装器，最后上传 `update.json`；Manifest 是发布开关，不得先于安装器公开。GitHub、Gitee 和 GitCode 只用于源码、历史归档、灾备或旧客户端兼容，不再作为新客户端的默认下载源。Stable 与 Preview 使用独立清单，发布时通过 `-Channel stable` 或 `-Channel preview` 明确选择。

仓库根目录和 `CDBox发布` 中既有的 4.1.1 `update.json` 暂时保留为旧客户端兼容清单，不再由新生成器覆盖。5.1.0 及后续正式清单只从上述 V2 流程生成。发布脚本会将所选通道传入插件构建，并拒绝发布版本号或通道不一致的旧构建产物。

只验证 Manifest 生成与契约时可运行：

```powershell
.\tests\Test-ReleaseManifest.ps1
```

## 本地加载

在 AutoCAD 2023 中执行 `NETLOAD`，选择 `bin\Debug\net48\CDBox.dll`。常用入口：

- `CDSTUDIO` / `CDS`：WebView2 Studio
- `CDBOX`：经典工具箱
- `CDCBL`：停靠侧栏
- `TCGL`：图层管理器
- `BZSZ`：统一标注设置
- `SX`：工程量属性编辑器
- `SXLEGACY`：旧版 WinForms 工程量属性编辑器
- `GCL`：正式工程量表
- `CDQBOARD`：工程量动态看板独立窗口
- `CDSELFTEST`：只读宿主环境自检

完整的宿主内验证步骤见 [`docs/manual-regression.md`](docs/manual-regression.md)。
最近一次自动宿主验证记录见 [`docs/host-validation-2026-07-12.md`](docs/host-validation-2026-07-12.md)。

## 代码入口

- AutoCAD 命令入口：`CDBox_Integrated\Commands\TCPipeCommands.cs`
- 模块注册：`CDBox_Integrated\Core\Modules\TCModuleRegistry.cs`
- Studio 宿主和路由：`CDBox_Integrated\UI\Studio`
- 业务模块：`CDBox_Integrated\Modules`
- 安装、更新、模块管理与卸载：`CDBoxInstaller`
- CAD 内网络检查及安装器启动：`CDBox_Integrated\UI\Studio`

## 数据位置

图纸相关数据保存在 DWG 内：工程量属性和图层元数据使用 ExtensionDictionary/Xrecord，统计区域使用多段线 XData。用户级设置、日志、识别规则和工程量看板缓存保存在 `%APPDATA%\CDBox`。

开发时不要随意修改已经写入图纸的 Xrecord 键名或字段语义；如需演进格式，应先设计向后兼容和迁移方案。
