# PulseCapsule

<p align="center">
  <img src="docs/images/logo.png" alt="PulseCapsule 临时 Logo" width="128">
</p>

> **Logo 说明：** 当前使用的 [Logo](docs/images/logo.png) 仅作临时使用，并非本项目作者本人设计。

原生 WPF Capsule Host，支持自由悬浮和嵌入 Windows 底部任务栏左侧。统一轮换 **Clock → Quota（各 Provider）→ System**：时间、钱包 / 额度、CPU 温度与风扇监控。

**当前 System 已验证温度与 RPM 监控，真实风扇写入仍为只读。** FA401KM 的原模式和恢复结果尚无可靠读回机制，因此不会开放 COOL 或声称已通过降温 A/B。具体边界见 [System 实现与验收](docs/system.md)。

## 使用

双击 `dist\PulseCapsule.exe`。这是包含 .NET 的 Windows x64 便携 EXE，无需安装运行环境。

1. 打开组件右上角的设置，选择 **Hone API**，粘贴 API key，点击“测试连接”并保存。默认地址是 `https://hone.vvvv.ee`。这里是 key / 兼容账单，不一定是账户钱包。
   要显示**钱包剩余**，点击“＋ 连接钱包余额”，在 **Hone 钱包** 中填写网站「安全与访问」里的**账户访问令牌**，测试后保存。同一站点的钱包和 API key 会合并为一张 **Hone** 卡片：钱包余额优先展示，账户累计消费与 key 消费各自标注；设置中仍分别管理两种凭据。普通 API key 无法代替账户访问令牌。可以通过 `dist\PulseCapsule.exe --settings` 启动并打开设置。
2. **Codex** 自动查找本机 Codex，并复用它已有的 ChatGPT 登录。读取官方 app-server 的 `account/rateLimits/read`；不创建会话、不发模型请求，不复制登录 token。若找不到程序，可在设置中填写 `codex.exe` 完整路径。
3. 单击胶囊展开，点击面板外部收起；任务栏模式关闭展开面板，自由悬浮模式收回胶囊。悬停保持收起，滚轮可在 **Clock、已启用 Quota Provider、System** 之间切换并首尾循环；时间页以较大字号居中显示 `yyyy-MM-dd　HH:mm`，日期与时间留有明显间距，不显示状态圆点、“时间”标题或右侧握柄，手动选择在重启后保持，自动刷新不会跳回。按住胶囊任意可见位置并移动即可拖动；时间页隐藏圆点和右侧握柄后仍可直接拖动胶囊。展开后也可拖动标题和卡片空白区域。拖动时保持当前形态，松手自动记住位置；仅移开鼠标不会收起。点击面板内部、设置和菜单不触发收起；设置窗口打开期间保持可见。
4. 锁定后鼠标穿透。用 **Ctrl+Alt+Q** 解锁；如冲突则尝试 **Ctrl+Alt+Shift+Q**，实际快捷键在锁定按钮提示中显示。托盘菜单始终可以解锁、刷新、设置、隐藏和退出。
5. **嵌入左下角任务栏**：在胶囊右键菜单、托盘菜单中勾选，或在设置的“桌面偏好”中勾选并保存。胶囊使用任务栏左侧空位，同样支持悬停滚轮切换Clock / Quota / System，单击在上方展开 / 收起卡片，右侧菜单可“切回自由悬浮”。任务栏模式不拖动，切回时恢复原有悬浮位置和展开状态；重启后记住所选模式。

任务栏嵌入面向主屏幕的底部任务栏，自动适配 DPI，并根据现有系统按钮调整宽度；窄宽度优先显示完整余额。空间不足或暂时无法读取任务栏布局时回退为左下角悬浮胶囊，有空间后自动恢复嵌入。不会移动“开始”、固定图标、小工具或通知区域；不修改 Windows 的任务栏设置。隐藏胶囊和锁定穿透仍可通过托盘菜单恢复。

初次启动展开以便连接账户，之后记住收起状态和位置。开机自启默认关闭，需要在设置中主动开启。

设置 → **Capsules 与 System** 支持启停、排序、记住选择、可选自动轮播，以及 CPU temperature / Fan RPM / Fan control 偏好。System 展开显示 Load、风扇、两分钟温度曲线及 Cooling 统计；次要信息放在 tooltip 和详情中。Clock 保持原字号和居中布局。

`--demo` 的 COOL 切换仅模拟状态机，不访问真实硬件。

## 数据源

| 数据源 | 显示内容 | 认证 / 说明 |
|---|---|---|
| Hone 钱包 | 账户钱包余额、账户累计消费 | 账户访问令牌；读取 `/api/user/self`，按站点 `quota_per_unit` 和展示币种换算 |
| Hone API / New API | key / 站点账单的剩余、额度、累计消费 | 普通 API key；无限 key 不代表钱包无限；`usage` 默认乘 0.01 |
| Codex | 每个窗口的剩余百分比、重置时间、可用重置次数 | 本机 Codex 的 ChatGPT 登录；窗口时长由接口决定 |
| DeepSeek | 指定币种的可用余额 | 普通 key；不把另一币种余额重标成 CNY |
| Kimi | 可用余额 | 国内站 CNY，国际站 USD |
| 硅基流动 | `totalBalance` | 普通 key |
| OpenRouter | 当前 key 限额 / 已用 / 本月消费 | 无上限时不推算账户余额 |
| 自定义 HTTP | 映射 JSON 金额字段 | HTTPS GET、请求头模板 `{key}`、金额换算倍数 |
| 手动录入 | 手动余额 / 已用 / 预算剩余 | 显示录入时间；预算剩余明确标记为估算 |

自定义路径支持 `$.data.balance`、`$.items[0].value`，不支持 JSONPath 通配符、递归与过滤表达式。不要在 URL 或模板里写实际 key。内置数据源不访问模型推理接口。

Codex 的剩余百分比与现金余额独立显示，不能换算成美元。读取返回的全部额度桶，并兼容只返回一个窗口的账户；API key 登录不等于 ChatGPT 订阅登录。可用重置次数仅展示，在 Codex 中手动使用。

## 常驻行为与数据

- 展开卡片按供应商及服务地址分组，钱包优先，其他服务紧凑显示；每项服务保留自己的金额、币种、趋势、更新时间和异常提示，不相加。不同站点 / 租户路径分开，手动录入独立展示，自定义 HTTP 按实际查询地址分组。胶囊可逐项切换本机时间、钱包与额度；每类视图通过相同 Capsule 生命周期运行。
- 置顶、无任务栏 / Alt+Tab 条目，点击组件不抢焦点；设置窗口可正常输入。
- 每家独立刷新；最多 2 家同时读取；失败保留旧金额 / 原更新时间，指数退避，尊重 `Retry-After`；手动刷新有 15 秒间隔保护。
- 锁屏 / 睡眠暂停，恢复后刷新；全屏游戏 / 演示自动隐藏可关闭。
- 低额度按各平台绝对金额或 Codex 剩余百分比告警；跨阈值一次提醒，状态写入 SQLite，重启不重复弹窗。
- 新安装默认使用 `%APPDATA%\PulseCapsule\settings.json`；检测到旧 `%APPDATA%\QuotaPeek` 时直接沿用旧目录，不复制或重建账户。API key 继续使用 Windows 凭据管理器的稳定 `QuotaPeek/...` 标识。可改为从指定环境变量读取；不会自动扫描其他软件的 API key。
- Hone 钱包的账户访问令牌也独立保存在 Windows 凭据管理器，可用 `HONE_ACCESS_TOKEN` 环境变量提供。程序仅调用查询接口；网页访问令牌本身的权限由 Hone 决定。
- 同一数据目录中的 `history.db` 保存 90 天快照，升级保留原 SQLite 文件和 Provider ID。至少 3 个有效记录、累计 12 小时有效间隔后才估算可用天数；排除充值上涨与超过 36 小时的断档。估算不代表真实账单。
- 切换账户 key、地址、币种或映射后新建历史流，防止混算。

## 构建与验证

需要 **.NET 10 SDK** 和 Windows。脚本依次查找 `-DotnetPath`、`PULSECAPSULE_DOTNET`、`.tools\dotnet\dotnet.exe`、本机已有 AltTabLock SDK，最后使用 PATH。

README 和界面直接使用 `docs/images/logo.png` 原图，保留原始透明背景。替换后运行 `powershell -NoProfile -File tools\update-logo.ps1`，按原比例重新生成 EXE / 窗口 / 托盘使用的多尺寸 ICO，不添加底色、不修改原图。

```powershell
.\build.ps1
.\build.ps1 -Check
.\build.ps1 -Publish
# 可显式指定 SDK，无需更改系统 PATH
.\build.ps1 -Publish -DotnetPath C:\tools\dotnet\dotnet.exe
```

发布前从托盘退出正在运行的 PulseCapsule。`dist` 是生成产物，不提交到 Git。自包含 EXE 约 73 MiB，包含 .NET Desktop Runtime；首次启动会解压运行库到 Windows 用户临时缓存。采用完整解压以减少常驻内存：200% DPI 的短时演示实例工作集约 166 MiB / 私有内存约 97 MiB；正式实例读到真实 Codex 后约 210 / 130 MiB。后续占用随账户数量与历史变化。

```powershell
# 阶段 0：不带 key 验证 Hone 的路由和站点计费单位
powershell -NoProfile -File tools\probe-hone.ps1 -WithoutKey
# 隐藏输入 key，按需把财务响应留在被 Git 忽略的 .artifacts 中
powershell -NoProfile -File tools\probe-hone.ps1 -SaveResponse
# Codex 只输出额度，省略账号 ID、登录凭据和重置券 ID
python tools\probe_codex.py --exe C:\path\to\codex.exe
```

`tests/PulseCapsule.Checks` 覆盖解析、单位、缓存、重试、历史与凭据。使用 `dotnet run --project tests/PulseCapsule.Checks -c Release -- --live-codex` 可额外读取本机真实 Codex 额度。

桌面测试使用用户级 `windows-desktop-e2e` 方法（pywinauto + UI Automation）：

```powershell
python -m pip install pywinauto Pillow
python tests\desktop_smoke.py --exe dist\PulseCapsule.exe
python tests\desktop_smoke.py --exe dist\PulseCapsule.exe --live
python tests\drag_smoke.py --exe dist\PulseCapsule.exe
python tests\taskbar_smoke.py --exe dist\PulseCapsule.exe
python tests\wheel_smoke.py --exe dist\PulseCapsule.exe
python tests\provider_group_smoke.py --exe dist\PulseCapsule.exe
python tests\outside_click_smoke.py --exe dist\PulseCapsule.exe
python tests\outside_click_smoke.py --exe dist\PulseCapsule.exe --docked
python tests\system_capsule_smoke.py --exe dist\PulseCapsule.exe
python tests\system_capsule_smoke.py --exe dist\PulseCapsule.exe --live
python tests\capsule_settings_smoke.py --exe dist\PulseCapsule.exe
python tests\capsule_settings_smoke.py --exe dist\PulseCapsule.exe --live
```

测试每次使用独立数据目录，不覆盖正常账户。拖动测试会操作真实鼠标，运行期间需要保持桌面空闲；检测到外部鼠标输入时会中止并报告环境干扰。`--demo` 仅用演示数据，不联网；`--data-dir` 可隔离设置与凭据命名空间；`--render` 输出 WPF 渲染图用于视觉检查，不能单独证明物理输入正常。

## 范围

已实现的是可常驻使用的首版。OpenAI / Anthropic Admin 费用接口、本地代理 token 统计、完整 JSONPath、主题切换、自动更新和安装器尚未实现。花费型可先使用自定义 HTTP 或手动预算，需保证预算与已用金额周期一致。

旧名称仅保留在存储兼容常量、对应测试和历史验证记录中，仓库所在目录可继续叫 `QuotaPeek`。图标资源已更名，沿用原透明图案。

本轮结果与未完成项见 [PulseCapsule 验证记录](docs/pulsecapsule-verification.md)，架构和协议依据见 [Capsule Host](docs/capsules.md)。早期研究与版本记录保留在 [docs/research.md](docs/research.md) 和 [docs/verification.md](docs/verification.md)。
