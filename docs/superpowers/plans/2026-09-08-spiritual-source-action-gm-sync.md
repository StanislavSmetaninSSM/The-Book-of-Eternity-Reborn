# Spiritual source action GM synchronization implementation plan — conditional on B1

> **For agentic workers:** REQUIRED SUB-SKILL: The parent must use superpowers:subagent-driven-development after inspection and tracking. Steps use checkbox (`- [ ]`) syntax for tracking. `docs/superpowers/plans/2026-09-08-spiritual-source-action-gm-sync.patch` is the complete eight-file candidate; do not recreate code from this prose.

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T089-T092 and T081-B2C-J2-B1.
**Goal:** Synchronize the GM-facing source-action target and terminal-exchange witness contract with B1's production-consumed shape checker and signed source-preparation path without claiming wound admission.
**Architecture:** Extend the already-landed `spiritual_wound_source_envelope_v1` checkpoint with one adjacent `spiritual_wound_source_action_v1` section and two explicitly fragment-scoped examples. Fast guards pin the prose/routing/manifest boundary; typed Integration tests parse the printed fragments, call B1's actual `ValidateSpiritualWoundSourceActionShape`, and place the printed terminal resolution into the existing signed production preparation fixture, where it must remain `TerminalClosure` pending and must not mint common-plan authority.
**Tech Stack:** Existing C#/.NET 8, xUnit, `System.Text.Json.Nodes`, existing test helpers, Markdown/JSON examples, and PowerShell 7 bounded lanes; no package or product runtime beyond the separately reviewed B1 dependency.

## Global Constraints

- Exact worktree: `E:/Games/worktrees/boe-1536-wound-materialization`; B1's source baseline is `30f0897c699bc883e38decdf6c8deb53d8f18819`. Parent reported docs-only checkpoint `9f301262eaa02b64266aa0b63b56bbce2ed4529f` before concurrent B0 work began; recheck the live HEAD and B1 target hashes at implementation. Apply only after B0 is accepted and the frozen B1 companion is implemented/reviewed.
- This plan complements, and must not overwrite or duplicate, `docs/superpowers/plans/2026-09-08-spiritual-source-envelope-gm-sync.md` and its companion. Its pending parent-owned `SpiritualWoundSourceEnvelope_DocumentedArtUsesProductionParser` file remains owned there and lands in the same bounded documentation checkpoint after B1 exposes the parser. That existing test extracts the first named JSON fence; the new action tests enumerate the bounded section and require the workflow heading to exclude unrelated examples.
- The only new action field is optional exact `spiritualWoundTarget` on the player exchange object or the opposition `incomingAction`; its closed members are exact nonblank untrimmed strings `actorType`, `actorId`, and optional `retraumaWoundRef`.
- An explicit target must be an original member of the affected side. Absence selects the original affected-side lead. `retraumaWoundRef` is accepted only through canonical prior active spiritual-wound carrier, identity, realm, owner, and immutable-history authority; current candidate agreement remains a later C/E gate.
- No exchange, `incomingAction`, audit, DTO, prose, or manifest may supply `traumaPressure`, `sourceSeverityCap`, `maximumSeverityRank`, `guaranteedSeverityRank`, or `spiritualWoundEnvelope`. Formula, harmful margin, destination strain, danger cap, source cap, guarantee, mechanical tiers, dice values, and source binding remain client-derived from signed originals; required cost/dice reports copy original client data and outcomes report only checked results.
- `resolution.terminalExchange` is one complete existing exchange witness, including complete matching `diceAudit` when contested. It is not a totals summary and cannot reuse a retained/current coordinate or die claim.
- Source preparation is not an admitted wound, opportunity, decision, receipt, successful resource/effect publication, or final accepted turn. A documented terminal witness is expected to produce the typed B1 `TerminalClosure` pending requirement and no common-plan authority.
- Lawful start, escalation, prefix, terminal, passive, champion-coordination, dice-free voluntary, special, guaranteed, and ordinary contours remain mandatory for C/D/E client-owned proof. Never invent dice, operation/damage evidence, or fake success to make an example pass. Soul dissipation remains optional in both real realms.
- Do not describe unfinished C/D/E behavior as authorable or accepted. The examples are complete only for their named source-local fragments.
- No Mortal contract changes: the action source is afterlife-only and is routed through the shared wound guide. Existing daemon/launcher entrypoints already mandate the matrix/CLI guide; no new pending file, command, transport, setting, receipt, or registry entry is introduced.
- Use `apply_patch`; preserve unrelated `.serena` and user changes. Implementer owns named Focused runs only. Parent owns the combined Fast checkpoint and conditional FullValidation; PreMerge remains reserved for merge.

---

## Exact Owned Files

| File | Responsibility |
| --- | --- |
| `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualSourceEnvelope.cs` | Add two deterministic Fast guards for exact action placement/shape, forbidden computed fields, terminal completeness, pending/non-admission language, both realms, and manifest route. |
| `OtherGuides/Wound_Materialization_Contract.md` | Add the authoritative `spiritual_wound_source_action_v1` contract next to the existing envelope section. |
| `OtherGuides/Afterlife_Contract_Matrix.md` | Route both real afterlife realms to the action contract and preserve source-local/pending boundaries. |
| `TaskGuides/CLI_Step_Main.txt` | Add one mandatory compact GM instruction block for target and terminal authoring. |
| `Examples/E_CLI_Afterlife_Turns.txt` | Add the worked player/opposition target fragment and complete contested terminal witness fragment. |
| `Examples/example_validation_manifest.json` | Add one honest focused-fragment entry with both real realms and the two code-consuming validation routes. |
| `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.SpiritualSourceAction.cs` | Parse the exact printed target fragment, validate both legal placements through B1's real shape method, reject a caller-computed field, and pin manifest truthfulness. Also expose only a test-local reader for the sibling signed fixture. |
| `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.SpiritualSourceDocumentation.cs` | Reuse the existing signed conflict fixture and B1 production preparation API to prove the printed terminal witness is retained only as `TerminalClosure`, with no accepted common plan or write. |

No B1 runtime source file, envelope companion file, Spec Kit artifact, task checkbox, HEAD/index, C/D/E implementation, remote, or cleanup is owned here.

## Interfaces

- **Consumes after B1:** `internal static void ValidationService.ValidateSpiritualWoundSourceActionShape(JsonObject exchange, string context, List<ValidationIssue> issues)` and `Task<ValidationService.SpiritualWoundSourcePreparation> ValidationService.BeginSpiritualWoundSourceSessionAsync(CanonicalWriteLease lease)`.
- **Consumes existing signed test fixture:** the private partial-class helpers `CreateCompleteConflictFrameContextAsync`, `AssertNoConflictFrameErrors`, `ResourceMaterializationTestContext.CaptureValidatedPendingSnapshotAsync`, and `AcceptedMechanicsPlanAuthority.HasValidated`.
- **Consumes existing example parser without copying it:** `ExampleDocumentationValidationTests.SpiritualSourceActionExamples()` is a narrow internal wrapper around the already-existing private `ParseNamedJsonFences` helper in the same partial class.
- **Produces:** no product API. It produces executable GM documentation for the source-local action and terminal witness only.

### Task 1: Deliver the bounded action/terminal GM synchronization

**Files:**
- Modify: `E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualSourceEnvelope.cs`
- Modify: `E:/Games/worktrees/boe-1536-wound-materialization/OtherGuides/Wound_Materialization_Contract.md`
- Modify: `E:/Games/worktrees/boe-1536-wound-materialization/OtherGuides/Afterlife_Contract_Matrix.md`
- Modify: `E:/Games/worktrees/boe-1536-wound-materialization/TaskGuides/CLI_Step_Main.txt`
- Modify: `E:/Games/worktrees/boe-1536-wound-materialization/Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `E:/Games/worktrees/boe-1536-wound-materialization/Examples/example_validation_manifest.json`
- Create: `E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.SpiritualSourceAction.cs`
- Create: `E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.SpiritualSourceDocumentation.cs`
- Parent-owned dependency applied in the same bounded change: `E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.SpiritualSourceEnvelope.cs` from the existing envelope companion.

- [ ] **Step 1: Confirm B0 is accepted, B1 is implemented/reviewed with its frozen APIs, and parent has tracked this eight-file action companion plus the old parent-owned envelope Integration partial.**

Expected: B1 exposes `ValidateSpiritualWoundSourceActionShape`, `BeginSpiritualWoundSourceSessionAsync`, and `SpiritualWoundSourceEnvelope.TryRead`; no C/D/E authority is inferred.

- [ ] **Step 2: Apply only the Fast-file update, including both the Regex import hunk and the two `SpiritualSourceActionDocumentation_` guard bodies.**

The guide assertion normalizes only Markdown backticks and whitespace before exact token matching. It does not lowercase, trim identities, fold confusables, or weaken the raw closed-string contract.

- [ ] **Step 3: Run the two Fast documentation guards for a genuine semantic RED.**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualSourceActionDocumentation_"
```

Expected: 0/2 with missing action guide/routing/manifest content after a successful compile. Preserve the exact result artifact.

- [ ] **Step 4: Apply the remaining seven files from this companion and the old parent-owned envelope Integration partial.**

The new `## spiritual_wound_source_action_v1` heading separates the envelope and action sections, and `## Afterlife turn workflow and worked examples` terminates the action section. The existing envelope test extracts its first named JSON fence; the new action parser enumerates exactly two action fences and excludes unrelated examples. Matrix/CLI guidance forbids inventing or choosing mechanical authority while explicitly preserving required cost/dice reports copied from original client data and checked outcomes.

- [ ] **Step 5: Run the same two Fast guards GREEN.**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualSourceActionDocumentation_"
```

Expected: 2/2 PASS with the same exact IDs, clean build/cleanup, no timeout or skips.

- [ ] **Step 6: Run one combined four-row Integration selection.**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~SpiritualSourceAction_|FullyQualifiedName~SpiritualWoundSourceEnvelope_DocumentedArtUsesProductionParser"
```

Expected: 4/4 PASS: the old envelope parser row plus the two typed target/manifest rows and one signed real-file/lease terminal-pending row. Do not run redundant per-pair selections.

- [ ] **Step 7: Run the complete afterlife documentation guards.**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
```

Expected: all discovered rows PASS. Record exact discovery/execution counts and the result directory.

- [ ] **Step 8: Hand the scoped diff and evidence to parent; parent owns final tracking, commits, one combined Fast, and conditional FullValidation.**

Report the eight action-companion files, the separately parent-owned envelope partial, RED/GREEN artifacts, exact four Integration IDs, and the no-update rationale: Mortal docs, rules glossary, daemon/launcher bodies, afterlife pending/control registry, and UI are unchanged because there is no new transport, state file, or player command. Do not run another Fast, commit, edit task checkboxes, or claim T089-T092/#1536 complete; C/D/E source reduction, admission, insertion, lifecycle, receipts, and publication remain open.

## Candidate Test Rows

| Project/lane | Test | Proves |
| --- | --- | --- |
| Fast | `SpiritualSourceActionDocumentation_DeclaresExactTargetAndTerminalWitness` | Exact placement/closed strings, affected-side/default lead, prior re-trauma authority, forbidden computed fields, complete terminal witness, matching dice, source-local non-admission. |
| Fast | `SpiritualSourceActionDocumentation_RoutesBothRealmsAndPendingContours` | Both real realms, optional dissipation, all C/D/E pending contours, example/manifest route. |
| Integration | `SpiritualSourceAction_DocumentedTargetsUseProductionShapeValidator` | Both printed action placements call B1's actual production-consumed deterministic checker; mutation rejects a caller cap. |
| Integration | `SpiritualSourceAction_ManifestDeclaresFragmentOnlyProductionRoutes` | Manifest is honest about two fragments and the exact production routes/limits. |
| Integration | `SpiritualSourceAction_DocumentedTerminalWitnessIsSourceLocalPendingOnly` | Printed full contested witness consumes signed original state/dice in real production preparation and remains `TerminalClosure`, never accepted authority. |

## Exact Blockers / Non-Claims

1. B0 snapshot presence authority must be implemented and accepted first.
2. B1 must be applied from `docs/superpowers/plans/2026-09-08-spiritual-source-owner-unit-b1.patch`, compile, pass its named controls, and expose the two candidate APIs exactly as frozen.
3. The existing envelope Integration partial remains in the old companion and cannot compile before B1. This plan references it only in combined verification.
4. A source-local `TerminalClosure` pending result is intentionally not an accepted turn. If the candidate returns admission/common-plan authority, invented dice, or no pending requirement, stop and diagnose B1/C-E drift rather than weakening the example.
5. C/D/E remain responsible for actual applied effects/champion coordination, dice-free voluntary binding, prior start/escalation authority, same-turn prefix, terminal closure, resource/effect reductions, wound opportunity/admission/insertion, receipts, and publication. Neither this plan nor B1 makes those flows accepted.

## Self-Review

- Spec coverage: every requested action/terminal/non-admission/both-realms boundary maps to an exact guide token and executable test; the existing envelope work is referenced without duplication.
- Completeness scan: no stub markers, stub exceptions, or unspecified code steps occur; complete bodies are in the adjacent companion.
- Type consistency: candidate names match B1's frozen `ValidateSpiritualWoundSourceActionShape`, `BeginSpiritualWoundSourceSessionAsync`, `SpiritualSourceRequirement.TerminalClosure`, `Sources`, `PendingRequirements`, `ClaimedDice`, and existing `AcceptedMechanicsPlanAuthority.HasValidated` APIs.
- Ownership: only this metadata plan/companion was written during preparation. No source, tests, docs, examples, index, HEAD, remote, task lifecycle, C# lane, or cleanup was changed.
