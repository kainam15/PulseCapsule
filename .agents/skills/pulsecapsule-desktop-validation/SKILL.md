---
name: pulsecapsule-desktop-validation
description: 验证 PulseCapsule 的 WPF 交互、任务栏行为与候选或已部署 EXE；用于桌面回归和交付验收。
---

# PulseCapsule 桌面验证

目标是给出与实际运行对象对应的证据。先确认本次要验证的行为、EXE 路径、候选或正式实例，以及用户是否已授权物理输入 / 部署。

按 [开发与验证](../../../docs/development.md) 选择能覆盖本次改动的检查和 smoke；设置与卡片问题再读 [回归矩阵](../../../docs/settings-ui-regression.md) 的相关用例。无需通跑所有脚本。

- 修复已有故障时先定位当前进程、日志或失败步骤，再决定是否发布候选。候选输出到 `.artifacts/`，使用独立 `--demo --data-dir`，记录 EXE SHA-256。
- core checks 与 `--ui-checks` 可直接进行；它们不证明正在运行的 `dist` 已更新。WPF harness 保留本地 STA continuation，不使用会触发生产启动的 `Dispatcher.Run`。
- 可见蓝框 Computer Use 需明确同意。物理输入仅在空闲、解锁桌面串行运行，操作前核对目标 HWND / 进程。环境干扰记为 `environment-blocked`，保留已完成的隔离检查。
- 使用脚本前检查其 `--live` / `--pid` 含义；授权不明确时先完成隔离部分，再确认具体真实账户或正式实例操作。
- UIA、渲染图、物理鼠标 / 键盘、实际 DPI、正式 EXE 分开报告。只有执行并观察到的行为可记为通过。

交付实际执行的命令和结果、证据位置、EXE 身份及尚未覆盖的行为。替换正式 EXE 时使用开发指南中的备份、退出、hash 和重启复核流程；只验证候选的任务到候选结论即完成。
