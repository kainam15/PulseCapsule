# Capsule Host

`MainWindow` 只管理当前视图、鼠标滚轮、展开、拖动与任务栏 Dock。`ICapsule` 提供统一文字、状态、tooltip、展开内容、操作及生命周期。外壳按列表循环，不认识 Clock、Quota 或 System 的业务类型。

`ClockCapsule` 每秒更新本机分钟显示。每个已启用 Provider 对应一个 `QuotaCapsule`，复用现有 `QuotaMonitor`、Credential Store、SQLite 和供应商分组卡片。Capsule 自己管理刷新；`CapsuleHost` 隔离生命周期异常、保存选择并管理轮播。

参考：

- [PowerToys Run 插件结构](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/modules/launcher/plugins/overview.md)：借鉴稳定接口与业务模块生命周期的分离，不引入动态插件加载或复制其代码。
- [G-Helper ASUS adapter](https://github.com/seerge/g-helper/blob/main/app/AsusACPI.cs) 与 [ModeControl](https://github.com/seerge/g-helper/blob/main/app/Mode/ModeControl.cs)：核对 ASUS 已知协议和返回值，不移植整个设备控制栈。
- [G-Helper #2997](https://github.com/seerge/g-helper/issues/2997)：不能根据 ASUS 品牌推断支持风扇曲线。
- [G-Helper PR #4508](https://github.com/seerge/g-helper/pull/4508)：作者指出标准 BIOS 曲线由固件解释，反应可能很慢；不采用该 PR 提议的反复发送曲线方式。

改名保持存储身份稳定：已有配置、凭据和历史是用户数据，不能因展示名称改变而重新创建账户。
