# Capsule Host

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

修复 WebView2 授权误报：浏览器已登录却被独立 HttpClient 返回 403 时不能判定 Cookie 已失效。当前登录验证调用 WebView2 的 `ExecuteScriptAsync` 读取当前 `/user` 页面的可见流量文本；启用 `WgetCloudUseBrowserSession` 后后台刷新使用同一个持久化 WebView2 用户数据目录访问 `GET /user`，而不是将 Cookie 导出给另一个 HTTP 客户端。只保存浏览器会话模式及目标 URL，不在配置或日志里存储 Cookie。旧版手动 Cookie 模式仍兼容。

便捷授权：设置中点击「在 PulseCapsule 中登录 WgetCloud」，会在应用专属 WebView2 浏览器里打开配置的 HTTPS `/user` 页面。用户自行完成一次登录及 2FA，点击「连接此账户」后只从该 WebView2 中提取目标用户中心域名适用的 Cookie，经只读用户中心请求验证后写入 Windows 凭据管理器。独立的浏览器用户数据目录位于 `%LOCALAPPDATA%/PulseCapsule/WgetCloudBrowser/`；下次打开授权窗口会复用此登录配置文件。不会读取 Chrome 的 Cookie 文件、浏览器历史或密码，也不会触发订阅链接。会话被网站撤销时仍须在授权窗口再次登录；不声称永久有效。使用 Microsoft 官方 WebView2 WPF SDK。


浏览器 Network 实测：用户中心首页的套餐总量来自 `GET /user` 的 HTML document，并非单独 XHR。页面包含 `已使用/总流量：13.18/360 G` 类似标签。现在原生支持只读解析这个 HTML 页面：填写当前站点的 HTTPS `/user` 地址和用户中心会话 Cookie 即可，不需要 JSON 字段。此模式读取显示的已用/总量及可选重置日、到期日；站点 HTML 改版会明确提示错误，绝不回退到订阅链接。

设置 → 添加数据源 →「WgetCloud 流量」。此数据源**永不访问订阅 URL**，节点订阅仍由代理客户端负责。WgetCloud 尚未公开用户中心 JSON API 文档，因此需先在浏览器开发者工具 Network 中找到真实的只读统计 JSON 请求；程序不猜测配额路径。用户填写请求完整 HTTPS 地址、GET/POST 方式以及至少两个字段路径（剩余、已用、总额度）。支持数值为 GB 或字节，缺少的一个字段由其余两个推算并校验，任何字段错误都明确显示失败。

认证采用用户自行在本机填写的会话 Cookie（`name=value`），加密存储在 Windows 凭据管理器；不要输入登录密码或私人订阅链接。仅允许无查询参数的 `https://*.wgetcloud.org/user/...`，禁止订阅/重置/购买等路径和任何跨站跳转；POST 仅可用于观察到的 `/user/log_month` 统计路径，其他仅限 GET。默认每天刷新一次，至少间隔 60 分钟；403、429 等失败遵守重试策略和原有缓存。旧版凭据即使保留订阅 URL 也会被拒绝并且不会触发网络请求。接口字段和实际鉴权还需在真实登录网站的 Network 中核对；在未配置之前 WgetCloud 不会联网。
