# CDBox 3.2.0 Codex 接续说明

## 接续基线

- 仓库：`https://github.com/ChuanSK/CDBox`
- 默认分支：`CDBox-Studio-Preview`
- 源码版本：`3.2.0`
- 版本码：`30200`
- AutoCAD 目标版本：AutoCAD 2023 / .NET Framework 4.8
- Release 构建输出：`bin/Release/net48/CDBox.dll`

在另一台电脑继续开发时，先拉取默认分支并阅读：

1. `CDBox_设计思想_方向与开发规范.md`
2. `CDBox_工程量统计与属性编辑器实现逻辑_Codex.md`
3. `开发文档/CDBox_Annotation_HUD_Implementation_Spec.md`
4. `开发文档/CDBox 标注浮窗动画设计文档 v1.0.md`
5. `开发文档/CDBox图层管理与属性识别设计文档.md`
6. 本文档

## 发布状态

3.2.0 当前是源码接续版本，尚未创建 GitHub Release，也未生成或上传 3.2.0 安装包。

仓库根目录和 `CDBox发布/update.json` 继续保留已经发布的 3.1.1 信息，这是有意行为。正式发布 3.2.0 时需要：

1. 生成 `CDBox.Studio.Preview.3.2.0.zip`。
2. 计算压缩包实际大小和 SHA-256。
3. 更新两个 `update.json` 的版本、日期、文件名、大小、哈希和下载地址。
4. 创建 GitHub 预发行版并上传压缩包。
5. Gitee、GitCode 发布包由用户手动替换。

## 当前已知问题：双击重叠管线未弹出选择浮窗

### 当前表现

- 管长标注、重新绑定等流程已经具备重叠对象选择浮窗。
- 双击普通主管或支管可以打开属性编辑器。
- 双击完全或部分重叠的管线时，通常直接打开 CAD 选中的上层管线，未进入重叠选择浮窗。
- 属性编辑器关闭后重新双击目前能够继续打开，窗口生命周期和 CAD 焦点恢复已经处理。

### 相关代码

- 双击消息入口：`CDBox_Integrated/Modules/PipeLengthAnnotation/PipeLengthAnnotationInteractionService.cs`
  - `PreTranslateMessage`
  - `GetDoubleClickScreenPoint`
- 属性对象解析：`CDBox_Integrated/Modules/QuantityCalculation/QuantityAttributeDoubleClickService.cs`
  - `TryOpenWithOverlapSelection`
  - `FindOpenableCandidates`
- 几何候选采集：`CDBox_Integrated/Modules/PipeLengthAnnotation/PipeLengthAnnotationService.cs`
  - `FindCandidatesAtPoint`
  - `GetPointPickTolerance`
  - `TryGetDisplayClosestPoint`
- 选择浮窗：`CDBox_Integrated/Modules/PipeLengthAnnotation/OverlappingPipeSelectionService.cs`
- 属性编辑器窗口：`CDBox_Integrated/UI/Studio/CDBoxStudioQuantityAttributeEditorWindow.cs`

### 当前实现逻辑

1. `PreTranslateMessage` 捕获鼠标左键双击消息。
2. 读取 CAD 隐式选择集，判断是否为标注或可编辑属性对象。
3. 将双击消息中的客户区坐标转换为屏幕坐标。
4. 延迟调用 `TryOpenWithOverlapSelection`。
5. 通过双击落点调用 `FindCandidatesAtPoint`，筛选可打开属性编辑器的对象。
6. 候选超过一个时调用 `OverlappingPipeSelectionService.Select`；否则直接打开单一对象。

### 后续建议

优先增加诊断日志，不要先改交互：

- 记录双击前和延迟回调中的隐式选择 ObjectId/Handle。
- 记录屏幕点、转换后的 WCS 点、当前视图高度、`PICKBOX` 和最终容差。
- 记录 `FindCandidatesAtPoint` 的原始候选数量、距离、类型、图层和 Handle。
- 记录可编辑属性筛选前后的候选数量。

如果落点扫描始终只得到一个对象，重点验证：

- `Editor.PointToWorld` 使用的坐标是否对应实际绘图区和当前视口。
- 重叠对象是否位于不同空间、块参照、外部参照或不同 Z 值中。
- 下层对象是否不是当前支持的 `Curve` 类型，或属性识别后未被视为主管/支管。
- 是否应改用 AutoCAD 的 `Application.BeginDoubleClick` 提供的 WCS `Location`，同时设计可靠方式抑制原生双击动作。

### 必须保持的安全约束

- 不要在 `PreTranslateMessage` 的 Windows 消息钩子中执行 `Editor.SelectCrossingWindow`、复杂事务、写数据库或重生成操作。
- 之前曾因在双击消息钩子内进行不安全 CAD 选择/数据库调用，出现 AutoCAD 卡死和 `Unhandled Access Violation Reading 0xffffffff` 致命错误。
- 消息钩子内只允许捕获最少状态；候选扫描、事务、窗口创建应延迟到 Dispatcher 或 AutoCAD 命令上下文中。
- 修改双击逻辑后必须同时回归：
  - 管长标注双击浮窗；
  - 节点和表面积标注双击浮窗；
  - 普通主管、支管、井双击属性编辑器；
  - 特殊对象双击属性编辑器；
  - 连续关闭并打开属性编辑器；
  - 注记移动后图形不消失；
  - 绑定点红圆和夹点拖动；
  - 图纸保存后不再立即变为未保存。

## 构建与测试

```powershell
dotnet build CDBox.sln -c Release -p:AutoCADManagedDir="D:\Program Files\Autodesk\AutoCAD 2023"
dotnet run --project tests\CDBox.CoreTests\CDBox.CoreTests.csproj -c Release --no-build
```

若另一台电脑的 AutoCAD 安装目录不同，只修改 `AutoCADManagedDir`。

## 工作区注意事项

- `.codex/` 是本机工作区设置，已加入 `.gitignore`。
- `CDBox发布/*.zip` 是本地发布产物，已加入 `.gitignore`。
- `CDBox发布/update.json` 是供手动发布时参考的清单，应继续保留。
- 不要覆盖与当前任务无关的用户改动；提交前先检查 `git status` 和差异范围。
