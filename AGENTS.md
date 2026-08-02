# Repository Agent Instructions

## Task tracking guardrail

Do not implement project changes without a tracked task.

Before editing code, tests, prompts, documentation, examples, or game contracts, first ensure there is an explicit task for the work. If the user asks to implement something and no task exists, create or request a task record before making repository changes. Small exploratory reads, reviews, and planning may happen without a task, but implementation work must be tied to a task.

## C# test execution policy

Use PowerShell 7 and `.\scripts\test-csharp.ps1` as the normal bounded entry
point for C# verification. Read `docs/testing.md` for lane selection, limits,
result artifacts, and failure diagnosis.

- During implementation, run the smallest relevant `Focused` selection, then
  one `Fast` control at a meaningful checkpoint.
- Immediately before merge, run one `PreMerge` control. Do not add duplicate
  Fast runs immediately before it because PreMerge already includes the full
  fast project.
- Run `DeepValidation`, `LifecycleIntegration`, or another diagnostic lane
  only for a related boundary change, failure diagnosis, or an explicitly
  requested exhaustive control.
- Do not use an unbounded full-solution or full-suite `dotnet test` command as
  an ordinary verification step.

## Spec Kit and Hermes/Codex orchestration guardrail

GitHub Issues remain the task tracker for lifecycle, comments, triage, and closure.
Spec Kit is the durable specification layer for large or contract-sensitive work,
not a replacement for issues and not required for every bug fix.

Before implementing a GitHub issue, decide whether it needs a Spec Kit feature.
Create or update `specs/NNN-feature/` when the issue is an epic, spans multiple
files or sessions, changes player-facing UX, changes console/browser parity,
changes validation/normalizer/canonical state, changes GM-facing docs/examples,
or touches afterlife/Chaos Sea/Shining Abode/Saref pending/control contracts.

Do not create a Spec Kit feature for tiny one-file fixes, quick inspections,
minor copy edits, or simple local refactors unless the user explicitly asks for
Spec Kit.

When Spec Kit is used:
- Link the source GitHub issue(s) in `spec.md`, `plan.md`, and `tasks.md`.
- Treat `.specify/memory/constitution.md` as project governance.
- Use `$speckit-specify`, `$speckit-clarify`, `$speckit-plan`,
  `$speckit-tasks`, and `$speckit-analyze` to keep artifacts aligned.
- Use Superpowers as the execution method: brainstorming, TDD, systematic
  debugging, review, and verification.
- If Hermes delegates to Codex, Hermes should load `spec-kit-superpowers-bridge`
  before `codex-delegate` and pass the active constitution/spec/plan/tasks and
  verification commands into the Codex prompt.
- Do not mark Spec Kit tasks complete or close GitHub issues from an agent report
  alone; inspect diffs and run or confirm verification evidence first.

## GM prompt, documentation, and example synchronization guardrail

The GM does not read client implementation code during normal play. If code adds,
removes, or changes a game capability, command, mechanic, state field,
validation rule, normalizer side effect, lifecycle flow, pending/control surface,
response field, receipt, report, or GM-authored output contract, update the
relevant GM-facing prompts, documentation, examples, manifests, and
documentation/source-guard tests in the same change.

This applies to both Mortal World and afterlife content. Do not leave a
code-only capability unless it is intentionally client-owned, not GM-authored,
and the tracked issue/spec explicitly says no GM prompt or example update is
required.

Every GM-affecting feature must include at least one worked GM example or update
an existing example that proves the GM can author the new or changed behavior.
If no suitable example file exists, add one or create a tracked follow-up issue
before completion.

Before finishing any gameplay or GM-authored contract change, explicitly check
whether Mortal World and afterlife prompts, docs, examples, manifests, and source
guards need updates; record either the prompt/docs/example updates or the no-update rationale in the final report or PR summary.

## GM harness engineering guardrail

When the GM or another game-running agent makes a technical mistake, prefer
harness engineering before prompt-only fixes. First ask whether the class of
error can be prevented, constrained, detected, repaired, rolled back, or made
unrepresentable by validators, normalizers, canonical state contracts,
pending-turn snapshots, rollback reports, repair loops, generated fixtures,
command protocols, runtime tools, or daemon/bridge controls.

Prompts still matter: after the harness/tooling change is designed or made,
update the relevant GM-facing prompts, docs, and examples so the GM understands
the available tools, constraints, and expected workflow. A prompt-only fix is
acceptable only when the tracked task or final report explicitly explains why a
harness/tooling change is not appropriate for that problem.

Apply the same rule during live GM tests. If the GM agent repeatedly cannot
complete a turn, gets stuck in repair loops, misunderstands a contract, or needs
manual reasoning to undo file damage, treat that friction as harness feedback.
Before blaming the prompt or the model, ask whether the client, daemon, bridge,
validator, repair request, snapshot, rollback mechanism, agent-console surface,
fixture, or command tooling can take that work over or make the bad action
impossible. Live-test notes should record not only player-facing bugs, but also
where the GM needed a clearer tool, narrower task packet, safer default, or
automatic repair/rollback.

## Afterlife contract documentation guardrail

If you change any `Chaos Sea` / `Shining Abode` runtime contract, update the GM-facing documentation in the same change.

This applies when adding, renaming, removing, or changing any afterlife:
- pending/control file in `game_state/control/`
- `pending_shining_abode_actions.json` `actionType`
- response field, receipt, report, or canonical state surface
- validation rule, scheduler contour, lifecycle mode, normalizer side effect, or authority path
- player-visible command behavior that the GM must resolve through prompts

Before finishing that change, check whether these files also need updates:
- `OtherGuides/Afterlife_Contract_Matrix.md`
- `Examples/E_CLI_Afterlife_Turns.txt`
- `Examples/example_validation_manifest.json`
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- daemon/launcher prompt entrypoints if the GM must be forced to read new guidance

If an explicit afterlife contract registry is added later, update it together with the matrix, examples, and manifest. Do not leave a code-only afterlife contract unless it is intentionally client-owned and documented as not GM-authored.

Minimum verification for documentation-sensitive afterlife changes:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
```

Run `FullValidation` here only when the documentation/examples boundary is
affected; it remains a conditional diagnostic lane.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/1510-complete-faction-materialization/plan.md
<!-- SPECKIT END -->
