# 开发与验证

当前维护指南。命令在仓库根目录的 PowerShell 中执行；需要原生 Windows 和 .NET 10 SDK。功能与账户配置见 [README](../README.md)，文档分类见 [索引](README.md)。

## 源码入口

| 位置 | 职责 |
| --- | --- |
| [PulseCapsule.Core](../src/PulseCapsule.Core) | Capsule 接口、Provider 协议与解析、遥测模型、风扇状态机、任务栏布局算法 |
| [App.xaml.cs](../src/PulseCapsule/App.xaml.cs) / [Services](../src/PulseCapsule/Services) | 启动与依赖组装、数据身份、配置、凭据、SQLite、刷新与告警 |
| [CapsuleHost](../src/PulseCapsule/Shell/CapsuleHost.cs) / [Capsules](../src/PulseCapsule/Capsules) | 切换与生命周期、Clock / Quota / System 业务 |
| [MainWindow.xaml.cs](../src/PulseCapsule/MainWindow.xaml.cs) / [Native](../src/PulseCapsule/Native) | 外壳、拖动、展开收起、NOACTIVATE、任务栏 HWND 与外部点击 |
| [SettingsWindow.xaml.cs](../src/PulseCapsule/SettingsWindow.xaml.cs) / [UI](../src/PulseCapsule/UI) | 账户表单、胶囊管理、展示模型和交互 |
| [App.xaml](../src/PulseCapsule/App.xaml) / [设计规范](design.md) | 共享资源与 UI 规范 |
| [PulseCapsule.Checks](../tests/PulseCapsule.Checks) | 自定义检查程序；默认 core checks，`--ui-checks` 单独运行 WPF 检查 |
| [tests](../tests) / [tools](../tools) | 桌面 smoke、只读探针、Logo 生成和文档检查 |

## 构建与隔离检查

[build.ps1](../build.ps1) 依次查找 `-DotnetPath`、`PULSECAPSULE_DOTNET`、仓库 `.tools\dotnet\dotnet.exe`、已有 AltTabLock SDK，最后使用 PATH。不要依赖本机的 AltTabLock 路径在其他机器存在。

```powershell
.\build.ps1
.\build.ps1 -Check
# WPF 检查是独立入口，-Check 不会运行这一组
dotnet run --project tests/PulseCapsule.Checks -c Release -- --ui-checks
```

不在 PATH 的 SDK 可以传给 `build.ps1 -DotnetPath C:\tools\dotnet\dotnet.exe`；直接 `dotnet` 命令则替换为 `& 'C:\tools\dotnet\dotnet.exe'`。

默认 core checks 使用 fixture HTTP、临时数据目录和独立凭据 scope；凭据 round trip 会创建并清理测试专用 Credential Manager 条目。WPF checks 使用隔离数据、生产控件和 handler，不显示窗口。二者都不需要真实账户，可在任务范围内直接运行并修复本次引入的失败。不要给 WPF harness 加 `Dispatcher.Run`：它会调度生产 `App.OnStartup`，可能打开应用和默认数据目录。

### 按变更选择验证

| 变更 | 相关验证 | 结论限制 |
| --- | --- | --- |
| Markdown / agent 指南 / skill | `python tools/check_docs.py`、skill frontmatter 检查、`git diff --check` | 不需要因此构建、发布或操作桌面 |
| Core、Provider、持久化 | Release 构建和 `build.ps1 -Check`；需要时选择真实只读探针 | fixture 通过不等于真实供应商协议或登录已验证 |
| 设置、展示与表单异步行为 | 构建、core checks、`--ui-checks`；按 [回归矩阵](settings-ui-regression.md) 选择受影响用例 | WPF 渲染不证明物理输入、模态框或实际 Windows DPI |
| 鼠标、焦点、菜单、拖动或任务栏 | 构建、相关 checks、候选 EXE 的专项桌面 smoke | UIA / Invoke 不等于真实鼠标或前台保持通过 |
| System 遥测或控制能力 | core checks、[只读探针](system.md)、必要的 System smoke | demo 与只读探针不验证风扇写入 |

通过相关验证后结束这一轮；只有新改动、失败或未解决的问题才扩大或重复检查。日志中记录真实执行的数量，不在指南里固定“当前应该通过多少项”。

## 候选包与正式部署

应用改动按[协作指南](../AGENTS.md)默认交付到当前正式版本。先发布到独立目录用于验证，检查通过后继续更新正式包：

```powershell
.\build.ps1 -Publish -OutputDirectory .artifacts/candidate
Get-FileHash -LiteralPath .artifacts/candidate/PulseCapsule.exe -Algorithm SHA256
```

需要显示候选界面时，使用 `--demo --data-dir` 指定本轮独立目录。可见蓝框 Computer Use 需用户明确同意；物理输入测试需空闲、解锁的交互桌面。

`build.ps1 -Publish` 默认写入 `dist`。依据协作指南的默认部署授权或用户本轮限定执行：确认目标 EXE 路径与进程，正常退出目标实例，备份 EXE、配置和数据库并确认备份可读。SQLite 需在应用正常退出后复制，或使用 SQLite backup API 获取一致快照。保护凭据及 Provider ID，不在日志输出秘密。

脚本会拒绝覆盖目标目录中正在运行的 EXE，不会替你停止进程。部署后比对候选与目标 SHA-256，确认实际运行路径，并复核原账户 / 历史和此次变更的交互。发布成功、文件 hash 相同和已部署功能验收是三种不同证据。

## 桌面 smoke

使用 `python -m pip install pywinauto Pillow` 安装桌面测试依赖后，按受影响行为挑选脚本；每个脚本的参数以源码或 `--help` 为准。

| 行为 | 脚本 |
| --- | --- |
| 基本窗口、设置、锁定与退出 | [desktop_smoke.py](../tests/desktop_smoke.py) |
| 拖动、位置恢复 | [drag_smoke.py](../tests/drag_smoke.py)、[lifecycle_smoke.py](../tests/lifecycle_smoke.py) |
| 任务栏、菜单、外部收起 | [taskbar_smoke.py](../tests/taskbar_smoke.py)、[context_menu_smoke.py](../tests/context_menu_smoke.py)、[outside_click_smoke.py](../tests/outside_click_smoke.py) |
| 滚轮、供应商分组 | [wheel_smoke.py](../tests/wheel_smoke.py)、[provider_group_smoke.py](../tests/provider_group_smoke.py) |
| System、胶囊设置 | [system_capsule_smoke.py](../tests/system_capsule_smoke.py)、[capsule_settings_smoke.py](../tests/capsule_settings_smoke.py) |

例如，验证候选包的滚轮：

```powershell
python tests/wheel_smoke.py --exe .artifacts/candidate/PulseCapsule.exe
```

默认 demo smoke 使用隔离数据。`--live` 是各脚本自己的选项：有的读取本机 Codex 登录，有的读取真实硬件，并非统一的“使用正式账户”开关。运行前查看实际分支；对正式实例的 `--pid` 操作需要明确目标及授权。

物理输入脚本串行运行；出现外部鼠标输入、锁屏、窗口被遮挡或任务栏空间被另一个实例占用时，记录 `environment-blocked`，不要为测试随意结束其他应用。`--uia`、WPF `--render` 和 pixel-scale PNG 的结果单独标注。

## 只读探针与资源

只在涉及真实数据来源时运行对应探针，不作为每次构建的前置步骤：

```powershell
# 无 key 检查 Hone 路由与计费单位
powershell -NoProfile -File tools/probe-hone.ps1 -WithoutKey
# 按需隐藏输入 key；-SaveResponse 将响应留在本机 .artifacts
powershell -NoProfile -File tools/probe-hone.ps1 -SaveResponse
# 读取本机 Codex 额度，不创建会话或模型请求
python tools/probe_codex.py --exe C:\path\to\codex.exe
dotnet run --project tests/PulseCapsule.Checks -c Release -- --live-codex
# 6 秒真实硬件只读采样；不是风扇写入验收
dotnet run --project tests/PulseCapsule.Checks -c Release -- --system-probe 6
```

Logo 原图位于 [docs/images/logo.png](images/logo.png)，由项目文件作为 WPF Resource 引用。替换原图后运行 `powershell -NoProfile -File tools/update-logo.ps1` 生成多尺寸 ICO，保持比例及透明背景。

## 验证记录

结果保存在 `.artifacts/`；正式写入 Markdown 的记录包含日期、代码版本或工作树说明、命令、退出状态、候选 / 部署路径及 hash（涉及 EXE 时）、证据路径和未验证项。区分 `passed`、`failed`、`not-run` 与 `environment-blocked`。截图中的读数和历史机器数据只证明采集时状态。
