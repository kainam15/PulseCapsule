# Settings UI/UX regression matrix

Living regression cases plus dated evidence. Later dated entries describe subsequent changes; earlier pass counts and candidate hashes retain their original scope. See the [development guide](development.md) for validation selection and the [documentation index](README.md) for history.

Use a **separate demo/test data directory** and never use a real credential. Physical-input tests need an idle, unlocked native Windows interactive session; run them serially. If another instance occupies the target area, arrange a free target or an authorized exit rather than stopping unrelated processes. Visible Computer Use requires explicit consent.

## Audit baseline before capsule management changes (2026-10-24)

The pasted 2026-10-23 review describes `ba2b103`. This baseline was reconciled against `c7071b3` and the connection-test fix in the working tree on 2026-10-24. The later capsule-management section records subsequent changes. The original P0/P1 list is historical: existing implementations need acceptance evidence, not duplicate implementation work. Visual congestion alone is not evidence of a release-blocking defect.

| Area | Implementation at this baseline | Acceptance evidence and remaining work |
| --- | --- | --- |
| Header, scrolling and actions | Fixed header/actions, independently scrolling list and editor | Minimum-client WPF layout/render checks pass; physical resizing and real DPI matrix pending |
| Unsaved account edits | Dirty marker, Save / Discard / Cancel on navigation and close | Automated discard/save checks pass; full native-dialog matrix for switch/add/close pending |
| Remove source / clear key | Confirmation, removal scope, retained history and environment-variable warning | Source verified; confirmation/cancellation and credential failure acceptance pending |
| Focus and accessible names | Shared focus style, named fields and live feedback | Physical keyboard, UIA naming and high-contrast acceptance pending |
| Navigation and advanced fields | Three settings pages, integrated capsule preferences, folded WgetCloud JSON fields | WPF page/layout checks pass; live browser-session/advanced-mode scenarios pending |
| Design system | Design guidance (now [design.md](design.md)), semantic resources and reusable styles exist | Compact capsules and settings have separate density requirements; remaining visual review pending |
| Connection-test ownership | Cancel and invalidate obsolete requests; guard success, errors and cleanup | 21 new automated checks pass with controlled delayed provider responses, including providers that ignore cancellation |

The connection-test run was **automation only**, as requested. It claimed no new physical-input, native-modal, screen-reader or Windows DPI acceptance. The existing WebView2 login/session flow supports authentication; WPF remains the UI framework.

## Behavior checklist

| Case | Steps | Pass condition |
| --- | --- | --- |
| Header + scroll | Select long WgetCloud/Custom form, shrink window to minimum and scroll fully | Title and bottom actions remain visible and clickable, scrollbar never covers them |
| Dirty / cancel | Change Name, key, interval or advanced JSON field; select another source; Cancel | Selection and every unsaved input remain unchanged |
| Enable source | Toggle a source on 胶囊管理, return to its account form, save connection details | Toggle persists immediately and survives later account saves; failed persistence restores the prior switch |
| Supplier group | Open 胶囊管理 with Hone wallet and API at the same site; toggle Hone off/on; simulate a failed save with mixed initial states | One Hone row has one switch for all member sources; disabled suppliers remain reachable, loading preserves mixed flags, failed saves restore every original flag, different sites stay separate |
| Hone account form | Select either member ID, edit both credentials/options, save/discard, add a missing connection and change the shared site | One Hone entry and page, independent secrets and history, group save rollback, no identity rewrite on unchanged save, different sites stay separate; partial test failures remain labeled and old async results are ignored |
| Module ordering | Drag a supplier before Clock and after hardware monitoring; cancel, leave the list, scroll at the edge and simulate a failed save | One named-module list controls host/wheel/carousel order; no family rows or up/down buttons; drop saves once, cancellation/failure keeps prior order and current selection |
| Management footer | Open at minimum window size and change rotation | Rotation stays below the independently scrolling list; hardware sub-option switches are absent and stored System preferences remain unchanged |
| Module migration | Load v1 family order with a selected Hone API and reload the persisted settings | v2 expands the old quota position, maps selection to one Hone module, preserves Provider IDs/configurations and migrates a disabled quota gate into disabled sources |
| Dirty / discard | Change a source; switch to another; choose Discard | New source loads; old configuration remains unchanged |
| Dirty / save | Modify source then switch; choose Save | Save validates, persists data and selects intended destination; invalid fields block navigation |
| Drafts | Add a new source, switch away / close; Save, Discard and Cancel separately | Draft cannot disappear silently; discarded draft is not persisted |
| Window close | Change source and close via X button or Alt+F4 | Three-way confirmation; Cancel keeps window open |
| Remove | Remove source → Cancel; repeat → Confirm | First attempt changes nothing; confirmed removal updates config, does not prematurely erase historical data |
| Credential clear | Clear key → Cancel; repeat → Confirm; environment variable configured | Nothing changes on Cancel; confirmed clearance affects Credential Manager only; copy explicitly warns environment variable may still work |
| Connection result ownership | Start A, switch to B or back to A, edit an already dirty form, save/discard/leave/close, then release A's delayed result | Old success/failure/cancellation never replaces current feedback; old cleanup cannot enable the button while a newer test runs |
| Failure | Simulate invalid interval, HTTP 401/403, timeout, rate-limit, offline and credential deletion failure | Accurate, local feedback; test ≠ saved; preserved old values keep original timestamp |
| Keyboard + UIA | Tab / Shift+Tab / Enter / Space; inspect UIA labels | Focus clearly visible and names convey each field without a tooltip |
| WgetCloud | Browser login, HTML/JSON switch, advanced settings expanded/collapsed | URL policy unchanged, nonrequired fields hidden, input values preserved |
| Window scale | Repeat key flows at 100/125/150/200% DPI and narrow window | No overlap/clipping, including long Chinese and English labels |
| Performance | Idle main capsule then open/scroll/close settings repeatedly | No unbounded resource growth or significant responsiveness regressions |

## Automated baseline

```powershell
dotnet build PulseCapsule.slnx
dotnet run --project tests/PulseCapsule.Checks/PulseCapsule.Checks.csproj
```

Desktop pywinauto / UI Automation checks require an **interactive, not minimized** Windows session. Native modal dialogs can block synchronous UIA calls; use controlled physical-input tests only with an idle desktop. Do not infer dialog success from a normal build or an unrelated smoke test.

## Current scope

The navigation redesign and immediate global preferences are implemented. Account forms retain explicit Save / Discard / Cancel. The automated checks below do not replace physical-input, native dialog or actual Windows DPI checks.

## 2026-10-24 navigation and density changes

- Three settings pages; source list and editor have independent scrolling. Save/Test/Discard stay fixed, key clearing sits in credentials and removal in a separate danger section.
- Global preferences save immediately. Failed writes restore controls; account saves merge with the latest globals, position and card detail states. Global changes preserve quota snapshots, backoff and Retry-After.
- Visible capsule navigation, remembered per-source details, per-source actions, inline connection recovery, field validation, readable dates and scaled trends. Dark ScrollBar, Expander, ContextMenu and ToolTip templates preserve visible hit areas and focus.

Run the production WPF templates and settings handlers without opening a window:

```powershell
dotnet run --project tests/PulseCapsule.Checks -c Release -- --ui-checks
```

The harness uses isolated demo data, renders the minimum client area (694 × 502 for an approximately 710 × 540 window), and emits 100% / 200% pixel-scale PNGs under `.artifacts/settings-ui-*`. Pixel-scale rendering is **not actual Windows 200% DPI verification**.

Additional manual cases:

| Case | Expected result |
| --- | --- |
| Minimum window, 14 long account names | All three navigation entries, Add and Save/Test/Discard remain reachable; names truncate with tooltips |
| Global preference save denied | Prior checkbox/order/interval restored and failure reported; next account save cannot overwrite the prior globals |
| Move or disable capsules | Immediate persistence; last enabled capsule cannot be disabled |
| Card detail toggle + refresh + restart | State follows source ID; Codex windows and resets always visible |
| Single-source refresh | Other sources are not requested; Retry-After and forced-refresh debounce remain effective |
| Capsule navigation mouse then left/right | Clear focus, selection and content agree; background refresh does not activate the window |
| Card menu + outside click + Escape | Correct source actions; menu dismisses normally on NOACTIVATE host |
| Actual 100/125/150/200% DPI | Native window chrome, popups, validation scrolling and fixed actions remain usable |

Research informing the implementation:

- [PowerToys GeneralViewModel](https://github.com/microsoft/PowerToys/blob/main/src/settings-ui/Settings.UI/ViewModels/GeneralViewModel.cs): property-driven global settings feedback/persistence. PulseCapsule retains its own WPF and storage boundaries.
- [PowerRename Fluent UX PR #13678](https://github.com/microsoft/PowerToys/pull/13678) (merged): direct settings access and applying changes without closing; no framework or theme migration was adopted.
- [PowerToys density issue #14164](https://github.com/microsoft/PowerToys/issues/14164): excess margins and horizontal competition reduced usefulness on small screens. Separate navigation and foldable details address density without enlarging the whole panel.

## Historical verification on 2026-10-24, before the connection-test fix

- Release build and self-contained `win-x64` publish succeeded. Core checks: **197 passed**. Production WPF handler/layout checks: **16 passed**. Updated Python smoke scripts compile; their interactive suites were not run in this session.
- WPF checks include the minimum client area, 14 long account names, independent scrolling, inline invalid-field feedback, failed-save rollback, preservation of global preferences when saving an account, capsule ordering, persisted card details, and single-source refresh with debounce preserved.
- Candidate: `.artifacts/ui-candidate/PulseCapsule.exe`; SHA-256 `1DB984A70ADC7CAF3335C3430E62FDC4BE87DEE19EE2529A3FD47B1CC4E9395D`. Test data: `.artifacts/ui-desktop-20261024`, launched with `--demo --settings --data-dir`. Formal accounts, deployed `dist` executable and Windows scaling were not changed.
- Authorized Computer Use on the candidate confirmed all three settings pages, visible dark scrollbars, immediate fullscreen-preference persistence in the isolated JSON file, inline interval validation, the native unsaved-changes dialog, Escape cancellation retaining the edit, and Discard restoring the saved value. A physical click on the visible Time navigation also changed the capsule content.
- Desktop acceptance is **partial**: the helper intermittently bound coordinates to the owner instead of the settings window and did not expose the standalone NOACTIVATE capsule as a selectable window. Actual minimum-window resizing, left/right focus, card-menu actions/dismissal and restart persistence were not accepted as physical-input passes. WPF/render checks do not substitute for these cases.
- Actual 100/125/150/200% Windows DPI acceptance remains pending. The 200% PNG is pixel-scale rendering only. Logs: `.artifacts/ui-core-checks.log`, `.artifacts/ui-wpf-checks.log`, `.artifacts/ui-publish.log`; render evidence: `.artifacts/settings-ui-20261024-105143/`.

## Connection-test verification on 2026-10-24

- Reproduced the stale-success bug before fixing it: start A, select B, then complete A's controlled response. The production async form method overwrote B's feedback. The failing run is `.artifacts/ui-connection-red.log`.
- Release solution build: **0 warnings, 0 errors**. Core checks: **197 passed**. WPF checks: **37 passed**, including **21 new connection checks**. Logs: `.artifacts/ui-connection-build.log`, `.artifacts/ui-connection-core.log`, `.artifacts/ui-connection-green.log`. Render evidence: `.artifacts/settings-ui-20261024-140845/`.
- The fix binds each test to its own cancellation source and checks ownership before all completion updates, including errors and `finally`. Source/page changes, every form edit (including an already dirty form), save/discard, credential changes and window closure invalidate that ownership. Cancellation alone is not treated as proof that a provider stopped.
- `SettingsConnectionChecks` invokes the same async method used by `Test_Click`, uses real WPF form controls on an STA thread, and substitutes only the provider-fetch boundary. Controlled responses deliberately ignore cancellation. Coverage includes A → B → A, old cleanup during a newer test, current success/failure/cancellation, key/enabled/name edits, saved/discarded feedback, page navigation, close, invalid input and demo mode. This does not exercise live provider HTTP/authentication behavior or native confirmation dialogs.
- Async test continuations use a local STA synchronization context. Do not introduce `Dispatcher.Run` into this harness: pumping the application's dispatcher also schedules production `App.OnStartup`, which can open the real application and default account directory.
- Research: [Rx.NET Switch](https://github.com/dotnet/reactive/blob/main/Rx.NET/Source/src/System.Reactive/Linq/Observable/Switch.cs) invalidates old subscriptions and checks notification identity; the existing `QuotaMonitor` also guards results by generation. PulseCapsule uses a per-test identity locally, retaining the current WPF/service architecture without adding a reactive dependency.
- This run did not publish/deploy a candidate or perform Computer Use. Physical-input and actual 100/125/150/200% DPI acceptance remain pending by request.

## 2026-10-24 capsule management and drag ordering

- The navigation label is now “胶囊管理”. Clock / Quota / System rows have dedicated drag handles, an insertion line and independent enable switches. A completed drop saves once; moving downward corrects the insertion index, and the current host selection remains stable. Up / Down buttons remain available for keyboard users.
- Source enable switches have moved out of account forms into the management page. Changes save immediately through the existing account-settings path; a failed save restores the displayed and stored switch. Returning to the source editor and saving details preserves the latest enable state. Credentials and provider IDs are unchanged by toggles.
- The drag implementation uses WPF `Thumb` capture, DIP-based drag thresholds and cumulative displacement. Escape, lost capture and release outside the list cancel; row gaps have stable midpoint-based insertion positions. Only the handle initiates a drag, so checkboxes remain independent.
- Production WPF checks cover preview versus commit, upward/downward drops, cancellation, release outside the list, handle clicks, write-failure rollback, independent provider switches, subsequent account saves, and connection-test cancellation when entering management. These are routed-event and render checks, not physical mouse verification.
- Existing `provider_group_smoke.py` and `wheel_smoke.py` target the relocated switches. Interactive drag/Escape, actual Windows DPI and deployed-instance acceptance remain separate manual checks.
- Verification: **197 core checks and 50 WPF checks passed**; Release build and self-contained publish succeeded. Updated Python smoke scripts compile and `git diff --check` passes. Logs: `.artifacts/capsule-management-{core,wpf,publish}.log`. Render evidence: `.artifacts/settings-ui-20261024-143352/`.
- Candidate: `.artifacts/capsule-management-candidate/PulseCapsule.exe`, SHA-256 `D482265DB3A8DA8644C2383C41DED608049C73475AECB3A6362FDFAC717BA798`. The running `dist` instance was not replaced; no Computer Use or physical mouse acceptance was performed in this run.

Research informing this change:

- [Gong WPF DefaultDropHandler](https://github.com/punker76/gong-wpf-dragdrop/blob/develop/src/GongSolutions.WPF.DragDrop/DefaultDropHandler.cs) and [PR #362](https://github.com/punker76/gong-wpf-dragdrop/pull/362): insertion-index correction when moving within a list, and preserving selection. PulseCapsule retains its existing save-then-reload settings model rather than adding a drag/drop dependency.
- [Issue #342](https://github.com/punker76/gong-wpf-dragdrop/issues/342) documents checkbox/drag selection conflicts; [Issue #512](https://github.com/punker76/gong-wpf-dragdrop/issues/512) documents unstable insertion feedback in row gaps. Separate handles and midpoint hit testing address those interaction pitfalls.
- [WPF Thumb source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/Thumb.cs): mouse capture, cancellation, and `DragDelta` displacement relative to the original handle point.

## 2026-10-24 capsule management deployment

- 用户确认应用修改验证完成后默认直接更新当前正式版本；约定已写入根 `AGENTS.md`，并同步开发指南。
- 已正常退出旧 PID `52892`，备份 EXE、配置和一致 SQLite 快照后，原子替换 `dist/PulseCapsule.exe` 并启动 PID `52212`。实际进程路径已复核，部署 SHA-256 与候选一致：`D482265DB3A8DA8644C2383C41DED608049C73475AECB3A6362FDFAC717BA798`。
- 配置文件 hash、全部 Provider 配置及已有偏好保持一致；原有 **6,190 条历史记录逐条一致**。备份：`.artifacts/backups/before-capsule-management-20261024-150401/`；部署证据：`.artifacts/capsule-management-deployment.json`。
- 正式实例的 UIA 检查确认“胶囊管理”导航、3 个拖动手柄和 4 个数据源开关，设置窗口已重新打开并停留在管理页。本轮未执行物理鼠标拖动或实际 Windows DPI 验收。

## 2026-10-24 Hone management grouping

- “胶囊管理”中，同一地址的 Hone 钱包与 Hone API 合并为一张 Hone 卡片，内部保留“账户钱包”和“API key”开关。停用的来源仍留在分组内，单来源保留紧凑行布局。额度卡片和设置分组共享供应商身份、地址规范化与服务命名规则；不同站点、租户路径和端口仍独立，配置、凭据标识和历史不合并。
- 基于本节之前的未提交工作树继续修改。`build.ps1 -Check`：**202 项通过**；`dotnet run --project tests/PulseCapsule.Checks -c Release -- --ui-checks`：**55 项通过**。新增覆盖停用来源、两项全停用、组内独立保存、失败回退、自定义名称和重复 key 名称。WPF 最小窗口及 200% pixel-scale 渲染：`.artifacts/settings-ui-20261024-151049/`；这不代表真实 Windows DPI 验收。
- `build.ps1 -Publish -OutputDirectory .artifacts/hone-settings-group-candidate` 成功，候选与正式 `dist/PulseCapsule.exe` SHA-256 一致：`FF70CFB9F087C75FF3661692EA5993496840B2C5F439DD4A0FA4B0DA0B56E6AB`。命令日志：`.artifacts/hone-settings-group-{core,wpf,publish}.log`。
- 正常退出旧实例，备份至 `.artifacts/backups/before-hone-settings-group-20261024-151059/` 后原子替换。正式 PID `50588` 的实际路径、配置 hash、全部 Provider 配置与原有 **6,197 条历史记录**已复核。正式 UIA 检查确认一个 Hone 标题、钱包/API 子项及全部 4 个来源开关的原状态；设置已打开到“胶囊管理”。证据：`.artifacts/hone-settings-group-deployment.json`。
- `python tools/check_docs.py` 与 `git diff --check` 通过。物理鼠标/键盘及真实 Windows DPI：`not-run`。未使用蓝框 Computer Use，未 commit / push。
- 设计参考 [CommunityToolkit SettingsExpander ItemsSource 示例](https://github.com/CommunityToolkit/Windows/blob/main/components/SettingsControls/samples/SettingsExpanderItemsSourceSample.xaml)：共享标题下独立绑定子选项。这里只采用组织方式，继续使用 WPF 并直接展示开关。

## 2026-10-24 Hone single switch

- 用户进一步明确只要一个 Hone：管理页现为一行 Hone、一个启用开关，移除钱包/API 子行。一次保存统一改变同站点全部成员的 Enabled；保留独立凭据、Provider ID、配置和历史。旧配置部分启用时，供应商开关显示启用，单纯打开设置不改写各项状态。
- `build.ps1 -Check`：**203 项通过**；`dotnet run --project tests/PulseCapsule.Checks -c Release -- --ui-checks`：**56 项通过**。覆盖单行单开关、读取混合状态、原子保存失败回退、统一停用、生产 UIA Toggle pattern 统一启用及配置完整性。渲染：`.artifacts/settings-ui-20261024-151955/`，日志：`.artifacts/hone-single-switch-{core,wpf,publish}.log`。
- `build.ps1 -Publish -OutputDirectory .artifacts/hone-single-switch-candidate` 成功。候选与正式 EXE SHA-256：`1B18D7F0C7E5F2F1A1C10FFA6F7D27D6334982CF13C4F02A75B0C1F3CCA4D5BF`；正式 PID `39956` 的运行路径已复核为 `dist/PulseCapsule.exe`。备份：`.artifacts/backups/before-hone-single-switch-20261024-152056/`。
- 正式 UIA 确认 Hone 只有一个开关、没有钱包/API 子行，管理页共 3 个供应商开关。原配置 hash 及 **6,208 条历史记录**保持一致；设置窗口已定位到管理页。证据：`.artifacts/hone-single-switch-deployment.json`。真实账户开关未被测试改写。
- `python tools/check_docs.py`、`git diff --check` 和更新的 `provider_group_smoke.py` 语法检查通过。物理输入、真实 Windows DPI 及完整物理 provider-group smoke：`not-run`；未 commit / push。
- 实现参考 [WPF ToggleButtonAutomationPeer 源码](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Automation/Peers/ToggleButtonAutomationPeer.cs)：UIA Toggle 直接调用 OnToggle，因此沿用 Checked/Unchecked 并过滤初始绑定，支持自动化和真实输入共用保存路径。

## 2026-10-24 unified module ordering

- 在上述未提交工作树继续实现：管理页现在统一列出时钟、Hone、Codex、WgetCloud、硬件监控等模块，每行一个拖动手柄和启用开关。删除时间 / 额度 / 系统分类行、上移 / 下移按钮及硬件监控子选项；自动轮播固定在列表底部。滚轮与轮播遵循同一模块顺序，同站点 Hone 钱包和 API 合为一个运行模块，各自的数据语义与凭据仍独立。
- `AppSettings` v1 → v2 明确迁移旧分类排序和启用状态，并将 Hone 的选中来源映射到合并后的模块。拖动保存使用偏好路径，保留快照、退避和当前选择。长列表边缘自动滚动；退回拖动起点、Esc、失去捕获及列表外松手取消目标。
- `build.ps1 -Check`：**211 项通过**；`dotnet run --project tests/PulseCapsule.Checks -c Release -- --ui-checks`：**60 项通过**。包含迁移文件重新加载、交错排序、单 Hone 运行模块、保存失败回退、最后一个模块保护、边缘滚动及隐藏硬件偏好保留。日志：`.artifacts/module-list-{core,wpf}.log`。WPF 生产模板的最小窗口、标准窗口和 200% pixel-scale 图：`.artifacts/settings-ui-20261024-154116/`；并非真实 Windows DPI 验收。
- `build.ps1 -Publish -OutputDirectory .artifacts/module-list-candidate` 成功。候选与正式 `dist/PulseCapsule.exe` SHA-256：`B812836447CBB6A12E98B7149BF79082DD080185CDE045FBD41ACC504B5E119C`。发布日志：`.artifacts/module-list-publish.log`。
- `python .artifacts/check-module-list-candidate.py`：**16 项 UIA 检查通过**，覆盖 v1 文件迁移、5 个具名模块、单 Hone 开关、旧控件移除、底部轮播、重启保留交错顺序，以及实际候选进程完整一轮自动轮播。证据：`.artifacts/module-list-uia.log`、`.artifacts/module-list-uia-20261024-153919/result.json`。早期探测中的设置窗口层级和浮动胶囊控件定位已修正；捕获图实际是锁屏，不用作应用视觉证据。
- 按默认交付约定正常退出旧 PID `39956`，备份 EXE、配置和一致 SQLite 快照至 `.artifacts/backups/before-module-list-20261024-154045/`，再原子替换和重启。正式 PID `28588` 的实际运行路径与候选 hash 已复核。除明确迁移的 Version、Capsules 和选中模块映射外，其余偏好及全部 Provider 配置不变；原有 **6,215 条历史记录逐条一致**。
- `python .artifacts/verify-module-list.py` 在正式实例以 UIA 确认 5 个具名模块、5 个手柄、一个 Hone 开关、顺序及各启用状态、移除的控件和底部轮播。该检查未切换真实账户开关，配置字节保持不变，设置已定位到管理页。部署证据：`.artifacts/module-list-deployment.json`、`.artifacts/module-list-deployed-uia.log`。
- `python tools/check_docs.py`、`git diff --check` 和更新的 Python smoke 语法检查通过。锁屏阻断实际鼠标拖动和桌面截图验收；物理输入、真实 Windows DPI 及完整物理 wheel/provider-group smoke：**not-run**。未使用蓝框 Computer Use，未 commit / push。
- 参考 [Gong WPF DragDrop 源码](https://github.com/punker76/gong-wpf-dragdrop/blob/develop/src/GongSolutions.WPF.DragDrop/DefaultDropHandler.cs)、[PR #362](https://github.com/punker76/gong-wpf-dragdrop/pull/362) 和 [Issue #342](https://github.com/punker76/gong-wpf-dragdrop/issues/342)：采用向下插入位置修正、保留选择和手柄/开关分离的交互思想，继续使用本项目 WPF Thumb 与原子配置保存，不增加拖放库。

## 2026-10-24 Hone unified account settings

- 在上述未提交工作树继续修改：“数据源”左侧也按现有地址规范化规则合并同站点 Hone，右侧同页显示账户访问令牌和 API key。站点地址共用，各连接的名称、刷新、提醒、环境变量和 key 换算选项独立保留；不同站点在列表附带地址。新增菜单只保留 Hone，缺少的钱包或额外 API key 在页面内添加，统一保存或放弃。
- 新增 `HoneConnectionEditor` 与 `SettingsWindow.Hone.cs`。保存先验证所有连接，将变更凭据暂存到新身份，再一次保存配置；凭据或配置写入失败时清理暂存项并保留原配置和原密钥。没有改动的来源保持 Provider ID、原始 URL、Enabled 和历史；本次界面整合不迁移配置。删除单项与整组分别说明范围。连接测试逐项标注结果，并沿用当前请求所有权，编辑或离开时拒收旧结果。
- `build.ps1 -Check`：**211 项通过**；`dotnet run --project tests/PulseCapsule.Checks -c Release -- --ui-checks`：**83 项通过**，其中本轮新增 23 项。覆盖双表单保存、无改动保存、失败回退、暂存凭据失败、缺失钱包、草稿放弃、不同站点、密码框不回填已有密钥、逐项认证结果和异步失效。凭据写入只发生在隔离测试 scope。日志：`.artifacts/hone-account-{core,wpf}.log`。
- WPF 生产控件渲染：`.artifacts/settings-ui-20261024-155246/hone-account-{minimum,standard,200-render}.png`。标准窗口可同时看到两个凭据输入框；最小窗口允许表单滚动，保存操作固定。200% 是 pixel-scale 渲染，不是实际 Windows DPI 验收。
- `build.ps1 -Publish -OutputDirectory .artifacts/hone-account-candidate` 成功；`python .artifacts/check-hone-account-candidate.py` 的 **9 项 UIA 检查通过**，覆盖真实候选 EXE 的单一入口、凭据字段命名、保存、无效输入、草稿放弃及重启。日志：`.artifacts/hone-account-{publish,uia}.log`；UIA 结果：`.artifacts/hone-account-uia-20261024-155404/result.json`。
- `python .artifacts/deploy-hone-account.py` 正常关闭 PID `28588`，备份 EXE、配置和一致 SQLite 快照到 `.artifacts/backups/before-hone-account-20261024-155457/`，原子替换正式 EXE 并启动 PID `27060`。实际路径为 `dist/PulseCapsule.exe`，候选与正式 SHA-256 一致：`8B41132F2E089E80528C29633757E72AE21343716F5AC30BD411217807041B10`。
- 正式实例 UIA 确认数据源列表为 Hone、Codex、WgetCloud 共 3 项，Hone 仅有一个入口、两个分别命名的凭据框，旧分离入口和连接钱包跳转按钮已移除。配置字节和全部 Provider 配置保持一致；原有 **6,225 条历史记录逐条一致**。设置窗口停留在 Hone 页。证据：`.artifacts/hone-account-deployment.json`。
- `python tools/check_docs.py`、`git diff --check` 和更新的 `provider_group_smoke.py` 语法检查通过。物理输入、原生确认对话框矩阵、真实 Windows DPI、完整物理 provider-group smoke：**not-run**。没有改写真实账户凭据，未使用蓝框 Computer Use，未 commit / push。
- 参考 [CommunityToolkit SettingsExpander ItemsSource 示例](https://github.com/CommunityToolkit/Windows/blob/main/components/SettingsControls/samples/SettingsExpanderItemsSourceSample.xaml) 的共享标题和独立子项组织方式，并检查 [Issue #191](https://github.com/CommunityToolkit/Windows/issues/191) 的打包模板问题。该 Issue 属于 WinUI；这里只采用界面组织思想，使用显式 WPF 控件，并分别验证模板渲染与发布 EXE。
