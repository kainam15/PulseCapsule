# Settings UI/UX regression matrix

Use a **separate demo/test data directory** and never use a real credential. Close other PulseCapsule instances if running physical-input tests; do not automate a user's active desktop. Test in a native Windows interactive session.

| Case | Steps | Pass condition |
| --- | --- | --- |
| P0 header + scroll | Select long WgetCloud/Custom form, shrink window to minimum and scroll fully | Title, enabled checkbox and bottom actions remain visible and clickable, scrollbar never covers them |
| P0 dirty / cancel | Change Name, Enabled, key, interval or advanced JSON field; select another source; Cancel | Selection and every unsaved input remain unchanged |
| P0 dirty / discard | Change a source; switch to another; choose Discard | New source loads; old configuration remains unchanged |
| P0 dirty / save | Modify source then switch; choose Save | Save validates, persists data and selects intended destination; invalid fields block navigation |
| P0 drafts | Add a new source, switch away / close; Save, Discard and Cancel separately | Draft cannot disappear silently; discarded draft is not persisted |
| P0 window close | Change source and close via X button or Alt+F4 | Three-way confirmation; Cancel keeps window open |
| P0 remove | Remove source → Cancel; repeat → Confirm | First attempt changes nothing; confirmed removal updates config, does not prematurely erase historical data |
| P1 credential clear | Clear key → Cancel; repeat → Confirm; environment variable configured | Nothing changes on Cancel; confirmed clearance affects Credential Manager only; copy explicitly warns environment variable may still work |
| P1 failure | Simulate invalid interval, HTTP 401/403, timeout, rate-limit, offline and credential deletion failure | Accurate, local feedback; test ≠ saved; preserved old values keep original timestamp |
| P1 keyboard + UIA | Tab / Shift+Tab / Enter / Space; inspect UIA labels | Focus clearly visible and names convey each field without a tooltip |
| P1 WgetCloud | Browser login, HTML/JSON switch, advanced settings expanded/collapsed | URL policy unchanged, nonrequired fields hidden, input values preserved |
| P2 window scale | Repeat key flows at 100/125/150/200% DPI and narrow window | No overlap/clipping, including long Chinese and English labels |
| P2 performance | Idle main capsule then open/scroll/close settings repeatedly | No unbounded resource growth or significant responsiveness regressions |

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

## Verification recorded on 2026-10-24

- Release build and self-contained `win-x64` publish succeeded. Core checks: **197 passed**. Production WPF handler/layout checks: **16 passed**. Updated Python smoke scripts compile; their interactive suites were not run in this session.
- WPF checks include the minimum client area, 14 long account names, independent scrolling, inline invalid-field feedback, failed-save rollback, preservation of global preferences when saving an account, capsule ordering, persisted card details, and single-source refresh with debounce preserved.
- Candidate: `.artifacts/ui-candidate/PulseCapsule.exe`; SHA-256 `1DB984A70ADC7CAF3335C3430E62FDC4BE87DEE19EE2529A3FD47B1CC4E9395D`. Test data: `.artifacts/ui-desktop-20261024`, launched with `--demo --settings --data-dir`. Formal accounts, deployed `dist` executable and Windows scaling were not changed.
- Authorized Computer Use on the candidate confirmed all three settings pages, visible dark scrollbars, immediate fullscreen-preference persistence in the isolated JSON file, inline interval validation, the native unsaved-changes dialog, Escape cancellation retaining the edit, and Discard restoring the saved value. A physical click on the visible Time navigation also changed the capsule content.
- Desktop acceptance is **partial**: the helper intermittently bound coordinates to the owner instead of the settings window and did not expose the standalone NOACTIVATE capsule as a selectable window. Actual minimum-window resizing, left/right focus, card-menu actions/dismissal and restart persistence were not accepted as physical-input passes. WPF/render checks do not substitute for these cases.
- Actual 100/125/150/200% Windows DPI acceptance remains pending. The 200% PNG is pixel-scale rendering only. Logs: `.artifacts/ui-core-checks.log`, `.artifacts/ui-wpf-checks.log`, `.artifacts/ui-publish.log`; render evidence: `.artifacts/settings-ui-20261024-105143/`.
