# Implementation Plan: Saref Main Story E2E Audit

**Branch**: `codex/692-saref-e2e-audit` | **Date**: 2026-06-08 | **Spec**: `specs/692-saref-main-story-e2e-audit/spec.md`

**Input**: Feature specification from `/specs/692-saref-main-story-e2e-audit/spec.md`

## Summary

Audit and harden the hidden Saref / `Крылья над Бездной` main-story progression end-to-end with deterministic fixtures, validation/normalizer/command-result tests, GM-facing docs/examples, and tracked follow-up issues for gaps too large to fix in the audit PR. The implementation should start with RED tests/source guards, then add the smallest runtime/docs/example changes needed to make the stage walkthrough and player/GM flow verifiable.

## Technical Context

**Language/Version**: C# / .NET 8 for runtime and tests; Markdown/JSON for GM-facing docs/examples; TypeScript/React only if browser Saref write surfaces are touched.

**Primary Dependencies**: `BookOfEternityClient`, `BookOfEternityClient.Tests`, Spectre.Console command-result surfaces, file-backed JSON state, existing validation and canonical normalizer services.

**Storage**: Local file-backed `game_session` / `game_state` JSON fixtures and pending/control files.

**Testing**: `dotnet test` against `BookOfEternityClient.Tests`; documentation/source-guard tests; optional frontend `npm run verify` only if web frontend changes.

**Target Platform**: Windows/local loopback game client, with cross-platform C# tests where practical.

**Project Type**: Local game client with console and browser frontends over shared C# runtime.

**Performance Goals**: Audit tests should remain focused enough for normal local verification and avoid requiring true interactive console E2E until #674-#679 harness support is available.

**Constraints**: Preserve hidden-story anti-spoiler behavior, GM-facing contract synchronization, local-only runtime, and existing console/browser parity rules. Do not invent new Saref canon or broad mechanics without updating this Spec Kit feature or creating follow-up issues.

**Scale/Scope**: One hidden-story audit slice spanning runtime validation, canonical normalizer behavior, command-result/player surfaces, afterlife docs/examples, and closure evidence.

**Source Issue(s)**: #692 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/692

**Contract Scope**: runtime-state, validation, normalizer, pending/control lifecycle, console player commands, browser write service if implicated, GM-facing docs/examples.

**Verification Commands**:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SarefMainStory|FullyQualifiedName~CanonicalStateNormalizerTests.SarefMainStory|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal
git diff --check origin/main...HEAD
git diff origin/main...HEAD -- . ':(exclude)specs/**' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Constitution Check

*GATE: Must pass before implementation. Re-check after design and before PR.*

- **GitHub traceability**: PASS — #692 is linked in `spec.md`, this `plan.md`, and `tasks.md`.
- **Spec Kit fit**: PASS — Saref hidden-story audit is multi-file, contract-sensitive, and expected to span tests/docs/examples/follow-ups.
- **Player-facing integrity**: PASS — `/сареф`, `/сареф найти_крылья`, and `/воспоминание` must keep Russian in-world non-spoiler copy and avoid debug/API leakage.
- **Contract/state authority**: PASS — validation, normalizer, pending/control lifecycle, GM docs/examples, and source guards are explicitly planned.
- **Test-first path**: PASS — each implementation story starts with RED tests or source guards before runtime/docs changes.
- **Verification evidence**: PASS — focused C#, docs coverage, build, diff check, and static scan commands are listed.
- **Agent orchestration**: PASS — Hermes will pass constitution/spec/plan/tasks/checklist/contract paths and Superpowers TDD/debugging/review requirements into Codex; Hermes owns final acceptance.

## Project Structure

### Documentation (this feature)

```text
specs/692-saref-main-story-e2e-audit/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/
│   └── requirements.md
└── contracts/
    └── saref-main-story-audit.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/SarefMainStoryState.cs
BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs
BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SarefStory.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.MemoryScene.cs
BookOfEternityClient/WebUi/BrowserSarefStoryWriteService.cs
BookOfEternityClient.Tests/SarefMainStoryStateValidationTests.cs
BookOfEternityClient.Tests/CanonicalStateNormalizerTests.SarefMainStory.cs
BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs
BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs
OtherGuides/Saref_Character_Bible.md
OtherGuides/Saref_Memory_System_Boundaries.md
OtherGuides/Saref_Guardian_Questlines/*.md
OtherGuides/Afterlife_Contract_Matrix.md
Examples/E_CLI_Afterlife_Turns.txt
Examples/example_validation_manifest.json
CLI_Agent_Daemon_Specification.md
docs/superpowers/specs/2026-05-20-wings-over-the-abyss-design.md
```

**Structure Decision**: Keep the audit artifacts under `specs/692-saref-main-story-e2e-audit/`. Add or extend C# tests near existing Saref validation/normalizer/Explorer command tests. Put GM-facing stage/audit guidance in existing Saref/Afterlife guide or a clearly named audit doc only if tests alone would be unreadable. Update examples/manifests/source guards when authoring expectations change.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | The issue naturally requires Spec Kit due to hidden-story/afterlife contract scope. | Ordinary issue-only workflow would lose durable stage and branch coverage. |

## Implementation Strategy

1. **Baseline and context**: Confirm #749-#753 are closed, read #692 and relevant Saref files, and run the focused baseline. Baseline on 2026-06-08 in the fresh worktree passed: `166 passed, 0 failed, 0 skipped`.
2. **Stage matrix first**: Establish the stage map and deterministic fixture expectations before touching runtime behavior.
3. **TDD by slice**: Add RED tests/source guards for each story slice, watch them fail for missing audit coverage or invalid behavior, then implement the smallest code/docs/example changes.
4. **Scope containment**: Fix small gaps discovered by the audit in the same PR only when they are direct #692 acceptance items. Create GitHub follow-up issues for large new mechanics, missing harness work, or separate parity gaps.
5. **Review and closure**: Run focused local gates, request independent review, create/merge PR via local verification, comment evidence on #692, and close only after PR merge and issue state verification.
