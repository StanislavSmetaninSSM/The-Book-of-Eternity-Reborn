# T227 — Windows source42 investigation

Task: [T227](../../tasks.md); scope and execution limits: [plan](../../plan.md). Approved spec3 remains unchanged. This is a bounded investigation, not crash5 qualification or permission to modify game production code.

## Prior evidence

T219 on source5f0c08ae5: four console-observed producer timeouts at220s, no observed exit73 or resumer, fifth case interrupted by category15m, no TRX. Windows FAIL_INCOMPLETE. All four stdout streams reached the source42 response author and then “Мастер игры размышляет...”. The complete original bytes are preserved in `T219-original-child-artifacts.zip`, with hashes in its manifest; Git-normalized text copies are convenience views.

Separate read-only consultation `diagnose_windows_producer_timeout`, explicitly gpt-6-astra/high, traced the actual chain without builds, tests or edits:

- The producer first invokes `RunJointCompletionControlAsync`, which awaits `RunActualQteStorySourceAsync` before attaching any stage43 crash probe.
- That source helper uses the actual `ProcessPlayerTurn`. Its Author reads the actual request/snapshot under an explicit lease, disposes the lease, then writes the original public QTE response and ready file.
- `WaitForTerminalSignalAsync` awaits Author at `TerminalWaitStarted` before starting the polling task. The saved Status text is emitted afterward: Author returned in the four completed timeout runs.
- The remaining interval includes inspection lease acquisition, polling/status/key settlement, terminal capture, validation/materialization and QTE handoff. Existing output cannot distinguish them. Empty live-input queue does not support a blocking native console read diagnosis.
- Windows ordinary admission takes an additional legacy recovery path before trusted-local recovery; this and accumulated validation cost are hypotheses only. No demonstrated self-deadlock was found. Retained cut3 was interrupted before source staging and does not explain the other four cases.

## Probe and provenance

Source `8326d14b1fe8471d258725ae172f75bc23969d73`, metadata/evidence `3175b8e59499e8f44fe94aad17e8432c6a6dd9b5`. One Windows-only Fact, one owned child, same physicalDeclaration/max2/live-input/configuration as the common source42 helper. No stage43, resumer, simulated crash, alternative waiter or production change. Existing process220s/join10s/EOF10s, exclusive category5m. Main CI selection and original five remain unchanged.

Optional test-only reporter observes existing checkpoints: first inspection lease acquired, terminal snapshot captured, accepted outcome validated; plus Author enter/return, actual QTE action entry and turn enter/return. It performs no canonical reads or awaited work. Other source-helper consumers pass null. The existing64-record/2048-byte/131072-total output limits and primary-failure precedence remain.

Mapping RED: source80b4a12f9,7completed/6PASS/1expectedFAIL. GREEN: source8326d14b1,7/7PASS,0skip,21.579s,cleanup complete. Two fresh XML builds, changed XML diagnostics0; six DLL hashes match. Catalog814/11833, probePlanOnly1, ordinaryPlanOnly5. Separate gpt-6-astra/high SOURCE and PREP reviews accepted with no findings. Fresh recovery in the new recovery directory verified117 selected files against metadata3175b8e59, clean.

## Observed result

Run `20261019-195127-804-38496-09a24c6d473e47d8aae2f02e4644757d-categories`: planned1/completed1/failed1, no skips. The child hit220s; the five-minute category did not time out. Runner exit1, total wall242.070s. Child42552 exited-1 after original-parent cleanup; EOF/join true, no cleanup/secondary failure, both owned fixture roots removed. Six DLL hashes still match. [Outcome](T227-probe-outcome.json), [TRX](T227-probe.trx), [summary](T227-probe-summary.json), [full log](T227-probe-dotnet-test.log), [raw archive](T227-source42-raw.zip).

| Actual observation | Seconds from reporter start |
| --- | ---: |
| Fixture returned | 0.720 |
| ProcessPlayerTurn entered | 18.597 |
| Author entered / returned | 83.683 / 85.641 |
| Inspection lease acquired | 85.815 |
| Terminal snapshot captured | 87.740 |
| Accepted-outcome validation checkpoint | 161.501 |

No QTE action entry or turn/helper return was observed before220s. The waiter and snapshot-capture path did make progress; the unknown interval is now after the actual accepted-outcome validation checkpoint and before the QTE action. The73.762s between capture and accepted-validation is a wall-time interval containing all intervening work, not an isolated measurement of one validator method. No deadlock or root cause is established by the last event alone. No unchanged retry was made. Windows5 remains FAIL_INCOMPLETE; this completed investigation result is not a sixth qualification identity. Linux remains UNRUN.

## T228 — causal count through existing fixture hooks

The existing fixture wires initial-read, after-read-attempt and before-mutation callbacks. Only the Windows source42 probe enabled bounded counters through those hooks; no production code or additional file reads were added. Initial and returned callbacks are distinct observations, not success counts. Source-capture and source-validated delimit intervals; snapshots are bounded and share the original stage record budget. Each interval retains at most1024 paths of length512, never emits paths or contents, and emits at eight read-count thresholds plus boundary/completion. Incomplete traces provide lower bounds.

Read-only source analysis identifies a concrete amplification mechanism: `ValidationService.AcceptedTurnAndInkFeathers` repeated lookups without prevalidated override call detached authority validation, whose `PendingTurnSnapshotAuthority.BuildAuthorityPayload` recomputes hashes of all rollback before-images. The console post-materialization call supplies no prevalidated context. Earlier accepted validation does install one. Browser-only read optimizations require browser authority and cannot be enabled by changing this fixture's identity.

Source25ef748ce, metadata779a8e2a8: counter RED9=7PASS/2expectedFAIL, then GREEN9/9PASS0skip22.577s. Fresh selected integration XML45.33s, changed XML diagnostics0, catalog814/11835 and probePlanOnly1. Independent SOURCE/PREP accepted. One causal-count run `20261019-200931-662-53708-059019b1da734236b481fd635758dfd7-categories` completed1/failed1 with child220s timeout, runner1, category timeout false, wall242.654s. Child49268 exit-1/EOF/join true, cleanup null, both owned roots removed. Three selected DLL hashes unchanged. [Outcome](T228-probe-outcome.json), [TRX](T228-probe.trx), [full trace](T228-probe-stages.json), [raw byte archive](T228-source42-raw.zip).

| Interval | Last recorded seconds | Initial callbacks | Returned callbacks | Rollback initial callbacks | Distinct rollback paths | Repeats | Before-mutation callbacks |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Before capture (closed interval) | 87.414 | 924 | 1154 | 472 | 59 | 413 | 191 |
| Capture to validated (closed interval) | 160.223 | 2081 | 2447 | 531 | 59 | 472 | 4 |
| Post-validated (incomplete lower bound) | 186.508 | 1988 | 2108 | 1740 | 59 | 1681 | 1 |

No path cap was reached. Repeated rollback read observations after the validation checkpoint are **confirmed**. The counters do not isolate one call site's time, prove all observations completed I/O, establish a deadlock, or prove this is the sole cause of220s. No QTE/turn return was observed. There is no justified test-fixture shortcut: setup finished around19s and subsequent work is actual production preparation/validation. Moving that work outside the child deadline, preauthoring the source, dropping validation or changing platform identity would alter the preserved contract.

Next proposed work is [spec amendment4](../../spec.md), requiring owner approval because it expands the production boundary beyond NativeLineageOwner. It seeks a fresh, immutable, operation-scoped validation proof with preserved dependency-change detection and explicit invalidation; blindly forwarding the earlier context is not an accepted fix. No production optimization has been implemented. T228 is a completed investigation once final evidence review is accepted; Windows5 remains FAIL_INCOMPLETE and Linux UNRUN.
