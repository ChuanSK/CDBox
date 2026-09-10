# 插件版本服务接入说明

## 本次范围与当前状态

仅修改 AutoCAD 插件内部，不创建网站、管理后台、云函数、数据库、登录、文件上传或发布接口，也不调整发行工具。

插件已移除旧版本清单读取、安装器地址解析、网络下载及 SHA-256 校验链路。版本查询只通过一个 HTTPS 服务地址完成；Stable / Preview 由用户选择并保存。发现更新后，用户确认即可打开固定 CDBox 发布网站。既有本地管理安装器入口保留，用于模块管理和卸载；本地安装器不存在时引导官网下载，不在插件中下载。

5.1.4 已内置用户指定的版本服务：`https://cdbox-d9gsv9fvj6a1aed69-1472504232.ap-shanghai.app.tcloudbase.com/api/releases/latest`。2026-09-09 实测 Stable 和 Preview 均返回 HTTP 200，响应通道与请求一致。设置界面不显示地址，旧设置文件中的地址覆盖值不再生效。

## 配置

CDBox 设置 → 版本检查：

- 更新通道：Stable（正式版）或 Preview（预览版）。
- 检查更新：后台只读查询，显示版本、时间、摘要和日志。
- 前往发布网站：始终可用，包括服务离线时。

更新通道保存在用户设置文件 studio-settings.xml 的 UpdateChannel 中；服务地址由 CDBoxStudioUpdateSourceCatalog.DefaultReleaseServiceUrl 统一内置，不再读取或保存 ReleaseServiceUrl 配置。设置中的“打开组件管理器”启动安装时保存在 `%ProgramData%/CDBox/CDBox组件管理器.exe` 的完整安装器副本。

## 查询约定

对内置的完整 URL 发起 GET，保留已有参数，并添加或替换 channel=stable / channel=preview。请求不包含图纸、宗地、安装模块清单、文件信息或管理凭证。

HTTP 200，UTF-8 JSON：

~~~json
{
  "channel": "stable",
  "release": {
    "version": "5.2.0",
    "title": "CDBox 5.2.0",
    "publishedAt": "2026-09-07T08:00:00+08:00",
    "summary": "本次更新摘要",
    "changelog": "新增：……\n修复：……"
  }
}
~~~

必填字段为 channel、release，以及存在 release 时的 release.version。其他已列字段为可选文本；插件忽略额外字段，不消费安装器、存储位置和下载链接。

某通道尚无公开版本时：

~~~json
{ "channel": "preview", "release": null }
~~~

通道需与请求一致；Stable 不接受带预发行后缀的版本。版本按主、次、补丁及预发行序号比较，同版本不提示更新，正式版高于相同版本号的预发行版。服务回滚到低于本地的版本时，不自动降级、不提示安装旧版本。

非 200、重定向、超时、空响应、错误 JSON、错通道和缺少必填字段均作为检查失败，不能转为“已是最新版”。仅 HTTPS，限制读取超时及响应长度，不执行服务端返回的链接或代码。启动检查失败只记录日志，用户手动检查时展示原因。

## 验证

~~~powershell
dotnet run --project tests/CDBox.CoreTests/CDBox.CoreTests.csproj -c Release
node tests/Test-ReleaseUpdateUi.js
~~~

5.1.4 已完成模拟服务、页面交互及真实接口只读请求验证；当时两个通道返回版本均为 5.1.1，插件不会据此降级。当前正式构建位于 bin/Release514/net48，安装器详见 release-5.1.4.md。未进行云端发布。
