# Tasks: Authoritative Saref Story Materialization

**Input**: Design documents from `specs/1519-saref-story-materialization/`

**Source issues**: Epic [#1519](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1519); implementation children [#1520](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1520), [#1521](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1521), and [#1522](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1522)

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, merged #1500, and the dependency gates listed below

**Tests**: Every behavior phase is test-first. Write the listed RED tests, demonstrate their intended failure, then implement. C# verification must use PowerShell 7 and `scripts/test-csharp.ps1` according to `docs/testing.md`.

**Delivery rule**: Complete and merge #1520 before starting #1521. Complete and merge #1521 before starting #1522. #1522 also waits for #1510 and #1514. Do not mark tasks complete from an agent report alone; inspect diffs and fresh verification evidence.

## Format: `[ID] [P?] [Story?] Description`

- **[P]** means the task can proceed in parallel after its phase prerequisites because it owns different files and does not depend on an incomplete sibling task.
- **[US1]–[US4]** map directly to the four user stories in `spec.md`.
- Every task names the concrete repository path it changes or validates.

## Phase 1: Setup and dependency gates

**Purpose**: Confirm traceability, isolation, and upstream contracts before implementation.

- [ ] T001 Confirm the child branch/worktree, clean scoped baseline, and source issue mapping against `specs/1519-saref-story-materialization/plan.md` before editing #1520
- [ ] T002 Confirm merged #1500 and record the exact #1510/#1514 dependency commits or blockers in `specs/1519-saref-story-materialization/quickstart.md`
- [ ] T003 Inventory the ten source bibles and every inconsistent current Wings/Guardian quest ID using `OtherGuides/Saref_Guardian_Questlines/`, `OtherGuides/Saref_Character_Bible.md`, and `Examples/` without changing runtime state

---

## Phase 2: Foundational test infrastructure

**Purpose**: Give all three child issues strict current-schema fixtures without adding product compatibility paths.

**⚠️ CRITICAL**: Complete this phase before the first behavioral RED tests.

- [ ] T004 Add a deterministic packaged-catalog fixture builder with corrupt/missing/duplicate/case-variant options in `BookOfEternityClient.TestSupport/SarefStoryCatalogFixtureBuilder.cs`
- [ ] T005 [P] Add schema-2 story-state, trace, transition, and exact binding fixture builders in `BookOfEternityClient.TestSupport/SarefStoryStateFixtureBuilder.cs`
- [ ] T006 [P] Add Guardian quest/materialization/pending-acceptance fixture builders with mandatory `storyScope` and `title` in `BookOfEternityClient.TestSupport/SarefGuardianFixtureBuilder.cs`

**Checkpoint**: Test infrastructure can express every valid and invalid current-schema case without a legacy flag.

---

## Phase 3: User Story 1 — GM knows the complete hidden story from the first turn (Priority: P1, Issue #1520) 🎯 MVP

**Goal**: Ship one immutable exact catalog, bind every New Game to it, and give every realm a compact private 10-Guardian/40-quest index plus exact relevant full packages.

**Independent Test**: Start a schema-2 game with no materialized story Guardian in each realm. Verify the GM prompt contains the complete compact index, one exact reference loads only its full line, invalid catalog/binding variants fail closed, and ordinary player output reveals none of the private payload.

### Tests for User Story 1 — write and observe RED first

- [ ] T007 [P] [US1] Add exact 10/40 inventory, ordinal identity, q4 reward, content-file inventory, semantic digest, and 32 KiB boundary tests in `BookOfEternityClient.Tests/SarefStoryCatalogTests.cs`
- [ ] T008 [P] [US1] Add all-realm compact-context, exact relevance, multiple-line deduplication, and no-silent-truncation tests in `BookOfEternityClient.Tests/SarefStoryContextTests.cs`
- [ ] T009 [P] [US1] Add fresh freeform/built-in New Game catalog-binding tests in `BookOfEternityClient.IntegrationTests/SarefStoryNewGameTests.cs`
- [ ] T010 [P] [US1] Add schema-1, missing-state, missing-binding, binding-mismatch, unknown-ID, and case-variant rejection tests in `BookOfEternityClient.IntegrationTests/SarefMainStoryStateValidationTests.Catalog.cs`
- [ ] T011 [P] [US1] Add player-facing narrative/interface/console/browser scans for catalog IDs, digest, receipts, and unrevealed template prose in `BookOfEternityClient.IntegrationTests/SarefStoryPrivacyTests.cs`

### Immutable content and runtime for User Story 1

- [ ] T012 [P] [US1] Author the exact catalog inventory plus complete Saref and Wings immutable templates in `BookOfEternityClient/story_content/saref/catalog.json`, `BookOfEternityClient/story_content/saref/saref_actor_materialization.json`, and `BookOfEternityClient/story_content/saref/wings_faction_materialization.json`
- [ ] T013 [P] [US1] Structure complete Guardian materialization and four-quest JSON for Azalia, Brann, Elyara, Ilarion, and Lissara under `BookOfEternityClient/system_guardians/built_in/<preset>/guardian_materialization.json` and `saref_questline.json`
- [ ] T014 [P] [US1] Structure complete Guardian materialization and four-quest JSON for Lucian, Myriel, Seret, Varak, and Veyra under `BookOfEternityClient/system_guardians/built_in/<preset>/guardian_materialization.json` and `saref_questline.json`
- [ ] T015 [US1] Ship `story_content/saref/**` beside existing built-in Guardian content by updating `BookOfEternityClient/BookOfEternityClient.csproj`
- [ ] T016 [US1] Implement typed immutable catalog/template models and ordinal identity indexes in `BookOfEternityClient/Services/SarefStoryCatalogModels.cs`
- [ ] T017 [US1] Implement canonical JSON hashing, complete validation, 32 KiB compact rendering, packaged-only resolution, and one-process caching in `BookOfEternityClient/Services/SarefStoryCatalogService.cs`
- [ ] T018 [US1] Create exact schema-2 empty state and catalog binding during every New Game path in `BookOfEternityClient/Services/SarefMainStoryState.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`
- [ ] T019 [US1] Enforce current schema, exact binding, ordinal catalog membership, all-realm authority, and no fallback/migration in `BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs`, `BookOfEternityClient/Services/AfterlifeContractRegistry.cs`, and `BookOfEternityClient.Tests/AfterlifeContractRegistryTests.cs`
- [ ] T020 [US1] Implement exact turn-relevance selection, progress summary, compact fragment, and deduplicated full-package composition in `BookOfEternityClient/Services/SarefStoryContextComposer.cs`
- [ ] T021 [US1] Append the story fragment unconditionally and separately from mutable campaign history in `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- [ ] T022 [US1] Add local-only catalog load/package-selection diagnostics and installation-safe failure text in `BookOfEternityClient/Services/SarefStoryCatalogService.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`

### GM documentation and verification for User Story 1

- [ ] T023 [P] [US1] Document catalog binding, latent-only compact behavior, full-package relevance, and no-migration rules in `TaskGuides/CLI_Step_Main.txt`, `Rules/Block_CLI_Operations.txt`, `BookOfEternityClient/game_master_daemon.ps1`, `CLI_API_Specification.md`, and `CLI_Agent_Daemon_Specification.md`
- [ ] T024 [P] [US1] Add the pre-Guardian latent-trace worked example, manifest entry, and exact source guards in `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`, and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T025 [US1] Run the #1520 Focused, Fast, documentation/FullValidation, required LifecycleIntegration, and PreMerge commands from `specs/1519-saref-story-materialization/quickstart.md`, inspect the diff, and post exact result directories to GitHub issue #1520 before merge

**Checkpoint**: #1520 is independently mergeable. A GM knows the whole hidden line from the first turn; no mutable story actor is required.

---

## Phase 4: User Story 2 — A story Guardian materializes as a complete actor (Priority: P1, Issue #1521 part 1)

**Goal**: Make every one of the ten registered built-in Guardians materialize from authored content as an atomic Guardian/profile/receipt/memory/location/story bundle.

**Independent Test**: From the same fresh baseline, materialize each exact Guardian through supported routes and verify complete authored identity, matching profile/receipt, one initial thought, exact location/story binding, idempotent retry, and full rollback on any invalid component. Repeat with a latent trace and verify the correct eligible projection appears.

### Tests for User Story 2 — write and observe RED first

- [ ] T026 [P] [US2] Add a ten-template completeness and exact identity contract matrix in `BookOfEternityClient.Tests/GuardianStoryMaterializationContractTests.cs`
- [ ] T027 [P] [US2] Add partial profile, wrong receipt/binding/location, wrong case, duplicate memory, retry, and semantic rollback tests in `BookOfEternityClient.IntegrationTests/GuardianStoryMaterializationAtomicityTests.cs`
- [ ] T028 [P] [US2] Add built-in selection, deterministic attraction, and catalog-backed story-appearance integration tests in `BookOfEternityClient.IntegrationTests/GuardianStoryMaterializationRouteTests.cs`
- [ ] T029 [P] [US2] Add pre-existing latent/recognized progress survival and exact post-materialization projection tests in `BookOfEternityClient.IntegrationTests/GuardianStoryMaterializationProjectionTests.cs`
- [ ] T030 [P] [US2] Add source guards tying all ten structured templates to their manifest/dossier/questline bibles and forbidding user override of story authority in `BookOfEternityClient.Tests/SarefGuardianContentSourceGuardTests.cs`

### Implementation for User Story 2

- [ ] T031 [US2] Resolve exact packaged story templates separately from user preset overrides in `BookOfEternityClient/Services/SystemGuardianLibraryService.cs`
- [ ] T032 [US2] Implement a staged all-or-nothing Guardian/profile/thought/location/story publication transaction in `BookOfEternityClient/Services/SarefGuardianMaterializationService.cs`
- [ ] T033 [US2] Populate the complete Guardian root and active-Guardian mirror from authored template plus documented dynamic fields in `BookOfEternityClient/Services/SarefGuardianMaterializationService.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs`
- [ ] T034 [US2] Populate the matching complete `actorType=guardian` profile and immutable Actor Materialization envelope in `BookOfEternityClient/Services/SarefGuardianMaterializationService.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeEntityProfiles.cs`
- [ ] T035 [US2] Append exactly one deterministic initial memory and preserve journal prefix semantics in `BookOfEternityClient/Services/SarefGuardianMaterializationService.cs` and `BookOfEternityClient/Services/GuardianThoughtJournalState.cs`
- [ ] T036 [US2] Validate and publish only exact #1514 abode/plane references without a story-private location object in `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs`
- [ ] T037 [US2] Implement the base catalog-to-Guardian/profile quest projector needed for pre-existing progress in `BookOfEternityClient/Services/SarefStoryProjectionService.cs`
- [ ] T038 [US2] Route built-in selection, attraction, and story appearance through the atomic service in `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- [ ] T039 [US2] Convert generic story-Guardian current-schema fixtures without compatibility branches in `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.ActorMemory.cs`, `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.AfterlifeLore.cs`, `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Afterlife.cs`, and `Examples/E_CLI_Afterlife_Turns.txt`

### GM documentation and verification for User Story 2

- [ ] T040 [P] [US2] Document the complete atomic bundle, actor-memory owner, exact location binding, and retry/repair rules with a Guardian materialization worked example in `Rules/Block_32_Guardians.txt`, `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, and `Examples/example_validation_manifest.json`
- [ ] T041 [US2] Run the #1521 Guardian-materialization Focused and Fast checkpoint commands from `specs/1519-saref-story-materialization/quickstart.md`, inspect all ten results, and retain evidence for the combined #1521 merge

**Checkpoint**: Every Predvechnye Guardian is a complete permanent actor; fixed quest progress still has one owner.

---

## Phase 5: User Story 3 — Complete four story quests without exhausting the Guardian (Priority: P1, Issue #1521 part 2)

**Goal**: Enforce one catalog-bound q1–q4 lifecycle, add the missing typed offer/accept path, and prove renewable `non_story` quests remain available before and indefinitely after q4 without touching Saref progress.

**Independent Test**: Complete one exact line through q4 and its playable memory scene, then offer, accept, progress, hand in, and complete a newly generated `non_story` quest. Verify console/browser acceptance parity, exact Guardian/profile projections, ordinary caps/difficulty, and complete semantic equality of Saref state across the non-story lifecycle.

### Tests for User Story 3 — write and observe RED first

- [ ] T042 [P] [US3] Add a parameterized all-forty-quest matrix for strict absent→latent→recognized→active→ready→completed transitions, rejected skipped stages, prior-ordinal gates, and the q4 composite exception in `BookOfEternityClient.IntegrationTests/SarefGuardianQuestLifecycleTests.cs`
- [ ] T043 [P] [US3] Add q4 active-to-memory completion, non-physical proof, exact revelation/advantage, and forged-reward rejection tests in `BookOfEternityClient.IntegrationTests/SarefGuardianQuestMemorySceneTests.cs`
- [ ] T044 [P] [US3] Add mandatory Guardian `storyScope`/`title`, catalog-membership/scope mismatch, ordinary-origin, cap, difficulty, historical-snapshot, and Russian `Несюжетный квест` projection tests in `BookOfEternityClient.Tests/GuardianQuestClassificationTests.cs`
- [ ] T045 [P] [US3] Add pending-backed `UpdateGuardians.offerQuest` tests for complete grounded `non_story` offers and unrelated-root protection in `BookOfEternityClient.IntegrationTests/GuardianNonStoryQuestOfferTests.cs`
- [ ] T046 [P] [US3] Add exact pending acceptance creation, semantic stale digest, duplicate request, accepted/rejected closure, client-owned registry/inventory/snapshot classification, Soul Gates blocking, and clear-after-validation tests in `BookOfEternityClient.Tests/GuardianQuestAcceptanceRequestStateTests.cs` and `BookOfEternityClient.Tests/AfterlifeContractRegistryTests.cs`
- [ ] T047 [P] [US3] Add console `/guardian_quest_accept` selection, Russian `Несюжетный квест` labels, pending status, and result-detail tests in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GuardianQuestAcceptance.cs`
- [ ] T048 [P] [US3] Add browser command menu/write/result parity and the same `Несюжетный квест` marker for `/принять_квест_хранителя` in `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.GuardianQuestAcceptance.cs`
- [ ] T049 [P] [US3] Add a ten-Guardian parameterized post-q4 generated quest lifecycle matrix that compares `main_story_saref_state.json` semantically before/after in `BookOfEternityClient.IntegrationTests/GuardianPostStoryQuestLifecycleTests.cs`

### Story lifecycle implementation for User Story 3

- [ ] T050 [US3] Implement schema-2 quest progress, append-only latent evidence, strict no-skip transitions, direct-encounter evidence only for `absent -> latent`, and typed `advance_guardian_quest` parsing in `BookOfEternityClient/Services/SarefMainStoryState.cs`
- [ ] T051 [US3] Enforce ordinal identity, legal edges/realms/evidence, prior-ordinal completion, full-package authority, pending acceptance, and exact q4 reward binding in `BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs`
- [ ] T052 [US3] Normalize accepted story transitions and atomically regenerate Guardian/profile projections from catalog plus story state in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs` and `BookOfEternityClient/Services/SarefStoryProjectionService.cs`
- [ ] T053 [US3] Add narrow `UpdateGuardians.offerQuest` authorization/normalization for complete grounded `non_story` offers in `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- [ ] T054 [US3] Implement client-owned request persistence, semantic snapshot digesting, resolution matching, cleanup, prompt fragment, registry/inventory ownership, validated snapshot/client-owned filters, and Soul Gates blocker in `BookOfEternityClient/Services/GuardianQuestAcceptanceRequestState.cs`, `BookOfEternityClient/Services/AfterlifeContractRegistry.cs`, `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`, `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`, and `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`
- [ ] T055 [US3] Add pending-backed `UpdateGuardians.acceptQuest` for exact non-story offers and dispatch story acceptance only to Saref progress in `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- [ ] T056 [US3] Require and preserve `storyScope`, `title`, origin, grounding, evidence, and reward audit across all quest arrays; reject story quests on ordinary progress/complete commands in `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs`

### Console/browser and documentation for User Story 3

- [ ] T057 [US3] Register the afterlife command and implement console selection/pending/result flow plus canonical `title` and `Несюжетный квест` rendering in `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`, `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs`, and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`
- [ ] T058 [US3] Implement equivalent browser command availability, prompts, pending writes, result DTOs, and `Несюжетный квест` marker in `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`, `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs`, and `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`
- [ ] T059 [US3] Adapt browser Guardian quest acceptance contracts/rendering in `BookOfEternityClient.WebFrontend/src/api/contracts.ts` and `BookOfEternityClient.WebFrontend/src/components/StatusView.tsx`, then run `npm run verify` or record in #1521 why no TypeScript change was needed
- [ ] T060 [P] [US3] Document story transitions, Guardian-only `storyScope`, `offerQuest`, registered pending acceptance, Mortal progress, afterlife hand-in, visible `Несюжетный квест`, and post-q4 agency in `Rules/Block_32_Guardians.txt`, `Rules/Block_CLI_Operations.txt`, `TaskGuides/CLI_Step_Main.txt`, `OtherGuides/Afterlife_Contract_Matrix.md`, `OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json`, `BookOfEternityClient/game_master_daemon.ps1`, `CLI_API_Specification.md`, and `CLI_Agent_Daemon_Specification.md`
- [ ] T061 [P] [US3] Add worked examples for first acceptance, q4 memory closure, and a complete post-q4 non-story quest plus manifest/source guards in `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`, and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T062 [US3] Run the complete #1521 Focused, frontend-conditional, Fast, documentation/FullValidation, required LifecycleIntegration, related DeepValidation Guardian matrix, and PreMerge controls from `specs/1519-saref-story-materialization/quickstart.md`, inspect the diff, and post exact evidence to GitHub issue #1521 before merge

**Checkpoint**: #1521 is independently mergeable. All ten fixed lines work, and completed Guardians remain living quest-givers through isolated non-story quests.

---

## Phase 6: User Story 4 — Saref and the Wings exist as exact hidden entities (Priority: P2, Issue #1522)

**Goal**: Materialize exact Saref and Wings entities through the generic actor/faction/location contracts and preserve hidden/revealed console-browser semantics.

**Independent Test**: Materialize complete hidden `saref:saref_001` and `shine_faction_wings_of_angels_001`, verify no private player leakage, execute a valid reveal, and verify public actor/faction actions with exact leadership/location/story links. Every old/forged/partial variant must roll back all targets.

### Tests for User Story 4 — write and observe RED first

- [ ] T063 [P] [US4] Add exact `actorType=saref` completeness, memory, receipt, and `saref_agent` separation tests in `BookOfEternityClient.Tests/SarefActorMaterializationTests.cs`
- [ ] T064 [P] [US4] Add exact Wings #1510 story-route, complete receipt, charter/lifecycle/memory/chronicle, and #1514 hall tests in `BookOfEternityClient.Tests/WingsFactionMaterializationTests.cs`
- [ ] T065 [P] [US4] Add actor/faction/leadership/location/story/q4 cross-link and case-variant rejection tests in `BookOfEternityClient.IntegrationTests/SarefWingsCrossLinkValidationTests.cs`
- [ ] T066 [P] [US4] Add hidden/partial/revealed console-browser parity and private-field leak scans in `BookOfEternityClient.IntegrationTests/SarefWingsVisibilityTests.cs`
- [ ] T067 [P] [US4] Add partial combined materialization, immutable receipt, exact repair target, and all-root rollback tests in `BookOfEternityClient.IntegrationTests/SarefWingsMaterializationAtomicityTests.cs`
- [ ] T068 [P] [US4] Add source guards rejecting old Wings values specifically in `factionId`/`wingsFactionId`, preserving `factionRole=wings_of_angels` and command aliases, and binding structured templates to Saref/questline bibles in `BookOfEternityClient.Tests/SarefWingsContentSourceGuardTests.cs`

### Actor/faction implementation for User Story 4

- [ ] T069 [US4] Add exact current-schema `saref` common-profile type and keep `saref_agent` disjoint in `BookOfEternityClient/Services/Validation/ValidationService.ActorMaterializationBinding.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeEntityProfiles.cs`
- [ ] T070 [US4] Materialize `saref:saref_001` from the packaged template with complete receipt, profile, agency, relationships, location, arts, private/public authority, and `gmThoughtsSummary` in `BookOfEternityClient/Services/SarefPrincipalMaterializationService.cs`
- [ ] T071 [US4] Materialize `shine_faction_wings_of_angels_001` through the merged #1510 Shining story route and exact #1514 hall reference in `BookOfEternityClient/Services/SarefPrincipalMaterializationService.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs`
- [ ] T072 [US4] Enforce exact story actor/faction links, leadership, visibility, provenance, hall, and q4 reward agreement in `BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs` and `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`
- [ ] T073 [US4] Stage combined Saref/Wings publication, immutable receipts, protected-root comparison, and bounded rollback/repair packets in `BookOfEternityClient/Services/SarefPrincipalMaterializationService.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T074 [US4] Route actor/faction/status/detail output through one reveal-filtered projection with safe Russian labels in `BookOfEternityClient/Services/SarefPlayerProjectionService.cs`, `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SarefStory.cs`, `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.SarefActions.cs`, `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs`, and `BookOfEternityClient/WebUi/BrowserSarefStoryWriteService.cs`
- [ ] T075 [US4] Replace old values only in identity-bearing `factionId`/`wingsFactionId` fields in `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.SarefMainStory.cs`, `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Afterlife.cs`, `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`, `BookOfEternityClient.IntegrationTests/SarefMainStoryStateValidationTests.cs`, and `Examples/E_CLI_Afterlife_Turns.txt`; preserve `factionRole=wings_of_angels` and player command aliases

### GM documentation and verification for User Story 4

- [ ] T076 [P] [US4] Document exact Saref/Wings materialization, leadership/location authority, hidden/revealed behavior, and repair boundaries in `TaskGuides/CLI_Step_Main.txt`, `Rules/Block_CLI_Operations.txt`, `OtherGuides/Afterlife_Contract_Matrix.md`, `BookOfEternityClient/game_master_daemon.ps1`, `CLI_API_Specification.md`, and `CLI_Agent_Daemon_Specification.md`
- [ ] T077 [P] [US4] Add hidden and revealed Saref/Wings worked examples, manifest entries, and documentation/source guards in `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`, and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T078 [US4] Run `npm run verify` from `BookOfEternityClient.WebFrontend/` if browser code changed or record the no-frontend-change rationale in GitHub issue #1522
- [ ] T079 [US4] Run the #1522 Focused, Fast, documentation/FullValidation, required LifecycleIntegration, and PreMerge controls from `specs/1519-saref-story-materialization/quickstart.md`, inspect the diff, and post exact evidence to GitHub issue #1522 before merge

**Checkpoint**: #1522 is independently mergeable. Saref and the Wings are complete exact entities, private until the canonical reveal allows public action.

---

## Phase 7: Epic-wide polish and closure

**Purpose**: Reconcile all three merged children and close #1519 only from repository and verification evidence.

- [ ] T080 [P] Re-run the complete structured-content/bible source guard and inspect every exact title/reward binding in `BookOfEternityClient.Tests/SarefGuardianContentSourceGuardTests.cs` and `OtherGuides/Saref_Guardian_Questlines/`
- [ ] T081 [P] Scan identity-bearing fields and current Guardian/story fixtures for old Wings IDs, missing Guardian `storyScope`, `name`-only Guardian quests, schema-1 state, case-insensitive catalog joins, and legacy/migration branches across `BookOfEternityClient/`, `BookOfEternityClient.Tests/`, `BookOfEternityClient.IntegrationTests/`, `OtherGuides/`, and `Examples/`, while explicitly preserving valid `factionRole=wings_of_angels` and command aliases
- [ ] T082 Execute the complete manual no-Guardian → trace → Guardian → q1–q4 → post-q4 non-story → hidden/revealed Saref/Wings walkthrough from `specs/1519-saref-story-materialization/quickstart.md`
- [ ] T083 Run focused `AfterlifeDocumentationCoverageTests` and the conditional FullValidation lane after the final docs/example reconciliation described in `docs/testing.md`
- [ ] T084 Run one final PreMerge control after the last epic-wide change, without an immediately duplicate Fast run, using `scripts/test-csharp.ps1`
- [ ] T085 Reconcile every implemented requirement and verification result against `specs/1519-saref-story-materialization/spec.md`, `plan.md`, `contracts/`, and this `tasks.md`; leave unchecked anything not proved
- [ ] T086 Inspect the final merged diff and exact evidence, then update/close #1520, #1521, #1522, and epic #1519 in GitHub only when their acceptance criteria are genuinely complete

---

## Dependencies and execution order

### Phase dependencies

```text
Setup
  -> Foundational test infrastructure
    -> US1 / #1520 catalog and GM competence
      -> merge #1520
        -> US2 / #1521 Guardian materialization
          -> US3 / #1521 quest lifecycle
            -> merge #1521
              -> US4 / #1522 Saref and Wings
                -> merge #1522
                  -> epic-wide closure
```

### External dependencies

- #1500 must remain merged before US2/US4 actor work.
- #1510 must be merged before final #1520 Wings template binding and before US4.
- #1514 must be merged before US2 exact Guardian location publication and US4 Wings hall publication.
- #1239 remains blocked until #1519 and the other planned materialization families are complete.

### User story dependencies

- **US1 (#1520)**: independent MVP after setup/foundation; it does not require a materialized actor.
- **US2 (#1521 part 1)**: depends on merged US1 and #1500/#1514; supplies complete actors and the base projector.
- **US3 (#1521 part 2)**: depends on US2 because acceptance and projections target complete Guardians; it is the merge gate for #1521.
- **US4 (#1522)**: depends on merged US1–US3 plus #1510/#1514.

### Within each user story

1. Complete the listed RED tests and record the intended failures.
2. Author immutable models/content before services that consume them.
3. Implement validation before allowing normalizer publication.
4. Implement canonical authority before player UI/projection.
5. Synchronize prompts/docs/examples/manifests/source guards in the same child.
6. Run the smallest Focused controls, one meaningful Fast checkpoint, conditional FullValidation, and one final PreMerge.

## Parallel opportunities

### US1

- T007–T011 can be authored in parallel in separate test files.
- T012, T013, and T014 own disjoint content paths and can proceed in parallel after the RED inventory contract is understood.
- T023 and T024 own documentation versus example/test-guard groups and can proceed in parallel after runtime fields settle.

### US2

- T026–T030 own separate contract/integration/source-guard files.
- T040 documentation work can begin after the atomic bundle field names stabilize while runtime verification continues.

### US3

- T042–T049 own separate lifecycle, classification, acceptance, parity, and post-story tests.
- T060 and T061 own documentation and worked-example groups after command/state names stabilize.

### US4

- T063–T068 own separate actor, faction, cross-link, visibility, atomicity, and source-guard tests.
- T076 and T077 own documentation and examples after final public/private field names stabilize.

Parallel tasks must not edit the same shared validation/normalizer file concurrently; implementation tasks on those files remain sequential.

## Implementation strategy

### MVP first

1. Complete Setup and Foundational phases.
2. Implement US1/#1520 only.
3. Validate an empty no-Guardian game in all three realms.
4. Merge #1520; this alone fixes the original GM-knowledge failure.

### Incremental delivery

1. **#1520**: immutable story knowledge and exact binding.
2. **#1521**: complete ten Guardian actors and all fixed/renewable quest mechanics.
3. **#1522**: exact Saref and Wings actor/faction entities.
4. **#1519 closure**: combined walkthrough and evidence audit.

### Scope discipline

- Do not add GM workers or networking.
- Do not add schema migration, alias, receipt-less, or save-compatibility paths.
- Do not duplicate #1514 locations or #1510 faction authority.
- A newly discovered unrelated gap gets a linked GitHub follow-up; the offer/acceptance gap remains inside #1521 because it is required to satisfy US3.

## Task format validation

All implementation entries use `- [ ] TNNN`, include `[USn]` exactly in user-story phases, include `[P]` only for disjoint work, and name concrete repository paths. Tasks are intentionally unchecked until implementation and fresh verification evidence exist.
