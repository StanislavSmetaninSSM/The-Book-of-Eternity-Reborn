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

One probe was started after these checks. Its final outcome and interpretation will be recorded from the actual raw trace, TRX, summary and original-owner cleanup evidence; an absent event alone will not be called proof of a blocked await.
