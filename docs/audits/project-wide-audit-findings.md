# Project-Wide Audit Findings

Tracking epic: `epic: project-wide-audit`

Task order: `docs/audits/project-wide-audit-task-order.md`

## Rules For This File

- Record every project-wide audit finding here before implementing a fix.
- If a finding requires code, test, prompt, documentation, example, contract, or UI changes, create or link a dedicated GitHub issue before making those changes.
- If a finding is too broad for one fix, split it into smaller GitHub issues and link all of them in the `Issue` column.
- If an audit slice finds no actionable issue, add an audit checkpoint instead of inventing a finding.
- Keep implementation status factual: do not mark a finding `fixed` without verification evidence.

## Finding ID Format

- Use `PWA-001`, `PWA-002`, and so on for project-wide audit findings.
- Use the specialized audit ledger id if a finding belongs entirely to an existing specialized audit file.
- Cross-link specialized ledgers when they already cover the area.

## Severity

- `P1` - build break, data loss, security/safety risk, invalid accepted-turn state, or blocker for normal play.
- `P2` - functional correctness bug, contract drift, validation/runtime mismatch, or serious UX confusion.
- `P3` - documentation gap, test gap, localization inconsistency, minor UI issue, or low-risk maintainability problem.

## Status

- `open` - finding recorded, fix not started.
- `split` - finding moved into one or more dedicated GitHub issues.
- `fixed` - implementation completed and verification evidence recorded.
- `wontfix` - explicitly accepted as not worth changing, with rationale.
- `checkpoint` - no discrete finding in the audited scope.

## Finding Template

| ID | Status | Issue | Area | Severity | Summary | Source / Evidence | Expected Behavior | Proposed Fix | Verification |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PWA-000 | open | #NNN | Runtime / UI / Prompt / Tests | P2 | Short defect summary. | Exact files, commands, examples, or mental experiment. | What should happen. | Concrete fix direction or split issue. | Test/manual command to prove the fix. |

## Findings

| ID | Status | Issue | Area | Severity | Summary | Source / Evidence | Expected Behavior | Proposed Fix | Verification |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Audit Checkpoints

| Date | Issue | Scope | Result | Verification |
| --- | --- | --- | --- | --- |
| 2026-05-21 | #626 | Created project-wide audit ledger and task closure order for issues #626-#636. | Ledger, taxonomy, severity/status rules, required finding fields, and checkpoint format are defined. | `git diff --check -- docs/audits/project-wide-audit-findings.md docs/audits/project-wide-audit-task-order.md` |

## Related Specialized Audit Ledgers

- `docs/audits/afterlife-chaos-shining-audit-findings.md` - existing Chaos Sea / Shining Abode audit findings and checkpoints.
