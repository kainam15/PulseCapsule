---
name: pulsecapsule-system-telemetry
description: 核实 PulseCapsule 的 CPU 温度、负载、风扇 RPM 来源，排查遥测异常或评估风扇控制能力。
---

# PulseCapsule System 遥测

先区分用户需要核实读数、修复遥测，还是改变风扇控制。读取 [System Capsule](../../../docs/system.md) 的对应部分，不把截图数字、旧机器记录或 demo 当成当前硬件证据。

核实来源时检查实际进程路径及 `--demo`；源码入口是 [HardwareMonitor](../../../src/PulseCapsule/Capsules/System/HardwareMonitor.cs)、[AsusAcpiAdapter](../../../src/PulseCapsule/Capsules/System/AsusAcpiAdapter.cs) 和 [CpuTemperatureReader](../../../src/PulseCapsule/Capsules/System/CpuTemperatureReader.cs)。必要时运行：

```powershell
dotnet run --project tests/PulseCapsule.Checks -c Release -- --system-probe 6
```

SDK 不在 PATH 时按 [开发指南](../../../docs/development.md) 指定可执行文件。探针只读，但它不是已部署 EXE 的同一时刻快照；将采样时间、硬件身份、来源和进程证据分别说明。

判读时保留这些区别：

- unsupported 风扇隐藏；stopped 可以显示 0 RPM；读取失败显示失败，不能补 0。
- ASUS 风扇值按 100 RPM 单位转换；两分钟曲线与最新采样的时间窗不同。
- `CanVerifyAutomaticRestore` / `CanVerifyState` 未满足时保持只读。型号在允许列表、demo 状态机通过、只读探针成功都不能证明 COOL 写入或 AUTO 恢复成功。

修改控制能力时先确认可靠的原状态读回与恢复验证方案；没有这些条件时交付只读诊断和缺失证据，不能为取得“成功”结果绕过能力判断。真实硬件写入或系统 / Armoury Crate 操作需要属于用户明确授权范围。
