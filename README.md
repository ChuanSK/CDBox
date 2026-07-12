# CDBox Studio Preview

CDBox 是面向 AutoCAD 2023 / CASS11 的 x64 .NET Framework 4.8 插件。主程序集提供经典工具箱、停靠侧栏和 WebView2 Studio，`CDBoxUpdater` 是等待 AutoCAD 退出后替换 `CDBox.bundle` 的独立更新器。

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
- `bin\Debug\net48\Updater\CDBoxUpdater.exe`
- `bin\Debug\net48\Templates\工程量计算表模板.xls`

不要把 AutoCAD 的 `AcMgd.dll`、`AcDbMgd.dll`、`AcCoreMgd.dll` 复制到发布目录；项目引用已设置为 `Private=false`，运行时由 AutoCAD 宿主提供。

## 核心逻辑测试

核心测试项目不依赖 AutoCAD 进程，也不需要下载第三方测试框架。它通过链接正式源码验证工程量属性规则、Studio 路由消息和 Studio 状态行为：

```powershell
dotnet run --project tests\CDBox.CoreTests\CDBox.CoreTests.csproj -c Debug
```

任一断言失败时进程返回非零退出码，适合放入后续 CI。涉及 AutoCAD Database、Editor、WebView2 或 CASS 的流程仍需宿主内集成测试和人工回归。

## 生成发布包

发布脚本会构建 Release、暂存 `CDBox.bundle`、生成 ZIP，并根据实际文件自动写入大小和 SHA256：

```powershell
.\scripts\New-CDBoxRelease.ps1 -Notes "完成插件更新系统","新增工程量动态看板"
```

Preview 版本身份、版本码、标题、包名和 AutoCAD AppVersion 统一定义在 `CDBox.csproj`。产物默认写入 `artifacts`。下载源模板位于 `scripts\update-sources.json`；正式发布前必须确认各平台的 release/tag 规则与模板一致。脚本会拒绝下载 URL 末尾文件名与实际 ZIP 不一致的清单。

## 本地加载

在 AutoCAD 2023 中执行 `NETLOAD`，选择 `bin\Debug\net48\CDBox.dll`。常用入口：

- `CDSTUDIO` / `CDS`：WebView2 Studio
- `CDBOX`：经典工具箱
- `CDCBL`：停靠侧栏
- `TCGL`：图层管理器
- `BZSZ`：统一标注设置
- `SX`：工程量属性编辑器
- `GCL`：正式工程量表

完整的宿主内验证步骤见 [`docs/manual-regression.md`](docs/manual-regression.md)。

## 代码入口

- AutoCAD 命令入口：`CDBox_Integrated\Commands\TCPipeCommands.cs`
- 模块注册：`CDBox_Integrated\Core\Modules\TCModuleRegistry.cs`
- Studio 宿主和路由：`CDBox_Integrated\UI\Studio`
- 业务模块：`CDBox_Integrated\Modules`
- 安装逻辑：`CDBox_Integrated\Core\Startup`
- 独立更新器：`CDBoxUpdater`

## 数据位置

图纸相关数据保存在 DWG 内：工程量属性和图层元数据使用 ExtensionDictionary/Xrecord，统计区域使用多段线 XData。用户级设置、日志、识别规则和工程量看板缓存保存在 `%APPDATA%\CDBox`。

开发时不要随意修改已经写入图纸的 Xrecord 键名或字段语义；如需演进格式，应先设计向后兼容和迁移方案。
