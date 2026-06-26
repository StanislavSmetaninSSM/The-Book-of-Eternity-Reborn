# Contract: Recursive Worker Delegation

## Purpose

Model RLM-style recursive subcalls through hidden/background GM worker tasks while preserving main GM authority and validation gates.

## Required task packet fields

- `taskId`
- `taskType`
- `workerRole`
- `contextRefs` with hashes
- `allowedSurfaces`
- `proposalSchema`
- `timeoutSeconds`
- `acceptanceCriteria`
- `forbiddenActions`

## Required proposal rules

- Proposal-only by default.
- No direct canonical game_session mutations.
- File content refs must remain under the worker proposal directory.
- The main GM or harness apply gate decides accept/reject.
- Validation must run where the proposal affects state or repair.

## Required audit rules

- Dispatch, timeout, proposal receipt, rejection, apply, validation, and rollback-sensitive outcomes must be represented in the GM trajectory ledger.

## Non-goals

- Implement every specialist worker role in this feature.
- Show worker windows to the player.
- Let workers bypass validators.
