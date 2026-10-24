# PulseCapsule Design System

> Windows desktop · WPF / .NET 10 · living guidelines (2026-10)

Current design guidance; dated acceptance results live in the [settings regression matrix](settings-ui-regression.md). See the [documentation index](README.md) for other topics.

## Product principles

PulseCapsule is a compact, always-available status monitor. Prioritize **glanceability**, predictable controls, safe account operations, and native Windows behavior. Avoid decorative animations, deep card nesting, and technical details in primary tasks. Prefer one column for editable forms and task-first labels.

## Tokens

All shared colors and reusable control styles belong in [App.xaml](../src/PulseCapsule/App.xaml).

| Resource | Baseline | Intended meaning |
| --- | --- | --- |
| Canvas | `#111820` | Window background |
| Panel | `#1A232D` | Content surface |
| Line | `#2B3845` | Separators |
| Text | `#F1F6FA` | Primary text |
| Muted | `#94A8B9` | Secondary descriptions |
| Mint / Success | `#A6EDCF` | Primary emphasis / success |
| Warning | `#FFD389` | Outdated data / caution |
| Danger | `#FFB1A6` | Destructive operations |
| Focus | `#B2E8FF` | Visible keyboard focus |

Do not encode status by color alone. Verify contrast for every new state; color values are initial baselines, not immutable brand assets.

Use Segoe UI / Microsoft YaHei UI; headings ~24 DIP, section headers 16–18 DIP, body ~14 DIP, supporting text 12–13 DIP. Prefer 4 DIP spacing increments: `4 / 8 / 12 / 16 / 24 / 32`. Controls should have consistent hit areas, deliberate focus/disabled/hover states, and clear labels.

These text sizes guide settings and expanded reading surfaces. Collapsed and taskbar capsules need a separate compact density budget, verified for legibility at actual Windows DPI settings.

## Settings and navigation

- Use three top-level pages: 数据源 / 胶囊管理 / 桌面偏好. The source list and account editor scroll independently; keep the account action bar fixed.
- Account changes use explicit Save / Discard / Cancel. Global toggles, ordering and rotation interval save immediately; show saved feedback and restore the prior value when persistence fails. Global preferences must preserve quota backoff and in-flight requests.
- Make the selected data source unmistakable. Treat adding a source as a separate unsaved task.
- Structure account forms as **identity → connection → credentials → refresh and reminders → advanced options**. Source enable switches live on 胶囊管理 and save immediately.
- The source list also uses one Hone entry per normalized site. Show the wallet token and API key on the same page, with a shared site address and separate expandable connection options. Save all member forms together; invalid input or failed credential/configuration staging must retain the original group. Merely opening or saving unchanged forms preserves each Provider ID, raw URL, enable flag and credential. Test each connection independently and label partial failures. Missing connections are added as drafts within the page; clearing/removing a connection states its scope, while removing Hone affects the whole site.
- Use one compact row and one enable switch per supplier, with the same provider identity and normalized site as quota cards. Hone displays only its supplier name; toggling it saves all member sources together. Keep disabled suppliers reachable. An existing mixed configuration appears enabled if any member is enabled; loading the page does not rewrite those flags. Credentials and monetary values remain source-owned.
- Place suppliers and built-in modules in one list under their own names (时钟, Hone, Codex, WgetCloud, 硬件监控). Each row has a separate drag handle and enable switch; do not add family rows or up/down buttons. Persist on drop, cancel outside the list or on Escape, and scroll at the list edges during a drag. Keep automatic rotation below the list. Remove hardware sub-option switches from this page while preserving their stored values.
- Keep the source title outside the scroll area; preserve a fixed action bar.
- Reveal WgetCloud JSON and other specialized configuration only when relevant. Never hide required connection fields.
- A changed form must require **Save / Discard / Cancel** on navigation, adding another source, or closing.
- Do not equate test success with saved configuration, nor save success with verified connectivity.
- Bind connection feedback to the exact form and request that produced it. Editing, switching sources/pages, saving, discarding, changing credentials or closing must invalidate a pending test. Obsolete success, errors and cleanup must not change the current feedback or a newer test's button state, even if the provider ignores cancellation.

## Credentials, errors and destructive operations

- Continue using Windows Credential Manager. Never display stored secrets or write them into logs/screenshots.
- Credential clearing needs confirmation and must disclose that environment variables might still authenticate.
- Removing a source needs confirmation, a clear affected-scope description and accurate error handling if the saved configuration and credential cleanup diverge.
- Keep WgetCloud URL restrictions and all existing provider identity, storage and network security checks.
- Place field-specific validation near the field when practical; display connectivity result near the Test button, including testing, success and failure.

## Accessibility and responsive sizing

- Preserve keyboard focus outlines on buttons, text fields, toggles and navigation items.
- Name every interactive input in the UI Automation tree; test with actual UIA and keyboard navigation.
- Support minimum settings window dimensions, long localized text and Windows 100/125/150/200% DPI.
- Never let scrollbars or clipping make important operations inaccessible.
- Confirm colors and semantics under Windows light and dark system modes.

## Expanded cards

- Expose 时间 / 额度 / 系统 navigation beneath the header, with selection and keyboard focus visible. Left/right keys operate within this navigation. A deliberate navigation click may acquire keyboard focus; background refreshes must not activate the window.
- Keep name, status, primary/secondary values and update time in the summary. Remember details per source ID. Codex rate windows and resets always remain visible.
- Move forecasts, trends and healthy WgetCloud plan notes into details. Errors retain inline Retry and Settings actions; each source has its own refresh/edit/disable menu.
- Trend plots require at least three distinct samples spanning one hour, use elapsed time for X and include zero in Y. Show exact point tooltips, scale and observed change; do not imply a complete week of samples.

## Performance and constraints

Retain WPF / .NET 10, current data persistence and runtime service boundaries. Avoid new WebView or rendering frameworks for cosmetic work. Measure transparent-window/shadow/DPI overhead before pursuing complex optimizations. Preserve existing monitor backoff, refresh behavior and historical timestamps.

## Quality gates

Select validation for the changed behavior using the [development guide](development.md) and affected cases in the [settings regression matrix](settings-ui-regression.md). UI code changes need a build and relevant automated checks; physical input, modal dialogs and actual Windows DPI need observed desktop evidence when those behaviors are in scope. Record unavailable cases as not-run or environment-blocked rather than rerunning unrelated suites. Wording-only documentation edits need documentation checks.

## References

- [Microsoft Fluent 2](https://fluent2.microsoft.design/)
- [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill)
- [Impeccable](https://github.com/pbakaus/impeccable)
- [Awesome DESIGN.md](https://github.com/VoltAgent/awesome-design-md)
- [Vercel Agent Skills](https://github.com/vercel-labs/agent-skills)
- [Taste Skill](https://github.com/Leonxlnx/taste-skill)
- [Anthropic Skills](https://github.com/anthropics/skills)
- [Rx.NET Switch implementation](https://github.com/dotnet/reactive/blob/main/Rx.NET/Source/src/System.Reactive/Linq/Observable/Switch.cs): retain only the current operation's notifications; PulseCapsule implements this locally without adding Rx dependencies.
- [CommunityToolkit SettingsExpander ItemsSource sample](https://github.com/CommunityToolkit/Windows/blob/main/components/SettingsControls/samples/SettingsExpanderItemsSourceSample.xaml): one account heading with independently configured children. The [template issue #191](https://github.com/CommunityToolkit/Windows/issues/191) concerns WinUI packaged controls, not this WPF application; PulseCapsule uses explicit WPF connection controls and verifies both rendered templates and the published EXE, without adding a WinUI dependency.
- [Gong WPF DragDrop source](https://github.com/punker76/gong-wpf-dragdrop/blob/develop/src/GongSolutions.WPF.DragDrop/DefaultDropHandler.cs), [PR #362](https://github.com/punker76/gong-wpf-dragdrop/pull/362) and [checkbox issue #342](https://github.com/punker76/gong-wpf-dragdrop/issues/342): correct downward insertion after source removal, preserve selection and separate dragging from toggling. PulseCapsule keeps its native WPF Thumb handler and atomic settings save; no drag library dependency is added.
- [WPF ToggleButtonAutomationPeer](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Automation/Peers/ToggleButtonAutomationPeer.cs): UIA Toggle invokes `OnToggle` directly. Supplier switches therefore handle Checked/Unchecked and ignore their initial bound value, preserving mixed source flags until a user or UIA toggle changes the group state.
