# #1559 independent ARCH review

**CHANGES REQUIRED: one narrow architecture amendment (1559-A01) before G1.** Reviewed published `3930bd49da3b173df6e2ba2acedfced4c628b9ce`, base `a82b3d8439f76f83baade6df8516e78eea86e546`. No runtime was executed and no repository file changed.

The proposed direction is viable: retain the dedicated helper and its lease, prepare existing script policy against a staged view, then call the existing multi-member publisher once. Its ordered journal supports all-before/whole-after recovery. The real daemon snapshot path acquires publication-read quiescence and settles trusted-local recovery before returning bytes. A second journal, new service/UI or changed gameplay policy is unnecessary.

**A01 — operational records currently fall under game rollback.** [Plan lines40/48](/workspace/The-Book-of-Eternity-Reborn-1559/specs/1559-relay-long-sessions/plan.md:40) proposes `game_state/control/gm_prompt_delivery.json` and `gm_relay_apply_receipt.json`. [Engine enumeration](/workspace/The-Book-of-Eternity-Reborn-1559/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1941) tracks both; [real restore](/workspace/The-Book-of-Eternity-Reborn-1559/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:2405) deletes new tracked files or restores old images. [Live snapshot preparation](/workspace/The-Book-of-Eternity-Reborn-1559/BookOfEternityClient/Services/LiveTurnPreparationService.cs:37) likewise has no exclusion. A rejected turn/repair before relay ACK can therefore erase or rewind the supposed durable dispatch/application evidence. This is an identified design integration gap, not an observed runtime failure.

Amend the existing plan/inventory with the two exact operational lifetimes: preserve them across ordinary snapshot rollback, define bounded replacement/retirement against original request/generation, and pin the real snapshot/restore/read owners. Use narrow path exclusions or an equivalently safe authorized location; no broad control-directory exemption. Add actual rollback with ordinary neighbors, receipt/lost-ACK after rejection/repair, and same-binding restart controls. Missing evidence must never grant replay. Only this amendment needs targeted re-review.

Mandatory later gates remain:

- Batch SOURCE must keep all physical witnesses distinct from staged values, preserve normalizers/realm/client-owned/terminal policy, and prove real readers cannot consume terminal before the journal decision. Commit/cleanup debt, third-member interruption and lost ACK need separate outcomes. Receipt is publication evidence, never client acceptance.
- Sequence tests must cover same-identity NotReady retry, concurrent producers/gaps, expired queries, duplicates across compaction, restart and late Stop/task/CTS settlement. No closed-queue reopening or old PID/run authority.
- Proposed4/16/64/256MiB defaults need measured admission budgets including old target before-images, parser/base64 copies, journal/scratch, logs and unresolved queue data. Transfer size alone is insufficient. Keep bounded chunks/control frames and concurrency.
- Exact neutral framing must address existing CRLF/CR normalization and128-row/65536-column view bounds, including final newlines/Unicode; do not enlarge Mini or treat a rolling tail as complete proof.
- The descriptor is an optional locator using existing computer tools. Actual shell-less exchange must be recorded independently of operator startup; a worker Python fixture is insufficient. Current client story/history acceptance must be observed separately from helper/terminal or daemon trajectory labels.
- Concrete method/row discovery and resource evidence remain later gates. No guessed count is accepted. Linux and unavailable native Windows stay separate; no unchanged historical-family replay.

Independently verified17candidate GitHub files/all14metadata changes and78baseline source pins; six additional actual dependencies are recorded in support. Product/tests/catalog/selection remain unchanged. Root may authorize the narrow A01 metadata correction, then tests-only authoring after its targeted acceptance; no production/runtime release follows this report.

Support: [static checks](/tmp/gm1559-architecture-independent-check-20261017.json), SHA256 `90ed99d075c56c806183a87328c39c412064774fd45b7a2dd811e9f024b0c413`.
