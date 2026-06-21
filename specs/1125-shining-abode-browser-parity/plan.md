# Implementation Plan: Shining Abode Browser Command Output Parity

**Branch**: `work/1125-shining-browser` | **Date**: 2026-06-21 | **Spec**: `specs/1125-shining-abode-browser-parity/spec.md`

**Input**: Feature specification from `specs/1125-shining-abode-browser-parity/spec.md`

## Summary

Close #1125 by auditing and hardening Shining Abode browser command output against console semantic expectations. The implementation path is test-first: add focused regression tests for audited gaps, then make the smallest shared C# builder changes required. If existing builders already satisfy a surface, keep regression coverage as evidence and avoid runtime contract changes.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: `ExplorerWebCommandService`, `ExplorerShiningAbodeCommandResultBuilder`, Shining Abode state services, and existing xUnit tests.

**Storage**: Existing JSON game-state fixtures in tests; no new persisted schema intended.

**Testing**: `dotnet test` focused xUnit filters.

**Target Platform**: Local browser client over the C# game engine.

**Project Type**: Desktop/local game client with browser UI facade.

**Performance Goals**: No measurable runtime impact; command-result builders remain lightweight over existing state reads.

**Constraints**: Shining Abode afterlife contract rules apply; hidden/GM-only state must remain hidden in default browser output.

**Scale/Scope**: Shining Abode browser command outputs named in #1125.

**Source Issue(s)**: #1125 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1125

**Contract Scope**: player-facing browser output and existing action previews/forms only; no GM-facing prompt/docs/runtime-state contract changes are planned.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Shining|Afterlife|ExplorerWebCommand" --verbosity minimal`
- If afterlife contracts change: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --verbosity minimal`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. #1125 is linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is broad afterlife/browser parity and contract-sensitive UX work.
- **Player-facing integrity**: PASS. Russian readable summaries/details and no technical leakage are explicit requirements.
- **Contract/state authority**: PASS with guardrail. Runtime contract changes are out of scope unless a tested gap proves them necessary.
- **Test-first path**: PASS. Production changes require failing browser command tests first.
- **Verification evidence**: PASS. Focused/broad dotnet command and contract-doc fallback are listed.
- **Agent orchestration**: PASS. Work is local Codex implementation, no delegation.

## Project Structure

### Documentation (this feature)

```text
specs/1125-shining-abode-browser-parity/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── UI/ExplorerShiningAbodeCommandResultBuilder.cs
└── UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode*.cs

BookOfEternityClient.Tests/
├── ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs
└── WebUi/BrowserShining*Tests.cs
```

**Structure Decision**: Keep command authority in shared C# builders and add/extend existing Shining browser command tests.

## Complexity Tracking

No constitution violations.
