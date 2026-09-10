# UI 基础组件归属（2026-09-07）

本阶段完成现有 UI 的物理归并，为后续界面结构、风格和主题配色重构建立唯一实现位置；不在此次迁移中重新设计页面布局。

后续首个外观试点已在宗地调查数据编辑器接入，范围、主题兼容及验证见 [宗地工作台外观试点](parcel-workbench-pilot.md)。其他业务页面继续沿用迁移后的原外观。

## 模块边界

| 组件 | 负责内容 |
| --- | --- |
| 基础组件 `CDBox.dll`（必装） | 页面 HTML/CSS/JavaScript、页面控制器、WebView2 宿主、WinForms/WPF 窗口、浮窗、文件选择、颜色选择器、动画、主题配置及窗口位置保存 |
| `CDBox.Shared.dll`（必装） | 数据模型和 UI 调用契约，不包含页面、控件或独立主题实现 |
| `CDBox.Common.dll` | 公共功能的 CAD 操作、导入、计算和业务数据 |
| `CDBox.Wastewater.dll` | 污水计算、属性持久化、检查同步、成果生成及 CAD 业务交互 |
| `CDBox.RealEstate.dll` | 宗地数据、几何识别、调查业务及成果导出 |

业务模块保留的图形文字颜色、图层样式、CAD 坐标类型和 Word/Excel 成果格式属于业务成果数据，不属于界面主题或控件实现。业务可以请求显示页面、输入或通知，但不得自行创建窗口、拼接页面或维护主题配置。

## 后续 UI 开发位置

- 本次迁移的公共、不动产和污水展示层：`CDBox_Integrated/UI/Business/Common`、`RealEstate`、`Wastewater`。
- 污水标注浮窗与断面原生窗体：`CDBox_Integrated/UI/Business/Wastewater/Annotation`、`SectionDrawing`。
- 已有统一宿主、页面框架和主题设置继续位于基础项目的 `CDBox_Integrated/UI/Studio` 等 UI 目录。
- 共用消息框和文件选择入口：`CDBox_Integrated/UI/Business/CDBoxCommonDialogs.cs`。
- 唯一 UI 调度入口：`CDBox_Integrated/UI/Business/CDBoxUiDispatcher.cs`。
- 业务请求契约：`CDBox.Shared/UI/CDBoxUiGateway.cs`。

迁移文件暂保留原来的 C# 命名空间，以降低行为变化风险；命名空间不代表程序集归属，所有这些 UI 文件仅编译进 `CDBox.dll`。Common/Wastewater 原先复制的外观配置、消息框和颜色选择实现已移除，改用基础实现。

## 可选安装与加载

基础项目在编译时引用业务类型，业务项目仅通过 Shared 契约调用 UI，不反向引用基础程序集。污水历史同名类型通过程序集别名隔离，避免把基础兼容类型误用作污水业务状态。

运行时采用固定白名单按需解析展示层类型，页面会话延迟创建；基础启动不遍历可选业务的展示层类型。CAD 扩展与命令入口使用显式程序集属性声明，避免宿主扫描全部展示类。未安装的业务仍不注册对应业务入口，不要求安装其 DLL。安装器继续使用原有必装基础组件及三个可选模块，无新增 UI 安装选项。

## 验证与防回退

```powershell
dotnet build CDBox.csproj -c Release
dotnet run --project tests/CDBox.CoreTests/CDBox.CoreTests.csproj -c Release --no-restore
./tests/Test-UiArchitecture.ps1
./tests/Test-UiModuleIsolation.ps1
```

架构检查覆盖业务目录和项目显式链接的源文件，拒绝 UI 框架依赖、页面实现及独立 HTML/CSS/JS/XAML 资源。核心测试检查实际程序集归属和调用端口。隔离测试在独立进程中验证三个可选模块的全部 8 种安装组合，检查基础主题及所安装业务的代表页面生成，不替代 CAD 内的窗口交互测试。这些检查已加入 CI。

输出位于 `bin/Release/net48`；替换测试时应使用同次构建的基础及业务 DLL，不能只替换其中一个程序集。迁移后的旧版业务 DLL 与新版基础 DLL 不应混用。

人工回归应覆盖：CAD 启动与菜单、图层管理器、公共工具页、宗地调查与界址浮窗、污水属性/工程量/标注设置、双击标注浮窗、纵断面与批量断面、主题切换及浮窗关闭重开。
