# CDBox 发布渠道与 update.json 调整方案

**文档版本：V2.0**
**适用版本：CDBox 4.1.1 及后续版本**
**涉及系统：CDBox 发布工具、update.json 生成程序、CDBox 发布网站、腾讯云 COS/CDN、CDBox 更新检测**

# 1. 文档目的

CDBox 当前已经完成安装体系调整。

正式发行物为单个文件，例如：

```
CDBox安装器.4.1.1.exe
```

该文件本身已经包含完整 CDBox 组件，可在无网络环境下完成安装、组件增减、维护和卸载。

因此，本次发布体系调整不涉及安装器内部架构改造。

本次工作的重点为：

1. 将 CDBox 正式发布源从 GitHub / Gitee 等仓库迁移至腾讯云；
2. 官网下载按钮由仓库跳转改为直接下载安装器；
3. 将 `update.json` 从仓库发布模式调整为 CDBox 官方 Release Manifest；
4. 修改 `update.json` 生成代码；
5. 统一网站与客户端更新检测的数据来源；
6. 保证每次生成的新 `update.json` 必然满足发布网站的数据要求；
7. 建立 Stable / Preview 发布通道；
8. 建立发布前校验机制；
9. 防止出现“安装器已更新但网站仍显示旧版本”等数据不一致问题。

# 2. 核心设计原则

新的 CDBox 发布体系应遵守以下原则。

## 2.1 腾讯云作为正式发行源

正式用户访问：

```
CDBox 官网
    ↓
腾讯云 CDN
    ↓
CDBox 安装器
```

不再：

```
CDBox 官网
    ↓
GitHub / Gitee
    ↓
Release
    ↓
安装器
```

GitHub、Gitee、GitCode 后续主要承担：

```
源代码管理
开发历史
Issue
CI/CD
发布归档
灾备
```

不再作为普通用户的主要下载渠道。

# 3. update.json 的新定位

`update.json` 不再只是简单的：

> 最新版本检测文件。

调整后正式定位为：

> **CDBox Release Manifest**

即：

> CDBox 当前正式发布状态的唯一数据源。

以下内容全部由 `update.json` 提供：

- 当前 CDBox 版本；
- 当前安装器版本；
- 发布日期；
- 发布标题；
- 更新摘要；
- 更新日志地址；
- 安装器正式下载地址；
- 安装器归档地址；
- 安装器文件大小；
- 安装器 SHA-256；
- 发布通道；
- Manifest Schema 版本。

# 4. 唯一发布数据源原则

以后必须避免以下情况：

```
官网配置：
4.1.2

update.json：
4.1.1

安装器：
4.1.3
```

新的关系固定为：

```
                 发布工具
                    │
                    ▼
                update.json
                    │
          ┌─────────┴─────────┐
          │                   │
          ▼                   ▼
      CDBox 官网          CDBox 更新检测
```

即：

> **官网不再维护独立的最新版本数据。**

> **客户端更新检测与官网读取同一个 Release Manifest。**

# 5. 总体发布架构

```
                    CDBox 发布工具
                         │
              ┌──────────┴──────────┐
              │                     │
              ▼                     ▼
      CDBox安装器.4.1.2.exe      update.json
              │                     │
              └──────────┬──────────┘
                         ▼
                    腾讯云 COS
                         │
                         ▼
                    腾讯云 CDN
                         │
              ┌──────────┴──────────┐
              │                     │
              ▼                     ▼
          CDBox 官网            CDBox 客户端
              │                     │
              ▼                     ▼
        下载最新安装器           检测新版本
```

# 6. 推荐域名结构

推荐将网站、更新和下载进行逻辑分离。

例如：

```
www.cdbox.xxx
```

负责：

```
官网
产品介绍
文档
更新日志
发布页
```

更新 Manifest：

```
update.cdbox.xxx
```

例如：

```
https://update.cdbox.xxx/stable/update.json
```

安装器下载：

```
download.cdbox.xxx
```

例如：

```
https://download.cdbox.xxx/installer/latest/CDBoxInstaller.exe
```

实际是否使用独立域名可以根据现有域名情况决定。

关键要求是：

> 客户端和网站中不得直接硬编码腾讯云 COS Bucket 原始地址。

CDN 域名应作为正式公开地址。

# 7. 腾讯云发布目录

因为 CDBox 当前只有一个完整安装器作为发行文件，因此发布目录无需拆分 Core、Module 等资源。

推荐：

```
/releases/

├── stable/
│   └── update.json
│
├── preview/
│   └── update.json
│
└── installer/
    │
    ├── latest/
    │   └── CDBoxInstaller.exe
    │
    ├── 4.1.1/
    │   └── CDBoxInstaller-4.1.1.exe
    │
    ├── 4.1.2/
    │   └── CDBoxInstaller-4.1.2.exe
    │
    └── 4.2.0/
        └── CDBoxInstaller-4.2.0.exe
```

如果继续使用当前中文文件名，也可以为：

```
CDBox安装器.4.1.2.exe
```

但建议 CDN 对外下载路径尽量使用英文 ASCII 文件名，以减少：

- URL 编码问题；
- 浏览器兼容问题；
- CDN 规则处理问题；
- 自动发布脚本转义问题。

用户实际下载后的显示名称仍可由 HTTP Header 或安装器自身名称处理。

# 8. latest 与版本归档

安装器同时保留两类地址。

## 8.1 Latest

固定：

```
/installer/latest/CDBoxInstaller.exe
```

用于：

```
官网“下载 CDBox”
```

该 URL 永远保持不变。

## 8.2 Versioned Package

例如：

```
/installer/4.1.2/CDBoxInstaller-4.1.2.exe
```

用于：

- 发布归档；
- SHA-256 校验；
- 历史版本追踪；
- 客户端精确版本升级；
- 出现问题后的版本回退。

原则：

> **已经发布的版本安装器不得覆盖。**

例如：

```
installer/4.1.2/CDBoxInstaller-4.1.2.exe
```

发布之后内容必须保持永久不变。

# 9. update.json V2 推荐结构

推荐从此次调整开始增加：

```
"schemaVersion": 2
```

建议正式结构：

```
{
  "schemaVersion": 2,
  "channel": "stable",

  "generatedAt": "2026-09-03T16:30:00+08:00",

  "release": {
    "version": "4.1.2",
    "title": "CDBox 4.1.2",
    "publishedAt": "2026-09-03T16:30:00+08:00",
    "summary": "优化 CDBox 组件管理与发布更新体系。",
    "releaseNotesUrl": "https://www.cdbox.xxx/releases/4.1.2"
  },

  "installer": {
    "version": "4.1.2",

    "downloadUrl": "https://download.cdbox.xxx/installer/latest/CDBoxInstaller.exe",

    "package": {
      "fileName": "CDBoxInstaller-4.1.2.exe",
      "versionedUrl": "https://download.cdbox.xxx/installer/4.1.2/CDBoxInstaller-4.1.2.exe",
      "sizeBytes": 125829120,
      "sha256": "ABCDEF0123456789..."
    }
  }
}
```

# 10. 字段定义

## schemaVersion

```
"schemaVersion": 2
```

作用：

标识 Manifest 数据结构版本。

以后即使新增字段，也可以通过：

```
schemaVersion
```

进行兼容处理。

# 11. channel

```
"channel": "stable"
```

允许值：

```
stable
preview
```

未来需要时可以扩展：

```
beta
dev
```

但第一阶段只建议支持：

```
stable
preview
```

# 12. generatedAt

表示：

> 此 Manifest 被生成的时间。

例如：

```
"generatedAt": "2026-09-03T16:30:00+08:00"
```

使用 ISO 8601。

# 13. release.version

```
"version": "4.1.2"
```

这是：

> CDBox 对外发布版本。

官网的：

```
最新版本 4.1.2
```

必须读取该字段。

# 14. release.title

例如：

```
"title": "CDBox 4.1.2"
```

供：

```
官网
发布页
更新弹窗
```

显示。

# 15. release.publishedAt

表示：

> 此版本正式发布时间。

例如：

```
"publishedAt": "2026-09-03T16:30:00+08:00"
```

注意：

```
generatedAt
```

与：

```
publishedAt
```

是两个不同概念。

# 16. release.summary

例如：

```
"summary": "优化 CDBox 组件管理与发布更新体系。"
```

用于官网首页或下载页展示简单更新说明。

不应在 `update.json` 中放入非常长的完整更新日志。

# 17. releaseNotesUrl

例如：

```
"releaseNotesUrl":
"https://www.cdbox.xxx/releases/4.1.2"
```

完整更新日志由网站页面承担。

Manifest 只保存对应页面地址。

# 18. installer.version

例如：

```
"version": "4.1.2"
```

当前安装器版本与 CDBox 主版本一致时：

```
release.version
=
installer.version
```

但仍建议两个字段分开保留。

这样未来：

```
CDBox 5.0
Installer 2.3
```

这种版本体系也可以支持，无需修改 Schema。

# 19. installer.downloadUrl

例如：

```
"downloadUrl":
"https://download.cdbox.xxx/installer/latest/CDBoxInstaller.exe"
```

用途：

> 官网正式下载按钮。

该 URL 不带版本号。

官网无需因为每次发布而修改 HTML。

# 20. installer.package.versionedUrl

例如：

```
"versionedUrl":
"https://download.cdbox.xxx/installer/4.1.2/CDBoxInstaller-4.1.2.exe"
```

表示：

> 当前版本安装器的不可变归档地址。

客户端如果需要自动更新，应优先使用：

```
versionedUrl
```

而不是 Latest。

原因是：

> versionedUrl 可以与 SHA-256 建立严格的一一对应关系。

# 21. sizeBytes

必须是：

```
字节
```

例如：

```
"sizeBytes": 125829120
```

禁止直接存：

```
"size": "120 MB"
```

网站自行进行格式化：

```
125829120
↓
120 MB
```

这样避免不同客户端解析问题。

# 22. sha256

必须是安装器实际文件的 SHA-256。

例如：

```
"sha256": "..."
```

必须由生成程序自动计算。

严禁人工填写。

# 23. 网站与 update.json 的正式数据契约

网站必须依赖以下字段：

| 网站内容   | Manifest 字段                 |
| ---------- | ----------------------------- |
| 最新版本   | `release.version`             |
| 发布标题   | `release.title`               |
| 发布时间   | `release.publishedAt`         |
| 更新摘要   | `release.summary`             |
| 更新日志   | `release.releaseNotesUrl`     |
| 下载按钮   | `installer.downloadUrl`       |
| 安装器版本 | `installer.version`           |
| 安装包大小 | `installer.package.sizeBytes` |

这些字段组成：

> **Website Contract**

# 24. Website Contract 必填字段

发布工具必须保证以下字段全部有效：

```
schemaVersion

channel

release.version
release.title
release.publishedAt

installer.version
installer.downloadUrl

installer.package.fileName
installer.package.versionedUrl
installer.package.sizeBytes
installer.package.sha256
```

推荐要求：

```
release.summary
release.releaseNotesUrl
```

同样存在。

# 25. 网站不得再维护版本配置

发布站代码中禁止出现：

```
const latestVersion = "4.1.2";
```

禁止：

```
const downloadUrl =
    "https://github.com/...";
```

禁止：

```
latest.json
website-version.json
config-release.json
```

等第二套最新版本配置。

正式关系必须为：

```
update.json
    │
    └── 网站读取
```

# 26. 发布网站调整

原来的发布网站如果存在：

```
GitHub 下载
Gitee 下载
Release 下载
仓库地址
压缩包下载
```

这些作为主要下载入口全部取消。

下载区域统一：

```
CDBox

最新版本：4.1.2

完整安装程序
文件大小：120 MB

[ 下载 CDBox ]
```

# 27. 网站加载 Manifest

网站启动或页面加载时请求：

```
https://update.cdbox.xxx/stable/update.json
```

得到 Manifest 后：

```
release.version
release.publishedAt
release.summary
installer.downloadUrl
installer.package.sizeBytes
```

填充 UI。

# 28. 下载按钮逻辑

按钮必须绑定：

```
installer.downloadUrl
```

即：

```
https://download.cdbox.xxx/installer/latest/CDBoxInstaller.exe
```

不要绑定：

```
installer.package.versionedUrl
```

原因：

网站只需要“下载最新版”。

客户端升级、版本归档等精确版本场景才使用：

```
versionedUrl
```

# 29. 网站启动时禁止硬依赖 Manifest

Manifest 获取失败不能导致整个官网不可用。

建议逻辑：

```
加载页面
    ↓
获取 update.json
    ↓
成功
    ↓
显示最新版本
```

失败：

```
Manifest 请求失败
    ↓
显示通用下载区域
```

例如：

```
暂时无法获取最新版本信息。
```

不得显示：

```
undefined

NaN MB

Version null
```

# 30. 网站缓存策略

网站可以缓存最近一次成功获取的 Manifest。

推荐：

```
首次加载
    ↓
读取缓存
    ↓
立即显示
    ↓
后台获取最新 Manifest
    ↓
成功
    ↓
更新缓存和页面
```

这样避免网络波动导致版本区域频繁空白。

# 31. Manifest 自身的 CDN 缓存

`update.json` 属于：

> 可变化文件。

推荐：

```
Cache-Control: no-cache
```

或者：

```
Cache-Control: public, max-age=60
```

不建议长时间缓存。

# 32. Latest 安装器缓存

```
installer/latest/CDBoxInstaller.exe
```

同样属于：

> 可变化文件。

不能配置：

```
immutable
```

建议：

```
no-cache
```

或者较短缓存。

# 33. 版本化安装器缓存

例如：

```
installer/4.1.2/CDBoxInstaller-4.1.2.exe
```

属于：

> 发布之后不会变化的文件。

可以：

```
Cache-Control:
public, max-age=31536000, immutable
```

# 34. CORS

如果：

```
https://www.cdbox.xxx
```

访问：

```
https://update.cdbox.xxx
```

则必须配置腾讯云 COS/CDN CORS。

至少允许：

```
Origin:
https://www.cdbox.xxx

Methods:
GET
HEAD
```

如果存在测试站：

```
preview.cdbox.xxx
```

也应加入允许列表。

# 35. update.json 生成程序调整目标

现有生成程序的职责应正式调整为：

```
版本发布信息
+
CDBox 安装器文件
+
发布环境配置
        ↓
Release Manifest
```

不再使用：

```
GitHub URL
Gitee URL
仓库 Release URL
```

作为默认发行地址。

# 36. ReleaseConfig

建议生成程序增加统一发布配置：

```
public sealed class ReleaseConfig
{
    public string Channel { get; set; }

    public string UpdateBaseUrl { get; set; }

    public string DownloadBaseUrl { get; set; }

    public string WebsiteBaseUrl { get; set; }
}
```

Stable：

```
Channel
stable

UpdateBaseUrl
https://update.cdbox.xxx/stable/

DownloadBaseUrl
https://download.cdbox.xxx/

WebsiteBaseUrl
https://www.cdbox.xxx/
```

Preview：

```
Channel
preview

UpdateBaseUrl
https://update.cdbox.xxx/preview/
```

# 37. URL 不得散落硬编码

禁止：

```
"https://download.cdbox.xxx/installer/" + version
```

在多个代码位置分别出现。

应建立统一：

```
ReleaseUrlBuilder
```

例如：

```
BuildInstallerLatestUrl()

BuildInstallerVersionedUrl(version, fileName)

BuildReleaseNotesUrl(version)

BuildManifestUrl(channel)
```

# 38. 推荐 URL 生成规则

## Latest

```
{DownloadBaseUrl}
/installer/latest/CDBoxInstaller.exe
```

## Versioned

```
{DownloadBaseUrl}
/installer/{version}/{fileName}
```

例如：

```
https://download.cdbox.xxx/
installer/
4.1.2/
CDBoxInstaller-4.1.2.exe
```

## Release Notes

```
{WebsiteBaseUrl}
/releases/{version}
```

# 39. 生成程序必须读取真实安装器

发布工具选择：

```
CDBoxInstaller-4.1.2.exe
```

然后自动获得：

```
FileName
FileSize
SHA256
```

禁止要求开发人员手工填写这些字段。

# 40. 文件大小计算

示例：

```
var file = new FileInfo(installerPath);

manifest.Installer.Package.FileName =
    file.Name;

manifest.Installer.Package.SizeBytes =
    file.Length;
```

# 41. SHA-256 自动计算

例如逻辑：

```
using var stream = File.OpenRead(filePath);

var hash = SHA256.HashData(stream);

var sha256 =
    Convert.ToHexString(hash);
```

Manifest 中建议统一使用：

```
大写 HEX
```

或：

```
小写 HEX
```

二选一。

推荐：

```
大写 HEX
```

关键是必须统一。

# 42. 版本号输入

生成程序至少输入：

```
CDBox Version
Installer Version
```

如果当前两者始终一致，可以默认：

```
InstallerVersion = ReleaseVersion
```

但数据模型保持分离。

# 43. 日期自动处理

默认：

```
generatedAt = 当前时间
publishedAt = 当前正式发布时间
```

如果发布工具在上传前提前生成 Manifest：

```
publishedAt
```

可以由发布人员确认。

统一 ISO 8601：

```
2026-09-03T16:30:00+08:00
```

禁止：

```
2026/9/3

2026-9-3

09/03/2026
```

# 44. JSON 输出规范

正式 `update.json` 应统一：

```
UTF-8
无 BOM
camelCase
缩进输出
ISO 8601
InvariantCulture
```

# 45. Manifest C# 模型建议

推荐正式建立：

```
ReleaseManifest
```

模型。

结构：

```
ReleaseManifest

├── SchemaVersion
├── Channel
├── GeneratedAt
│
├── Release
│   ├── Version
│   ├── Title
│   ├── PublishedAt
│   ├── Summary
│   └── ReleaseNotesUrl
│
└── Installer
    ├── Version
    ├── DownloadUrl
    │
    └── Package
        ├── FileName
        ├── VersionedUrl
        ├── SizeBytes
        └── Sha256
```

禁止使用：

```
Dictionary<string, object>
```

拼 Manifest。

应使用强类型模型。

# 46. 引入 update.schema.json

必须新增：

```
update.schema.json
```

用于定义：

> Release Manifest V2 的正式结构。

Schema 至少约束：

```
schemaVersion
channel
release
installer
package
```

及各字段类型。

# 47. Schema 主要约束

例如：

```
schemaVersion
integer
必须为 2

channel
enum:
stable
preview
```

版本：

```
release.version
string
non-empty
```

URL：

```
format:
uri
```

并要求 HTTPS。

文件大小：

```
sizeBytes
integer
> 0
```

SHA-256：

```
64 位十六进制字符
```

正则：

```
^[A-Fa-f0-9]{64}$
```

# 48. Website Contract Validator

仅有 JSON Schema 仍然不够。

还需要增加：

```
Website Contract Validator
```

因为某些字段：

```
JSON 合法
```

并不代表：

```
网站一定能正常发布。
```

# 49. Website Contract 检查项

至少检查：

```
release.version 非空

release.title 非空

release.publishedAt 有效

installer.downloadUrl 有效

installer.package.sizeBytes > 0

installer.package.versionedUrl 有效

installer.package.sha256 有效
```

# 50. Stable URL 额外限制

Stable Manifest 中：

```
installer.downloadUrl
```

和：

```
installer.package.versionedUrl
```

必须属于：

```
download.cdbox.xxx
```

正式下载域名。

禁止主下载链接指向：

```
github.com

raw.githubusercontent.com

gitee.com

gitcode.com
```

# 51. Manifest URL 校验

Stable Manifest：

```
update.json
```

正式发布位置必须属于：

```
update.cdbox.xxx/stable/
```

Preview：

```
update.cdbox.xxx/preview/
```

# 52. 发布工具校验流程

Manifest 生成完成之后：

```
生成 JSON
    ↓
Schema Validation
    ↓
Website Contract Validation
    ↓
URL Validation
    ↓
Installer Validation
    ↓
允许发布
```

任何一步失败：

```
禁止正式发布
```

# 53. Installer Validation

至少检查：

```
安装器文件存在

安装器文件长度 > 0

SHA-256 已成功计算

文件名与 Manifest 一致

版本号有效
```

# 54. 推荐增加发布验证报告

例如：

```
CDBox Release Validation

Channel
Stable

Release Version
4.1.2

Installer Version
4.1.2

────────────────────────

Installer File
PASS

Installer Size
PASS

SHA-256
PASS

Latest URL
PASS

Versioned URL
PASS

Schema V2
PASS

Website Contract
PASS

Stable Domain
PASS

────────────────────────

READY TO PUBLISH
```

只有：

```
READY TO PUBLISH
```

才能执行发布。

# 55. Stable / Preview

从此次调整建议正式分为：

```
Stable
Preview
```

Stable：

```
https://update.cdbox.xxx/stable/update.json
```

Preview：

```
https://update.cdbox.xxx/preview/update.json
```

# 56. 两个通道可以独立存在

例如：

```
Stable
4.1.2
```

同时：

```
Preview
4.2.0-preview.3
```

官网默认读取：

```
Stable
```

Preview 页面才读取：

```
Preview
```

# 57. Preview 不影响正式站

任何测试发布都不得修改：

```
/stable/update.json
```

Preview 测试完整完成后，再进入 Stable 正式发布。

# 58. 正式发布流程

新的正式发布流程建议固定如下。

## 阶段 1：生成安装器

完成：

```
CDBoxInstaller-4.1.2.exe
```

或：

```
CDBox安装器.4.1.2.exe
```

## 阶段 2：发布工具读取安装器

自动：

```
读取文件名
读取大小
计算 SHA-256
```

## 阶段 3：上传版本安装器

上传：

```
/installer/4.1.2/CDBoxInstaller-4.1.2.exe
```

## 阶段 4：验证版本安装器

至少验证：

```
HTTP 200

Content-Length 正确

文件可下载
```

必要时可以下载服务器文件再次计算 SHA-256。

# 59. 更新 Latest

版本安装器验证完成后：

更新：

```
/installer/latest/CDBoxInstaller.exe
```

指向或复制为新版本。

# 60. 验证 Latest

验证：

```
Latest 可正常下载

文件大小正确
```

# 61. 生成最终 Manifest

此时才能正式生成：

```
update.json
```

# 62. Manifest 本地验证

执行：

```
Schema Validation

Website Contract Validation

Stable Domain Validation
```

# 63. Preview 发布验证

首次切换体系时，建议先发布：

```
preview/update.json
```

测试：

```
网站读取

版本展示

发布日期展示

文件大小展示

下载按钮

更新检测
```

# 64. Stable 发布

全部测试通过后，最后上传：

```
/stable/update.json
```

这一操作代表：

> **该版本正式发布。**

# 65. 必须坚持的发布原则

顺序必须为：

```
安装器
↓
Latest
↓
验证
↓
update.json
```

即：

> **先文件，后 Manifest。**

绝不能：

```
先 update.json
↓
再上传 Installer
```

否则可能出现：

```
官网显示 4.1.2
```

但：

```
安装器仍然无法下载
```

# 66. update.json 是发布开关

新版本文件上传并不代表正式发布。

只有：

```
stable/update.json
```

切换到新版本后：

> 新版本才正式对用户可见。

因此：

```
update.json
```

本质上承担：

> Release Commit

的作用。

# 67. 网站更新流程

网站不需要随着每个 CDBox 版本重新部署。

网站只需要：

```
读取 update.json
```

因此以后：

```
4.1.2
↓
4.1.3
↓
4.2.0
```

发布过程中：

> 网站代码无需修改。

# 68. 网站建议的数据加载层

不要让每个组件分别请求 Manifest。

建议统一：

```
ReleaseService
```

负责：

```
FetchManifest()

GetLatestVersion()

GetPublishedAt()

GetDownloadUrl()

GetInstallerSize()

GetReleaseSummary()
```

页面组件只使用：

```
ReleaseService
```

# 69. 网站格式化职责

Manifest 保存：

```
"sizeBytes": 125829120
```

网站负责显示：

```
120 MB
```

Manifest 保存：

```
2026-09-03T16:30:00+08:00
```

网站负责显示：

```
2026年9月3日
```

即：

> Manifest 保存机器数据。

> 网站负责展示格式。

# 70. Manifest 不负责网站 UI

禁止为了页面展示方便加入：

```
"downloadButtonText": "立即下载"

"versionColor": "green"

"badge": "最新版本"
```

这些属于：

```
网站 UI
```

而不是发布数据。

# 71. update.json 生成器不负责网站内容布局

生成器负责：

```
版本数据
安装器数据
发布时间
发布摘要
发布地址
完整性信息
```

不负责：

```
网页 HTML
下载按钮样式
版本卡片布局
动画
颜色
```

# 72. 网站不负责生成发布数据

网站负责：

```
读取
解析
展示
```

不得负责：

```
推算最新版本
拼安装器 URL
计算 Installer 文件名
自行查仓库 Release
```

# 73. GitHub/Gitee 调整

官网公开页面中的：

```
GitHub 下载
Gitee 下载
GitCode 下载
```

应取消主入口。

代码仓库链接可以继续保留在：

```
开发者
开源
项目仓库
反馈
```

等页面。

但不得与：

```
下载 CDBox
```

混为一体。

# 74. 灾备原则

仓库可以继续保留：

```
Release Mirror
update.json Mirror
```

但只用于：

```
腾讯云异常时
```

的灾备。

正常官网：

```
不展示镜像地址
```

正常客户端：

```
Primary = 腾讯云
```

# 75. 旧 update.json 迁移

如果已经有旧版本客户端仍然访问：

```
Gitee update.json
```

则旧地址暂时不能直接删除。

建议：

```
腾讯云
Primary

Gitee
Legacy / Mirror
```

旧地址继续提供：

```
兼容 Manifest
```

一段时间。

# 76. 推荐迁移步骤

## P0 — 定义 Release Manifest V2

完成：

```
ReleaseManifest C# Model

update.schema.json

Website Contract
```

这是整个改造的第一优先级。

## P1 — 修改 update.json 生成器

完成：

```
腾讯云 URL 生成

Installer 文件读取

sizeBytes 自动计算

SHA-256 自动计算

Stable / Preview

Schema Validation

Website Contract Validation
```

## P2 — 腾讯云基础配置

完成：

```
COS

CDN

HTTPS

update 域名

download 域名

CORS

缓存规则
```

## P3 — Preview 发布

上传：

```
Preview Installer

preview/update.json
```

## P4 — 修改发布网站

完成：

```
读取 Manifest

展示 Version

展示 PublishedAt

展示 Summary

展示 Size

绑定 DownloadUrl
```

## P5 — 测试网站

验证：

```
正常网络

Manifest 不存在

Manifest JSON 错误

Manifest 字段缺失

CDN 请求失败

下载按钮

移动端
```

## P6 — Stable 切换

官网主发布数据切换至：

```
stable/update.json
```

主下载切换至：

```
腾讯云 CDN
```

## P7 — 仓库降级

GitHub/Gitee：

```
从 Primary Distribution
```

降级为：

```
Repository / Mirror
```

# 77. 发布工具最终流程

正式流程建议最终做到：

```
选择安装器
    ↓
填写版本信息
    ↓
填写更新摘要
    ↓
选择 Stable / Preview
    ↓
自动读取 Installer
    ↓
自动计算 SHA-256
    ↓
自动生成 URL
    ↓
Generate Manifest
    ↓
Validate
    ↓
READY TO PUBLISH
```

# 78. 发布工具建议界面

例如：

```
CDBox 发布工具

发布通道
[ Stable ▼ ]

CDBox 版本
[ 4.1.2 ]

Installer 版本
[ 4.1.2 ]

Installer 文件
[ CDBoxInstaller-4.1.2.exe ] [选择]

────────────────────────

标题
[ CDBox 4.1.2 ]

更新摘要
[                                ]

发布时间
[ 自动 ]

────────────────────────

Installer

文件大小
120.00 MB

SHA-256
ABCD...

Latest URL
https://download...

Versioned URL
https://download...

────────────────────────

验证

✓ Installer
✓ SHA-256
✓ Schema
✓ Website Contract
✓ Stable URL

READY TO PUBLISH

[生成 update.json]
```

# 79. update.json 生成器禁止事项

禁止：

```
人工填写文件大小
```

禁止：

```
人工填写 SHA-256
```

禁止：

```
手工输入完整下载 URL
```

禁止：

```
业务代码中硬编码 GitHub/Gitee
```

禁止：

```
生成完成后不校验直接发布
```

# 80. 网站禁止事项

禁止：

```
硬编码 latestVersion
```

禁止：

```
硬编码 Installer URL
```

禁止：

```
从 GitHub API 获取正式最新版本
```

禁止：

```
同时维护 update.json 与 website-version.json
```

禁止：

```
Manifest 请求失败导致整个页面崩溃
```

# 81. 发布系统禁止事项

禁止覆盖：

```
/installer/4.1.2/
```

中的历史安装器。

禁止：

```
update.json 已发布
但安装器尚未上传完成
```

禁止：

```
Stable Manifest 指向 Preview 文件
```

禁止：

```
SHA-256 与实际文件不一致
```

# 82. update.schema.json 最低要求

建议至少定义：

```
schemaVersion
channel
generatedAt
release
installer
```

`required` 至少：

```
schemaVersion
channel
generatedAt
release
installer
```

Release 必填：

```
version
title
publishedAt
```

Installer 必填：

```
version
downloadUrl
package
```

Package 必填：

```
fileName
versionedUrl
sizeBytes
sha256
```

# 83. 网站运行时 Schema Version 检测

网站读取 Manifest 时首先判断：

```
schemaVersion
```

如果：

```
2
```

正常解析。

如果未来：

```
3
```

而旧网站不支持：

不要继续猜测字段。

应进入兼容失败状态：

```
当前版本信息暂时不可用。
```

# 84. 发布工具中的兼容性保障

本次实现最关键的机制是：

```
ReleaseManifest Model
        +
update.schema.json
        +
Website Contract Validator
```

三者共同约束。

这样可以避免未来某位开发人员将：

```
"downloadUrl"
```

改成：

```
"url"
```

结果：

```
生成器正常
客户端正常
网站下载按钮突然失效
```

这种问题。

# 85. Website Contract 应进入代码共享

如果网站与生成工具在同一代码体系，可以将字段协议放入：

```
shared/
release-manifest/
```

例如：

```
release-manifest.schema.json
```

让：

```
发布工具
网站
测试
```

全部引用同一个 Schema。

不要复制三份 Schema。

# 86. 推荐自动化测试

至少增加一个标准测试 Manifest：

```
tests/fixtures/update.v2.valid.json
```

以及错误数据：

```
missing-version.json

invalid-sha256.json

invalid-download-url.json

missing-size.json
```

测试：

```
生成器 Validator

网站 Parser
```

必须给出一致结果。

# 87. 网站发布前兼容测试

每次修改 Release Manifest Schema 后必须运行：

```
Manifest
    ↓
Website Parser
    ↓
Download Card
```

自动测试以下值：

```
Version
PublishedAt
Summary
Size
DownloadUrl
```

确保页面正常生成。

# 88. 验收标准——update.json 生成器

必须达到：

- 

  使用 ReleaseManifest 强类型模型；

- 

  支持 `schemaVersion = 2`；

- 

  支持 Stable / Preview；

- 

  不再默认生成仓库 URL；

- 

  CDN URL 由配置统一生成；

- 

  自动读取安装器文件名；

- 

  自动读取安装器文件大小；

- 

  自动计算 SHA-256；

- 

  自动生成 Latest URL；

- 

  自动生成 Versioned URL；

- 

  使用 ISO 8601 日期；

- 

  输出 UTF-8 JSON；

- 

  Schema Validation 正常；

- 

  Website Contract Validation 正常；

- 

  Stable 域名检查正常；

- 

  校验失败禁止正式发布。

# 89. 验收标准——网站

必须达到：

- 

  不再使用仓库作为主下载入口；

- 

  下载按钮直接下载腾讯云安装器；

- 

  最新版本读取 `release.version`；

- 

  发布时间读取 `release.publishedAt`；

- 

  更新摘要读取 `release.summary`；

- 

  文件大小读取 `installer.package.sizeBytes`；

- 

  下载按钮读取 `installer.downloadUrl`；

- 

  网站不存在独立 latestVersion 配置；

- 

  Manifest 请求失败具有降级处理；

- 

  Manifest 数据非法不会导致页面崩溃；

- 

  支持 Schema Version 检测；

- 

  CORS 配置正常；

- 

  CDN HTTPS 正常。

# 90. 验收标准——腾讯云

必须达到：

- 

  Stable Manifest 可通过 HTTPS 获取；

- 

  Preview Manifest 可独立获取；

- 

  Latest Installer 可下载；

- 

  Versioned Installer 可下载；

- 

  版本 Installer 不允许覆盖；

- 

  `update.json` 使用短缓存或 no-cache；

- 

  Latest 使用短缓存或 no-cache；

- 

  Versioned Installer 使用长期缓存；

- 

  官网 Origin 的 CORS 正常；

- 

  CDN 域名成为正式公开下载地址。

# 91. 开发任务拆分

建议拆分为以下任务。

## Release-01

建立：

```
ReleaseManifest V2
```

模型。

## Release-02

建立：

```
update.schema.json
```

## Release-03

重构：

```
update.json Generator
```

## Release-04

增加：

```
SHA256 Calculator
```

## Release-05

增加：

```
ReleaseUrlBuilder
```

## Release-06

增加：

```
ManifestValidator
```

## Release-07

增加：

```
WebsiteContractValidator
```

## Release-08

增加：

```
Stable / Preview
```

配置。

## Web-01

实现：

```
ReleaseService
```

读取 Manifest。

## Web-02

删除：

```
硬编码 Version
```

## Web-03

删除：

```
GitHub/Gitee 主下载
```

## Web-04

下载按钮改为：

```
installer.downloadUrl
```

## Web-05

版本卡片改为 Manifest 驱动。

## Web-06

增加：

```
Manifest Error Fallback
```

## Cloud-01

建立：

```
update.cdbox.xxx
```

## Cloud-02

建立：

```
download.cdbox.xxx
```

## Cloud-03

配置：

```
HTTPS
CORS
Cache
```

## Release-09

完成 Preview 全流程测试。

## Release-10

完成 Stable 正式切换。

# 92. 最终职责边界

## update.json 生成程序

负责：

```
生成可信发布数据
```

## 腾讯云

负责：

```
提供正式 Manifest 与 Installer
```

## 发布网站

负责：

```
消费 Manifest
并向用户展示当前发布状态
```

## Git 仓库

负责：

```
开发和灾备
```

# 93. 最终数据关系

```
                         发布人员
                            │
                            ▼
                      Release Tool
                            │
                   ┌────────┴────────┐
                   │                 │
                   ▼                 ▼
             Installer.exe      update.json
                   │                 │
                   └────────┬────────┘
                            ▼
                     腾讯云 COS/CDN
                            │
               ┌────────────┴────────────┐
               ▼                         ▼
           CDBox 官网              CDBox 更新检测
               │
               ▼
          下载 Installer
```

# 94. 本次调整的核心结果

调整完成后，CDBox 发布一个新版本时：

不需要：

```
修改网站版本号

修改网站安装器地址

修改 GitHub Release 下载按钮

修改多个版本配置
```

只需要：

```
生成新版 Installer
        ↓
上传腾讯云
        ↓
生成并验证 update.json
        ↓
发布 update.json
```

官网和客户端自动得到新版本信息。

# 95. 最终原则总结

本次发布体系调整最终固定以下五条规则。

### 第一条

> **腾讯云是 CDBox 正式发行源。**

### 第二条

> **update.json 是 CDBox 当前发布状态的唯一数据源。**

### 第三条

> **官网只读取 Release Manifest，不单独维护版本。**

### 第四条

> **安装器先上传验证，update.json 最后发布。**

### 第五条

> **ReleaseManifest Model + JSON Schema + Website Contract Validator 共同保证生成的数据一定满足网站要求。**

# 96. 结论

此次改造的本质不是单纯：

```
GitHub URL
↓
腾讯云 URL
```

而是建立：

```
发布工具
    ↓
统一 Release Manifest
    ↓
腾讯云
    ↓
官网 + 客户端
```

的正式发布协议。

其中最优先实现的部分不是网站按钮，而是：

```
ReleaseManifest V2

update.schema.json

Website Contract Validator
```

当这三部分确定以后：

```
update.json
```

就成为 CDBox 发布体系中稳定的数据契约。

以后无论：

```
更换 CDN

增加 Preview

修改官网

增加自动发布

增加历史版本页面

增加数字签名

增加自动升级
```

都可以在这一协议基础上继续扩展，而无需再次重构整个发布体系。
