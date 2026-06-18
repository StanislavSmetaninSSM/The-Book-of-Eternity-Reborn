# Requirements Checklist: Console Faction Detail Drill-Down Menu Sections

Source issue: #1086 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1086

## Spec Quality

- [X] Source GitHub issue is linked in spec, plan, tasks, contract, and checklist.
- [X] Spec Kit justification is explicit and aligned with the constitution usage policy.
- [X] Scope separates Console Client read-only UX from browser UI, mutating faction actions, Shining write flows, and runtime schema changes.
- [X] User stories are independently testable.
- [X] Acceptance criteria include menu/actions, section details, empty states, hidden/default visibility, tests, build, and terminal/capture evidence.
- [X] Verification commands are listed with concrete paths and filters.

## Implementation Guardrails

- [X] Default output must be Russian/in-world and player-facing.
- [X] Raw JSON/API/DTO/debug/internal wording is forbidden in default player-facing output.
- [X] Hidden/GM-only faction memory and chronicle entries remain hidden.
- [X] Dynamic text must be escaped before Spectre.Console markup.
- [X] Implementation must stay read-only unless a new tracked issue/spec explicitly expands scope.
- [X] Existing overview/summary and #1085 column alignment expectations must remain covered.

## Closure Gates

- [X] Focused tests/source guards added with RED evidence before production changes.
- [X] Focused and broader local verification commands pass with exact counts.
- [X] `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passes.
- [X] Spec Kit prerequisite check resolves `specs/1086-console-faction-drilldowns`.
- [X] `git diff --check origin/main...HEAD` and added-line scan pass.
- [X] Console terminal/plain-text evidence shows new selected faction section menu/actions.
- [ ] Independent review approves or all Critical/Important findings are resolved before PR/merge.
