# Agent C# Test Execution Policy Design

## Context

Issue #1505 introduced physically separate fast and integration test projects,
bounded test lanes, and a documented development rhythm. The detailed guidance
lives in `docs/testing.md`, but root `AGENTS.md` does not permanently require
agents to follow it. Its generated Spec Kit pointer can move to another feature,
and its documentation-sensitive afterlife command still invokes `dotnet test`
directly and names a test class that has moved to the integration project.

An agent that reads only `AGENTS.md` can therefore miss the bounded runner and
start an unnecessarily broad or unbounded test run.

## Goals

- Make the development-friendly C# test rhythm visible to every repository
  agent through root `AGENTS.md`.
- Keep ordinary edit feedback below the Fast lane's five-minute deadline.
- Keep the final uninterrupted pre-merge gate within the PreMerge lane's
  fifteen-minute deadline.
- Keep heavy diagnostic lanes conditional instead of coupling them to every
  edit or every successful PreMerge run.
- Correct the stale afterlife verification guidance without expanding this
  documentation-only change into a test-runner feature.

## Non-goals

- Change lane composition, filtering, deadlines, process containment, or test
  categorization.
- Add a technical block that makes direct `dotnet test` impossible.
- Add an integration-focused runner lane.
- Run the C# test suite merely to verify instruction-only edits.

## Decision

Add a concise mandatory C# test policy to root `AGENTS.md` and keep
`docs/testing.md` as the canonical detailed runbook.

The root policy will require agents to:

1. Use `scripts/test-csharp.ps1` as the normal C# verification entry point.
2. During implementation, run the smallest relevant `Focused` selection and
   one `Fast` control at a meaningful checkpoint.
3. Before merge, run one `PreMerge` control. Do not add two immediately
   consecutive Fast runs because PreMerge already includes the complete fast
   project.
4. Run `DeepValidation`, `LifecycleIntegration`, and other diagnostic lanes
   only for a related boundary change, failure diagnosis, or an explicit
   exhaustive-verification request.
5. Avoid unbounded full-solution or full-suite `dotnet test` commands as an
   ordinary verification step.
6. Read `docs/testing.md` for lane selection, limits, output, and failure
   diagnosis.

This is guidance rather than a shell-level prohibition. A narrowly filtered
direct command remains possible when a repository-specific instruction
explicitly requires it, but an agent must not substitute an unbounded direct
run for the bounded workflow.

## Documentation-sensitive afterlife verification

Keep the fast `AfterlifeDocumentationCoverageTests` selection bounded through
the `Focused` lane. `ExampleDocumentationValidationTests` now belongs to the
integration project's `FullValidation` category, so the afterlife guidance
will name its current path and route that portion through `FullValidation`.

The resulting guidance intentionally uses two purpose-specific bounded
controls rather than one stale direct command:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
```

`FullValidation` is appropriate only when the documentation/examples boundary
is actually affected. It remains a conditional diagnostic lane under the
general policy.

## Detailed runbook alignment

Update `docs/testing.md` so final verification says:

- use `Focused` while implementing;
- run one `Fast` control at a meaningful checkpoint;
- run one `PreMerge` control immediately before merge;
- do not repeat Fast immediately before PreMerge solely as a ritual.

The historical evidence table remains unchanged. Its two accepted Fast runs
prove the original lane performance requirement; they do not prescribe two
Fast runs for every future change.

Update the active #1505 plan and quickstart only where they currently state the
old two-Fast future workflow. Preserve their historical verification evidence
and task-specific record that #1505 itself used two final Fast controls.

## Verification

This change is documentation and agent-instruction only. Verification will:

- assert that root `AGENTS.md` names the bounded runner, ordinary rhythm,
  conditional heavy lanes, one final PreMerge control, and `docs/testing.md`;
- assert that the stale fast-project path for
  `ExampleDocumentationValidationTests` is gone;
- assert that current workflow text no longer requires two consecutive Fast
  controls before PreMerge;
- scan the edited documents for contradictory current-workflow wording;
- run `git diff --check`.

No C# build or test lane is required because no executable code, project file,
test source, or runner behavior changes.
