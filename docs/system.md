# System Capsule：实现与验收边界

## 当前可用

- CPU temperature、CPU Load、CPU / GPU Fan RPM，每秒采样一次；最近 120 秒只在内存保存。
- ASUS adapter 独立封装 ATKACPI。CPU 温度使用 `0x00120094`，有效范围 0–125°C。没有裸 EC 读写。
- 独立探测各风扇；unsupported 隐藏，stopped 显示 0 RPM，已支持后读取失败显示错误而不伪造 0。
- ASUS 温度读取失败时尝试已运行的 LibreHardwareMonitor / OpenHardwareMonitor WMI CPU 传感器。不安装内核驱动、不使用 MSAcpi thermal zone 冒充 CPU package 温度；无可用来源时显示 unavailable。
- 展开页显示 CPU、Load、风扇、两分钟 sparkline、Cooling 统计和折叠诊断。收起宽度仍为 252 DIP，任务栏布局沿用原实现。

## 风扇写入尚未开放

**本轮实机只验证了监控，不能宣称 AUTO → COOL → AUTO 已完成。**

FA401KM / ASUS System Control Interface 3.1.72.0 的只读探测成功读到 CPU 温度和两个风扇，但 `DSTS(0x00120075)` 返回 3，不能据此确认当前性能模式。G-Helper 的 `GetFanCurve(mode)` 读取按模式索引的默认曲线，不能用作“刚写入的曲线已生效”的回读证明。也没有获得能够证明原模式完整保存、BIOS 已重新接管的可靠读回机制。

因此 `AsusFanBackend.CanVerifyState` 为 false。生产环境只显示禁用的 AUTO 按钮及原因，**没有任何风扇写入**。允许型号名单只有 FA401KM，但“型号匹配”不等于“控制已验证”。不会停止 Armoury Crate、卸载服务、降低驱动版本或猜测 EC 地址。

`FanController` 已实现可测试的控制协议：硬件身份与状态可验证 → 获取独占控制权 → 原模式与曲线写入持久恢复记录 → 保守 CPU 曲线 → 读回确认 → COOL。写入失败回滚；恢复失败保留记录并显示 RecoveryRequired；下次启动先恢复；关闭、睡眠、会话结束和异常处理进入恢复路径。只设置 CPU fan，GPU 目前只监控。

恢复记录使用 `%ProgramData%\PulseCapsule\fan-recovery.json`，跨账户数据目录使用同一个独占锁文件。只有未来通过验证的 backend 才能获取写控制；不可验证的生产 adapter 不创建恢复文件。`--demo` 使用隔离的模拟 backend，明确标注“不读写真实硬件”。

状态机通过模拟 backend 验证了命令拒绝、异常、未确认写入、恢复失败、重启恢复、异常恢复文件、型号不匹配、并发控制以及 20 次来回切换。这些检查不能替代实机风扇验收。

## 温降统计

开启前的 10 秒有效采样形成 baseline；开启后的 10 秒滚动平均与其比较。至少 8 个带 CPU Load 的有效样本，并覆盖至少 7 秒。当前平均 Load 与 baseline 差超过 10 个百分点时不显示下降箭头，不将突然空闲归因于 Cooling。缺失样本不当作 0，时间倒退的样本不加入缓冲。

## 尚未完成的实机验收

- 找到并验证 FA401KM 当前模式 / 自定义曲线状态的可靠读回机制，完成真实 AUTO → COOL → AUTO。
- 通过后再执行 Idle AUTO 2 min → COOL 2 min → AUTO 2 min，以及相同 workload 的 Load A/B。比较 mean、P95、peak、RPM、Load 和稳定时间。
- 真正强杀时不能运行进程内退出处理；必须依赖持久恢复记录在下次启动恢复。当前已做状态机模拟，尚未以真实风扇写入做强杀实验。
- Windows 注销、重启、睡眠与唤醒，以及 Armoury Crate 模式冲突需要单独做实机验收；没有为了测试而中断用户会话。

只读基线工具（不含账户信息，不写硬件）：

```powershell
dotnet run --project tests/PulseCapsule.Checks -c Release -- --system-probe 120
```

工程依据见 [capsules.md](capsules.md)。协议常量仅参考 G-Helper；状态机、遥测、宿主与 UI 为本项目独立实现。
