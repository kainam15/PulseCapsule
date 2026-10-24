# PulseCapsule 协作指南

PulseCapsule 是 Windows 原生 WPF / .NET 10 桌面应用，统一承载 Clock、Quota、System。本文件保留项目约束和入口；按任务读取所需文档，无需每次通读文档目录。

## 工作约定

- 始终使用中文回答，保留英文专有名词。
- 代码类任务默认先调研 GitHub 上相关成熟实现、源码、Issue 和 PR，再结合当前项目设计；说明重要来源、采用的思想和适用边界。
- 用户的明确要求优先于本仓库的工作流建议。普通实现、文档整理、隔离构建和检查可连续完成；已获授权的步骤无需反复确认。
- 应用代码或界面修改完成并通过相应检查后，默认直接更新当前使用的正式版本（`dist/PulseCapsule.exe`）并正常重启，无需再询问是否部署，不以生成候选 EXE 作为交付终点。按[开发指南](docs/development.md)完成备份、文件 hash 和实际运行路径复核，保留现有配置、凭据与历史。用户明确限定只做调研、文档或候选验证时，按其限定范围执行。
- 保留工作树中已有改动。只有用户明确要求时才 commit / push；提交格式见 [.github/commit-message.instructions.md](.github/commit-message.instructions.md)。
- 修复以具体日志、失败路径或复现为依据。验证范围匹配改动；通过后不为凑数量重复检查。

## 项目约束

- `MainWindow` / `CapsuleHost` 管理外壳和切换，具体业务留在各 Capsule / service；保持现有 WPF 架构。WebView2 用于已有 WgetCloud 登录会话。
- `DataIdentity` 将品牌与数据身份分开：新安装用 `%APPDATA%\PulseCapsule`，发现旧 `%APPDATA%\QuotaPeek` 则沿用；Credential Manager 和 mutex 的 `QuotaPeek` 标识、Provider ID、SQLite 历史不能随改名重建。
- 钱包、key 额度、Codex 百分比、流量和不同币种保持独立语义。失败保留旧值及原时间戳；未知值不伪造为 0；保留退避和 `Retry-After`。
- 真实风扇控制当前只读。可靠状态读回与恢复验证未完成时不能开放 COOL；demo / fake backend 通过不代表硬件写入通过。
- 不为尚未发布的内部接口新增 alias、shim 或双入口；已有用户数据身份及已持久化格式需要保留或明确迁移。
- 源码检查、WPF 渲染、UIA、真实输入、候选 EXE、已部署 EXE 分别报告。开始可见蓝框 Computer Use 前取得明确同意；物理输入测试需空闲桌面并串行执行。
- 正式 EXE 按上述默认交付约定更新；操作真实凭据、改变系统设置或执行硬件写入时，仍需核对用户授权范围。

## 按任务查找

| 任务 | 入口 |
| --- | --- |
| 使用方式、数据源、数据目录 | [README.md](README.md) |
| 源码入口、构建、检查、候选发布与验证边界 | [docs/development.md](docs/development.md) |
| Capsule 生命周期、Host、WgetCloud 会话 | [docs/capsules.md](docs/capsules.md) |
| 设置与卡片 UI | [设计规范](docs/design.md)、[设置回归矩阵](docs/settings-ui-regression.md) |
| System 温度、RPM、风扇能力 | [docs/system.md](docs/system.md) |
| 文档定位、历史证据、维护规则 | [docs/README.md](docs/README.md) |

## 项目 skills

skills 位于 `.agents/skills/`，按触发条件读取对应 `SKILL.md`；它们不要求预先加载其他 skills。

| Skill | 适用任务 |
| --- | --- |
| [pulsecapsule-desktop-validation](.agents/skills/pulsecapsule-desktop-validation/SKILL.md) | 验证 WPF 交互、任务栏行为或候选 / 部署 EXE |
| [pulsecapsule-system-telemetry](.agents/skills/pulsecapsule-system-telemetry/SKILL.md) | 判断 System 读数来源、排查传感器或评估风扇能力 |
| [pulsecapsule-docs-maintenance](.agents/skills/pulsecapsule-docs-maintenance/SKILL.md) | 整理项目 Markdown、维护文档索引或调整 agent 指南 |

## 完成标准

完成本次要求的实现和相关文档，运行能证明改动的检查，并说明实际结果与尚未验证的部分。文档改动运行 `python tools/check_docs.py` 和 `git diff --check`；代码 / UI 的验证选择见开发指南。检查失败时修复本次引入的问题；环境阻断、历史记录和未运行项不能报告为通过。
