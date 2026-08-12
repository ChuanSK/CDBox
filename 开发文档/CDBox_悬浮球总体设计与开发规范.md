# CDBox 悬浮球总体设计与开发规范

版本：1.0
日期：2026-08-12
状态：悬浮球版本第一阶段强制实施规范

## 0. 文档用途与执行优先级

本文用于确定 CDBox 悬浮球版本的产品边界、WPF 界面体系、统一消息协议、CAD 生命周期、同步与检查接入方式，以及分阶段迁移规则。

文档关系如下：

1. 本文负责悬浮球外壳、通知、提示、进度、警告、交互与宿主架构；
2. 《悬浮球同步和检查功能开发文档》负责同步中心、图纸检查中心的业务规则和任务模型；
3. 《CDBox 设计思想、产品方向与开发规范》继续作为产品、原生兼容、事务和代码修改的上位规范；
4. 《CDBox Annotation HUD Implementation Spec》与《CDBox 标注浮窗动画设计文档》提供现有 WPF 浮窗的宿主、DPI、动画和焦点经验；
5. 若旧文档中“悬浮球展开区可使用 WebView2”等建议与本文冲突，以本文为准。

本文不要求第一阶段一次实现完整同步依赖图，但从第一版起必须遵守本文的数据边界、线程边界和文档隔离规则，避免后续重写。

---

# 1. 最终技术与产品决策

## 1.1 技术选型

悬浮球、悬浮球展开面板、悬浮球提示卡、任务摘要和进度反馈统一采用原生 WPF。

明确不采用：

- WebView2 构建悬浮球或其轻量详情面板；
- WinForms 构建新的悬浮球窗口；
- 每条通知创建一个互不关联的长期窗口；
- 全局 `Topmost=true`；
- 在 WPF 控件事件中直接持有长期 CAD `Transaction`；
- 通过不断写入 DWG 保存悬浮球临时状态。

复杂设置、完整报表、批量属性编辑仍可跳转工作台或现有独立 WebView2 页面。悬浮球不复制这些复杂界面。

## 1.2 产品定位

悬浮球是当前图纸的常驻状态中心，而不是第二个菜单栏。

它持续回答：

1. 当前图纸是否正常；
2. 是否存在尚未同步或已经过期的数据；
3. 是否有进行中的 CDBox 操作；
4. 哪些问题需要用户处理；
5. CDBox 刚才做了什么。

悬浮球负责状态、任务、消息和操作反馈，不负责罗列所有 CDBox 命令。

## 1.3 统一消息目标

目标状态下，以下信息均通过悬浮球体系呈现：

- 操作提示；
- 选择提示；
- 输入提示；
- 普通通知；
- 成功反馈；
- 警告与错误；
- 可取消或不可取消的进度；
- 同步任务；
- 检查问题；
- 需要用户确认的决策；
- 本次会话的操作历史。

例外：

- 独立更新器仍使用自己的窗口，因为此时 CAD 主窗口可能已经关闭；
- 工作台字段校验、表格编辑错误等局部 UI 反馈应留在工作台当前页面；
- 工作台触发的全局任务、后台任务完成、严重错误仍应同步进入悬浮球；
- AutoCAD 自身命令行内容不由 CDBox 强制接管；
- 无界面自检、诊断日志和开发信息继续写入命令行或日志，不进入用户消息流。

---

# 2. 界面组成

悬浮球体系只保留一个长期宿主，由三个视觉状态组成。

## 2.1 收起状态：Floating Ball

默认只显示一个小型圆形状态入口。

建议初始视觉参数：

- 命中区域：48 × 48 DIP；
- 可见球体：40～44 DIP；
- 正常状态使用 CDBox 主色；
- 右上角显示任务组角标；
- 外圈显示当前进度或检查活动；
- 鼠标悬停时提高透明度并轻微放大；
- 不显示长文本，不持续遮挡绘图区。

尺寸作为主题令牌统一管理，不允许在多个窗口类中散落硬编码。

## 2.2 展开状态：Floating Center Panel

单击悬浮球展开轻量详情面板。建议默认宽度 360 DIP，最大高度不超过当前工作区高度的 70%，内容超出时内部滚动。

一级只保留：

- `任务`：同步、检查、警告、错误和进行中任务；
- `历史`：最近通知、同步、修复、忽略和失败记录。

顶部固定显示：

- 当前图纸名称；
- 当前总体状态；
- 最近检查时间；
- 进行中的任务；
- 收起按钮。

底部按当前状态显示少量主操作，例如：

- 全部同步；
- 重新检查；
- 查看严重错误；
- 清除已完成历史。

面板不放置属性表单、完整设置页、工程量明细或全部功能导航。

## 2.3 提示状态：Anchored Message Card

通知、命令提示、警告和错误以锚定在悬浮球附近的 WPF 提示卡呈现。

要求：

- 同一时刻最多显示一张主动提示卡；
- 新消息根据优先级替换、排队或合并；
- 提示卡不跟随鼠标，不遮挡当前拾取点；
- 普通提示自动收起，鼠标悬停暂停倒计时；
- 警告和错误进入历史，必要时同时生成任务；
- 点击提示卡或悬浮球可展开对应详情；
- 关闭提示卡不等于忽略同步任务或检查问题。

---

# 3. 状态与视觉规范

## 3.1 状态分为“健康状态”和“活动状态”

不得把“正在检查”与“存在错误”合并为一个枚举。

健康状态：

```text
Normal
Pending
Warning
Critical
```

活动状态：

```text
Idle
Working
WaitingForCadInput
WaitingForUserDecision
Paused
```

例如图纸存在警告且正在重新检查时：

```text
Health = Warning
Activity = Working
```

悬浮球保留警告颜色，同时显示进度外圈。

## 3.2 状态优先级

显示优先级固定为：

```text
Critical > Warning > Pending > Normal
```

状态来源由 `FloatingStatusService` 聚合，WPF 窗口不得自行扫描图纸或推断业务状态。

## 3.3 角标统计

角标统计任务组数量，而不是实体数量。

例如：

```text
标注过期 86 处
管线缺少节点 3 条
默认属性待同步 169 个对象
```

角标显示 `3`，详情中再显示各任务影响的实体数量。

角标建议显示 `1`～`9`，超过后显示 `9+`，避免大数字造成压力和布局抖动。

## 3.4 颜色

统一使用 CDBox 主题颜色服务，不在悬浮球代码中复制颜色常量。

语义建议：

- Normal：CDBox 蓝色或中性蓝；
- Pending：蓝紫或琥珀辅助色；
- Warning：柔和橙色；
- Critical：高辨识红色；
- Success：绿色，仅用于短时完成动画，不作为长期健康状态；
- Working：使用外圈进度，不覆盖健康颜色。

必须同时提供图标、形状或文字信息，不能只靠颜色区分状态。

---

# 4. 交互、位置与动画

## 4.1 拖动与吸附

- 悬浮球可拖动；
- 拖动结束后约束在当前显示器工作区内；
- 靠近屏幕边缘时吸附；
- 展开面板优先向屏幕剩余空间较大的一侧展开；
- 展开和收起不得使悬浮球位置跳变；
- 位置记忆采用“显示器标识 + 吸附边 + 边缘偏移”优先，原始 Left/Top 仅作为兼容数据；
- 原显示器不存在或 DPI 改变时自动收回可见工作区。

## 4.2 DPI 与多显示器

- WPF 使用 DIP；
- AutoCAD 屏幕坐标、Win32 像素和 WPF DIP 必须显式换算；
- 支持 100%、125%、150%、200% DPI；
- 跨不同缩放率显示器移动后重新计算工作区和吸附位置；
- 禁止直接复用未经 DPI 适配的像素坐标。

## 4.3 窗口宿主

- 使用 CAD 主窗口作为 Owner；
- `ShowInTaskbar=false`；
- 不使用应用级永久置顶；
- CAD 最小化时同步隐藏；
- CAD 恢复时按上次状态恢复；
- 没有活动 DWG 时默认隐藏；
- 切换 DWG 时窗口不重建，只切换 ViewModel 与文档状态。

## 4.4 动画

第一阶段复用现有标注浮窗的动画语言，但不能直接复制所有参数。

必须提供：

- 首次出现：轻微缩放和淡入；
- 提示到达：外圈脉冲一次，禁止持续闪烁；
- 展开：从悬浮球位置展开为面板；
- 收起：缩回悬浮球；
- 完成：短时成功反馈后恢复健康状态；
- 状态切换：颜色平滑过渡；
- 设置关闭动画后立即切换，不保留延迟。

动画时长建议控制在 120～220ms。严重错误可以增加一次明显脉冲，但不能循环闪烁。

## 4.5 透明度和光圈

- 第一阶段可复用现有浮窗的常态透明度、悬停透明度、动画和光圈设置；
- 后续可单独增加悬浮球设置，但不得破坏旧设置迁移；
- 收起球体可整体调整透明度；
- 展开面板应优先调整背景透明度，文字和控件保持清晰；
- 鼠标进入悬浮球、提示卡或展开面板时均视为悬停，防止面板之间移动时反复变暗；
- 边缘光圈只表达悬停或状态到达，不得干扰警告颜色。

## 4.6 CAD 输入期间的焦点规则

当 CDBox 正在调用 `Editor.GetPoint`、`GetEntity`、`GetSelection`、`Drag` 等 CAD 输入时：

- 悬浮球进入 `WaitingForCadInput`；
- 命令提示持续显示；
- 悬浮球和提示卡不得抢夺 CAD 键盘焦点；
- 需要时临时禁用展开面板的鼠标交互；
- 用户按 Esc 仍由 CAD 当前输入流程处理；
- 输入结束后恢复悬浮球交互；
- 不得因为点击提示卡而意外取消当前 CAD 命令。

---

# 5. 统一消息协议

## 5.1 禁止以文本关键词作为主协议

现有 `WriteHudMessage` 通过“完成、失败、警告”等关键词推断类型，只可作为旧代码兼容层。

新代码必须发布结构化消息，不得依赖中文文本分类。

建议模型：

```csharp
public sealed class FloatingMessage
{
    public string Id { get; set; }
    public string DocumentId { get; set; }
    public string Source { get; set; }
    public FloatingMessageKind Kind { get; set; }
    public FloatingMessagePriority Priority { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public string Detail { get; set; }
    public double? Progress { get; set; }
    public bool IsIndeterminate { get; set; }
    public bool IsPersistent { get; set; }
    public bool RequiresDecision { get; set; }
    public IList<FloatingAction> Actions { get; set; }
    public DateTime CreatedAt { get; set; }
    public string MergeKey { get; set; }
    public string CorrelationId { get; set; }
}
```

`Kind` 至少包含：

```text
Information
Success
Prompt
Progress
Warning
Error
Decision
Sync
Check
```

## 5.2 路由规则

| 消息类型 | 主动提示 | 自动关闭 | 写入历史 | 生成任务 |
|---|---:|---:|---:|---:|
| 普通信息 | 可选 | 是 | 可选 | 否 |
| 成功 | 是 | 是 | 是 | 否 |
| CAD 操作提示 | 是 | 否，随输入结束关闭 | 否 | 否 |
| 进度 | 是或只显示外圈 | 否，完成后短时关闭 | 完成后记录 | 否 |
| 普通检查问题 | 否 | 不适用 | 是 | 是 |
| 警告 | 视影响决定 | 可选 | 是 | 可选 |
| 严重错误 | 是 | 否 | 是 | 是 |
| 用户决策 | 是 | 否 | 记录结果 | 视业务决定 |

## 5.3 排队、合并和去重

- 同一时刻只显示一张主动提示卡；
- Critical/Error 可以打断普通信息和成功提示；
- CAD 当前操作提示优先保持，不被普通 Toast 替换；
- 相同 `MergeKey` 在短时间内合并；
- 高频重复消息显示“重复 N 次”，不连续弹出；
- 队列必须有上限，溢出时优先丢弃最旧的普通信息，不能丢严重错误；
- 合并只影响呈现，不得丢失诊断日志。

## 5.4 普通通知

普通信息和成功反馈建议显示 4～6 秒。鼠标悬停暂停倒计时，移开后继续。

成功提示不得生成长期角标。若成功中包含跳过项或警告，应以“完成但有问题”任务进入历史或待处理状态。

## 5.5 CAD 操作提示

`GetHudPoint`、`GetHudEntity`、`GetHudSelection`、`DragWithHud` 等现有扩展方法保留调用方式，但内部改为发布 `PromptSession`。

要求：

- 同一命令内新提示替换旧提示，不进入普通 Toast 队列；
- 提示生命周期与 `IDisposable` 会话一致；
- 提示结束即清除；
- 提示卡不出现无意义的关闭按钮和倒计时；
- 提示内容不写入历史，除非操作失败或产生业务结果。

## 5.6 进度

新业务不得再直接创建新的 `CDBoxProgressForm`。

统一进度 API 建议：

```csharp
using (IProgressHandle progress = floatingCenter.BeginProgress(spec))
{
    progress.Report(percent, message);
    progress.Complete(resultMessage);
}
```

进度要求：

- 收起状态用外圈显示；
- 展开状态显示任务名称、阶段、百分比和取消能力；
- 支持确定进度和不确定进度；
- 一个前台主任务占用主进度环，其他后台任务进入任务列表；
- `Complete`、`Fail`、`Cancel` 必须终结句柄；
- 异常或代码遗漏时，句柄释放不得让悬浮球永远停在进行中；
- 取消只发布取消请求，真正取消由业务服务确认；
- AutoCAD API 扫描期间不得通过虚假后台线程伪装“不卡顿”。

## 5.7 决策和确认

最终目标是将确认问题显示为悬浮球锚定的 WPF 决策卡，并暂停发起操作，等待用户明确选择。

原则：

- 决策卡不自动关闭；
- 明确标出默认动作和取消动作；
- Esc 等价于安全取消，不等价于确认；
- 涉及覆盖、批量修改、删除或冲突处理时，不允许只有“知道了”；
- 决策结果写入历史；
- 迁移期间若旧命令必须同步返回 `DialogResult`，可暂时保留同风格的 WPF 模态语义，但不得新增 WinForms MessageBox；
- 待命令层完成异步编排后，再完全并入悬浮球面板。

---

# 6. 同步中心和检查中心接入

同步中心与检查中心共享悬浮球入口，但业务逻辑保持独立。

```text
同步中心：数据之间是否一致，哪些派生数据已经过期？
检查中心：当前图纸是否存在不合法、不完整或失效对象？
```

## 6.1 悬浮球只消费摘要

`FloatingStatusService` 只读取：

- `SyncManager` 的待处理任务摘要；
- `CheckManager` 的问题摘要；
- `ProgressManager` 的活动任务；
- `FloatingMessageStore` 的未读严重消息。

WPF ViewModel 不得直接扫描数据库、计算工程量或分析依赖关系。

## 6.2 任务详情

同步任务和检查问题卡片至少提供：

- 标题；
- 严重程度；
- 影响对象数；
- 影响说明；
- 创建时间或最近更新时间；
- 定位；
- 查看影响；
- 修复、同步或忽略；
- 失败原因。

复杂冲突比较、属性逐项选择或完整检查报告跳转现有独立页面，不在悬浮球里堆叠复杂表格。

## 6.3 对象引用

`SyncTask`、`CheckIssue` 和历史记录不得把长期 `ObjectId` 作为跨事务、跨文档生命周期的唯一标识。

建议同时保存：

- CDBox 稳定对象 ID；
- DWG Handle 字符串；
- 文档身份；
- 必要的对象类型和最后已知摘要。

定位或执行时在当前事务中重新解析实体。对象已删除时返回结构化 `Unavailable`，不得使用陈旧引用继续写入。

## 6.4 同步执行

同步执行继续遵守《悬浮球同步和检查功能开发文档》：

```text
检测 → Dirty → 影响分析 → 同步策略 → 单事务执行 → 结果和历史
```

悬浮球只负责发起、显示进度和呈现结果，不负责实现同步公式。

## 6.5 检查执行

- 实时事件只产生轻量检查提示或脏标记；
- 全图检查由用户、打开图纸后的空闲时机、大型同步完成后或输出前触发；
- 检查结果按规则与问题类型聚合为任务组；
- 普通问题只更新角标，不频繁弹 Toast；
- 严重错误主动提示；
- 自动修复必须经过规则声明，不能由 UI 猜测。

---

# 7. 推荐代码架构

不要求立即拆分程序集，但职责必须分离。

```text
CDBox_Integrated
├── Core
│   ├── FloatingCenter
│   │   ├── FloatingCenterService
│   │   ├── FloatingStatusService
│   │   ├── FloatingMessageStore
│   │   ├── FloatingProgressManager
│   │   ├── FloatingDocumentState
│   │   └── FloatingCenterSettings
│   ├── ChangeTracking
│   ├── Sync
│   └── Check
├── Cad
│   ├── CadDocumentContextService
│   ├── CadUiDispatcher
│   └── ObjectHighlightService
└── UI
    └── FloatingCenter
        ├── FloatingCenterWindow
        ├── FloatingCenterViewModel
        ├── FloatingBallView
        ├── FloatingPanelView
        ├── FloatingMessageCard
        ├── FloatingPlacementService
        └── FloatingAnimationController
```

## 7.1 服务职责

### FloatingCenterService

- 对业务模块提供统一发布入口；
- 不解析 CAD 实体；
- 不依赖具体 WPF 控件；
- 将调用调度到 CAD/WPF UI Dispatcher；
- 管理窗口显示、隐藏和当前文档切换。

### FloatingStatusService

- 聚合任务组数量和最高严重程度；
- 生成不可变 `FloatingStatusSnapshot`；
- 不执行同步或检查。

### FloatingMessageStore

- 维护队列、去重、合并、历史和未读状态；
- 每个文档独立；
- 保留有限数量的会话记录。

### FloatingProgressManager

- 管理活动进度句柄；
- 选择前台主任务；
- 将完成、失败和取消转为结果消息；
- 防止失效句柄留下永久进度状态。

### FloatingCenterViewModel

- 仅消费快照和执行命令委托；
- 不持有 CAD `Transaction`；
- 不在属性 Getter 中访问数据库；
- 所有集合更新在 WPF Dispatcher 上执行。

## 7.2 统一发布接口

建议保留一个稳定门面：

```csharp
public interface IFloatingCenter
{
    void Publish(FloatingMessage message);
    IPromptSession BeginPrompt(FloatingPrompt prompt);
    IProgressHandle BeginProgress(FloatingProgressSpec spec);
    void PublishSyncTask(SyncTaskSummary task);
    void PublishCheckIssue(CheckIssueSummary issue);
    void RefreshDocumentStatus(string documentId);
}
```

业务模块只依赖接口或静态兼容门面，不直接 `new FloatingCenterWindow()`。

## 7.3 现有代码映射

| 现有组件 | 迁移策略 |
|---|---|
| `CDBoxNotificationService` | 保留为兼容门面，内部转发到 `IFloatingCenter` |
| `CDBoxNotificationWindow` | 第一阶段作为回退窗口，完成消息迁移后逐步退出普通通知职责 |
| `CDBoxEditorNotificationExtensions` | 保持现有调用方式，内部改为结构化 PromptSession |
| `CDBoxProgressForm` | 只保留旧功能兼容；新功能使用 ProgressHandle，逐模块迁移 |
| `AnnotationHudWindowBase` | 提取 Owner、DPI、边缘约束、外观和动画的通用能力；不要让悬浮球复制一套不一致实现 |
| `PipeLengthAnnotationPopupAnimationController` | 复用动画语言和可取消控制思想，不强制复用全部尺寸参数 |
| `CDBoxStudioSettings` | 第一阶段复用动画、透明度和光圈设置，新增悬浮球开关及位置设置 |
| `QuantityDashboardLiveMonitor` | 不直接复用为全局监视器；其事件订阅经验可迁移到统一 DocumentState 监听 |

---

# 8. CAD 线程、事件和事务规范

## 8.1 主线程边界

所有 AutoCAD 数据库访问、选择、定位、写事务和 `Editor` 操作必须回到 CAD UI 线程，并在正确文档上下文执行。

WPF 异步回调不得直接：

- 打开或提交 CAD 事务；
- 使用另一图纸的 `ObjectId`；
- 在没有 `DocumentLock` 的情况下写数据库；
- 从后台线程调用 `Editor`；
- 在文档关闭后继续执行延迟动作。

## 8.2 事件回调

`ObjectModified`、`ObjectAppended`、`ObjectErased` 等事件只允许：

- 记录轻量 ChangeHint；
- 标记文档状态待刷新；
- 收集 Handle 或稳定 ID；
- 安排命令结束或 Idle 后处理。

事件回调内禁止：

- 全图扫描；
- 写回关联对象；
- 刷新全部标注；
- 计算完整工程量；
- 弹出大量通知；
- 嵌套写事务。

推荐流程：

```text
ObjectModified
→ ChangeHint
→ 当前命令结束 / Idle 防抖
→ 小范围读取快照
→ DependencyResolver
→ Dirty / CheckIssue
→ FloatingStatusSnapshot
```

## 8.3 后台检查

AutoCAD 数据库对象通常不允许在后台线程自由读取。

全图检查必须拆分为：

1. CAD UI 线程在短事务内分批建立不可变 DTO 快照；
2. 纯业务规则可在后台处理 DTO；
3. 结果回到 UI 线程更新 DocumentState；
4. 真正修复仍在 CAD UI 线程单事务执行。

若当前规则无法安全快照，宁可在 Idle 分批执行，也不能直接 `Task.Run` 访问数据库。

## 8.4 撤销与失败回滚

- 每次同步或批量修复形成一个 AutoCAD Undo 单元；
- 影响分析不写图；
- 执行前重新验证对象和版本；
- 失败时中止整个事务，不允许部分同步；
- UI 在提交成功后再显示成功；
- 历史记录应区分 `Completed`、`PartiallySkipped`、`Failed`、`Cancelled`；
- “跳过锁定对象”属于明确结果，不得伪装成全部成功。

---

# 9. 多文档生命周期

## 9.1 独立 DocumentState

每个 DWG 拥有独立状态：

```text
DocumentIdentity
SyncTasks
CheckIssues
DirtyHints
ActiveProgress
MessageHistory
LastCheckTime
QuantityState
AnnotationState
```

悬浮球窗口全局复用，但当前绑定的 ViewModel 状态随活动 DWG 切换。

## 9.2 文档身份

第一阶段的文档身份至少组合：

- 当前会话内部 ID；
- Database 指纹或可用的 FingerprintGuid；
- 规范化文件路径；
- 未保存图纸的临时 ID。

不得只用文件名区分图纸。

## 9.3 生命周期事件

### 新建或打开

- 建立 DocumentState；
- 延迟至 CAD 空闲后执行快速检查；
- 检查期间显示活动外圈；
- 不因建立状态而修改图纸的已保存标记。

### 激活

- 立即切换悬浮球摘要；
- 取消上一图纸的临时高亮；
- 不销毁其他已打开图纸的会话状态。

### 关闭

- 取消该文档延迟任务和取消令牌；
- 对称解绑事件；
- 释放 `ObjectId`、事务、选择和高亮；
- 关闭未完成的 CAD 输入提示；
- 会话历史可留在内存直到 CAD 退出，但不得继续操作关闭的数据库。

### CAD 退出

- 停止 DispatcherTimer；
- 关闭悬浮球窗口；
- 释放全局事件；
- 不阻碍 AutoCAD 正常退出。

---

# 10. 设置与持久化

第一阶段设置至少包含：

- 启用悬浮球；
- 启动后显示悬浮球；
- 动画开关；
- 常态透明度；
- 悬停透明度；
- 光圈开关和强度；
- 普通提示自动关闭时间；
- 屏幕边缘吸附；
- 自动检查开关；
- 安全小范围自动同步开关。

位置和外观属于用户级设置，保存在 `%AppData%/CDBox` 或现有统一设置存储中。

同步任务、检查问题和历史第一阶段只保存在会话内存，不写入 DWG。以后若需要持久化，必须进行版本设计和迁移，不得直接序列化 `ObjectId`。

设置页面继续使用 CDBox 统一设置页。悬浮球面板只提供“暂时隐藏”和“打开设置”，不复制全部设置控件。

---

# 11. 性能与稳定性预算

## 11.1 UI

- 悬浮球窗口长期只保留一个实例；
- 不为每条通知创建长期 WebView2 或隐藏窗口；
- 普通状态变化不触发全窗口重建；
- 使用不可变快照和差异更新 ViewModel；
- 动画期间禁止重复启动同类 Storyboard；
- 提示队列、历史和任务卡数量必须有限制或虚拟化。

## 11.2 图纸监听

- 连续对象事件必须防抖并批处理；
- 同一命令产生的多次变化按相关 ID 合并；
- 打开大图时先快速摘要，完整检查可延迟和分批；
- 工程量只标记 Dirty，不因每次修改立即全图计算；
- 普通检查问题不主动弹出成百上千条提示。

## 11.3 故障隔离

- 悬浮球异常不得令 CAD 崩溃；
- WPF 未处理异常应记录日志、关闭或重建悬浮球，不进入半提交状态；
- 悬浮球关闭不影响核心 CAD 命令；
- 同步/检查服务不可用时显示明确降级状态；
- 用户应能在设置或命令中重新启动悬浮球；
- 业务日志不得只存在于 UI 历史中。

---

# 12. 第一阶段实施范围

## Phase 0：兼容门面与基础模型

- 建立结构化 `FloatingMessage`、`FloatingStatusSnapshot`；
- 建立 `IFloatingCenter`；
- 让现有 `CDBoxNotificationService` 可以转发到新门面；
- 不立即删除旧通知窗口；
- 增加单元测试验证路由、优先级、合并和文档隔离。

## Phase 1：WPF 悬浮球外壳

- 单实例 WPF 悬浮球；
- 拖动、边缘吸附、位置记忆；
- DPI、多显示器、Owner 和 CAD 最小化；
- 展开/收起动画；
- 模拟状态、角标、进度环；
- 多图纸切换。

本阶段不接真实同步写操作。

## Phase 2：统一通知、提示和进度

- 普通通知进入悬浮球提示卡；
- `WriteHudMessage` 旧代码通过兼容适配器进入；
- CAD PromptSession 接入；
- 新 ProgressHandle 接入；
- 逐步替换 `CDBoxProgressForm`；
- 工作台局部 Toast 保持局部，全局结果同步进入悬浮球。

## Phase 3：检查中心 V1

- 接入《悬浮球同步和检查功能开发文档》第一批检查规则；
- 实现重新检查、聚合、定位和临时高亮；
- 普通问题只更新角标，严重错误主动提示；
- 完成大图分批检查和取消测试。

## Phase 4：同步中心 V1

- 标注 Dirty；
- 工程量 Dirty；
- SyncTask 聚合；
- 查看影响；
- 安全小范围同步；
- 批量同步确认；
- 同步历史和单 Undo 节点。

## Phase 5：迁移和清理

- 统计仍直接使用 MessageBox、命令行提示和独立进度窗的调用；
- 逐模块迁移结构化消息；
- 删除无用通知窗口路径；
- 保留独立更新器；
- 更新开发文档和测试矩阵。

### Phase 5 实施记录（2026-08-12）

- 普通 `OK` 通知继续通过 `CDBoxMessageBox` 兼容入口发布结构化消息，不再创建独立普通通知窗口；
- 必须同步返回 `Yes/No/Cancel/Retry` 等结果的操作保留统一 WPF 决策窗口；
- 悬浮球被用户关闭或不可用时，普通通知和 CAD 操作提示降级写入命令行，不建立第二套浮窗；
- 删除旧 `CDBoxProgressForm`，批量断面、纵断面数据准备、属性默认补填、属性批量赋值和工程量表格导出统一使用 `CDBoxProgressSession` / `IProgressHandle`；
- CASS 表面积标注的流程提示改为结构化通知，外部 CASS 命令本身及独立更新器保持原有边界；
- 无界面自检仍允许直接向命令行输出测试结果。

---

# 13. 测试与验收

## 13.1 UI 验收

1. 悬浮球可拖动、吸附且不会移出屏幕；
2. 位置跨 CAD 会话恢复；
3. 100%～200% DPI 和多显示器下位置正确；
4. CAD 最小化、恢复、切换和关闭图纸时无孤立窗口；
5. 悬浮球不会置顶到其他应用之上；
6. 点击展开只显示任务和历史，不成为功能菜单；
7. 动画关闭后无残留延迟；
8. 普通提示自动关闭，悬停暂停；
9. 警告和错误可从历史重新查看；
10. 角标按任务组统计，超过 9 显示 9+。

## 13.2 CAD 交互验收

1. `GetPoint`、`GetEntity`、`GetSelection`、Jig 期间提示可见且不抢焦点；
2. Esc 仍正确取消 CAD 当前输入；
3. 悬浮球状态刷新不会改变图纸已保存状态；
4. 事件回调中不发生嵌套写事务；
5. 切换图纸不会混用 ObjectId、任务或历史；
6. 定位后对象正确高亮，切换任务或文档时清理高亮；
7. 批量同步只有一个 Undo 动作；
8. 取消、失败和异常不会留下永久进度状态。

## 13.3 消息验收

1. 结构化严重错误不会被普通 Toast 覆盖；
2. 相同高频消息正确合并；
3. Prompt 不进入普通历史；
4. 工作台局部字段校验不重复弹悬浮球；
5. 全局任务完成会进入悬浮球历史；
6. 独立更新器不依赖 CAD 内悬浮球；
7. 无界面自检仍能从命令行读取结果。

## 13.4 同步与检查验收

以《悬浮球同步和检查功能开发文档》的任务、影响分析、冲突、检查规则和撤销要求为准，并额外验证：

- UI 关闭或收起不影响任务状态；
- 忽略问题有明确记录；
- 已删除对象不会使用陈旧引用修复；
- 全图检查期间 CAD 不出现长时间无反馈假死；
- 普通问题不会产生消息风暴；
- 安全小修改自动同步，大范围修改先展示影响，有冲突交给用户。

---

# 14. 开发强制规则

后续 Codex 或开发者修改悬浮球相关代码时必须遵守：

1. 修改前先检查现有通知、进度、WPF 浮窗、文档事件和业务调用，不建立平行体系；
2. 业务模块发布结构化事件，不直接操作悬浮球 WPF 控件；
3. 新功能不得新增 WinForms 普通通知窗或进度窗；
4. 新提示不得依赖中文关键词判断严重程度；
5. 所有 CAD 写操作回到正确文档的 CAD UI 线程；
6. `ObjectModified` 等事件中不执行完整联动；
7. 每个事件订阅必须有对称释放；
8. 多图纸状态必须隔离；
9. 临时 UI 状态不得导致 DWG 被标记为已修改；
10. 动画、位置、DPI 和外观能力应复用统一基础设施；
11. 提交前必须执行单元测试、目标 CAD 集成测试和多文档回归；
12. 若实现与本文不一致，应先更新设计决策和变更记录，不得静默偏离。

---

# 15. 最终定义

悬浮球版本完成后，用户应获得以下一致体验：

> 平时只看到一个不打扰绘图的状态球；操作时提示、进度和结果都从同一位置出现；图纸存在待同步或检查问题时，角标会明确提醒；点击后能看到当前需要处理的事情和 CDBox 最近做过的事情；复杂编辑仍回到合适的专业页面。

其最终架构原则是：

> **业务产生结构化状态，核心服务聚合状态，WPF 悬浮球只负责呈现和发起明确动作。**

其最终产品原则是：

> **小修改自动联动，大修改先展示影响，有冲突交给用户；普通消息不打扰，严重问题不沉默。**

## 变更记录

### 1.0 — 2026-08-12

- 明确悬浮球及轻量详情统一采用 WPF；
- 明确通知、提示、进度、警告、同步和检查的统一入口；
- 明确与现有标注浮窗、通知服务、进度窗口和工作台的迁移边界；
- 明确多文档、CAD 线程、事件、事务、DPI、位置和性能规范；
- 确定分阶段实施与验收要求。
