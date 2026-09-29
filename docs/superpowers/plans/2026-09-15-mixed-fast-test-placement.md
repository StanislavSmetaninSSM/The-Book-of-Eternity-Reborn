# Mixed Fast Test Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan under the parent's serialized build ownership. Steps use checkbox syntax for tracking.

**Goal:** Relocate exactly 16 existing filesystem/session cases to Integration while retaining 50 existing Fast cases and every original method, assertion, and theory row.

**Architecture:** Split three mixed classes into Fast logic and Integration `.Session.cs` source files. Share only the Mortal helper partial as source; it contains no test attributes. Extend existing boundary checks with exact six-part case inventories and a helper-link guard, without changing production, runner limits, or unrelated classifications. Assign the three session classes to the existing RegressionIntegration category and extend its exact manifest.

**Tech Stack:** C# / .NET 8, xUnit 2, existing Roslyn integration boundary checks, PowerShell 7 bounded test lanes, Python 3 for scratch package generation.

**Spec:** Issue #1536; `specs/1536-complete-wound-materialization/tasks.md` task T081-B2C-J2-C2-V2; current `docs/testing.md`; sibling `fast-placement-audit.md` and `fast-budget-observations.json`. This plan concerns test placement only; C2 gameplay integration remains open.

## Global constraints

- The parent publishes/reviews this proposed plan before actual checkout edits. User authorization already covers the concrete follow-up; no further user confirmation is needed.
- No production or runner changes. Assign class-level RegressionIntegration only to the three session classes; leave Fast and helper sources unclassified. Fast remains the existing five-minute lane; this plan does not increase a limit or reclassify pure tests merely because they were slow in a contended run.
- Preserve all 43 original test methods and 66 cases, their full assertions and InlineData rows. Add two boundary guard Facts in Integration; the original coverage is still 50 Fast + 16 Integration.
- Preserve current C2, zero-correction, source, receipt, and other dirty work. Use the pinned unit patch, not a whole-checkout diff/reset.
- Serialize all builds through the active parent-designated worker. No staging, commits, pushes, or issue closure in this bounded execution.
- Both projects already reference production and TestSupport. Do not make Integration reference the Fast test assembly, add a package, or compile a test-bearing source file in both projects.
- This changes no Mortal/afterlife GM contract. No GM prompt, example, matrix, manifest, or FullValidation rerun is required solely for test placement. The zero-correction's separate outstanding evidence remains separate.

## Complete proposed artifact

The complete patch is `fast-placement-migration/migration.patch`. Every modified/new file has a full corresponding file under `fast-placement-migration/postimages/`. Existing-file bytes are captured under `preimages/`; `pins.json` identifies all 13 pre/postimages. `original-test-member-sha256.json` records all 43 original attribute/body blocks, and `case-inventories.json` records every one of the 66 case signatures.

The generator is `build-fast-placement-proposal.py`. It creates only scratch artifacts, checks exact 40/4/8/1/2/11 case counts, verifies the union of each split equals the original case list, and verifies that every original attribute/body block remains unchanged in its destination. Once pins exist, it rejects any changed checkout preimage or new-target presence; a changed parent file requires an explicit reviewed rebase.

### File map

| Proposed path | Responsibility |
|---|---|
| `BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs` | Retain 22 methods / 40 cases, including the cheap repository-read write-free guard; declaration becomes partial |
| `BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.Helpers.cs` | Existing constants and helper bodies, unchanged, in a helper-only partial with no Fact/Theory/InlineData |
| `BookOfEternityClient.IntegrationTests/MortalItemConsumptionPlannerTests.Session.cs` | Three original methods / four cases: signed projection theory true/false, five-collector identity ordering, raw mirrored creation rejection; class-level RegressionIntegration |
| `BookOfEternityClient.Tests/ShiningAbodeTradeAndForgeStateTests.cs` | Retain seven pure methods / eight cases and their JSON helpers |
| `BookOfEternityClient.IntegrationTests/ShiningAbodeTradeAndForgeStateTests.Session.cs` | Original reroll bootstrap Fact plus its unchanged quartet commit helper; class-level RegressionIntegration |
| `BookOfEternityClient.Tests/GmWorkerAuditLogTests.cs` | Retain two pure ID-generator Facts, including all 10,000 concurrent invocations |
| `BookOfEternityClient.IntegrationTests/GmWorkerAuditLogTests.Session.cs` | Original eight disk/locking methods / eleven cases and all their local helpers/enum; class-level RegressionIntegration |
| `BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj` | Link only the Mortal helper partial; retain all project/package references |
| `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` | Add three `.Session.cs` paths to the existing reviewed-heavy source list |
| `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs` | Make its class declaration partial to reuse the Roslyn checker; add exactly three session paths to RegressionIntegrationSources without removing other entries |
| `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.FastPlacement.cs` | Two new exact-inventory/helper-link guards using existing private boundary helpers |
| `BookOfEternityClient.IntegrationTests/FastPlacementTestInventories.cs` | Six exact reviewed case manifests; all original 66 signatures |
| `docs/testing.md` | Record actual placement, shared-helper ownership, retained write-free guard and timing limits |

The same public class and method names are retained in their destination assemblies. Partial classes do not span assemblies: the helper-only source is independently compiled with each Mortal test half. It is intentionally shared as source because its existing private assertion helpers use xUnit and the current TestSupport project does not reference xUnit. No new dependency or assertion rewrite is needed.

## Task 1: Pin the package and prove the placement guard is red

**Files:** the pinned package, Integration boundary class/manifest/partial/inventories only.

**Consumes:** actual checkout preimages in `pins.json`, existing `ExactTestInventoryViolations`, `ManifestLines`, `SourcePath` and `AttributeNameIs` helpers.

**Produces:** actual OLD evidence for the two new guard Facts and existing exact category guard before moving any original test.

- [ ] Confirm build ownership is free and that the parent has accepted the final zero-correction evidence or explicitly ordered this placement next.
- [ ] Run the scratch generator and applicability check from the worktree. Both are read-only for production/test source; do not erase pins to bypass drift.

```powershell
C:/Python314/python.exe .superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/build-fast-placement-proposal.py
git apply --check .superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fast-placement-migration/migration.patch
```

- [ ] Apply only the three Integration guard files from the full unit patch: the original boundary declaration becomes partial and its regression manifest gains the three future session files; the two new files supply complete executable guards/case manifests.

```powershell
git apply --include=BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs --include=BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.FastPlacement.cs --include=BookOfEternityClient.IntegrationTests/FastPlacementTestInventories.cs .superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fast-placement-migration/migration.patch
```

- [ ] Run the guard-only OLD selection with the standard environment and bounded entry point. Expected: it compiles and fails because original heavy cases remain in Fast and the new Integration/helper files do not yet exist. A compiler failure is not sufficient RED evidence.

```powershell
. ./.superpowers/sdd/2026-09-08-spiritual-journal-c1-implementation/enter-net8.ps1
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests.MixedFastPlacement_|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
```

- [ ] Record the actual summary/TRX identifiers and failure reasons. Do not remove original assertions or weaken other manifests to make this guard green.

## Task 2: Apply the exact split and verify all original cases

**Files:** remaining ten patch paths, with complete contents in `postimages/`.

**Consumes:** the accepted three-class split and red guard from Task 1.

**Produces:** 50 original Fast cases, 16 original Integration cases, and two green Integration guard Facts.

- [ ] Apply the remainder of the same patch, excluding only the three already-applied guard files.

```powershell
git apply --exclude=BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs --exclude=BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.FastPlacement.cs --exclude=BookOfEternityClient.IntegrationTests/FastPlacementTestInventories.cs .superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fast-placement-migration/migration.patch
```

- [ ] Inspect the unit against its preimages. Confirm every original method's attributes/body matches `original-test-member-sha256.json`; the generator's output demonstrates the intended unchanged extraction. Confirm the helper file has no tests, only one helper link exists, and old test-bearing files are not linked across projects.
- [ ] Run the complete retained-case selection in Fast. Expected original case count: 50, with no duplicates.

```powershell
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~MortalItemConsumptionPlannerTests|FullyQualifiedName~ShiningAbodeTradeAndForgeStateTests|FullyQualifiedName~GmWorkerAuditLogTests"
```

- [ ] Run all migrated cases and both new boundary guards and the existing regression category manifest guard in Integration. Expected case count: 19 (16 migrated, 2 new guards, and 1 existing category guard), with no duplicates. This preserves the signed snapshot bootstrap, both vehicle-root variants, all audit writer/barrier/cancellation rows, and real Shining quartet publication assertions.

```powershell
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemConsumptionPlannerTests|FullyQualifiedName~ShiningAbodeTradeAndForgeStateTests|FullyQualifiedName~GmWorkerAuditLogTests|FullyQualifiedName~IntegrationTestBoundaryTests.MixedFastPlacement_|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
```

- [ ] If a real assertion/compiler failure occurs, diagnose the split/helper boundary and preserve every original assertion. Keep focus on the actual demonstrated failure; no broad test rerun or automatic budget increase.
- [ ] Run a reverse applicability check of the unit after successful application and inspect status without staging. Existing unrelated dirty files remain untouched.

```powershell
git apply --reverse --check .superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fast-placement-migration/migration.patch
git diff --check
git status --short
```

## Task 3: Parent checkpoint, independent review and truthful reporting

**Files:** execution report/progress for V2 and the proposed `docs/testing.md` paragraph. No additional implementation is implied.

**Consumes:** actual 50 + 19 focused results and exact unit diff.

**Produces:** independently reviewed placement with measured Fast checkpoint evidence; parent-owned integration decision.

- [ ] Parent runs one Fast checkpoint when the related C2 changes settle. Do not add a duplicate worker Fast run. Keep Fast's existing five-minute limit.

```powershell
./scripts/test-csharp.ps1 -Lane Fast
```

- [ ] Record actual case counts, wall time, timeout status, cleanup and duplicates. Compare the measured checkpoint to the prior 7,882-case baseline with the caveat that parallel test durations are not isolated benchmarks. Do not claim an 82-second wall-time saving merely by adding case durations.
- [ ] Independently review the actual pinned unit, all original member/case preservation, the three source placement boundaries and the case/helper/category guard results. Do not weaken unrelated Fast/Integration manifests or move the cheap write-free guard as an incidental change.
- [ ] Report no GM-contract change and no production/runner change. Keep C2 and issue #1536 open. Any commit or PreMerge belongs to the later parent integration workflow, not this bounded plan.

## Preparation evidence and limitations

The author generated all 13 full postimages, verified the exact 40/4/8/1/2/11 split, preserved all 43 original test-member blocks unchanged, and obtained exit 0 from `git apply --check`. No source file was applied, compiled, or executed. Runtime/compiler correctness and timing remain to be established by the serialized execution above. The new guard checks inventories and helper ownership; unchanged assertion/body evidence is additionally supplied by the generator's exact original-member hashes and scoped diff for independent review.
