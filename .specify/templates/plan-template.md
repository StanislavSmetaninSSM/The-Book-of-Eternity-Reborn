# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]

**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]

**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

**Source Issue(s)**: [GitHub issue numbers and URLs]

**Contract Scope**: [player-facing / GM-facing prompts / runtime-state / validation / docs / examples / console / browser / frontend / none]

**Verification Commands**: [focused commands required before completion]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **GitHub traceability**: Source issue(s) are linked and this plan maps each accepted scope item to the issue/spec.
- **Spec Kit fit**: The issue is large, cross-contract, player-facing, validation/runtime-wide, epic, or multi-session; otherwise justify why Spec Kit is being used.
- **Player-facing integrity**: If console/browser/player UI is touched, Russian in-world copy, no debug/API leakage, and parity expectations are defined.
- **Contract/state authority**: If summaries, mechanics, validation, pending/control files, GM prompts/docs, or examples are touched, canonical authority and prompts/docs/examples/tests updates are planned for Mortal World and afterlife surfaces.
- **Test-first path**: Regression or feature tests are identified before implementation tasks.
- **Verification evidence**: Focused `dotnet test`, docs coverage, frontend verification, and/or browser visual checks are listed.
- **Agent orchestration**: Hermes/Codex delegation packets must include source issues, active Spec Kit artifacts, Superpowers method requirements, and verification commands.
- **Pre-release save policy**: Backward compatibility is not assumed before the first public release. The plan migrates active bootstrap state/templates/examples/tests and removes obsolete fallbacks, or links an explicit issue/spec exception with a concrete migration and support horizon.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Keep only the paths touched by this feature and expand them
  with real files/modules. Do not include unrelated directories in the delivered
  plan.
-->

```text
BookOfEternityClient/                 # C# game client, runtime, services, local web host
BookOfEternityClient.Tests/           # C# tests, docs coverage, source guards
BookOfEternityClient.WebFrontend/     # React/Vite browser client
BookOfEternityGMBridge/               # GM bridge integration
Rules/                                # rules blocks
TaskGuides/                           # task guidance
OtherGuides/                          # GM/player guidance and contracts
Examples/                             # worked examples and validation manifest
docs/                                 # audits, Superpowers docs, project docs
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
