# CDBox Studio Preview 5.1.0

CDBox 5.1.0 固定为业务模块物理分离后的首个统一安装器预发行版本。

## 本次发布

- 基础组件、公共业务、污水业务和不动产业务完成独立装配，安装器可按 AutoCAD 版本与业务模块安装、增减、修复和卸载。
- 插件内网络更新统一启动安装器执行完整替换，取消 ZIP 更新包和仓库下载源。
- 发布协议升级为 Release Manifest V2；官网与客户端以腾讯云静态托管/CDN 上的 `update.json` 为唯一正式发布数据源。
- 新增 Stable/Preview 独立通道、强类型 Manifest 生成器、JSON Schema、网站数据契约、官方域名和安装器完整性校验。
- 版本安装器使用不可变地址，官网 Latest 地址保持稳定；发布流程固定为先验证安装器、最后发布 Manifest。

## 版本信息

- 产品版本：5.1.0
- 版本码：50100
- 发布通道：Preview
- 安装器：`CDBoxInstaller-5.1.0.exe`
