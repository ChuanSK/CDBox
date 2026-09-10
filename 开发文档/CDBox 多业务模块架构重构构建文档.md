# CDBox 多业务模块架构重构构建文档

## 1. 文档目的

本次重构的目标，是将当前 CDBox 从“单一大型插件”逐步改造为支持多个专业业务模块的统一插件平台。

未来 CDBox 应允许多个业务模块共用：

- CAD 基础能力；
- WebView2 UI 框架；
- Toast / Dialog / Loading 等交互组件；
- 配置系统；
- 日志系统；
- 更新系统；
- 模板系统；
- Excel / CAD 数据交换；
- 通用对象选择、事务、图层及几何处理能力；
- 其他明确属于公共基础设施的能力。

同时，各专业业务之间必须保持独立。

第一阶段至少需要支持：

- 现有污水管线 / 工程量业务；
- 后续新增的不动产业务。

本次重构**不实现具体不动产业务逻辑**。

本次工作的重点是：

> 将 CDBox 改造成可承载多个业务模块的稳定基础架构，同时确保现有污水管线功能、公共功能和底层基础设施在重构过程中不发生功能退化、数据损坏或插件崩溃。

# 2. 最高优先级原则

本次重构必须严格遵守以下优先级：

1. **现有 CDBox 稳定性**
2. **现有污水管线业务完整性**
3. **现有图纸兼容性**
4. **现有用户数据兼容性**
5. **现有命令兼容性**
6. **现有 UI 和交互兼容性**
7. **架构解耦**
8. **新增业务扩展能力**
9. 代码整洁程度

如果“更干净的架构”和“现有功能稳定”发生冲突：

> 必须优先选择稳定。

禁止为了追求理想架构而一次性重写已经稳定运行的核心业务。

# 3. 重构总体策略

本次重构采用：

> **渐进式模块化重构**

而不是：

> **推倒重写**

不得一次性将现有 CDBox 拆成大量新项目并同时修改全部引用。

应采用以下路径：

```
现有 CDBox
    │
    │ 保持正常运行
    ↓
提取 Shared / Platform 公共基础
    │
    ↓
现有污水业务继续依赖公共基础
    │
    ↓
验证完全稳定
    │
    ↓
建立业务模块边界
    │
    ├── Wastewater
    │
    └── RealEstate
```

每一步都必须：

- 可以独立编译；
- 可以独立测试；
- 可以回退；
- 不影响上一阶段稳定版本。

# 4. 目标架构

最终推荐架构如下：

```
CDBox Platform
│
├── CDBox.Core
│
├── CDBox.Cad
│
├── CDBox.UI
│
├── CDBox.Common
│
│
├── CDBox.Wastewater
│
└── CDBox.RealEstate
```

但本次不要求立即一次拆成以上全部程序集。

第一阶段可以先采用：

```
CDBox.Shared
CDBox
CDBox.RealEstate
```

随后在稳定基础上逐步将 Shared 拆分。

# 5. 模块职责

## 5.1 CDBox.Core

只允许放置与具体 CAD 业务无关的基础能力。

例如：

```
配置
日志
序列化
文件路径
版本信息
异常处理
服务注册
任务状态
模板路径
公共数据结构
```

禁止出现：

```
主管
支管
检查井
宗地
界址点
房屋
```

等业务对象。

# 5.2 CDBox.Cad

负责所有通用 CAD API 封装。

包括但不限于：

```
Document
Database
Editor
Transaction
ObjectId
Entity
Polyline
BlockReference
DBText
MText
Layer
SelectionSet
Geometry
```

以及公共能力：

```
选择对象
框选
拾取点
图层查询
事务管理
对象读取
对象修改
面积计算
长度计算
相交判断
空间查询
对象复制
对象删除
对象创建
```

原则：

> CDBox.Cad 只知道“CAD 对象”，不知道“污水主管”或“宗地”。

例如允许：

```
GetPolylineArea()
FindEntitiesByLayer()
GetIntersectingEntities()
```

禁止：

```
GetMainPipeDepth()
FindParcelBoundary()
CalculateInspectionWellDepth()
```

后者属于业务模块。

# 5.3 CDBox.UI

负责 CDBox 整个产品体系统一 UI。

包括：

```
WebView2 Host
窗口宿主
Toast
Dialog
Loading
Tooltip
主题
动画
页面框架
卡片
表格
属性编辑基础组件
窗口拖动
窗口 Resize
关闭
最小化
DPI
缩放
```

所有业务模块都必须使用同一套 UI 基础。

禁止不同业务重新实现：

```
第二套 Toast
第二套 WebView2 Host
第二套主题系统
第二套窗口框架
```

# 5.4 CDBox.Common

负责可以被多个专业模块直接使用的实际工具。

例如：

```
Excel → CAD
CAD → Excel
文本提取
表格提取
模板填充
通用图层工具
通用对象检查
```

判断标准：

> 如果该功能离开“污水管线”仍然有独立使用价值，应优先考虑 Common。

# 5.5 CDBox.Wastewater

承载现有污水管线业务。

包括当前已有的：

```
主管
支管
检查井
沉泥井
结构层
起终点
管线深度
工程量
对象属性
属性识别
标注
节点—管线联动
工程量导出
```

以及与这些业务强相关的：

```
图层父属性
管线分类
污水业务默认表
业务识别规则
```

第一阶段不要求立即把所有现有代码物理移动到 CDBox.Wastewater 项目。

可以先：

```
逻辑归类
↓
接口隔离
↓
逐步迁移
```

禁止直接大规模移动所有文件。

# 5.6 CDBox.RealEstate

本次只建立模块入口、生命周期和依赖关系。

暂时只需要保证：

```
能够加载
能够注册命令
能够打开统一 UI
能够使用公共服务
能够独立启用 / 禁用
```

本次禁止实现复杂不动产业务。

# 6. 最重要的依赖方向

依赖关系必须严格保持：

```
        Core
       ↑ ↑ ↑
      /  |  \
    Cad UI Common
      ↑   ↑
       \ /
   Wastewater

      ↑
 RealEstate
```

更加严格地说：

```
Wastewater
    ↓
Core / Cad / UI / Common

RealEstate
    ↓
Core / Cad / UI / Common
```

禁止：

```
RealEstate
    ↓
Wastewater
```

禁止：

```
Wastewater
    ↓
RealEstate
```

禁止：

```
Core
    ↓
Wastewater
```

禁止：

```
Cad
    ↓
Wastewater
```

业务模块必须处于依赖链末端。

# 7. 公共功能不得依赖业务对象

例如 Excel → CAD。

错误：

```
ExcelImporter
    ↓
MainPipe
    ↓
InspectionWell
```

正确：

```
ExcelImporter
    ↓
CadEntityFactory
    ↓
CDBox.Cad
```

业务模块可以：

```
Wastewater
    ↓
ExcelImporter
```

但 ExcelImporter 本身不能知道 Wastewater。

# 8. 污水业务稳定性保护策略

这是本次重构最重要的部分。

## 8.1 不允许第一阶段修改核心业务算法

第一阶段不得重写：

```
主管识别
支管识别
井识别
起终点识别
深度计算
结构层计算
工程量计算
标注绑定
节点—管线联动
```

即：

> 第一阶段只改“代码放在哪里”和“依赖怎么调用”，不改“计算结果是什么”。

# 8.2 必须保持原有调用路径可用

假设当前代码：

```
MainPipeService.CalculateDepth()
```

如果需要迁移到：

```
Wastewater.MainPipeService
```

不得一次性删除旧入口。

可以临时保留：

```
MainPipeService
    ↓
Wastewater.MainPipeService
```

作为 Compatibility Adapter。

待全部调用迁移完成并测试后，再删除旧接口。

# 8.3 禁止同时进行架构重构和业务优化

重构期间不得顺手：

- 修改工程量算法；
- 修改管线联动逻辑；
- 改变默认表行为；
- 修改图层识别规则；
- 重做属性系统；
- 重做标注；
- 改变 XData / ExtensionDictionary 数据结构；
- 调整旧图纸对象格式。

如果发现业务 Bug：

记录。

除非属于阻塞性错误，否则不得和架构重构一起修改。

# 9. 图纸数据兼容要求

这是绝对红线。

现有 CDBox 图纸必须：

```
旧版 CDBox 创建
       ↓
新版多模块 CDBox 打开
       ↓
无需转换
       ↓
正常识别
       ↓
正常编辑
```

禁止在本次重构中改变：

```
XData AppName
ExtensionDictionary Key
对象属性字段名
JSON Key
内部 GUID
对象类型标记
图层父属性保存格式
标注绑定格式
```

除非存在明确迁移器。

本次原则：

> 数据格式零迁移。

# 10. 配置兼容

旧配置必须继续正常读取。

例如原有：

```
Config
Settings
DefaultTable
LayerRules
AnnotationSettings
```

不得因为项目拆分而直接改变路径。

错误：

```
原：
%AppData%\CDBox\Config.json

重构后：
%AppData%\CDBox.Wastewater\Config.json
```

这样会导致原配置失效。

第一阶段应继续读取原路径。

未来如果需要分模块：

```
CDBox
├ Core
├ Wastewater
└ RealEstate
```

必须提供兼容读取逻辑。

# 11. 命令兼容

现有命令名必须保持。

例如：

```
SX
SXMRB
GCL
```

以及当前 CDBox 已投入使用的其他命令。

禁止因模块化改为：

```
WASTEWATER_SX
WASTEWATER_GCL
```

内部可以：

```
SX
 ↓
WastewaterCommandService.OpenPropertyEditor()
```

但用户入口不变。

# 12. UI 兼容

重构后现有 CDBox 用户应尽量感知不到底层变化。

必须保持：

```
原菜单
原命令
原窗口
原 Toast
原主题
原交互
```

第一阶段禁止以重构为理由重新设计现有污水 UI。

UI 模块化和 UI 改版必须分开。

# 13. 模块生命周期

建议定义统一：

```
public interface ICDBoxModule
{
    string Id { get; }
    string Name { get; }

    void Initialize();
    void RegisterCommands();
    void RegisterServices();
    void Shutdown();
}
```

未来：

```
WastewaterModule
RealEstateModule
```

均实现该接口。

但必须注意：

> 不允许因为某一个模块 Initialize() 失败导致整个 CDBox 插件加载失败。

# 14. 模块崩溃隔离

模块初始化必须分别捕获异常。

例如：

```
CDBox Start
   │
   ├─ Core OK
   ├─ UI OK
   ├─ Wastewater OK
   └─ RealEstate ERROR
```

结果必须是：

```
CDBox 继续运行
Wastewater 正常运行

提示：
“不动产模块加载失败”
```

而不是：

```
整个 CDBox 无法加载
```

每一个模块：

```
Initialize
RegisterCommands
OpenPage
```

都必须有独立异常边界。

# 15. 服务容器

建议建立轻量级 Service Registry。

例如：

```
ICDBoxServices
```

注册：

```
ILogger
IConfigService
IToastService
IWebViewService
IUpdateService
IExcelService
ICadService
ITemplateService
```

业务模块通过接口使用：

```
Services.Get<IExcelService>()
```

不要：

```
new ExcelService()
```

散落在业务代码中。

目的不是为了引入复杂 DI 框架，而是防止业务模块直接耦合具体实现。

第一阶段允许自行实现简单 ServiceRegistry。

不要求引入大型依赖注入框架。

# 16. 全局静态对象处理

重点检查当前代码中的：

```
static
Singleton
Current
Instance
```

尤其：

```
CurrentDocument
CurrentDatabase
CurrentEditor
CurrentWebView
CurrentSettings
CurrentProject
```

不要在重构时一次性删除。

但需要逐步避免不同模块共享可变静态状态。

特别禁止：

```
Wastewater 修改 Global.CurrentXXX
RealEstate 同时修改 Global.CurrentXXX
```

从而产生业务串扰。

# 17. CAD Document 生命周期

必须特别保护：

```
DocumentCreated
DocumentActivated
DocumentToBeDestroyed
DocumentDestroyed
```

多业务模块不能分别随意注册重复生命周期事件。

建议统一：

```
CDBox.Cad.DocumentManager
```

监听 CAD 生命周期。

然后发布内部事件：

```
DocumentOpened
DocumentActivated
DocumentClosing
```

各业务模块订阅。

这样避免：

```
Wastewater 注册一套
RealEstate 注册一套
Common 注册一套
```

最终引发重复执行。

# 18. Transaction 规则

公共 CAD 层不得长期持有：

```
Transaction
DBObject
Entity
DocumentLock
```

禁止跨业务模块共享正在打开的 DBObject。

原则：

```
打开
↓
读取 / 修改
↓
提交
↓
释放
```

业务模块之间传递：

```
ObjectId
数据 DTO
几何数据
```

而不是：

```
Polyline 实例
DBText 实例
Transaction 实例
```

这是防止 CAD 崩溃的重要要求。

# 19. WebView2 生命周期

当前所有 WebView2 页面应逐步统一由：

```
CDBox.UI.WebViewHost
```

管理。

禁止：

```
Wastewater 创建 WebView2 Runtime 管理器
RealEstate 再创建一套
```

统一处理：

```
Runtime 检测
Environment
Host
页面加载
消息路由
异常
Dispose
```

业务模块只注册：

```
Page
Route
Command
```

# 20. WebView 消息路由

未来建议统一：

```
studio|module|action|args
```

或：

```
cdbox|module|action|args
```

例如：

```
cdbox|wastewater|openQuantity
cdbox|realestate|openParcel
```

现有旧路由必须继续兼容。

不得一次修改全部前端页面消息协议。

可以：

```
旧协议
↓
Compatibility Router
↓
新 Module Router
```

# 21. 更新系统

更新器必须逐渐支持组件化。

未来可以识别：

```
Core
Common
Wastewater
RealEstate
```

但第一阶段不能因此破坏当前 updater。

建议先只修改内部结构：

```
UpdateManifest
├ CoreVersion
├ HostVersion
└ Modules[]
```

如果当前 update.json 仍为单版本：

继续兼容。

必须支持：

```
旧 update.json
新 update.json
```

# 22. 模块版本

建议每个模块拥有独立版本：

```
Platform: 3.0.0
Wastewater: 3.0.0
RealEstate: 0.1.0
```

但第一阶段用户可继续只看到：

```
CDBox Version
```

模块版本主要供：

```
日志
诊断
更新
兼容判断
```

使用。

# 23. 日志系统

所有模块统一：

```
ILogger
```

日志必须包含模块来源。

例如：

```
[Core]
[Cad]
[UI]
[Common]
[Wastewater]
[RealEstate]
```

示例：

```
[Wastewater][ERROR]
MainPipeEditor.Refresh failed
```

以后诊断问题时必须能够区分：

> 是平台失败，还是业务失败。

# 24. 异常处理原则

严禁出现：

```
catch
{
}
```

所有模块级异常：

```
捕获
↓
记录日志
↓
Toast / Dialog
↓
保持插件运行
```

但 CAD 数据事务异常必须：

```
Abort Transaction
```

不允许部分提交。

# 25. 重构实施阶段

## Phase 0：建立安全基线

在修改任何架构前：

创建稳定标签：

```
pre-modular-refactor
```

保存：

- 当前完整源码；
- 当前可运行 DLL；
- 当前 updater；
- 当前配置示例；
- 当前测试图纸。

必须保证可随时回退。

# 26. Phase 1：建立 Shared

建立：

```
CDBox.Shared
```

第一阶段仅迁移最安全的公共代码：

```
Logging
Config
Version
Common DTO
Path
Utility
```

不得迁移复杂 CAD 业务。

编译并测试 CDBox。

# 27. Phase 2：迁移 UI 基础

将明确公共的：

```
WebViewHost
Toast
Dialog
Theme
Window Base
```

迁入 Shared / UI。

此时必须验证：

```
属性识别表
属性默认表
标注设置
图层管理器
工程量页面
Studio 页面
```

全部仍可正常打开。

# 28. Phase 3：迁移 CAD 公共层

逐步迁移：

```
Document
Transaction
Selection
Layer
Geometry
Entity Helper
```

每迁移一组：

立即编译。

禁止一次迁完全部 CAD Helper。

# 29. Phase 4：公共工具迁移

逐步识别：

```
Excel
文本
表格
模板
```

等公共工具。

迁入：

```
CDBox.Common
```

或者第一阶段继续放 Shared。

# 30. Phase 5：建立 Wastewater Module

建立：

```
WastewaterModule
```

首先只负责：

```
注册现有命令
注册服务
注册菜单
```

此阶段可以仍调用原有业务代码。

即：

```
WastewaterModule
      ↓
Legacy Wastewater Code
```

等稳定后再逐步移动真实业务类。

这是最重要的迁移原则。

# 31. Phase 6：逐步迁移污水业务

迁移顺序建议：

```
DTO / Model
↓
纯计算 Service
↓
识别 Service
↓
CAD 写入 Service
↓
UI
↓
命令入口
```

不要反过来。

# 32. Phase 7：建立 RealEstate 空模块

只建立：

```
RealEstateModule
```

实现：

```
Initialize
Command registration
Menu registration
UI route
```

验证：

```
RealEstate 加载失败
```

不会影响：

```
Wastewater
```

# 33. Phase 8：模块开关

未来允许：

```
Wastewater = Enabled
RealEstate = Disabled
```

启动时：

```
ModuleManager
```

读取配置。

禁止卸载 Core。

# 34. 编译策略

每个阶段结束必须：

```
Clean
Build
启动 CAD
加载插件
执行 Smoke Test
```

禁止连续修改多个阶段后才第一次运行 CAD。

# 35. Smoke Test

每次架构提交至少执行以下测试：

## 插件启动

```
CAD 启动
CDBox 加载
无异常 Dialog
无命令行异常
```

## UI

```
打开 CDBox 页面
打开 WebView2
Toast
关闭页面
重新打开
```

## CAD

```
打开旧图纸
切换图纸
关闭图纸
新建图纸
```

## 污水

至少执行：

```
选择主管
打开属性编辑
识别起终点
修改深度
结构层联动
刷新
工程量统计
```

全部通过才能继续下一阶段。

# 36. 污水回归测试

建议建立固定测试 DWG：

```
Test_Wastewater_Base.dwg
```

至少包含：

```
主管
支管
检查井
沉泥井
标注
结构层
已有属性
无属性对象
旧版本对象
```

保存基准结果：

```
主管数量
主管长度
平均深度
井数量
工程量
起终点
结构层
```

重构后逐项比对。

原则：

> 同一张图、同一操作，重构前后结果必须一致。

# 37. 数据完整性测试

重点验证：

修改一次主管深度后：

```
管线属性
结构层
起终点
工程量
标注
```

是否仍按原逻辑联动。

必须防止：

```
UI 显示更新
但 DWG 数据未更新
```

或者：

```
DWG 更新
但标注未同步
```

# 38. 多图纸测试

必须同时打开：

```
Drawing A
Drawing B
```

分别存在不同污水数据。

切换：

```
A → B → A
```

不得出现：

```
B 显示 A 的属性
A 操作修改 B
WebView 缓存错误 Document
```

这是模块化后最容易出现的严重问题之一。

# 39. 模块故障测试

人为让：

```
RealEstate.Initialize()
```

抛出异常。

预期：

```
CDBox 启动成功
Wastewater 可用
Common 可用
提示 RealEstate 加载失败
```

然后人为让：

```
Wastewater
```

失败。

Core / UI 不应因此直接导致 CAD 崩溃。

# 40. DLL 缺失测试

模拟：

```
CDBox.RealEstate.dll
```

不存在。

CDBox 必须仍可运行。

如果：

```
CDBox.Wastewater.dll
```

不存在：

平台可以启动，并明确提示：

```
污水模块未安装 / 加载失败
```

不得发生：

```
FileNotFoundException
→ 整个插件加载失败
```

# 41. 版本不兼容保护

模块需要声明最低平台版本。

例如：

```
RealEstate
RequiresPlatform >= 3.0
```

如果不兼容：

```
禁止加载模块
记录日志
提示用户
```

不得强行加载后崩溃。

# 42. 不允许出现的架构

禁止：

```
Shared
 ↓
Wastewater
```

禁止：

```
Core
 ↓
RealEstate
```

禁止：

```
Common
 ↓
MainPipe
```

禁止：

```
Wastewater
 ↔
RealEstate
```

禁止：

```
所有项目互相引用
```

如果出现循环引用：

必须停止继续迁移并重新调整边界。

# 43. 不允许进行的“顺手优化”

Codex 在本次任务中禁止：

```
重命名大量业务类
调整业务算法
统一所有文件命名
改变所有 namespace
重写属性系统
重写工程量系统
修改数据结构
修改 UI
重新设计菜单
更换 JSON 库
更换日志库
更换 WebView2 架构
引入大型 DI 框架
```

除非是实现模块化所必需。

# 44. Git 提交要求

重构必须采用小提交。

例如：

```
refactor: add shared project
refactor: move logging to shared
refactor: move config service
refactor: add module interface
refactor: register wastewater module
```

禁止：

```
refactor entire project
```

这种一次修改数百个文件的大提交。

# 45. 每次提交要求

每一次提交必须满足：

```
可以编译
```

关键节点必须满足：

```
可以加载 CAD
```

阶段节点必须满足：

```
污水 Smoke Test 通过
```

# 46. Compatibility 层

允许存在临时：

```
Legacy
Compatibility
Adapter
Bridge
```

不要急着删除。

例如：

```
LegacyToast
      ↓
IToastService
```

或者：

```
LegacyMainPipeService
      ↓
WastewaterMainPipeService
```

只要能够降低迁移风险，就是合理的。

稳定后再清理。

# 47. 命名空间建议

最终建议：

```
CDBox.Core
CDBox.Cad
CDBox.UI
CDBox.Common

CDBox.Modules.Wastewater
CDBox.Modules.RealEstate
```

业务实体：

```
CDBox.Modules.Wastewater.Models
CDBox.Modules.Wastewater.Services
CDBox.Modules.Wastewater.Commands
CDBox.Modules.Wastewater.UI
```

不需要第一阶段全部改 namespace。

# 48. Plugin Entry Point

建议最终只保留一个主要 CAD 插件入口：

```
CDBoxPlugin
```

负责：

```
Initialize Platform
Initialize ModuleManager
Load Modules
```

而不是让每个模块独立注册 AutoCAD Plugin Entry。

这样可以集中管理：

```
生命周期
异常
版本
服务
UI
更新
```

# 49. ModuleManager

建议实现：

```
ModuleManager
```

负责：

```
Discover
Load
Initialize
Enable
Disable
Shutdown
```

第一版甚至可以静态注册：

```
Register(new WastewaterModule());
Register(new RealEstateModule());
```

不需要立即做 DLL 动态扫描。

优先稳定。

# 50. 不要过早做真正动态插件系统

当前阶段不要实现：

```
plugins/
    xxx.dll

运行时反射扫描
热加载
热卸载
```

这会显著增加：

```
AssemblyLoadContext
DLL Dependency
Version Conflict
Unload
CAD 生命周期
```

等复杂度。

当前所谓“多模块”首先指：

> 代码和业务边界模块化。

不等于：

> 第三方动态插件平台。

# 51. 安装结构

短期推荐：

```
CDBox/
│
├ CDBox.dll
├ CDBox.Shared.dll
├ CDBox.Common.dll
├ CDBox.Wastewater.dll
├ CDBox.RealEstate.dll
│
├ Web/
├ Templates/
├ Config/
└ CDBoxUpdater.exe
```

未来再根据实际情况优化。

# 52. 公共资源

建议逐步形成：

```
Web/Common
Web/Wastewater
Web/RealEstate

Templates/Common
Templates/Wastewater
Templates/RealEstate
```

但旧路径必须继续兼容。

# 53. 功能注册机制

菜单项、命令面板、页面应允许模块注册。

例如：

```
IMenuRegistry
ICommandRegistry
IPageRegistry
```

Wastewater：

```
Register Wastewater pages
```

RealEstate：

```
Register RealEstate pages
```

从而避免以后在一个巨大：

```
MainMenu.cs
```

里不断追加业务判断。

# 54. 用户复杂度控制

多模块架构的最终用户效果应该是：

只安装 / 启用 Wastewater：

```
CDBox
```

看不到任何不动产内容。

启用 RealEstate：

```
CDBox
CDBox 不动产
```

两个专业工作区可以独立存在。

但是：

```
UI
Toast
Update
Common
```

保持完全一致。

# 55. 开发完成判定

本轮架构重构完成，不以“文件全部移动完”为标准。

而必须满足：

### 架构

- 存在明确 Platform / Common / Business 边界；
- Wastewater 与 RealEstate 无直接依赖；
- 公共服务可供两个模块使用；
- 模块拥有统一生命周期；
- 模块初始化异常能够隔离。

### 稳定性

- 原 CDBox 正常启动；
- 原命令正常；
- 原 UI 正常；
- 原设置正常；
- 原图纸正常；
- 原属性正常；
- 原工程量正常；
- 原管线联动正常；
- 原标注正常。

### 扩展性

能够新建：

```
RealEstateModule
```

并：

```
注册命令
注册页面
调用 Toast
调用日志
调用 Excel
调用 CAD API
```

而无需引用 Wastewater。

满足以上条件即可认为：

> 多业务模块基础架构完成。

# 56. Codex 执行规则

Codex 必须遵守：

## 修改前

先分析现有项目：

```
Solution
Project
Reference
Namespace
Plugin Entry
Command registration
Global static
WebView host
Update
Config
Logging
```

输出依赖关系。

不得先修改后分析。

## 修改过程中

每一个阶段：

```
修改
↓
编译
↓
处理编译错误
↓
确认无新增错误
↓
再进入下一阶段
```

## 遇到高风险代码

如果某代码同时承担：

```
CAD 生命周期
业务逻辑
数据保存
UI
```

不要直接迁移。

先通过：

```
Adapter
Facade
Interface
```

隔离。

# 57. 最重要的原则

整个重构期间始终遵守：

> **先包起来，再拆开。**

而不是：

> **先拆开，再想办法修。**

例如当前存在一个复杂：

```
PipeManager
```

不要第一步拆成：

```
PipeModel
PipeService
PipeRepository
PipeController
PipeAdapter
```

正确方法：

```
Legacy PipeManager
        ↓
IPipeService / WastewaterFacade
```

先建立边界。

待系统稳定后再逐步整理内部实现。

# 58. 回退原则

任何阶段如果出现：

```
CAD 随机崩溃
旧图纸异常
属性丢失
工程量变化
管线联动异常
WebView 不稳定
```

优先：

```
回退当前迁移
```

而不是继续追加 Patch。

必须先找到：

```
是生命周期
Transaction
静态状态
Document
Assembly
还是业务逻辑
```

导致的问题。

# 59. 本轮明确不做

本轮禁止扩展范围到：

- 不动产具体业务实现；
- 宗地识别；
- 地籍调查表；
- 房屋调查；
- 新工程量功能；
- 自定义对象系统；
- UI 大改版；
- 同步中心重做；
- 第三方插件 SDK；
- 插件市场；
- 动态热加载；
- 云端功能。

这些全部属于后续任务。

# 60. 最终目标

此次重构完成后，CDBox 应从：

```
一个不断增加功能的大型 CAD 插件
```

转变为：

```
CDBox Platform

统一底层
统一 UI
统一更新
统一公共工具

        ↓

多个彼此隔离的专业业务模块
```

其中当前已有污水管线模块必须保持：

> **功能零退化、数据零破坏、用户使用习惯尽可能零变化。**

后续新增不动产模块时：

> 不需要继续向污水业务内部加入代码，也不需要复制 CDBox 的公共基础设施。

这才是此次重构成功的最终判定标准。
