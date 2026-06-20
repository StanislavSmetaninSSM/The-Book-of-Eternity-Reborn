# Implementation Plan: Repository Documentation Cleanup

**Branch**: `task/1190-doc-cleanup` | **Date**: 2026-06-21 | **Spec**: `specs/1190-doc-cleanup/spec.md`

**Input**: Feature specification from `specs/1190-doc-cleanup/spec.md`

## Summary

Clean obsolete agent/developer planning noise out of game-facing documentation directories, especially `OtherGuides`, while preserving active GM guidance and contract documentation. The approach is audit-first: inventory candidate files, check references before deletion, remove only clearly obsolete files, then verify references and docs tests.

## Technical Context

**Language/Version**: Markdown, text, JSON, C# docs/source-guard tests in .NET 8.

**Primary Dependencies**: Git, ripgrep, GitHub issue #1190, existing C# test project.

**Storage**: Repository files.

**Testing**: `rg` reference checks, `git diff --check`, targeted `dotnet test`.

**Target Platform**: Local repository on Windows.

**Project Type**: Local game client repository with GM-facing docs and runtime tests.

**Performance Goals**: Keep cleanup scoped and reviewable; do not perform broad rewrites.

**Constraints**: Do not touch `docs/superpowers/**`; do not delete contract docs referenced by tests/prompts without updating references; do not alter runtime code unless a broken reference requires a source guard update.

**Scale/Scope**: Repo-wide docs audit, with first-pass focus on `OtherGuides`, root README-like files, `TaskGuides`, `Rules`, `Examples`, and stray technical files outside designated technical directories.

**Source Issue(s)**: #1190 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1190

**Contract Scope**: GM-facing docs, examples/reference integrity.

**Verification Commands**:

- `git diff --check`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|BrowserFrontendWorkspaceTests"`
- `rg` checks for deleted filenames and paths

## Constitution Check

- **GitHub traceability**: Pass. Source issue #1190 exists.
- **Spec Kit fit**: Pass. GM-facing docs and repo-wide cleanup span multiple files and require durable scope.
- **Player-facing integrity**: Pass. No player UI changes planned.
- **Contract/state authority**: Pass. Contract docs and examples are preserved unless references are updated.
- **Test-first path**: Documentation cleanup uses reference inventory before deletion and targeted docs/source-guard verification after deletion. Automated red-green tests are not applicable to removing obsolete unreferenced docs.
- **Verification evidence**: Pass. Verification commands are listed above.
- **Agent orchestration**: Pass. Work is done directly by Codex with Superpowers discipline.

## Project Structure

### Documentation (this feature)

```text
specs/1190-doc-cleanup/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code / Documentation Touched

```text
OtherGuides/                         # Primary GM-facing docs cleanup target
TaskGuides/                          # Secondary guidance cleanup target
Rules/                               # Preserve rule docs unless clearly obsolete
Examples/                            # Preserve examples/manifests unless references are updated
README*.md and root docs             # Check for stale/developer-only files
BookOfEternityClient.Tests/          # Only update if source guards reference removed files
AGENTS.md                            # Spec Kit active plan pointer only
```

**Structure Decision**: Keep technical working artifacts inside `docs/superpowers/**` and Spec Kit artifacts inside `specs/1190-doc-cleanup/**`. Remove or relocate only files that are misleading in game-facing directories.

## Complexity Tracking

No constitution violations.
