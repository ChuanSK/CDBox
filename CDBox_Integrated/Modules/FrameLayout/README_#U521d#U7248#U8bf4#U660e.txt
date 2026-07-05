初版代码目录：FrameLayout

命令：
1. TCFRAMEADD
   添加图框模板。
   - 点选已有图框块：直接读取该块作为模板。
   - 直接回车/空格：选择模板 DWG 文件，插件会将模板 DWG 模型空间图形导入当前图纸并打包为块。
   - 随后点选有效绘制区域两角点，作为后续裁图区域。

2. TCFRAMECUT
   矩形裁图并布置图框。
   - 读取上一步保存的模板配置。
   - 选择总裁图矩形。
   - 指定裁图方向。
   - 输入搭接长度。
   - 自动按有效绘制区域尺寸分幅，插入图框、TC_裁图范围、TC_指北针。

重要说明：
- 模块目录命名为 FrameLayout，不使用 RoadFrame 作为总模块名。
- RoadFrame 后续只建议用于单独的道路分幅算法文件，例如 RoadFrameLayoutService.cs。
- 第一版不真正切断实体，不修改原始图形，只生成图框、裁图矩形和指北针。
- 模板配置保存到：
  AutoCAD/插件运行目录/FrameLayoutConfig/FrameTemplate.ini
- 第一版要求模板最终是 BlockReference。若模板 DWG 是散线散文字，TCFRAMEADD 直接回车导入时会自动打包为块。
