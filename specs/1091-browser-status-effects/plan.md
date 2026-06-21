# Implementation Plan: Browser Status and Effect Details

**Branch**: `work/1091-browser-status` | **Date**: 2026-06-21 | **Spec**: `specs/1091-browser-status-effects/spec.md`

**Input**: Feature specification from `specs/1091-browser-status-effects/spec.md`

## Summary

Close #1091 by hardening browser `/статус` output: make resource values visually scannable, keep realm/time labels player-facing, expose active effect summaries, and add read-only effect detail routes where structured effect data exists. The implementation path is audit plus TDD: find the current browser status/effect command path, add failing xUnit coverage for the visible gaps, then make the smallest C#/frontend changes needed.

## Technical Context

**Language/Version**: C#/.NET 8 backend command services; TypeScript/React only if current browser status UI needs frontend support.

**Primary Dependencies**: `ExplorerWebCommandService`, browser command-result builders, status/effects state models, existing frontend status components.

**Storage**: Existing JSON fixtures/state only; no new persisted schema planned.

**Testing**: xUnit for browser command output, frontend verify if React/CSS changes are touched.

**Target Platform**: Local browser client over the C# game engine.

**Project Type**: Desktop/local game with browser facade.

**Performance Goals**: Status/effect rendering stays lightweight over already-loaded state.

**Constraints**: No raw JSON/debug/default internal wording; preserve existing dark-fantasy style; no GM contract change unless proven necessary.

**Source Issue(s)**: #1091 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1091

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Status|Effect|ExplorerWebCommand" --verbosity minimal`
- If frontend files change: `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. #1091 is linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This changes player-facing browser UX and console/browser parity.
- **Player-facing integrity**: PASS. Russian labels, no raw diagnostics, and detail actions are explicit.
- **Contract/state authority**: PASS with guardrail. No GM-authored contract changes are planned.
- **Test-first path**: PASS. New behavior requires failing tests before production changes.
- **Verification evidence**: PASS. C# verification, optional frontend verification, diff check, and screenshots are listed.

## Project Structure

### Documentation

```text
specs/1091-browser-status-effects/
├── spec.md
├── plan.md
└── tasks.md
```

### Likely Source Areas

```text
BookOfEternityClient/
└── UI/
    ├── ExplorerMortalWorldCommandResultBuilder.cs
    └── Explorer*CommandResultBuilder*.cs

BookOfEternityClient.Tests/
└── *ExplorerWebCommand* / *Status* / *Effects* tests

BookOfEternityClient.WebFrontend/
└── src/components/StatusView.tsx (only if frontend changes are needed)
```

**Structure Decision**: Prefer typed `UiBlock` command-result improvements in C# builders. Touch React/CSS only if existing block rendering cannot express the needed bars/details.

## Complexity Tracking

No constitution violations.
