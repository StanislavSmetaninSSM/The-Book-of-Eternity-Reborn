# Quickstart: Complete Faction Materialization

## Preconditions

Work only on the issue
[#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510)
branch/worktree. Before implementation:

```powershell
git status --short
gh issue view 1510 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn
Get-Content .\AGENTS.md
Get-Content .\.specify\memory\constitution.md
Get-Content .\specs\1510-complete-faction-materialization\spec.md
Get-Content .\specs\1510-complete-faction-materialization\plan.md
Get-Content .\specs\1510-complete-faction-materialization\tasks.md
```

Do not stage `.serena/`, `bin/`, `obj/`, test results, or unrelated worktree
artifacts.

## Development loop

Use the task order in `tasks.md`. For each behavior:

1. Add the smallest focused test.
2. Run the exact project-scoped `Focused` filter and record the expected RED
   failure.
3. Implement only enough production behavior for GREEN.
4. Re-run the same filter.
5. Inspect the diff for authority bypass, semantic defaulting, metadata leakage,
   and unrelated state mutation.
6. Commit one coherent slice with `(#1510)` in the message.

Never use an unbounded full-solution or full-suite `dotnet test` command.
Focused defaults to the fast project. Pass `-FocusedProject Integration` for
classes owned by `BookOfEternityClient.IntegrationTests`, and never mix test
classes from the two projects in one filter.

## Focused filters by slice

### Common envelope and classification

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests"
```

### Mortal normalization and narrow updates

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionCoreChangesContractTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionCoreChangesTests|FullyQualifiedName~CanonicalStateNormalizerTests"
```

### Shining route materialization

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests"
```

If this combined filter becomes noisy, use one exact class or method filter
while iterating; do not compensate by raising lane limits.

### Validation phase and repair routing

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FullValidationEquivalenceTests|FullyQualifiedName~IntegrationTestBoundaryTests|FullyQualifiedName~FactionMaterializationValidationTests"
```

### Documentation, examples, and privacy guards

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~ValidationSourceGuardTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~AfterlifeShiningPlayerFacingSourceGuardTests|FullyQualifiedName~ShiningAbodeStateTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests"
```

## Checkpoints

### Stable cross-domain checkpoint

Run Fast once after common, Mortal, and Shining focused checks are green:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Fast
```

Do not rerun it after every documentation edit.

### Required afterlife documentation check

Because #1510 changes Shining runtime contracts, matrix/examples/manifest, and
afterlife source guards:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh .\scripts\test-csharp.ps1 -Lane FullValidation
```

Run `FullValidation` once after those surfaces stabilize, not as a routine loop.

### Final integration check

From a clean checkout of the final candidate commit:

```powershell
git status --short
pwsh .\scripts\test-csharp.ps1 -Lane PreMerge
```

PreMerge already includes the complete fast project; do not run a duplicate
Fast immediately before it. Keep the existing 15-minute hard cap and current
concurrency.

## Manual contract probes

Automated tests are primary. During implementation, these reviewed fixtures
should also be easy to reason about:

- populated Mortal first creation;
- seven-section empty-by-design Mortal creation;
- legacy Mortal promotion with preserved history;
- `FactionCoreChanges.relations` absolute update;
- forbidden full resend of an already materialized faction;
- native Shining discovery with 1/1/2–4/2 counts;
- exact player founding;
- hidden story faction with exact story authority;
- missing Shining agenda before normalization;
- immutable receipt mutation;
- bounded Mortal and Shining repair packets;
- untouched legacy load and derived-only recomputation.

Do not manually alter a real user save for these probes. Use isolated test
fixtures and the existing test filesystem.

## Completion evidence

Before reporting completion:

- every completed `tasks.md` item has a matching diff and verification result;
- `git diff --check` is clean;
- no private materialization field/reason is rendered in ordinary UI;
- Mortal and Shining prompts/docs/examples/manifests agree with runtime;
- FullValidation and clean PreMerge results are fresh;
- no timeout/concurrency was widened;
- #1222, #1368, and #1462 boundaries remain out of the implementation;
- the PR links #1510 and records commands, counts, durations, and residual risk.
