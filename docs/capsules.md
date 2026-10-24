# Capsule Host

当前架构说明；命令见 [开发与验证](development.md)，其他主题见 [文档索引](README.md)。

`MainWindow` 只管理当前视图、鼠标滚轮、展开、拖动与任务栏 Dock。`ICapsule` 提供统一文字、状态、tooltip、展开内容、操作及生命周期。外壳按列表循环，不认识 Clock、Quota 或 System 的业务类型。

`ClockCapsule` 每秒更新本机分钟显示。每个已启用供应商组对应一个 `QuotaCapsule`，复用现有 `QuotaMonitor`、Credential Store、SQLite 和供应商分组卡片。同站点的 Hone 钱包与 API 只占一个切换位置，金额、凭据和刷新仍由各数据源独立管理。Capsule 自己管理刷新；`CapsuleHost` 隔离生命周期异常、保存选择并管理轮播。

设置 → **胶囊管理** 使用实际模块名称统一排序和启停。时钟、各供应商与硬件监控可以任意交错；滚轮和轮播直接使用该顺序，自动轮播控件位于列表底部。拖动只在列表内正常松手时保存，取消或持久化失败保留原顺序。此页移除系统监控子选项，但不改写已有 `SystemPreferences` 或开放硬件写入。上次选中 ID 持久化到设置中；设置账户时保留胶囊管理页刚保存的选择和偏好。

`AppSettings.Version = 2` 在 `Capsules` 中保存 `clock`、`system` 与供应商模块 ID。模块使用钱包优先的数据源 ID 生成 `quota:<provider-id>`；同组其他 ID 在加载时归入该模块。v1 的 `quota` 位置展开为原顺序的供应商模块，旧总开关停用时迁移为各数据源停用；其他启用状态及上次选中来源保留或映射到所属模块。新增模块追加，已删除来源不产生空模块。`ProviderGroup` 的站点分组同时供设置与卡片使用；Provider ID、凭据标识、SQLite 历史不迁移或重建。

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
