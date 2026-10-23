# PulseCapsule Design System

> Windows desktop · WPF / .NET 10 · living guidelines (2026-10)

## Product principles

PulseCapsule is a compact, always-available status monitor. Prioritize **glanceability**, predictable controls, safe account operations, and native Windows behavior. Avoid decorative animations, deep card nesting, and technical details in primary tasks. Prefer one column for editable forms and task-first labels.

## Tokens

All shared colors and reusable control styles belong in `src/PulseCapsule/App.xaml`.

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

## Settings and navigation

- Keep data sources distinct from global desktop preferences and capsule management.
- Make the selected data source unmistakable. Treat adding a source as a separate unsaved task.
- Structure forms as **identity and enabled state → connection → credentials → refresh and reminders → advanced options**.
- Keep the source title and enabled state outside the scroll area; preserve a fixed action bar.
- Reveal WgetCloud JSON and other specialized configuration only when relevant. Never hide required connection fields.
- A changed form must require **Save / Discard / Cancel** on navigation, adding another source, or closing.
- Do not equate test success with saved configuration, nor save success with verified connectivity.

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

## Performance and constraints

Retain WPF / .NET 10, current data persistence and runtime service boundaries. Avoid new WebView or rendering frameworks for cosmetic work. Measure transparent-window/shadow/DPI overhead before pursuing complex optimizations. Preserve existing monitor backoff, refresh behavior and historical timestamps.

## Quality gates

Every UI change needs: a clean build, core checks, an isolated-data desktop smoke where reliable, and the manual cases in `docs/settings-ui-regression.md`. Never mark modal-dialog, high-DPI or physical-input checks as passed without observing them.

## References

- [Microsoft Fluent 2](https://fluent2.microsoft.design/)
- [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill)
- [Impeccable](https://github.com/pbakaus/impeccable)
- [Awesome DESIGN.md](https://github.com/VoltAgent/awesome-design-md)
- [Vercel Agent Skills](https://github.com/vercel-labs/agent-skills)
- [Taste Skill](https://github.com/Leonxlnx/taste-skill)
- [Anthropic Skills](https://github.com/anthropics/skills)
