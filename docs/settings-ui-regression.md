# Settings UI/UX regression matrix

Living regression cases plus dated evidence. Later dated entries describe subsequent changes; earlier pass counts and candidate hashes retain their original scope. See the [development guide](development.md) for validation selection and the [documentation index](README.md) for history.

Use a **separate demo/test data directory** and never use a real credential. Physical-input tests need an idle, unlocked native Windows interactive session; run them serially. If another instance occupies the target area, arrange a free target or an authorized exit rather than stopping unrelated processes. Visible Computer Use requires explicit consent.

## Connection-test audit baseline (2026-10-24)

The pasted 2026-10-23 review describes `ba2b103`. This baseline was reconciled against `c7071b3` and the connection-test fix in the working tree on 2026-10-24. The original P0/P1 list is historical: existing implementations need acceptance evidence, not duplicate implementation work. Visual congestion alone is not evidence of a release-blocking defect.

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
| Header + scroll | Select long WgetCloud/Custom form, shrink window to minimum and scroll fully | Title, enabled checkbox and bottom actions remain visible and clickable, scrollbar never covers them |
| Dirty / cancel | Change Name, Enabled, key, interval or advanced JSON field; select another source; Cancel | Selection and every unsaved input remain unchanged |
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
