# Quickstart: Implementing Authoritative Saref Story Materialization

This feature is delivered through three independently merged child issues. Do not start #1521 before #1520 is merged, and do not start #1522 before #1510, #1514, #1520, and #1521 are merged. Do not implement GM workers, multiplayer, legacy compatibility, or runtime migration here.

## Common start for every child

1. Work in an isolated feature worktree for the child issue.
2. Confirm the repository, branch, and dirty state:

   ```powershell
   git status --short --branch
   ```

3. Read `AGENTS.md`, `.specify/memory/constitution.md`, the child GitHub issue, and all files in `specs/1519-saref-story-materialization/`.
4. Confirm upstream dependency commits are present.
5. Use TDD: add the smallest failing focused contract/integration test before behavior code.
6. Preserve unrelated work and never stage generated `bin/`, `obj/`, or `TestResults/`.

## Child A — #1520 catalog and GM competence

### RED

Add failing tests proving:

- the loader requires exactly ten approved Guardians and forty quests;
- digest, case, q4 reward, and content inventory mismatches fail;
- the compact index is complete, deterministic, and at most 32 KiB;
- an empty New Game writes schema-2 state with exact binding;
- Mortal World, Chaos Sea, and Shining Abode prompts contain the complete compact index before any story actor exists;
- exact relevance attaches only the deduplicated full packages;
- player output contains no private catalog markers.

Suggested focused filter:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SarefStoryCatalogTests|FullyQualifiedName~SarefStoryContextTests|FullyQualifiedName~SarefMainStoryStateValidationTests"
```

### GREEN

1. Add the catalog/content files and project copy rule.
2. Implement deterministic canonical JSON hashing and the cached loader.
3. Create schema-2 story state and binding during every New Game.
4. Add the dedicated all-realm reminder fragment and exact relevance selector.
5. Reject old/missing/mismatched state without migration.
6. Update GM prompts, catalog contract docs, one pre-Guardian latent-trace worked example, manifest, and source guards.

### CHECKPOINT

Run the focused selection, one Fast checkpoint, the focused documentation tests, conditional FullValidation, the required LifecycleIntegration control for New Game/turn preparation, then PreMerge immediately before merge.

## Child B — #1521 Guardians and quest lifecycle

### RED

Add failing tests for:

- complete atomic materialization of each of the ten exact Guardians;
- profile/receipt/thought/location/binding mismatch and rollback;
- idempotent retry and pre-existing latent trace projection;
- exact story lifecycle and q1→q4 ordering;
- q4 memory-scene reward binding;
- the strict no-skip lifecycle for all forty fixed quests, including the q4 memory-scene exception;
- mandatory `storyScope` and title on all Guardian quest snapshots plus the visible Russian `Несюжетный квест` marker in console/browser;
- `UpdateGuardians.offerQuest` accepting only complete grounded `non_story` quests within ordinary cap/difficulty;
- console/browser `/принять_квест_хранителя` parity and exact pending request, registry, snapshot, Soul Gates blocker, cleanup, and daemon routing;
- story acceptance through Saref progress, non-story acceptance through `UpdateGuardians.acceptQuest`;
- a post-q4 non-story offer, acceptance, Mortal progress, hand-in, and completion for each of the ten Guardians with Saref state unchanged.

Suggested focused filter:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GuardianStoryMaterializationTests|FullyQualifiedName~GuardianQuestAcceptanceTests|FullyQualifiedName~SarefMainStoryStateValidationTests|FullyQualifiedName~ActorMaterializationContractTests"
```

### GREEN

1. Structure the ten complete Guardian and questline templates from the bibles.
2. Integrate exact templates into selection, attraction, and story appearance.
3. Stage Guardian/profile/thought/location/projection publication as one validated transaction.
4. Implement closed story progress, transition validation, and the only fixed-quest projector.
5. Implement `offerQuest`, client-owned acceptance requests, resolution validation, `acceptQuest`, and every registry/snapshot/blocker/cleanup/daemon surface required by the new pending contract.
6. Preserve classification and complete snapshots through progress/completion.
7. Add console/browser in-world acceptance affordances and result visibility.
8. Synchronize Mortal/afterlife rules, daemon/API docs, Actor Brain guidance, examples, manifest, and source guards.

### MANUAL WALKTHROUGH

1. Start with a freeform Guardian and confirm all story knowledge remains in the GM prompt.
2. Discover a latent trace for an absent story Guardian.
3. Materialize the exact Guardian and verify the trace survives and the eligible quest appears.
4. Accept q1 using the console, then repeat equivalent acceptance in the browser.
5. Complete q1–q3 and the q4 memory scene.
6. Create and complete a new `non_story` quest; compare Saref state before and after.

### CHECKPOINT

Run the focused selection, `npm run verify` if frontend code changed, one Fast checkpoint, documentation coverage plus FullValidation, the required LifecycleIntegration control, the related DeepValidation Guardian matrix, then PreMerge immediately before merge.

## Child C — #1522 Saref and Wings

### RED

Add failing tests proving:

- exact `saref:saref_001` materializes completely and `saref_agent:saref_001` is rejected;
- exact `shine_faction_wings_of_angels_001` uses #1510's story route and #1514's hall reference;
- leadership, story links, visibility, provenance, receipts, and q4 references agree exactly;
- hidden console/browser views expose no private entities or receipts;
- valid reveal exposes only intended public actions/details;
- wrong/old/case-variant IDs, partial bundles, receipt rewrites, and cross-link mismatches roll back all targets.

Suggested focused filter:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SarefActorMaterializationTests|FullyQualifiedName~FactionMaterializationContractTests|FullyQualifiedName~SarefMainStoryStateValidationTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
```

### GREEN

1. Add `actorType=saref` to the current common-profile contract without weakening `saref_agent`.
2. Structure and materialize Saref's complete profile/receipt/memory from the character bible.
3. Structure and materialize the Wings through the merged #1510 story route and exact #1514 location.
4. Enforce all cross-links and receipt immutability.
5. Route player views through one reveal-filtered console/browser projection.
6. Replace old Wings values only in `factionId`/`wingsFactionId` fields; do not add ID aliases and preserve `factionRole=wings_of_angels` plus player command aliases.
7. Add hidden/revealed, repair/rollback, and combined-flow worked examples and source guards.

### CHECKPOINT

Run the focused selection, `npm run verify` if frontend code changed, one Fast checkpoint, focused afterlife documentation coverage, FullValidation, the required LifecycleIntegration control for materialization/accepted-turn flow, and PreMerge immediately before merge.

## Required documentation audit for each child

Explicitly inspect and either update or record a no-update rationale for:

- `TaskGuides/CLI_Step_Main.txt`
- `Rules/Block_32_Guardians.txt`
- `Rules/Block_CLI_Operations.txt`
- Mortal-world rules that consume story quest progress
- `OtherGuides/Afterlife_Contract_Matrix.md`
- `OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json`
- `OtherGuides/Saref_Character_Bible.md`
- `OtherGuides/Saref_Guardian_Questlines/*.md`
- `Examples/E_CLI_Afterlife_Turns.txt`
- `Examples/example_validation_manifest.json`
- `CLI_API_Specification.md`
- `CLI_Agent_Daemon_Specification.md`
- daemon/launcher prompt entrypoints
- `BookOfEternityClient/game_master_daemon.ps1`
- `BookOfEternityClient/Services/AfterlifeContractRegistry.cs`
- `BookOfEternityClient.Tests/AfterlifeContractRegistryTests.cs`
- `AfterlifeDocumentationCoverageTests`
- `ExampleDocumentationValidationTests`

Every GM-affecting child needs at least one worked example proving the GM can author the changed behavior.

## Completion evidence per child

Before marking tasks or the GitHub child complete, record:

- exact commit/branch and reviewed diff;
- focused RED and GREEN evidence;
- Fast result directory;
- conditional FullValidation result directory and why it was required;
- required LifecycleIntegration result directory and #1521 DeepValidation result directory;
- frontend verification when touched or an explicit no-frontend-change rationale;
- final PreMerge result directory;
- changed GM prompts/docs/examples/manifests or an explicit no-update rationale;
- remaining dependency/risk, if any.
