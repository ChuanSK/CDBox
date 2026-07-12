# CDBox Preview 6 宿主验证记录（2026-07-12）

## 环境

- Windows x64
- AutoCAD 2023：`R24.2.53.0.0`
- AutoCAD Core Console：`24.2.53.0.0`
- CASS11 For AutoCAD 2023：已安装
- WebView2 Runtime：`150.0.4078.65`
- CDBox：`2.6.0-studio-preview.6 / 20600`

## 自动验证结果

| 检查项 | 结果 |
|---|---|
| Release 构建 | PASS，0 警告、0 错误 |
| 核心逻辑测试 | PASS，9/9 |
| AutoCAD Core Console 加载 `CDBox.dll` | PASS |
| Preview 6 发布身份与版本码 | PASS |
| WebView2 Core / WinForms / Loader 文件 | PASS |
| WebView2 Runtime 探测 | PASS |
| 独立更新器文件 | PASS |
| 工程量 Excel 模板 | PASS |
| 示例 DWG 只读数据库事务 | PASS，图层数 2 |
| Core Console 自检汇总 | `CDBOX_SELFTEST_RESULT=PASS`，12/12 |

完整输出位于忽略目录：`artifacts\host-selftest\CDBoxSelfTest.log`。

## 交互式启动结果

自动脚本成功创建隔离图纸并启动 `acad.exe`，但当前自动化执行会话中的 AutoCAD 停在 Autodesk 自身的授权/初始化阶段，没有创建可见顶层窗口，也没有执行到插件 Studio 日志。确认进程无窗口后，仅终止了本次测试创建的 AutoCAD 进程；没有发现遗留的 `acad`、`accoreconsole` 或 `CDBoxUpdater` 进程。

这不属于插件加载失败：同一 Release DLL 已在 AutoCAD Core Console 中成功加载并完成全部宿主检查。可视化 Studio、CASS 命令和交互式 CAD 操作仍需从用户桌面正常启动 AutoCAD 后，按照 `docs\manual-regression.md` 继续验证。

## 下一步人工入口

1. 从桌面正常启动 AutoCAD 2023，确认 Autodesk 授权完成。
2. 执行 `NETLOAD`，选择 `bin\Release\net48\CDBox.dll`。
3. 执行 `CDSELFTEST`，确认命令行末尾为 `CDBOX_SELFTEST_RESULT=PASS`。
4. 执行 `CDSTUDIO`，检查总览、图层管理、标注设置、工程量看板和设置页。
5. 使用测试 DWG 完成 CASS 表面积标注、工程量属性、GCL 和实时看板回归。
