# Ordinary award-level A2 — bounded implementation contract

Task [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), branch1536-wound-b2-cloud-20261012, base `65fcce707ce63be31e2a1fb462f89998bc39bfd1`. Latest user authorizes autonomous contract→independent review→causal TDD→minimal implementation→affected checks→remote checkpoint. This is implementation preparation, not a new DESIGN-only discussion STOP. Product/tests/catalog/selection remain unchanged until independent contract acceptance and root release. [Approved3c API contract](../recovery/t179pd-automatic-player-time-policy-engineering-design-20261016.md#concrete-shared-api-and-integration-contract) remains the basis; A1 is complete and is not replayed.

## Exact source seam and retained owner

Add only `internal static int? ReadAwardExperienceLevel(JsonElement)` and `ReadAwardStatusLevel(JsonElement)` in existing `CharacteristicsService.LevelPolicies.cs`, separate from training names. Experience: exact case-sensitive numeric `level` first, then numeric `playerLevel` only if level is absent/wrong-kind; status: exact numeric `level` only. No matching numeric field returns null. Existing TryGetProperty/GetInt32 semantics remain: duplicate last property, unmodified zero/negative/Int32 endpoints, FormatException on numeric fraction/overflow before alias fallback, InvalidOperationException on a non-object root. Helpers perform no IO, parsing, writes, clock/GUID/UI/watermark/arithmetic or `_lastKnownLevel` mutation.

Only replace the two parsed leaves at `Core/GameEngine/GameEngine.TurnLifecycle.cs:2552–2582`; immediately assign `currentLevel` **only when nullable result has a value**. The real wrapper keeps default1; await experience read then JsonDocument.Parse for every nonnull string (including empty). Only currentLevel<=1 reads/parses status; a null status helper retains an experience0/negative value, never resets1. No soul read is added. Preserve the single enclosing catch(:2620): ordinary read/JSON/nonobject/conversion faults abort the owning body and log, with no next source/watermark/UI/lastKnown update; CoordinatedStatePublicationUncertainException escapes. Await positions, document lifetimes, marker/init reads and writes, award arithmetic/UI, `_lastKnownLevel` updates and callers remain untouched. Watermark/award arithmetic extraction is explicitly later work; no strict joint/legacy source redirect or HOT integration.

## Causal tests and finite selection proposal

Use actual constructed GameEngine via existing integration CreateGameEngine(:10916), real FileSystemManager/CharacteristicsService and real CheckLevelUpAsync invocation. No uninitialized engine, substitute owner, reflected policy stub or caller-supplied fake authority. Each mutable case owns a fresh root; physical probes use ResolvePath and existing BOM encoding, guard supplied presence/exact preimages **before** entry, then compare after bytes. Observe actual canonical read attempts only after setup. Existing watermark10 prevents award/UI while preserving normal source resolution; real initial `_lastKnownLevel`1 and final value/read-order witness are checked. Helpers are invoked by reflection only to test the absent named seam/result after the real owner witness; reflection does not replace the owner.

| Category proposal | Cases | Meaning |
|---|---:|---|
| fr063-award-level-policy-boundary | 2 | Experience exp2/status7: real1→2, skip status. Status exp1/status2: real1→2 through status. Owner/read/source-byte/noUI/no-write witness precedes missing-helper assertion for each matching named leaf. First intended RED is only these2. |
| fr063-award-level-policy | 33 |13 experience priority/value tuples +10 status tuples +4 conversion failures +6 nonobject failures; new award names, pure zero-root cleanup N/A. |
| fr063-award-level-owner | 20 |18 finite source rows below +2 real IOException read faults (experience/status), outer catch/log abort, exact read order, source/points bytes, no input or mutation. |
| fr063-award-level-existing | 2 | Two thin A2 fact entries reuse unchanged bodies: actual restart guard(:132) is wrapped in strict CleanupOwnedFixture (its old class Dispose is best-effort and cannot prove strict cleanup); exact publication-uncertainty level_mark body (ProgressionOutcome.cs:19/74) already owns strict CleanupOwnedFixture and gets no duplicate outer cleanup receipt. No full4mode family replay; real queued input/real cut/original assertions remain. |
| fr063-joint-late-completion | 2 separate | Actually affected through genuine source42 prelude, described below; only one new FIRST after minimal production/fresh preparation/accepted SOURCE, then separate EVIDENCE before A2 GREEN. Not before product edits/causal RED. |

A2 proposal is **57 cases (2+33+20+2)**; protected Late2 is separately2, total planned59/5descriptors. Expected A2 strict owned roots24 =boundary2+ordinary20+restart1+uncertainty1; pure33 owns0/cleanupN-A, protectedLate2 roots remain separately2. New categories use5m/exclusive/no frontend; existing protected budget5m unchanged. Use singleton Category actuals only, never an aggregate actual. Preserve counts/first native exit/TRX/raw argv and source/compiled/four-DLL pins/strict cleanup; stop first unexpected, no retry. Fresh successful BOTH/catalog and exact PlanOnly0 precede each changed compiled test/product source and concrete SOURCE/FIXTURE/SELECTION gate. Tests-only first causal RED must be separately qualified/preserved before production changes. After product extraction, fresh preparation/SOURCE→one FIRST protectedLate2→separate EVIDENCE→individually released A2 owners→final independent review/checkpoint. Passing unchanged source requires no count-only replay.

The18 ordinary rows are finite (`e` experience, `s` status; level values are JSON objects, M=missing):

| e | s | Expected owner branch |
|---|---|---|
| level3/playerLevel9 | level4 |3; skip status |
| level string3/playerLevel7 | level4 |7; skip status |
| level1 | level4 |4; read status |
| level0 | level4 |4; read status |
| level-2 | level4 |4; read status |
| {} | level4 |4; read status |
| M | M |default1 |
| M | playerLevel7 |default1; no status alias |
| level0 | {} |retain0; resulting lastKnown1 |
| level-2 | level string7 |retain-2; resulting lastKnown1 |
| empty string | level4 |parse abort before status/marker |
| malformed `{` | level4 |parse abort before status/marker |
| [] | level4 |nonobject abort before status/marker |
| level2.5/playerLevel7 | level4 |conversion abort, no alias/status/marker |
| level2147483648/playerLevel7 | level4 |conversion abort, no alias/status/marker |
| {} | level3.5 |conversion abort before marker |
| {} | malformed |parse abort before marker |
| {} | JSON null |nonobject abort before marker |

Nonpositive current values are pure-helper/data/source assertions; owner lastKnown remains max(1,current) and does not independently expose a negative scalar. No fabricated current-level diagnostic is used. Two real read faults exercise the same enclosing ordinary catch; the existing real level_mark coordinated-publication cut proves its exception exemption. The original restart control proves real awarding/distribution/restart deduplication; no arithmetic change is inferred.

## Actual impact and recovery

Protected final joint43 branch bypasses ordinary progression at TurnLifecycle:952/1040 through `TryReconcileMortalJointStoryTurnAsync`; unchanged `MortalJointInactiveTail` strict OptionalPositiveLevel is not redirected. **Nevertheless both protected controls construct their genuine source42 first:** JointCompletion.cs:35→RunActualQteStorySourceAsync/QteStoryAppend.cs:361→real ProcessPlayerTurn/FinalizePlayerTurnAsync(:1963)→ProcessMortalProgressionAfterAcceptedTurnAsync(:2456/2463)→changed CheckLevelUpAsync. This concrete prelude makes exactly one new protected Late2 affected after product edits; mere common partial-file/binary adjacency is not its reason. A1 pure/training/ordinary source is untouched and its accepted52 plus original465/e247 Late2 remain historical, not new A2 proof. No worker/HOT/M3/F1/T197/QTE/platform/CI/security/ZIP or #1557 work is selected.

[Exact dependency recovery](../recovery/t179pd-award-level-a2-contract-dependency-recovery-20261017.json) pins21 named current governance/source/fixture/runner/catalog inputs from exact GitHub65fc into a new directory, including GameEngine owner/factory, resolver/encoding, real publication cut and protected source prelude. This supplements retained full7a +13complete changed deltas covering54629current paths; no new full clone/base-byte rehash/runtime is performed. This contract carrier must be non-force published/read back and every newly changed path recovered exact before independent contract review. GM no-update rationale: internal behavior-preserving leaf extraction emits no new capability/schema/validation/UI contract. All prior broader/full-stage/Windows/CI/ZIP/settings holds remain.
