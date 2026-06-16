# Requirements Checklist: Browser Detail Actions for Mortal Reference Commands

Source issue: #1057 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1057

## Completeness

- [x] Source GitHub issue is linked in spec, plan, tasks, contract, and checklist.
- [x] Spec Kit applicability is justified as multi-command player-facing browser/console parity work.
- [x] Affected commands are enumerated.
- [x] Out-of-scope boundaries include #946 NPC, #947 books, #949 afterlife, and already closed #1054/#1055/#1056 command-specific children.
- [x] Acceptance criteria from the issue are represented as functional requirements and success criteria.
- [x] Verification commands are listed for focused C#, broader C#, builds, Spec Kit prerequisites, diff hygiene, static/security scan, and frontend verify if frontend changes.
- [x] GM-facing docs/examples impact is explicitly scoped as not required unless runtime/GM-authored contracts change.

## Ambiguity Review

- [x] "Browser detail actions" may be implemented as action metadata, detail commands, or equivalent command-result affordances, as long as the default browser player can inspect one rich entity without raw JSON as the only path.
- [x] Complete coverage of all eight affected commands is preferred; if a command is deferred, the implementation must record a precise follow-up issue and reason in the audit artifact before merge.
- [x] Browser UI direction is current minimalist command-result flow, not obsolete card-heavy Feature-branch UI.
- [x] React gameplay logic is explicitly disallowed unless a tracked follow-up changes the browser architecture; C# shared command/result authority remains source of truth.

## Testability

- [x] Requirements identify focused browser/shared command-result tests.
- [x] Requirements identify console/catalog/source-guard parity tests.
- [x] Requirements identify overview-preservation tests.
- [x] Requirements identify raw/debug/default-copy negative checks.
- [x] Requirements include post-implementation update of exact focused test filters in `tasks.md`.

## Scope Guard

- [x] No afterlife, Chaos Sea, Shining Abode, GM prompt/example, validation, normalizer, pending/control, NPC, books, or mutating prompt-session changes are authorized by this feature.
- [x] Any discovered contract/schema gap must be split into a tracked follow-up rather than silently implemented.
