# Focused Integration Test Selection Design

**Issue:** [#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510)

**Status:** Approved in conversation on 2026-08-03

## Context

Faction Materialization adds focused tests to both
`BookOfEternityClient.Tests` and `BookOfEternityClient.IntegrationTests`.
The approved #1510 plan routes every C# check through
`scripts/test-csharp.ps1` and forbids raw `dotnet test`, broad per-edit
diagnostic lanes, wider timeouts, or higher concurrency.

The current runner defines `Focused` as a five-minute caller-filtered lane
hard-wired to the fast test project. A filter containing integration class
names therefore exits successfully after running only matching fast-project
tests; it neither builds nor discovers the requested integration tests. This
creates false GREEN evidence and makes focused integration TDD impossible.

## Decision

Extend the existing `Focused` lane with one explicit project selector:

```powershell
pwsh .\scripts\test-csharp.ps1 `
  -Lane Focused `
  -FocusedProject Integration `
  -Filter "FullyQualifiedName~FactionMaterializationValidationTests"
```

`-FocusedProject` accepts exactly `Fast` or `Integration`.

- Omitting it preserves the current `Fast` default.
- Supplying it is valid only with `-Lane Focused`.
- `FocusedProject=Integration` selects
  `BookOfEternityClient.IntegrationTests.csproj`.
- The caller-provided VSTest filter remains mandatory.
- The existing five-minute Focused hard limit remains unchanged.
- Existing parallelism limits remain unchanged.
- No new lane is introduced.

The runner must expose enough plan/result evidence to prove which project was
selected. At minimum, `-PlanOnly` and the ordinary log must name the resolved
test project; a successful run whose filter matches no tests still fails under
the existing zero-result guard.

## Validation

Development follows RED → GREEN:

1. Add fast source/contract coverage that requires the selector, its closed
   values, default-fast compatibility, non-Focused rejection, and project
   override.
2. Before implementation, demonstrate that the desired
   `FocusedProject=Integration` invocation is rejected as an unknown
   parameter.
3. Implement the smallest runner change.
4. Run the focused fast boundary tests.
5. Run one exact pre-existing integration boundary method through
   `-Lane Focused -FocusedProject Integration` and verify the integration
   project is built and the requested method is reported.
6. Verify `-PlanOnly` identifies the Integration project and that the default
   Focused invocation still identifies the Fast project.

`docs/testing.md`, the #1510 Spec Kit quickstart/verification commands, and the
detailed Superpowers plan must distinguish fast-project and integration-project
Focused commands. Mixed filters that silently target only one project are
forbidden.

## Alternatives Considered

### Add a new `IntegrationFocused` lane

Rejected. It duplicates Focused semantics, expands the public lane vocabulary,
and conflicts with #1510's requirement not to add another test lane.

### Use `RegressionIntegration`, `FullValidation`, or `PreMerge` while iterating

Rejected. Those lanes are broader diagnostic or merge controls and would
restore the long feedback loops that the test-harness redesign was meant to
remove.

### Invoke `dotnet test` directly against the integration project

Rejected. It bypasses the single bounded runner, its timeout, TRX merge,
duplicate detection, process ownership, and cleanup guarantees.

### Move file-backed tests into the fast project

Rejected. It breaks the deliberate fast/integration assembly boundary and
would make the ordinary Fast checkpoint slower.

## Scope Boundaries

This correction changes only focused project selection, its tests, runner
documentation, and #1510 verification commands. It does not:

- change category membership;
- move test files between projects;
- change Fast, diagnostic, or PreMerge selections;
- increase timeout or concurrency;
- alter production game behavior;
- create a general arbitrary-project execution surface.
