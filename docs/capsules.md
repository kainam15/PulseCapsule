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

设置 → 添加数据源 →「WgetCloud 流量」。此数据源**永不访问订阅 URL**，节点订阅仍由代理客户端负责。WgetCloud 尚未公开用户中心 JSON API 文档，因此需先在浏览器开发者工具 Network 中找到真实的只读统计 JSON 请求；程序不猜测配额路径。用户填写请求完整 HTTPS 地址、GET/POST 方式以及至少两个字段路径（剩余、已用、总额度）。支持数值为 GB 或字节，缺少的一个字段由其余两个推算并校验，任何字段错误都明确显示失败。

认证采用用户自行在本机填写的会话 Cookie（`name=value`），加密存储在 Windows 凭据管理器；不要输入登录密码或私人订阅链接。仅允许无查询参数的 `https://*.wgetcloud.org/user/...`，禁止订阅/重置/购买等路径和任何跨站跳转；POST 仅可用于观察到的 `/user/log_month` 统计路径，其他仅限 GET。默认每天刷新一次，至少间隔 60 分钟；403、429 等失败遵守重试策略和原有缓存。旧版凭据即使保留订阅 URL 也会被拒绝并且不会触发网络请求。接口字段和实际鉴权还需在真实登录网站的 Network 中核对；在未配置之前 WgetCloud 不会联网。
