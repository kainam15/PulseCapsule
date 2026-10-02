# PulseCapsule 0.2 验证记录

日期：2026-10-02。环境：Windows、ASUS TX Air FA401KM_FA401KM、ASUS System Control Interface 3.1.72.0、主屏幕 200% DPI。

**改名、Capsule Host 和硬件监控已部署；最重要的真实 AUTO → COOL → AUTO 验收尚未完成。生产环境没有执行风扇写入。**

## 交付范围

| 项目 | 当前结果 |
| --- | --- |
| Solution、project、namespace、EXE、ICO、窗口和托盘名称 | 已改为 PulseCapsule；原图案保留 |
| Clock / 每个 Quota Provider / System | 统一 Capsule 接口、切换、刷新与生命周期；MainWindow 不依赖 QuotaMonitor |
| Capsule 故障隔离 | 初始化、刷新和恢复失败显示 unavailable，健康 Capsule 可继续使用 |
| 设置 | 启停、排序、记住选择、自动轮播和 System 偏好已实现 |
| 监控 | ASUS CPU 温度、CPU Load、CPU / GPU Fan RPM、120 秒曲线；不支持的第三风扇隐藏 |
| Cooling 状态机与统计 | Demo / fake backend 验证；持久恢复记录、失败回滚、独占控制、20 次切换、负载可比性检查 |
| 实机 Cooling | 只读；当前模式、曲线生效和恢复 AUTO 没有可靠的验证途径 |
| Idle / Load A/B | 未执行；不能给出实机降温收益 |

旧名称只保留在稳定存储身份、兼容检查及历史文档中。工作目录仍为 `D:\app\QuotaPeek`，已有数据仍在 `%APPDATA%\QuotaPeek`；不迁移账号 ID 或重新创建 Credential Store 条目。

## 构建与自动检查

- `build.ps1`：solution Release 构建成功，0 warning、0 error。
- `build.ps1 -Check`：137 checks 通过，包括原有 Provider、解析、重试、历史、提醒和凭据检查，以及新增 Capsule、监控、风扇状态机和改名兼容检查。
- 发布为自包含 Windows x64 单文件，`dist\PulseCapsule.exe`，77,005,868 bytes。
- 部署文件与候选文件 SHA-256 一致：`DEBC3BDFE1134D4C83B0D71F417F79904AAA909A1A434F50E467D4929D6DAC22`。

## 原生桌面验证

各脚本使用独立数据目录；有物理输入的脚本串行执行。发现外部鼠标移动后中止了受影响测试，用户确认桌面空闲后复测。UI Automation 不作为物理输入或焦点行为的替代证明。

| 验证 | 结果和边界 |
| --- | --- |
| `desktop_smoke.py` | 通过：真实点击、前台保持、拖动、解锁快捷键和正常退出 |
| `wheel_smoke.py` | 空闲桌面复测通过：浮动 / Dock、Clock / Quota、半档和方向反转、禁用账号回退、设置和 Dock 保持选择 |
| `drag_smoke.py` | 通过：胶囊不同区域、展开卡片、Clock、位置保存和重启恢复 |
| `outside_click_smoke.py` | 浮动和 Dock 通过：内部点击、外部左右中键、设置窗口边界、反复展开收起 |
| `context_menu_smoke.py` | 通过：按钮 / 右键打开、外部点击、Esc 和进入设置，多轮重复 |
| `taskbar_smoke.py` | 物理验证通过嵌入、DPI、按钮避让、展开收起和前台保持；模拟丢失子窗口后的菜单步骤曾超时，未将整套物理测试记为通过 |
| `taskbar_smoke.py --uia` | 通过：子 HWND 重建、菜单切回悬浮、保存位置、退出清理和重启重新嵌入；物理焦点标记为 not-run |
| `provider_group_smoke.py` | 通过：钱包和 API 卡片独立保留，启停与分组回归 |
| `lifecycle_smoke.py` | 通过：离屏位置恢复、普通退出和重启；没有真实注销、睡眠、重启 Windows 或多显示器切换 |
| `system_capsule_smoke.py` | 浮动 Demo 通过真实滚轮与操作按钮：Clock → Quota → System、尺寸、COOL / AUTO、展开详情、重启记忆、禁用回退 |
| `system_capsule_smoke.py --live` | 实机温度与 RPM 展示通过；写控制禁用并显示原因 |
| `capsule_settings_smoke.py` | UIA 通过：任务栏 Demo AUTO / COOL 不误展开，恢复记录、排序、5 秒轮播顺序及关闭轮播 |
| `capsule_settings_smoke.py --live` | UIA 通过：任务栏实机 CPU 温度、RPM 展开页和禁用的写控制 |

任务栏物理测试的菜单超时保留为未闭环项。读取当前正式实例另外确认了任务栏 System、禁用的 AUTO 和展开页；DPI-aware 截图显示 CPU 53°C、CPU Fan 2400 RPM、GPU Fan 2200 RPM、两分钟曲线及只读原因。这是一次现场读数，不代表长期稳态。

## 正式数据保留

升级前备份位于本机 `.artifacts/backups/before-pulsecapsule-20261002-134950/`，包含 SHA-256 核对后的旧 EXE / 设置，以及 SQLite 一致性备份。旧进程通过窗口正常关闭，未强杀。

新 EXE 正常启动后：

- HoneWallet、Hone、Codex 三个 Provider 的完整配置与备份一致。
- 三个真实数据源均生成 `Ok` 新快照，现有认证可继续使用。
- 原有 1,812 条历史逐条比较一致；最终核验时为 1,821 条，增加的是新刷新记录。
- 原桌面偏好保留，当前选择可以正常变化；旧 EXE 保存在备份目录，正式入口为 `dist\PulseCapsule.exe`。
- 未读取或输出登录 token，未更改 Armoury Crate 驱动、服务或模式。

证据保存在被 Git 忽略的 `.artifacts/`：`deployment.json`、`final-build.txt`、`final-checks.txt`、`system-baseline.jsonl`、`e2e-suite/` 及每个脚本的独立结果目录。失败轮次日志保留，最终结果按上述逐项记录。

## 实机风扇阻塞与后续验收

120 秒只读采样得到 120 个有效温度：平均 44.02°C、P95 46°C、峰值 47°C、平均 CPU Load 28.42%。这只是当前后台负载下的基线，既不是固定 workload，也不是 Idle / COOL A/B。

ASUS 模式端点返回值不能可靠解释为当前模式；按模式读取的工厂曲线也不能证明自定义曲线已生效。因此保持 `CanVerifyState=false`。后续需要先验证保存原状态、写入生效和 BIOS 恢复接管，再做同负载 A/B、真实异常恢复、注销、睡眠、锁屏、Windows 重启及 Armoury Crate 模式冲突实验。

架构和协议参考来源见 [Capsule Host](capsules.md)；安全协议与精确边界见 [System Capsule](system.md)。历史版本证据保留在 [verification.md](verification.md)，不将旧版本测试合并为本轮成功结果。
