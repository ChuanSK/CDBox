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

自动化执行会话中的首次 GUI 启动曾停在 Autodesk 自身的授权/初始化阶段，因此仅终止了该次测试创建的后台 AutoCAD 进程。随后从用户桌面会话正常启动 AutoCAD 2023，并完成交互式验证。

用户确认以下项目工作正常：

| 交互检查项 | 结果 |
|---|---|
| CDBox Studio 窗口显示与 WebView2 初始化 | PASS |
| 图层管理功能 | PASS |
| 标注相关功能 | PASS |
| 工程量动态看板 | PASS |
| 其他已检查的 Studio 页面与入口 | PASS |

至此，Preview 6 已同时通过核心逻辑测试、AutoCAD Core Console 只读宿主自检和桌面 AutoCAD 交互式验证。后续针对具体业务算法的修改，仍应按 `docs\manual-regression.md` 对受影响模块做定向回归。
