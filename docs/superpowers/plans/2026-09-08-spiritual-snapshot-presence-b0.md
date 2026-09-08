# Spiritual snapshot original-presence B0 implementation plan

> For agentic workers: use the parent-owned subagent-driven-development controller, test-driven-development, requesting-code-review, and verification-before-completion. This is one bounded implementation task with five sequential steps. Apply only the complete companion named below; do not reconstruct its code from this prose.

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T081-B2C-J2-B0 prerequisite only.

**Goal:** Let an opt-in pending-turn snapshot reader distinguish “this closed canonical source path was absent when the turn was captured” from “this path was not covered,” without turning metadata into a restorable canonical file or changing legacy optional-path behavior.

**Architecture:** Add one nullable, typed, manifest-hashed `originalPathPresenceV1` dictionary to every pending-turn manifest DTO. Three real lease-bound producers snapshot the closed sixteen-path source set, then derive a complete ordered presence map from exact `files`/`snapshotFileHashes` coverage. The reader authenticates that typed field as part of the existing manifest payload hash, validates its raw/typed shape and coverage agreement, and exposes only authenticated absences through an immutable `AbsentLogicalPaths` list when callers use the new opt-in selection factory. Rollback continues to consume only its existing files/backups/baseline members.

**Tech stack:** Existing C#/.NET 8, `System.Text.Json`, xUnit, and `scripts/test-csharp.ps1`; no package or schema-version migration.

## Status and exact companion

**Implemented at `4de64a34`, fixture review correction at `084146ea`; acceptance pending the independent re-review and older test-boundary gate.** The parent read the full original, all final deltas, and the final plan. At corrected source `084146ea`, full-line replay from base `bd8f3d74` and comparison with all thirteen actual files confirms every corrected companion postimage. Actual initial evidence: semantic RED0/5, GREEN5/5, Integration17/17, GameEngine2/2 and existing156/156; correction RED1/2, GREEN2/2 and combined19+2/21. All builds0/0, no timeouts, cleanup complete. The unrelated boundary58/60 remains a failed artifact, not a passing control.

- Worktree: `E:/Games/worktrees/boe-1536-wound-materialization`.
- Audited production baseline: `e720b03f`; implementation source baseline: `9f301262eaa02b64266aa0b63b56bbce2ed4529f` (later planning-only commit is permitted).
- Complete apply-patch companion: `docs/superpowers/plans/2026-09-08-spiritual-snapshot-presence-b0.patch`.
- Predecessor audit: `sdd/spiritual-snapshot-absence-authority-audit.md`.
- B1 must depend on this API and must not duplicate producer or `ResourceMaterializationTestContext` edits.

## Frozen public-to-internal contract

The companion implements these exact names and shapes:

```csharp
internal static class PendingTurnSnapshotPathPresenceV1
{
    internal static IReadOnlyList<string> LogicalPaths { get; }

    internal static Dictionary<string, bool> Create(
        IReadOnlyDictionary<string, string> files,
        IReadOnlyDictionary<string, string> snapshotFileHashes);
}

internal sealed class PendingTurnSnapshotPathSelection : IReadOnlyCollection<string>
{
    internal PendingTurnSnapshotPathSelection(
        IEnumerable<string> requiredLogicalPaths,
        IEnumerable<string> optionalLogicalPaths);

    internal static PendingTurnSnapshotPathSelection CreateWithObservedOptionalPaths(
        IEnumerable<string> requiredLogicalPaths,
        IEnumerable<string> optionalLogicalPaths);
}

internal sealed class PendingTurnSnapshotReadAuthority
{
    internal IReadOnlyList<string> AbsentLogicalPaths { get; }
    public byte[] ReadRequiredBytes(string logicalPath);
}
```

All six manifest DTOs add this property immediately after `SnapshotFileHashes`, preserving identical reflection/serialization order:

```csharp
public Dictionary<string, bool>? OriginalPathPresenceV1 { get; set; }
```

Existing camel-case hash options serialize it as `originalPathPresenceV1`; existing `WhenWritingNull` omission preserves the exact payload hash of old current-turn manifests. There is no migration, default empty map, or re-signing of an old manifest.

## Closed observation set and meaning

`PendingTurnSnapshotPathPresenceV1.LogicalPaths` is an immutable list in the exact contract order below, containing exactly these sixteen canonical paths:

1. `game_state/meta/soul_state.json`
2. `game_state/resources/resource_definitions.json`
3. `game_state/resources/resource_state.json`
4. `game_state/resources/resource_history.json`
5. `game_state/resources/resource_owner_authority.json`
6. `game_state/meta/afterlife_spiritual_conflict_state.json`
7. `game_state/meta/afterlife_entity_profiles.json`
8. `game_state/meta/shining_abode_state.json`
9. `game_state/core/game_settings.json`
10. `game_state/effects/effect_identity_index.json`
11. `game_state/wounds/wound_identity_index.json`
12. `game_state/wounds/wound_history.json`
13. `game_state/player/wounds.json`
14. `game_state/npcs/npc_wounds.json`
15. `game_state/combat/enemies.json`
16. `game_state/combat/allies.json`

For each path, `true` means the producer observed the canonical path under its active `CanonicalWriteLease`, copied its bytes into the pending snapshot, and placed one exact-case nonblank row in both `files` and `snapshotFileHashes`. `false` means the same lease-bound producer tried that path and neither coverage dictionary contains a row. It does not mean “missing now,” “default value,” or “not selected.” The map is derived only after snapshot enumeration; no reader may inspect the current canonical root to manufacture historical absence.

The producer helper rejects exact/asymmetric coverage, case-confusable keys, blank snapshot paths, and blank hashes. The reader also rejects every non-null map unless it has exactly the closed sixteen keys, exact casing, strict JSON booleans, no duplicate or case-confusable raw key, and each boolean agrees with exact `files` plus hash coverage. Raw recursive duplicate detection already runs with `OrdinalIgnoreCase`; non-boolean values already fail typed deserialization. A changed-but-not-re-signed boolean fails detached authority before semantic agreement. A re-signed contradiction fails `pending_turn_snapshot_reader_presence_invalid`.

## Reader compatibility and limits

- The existing two-argument constructor is unchanged and sets `RequireSignedAbsenceForMissingOptionalPaths` false. Optional paths omitted from old snapshots continue to be skipped successfully and do not appear in `AbsentLogicalPaths`.
- `CreateWithObservedOptionalPaths` sets that flag true. Its optional paths must belong to the closed v1 set. A present optional path is read from signed snapshot bytes normally. A missing optional path succeeds only with exact authenticated `false`; it is added to immutable, ordinally sorted `AbsentLogicalPaths`.
- A missing opt-in path with a null map, missing key, or otherwise unavailable proof fails with `pending_turn_snapshot_reader_absence_unproven`; no read authority is returned.
- Required paths still require snapshot bytes regardless of any false observation. `ReadRequiredBytes`, `CoveredLogicalPaths`, request/realm checks, exact byte hashes, and all existing issue codes remain unchanged.
- Both constructors still flow through `IReadOnlyCollection`; the existing maximum of 64 selected paths remains enforced. B1's five required plus eleven optional paths total sixteen.

## Restore and cold-replay semantics

`originalPathPresenceV1` is a typed manifest field, never a `files` entry or physical snapshot object. This B0 patch intentionally does not edit `RealmSegregationAutoRollbackService`, `ValidateClientOwnedControlFilesAsync`, `BuildValidatedRollbackSnapshot`, `RestorePreTurnBackupAsync`, canonical baseline selection, or tracked-file enumeration. The three producers union the closed paths only into a local snapshot selection, never into rollback baseline/backups. Therefore restore membership and canonical writes remain exactly controlled by the old members.

Old current-turn snapshots deserialize with a null property and retain their original hash. Legacy readers can consume them. Opt-in callers may consume present selected paths from them, but cannot turn an uncovered missing path into absence. Cold replay reads the signed captured map/bytes only; a file created in the live root after capture cannot change `false` or supply bytes.

## Exact owned files

| File | Change |
| --- | --- |
| `BookOfEternityClient/Services/PendingTurnSnapshotReader.cs` | Closed v1 helper; authenticated semantic validation; opt-in selection; immutable absent authority |
| `BookOfEternityClient/Core/GameEngine/GameEngine.PrivateImplementation.cs` | DTO field |
| `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` | Real GameEngine lease-bound observation producer |
| `BookOfEternityClient/Services/LiveTurnPreparationService.cs` | Real live lease-bound producer and DTO field |
| `BookOfEternityClient/WebUi/BrowserAfterlifeTurnRequestQueue.cs` | Real browser direct-gacha lease-bound producer and DTO field |
| `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs` | DTO field used by validation/hash authority |
| `BookOfEternityClient/Services/GuardianPowerEventState.cs` | DTO field used by guardian/hash authority |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs` | DTO field used by normalizer/hash authority |
| `BookOfEternityClient.Tests/PendingTurnSnapshotPresenceContractTests.cs` | New fixture-free deterministic Fast contract suite |
| `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.cs` | Shared signed snapshot helper emits exact map; owned by B0 |
| `BookOfEternityClient.IntegrationTests/PendingTurnSnapshotPresenceIntegrationTests.cs` | New real live/browser/reader/authenticity tests, `RegressionIntegration` |
| `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs` | Real GameEngine producer plus rollback non-membership tests |
| `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs` | Register new physical integration source |

No GM prompt, example, matrix, manifest, or player-facing surface changes are needed: this is client-owned snapshot authentication infrastructure and explicitly not wound admission or J2 completion.

## Global Constraints

- Exactly the thirteen owned files below; no other source, restore consumers, GM surfaces, `.serena`, remote writes, issue closure, or cleanup.
- Use `apply_patch`, absolute worktree paths, PowerShell 7 and the repository bounded test entry point. One implementation/source owner and one C# lane at a time. Do not mutate source, index or HEAD during a lane.
- Run named Focused selections only with their default 5-minute bound; no child Fast, FullValidation, PreMerge, unbounded suite, or speculative time-limit increase. Parent owns the meaningful combined B0/B1 Fast checkpoint; the already accepted projector Fast is not repeated for planning.
- Preserve every failed attempt in the report. Compilation or fixture failures are not semantic RED. Do not create an artificial RED by breaking already implemented code.
- The final companion includes two parent test corrections: assert the reflected presence property exists before SetValue, and test both raw boolean tampering and recomputation of only the manifest hash while keeping original detached authority.
- Expected new rows after fixture review correction: 5 deterministic Fast, 19 physical Integration class cases, and 2 GameEngine producer/restore cases. No B1 test is an acceptance prerequisite before B1 exists.
- Escalate nontrivial contract/fixture corrections before changing reviewed code. With all named controls complete, one exact thirteen-file local implementation commit is permitted. Do not stage parent planning or metadata files.
- Report to `sdd/spiritual-snapshot-presence-b0-task-1-report.md` in this worktree's Git metadata; list commands, exact artifacts, row counts, failures, final code state, and deviations.

### Task 1: Implement original-path presence authority

#### Step 1 — fixture-free semantic RED

- [ ] Through the parent-owned SDD controller, confirm the current 13-file scope and reconcile only changed patch context; do not touch parent-owned projector, GM docs, or `.serena`.
- [ ] Add **only** `PendingTurnSnapshotPresenceContractTests.cs`. It names the missing helper/factory/private DTO members through reflection, so the project compiles against the pre-B0 source and fails semantically rather than from unresolved C# types. Do not add Integration sources or boundary registration during RED.
- [ ] Run the fixture-free RED:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PendingTurnSnapshotPresenceContractTests"
```

- [ ] Record the exact failed assertions for missing `OriginalPathPresenceV1`, the closed helper/factory, and absence authority; reject an unrelated compile or environment failure as RED evidence.

#### Step 2 — apply the single reviewed B0 source and typed-test unit

- [ ] Apply the remainder of the companion as one bounded source unit: all six aligned DTO fields, the closed helper, all three producer changes, reader/factory/authority changes, fixture emission, Integration source and registration, and GameEngine lifecycle rows.
- [ ] In each producer, keep rollback baseline calculation unchanged; union `LogicalPaths` only into the local snapshot selection, copy through the existing lease-taking helper, derive the map from exact files/hashes, and assign it before the existing manifest hash/authority computation.
- [ ] In the reader, authenticate the nullable field as part of the existing typed payload. A non-null map must be the exact sixteen-key shape; `true` requires one exact file and one exact hash, while `false` requires zero of both. The unchanged raw recursive duplicate check remains the strict duplicate/case-confusable gate before typed deserialization.
- [ ] Add the opt-in factory flag without changing the old constructor. Clone/sort authenticated absences into `PendingTurnSnapshotReadAuthority`; never add them to its byte dictionary.
- [ ] Update `ResourceMaterializationTestContext.CaptureValidatedPendingSnapshotAsync` in B0 only and derive its map through the shared helper, not a second observation algorithm.
- [ ] Run the Fast contract GREEN only after the full reviewed production API exists:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PendingTurnSnapshotPresenceContractTests"
```

#### Step 3 — exact real-file producer, reader, authenticity, and limit evidence

- [ ] Run the new physical Integration suite:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~PendingTurnSnapshotPresenceIntegrationTests"
```

- [ ] Positive signed absence/cold divergence: the real live producer captures missing settings as `false`; after a current settings file is created, a reader under a real lease still reports signed absence and does not expose bytes.
- [ ] Positive signed presence: the real browser producer captures settings as `true`; after current bytes change, the reader returns the captured bytes.
- [ ] Closed-set and bound: a real 16-path observed selection classifies every path; a real signed snapshot succeeds with exactly 64 selected paths, while the same observed factory fails the 65th with the existing selection-limit code.
- [ ] Old current-turn compatibility: remove the nullable field, recompute its old-shaped manifest hash and matching detached authority; opt-in absence fails exactly `pending_turn_snapshot_reader_absence_unproven`, while the legacy constructor still skips the uncovered optional path and exposes no absence claim.
- [ ] Detached-authority forgery: change a boolean and retain the original detached authority; both leaving the manifest hash unchanged and recomputing only that hash fail `pending_turn_snapshot_reader_authority_invalid`.
- [ ] Raw/typed shape: exact duplicate, ordinal-ignore-case variant, visually confusable Unicode extra key, non-boolean, missing, and extra keys fail before absence authority. Authenticated `false` with either one-sided coverage row and authenticated true/false coverage contradictions fail closed.
- [ ] Six typed DTOs: populated manifests round-trip to identical JSON and payload hashes; null old shapes omit the field.

#### Step 4 — actual restore hazard and GameEngine lifecycle controls

- [ ] Run the GameEngine producer/rollback rows:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests.CreateCanonicalBaselineSnapshotAsync_RecordsClosedOriginalPathPresenceWithoutChangingRollbackMembership|FullyQualifiedName~GameEngineTurnLifecycleTests.PreTurnRollback_PresenceMetadataDoesNotChangeCanonicalRestoreSemantics"
```

- [ ] The GameEngine producer proves exact true/false emission, no synthetic `files` row, and no absent-path addition to rollback baseline. Existing rollback restores byte-exact canonical state and handles newly created tracked state through its unchanged baseline algorithm.
- [ ] The new Integration suite must also execute `RealmSegregationAutoRollbackService.TryRollbackForbiddenRealmMutationsAsync` with a signed presence-bearing manifest and a forbidden realm file. Assert byte-exact restore and that no metadata artifact is written. This is the direct regression for the rejected synthetic-`files` design.
- [ ] Do not edit either restore implementation to make these tests pass.

#### Step 5 — boundary, parent Fast, and review handoff

- [ ] Run the physical Integration boundary guard:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests"
```

- [ ] Run the existing physical Fast reader/authority/live/rollback controls (the repository already owns their current category). Do not add B1 tests before B1 exists:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PendingTurnSnapshotReaderTests|FullyQualifiedName~PendingTurnSnapshotAuthorityTests|FullyQualifiedName~LiveTurnPreparationServiceTests|FullyQualifiedName~AfterlifeRealmAutoRollbackTests"
```
- [ ] Parent owns a single combined B0/B1 Fast checkpoint after independent review; the implementer does not run it. The projector prerequisite already passed parent Fast7849/7849 and is accepted. B0 named controls plus review gate B1 dispatch; B1 is not a circular B0 acceptance prerequisite.
- [ ] Review `git diff --check`, exact file inventory, no incomplete marker or body, and no changes to restore consumers or GM surfaces.
- [ ] Request code review before claiming B0 complete. Report focused artifacts and distinguish B0 completion from T081-B2C-J2-B / issue #1536 completion.

## Acceptance boundary

Review correction at source `4de64a34` (not yet accepted): independent review
found the shared Integration fixture enlarged rollback membership by unioning
the observation set into trackedPaths. The parent verified the code and rules;
the original rollback contract governs over that companion mistake. Preserve
the old tracked set separately, capture its union with the sixteen observed
paths, and add rollback rows only for original tracked members. A new two-row
Integration regression proves observation-only settings stay excluded while
explicit additionalTrackedPaths still include settings, with signed bytes in
both cases. This remains T081-B2C-J2-B0 and changes no production producer or
GM contract. The separate old boundary58/60 failure remains unresolved.

B0 is complete only when all six typed hash DTOs agree, all three actual producers emit an authenticated closed map under real leases, old-shaped current snapshots preserve legacy behavior but cannot prove opt-in absence, the reader exposes only authenticated absent paths, and rollback membership is unchanged. B0 does not admit spiritual wound sources, materialize wounds, change gameplay, update GM contracts, or complete T081-B2C-J2-B / #1536.
