# T282 — narrow snapshot probe, before execution

Baseline: b675934e540fe8235d2b055408e396418b0cc21b; compiled game/test source f479d42a72f7412871e797575a1b1bd2c6dabd07. This is source analysis and probe design, not a new runtime result.

## What T279 actually observed

The producer genuinely exited73 after155.266s; a distinct resumer exhausted600s. Its preflight lease/snapshot/runtime refresh returned before late-first entry. The last of24 bounded activity samples at316.426s names MortalJointCustodyCaptureCompletedBeforePreparation and23891 CanonicalInitial observations, with Limited=true. The final approximately284s have no activity samples. This cannot identify the final active method or prove an infinite loop, CPU cause, environment restriction or security block.

## Actual source chain

- `Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs:48–50`: hot custody capture precedes the checkpoint, then `CreateJointRepairPreparationAsync` executes. The checkpoint names the interval, not its entire stack.
- `Services/WoundAcceptedTurnPlanner.MortalJointRepair.cs:148–219`: genuine private RejectedCapture must be current; installed wave is reconstructed, correction revalidated, publication baselines read and continuation revalidated. A source42 copy alone cannot authorize this operation.
- The later common-plan graph has a previously checked conditional lower bound of64 full snapshot reads, documented in `T263-static-read-amplification.md`. This is not a count of calls reached by T279 and excludes preparation/preflight/publication.
- `Services/MortalQteWoundOpportunityProjection.cs:23–28`: `ReadSnapshot` requests soul_state plus9 observed optional roots through the real `PendingTurnSnapshotReader`.
- `Services/PendingTurnSnapshotReader.cs:207–225`: before selected bytes are exposed, detached authority authenticates the original inventory with a physical-read callback. `Services/PendingTurnSnapshotAuthority.cs:788–801` copies Files/SnapshotFileHashes as metadata but physically hashes only RollbackBackups through lines861–899. LivePrepare sets that rollback map empty at169, so it cannot reproduce the suspected physical amplification. A narrow selected result with a genuine nonempty rollback map need not imply a narrow physical read population.
- `Core/FileSystemManager.OrdinaryReads.cs:175–205` invokes the existing canonical-initial hook after opening an ordinary synchronous file; it is an observation of that boundary, not proof that the whole read completed. Explicit asynchronous leased reads use `FileSystemManager.cs:1609–1622` and ordinary reader lines66–130. Hooks remain observers only.

## One falsifiable hypothesis

For an unchanged requested projection, a larger genuinely signed original map increases the observed physical-read population on each `ReadSnapshot`, including files outside the selected10 paths. Compare two independently prepared genuine originals:1 present file versus17 present files. Record actual call durations separately from read counts; do not assume a performance ratio or extrapolate to600s.

Both roots are new owned diagnostic fixtures. The only game content copied is the exact415-byte source42 game_state/meta/soul_state.json, SHA25619E66A7A2059C8F569862E55FE728B6A6A3BA62C93FDBE42265E821B7DE68430; its actual currentRealm is Mortal World. Variant B additionally contains16 tiny synthetic JSON members under lore/probe/member_00.json through member_15.json. These are inert diagnostic input, not game results or transported authority.

**Pre-execution correction:** the initial LiveTurnPreparationService proposal was rejected during independent source consultation: its RollbackBackups map is empty, so changing an unselected snapshot file must not be expected to fail this reader. No harness or runtime was executed under that incorrect proposal. The corrected path below creates real nonempty rollback maps through the engine itself.

FileSystemManager.EnsureDirectoryStructure initializes only the own new roots. The existing GameEngineTurnLifecycleTests adopted-root factory constructs a real inert engine sharing that manager (no bridge/music/sound, no turn). Actual private GameEngine.CreatePreTurnBackup(string)1996–2026 creates its own real RollbackSnapshot; pass that unmodified production result to actual4-argument CreateCanonicalBaselineSnapshotAsync(TurnRequest, rollback, sourceLabel, null)412–419. Its manifest uses actual BackupFiles546–548 and production signing558–584. No private authority/data object is constructed by the harness. This same route appears in ConsoleRollback.cs113–119. A new ordinary TurnRequest is DATA: new session/request identifiers, turn1, Mortal World progression, dice3,17; serialize it only after capture as in the existing route.

Engine rollback enumeration1939–1993 includes ordinary lore files, so the16 extra members become genuine rollback-backed inputs. Require exact input membership and actual rollback/file counts1/17 with delta16. All4 afterlife initializers return early for this genuine Mortal realm (lines299,327,355,383); unexplained extra members are setup failure, never silently accepted or removed. ReadSnapshot must return the fresh exact session/request/manifest token/turn1/realm, exact soul bytes, exactly1 selected present path and9 observed optional absences. Eight reads per root observe physical paths/counts and per-call elapsed through the existing canonical-initial hook. Exact game_session file inventory/hashes remain unchanged across positives.16raw versus16actual leased async soul reads per root give a separate primitive timing control.

Finally append one space ONLY to the own variant-B physical rollback backup named by manifest.RollbackBackups["lore/probe/member_00.json"]. The next real ReadSnapshot must refuse InvalidDataException containing pending_turn_snapshot_reader_authority_invalid (underlying detached_authority_mismatch), before returning selected bytes. No reseal, fallback or retry. Altering Files[logical] would test a different path and is specifically excluded. This negative proves the genuinely wider rollback-backed authority was checked even though the16 lore paths were not selected for byte exposure.

## Execution boundaries

External throwaway .NET8 project references the accepted production DLL; game/test source and source42 artifact remain unchanged. Reflection selects existing internal APIs, but every lease/original/read authority is genuinely issued by them. The harness is preserved solely as reproducible diagnostic source, not promoted into production or a gameplay test.

One child attempt,30s total including both preparations and all controls. Parent uses original `GmWorkerWindowsOwnedLaunch` plus `GmWorkerProcessTreeFactory.Instance`, retains the original owner across partial failures, and releases the child gate only after retained Job authority is confirmed. StopAndObserveAsync, SettleOutputsAsync and DisposeAsync are the existing production cleanup route; no alternate killer. Parent ownership/cleanup is bounded by2min; missing cleanup remains failure and retains the owned directory. Historical T219 and immutable source artifact are read-only/untouched.

Success establishes the component's actual read amplification and timings only. Any validation/setup/budget failure remains explicit; no unchanged retry, full resumer, Stage43, generator, Linux run, production optimization or crash PASS follows.
