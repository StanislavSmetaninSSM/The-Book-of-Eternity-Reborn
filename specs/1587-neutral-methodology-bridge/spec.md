# Feature Specification: Project methodology bridge

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1587
Date: 2026-11-03. Branch: `process/1587-neutral-methodology-bridge`.
Status: approved distribution scope; independent acceptance pending.

## Requested result and approval

Make the reviewed application-independent Spec Kit / Superpowers bridge discoverable in this project's skills. The owner explicitly authorized synchronizing the necessary local, Kimi and GitHub copies on 2026-11-03. This specification records that concrete distribution decision; it adds no new approval gate. The parent distribution feature is 002; this issue-linked feature records the book repository's bounded contribution.

## User Scenarios & Testing

### US1 — Discover the shared methodology (P1)

Given a project session reading `.agents/skills/`, when it loads the bridge, it receives the same reviewed methodology and UI metadata as the approved upstream distribution. Validate both file hashes and the skill validator.

### US2 — Resume without losing project governance (P1)

Given a later session, when it reads this issue, feature and checkpoint, it can identify the exact source, completed checks and remaining independent review/recovery work. The bridge preserves prior concrete approvals, canonical artifacts and Superpowers execution/review/verification.

## Requirements

- FR-001: Add exactly `.agents/skills/spec-kit-superpowers-bridge/SKILL.md` and `agents/openai.yaml` beneath that directory with bytes identical to the reviewed upstream distribution.
- FR-002: Preserve existing skills, AGENTS.md, constitution, application configuration and project runtime/GM contracts. The shared bridge must not select or install agent applications, set models, or import application-specific delegation.
- FR-003: Track this repository contribution through issue-linked spec/plan/tasks, with actual planning, consistency analysis and evidence.
- FR-004: Validate the skill and both hashes; have a separate reviewer inspect the exact diff, original requirements and verification selection.
- FR-005: Publish a WIP checkpoint before long review; verify the branch SHA and recover the listed new files plus required instructions from GitHub at that SHA into a fresh directory before claiming limited source recovery.

## Boundaries and preserved contracts

Contract scope: developer documentation and skill metadata only. No game runtime, player UI, GM prompts/examples, state/schema, save compatibility, test infrastructure, category catalog, CI, protection rules, or unrelated branches/skills change. Independent review and required GitHub code-owner approval remain gates. No merge or issue closure is authorized by this block.

Edge cases: stale project feature pins must not redirect scripts into another task; use explicit per-command feature environment overrides. A failed partial clone must not publish staged deletions. API recovery proves only the downloaded files, not a full checkout or runtime build.

## Success Criteria

- SC-001: Both added skill files match the approved upstream SHA-256 values and pass skill validation.
- SC-002: The remote diff contains only this skill directory and this feature's records, with no preserved-contract edits.
- SC-003: Independent review and exact-SHA source recovery are recorded before final acceptance; unmet gates remain explicit.

## Decisions

No unresolved material requirements. Existing project governance remains unchanged. Source distribution validation and behavioral scenario evidence may be supplied by the parent task; this repository block must not misrepresent them as locally executed Kimi or game tests.

