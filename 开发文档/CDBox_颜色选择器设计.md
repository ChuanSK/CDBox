下面文档定位为 **CDBox开发规范补充文档**。重点不是简单实现一个颜色下拉框，而是建立 CDBox 统一颜色管理体系。

# CDBox 颜色选择器（CDBox Color Picker）设计文档

版本：v1.0
适用模块：CDBox Studio / 图层管理器 / 属性编辑器 / 标注系统 / 图纸检查中心
目标：替代当前简单 CAD 索引颜色下拉框，实现兼容 AutoCAD 三类颜色体系，同时提供现代化颜色选择体验。

------

# 1. 设计目标

## 1.1 当前问题

目前 CDBox 中颜色选择直接读取 CAD Color Index：

例如：

```
索引194
索引30
索引256
```

存在问题：

1. 用户无法直观判断颜色
2. 无颜色预览
3. 不支持 RGB 真彩色
4. 不支持 AutoCAD Color Book
5. 不符合现代软件交互习惯
6. 无法支撑 CDBox 后续工程标准化颜色体系

------

# 2. 总体设计原则

## 2.1 兼容优先

必须完全兼容 CAD 原生颜色：

支持：

1. ACI索引颜色
2. TrueColor真彩色
3. Color Book配色系统

任何 CDBox 创建或修改的对象：

必须保证：

- 在无 CDBox 环境中正常显示
- CAD 原生颜色属性保持有效

------

## 2.2 颜色不是UI属性，而是数据对象

禁止：

```
ComboBox.SelectedIndex = 194;
```

改为：

```
CDBoxColor
{
    ColorType;
    Index;
    R;
    G;
    B;
    ColorBook;
    ColorName;
}
```

所有模块统一使用：

```
CDBoxColor
```

禁止各模块自行解析颜色。

------

# 3. 数据结构设计

## 3.1 CDBoxColor

```
public class CDBoxColor
{
    public ColorType Type { get; set; }


    // ACI
    public int Index { get; set; }


    // TrueColor
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }


    // Color Book
    public string BookName { get; set; }
    public string ColorName { get; set; }


    // 显示信息
    public string DisplayName { get; set; }


    // 是否为CDBox标准颜色
    public bool IsCDBoxStandard { get; set; }
}
```

------

## 3.2 ColorType

```
public enum ColorType
{
    ByLayer,

    ByBlock,

    IndexColor,

    TrueColor,

    ColorBook,

    CDBoxStandard
}
```

------

# 4. ColorService颜色服务层

新增：

```
Services
 └── ColorService
       |
       ├── AciColorService
       ├── TrueColorService
       ├── ColorBookService
       └── CDBoxColorService
```

负责：

- CAD颜色读取
- CAD颜色写入
- 类型转换
- 显示名称生成
- 标准颜色管理

------

# 5. 颜色选择器UI设计

名称：

```
CDBoxColorPicker
```

形式：

点击颜色区域打开弹窗。

默认显示：

```
当前颜色

┌──────────────┐
│ ██████       │
│ 索引194      │
│ RGB 255 128 0│
└──────────────┘

▼
```

------

# 6. ColorPicker窗口结构

采用 Tab。

```
CDBox Color Picker


┌─────────────────────────┐
│ 索引颜色 │ 真彩色 │ 配色系统 │ CDBox标准 │
├─────────────────────────┤


内容区域


                     确定 取消

└─────────────────────────┘
```

------

# 7. 索引颜色模块

## 7.1 UI

显示 CAD 255色表。

不要显示：

```
索引194
```

显示：

```
┌─────┐
│ ███ │ 194
│     │ RGB255,128,0
└─────┘
```

------

## 7.2 交互

鼠标悬停：

显示：

```
ACI Index:
194


RGB:
255,128,0


HEX:
#FF8000
```

点击：

返回：

```
CDBoxColor
{
 Type=IndexColor,
 Index=194
}
```

------

# 8. TrueColor模块

## 8.1 UI

类似 Photoshop / VSCode。

包含：

## 色盘

支持：

- 色相选择
- 饱和度
- 明度

## 输入区域

```
R:
[255]


G:
[128]


B:
[0]


HEX:

#FF8000
```

------

## 8.2 输入同步

任何修改：

RGB变化

↓

HEX同步

↓

颜色预览同步

------

# 9. Color Book模块

## 9.1 数据来源

支持 CAD Color Book。

结构：

```
ColorBooks

    PANTONE
       |
       123C

    RAL
       |
       2004
```

------

## 9.2 UI

```
配色系统:


[ RAL ▼]


颜色:


2004
纯橙色


预览:

████


RGB:
244,70,17
```

------

# 10. CDBox标准颜色模块（重点）

## 10.1 目的

用于工程统一。

用户无需知道RGB。

例如：

```
道路工程

给水管
污水管
雨水管
电力管
通信管
```

------

## 10.2 数据

新增：

```
CDBoxColors.json
```

示例：

```
[
 {
  "Name":"污水主管",
  "RGB":"0,128,255",
  "Category":"管线"
 },

 {
  "Name":"错误对象",
  "RGB":"255,0,0",
  "Category":"检查"
 }
]
```

------

# 11. CAD颜色转换

新增：

```
ColorConverter
```

功能：

```
ACI → RGB

RGB → ACI

ColorBook → RGB

RGB → CAD Color
```

------

# 12. CAD写入规则

## 12.1 保留原类型

默认：

```
修改颜色

保持原颜色类型
```

例如：

原：

```
ACI 194
```

修改：

仍写入：

```
ACI
```

------

## 12.2 用户设置

增加：

CDBox设置：

```
颜色输出模式


○ 保持原类型

○ 优先索引颜色

○ 优先真彩色

○ CDBox标准颜色
```

默认：

```
保持原类型
```

------

# 13. 与现有模块集成

## 13.1 图层管理器

原：

```
颜色:
索引194
```

改：

```
颜色:

🟧
索引194
RGB255,128,0
```

点击进入：

CDBoxColorPicker。

------

## 13.2 属性编辑器

字段：

```
颜色
```

使用：

```
CDBoxColorPicker
```

------

## 13.3 标注系统

支持：

```
对象颜色
固定颜色
CDBox标准颜色
```

------

## 13.4 图纸检查中心

错误提示颜色：

使用：

```
CDBoxStandard
```

例如：

```
错误:
红色

警告:
黄色

通过:
绿色
```

------

# 14. WPF实现建议

## 控件结构

```
Controls

 CDBoxColorPicker.xaml

 Views

 ColorPickerWindow.xaml


ViewModels

 ColorPickerViewModel.cs


Models

 CDBoxColor.cs


Services

 ColorService.cs
```

------

# 15. 不推荐方案

禁止：

## 方案1

直接扩展ComboBox：

原因：

- 无法承载多种颜色体系
- 后续扩展困难

------

## 方案2

只增加颜色小方块：

原因：

只能解决当前问题：

```
不知道194是什么颜色
```

无法解决：

- 真彩色
- 标准化
- 工程颜色体系

------

# 16. 开发阶段

## Phase 1（必须）

完成：

- CDBoxColor数据结构
- ACI颜色表
- 颜色预览
- 替换现有颜色ComboBox

------

## Phase 2

完成：

- TrueColor
- RGB/HEX输入
- 最近颜色

------

## Phase 3

完成：

- Color Book
- CDBox标准颜色

------

# 最终目标

CDBox颜色系统不是简单的颜色选择控件，而是：

> 一个兼容AutoCAD颜色体系，同时服务于工程标准化、图层管理、对象属性和图纸检查的统一颜色管理系统。

实现后，所有 CDBox 功能模块禁止直接操作 CAD ColorIndex，必须通过 CDBoxColor 和 ColorService 管理。