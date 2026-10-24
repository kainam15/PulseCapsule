# 文档索引与维护

本目录使用小写 `docs/`，集中维护设计、开发和专题文档。根目录保留产品入口 `README.md` 与协作入口 `AGENTS.md`，设计规范位于 [design.md](design.md)。根据任务选读下表；历史材料不作为当前代码或正式 EXE 已通过验收的证明。

## 当前指南

| 文档 | 用途与维护时机 |
| --- | --- |
| [产品说明](../README.md) | 用户操作、功能范围、数据源和存储行为变化时更新 |
| [协作指南](../AGENTS.md) | 项目长期约束、入口或授权边界变化时更新 |
| [开发与验证](development.md) | 构建参数、源码职责、检查入口或部署流程变化时更新 |
| [设计规范](design.md) | 设置、卡片、交互语义与共享样式变化时更新 |
| [Capsule Host](capsules.md) | Capsule 生命周期、宿主职责或 WgetCloud 会话变化时更新 |
| [System Capsule](system.md) | 遥测来源、能力判断、恢复协议或真实硬件证据变化时更新 |
| [设置回归矩阵](settings-ui-regression.md) | 设置 / 卡片行为变化时更新用例；新验收结果另加日期段落 |

## 历史与研究

| 文档 | 范围与使用限制 |
| --- | --- |
| [数据源与工程研究](research.md) | 2026-09-25～26 的协议和实现调研；复用前核对当前源码及上游状态 |
| [QuotaPeek 验证记录](verification.md) | 2026-09-25～26 的早期构建、现场交互、机器读数和未完成项 |
| [PulseCapsule 0.2 验证记录](pulsecapsule-verification.md) | 2026-10-02 的改名、数据保留、Capsule Host 和硬件监控验收 |

历史文件保留原路径，便于已有引用继续定位证据。旧名称、PID、hash、测试数量、账户读数和机器路径只描述当时状态，不做全局替换或当作最新基线。后续复测新增注明日期和对象的记录，并链接相关历史段落。

## 项目 skills

| Skill | 入口与示例 |
| --- | --- |
| 桌面验证 | [SKILL.md](../.agents/skills/pulsecapsule-desktop-validation/SKILL.md)；`$pulsecapsule-desktop-validation 验证候选 EXE 的任务栏点击行为` |
| System 遥测 | [SKILL.md](../.agents/skills/pulsecapsule-system-telemetry/SKILL.md)；`$pulsecapsule-system-telemetry 核实温度与 RPM 的来源` |
| 文档维护 | [SKILL.md](../.agents/skills/pulsecapsule-docs-maintenance/SKILL.md)；`$pulsecapsule-docs-maintenance 整理本次变更涉及的文档` |

使用标准 `AGENTS.md` 文件名，不额外维护 `agent.md` 副本。项目 skills 随仓库保存在 `.agents/skills/`。从本仓库或子目录启动 Codex；若列表未刷新，重开会话，也可直接指定上表的文件路径。创建文件和通过格式校验不等于已经验证当前客户端的自动发现。

## 维护规则

- 一个事实有一个主要维护位置。README 面向使用者，开发指南维护命令，设计文档维护交互约束，skill 维护专项决策流程；其他位置链接引用。
- 当前指南描述已实现行为；计划标明“计划”，未验证项标明“待验证”。带日期的记录保留其证据边界，不能在后续整理时改写成当前成功结论。
- 新文档使用明确主题的英文小写连字符文件名，加入本索引；只有需要独立上下文时才拆文件。不要为每次小改动新建总结文档或复制整套规则。
- 新 skill 的 `description` 只写能力和触发条件；细节链接现有项目文档。保持默认自动选择，不复制个人机器上的通用 skills 或写死其安装路径。
- 移动 / 改名文件前搜索引用，同时更新 Markdown、项目资源和脚本路径。`docs/images/logo.png` 是构建资源，不是可随意归档的插图。
- Markdown 使用 UTF-8 无 BOM、LF、结尾换行；链接使用仓库内相对路径并匹配大小写。机器专属路径或 `.artifacts` 证据写为代码文本，不伪装成每个 clone 都可用的链接。
- API key、Cookie、token 和私人订阅链接不进入文档。原始日志、截图、候选包和本地检查报告留在 Git 忽略的 `.artifacts/`。

文档检查从仓库根目录运行：

```powershell
python tools/check_docs.py
git diff --check
```

检查器只读检查 Git 可见的 Markdown（含未跟踪文件）的 UTF-8 / LF、常用链接语法的本地文件路径与大小写。不请求外部 URL，不校验标题锚点或完整 CommonMark 语法；它不能判断文字是否仍符合源码。

## 本次规范的参考依据

2026-10-24 整理时参考：

- [OpenAI：Rethinking skills and prompts for GPT-6 Astra](https://developers.openai.com/blog/rethinking-skills-and-prompts-for-gpt-6-astra)：采用短入口、精确触发、按需读取和与任务匹配的完成标准。
- [AGENTS.md 官方说明](https://learn.chatgpt.com/docs/agent-configuration/agents-md)与 [Build skills](https://learn.chatgpt.com/docs/build-skills)：核对文件名、项目目录和技能元数据格式。
- [openai/skills 的 skill-creator](https://github.com/openai/skills/blob/main/skills/.system/skill-creator/SKILL.md)：借鉴简洁说明与渐进披露；结合当前随附 skill-creator 的指导，保留项目实际需要的三个流程。
- [Codex host_roots.rs](https://github.com/openai/codex/blob/main/codex-rs/ext/skills/src/host_roots.rs)与 [Issue #52266](https://github.com/openai/codex/issues/52266)：核对 `.agents/skills` 的根路径发现；该 Issue 是 Cloud 环境的报告，不能据此判断本机也有相同故障。
- [Codex PR #31507](https://github.com/openai/codex/pull/31507)：远程技能扫描的批量读取提案，提醒文件数量和加载成本也需要控制；调研时仍是 draft，不将提案当作已发布能力。

这些资料用于决定文档组织方式，不引入上游框架、远程服务或全局 Codex 配置修改。
