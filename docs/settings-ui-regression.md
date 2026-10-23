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

The settings safety work is intentionally split from a full navigation redesign and broader display/performance baselines. Review and run the remaining rows before declaring the full roadmap complete.
