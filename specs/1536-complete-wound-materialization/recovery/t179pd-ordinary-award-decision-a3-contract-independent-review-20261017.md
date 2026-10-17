# A3 / AP06 independent necessity and short-contract review

**Verdict: NECESSITY NOT ESTABLISHED — do not select standalone A3 for implementation now.** One material scope finding, A3-N01. The proposed ordinary semantics are internally consistent, but that is insufficient under the user's latest condition: another kernel only when needed for main wound materialization / ordinary continuation, not refactoring for itself. This is a bounded necessity decision, not a claim that A3 can never be useful.

Reviewed carrier `387c4f481f5722aa3318af49033b1401ca49aaf7`, accepted A2 base `9416298b144c836aaa88e98e4545ea23626929f3`. Reviewer remained repository-read-only; no build, tests, application runtime, CI, Windows artifacts, or historical family/base replay.

## A3-N01 — proposed extraction does not establish the required current continuation dependency

**Location:** `specs/1536-complete-wound-materialization/design/ordinary-award-decision-a3-contract.md:7` and the new `plan.md:3` necessity assertion.

**Scenario and consequence:** implement the two proposed helpers and their 44-case selection while all actual Capturing/HOT consumers remain excluded. The only immediate consumer is the existing ordinary `CheckLevelUpAsync`, whose initializer and deduplication already execute. The packet identifies no missing ordinary capability or current defect that the extraction fixes. It instead promises a future fixed-source interpreter. This does not substantiate the user's condition for spending the next implementation block on a kernel.

The cited main-plan P2 fence is specifically T188/B10, `lawful-active-joint-tail-b-implementation-plan.md:587–597`: post-QTE terminal COMMIT, a B02-like automatic slot, then B03 projection. B02 at lines 490–498 requires strict existing watermark ≥1, nonnegative balance, checked arithmetic and no initialization. Existing `Services/CharacteristicsService.MortalJointAutomatic.cs:33–45,77–83` already calculates that strict deduplication/award data, refusing absent initialization and performing checked arithmetic. It does not use the ordinary computed/unspent heuristic or `_lastKnownLevel`. Thus that P2 reference does not establish a dependency on the proposed legacy ordinary helpers.

The older engineering design does name both APIs at lines 101–106 and AP06 at 205, but line 79 also calls for an actual Capturing consumer of the same policies, rather than a future-reader promise. The current A3 packet excludes that consumer. The real source42→ordinary progression→CheckLevelUp path establishes affected-regression scope if this body changes; it does not by itself establish why changing it is necessary now. No accepted A1/A2 decision is reopened by this finding.

**Disposition:** retain the proposal as deferred analysis and do not release tests/preparation/production for this standalone block. Reconsider only with a concrete main-path consumer or diagnosed behavior gap showing why these exact ordinary policies are needed, while preserving the distinction from strict joint rules. Under the user's stated fallback, proceed to the separately authorized #1557 rules-delivery work instead; this report grants no extra #1557 technical scope or runtime authority.

## Technical contract observations, subordinate to necessity

No material semantic error was found in the proposed two-helper contract itself. It correctly preserves both awaited initialization reads in computed→unspent order, even when the first value satisfies the predicate; Mark completion precedes the unnormalized owner-local watermark assignment and subsequent descriptor call. Persisted Mark normalization to at least 1 remains distinct from that local value.

The proposed descriptor keeps `known = max(lastKnown, max(1, watermark))`, uses `current > known` for ShouldAward, and evaluates difference×5 only inside that branch. Its stated Int32 wrap constants are consistent; a negative wrapped TotalPoints must not suppress the award branch. Existing product csproj has no overflow-check override, the owner has no checked block, and repository/ancestor Directory.Build props/targets/rsp checks found no override. This was static configuration inspection, not a fresh compiler evaluation. The strict calculator's explicit checked policy must remain separate. Existing AddStatPoints also performs balance addition and projection; extraction must not move or replace that mutation sequence.

The proposed real initialization and +5/distribution boundary witnesses are causally stronger than no-award-only witnesses: owner assertions, bytes and strict cleanup must precede missing-helper reflection. The 44 A3 + 2 separate affected Late2 cases are only a finite proposal, not accepted concrete fixture SOURCE or executed EVIDENCE. If A3 were later justified, the changed source42 owner would require the new Late2 gate; unchanged A1/A2 reader families would not require replay merely for assembly changes.

## Publication and limits

Independently verified clean carrier/remote branch, unchanged main `d0241e71e349fbe2020e4a41b6be7c627c81cbb4`, and the complete new GitHub recovery delta: all 8 recovered files / all 4 changed paths, exact stored bytes, SHA256, Git blob identities and modes, no deletions. Product, tests, catalog/selection, scripts and workflow scopes are unchanged from accepted A2. The supplied receipt is `/tmp/a3-contract-delta-github-387c4f48-20261017.json` (SHA256 `988c35ffa60dfd72112ee6ddfce45297b05f82d3854f3f0a322bb22deece777c`); independent binding is `/tmp/a3-contract-independent-binding-20261017.json` (SHA256 `eca13c2bfe93682e8e7f7a1023b68af1762d2796802219c599ab673e2d1523f4`). No full-base or accepted raw-family verification was repeated.

**Gate:** no A3 implementation/runtime release. A1/A2 accepted results and their original proof limits remain intact. No HOT/M3/F1/T197/QTE/Windows/CI/full-stage qualification, merge, main update, or issue closure is claimed. The only remaining blocker for this proposed block is substantiating necessity; no tool or recovery failure blocks the review.
