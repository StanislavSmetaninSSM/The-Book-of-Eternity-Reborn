# Tasks: Project methodology bridge

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1587
Spec: [spec.md](spec.md). Plan/checkpoint: [plan.md](plan.md).

## Setup

- [x] T001 Record approved scope, governance and native phase setup in `specs/1587-neutral-methodology-bridge/spec.md` and `plan.md`.
- [x] T002 Run read-only Spec Kit consistency analysis for `specs/1587-neutral-methodology-bridge/{spec,plan,tasks}.md` before skill copying.

## US1 — Discover the reviewed bridge

- [x] T003 [US1] Copy approved source bytes into `.agents/skills/spec-kit-superpowers-bridge/SKILL.md` and `agents/openai.yaml`.
- [x] T004 [US1] Validate `.agents/skills/spec-kit-superpowers-bridge/` and compare both upstream hashes; record evidence in `specs/1587-neutral-methodology-bridge/plan.md`.

## US2 — Durable and governed resumption

- [x] T005 [US2] Publish explicit changed paths plus WIP `specs/1587-neutral-methodology-bridge/plan.md` checkpoint; verify full remote branch SHA before long review.
- [x] T006 [US2] Recover listed changed/governance files by exact SHA from GitHub into a fresh directory; verify contents and record limited scope in `specs/1587-neutral-methodology-bridge/plan.md`.
- [ ] T007 [US2] Parent obtains separate independent review of the exact diff and verification selection, resolves validated findings and records acceptance evidence in `specs/1587-neutral-methodology-bridge/plan.md`.

## Dependencies and independent checks

Execute T001→T002→T003→T004→T005→T006→T007; no parallel implementation is useful. US1 is the bounded deliverable, independently checked by validator and 2/2 hashes. US2 requires remote exact-SHA source evidence and separate review. No game tests or new prose-mirroring tests. Pending checks stay unchecked; publication is not acceptance.

