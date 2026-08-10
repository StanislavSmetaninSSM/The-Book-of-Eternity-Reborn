<!--
Sync Impact Report
Version change: 1.1.0 -> 1.2.0
Source task: GitHub issue #1510 and direct user instruction on 2026-08-10
Modified principles:
- None
Added sections:
- Pre-Release Save Compatibility Policy
Removed sections:
- None
Templates requiring updates:
- .specify/templates/plan-template.md: updated
- .specify/templates/spec-template.md: updated
- .specify/templates/tasks-template.md: updated
- AGENTS.md: updated
Follow-up TODOs:
- Reconcile the active #1510 feature artifacts that still require legacy-save
  compatibility before implementation resumes.
-->

# The Book of Eternity Reborn Constitution

## Core Principles

### I. GitHub Issue Traceability

Every implementation change MUST be tied to a tracked GitHub issue before code,
tests, prompts, documentation, examples, game contracts, or player-facing copy
are edited. A direct user request can authorize exploration, review, and setup
planning, but repository implementation requires an explicit issue or a newly
created issue.

Spec Kit artifacts MUST reference their source GitHub issues in `spec.md`,
`plan.md`, and `tasks.md`. GitHub Issues remain the lifecycle tracker for
triage, assignment, comments, closure, and follow-up issues. Spec Kit owns
durable requirements, planning, task decomposition, and handoff context for
large or contract-sensitive work.

### II. Player-Facing Game Client Integrity

The console client and browser client are player-facing game clients, not debug
terminals. Player-facing surfaces MUST use in-world Russian terminology and
MUST NOT expose API, DTO, endpoint, validation, or agent meta-language unless an
explicit advanced/debug mode is active.

Console and browser workflows MUST preserve semantic parity when they expose the
same action, state, command, or choice. They may differ visually, but the player
must receive equivalent affordances, blocking states, error explanations, and
result visibility.

### III. Contract and State Authority

Player-visible summaries that imply inspectable or mechanical entities MUST
resolve to canonical detail authority, a stable reference, or an explicit
player-facing unresolved/unreadable reason. This applies to status effects,
inventory documents, mechanical item bonuses, quest rewards, skills, map and
transport references, afterlife contracts, and comparable game-state surfaces.

GM-facing prompts, guidance, contracts, and worked examples MUST stay
synchronized with code. Any code change that adds, removes, or changes a game
capability, command, mechanic, state field, validation rule, normalizer side
effect, lifecycle flow, pending/control surface, response field, receipt, report,
or GM-authored output contract MUST update the relevant GM-facing prompts,
documentation, examples, manifests, and documentation/source-guard tests in the
same change.

This rule applies to both Mortal World and afterlife content, including Chaos
Sea, Shining Abode, Saref, hidden-story arcs, browser/console player actions,
and any GM-authored state the player later inspects. Do not leave a code-only
capability unless the feature is intentionally client-owned, not GM-authored,
and the spec/issue explicitly says no GM prompt or example update is required.

Every GM-affecting feature MUST include at least one worked GM example or update
an existing example that proves the GM can author the new or changed behavior.
If no example file currently exists for the surface, the change MUST either add
one or create a tracked follow-up issue before completion.

Dynamic game-state, GM-authored, and user-authored text MUST be treated as
untrusted text before entering Spectre.Console markup APIs or browser-rendered
HTML. Escaping, sanitization, and source-guard coverage are required for unsafe
rendering surfaces.

### IV. Test-First Verification

Behavior changes and bug fixes MUST use test-first discipline unless the work is
pure scaffolding, generated assets, or explicitly documented as exploratory.
For defects, the failing behavior MUST be captured by a regression test before
the fix. For features, independently testable user stories MUST include focused
tests or a documented reason why automated coverage is not feasible.

Agents MUST run fresh verification before claiming completion. Verification
must cover the touched layer: C# unit/integration tests, documentation coverage
tests, frontend typecheck/player-facing tests/build, browser visual checks, or
targeted source guards as appropriate. A final report MUST name the commands
run, their outcomes, changed files, and residual risks.

### V. Agent Orchestration Discipline

Superpowers is the execution method layer: brainstorming, TDD, systematic
debugging, code review, and verification discipline. Spec Kit is the durable
requirements and planning layer. Codex is the repository implementation worker.
Hermes is the conversation, orchestration, delegation, and final verification
surface.

Hermes MUST use `spec-kit-superpowers-bridge` before `codex-delegate` when a
task mentions Spec Kit, SDD, `.specify`, `specs/NNN-*`, or when the target issue
meets the Spec Kit Usage Policy below. Codex output is an implementation report,
not proof. Hermes or the active front agent MUST inspect diffs, run or confirm
verification, and reconcile results against GitHub issues and Spec Kit artifacts
before reporting success.

## Project Constraints

The project is a local game client stack built primarily with C#/.NET 8,
Spectre.Console, file-backed JSON game state, a local browser UI, React, Vite,
TypeScript, and local/loopback runtime services. Features MUST preserve local
play and must not introduce cloud dependencies, telemetry, or remote services
unless a tracked issue and accepted spec explicitly require them.

Core source areas include:

- `BookOfEternityClient/` for the C# game client, runtime services, command
  protocol, local web host, and game state handling.
- `BookOfEternityClient.Tests/` for C# test coverage and documentation/source
  guards.
- `BookOfEternityClient.WebFrontend/` for the browser client.
- `BookOfEternityGMBridge/` for GM bridge integration.
- `Rules/`, `TaskGuides/`, `OtherGuides/`, `Examples/`, and `docs/` for
  player, GM, rules, examples, audits, and implementation documentation.

Work MUST respect existing repository instructions in `AGENTS.md`, local
Superpowers plans/specs, GitHub issue acceptance criteria, and established code
patterns. Do not revert unrelated dirty worktree changes.

## Pre-Release Save Compatibility Policy

The game has not had a public release. Until a first public release baseline is
explicitly declared by a tracked issue and accepted specification, backward
compatibility with earlier save files, canonical JSON schemas, development
snapshots, or obsolete test fixtures is NOT a project requirement.

When a current contract or state schema changes, implementation MUST migrate
the repository's active bootstrap state, templates, examples, and tests to the
new authority. Agents MUST prefer removing legacy fallbacks and compatibility
branches over adding complexity for hypothetical old saves. An old test or
fixture proves only historical development behavior; it MUST NOT establish a
compatibility requirement by itself.

Any exception MUST be explicit in a tracked issue and Spec Kit artifact and
MUST define the concrete save population, migration/reader behavior,
verification, and removal or support horizon. This policy does not weaken
same-turn atomicity, current canonical-state integrity, immutable accepted
receipts, or preservation rules inside the current supported schema.

## Spec Kit Usage Policy

Create or update a Spec Kit feature directory when a GitHub issue or user task
has one or more of these traits:

- Epic or roadmap work.
- Multi-file implementation across runtime, tests, prompts, docs, examples,
  frontend, or GM-facing contracts.
- Console/browser parity or player-facing UX redesign.
- Validation, normalizer, canonical-state, or summary/detail authority changes.
- Afterlife, Chaos Sea, Shining Abode, Saref, hidden-story, or pending/control
  lifecycle changes.
- Work expected to span multiple sessions, agents, branches, or pull requests.
- Work where acceptance criteria need decomposition into durable tasks.

Do not create a Spec Kit feature for tiny one-file bug fixes, simple inspection,
minor copy edits, dependency-free cleanup, or pure Q&A unless the user explicitly
requests Spec Kit.

Each Spec Kit feature MUST include:

- Source GitHub issue numbers and URLs.
- User stories or scenarios that are independently testable.
- Contract scope: player-facing, GM-facing prompts, runtime-state, validation,
  docs, examples, frontend, console, browser, or none.
- Acceptance criteria copied or normalized from the issue.
- Out-of-scope boundaries and follow-up issue policy.
- Verification commands appropriate to the touched surfaces.

Backlog migration MUST be incremental. Do not convert all open GitHub issues to
Spec Kit specs in one pass. Start with active epics or cross-contract issues.

## Development Workflow and Quality Gates

Before implementation:

1. Confirm the target repository path and run `git status --short`.
2. Confirm or create the source GitHub issue.
3. Decide whether the issue needs Spec Kit using the Spec Kit Usage Policy.
4. If Spec Kit is needed, create or update `specs/NNN-feature/` before coding.
5. Read `AGENTS.md`, the active issue, relevant Spec Kit artifacts, existing
   Superpowers docs, and nearby code/tests before editing.

During implementation:

1. Use TDD for behavior changes and regression fixes.
2. Keep edits scoped to the issue/spec.
3. Update code, tests, GM-facing prompts, docs, examples, manifests, and
   contracts together when capabilities or contracts change.
4. Maintain console/browser parity where the feature crosses both clients.
5. Mark Spec Kit tasks complete only after code and verification evidence exist.

Minimum verification examples:

- Documentation-sensitive GM/contract changes:
  `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- C# runtime changes:
  `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "<focused-filter>"`
- Broad C# changes:
  `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`
- Browser client changes:
  run `npm run verify` from `BookOfEternityClient.WebFrontend/`.
- Browser visual or interaction changes:
  run the relevant local app/browser verification and capture evidence when the
  UI surface can regress visually.

## Governance

This constitution is the highest project-local Spec Kit governance document. It
does not override newer direct user instructions, repository safety constraints,
or GitHub issue acceptance criteria, but it governs how agents convert those
inputs into specs, plans, tasks, implementation, and verification.

Amendments require a tracked GitHub issue, an update to this file, and a Sync
Impact Report describing changed principles, templates, and follow-up work.
Versioning follows semantic versioning:

- MAJOR for incompatible governance changes or removed principles.
- MINOR for new principles, new mandatory gates, or materially expanded scope.
- PATCH for clarifications that do not change obligations.

Agents MUST review constitution compliance before implementing, before
delegating to Codex, and before reporting completion. If a task conflicts with
this constitution, the agent must report the conflict and either update the
Spec Kit artifacts through the proper phase or ask the user for direction.

**Version**: 1.2.0 | **Ratified**: 2026-06-05 | **Last Amended**: 2026-08-10
