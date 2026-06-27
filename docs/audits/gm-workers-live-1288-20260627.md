# GM Workers Live #1288 - 2026-06-27

Issue: #1288 / #1249  
Spec: `specs/1285-rlm-gm-harness/`

## Run

Run root: `C:\Temp\boe-gm-workers-live-20260627-221026`

Setup:
- Disposable copy of `FileSystemExample/game_session`.
- `system_guardians` copied next to `game_session`.
- Console client launched through Agent Console using prebuilt `BookOfEternityClient.exe`.
- Main bridge command: `codex --dangerously-bypass-approvals-and-sandbox`.
- Hidden worker command: `codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -`.

## Findings

### Build/live process conflict

The first run attempted to start the client with `dotnet run`. A stale live
client from the previous Chaos Sea test held
`BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.exe`, so the new
run failed before Agent Console startup.

Harness conclusion:
- Live runbooks should start the already built executable after prerequisite
  build/tests.
- Teardown must stop client/daemon/bridge before any build or test command.

### Worker proposal preserved but dispatch reported failure

Two proposal-only worker dispatches were sent through `dispatchworkertask`:

- `narrative-draft`: worker task `worker_task_narrative_draft_f3c8b18f61c14a86b48f850aa042e868`.
- `analysis`: worker task `worker_task_analysis_3deb41605b444ac5a98858c805e6054a`.

Both Codex workers wrote proposal-shaped `worker-proposal-v1` JSON under
`worker_proposals/inbox/<taskId>/proposal.json`, but the CLI runner timed out
after 120 seconds. The bridge response reported `worker-failed` with empty
`proposalId`, even though the useful proposal was already present.

Harness conclusion:
- A valid proposal written before worker timeout/nonzero exit should be treated
  as `proposal-received` and returned to the main GM.
- The timeout/nonzero exit remains diagnostic evidence, but it should not hide a
  valid proposal from the proposal inbox or dispatch result.

## Changes Made

- `GmWorkerBridgePool` now checks for an existing proposal inbox file before
  recording terminal worker failure/timeout.
- If the proposal validates, it is saved and returned even when the worker CLI
  exits nonzero or keeps running until timeout.
- If no proposal exists, the previous failure/timeout behavior remains.
- Worker guide and live runbook were updated with this behavior.

## Second Run

Run root: `C:\Temp\boe-gm-workers-live-20260627-223852`

After the proposal-preservation change, the same live dispatch path reached
proposal validation instead of reporting the CLI timeout as the primary failure.
The new bridge response was `worker-failed` with fallback reason
`summary is required.` and the audit recorded `proposal-rejected`.

Harness conclusion:
- The preservation fix changed the failure mode correctly: the proposal file was
  found and validated.
- The next harness gap is the runner prompt. It names `worker-proposal-v1` but
  did not provide a self-contained required-field skeleton, so Codex workers
  guessed the shape and omitted `summary`.
- The runner prompt should include the exact required JSON shape and required
  field rules before the next live test.

## Next Test

Repeat the same two `dispatchworkertask` calls after rebuilding the client. The
expected result is `workerDispatch.outcome=Completed` and a non-empty
`proposalId` for both proposal-only tasks. If the worker writes malformed JSON
or omits required fields after the runner schema hardening, keep the rejection
as a useful contract failure and record the missing field.

## Third Run

Run root: `C:\Temp\boe-gm-workers-live-20260627-230229`

After hardening the runner prompt with a self-contained `worker-proposal-v1`
JSON skeleton, the same proposal-only dispatch path completed:

- `narrative-draft`: `workerDispatch.outcome=completed`,
  `proposalId=worker_proposal_narrative_draft_232ab362d0374d7080bfe91127abe51f`.
- `analysis`: `workerDispatch.outcome=completed`,
  `proposalId=worker_proposal_analysis_3daf201919694091a8054a5537c5eba2`.

Audit evidence:
- `gm_worker_audit.jsonl` contains `task-dispatched` and `proposal-received`
  for both tasks.
- Saved proposal inbox diagnostics show readable `summary`, `createdAtUtc`,
  `draftText` or `findings`, empty `changedFiles`, and `review-only` state.

Harness conclusion:
- T047 is verified: a proposal written before worker terminal quirks is
  preserved and returned through the proposal inbox path.
- T048 is verified: Codex workers now produce validator-compatible proposal
  fields from the runner prompt without reading implementation source.
- The main visible Codex bridge still displays ordinary Codex CLI startup/update
  UI noise, but proposal-only worker dispatch does not depend on the main GM
  prompt state for this path.
