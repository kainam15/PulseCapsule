---
name: pulsecapsule-docs-maintenance
description: 整理 PulseCapsule 的 Markdown 文档、索引和 agent 指南；用于文档归类、消除重复或同步已改变的行为。
---

# PulseCapsule 文档维护

从 [文档索引](../../../docs/README.md) 和工作树状态确定涉及的文件。依据当前源码、命令参数及实际验证记录调整文字；不因整理任务而重新运行应用、访问真实账户或操作桌面。

将新信息放到其主要维护位置：使用方式进根 README，命令进开发指南，交互约束进 [docs/design.md](../../../docs/design.md)，架构与硬件协议进专题文档，日期和实际结果进验收记录。其他位置用链接，避免复制整段规范。

- `AGENTS.md` 只保留全局适用的项目约束和导航；专项流程进入精确触发的 skill。用户要求优先，不新增无依据的确认步骤。
- 历史记录保留原日期、名称、hash、结果和未完成项；补充状态说明，不把旧“通过”升级为当前结论。没有源码或实测依据的内容标明待核实。
- 改名 / 移动前用 `rg` 检查 Markdown、脚本和项目引用。图片可能是 WPF 构建资源；本地 `.artifacts` 证据无需搬入 Git。
- 新文档加入索引；新 skill 使用 `.agents/skills/<name>/SKILL.md`、短 `description` 和相对链接。默认保持自动选择，无需创建重复 `agent.md` 或复制用户级 skills。
- 保存为 UTF-8 无 BOM / LF，保留已有未提交内容。修改后运行 `python tools/check_docs.py`、`git diff --check`；新 skill 还需检查 YAML frontmatter、名称及触发边界。

交付改动的入口、修正的矛盾或失效引用、检查结果和仍待核实的事实。纯文字修改不扩大为应用全量回归；未得到 commit / push 指令时保留为工作树改动。
