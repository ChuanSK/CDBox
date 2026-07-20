# CDBox 标注 HUD 重设计实施规格

**文档状态：** 已确认方案 / 可进入实现
**版本：** 1.0
**目标读者：** Codex、CDBox 开发与测试人员
**适用范围：** 标注选择、文字与样式编辑、引线和绑定点拖动、绑定关系维护

> **最终技术决策：采用原生 WPF 实现半透明上下文 HUD，不采用 WebView2。** 保留现有“单击选中即可直接调整”的能力；双击只增加 HUD，不改变直接操控权限；任何空间拖动开始时隐藏 HUD，结束后再定位并恢复。

## 1. 文档目的

本文是 CDBox 标注编辑浮窗重设计的实现依据。Codex 应以本文为功能边界、状态边界和验收依据，并先复用现有标注选择、夹点拖动、CAD 对象捕捉和数据库事务能力，再新增 HUD 与编排层。除非现有代码结构确实无法承载，不应重新实现一套平行的标注拖动或对象捕捉系统。

本次改造解决以下问题：

- 现有大型编辑窗口占据绘图区域并遮挡标注位置调整。
- 双击打开编辑窗口后，界面与空间编辑相互争夺注意力和输入焦点。
- “更换绑定对象”被表达为额外命令，而实际最自然的入口是引线绑定点本身。
- 绑定长度与可编辑文字混在同一文本中，容易误改系统数据。
- 解除绑定、重新绑定、拖动换绑的规则需要形成可测试的数据不变量。

## 2. 范围与非目标

### 2.1 本期范围

- 用轻量半透明 HUD 替换当前大型标注编辑窗口。
- 保留单击选中后的全部直接操控能力。
- 双击打开 HUD 后仍允许拖动标注、文字位置、折点和绑定点。
- 明确绑定、解除绑定、重新绑定和拖动换绑的事务规则。
- 将绑定长度从可编辑文字中结构化分离。
- 在 HUD 内提供折叠式“更多编辑”。
- 提供 WPF 架构、状态机、数据模型、迁移和测试方案。

### 2.2 本期非目标

- 不实现 CDBox 自定义的管线/多段线吸附；继续使用 CAD 原生对象捕捉。
- 不增加最小拖动距离、防误触确认或二次确认。
- 不在“重新绑定”后启动额外的点选对象命令。
- 不增加侧边属性面板、固定面板或大型模态窗口。
- 不在第一版增加 HUD 固定、手动拖动、用户布局记忆或全局 HUD 框架。
- 不把调试字段（Handle、GUID、CDBoxObjectId、内部实体类型名）展示给普通用户。

## 3. 核心产品原则

1. **对象即入口。** 单击负责空间调整，双击只增加文字和样式编辑。
2. **HUD 不夺权。** HUD 打开后，原有夹点和拖动能力仍然可用。
3. **空间操作优先。** 一旦开始任何绘图区域拖动，HUD 必须先隐藏。
4. **绑定保护系统数据。** 绑定状态下，系统长度始终只读；用户只编辑文字部分。
5. **解绑即文本化。** 解除绑定时把当前完整显示结果固化为普通文字，不删除内容。
6. **重绑以端点为准。** 点击“重新绑定”只检查当前引线端点处对象，不要求再次拾取端点或对象。
7. **CAD 能力优先。** 普通对象依赖 CAD 原生对象捕捉；井对象保留 CDBox 专用连接点规则。
8. **原子提交。** 绑定关系、文字组合、引线几何与显示刷新必须在同一逻辑事务中一致成功或一致回退。

## 4. 最终 HUD 设计

### 4.1 已绑定状态

```text
┌────────────────────────────────┐
│ 绑定对象：110PVC管（原土回填） │
│                                │
│ 上标注：[可编辑文字________] [19.65m] │
│ 下标注：[可编辑文字____________]      │  ← 仅在下标注存在时显示
│                                │
│ [更多编辑]            [解除绑定] │
└────────────────────────────────┘
```

- “绑定对象”只显示目标对象所在图层；不显示内部类型、ID 或句柄。
- 上标注的用户文字为可编辑控件，系统长度使用独立只读文本或只读标签显示。
- 系统长度不能进入可编辑 TextBox，不能被选择、删除、粘贴覆盖或键盘修改。
- 下标注不存在时，整行不创建或折叠为零高度，不保留空白占位。
- “更多编辑”在同一 HUD 内向下展开；不打开当前大型窗口。

### 4.2 未绑定状态

```text
┌────────────────────────────────┐
│ 绑定对象：未绑定               │
│                                │
│ 上标注：[完整文字______________] │
│ 下标注：[完整文字______________] │  ← 仅在下标注存在时显示
│                                │
│ [更多编辑]            [重新绑定] │
└────────────────────────────────┘
```

- 上标注为完整普通文字，包含解除绑定时固化的长度内容，并允许全部修改。
- 下标注存在时允许完整修改。
- 按钮文案由“解除绑定”切换为“重新绑定”。
- 未绑定状态拖动引线端点只改变几何位置，不自动建立绑定；只有点击“重新绑定”才尝试绑定。

### 4.3 更多编辑

第一版建议包含现有大型编辑窗口中的高频样式项，并按现有数据模型实际字段取舍：

- 文字样式与文字高度。
- 文字颜色。
- 引线颜色、线型和线宽。
- 标注/组所在图层。
- “启用下标注”或“删除下标注”，用于创建或移除可选下标注行。

更多编辑采用折叠区域，不提供“应用”按钮：

- 文本输入在 `Enter` 或失去焦点时提交。
- 下拉选择和开关在选择完成时提交。
- `Esc` 先撤销当前字段的未提交编辑；无字段编辑时再关闭 HUD。
- 输入过程可更新内存预览，但不得每个字符都开启 CAD 数据库写事务。

### 4.4 视觉与尺寸规范

- 默认折叠宽度建议为 `300–340 px`，高度由内容自动计算；不得回到大面积固定窗口。
- 背景使用带 Alpha 的浅色画刷（建议视觉不透明度约 `92%`），文字和输入控件保持完全不透明。
- 圆角建议 `8 px`，内边距 `12–16 px`，轻量阴影，无标题栏、系统边框、任务栏入口和调整大小边框。
- HUD 与标注包围框保持约 `12–16 px` 间距。
- 候选位置按“右上、左上、右下、左下”评估，以不遮挡当前标注及夹点、且尽量留在当前绘图区为优先。
- 第一版不支持手动拖动或固定 HUD；所有位置由布局服务计算。
- 多显示器和不同 DPI 下必须按设备独立像素换算，不得直接把 CAD 屏幕像素当作 WPF DIPs。

## 5. 统一操作逻辑

| 用户操作 | 系统结果 |
|---|---|
| 单击标注 | 选中并显示现有控制点；不显示 HUD。 |
| 选中后拖动标注/文字/折点 | 保持现有空间调整能力。 |
| 选中后拖动绑定点 | 已绑定时可调整当前对象上的连接位置或换绑到有效新对象。 |
| 双击标注 | 在标注附近打开 HUD；标注仍保持选中和可拖动。 |
| HUD 打开时开始任意空间拖动 | 在 CAD 捕获拖动输入前隐藏 HUD。 |
| 空间拖动提交或取消 | 保持选中；若拖动前 HUD 已打开，则重新定位并恢复 HUD。 |
| 编辑字段后按 Enter/失焦 | 单次事务提交字段并刷新标注。 |
| 字段编辑中按 Esc | 恢复该字段进入编辑前的值。 |
| HUD 中无未提交字段时按 Esc | 关闭 HUD，回到 Selected；标注保持选中。 |
| 单击其他对象或取消选择 | 关闭 HUD，并按 CAD 既有逻辑改变选择。 |
| 点击“解除绑定” | 固化当前完整文字，清除绑定引用，按钮切换为“重新绑定”。 |
| 点击“重新绑定” | 检查当前引线端点处唯一有效对象；成功则直接绑定，不进入拾取命令。 |

## 6. 绑定、解绑、重绑与拖动换绑

### 6.1 已绑定时的数据不变量

- `BindingMode == Bound` 时必须存在可解析的绑定引用；运行时可另外标记引用健康状态。
- 系统长度来自绑定对象，不从用户输入文本反向解析为权威值。
- HUD 中系统长度与可编辑文字使用不同控件、不同 ViewModel 属性。
- 保存后显示文字由 `AnnotationTextComposer` 统一组合，界面层不得自行拼接。
- 健康绑定提交后，引线绑定端点必须落在目标对象允许的几何位置；井对象使用专用连接点规则。

### 6.2 拖动绑定点：同一对象

1. 开始拖动，HUD 立即隐藏。
2. 继续使用 CAD 原生对象捕捉移动端点。
3. 松开时端点命中当前绑定对象。
4. 仅更新端点/引线几何；绑定引用不变。
5. 在一个撤销单元中提交，重绘并恢复 HUD（若拖动前已打开）。

### 6.3 拖动绑定点：新对象

1. 松开时按端点位置查询可绑定候选对象。
2. 过滤不支持的实体、已删除实体、不可访问实体以及标注自身相关实体。
3. 解析到唯一有效新对象后，在同一事务内更新绑定引用、对象图层显示、系统长度和引线端点。
4. 保留用户文字部分，不用新图层名覆盖用户已编辑文字。
5. 提交后 HUD 内容与标注图形同时刷新。

普通对象不做 CDBox 自定义吸附。端点查询允许使用仅用于命中验证的几何容差；该容差不能改变端点坐标，也不能表现为第二套吸附。井对象例外，可在提交时归一到井的有效连接点。

### 6.4 拖动绑定点：无有效对象

本规格不增加拖动阈值或误操作确认，但必须维护数据一致性：

- 已绑定标注若释放点没有有效对象，不提交新的端点和绑定关系；几何与绑定一起回退到拖动前状态。
- 使用非模态短提示或 CAD 状态栏提示“未找到可绑定对象，已保留原绑定”。
- 不弹出阻断式对话框，不自动解除绑定。

### 6.5 解除绑定

点击“解除绑定”时，在一个事务中完成：

1. 读取当前最终显示的上标注和下标注。
2. 将上标注（包括当前系统长度）固化为完全可编辑的普通文字。
3. 保留下标注当前文字与引线几何。
4. 清除持久绑定引用并将 `BindingMode` 设为 `Detached`。
5. 保留最后一次系统长度标记，供重新绑定时安全拆分文本使用。
6. 更新 HUD 为“未绑定”和“重新绑定”，重绘标注。

解除绑定不是删除长度、删除引线或删除对象，只是停止跟随对象数据。

### 6.6 重新绑定

点击“重新绑定”时不启动拾取命令，直接执行：

```text
读取当前引线端点
→ 查询端点处可绑定候选
→ 过滤并解析唯一有效对象
→ 写入绑定引用
→ 读取对象图层和长度
→ 将长度恢复为系统只读段
→ 保留用户文字部分
→ 刷新图形和 HUD
```

若端点处没有唯一、正确的可绑定对象：

- 不改变文字、几何或绑定模式。
- 提示：“当前引线端点处没有唯一可绑定对象，请先拖动引线端点到目标对象上。”
- 用户拖动端点后再次点击“重新绑定”；不得要求用户重新点击端点。

### 6.7 重新绑定时的文本恢复规则

解绑后的上标注是完整自由文本。重新绑定时必须避免把旧长度和新长度重复显示：

1. 若自由文本末尾仍包含解绑时记录的旧长度标记，则去掉旧标记，把其余内容作为用户文字，再附加新系统长度。
2. 若末尾存在符合当前单位格式、且可可靠识别的长度段，则将其替换为新系统长度。
3. 若无法可靠识别旧长度，保留全部自由文本作为用户文字并附加新系统长度；不得静默删除任何无法确认的用户内容。

以上规则必须由 `AnnotationTextComposer` 集中实现并单元测试，不能散落在按钮事件或 ViewModel 中。

## 7. HUD 隐藏、恢复与定位

### 7.1 必须触发隐藏的空间操作

- 拖动标注整体。
- 拖动文字位置控制点。
- 拖动引线折点。
- 拖动引线绑定点。

隐藏必须发生在 CAD 获取鼠标捕获之前，避免透明窗口继续拦截鼠标或键盘。

### 7.2 恢复规则

- 拖动提交和取消都结束临时隐藏。
- 若拖动前 HUD 已打开，鼠标释放后等待约 `150 ms`；若没有新的拖动开始，则重新计算位置并淡入。
- 若拖动前 HUD 未打开，只回到 Selected，不自动打开 HUD。
- 若标注被删除、取消选择、文档切换或绘图窗口失活，不恢复 HUD。
- 连续拖动时取消尚未执行的恢复计时，避免闪烁。

### 7.3 视图变化

HUD 打开期间，缩放、平移、视口切换、窗口移动或 DPI 改变时，位置服务应节流重算。若锚点移出当前绘图区，临时隐藏 HUD；锚点重新可见时再显示。HUD 不写入图纸坐标，其位置属于纯运行时 UI 状态。

## 8. 状态机

界面交互状态与绑定数据状态必须正交建模。

### 8.1 交互状态

| 状态 | 说明 | HUD | 直接拖动 |
|---|---|---:|---:|
| `Idle` | 无目标标注 | 隐藏 | 否 |
| `Selected` | 标注选中、夹点可用 | 隐藏 | 是 |
| `HudEditing` | 标注选中且 HUD 打开 | 显示 | 是 |
| `SpatialDragging` | 正在进行空间拖动，携带 `DragKind` 和返回状态 | 隐藏 | 进行中 |
| `Updating` | 极短的内部提交门闩，防止重入，不作为可见模式 | 保持当前策略 | 否 |

`DragKind` 至少包含：`AnnotationBody`、`TextAnchor`、`LeaderElbow`、`BindingEndpoint`。

### 8.2 主要转换

| 当前状态 | 事件 | 下一状态 | 动作 |
|---|---|---|---|
| `Idle` | 单击标注 | `Selected` | 高亮并显示夹点。 |
| `Selected` | 双击同一标注 | `HudEditing` | 创建/绑定 ViewModel，定位并显示 HUD。 |
| `Selected` | 开始拖动 | `SpatialDragging(return=Selected)` | 记录拖动快照。 |
| `HudEditing` | 开始拖动 | `SpatialDragging(return=HudEditing)` | 先提交或取消活动字段，再隐藏 HUD。 |
| `SpatialDragging` | 提交 | 返回状态 | 单事务提交；按返回状态决定是否恢复 HUD。 |
| `SpatialDragging` | 取消 | 返回状态 | 恢复快照；按返回状态决定是否恢复 HUD。 |
| `HudEditing` | Esc（无活动字段） | `Selected` | 关闭 HUD，保留选择。 |
| 任意状态 | 选择其他对象/取消选择/文档关闭 | `Idle` | 取消计时、解除订阅、隐藏 HUD。 |

### 8.3 绑定状态

- `Bound`：绑定引用存在，系统长度只读，按钮为“解除绑定”。
- `Detached`：无绑定引用，完整文字自由编辑，按钮为“重新绑定”。
- `BindingHealth` 是运行时诊断值：`Healthy`、`MissingTarget`、`UnsupportedTarget`、`ResolveFailed`。它不取代上述持久状态。

禁止用 `HudEditing` 代表“已绑定”，也禁止用 `Detached` 推导 HUD 是否显示。

## 9. 推荐数据模型

以下名称是目标结构；Codex 应适配现有命名，不应因命名差异复制平行模型。

```csharp
public sealed class AnnotationRecord
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; set; }          // 新结构建议为 2
    public AnnotationBindingMode BindingMode { get; set; }
    public AnnotationBindingRef? Binding { get; set; }

    public string UpperEditableText { get; set; }   // Bound 时的用户文字段
    public string? LowerText { get; set; }           // null 表示不存在下标注
    public string? DetachedUpperText { get; set; }   // Detached 时的完整自由文本
    public string? DetachedLengthToken { get; set; } // 解绑时记录，供安全重绑

    public CadPoint3d TextAnchor { get; set; }
    public CadPoint3d LeaderElbow { get; set; }
    public CadPoint3d BindingEndpoint { get; set; }
    public AnnotationStyle Style { get; set; }
}

public sealed class AnnotationBindingRef
{
    public string? CdBoxObjectId { get; init; } // 优先持久标识（若现有模型具备）
    public string Handle { get; init; }         // 数据库内持久回退标识
    public string EntityKind { get; init; }
    public string LastKnownLayer { get; set; }
    public string? LastRenderedLength { get; set; }
    // 运行时 ObjectId 仅作缓存，不作为跨会话持久主键。
}
```

### 9.1 模型约束

- `LowerText == null` 表示没有下标注；空字符串是否等价于不存在应在仓库层统一归一化。
- `LastKnownLayer` 和 `LastRenderedLength` 是失效时的显示/恢复缓存，不是绑定对象的权威属性。
- 长度格式化集中到现有单位/精度服务；HUD 和标注图形不得各自格式化。
- 绑定对象解析优先使用 CDBox 持久对象 ID，其次使用 Handle；运行时 `ObjectId` 不跨会话保存。
- 所有新字段带 `SchemaVersion`，支持按对象懒迁移。

## 10. 推荐代码结构

```text
Annotations/
├─ Application/
│  ├─ AnnotationHudController.cs
│  ├─ AnnotationStateMachine.cs
│  ├─ AnnotationDragCoordinator.cs
│  └─ AnnotationCommandService.cs
├─ Domain/
│  ├─ AnnotationRecord.cs
│  ├─ AnnotationBindingRef.cs
│  ├─ AnnotationTextComposer.cs
│  ├─ AnnotationBindingRules.cs
│  └─ AnnotationStyle.cs
├─ Infrastructure/Cad/
│  ├─ CadAnnotationRepository.cs
│  ├─ CadAnnotationTransactionService.cs
│  ├─ CadBindingCandidateResolver.cs
│  ├─ CadViewportCoordinateService.cs
│  ├─ CadSelectionEventAdapter.cs
│  └─ WellConnectionPointPolicy.cs
└─ Presentation/Wpf/
   ├─ AnnotationHudWindow.xaml
   ├─ AnnotationHudWindow.xaml.cs
   ├─ AnnotationHudViewModel.cs
   ├─ AnnotationHudPlacementService.cs
   ├─ AnnotationHudLifetimeService.cs
   └─ Converters/
```

### 10.1 职责边界

**`AnnotationHudController`**

- 唯一的交互编排入口。
- 订阅选择、双击、拖动开始/结束、文档切换和视图变化事件。
- 驱动状态机和 HUD 生命周期，不直接拼接文字或操作 WPF 控件。

**`AnnotationStateMachine`**

- 纯状态转换，无 CAD 数据库调用。
- 对非法转换抛出可诊断错误或返回拒绝结果。
- 可用单元测试覆盖全部转换。

**`AnnotationDragCoordinator`**

- 适配现有夹点/拖动实现，发布统一的 `DragStarted`、`DragCommitted`、`DragCanceled`。
- 保存拖动前快照和 `DragKind`。
- 不实现第二套对象捕捉。

**`AnnotationCommandService`**

- 实现编辑字段、解绑、重绑和换绑用例。
- 控制文档锁、事务和单一撤销边界。

**`AnnotationTextComposer`**

- 组合绑定状态显示文本。
- 固化解绑文本。
- 重新绑定时识别/替换旧长度标记。
- 负责长度分隔符、单位和精度的一致性。

**`CadBindingCandidateResolver`**

- 按端点位置查询候选、过滤实体和处理歧义。
- 普通对象只验证 CAD 捕捉后的端点，不修改端点坐标。
- 井对象委托 `WellConnectionPointPolicy` 处理专用连接点。

**`AnnotationHudViewModel`**

- 暴露可编辑文字、只读长度、图层名、下标注可见性和命令。
- 不持有 CAD Transaction，不直接查询数据库。
- 通过命令服务提交，失败时恢复快照并显示非模态错误。

**`AnnotationHudWindow`**

- 只负责 WPF 呈现、焦点和键盘行为。
- 作为 CAD 主窗口的 owned modeless window；`ShowInTaskbar=false`，不使用应用级 `Topmost=true`。
- HUD 隐藏时必须释放鼠标命中，不保留透明的可点击窗口区域。

## 11. WPF 与 WebView2 选型

| 维度 | WPF | WebView2 |
|---|---|---|
| CAD/.NET 集成 | 与现有 C# 命令、事件、Dispatcher、窗口句柄直接集成 | 需要 JS/C# 桥接和异步消息协议 |
| 焦点与键盘 | 可直接处理 `Esc`、Enter、IME、焦点恢复和 owned window | Web 内容焦点与 CAD 命令焦点容易冲突 |
| 拖动期间隐藏 | 同步隐藏并立即释放命中区域 | 浏览器合成与跨进程消息可能产生时序问题 |
| 透明与小型 HUD | Border、Alpha 背景、圆角和阴影足够 | 透明 WebView 合成、窗口穿透和裁剪更复杂 |
| 资源与部署 | 单进程、依赖少、启动快 | 增加 WebView2 Runtime、子进程、内存和版本管理 |
| UI 复杂度 | 本 HUD 只有少量表单控件，WPF 足够 | 适合复杂富 Web UI，但本场景收益有限 |
| 自动化测试 | ViewModel 与服务可直接单测 | 需同时测试 DOM、桥接和宿主 |

### 11.1 最终推荐

**选择 WPF。** 本功能是与 CAD 选择、夹点拖动、文档锁和输入焦点紧密耦合的低延迟上下文工具，而不是独立的复杂 Web 应用。WPF 能以更少的进程、桥接层和焦点风险完成需求，也更容易与现有 .NET 标注逻辑共享类型和命令。

推荐使用一个由 CAD 主窗口拥有的无边框 modeless WPF Window（或项目现有等价托管浮层），而不是 WebView2。通过 `WindowInteropHelper.Owner` 或现有宿主窗口服务设置 owner；使用 WPF `Border` 的半透明背景实现视觉透明，不把整个窗口 `Opacity` 降低，以保证文字清晰。

若宿主对 `AllowsTransparency=true` 存在渲染或性能问题，优先改为普通无边框窗口加不透明浅色背景和圆角裁剪的兼容方案，不能因此切换到 WebView2。是否使用 `Popup`、独立 `Window` 或现有 Dock/Palette 基础设施，应以代码仓现有窗口宿主管理方式为准，但不得采用模态对话框。

### 11.2 何时才考虑 WebView2

只有未来 HUD 发展为包含富文本、复杂模板设计器、跨产品共享的前端应用，且团队已具备稳定的 WebView2 宿主、焦点治理和版本部署体系时，才重新评估。当前版本不为 WebView2 预留双实现抽象，也不引入前端构建链。

## 12. 事务与更新流程

### 12.1 通用提交模板

```text
捕获 UI 输入和当前标注版本
→ 在 CAD UI 线程获取文档锁
→ 开启单个写事务/撤销组
→ 重新解析标注和目标对象，防止使用陈旧 ObjectId
→ 验证操作前置条件
→ 更新领域模型、扩展数据和几何
→ 统一重建显示文字/图形
→ 提交事务
→ 释放锁
→ 重新读取最终状态并刷新 ViewModel/HUD
```

任一步失败均中止事务，恢复 UI 快照，不允许只更新绑定 ID 但未更新文字，或只更新引线但未更新绑定。

### 12.2 文本提交

- TextBox 中的每个字符只更新 ViewModel，可选使用非持久预览。
- `Enter`/失焦时检查当前实体版本；无冲突则一次写事务提交。
- 绑定状态仅提交 `UpperEditableText` 和下标注；系统长度从对象重读并组合。
- 提交失败时恢复字段进入编辑前值，并显示非模态错误。

### 12.3 拖动提交

- 拖动开始保存几何、绑定引用和 HUD 返回状态快照。
- 预览阶段使用现有 transient/jig/夹点机制，不持续写数据库。
- 鼠标释放时一次事务提交最终几何；绑定点拖动还执行候选解析与绑定更新。
- 整次拖动在撤销栈中表现为一个动作。

### 12.4 对象变化刷新

当健康绑定对象的长度、图层或关键属性由其他命令修改时，沿用现有反应器/事件机制标记标注为脏，并在安全时机重读。HUD 打开时刷新 ViewModel；HUD 未打开时只刷新标注图形。避免在对象修改事件回调内部嵌套写事务。

## 13. 候选对象解析规则

1. 以引线端点和仅用于命中验证的容差查询当前空间对象。
2. 排除标注文本、引线自身、控制点辅助实体和不支持类型。
3. 排除已删除、不可打开或违反现有 CDBox 绑定规则的对象。
4. 普通对象按几何距离排序；只有唯一最佳候选时成功。
5. 若多个候选在容差内无法稳定区分，返回 `Ambiguous`，不猜测、不启动选择命令。
6. 井对象通过专用策略返回规范连接点。

候选解析返回结构化结果：`Success(target, endpoint)`、`NotFound`、`Ambiguous`、`Unsupported`、`TargetUnavailable`。UI 文案由上层映射，不在基础设施服务中弹窗。

## 14. 错误处理与恢复

| 场景 | 行为 |
|---|---|
| 重新绑定端点无对象 | 不修改数据；提示先拖动端点到目标对象。 |
| 端点存在多个同优先级对象 | 不猜测；提示端点处没有唯一可绑定对象。 |
| 拖动换绑释放到无效位置 | 回退几何和绑定；非模态提示已保留原绑定。 |
| 目标对象在提交前被删除 | 事务中止；恢复拖动/编辑快照并刷新选择。 |
| 当前绑定引用无法解析 | 不自动改写数据；HUD 显示“对象不可用”，保留最后显示值，允许解除绑定或通过端点换绑恢复。 |
| 图层被重命名 | 下次解析时刷新显示；持久主键不依赖图层名。 |
| 文字提交与外部修改冲突 | 拒绝陈旧提交，重读对象并提示内容已更新。 |
| CAD 文档切换/关闭 | 立即隐藏 HUD，取消计时和订阅，释放 ViewModel 引用。 |
| WPF 未处理异常 | 记录诊断信息，关闭 HUD，不得令 CAD 主进程进入半提交状态。 |

错误反馈优先使用 HUD 内短状态或 CAD 状态栏；仅无法继续且可能造成数据丢失时使用对话框。

## 15. 从当前大型编辑窗口迁移

### 阶段 1：盘点与隔离

1. 搜索现有标注双击入口、大型窗口类、选择事件、夹点拖动、绑定更新和事务代码。
2. 建立字段映射：哪些进入 HUD 主区、哪些进入“更多编辑”、哪些仅保留为调试信息、哪些淘汰。
3. 把现有业务逻辑从窗口事件中提取到命令服务；此阶段保持旧窗口可用。

### 阶段 2：接入 WPF HUD

1. 创建 HUD View/ViewModel/Lifetime/Placement 组件。
2. 双击入口切换为 `AnnotationHudController.Open(annotationId)`。
3. 接入现有拖动开始/结束事件，实现隐藏、返回状态和延迟恢复。
4. 确认 HUD 打开时不改变选择集、不禁用夹点。

### 阶段 3：绑定与文字结构化

1. 引入 `AnnotationTextComposer` 和 `AnnotationBindingService`。
2. 将解除绑定、重新绑定、拖动换绑改为单一业务服务入口。
3. 增加 `SchemaVersion=2` 和结构化文字字段；优先采用按对象首次编辑时懒迁移。
4. 旧数据若已分离文字与长度，直接映射；若只有组合文本，按当前对象实际长度和单位格式识别末尾长度段。
5. 无法可靠拆分的旧文本标记为 `LegacyTextNeedsReview`，保持原显示不变；首次编辑时要求开发逻辑走保守无丢失路径，不做批量静默删改。

### 阶段 4：灰度与删除旧窗口

1. 使用功能开关让内部测试可在新旧编辑器之间切换。
2. 跑完本文验收用例，并重点验证不同 CAD 版本、DPI 和多文档场景。
3. 保留旧窗口作为一个发布周期的回退入口，但不再增加新功能。
4. 新 HUD 达标后删除旧双击入口和废弃窗口；若仍有调试需求，转移到开发者诊断工具，不回到用户 HUD。

## 16. 测试策略

### 16.1 单元测试

- `AnnotationStateMachine` 的全部合法/非法转换。
- `AnnotationTextComposer`：绑定组合、解绑固化、旧长度替换、无法识别长度时不丢字、单位与精度变化。
- `CadBindingCandidateResolver` 的过滤、唯一候选、无候选、歧义和井对象分支。
- `AnnotationHudPlacementService` 的四象限候选、边缘约束、DPI 换算和标注遮挡判定。
- `AnnotationCommandService` 在仓库异常、版本冲突和目标删除时的回退。

### 16.2 CAD 集成测试

- 选择、双击、Esc、点击其他对象和多文档切换。
- HUD 打开时四类空间拖动均可开始，且 HUD 在捕获输入前隐藏。
- 普通管线/多段线使用 CAD 原生对象捕捉；关闭对象捕捉时 CDBox 不偷偷吸附。
- 井对象落到预定连接点。
- 解绑、重绑、同对象端点调整、新对象换绑和无效释放。
- 每次编辑、拖动、解绑和重绑均为一个撤销动作，Redo 可恢复。
- 绑定对象长度或图层外部变化后，标注和打开的 HUD 同步刷新。

### 16.3 UI 与兼容性测试

- 100%、125%、150%、200% DPI。
- 主屏/副屏、不同缩放率、窗口跨屏移动。
- 绘图区四角、标注密集区、缩放和平移后的 HUD 定位。
- 中文输入法、复制粘贴、Enter、Esc、Tab 顺序和焦点返回 CAD。
- 长图层名、长上标注、存在/不存在下标注、更多编辑展开后的自适应高度。
- 宿主窗口最小化、失焦、文档关闭和插件卸载时无孤立 HUD。

## 17. 验收标准

以下条件全部满足才视为本次改造完成：

1. 单击标注后无需打开 HUD 即可使用现有控制点调整位置和引线。
2. 双击仅打开轻量 HUD，不改变标注选择状态和直接拖动能力。
3. HUD 主区严格显示“绑定对象、上标注、可选下标注、更多编辑、解除绑定/重新绑定”。
4. 无下标注时不显示该行且不保留空白高度。
5. 绑定对象只显示所在图层，不显示 Handle、GUID、CDBoxObjectId 或内部类型名。
6. 绑定状态的系统长度不属于可编辑控件，键盘、粘贴和全选均不能修改它。
7. 解除绑定后当前完整文字不丢失，长度可自由编辑，按钮立即变为“重新绑定”。
8. 重新绑定成功时不进入选择命令、不要求再次点击端点，并以当前端点对象建立绑定。
9. 重新绑定失败时数据完全不变，并明确提示先移动端点。
10. 已绑定时拖动端点到有效新对象可直接换绑，系统长度刷新，用户文字部分保留。
11. 已绑定时拖动端点到同一对象仅更新端点几何。
12. 已绑定时拖动端点到无效位置时几何与绑定一起回退，不自动解除绑定。
13. 普通对象没有 CDBox 自定义吸附；井对象按专用连接点规则处理。
14. HUD 打开时开始任意空间拖动，HUD 在 CAD 捕获鼠标前隐藏。
15. 拖动结束后仅在拖动前 HUD 已打开、标注仍选中且可见时恢复，并重新计算位置。
16. 连续拖动没有 HUD 闪烁，取消恢复计时有效。
17. HUD 不遮挡当前标注和控制点，并在绘图区边缘选择可用象限。
18. 每个用户动作只有一个撤销单元；异常时无部分提交或脏绑定。
19. 文档切换、关闭和目标删除不会留下孤立窗口或未释放事件订阅。
20. 新旧标注数据均可打开；迁移不静默删除无法可靠识别的旧文字。
21. WPF HUD 在目标 CAD 版本、中文输入法和要求的 DPI/多显示器组合下通过回归。
22. 当前大型编辑窗口的用户入口已移除或仅由受控回退开关启用。

## 18. Codex 实施顺序

1. 先阅读仓库结构和现有标注编辑/拖动代码，输出实际类名与本文职责的映射。
2. 写状态机、文字组合和绑定候选解析的单元测试，再实现对应纯逻辑。
3. 从旧窗口事件中提取命令服务和事务边界，不先改 UI。
4. 实现 WPF HUD 与 ViewModel，接入只读数据并验证焦点、Owner 和 DPI。
5. 接入字段提交、解绑和重绑。
6. 接入现有拖动事件的隐藏/恢复与换绑事务。
7. 完成懒迁移、Undo/Redo、异常恢复和跨文档生命周期。
8. 执行验收矩阵，灰度开启新 HUD，最后删除旧入口。

## 19. 实现注意事项

- Codex 应优先修改现有类并保持命名风格，本文目录结构是职责建议，不是强制创建同名文件。
- UI 代码不得直接持有长生命周期 CAD Transaction 或跨文档 ObjectId。
- 事件订阅必须集中管理并可在 HUD 关闭、文档切换和插件卸载时对称释放。
- 所有恢复计时使用可取消 token/DispatcherTimer，并在新拖动或关闭时取消。
- 不使用全局 `Topmost`，避免 HUD 压在其他应用之上；使用 CAD 主窗口 owner 保持正确 Z 序。
- 不用整个 Window 的 `Opacity` 实现半透明，以免文字和控件一起变淡；只设置背景 Brush Alpha。
- 任何需要宿主 API 的具体类型名（文档锁、事务、视口转换、夹点事件）以当前代码仓和支持的 CAD 版本为准。
- 若现有 CAD 宿主要求所有数据库操作在主线程执行，命令服务必须通过现有 Dispatcher/DocumentContext 调度，不能从 WPF 异步回调直接写库。

## 20. 完成定义

实现、测试与迁移代码合并前，开发者应能用一句话验证整体模型：

> 单击保持直接操控；双击只增加 WPF HUD；拖动时 HUD 隐藏；绑定状态保护系统长度；解绑后文字完全自由；重绑只认当前引线端点，所有数据更新原子完成。

## 额外补充：

一、绑定数据模型调整
1. 不再把世界坐标作为主要绑定依据
绑定信息至少应包含：
public sealed class AnnotationBindingData
{
    // 持久对象标识
    public string TargetObjectId { get; set; }
    public string TargetHandle { get; set; }

    // 锚点类型
    public AnnotationAnchorType AnchorType { get; set; }

    // 在曲线总长度中的归一化位置，范围 0～1
    public double NormalizedPosition { get; set; }

    // 多段线辅助信息
    public int? SegmentIndex { get; set; }
    public double? SegmentParameter { get; set; }

    // 井等节点对象的连接点
    public string ConnectionPointId { get; set; }

    // 上一次解析出的世界坐标，仅用于显示和异常回退
    public Point3d LastResolvedPoint { get; set; }

    public bool IsValid { get; set; }
}
锚点类型：
public enum AnnotationAnchorType
{
    CurvePosition,
    StartPoint,
    EndPoint,
    MidPoint,
    WellCenter,
    WellConnectionPoint
}
LastResolvedPoint只能作为缓存和异常回退，不能作为实际绑定关系。
二、管线绑定点的记录方式
用户拖动引线端点到管线后，继续使用 CAD 原生对象捕捉，不增加 CDBox 自定义吸附。
松开端点时，CDBox执行：
识别端点处目标对象
→ 获取端点在目标曲线上的位置
→ 计算几何锚点
→ 保存绑定关系
普通位置
对于直线、多段线、圆弧等曲线：
NormalizedPosition =
绑定点距曲线起点的长度 ÷ 曲线总长度
例如绑定在管线全长的 35% 处：
NormalizedPosition = 0.35
管线整体移动、旋转或缩放后，重新计算：
新绑定点 =
目标曲线总长度 × 0.35 对应的曲线点
这样绑定点便会跟随目标对象。
特殊位置
若绑定点位于明显的特征位置，应记录语义锚点：
管线起点 → StartPoint
管线终点 → EndPoint
管线中点 → MidPoint
普通位置 → CurvePosition
井对象继续使用：
WellCenter
WellConnectionPoint
井连接点由 CDBox 管理，不完全依赖曲线比例。
三、目标对象变化后的更新规则
1. 整体移动、旋转、镜像和缩放
重新解析几何锚点，使引线端点跟随目标对象。
同时保持：
标注文字位置不变；
引线折点不变；
仅更新引线与目标对象相连的一端；
若标注内容包含管线长度，同步刷新长度。
例如：
管线移动
→ 绑定端点跟随移动
→ 引线重新连接
→ 标注文字位置保持原位
这通常是用户最期待的行为。
2. 管线拉伸或长度变化
绑定点根据锚点类型更新：
锚点类型	更新方式
StartPoint	跟随新起点
EndPoint	跟随新终点
MidPoint	跟随新中点
CurvePosition	保持原归一化位置
井连接点	重新读取对应连接点

例如原来绑定在管线长度的 40%：
原长度：10m
绑定位置：距起点4m

拉伸后：20m
新绑定位置：距起点8m
这是“保持相对位置”的逻辑。
3. 多段线顶点发生改变
优先使用：
SegmentIndex + SegmentParameter
当原线段仍存在时，保持在线段上的局部位置。
如果原线段已经被删除、合并或结构发生重大改变，则回退到：
NormalizedPosition
即：
优先保持原线段位置
→ 原线段无效时保持整条曲线比例位置
这样比单纯保存某一个世界坐标稳定。
4. 管线反转
执行 REVERSE 或程序交换起终点时，不能让绑定点突然跳到对称位置。
检测到曲线方向反转后：
NormalizedPosition =
1 - NormalizedPosition
例如：
原位置：0.25
反转后：0.75
其实际世界位置保持不变。
5. 管线删除
   目标对象被删除时：
   不删除标注；
   保留标注当前文字；
   保留引线最后位置；
   将绑定状态设置为失效；
   HUD显示：
   绑定对象：对象已失效
   按钮变为：
   [重新绑定]
   此时重新绑定仍使用当前引线端点处对象。
   如果删除操作被撤销，目标对象重新恢复且持久标识仍有效，可自动恢复绑定。
   四、对象变化监听机制
   不要在每一次 ObjectModified 回调中直接修改标注对象，这容易导致：
   嵌套事务；
   循环触发；
   Grip 拖动卡顿；
   CAD 数据库状态异常。
   推荐采用“收集变化对象，命令结束后统一刷新”的机制。
   监听层
   Database.ObjectModified
   Database.ObjectErased
   Document.CommandEnded
   Document.CommandCancelled
   Document.CommandFailed
   对象发生变化时，只加入待刷新集合：
   _dirtyTargetIds.Add(objectId);
   命令结束时统一处理：
   查询哪些标注绑定了这些对象
   → 开启一次事务
   → 批量重新解析绑定点
   → 更新引线
   → 更新长度文字
   → 刷新打开的HUD
   建议增加：
   AnnotationUpdateGuard
   防止 CDBox 更新标注时，再次触发自己的绑定更新。
   示意：
   if (AnnotationUpdateGuard.IsUpdating)
    return;

using (AnnotationUpdateGuard.Enter())
{
    UpdateBoundAnnotations(dirtyObjectIds);
}
五、推荐代码结构
Annotation
├─ Binding
│  ├─ AnnotationBindingData
│  ├─ AnnotationAnchorType
│  ├─ AnnotationBindingService
│  ├─ AnnotationAnchorResolver
│  └─ BoundAnnotationRepository
│
├─ Tracking
│  ├─ BindingChangeTracker
│  ├─ AnnotationUpdateQueue
│  └─ AnnotationUpdateGuard
│
├─ Geometry
│  ├─ CurveAnchorResolver
│  ├─ PolylineAnchorResolver
│  ├─ WellAnchorResolver
│  └─ LeaderGeometryUpdater
│
├─ Editing
│  ├─ AnnotationEditorController
│  ├─ AnnotationEditState
│  └─ AnnotationGripController
│
└─ Hud
   ├─ AnnotationHudView
   ├─ AnnotationHudViewModel
   └─ AnnotationHudHost
核心职责应明确分开。
AnnotationBindingService
负责：
BindAtLeaderPoint(...)
Detach(...)
RebindAtCurrentLeaderPoint(...)
ChangeBindingByDragging(...)
RefreshBinding(...)
AnnotationAnchorResolver
负责：
CreateAnchor(targetEntity, pickedPoint)
ResolvePoint(targetEntity, bindingData)
ValidateAnchor(targetEntity, bindingData)
BindingChangeTracker
负责监听目标对象变化，不处理 HUD 业务。
LeaderGeometryUpdater
只负责将新绑定点写入标注几何，不负责判断绑定对象。
六、与现有 HUD 逻辑的关系
HUD打开时管线移动
管线修改结束后：
刷新绑定点
→ 更新引线
→ 更新长度
→ 刷新HUD绑定对象和文字
→ 重新计算HUD显示位置
如果用户正在拖动标注或绑定点，HUD继续保持隐藏，拖动结束后再显示。
解除绑定后
点击 [解除绑定] 后：
删除几何锚点数据；
当前绑定点转为普通固定坐标；
管线之后再移动，不影响引线；
全部文字转为自由编辑状态。
也就是说：
已绑定：引线端点跟随目标对象
未绑定：引线端点保持世界坐标
重新绑定后
点击 [重新绑定]：
使用当前引线端点
→ 查找端点处对象
→ 创建新的几何锚点
→ 开始跟随新对象
不需要重新选择绑定点。
七、更新时应保持原子性
一次目标对象更新应在同一事务中完成：
解析新绑定点
更新引线端点
更新绑定缓存
读取新管线长度
更新系统长度文字
更新对象所在图层信息
标记图形重绘
不应出现：
引线已经移动，但长度还未刷新；
长度已刷新，但绑定对象仍是旧对象；
HUD和图纸显示不同步。
八、验收测试
Codex实现后至少测试以下场景：
MOVE移动管线，引线端点跟随。
Grip移动管线，引线端点跟随。
ROTATE旋转管线，绑定点保持在原相对位置。
SCALE缩放管线，绑定点按比例变化。
STRETCH拉伸管线，长度与绑定点同时刷新。
修改多段线顶点，连接点仍位于目标多段线上。
反转管线方向，绑定点世界位置不跳变。
更换绑定对象后，旧对象移动不再影响标注。
解除绑定后，原管线移动不影响标注。
删除目标对象，标注进入失效绑定状态。
撤销删除，绑定能够恢复。
保存并重新打开图纸，绑定关系仍然有效。
复制管线与标注后，不错误绑定回原对象。
HUD打开期间修改管线，HUD和引线同步刷新。
一次移动大量管线时，不出现明显卡顿。
