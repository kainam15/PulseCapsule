# Capsule Host

当前架构说明；命令见 [开发与验证](development.md)，其他主题见 [文档索引](README.md)。

`MainWindow` 只管理当前视图、鼠标滚轮、展开、拖动与任务栏 Dock。`ICapsule` 提供统一文字、状态、tooltip、展开内容、操作及生命周期。外壳按列表循环，不认识 Clock、Quota 或 System 的业务类型。

`ClockCapsule` 每秒更新本机分钟显示。每个已启用 Provider 对应一个 `QuotaCapsule`，复用现有 `QuotaMonitor`、Credential Store、SQLite 和供应商分组卡片。Capsule 自己管理刷新；`CapsuleHost` 隔离生命周期异常、保存选择并管理轮播。

设置 → **Capsules 与 System** 可以启停、上下排序、设置自动轮播及温度 / RPM / 风扇控制偏好。Provider 在 Quota 所在位置依原数据源顺序展开，新增 Capsule 无需修改滚轮算法。上次选中 ID 持久化到设置中；设置账户时保留 Capsule 页面刚保存的选择和偏好。

参考：

- [PowerToys Run 插件结构](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/modules/launcher/plugins/overview.md)：借鉴稳定接口与业务模块生命周期的分离，不引入动态插件加载或复制其代码。
- [G-Helper ASUS adapter](https://github.com/seerge/g-helper/blob/main/app/AsusACPI.cs) 与 [ModeControl](https://github.com/seerge/g-helper/blob/main/app/Mode/ModeControl.cs)：核对 ASUS 已知协议和返回值，不移植整个设备控制栈。
- [G-Helper #2997](https://github.com/seerge/g-helper/issues/2997)：不能根据 ASUS 品牌推断支持风扇曲线。
- [G-Helper PR #4508](https://github.com/seerge/g-helper/pull/4508)：作者指出标准 BIOS 曲线由固件解释，反应可能很慢；不采用该 PR 提议的反复发送曲线方式。

改名保持存储身份稳定：已有配置、凭据和历史是用户数据，不能因展示名称改变而重新创建账户。

## WgetCloud 流量

### 浏览器会话（当前连接方式）

设置中点击「在 PulseCapsule 中登录 WgetCloud」，在应用专属 WebView2 打开配置的 HTTPS `/user` 页面。用户自行完成登录和 2FA，再点击「连接此账户」。`WgetCloudLoginWindow` 通过 `WgetCloudBrowserSession.ReadPageAsync` 读取当前页面可见的套餐流量；成功后保存 `WgetCloudUseBrowserSession` 及目标 URL。

后台 `WgetCloudBrowserQuotaProvider` 使用同一个持久化 WebView2 profile 访问用户中心，不导出 Cookie 给独立 HttpClient，也不把浏览器 Cookie 写入 settings 或日志。profile 位于 `%LOCALAPPDATA%/PulseCapsule/WgetCloudBrowser/` 下，按站点隔离；不会读取 Chrome 的 Cookie 文件、浏览器历史或密码。网站撤销会话后需重新登录，不保证永久有效。此功能使用 Microsoft WebView2 WPF SDK，并需要可用的 WebView2 Runtime。

用户中心首页的套餐总量来自 `GET /user` 的 HTML document；解析已用 / 总量及可选重置日、到期日。页面改版无法解析时明确报错，不回退到订阅链接。浏览器中可读而独立 HttpClient 返回 403，不能直接当作当前 WebView2 会话失效。

### 手动 Cookie 与统计 JSON

已有手动 Cookie 模式继续由 Windows Credential Manager 保存用户填写的 `name=value`，不输入登录密码或私人订阅链接。HTML 模式读取用户中心；高级 JSON 模式由用户根据真实 Network 请求填写完整 HTTPS URL、方法和字段路径，不猜测未公开的接口。至少提供剩余、已用、总额中的两个字段，按 GB 或字节换算，推算缺少的一个并校验一致性。

### 请求边界

流量请求仅允许无查询参数的 `https://*.wgetcloud.org/user/...`，拒绝订阅 / 重置 / 购买路径和跨站跳转；POST 只允许已观察到的 `/user/log_month`，其他请求限 GET。浏览器导航另允许同站登录路径，仍阻止下载、订阅和账户变更页面。节点订阅由代理客户端负责。

默认每天刷新，最小间隔 60 分钟；403 / 429 等错误沿用缓存和退避。未配置时不联网；旧配置含订阅 URL 也会在发出请求前拒绝。真实接口与鉴权只能用对应站点的当前会话核实，不能用 fixture 或其他站点代替。
