# CDBox Studio Preview 8–10 界面路线

## 版本顺序

1. Preview 8 前置收口：工程量动态看板增加独立 WebView2 窗口；Studio 内嵌页和独立页复用 `CDBoxStudioQuantityDashboardPage`、同一套路由与实时数据源。
2. Preview 9：属性编辑器界面。已建立共享页面、Studio 内嵌页和独立窗口；`SX` 进入新版，`SXLEGACY` 保留旧 WinForms 兜底。桌面验收通过后再切换正式发布身份。
3. Preview 10：断面图生成界面。保留现有 `DM` / `PLDM` 计算与绘图服务，界面层采用与 Preview 9 相同的共享组件双宿主结构。

## 统一约束

- 一个功能只维护一份页面组件、样式和前端交互逻辑；内嵌页与独立页只负责提供宿主外壳。
- AutoCAD 数据访问和绘图逻辑继续留在 API / Service 层，不写入 HTML 生成器或窗口类。
- 独立窗口统一使用 `CDBoxStudioWebPageForm`，继承 Studio 主题、动画、圆角、拖动、缩放、最小化、关闭和 WebView2 错误兜底。
- 每个版本先补核心可测边界，再执行 Release 构建、核心测试、Core Console 自检和对应人工回归。
- Preview 9 / Preview 10 的正式版本身份只在各自功能达到可验收状态后统一更新，避免开发中途出现身份与能力不一致。
