# Implementation Plan: Authoritative Saref Story Materialization

**Branch**: `1519-saref-story-materialization` | **Date**: 2026-08-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification in `specs/1519-saref-story-materialization/spec.md`

## Summary

Ship the hidden `Крылья над Бездной` line as immutable packaged authority that is available to the GM from the first turn, while materializing mutable Guardians, quests, Saref, and the Wings only when play requires them. A cached catalog service validates exactly ten Guardian templates and forty fixed quest templates, binds every current-schema New Game to one exact digest, and contributes a compact private index to every Mortal World, Chaos Sea, and Shining Abode prompt. Exact relevant references add only the matching full questline packages.

The implementation is split into three mergeable issue phases: #1520 establishes catalog and prompt competence; #1521 materializes the ten Guardians and implements the fixed/renewable quest lifecycle, including the currently missing typed player-acceptance path; #1522 materializes Saref and the Wings through the generic actor, faction, and location contracts. The fixed quest catalog remains immutable, `main_story_saref_state.json` is the sole mutable story-progress authority, and client-owned projectors publish Guardian and Actor Brain views atomically.

## Technical Context

**Language/Version**: C# 12 on .NET 8; TypeScript 6, React 19, and Vite 8 only where browser player-flow work is required

**Primary Dependencies**: `System.Text.Json`, Microsoft.Extensions hosting/logging, Spectre.Console, ASP.NET Core local host, existing Actor Materialization (#1500), Faction Materialization (#1510), and shared location materialization (#1514)

**Storage**: Versioned packaged JSON plus file-backed canonical JSON under `game_state/`, `input/`, and `output/`; no database or remote service

**Testing**: xUnit through PowerShell 7 and `scripts/test-csharp.ps1`; frontend `npm run verify` when browser code changes; documentation/source guards and manifest-backed example validation

**Target Platform**: Offline Windows console and loopback browser client

**Project Type**: Local C# game client with embedded/local React browser UI and GM prompt bridge

**Performance Goals**: Validate and parse packaged story content once per process; render the compact story index at no more than 32 KiB UTF-8; compose each turn in `O(10 + 40 + relevant references)` without reparsing content files; do not silently truncate relevant packages

**Constraints**: Exact case-sensitive identity; all-or-nothing content load and multi-root publication; no player-facing private catalog or receipt leakage; no cloud dependency, telemetry, GM worker, multiplayer, legacy fallback, or runtime migration

**Scale/Scope**: 10 Guardian materialization templates, 40 fixed quest templates, 10 full questline packages, 1 Saref actor template, 1 Wings faction template, 3 realms, 3 issue/merge phases, and synchronized runtime/docs/examples/source guards

**Source Issue(s)**: [#1519](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1519), [#1520](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1520), [#1521](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1521), [#1522](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1522)

**Contract Scope**: GM-private prompt context; New Game and turn preparation; Saref story state; Guardian, actor-profile, actor-memory, quest, and Shining faction canonical authority; console/browser quest acceptance and reveal projections; validation, normalization, repair packets; Mortal and afterlife rules, examples, manifests, daemon entrypoints, and source guards

**Verification Commands**:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SarefStoryCatalogTests|FullyQualifiedName~SarefStoryContextTests|FullyQualifiedName~SarefMainStoryStateValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GuardianStoryMaterializationTests|FullyQualifiedName~GuardianQuestAcceptanceTests|FullyQualifiedName~ActorMaterializationContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane DeepValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Run the smallest relevant Focused selection during each child, one Fast checkpoint per child, conditional FullValidation whenever afterlife prompts/docs/examples/manifests change, and one final PreMerge for that independently merged child. Because the planned work changes New Game, accepted-turn, and pending/control lifecycle boundaries, run LifecycleIntegration for #1520, #1521, and #1522. Run DeepValidation for #1521 because it changes shared Guardian validation/normalization and the exhaustive Guardian matrix. Run `npm run verify` from `BookOfEternityClient.WebFrontend/` when #1521 or #1522 changes browser code.

## Constitution Check

*GATE: passed before Phase 0 research and re-checked after Phase 1 design.*

- **GitHub traceability — PASS**: the epic and all three implementation sub-issues are linked in the spec, plan, contracts, quickstart, and future tasks; issue phases are #1520 → #1521 → #1522.
- **Spec Kit fit — PASS**: this is a multi-session hidden-story epic spanning canonical state, validation, normalizers, console/browser behavior, actor/faction/location authority, prompts, and afterlife documentation.
- **Player-facing integrity — PASS**: raw catalog terms, receipts, private truth, and internal status enums remain GM-private. Console and browser use the same acceptance/reveal semantics and Russian in-world labels.
- **Contract/state authority — PASS**: immutable definitions, mutable story progress, derived quest views, ordinary non-story quest authority, actor memory, faction memory, prompt context, and repair ownership are assigned explicitly in [data-model.md](./data-model.md) and `contracts/`.
- **Test-first path — PASS**: every issue phase begins with failing catalog/state/contract/integration tests; fixture conversion is current-schema work, not a compatibility layer.
- **Verification evidence — PASS**: Focused, Fast, conditional FullValidation, frontend verify where touched, and PreMerge controls are specified above and in [quickstart.md](./quickstart.md).
- **Agent orchestration — PASS**: any delegated implementation packet must include #1519 and the active child issue, this complete Spec Kit directory, Superpowers TDD/review/verification requirements, and the bounded commands above.
- **Local/offline constraint — PASS**: the design adds only packaged files, local JSON, local logging, and loopback UI behavior; no network, telemetry, or remote catalog is introduced.

No constitution violation requires an exception.

## Project Structure

### Documentation (this feature)

```text
specs/1519-saref-story-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── saref-story-catalog.md
│   ├── saref-guardian-materialization.md
│   ├── saref-story-quest-lifecycle.md
│   ├── saref-and-wings-materialization.md
│   └── saref-story-repair-and-visibility.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── story_content/saref/
│   ├── catalog.json
│   ├── saref_actor_materialization.json
│   └── wings_faction_materialization.json
├── system_guardians/built_in/<preset>/
│   ├── manifest.json
│   ├── dossier.md
│   ├── guardian_materialization.json
│   └── saref_questline.json
├── Services/
│   ├── SarefMainStoryState.cs
│   ├── SarefStoryCatalogService.cs                 # new
│   ├── SarefStoryContextComposer.cs                # new
│   ├── SarefStoryProjectionService.cs              # new
│   ├── GuardianQuestAcceptanceRequestState.cs      # new
│   ├── AfterlifeContractRegistry.cs
│   ├── SystemGuardianLibraryService.cs
│   ├── CanonicalStateNormalizer/
│   └── Validation/
├── Core/GameEngine/
├── CommandProtocol/
├── UI/
├── WebUi/
└── game_master_daemon.ps1

BookOfEternityClient.Tests/
BookOfEternityClient.IntegrationTests/
BookOfEternityClient.WebFrontend/
Rules/
TaskGuides/
OtherGuides/
├── Saref_Character_Bible.md
├── Saref_Guardian_Questlines/
├── Afterlife_Pending_Control_Surface_Inventory.json
└── Afterlife_Contract_Matrix.md
Examples/
├── E_CLI_Afterlife_Turns.txt
└── example_validation_manifest.json
CLI_API_Specification.md
CLI_Agent_Daemon_Specification.md
```

**Structure Decision**: Keep immutable story definitions beside the packaged system Guardian assets that own them, with the cross-story catalog and Saref/Wings templates under `story_content/saref/`. The existing `system_guardians/built_in/**` copy rule automatically ships Guardian additions; `BookOfEternityClient.csproj` must add an explicit copy rule for `story_content/saref/**`. Runtime behavior remains in the existing C# client and state/validation/normalizer seams. No new project, database, service, or duplicate location/faction model is introduced.

## Architecture and Delivery Sequence

### Phase A — #1520: catalog, binding, and always-on GM competence

1. Add the packaged-only catalog loader, deterministic digest verification, typed models, and one-process cache.
2. Structure the ten Guardian/four-quest indexes and full packages from the existing bibles without yet publishing mutable story actors.
3. Create current-schema `main_story_saref_state.json` with exact binding during every New Game.
4. Append the compact index unconditionally to Mortal World, Chaos Sea, and Shining Abode turn reminders; attach deduplicated full packages by exact relevance.
5. Reject corrupt content, missing current-schema state, binding mismatch, case variants, and unknown references without migration or fallback.
6. Update catalog/binding prompt docs, examples, manifests, and source guards; merge #1520 independently.

### Phase B — #1521: ten Guardians and all Guardian quest lifecycles

1. Add complete authored materialization templates for the ten exact built-in Guardians and integrate them into selection, attraction, and story-appearance routes.
2. Publish the Guardian, common afterlife profile, immutable actor receipt, deterministic initial thought-journal entry, exact location reference, and story binding atomically.
3. Upgrade Saref story progress to the current closed catalog schema and project fixed quest views from that sole authority.
4. Add `UpdateGuardians.offerQuest` for complete GM-authored `non_story` offers, replacing unbounded direct quest-list authoring for new offers.
5. Add the afterlife player command `/guardian_quest_accept` (`/принять_квест_хранителя`), the client-owned `pending_guardian_quest_acceptances.json` request, console/browser parity, and exact accepted/rejected resolution.
6. Register the pending path in `AfterlifeContractRegistry`, the pending-control inventory, validated snapshots/client-owned filters, Soul Gates blockers, accepted-resolution cleanup, daemon routing, and registry/documentation guards.
7. Route story acceptance through `advance_guardian_quest`; route non-story acceptance through pending-backed `UpdateGuardians.acceptQuest`. Keep Mortal progress and afterlife hand-in separated by `storyScope`, and render `non_story` to players as `Несюжетный квест` rather than the raw enum.
8. Preserve continued Guardian agency before and indefinitely after q4; prove the fixed lifecycle for all forty quests and a post-q4 non-story lifecycle for each of the ten Guardians; merge #1521 independently.

### Phase C — #1522: Saref and Wings exact entities

1. Extend common afterlife profiles with exact `actorType=saref` while keeping `saref_agent` distinct.
2. Materialize `saref_001` from its packaged template with complete profile, receipt, private/public authority, goals, abilities, relationships, and `gmThoughtsSummary` memory.
3. Materialize `shine_faction_wings_of_angels_001` through #1510's `story` faction route with the exact #1514 hall reference and complete faction receipt/memory.
4. Enforce exact cross-links and hidden/revealed projections; replace inconsistent Wings values only in identity-bearing `factionId`/`wingsFactionId` fields while preserving `factionRole=wings_of_angels` and player command aliases.
5. Add bounded repair/rollback, player privacy, console/browser parity, examples, manifests, and source guards; merge #1522 independently.

## Dependency Gates

- #1520 depends on the accepted #1519 spec and authored bibles. Its final Wings template shape must be rebased on the merged #1510 faction contract.
- #1521 depends on #1520 and the merged #1500 Actor Materialization contract. Exact Guardian abode/plane references depend on #1514; do not create a temporary private location schema.
- #1522 depends on #1520, #1521, merged #1510, and merged #1514.
- #1239 GM workers remains blocked until this epic and the other planned materialization families are complete.
- Multiplayer/network work and legacy-save compatibility remain closed scope for all phases.

## Phase 0 Research

The decisions, alternatives, existing gaps, and evidence are recorded in [research.md](./research.md). Important discoveries include the absence of a typed available-to-active Guardian quest path, the direct-write nature of new ordinary quest offers, the generic built-in Guardian bootstrap, and the existing case-insensitive/open Saref progress schema.

## Phase 1 Design Outputs

- Canonical entities, ownership, transitions, invariants, and projection rules: [data-model.md](./data-model.md)
- Catalog and prompt contract: [contracts/saref-story-catalog.md](./contracts/saref-story-catalog.md)
- Guardian atomic materialization contract: [contracts/saref-guardian-materialization.md](./contracts/saref-guardian-materialization.md)
- Story/non-story quest lifecycle and acceptance contract: [contracts/saref-story-quest-lifecycle.md](./contracts/saref-story-quest-lifecycle.md)
- Saref/Wings actor/faction contract: [contracts/saref-and-wings-materialization.md](./contracts/saref-and-wings-materialization.md)
- Repair, rollback, and privacy contract: [contracts/saref-story-repair-and-visibility.md](./contracts/saref-story-repair-and-visibility.md)
- Per-child execution and verification handoff: [quickstart.md](./quickstart.md)

## Post-Design Constitution Re-check

The Phase 1 design preserves one owner per mutable fact, makes all GM-authored surfaces typed and documented, adds console/browser parity for the newly discovered player action, treats packaged/private text separately from player rendering, and contains repair to exact targets. The three-child sequence remains independently reviewable, test-first, local-only, and fully traceable to GitHub. Gate remains **PASS**.
