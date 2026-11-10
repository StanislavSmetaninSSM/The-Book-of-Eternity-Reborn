# Windows worker2 availability — narrow independent addendum

Decision: **the existing conditional source grant permits one guarded release-file push under unchanged repository/workflow settings. No additional routine user authorization is required for that bounded attempt within the current delegated 3b scope.** The new facts do not establish an authority blocker or hosted availability. This addendum neither broadens the two-case grant nor authorizes changing any CI setting.

Reviewed source remains `8e0a5e40d28b103fa509f40dd7f1a5b293f6545c`; the dedicated release file remains absent. During documentary preservation, observed HEAD advanced to clean `b3b12801e3c4f098fc0c035005c779f38fabf27a`, changing only task documents/recovery artifacts. Product/tests/workflow remain unchanged. The initial read-only finalizer stopped on its obsolete HEAD-equality precondition before report creation; a new finalizer pins the original source and verifies this documentary-only delta. This was not a runtime failure. The immutable SOURCE pair is unchanged:

- MD SHA256 `4b296c8b0318017e91442ad0df6a0451bc7a64ae15963ec2991ae5c5ed7003ae`.
- JSON SHA256 `fd5dbe79fd801210b91c17965b4b0da21020fbcc7dd7fdde13583a1ee802ab48`.

The separate reviewer retains coordinator-confirmed `gpt-6-astra` / `xhigh`; backend telemetry is unavailable. This follow-up used only local documentary/source/Git reads and new `/tmp` report writes. No network, build, discovery, test, runtime, repository mutation or delegation occurred.

## Evidence and scope

Root's saved read-only API receipt at `/tmp/t179pd-hot-worker-windows-actions-availability-20261016.json`, SHA256 `a8d1171e2a6d4f68cbd424460cd98447295350e66579bf5c3acae3f9796c93d0`, contains three exact argv/native/stdout/stderr/time records around 2026-10-16 22:55:30–31 UTC. I independently verified its bytes, parsed contents and historical source projection; I did not repeat those network requests.

The workflows GET succeeded and reported only `.NET CI` (280957256) and `Portable load selected Linux qualification` (374267479), both `disabled_manually`. The repository Actions-permissions GET returned native 1 / HTTP 403 `Resource not accessible by integration`. The exact task-branch runs GET succeeded with `total_count=0`. Thus there was no listed new worker workflow or observed branch run at that snapshot. Neither the two disabled workflow states nor the permissions denial proves that repository Actions are globally disabled, enabled, or currently allow the new workflow/actions.

Pinned `specs/1553-portable-local-storage/final-readiness.md:60–76` and `tasks.md:567–571` record the 2026-10-08 owner decision for PR1555: disable both existing workflows after a hosted failure and unavailable logs/artifacts, preserve failure evidence, and supersede that integration's hosted-green gate. The explicit subjects are those two workflows and that integration. The examined record does not state a blanket prohibition on every later bounded CI run. It also supplies no permission to re-enable them, alter permissions/allowlists, bypass transfer denials or infer a hosted PASS. The current #1536 delegation and separately reviewed new workflow supply the bounded work scope; historical settings are not treated as authorization.

## Exact boundary before the release push

Preserve this addendum, the original SOURCE pair and the availability receipt together in the verified/recovered acceptance checkpoint. Then the already-reviewed **single-parent, dedicated-file-only, first-addition push** may test existing availability. If the platform admits it under unchanged settings, the existing workflow may perform its reviewed fresh Windows preparation and only `fr063-hot-worker-ready-disposition` — **two cases, five minutes, FIRST_NATIVE_WINDOWS_ONLY**. Every original review/hash/source/attempt guard remains required. No source change or expanded run is authorized by this addendum.

**Do not enable either old workflow, enable the new workflow through a settings/API action, change repository Actions settings/allowlists/permissions, alter main/protection, approve a new authorization requirement, dispatch/retry the workflow, or work around a denied artifact/network route to force execution.** The one reviewed release-file push itself is the entire permitted availability attempt; there is no fallback trigger.

If the run is not admitted, remains unavailable, encounters an authorization/access barrier, or necessary evidence cannot be retrieved, retain the exact push/readback and available run/status/error evidence and **STOP dependent runtime qualification**. Harmless status reads may establish what happened; absence of a run at a single instant must not be relabeled a test failure or proof of permanent global disablement. Record NOT_RUN/unavailable/unknown as supported. Any actual first failure remains frozen without retry. A further change of settings or authorization would be a separate decision, not covered by this source grant.

If the two cases do execute, source feasibility and successful scheduling still do not establish their result. The complete native packet, strict Windows cleanup and exact source/binary/semantic evidence require the separate EVIDENCE and final bounded integration reviews already specified. Prior 13 PASS and Linux 2 FAIL remain unchanged; all full 3b/S/B/M3/F1/T197, general Windows/native, DOM/visual and prior limits remain open.

**FINALSTOP for this reviewer.** No actual execution, workflow enablement or repository-setting change was performed or certified by this addendum.
